namespace FS.GG.SDD.Commands

open System
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
        | DependencySurface
        /// The scope a `--help` report is stamped with. Not an invocable command — `parseCommand`
        /// never yields it, so `fsgg-sdd help` stays an unknown command. It exists so a help
        /// report can carry its own identity instead of masquerading as `init` (FS.GG.SDD#352).
        | Help

    type OutputFormat =
        | Json
        | Text
        | Rich

    /// How a stage reconciles a `HybridArtifact`'s tool-owned regions with the text already on
    /// disk. The policy travels *with* the write tag, so the permission to overwrite and the
    /// merge step that earns it cannot drift apart: `canOverwrite` lets a hybrid write land
    /// precisely because one of these ran first and produced the merge result.
    type MergePolicy =
        /// Markdown. `Ensured` headings are guaranteed to exist — a missing one is appended, an
        /// existing body is left untouched. `Rederived` bodies are replaced wholesale from
        /// current source on every run, so nothing a hand wrote there survives. `Appended`
        /// sections gain newly-derived entries beneath whatever the author already wrote. Every
        /// other line in the file is authored, and the merge never reaches it.
        | SectionMerge of ensured: string list * rederived: string list * appended: string list
        /// Structured YAML. The artifact is re-derived from source in full while selected
        /// authored state is carried forward (a task's `status`/`owner` and its still-live
        /// disposition refs; an evidence declaration), so the rendered text the interpreter
        /// sees is a merge result rather than a replacement.
        | StructuredMerge

    /// How a written path is owned, and therefore whether the interpreter may overwrite it.
    ///
    /// `AuthoredSource` is the strict case: content the tool reads and never writes. It has no
    /// write site in `src/`, and `canOverwrite` refuses any effect that claims it, so a handler
    /// cannot acquire the ability to clobber authored prose by tagging a write wrongly.
    ///
    /// The seven lifecycle artifacts are `HybridArtifact`: each carries tool-owned regions its
    /// stage re-derives every run, alongside authored regions the stage preserves. They are
    /// overwritable *because* the handler's merge step already preserved the authored content —
    /// the write the interpreter sees is the merge result, not a replacement. The `MergePolicy`
    /// payload names which regions those are (#309); `MergePolicies` holds the seven values.
    type ArtifactWriteKind =
        /// Deliberately never constructed — no `WriteFile` in `src/` carries this tag, an invariant
        /// pinned by the `no command plans a WriteFile tagged AuthoredSource` test. It is not dead
        /// code: it gives a strictly authored path (`contracts/…`) the tool reads but never writes a
        /// tag the interpreter refuses (`canOverwrite`), rather than borrowing a tool-owned kind.
        | AuthoredSource
        | HybridArtifact of policy: MergePolicy
        | StructuredSource
        | GeneratedView
        | AgentGuidanceTarget

    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module MergePolicy =
        let ensuredSections (policy: MergePolicy) =
            match policy with
            | SectionMerge(ensured, _, _) -> ensured
            | StructuredMerge -> []

        let rederivedSections (policy: MergePolicy) =
            match policy with
            | SectionMerge(_, rederived, _) -> rederived
            | StructuredMerge -> []

        let appendedSections (policy: MergePolicy) =
            match policy with
            | SectionMerge(_, _, appended) -> appended
            | StructuredMerge -> []

    /// The seven lifecycle artifacts' merge policies — the single place that says which regions of
    /// each hybrid the tool owns. A stage's `WriteFile` carries the policy its merge step
    /// implements, and the merge functions read their section names from here rather than
    /// hardcoding them, so removing a heading below is enough to hand that region back to the
    /// author (#309). `rederived` and `appended` are disjoint subsets of `ensured`.
    module MergePolicies =
        /// The charter has no `LifecycleArtifacts` parser module, so its sections live here.
        let charterSections =
            [ "Identity"
              "Principles"
              "Scope Boundaries"
              "Policy Pointers"
              "Lifecycle Notes" ]

        let charter = SectionMerge(charterSections, [], [])

        let specification = SectionMerge(specificationStandardSections (), [], [])

        let clarifications =
            SectionMerge(
                clarificationStandardSections (),
                [],
                [ "Clarification Questions"
                  "Answers"
                  "Decisions"
                  "Accepted Deferrals"
                  "Remaining Ambiguity" ]
            )

        let checklist =
            SectionMerge(
                checklistStandardSections (),
                [ "Source Snapshot"
                  "Checklist Items"
                  "Review Results"
                  "Accepted Deferrals"
                  "Blocking Findings" ],
                []
            )

        let plan =
            SectionMerge(
                planStandardSections (),
                [ "Source Snapshot" ],
                [ "Plan Decisions"
                  "Contract Impact"
                  "Verification Obligations"
                  "Migration Posture"
                  "Generated View Impact"
                  "Accepted Deferrals"
                  "Planning Findings"
                  "Advisory Notes" ]
            )

        let tasks = StructuredMerge

        let evidence = StructuredMerge

        /// Every stage that writes a `work/<id>/` artifact, the file it writes, and the policy it
        /// writes it under. All seven are `HybridArtifact`: no lifecycle stage writes a file the
        /// author alone owns, and none writes one the author has no stake in.
        ///
        /// This is the authority the prose is pinned to. `fs-gg-sdd-lifecycle`'s stage table and
        /// `docs/reference/artifact-taxonomy.md` both classify these files, and both used to say
        /// "authored" — a claim the tag contradicts and a re-run disproves. Drift guards now hold
        /// them to this list (#309), so the docs cannot describe an ownership the code does not have.
        let byStage: (SddCommand * string * MergePolicy) list =
            [ Charter, "charter.md", charter
              Specify, "spec.md", specification
              Clarify, "clarifications.md", clarifications
              Checklist, "checklist.md", checklist
              Plan, "plan.md", plan
              Tasks, "tasks.yml", tasks
              Evidence, "evidence.yml", evidence ]

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
          Provider: string option
          Parameters: (string * string) list
          Force: bool
          TemplateUpdate: bool
          AssumeYes: bool
          IsInteractive: bool
          Artifact: string option
          Explain: bool
          // Evidence input (`fsgg-sdd evidence --from-tests <path>`); ignored by other commands
          // (feature 077). Pre-maps each newly scaffolded obligation to a verification-kind source.
          FromTests: string option
          // Evidence input (`fsgg-sdd evidence --from-test-report <path>`); ignored by other commands
          // (FS.GG.SDD#350, ADR-0035). Records an `observedRun` receipt from a runner-produced TRX /
          // JUnit report that SDD reads, parses, and hashes.
          //
          // DELIBERATELY NOT `--from-tests`. That flag names where the tests LIVE (a project path,
          // seeded onto scaffolded obligations); this one names a REPORT OF A RUN. ADR-0035 proposed
          // reusing `--from-tests`, having read it as "already takes a report path" — it does not, and
          // three committed tests pass it a project directory. Overloading one flag with both meanings
          // would make an unparseable *directory* a blocking error on the feature-077 path.
          FromTestReport: string option
          // Evidence input (`fsgg-sdd evidence --sync-observed-run <trx>`); ignored by other commands
          // (FS.GG.SDD#550). Re-stamps every obligation that ALREADY carries an `observedRun` receipt
          // sourced from this report, recomputing the digest and the passed/failed/skipped counts from
          // the report's current bytes. The maintenance complement to `--from-test-report`: when a TRX is
          // regenerated (e.g. a test is added late), the authored receipts pinned to it go stale, and this
          // reconciles them in place without re-typing. Receipts sourced from a DIFFERENT report are left
          // untouched. `None` ⇒ inert. Mutually exclusive with `--from-test-report`.
          SyncObservedRun: string option
          // Feature 086: `fsgg-sdd surface --update` refreshes the `docs/api-surface/**` baselines
          // from the authored `.fsi` signatures; default false (read-only `--check`).
          SurfaceUpdate: bool
          // Feature 090: `fsgg-sdd plan --accept-upstream` re-baselines the plan's
          // `## Source Snapshot` against the current sources; default false (a moved digest blocks
          // with `stalePlanSnapshot` and writes nothing). Read only by `plan`.
          AcceptUpstream: bool
          // FS.GG.SDD#350 / ADR-0035 stage 3: `--require-observed` makes an obligation fail CLOSED —
          // a `result: pass` carrying no `observedRun` receipt stops satisfying. Default false, which
          // is byte-for-byte the pre-#350 behavior.
          //
          // Read by BOTH `verify` and `ship`, and that is not redundancy. `ship` does NOT simply
          // inherit the gate by refusing a non-`verificationReady` record: a blocked `verify` writes
          // nothing, so it leaves the PREVIOUS green verify.json standing and still digest-current,
          // and a `ship` that trusted `verificationReady` alone certified a lifecycle `verify` had
          // just refused. Deleting the `ship` arm as redundant re-opens exactly that fail-open.
          RequireObserved: bool }

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
        {
            WorkId: string
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
            /// WI-4 (ADR-0048): classified `{gameplay}` FR obligations left unmet — not discharged by a
            /// real non-synthetic test (nor an accepted deferral). The aggregate Governance binds to
            /// block-on-ship. `0` for every work item that classifies no FR (additive, backward-compatible).
            ClassifiedObligationsUnmetCount: int
            SourceSnapshotCount: int
            Readiness: string
        }

    type VerificationSummary =
        {
            WorkId: string
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
            /// FS.GG.SDD#398: the two halves of `EvidenceSupportedCount`
            /// (`supported = selfAttested + observed`). FS.GG.SDD#350 / ADR-0035 made
            /// `EvidenceObservedCount` real: it counts the obligations discharged by a run SDD
            /// *read* (an `observedRun` receipt), and `EvidenceSelfAttestedCount` the ones resting on
            /// the author's word. A work item that records no receipt still reports
            /// `selfAttested == supported, observed == 0` — which is the disclosure, not a bug.
            EvidenceSelfAttestedCount: int
            EvidenceObservedCount: int
            EvidenceDeferredCount: int
            EvidenceMissingCount: int
            EvidenceStaleCount: int
            EvidenceSyntheticCount: int
            EvidenceInvalidCount: int
            TestSatisfiedCount: int
            /// FS.GG.SDD#398: the two halves of `TestSatisfiedCount`. The name says a test was
            /// satisfied — and until FS.GG.SDD#350 nothing had ever run one. `TestObservedCount` now
            /// counts the obligations backed by a receipt SDD parsed; the rest are self-attested.
            TestSelfAttestedCount: int
            TestObservedCount: int
            TestDeferredCount: int
            TestMissingCount: int
            TestStaleCount: int
            TestInvalidCount: int
            /// WI-4 (ADR-0048): classified `{gameplay}` FR obligations left unmet — carried through
            /// from the evidence dispositions so verify reports the same aggregate ship binds to.
            ClassifiedObligationsUnmetCount: int
            SkillVisibleCount: int
            SkillMissingCount: int
            SourceSnapshotCount: int
            Readiness: string
        }

    type ShipSummary =
        {
            WorkId: string
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
            /// FS.GG.SDD#398: the two halves of `EvidenceSupportedCount`
            /// (`supported = selfAttested + observed`). FS.GG.SDD#350 / ADR-0035 made
            /// `EvidenceObservedCount` real: it counts the obligations discharged by a run SDD
            /// *read* (an `observedRun` receipt), and `EvidenceSelfAttestedCount` the ones resting on
            /// the author's word. A work item that records no receipt still reports
            /// `selfAttested == supported, observed == 0` — which is the disclosure, not a bug.
            EvidenceSelfAttestedCount: int
            EvidenceObservedCount: int
            EvidenceDeferredCount: int
            EvidenceMissingCount: int
            EvidenceStaleCount: int
            EvidenceSyntheticCount: int
            EvidenceInvalidCount: int
            /// WI-4 (ADR-0048): classified `{gameplay}` FR obligations left unmet — the merge-boundary
            /// aggregate Governance binds to block-on-ship. `0` when no FR is classified.
            ClassifiedObligationsUnmetCount: int
            GeneratedViewState: string
            SourceSnapshotCount: int
            Readiness: string
        }

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
        | AwaitingLifecycle
        // Feature 068 / US2 (2b): the pre-work-model early-stage disposition, formerly written as a
        // bare "early-stage" literal bypassing this DU (a latent inconsistency the review flagged).
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

    type ProviderInvocationResult =
        { CommandLine: string
          ProcessStarted: bool
          ExitCode: int option
          StandardOutput: string
          StandardOutputTruncated: bool
          StandardError: string
          StandardErrorTruncated: bool }

    type ScaffoldSummary =
        { ProviderName: string option
          ProviderContractVersion: string option
          RequiredMinimumCliVersion: string option
          Outcome: string
          SkeletonCreated: bool
          ProviderInvoked: bool
          ProducedPathCount: int
          ProducedPaths: string list
          MirroredPaths: string list
          MaterializedDriverPaths: string list
          MaterializedGameSkillPaths: string list
          EffectiveParameters: (string * string) list
          RepoInitOutcome: string
          ToolManifestOutcome: string
          ExecutableScriptCount: int
          ExecutableScriptsSkipped: int
          NextActionHint: string
          ProviderInvocation: ProviderInvocationResult option }

    // Feature 068 / US2: the closed remediation-step vocabularies, formerly raw strings on
    // `ReconciliationStep` (a typo compiled). The `…Value` mappings below reproduce the exact
    // wire spellings, pinned by Remediation* tests + the release-baseline byte-identity suites.
    [<RequireQualifiedAccess>]
    type ReconciliationStepId =
        | CliSelfUpdate
        | TemplateRePin
        | ArtifactReSeed

    [<RequireQualifiedAccess>]
    type ReconciliationOutcome =
        | WouldApply
        | Applied
        | Skipped
        | Failed
        | NoTarget

    type ReconciliationStep =
        { StepId: ReconciliationStepId
          Kind: ReconciliationStepId
          DiffPreview: string
          Outcome: ReconciliationOutcome
          TargetPaths: string list }

    type DoctorSummary =
        { HasProvenance: bool
          ProviderName: string option
          InstalledCliVersion: string
          RequiredMinimumCliVersion: string option
          RequiredMinimumCliVersionSource: string option
          CliAxis: string
          CliBehindBy: string option
          ExpectedArtifactCount: int
          MissingArtifactPaths: string list
          SkillDriftPaths: string list
          PreviewSteps: ReconciliationStep list
          IsCoherent: bool }

    type UpgradeSummary =
        { HasProvenance: bool
          Mode: string
          AlreadyCoherent: bool
          Steps: ReconciliationStep list
          AppliedStepIds: ReconciliationStepId list
          SkippedStepIds: ReconciliationStepId list
          FailedStepIds: ReconciliationStepId list
          SkillDriftPaths: string list
          ResidualDrift: bool
          NextActionHint: string }

    // Feature 087: one classified drifted `.fsi`. See CommandTypes.fsi for docs.
    type ClassifiedEntry =
        { Path: string
          Classification: string
          RecommendedBump: string
          AddedMembers: string list
          RemovedOrChangedMembers: string list
          UnparseableFallback: bool }

    // Feature 087: the run-level additive-vs-breaking classification. See CommandTypes.fsi for docs.
    type SurfaceClassification =
        { Verdict: string
          RecommendedBump: string
          Entries: ClassifiedEntry list }

    // Feature 094: the coherent-set version obligation a classified mutation implies. See
    // CommandTypes.fsi for docs.
    type VersionBumpPrompt =
        { AxisFile: string
          AxisProperty: string
          AxisState: string
          CurrentVersion: string option
          RequiredBump: string
          SuggestedVersion: string option }

    // Feature 086: the API-surface drift picture `surface` emits. See CommandTypes.fsi for docs.
    type SurfaceSummary =
        { SourceRoot: string
          BaselineRoot: string
          Mode: string
          CheckedCount: int
          MissingBaselinePaths: string list
          DriftedSourcePaths: string list
          OrphanBaselinePaths: string list
          UpdatedBaselinePaths: string list
          IsCoherent: bool
          Classification: SurfaceClassification
          VersionBump: VersionBumpPrompt }

    type DependencySurfaceEntry =
        { PackageId: string
          Version: string
          Status: string
          CommittedSha256: string option
          ObservedSha256: string option
          ObservedSymbolCount: int }

    type DependencySurfaceSummary =
        { BaselineRoot: string
          Mode: string
          CheckedCount: int
          Entries: DependencySurfaceEntry list
          DriftedPackages: string list
          UnavailablePackages: string list
          UpdatedPackages: string list
          IsCoherent: bool }

    type GovernanceCompatibilityFact =
        { Path: string
          Relationship: string
          RequiredBySdd: bool
          State: string
          DiagnosticIds: string list }

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

    type LintDefectClass =
        | CoverageLine
        | MissingDecisionTag
        | FrontMatter
        | DuplicateId
        | Parse
        | Unresolvable

    type LintOutcome =
        | Clean
        | DefectsFound
        | UnusableInput

    type GrammarPointer =
        { Skill: string
          Section: string option
          ExampleTag: string option }

    type LintDefect =
        { Class: LintDefectClass
          Diagnostic: Diagnostic
          GrammarPointer: GrammarPointer option }

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

    type HelpFlag =
        { Name: string
          Argument: string option
          Description: string }

    type HelpCommandEntry = { Name: string; Description: string }

    type HelpScope =
        | TopLevel
        | Command of string

    type HelpSummary =
        { Scope: HelpScope
          Usage: string
          Commands: HelpCommandEntry list
          GlobalFlags: HelpFlag list
          CommandFlags: HelpFlag list }

    // Feature 084: lifecycle-status footer types (see CommandTypes.fsi for docs).
    [<RequireQualifiedAccess>]
    type StageState =
        | Done
        | Current
        | Next
        | Pending
        | Blocked

    type StageEntry =
        { Command: SddCommand
          Ordinal: int
          State: StageState }

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
          // The fsgg-sdd version that produced this report (FS-GG/FS.GG.SDD#305). A stale toolchain is
          // otherwise invisible in the artifacts it emits, so a feedback report cannot be told apart
          // from a stale one without re-verifying every finding against main. Sourced from the request's
          // injected GeneratorVersion — never read from the assembly here, which would put reflection in
          // pure code.
          ToolVersion: string
          Command: SddCommand
          ProjectRoot: string
          OutputFormat: OutputFormat
          DryRun: bool
          Outcome: CommandOutcome
          // A positive "clean, advance" signal that disambiguates `Outcome = NoChange`: `true` when the
          // stage ran, every artifact it evaluated was already present and current (all recorded changes
          // NoChange/Preserve), and nothing blocked — so the work item is coherent for this stage and it
          // is safe to advance. `false` for a bare no-op (nothing recorded) and for every non-NoChange
          // outcome. Orthogonal to `Outcome`; leaves the outcome vocabulary stable (FS-GG/FS.GG.SDD#183).
          Coherent: bool
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
          DependencySurface: DependencySurfaceSummary option
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
        | ReadPackageSurface of packageId: string * version: string
        | SetExecutable of path: string
        | Confirm of stepId: string * prompt: string

    type ProcessRunResult =
        { Started: bool
          ExitCode: int
          Command: string
          StandardOutput: string
          StandardOutputTruncated: bool
          StandardError: string
          StandardErrorTruncated: bool }

    type CommandEffectResult =
        { Effect: CommandEffect
          Succeeded: bool
          Snapshot: FileSnapshot option
          Process: ProcessRunResult option
          Confirmed: bool option
          Diagnostic: Diagnostic option }

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
          DependencySurface: DependencySurfaceSummary option
          GeneratedViews: GeneratedViewState list
          Report: CommandReport option }

    type CommandMsg =
        | EffectInterpreted of CommandEffectResult
        | BuildReport

    let commandName (command: SddCommand) =
        match command with
        | Init -> "init"
        | Charter -> "charter"
        | Specify -> "specify"
        | Clarify -> "clarify"
        | Checklist -> "checklist"
        | Plan -> "plan"
        | Tasks -> "tasks"
        | Analyze -> "analyze"
        | Evidence -> "evidence"
        | Verify -> "verify"
        | Ship -> "ship"
        | Agents -> "agents"
        | Refresh -> "refresh"
        | Scaffold -> "scaffold"
        | Doctor -> "doctor"
        | Upgrade -> "upgrade"
        | Lint -> "lint"
        | Surface -> "surface"
        | DependencySurface -> "dependency-surface"
        | Help -> "help"

    let commandStage (command: SddCommand) =
        match command with
        | Init -> "project"
        | _ -> commandName command

    let parseCommand (value: string) =
        match value.Trim().ToLowerInvariant() with
        | "init" -> Ok Init
        | "charter" -> Ok Charter
        | "specify" -> Ok Specify
        | "clarify" -> Ok Clarify
        | "checklist" -> Ok Checklist
        | "plan" -> Ok Plan
        | "tasks" -> Ok Tasks
        | "analyze" -> Ok Analyze
        | "evidence" -> Ok Evidence
        | "verify" -> Ok Verify
        | "ship" -> Ok Ship
        | "agents" -> Ok Agents
        | "refresh" -> Ok Refresh
        | "scaffold" -> Ok Scaffold
        | "doctor" -> Ok Doctor
        | "upgrade" -> Ok Upgrade
        | "lint" -> Ok Lint
        | "surface" -> Ok Surface
        | "dependency-surface" -> Ok DependencySurface
        | other -> Error $"Unknown SDD command '{other}'."

    let outputFormatValue (format: OutputFormat) =
        match format with
        | Json -> "json"
        | Text -> "text"
        | Rich -> "rich"

    let writeKindValue (kind: ArtifactWriteKind) =
        match kind with
        | AuthoredSource -> "authoredSource"
        | HybridArtifact _ -> "hybridArtifact"
        | StructuredSource -> "structuredSource"
        | GeneratedView -> "generatedView"
        | AgentGuidanceTarget -> "agentGuidance"

    let artifactOperationValue (operation: ArtifactOperation) =
        match operation with
        | ArtifactOperation.Create -> "create"
        | ArtifactOperation.Update -> "update"
        | ArtifactOperation.Preserve -> "preserve"
        | ArtifactOperation.Refuse -> "refuse"
        | ArtifactOperation.NoChange -> "noChange"

    let generatedViewCurrencyValue (currency: GeneratedViewCurrency) =
        match currency with
        | GeneratedViewCurrency.Current -> "current"
        | GeneratedViewCurrency.Missing -> "missing"
        | GeneratedViewCurrency.Stale -> "stale"
        | GeneratedViewCurrency.Malformed -> "malformed"
        | GeneratedViewCurrency.Blocked -> "blocked"

    let guidanceDispositionValue (disposition: GuidanceDisposition) =
        match disposition with
        | GeneratedCurrent -> "generated-current"
        | GuidanceStale -> "stale"
        | GuidanceBlocked -> "blocked"
        | GuidanceAdvisory -> "advisory"

    let refreshDispositionValue (disposition: RefreshDisposition) =
        match disposition with
        | RefreshedCurrent -> "refreshed-current"
        | PartiallyBlocked -> "partially-blocked"
        | RefreshBlocked -> "blocked"
        | AwaitingLifecycle -> "awaiting-lifecycle"
        | EarlyStage -> "early-stage"

    let reconciliationStepIdValue (stepId: ReconciliationStepId) =
        match stepId with
        | ReconciliationStepId.CliSelfUpdate -> "cliSelfUpdate"
        | ReconciliationStepId.TemplateRePin -> "templateRePin"
        | ReconciliationStepId.ArtifactReSeed -> "artifactReSeed"

    let reconciliationOutcomeValue (outcome: ReconciliationOutcome) =
        match outcome with
        | ReconciliationOutcome.WouldApply -> "wouldApply"
        | ReconciliationOutcome.Applied -> "applied"
        | ReconciliationOutcome.Skipped -> "skipped"
        | ReconciliationOutcome.Failed -> "failed"
        | ReconciliationOutcome.NoTarget -> "noTarget"

    let outcomeValue (outcome: CommandOutcome) =
        match outcome with
        | CommandOutcome.Succeeded -> "succeeded"
        | CommandOutcome.SucceededWithWarnings -> "succeededWithWarnings"
        | CommandOutcome.Blocked -> "blocked"
        | CommandOutcome.NoChange -> "noChange"

    // Feature 084: the single canonical StageState -> token map, shared by the JSON serializer and
    // the text/rich footer projections so the `state` string cannot diverge between them.
    let stageStateName (state: StageState) =
        match state with
        | StageState.Done -> "done"
        | StageState.Current -> "current"
        | StageState.Next -> "next"
        | StageState.Pending -> "pending"
        | StageState.Blocked -> "blocked"

    let lintArtifactKindValue (kind: LintArtifactKind) =
        match kind with
        | LintArtifactKind.Charter -> "charter"
        | LintArtifactKind.Specification -> "specification"
        | LintArtifactKind.Clarification -> "clarification"
        | LintArtifactKind.Checklist -> "checklist"
        | LintArtifactKind.Plan -> "plan"
        | LintArtifactKind.Tasks -> "tasks"
        | LintArtifactKind.Evidence -> "evidence"
        | LintArtifactKind.Unrecognized -> "unrecognized"

    let lintOutcomeValue (outcome: LintOutcome) =
        match outcome with
        | Clean -> "clean"
        | DefectsFound -> "defectsFound"
        | UnusableInput -> "unusableInput"

    let lintDefectClassValue (cls: LintDefectClass) =
        match cls with
        | CoverageLine -> "coverageLine"
        | MissingDecisionTag -> "missingDecisionTag"
        | FrontMatter -> "frontMatter"
        | DuplicateId -> "duplicateId"
        | Parse -> "parse"
        | Unresolvable -> "unresolvable"

    let nextLifecycleCommand (command: SddCommand) =
        match command with
        | Init -> Some Charter
        | Charter -> Some Specify
        | Specify -> Some Clarify
        | Clarify -> Some Checklist
        | Checklist -> Some Plan
        | Plan -> Some Tasks
        | Tasks -> Some Analyze
        | Analyze -> Some Evidence
        | Evidence -> Some Verify
        | Verify -> Some Ship
        | Ship -> None
        | Agents -> None
        | Refresh -> None
        | Scaffold -> None
        | Doctor -> None
        | Upgrade -> None
        | Lint -> None
        | Surface -> None
        | DependencySurface -> None
        | Help -> None

    /// FS.GG.SDD#642: the lifecycle stages strictly downstream of `command`, in canonical order,
    /// derived by walking `nextLifecycleCommand`. Used to name the ordered re-run set an author must
    /// execute after an upstream artifact edit stales a downstream stage's recorded source digest —
    /// the order that was previously learned by trial rather than surfaced. Empty for `Ship` and for
    /// every cross-cutting command (their `nextLifecycleCommand` is `None`).
    let rec downstreamLifecycleStages (command: SddCommand) : SddCommand list =
        match nextLifecycleCommand command with
        | Some successor -> successor :: downstreamLifecycleStages successor
        | None -> []

    let effectPath (effect: CommandEffect) =
        match effect with
        | ReadFile path
        | EnumerateDirectory path
        | CreateDirectory path
        | WriteFile(path, _, _) -> Some path
        | RunProcess(_, _, workingDir) -> Some workingDir
        | SetExecutable path -> Some path
        | ReadPackageSurface _ -> None
        | Confirm _ -> None
