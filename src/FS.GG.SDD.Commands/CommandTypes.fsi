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

    type OutputFormat =
        | Json
        | Text
        | Rich

    type OverwritePolicy =
        | RefuseUnsafe
        | AllowGeneratedRefresh

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
          OverwritePolicy: OverwritePolicy
          GeneratorVersion: GeneratorVersion
          // Scaffold inputs (`fsgg-sdd scaffold`); ignored by other commands.
          Provider: string option
          Parameters: (string * string) list
          Force: bool
          // Refresh the provider template (`dotnet new update`) before create.
          // Default true; `--no-update` clears it for create-only / offline runs.
          TemplateUpdate: bool }

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
          AmbiguityIds: string list
          UnresolvedAmbiguityCount: int }

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
    type ScaffoldSummary =
        { ProviderName: string option
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
          /// The effective `key → value` parameters forwarded to the provider —
          /// provider-declared `default`s overlaid by author `--param` overrides
          /// (author wins). Sorted ascending by key; `[]` when none forwarded
          /// (FR-003). Projected after `producedPaths` in json/text/rich.
          EffectiveParameters: (string * string) list
          RepoInitOutcome: string
          ExecutableScriptCount: int
          ExecutableScriptsSkipped: int
          NextActionHint: string }

    type GovernanceCompatibilityFact =
        { Path: string
          Relationship: string
          RequiredBySdd: bool
          State: string
          DiagnosticIds: string list }

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
    type HelpCommandEntry =
        { Name: string
          Description: string }

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

    type CommandReport =
        { SchemaVersion: int
          ReportVersion: string
          Command: SddCommand
          ProjectRoot: string
          OutputFormat: OutputFormat
          DryRun: bool
          OverwritePolicy: OverwritePolicy
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
          GeneratedViews: GeneratedViewState list
          Diagnostics: Diagnostic list
          GovernanceCompatibility: GovernanceCompatibilityFact list
          NextAction: NextAction option
          Help: HelpSummary option }

    type CommandEffect =
        | ReadFile of path: string
        | EnumerateDirectory of path: string
        | CreateDirectory of path: string
        | WriteFile of path: string * text: string * kind: ArtifactWriteKind
        | RunProcess of command: string * args: string list * workingDir: string
        | SetExecutable of path: string
        | EmitStdout of text: string
        | EmitStderr of text: string
        | SetExitCode of code: int

    /// Captured outcome of a `RunProcess` effect at the edge. `Started = false`
    /// means the process could not be launched (engine/command absent). Process
    /// stdout/stderr are intentionally excluded from the deterministic contract.
    type ProcessRunResult =
        { Started: bool
          ExitCode: int }

    type CommandEffectResult =
        { Effect: CommandEffect
          Succeeded: bool
          Snapshot: FileSnapshot option
          Process: ProcessRunResult option
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
          GeneratedViews: GeneratedViewState list
          Report: CommandReport option }

    type CommandMsg =
        | LoadProject
        | LoadWorkItem
        | ApplyUserIntent
        | PlanGeneratedViewRefresh
        | EffectInterpreted of CommandEffectResult
        | BuildReport

    val commandName: command: SddCommand -> string
    val commandStage: command: SddCommand -> string
    val parseCommand: value: string -> Result<SddCommand, string>
    val outputFormatValue: format: OutputFormat -> string
    val overwritePolicyValue: policy: OverwritePolicy -> string
    val writeKindValue: kind: ArtifactWriteKind -> string
    val artifactOperationValue: operation: ArtifactOperation -> string
    val generatedViewCurrencyValue: currency: GeneratedViewCurrency -> string
    val guidanceDispositionValue: disposition: GuidanceDisposition -> string
    val refreshDispositionValue: disposition: RefreshDisposition -> string
    val outcomeValue: outcome: CommandOutcome -> string
    val nextLifecycleCommand: command: SddCommand -> SddCommand option
    val effectPath: effect: CommandEffect -> string option
