namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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
          /// `disposition.blockingFindingIds`, sorted. Feature 092: the compact ship verdict
          /// carries these, so the parse no longer flattens `disposition` to its state alone.
          DispositionBlockingFindingIds: string list
          GeneratedViews: AnalysisGeneratedViewRecord list
          Findings: ShipReadinessFinding list
          OptionalBoundaryFacts: AnalysisOptionalBoundaryFact list
          Diagnostics: Diagnostic list
          Readiness: string }

    val parseShipView: snapshot: FileSnapshot -> Result<ShipView, Diagnostic list>
