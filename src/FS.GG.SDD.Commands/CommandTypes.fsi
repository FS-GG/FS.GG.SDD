namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion

module CommandTypes =
    type SddCommand =
        | Init
        | Charter
        | Specify
        | Clarify
        | Checklist
        | Plan
        | Tasks
        | Analyze
        | Evidence
        | Verify
        | Ship
        | Agents
        | Refresh
        | Scaffold
        | Doctor
        | Upgrade
        | Lint
        | Surface

    type OutputFormat =
        | Json
        | Text
        | Rich

    type ArtifactWriteKind =
        | AuthoredSource
        | StructuredSource
        | GeneratedView
        | AgentGuidanceTarget

    type ArtifactOperation =
        | Create
        | Update
        | Preserve
        | Refuse
        | NoChange

    type GeneratedViewCurrency =
        | Current
        | Missing
        | Stale
        | Malformed
        | Blocked

    type CommandOutcome =
        | Succeeded
        | SucceededWithWarnings
        | Blocked
        | NoChange

    type CommandRequest =
        { Command: SddCommand
          ProjectRoot: string
          WorkId: string option
          Title: string option
          InputText: string option
          OutputFormat: OutputFormat
          DryRun: bool
          GeneratorVersion: GeneratorVersion
          // Scaffold inputs (`fsgg-sdd scaffold`); ignored by other commands.
          Provider: string option
          Parameters: (string * string) list
          Force: bool
          // Refresh the provider template (`dotnet new update`) before create.
          // Default true; `--no-update` clears it for create-only / offline runs.
          TemplateUpdate: bool
          // Remediation inputs (`fsgg-sdd upgrade`); ignored by other commands (E2).
          // `--yes`: confirm every reconciliation step without prompting (FR-011).
          AssumeYes: bool
          // Computed at the edge from `Console.IsInputRedirected` (R7). Lets the pure
          // core refuse a non-interactive `upgrade` without `--yes` up front (FR-012).
          IsInteractive: bool
          // Lint inputs (`fsgg-sdd lint <artifact>`); ignored by other commands (feature 076).
          // `Artifact`: the positional path to pre-flight.
          Artifact: string option
          // `Explain`: `<stage> --explain` non-blocking dry-run flag (FR-016); default false.
          Explain: bool
          // Evidence input (`fsgg-sdd evidence --from-tests <path>`); ignored by other commands
          // (feature 077). Pre-maps each newly scaffolded obligation to a verification-kind source
          // pointing at this test path. `None` ⇒ inert (output byte-identical aside from refs).
          FromTests: string option
          // Surface input (`fsgg-sdd surface --update`); ignored by other commands (feature 086).
          // `true` ⇒ refresh the `docs/api-surface/**` baselines from the authored `.fsi`
          // signatures; `false` (default, or `--check`) ⇒ read-only drift check.
          SurfaceUpdate: bool
          // Plan input (`fsgg-sdd plan --accept-upstream`); ignored by other commands (feature 090).
          // `true` ⇒ re-baseline the plan's `## Source Snapshot` digests against the current
          // spec/clarifications/checklist, rewriting that section body and nothing else. `false`
          // (default) ⇒ a plan whose recorded digests moved blocks with `stalePlanSnapshot` and
          // writes zero bytes. Never honored by `tasks`/`analyze`: accepting the upstream is the
          // operator's gesture at `plan`, not an implicit downstream one (FR-008).
          AcceptUpstream: bool }

    type GeneratedViewSource =
        { Path: string
          Digest: SourceDigest option
          SchemaVersion: int option
          SchemaStatus: string option }

    type ArtifactChange =
        { Path: string
          Kind: string
          Ownership: string
          Operation: ArtifactOperation
          BeforeDigest: SourceDigest option
          AfterDigest: SourceDigest option
          SafeWriteDecision: string
          DiagnosticIds: string list }

    type GeneratedViewState =
        { Path: string
          Kind: string
          SchemaVersion: int option
          Generator: GeneratorVersion option
          Sources: GeneratedViewSource list
          OutputDigest: OutputDigest option
          Currency: GeneratedViewCurrency
          DiagnosticIds: string list }

    type SpecificationSummary =
        { WorkId: string
          Stage: string
          Status: string
          StoryIds: string list
          RequirementIds: string list
          AcceptanceScenarioIds: string list
          AmbiguityIds: string list }

    type ClarificationSummary =
        { WorkId: string
          Stage: string
          Status: string
          SourceSpec: string
          QuestionIds: string list
          AnsweredQuestionIds: string list
          DecisionIds: string list
          AcceptedDeferralIds: string list
          RemainingAmbiguityCount: int
          BlockingAmbiguityCount: int }

    type ChecklistSummary =
        { WorkId: string
          Stage: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          ItemIds: string list
          ResultIds: string list
          PassedCount: int
          FailedBlockingCount: int
          AcceptedDeferralCount: int
          StaleResultCount: int
          AdvisoryCount: int }

    type PlanSummary =
        { WorkId: string
          Stage: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          DecisionIds: string list
          ContractReferenceIds: string list
          VerificationObligationIds: string list
          MigrationNoteIds: string list
          GeneratedViewImpactIds: string list
          AcceptedDeferralCount: int
          StaleDecisionCount: int
          BlockingFindingCount: int
          AdvisoryCount: int }

    type TasksSummary =
        { WorkId: string
          Stage: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          TaskIds: string list
          DependencyCount: int
          RequiredSkillCount: int
          RequiredEvidenceCount: int
          PendingCount: int
          InProgressCount: int
          DoneCount: int
          SkippedCount: int
          StaleCount: int
          AcceptedDeferralCount: int
          BlockingFindingCount: int
          AdvisoryCount: int }

    type AnalysisSummary =
        { WorkId: string
          Stage: string
          Status: string
          AnalysisPath: string
          SourceCount: int
          SourceRelationshipCount: int
          ReadyFindingCount: int
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          StaleSourceCount: int
          MissingDispositionCount: int
          MalformedSourceCount: int
          GeneratedViewFindingCount: int
          AcceptedDeferralCount: int
          Readiness: string }

    type EvidenceSummary =
        { WorkId: string
          Stage: string
          Status: string
          EvidencePath: string
          DeclarationIds: string list
          DeclarationCount: int
          ObligationCount: int
          SupportedCount: int
          DeferredCount: int
          MissingCount: int
          StaleCount: int
          SyntheticCount: int
          InvalidCount: int
          AdvisoryCount: int
          BlockingCount: int
          SourceSnapshotCount: int
          Readiness: string }

    type VerificationSummary =
        { WorkId: string
          Stage: string
          Status: string
          VerifyPath: string
          FindingIds: string list
          ReadyFindingCount: int
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          ObligationCount: int
          EvidenceSupportedCount: int
          EvidenceDeferredCount: int
          EvidenceMissingCount: int
          EvidenceStaleCount: int
          EvidenceSyntheticCount: int
          EvidenceInvalidCount: int
          TestSatisfiedCount: int
          TestDeferredCount: int
          TestMissingCount: int
          TestStaleCount: int
          TestInvalidCount: int
          SkillVisibleCount: int
          SkillMissingCount: int
          SourceSnapshotCount: int
          Readiness: string }

    type ShipSummary =
        { WorkId: string
          Stage: string
          Status: string
          ShipPath: string
          FindingIds: string list
          ReadyFindingCount: int
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          Disposition: string
          LifecycleStageReadiness: (string * string) list
          VerificationReadiness: string
          EvidenceSupportedCount: int
          EvidenceDeferredCount: int
          EvidenceMissingCount: int
          EvidenceStaleCount: int
          EvidenceSyntheticCount: int
          EvidenceInvalidCount: int
          GeneratedViewState: string
          SourceSnapshotCount: int
          Readiness: string }

    type GuidanceDisposition =
        | GeneratedCurrent
        | GuidanceStale
        | GuidanceBlocked
        | GuidanceAdvisory

    type AgentGuidanceFinding =
        { Id: string
          Severity: string
          Category: string
          Path: string
          RelatedIds: string list
          Message: string
          Correction: string }

    type AgentGuidanceSummary =
        { WorkId: string
          Stage: string
          Status: string
          GeneratedRoots: string list
          GeneratedTargetIds: string list
          RefusedTargetIds: string list
          FindingIds: string list
          ReadyFindingCount: int
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          Disposition: string
          EquivalenceRequired: bool
          DivergentTargetIds: string list
          GeneratedViewState: string
          SourceSnapshotCount: int
          Readiness: string }

    type RefreshDisposition =
        | RefreshedCurrent
        | PartiallyBlocked
        | RefreshBlocked
        /// Feature 068 / US2 (2b): pre-work-model early-stage disposition (serializes to
        /// `early-stage`), formerly a bare literal bypassing this DU.
        | EarlyStage

    type RefreshSummary =
        { WorkId: string
          Stage: string
          Status: string
          SummaryPath: string
          RefreshedViewIds: string list
          AlreadyCurrentViewIds: string list
          BlockedViewIds: string list
          NotApplicableViewIds: string list
          PreservedAuthoredPaths: string list
          FindingIds: string list
          AdvisoryCount: int
          WarningCount: int
          BlockingCount: int
          Disposition: string
          PerViewState: (string * string) list
          SourceSnapshotCount: int
          Readiness: string }

    /// Advisory per-command fact noting that an optional `.fsgg` Governance path was *not evaluated*
    /// by SDD. SUPERSEDED by the concrete `GovernanceHandoff` view
    /// (`readiness/<id>/governance-handoff.json`): the handoff carries the real declared
    /// evidence/governed-reference/readiness/config-presence facts Governance consumes, whereas this
    /// fact only marks the boundary as SDD-unevaluated. Retained as a pointer to that contract
    /// (Constitution VII); it asserts no route/profile/gate/verdict.
    /// The diagnostic runtime facts of a provider-defect scaffold invocation, surfaced
    /// in the scaffold report (FR-001/002/003/005) and never persisted (FR-010). Present
    /// only on the three provider-defect outcomes; absent (`None`) on success, dry-run,
    /// and every pre-invocation user-input block (FR-006).
    type ProviderInvocationResult =
        {
            /// Fully-resolved invoked command line, program + args as executed (FR-001).
            CommandLine: string
            /// Whether the provider process actually started (FR-003 discriminator).
            ProcessStarted: bool
            /// The provider exit code; `None` when the process never launched (FR-003) —
            /// distinct from a real `0`. Projected as int-or-null in json.
            ExitCode: int option
            /// Captured standard output, bounded to the per-stream cap (FR-002/005).
            StandardOutput: string
            StandardOutputTruncated: bool
            /// Captured standard error — carries the engine's own rejection text
            /// (FR-002/005). On a launch failure this holds the launch error (R4).
            StandardError: string
            StandardErrorTruncated: bool
        }

    type ScaffoldSummary =
        {
            ProviderName: string option
            ProviderContractVersion: string option
            /// The provider-declared minimum coherent `fsgg-sdd` CLI version (feature 052,
            /// E4), recorded beside the producing CLI version for audit. `None` when the
            /// provider declares none or a malformed minimum. Projected as string-or-null
            /// in json and as `scaffoldRequiredMinimumCliVersion` in text/rich.
            RequiredMinimumCliVersion: string option
            Outcome: string
            SkeletonCreated: bool
            ProviderInvoked: bool
            ProducedPathCount: int
            ProducedPaths: string list
            /// 056: the `.claude`/`.codex` mirror copies of the provider's produced
            /// `.agents/skills/*` skills that SDD fanned out. Sorted; `[]` when the provider
            /// produced no skills. Projected after `producedPaths` in json/text/rich.
            MirroredPaths: string list
            /// The effective `key → value` parameters forwarded to the provider —
            /// provider-declared `default`s overlaid by author `--param` overrides
            /// (author wins). Sorted ascending by key; `[]` when none forwarded
            /// (FR-003). Projected after `producedPaths` in json/text/rich.
            EffectiveParameters: (string * string) list
            RepoInitOutcome: string
            ExecutableScriptCount: int
            ExecutableScriptsSkipped: int
            NextActionHint: string
            /// Provider-defect diagnostic facts (FR-006 gate): `Some` only on
            /// `providerFailed` / `providerUnavailable` / `providerWroteSddTree`;
            /// `None` on success, empty-success, dry-run, and user-input blocks.
            ProviderInvocation: ProviderInvocationResult option
        }

    /// The closed set of reconciliation step ids / kinds (feature 068 / US2), formerly a raw
    /// string. Serializes to `cliSelfUpdate` / `templateRePin` / `artifactReSeed` via
    /// `reconciliationStepIdValue`.
    [<RequireQualifiedAccess>]
    type ReconciliationStepId =
        | CliSelfUpdate
        | TemplateRePin
        | ArtifactReSeed

    /// The closed set of reconciliation step outcomes (feature 068 / US2), formerly a raw
    /// string. Serializes to `wouldApply` / `applied` / `skipped` / `failed` / `noTarget` via
    /// `reconciliationOutcomeValue`.
    [<RequireQualifiedAccess>]
    type ReconciliationOutcome =
        | WouldApply
        | Applied
        | Skipped
        | Failed
        | NoTarget

    /// One confirmable unit of `upgrade` (CLI self-update, template re-pin, or
    /// artifact re-seed), shared by `DoctorSummary.PreviewSteps` (dry-run preview) and
    /// `UpgradeSummary.Steps` (apply outcome). Data-model E6.
    type ReconciliationStep =
        {
            StepId: ReconciliationStepId
            Kind: ReconciliationStepId
            /// Compact before/after preview per step kind (R5): version delta /
            /// created-path list / changed-line pin preview. Presentation-only fact.
            DiffPreview: string
            /// Preview context: `wouldApply` / `noTarget`. Apply context:
            /// `applied` / `skipped` / `failed` / `noTarget`.
            Outcome: ReconciliationOutcome
            /// The consumer paths the step would write (re-seed: missing artifacts;
            /// re-pin: `.fsgg/providers.yml`; self-update: `[]`). Sorted.
            TargetPaths: string list
        }

    /// The read-only drift picture `doctor` emits (spec Key Entity "Drift report",
    /// data-model E4). `doctor` never blocks → `NoChange`/`SucceededWithWarnings`.
    type DoctorSummary =
        {
            HasProvenance: bool
            ProviderName: string option
            InstalledCliVersion: string
            RequiredMinimumCliVersion: string option
            CliAxis: string
            CliBehindBy: string option
            ExpectedArtifactCount: int
            MissingArtifactPaths: string list
            /// 058/ADR-0014 §Decision 3: content-addressed skill drift — the concrete
            /// root/skill paths in the union (process OR product) that are missing from a
            /// root, byte-divergent across roots, or hash-mismatched. Advisory; sorted.
            SkillDriftPaths: string list
            PreviewSteps: ReconciliationStep list
            IsCoherent: bool
        }

    /// The outcome of a reconciliation run (data-model E5).
    type UpgradeSummary =
        {
            HasProvenance: bool
            Mode: string
            AlreadyCoherent: bool
            Steps: ReconciliationStep list
            AppliedStepIds: ReconciliationStepId list
            SkippedStepIds: ReconciliationStepId list
            FailedStepIds: ReconciliationStepId list
            /// 058/ADR-0014 §Decision 3: content-addressed skill drift surfaced at reconcile
            /// time (advisory in P1 — a present-but-divergent copy is reported, not clobbered).
            SkillDriftPaths: string list
            ResidualDrift: bool
            NextActionHint: string
        }

    /// One classified drifted `.fsi` (feature 087). Only `drifted` files (a committed baseline
    /// that exists and differs byte-for-byte) are classified — a `missing-baseline` file is a
    /// *new* surface (fresh registration), and `matched`/`orphan` have no delta.
    type ClassifiedEntry =
        {
            /// The drifted **source**-relative `.fsi` path (e.g. `src/Foo/Bar.fsi`).
            Path: string
            /// `additive` | `breaking` | `cosmetic`.
            Classification: string
            /// Per-file recommended coherent-set bump: `major` | `minor` | `none`.
            RecommendedBump: string
            /// Member tokens present in the source but absent in the baseline. Sorted.
            AddedMembers: string list
            /// Member tokens present in the baseline but absent in the source (removed, renamed,
            /// or signature-changed). Sorted.
            RemovedOrChangedMembers: string list
            /// True when the source yielded no member tokens and was conservatively classified
            /// `breaking` so the operator inspects it (FR-011).
            UnparseableFallback: bool
        }

    /// The run-level additive-vs-breaking classification of the drifted set (feature 087,
    /// FS-GG/.github ADR-0025). Advisory-but-loud: it maps to a recommended coherent-set bump but
    /// emits no diagnostic and changes no exit code — a drifted tree still exits 1 under `--check`
    /// exactly as in feature 086.
    type SurfaceClassification =
        {
            /// Most-severe entry: `breaking` | `additive` | `cosmetic` | `none` (none ⇔ no drift).
            Verdict: string
            /// Mapped from the verdict: breaking→`major`, additive→`minor`, cosmetic/none→`none`.
            RecommendedBump: string
            /// One entry per drifted file, sorted by `Path`; empty when nothing drifted.
            Entries: ClassifiedEntry list
        }

    /// Feature 094 (FS-GG/.github ADR-0025 reconcile step 3a): the coherent-set version obligation
    /// implied by a classified shipped-surface mutation. Advisory — it emits a
    /// `surface.versionBumpRequired` warning and never changes the exit code. SDD cannot see the
    /// previously *published* version (that lives in the feed and the `.github` registry pin), so
    /// this states facts and an implication, never an accusation: the bump may already be applied
    /// in the change under review.
    type VersionBumpPrompt =
        {
            /// The workspace-relative file the axis was read from. `--param versionAxisFile`,
            /// default `Directory.Build.props`. Echoed even when unresolved, so the operator can
            /// see what was looked for.
            AxisFile: string
            /// The MSBuild property element name. `--param versionAxisProperty`, default `Version`.
            /// Generic SDD embeds no concrete axis name (FR-003).
            AxisProperty: string
            /// `resolved` | `undeterminable` | `unparseable`.
            AxisState: string
            /// The axis value, present only when `AxisState = "resolved"`.
            CurrentVersion: string option
            /// `major` | `minor` | `none`, mirroring the run verdict's `RecommendedBump`. Depends
            /// only on the classification, so it is reported in every axis state.
            RequiredBump: string
            /// `CurrentVersion` with `RequiredBump` applied. `None` whenever `CurrentVersion` is
            /// `None`; equal to `CurrentVersion` when `RequiredBump = "none"`.
            SuggestedVersion: string option
        }

    /// The read-only (or, under `--update`, reconciling) API-surface picture `surface` emits
    /// (feature 086). Each authored `src/**/*.fsi` signature has a byte-identical committed
    /// baseline under `docs/api-surface/`; drift is a missing or byte-differing baseline.
    /// `--check` blocks on drift (exit 1); `--update` refreshes the baselines (exit 0). Orphan
    /// baselines (no source) are advisory and never auto-removed.
    type SurfaceSummary =
        {
            SourceRoot: string
            BaselineRoot: string
            /// `check` (read-only, blocks on drift) or `update` (refresh baselines).
            Mode: string
            /// Count of authored `.fsi` signatures discovered under the source root.
            CheckedCount: int
            /// Baseline paths absent for a discovered source signature. Sorted.
            MissingBaselinePaths: string list
            /// Source signature paths whose baseline exists but differs byte-for-byte. Sorted.
            DriftedSourcePaths: string list
            /// Baseline `.fsi` files with no corresponding source signature. Advisory (never
            /// deleted); sorted.
            OrphanBaselinePaths: string list
            /// Baseline paths written this run (`--update` only). Sorted; `[]` under `--check`.
            UpdatedBaselinePaths: string list
            /// True when every discovered source signature had a matching baseline at the start
            /// of the run (no missing, no drift). Orphans do not affect coherence.
            IsCoherent: bool
            /// Feature 087: the additive-vs-breaking classification of the drifted set. Always
            /// present; `none`/`none`/`[]` when nothing drifted. Advisory — never changes the
            /// exit code.
            Classification: SurfaceClassification
            /// Feature 094: the coherent-set version obligation the classification implies. Always
            /// present (never `option`, so the automation contract keeps a stable shape); inert
            /// (`RequiredBump = "none"`, no warning) when nothing drifted.
            VersionBump: VersionBumpPrompt
        }

    type GovernanceCompatibilityFact =
        { Path: string
          Relationship: string
          RequiredBySdd: bool
          State: string
          DiagnosticIds: string list }

    /// The authored-artifact kind `lint`/`--explain` auto-detects and routes on
    /// (feature 076, FR-002). `Unrecognized` means the kind could not be determined
    /// (missing/unreadable/unknown) → the unusable-input outcome (exit 2). Qualified
    /// access keeps its cases from shadowing the like-named `SddCommand` cases; the
    /// `Lint`-prefixed name avoids colliding with `Artifacts.ArtifactRef.ArtifactKind`.
    [<RequireQualifiedAccess>]
    type LintArtifactKind =
        | Charter
        | Specification
        | Clarification
        | Checklist
        | Plan
        | Tasks
        | Evidence
        | Unrecognized

    /// Classification attached to each surfaced lint defect, driving the grammar
    /// pointer (FR-007). The four grammar classes always carry a pointer; `Parse`
    /// (unparseable artifact) and `Unresolvable` (undetectable kind) do not.
    type LintDefectClass =
        | CoverageLine
        | MissingDecisionTag
        | FrontMatter
        | DuplicateId
        | Parse
        | Unresolvable

    /// The pass/fail outcome of one lint run, driving the bespoke exit mapping
    /// (FR-011): `Clean` → 0, `DefectsFound` → 1, `UnusableInput` → 2.
    type LintOutcome =
        | Clean
        | DefectsFound
        | UnusableInput

    /// A stable pointer from a defect to the grammar of record
    /// (`docs/reference/authoring-contracts.md`) — the anchor is drift-guarded to
    /// resolve to a real heading; `ExampleTag` names a tagged fenced example when one
    /// exists (FR-007b).
    type GrammarPointer =
        { Doc: string
          Anchor: string
          ExampleTag: string option }

    /// One reported lint defect: the reused parser `Diagnostic` plus its lint
    /// classification and (for grammar classes) a grammar pointer. Every defect is an
    /// `Error` (FR-017).
    type LintDefect =
        { Class: LintDefectClass
          Diagnostic: Diagnostic
          GrammarPointer: GrammarPointer option }

    /// The read-only pre-flight picture `lint`/`<stage> --explain` emits (feature 076).
    /// `Defects = []` ⇔ `Outcome = Clean`. Writes nothing; not a lifecycle stage.
    type LintSummary =
        { ArtifactPath: string
          Kind: LintArtifactKind
          Defects: LintDefect list
          Outcome: LintOutcome }

    type NextAction =
        { ActionId: string
          Command: SddCommand option
          WorkId: string option
          Reason: string
          RequiredArtifacts: string list
          BlockingDiagnosticIds: string list }

    /// One accepted flag in a help listing. `Argument` is `Some "<id>"` for value-taking
    /// flags and `None` for switches; aliases are listed in `Name` (e.g. `--help, -h`).
    type HelpFlag =
        { Name: string
          Argument: string option
          Description: string }

    /// One command in the top-level help command list.
    type HelpCommandEntry = { Name: string; Description: string }

    /// Whether a help summary describes the top-level CLI or one command.
    type HelpScope =
        | TopLevel
        | Command of string

    /// Static, deterministic help payload projected through the standard three views.
    type HelpSummary =
        { Scope: HelpScope
          Usage: string
          Commands: HelpCommandEntry list
          GlobalFlags: HelpFlag list
          CommandFlags: HelpFlag list }

    /// Feature 084: the sensed state of one lifecycle stage in the status footer.
    [<RequireQualifiedAccess>]
    type StageState =
        | Done
        | Current
        | Next
        | Pending
        | Blocked

    /// Feature 084: one stage's position and sensed state in the lifecycle rail.
    type StageEntry =
        { Command: SddCommand
          Ordinal: int
          State: StageState }

    /// Feature 084: the standardized lifecycle-status fact carried on every command report
    /// and rendered as the final footer in all three projections. Additive; the failure
    /// explanation/options are NOT stored here — they are projected at render time from the
    /// report's existing `Diagnostics`/`NextAction` (FR-017).
    type LifecycleStatus =
        { WorkId: string option
          Stages: StageEntry list
          CurrentOrdinal: int option
          TotalStages: int
          Outcome: CommandOutcome
          NextCommand: SddCommand option
          IsLifecycleStage: bool }

    type CommandReport =
        { SchemaVersion: int
          ReportVersion: string
          Command: SddCommand
          ProjectRoot: string
          OutputFormat: OutputFormat
          DryRun: bool
          Outcome: CommandOutcome
          WorkId: string option
          ChangedArtifacts: ArtifactChange list
          Specification: SpecificationSummary option
          Clarification: ClarificationSummary option
          Checklist: ChecklistSummary option
          Plan: PlanSummary option
          Tasks: TasksSummary option
          Analysis: AnalysisSummary option
          Evidence: EvidenceSummary option
          Verification: VerificationSummary option
          Ship: ShipSummary option
          AgentGuidance: AgentGuidanceSummary option
          Refresh: RefreshSummary option
          Scaffold: ScaffoldSummary option
          Doctor: DoctorSummary option
          Upgrade: UpgradeSummary option
          Lint: LintSummary option
          Surface: SurfaceSummary option
          GeneratedViews: GeneratedViewState list
          Diagnostics: Diagnostic list
          GovernanceCompatibility: GovernanceCompatibilityFact list
          NextAction: NextAction option
          Help: HelpSummary option
          LifecycleStatus: LifecycleStatus }

    type CommandEffect =
        | ReadFile of path: string
        | EnumerateDirectory of path: string
        | CreateDirectory of path: string
        | WriteFile of path: string * text: string * kind: ArtifactWriteKind
        | RunProcess of command: string * args: string list * workingDir: string
        | SetExecutable of path: string
        /// Requests per-step confirmation for one reconciliation step (R7). Interpreted
        /// at the edge by a stdin read when `IsInteractive`; the pure `update` re-derives
        /// the next step from the confirmed results in the interpreted-effect log.
        | Confirm of stepId: string * prompt: string

    /// Captured outcome of a `RunProcess` effect at the edge. `Started = false` means the
    /// process could not be launched (engine/command absent); its exit code is meaningless
    /// and surfaced as `ExitCode = None` in the report (FR-003). Captured stdout/stderr are
    /// carried forward (bounded) so the scaffold report can surface a provider defect.
    type ProcessRunResult =
        {
            Started: bool
            ExitCode: int
            /// The fully-resolved command line as executed (program + args) — FR-001.
            Command: string
            /// Captured stdout/stderr, each bounded to `providerOutputCapChars` with a
            /// truncation flag (FR-002/005). On a launch failure `StandardError` holds the
            /// launch exception message (R4).
            StandardOutput: string
            StandardOutputTruncated: bool
            StandardError: string
            StandardErrorTruncated: bool
        }

    type CommandEffectResult =
        {
            Effect: CommandEffect
            Succeeded: bool
            Snapshot: FileSnapshot option
            Process: ProcessRunResult option
            /// The confirmation decision for a `Confirm` effect: `Some true` = confirmed,
            /// `Some false` = declined/dry-run, `None` = not a `Confirm` result (E3).
            Confirmed: bool option
            Diagnostic: Diagnostic option
        }

    type CommandModel =
        { Request: CommandRequest
          PendingEffects: CommandEffect list
          InterpretedEffects: CommandEffectResult list
          Diagnostics: Diagnostic list
          Specification: SpecificationSummary option
          Clarification: ClarificationSummary option
          Checklist: ChecklistSummary option
          Plan: PlanSummary option
          Tasks: TasksSummary option
          Analysis: AnalysisSummary option
          Evidence: EvidenceSummary option
          Verification: VerificationSummary option
          Ship: ShipSummary option
          AgentGuidance: AgentGuidanceSummary option
          Refresh: RefreshSummary option
          Scaffold: ScaffoldSummary option
          Doctor: DoctorSummary option
          Upgrade: UpgradeSummary option
          Lint: LintSummary option
          Surface: SurfaceSummary option
          GeneratedViews: GeneratedViewState list
          Report: CommandReport option }

    type CommandMsg =
        | EffectInterpreted of CommandEffectResult
        | BuildReport

    val commandName: command: SddCommand -> string
    val commandStage: command: SddCommand -> string
    val parseCommand: value: string -> Result<SddCommand, string>
    val outputFormatValue: format: OutputFormat -> string
    val writeKindValue: kind: ArtifactWriteKind -> string
    val artifactOperationValue: operation: ArtifactOperation -> string
    val generatedViewCurrencyValue: currency: GeneratedViewCurrency -> string
    val guidanceDispositionValue: disposition: GuidanceDisposition -> string
    val refreshDispositionValue: disposition: RefreshDisposition -> string
    val reconciliationStepIdValue: stepId: ReconciliationStepId -> string
    val reconciliationOutcomeValue: outcome: ReconciliationOutcome -> string
    val outcomeValue: outcome: CommandOutcome -> string

    /// Feature 084: the canonical lifecycle stage-state token (`done`/`current`/`next`/`pending`/
    /// `blocked`), shared by the JSON serializer and the footer projections.
    val stageStateName: state: StageState -> string

    val nextLifecycleCommand: command: SddCommand -> SddCommand option
    val effectPath: effect: CommandEffect -> string option

    /// The canonical lowercase name of a lint artifact kind (feature 076), used in the
    /// lint report JSON/text projections and diagnostics.
    val lintArtifactKindValue: kind: LintArtifactKind -> string

    /// The canonical lowercase name of a lint outcome (`clean`/`defectsFound`/`unusableInput`).
    val lintOutcomeValue: outcome: LintOutcome -> string

    /// The canonical lowercase name of a lint defect class.
    val lintDefectClassValue: cls: LintDefectClass -> string
