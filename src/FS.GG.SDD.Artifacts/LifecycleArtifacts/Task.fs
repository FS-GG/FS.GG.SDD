namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module Task =
    type TaskFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          PublicOrToolFacingImpact: bool option }

    type TaskSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type TaskGraphFinding =
        { FindingId: string
          Severity: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type TaskStatus =
        | Pending
        | InProgress
        | Done
        | Skipped of string
        | Stale

    type WorkTask =
        { Id: TaskId
          Title: string
          Status: TaskStatus
          Owner: string
          Dependencies: TaskId list
          Requirements: RequirementId list
          Decisions: DecisionId list
          SourceIds: string list
          RequiredSkills: string list
          RequiredEvidence: EvidenceId list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type TaskFacts =
        { FrontMatter: TaskFrontMatter
          SourceSnapshots: TaskSourceSnapshot list
          Tasks: WorkTask list
          AcceptedDeferrals: string list
          Findings: TaskGraphFinding list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleTaskCount: int
          Diagnostics: Diagnostic list }

    let parseTaskStatus (value: string) =
        match
            if String.IsNullOrEmpty value then
                ""
            else
                value.Trim().ToLowerInvariant()
        with
        | "pending" -> Pending
        | "in-progress" -> InProgress
        | "done" -> Done
        | "skipped" -> Skipped "No rationale provided."
        | "stale" -> Stale
        | _ -> Pending

    let taskStatusSourceValue status =
        match status with
        | Pending -> "pending"
        | InProgress -> "in-progress"
        | Done -> "done"
        | Skipped _ -> "skipped"
        | TaskStatus.Stale -> "stale"

    let workIdFromTaskPath (path: string) =
        let normalized = normalizePath path
        let parts = normalized.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 3 && parts.[0] = "work" then
            parts.[1]
        else
            "unknown-work"

    let parseTaskFrontMatter (artifact: ArtifactRef) (snapshot: FileSnapshot) (root: YamlNode) version =
        let workNode = tryNodeAt [ "work" ] root |> Option.defaultValue root
        let defaultWorkId = workIdFromTaskPath snapshot.Path
        let workIdValue = tryScalarAt [ "id" ] workNode |> Option.defaultValue defaultWorkId

        let workId =
            Identifiers.createWorkId workIdValue
            |> Result.toOption
            |> Option.defaultValue { Value = workIdValue }

        let stage =
            tryScalarAt [ "stage" ] workNode
            |> Option.bind (Identifiers.parseStage >> Result.toOption)
            |> Option.defaultValue LifecycleStage.Tasks

        { SchemaVersion = version
          WorkId = workId
          Title = tryScalarAt [ "title" ] workNode |> Option.defaultValue workId.Value
          Stage = stage
          Status = tryScalarAt [ "status" ] workNode |> Option.defaultValue "tasksReady"
          SourceSpec =
            tryScalarAt [ "sourceSpec" ] workNode
            |> Option.defaultValue $"work/{workId.Value}/spec.md"
          SourceClarifications =
            tryScalarAt [ "sourceClarifications" ] workNode
            |> Option.defaultValue $"work/{workId.Value}/clarifications.md"
          SourceChecklist =
            tryScalarAt [ "sourceChecklist" ] workNode
            |> Option.defaultValue $"work/{workId.Value}/checklist.md"
          SourcePlan =
            tryScalarAt [ "sourcePlan" ] workNode
            |> Option.defaultValue $"work/{workId.Value}/plan.md"
          PublicOrToolFacingImpact = boolScalarAt [ "publicOrToolFacingImpact" ] workNode }

    let parseTaskSourceSnapshots root : TaskSourceSnapshot list =
        trySequenceAt [ "sources" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.bind (fun mapping ->
                    match tryScalarAt [ "label" ] mapping, tryScalarAt [ "path" ] mapping with
                    | Some label, Some path ->
                        let schemaVersion =
                            tryScalarAt [ "schemaVersion" ] mapping
                            |> Option.bind (fun value ->
                                match Int32.TryParse value with
                                | true, parsed -> Some parsed
                                | _ -> None)

                        Some(
                            { Label = label
                              Path = normalizePath path
                              Digest =
                                tryScalarAt [ "digest" ] mapping
                                |> Option.map (fun value -> value.ToLowerInvariant())
                              SchemaVersion = schemaVersion
                              SourceLocation = sourceLocation (index + 1) }
                            : TaskSourceSnapshot
                        )
                    | _ -> None))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue ([]: TaskSourceSnapshot list)

    let parseTaskFindings root =
        trySequenceAt [ "findings" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun mapping ->
                    let id =
                        tryScalarAt [ "id" ] mapping
                        |> Option.defaultValue (sprintf "TF-%03d" (index + 1))

                    { FindingId = id
                      Severity = tryScalarAt [ "severity" ] mapping |> Option.defaultValue "warning"
                      Text = tryScalarAt [ "text" ] mapping |> Option.defaultValue id
                      SourceIds = scalarList [ "sourceIds" ] mapping
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    let taskSchemaDiagnostics artifact (tasks: WorkTask list) =
        let duplicateTasks =
            duplicateScopedDiagnostics
                artifact
                (fun (id: TaskId) -> id.Value)
                (tasks |> List.map (fun task -> task.Id, task.SourceLocation))

        duplicateTasks
        |> List.map (fun diagnostic ->
            Diagnostics.workModelInconsistent
                artifact
                diagnostic.Message
                "Use each task id only once in tasks.yml."
                diagnostic.RelatedIds)

    let parseTaskFacts (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Tasks

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Tasks file is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root

            match version, versionDiagnostics with
            | Some schema, [] ->
                let frontMatter = parseTaskFrontMatter artifact snapshot root schema

                // Each task node yields (Task option, diagnostics). Malformed cross-references
                // are surfaced as blocking diagnostics instead of being silently dropped by the
                // parse*Ids helpers, and a whole entry skipped for a malformed id is diagnosed
                // rather than vanishing (#70/§2.5).
                let taskParse =
                    trySequenceAt [ "tasks" ] root
                    |> Option.map (fun sequence ->
                        sequence.Children
                        |> Seq.mapi (fun index node ->
                            match node |> tryMapping with
                            | None -> None, []
                            | Some mapping ->
                                match tryScalarAt [ "id" ] mapping with
                                | None -> None, []
                                | Some rawId ->
                                    let refDiagnostics =
                                        [ scalarList [ "dependencies" ] mapping
                                          |> malformedRefs Identifiers.createTaskId
                                          |> List.map (Diagnostics.malformedReference artifact "task dependency")
                                          scalarList [ "requirements" ] mapping
                                          |> malformedRefs Identifiers.createRequirementId
                                          |> List.map (Diagnostics.malformedReference artifact "requirement")
                                          scalarList [ "decisions" ] mapping
                                          |> malformedRefs Identifiers.createDecisionId
                                          |> List.map (Diagnostics.malformedReference artifact "decision")
                                          scalarList [ "requiredEvidence" ] mapping
                                          |> malformedRefs Identifiers.createEvidenceId
                                          |> List.map (Diagnostics.malformedReference artifact "evidence") ]
                                        |> List.concat

                                    match Identifiers.createTaskId rawId with
                                    | Error _ ->
                                        None, (Diagnostics.malformedReference artifact "task" rawId :: refDiagnostics)
                                    | Ok id ->
                                        let status =
                                            tryScalarAt [ "status" ] mapping
                                            |> Option.map parseTaskStatus
                                            |> Option.defaultValue Pending

                                        let skipRationale = tryScalarAt [ "skipRationale" ] mapping

                                        let status =
                                            match status, skipRationale with
                                            | Skipped _, Some rationale -> Skipped rationale
                                            | _ -> status

                                        Some
                                            { Id = id
                                              Title =
                                                tryScalarAt [ "title" ] mapping
                                                |> Option.defaultValue (Identifiers.taskIdValue id)
                                              Status = status
                                              Owner =
                                                tryScalarAt [ "owner" ] mapping |> Option.defaultValue "unassigned"
                                              Dependencies = scalarList [ "dependencies" ] mapping |> parseTaskIds
                                              Requirements =
                                                scalarList [ "requirements" ] mapping |> parseRequirementIds
                                              Decisions = scalarList [ "decisions" ] mapping |> parseDecisionIds
                                              SourceIds =
                                                scalarList [ "sourceIds" ] mapping
                                                |> List.map (fun value -> value.ToUpperInvariant())
                                                |> List.distinct
                                                |> List.sort
                                              RequiredSkills = scalarList [ "requiredSkills" ] mapping
                                              RequiredEvidence =
                                                scalarList [ "requiredEvidence" ] mapping |> parseEvidenceIds
                                              Source = artifact
                                              SourceLocation = sourceLocation (index + 1) },
                                        refDiagnostics)
                        |> Seq.toList)
                    |> Option.defaultValue []

                let tasks = taskParse |> List.choose fst
                let referenceDiagnostics = taskParse |> List.collect snd

                let acceptedDeferrals = scalarList [ "acceptedDeferrals" ] root
                let advisoryNotes = scalarList [ "advisoryNotes" ] root
                let lifecycleNotes = scalarList [ "lifecycleNotes" ] root
                let findings = parseTaskFindings root

                let staleCount =
                    tasks |> List.filter (fun task -> task.Status = TaskStatus.Stale) |> List.length

                let diagnostics =
                    (taskSchemaDiagnostics artifact tasks @ referenceDiagnostics)
                    |> Diagnostics.sort

                Ok
                    { FrontMatter = frontMatter
                      SourceSnapshots =
                        parseTaskSourceSnapshots root
                        |> List.sortBy (fun snapshot -> snapshot.Label, snapshot.Path)
                      Tasks = tasks |> List.sortBy (fun task -> task.Id.Value)
                      AcceptedDeferrals = acceptedDeferrals |> List.sort
                      Findings = findings |> List.sortBy (fun finding -> finding.FindingId)
                      AdvisoryNotes = advisoryNotes |> List.sort
                      LifecycleNotes = lifecycleNotes
                      StaleTaskCount = staleCount
                      Diagnostics = diagnostics }
            | _ -> Error versionDiagnostics

    // NOT derived (#164). Unioning `requirements`/`decisions` into `SourceIds` here was tried and
    // deferred: `SourceIds` is what `taskValidationDiagnostics.unknownSources` gates on, so the union
    // silently subjects the typed ref fields to a validation they never had — an existing workspace
    // whose hand-authored `requirements: [FR-007]` names an id since dropped from spec.md goes from
    // green to exit 1 with no `schemaVersion` signal. It also widens `existingSources` (derivation
    // suppression) and `derivedCoverage` (prior-task orphan deletion) in the re-generation merge.
    // The blindness it was meant to fix is real — `evidence` reads only `SourceIds`, while the shipped
    // example authors only typed refs — but the fix belongs at those consumers, not at the parser.
    // Tracked separately; see the follow-up to FS.GG.SDD#164.
    let parseTasks (snapshot: FileSnapshot) =
        parseTaskFacts snapshot |> Result.map (fun facts -> facts.Tasks)
