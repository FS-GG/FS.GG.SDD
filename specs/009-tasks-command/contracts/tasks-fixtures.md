# Contract: Tasks Fixture Families

## Purpose

The tasks implementation must add fixture-backed tests that prove valid task
creation, safe reruns, blocked graph states, generated-view behavior,
deterministic output, text projection, and no-Governance operation.

Fixtures should use real temporary filesystem roots when testing command
behavior. Synthetic source content is acceptable only when the test name or
nearby comment discloses what real path it stands in for.

## Valid Fixture Families

| Family | Required behavior |
|---|---|
| `tasks-create` | Creates `work/<id>/tasks.yml`, emits task summary, refreshes or reports generated work-model state, and points next action to `analyze`. |
| `tasks-rerun-preserves-status` | Rerun preserves task ids, statuses, owners, dependencies, required skills, required evidence, skip rationales, and user notes. |
| `tasks-adds-missing-items` | Compatible source additions add new required tasks without renumbering existing tasks. |
| `tasks-preserves-stable-ids` | Existing `T###` ids remain stable across repeated task command runs. |
| `tasks-records-required-skills` | Task entries include required skills or capability tags derived from source facts and implementation obligations. |
| `tasks-records-evidence-obligations` | Task entries include required evidence ids and later evidence obligations can reference them. |
| `tasks-accepted-deferral` | Accepted deferrals remain visible as task dispositions and do not hide blocking identity or graph defects. |
| `dry-run` | Reports proposed authored and generated changes without mutating `tasks.yml` or `work-model.json`. |
| `deterministic-report` | Three identical runs over identical input produce byte-identical JSON reports. |
| `text-projection` | Text output includes only facts present in the JSON report. |
| `governance-boundary` | Task creation works without Governance files and reports optional Governance pointers only as not-evaluated compatibility facts. |

## Blocked Fixture Families

| Family | Required behavior |
|---|---|
| `outside-project` | Blocks outside an initialized SDD project and writes no task artifact. |
| `missing-specification` | Blocks when `work/<id>/spec.md` is missing. |
| `missing-clarification` | Blocks when `work/<id>/clarifications.md` is missing. |
| `missing-checklist` | Blocks when `work/<id>/checklist.md` is missing. |
| `missing-plan` | Blocks when `work/<id>/plan.md` is missing. |
| `failed-plan` | Blocks when plan facts are stale, incomplete, or contain blocking findings. |
| `malformed-work-id` | Blocks malformed selected work ids before source reads or writes. |
| `malformed-tasks` | Blocks malformed `tasks.yml` schema or unsupported version before mutation. |
| `duplicate-work-id` | Blocks duplicate logical work ids and reports all known conflicting paths. |
| `duplicate-task-id` | Blocks duplicate task ids without mutating existing content. |
| `unknown-source-reference` | Blocks tasks that reference unknown requirements, decisions, plan decisions, contracts, verification obligations, migration notes, generated-view impacts, or evidence ids. |
| `dependency-cycle` | Blocks dependency cycles and reports the involved task ids. |
| `tasks-identity-mismatch` | Blocks when existing `tasks.yml` belongs to another work id. |
| `unsafe-overwrite` | Blocks removal, renumbering, destructive dependency changes, unsupported status changes, or ambiguous YAML edits. |
| `done-task-missing-evidence` | Blocks or marks task-readiness defective when a completed task lacks required evidence. |
| `stale-generated-view` | Reports stale, malformed, missing, or blocked generated work-model state without treating the view as current. |

## Evidence Requirements

Implementation readiness evidence must include:

- clean Release build;
- focused artifact-model task tests;
- focused command workflow task tests;
- create/rerun/output/blocker test transcripts;
- deterministic JSON comparison evidence;
- text projection human-summary review;
- disposable-directory CLI JSON, dry-run, and text smoke output;
- FSI or prelude transcript for public tasks surface;
- performance evidence for create and rerun fixtures;
- SDD/Governance boundary review;
- artifact traceability review mapping spec requirements to plan, tasks,
  tests, and readiness evidence.
