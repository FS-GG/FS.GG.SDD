# Contract: Tasks Report JSON

## Purpose

The tasks command report is the immediate automation contract for
`fsgg-sdd tasks`. JSON is authoritative for CLI callers, CI, agents, and
optional Governance consumers. Text output is rendered from the same value.

## Top-Level Shape

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0",
  "command": "tasks",
  "projectRoot": ".",
  "outputFormat": "json",
  "dryRun": false,
  "overwritePolicy": "refuseUnsafe",
  "outcome": "succeeded",
  "workId": "009-tasks-command",
  "changedArtifacts": [],
  "specification": null,
  "clarification": null,
  "checklist": null,
  "plan": null,
  "tasks": null,
  "generatedViews": [],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": null
}
```

The exact implementation may use existing report wrapper fields, but it MUST
add a tasks summary alongside the existing specification, clarification,
checklist, and plan summaries.

## Tasks Summary

```json
{
  "workId": "009-tasks-command",
  "stage": "tasks",
  "status": "tasksReady",
  "sourceSpec": "work/009-tasks-command/spec.md",
  "sourceClarifications": "work/009-tasks-command/clarifications.md",
  "sourceChecklist": "work/009-tasks-command/checklist.md",
  "sourcePlan": "work/009-tasks-command/plan.md",
  "taskIds": ["T001"],
  "dependencyCount": 0,
  "requiredSkillCount": 1,
  "requiredEvidenceCount": 1,
  "skippedTaskCount": 0,
  "staleTaskCount": 0,
  "blockingFindingCount": 0,
  "advisoryCount": 0,
  "readiness": "ready"
}
```

Field rules:

- ids are sorted lexically by stable id value;
- counts are derived from parsed task facts;
- source paths are project-relative;
- readiness is derived from graph validation and blocking diagnostics;
- no absolute host paths, timestamps, process ids, terminal details, random
  values, or directory enumeration order are allowed.

## Changed Artifacts

Task artifact changes use the existing `ArtifactChange` report family:

```json
{
  "path": "work/009-tasks-command/tasks.yml",
  "kind": "structuredSource",
  "ownership": "SDD",
  "operation": "create",
  "beforeDigest": null,
  "afterDigest": "sha256:...",
  "safeWriteDecision": "createNewTasks",
  "diagnosticIds": []
}
```

Allowed operations are `create`, `update`, `preserve`, `refuse`, and
`noChange`.

## Generated View State

Generated work-model state uses the existing `GeneratedViewState` report
family:

```json
{
  "path": "readiness/009-tasks-command/work-model.json",
  "kind": "workModel",
  "schemaVersion": 1,
  "generator": "fsgg-sdd",
  "sources": [],
  "outputDigest": "sha256:...",
  "currency": "current",
  "diagnosticIds": []
}
```

Currency values are `current`, `missing`, `stale`, `malformed`, and `blocked`.

## Diagnostics

Task diagnostics use the existing diagnostic shape:

```json
{
  "id": "tasks.dependencyCycle",
  "severity": "error",
  "path": "work/009-tasks-command/tasks.yml",
  "message": "Task dependencies contain a cycle.",
  "correction": "Remove or reorder dependencies so the task graph is acyclic.",
  "relatedIds": ["T001", "T002"]
}
```

Required diagnostic families:

- `tasks.missingPlanPrerequisite`;
- `tasks.failedPlanPrerequisite`;
- `tasks.identityMismatch`;
- `tasks.malformedSchema`;
- `tasks.duplicateTaskId`;
- `tasks.unknownSourceReference`;
- `tasks.unknownDependency`;
- `tasks.dependencyCycle`;
- `tasks.staleTask`;
- `tasks.unsafeStatusChange`;
- `tasks.unsafeOverwrite`;
- `tasks.doneTaskMissingEvidence`;
- generated-view diagnostics reused from existing command report families;
- outside project, missing project config, malformed project config, missing
  work id, malformed work id, missing specification, missing clarification,
  missing checklist, and failed checklist diagnostics reused from existing
  command report families.

Diagnostics sort by severity, id, artifact path, source location, and message.

## Next Action

Successful task-ready reports point to `analyze`:

```json
{
  "actionId": "tasks.next.analyze",
  "command": "analyze",
  "workId": "009-tasks-command",
  "reason": "Task graph is current and ready for analysis.",
  "requiredArtifacts": ["work/009-tasks-command/tasks.yml"],
  "blockingDiagnosticIds": []
}
```

Blocked reports point to specification, clarification, checklist, plan, or
task correction and include blocking diagnostic ids.

## Determinism Rules

The same project state and same command input MUST produce byte-identical JSON
reports. Reports MUST:

- sort lists by stable keys;
- normalize paths to `/`;
- omit wall-clock timestamps and durations;
- omit terminal width and ANSI styling;
- omit absolute host paths;
- omit process ids and random values;
- include digests only when they are derived from normalized source bytes.
