namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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
        {
            DispositionId: string
            ObligationId: string
            State: EvidenceDispositionState
            /// FS.GG.SDD#398 (FR-003): was this obligation discharged by a run the tool *observed*, or
            /// only by the author's word? FS.GG.SDD#350 / ADR-0035 made this answerable: it is `true`
            /// when the obligation is backed by an `observedRun` receipt SDD parsed from a runner's
            /// report. Carried per-obligation, so `ship` and the committed verdict COUNT it rather
            /// than assuming it. See `Evidence.isObserved`.
            Observed: bool
            /// WI-4 (ADR-0048): is this the disposition of a classified `{gameplay}` FR obligation?
            /// Carried per-disposition so `ship` and the Governance handoff count "classified-FR
            /// obligations unmet" over the committed verify view. Absent in a pre-WI-4 view ⇒ `false`.
            ClassifiedRequirement: bool
            EvidenceIds: EvidenceId list
            AffectedTaskIds: TaskId list
            AffectedSourceIds: string list
            Severity: string
            DiagnosticIds: string list
            Correction: string
        }

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
        {
            DispositionId: string
            ObligationId: string
            State: RequiredTestDispositionState
            /// FS.GG.SDD#398: the `TD-` attestation basis — the disposition named for a test that,
            /// until FS.GG.SDD#350, nothing had ever run. See `Evidence.obligationIsObserved`.
            Observed: bool
            EvidenceIds: EvidenceId list
            AffectedTaskIds: TaskId list
            AffectedRequirementIds: RequirementId list
            Severity: string
            DiagnosticIds: string list
            Correction: string
        }

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

    val parseVerificationView: snapshot: FileSnapshot -> Result<VerificationView, Diagnostic list>
