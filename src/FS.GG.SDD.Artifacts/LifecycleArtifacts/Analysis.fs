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
module Analysis =
    type AnalysisSourceRelationship =
        { Id: string
          SourcePath: string
          TargetPath: string
          SourceId: string option
          TargetId: string option
          Relationship: string
          State: string
          DiagnosticIds: string list }

    type AnalysisFinding =
        { Id: string
          Category: string
          Severity: string
          State: string
          Path: string
          RelatedIds: string list
          Message: string
          Correction: string }

    type AnalysisReadiness =
        { Status: string
          ReadyCount: int
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          StaleSourceCount: int
          MissingDispositionCount: int
          MalformedSourceCount: int
          GeneratedViewFindingCount: int
          AcceptedDeferralCount: int }

    type AnalysisNextAction =
        { ActionId: string
          Command: string option
          Reason: string }

    type AnalysisView =
        { SchemaVersion: SchemaVersion
          ViewVersion: string
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          Generator: string
          Sources: AnalysisSourceRecord list
          SourceRelationships: AnalysisSourceRelationship list
          Readiness: AnalysisReadiness
          Findings: AnalysisFinding list
          GeneratedViews: AnalysisGeneratedViewRecord list
          OptionalBoundaryFacts: AnalysisOptionalBoundaryFact list
          Diagnostics: Diagnostic list
          NextAction: AnalysisNextAction option }

    let parseAnalysisDiagnostic (element: JsonElement) =
        Diagnostics.create
            (jsonRequiredString "id" element)
            (jsonRequiredString "severity" element |> diagnosticSeverityFromJson)
            (jsonString "artifact" element |> Option.orElseWith (fun () -> jsonString "path" element) |> Option.bind artifactFromJsonPath)
            None
            (jsonRequiredString "message" element)
            (jsonRequiredString "correction" element)
            (jsonStringList "relatedIds" element)

    let parseAnalysisSource (element: JsonElement) =
        { Path = normalizePath (jsonRequiredString "path" element)
          Kind = jsonRequiredString "kind" element
          Digest = jsonDigest "digest" element |> Option.orElseWith (fun () -> jsonDigest "sourceDigest" element)
          SchemaVersion = jsonInt "schemaVersion" element
          SchemaStatus = jsonString "schemaStatus" element }

    let parseAnalysisRelationship (element: JsonElement) =
        { Id = jsonRequiredString "id" element
          SourcePath = normalizePath (jsonRequiredString "sourcePath" element)
          TargetPath = normalizePath (jsonRequiredString "targetPath" element)
          SourceId = jsonString "sourceId" element
          TargetId = jsonString "targetId" element
          Relationship = jsonRequiredString "relationship" element
          State = jsonRequiredString "state" element
          DiagnosticIds = jsonStringList "diagnosticIds" element }

    let parseAnalysisFinding (element: JsonElement) =
        { Id = jsonRequiredString "id" element
          Category = jsonRequiredString "category" element
          Severity = jsonRequiredString "severity" element
          State = jsonRequiredString "state" element
          Path = normalizePath (jsonRequiredString "path" element)
          RelatedIds = jsonStringList "relatedIds" element
          Message = jsonRequiredString "message" element
          Correction = jsonRequiredString "correction" element }

    let parseAnalysisReadiness (element: JsonElement) =
        { Status = jsonString "status" element |> Option.defaultValue "blocked"
          ReadyCount = jsonInt "readyCount" element |> Option.defaultValue 0
          AdvisoryCount = jsonInt "advisoryCount" element |> Option.defaultValue 0
          WarningCount = jsonInt "warningCount" element |> Option.defaultValue 0
          BlockingCount = jsonInt "blockingCount" element |> Option.defaultValue 0
          StaleSourceCount = jsonInt "staleSourceCount" element |> Option.defaultValue 0
          MissingDispositionCount = jsonInt "missingDispositionCount" element |> Option.defaultValue 0
          MalformedSourceCount = jsonInt "malformedSourceCount" element |> Option.defaultValue 0
          GeneratedViewFindingCount = jsonInt "generatedViewFindingCount" element |> Option.defaultValue 0
          AcceptedDeferralCount = jsonInt "acceptedDeferralCount" element |> Option.defaultValue 0 }

    let parseAnalysisGeneratedView (element: JsonElement) =
        { Path = normalizePath (jsonRequiredString "path" element)
          Kind = jsonRequiredString "kind" element
          Currency = jsonRequiredString "currency" element
          DiagnosticIds = jsonStringList "diagnosticIds" element }

    let parseAnalysisBoundaryFact (element: JsonElement) =
        { Path = normalizePath (jsonRequiredString "path" element)
          Relationship = jsonRequiredString "relationship" element
          RequiredBySdd = jsonBool "requiredBySdd" element |> Option.defaultValue false
          State = jsonRequiredString "state" element
          DiagnosticIds = jsonStringList "diagnosticIds" element }

    let parseAnalysisNextAction (element: JsonElement) =
        { ActionId = jsonRequiredString "actionId" element
          Command = jsonString "command" element
          Reason = jsonRequiredString "reason" element }

    let parseAnalysisView (snapshot: FileSnapshot) =
        parseJsonView
            "Analysis view"
            "Regenerate readiness/<id>/analysis.json with valid JSON."
            (fun artifact schema root ->
                let workIdText = jsonRequiredString "workId" root
                let stageText = jsonRequiredString "stage" root

                match Identifiers.createWorkId workIdText, Identifiers.parseStage stageText with
                | Ok workId, Ok stage ->
                    let readiness =
                        tryJsonProperty "readiness" root
                        |> Option.map parseAnalysisReadiness
                        |> Option.defaultValue
                            { Status = jsonString "status" root |> Option.defaultValue "blocked"
                              ReadyCount = 0
                              AdvisoryCount = 0
                              WarningCount = 0
                              BlockingCount = 0
                              StaleSourceCount = 0
                              MissingDispositionCount = 0
                              MalformedSourceCount = 0
                              GeneratedViewFindingCount = 0
                              AcceptedDeferralCount = 0 }

                    let diagnostics = jsonArray "diagnostics" root |> List.map parseAnalysisDiagnostic |> Diagnostics.sort

                    Ok
                        { SchemaVersion = schema
                          ViewVersion = jsonString "viewVersion" root |> Option.defaultValue "1.0"
                          WorkId = workId
                          Stage = stage
                          Status = jsonString "status" root |> Option.defaultValue readiness.Status
                          Generator = jsonString "generator" root |> Option.defaultValue "fsgg-sdd"
                          Sources = jsonArray "sources" root |> List.map parseAnalysisSource |> List.sortBy (fun source -> source.Path)
                          SourceRelationships =
                            jsonArray "sourceRelationships" root
                            |> List.map parseAnalysisRelationship
                            |> List.sortBy (fun relationship -> relationship.Id)
                          Readiness = readiness
                          Findings = jsonArray "findings" root |> List.map parseAnalysisFinding |> List.sortBy (fun finding -> finding.Id)
                          GeneratedViews =
                            jsonArray "generatedViews" root
                            |> List.map parseAnalysisGeneratedView
                            |> List.sortBy (fun view -> view.Path)
                          OptionalBoundaryFacts =
                            jsonArray "optionalBoundaryFacts" root
                            |> List.map parseAnalysisBoundaryFact
                            |> List.sortBy (fun fact -> fact.Path)
                          Diagnostics = diagnostics
                          NextAction = tryJsonProperty "nextAction" root |> Option.map parseAnalysisNextAction }
                | _ ->
                    Error
                        [ Diagnostics.workModelInconsistent
                              artifact
                              "Analysis view identity fields are malformed."
                              "Regenerate analysis.json with a valid workId and stage: analyze."
                              [ workIdText; stageText ] ])
            snapshot.Path
            snapshot.Text
