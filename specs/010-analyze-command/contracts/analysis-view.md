# Contract: Analysis View

## Purpose

`readiness/<id>/analysis.json` is the generated cross-artifact consistency
view for one SDD work item. It records source relationships, source currency,
task-readiness facts, generated-view state, structured analysis findings,
diagnostics, optional boundary facts, and implementation-readiness state.

The analysis view is generated readiness data. It is not an authored source of
lifecycle intent and must not replace `spec.md`, `clarifications.md`,
`checklist.md`, `plan.md`, or `tasks.yml`.

## Location

```text
readiness/<work-id>/analysis.json
```

The path is resolved from the configured SDD readiness root. Reports use
project-relative paths only.

## Top-Level Shape

Required fields for command-generated version 1:

```json
{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "010-analyze-command",
  "stage": "analyze",
  "status": "implementationReady",
  "generator": "fsgg-sdd",
  "sources": [],
  "sourceRelationships": [],
  "readiness": {
    "status": "implementationReady",
    "readyCount": 0,
    "advisoryCount": 0,
    "warningCount": 0,
    "blockingCount": 0,
    "staleSourceCount": 0,
    "missingDispositionCount": 0,
    "malformedSourceCount": 0,
    "generatedViewFindingCount": 0,
    "acceptedDeferralCount": 0
  },
  "findings": [],
  "generatedViews": [],
  "optionalBoundaryFacts": [],
  "diagnostics": [],
  "nextAction": {
    "actionId": "analysis.next.implement",
    "command": null,
    "reason": "Lifecycle sources are current and ready for implementation."
  }
}
```

Field rules:

- `schemaVersion` MUST be integer `1` for this feature.
- `workId` MUST equal the selected work id.
- `stage` MUST be `analyze`.
- `status` MUST be `implementationReady`, `needsCorrection`,
  `needsGeneratedViewRefresh`, or `blocked`.
- `generator` MUST identify the SDD generator version used to produce the
  view.
- `sources` MUST use project-relative paths and normalized source digests.
- `findings`, `diagnostics`, and ids inside nested arrays MUST be sorted by
  stable keys.
- The view MUST NOT include timestamps, durations, terminal details, process
  ids, random values, absolute host paths, or directory enumeration order.

## Source Records

Each source record describes one source or generated input to analysis:

```json
{
  "path": "work/010-analyze-command/tasks.yml",
  "kind": "tasks",
  "digest": "sha256:...",
  "schemaVersion": 1,
  "schemaStatus": "current"
}
```

Required sources when available:

- `.fsgg/project.yml`
- `.fsgg/sdd.yml`
- `.fsgg/agents.yml`
- `work/<id>/spec.md`
- `work/<id>/clarifications.md`
- `work/<id>/checklist.md`
- `work/<id>/plan.md`
- `work/<id>/tasks.yml`
- `readiness/<id>/work-model.json`

Missing or blocked sources produce diagnostics instead of incomplete current
source records.

## Source Relationships

Each source relationship records an expected link between artifacts or ids:

```json
{
  "id": "AR001",
  "sourcePath": "work/010-analyze-command/plan.md",
  "targetPath": "work/010-analyze-command/tasks.yml",
  "sourceId": "VO-001",
  "targetId": "T004",
  "relationship": "verificationDisposition",
  "state": "current",
  "diagnosticIds": []
}
```

Relationship states are `current`, `stale`, `missing`, `unknownReference`, and
`blocked`.

Analysis MUST cover relationships for requirements, acceptance scenarios,
clarification decisions, checklist results, plan decisions, contract
references, verification obligations, migration notes, generated-view impacts,
accepted deferrals, task dependencies, required skills, and required evidence
obligations when those facts are present.

## Analysis Findings

Each finding records a user-correctable consistency or readiness result:

```json
{
  "id": "AF001",
  "category": "missingDisposition",
  "severity": "blocking",
  "state": "open",
  "path": "work/010-analyze-command/tasks.yml",
  "relatedIds": ["VO-001", "T004"],
  "message": "Verification obligation VO-001 has no current task disposition.",
  "correction": "Update tasks.yml or rerun fsgg-sdd tasks after correcting the plan."
}
```

Required severity values:

- `ready`
- `advisory`
- `warning`
- `blocking`
- `staleSource`
- `missingDisposition`
- `malformedSource`
- `generatedView`

Accepted deferrals may keep implementation readiness possible only when the
deferral is visible in the downstream source and named in the finding.

## Generated View Relationship

The analysis view records generated work-model and analysis view states:

```json
{
  "path": "readiness/010-analyze-command/work-model.json",
  "kind": "workModel",
  "currency": "current",
  "diagnosticIds": []
}
```

Currency values are `current`, `missing`, `stale`, `malformed`, and `blocked`.
Analysis MUST NOT treat an existing generated view as current unless source
digests and generator identity match current inputs or the command safely
refreshes the view.

## Readiness Rules

The analysis view reports `implementationReady` only when:

- selected work id and source artifact identities agree;
- prerequisite specification, clarification, checklist, plan, and tasks facts
  parse successfully;
- blocking ambiguity is resolved or visibly deferred;
- blocking checklist failures and stale checklist results are absent or
  visibly deferred;
- plan decisions, contract references, verification obligations, migration
  notes, and generated-view impacts have current task dispositions;
- task ids are unique and dependencies are acyclic;
- stale task source links are absent or explicitly accepted;
- completed tasks with required evidence obligations have supporting evidence
  state when such state is already recorded;
- generated work-model state is current or safely refreshed;
- no malformed generated analysis view blocks refresh.

Blocked or needs-correction states MUST name the responsible artifact and next
correction action.

## Determinism Rules

The same project state and same command input MUST produce byte-identical
analysis views. The view MUST:

- sort lists by stable ids and paths;
- normalize paths to `/`;
- include digests only when derived from normalized source bytes;
- omit wall-clock timestamps and durations;
- omit terminal width and ANSI styling;
- omit absolute host paths;
- omit process ids and random values.
