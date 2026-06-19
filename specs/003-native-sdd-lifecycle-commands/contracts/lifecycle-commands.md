# Contract: Lifecycle Commands

## Scope

This contract defines the behavior of the native `fsgg-sdd` commands introduced
by this feature. All commands emit a deterministic command report. Commands that
change files plan safe write effects before interpretation and report generated
view currency after the command completes or is blocked.

## Shared Rules

- All paths in command reports are repository-relative and use `/`.
- Markdown files are authoring surfaces.
- Schema-versioned structured artifacts are the machine contract.
- Existing user-authored content is preserved unless the command can prove the
  update is safe.
- Generated views are refreshed only when source data is valid enough.
- Missing, stale, malformed, or blocked generated views are reported with
  diagnostics.
- SDD remains usable without Governance installed.
- Governance files, when present, are optional compatibility boundaries only.

## `fsgg-sdd init`

Purpose: create the minimum SDD project skeleton.

Required created or verified artifacts:

- `.fsgg/project.yml`
- `.fsgg/sdd.yml`
- `.fsgg/agents.yml`
- `work/`
- `readiness/`
- Claude and Codex guidance targets named in `.fsgg/agents.yml`

Behavior:

- Empty target directories receive the full SDD skeleton.
- Unrelated user files are preserved.
- Existing lifecycle paths are compared before any write.
- Unsafe conflicts are reported and refused before overwrite.
- Optional Governance files are not required and are not created by default.
- Generated agent command guidance is not created in this feature.

Generated-view behavior:

- No work-item generated view is required during project-only initialization.
- The command report lists every created, preserved, refused, or unchanged
  artifact.

Next action:

- `charter` for the first selected work item or project lifecycle slice.

## `fsgg-sdd charter`

Purpose: create or update `work/<id>/charter.md`.

Required prerequisites:

- Initialized SDD project.
- Valid selected work id.

Behavior:

- Records lifecycle principles, scope boundaries, and policy pointers for the
  selected work item.
- Preserves existing prose and structured front matter unless a safe update is
  planned.
- Reports missing project configuration or malformed work id before writes.

Generated-view behavior:

- Refreshes `readiness/<id>/work-model.json` when enough source data exists.
- Reports blocked or stale generated-view diagnostics when required sources are
  incomplete.

Next action:

- `specify`.

## `fsgg-sdd specify`

Purpose: create or update `work/<id>/spec.md`.

Required prerequisites:

- Initialized SDD project.
- Valid selected work id.

Behavior:

- Records user value, scope, non-goals, user stories, functional requirements,
  acceptance scenarios, change tier, and ambiguity state.
- Links structured work-item metadata to the selected work id.
- Preserves user-authored content during re-runs.

Generated-view behavior:

- Refreshes `work-model.json` when the spec and project sources are valid.
- Emits requirement typing, prose/structured mismatch, malformed schema, and
  stale-view diagnostics as applicable.

Next action:

- `clarify`.

## `fsgg-sdd clarify`

Purpose: create or update `work/<id>/clarifications.md`.

Required prerequisites:

- Initialized SDD project.
- Valid selected work id.
- Existing specification artifact.

Behavior:

- Records ambiguity questions, answers, decision ids, and source links.
- Keeps clarification answers tied to the same work id as the spec.
- Reports missing or malformed specification data instead of guessing.

Generated-view behavior:

- Refreshes `work-model.json` when clarification decisions can be normalized.
- Reports stale generated views when clarification data cannot be parsed.

Next action:

- `checklist`.

## `fsgg-sdd checklist`

Purpose: create or update `work/<id>/checklist.md`.

Required prerequisites:

- Initialized SDD project.
- Valid selected work id.
- Existing specification and clarification artifacts, or diagnostics explaining
  why clarification is not required.

Behavior:

- Records requirements-quality checks for ambiguity, testability, scope,
  measurable outcomes, non-goals, and change-tier obligations.
- Reports unresolved requirement gaps with stable diagnostics.

Generated-view behavior:

- Refreshes `work-model.json` when checklist status and requirements are
  consistent.
- Reports blocked refresh when checklist findings invalidate later planning.

Next action:

- `plan`.

## `fsgg-sdd plan`

Purpose: create or update `work/<id>/plan.md` and `work/<id>/contracts/`.

Required prerequisites:

- Initialized SDD project.
- Valid selected work id.
- Specification, clarification, and checklist artifacts sufficient for
  planning.

Behavior:

- Records technical context, architecture decisions, contracts, public API
  impact, dependency choices, generated-view behavior, verification strategy,
  migration posture, and Governance boundary notes.
- Creates or preserves contract documents under `work/<id>/contracts/`.
- Reports unresolved planning prerequisites instead of generating unsupported
  assumptions.

Generated-view behavior:

- Refreshes `work-model.json` when plan decisions and source digests are valid.
- Reports unknown decision references or stale generated views.

Next action:

- `tasks`.

## `fsgg-sdd tasks`

Purpose: create or update `work/<id>/tasks.yml`.

Required prerequisites:

- Initialized SDD project.
- Valid selected work id.
- Specification, clarification, checklist, plan, and contract artifacts
  sufficient for task generation.

Behavior:

- Records typed implementation task graph entries with ids, owners,
  dependencies, requirement references, decision references, required skills,
  and required evidence.
- Uses structured `tasks.yml` as the machine contract.
- Does not introduce task/evidence update commands or mark implementation work
  complete.

Generated-view behavior:

- Refreshes `work-model.json` when the task graph is valid.
- Emits duplicate task, unknown reference, cyclic dependency, missing skill, or
  stale-view diagnostics as applicable.

Next action:

- `analyze`.

## `fsgg-sdd analyze`

Purpose: check cross-artifact consistency and emit
`readiness/<id>/analysis.json`.

Required prerequisites:

- Initialized SDD project.
- Valid selected work id.
- Available lifecycle sources for the selected work item.

Behavior:

- Loads project settings, work-item metadata, spec, clarifications, checklist,
  plan, contracts, and task graph artifacts.
- Normalizes sources into the work model.
- Reports consistency diagnostics for missing prerequisites, malformed
  artifacts, unknown references, task graph conflicts, requirement typing gaps,
  generated-view currency, and optional Governance boundary issues.
- Does not run implementation, evidence freshness, verify, ship, route, profile,
  gate, or protected-branch behavior.

Generated-view behavior:

- Refreshes `work-model.json` when enough valid data exists.
- Emits `analysis.json` from the same normalized facts and diagnostics.
- Reports blocked analysis if required sources are malformed.

Next action:

- Implementation planning can proceed when no blocking diagnostics remain.
- Otherwise the next action points to the artifact that must be corrected.

## Explicit Non-Responsibilities

This command feature does not introduce:

- task or evidence update commands;
- `fsgg-sdd verify`;
- `fsgg-sdd ship`;
- full generated-view refresh command;
- generated agent command or skill files;
- product runtime templates;
- rendering assets;
- package release behavior;
- Governance route, freshness, profile, gate, or enforcement behavior.
