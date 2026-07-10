namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

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

    type SyntheticDisclosure = { StandsInFor: string; Reason: string }

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
          // Feature 077: the originating task's full source-id lineage bag, carried verbatim so
          // scaffolding can grammar-route it into the declaration's typed ref buckets. Recovers
          // the plan-decision id (and any FR it traces to) that task.Requirements/task.Decisions
          // drop for a plan-decision task.
          LinkedSourceIds: string list
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

    /// Shared authored-record field lists (ADR-0002 invariant 1 / FR-007, FS.GG.SDD#201): one
    /// `FieldCodec` list drives both the reader (here) and the renderer (`HandlersEvidence`) for each
    /// record, so a field cannot be read without being written or vice versa (#180/#181).
    module EvidenceCodec =
        val sourceRefSeed: EvidenceSourceReference
        val sourceRefFields: ArtifactCodec.FieldCodec<EvidenceSourceReference> list

        type DisclosureDraft =
            { StandsInFor: string option
              Reason: string option }

        val disclosureDraftSeed: DisclosureDraft
        val disclosureFields: ArtifactCodec.FieldCodec<DisclosureDraft> list

        /// The whole authored evidence declaration as one shared field list — drives both the
        /// reader (`parseEvidenceArtifact`) and the renderer (`HandlersEvidence`). `id` is framed by
        /// the artifact-level renderer and read by the semantic layer, so it is not a field here.
        val declarationSeed: EvidenceDeclaration
        val declarationFields: ArtifactCodec.FieldCodec<EvidenceDeclaration> list

    /// Serialization mappings shared by the codec and the Commands validation/render (FS.GG.SDD#260).
    val evidenceKindSourceValue: kind: EvidenceKind -> string
    val allowedEvidenceResults: Set<string>
    val normalizedEvidenceResult: result: string -> string

    /// The skill tag marking a task, and the obligation minted from it, as discharged by rendering a
    /// frame and looking at it (FS.GG.SDD#306).
    val visualInspectionSkill: string

    val isVisualInspectionTagged: tags: string list -> bool

    /// Does this declaration name a rendered artifact — an `artifactRefs` entry, or a `sourceRefs[]`
    /// entry carrying a `path` or a `uri`?
    val namesRenderedArtifact: declaration: EvidenceDeclaration -> bool

    /// The visual-inspection artifact rule, stated once for the `evidence` gate, the `ED-`
    /// disposition, and the `TD-` mirror: a real (non-synthetic) `pass` that names no rendered
    /// artifact. A synthetic pass and a deferral both fall outside it.
    val passesWithoutRenderedArtifact: declaration: EvidenceDeclaration -> bool

    val parseEvidenceArtifact: snapshot: FileSnapshot -> Result<EvidenceArtifact, Diagnostic list>
    val parseEvidence: snapshot: FileSnapshot -> Result<EvidenceDeclaration list, Diagnostic list>
