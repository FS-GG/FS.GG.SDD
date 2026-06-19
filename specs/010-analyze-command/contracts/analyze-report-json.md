# Contract: Analyze Report JSON

## Purpose

The analyze command report is the immediate automation contract for
`fsgg-sdd analyze`. JSON is authoritative for CLI callers, CI, agents, and
optional Governance consumers. Text output is rendered from the same value.

## Top-Level Shape

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0",
  "command": "analyze",
  "projectRoot": ".",
  "outputFormat": "json",
  "dryRun": false,
  "overwritePolicy": "refuseUnsafe",
  "outcome": "succeeded",
  "workId": "010-analyze-command",
  "changedArtifacts": [],
  "specification": null,
  "clarification": null,
  "checklist": null,
  "plan": null,
  "tasks": null,
  "analysis": null,
  "generatedViews": [],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": null
}
```

The exact implementation may use existing report wrapper fields, but it MUST
add an analysis summary alongside the existing specification, clarification,
checklist, plan, and tasks summaries.

## Analysis Summary

```json
{
  "workId": "010-analyze-command",
  "stage": "analyze",
  "status": "implementationReady",
  "analysisPath": "readiness/010-analyze-command/analysis.json",
  "sourceCount": 8,
  "sourceRelationshipCount": 12,
  "readyFindingCount": 0,
  "advisoryCount": 0,
  "warningCount": 0,
  "blockingCount": 0,
  "staleSourceCount": 0,
  "missingDispositionCount": 0,
  "malformedSourceCount": 0,
  "generatedViewFindingCount": 0,
  "acceptedDeferralCount": 0,
  "readiness": "implementationReady"
}
```

Field rules:

- ids are sorted lexically by stable id value;
- counts are derived from parsed analysis facts;
- source paths are project-relative;
- readiness is derived from analysis findings and blocking diagnostics;
- no absolute host paths, timestamps, process ids, terminal details, random
  values, or directory enumeration order are allowed.

## Changed Artifacts

Analysis and work-model generated-view changes use the existing
`ArtifactChange` report family:

```json
{
  "path": "readiness/010-analyze-command/analysis.json",
  "kind": "generatedView",
  "ownership": "SDD",
  "operation": "create",
  "beforeDigest": null,
  "afterDigest": "sha256:...",
  "safeWriteDecision": "createAnalysisView",
  "diagnosticIds": []
}
```

Allowed operations are `create`, `update`, `preserve`, `refuse`, and
`noChange`.

Analyze MUST NOT report authored-source operations except `preserve` when the
report needs to explain that authored artifacts were intentionally not changed.

## Generated View State

Generated work-model and analysis states use the existing `GeneratedViewState`
report family:

```json
{
  "path": "readiness/010-analyze-command/analysis.json",
  "kind": "analysis",
  "schemaVersion": 1,
  "generator": "fsgg-sdd",
  "sources": [],
  "outputDigest": "sha256:...",
  "currency": "current",
  "diagnosticIds": []
}
```

Currency values are `current`, `missing`, `stale`, `malformed`, and `blocked`.

The report MUST include generated-view state for the selected work-model view
and the analysis view whenever those paths are known.

## Diagnostics

Analysis diagnostics use the existing diagnostic shape:

```json
{
  "id": "analysis.missingDisposition",
  "severity": "error",
  "path": "work/010-analyze-command/tasks.yml",
  "message": "A plan verification obligation has no current task disposition.",
  "correction": "Update tasks.yml or rerun fsgg-sdd tasks after correcting the plan.",
  "relatedIds": ["VO-001"]
}
```

Required diagnostic families:

- `analysis.missingTasksPrerequisite`;
- `analysis.failedChecklistPrerequisite`;
- `analysis.failedPlanPrerequisite`;
- `analysis.failedTasksPrerequisite`;
- `analysis.identityMismatch`;
- `analysis.malformedAnalysisView`;
- `analysis.unknownSourceReference`;
- `analysis.unknownDependency`;
- `analysis.dependencyCycle`;
- `analysis.unresolvedAmbiguity`;
- `analysis.failedChecklistResult`;
- `analysis.staleChecklistResult`;
- `analysis.incompletePlanDecision`;
- `analysis.stalePlanDecision`;
- `analysis.missingDisposition`;
- `analysis.staleTask`;
- `analysis.unsupportedTaskState`;
- `analysis.doneTaskMissingEvidence`;
- generated-view diagnostics reused from existing command report families;
- outside project, missing project config, malformed project config, missing
  work id, malformed work id, missing specification, missing clarification,
  missing checklist, missing plan, duplicate work id, and malformed task
  diagnostics reused from existing command report families.

Diagnostics sort by severity, id, artifact path, source location, and message.

## Next Action

Successful implementation-ready reports point to implementation without
introducing implementation execution in this feature:

```json
{
  "actionId": "analysis.next.implement",
  "command": null,
  "workId": "010-analyze-command",
  "reason": "Lifecycle sources are current and ready for implementation.",
  "requiredArtifacts": [
    "readiness/010-analyze-command/analysis.json"
  ],
  "blockingDiagnosticIds": []
}
```

Blocked reports point to specification, clarification, checklist, plan, tasks,
or generated-view correction and include blocking diagnostic ids.

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
