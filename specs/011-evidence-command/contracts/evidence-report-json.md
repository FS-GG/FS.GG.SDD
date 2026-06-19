# Contract: Evidence Report JSON

## Purpose

The evidence command report is the immediate automation contract for
`fsgg-sdd evidence`. JSON is authoritative for CLI callers, CI, agents, and
optional Governance consumers. Text output is rendered from the same value.

## Top-Level Shape

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0",
  "command": "evidence",
  "projectRoot": ".",
  "outputFormat": "json",
  "dryRun": false,
  "overwritePolicy": "refuseUnsafe",
  "outcome": "succeeded",
  "workId": "011-evidence-command",
  "changedArtifacts": [],
  "specification": null,
  "clarification": null,
  "checklist": null,
  "plan": null,
  "tasks": null,
  "analysis": null,
  "evidence": null,
  "generatedViews": [],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": null
}
```

The exact implementation may use existing report wrapper fields, but it MUST
add an evidence summary alongside existing specification, clarification,
checklist, plan, tasks, and analysis summaries.

## Evidence Summary

```json
{
  "workId": "011-evidence-command",
  "stage": "evidence",
  "status": "evidenceReady",
  "evidencePath": "work/011-evidence-command/evidence.yml",
  "declarationIds": ["EV001"],
  "declarationCount": 1,
  "obligationCount": 1,
  "supportedCount": 1,
  "deferredCount": 0,
  "missingCount": 0,
  "staleCount": 0,
  "syntheticCount": 0,
  "invalidCount": 0,
  "advisoryCount": 0,
  "blockingCount": 0,
  "sourceSnapshotCount": 6,
  "readiness": "evidenceReady"
}
```

Field rules:

- ids are sorted lexically by stable id value;
- counts are derived from parsed evidence facts and current dispositions;
- source paths are project-relative;
- readiness is derived from evidence dispositions and blocking diagnostics;
- no absolute host paths, timestamps, process ids, terminal details, random
  values, or directory enumeration order are allowed.

## Changed Artifacts

Evidence authored-source changes use the existing `ArtifactChange` report
family:

```json
{
  "path": "work/011-evidence-command/evidence.yml",
  "kind": "authoredSource",
  "ownership": "SDD",
  "operation": "create",
  "beforeDigest": null,
  "afterDigest": "sha256:...",
  "safeWriteDecision": "createEvidenceArtifact",
  "diagnosticIds": []
}
```

Generated work-model changes use `kind: "generatedView"` and the existing
generated-view safe-write decisions.

Allowed operations are `create`, `update`, `preserve`, `refuse`, and
`noChange`.

The evidence command MUST NOT report authored-source operations for
specification, clarification, checklist, plan, tasks, or analysis except
`preserve` when the report needs to explain that those artifacts were
intentionally not changed.

## Generated View State

Generated work-model state uses the existing `GeneratedViewState` report
family:

```json
{
  "path": "readiness/011-evidence-command/work-model.json",
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

The report MUST include generated-view state for the selected work-model view
whenever that path is known. It MUST report analysis generated-view state as a
prerequisite when analysis is missing, stale, malformed, blocked, or otherwise
diagnostic-bearing.

## Diagnostics

Evidence diagnostics use the existing diagnostic shape:

```json
{
  "id": "evidence.missingRequiredEvidence",
  "severity": "error",
  "path": "work/011-evidence-command/evidence.yml",
  "message": "Completed task T001 has no current evidence or accepted deferral for obligation VO-001.",
  "correction": "Add an evidence declaration or accepted deferral linked to T001 and VO-001.",
  "relatedIds": ["T001", "VO-001"]
}
```

Required diagnostic families:

- `evidence.missingAnalysisPrerequisite`;
- `evidence.analysisNotReady`;
- `evidence.identityMismatch`;
- `evidence.malformedEvidenceArtifact`;
- `evidence.duplicateEvidenceId`;
- `evidence.unknownReference`;
- `evidence.missingRequiredEvidence`;
- `evidence.staleEvidence`;
- `evidence.staleEvidenceSource`;
- `evidence.undisclosedSyntheticEvidence`;
- `evidence.missingDeferralRationale`;
- `evidence.missingRequiredSkill`;
- `evidence.unsupportedResultState`;
- `evidence.unsafeUpdate`;
- generated-view diagnostics reused from existing command report families;
- outside project, missing project config, malformed project config, missing
  work id, malformed work id, missing specification, missing clarification,
  missing checklist, missing plan, missing tasks, duplicate work id, malformed
  task, and malformed analysis diagnostics reused from existing command report
  families.

Diagnostics sort by severity, id, artifact path, source location, and message.

## Next Action

Successful evidence-ready reports point to verify without adding verify
behavior in this feature:

```json
{
  "actionId": "evidence.next.verify",
  "command": null,
  "workId": "011-evidence-command",
  "reason": "Evidence declarations are current and ready for verification.",
  "requiredArtifacts": [
    "work/011-evidence-command/evidence.yml",
    "readiness/011-evidence-command/work-model.json"
  ],
  "blockingDiagnosticIds": []
}
```

Blocked reports point to implementation continuation, evidence correction,
task correction, analysis rerun, or generated-view refresh and include
blocking diagnostic ids.

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
