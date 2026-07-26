namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.Serialization
open FS.GG.SDD.Artifacts.WorkModel
open FS.GG.SDD.Commands.Internal.Foundation

/// Pure evidence mutation policy. Handlers coordinate prerequisites and effects; this
/// service owns evidence identity, source currency, and obligation derivation.
module internal EvidenceDomain =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let sourceSnapshot label path text : EvidenceSourceSnapshot =
        { Label = label
          Path = path
          Digest = Some((SchemaVersionModule.sha256Text text).Value)
          SchemaVersion = Some 1
          SourceLocation = None }

    let currentSourceSnapshots workId specText clarificationText checklistText planText tasksText analysisText =
        [ sourceSnapshot "spec" (specPath workId) specText
          sourceSnapshot "clarifications" (clarificationPath workId) clarificationText
          sourceSnapshot "checklist" (checklistPath workId) checklistText
          sourceSnapshot "plan" (planPath workId) planText
          sourceSnapshot "tasks" (tasksPath workId) tasksText
          sourceSnapshot "analysis" (analysisPath workId) analysisText ]

    let sourceSnapshotStale (current: EvidenceSourceSnapshot list) (recorded: EvidenceSourceSnapshot list) =
        let currentMap =
            current
            |> List.choose (fun snapshot -> snapshot.Digest |> Option.map (fun digest -> snapshot.Path, digest))
            |> Map.ofList

        recorded
        |> List.exists (fun snapshot ->
            match snapshot.Digest, Map.tryFind snapshot.Path currentMap with
            | Some recordedDigest, Some currentDigest ->
                not (String.Equals(recordedDigest, currentDigest, StringComparison.OrdinalIgnoreCase))
            | Some _, None -> true
            | _ -> false)

    let declarationMeaningKey (declaration: EvidenceDeclaration) =
        (evidenceKindSourceValue declaration.Kind,
         declaration.Subject.SubjectType,
         declaration.Subject.Id,
         declaration.TaskRefs |> List.map _.Value |> List.sort,
         declaration.RequirementRefs |> List.map _.Value |> List.sort,
         declaration.ObligationRefs |> List.sort,
         declaration.SourceRefs
         |> List.map (fun source -> source.Kind, source.Path, source.Uri, source.Result)
         |> List.sort,
         normalizedEvidenceResult declaration.Result,
         declaration.Synthetic,
         declaration.SyntheticDisclosure
         |> Option.map (fun disclosure -> disclosure.StandsInFor, disclosure.Reason),
         declaration.Rationale,
         declaration.Owner,
         declaration.Scope,
         declaration.LaterLifecycleVisibility)

    let obligations (taskFacts: TaskFacts) : EvidenceObligation list =
        taskFacts.Tasks
        |> List.collect (fun task ->
            let ids =
                if List.isEmpty task.RequiredEvidence && task.Status = TaskStatus.Done then
                    [ $"task.{task.Id.Value}.completion" ]
                else
                    task.RequiredEvidence |> List.map _.Value

            ids
            |> List.map (fun id ->
                { ObligationId = id
                  Kind = "taskEvidence"
                  SourceArtifactPath = task.Source.Path
                  SourceId = Some task.Id.Value
                  LinkedTaskIds = [ task.Id ]
                  LinkedRequirementIds = task.Requirements
                  LinkedDecisionIds = task.Decisions |> List.map _.Value
                  LinkedSourceIds = task.SourceIds
                  ExpectedEvidenceKinds = [ "implementation"; "verification"; "deferral"; "synthetic" ]
                  RequiredEvidenceKinds =
                    if
                        isGameplayTestTagged task.RequiredSkills
                        || isProductionJourneyTagged task.RequiredSkills
                    then
                        realTestEvidenceKinds
                    else
                        []
                  RequiredSkillOrCapabilityTags = task.RequiredSkills
                  Blocking = true
                  Correction =
                    $"Add evidence {id} for {task.Id.Value} with result: pass and synthetic: false (a synthetic pass does not satisfy it), or an accepted deferral linked to {task.Id.Value}." }))
        |> List.groupBy _.ObligationId
        |> List.map (fun (_, group) ->
            { List.head group with
                LinkedTaskIds = group |> List.collect _.LinkedTaskIds |> List.distinct
                LinkedRequirementIds = group |> List.collect _.LinkedRequirementIds |> List.distinct
                LinkedDecisionIds = group |> List.collect _.LinkedDecisionIds |> List.distinct
                LinkedSourceIds = group |> List.collect _.LinkedSourceIds |> List.distinct
                RequiredSkillOrCapabilityTags = group |> List.collect _.RequiredSkillOrCapabilityTags |> List.distinct
                RequiredEvidenceKinds = group |> List.collect _.RequiredEvidenceKinds |> List.distinct })
