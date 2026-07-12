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
    /// FS.GG.SDD#359 / #365. The one lexical containment rule for a CITED path (`artifacts:` and
    /// `sourceRefs[].path` alike): repository-relative, no `..`. Total — it reports rather than
    /// raising, so a malformed authored path is user input, not a tool defect.
    val citedPathIsContained: path: string -> bool

    val namesRenderedArtifact: declaration: EvidenceDeclaration -> bool

    /// The visual-inspection artifact rule, stated once for the `evidence` gate, the `ED-`
    /// disposition, and the `TD-` mirror: a real (non-synthetic) `pass` that names no rendered
    /// artifact. A synthetic pass and a deferral both fall outside it.
    val passesWithoutRenderedArtifact: declaration: EvidenceDeclaration -> bool

    /// Every locally-resolvable path this declaration cites: `artifactRefs` ∪ `sourceRefs[].path`
    /// (FS.GG.SDD#349, FR-002). A `sourceRefs[].uri` is deliberately excluded — it is not a local
    /// file and is never probed. Blanks are dropped; the result is deduplicated and sorted.
    val citedArtifactPaths: declaration: EvidenceDeclaration -> string list

    /// The cited-artifact existence rule, stated once for the `evidence` gate, the `ED-`
    /// disposition, and the `TD-` mirror (FS.GG.SDD#349, FR-007): the cited paths of a *satisfying*
    /// declaration that `exists` reports absent, sorted.
    ///
    /// Only `result: pass` ∧ `synthetic: false` — the satisfaction rule — is held to this. A
    /// deferral, a disclosed synthetic pass, and any non-pass result may legitimately cite an
    /// artifact that does not exist yet, and yield `[]` (FR-006).
    ///
    /// `exists` is injected so that `Artifacts` performs no I/O: the probe is a `ReadFile` effect
    /// interpreted at the edge, and this fold reads its result (Constitution V, FR-003).
    val missingCitedArtifacts: exists: (string -> bool) -> declaration: EvidenceDeclaration -> string list

    /// Does this declaration rest on a run the tool **observed**, rather than on the author's word?
    /// (FS.GG.SDD#398, FR-001/FR-002.)
    ///
    /// Today this is `false` for every declaration, and that is the disclosure — not an oversight.
    /// SDD invokes no test runner: `Process.Start` occurs once in `src/` (`CommandEffects.fs`),
    /// serving `scaffold`'s provider and `upgrade`'s self-update, and no evidence field carries a run
    /// receipt. So every obligation that reaches `supported` does so on an assertion by the same
    /// agent that authored the work — which is what FS.GG.SDD#350 exists to fix.
    ///
    /// It is a **function over the declaration, not a constant**, so the counters that read it are
    /// computed rather than hardcoded. When #350's observed-receipt model lands, this is the one
    /// place that learns to say `true`, and `evidenceObservedCount` rises from `verify.json` all the
    /// way to the committed `ship-verdict.json` without a schema, projection, or consumer changing.
    ///
    /// Total and I/O-free: reading a receipt would be an effect at the edge, and its *result* would
    /// be threaded in here — exactly as `missingCitedArtifacts` takes an injected `exists`.
    val isObserved: declaration: EvidenceDeclaration -> bool

    /// Does this declaration claim a real pass — `result: pass`, not disclosed `synthetic`? The
    /// satisfaction rule, named once because the attestation split below partitions exactly it.
    val claimsRealPass: declaration: EvidenceDeclaration -> bool

    /// Does this declaration discharge its obligation on the author's word alone? (FS.GG.SDD#398.)
    ///
    /// The exact complement of `isObserved` over the satisfaction rule, which is what makes
    /// `supported = selfAttested + observed` hold by construction rather than by coincidence (FR-007).
    val isSelfAttested: declaration: EvidenceDeclaration -> bool

    /// Was an *obligation* — matched by these declarations — discharged by an observed run?
    /// (FS.GG.SDD#398, FR-003.) The one rule `verify`, `ship`, and the committed verdict all read;
    /// consuming it is what stops the three from drifting on what "observed" means.
    ///
    /// Consults only the declarations that claim a real pass (a `supported` obligation may carry a
    /// deferral alongside), and requires **all** of them to be observed — one observed run must not
    /// launder a hand-asserted pass sitting beside it. Both moot while `isObserved` is constantly
    /// `false`; both load-bearing the day FS.GG.SDD#350 lands.
    val obligationIsObserved: declarations: EvidenceDeclaration list -> bool

    val parseEvidenceArtifact: snapshot: FileSnapshot -> Result<EvidenceArtifact, Diagnostic list>
    val parseEvidence: snapshot: FileSnapshot -> Result<EvidenceDeclaration list, Diagnostic list>
