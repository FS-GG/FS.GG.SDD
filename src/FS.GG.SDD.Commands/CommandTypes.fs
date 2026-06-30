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
          Provider: string option
          Parameters: (string * string) list
          Force: bool
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

    type ScaffoldSummary =
        { ProviderName: string option
          ProviderContractVersion: string option
          Outcome: string
          SkeletonCreated: bool
          ProviderInvoked: bool
          ProducedPathCount: int
          ProducedPaths: string list
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

    type HelpFlag =
        { Name: string
          Argument: string option
          Description: string }

    type HelpCommandEntry =
        { Name: string
          Description: string }

    type HelpScope =
        | TopLevel
        | Command of string

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
        | other -> Error $"Unknown SDD command '{other}'."

    let outputFormatValue (format: OutputFormat) =
        match format with
        | Json -> "json"
        | Text -> "text"
        | Rich -> "rich"

    let overwritePolicyValue (policy: OverwritePolicy) =
        match policy with
        | RefuseUnsafe -> "refuseUnsafe"
        | AllowGeneratedRefresh -> "allowGeneratedRefresh"

    let writeKindValue (kind: ArtifactWriteKind) =
        match kind with
        | AuthoredSource -> "authoredSource"
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

    let outcomeValue (outcome: CommandOutcome) =
        match outcome with
        | CommandOutcome.Succeeded -> "succeeded"
        | CommandOutcome.SucceededWithWarnings -> "succeededWithWarnings"
        | CommandOutcome.Blocked -> "blocked"
        | CommandOutcome.NoChange -> "noChange"

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

    let effectPath (effect: CommandEffect) =
        match effect with
        | ReadFile path
        | EnumerateDirectory path
        | CreateDirectory path
        | WriteFile(path, _, _) -> Some path
        | RunProcess(_, _, workingDir) -> Some workingDir
        | SetExecutable path -> Some path
        | EmitStdout _
        | EmitStderr _
        | SetExitCode _ -> None
