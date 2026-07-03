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
module Ship =
    type ShipReadinessFinding =
        { Id: string
          Severity: string
          Category: string
          Path: string
          RelatedIds: string list
          Message: string
          Correction: string }

    type ShipLifecycleStageReadiness = { Stage: string; Status: string }

    type ShipVerificationReadinessSummary =
        { Status: string
          BlockingFindingIds: string list
          EvidenceSupportedCount: int
          EvidenceDeferredCount: int
          EvidenceMissingCount: int
          EvidenceStaleCount: int
          EvidenceSyntheticCount: int
          EvidenceInvalidCount: int }

    type ShipView =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          Generator: string
          Sources: AnalysisSourceRecord list
          LifecycleReadiness: ShipLifecycleStageReadiness list
          VerificationReadiness: ShipVerificationReadinessSummary
          Disposition: string
          GeneratedViews: AnalysisGeneratedViewRecord list
          Findings: ShipReadinessFinding list
          OptionalBoundaryFacts: AnalysisOptionalBoundaryFact list
          Diagnostics: Diagnostic list
          Readiness: string }

    let parseShipFinding (element: JsonElement) : ShipReadinessFinding =
        { Id = jsonRequiredString "id" element
          Severity = jsonRequiredString "severity" element
          Category = jsonRequiredString "category" element
          Path = normalizePath (jsonRequiredString "path" element)
          RelatedIds = jsonStringList "relatedIds" element
          Message = jsonRequiredString "message" element
          Correction = jsonRequiredString "correction" element }

    let parseShipLifecycleStage (element: JsonElement) : ShipLifecycleStageReadiness =
        { Stage = jsonRequiredString "stage" element
          Status = jsonRequiredString "status" element }

    let parseShipVerificationReadiness (element: JsonElement) : ShipVerificationReadinessSummary =
        { Status = jsonString "status" element |> Option.defaultValue "needsVerificationCorrection"
          BlockingFindingIds = jsonStringList "blockingFindingIds" element
          EvidenceSupportedCount = jsonInt "evidenceSupportedCount" element |> Option.defaultValue 0
          EvidenceDeferredCount = jsonInt "evidenceDeferredCount" element |> Option.defaultValue 0
          EvidenceMissingCount = jsonInt "evidenceMissingCount" element |> Option.defaultValue 0
          EvidenceStaleCount = jsonInt "evidenceStaleCount" element |> Option.defaultValue 0
          EvidenceSyntheticCount = jsonInt "evidenceSyntheticCount" element |> Option.defaultValue 0
          EvidenceInvalidCount = jsonInt "evidenceInvalidCount" element |> Option.defaultValue 0 }

    let parseShipView (snapshot: FileSnapshot) =
        parseJsonView
            "Ship view"
            "Regenerate readiness/<id>/ship.json with valid JSON."
            (fun artifact schema root ->
                let workIdText = jsonRequiredString "workId" root
                let stageText = jsonRequiredString "stage" root

                match Identifiers.createWorkId workIdText, Identifiers.parseStage stageText with
                | Ok workId, Ok stage ->
                    let lifecycleReadiness =
                        tryJsonProperty "lifecycleReadiness" root
                        |> Option.map (fun element -> jsonArray "stages" element |> List.map parseShipLifecycleStage)
                        |> Option.defaultValue []
                        |> List.sortBy (fun stage -> stage.Stage)

                    let verificationReadiness =
                        tryJsonProperty "verificationReadiness" root
                        |> Option.map parseShipVerificationReadiness
                        |> Option.defaultValue
                            { Status = "needsVerificationCorrection"
                              BlockingFindingIds = []
                              EvidenceSupportedCount = 0
                              EvidenceDeferredCount = 0
                              EvidenceMissingCount = 0
                              EvidenceStaleCount = 0
                              EvidenceSyntheticCount = 0
                              EvidenceInvalidCount = 0 }

                    let disposition =
                        tryJsonProperty "disposition" root
                        |> Option.bind (fun element -> jsonString "state" element)
                        |> Option.defaultValue "blocked"

                    Ok
                        { SchemaVersion = schema
                          ViewVersion = jsonString "viewVersion" root |> Option.defaultValue "1.0"
                          WorkId = workId
                          Stage = stage
                          Status = jsonString "status" root |> Option.defaultValue "needsShipCorrection"
                          Generator = jsonString "generator" root |> Option.defaultValue "fsgg-sdd"
                          Sources =
                            jsonArray "sources" root
                            |> List.map parseAnalysisSource
                            |> List.sortBy (fun source -> source.Path)
                          LifecycleReadiness = lifecycleReadiness
                          VerificationReadiness = verificationReadiness
                          Disposition = disposition
                          GeneratedViews =
                            jsonArray "generatedViews" root
                            |> List.map parseAnalysisGeneratedView
                            |> List.sortBy (fun view -> view.Path)
                          Findings =
                            jsonArray "findings" root
                            |> List.map parseShipFinding
                            |> List.sortBy (fun finding -> finding.Id)
                          OptionalBoundaryFacts =
                            jsonArray "governanceCompatibility" root
                            |> List.map parseAnalysisBoundaryFact
                            |> List.sortBy (fun fact -> fact.Path)
                          Diagnostics =
                            jsonArray "diagnostics" root
                            |> List.map parseAnalysisDiagnostic
                            |> Diagnostics.sort
                          Readiness = jsonString "readiness" root |> Option.defaultValue "needsShipCorrection" }
                | _ ->
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Ship view identity fields are malformed."
                              "Regenerate ship.json with a valid workId and stage: ship."
                              [ workIdText; stageText ] ])
            snapshot.Path
            snapshot.Text
