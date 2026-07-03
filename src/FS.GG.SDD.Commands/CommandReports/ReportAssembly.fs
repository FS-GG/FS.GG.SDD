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
        if diagnostics |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticError) then
            CommandOutcome.Blocked
        elif diagnostics |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticWarning) then
            CommandOutcome.SucceededWithWarnings
        elif List.isEmpty changes then
            CommandOutcome.NoChange
        elif changes |> List.forall (fun change -> change.Operation = ArtifactOperation.NoChange || change.Operation = ArtifactOperation.Preserve) then
            CommandOutcome.NoChange
        else
            CommandOutcome.Succeeded

    let sortChanges (changes: ArtifactChange list) =
        changes
        |> List.sortBy (fun change -> change.Path, artifactOperationValue change.Operation, change.Ownership)

    let sortGovernance (facts: GovernanceCompatibilityFact list) =
        facts |> List.sortBy (fun fact -> fact.Path)

    let buildReport (model: CommandModel) =
        let effectDiagnostics =
            model.InterpretedEffects
            |> List.choose (fun result -> result.Diagnostic)

        let diagnostics =
            model.Diagnostics @ effectDiagnostics
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
            (model.InterpretedEffects |> List.choose (changeFromEffectResult model.Request)) @ scaffoldChanges
            |> sortChanges

        let reportOutcome = outcome diagnostics changes

        { SchemaVersion = 1
          ReportVersion = "1.0.0"
          Command = model.Request.Command
          // Intentionally the literal "." — decoupled from model.Request.ProjectRoot (which may be
          // an absolute/temporary path) so the report JSON stays reproducible/deterministic. Do not
          // echo the request root here (feature 063, FR-007).
          ProjectRoot = "."
          OutputFormat = model.Request.OutputFormat
          DryRun = model.Request.DryRun
          Outcome = reportOutcome
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
          GeneratedViews = model.GeneratedViews |> List.sortBy (fun view -> view.Path)
          Diagnostics = diagnostics
          GovernanceCompatibility = sortGovernance governanceCompatibility
          NextAction = nextAction diagnostics reportOutcome model.Request model.Checklist model.Plan model.Tasks model.Analysis model.Evidence model.Verification model.Ship model.AgentGuidance model.Refresh
          Help = None }

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
    let exitCodeForReport (report: CommandReport) =
        match report.Outcome with
        | CommandOutcome.Blocked ->
            if report.Diagnostics |> List.exists (fun d -> d.IsToolDefect) then 2 else 1
        | CommandOutcome.Succeeded
        | CommandOutcome.SucceededWithWarnings
        | CommandOutcome.NoChange -> 0
