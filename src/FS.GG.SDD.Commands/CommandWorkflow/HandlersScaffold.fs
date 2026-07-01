namespace FS.GG.SDD.Commands.Internal

open System
open Fsgg.Provider
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
        // 051: the seeded fs-gg-sdd-* process-skill subtrees are SDD-owned skeleton
        // (FR-008). A provider that writes into them is rejected as an intrusion, and
        // they are never recorded as generatedProduct in scaffold-provenance.json.
        || p.StartsWith(".claude/skills/", StringComparison.Ordinal)
        || p.StartsWith(".codex/skills/", StringComparison.Ordinal)

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
          // No resolved provider on a blocked/not-run path, so no minimum to record.
          RequiredMinimumCliVersion = None
          Outcome = scaffoldOutcomeValue ProviderNotRun
          SkeletonCreated = false
          ProviderInvoked = false
          ProducedPathCount = 0
          ProducedPaths = []
          EffectiveParameters = []
          RepoInitOutcome = "notApplicable"
          ExecutableScriptCount = 0
          ExecutableScriptsSkipped = 0
          NextActionHint = hint
          ProviderInvocation = None }

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

    // Post-instantiation marker effects (contracts/post-instantiation-staging.md). They
    // are disjoint from the create marker, so the staged driver detects each tick's phase
    // unambiguously by re-deriving it from the interpreted-effect log (Decision 3).
    let private isProbeProcess effect =
        match effect with
        | RunProcess("git", [ "rev-parse"; "--is-inside-work-tree" ], _) -> true
        | _ -> false

    let private isInitProcess effect =
        match effect with
        | RunProcess("git", [ "init" ], _) -> true
        | _ -> false

    let private isSetExecutableEffect effect =
        match effect with
        | SetExecutable _ -> true
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

    // ----- CLI version coherence (feature 052; pure, no new effect — D11) -----

    /// The required-minimum CLI version to *record* in provenance (E1/D6): the raw
    /// provider-declared value when it parses; `None` when the provider declares none
    /// or declares a malformed minimum (never fabricated, malformed not persisted).
    let resolvedRequiredMinimumCliVersion (descriptor: ProviderDescriptor) : string option =
        descriptor.MinimumCliVersion
        |> Option.bind (fun raw -> Fsgg.Version.tryParse raw |> Option.map (fun _ -> raw))

    /// Pure CLI-coherence advisories for a resolved provider (D11). Emits
    /// `scaffold.cliBehindMinimum` iff the installed CLI is strictly behind a valid
    /// declared minimum; `scaffold.providerMinimumMalformed` iff a declared minimum is
    /// unparseable; nothing when the minimum is absent, equal/above, or the installed
    /// version itself is unparseable (D4/D6/D7). All non-blocking (Info/Warning).
    let cliCoherenceDiagnostics (descriptor: ProviderDescriptor) (request: CommandRequest) : Diagnostic list =
        match descriptor.MinimumCliVersion with
        | None -> []
        | Some rawMinimum ->
            match Fsgg.Version.tryParse rawMinimum with
            | None -> [ scaffoldProviderMinimumMalformed rawMinimum ]
            | Some _ ->
                let installed = request.GeneratorVersion.Version

                match Fsgg.Version.compare installed rawMinimum with
                | Some -1 -> [ scaffoldCliBehindMinimum installed rawMinimum ]
                | _ -> []

    // ----- finalization (stage 3) -----

    let provenanceWriteEffect (request: CommandRequest) (descriptor: ProviderDescriptor) outcome (producedPaths: string list) (effective: Map<string, string>) =
        let record: ScaffoldProvenanceRecord =
            { SchemaVersion = 1
              Generator = request.GeneratorVersion
              RequiredMinimumCliVersion = resolvedRequiredMinimumCliVersion descriptor
              ProviderName = descriptor.Name
              ProviderContractVersion = descriptor.ContractVersion
              TemplateRef = descriptor.TemplateId
              Outcome = scaffoldOutcomeValue outcome
              ProducedPaths = producedPaths |> List.map (fun path -> { Path = path; Owner = GeneratedProduct })
              // `Map.toList` is already ascending by key — the FR-003 effective set
              // (declared defaults overlaid by `--param` overrides) forwarded verbatim.
              EffectiveParameters = Map.toList effective }

        [ WriteFile(ScaffoldProvenance.provenancePath, ScaffoldProvenance.serialize record, StructuredSource) ]

    // The create outcome classified from the interpreted-effect log. A **terminal**
    // outcome (dry-run, provider unavailable/failed, SDD-tree intrusion) finalizes in a
    // single tick — summary + diagnostics + the provenance write — and runs no
    // post-instantiation steps (FR-009). A **success** outcome (incl. the empty-but-
    // successful one) defers the single provenance write and the final summary to the
    // post-instantiation phase (TICK A/C), so provenance is written exactly once.
    type ScaffoldFinalization =
        | FinalizeTerminal of ScaffoldSummary * Diagnostic list * CommandEffect list
        | FinalizeSuccess of ScaffoldOutcome * string list

    let finalizeScaffold model (descriptor: ProviderDescriptor) (effective: Map<string, string>) =
        let request = model.Request
        let name = descriptor.Name
        let version = descriptor.ContractVersion

        let requiredMinimum = resolvedRequiredMinimumCliVersion descriptor

        // Project the edge's captured `ProcessRunResult` into the report's provider-defect
        // diagnostic facts (E1). `ExitCode` is `Some` only when the process actually
        // started, so a never-launched provider surfaces `null` — never a spurious `0`
        // (FR-003).
        let providerInvocationOf (processResult: ProcessRunResult) : ProviderInvocationResult =
            { CommandLine = processResult.Command
              ProcessStarted = processResult.Started
              ExitCode = if processResult.Started then Some processResult.ExitCode else None
              StandardOutput = processResult.StandardOutput
              StandardOutputTruncated = processResult.StandardOutputTruncated
              StandardError = processResult.StandardError
              StandardErrorTruncated = processResult.StandardErrorTruncated }

        let terminalSummary outcome producedPaths providerInvoked skeletonCreated hint providerInvocation : ScaffoldSummary =
            { ProviderName = Some name
              ProviderContractVersion = Some version
              RequiredMinimumCliVersion = requiredMinimum
              Outcome = scaffoldOutcomeValue outcome
              SkeletonCreated = skeletonCreated
              ProviderInvoked = providerInvoked
              ProducedPathCount = List.length producedPaths
              ProducedPaths = producedPaths
              EffectiveParameters = Map.toList effective
              RepoInitOutcome = "notApplicable"
              ExecutableScriptCount = 0
              ExecutableScriptsSkipped = 0
              NextActionHint = hint
              ProviderInvocation = providerInvocation }

        if request.DryRun then
            let planned = plannedCreateCommand descriptor effective request

            let summary =
                { ProviderName = Some name
                  ProviderContractVersion = Some version
                  RequiredMinimumCliVersion = requiredMinimum
                  Outcome = scaffoldOutcomeValue ProviderNotRun
                  SkeletonCreated = false
                  ProviderInvoked = false
                  ProducedPathCount = 0
                  ProducedPaths = []
                  // The dry-run preview records exactly what would be forwarded
                  // (FR-003 audit preview): the resolved effective set.
                  EffectiveParameters = Map.toList effective
                  RepoInitOutcome = "notApplicable"
                  ExecutableScriptCount = 0
                  ExecutableScriptsSkipped = 0
                  NextActionHint =
                    $"dry run: would run `{planned}`, initialize a git repository, and make produced scripts executable (produced paths are determined at execution)."
                  ProviderInvocation = None }

            FinalizeTerminal(summary, [], [])
        else
            let createResult = model.InterpretedEffects |> List.tryFind (fun result -> isCreateProcess result.Effect)
            let createProcess = createResult |> Option.bind (fun result -> result.Process)

            match createProcess with
            | None
            | Some { Started = false } ->
                FinalizeTerminal(
                    terminalSummary ProviderFailed [] false true "Install the .NET SDK and the named template, then re-run scaffold." (createProcess |> Option.map providerInvocationOf),
                    [ DiagnosticsModule.scaffoldProviderUnavailable name ],
                    []
                )
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
                    FinalizeTerminal(
                        terminalSummary ProviderFailed producedPaths true true "Fix the provider; it wrote into SDD-owned trees." (Some(providerInvocationOf processResult)),
                        [ DiagnosticsModule.scaffoldProviderWroteSddTree intrusions ],
                        provenanceWriteEffect request descriptor ProviderFailed producedPaths effective
                    )
                elif processResult.ExitCode <> 0 then
                    FinalizeTerminal(
                        terminalSummary ProviderFailed producedPaths true true "Inspect the provider failure, then re-run scaffold." (Some(providerInvocationOf processResult)),
                        [ DiagnosticsModule.scaffoldProviderFailed name processResult.ExitCode ],
                        provenanceWriteEffect request descriptor ProviderFailed producedPaths effective
                    )
                elif List.isEmpty producedPaths then
                    FinalizeSuccess(ProviderSucceededEmpty, [])
                else
                    FinalizeSuccess(ProviderSucceeded, producedPaths)

    // ----- post-instantiation staging (TICK A → B → C) -----

    // TICK C: compute the terminal success summary from the interpreted post-instantiation
    // effects — the repo-init outcome from the `git rev-parse` probe (exit-code only,
    // Decision 1) and the make-executable counts from the `SetExecutable` results. Every
    // emitted diagnostic is advisory and non-fatal (FR-010).
    let private finalizePostInstantiation model (descriptor: ProviderDescriptor) outcome (producedPaths: string list) probeProcess (effective: Map<string, string>) =
        let repoInitOutcome, repoInitDiagnostics =
            match probeProcess with
            | Some { Started = false } ->
                "skippedGitUnavailable", [ DiagnosticsModule.scaffoldRepoInitSkippedGitUnavailable () ]
            | Some { Started = true; ExitCode = 0 } ->
                "skippedExistingRepository", [ DiagnosticsModule.scaffoldRepoInitSkippedExistingRepository () ]
            | Some { Started = true } -> "initialized", []
            | None -> "notApplicable", []

        let execResults = model.InterpretedEffects |> List.filter (fun result -> isSetExecutableEffect result.Effect)
        let executableCount = execResults |> List.filter (fun result -> result.Succeeded) |> List.length

        let skippedPaths =
            execResults
            |> List.filter (fun result -> not result.Succeeded)
            |> List.choose (fun result ->
                match result.Effect with
                | SetExecutable path -> Some path
                | _ -> None)
            |> List.sort

        let execDiagnostics =
            if List.isEmpty skippedPaths then []
            else [ DiagnosticsModule.scaffoldScriptsNotMadeExecutable skippedPaths ]

        let outcomeDiagnostics =
            match outcome with
            | ProviderSucceededEmpty -> [ DiagnosticsModule.scaffoldProviderEmpty descriptor.Name ]
            | _ -> []

        let hint =
            match outcome with
            | ProviderSucceededEmpty -> "Provider produced no files; begin the lifecycle at `charter`."
            | _ -> "SDD skeleton ready; begin the lifecycle at `charter`."

        let summary: ScaffoldSummary =
            { ProviderName = Some descriptor.Name
              ProviderContractVersion = Some descriptor.ContractVersion
              RequiredMinimumCliVersion = resolvedRequiredMinimumCliVersion descriptor
              Outcome = scaffoldOutcomeValue outcome
              SkeletonCreated = true
              ProviderInvoked = true
              ProducedPathCount = List.length producedPaths
              ProducedPaths = producedPaths
              EffectiveParameters = Map.toList effective
              RepoInitOutcome = repoInitOutcome
              ExecutableScriptCount = executableCount
              ExecutableScriptsSkipped = List.length skippedPaths
              NextActionHint = hint
              ProviderInvocation = None }

        summary, outcomeDiagnostics @ repoInitDiagnostics @ execDiagnostics

    // The three-tick post-instantiation machine, re-derived from the interpreted-effect
    // log each tick (no new model field). Reached only on a success create outcome.
    let private postInstantiationNext model (descriptor: ProviderDescriptor) outcome (producedPaths: string list) (effective: Map<string, string>) =
        let probeInterpreted = model.InterpretedEffects |> List.exists (fun result -> isProbeProcess result.Effect)
        let probePlanned = model.PendingEffects |> List.exists isProbeProcess
        let initInterpreted = model.InterpretedEffects |> List.exists (fun result -> isInitProcess result.Effect)
        let initPlanned = model.PendingEffects |> List.exists isInitProcess

        if not (probeInterpreted || probePlanned) then
            // TICK A — the success path's single provenance write (FR-004, before `git
            // init`), the work-tree probe, and one SetExecutable per produced `.sh`.
            let scriptEffects =
                producedPaths
                |> List.filter (fun path -> path.EndsWith(".sh", StringComparison.Ordinal))
                |> List.map SetExecutable

            let effects =
                provenanceWriteEffect model.Request descriptor outcome producedPaths effective
                @ [ RunProcess("git", [ "rev-parse"; "--is-inside-work-tree" ], "") ]
                @ scriptEffects

            { model with PendingEffects = model.PendingEffects @ effects }, effects
        elif not probeInterpreted then
            // Probe planned, awaiting interpretation.
            model, []
        else
            let probeProcess =
                model.InterpretedEffects
                |> List.tryPick (fun result -> if isProbeProcess result.Effect then result.Process else None)

            let shouldInit =
                match probeProcess with
                | Some { Started = true; ExitCode = code } -> code <> 0
                | _ -> false

            if shouldInit && not (initInterpreted || initPlanned) then
                // TICK B — not inside a work tree and git is available: initialize.
                let effect = RunProcess("git", [ "init" ], "")
                { model with PendingEffects = model.PendingEffects @ [ effect ] }, [ effect ]
            elif shouldInit && not initInterpreted then
                // Init planned, awaiting interpretation.
                model, []
            else
                // TICK C — init interpreted or skipped: set the terminal summary once.
                let summary, diagnostics = finalizePostInstantiation model descriptor outcome producedPaths probeProcess effective

                // Feature 052 (US2): merge the non-blocking CLI-coherence advisory on the
                // success path too, so the advisory appears in every descriptor-resolved outcome.
                { model with
                    Scaffold = Some summary
                    Diagnostics = model.Diagnostics @ diagnostics @ cliCoherenceDiagnostics descriptor model.Request },
                []

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
                match finalizeScaffold model descriptor effective with
                | FinalizeTerminal(summary, diagnostics, provenanceEffects) ->
                    // Feature 052 (US2): merge the non-blocking CLI-coherence advisory on
                    // every descriptor-resolved terminal path (dry-run, unavailable, failed,
                    // intrusion) so it appears in all outcomes without blocking.
                    { model with
                        PendingEffects = model.PendingEffects @ provenanceEffects
                        Scaffold = Some summary
                        Diagnostics = model.Diagnostics @ diagnostics @ cliCoherenceDiagnostics descriptor model.Request },
                    provenanceEffects
                | FinalizeSuccess(outcome, producedPaths) ->
                    postInstantiationNext model descriptor outcome producedPaths effective
