---
name: speckit-tasks
description: Generate the feature's tasks.md from its spec and plan.
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: github-spec-kit
  source: preset:fsharp-opinionated
user-invocable: true
disable-model-invocation: false
---

# Speckit Tasks Skill

# /speckit.tasks

Generate the feature's task breakdown from its spec and plan into a single
`tasks.md` in `specs/[FEATURE_ID]/`. Use the `tasks-template.md` as the starting
point; replace the example task bodies with real work items derived from the spec
and plan.

This repository follows standard Spec Kit: `tasks.md` is an authored checklist,
not a generated dependency graph. There is no `tasks.deps.yml`, no DAG validator,
and no evidence audit — express ordering directly in `tasks.md` (phases run in
sequence; tasks within a phase may run in parallel).

## Status legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per
  Principle V — see `/speckit.implement`)
- `[-]` — skipped, with written rationale on the task line

Never mark a failing task `[X]`. Never weaken an assertion to green a build —
narrow the scope and document it.

## Discipline

- **Story grouping.** Tasks belong to a phase (Phase 1..N) and optionally a
  user story (`[US1]`, `[US2]`, ...). Keep phases sequential; stories within a
  phase may run in parallel.
- **Tier annotation.** Mark each task `[T1]` or `[T2]` if the phase
  classification differs from the spec's overall tier. Omit when it matches.
- **Parallel-safe marker.** `[P]` means "no dependency on another incomplete
  task in this phase." Emit it as a hint where it holds.
- **Cross-task dependencies.** When a task depends on a non-obvious earlier task
  (beyond plain phase ordering), state it in the task description (e.g. "after
  T011") or in a short Dependencies section. Keep it readable; do not maintain a
  separate machine-validated graph.
- **Elmish/MVU applicability.** For any stateful or I/O-bearing story, emit
  explicit tasks for the `.fsi` contract (`Model`, `Msg`, `Effect` or
  `Cmd<Msg>`, `init`, `update`, interpreter boundary), pure transition tests,
  emitted-effect assertions, and real interpreter evidence where safe. For a
  simple pure feature, note in the evidence-obligations task that Principle IV is
  not applicable.

## Completion

Report the path to the generated `tasks.md`, the task count per user story, the
parallel opportunities identified, and the suggested MVP scope (typically User
Story 1). Each task must be specific enough that it can be completed without
additional context: a clear action and an exact file path.
