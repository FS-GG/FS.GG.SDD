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
module Evidence =
    type EvidenceKind =
        | Implementation
        | Verification
        | Review
        | GeneratedViewEvidence
        | Synthetic
        | Deferral
        | Note
        | Missing

    type EvidenceSubject = { SubjectType: string; Id: string }

    type EvidenceSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type EvidenceSourceReference =
        { ReferenceId: string option
          Kind: string
          Path: string option
          Uri: string option
          Digest: string option
          RelatedSourceId: string option
          Result: string option
          SourceLocation: SourceLocation option }

    type SyntheticDisclosure =
        { StandsInFor: string
          Reason: string }

    type EvidenceDeclaration =
        { Id: EvidenceId
          Kind: EvidenceKind
          Subject: EvidenceSubject
          TaskRefs: TaskId list
          RequirementRefs: RequirementId list
          AcceptanceScenarioRefs: AcceptanceScenarioId list
          ClarificationDecisionRefs: DecisionId list
          ChecklistResultRefs: ChecklistResultId list
          PlanDecisionRefs: PlanDecisionId list
          ObligationRefs: string list
          ArtifactRefs: ArtifactRef list
          SourceRefs: EvidenceSourceReference list
          Result: string
          Synthetic: bool
          SyntheticDisclosure: SyntheticDisclosure option
          Rationale: string option
          Owner: string option
          Scope: string option
          LaterLifecycleVisibility: string option
          Notes: string list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type EvidenceObligation =
        { ObligationId: string
          Kind: string
          SourceArtifactPath: string
          SourceId: string option
          LinkedTaskIds: TaskId list
          LinkedRequirementIds: RequirementId list
          LinkedDecisionIds: string list
          ExpectedEvidenceKinds: string list
          RequiredSkillOrCapabilityTags: string list
          Blocking: bool
          Correction: string }

    type EvidenceArtifact =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          SourceTasks: string
          SourceAnalysis: string
          SourceSnapshots: EvidenceSourceSnapshot list
          Evidence: EvidenceDeclaration list
          LifecycleNotes: string list
          Diagnostics: Diagnostic list }

    let parseEvidenceKind (value: string) =
        match if String.IsNullOrEmpty value then "" else value.Trim().ToLowerInvariant() with
        | "implementation" -> Implementation
        | "verification" -> Verification
        | "review" -> Review
        | "generated-view" -> GeneratedViewEvidence
        | "generatedview" -> GeneratedViewEvidence
        | "synthetic" -> Synthetic
        | "deferral" -> Deferral
        | "note" -> Note
        | "missing" -> Missing
        | _ -> Verification

    let parseArtifactRefs values =
        values
        |> List.map (fun path -> artifact path (ArtifactKind.Other "evidenceArtifact") ArtifactOwner.Sdd false)

    let parseEvidenceSourceSnapshots root =
        trySequenceAt [ "sourceSnapshots" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun mapping ->
                    { Label = tryScalarAt [ "label" ] mapping |> Option.defaultValue ""
                      Path = tryScalarAt [ "path" ] mapping |> Option.defaultValue ""
                      Digest = tryScalarAt [ "digest" ] mapping
                      SchemaVersion =
                        tryScalarAt [ "schemaVersion" ] mapping
                        |> Option.bind (fun value ->
                            match Int32.TryParse value with
                            | true, parsed -> Some parsed
                            | _ -> None)
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    let parseEvidenceSourceRefs (mapping: YamlMappingNode) =
        trySequenceAt [ "sourceRefs" ] mapping
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun source ->
                    { ReferenceId = tryScalarAt [ "id" ] source
                      Kind = tryScalarAt [ "kind" ] source |> Option.defaultValue "artifact"
                      Path = tryScalarAt [ "path" ] source
                      Uri = tryScalarAt [ "uri" ] source
                      Digest = tryScalarAt [ "digest" ] source
                      RelatedSourceId = tryScalarAt [ "relatedSourceId" ] source
                      Result = tryScalarAt [ "result" ] source
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    let parseSyntheticDisclosure (mapping: YamlMappingNode) =
        match tryScalarAt [ "syntheticDisclosure"; "standsInFor" ] mapping, tryScalarAt [ "syntheticDisclosure"; "reason" ] mapping with
        | Some standsInFor, Some reason when not (String.IsNullOrWhiteSpace standsInFor) && not (String.IsNullOrWhiteSpace reason) ->
            Some { StandsInFor = standsInFor; Reason = reason }
        | _ -> None

    let workIdFromEvidencePath (path: string) =
        let normalized = normalizePath path
        let parts = normalized.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 3 && parts.[0] = "work" then parts.[1] else "unknown-work"

    let parseEvidenceArtifact (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Evidence

        match parseYaml snapshot.Text with
        | None -> Error [ Diagnostics.malformedSchemaVersion artifact "Evidence file is empty." ]
        | Some root ->
            let version, versionDiagnostics = schemaVersion artifact root
            let workIdValue = tryScalarAt [ "workId" ] root |> Option.defaultValue (workIdFromEvidencePath snapshot.Path)
            let workId = Identifiers.createWorkId workIdValue
            let stage = tryScalarAt [ "stage" ] root |> Option.bind (Identifiers.parseStage >> Result.toOption) |> Option.defaultValue LifecycleStage.Evidence

            let evidence =
                trySequenceAt [ "evidence" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.mapi (fun index node ->
                        node
                        |> tryMapping
                        |> Option.bind (fun mapping ->
                            tryScalarAt [ "id" ] mapping
                            |> Option.bind (Identifiers.createEvidenceId >> Result.toOption)
                            |> Option.map (fun id ->
                                let subjectType = tryScalarAt [ "subject"; "type" ] mapping |> Option.defaultValue "task"
                                let subjectId = tryScalarAt [ "subject"; "id" ] mapping |> Option.defaultValue ""
                                let taskRefs =
                                    match subjectType with
                                    | "task" -> subjectId :: scalarList [ "taskRefs" ] mapping
                                    | _ -> scalarList [ "taskRefs" ] mapping
                                    |> parseTaskIds

                                let requirementRefs =
                                    match subjectType with
                                    | "requirement" -> subjectId :: scalarList [ "requirementRefs" ] mapping
                                    | _ -> scalarList [ "requirementRefs" ] mapping
                                    |> parseRequirementIds

                                { Id = id
                                  Kind = tryScalarAt [ "kind" ] mapping |> Option.map parseEvidenceKind |> Option.defaultValue Verification
                                  Subject = { SubjectType = subjectType; Id = subjectId }
                                  TaskRefs = taskRefs
                                  RequirementRefs = requirementRefs
                                  AcceptanceScenarioRefs = scalarList [ "acceptanceScenarioRefs" ] mapping |> parseAcceptanceScenarioIds
                                  ClarificationDecisionRefs = scalarList [ "clarificationDecisionRefs" ] mapping |> parseDecisionIds
                                  ChecklistResultRefs = scalarList [ "checklistResultRefs" ] mapping |> parseChecklistResultIds
                                  PlanDecisionRefs = scalarList [ "planDecisionRefs" ] mapping |> parsePlanDecisionIds
                                  ObligationRefs = scalarList [ "obligationRefs" ] mapping |> List.distinct |> List.sort
                                  ArtifactRefs = scalarList [ "artifacts" ] mapping |> parseArtifactRefs
                                  SourceRefs = parseEvidenceSourceRefs mapping
                                  Result = tryScalarAt [ "result" ] mapping |> Option.defaultValue "pending"
                                  Synthetic = boolAt [ "synthetic" ] mapping false
                                  SyntheticDisclosure = parseSyntheticDisclosure mapping
                                  Rationale = tryScalarAt [ "rationale" ] mapping
                                  Owner = tryScalarAt [ "owner" ] mapping
                                  Scope = tryScalarAt [ "scope" ] mapping
                                  LaterLifecycleVisibility = tryScalarAt [ "laterLifecycleVisibility" ] mapping
                                  Notes = scalarList [ "notes" ] mapping
                                  Source = artifact
                                  SourceLocation = sourceLocation (index + 1) })))
                    |> Seq.choose id
                    |> Seq.toList)
                |> Option.defaultValue []

            let duplicateDiagnostics =
                evidence
                |> List.groupBy (fun declaration -> declaration.Id.Value)
                |> List.choose (fun (id, declarations) ->
                    if List.length declarations > 1 then
                        Some(Diagnostics.duplicateIdentifier artifact id (declarations |> List.choose (fun declaration -> declaration.SourceLocation)))
                    else
                        None)

            let artifactDiagnostics =
                [ if stage <> LifecycleStage.Evidence then
                      Diagnostics.workModelInconsistent artifact $"Evidence stage '{Identifiers.stageValue stage}' is not 'evidence'." "Set stage: evidence before rerunning." [ Identifiers.stageValue stage ] ]

            match version, workId, versionDiagnostics with
            | Some schema, Ok workId, [] ->
                Ok
                    { SchemaVersion = schema
                      WorkId = workId
                      Stage = stage
                      Status = tryScalarAt [ "status" ] root |> Option.defaultValue "draft"
                      SourceSpec = tryScalarAt [ "sourceSpec" ] root |> Option.defaultValue $"work/{workId.Value}/spec.md"
                      SourceClarifications = tryScalarAt [ "sourceClarifications" ] root |> Option.defaultValue $"work/{workId.Value}/clarifications.md"
                      SourceChecklist = tryScalarAt [ "sourceChecklist" ] root |> Option.defaultValue $"work/{workId.Value}/checklist.md"
                      SourcePlan = tryScalarAt [ "sourcePlan" ] root |> Option.defaultValue $"work/{workId.Value}/plan.md"
                      SourceTasks = tryScalarAt [ "sourceTasks" ] root |> Option.defaultValue $"work/{workId.Value}/tasks.yml"
                      SourceAnalysis = tryScalarAt [ "sourceAnalysis" ] root |> Option.defaultValue $"readiness/{workId.Value}/analysis.json"
                      SourceSnapshots = parseEvidenceSourceSnapshots root
                      Evidence = evidence |> List.sortBy (fun declaration -> declaration.Id.Value)
                      LifecycleNotes = scalarList [ "lifecycleNotes" ] root
                      Diagnostics = duplicateDiagnostics @ artifactDiagnostics |> Diagnostics.sort }
            | _ ->
                let workIdDiagnostics =
                    match workId with
                    | Error message -> [ Diagnostics.workModelInconsistent artifact message "Use a valid work id in evidence.yml." [ workIdValue ] ]
                    | Ok _ -> []

                Error
                    (versionDiagnostics
                     @ duplicateDiagnostics
                     @ workIdDiagnostics)

    let parseEvidence (snapshot: FileSnapshot) =
        parseEvidenceArtifact snapshot |> Result.map (fun artifact -> artifact.Evidence)
