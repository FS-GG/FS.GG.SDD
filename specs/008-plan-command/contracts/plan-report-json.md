# Contract: Plan Report JSON

## Purpose

The plan command report is the immediate automation contract for
`fsgg-sdd plan`. JSON is authoritative for CLI callers, CI, agents, and
optional Governance consumers. Text output is rendered from the same value.

## Top-Level Shape

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0",
  "command": "plan",
  "projectRoot": ".",
  "outputFormat": "json",
  "dryRun": false,
  "overwritePolicy": "refuseUnsafe",
  "outcome": "succeeded",
  "workId": "008-plan-command",
  "changedArtifacts": [],
  "specification": null,
  "clarification": null,
  "checklist": null,
  "plan": null,
  "generatedViews": [],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": null
}
```

The exact implementation may use existing report wrapper fields, but it MUST
add a plan summary alongside the existing specification, clarification, and
checklist summaries.

## Plan Summary

```json
{
  "workId": "008-plan-command",
  "stage": "plan",
  "status": "planned",
  "sourceSpec": "work/008-plan-command/spec.md",
  "sourceClarifications": "work/008-plan-command/clarifications.md",
  "sourceChecklist": "work/008-plan-command/checklist.md",
  "decisionIds": ["PD-001"],
  "contractReferenceIds": ["PC-001"],
  "verificationObligationIds": ["VO-001"],
  "migrationNoteIds": ["PM-001"],
  "generatedViewImpactIds": ["GV-001"],
  "acceptedDeferralCount": 0,
  "staleDecisionCount": 0,
  "blockingFindingCount": 0,
  "advisoryCount": 0
}
```

Field rules:

- ids are sorted lexically by stable id value;
- counts are derived from parsed plan facts;
- source paths are project-relative;
- no absolute host paths, timestamps, process ids, terminal details, random
  values, or directory enumeration order are allowed.

## Changed Artifacts

Plan artifact changes use the existing `ArtifactChange` report family:

```json
{
  "path": "work/008-plan-command/plan.md",
  "kind": "authoredSource",
  "ownership": "SDD",
  "operation": "create",
  "beforeDigest": null,
  "afterDigest": "sha256:...",
  "safeWriteDecision": "createNewPlan",
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
  "path": "readiness/008-plan-command/work-model.json",
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

Plan diagnostics use the existing diagnostic shape:

```json
{
  "id": "plan.missingChecklistPrerequisite",
  "severity": "error",
  "path": "work/008-plan-command/checklist.md",
  "message": "Checklist prerequisite is missing.",
  "correction": "Run fsgg-sdd checklist for the selected work item before planning.",
  "relatedIds": ["008-plan-command"]
}
```

Required diagnostic families:

- `plan.missingChecklistPrerequisite`;
- `plan.failedChecklistPrerequisite`;
- `plan.identityMismatch`;
- `plan.malformedFrontMatter`;
- `plan.duplicateId`;
- `plan.unknownSourceReference`;
- `plan.staleDecision`;
- `plan.unsafeDecisionChange`;
- generated-view diagnostics reused from existing command report families;
- outside project, missing project config, malformed project config, missing
  work id, malformed work id, missing specification, and missing clarification
  diagnostics reused from existing command report families.

Diagnostics sort by severity, id, artifact path, source location, and message.

## Next Action

Successful planned reports point to `tasks`:

```json
{
  "actionId": "plan.next.tasks",
  "command": "tasks",
  "workId": "008-plan-command",
  "reason": "Plan is current and ready for task generation.",
  "requiredArtifacts": ["work/008-plan-command/plan.md"],
  "blockingDiagnosticIds": []
}
```

Blocked reports point to specification, clarification, checklist, or plan
correction and include blocking diagnostic ids.

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
