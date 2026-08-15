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

    /// A run the tool **read**, rather than a `pass` an agent **typed** (FS.GG.SDD#350, ADR-0035).
    /// Recorded by `evidence --from-test-report` from a TRX / JUnit report: every field is derived from the
    /// report SDD opened, and `Digest` in particular cannot be authored.
    ///
    /// It does not make evidence unforgeable — it moves the bar from an assertion to an artifact of a
    /// declared format, whose counts must agree and whose file must still be on disk at `verify`.
    type ObservedRun =
        {
            Source: string
            Digest: string
            /// `exact-bytes-v1`; legacy receipts missing the field require explicit re-sync.
            DigestContract: string
            Outcome: string
            Passed: int
            Failed: int
            Skipped: int
        }

    /// A durable **record** the obligation rests on, rather than a run (FS.GG.SDD#865).
    ///
    /// `ObservedRun` answers "did a test run discharge this?". For an obligation discharged by filing a
    /// row, recording a decision, or performing a routing, no run ever could — so before this type
    /// existed such an obligation could not reach `Observed: true` by any route,
    /// `verify.unobservedRequiredTest` fired forever, and `ship` was unreachable.
    ///
    /// It is authored, not derived: there is no runner to read a record from. Its strength is therefore
    /// its FORM — a closed-set `Kind`, a `Locator` checked against that kind, a byte `Digest` when the
    /// record is repository-local, a `Statement` a reader can check the record against, and a date. SDD
    /// never dereferences the locator (DEC-001); the locator is committed into `verify.json` and the
    /// ship verdict so a later reader can re-check it out of band.
    ///
    /// It does not make a record unforgeable, exactly as `ObservedRun` does not make a run unforgeable.
    /// What it must not do is let the author's word back in, and it does not: a record-class obligation
    /// with no receipt, or an incoherent one, does not satisfy.
    type RecordReceipt =
        {
            /// One of `recordReceiptKinds`: `decision`, `issue`, or `commit`.
            Kind: string
            /// The durable reference, in the form `Kind` requires.
            Locator: string
            /// `durable-locator-v1`; anything else is rejected rather than reinterpreted.
            LocatorContract: string
            /// `sha256:<64 hex>` of a repository-local `decision` record's exact bytes; empty for
            /// `issue`/`commit`, which have no local bytes.
            Digest: string
            /// What the record is asserted to establish.
            Statement: string
            /// When the record was made, as an ISO-8601 instant.
            RecordedAt: string
        }

    type JourneyReceipt =
        { SchemaVersion: int
          RunnerIdentity: string
          RunnerVersion: string
          Origin: string
          RouteId: string
          ScenarioId: string
          TestId: string
          InputKind: string
          InputDigest: string
          ReplayDigest: string
          TraceDigest: string
          InitialFingerprint: string
          TerminalFingerprint: string
          TerminalPredicateReached: bool
          Outcome: string
          MaximumSteps: int
          ActualSteps: int
          ObservedReportSource: string
          ObservedReportDigest: string
          ObservedTestName: string
          ObservedTestOutcome: string }

    /// A typed, active normal-play performance gate attached to an evidence declaration.
    /// Absence means the cited artifact is baseline/stress information only and carries no target.
    type PerformanceIntentDeclaration = Fsgg.Schemas.PerformanceIntentDeclaration

    type PerformanceBudgetDeclaration =
        { ArtifactPath: string
          Intent: PerformanceIntentDeclaration option
          TargetFps: int
          WorkloadIds: string list
          StressWorkloadIds: string list
          WorkloadDefinitionDigests: string list
          CurrencyToken: string
          CapturedAfterUtc: string
          MaxP95Ms: decimal
          MaxP99Ms: decimal
          MaxCatchUpFrames: int
          MeasurementScope: string
          RequiredCapability: string
          LiveCompositorRequired: bool
          DeferralIssue: string option }

    type PerformanceBudgetState =
        | PerformancePassed
        | PerformanceFailed
        | PerformanceDeferred
        | PerformanceMalformed

    type PerformanceEvidenceSampleSet = Fsgg.Schemas.PerformanceEvidenceSampleSet
    type PerformanceEvidenceArtifact = Fsgg.Schemas.PerformanceEvidenceArtifact
    type PerformanceEvidenceMeasurement = Fsgg.Schemas.PerformanceEvidenceMeasurement

    type PerformanceBudgetEvaluation =
        { DeclarationId: string
          ArtifactPath: string
          State: PerformanceBudgetState
          WorkloadIds: string list
          Reasons: string list
          DeferralIssue: string option
          Artifact: PerformanceEvidenceArtifact option
          Measurements: PerformanceEvidenceMeasurement list }

    val parsePerformanceIntentYaml: yaml: string -> Result<PerformanceIntentDeclaration option, string>
    val isPerformanceDebtIssueReference: value: string -> bool
    val requiresPerformanceIntentProfile: profile: string option -> bool
    val performanceIntentProblems: intent: PerformanceIntentDeclaration -> string list

    type EvidenceDeclaration =
        {
            Id: EvidenceId
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
            /// FS.GG.SDD#350: the receipt, when a run was observed. `None` is the honest state for an
            /// obligation discharged on the author's word — it is what `isSelfAttested` counts.
            ObservedRun: ObservedRun option
            /// FS.GG.SDD#865: the receipt, when the obligation rests on a durable record rather than a
            /// run. `None` is the honest state for an unrecorded record-class obligation, and the
            /// ordinary state for a test-class one — a record does not discharge a test (DEC-002).
            RecordReceipt: RecordReceipt option
            JourneyReceipt: JourneyReceipt option
            PerformanceBudget: PerformanceBudgetDeclaration option
            Rationale: string option
            Owner: string option
            Scope: string option
            LaterLifecycleVisibility: string option
            Notes: string list
            Source: ArtifactRef
            SourceLocation: SourceLocation option
        }

    type EvidenceObligation =
        {
            ObligationId: string
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
            /// WI-4 (ADR-0048): when non-empty, the obligation is satisfied only by a matching
            /// declaration whose kind is one of these AND is a non-synthetic pass — the "real test
            /// kind ∧ synthetic:false" gate a classified `{gameplay}` FR carries. Empty (the default
            /// for every other obligation) imposes no kind restriction, so this is additive and
            /// backward-compatible.
            RequiredEvidenceKinds: string list
            /// FS.GG.SDD#865: what class of evidence could EVER discharge this — one of
            /// `dischargeClasses`. `test` (the default, and what every obligation minted before this
            /// field existed meant) says a test run discharges it; `record` says a durable record does,
            /// and that no run ever will.
            ///
            /// This is the axis the type was missing: `RequiredEvidenceKinds` restricts which
            /// declaration kind satisfies and the disposition `State` records how well the evidence
            /// stands up, but neither can say "no runner report will ever exist for this obligation".
            DischargeClass: string
            RequiredSkillOrCapabilityTags: string list
            Blocking: bool
            Correction: string
        }

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

        /// FS.GG.SDD#350. The receipt's read draft: `source`/`digest`/`outcome` are null-aware, so a
        /// partial or blank mapping lifts to `None` (no receipt) rather than to a receipt made of
        /// empty strings. The counts are plain ints — a junk token reads as `0` and
        /// `observedRunInconsistency` judges it, rather than the codec dropping the whole receipt.
        type ObservedRunDraft =
            { Source: string option
              Digest: string option
              DigestContract: string option
              Outcome: string option
              Passed: int
              Failed: int
              Skipped: int }

        val observedRunDraftSeed: ObservedRunDraft
        val observedRunFields: ArtifactCodec.FieldCodec<ObservedRunDraft> list

        /// A receipt exists only if it names BOTH what was read and the hash of what was read: a
        /// source with no digest is a filename, a digest with no source is a number.
        val liftObservedRun: draft: ObservedRunDraft -> ObservedRun option
        val lowerObservedRun: run: ObservedRun -> ObservedRunDraft

        /// FS.GG.SDD#865. The record receipt's read draft: its scalars are null-aware, so a partial or
        /// blank mapping lifts to `None` (no receipt) rather than to a receipt of empty strings.
        /// `digest` is option-carrying because its absence is legal for an `issue`/`commit` record —
        /// `recordReceiptInconsistency` decides per kind whether the absence is right.
        type RecordReceiptDraft =
            { Kind: string option
              Locator: string option
              LocatorContract: string option
              Digest: string option
              Statement: string option
              RecordedAt: string option }

        val recordReceiptDraftSeed: RecordReceiptDraft
        val recordReceiptFields: ArtifactCodec.FieldCodec<RecordReceiptDraft> list

        /// A receipt exists only if it names BOTH the class of record and the record itself: a kind with
        /// no locator is a category, a locator with no kind is a string nobody knows how to check. Every
        /// other field lifts verbatim and is refused, by name, by `recordReceiptInconsistency` — a
        /// malformed receipt is never silently downgraded to "no receipt".
        val liftRecordReceipt: draft: RecordReceiptDraft -> RecordReceipt option

        val lowerRecordReceipt: receipt: RecordReceipt -> RecordReceiptDraft
        val journeyReceiptSeed: JourneyReceipt
        val journeyReceiptFields: ArtifactCodec.FieldCodec<JourneyReceipt> list

        val performanceBudgetSeed: PerformanceBudgetDeclaration
        val performanceIntentSeed: PerformanceIntentDeclaration
        val performanceIntentFields: ArtifactCodec.FieldCodec<PerformanceIntentDeclaration> list
        val performanceBudgetFields: ArtifactCodec.FieldCodec<PerformanceBudgetDeclaration> list

        /// The whole authored evidence declaration as one shared field list — drives both the
        /// reader (`parseEvidenceArtifact`) and the renderer (`HandlersEvidence`). `id` is framed by
        /// the artifact-level renderer and read by the semantic layer, so it is not a field here.
        val declarationSeed: EvidenceDeclaration
        val declarationFields: ArtifactCodec.FieldCodec<EvidenceDeclaration> list

    /// Serialization mappings shared by the codec and the Commands validation/render (FS.GG.SDD#260).
    val evidenceKindSourceValue: kind: EvidenceKind -> string
    val allowedEvidenceResults: Set<string>
    val normalizedEvidenceResult: result: string -> string

    /// Parse the independently verifiable JSON evidence contract.
    val parsePerformanceEvidence: text: string -> Result<PerformanceEvidenceArtifact, string list>

    /// Evaluate every active performance gate against `performance-evidence-v1`.
    /// `artifactText` is an injected, already-sensed lookup; the pure artifact layer performs no IO.
    val evaluatePerformanceBudgets:
        artifactText: (string -> string option) ->
        declarations: EvidenceDeclaration list ->
            PerformanceBudgetEvaluation list

    /// The skill tag marking a task, and the obligation minted from it, as discharged by rendering a
    /// frame and looking at it (FS.GG.SDD#306).
    val visualInspectionSkill: string

    val isVisualInspectionTagged: tags: string list -> bool

    /// The FR classification facet (ADR-0048) that carries the per-FR non-synthetic test obligation —
    /// one of `RequirementModel.recognizedRequirementClasses`, named because it is this class the task
    /// generator maps to a gameplay-test obligation.
    val gameplayClassification: string

    /// The capability tag marking a task, and the obligation minted from it, as a per-classified-FR
    /// gameplay test obligation discharged only by a real, non-synthetic test (ADR-0048, WI-4).
    val gameplayTestCapability: string
    val productionJourneyClassification: string
    val productionJourneyCapability: string

    /// The evidence kinds that count as a *real test* for a classified-FR obligation (ADR-0048) — the
    /// single source of truth for the derived obligation's `RequiredEvidenceKinds`.
    val realTestEvidenceKinds: string list

    val isGameplayTestTagged: tags: string list -> bool
    val isProductionJourneyTagged: tags: string list -> bool

    /// FS.GG.SDD#865: the capability tag marking a task, and the obligation minted from it, as
    /// discharged by a durable RECORD rather than by a test run. DEC-003 puts the class on this
    /// authored task tag rather than a new spec classification facet, because `requiredSkills` is
    /// authored state the task generator unions across regeneration (FS.GG.SDD#310, AC7).
    val recordDischargeCapability: string

    /// The obligation discharge classes. `test` is the default and the only thing an obligation minted
    /// before this axis existed could have meant.
    val testDischargeClass: string

    val recordDischargeClass: string
    val dischargeClasses: string list
    val isRecordDischargeTagged: tags: string list -> bool

    /// The discharge class these tags declare — the one place a tag becomes a class, so the obligation
    /// minter and every later reader cannot disagree about what the tag meant.
    val dischargeClassFromTags: tags: string list -> string

    /// Is this class record-discharged? Read from the persisted class STRING, not re-derived from tags,
    /// because the class is what `verify` writes and `ship` reads back. Unrecognized or absent reads as
    /// test-class — the fail-closed direction, since test-class carries the stricter requirement.
    val isRecordDischargeClass: dischargeClass: string -> bool

    /// The closed set of durable-record kinds a `RecordReceipt` may name: `decision` (a record committed
    /// in this repository, located by contained relative path and byte-bound), `issue` (a filed row,
    /// located by absolute https URI), `commit` (a landed commit, located by 40-hex object name).
    val recordReceiptKinds: string list

    /// The only current `RecordReceipt.LocatorContract`.
    val recordLocatorContract: string
    val journeyReceiptProblems: declaration: EvidenceDeclaration -> string list
    val hasValidJourneyReceipt: declaration: EvidenceDeclaration -> bool

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

    /// The classified-FR (`{gameplay}`) required-evidence-kind rule (ADR-0048, WI-4), stated once for
    /// the `ED-` disposition and its `TD-` mirror: a real (non-synthetic) `pass` whose kind is one of
    /// `requiredKinds`. A synthetic pass and a non-test kind both fall outside it.
    val satisfiesRequiredEvidenceKinds: requiredKinds: string list -> declaration: EvidenceDeclaration -> bool

    /// Every locally-resolvable path this declaration cites: `artifactRefs` ∪ `sourceRefs[].path` ∪
    /// `observedRun.source` (FS.GG.SDD#349 FR-002; FS.GG.SDD#350 FR-009). A `sourceRefs[].uri` is
    /// deliberately excluded — it is not a local file and is never probed. Blanks are dropped; the
    /// result is deduplicated and sorted.
    ///
    /// Including the receipt's report here is what makes it *checked* rather than merely recorded: a
    /// report deleted after recording turns its obligation `invalid` at `verify` through the existing
    /// #349 cascade, with no new gate.
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
    /// (FS.GG.SDD#398, FR-001/FR-002 — now answered by FS.GG.SDD#350 / ADR-0035.)
    ///
    /// `true` exactly when the declaration carries an `ObservedRun` receipt whose run passed
    /// (`outcome: passed` ∧ `failed = 0`). #398 wrote this as a function over the declaration rather
    /// than the constant `false` it then returned, so that #350 could change **this body alone** —
    /// and it did: `evidenceObservedCount` now rises from `verify.json` all the way to the committed
    /// `ship-verdict.json` with no schema, projection, or consumer changed.
    ///
    /// Total and I/O-free: the report was read at the effect edge, and only its *result* reaches
    /// here — exactly as `missingCitedArtifacts` takes an injected `exists`.
    val isObserved: declaration: EvidenceDeclaration -> bool

    /// The receipt's internal-consistency rule (FS.GG.SDD#350, FR-005). `TestReport.parse` derives
    /// `Outcome` from the counts, so a *recorded* receipt cannot fail this — but an **authored** one
    /// can, and `evidence.yml` is a text file. Rejecting an incoherent receipt is what stops it from
    /// becoming a new place to type `pass`.
    ///
    /// Returns the reason, or `None` when the receipt is coherent.
    val observedRunInconsistency: run: ObservedRun -> string option

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

    /// The record receipt's internal-consistency rule (FS.GG.SDD#865, FR-003) — the exact shape of
    /// `observedRunInconsistency`, and for the same reason. A record receipt is *authored*: there is no
    /// runner to derive it from, so every field is user input, and judging its form is the whole of what
    /// stops it becoming a second, roomier place to type `pass`.
    ///
    /// Decides FORM only, and is total and I/O-free. Existence of a `decision` record is the cited-path
    /// cascade's job, byte-currency is `recordReceiptIsCurrent`'s, and the CONTENT of a remote record is
    /// a later reader's — deliberately, because reading it here would mean dereferencing it (DEC-001).
    ///
    /// Returns the reason, or `None` when the receipt is coherent.
    val recordReceiptInconsistency: receipt: RecordReceipt -> string option

    /// Does this declaration rest on a durable record a later reader can be handed? (FS.GG.SDD#865.)
    /// The record twin of `isObserved`, deliberately NOT folded into it — keeping them separate is what
    /// makes a record receipt unable to discharge a test obligation (DEC-002).
    val isRecorded: declaration: EvidenceDeclaration -> bool

    /// Was an *obligation* discharged by a durable record? The record twin of `obligationIsObserved`,
    /// sharing its two load-bearing decisions rather than restating them: only declarations claiming a
    /// real pass are consulted, and **all** must be recorded, so one receipt cannot launder a bare
    /// `result: pass` beside it.
    val obligationIsRecorded: declarations: EvidenceDeclaration list -> bool

    /// **The one kind-directed discharge rule** (FS.GG.SDD#865, FR-008 / DEC-002) — the single function
    /// the `ED-` ladder, the `TD-` ladder, `ship`, and the committed verdict all consume, so the four
    /// cannot drift on what discharges an obligation.
    ///
    /// Fail-closed both ways: a record receipt never discharges a test-class obligation, and an observed
    /// run never discharges a record-class one.
    val obligationDischarged: dischargeClass: string -> declarations: EvidenceDeclaration list -> bool

    val parseEvidenceArtifact: snapshot: FileSnapshot -> Result<EvidenceArtifact, Diagnostic list>
    val parseEvidence: snapshot: FileSnapshot -> Result<EvidenceDeclaration list, Diagnostic list>
