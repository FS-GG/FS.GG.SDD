namespace FS.GG.SDD.Commands.Internal

open System
open System.Text.Json
open Fsgg.Provider
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Config
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.Foundation

/// `fsgg-sdd scaffold` handler. The pure `plan`/`update` boundary produces the
/// effects (`RunProcess`, skeleton writes, the provenance `WriteFile`); the edge
/// interpreter performs the real process + filesystem I/O (Constitution V). Staging
/// is recomputed from the model each `nextLifecycleEffects` tick:
///   1. resolve `--provider` + validate version/params/collision (pure);
///   2. on a valid provider, plan `dotnet new install` + the init skeleton +
///      `dotnet new <templateId>`;
///   3. once the create process is interpreted, diff produced paths, guard the SDD
///      trees, and plan the deterministic `.fsgg/scaffold-provenance.json` write.
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
        match version.Trim().Trim('"').Split('.') with
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
        // The `.claude`/`.codex` roots stay WHOLE-ROOT reserved (056 keeps the guard
        // strict — the opposite of the reverted 055 narrowing).
        || p.StartsWith(".claude/skills/", StringComparison.Ordinal)
        || p.StartsWith(".codex/skills/", StringComparison.Ordinal)
        // 056: the neutral `.agents/skills/` root is the provider's to write, EXCEPT the
        // reserved `fs-gg-sdd-*` namespace — a provider write there is an intrusion, so a
        // provider can never clobber SDD's seeded skills in the root it does own (FR-002).
        || p.StartsWith(".agents/skills/fs-gg-sdd-", StringComparison.Ordinal)

    let isSddOwned (path: string) =
        let p = normalizeRelativePath path
        isSddTree p || p = "AGENTS.md" || p = "CLAUDE.md"

    let parseListing (text: string) =
        text.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map normalizeRelativePath
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Set.ofArray

    let beforePaths model =
        parseListing (directoryListing "" model)

    // Pre-existing, non-SDD content the provider would materialize over (FR-010).
    let collisionPaths model =
        beforePaths model |> Set.filter (isSddOwned >> not) |> Set.toList |> List.sort

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

    // `dotnet new <template>` exposes each template symbol as a `--<symbol>` option and
    // offers no collision-free passthrough (see `scaffoldInvocationEffects`). An author
    // `--param <key>=<value>` whose key is empty (renders to a bare `--` options
    // terminator), dash-prefixed (a malformed option token), or shadows a `dotnet new`
    // built-in long option (`force`→`--force`, `output`→`--output <path>`, …) would
    // silently inject that option instead of forwarding a template symbol. Reject such
    // keys at the boundary rather than forward option-injection to the child (Gap C
    // finding 5 / ADR-0002). Short single-dash aliases (`-o`/`-n`/…) cannot be produced
    // by the `--<key>` interpolation, so only the long forms can collide. Only
    // author-supplied keys are validated; provider-declared defaults are the trusted
    // contract.
    let private reservedDotnetNewOptions =
        set
            [ "force"
              "output"
              "name"
              "dry-run"
              "no-update"
              "language"
              "type"
              "project"
              "verbosity"
              "diagnostics"
              "help" ]

    let invalidAuthorParamKeys (request: CommandRequest) =
        request.Parameters
        |> List.map fst
        |> List.filter (fun key -> key = "" || key.StartsWith "-" || Set.contains key reservedDotnetNewOptions)
        |> List.distinct

    let missingRequiredParameters (descriptor: ProviderDescriptor) (effective: Map<string, string>) =
        descriptor.Parameters
        |> List.filter (fun spec -> spec.Required && not (Map.containsKey spec.Key effective))
        |> List.map (fun spec -> spec.Key)

    // Feature 080: derive a valid F# identifier from the raw product name and forward it
    // under the provider-declared `IdentifierParameter` (the sink), leaving the raw name
    // (the `NameParameter` source) untouched for string-literal/path/.fsproj contexts.
    // No sink declared ⇒ no derivation (backward compatible). An author `--param` on the
    // sink key wins (FR-008). A name with no identifier character blocks (FR-009).
    let deriveIdentifierParameter
        (descriptor: ProviderDescriptor)
        (request: CommandRequest)
        (effective: Map<string, string>)
        : Result<Map<string, string>, Diagnostic> =
        match descriptor.IdentifierParameter with
        | None -> Ok effective
        | Some sinkKey when sinkKey = resolveNameParameter descriptor ->
            // Provider misconfiguration: the sink equals the name key. Deriving here would
            // silently overwrite the raw name, defeating the whole point (raw name preserved
            // for string/path contexts). Forward unchanged rather than clobber it.
            Ok effective
        | Some sinkKey when request.Parameters |> List.exists (fun (key, _) -> key = sinkKey) ->
            // Author supplied the sink value explicitly — verbatim, no derivation.
            Ok effective
        | Some sinkKey ->
            match Map.tryFind (resolveNameParameter descriptor) effective with
            | None -> Ok effective // No name to derive from; forward as-is.
            | Some rawName ->
                match FsharpIdentifier.deriveNamespace rawName with
                | Ok identifier -> Ok(Map.add sinkKey identifier effective)
                | Error(FsharpIdentifier.Unrepresentable name) ->
                    Error(DiagnosticsModule.scaffoldNameUnrepresentable name)

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
          MirroredPaths = []
          MaterializedDriverPaths = []
          MaterializedGameSkillPaths = []
          EffectiveParameters = []
          RepoInitOutcome = "notApplicable"
          ToolManifestOutcome = "notApplicable"
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
            match
                resolveDescriptors model
                |> List.tryFind (fun descriptor -> descriptor.Name = name)
            with
            | None ->
                ScaffoldBlocked(
                    [ DiagnosticsModule.scaffoldProviderUnknown name ],
                    notRunSummary
                        (Some name)
                        None
                        $"Register '{name}' in `.fsgg/providers.yml` or correct the `--provider` name."
                )
            | Some descriptor when not (isSupportedContract descriptor.ContractVersion) ->
                ScaffoldBlocked(
                    [ DiagnosticsModule.scaffoldProviderVersionUnsupported
                          name
                          descriptor.ContractVersion
                          supportedContractRange ],
                    notRunSummary
                        (Some name)
                        (Some descriptor.ContractVersion)
                        $"Upgrade SDD or the provider to a contract version within {supportedContractRange}."
                )
            | Some descriptor when not (List.isEmpty (invalidAuthorParamKeys request)) ->
                ScaffoldBlocked(
                    [ DiagnosticsModule.scaffoldInvalidParamKey (invalidAuthorParamKeys request) ],
                    notRunSummary
                        (Some name)
                        (Some descriptor.ContractVersion)
                        "Rename the offending `--param` key(s) to a template symbol name that is not a `dotnet new` option."
                )
            | Some descriptor ->
                match deriveIdentifierParameter descriptor request (effectiveParameters descriptor request) with
                | Error unrepresentable ->
                    ScaffoldBlocked(
                        [ unrepresentable ],
                        notRunSummary
                            (Some name)
                            (Some descriptor.ContractVersion)
                            "Choose a product name containing at least one letter, digit, or underscore."
                    )
                | Ok effective ->

                    match missingRequiredParameters descriptor effective with
                    | _ :: _ as missing ->
                        ScaffoldBlocked(
                            [ DiagnosticsModule.scaffoldProviderParamMissing name missing ],
                            notRunSummary
                                (Some name)
                                (Some descriptor.ContractVersion)
                                "Supply the missing parameter(s) with `--param <key>=<value>`."
                        )
                    | [] ->
                        match collisionPaths model with
                        | _ :: _ as collisions when not request.Force ->
                            ScaffoldBlocked(
                                [ DiagnosticsModule.scaffoldTargetCollision collisions ],
                                notRunSummary
                                    (Some name)
                                    (Some descriptor.ContractVersion)
                                    "Re-run with `--force` to materialize into a non-empty target."
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

    /// The dotnet tool manifest scaffold pins `fsgg-sdd` into (FS.GG.SDD#315), so a scaffolded
    /// product's toolchain is reproducible and Renovate-updatable rather than "whatever is
    /// installed globally". A generic, SDD-owned post-instantiation step in the same class as
    /// `git init` and the script executable bit — never delegated to the provider. The pinned id
    /// is SDD's **own** (`FS.GG.SDD.Cli` / `fsgg-sdd`), which generic SDD may legitimately name;
    /// no provider package id, template id, or docs URL appears here.
    let toolManifestPath = ".config/dotnet-tools.json"

    let private isToolManifestWrite effect =
        match effect with
        | WriteFile(path, _, _) -> normalizeRelativePath path = toolManifestPath
        | _ -> false

    /// The canonical `dotnet tool` manifest shape, pinning the scaffolding CLI's version.
    /// `dotnet` keys the manifest by the lowercased package id and lists the `ToolCommandName`.
    /// A pure function of `version` — no clock, no environment — so two runs of one CLI produce
    /// byte-identical bytes (the determinism the rest of scaffold's output already guarantees).
    let toolManifestText (version: string) =
        // Escaped through the JSON serializer rather than interpolated raw: the version reaches
        // here from an assembly attribute, and a report must never emit malformed JSON.
        let quotedVersion = JsonSerializer.Serialize(version: string)

        String.Join(
            "\n",
            [ "{"
              "  \"version\": 1,"
              "  \"isRoot\": true,"
              "  \"tools\": {"
              "    \"fs.gg.sdd.cli\": {"
              $"      \"version\": {quotedVersion},"
              "      \"commands\": ["
              "        \"fsgg-sdd\""
              "      ]"
              "    }"
              "  }"
              "}"
              "" ]
        )

    let scaffoldInvocationEffects
        (request: CommandRequest)
        (descriptor: ProviderDescriptor)
        (effective: Map<string, string>)
        =
        let installEffect =
            RunProcess("dotnet", [ "new"; "install"; descriptor.Source ], "")
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
        let refreshEffects =
            if request.TemplateUpdate then
                [ installEffect; updateEffect ]
            else
                [ installEffect ]

        refreshEffects @ initEffects request @ [ RunProcess("dotnet", createArgs, "") ]

    let plannedCreateCommand
        (descriptor: ProviderDescriptor)
        (effective: Map<string, string>)
        (request: CommandRequest)
        =
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

    // ----- 056 fan-out: the union mirror (SDD is the sole mirror authority) -----

    // The provider's produced skills live under the neutral `.agents/skills/` root; the
    // reserved `fs-gg-sdd-*` namespace there is already an intrusion (isSddTree), so a
    // produced path under `.agents/skills/` is a provider co-tenant skill file. SDD copies
    // each byte-identically into every other declared root (FR-003/FR-005). 058/ADR-0014
    // §Decision 5: destinations derive from the one `agentSkillRoots` constant through the
    // shared `SkillMirror`, never a hardcoded `.claude`/`.codex` list here.
    let private agentsSkillsPrefix = Fsgg.SkillMirror.providerSourceRoot + "/skills/"

    let providerSkillFiles (producedPaths: string list) =
        producedPaths
        |> List.map normalizeRelativePath
        |> List.filter (fun p -> p.StartsWith(agentsSkillsPrefix, StringComparison.Ordinal))
        |> List.sort

    // The byte-identical mirror-target paths for one `.agents/skills/REST` source — one per
    // non-source root, computed by the shared library (`retargetSkillPath` keeps REST verbatim).
    let mirrorTargetsFor (agentsPath: string) =
        Fsgg.SkillMirror.mirrorTargetRoots Fsgg.Schemas.agentSkillRoots
        |> List.map (fun targetRoot -> Fsgg.SkillMirror.retargetSkillPath targetRoot agentsPath)

    // The mirrored-copy path set for a produced-path set — pure, deterministic, sorted.
    // Known before the mirror I/O runs (targets are a function of the produced paths), so
    // provenance/report carry them independent of the read/write results (FR-007).
    let plannedMirroredPaths (producedPaths: string list) =
        providerSkillFiles producedPaths |> List.collect mirrorTargetsFor |> List.sort

    // 108 / ADR-0054: the driver materialization planned from the CLI's embedded `FS.GG.Drivers`
    // bytes, gated by the skill ids present in the workspace (seeded ∪ provider) for `has …`
    // predicate evaluation. Pure & deterministic (reads only compiled-in resources + `producedPaths`),
    // re-derivable each tick like `plannedMirroredPaths`, so both the TICK-A write/provenance and the
    // finalize summary/diagnostics draw from the same plan without a new model field.
    let plannedDriverOutcome (producedPaths: string list) =
        let providerIds =
            providerSkillFiles producedPaths |> List.choose Fsgg.SkillMirror.skillIdOfPath

        let presentIds = Set.ofList (SeededSkills.skillNames @ providerIds)
        let outcome = DriverSkills.plan presentIds

        // No-clobber honesty (FR-005/FR-009): a driver target already occupied by the provider's own
        // output — its `.agents/skills/<id>` skill, or the `.claude`/`.codex` mirror copies of it the
        // preceding TICK-MIRROR fanned out — is *preserved* by the `AgentGuidanceTarget` write, not
        // materialized by us; claiming it would double-own the path (`mirrored` + `driver`) with a
        // possibly-different digest. `occupied` is a pure function of `producedPaths` (never our own
        // just-written driver files), so it is identical at the TICK-A write and the finalize summary
        // — dropping such paths keeps both, and provenance, from over-claiming a refused write.
        let occupied = Set.ofList (producedPaths @ plannedMirroredPaths producedPaths)

        let kept =
            outcome.ProvenancePaths
            |> List.filter (fun (path, _) -> not (occupied.Contains path))

        let keptPaths = kept |> List.map fst |> Set.ofList

        { outcome with
            Writes =
                outcome.Writes
                |> List.filter (fun effect ->
                    match effectPath effect with
                    | Some path -> keptPaths.Contains path
                    | None -> true)
            ProvenancePaths = kept
            MaterializedIds =
                kept
                |> List.choose (fun (path, _) -> Fsgg.SkillMirror.skillIdOfPath path)
                |> List.distinct }

    // The fail-closed driver diagnostics for a planned outcome (empty on a clean plan). Manifest
    // defect and namespace-collision/verify failures are tool-defect errors; an unevaluable
    // predicate is a non-blocking advisory (FR-004).
    let driverDiagnostics (outcome: DriverSkills.DriverOutcome) : Diagnostic list =
        [ yield!
              outcome.ManifestError
              |> Option.map DiagnosticsModule.scaffoldDriverManifestMalformed
              |> Option.toList
          if not (List.isEmpty outcome.NamespaceCollisionIds) then
              yield DiagnosticsModule.scaffoldDriverNamespaceCollision outcome.NamespaceCollisionIds
          if not (List.isEmpty outcome.VerifyFailedIds) then
              yield DiagnosticsModule.scaffoldDriverVerifyFailed outcome.VerifyFailedIds
          if not (List.isEmpty outcome.PredicateUnevaluatedIds) then
              yield DiagnosticsModule.scaffoldDriverPredicateUnevaluated outcome.PredicateUnevaluatedIds ]

    // ADR-0063 / FS.GG.SDD#623: the owner-skill materialization planned from the CLI's embedded
    // the owner-skills package bytes, gated by the effective scaffold parameter set (`profile in [..
    // ..]`, …) for `materializes-when` evaluation. Pure & deterministic (reads only
    // compiled-in resources + `producedPaths` + `effective`), re-derivable each tick like
    // `plannedDriverOutcome`, so both the TICK-A write/provenance and the finalize summary draw
    // from the same plan without a new model field.
    let plannedGameSkillOutcome (producedPaths: string list) (effective: Map<string, string>) =
        let outcome = GameSkills.plan effective

        // No-clobber honesty: a owner-skill target already occupied by the provider's own output —
        // its `.agents/skills/<id>` skill, or the `.claude`/`.codex` mirror copies of it — is
        // *preserved* by the `AgentGuidanceTarget` write, not materialized by us; claiming it would
        // double-own the path. A delivered (`mirrored: false`) owner skill has no provider copy by
        // construction (ADR-0022 §6), so this normally drops nothing — but it keeps the summary and
        // provenance from over-claiming a refused write should a provider ship a same-named skill.
        let occupied = Set.ofList (producedPaths @ plannedMirroredPaths producedPaths)

        let kept =
            outcome.ProvenancePaths
            |> List.filter (fun (path, _) -> not (occupied.Contains path))

        let keptPaths = kept |> List.map fst |> Set.ofList

        { outcome with
            Writes =
                outcome.Writes
                |> List.filter (fun effect ->
                    match effectPath effect with
                    | Some path -> keptPaths.Contains path
                    | None -> true)
            ProvenancePaths = kept
            MaterializedIds =
                kept
                |> List.choose (fun (path, _) -> Fsgg.SkillMirror.skillIdOfPath path)
                |> List.distinct }

    // The fail-closed owner-skill diagnostics for a planned outcome (empty on a clean plan). Same
    // classes as the driver seam: manifest defect / namespace collision / verify failure are
    // tool-defect errors; an unevaluable predicate is a non-blocking advisory (FR-004).
    let gameSkillDiagnostics (outcome: GameSkills.GameSkillOutcome) : Diagnostic list =
        [ yield!
              outcome.ManifestError
              |> Option.map DiagnosticsModule.scaffoldGameSkillManifestMalformed
              |> Option.toList
          if not (List.isEmpty outcome.NamespaceCollisionIds) then
              yield DiagnosticsModule.scaffoldGameSkillNamespaceCollision outcome.NamespaceCollisionIds
          if not (List.isEmpty outcome.VerifyFailedIds) then
              yield DiagnosticsModule.scaffoldGameSkillVerifyFailed outcome.VerifyFailedIds
          if not (List.isEmpty outcome.PredicateUnevaluatedIds) then
              yield DiagnosticsModule.scaffoldGameSkillPredicateUnevaluated outcome.PredicateUnevaluatedIds ]

    // ----- finalization (stage 3) -----

    let provenanceWriteEffect
        (request: CommandRequest)
        (descriptor: ProviderDescriptor)
        outcome
        (producedPaths: string list)
        (mirroredPaths: string list)
        (sddOwnedPaths: string list)
        (driverPaths: (string * string) list)
        (gameSkillPaths: (string * string) list)
        (skillDigests: Map<string, string>)
        (effective: Map<string, string>)
        =
        let record: ScaffoldProvenanceRecord =
            { SchemaVersion = 1
              Generator = request.GeneratorVersion
              RequiredMinimumCliVersion = resolvedRequiredMinimumCliVersion descriptor
              ProviderName = descriptor.Name
              ProviderContractVersion = descriptor.ContractVersion
              TemplateRef = descriptor.TemplateId
              Outcome = scaffoldOutcomeValue outcome
              // 058/ADR-0014 §Decision 3: content-addressed provenance — each produced/mirrored
              // skill copy carries the `sha256` of its materialized body (`skillDigests`);
              // non-skill produced paths carry none. The field is omitted from output while None.
              ProducedPaths =
                producedPaths
                |> List.map (fun path ->
                    { Path = path
                      Owner = GeneratedProduct
                      Sha256 = Map.tryFind path skillDigests })
              // 056: the fan-out mirror copies, owner `Mirrored` (never `generatedProduct`).
              // Empty on any non-success terminal path so an incomplete fan-out is never
              // recorded as complete (FR-012).
              MirroredPaths =
                mirroredPaths
                |> List.map (fun path ->
                    { Path = path
                      Owner = ArtifactOwner.Mirrored
                      Sha256 = Map.tryFind path skillDigests })
              // FS.GG.SDD#315: files SDD itself wrote post-instantiation (owner `sdd`), kept out
              // of `producedPaths` so the app-only invariant — producedPaths == exactly the
              // provider's tree — survives. Empty on every non-success terminal path.
              SddOwnedPaths =
                sddOwnedPaths
                |> List.map (fun path ->
                    { Path = path
                      Owner = ArtifactOwner.Sdd
                      Sha256 = None })
              // 108 / ADR-0054: the `.github`-authored driver skill copies materialized from the
              // pinned package, owner `Driver`, each carrying the manifest `sha256` it was
              // content-verified against. Empty on every non-success/terminal path.
              DriverPaths =
                driverPaths
                |> List.map (fun (path, sha256) ->
                    { Path = path
                      Owner = ArtifactOwner.Driver
                      Sha256 =
                        (if String.IsNullOrWhiteSpace sha256 then
                             None
                         else
                             Some sha256) })
              // ADR-0063 / FS.GG.SDD#623: the owner-authored product skill copies materialized
              // from the pinned the owner-skills package, owner `GameSkill`, each carrying the
              // manifest `sha256` it was content-verified against. Empty on every non-success path.
              GameSkillPaths =
                gameSkillPaths
                |> List.map (fun (path, sha256) ->
                    { Path = path
                      Owner = ArtifactOwner.GameSkill
                      Sha256 =
                        (if String.IsNullOrWhiteSpace sha256 then
                             None
                         else
                             Some sha256) })
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
              ExitCode =
                if processResult.Started then
                    Some processResult.ExitCode
                else
                    None
              StandardOutput = processResult.StandardOutput
              StandardOutputTruncated = processResult.StandardOutputTruncated
              StandardError = processResult.StandardError
              StandardErrorTruncated = processResult.StandardErrorTruncated }

        let terminalSummary
            outcome
            producedPaths
            providerInvoked
            skeletonCreated
            hint
            providerInvocation
            : ScaffoldSummary =
            { ProviderName = Some name
              ProviderContractVersion = Some version
              RequiredMinimumCliVersion = requiredMinimum
              Outcome = scaffoldOutcomeValue outcome
              SkeletonCreated = skeletonCreated
              ProviderInvoked = providerInvoked
              ProducedPathCount = List.length producedPaths
              ProducedPaths = producedPaths
              // Terminal (non-success) paths perform no fan-out (FR-012).
              MirroredPaths = []
              // Terminal paths materialize no driver either (108).
              MaterializedDriverPaths = []
              // ...and no owner-sourced skill either (ADR-0063 / FS.GG.SDD#623).
              MaterializedGameSkillPaths = []
              EffectiveParameters = Map.toList effective
              RepoInitOutcome = "notApplicable"
              ToolManifestOutcome = "notApplicable"
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
                  MirroredPaths = []
                  MaterializedDriverPaths = []
                  MaterializedGameSkillPaths = []
                  // The dry-run preview records exactly what would be forwarded
                  // (FR-003 audit preview): the resolved effective set.
                  EffectiveParameters = Map.toList effective
                  RepoInitOutcome = "notApplicable"
                  ToolManifestOutcome = "notApplicable"
                  ExecutableScriptCount = 0
                  ExecutableScriptsSkipped = 0
                  NextActionHint =
                    $"dry run: would run `{planned}`, initialize a git repository, pin `fsgg-sdd` in `{toolManifestPath}`, and make produced scripts executable (produced paths are determined at execution)."
                  ProviderInvocation = None }

            FinalizeTerminal(summary, [], [])
        else
            let createResult =
                model.InterpretedEffects
                |> List.tryFind (fun result -> isCreateProcess result.Effect)

            let createProcess = createResult |> Option.bind (fun result -> result.Process)

            match createProcess with
            | None
            | Some { Started = false } ->
                FinalizeTerminal(
                    terminalSummary
                        ProviderFailed
                        []
                        false
                        true
                        "Install the .NET SDK and the named template, then re-run scaffold."
                        (createProcess |> Option.map providerInvocationOf),
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

                let producedPaths =
                    produced |> Set.filter (isSddTree >> not) |> Set.toList |> List.sort

                if not (List.isEmpty intrusions) then
                    FinalizeTerminal(
                        terminalSummary
                            ProviderFailed
                            producedPaths
                            true
                            true
                            "Fix the provider; it wrote into SDD-owned trees."
                            (Some(providerInvocationOf processResult)),
                        [ DiagnosticsModule.scaffoldProviderWroteSddTree intrusions ],
                        provenanceWriteEffect
                            request
                            descriptor
                            ProviderFailed
                            producedPaths
                            []
                            []
                            []
                            []
                            Map.empty
                            effective
                    )
                elif processResult.ExitCode <> 0 then
                    FinalizeTerminal(
                        terminalSummary
                            ProviderFailed
                            producedPaths
                            true
                            true
                            "Inspect the provider failure, then re-run scaffold."
                            (Some(providerInvocationOf processResult)),
                        [ DiagnosticsModule.scaffoldProviderFailed name processResult.ExitCode ],
                        provenanceWriteEffect
                            request
                            descriptor
                            ProviderFailed
                            producedPaths
                            []
                            []
                            []
                            []
                            Map.empty
                            effective
                    )
                elif List.isEmpty producedPaths then
                    FinalizeSuccess(ProviderSucceededEmpty, [])
                else
                    FinalizeSuccess(ProviderSucceeded, producedPaths)

    // ----- post-instantiation staging (TICK A → B → C) -----

    // TICK C: compute the terminal success summary from the interpreted post-instantiation
    // effects — the repo-init outcome from the `git rev-parse` probe (exit-code only,
    // Decision 1) and the make-executable counts from the `SetExecutable` results. Every
    // diagnostic emitted *here* is advisory and non-fatal (FR-010). That is a statement about
    // this function, not about the whole step set: a failed `WriteFile` of the tool manifest
    // carries its own `toolDefect`/`unsafeOverwrite` error from the interpreter and blocks, as
    // every SDD artifact write does (`.fsgg/*`, `.gitignore`, provenance). The non-fatal
    // siblings — `git init`, `chmod` — are `RunProcess`/`SetExecutable` over externally-owned
    // state, which is why they degrade instead.
    let private finalizePostInstantiation
        model
        (descriptor: ProviderDescriptor)
        outcome
        (producedPaths: string list)
        (mirroredPaths: string list)
        probeProcess
        (effective: Map<string, string>)
        =
        let repoInitOutcome, repoInitDiagnostics =
            match probeProcess with
            | Some { Started = false } ->
                "skippedGitUnavailable", [ DiagnosticsModule.scaffoldRepoInitSkippedGitUnavailable () ]
            | Some { Started = true; ExitCode = 0 } ->
                "skippedExistingRepository", [ DiagnosticsModule.scaffoldRepoInitSkippedExistingRepository () ]
            | Some { Started = true } -> "initialized", []
            | None -> "notApplicable", []

        // FS.GG.SDD#315: re-derived from the interpreted log like every other post-instantiation
        // fact. `pinned` — SDD wrote the manifest; `skippedExisting` — one was already there and
        // was preserved; `failed` — the write was planned and did not land (its own diagnostic
        // already rides on the effect result, so none is added here); `notApplicable` — the step
        // never ran. An incomplete pin is never reported as a pin (FR-009).
        let toolManifestOutcome, toolManifestDiagnostics =
            match
                model.InterpretedEffects
                |> List.tryFind (fun result -> isToolManifestWrite result.Effect)
            with
            | Some result when result.Succeeded -> "pinned", []
            | Some _ -> "failed", []
            | None ->
                if snapshot toolManifestPath model |> Option.isSome then
                    "skippedExisting", [ DiagnosticsModule.scaffoldToolManifestSkippedExisting toolManifestPath ]
                else
                    "notApplicable", []

        let execResults =
            model.InterpretedEffects
            |> List.filter (fun result -> isSetExecutableEffect result.Effect)

        let executableCount =
            execResults |> List.filter (fun result -> result.Succeeded) |> List.length

        let skippedPaths =
            execResults
            |> List.filter (fun result -> not result.Succeeded)
            |> List.choose (fun result ->
                match result.Effect with
                | SetExecutable path -> Some path
                | _ -> None)
            |> List.sort

        let execDiagnostics =
            if List.isEmpty skippedPaths then
                []
            else
                [ DiagnosticsModule.scaffoldScriptsNotMadeExecutable skippedPaths ]

        let outcomeDiagnostics =
            match outcome with
            | ProviderSucceededEmpty -> [ DiagnosticsModule.scaffoldProviderEmpty descriptor.Name ]
            | _ -> []

        let hint =
            match outcome with
            | ProviderSucceededEmpty -> "Provider produced no files; begin the lifecycle at `charter`."
            | _ -> "SDD skeleton ready; begin the lifecycle at `charter`."

        // 108 / ADR-0054: re-derived (pure) from the same plan TICK A emitted the writes from, so
        // the summary's materialized set and the fail-closed diagnostics agree with what was
        // written and recorded in provenance.
        let driverOutcome = plannedDriverOutcome producedPaths
        // ADR-0063 / FS.GG.SDD#623: re-derived (pure) from the same plan TICK A emitted the owner-skill
        // writes from, so the summary's materialized set and the fail-closed diagnostics agree with
        // what was written and recorded under `gameSkillPaths` in provenance.
        let gameSkillOutcome = plannedGameSkillOutcome producedPaths effective

        let summary: ScaffoldSummary =
            { ProviderName = Some descriptor.Name
              ProviderContractVersion = Some descriptor.ContractVersion
              RequiredMinimumCliVersion = resolvedRequiredMinimumCliVersion descriptor
              Outcome = scaffoldOutcomeValue outcome
              SkeletonCreated = true
              ProviderInvoked = true
              ProducedPathCount = List.length producedPaths
              ProducedPaths = producedPaths
              MirroredPaths = mirroredPaths
              MaterializedDriverPaths = driverOutcome.ProvenancePaths |> List.map fst |> List.sort
              MaterializedGameSkillPaths = gameSkillOutcome.ProvenancePaths |> List.map fst |> List.sort
              EffectiveParameters = Map.toList effective
              RepoInitOutcome = repoInitOutcome
              ToolManifestOutcome = toolManifestOutcome
              ExecutableScriptCount = executableCount
              ExecutableScriptsSkipped = List.length skippedPaths
              NextActionHint = hint
              ProviderInvocation = None }

        summary,
        outcomeDiagnostics
        @ repoInitDiagnostics
        @ toolManifestDiagnostics
        @ execDiagnostics
        @ driverDiagnostics driverOutcome
        @ gameSkillDiagnostics gameSkillOutcome

    // The post-instantiation machine, re-derived from the interpreted-effect log each tick
    // (no new model field). Reached only on a success create outcome. 056 prepends a MIRROR
    // gate (TICK MIRROR) before the probe-based TICK A→C: read each provider `.agents/skills/*`
    // skill and fan its exact body out into `.claude`/`.codex` (no-clobber) before anything
    // finalizes, so an incomplete fan-out never reports complete (FR-005/FR-012).
    let private postInstantiationNext
        model
        (descriptor: ProviderDescriptor)
        outcome
        (producedPaths: string list)
        (effective: Map<string, string>)
        =
        // The mirror record is a pure function of the produced paths (known before the I/O).
        let mirroredPaths = plannedMirroredPaths producedPaths
        let mirrorSources = providerSkillFiles producedPaths
        let readEffects = mirrorSources |> List.map ReadFile

        let readsInterpreted =
            readEffects
            |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

        let readsPlanned =
            readEffects |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

        // TICK MIRROR finalization on a read/write fault: a non-success scaffold at exit 2,
        // provenance recording NO fan-out (FR-012). The scaffold.mirrorFailed id is additive.
        let mirrorFailedFinalize (failedPaths: string list) =
            let summary: ScaffoldSummary =
                { ProviderName = Some descriptor.Name
                  ProviderContractVersion = Some descriptor.ContractVersion
                  RequiredMinimumCliVersion = resolvedRequiredMinimumCliVersion descriptor
                  Outcome = scaffoldOutcomeValue ProviderFailed
                  SkeletonCreated = true
                  ProviderInvoked = true
                  ProducedPathCount = List.length producedPaths
                  ProducedPaths = producedPaths
                  MirroredPaths = []
                  MaterializedDriverPaths = []
                  MaterializedGameSkillPaths = []
                  EffectiveParameters = Map.toList effective
                  RepoInitOutcome = "notApplicable"
                  ToolManifestOutcome = "notApplicable"
                  ExecutableScriptCount = 0
                  ExecutableScriptsSkipped = 0
                  NextActionHint =
                    "The skill fan-out could not be completed; resolve the filesystem issue and re-run scaffold."
                  ProviderInvocation = None }

            let provenanceEffects =
                provenanceWriteEffect model.Request descriptor ProviderFailed producedPaths [] [] [] [] Map.empty effective

            { model with
                PendingEffects = model.PendingEffects @ provenanceEffects
                Scaffold = Some summary
                Diagnostics =
                    model.Diagnostics
                    @ [ DiagnosticsModule.scaffoldMirrorFailed failedPaths ]
                    @ cliCoherenceDiagnostics descriptor model.Request },
            provenanceEffects

        // Gate: run the MIRROR tick(s) to completion before the probe-based TICK A→C.
        // `None` ⇒ mirror complete (proceed); `Some result` ⇒ the mirror is still working or
        // has finalized (return that result directly).
        let mirrorGate =
            if List.isEmpty mirrorSources then
                None
            elif not readsInterpreted then
                if readsPlanned then
                    Some(model, [])
                else
                    Some(
                        { model with
                            PendingEffects = model.PendingEffects @ readEffects },
                        readEffects
                    )
            else
                // Reads interpreted: a missing/unreadable source (snapshot None) is a fault.
                let bodyOf src = snapshot src model

                let readFailures =
                    mirrorSources
                    |> List.filter (fun src -> Option.isNone (bodyOf src))
                    |> List.sort

                if not (List.isEmpty readFailures) then
                    Some(mirrorFailedFinalize readFailures)
                else
                    let writeEffects =
                        mirrorSources
                        |> List.collect (fun src ->
                            let body = (bodyOf src |> Option.get).Text

                            mirrorTargetsFor src
                            |> List.map (fun target -> WriteFile(target, body, AgentGuidanceTarget)))

                    let writesInterpreted =
                        writeEffects
                        |> List.forall (fun effect -> hasInterpreted (effectKey effect) model)

                    let writesPlanned =
                        writeEffects |> List.exists (fun effect -> hasPlanned (effectKey effect) model)

                    if not writesInterpreted then
                        if writesPlanned then
                            Some(model, [])
                        else
                            Some(
                                { model with
                                    PendingEffects = model.PendingEffects @ writeEffects },
                                writeEffects
                            )
                    else
                        let writeFailures =
                            writeEffects
                            |> List.choose (fun effect ->
                                match
                                    model.InterpretedEffects
                                    |> List.tryFind (fun result -> effectKey result.Effect = effectKey effect)
                                with
                                | Some result when not result.Succeeded -> effectPath effect
                                | _ -> None)
                            |> List.sort

                        if not (List.isEmpty writeFailures) then
                            Some(mirrorFailedFinalize writeFailures)
                        else
                            None

        match mirrorGate with
        | Some result -> result
        | None ->

            let probeInterpreted =
                model.InterpretedEffects
                |> List.exists (fun result -> isProbeProcess result.Effect)

            let probePlanned = model.PendingEffects |> List.exists isProbeProcess

            let initInterpreted =
                model.InterpretedEffects
                |> List.exists (fun result -> isInitProcess result.Effect)

            let initPlanned = model.PendingEffects |> List.exists isInitProcess

            // FS.GG.SDD#315 — TICK 0 (read) and TICK 0b (write) both precede TICK A, because the
            // single provenance write must record whether SDD actually owns
            // `.config/dotnet-tools.json`, and that is only knowable once the write is
            // interpreted. Recording ownership from the *plan* would leave provenance attesting
            // to a file a failed write never produced (FR-009: never report incomplete as
            // complete). Both effects still precede `git init`, so FR-004's ordering holds.
            let manifestReadEffect = ReadFile toolManifestPath
            let manifestReadKey = effectKey manifestReadEffect
            let manifestReadInterpreted = hasInterpreted manifestReadKey model
            let manifestReadPlanned = hasPlanned manifestReadKey model

            // Interpreted read + no snapshot ⇒ absent ⇒ SDD writes the pin. Present ⇒ preserve
            // it (no-clobber): either the author placed it there or the provider produced it,
            // and in the latter case it already stands in `producedPaths` as generatedProduct.
            // Planning the write over a present file would instead hand `canOverwrite
            // StructuredSource` a snapshot and be refused as `unsafeOverwrite` — an error, not
            // the graceful preserve the step owes. (A file appearing between the read and the
            // write is still refused rather than clobbered, which is the safe direction.)
            let manifestPresent =
                manifestReadInterpreted && (snapshot toolManifestPath model |> Option.isSome)

            let manifestWriteEffect =
                WriteFile(toolManifestPath, toolManifestText model.Request.GeneratorVersion.Version, StructuredSource)

            let manifestWriteResult =
                model.InterpretedEffects
                |> List.tryFind (fun result -> isToolManifestWrite result.Effect)

            // SDD claims ownership only of a write that actually landed. A refused or failed
            // write leaves `sddOwnedPaths` empty, so provenance and `toolManifestOutcome` agree.
            let sddOwnedPaths =
                match manifestWriteResult with
                | Some result when result.Succeeded -> [ toolManifestPath ]
                | _ -> []

            if not manifestReadInterpreted then
                if manifestReadPlanned then
                    // Read planned, awaiting interpretation.
                    model, []
                else
                    { model with
                        PendingEffects = model.PendingEffects @ [ manifestReadEffect ] },
                    [ manifestReadEffect ]
            elif not manifestPresent && Option.isNone manifestWriteResult then
                // TICK 0b — the manifest is absent: write the pin, then let TICK A record the
                // interpreted result. `hasPlanned` keeps the write from being re-emitted while
                // it awaits interpretation.
                if hasPlanned (effectKey manifestWriteEffect) model then
                    model, []
                else
                    { model with
                        PendingEffects = model.PendingEffects @ [ manifestWriteEffect ] },
                    [ manifestWriteEffect ]
            elif not (probeInterpreted || probePlanned) then
                // TICK A — the success path's single provenance write (FR-004, before `git
                // init`), the work-tree probe, and one SetExecutable per produced `.sh`.
                let scriptEffects =
                    producedPaths
                    |> List.filter (fun path -> path.EndsWith(".sh", StringComparison.Ordinal))
                    |> List.map SetExecutable

                // 058/ADR-0014 §Decision 3: the content-addressed digest of every produced/mirrored
                // skill copy. The reads are interpreted by this tick, so each provider `.agents`
                // skill body is in a snapshot; the mirror copies are byte-identical, so they share
                // the source digest. Non-skill produced paths get no entry (⇒ `Sha256 = None`).
                let skillDigests =
                    mirrorSources
                    |> List.collect (fun src ->
                        match snapshot src model with
                        | Some snap ->
                            let digest = Fsgg.SkillMirror.sha256 snap.Text

                            (src, digest)
                            :: (mirrorTargetsFor src |> List.map (fun target -> target, digest))
                        | None -> [])
                    |> Map.ofList

                // 108 / ADR-0054: the driver materialization — no-clobber writes from the CLI's
                // embedded package bytes (a pure plan; content-addressed verify already ran), with
                // its content-verified paths recorded in provenance under `driverPaths`. Emitted in
                // this same batch as the probe, so they are interpreted before finalize (TICK C).
                let driverOutcome = plannedDriverOutcome producedPaths

                // ADR-0063 / FS.GG.SDD#623: the owner-skill materialization — the same no-clobber
                // embedded-bytes plan as the driver, gated by the effective scaffold parameters, its
                // content-verified paths recorded in provenance under `gameSkillPaths`. Emitted in
                // this same batch so they are interpreted before finalize (TICK C).
                let gameSkillOutcome = plannedGameSkillOutcome producedPaths effective

                let effects =
                    driverOutcome.Writes
                    @ gameSkillOutcome.Writes
                    @ provenanceWriteEffect
                        model.Request
                        descriptor
                        outcome
                        producedPaths
                        mirroredPaths
                        sddOwnedPaths
                        driverOutcome.ProvenancePaths
                        gameSkillOutcome.ProvenancePaths
                        skillDigests
                        effective
                    @ [ RunProcess("git", [ "rev-parse"; "--is-inside-work-tree" ], "") ]
                    @ scriptEffects

                { model with
                    PendingEffects = model.PendingEffects @ effects },
                effects
            elif not probeInterpreted then
                // Probe planned, awaiting interpretation.
                model, []
            else
                let probeProcess =
                    model.InterpretedEffects
                    |> List.tryPick (fun result ->
                        if isProbeProcess result.Effect then
                            result.Process
                        else
                            None)

                let shouldInit =
                    match probeProcess with
                    | Some { Started = true; ExitCode = code } -> code <> 0
                    | _ -> false

                if shouldInit && not (initInterpreted || initPlanned) then
                    // TICK B — not inside a work tree and git is available: initialize.
                    let effect = RunProcess("git", [ "init" ], "")

                    { model with
                        PendingEffects = model.PendingEffects @ [ effect ] },
                    [ effect ]
                elif shouldInit && not initInterpreted then
                    // Init planned, awaiting interpretation.
                    model, []
                else
                    // TICK C — init interpreted or skipped: set the terminal summary once.
                    let summary, diagnostics =
                        finalizePostInstantiation
                            model
                            descriptor
                            outcome
                            producedPaths
                            mirroredPaths
                            probeProcess
                            effective

                    // Feature 052 (US2): merge the non-blocking CLI-coherence advisory on the
                    // success path too, so the advisory appears in every descriptor-resolved outcome.
                    { model with
                        Scaffold = Some summary
                        Diagnostics =
                            model.Diagnostics
                            @ diagnostics
                            @ cliCoherenceDiagnostics descriptor model.Request },
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
            let createInterpreted =
                model.InterpretedEffects
                |> List.exists (fun result -> isCreateProcess result.Effect)

            let createPlanned = model.PendingEffects |> List.exists isCreateProcess

            if not createInterpreted then
                if createPlanned then
                    model, []
                else
                    let effects = scaffoldInvocationEffects model.Request descriptor effective

                    { model with
                        PendingEffects = model.PendingEffects @ effects },
                    effects
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
                        Diagnostics =
                            model.Diagnostics
                            @ diagnostics
                            @ cliCoherenceDiagnostics descriptor model.Request },
                    provenanceEffects
                | FinalizeSuccess(outcome, producedPaths) ->
                    postInstantiationNext model descriptor outcome producedPaths effective
