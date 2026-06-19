# Contract: Work Model JSON

## Scope

`readiness/<id>/work-model.json` is the deterministic normalized SDD lifecycle
contract for one work item. It is generated from authored SDD sources and
structured artifacts. The generated file is an output, not an authored source
of truth; its currency must be checked against source digests, schema versions,
generator version, and output digest.

## Top-Level Shape

```json
{
  "schemaVersion": 1,
  "modelVersion": "1.1.0",
  "workId": "002-normalized-work-model",
  "project": {},
  "sources": [],
  "workItem": {},
  "requirements": [],
  "decisions": [],
  "tasks": [],
  "evidence": [],
  "generatedViews": [],
  "diagnostics": [],
  "governanceBoundaries": []
}
```

Required ordering:

1. Object properties emit in the order documented by this contract.
2. Source artifacts sort by repository-relative path.
3. Requirements, decisions, tasks, and evidence sort by stable id.
4. Generated views sort by view path.
5. Diagnostics sort by severity rank, diagnostic id, artifact path, source
   location, then message.
6. Governance boundaries sort by path.

The generated content must not include implicit timestamps. If later features
include sensed timestamps for human context, they must be marked
non-authoritative and excluded from deterministic comparisons.

## Source Entry

```json
{
  "path": "work/002-normalized-work-model/spec.md",
  "kind": "spec",
  "owner": "sdd",
  "schemaVersion": 1,
  "schemaStatus": "current",
  "sourceDigest": {
    "algorithm": "sha256",
    "value": "..."
  }
}
```

Validation:

- Missing required sources emit `missingArtifact`.
- Malformed schema versions emit `malformedSchemaVersion`.
- Deprecated schema versions emit `deprecatedSchemaVersion`.
- Unsupported versions emit `unsupportedSchemaVersion`.
- Future versions emit `futureSchemaVersion` unless explicitly accepted by a
  migration rule.
- Source digests use normalized source bytes and lowercase SHA-256 hex.

## Work Item Entry

```json
{
  "id": "002-normalized-work-model",
  "title": "Normalized Work Model",
  "stage": "plan",
  "changeTier": "tier1",
  "status": "ready-for-planning"
}
```

Validation:

- Work id must match the selected work id.
- Stage must be one known SDD lifecycle stage.
- Structured status is authoritative for executable decisions.
- Prose status mismatches emit `proseStructuredMismatch`.

## Requirement Entry

```json
{
  "id": "FR-001",
  "title": "Normalize authored lifecycle work",
  "text": "The feature MUST normalize SDD-owned project-level sources...",
  "acceptanceCriteria": ["AC-001"],
  "priority": "P1",
  "source": "work/002-normalized-work-model/spec.md",
  "sourceLocation": {
    "line": 123,
    "column": 1
  },
  "linkedTaskIds": ["T001"],
  "linkedEvidenceIds": ["EV001"]
}
```

Validation:

- Requirement ids must be unique within a work item.
- Markdown requirement ids or acceptance criterion ids that are not represented
  in the normalized structured requirement set emit `requirementNotTyped`.
- Links use stable ids only.

## Decision Entry

```json
{
  "id": "DEC-001",
  "title": "Use pure snapshot input",
  "decision": "Normalize from FileSnapshot values and return generated output.",
  "source": "work/002-normalized-work-model/plan.md",
  "sourceLocation": {
    "line": 42,
    "column": 1
  },
  "linkedTaskIds": ["T002"]
}
```

Validation:

- Decision ids must be unique.
- Task decision references must resolve to known decisions.

## Task Entry

```json
{
  "id": "T001",
  "title": "Add normalized work-model signatures",
  "status": "pending",
  "owner": "codex",
  "dependencies": [],
  "requirements": ["FR-001"],
  "decisions": ["DEC-001"],
  "requiredSkills": ["fs-gg-sdd-project"],
  "requiredEvidence": ["EV001"],
  "source": "work/002-normalized-work-model/tasks.yml",
  "sourceLocation": {
    "line": 5,
    "column": 1
  }
}
```

Validation:

- Task ids must be unique.
- Dependencies must reference known tasks and must not cycle.
- Requirement, decision, and required-evidence references must exist.
- Unknown structured references emit `workModelInconsistent`.
- Done tasks require evidence declarations.

## Evidence Entry

```json
{
  "id": "EV001",
  "kind": "verification",
  "subjectType": "task",
  "subjectId": "T001",
  "taskRefs": ["T001"],
  "requirementRefs": ["FR-001"],
  "artifactRefs": ["tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs"],
  "result": "pending",
  "synthetic": false,
  "source": "work/002-normalized-work-model/evidence.yml",
  "sourceLocation": {
    "line": 8,
    "column": 1
  }
}
```

Validation:

- Evidence ids must be unique.
- Subjects and references must resolve to known model entries.
- Synthetic evidence requires visible rationale.
- Deferrals require a reason and removal condition.

## Generated View Entry

```json
{
  "path": "readiness/002-normalized-work-model/work-model.json",
  "kind": "workModel",
  "schemaVersion": 1,
  "generator": {
    "id": "FS.GG.SDD.Artifacts",
    "version": "0.2.0"
  },
  "sources": [
    {
      "path": "work/002-normalized-work-model/spec.md",
      "digest": {
        "algorithm": "sha256",
        "value": "..."
      },
      "schemaVersion": 1
    }
  ],
  "outputDigest": {
    "algorithm": "sha256",
    "value": "..."
  },
  "currency": "current"
}
```

Validation:

- Any mismatch in source digest, schema version, generator version, or output
  digest emits `staleGeneratedView`.
- Missing expected work-model output emits `missingGeneratedWorkModel`.
- Malformed generated JSON emits `staleGeneratedView` with a parse-failure
  explanation.

## Diagnostic Entry

```json
{
  "id": "workModelInconsistent",
  "severity": "error",
  "artifact": "work/002-normalized-work-model/tasks.yml",
  "location": {
    "line": 12,
    "column": 1
  },
  "message": "Task T003 references unknown requirement FR-999.",
  "correction": "Declare FR-999 in the structured requirement model or update the task reference.",
  "relatedIds": ["T003", "FR-999"]
}
```

Required diagnostic families:

- `missingArtifact`
- `malformedSchemaVersion`
- `deprecatedSchemaVersion`
- `unsupportedSchemaVersion`
- `futureSchemaVersion`
- `duplicateIdentifier`
- `unknownReference`
- `requirementNotTyped`
- `workModelInconsistent`
- `proseStructuredMismatch`
- `staleGeneratedView`
- `missingGeneratedWorkModel`
- `malformedDigest`

## Governance Boundary Entry

```json
{
  "path": ".fsgg/capabilities.yml",
  "owner": "governance",
  "requiredBySdd": false,
  "relationship": "optionalCompatibilityBoundary"
}
```

SDD records optional boundaries so consumers and Governance can find them. SDD
does not compute route selection, freshness, profile severity, gate selection,
audit verdicts, or protected-boundary enforcement.
