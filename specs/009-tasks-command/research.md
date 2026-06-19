# Research: Tasks Command

## Decision: Extend The Existing Lifecycle Command Stack

Use the existing `FS.GG.SDD.Commands` library and `FS.GG.SDD.Cli` host for
`fsgg-sdd tasks`. The command enum already includes `Tasks`, so the feature
should remove the unsupported-command path for that command and reuse the
existing request, effect, interpreter, report, serialization, rendering, and
fixture patterns.

**Rationale**: `tasks` is a stateful lifecycle command with the same shape as
the preceding command slices: load project context, validate one work item,
plan safe authored-source changes, refresh or diagnose generated views, and
emit one deterministic report.

**Alternatives considered**:

- Add a separate task-generation executable. Rejected because it would split
  the lifecycle command contract and duplicate effect/report machinery.
- Treat task generation as a pure artifact parser only. Rejected because the
  feature must author `work/<id>/tasks.yml`, report next actions, and refresh
  generated-view state.

## Decision: `tasks.yml` Is The Structured Authored Task Source

Represent the native SDD task graph as `work/<id>/tasks.yml`, with
`schemaVersion: 1`, selected work identity, prerequisite source links, source
snapshots, task entries, dependency relationships, required skills, required
evidence obligations, task findings, and lifecycle notes.

**Rationale**: The constitution requires schema-versioned structured artifacts
to be the machine contract. Tasks are already declared as structured lifecycle
data in the artifact model and normalized work model. YAML keeps task state
editable by humans while remaining directly parseable by tools.

**Alternatives considered**:

- Store native tasks in Markdown. Rejected because task graph state,
  dependencies, stable ids, and evidence obligations need a structured machine
  contract.
- Store tasks only in generated readiness JSON. Rejected because generated
  views are outputs and their presence is not proof of currency.

## Decision: Keep Spec Kit `tasks.md` Separate From Native SDD `tasks.yml`

Use `specs/009-tasks-command/tasks.md` only for this repository's Spec Kit
implementation task list, generated later by `$speckit-tasks`. The command
being planned writes consumer-project task state to `work/<id>/tasks.yml`.

**Rationale**: The repository uses Spec Kit to develop FS.GG.SDD itself, while
the product being built exposes native lifecycle artifacts to consumers. Mixing
those files would blur planning evidence with the product contract.

**Alternatives considered**:

- Reuse `specs/<feature>/tasks.md` as the native command output. Rejected
  because it is a Spec Kit implementation artifact, not a consumer project
  lifecycle contract.
- Generate both files in this command. Rejected because `$speckit-tasks`
  remains responsible for repository implementation tasks.

## Decision: Add Narrow Task-Specific Facts Without Replacing Existing Ids

Extend the artifact model around existing `TaskId` and `EvidenceId` contracts
instead of inventing a separate task-id family. Task entries should keep
`T001`-style ids and required evidence should point at `EV001`-style
obligations or declarations. Add task source snapshots, task dispositions,
graph-readiness summaries, stale-task state, and task findings as narrow
facts around the existing model.

**Rationale**: Existing public signatures already expose `TaskId`, `WorkTask`,
`TaskStatus`, `EvidenceId`, and `parseTasks`. This feature needs richer
command/report behavior, not a parallel identifier system.

**Alternatives considered**:

- Introduce new id formats for task obligations. Rejected because evidence ids
  already exist and later evidence declarations need stable references.
- Use free-form strings for task references. Rejected because it weakens graph
  validation and readiness diagnostics.

## Decision: Diagnose-Only Schema Migration For Task Version 1

Use `schemaVersion: 1` for `tasks.yml` and task command reports, accept only
current version 1 in this slice, and block future, unsupported, malformed, or
deprecated versions with actionable diagnostics.

**Rationale**: The first tasks command must establish a stable contract without
inventing migration behavior before a second version exists. This matches the
preceding lifecycle command slices.

**Alternatives considered**:

- Attempt automatic migration of unknown task schemas. Rejected because no
  historical native task schema exists and unsafe rewrites would risk authored
  task state.
- Ignore schema versions. Rejected because schema-versioned artifacts are the
  machine contract.

## Decision: Preserve Existing Task State And Mark Stale Source Links

On rerun, preserve existing task ids, titles, statuses, ownership,
dependencies, required skills, required evidence obligations, skip rationales,
source links, and lifecycle notes unless a safe non-destructive update can be
proven. If referenced specification, clarification, checklist, or plan facts
change, mark affected task entries stale or needing review rather than treating
them as current.

**Rationale**: Task ids and statuses carry coordination value once
implementation begins. Silent rewrites would break traceability and could hide
work already in progress.

**Alternatives considered**:

- Regenerate the task graph from the plan on every run. Rejected because it
  would discard human task state and renumber work.
- Never update an existing task file. Rejected because compatible additions
  from changed plans should be safe and visible.

## Decision: Validate The Task Graph Before Any Authored Write

Validate duplicate task ids, unknown task dependencies, self-dependencies,
dependency cycles, unknown requirement or decision references, missing source
links, unsupported status transitions, and completed tasks without required
evidence before writing `tasks.yml`.

**Rationale**: Defective task graphs cascade into analysis, implementation,
evidence, generated readiness views, and optional Governance-compatible
contracts. Blocking before mutation keeps failure states inspectable and
repairable.

**Alternatives considered**:

- Write best-effort tasks and let analyze find graph defects later. Rejected
  because the command's success state would misrepresent readiness.
- Permit cycles or unknown references with warnings. Rejected because later
  stages need a graph they can traverse deterministically.

## Decision: Command Report JSON Is The Immediate Automation Contract

Expose task context, artifact changes, task summary, graph readiness,
generated-view state, diagnostics, optional Governance compatibility facts,
and next action in the existing command-report JSON family, with text output
rendered as a projection.

**Rationale**: Humans, CLI callers, CI, agents, and optional Governance
consumers need the same facts without treating terminal text as authoritative.

**Alternatives considered**:

- Emit only text for `tasks`. Rejected because deterministic JSON is required
  for automation.
- Add a separate report format for tasks. Rejected because existing lifecycle
  command reports already define the shared command surface.

## Decision: Keep Governance Integration Advisory And Optional

Report optional Governance pointers as compatibility facts only. Do not parse
Governance-owned schemas, evaluate routes, compute freshness, select profiles,
select gates, verify evidence freshness, or enforce protected boundaries.

**Rationale**: FS.GG.SDD must remain independently useful without Governance.
Governance owns rule evaluation, evidence freshness, routing, profiles, and
gate enforcement.

**Alternatives considered**:

- Block task creation when Governance files are absent. Rejected because that
  violates the no-Governance workflow requirement.
- Evaluate Governance route or gate state in tasks. Rejected because that
  would cross the repository boundary defined by the constitution.
