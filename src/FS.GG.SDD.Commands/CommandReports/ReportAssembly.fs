namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

[<AutoOpen>]
module internal ReportAssembly =
    module ArtifactRefModule = FS.GG.SDD.Artifacts.ArtifactRef
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics

    let outcome (diagnostics: Diagnostic list) (changes: ArtifactChange list) =
        if
            diagnostics
            |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticError)
        then
            CommandOutcome.Blocked
        elif
            diagnostics
            |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticWarning)
        then
            CommandOutcome.SucceededWithWarnings
        elif List.isEmpty changes then
            CommandOutcome.NoChange
        elif
            changes
            |> List.forall (fun change ->
                change.Operation = ArtifactOperation.NoChange
                || change.Operation = ArtifactOperation.Preserve)
        then
            CommandOutcome.NoChange
        else
            CommandOutcome.Succeeded

    // The positive "clean, advance" signal for FS-GG/FS.GG.SDD#183. `resolveOutcome` collapses two
    // distinct situations to `NoChange`: a bare no-op (no changes recorded at all) and a clean re-run
    // (the command evaluated its artifacts and found every one already current — all changes
    // NoChange/Preserve). Only the second means "everything is already coherent, advance". Because
    // `NoChange` with a non-empty change set can arise *only* from the all-NoChange/Preserve branch
    // above (any real Create/Update/Delete yields `Succeeded`; any error/warning pre-empts `NoChange`),
    // the discriminator is exactly: outcome is `NoChange` and at least one change was recorded.
    let coherent (reportOutcome: CommandOutcome) (changes: ArtifactChange list) =
        reportOutcome = CommandOutcome.NoChange && not (List.isEmpty changes)

    let sortChanges (changes: ArtifactChange list) =
        changes
        |> List.sortBy (fun change -> change.Path, artifactOperationValue change.Operation, change.Ownership)

    let sortGovernance (facts: GovernanceCompatibilityFact list) =
        facts |> List.sortBy (fun fact -> fact.Path)

    // FS-GG/FS.GG.SDD#305: the workspace-declared tool floor, checked once here rather than per handler,
    // so every command that reads .fsgg/project.yml warns uniformly. A command that never reads the
    // config (init on an empty directory, scaffold) simply finds no snapshot and warns nothing.
    //
    // A malformed project.yml yields no floor and no diagnostic: the handler that required the config
    // already reported the parse failure, and re-reporting it here would double-count it.
    let private minToolVersionDiagnostics (model: CommandModel) =
        let projectConfigSnapshot =
            model.InterpretedEffects
            |> List.tryPick (fun result ->
                match result.Effect with
                | ReadFile path when path = ".fsgg/project.yml" -> result.Snapshot
                | _ -> None)

        let declaredFloor =
            projectConfigSnapshot
            |> Option.bind (fun snapshot ->
                match FS.GG.SDD.Artifacts.Config.parseProjectConfig snapshot with
                | Ok config -> config.MinToolVersion
                | Error _ -> None)

        match declaredFloor with
        | None -> []
        | Some floor ->
            let installed = model.Request.GeneratorVersion.Version

            match Fsgg.Version.tryParse floor with
            | None -> [ projectMinToolVersionUnparseable floor ]
            | Some _ ->
                // `compare` returns None when either side is unparseable. The floor already parsed, so a
                // None here means the *installed* version did — it comes from the assembly, not the
                // workspace, and there is nothing an author could fix. Degrade to silence.
                match Fsgg.Version.compare installed floor with
                | Some rank when rank < 0 -> [ projectToolVersionBelowMinimum installed floor ]
                | _ -> []

    let buildReport (model: CommandModel) =
        let effectDiagnostics =
            model.InterpretedEffects |> List.choose (fun result -> result.Diagnostic)

        let diagnostics =
            model.Diagnostics @ effectDiagnostics @ minToolVersionDiagnostics model
            |> DiagnosticsModule.sort

        // Produced provider files are not SDD write effects; they are discovered by
        // the scaffold diff and recorded as externally-owned change entries so the
        // artifact ledger is complete (data-model §5).
        let scaffoldChanges =
            model.Scaffold
            |> Option.map (fun summary ->
                summary.ProducedPaths
                |> List.map (fun path ->
                    { Path = path
                      Kind = "product"
                      Ownership = ArtifactOwner.GeneratedProduct |> ArtifactRefModule.ownerValue
                      Operation = ArtifactOperation.Create
                      BeforeDigest = None
                      AfterDigest = None
                      SafeWriteDecision = "externalProvider"
                      DiagnosticIds = [] }))
            |> Option.defaultValue []

        let changes =
            (model.InterpretedEffects |> List.choose (changeFromEffectResult model.Request))
            @ scaffoldChanges
            |> sortChanges

        let reportOutcome = outcome diagnostics changes

        { SchemaVersion = 1
          // Additive optional command blocks/fields bump the semantic reportVersion one minor while
          // `schemaVersion` stays Stable (1): 1.1.0 added `lifecycleStatus` (feature 084); 1.2.0
          // adds `surface` (feature 086); 1.3.0 adds `surface.classification` (feature 087).
          // A *removal* forces a major bump (versioning-policy.md, "Change class to bump rule"):
          // feature 093 (FS-GG/FS.GG.SDD#164) removed `specification.unresolvedAmbiguityCount`, so
          // reportVersion goes 1.3.0 -> 2.0.0. 2.1.0 then adds `surface.versionBump` (feature 094).
          // 2.2.0 adds the top-level `coherent` fact (FS-GG/FS.GG.SDD#183). 2.3.0 adds the top-level
          // `toolVersion` fact (FS-GG/FS.GG.SDD#305). 2.4.0 adds `scaffold.toolManifestOutcome`
          // (FS-GG/FS.GG.SDD#315). 2.5.0 adds `doctor.requiredMinimumCliVersionSource`
          // (FS-GG/FS.GG.SDD#313).
          ReportVersion = "2.5.0"
          // The version of the CLI that produced this report, so a stale toolchain is legible in the
          // artifact rather than only in the shell that ran it (FS-GG/FS.GG.SDD#305). Same source as
          // `fsgg-sdd --version`, injected into the request at the CLI edge.
          ToolVersion = model.Request.GeneratorVersion.Version
          Command = model.Request.Command
          // Intentionally the literal "." — decoupled from model.Request.ProjectRoot (which may be
          // an absolute/temporary path) so the report JSON stays reproducible/deterministic. Do not
          // echo the request root here (feature 063, FR-007).
          ProjectRoot = "."
          OutputFormat = model.Request.OutputFormat
          DryRun = model.Request.DryRun
          Outcome = reportOutcome
          Coherent = coherent reportOutcome changes
          WorkId = model.Request.WorkId
          ChangedArtifacts = changes
          Specification = model.Specification
          Clarification = model.Clarification
          Checklist = model.Checklist
          Plan = model.Plan
          Tasks = model.Tasks
          Analysis = model.Analysis
          Evidence = model.Evidence
          Verification = model.Verification
          Ship = model.Ship
          AgentGuidance = model.AgentGuidance
          Refresh = model.Refresh
          Scaffold = model.Scaffold
          Doctor = model.Doctor
          Upgrade = model.Upgrade
          Lint = model.Lint
          Surface = model.Surface
          DependencySurface = model.DependencySurface
          GeneratedViews = model.GeneratedViews |> List.sortBy (fun view -> view.Path)
          Diagnostics = diagnostics
          GovernanceCompatibility = sortGovernance governanceCompatibility
          NextAction =
            nextAction
                diagnostics
                reportOutcome
                model.Request
                model.Checklist
                model.Plan
                model.Tasks
                model.Analysis
                model.Evidence
                model.Verification
                model.Ship
                model.AgentGuidance
                model.Refresh
                model.Doctor
                model.Upgrade
          Help = None
          // Feature 084: sensed from the interpreted stage-artifact reads; pure fold, no I/O here.
          LifecycleStatus =
            LifecycleSensing.deriveFromEffects
                model.Request.Command
                model.Request.WorkId
                reportOutcome
                model.InterpretedEffects }

    /// §3.5: build the informational help report. Help carries no diagnostics and no changed
    /// artifacts → `NoChange` → exit 0, routed to stdout. `Help` is populated; `NextAction`
    /// is dropped (help is a discoverability surface, not a lifecycle step).
    let helpReport (request: CommandRequest) (summary: HelpSummary) =
        let model =
            { Request = request
              PendingEffects = []
              InterpretedEffects = []
              Diagnostics = []
              Specification = None
              Clarification = None
              Checklist = None
              Plan = None
              Tasks = None
              Analysis = None
              Evidence = None
              Verification = None
              Ship = None
              AgentGuidance = None
              Refresh = None
              Scaffold = None
              Doctor = None
              Upgrade = None
              Lint = None
              Surface = None
              DependencySurface = None
              GeneratedViews = []
              Report = None }

        { buildReport model with
            Help = Some summary
            NextAction = None }

    // A blocked command escalates to exit 2 (the tool-defect class) when any diagnostic
    // carries the typed `IsToolDefect` bit (set at construction via `markToolDefect`);
    // malformed user input stays at exit 1. This replaces the old hand-maintained
    // `providerDefectIds` id set, so a new defect diagnostic escalates without a second
    // registration (feature 062).
    // Caveat: this shared "exit 2 = tool defect" doctrine holds for every command *except*
    // `lint` / `<stage> --explain`, whose feature-076 pre-flight uses a bespoke polarity
    // (0 clean / 1 defects / 2 unusable input) applied in `Program.fs` ahead of this
    // mapping — there, exit 2 means unusable input, not a tool defect. See docs/reference/lint.md.
    let exitCodeForReport (report: CommandReport) =
        match report.Outcome with
        | CommandOutcome.Blocked ->
            if report.Diagnostics |> List.exists (fun d -> d.IsToolDefect) then
                2
            else
                1
        | CommandOutcome.Succeeded
        | CommandOutcome.SucceededWithWarnings
        | CommandOutcome.NoChange -> 0
