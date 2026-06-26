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
module Verify =
    type EvidenceDispositionState =
        | EvidenceSupported
        | EvidenceDeferred
        | EvidenceMissingDisposition
        | EvidenceStale
        | EvidenceSyntheticDisposition
        | EvidenceInvalid
        | EvidenceAdvisory
        | EvidenceBlocking

    type EvidenceDisposition =
        { DispositionId: string
          ObligationId: string
          State: EvidenceDispositionState
          EvidenceIds: EvidenceId list
          AffectedTaskIds: TaskId list
          AffectedSourceIds: string list
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type RequiredTestDispositionState =
        | TestSatisfied
        | TestDeferred
        | TestMissingDisposition
        | TestStale
        | TestSyntheticDisposition
        | TestInvalid
        | TestAdvisory
        | TestBlocking

    type RequiredTestDisposition =
        { DispositionId: string
          ObligationId: string
          State: RequiredTestDispositionState
          EvidenceIds: EvidenceId list
          AffectedTaskIds: TaskId list
          AffectedRequirementIds: RequirementId list
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type SkillVisibilityState =
        | SkillVisible
        | SkillMissing

    type SkillVisibilityFact =
        { Skill: string
          RequiringTaskIds: TaskId list
          Visibility: SkillVisibilityState
          SourceArtifactPath: string
          Severity: string
          DiagnosticIds: string list
          Correction: string }

    type VerificationFinding =
        { Id: string
          Severity: string
          Category: string
          Path: string
          RelatedIds: string list
          Message: string
          Correction: string }

    type VerificationStageReadiness = { Stage: string; Status: string }

    type VerificationLifecycleReadiness =
        { Stages: VerificationStageReadiness list
          Status: string }

    type VerificationTaskGraphReadiness =
        { TaskCount: int
          DependencyCount: int
          DependenciesValid: bool
          StatusesValid: bool
          FindingIds: string list }

    type VerificationView =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          Generator: string
          Sources: AnalysisSourceRecord list
          LifecycleReadiness: VerificationLifecycleReadiness
          TaskGraph: VerificationTaskGraphReadiness
          EvidenceDispositions: EvidenceDisposition list
          TestDispositions: RequiredTestDisposition list
          SkillVisibility: SkillVisibilityFact list
          GeneratedViews: AnalysisGeneratedViewRecord list
          Findings: VerificationFinding list
          OptionalBoundaryFacts: AnalysisOptionalBoundaryFact list
          Diagnostics: Diagnostic list
          Readiness: string }

    let evidenceDispositionStateFromString (value: string) =
        match (if String.IsNullOrEmpty value then "" else value).Trim().ToLowerInvariant() with
        | "supported" -> EvidenceSupported
        | "deferred" -> EvidenceDeferred
        | "missing" -> EvidenceMissingDisposition
        | "stale" -> EvidenceStale
        | "synthetic" -> EvidenceSyntheticDisposition
        | "invalid" -> EvidenceInvalid
        | "advisory" -> EvidenceAdvisory
        | _ -> EvidenceBlocking

    let requiredTestDispositionStateFromString (value: string) =
        match (if String.IsNullOrEmpty value then "" else value).Trim().ToLowerInvariant() with
        | "satisfied" -> TestSatisfied
        | "deferred" -> TestDeferred
        | "missing" -> TestMissingDisposition
        | "stale" -> TestStale
        | "synthetic" -> TestSyntheticDisposition
        | "invalid" -> TestInvalid
        | "advisory" -> TestAdvisory
        | _ -> TestBlocking

    let skillVisibilityStateFromString (value: string) =
        match (if String.IsNullOrEmpty value then "" else value).Trim().ToLowerInvariant() with
        | "visible" -> SkillVisible
        | _ -> SkillMissing

    let private taskIdsFromJson name element =
        jsonStringList name element |> List.choose (Identifiers.createTaskId >> Result.toOption)

    let private evidenceIdsFromJson name element =
        jsonStringList name element |> List.choose (Identifiers.createEvidenceId >> Result.toOption)

    let private requirementIdsFromJson name element =
        jsonStringList name element |> List.choose (Identifiers.createRequirementId >> Result.toOption)

    let parseVerificationEvidenceDisposition (element: JsonElement) : EvidenceDisposition =
        { DispositionId = jsonRequiredString "id" element
          ObligationId = jsonRequiredString "obligationId" element
          State = jsonRequiredString "state" element |> evidenceDispositionStateFromString
          EvidenceIds = evidenceIdsFromJson "evidenceIds" element
          AffectedTaskIds = taskIdsFromJson "affectedTaskIds" element
          AffectedSourceIds = jsonStringList "affectedSourceIds" element
          Severity = jsonRequiredString "severity" element
          DiagnosticIds = jsonStringList "diagnosticIds" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationTestDisposition (element: JsonElement) : RequiredTestDisposition =
        { DispositionId = jsonRequiredString "id" element
          ObligationId = jsonRequiredString "obligationId" element
          State = jsonRequiredString "state" element |> requiredTestDispositionStateFromString
          EvidenceIds = evidenceIdsFromJson "evidenceIds" element
          AffectedTaskIds = taskIdsFromJson "affectedTaskIds" element
          AffectedRequirementIds = requirementIdsFromJson "affectedRequirementIds" element
          Severity = jsonRequiredString "severity" element
          DiagnosticIds = jsonStringList "diagnosticIds" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationSkillVisibility (element: JsonElement) : SkillVisibilityFact =
        { Skill = jsonRequiredString "skill" element
          RequiringTaskIds = taskIdsFromJson "requiringTaskIds" element
          Visibility = jsonRequiredString "visibility" element |> skillVisibilityStateFromString
          SourceArtifactPath = normalizePath (jsonRequiredString "sourceArtifactPath" element)
          Severity = jsonRequiredString "severity" element
          DiagnosticIds = jsonStringList "diagnosticIds" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationFinding (element: JsonElement) : VerificationFinding =
        { Id = jsonRequiredString "id" element
          Severity = jsonRequiredString "severity" element
          Category = jsonRequiredString "category" element
          Path = normalizePath (jsonRequiredString "path" element)
          RelatedIds = jsonStringList "relatedIds" element
          Message = jsonRequiredString "message" element
          Correction = jsonRequiredString "correction" element }

    let parseVerificationLifecycleReadiness (element: JsonElement) : VerificationLifecycleReadiness =
        { Stages =
            jsonArray "stages" element
            |> List.map (fun stage ->
                { VerificationStageReadiness.Stage = jsonRequiredString "stage" stage
                  Status = jsonRequiredString "status" stage })
            |> List.sortBy (fun stage -> stage.Stage)
          Status = jsonString "status" element |> Option.defaultValue "blocked" }

    let parseVerificationTaskGraph (element: JsonElement) : VerificationTaskGraphReadiness =
        { TaskCount = jsonInt "taskCount" element |> Option.defaultValue 0
          DependencyCount = jsonInt "dependencyCount" element |> Option.defaultValue 0
          DependenciesValid = jsonBool "dependenciesValid" element |> Option.defaultValue false
          StatusesValid = jsonBool "statusesValid" element |> Option.defaultValue false
          FindingIds = jsonStringList "findingIds" element }

    let parseVerificationView (snapshot: FileSnapshot) =
        parseJsonView
            "Verification view"
            "Regenerate readiness/<id>/verify.json with valid JSON."
            (fun artifact schema root ->
                let workIdText = jsonRequiredString "workId" root
                let stageText = jsonRequiredString "stage" root

                match Identifiers.createWorkId workIdText, Identifiers.parseStage stageText with
                | Ok workId, Ok stage ->
                    let lifecycleReadiness =
                        tryJsonProperty "lifecycleReadiness" root
                        |> Option.map parseVerificationLifecycleReadiness
                        |> Option.defaultValue { Stages = []; Status = "blocked" }

                    let taskGraph =
                        tryJsonProperty "taskGraph" root
                        |> Option.map parseVerificationTaskGraph
                        |> Option.defaultValue
                            { TaskCount = 0
                              DependencyCount = 0
                              DependenciesValid = false
                              StatusesValid = false
                              FindingIds = [] }

                    Ok
                        { SchemaVersion = schema
                          ViewVersion = jsonString "viewVersion" root |> Option.defaultValue "1.0"
                          WorkId = workId
                          Stage = stage
                          Status = jsonString "status" root |> Option.defaultValue "needsVerificationCorrection"
                          Generator = jsonString "generator" root |> Option.defaultValue "fsgg-sdd"
                          Sources = jsonArray "sources" root |> List.map parseAnalysisSource |> List.sortBy (fun source -> source.Path)
                          LifecycleReadiness = lifecycleReadiness
                          TaskGraph = taskGraph
                          EvidenceDispositions =
                            jsonArray "evidenceDispositions" root
                            |> List.map parseVerificationEvidenceDisposition
                            |> List.sortBy (fun disposition -> disposition.DispositionId)
                          TestDispositions =
                            jsonArray "testDispositions" root
                            |> List.map parseVerificationTestDisposition
                            |> List.sortBy (fun disposition -> disposition.DispositionId)
                          SkillVisibility =
                            jsonArray "skillVisibility" root
                            |> List.map parseVerificationSkillVisibility
                            |> List.sortBy (fun fact -> fact.Skill)
                          GeneratedViews =
                            jsonArray "generatedViews" root
                            |> List.map parseAnalysisGeneratedView
                            |> List.sortBy (fun view -> view.Path)
                          Findings = jsonArray "findings" root |> List.map parseVerificationFinding |> List.sortBy (fun finding -> finding.Id)
                          OptionalBoundaryFacts =
                            jsonArray "governanceCompatibility" root
                            |> List.map parseAnalysisBoundaryFact
                            |> List.sortBy (fun fact -> fact.Path)
                          Diagnostics = jsonArray "diagnostics" root |> List.map parseAnalysisDiagnostic |> Diagnostics.sort
                          Readiness = jsonString "readiness" root |> Option.defaultValue "needsVerificationCorrection" }
                | _ ->
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Verification view identity fields are malformed."
                              "Regenerate verify.json with a valid workId and stage: verify."
                              [ workIdText; stageText ] ])
            snapshot.Path
            snapshot.Text
