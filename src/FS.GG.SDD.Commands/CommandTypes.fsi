namespace FS.GG.SDD.Commands

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.LifecycleArtifacts
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

    type OutputFormat =
        | Json
        | Text

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
          GeneratorVersion: GeneratorVersion }

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
          GeneratedViews: GeneratedViewState list
          Diagnostics: Diagnostic list
          GovernanceCompatibility: GovernanceCompatibilityFact list
          NextAction: NextAction option }

    type CommandEffect =
        | ReadFile of path: string
        | EnumerateDirectory of path: string
        | CreateDirectory of path: string
        | WriteFile of path: string * text: string * kind: ArtifactWriteKind
        | EmitStdout of text: string
        | EmitStderr of text: string
        | SetExitCode of code: int

    type CommandEffectResult =
        { Effect: CommandEffect
          Succeeded: bool
          Snapshot: FileSnapshot option
          Diagnostic: Diagnostic option }

    type CommandModel =
        { Request: CommandRequest
          PendingEffects: CommandEffect list
          InterpretedEffects: CommandEffectResult list
          Diagnostics: Diagnostic list
          Specification: SpecificationSummary option
          Clarification: ClarificationSummary option
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
    val outcomeValue: outcome: CommandOutcome -> string
    val nextLifecycleCommand: command: SddCommand -> SddCommand option
    val effectPath: effect: CommandEffect -> string option
