# Data Model: Native SDD Lifecycle Commands

## SDDProject

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agent config path, generated-view policy, schema migration posture, agent
  guidance targets, optional Governance boundary pointers
- **Validation**: Paths are repository-relative and normalized with `/`;
  required SDD files use current schema versions; Governance pointers are
  optional references only.
- **Relationships**: Loaded by every command except a first `init`; contributes
  project context to command reports and generated work-model refreshes.

## LifecycleCommand

- **Allowed values**: `init`, `charter`, `specify`, `clarify`, `checklist`,
  `plan`, `tasks`, `analyze`
- **Validation**: Unknown command names produce a command diagnostic before any
  filesystem write is requested.
- **Relationships**: Selects the command intent, lifecycle stage, expected
  artifacts, prerequisites, generated-view refresh behavior, and next action.

## CommandInvocation

- **Fields**: command, project root token, optional work id, normalized options,
  output format, dry-run flag, overwrite policy, input text digest when
  supplied, generator version
- **Validation**: Authoritative content uses normalized project-relative paths;
  absolute host paths and wall-clock data are excluded from deterministic
  reports; work ids must satisfy the existing `WorkId` contract.
- **Relationships**: Input to the command workflow and recorded in the
  `CommandReport`.

## CommandWorkflowState

- **Fields**: invocation, loaded project, selected work item, source snapshots,
  normalized work model when available, pending artifact changes, generated-view
  plans, diagnostics, outcome, next action
- **Validation**: State transitions are pure and deterministic for the same
  loaded snapshots and invocation.
- **State transitions**: `initialized` -> `projectLoaded` -> `intentApplied`
  -> `effectsPlanned` -> `effectsInterpreted` -> `reported`. Invalid input may
  move directly to `reported` with a blocking diagnostic and no write effects.

## CommandMsg

- **Fields**: lifecycle event name plus event-specific payload
- **Required messages**: load project, load work item, apply user intent, plan
  safe writes, refresh generated views, record interpreted effect, render report
- **Validation**: Messages cannot mutate host state directly; they only produce
  a new model and requested effects.
- **Relationships**: Consumed by `update` to move the `CommandWorkflowState`.

## CommandEffect

- **Allowed values**: read file, enumerate directory, create directory, write
  file, skip unsafe write, delete generated temporary output if needed, emit
  stdout, emit stderr, set process exit code
- **Validation**: Write effects must carry artifact kind, source digest or
  output digest where applicable, overwrite policy, and whether the target is
  authored or generated. Effects never contain implicit clocks.
- **Relationships**: Interpreted only by the CLI edge layer or test harness.

## ArtifactChange

- **Fields**: path, artifact kind, ownership (`authored`, `structured`,
  `generated`, `agentTarget`), operation (`create`, `update`, `preserve`,
  `refuse`, `noChange`), before digest, after digest, safe-write decision,
  related diagnostics
- **Validation**: User-authored content is never overwritten unless the planned
  operation is explicitly safe. Generated artifacts record source identities
  and generator version.
- **Relationships**: Appears in command reports, generated-view refresh plans,
  tests, and traceability evidence.

## SafeWriteDecision

- **Fields**: path, target class, requested operation, decision, reason,
  correction, existing digest, proposed digest
- **Allowed decisions**: `safe`, `preserveExisting`, `refuseConflict`,
  `generatedRefresh`, `dryRunOnly`
- **Validation**: Conflicts for authored content produce blocking diagnostics
  before a write effect is interpreted.
- **Relationships**: Drives `ArtifactChange` and unsafe-overwrite diagnostics.

## WorkItemCommandState

- **Source**: `work/<id>` authored and structured artifacts
- **Fields**: work id, title, current lifecycle stage, status, existing stage
  artifacts, requirements, decisions, task graph, generated-view state,
  diagnostics
- **Validation**: Work id must match selected id and structured metadata; prior
  stage prerequisites are reported when missing or malformed.
- **State transitions**: `none` -> `charter` -> `specify` -> `clarify` ->
  `checklist` -> `plan` -> `tasks` -> `analyze`. Commands may be re-run for
  safe updates and must report incomplete prior artifacts instead of guessing.

## CommandReport

- **Generated view**: command stdout or requested report file
- **Fields**: schema version, report version, command identity, selected
  context, invocation, outcome, changed artifacts, generated views,
  diagnostics, next action, optional Governance compatibility facts
- **Validation**: JSON is byte-stable for identical inputs; lists are sorted by
  documented keys; diagnostics reuse the stable diagnostic contract; text output
  is rendered from this value.
- **Relationships**: Authoritative command result for humans, agents, CI, and
  optional Governance consumers.

## GeneratedViewState

- **Sources**: Existing generated view snapshots and newly generated output
- **Fields**: path, kind, schema version, generator id, generator version,
  source identities, output digest, currency status, diagnostics
- **Allowed kinds in this feature**: `workModel`, `analysis`
- **Validation**: Missing, malformed, stale source digest, stale generator
  version, blocked refresh, and output-digest mismatch emit diagnostics.
- **Relationships**: Included in command reports; `workModel` uses existing
  `FS.GG.SDD.Artifacts` generation metadata; `analysis` is emitted by
  `analyze`.

## AnalysisView

- **Generated view**: `readiness/<id>/analysis.json`
- **Fields**: schema version, work id, analyzed sources, prerequisite status,
  cross-artifact diagnostics, implementation readiness, next action,
  generated-view currency summary
- **Validation**: Derived from the normalized work model and command
  diagnostics; does not add route, profile, freshness, gate, or enforcement
  facts.
- **Relationships**: Produced by `analyze` and referenced by its
  `CommandReport`.

## CommandDiagnostic

- **Fields**: diagnostic id, severity, artifact, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, unknown command,
  malformed work id, missing artifact, malformed schema version, duplicate id,
  unknown reference, missing prerequisite, unsafe overwrite, stale generated
  view, blocked generated-view refresh, optional Governance boundary issue,
  tool defect
- **Validation**: Diagnostics must distinguish malformed user input from tool
  defects and sort by severity rank, id, artifact path, source location, and
  message.
- **Relationships**: Appears in command reports, generated views, tests, and
  readiness evidence.

## TextProjection

- **Source**: `CommandReport`
- **Fields**: command name, outcome, changed artifact summary, generated-view
  summary, diagnostics summary, next action
- **Validation**: Contains no facts absent from the report; wrapping, ANSI
  styling, and terminal width do not affect JSON output.
- **Relationships**: Human stdout mode for the CLI.

## GovernanceCompatibilityFact

- **Source**: Optional Governance pointers in SDD-owned configuration or work
  artifacts
- **Fields**: path, relationship, required by SDD flag, observed state,
  diagnostic ids
- **Validation**: Missing or malformed Governance files do not block SDD-only
  command execution unless an SDD artifact explicitly requires that boundary.
- **Relationships**: Exposed for future Governance consumers without executing
  Governance route, freshness, profile, gate, or enforcement semantics.

## NextAction

- **Fields**: action id, command, work id when applicable, reason, required
  artifacts, blocking diagnostic ids
- **Validation**: A successful command points at the next expected lifecycle
  command. A blocked command points at the user-correctable artifact.
- **Relationships**: Included in `CommandReport` and text projection.
