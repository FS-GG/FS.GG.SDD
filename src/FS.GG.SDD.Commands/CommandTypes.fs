namespace FS.GG.SDD.Commands

open System
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
        | other -> Error $"Unknown SDD command '{other}'."

    let outputFormatValue (format: OutputFormat) =
        match format with
        | Json -> "json"
        | Text -> "text"

    let overwritePolicyValue (policy: OverwritePolicy) =
        match policy with
        | RefuseUnsafe -> "refuseUnsafe"
        | AllowGeneratedRefresh -> "allowGeneratedRefresh"

    let writeKindValue (kind: ArtifactWriteKind) =
        match kind with
        | AuthoredSource -> "authored"
        | StructuredSource -> "structured"
        | GeneratedView -> "generated"
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
        | Analyze -> None

    let effectPath (effect: CommandEffect) =
        match effect with
        | ReadFile path
        | EnumerateDirectory path
        | CreateDirectory path
        | WriteFile(path, _, _) -> Some path
        | EmitStdout _
        | EmitStderr _
        | SetExitCode _ -> None
