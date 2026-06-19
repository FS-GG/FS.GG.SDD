# Contract: Command API

## Scope

This contract describes the public library surface for native SDD lifecycle
commands. The surface belongs in `FS.GG.SDD.Commands` and depends on
`FS.GG.SDD.Artifacts` for lifecycle artifact parsing, normalized work-model
generation, source digests, generated-view currency metadata, and diagnostics.

The contract does not define Governance route selection, evidence freshness,
profile adjustment, protected-boundary gate enforcement, release behavior,
rendering templates, task/evidence update commands, verify commands, or ship
commands.

## Public Modules

The implementation should expose these modules with `.fsi` signatures before
`.fs` bodies:

- `CommandTypes` for command ids, options, workflow state, messages, effects,
  outcomes, and next actions.
- `CommandReports` for command report records, artifact changes,
  generated-view states, and report diagnostics.
- `CommandWorkflow` for pure `init` and `update` transitions.
- `CommandEffects` for the edge interpreter contract and test interpreter.
- `CommandSerialization` for deterministic JSON report serialization.
- `CommandRendering` for plain text projection from a command report.

The `FS.GG.SDD.Cli` executable should remain a thin host. If it grows reusable
public modules, those modules require matching `.fsi` files and surface tests.

## Command Identity

```fsharp
type SddCommand =
    | Init
    | Charter
    | Specify
    | Clarify
    | Checklist
    | Plan
    | Tasks
    | Analyze
```

Validation rules:

- Command names accepted by the CLI are lowercase: `init`, `charter`,
  `specify`, `clarify`, `checklist`, `plan`, `tasks`, `analyze`.
- Unknown command names produce a blocking diagnostic and no write effects.
- Commands after `init` require an SDD project root.

## Command Request

```fsharp
type OutputFormat =
    | Json
    | Text

type OverwritePolicy =
    | RefuseUnsafe
    | AllowGeneratedRefresh

type CommandRequest =
    { Command: SddCommand
      ProjectRoot: string
      WorkId: string option
      Title: string option
      InputText: string option
      OutputFormat: OutputFormat
      DryRun: bool
      OverwritePolicy: OverwritePolicy
      GeneratorVersion: SchemaVersion.GeneratorVersion }
```

Validation rules:

- `ProjectRoot` is normalized to a deterministic token in reports; artifact
  paths are repository-relative.
- `WorkId` is required for all work-item commands and must satisfy the existing
  work-id contract.
- `DryRun` plans effects and reports what would change, but does not write.
- `OverwritePolicy` allows generated-view refreshes but refuses unsafe
  authored-content overwrites.

## Workflow Boundary

```fsharp
type CommandModel =
    { Request: CommandRequest
      Project: LifecycleArtifacts.ProjectLifecycleConfig option
      WorkModel: WorkModel.WorkModel option
      PendingEffects: CommandEffect list
      Diagnostics: Diagnostics.Diagnostic list
      Report: CommandReport option }

type CommandMsg =
    | LoadProject
    | LoadWorkItem
    | ApplyUserIntent
    | PlanGeneratedViewRefresh
    | EffectInterpreted of CommandEffectResult
    | BuildReport

val init: request: CommandRequest -> CommandModel * CommandEffect list
val update: msg: CommandMsg -> model: CommandModel -> CommandModel * CommandEffect list
```

Validation rules:

- `init` and `update` are pure and deterministic for the same request and
  loaded snapshots.
- Pure transitions do not read or write the host filesystem directly.
- Blocking diagnostics prevent write effects.
- A final report is built even for blocked commands.

## Effects

```fsharp
type ArtifactWriteKind =
    | AuthoredSource
    | StructuredSource
    | GeneratedView
    | AgentGuidanceTarget

type CommandEffect =
    | ReadFile of path: string
    | EnumerateDirectory of path: string
    | CreateDirectory of path: string
    | WriteFile of path: string * text: string * kind: ArtifactWriteKind
    | EmitStdout of text: string
    | EmitStderr of text: string
    | SetExitCode of code: int
```

Validation rules:

- Paths are repository-relative after project root discovery.
- Write effects must be preceded by safe-write planning.
- Authored source writes are refused when an existing file would be overwritten
  unsafely.
- Generated-view writes include source identities, generator version, and
  output digest in the resulting report.

## Effect Interpreter

```fsharp
type CommandEffectResult =
    { Effect: CommandEffect
      Succeeded: bool
      Snapshot: LifecycleArtifacts.FileSnapshot option
      Diagnostic: Diagnostics.Diagnostic option }

val interpret:
    projectRoot: string ->
    effect: CommandEffect ->
        CommandEffectResult
```

Validation rules:

- The real interpreter is the only layer that touches the filesystem.
- Test interpreters can supply snapshots and capture writes without modifying
  disk.
- I/O failures become diagnostics that distinguish user-correctable input from
  tool defects.

## Command Report API

```fsharp
type CommandOutcome =
    | Succeeded
    | SucceededWithWarnings
    | Blocked
    | NoChange

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
      Outcome: CommandOutcome
      WorkId: string option
      ChangedArtifacts: ArtifactChange list
      GeneratedViews: GeneratedViewState list
      Diagnostics: Diagnostics.Diagnostic list
      GovernanceCompatibility: GovernanceCompatibilityFact list
      NextAction: NextAction option }

val serializeReport: report: CommandReport -> string
val renderText: report: CommandReport -> string
```

Validation rules:

- `serializeReport` emits deterministic JSON using the
  [command-report-json.md](command-report-json.md) contract.
- `renderText` is a projection from `CommandReport` and cannot introduce facts
  that are absent from the report.
- Diagnostics reuse existing stable diagnostic ids where possible and add
  command-specific ids only when needed.

## Exit Codes

- `0`: command succeeded or produced warnings without blocking diagnostics.
- `1`: command was blocked by user-correctable diagnostics.
- `2`: command failed because of a tool defect or unexpected interpreter error.

Exit codes are presentation and process behavior; the command report remains
the authoritative result.

## Governance Boundary

The API may include optional Governance compatibility facts by path and
relationship. It must not parse `.fsgg/policy.yml`, `.fsgg/capabilities.yml`,
or `.fsgg/tooling.yml` as Governance schemas, select routes, compute evidence
freshness, adjust severities by profile, select gates, or emit protected-branch
verdicts.
