namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Config
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes

/// `fsgg-sdd scaffold` handler. The pure `plan`/`update` boundary produces the
/// effects (`RunProcess`, skeleton writes, the provenance `WriteFile`); the edge
/// interpreter performs the real process + filesystem I/O (Constitution V). Staging
/// is recomputed from the model each `nextLifecycleEffects` tick:
///   1. resolve `--provider` + validate version/params/collision (pure);
///   2. on a valid provider, plan `dotnet new install` + the init skeleton +
///      `dotnet new <templateId>`;
///   3. once the create process is interpreted, diff produced paths, guard the SDD
///      trees, and plan the deterministic `.fsgg/scaffold-provenance.json` write.
[<AutoOpen>]
module internal HandlersScaffold =
    module ConfigModule = FS.GG.SDD.Artifacts.Config
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    type ScaffoldOutcome =
        | ProviderSucceeded
        | ProviderSucceededEmpty
        | ProviderNotRun
        | ProviderFailed

    let scaffoldOutcomeValue outcome =
        match outcome with
        | ProviderSucceeded -> "providerSucceeded"
        | ProviderSucceededEmpty -> "providerSucceededEmpty"
        | ProviderNotRun -> "providerNotRun"
        | ProviderFailed -> "providerFailed"

    let supportedContractRange = ">=1.0.0 <2.0.0"

    let contractMajor (version: string) =
        match (Option.ofObj version |> Option.defaultValue "").Trim().Trim('"').Split('.') with
        | parts when parts.Length >= 1 ->
            match Int32.TryParse parts.[0] with
            | true, value -> Some value
            | _ -> None
        | _ -> None

    let isSupportedContract version = contractMajor version = Some 1

    // The SDD-owned trees a compliant provider must never write into (FR-011), and
    // the paths the SDD skeleton/provenance own (excluded from collision + diff).
    let isSddTree (path: string) =
        let p = normalizeRelativePath path
        p.StartsWith(".fsgg/", StringComparison.Ordinal)
        || p.StartsWith("work/", StringComparison.Ordinal)
        || p.StartsWith("readiness/", StringComparison.Ordinal)

    let isSddOwned (path: string) =
        let p = normalizeRelativePath path
        isSddTree p || p = "AGENTS.md" || p = "CLAUDE.md"

    let parseListing (text: string) =
        (Option.ofObj text |> Option.defaultValue "").Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map normalizeRelativePath
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Set.ofArray

    let beforePaths model = parseListing (directoryListing "" model)

    // Pre-existing, non-SDD content the provider would materialize over (FR-010).
    let collisionPaths model =
        beforePaths model
        |> Set.filter (isSddOwned >> not)
        |> Set.toList
        |> List.sort

    let skeletonFiles request =
        initEffects request
        |> List.choose (function
            | WriteFile(path, _, _) -> Some(normalizeRelativePath path)
            | _ -> None)
        |> Set.ofList

    let effectiveParameters (descriptor: ProviderDescriptor) (request: CommandRequest) =
        let defaults =
            descriptor.Parameters
            |> List.choose (fun spec -> spec.Default |> Option.map (fun value -> spec.Key, value))
            |> Map.ofList

        request.Parameters
        |> List.fold (fun (state: Map<string, string>) (key, value) -> Map.add key value state) defaults

    let missingRequiredParameters (descriptor: ProviderDescriptor) (effective: Map<string, string>) =
        descriptor.Parameters
        |> List.filter (fun spec -> spec.Required && not (Map.containsKey spec.Key effective))
        |> List.map (fun spec -> spec.Key)

    let resolveDescriptors model =
        match snapshot ".fsgg/providers.yml" model with
        | Some registrySnapshot ->
            match ConfigModule.parseProviderRegistry registrySnapshot with
            | Ok descriptors -> descriptors
            | Error _ -> []
        | None -> []

    type ScaffoldResolution =
        | ScaffoldBlocked of Diagnostic list * ScaffoldSummary
        | ScaffoldProceed of ProviderDescriptor * Map<string, string>

    let notRunSummary providerName providerVersion hint : ScaffoldSummary =
        { ProviderName = providerName
          ProviderContractVersion = providerVersion
          Outcome = scaffoldOutcomeValue ProviderNotRun
          SkeletonCreated = false
          ProviderInvoked = false
          ProducedPathCount = 0
          ProducedPaths = []
          NextActionHint = hint }

    let resolveScaffold model =
        let request = model.Request

        match request.Provider with
        | None ->
            ScaffoldBlocked(
                [ DiagnosticsModule.scaffoldProviderMissing () ],
                notRunSummary None None "Pass `--provider <name>`; for the SDD skeleton only, run `fsgg-sdd init`."
            )
        | Some name ->
            match resolveDescriptors model |> List.tryFind (fun descriptor -> descriptor.Name = name) with
            | None ->
                ScaffoldBlocked(
                    [ DiagnosticsModule.scaffoldProviderUnknown name ],
                    notRunSummary (Some name) None $"Register '{name}' in `.fsgg/providers.yml` or correct the `--provider` name."
                )
            | Some descriptor when not (isSupportedContract descriptor.ContractVersion) ->
                ScaffoldBlocked(
                    [ DiagnosticsModule.scaffoldProviderVersionUnsupported name descriptor.ContractVersion supportedContractRange ],
                    notRunSummary (Some name) (Some descriptor.ContractVersion) $"Upgrade SDD or the provider to a contract version within {supportedContractRange}."
                )
            | Some descriptor ->
                let effective = effectiveParameters descriptor request

                match missingRequiredParameters descriptor effective with
                | _ :: _ as missing ->
                    ScaffoldBlocked(
                        [ DiagnosticsModule.scaffoldProviderParamMissing name missing ],
                        notRunSummary (Some name) (Some descriptor.ContractVersion) "Supply the missing parameter(s) with `--param <key>=<value>`."
                    )
                | [] ->
                    match collisionPaths model with
                    | _ :: _ as collisions when not request.Force ->
                        ScaffoldBlocked(
                            [ DiagnosticsModule.scaffoldTargetCollision collisions ],
                            notRunSummary (Some name) (Some descriptor.ContractVersion) "Re-run with `--force` to materialize into a non-empty target."
                        )
                    | _ -> ScaffoldProceed(descriptor, effective)

    // ----- invocation planning (stage 2) -----

    let private isCreateProcess effect =
        match effect with
        | RunProcess("dotnet", args, _) -> List.contains "-o" args
        | _ -> false

    let scaffoldInvocationEffects (request: CommandRequest) (descriptor: ProviderDescriptor) (effective: Map<string, string>) =
        let installEffect = RunProcess("dotnet", [ "new"; "install"; descriptor.Source ], "")
        // Best-effort: upgrade an already-installed (e.g. NuGet-sourced) template to its
        // latest version before invoking it. A first-time/local install is handled by
        // `installEffect`; `update` is a no-op when packages are already current. Its
        // result is ignored (offline/up-to-date is not a failure) — only the create
        // process drives the outcome.
        let updateEffect = RunProcess("dotnet", [ "new"; "update" ], "")

        // `dotnet new` exposes each declared template symbol as a `--<symbol>` option;
        // it has no MSBuild-style `-p:k=v` passthrough (in SDK 10 `-p` is merely the
        // auto-generated short alias of the first parameter, so `-p:k=v` is mis-parsed
        // as that option's value). Forward each effective param as a verbatim
        // `--<key> <value>` pair so the author's value reaches the child unchanged.
        let parameterArgs =
            effective
            |> Map.toList
            |> List.collect (fun (key, value) -> [ $"--{key}"; value ])

        let createArgs =
            [ "new"; descriptor.TemplateId; "-o"; "." ]
            @ parameterArgs
            @ (if request.Force then [ "--force" ] else [])

        // install (+ update by default; skipped by `--no-update`), then the SDD
        // skeleton (reused init effects, unchanged), then the create — so the create's
        // after-snapshot includes the skeleton (which the diff subtracts) and the
        // provider's product.
        let refreshEffects = if request.TemplateUpdate then [ installEffect; updateEffect ] else [ installEffect ]
        refreshEffects @ initEffects request @ [ RunProcess("dotnet", createArgs, "") ]

    let plannedCreateCommand (descriptor: ProviderDescriptor) (effective: Map<string, string>) (request: CommandRequest) =
        let parameters =
            effective
            |> Map.toList
            |> List.map (fun (key, value) -> $"--{key} {value}")
            |> String.concat " "

        let parameterSegment = if parameters = "" then "" else " " + parameters
        let forceSegment = if request.Force then " --force" else ""
        $"dotnet new {descriptor.TemplateId} -o .{parameterSegment}{forceSegment}"

    // ----- finalization (stage 3) -----

    let provenanceWriteEffect (request: CommandRequest) (descriptor: ProviderDescriptor) outcome (producedPaths: string list) =
        let record: ScaffoldProvenanceRecord =
            { SchemaVersion = 1
              Generator = request.GeneratorVersion
              ProviderName = descriptor.Name
              ProviderContractVersion = descriptor.ContractVersion
              TemplateRef = descriptor.TemplateId
              Outcome = scaffoldOutcomeValue outcome
              ProducedPaths = producedPaths |> List.map (fun path -> { Path = path; Owner = GeneratedProduct }) }

        [ WriteFile(ScaffoldProvenance.provenancePath, ScaffoldProvenance.serialize record, StructuredSource) ]

    let finalizeScaffold model (descriptor: ProviderDescriptor) =
        let request = model.Request
        let name = descriptor.Name
        let version = descriptor.ContractVersion

        let summaryOf outcome producedPaths providerInvoked skeletonCreated hint : ScaffoldSummary =
            { ProviderName = Some name
              ProviderContractVersion = Some version
              Outcome = scaffoldOutcomeValue outcome
              SkeletonCreated = skeletonCreated
              ProviderInvoked = providerInvoked
              ProducedPathCount = List.length producedPaths
              ProducedPaths = producedPaths
              NextActionHint = hint }

        if request.DryRun then
            let effective = effectiveParameters descriptor request
            let planned = plannedCreateCommand descriptor effective request

            let summary =
                { ProviderName = Some name
                  ProviderContractVersion = Some version
                  Outcome = scaffoldOutcomeValue ProviderNotRun
                  SkeletonCreated = false
                  ProviderInvoked = false
                  ProducedPathCount = 0
                  ProducedPaths = []
                  NextActionHint = $"dry run: would run `{planned}` (produced paths are determined at execution)." }

            summary, [], []
        else
            let createResult = model.InterpretedEffects |> List.tryFind (fun result -> isCreateProcess result.Effect)

            match createResult |> Option.bind (fun result -> result.Process) with
            | None
            | Some { Started = false } ->
                summaryOf ProviderFailed [] false true "Install the .NET SDK and the named template, then re-run scaffold.",
                [ DiagnosticsModule.scaffoldProviderUnavailable name ],
                []
            | Some processResult ->
                let afterSet =
                    createResult
                    |> Option.bind (fun result -> result.Snapshot)
                    |> Option.map (fun snapshot -> parseListing snapshot.Text)
                    |> Option.defaultValue Set.empty

                let produced =
                    Set.difference (Set.difference afterSet (beforePaths model)) (skeletonFiles request)
                    |> Set.remove (normalizeRelativePath ScaffoldProvenance.provenancePath)

                let intrusions = produced |> Set.filter isSddTree |> Set.toList |> List.sort
                let producedPaths = produced |> Set.filter (isSddTree >> not) |> Set.toList |> List.sort

                if not (List.isEmpty intrusions) then
                    summaryOf ProviderFailed producedPaths true true "Fix the provider; it wrote into SDD-owned trees.",
                    [ DiagnosticsModule.scaffoldProviderWroteSddTree intrusions ],
                    provenanceWriteEffect request descriptor ProviderFailed producedPaths
                elif processResult.ExitCode <> 0 then
                    summaryOf ProviderFailed producedPaths true true "Inspect the provider failure, then re-run scaffold.",
                    [ DiagnosticsModule.scaffoldProviderFailed name processResult.ExitCode ],
                    provenanceWriteEffect request descriptor ProviderFailed producedPaths
                elif List.isEmpty producedPaths then
                    summaryOf ProviderSucceededEmpty [] true true "Provider produced no files; begin the lifecycle at `charter`.",
                    [ DiagnosticsModule.scaffoldProviderEmpty name ],
                    provenanceWriteEffect request descriptor ProviderSucceededEmpty []
                else
                    summaryOf ProviderSucceeded producedPaths true true "SDD skeleton ready; begin the lifecycle at `charter`.",
                    [],
                    provenanceWriteEffect request descriptor ProviderSucceeded producedPaths

    // ----- staged driver entry (called from nextLifecycleEffects) -----

    let computeScaffoldNext model =
        match resolveScaffold model with
        | ScaffoldBlocked(diagnostics, summary) ->
            match model.Scaffold with
            | Some _ -> model, []
            | None ->
                { model with
                    Scaffold = Some summary
                    Diagnostics = model.Diagnostics @ diagnostics },
                []
        | ScaffoldProceed(descriptor, effective) ->
            let createInterpreted = model.InterpretedEffects |> List.exists (fun result -> isCreateProcess result.Effect)
            let createPlanned = model.PendingEffects |> List.exists isCreateProcess

            if not createInterpreted then
                if createPlanned then
                    model, []
                else
                    let effects = scaffoldInvocationEffects model.Request descriptor effective
                    { model with PendingEffects = model.PendingEffects @ effects }, effects
            elif Option.isSome model.Scaffold then
                model, []
            else
                let summary, diagnostics, provenanceEffects = finalizeScaffold model descriptor

                { model with
                    PendingEffects = model.PendingEffects @ provenanceEffects
                    Scaffold = Some summary
                    Diagnostics = model.Diagnostics @ diagnostics },
                provenanceEffects
