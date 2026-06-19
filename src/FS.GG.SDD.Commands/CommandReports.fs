namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.CommandTypes

module CommandReports =
    module ArtifactRefModule = FS.GG.SDD.Artifacts.ArtifactRef
    module DiagnosticsModule = FS.GG.SDD.Artifacts.Diagnostics
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let artifactForPath (path: string) =
        match ArtifactRefModule.create path (ArtifactKind.Other "command") ArtifactOwner.Sdd true with
        | Ok artifact -> Some artifact
        | Error _ -> None

    let commandDiagnostic id severity path message correction relatedIds =
        DiagnosticsModule.create id severity (path |> Option.bind artifactForPath) None message correction relatedIds

    let unknownCommand (value: string) =
        commandDiagnostic
            "unknownCommand"
            DiagnosticSeverity.DiagnosticError
            None
            $"Unknown SDD command '{value}'."
            "Use one of: init, charter, specify, clarify, checklist, plan, tasks, analyze."
            []

    let malformedWorkId (value: string) =
        commandDiagnostic
            "malformedWorkId"
            DiagnosticSeverity.DiagnosticError
            None
            $"Work id '{value}' is malformed."
            "Use a stable lowercase work id such as 003-native-sdd-lifecycle-commands."
            [ value ]

    let missingWorkId (command: SddCommand) =
        commandDiagnostic
            "missingWorkId"
            DiagnosticSeverity.DiagnosticError
            None
            $"Command '{commandName command}' requires --work."
            "Pass --work <id> for work-item lifecycle commands."
            [ commandName command ]

    let unsupportedCommand (command: SddCommand) =
        commandDiagnostic
            "unsupportedLifecycleCommand"
            DiagnosticSeverity.DiagnosticError
            None
            $"Command '{commandName command}' is declared but not implemented in the current MVP slice."
            "Use fsgg-sdd init in this slice; later lifecycle commands remain pending in tasks.md."
            [ commandName command ]

    let unsafeOverwrite (path: string) =
        commandDiagnostic
            "unsafeOverwrite"
            DiagnosticSeverity.DiagnosticError
            (Some path)
            "The command would overwrite existing authored content."
            "Review the existing file and choose an explicit safe update path before rerunning."
            [ path ]

    let toolDefect (path: string option) (message: string) =
        commandDiagnostic
            "toolDefect"
            DiagnosticSeverity.DiagnosticError
            path
            message
            "Inspect the command failure and fix the tool or environment before rerunning."
            []

    let changeFromEffectResult (result: CommandEffectResult) =
        match result.Effect with
        | CreateDirectory path ->
            Some
                { Path = path
                  Kind = "directory"
                  Ownership = "sdd"
                  Operation = if result.Succeeded then ArtifactOperation.Create else ArtifactOperation.Refuse
                  BeforeDigest = None
                  AfterDigest = None
                  SafeWriteDecision = if result.Succeeded then "safe" else "refused"
                  DiagnosticIds = result.Diagnostic |> Option.map (fun d -> [ d.Id ]) |> Option.defaultValue [] }
        | WriteFile(path, text, kind) ->
            let operation =
                if result.Succeeded then
                    match result.Snapshot with
                    | Some snapshot when snapshot.Text = text -> ArtifactOperation.NoChange
                    | Some _ -> ArtifactOperation.Update
                    | None -> ArtifactOperation.Create
                else
                    ArtifactOperation.Refuse

            let beforeDigest =
                result.Snapshot
                |> Option.map (fun snapshot -> SchemaVersionModule.sha256Text snapshot.Text)

            let afterDigest =
                if result.Succeeded then Some(SchemaVersionModule.sha256Text text) else None

            Some
                { Path = path
                  Kind = writeKindValue kind
                  Ownership = if kind = GeneratedView then "generated" else "authored"
                  Operation = operation
                  BeforeDigest = beforeDigest
                  AfterDigest = afterDigest
                  SafeWriteDecision = if result.Succeeded then "safe" else "refused"
                  DiagnosticIds = result.Diagnostic |> Option.map (fun d -> [ d.Id ]) |> Option.defaultValue [] }
        | ReadFile _
        | EnumerateDirectory _
        | EmitStdout _
        | EmitStderr _
        | SetExitCode _ -> None

    let governanceCompatibility : GovernanceCompatibilityFact list =
        [ { Path = ".fsgg/policy.yml"
            Relationship = "optionalGovernancePolicy"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/capabilities.yml"
            Relationship = "optionalGovernanceCapabilities"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] }
          { Path = ".fsgg/tooling.yml"
            Relationship = "optionalGovernanceTooling"
            RequiredBySdd = false
            State = "notEvaluated"
            DiagnosticIds = [] } ]

    let nextAction (diagnostics: Diagnostic list) (request: CommandRequest) =
        let blocking =
            diagnostics
            |> List.filter (fun diagnostic -> diagnostic.Severity = DiagnosticSeverity.DiagnosticError)
            |> List.map (fun diagnostic -> diagnostic.Id)
            |> List.distinct
            |> List.sort

        if not (List.isEmpty blocking) then
            Some
                { ActionId = "correctBlockingDiagnostics"
                  Command = None
                  WorkId = request.WorkId
                  Reason = "The command is blocked by diagnostics."
                  RequiredArtifacts = []
                  BlockingDiagnosticIds = blocking }
        else
            match nextLifecycleCommand request.Command with
            | Some command ->
                Some
                    { ActionId = "nextLifecycleCommand"
                      Command = Some command
                      WorkId = request.WorkId
                      Reason = $"Command '{commandName request.Command}' completed."
                      RequiredArtifacts = []
                      BlockingDiagnosticIds = [] }
            | None -> None

    let outcome (diagnostics: Diagnostic list) (changes: ArtifactChange list) =
        if diagnostics |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticError) then
            CommandOutcome.Blocked
        elif diagnostics |> List.exists (fun d -> d.Severity = DiagnosticSeverity.DiagnosticWarning) then
            CommandOutcome.SucceededWithWarnings
        elif List.isEmpty changes then
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

        let changes =
            model.InterpretedEffects
            |> List.choose changeFromEffectResult
            |> sortChanges

        { SchemaVersion = 1
          ReportVersion = "1.0.0"
          Command = model.Request.Command
          ProjectRoot = "."
          OutputFormat = model.Request.OutputFormat
          DryRun = model.Request.DryRun
          OverwritePolicy = model.Request.OverwritePolicy
          Outcome = outcome diagnostics changes
          WorkId = model.Request.WorkId
          ChangedArtifacts = changes
          GeneratedViews = []
          Diagnostics = diagnostics
          GovernanceCompatibility = sortGovernance governanceCompatibility
          NextAction = nextAction diagnostics model.Request }

    let exitCodeForReport (report: CommandReport) =
        match report.Outcome with
        | CommandOutcome.Blocked ->
            if report.Diagnostics |> List.exists (fun d -> d.Id = "toolDefect") then 2 else 1
        | CommandOutcome.Succeeded
        | CommandOutcome.SucceededWithWarnings
        | CommandOutcome.NoChange -> 0
