namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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

    val parseAnalysisView: snapshot: FileSnapshot -> Result<AnalysisView, Diagnostic list>
    val internal parseAnalysisDiagnostic: element: JsonElement -> Diagnostic
    val internal parseAnalysisSource: element: JsonElement -> AnalysisSourceRecord
    val internal parseAnalysisGeneratedView: element: JsonElement -> AnalysisGeneratedViewRecord
    val internal parseAnalysisBoundaryFact: element: JsonElement -> AnalysisOptionalBoundaryFact
