# Contract: Tasks Artifact

## Purpose

`work/<id>/tasks.yml` is the authored task-graph source for one SDD work item.
It turns planned lifecycle facts into durable task entries, dependencies,
owners, required skills, required evidence obligations, source links, stale
task state, accepted deferrals, and lifecycle notes for later analysis,
implementation, evidence, verify, and ship stages.

`tasks.yml` is structured YAML. It is both human-editable authored source and
the machine contract. Generated readiness views remain outputs and do not
replace this source.

## Location

```text
work/<work-id>/tasks.yml
```

The path is resolved from the configured SDD work root. Reports use
project-relative paths only.

## Root Shape

Required fields for command-authored version 1:

```yaml
schemaVersion: 1
workId: 009-tasks-command
title: Tasks Command
stage: tasks
status: tasksReady
sourceSpec: work/009-tasks-command/spec.md
sourceClarifications: work/009-tasks-command/clarifications.md
sourceChecklist: work/009-tasks-command/checklist.md
sourcePlan: work/009-tasks-command/plan.md
sources:
  - label: plan
    path: work/009-tasks-command/plan.md
    digest: sha256:...
    schemaVersion: 1
tasks:
  - id: T001
    title: Add public task artifact contracts
    status: pending
    owner: codex
    dependencies: []
    requirements: [FR-001]
    decisions: []
    planDecisions: [PD-001]
    contracts: [PC-001]
    verification: [VO-001]
    generatedViews: [GV-001]
    requiredSkills: [fs-gg-sdd-project]
    requiredEvidence: [EV001]
    sourceIds: [FR-001, PD-001, VO-001]
findings: []
advisoryNotes: []
lifecycleNotes:
  - Next lifecycle action: analyze.
```

Field rules:

- `schemaVersion` MUST be integer `1` for this feature.
- `workId` MUST equal the selected work id when present.
- `stage` MUST be `tasks` when present.
- `status` MUST be one of `tasksReady`, `needsCorrection`, `needsReview`, or
  `blocked`.
- `sourceSpec`, `sourceClarifications`, `sourceChecklist`, and `sourcePlan`
  MUST point to the selected work item's prerequisite artifacts when present.
- `sources` MUST use project-relative paths and normalized source digests when
  digests are available.
- Existing version 1 task files that only contain `schemaVersion` and `tasks`
  MAY be parsed for compatibility, but command-authored writes SHOULD include
  the root metadata above.

## Task Entries

Each task entry records:

- `id`: stable task id in `T001` format;
- `title`: human-readable implementation task title;
- `status`: `pending`, `inProgress`, `done`, `skipped`, `stale`, or a
  documented equivalent;
- `owner`: responsible role or actor;
- `dependencies`: task ids that must be complete or reviewed first;
- `requirements`: requirement ids from the specification;
- `decisions`: clarification decision ids when applicable;
- `planDecisions`: plan decision ids when applicable;
- `contracts`: plan contract reference ids when applicable;
- `verification`: verification obligation ids when applicable;
- `migration`: migration note ids when applicable;
- `generatedViews`: generated-view impact ids when applicable;
- `acceptedDeferrals`: accepted deferral ids when applicable;
- `requiredSkills`: skills or capability tags needed to do the work;
- `requiredEvidence`: evidence ids such as `EV001`;
- `sourceIds`: stable ids from source artifacts that justify the task;
- `skipRationale`: required when status is `skipped`;
- `stale`: optional boolean or reason when source links need review;
- `notes`: optional lifecycle notes.

The command MUST preserve unknown non-conflicting fields on existing task
entries when it can do so without changing the structured meaning.

## Stable Id Families

| Kind | Format | Purpose |
|---|---|---|
| Task id | `T###` | Durable implementation work item |
| Evidence id | `EV###` | Required evidence obligation or later declaration |
| Requirement id | `FR-###` | Source requirement link |
| Clarification decision id | `DEC-###` | Source clarification decision link |
| Plan decision id | `PD-###` | Source plan decision link |
| Contract reference id | `PC-###` | Source contract impact link |
| Verification obligation id | `VO-###` | Source verification obligation link |
| Migration note id | `PM-###` | Source migration posture link |
| Generated-view impact id | `GV-###` | Source generated-view impact link |

Ids MUST be unique within their artifact scope, stable across reruns, and
sorted deterministically in generated task sections and reports.

## Source Snapshot Records

Each source snapshot records:

- source label;
- project-relative source path;
- source digest when known;
- schema version when known;
- referenced ids;
- snapshot status.

Source snapshots MUST cover the selected specification, clarification,
checklist, plan, and existing tasks when present. Changed source digests mark
related task entries stale or needing review unless the command can prove the
change does not affect that task.

## Graph Rules

The task graph MUST satisfy:

- task ids are unique;
- every dependency references a known task;
- no task depends on itself;
- dependency edges do not form a cycle;
- every source id referenced by a task exists in the selected source facts;
- every in-scope requirement, plan decision, contract impact, verification
  obligation, migration note, generated-view impact, and accepted deferral has
  a visible task disposition or accepted deferral;
- `done` tasks require declared evidence when evidence state exists or is
  required for the status;
- `skipped` tasks require a rationale.

Graph blockers prevent tasks-ready state.

## Safe Rerun Rules

The command MAY write when it is creating a new tasks artifact, preserving
exact content, adding source-derived tasks, adding missing root metadata,
adding missing source snapshots, adding compatible required skills or required
evidence, or marking affected tasks stale.

The command MUST refuse to write when it detects:

- selected work id mismatch;
- malformed or unsupported task schema;
- duplicate task ids;
- dependency cycles;
- dependencies on unknown task ids;
- unknown source references;
- unsafe removal or renumbering;
- unsupported destructive status changes;
- completed task without required evidence;
- ambiguous YAML structure that prevents non-destructive insertion.

## Generated View Relationship

The tasks artifact contributes source identity, task graph facts, and digest
facts to `readiness/<id>/work-model.json`. The command refreshes that view
when all required source facts are valid, or reports a generated-view
diagnostic when refresh is missing, stale, malformed, or blocked.
