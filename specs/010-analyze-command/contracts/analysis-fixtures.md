# Contract: Analysis Fixture Families

## Purpose

The analyze implementation must add fixture-backed tests that prove valid
analysis generation, non-destructive source handling, blocked consistency
states, generated-view behavior, deterministic output, text projection, and
no-Governance operation.

Fixtures should use real temporary filesystem roots when testing command
behavior. Synthetic source content is acceptable only when the test name or
nearby comment discloses what real path it stands in for.

## Valid Fixture Families

| Family | Required behavior |
|---|---|
| `analysis-create` | Creates `readiness/<id>/analysis.json`, emits analysis summary, refreshes or reports generated work-model state, preserves authored sources, and points next action to implementation. |
| `analysis-rerun-current` | Rerun over unchanged sources reports current generated analysis and work-model state with no authored mutations. |
| `analysis-preserves-authored` | Analyze does not create, update, reorder, normalize, or remove `spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, or `tasks.yml`. |
| `analysis-refreshes-work-model` | Valid source data refreshes `readiness/<id>/work-model.json` before analysis uses generated work-model state. |
| `analysis-accepted-deferral` | Accepted deferrals remain visible in findings and readiness rather than hiding affected source identities. |
| `dry-run` | Reports proposed generated changes without mutating `work-model.json` or `analysis.json`. |
| `deterministic-report` | Three identical runs over identical input produce byte-identical generated analysis views and JSON reports. |
| `text-projection` | Text output includes only facts present in the JSON report. |
| `governance-boundary` | Analysis works without Governance files and reports optional Governance pointers only as not-evaluated compatibility facts. |

## Blocked Fixture Families

| Family | Required behavior |
|---|---|
| `outside-project` | Blocks outside an initialized SDD project and writes no analysis view. |
| `missing-specification` | Blocks when `work/<id>/spec.md` is missing. |
| `missing-clarification` | Blocks when `work/<id>/clarifications.md` is missing. |
| `missing-checklist` | Blocks when `work/<id>/checklist.md` is missing. |
| `missing-plan` | Blocks when `work/<id>/plan.md` is missing. |
| `missing-tasks` | Blocks when `work/<id>/tasks.yml` is missing. |
| `failed-checklist` | Blocks implementation readiness when checklist results contain blocking failures or stale result state. |
| `failed-plan` | Blocks when plan facts are stale, incomplete, or contain blocking findings. |
| `failed-tasks` | Blocks when task facts are stale, incomplete, malformed, or contain blocking findings. |
| `malformed-work-id` | Blocks malformed selected work ids before source reads or writes. |
| `malformed-analysis` | Reports malformed existing analysis view and refreshes or refuses generated output according to source validity. |
| `duplicate-work-id` | Blocks duplicate logical work ids and reports all known conflicting paths. |
| `unknown-source-reference` | Blocks unknown requirements, acceptance scenarios, clarification decisions, checklist results, plan decisions, contracts, verification obligations, migration notes, generated-view impacts, accepted deferrals, tasks, dependencies, required skills, or evidence ids. |
| `dependency-cycle` | Blocks dependency cycles and reports the involved task ids. |
| `stale-plan` | Blocks or needs correction when plan source snapshots no longer match current upstream source facts. |
| `stale-tasks` | Blocks or needs correction when task source snapshots no longer match current specification, clarification, checklist, or plan facts. |
| `analysis-identity-mismatch` | Blocks when existing generated analysis belongs to another work id or incompatible source set. |
| `done-task-missing-evidence` | Reports completed tasks without required evidence as analysis findings. |
| `stale-generated-view` | Reports stale, malformed, missing, or blocked generated work-model or analysis state without treating the view as current. |

## Evidence Requirements

Implementation readiness evidence must include:

- clean Release build;
- focused artifact-model analysis view tests;
- focused command workflow analyze tests;
- create/rerun/output/blocker test transcripts;
- authored-source preservation evidence;
- deterministic JSON and generated analysis comparison evidence;
- text projection human-summary review;
- disposable-directory CLI JSON, dry-run, and text smoke output;
- FSI or prelude transcript for public analyze surface;
- performance evidence for create and rerun fixtures;
- SDD/Governance boundary review;
- artifact traceability review mapping spec requirements to plan, tasks,
  tests, and readiness evidence.
