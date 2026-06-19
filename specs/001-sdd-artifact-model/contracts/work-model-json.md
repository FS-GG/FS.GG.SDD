# Contract: Work Model JSON

## Scope

`readiness/<id>/work-model.json` is the deterministic normalized SDD lifecycle
contract for one work item. It is generated from authored SDD sources and
structured artifacts. Generated JSON is automation truth for SDD readiness, CI,
agents, scripts, and optional Governance consumers.

## Top-Level Shape

```json
{
  "schemaVersion": 1,
  "modelVersion": "1.0.0",
  "workId": "001-sdd-artifact-model",
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
4. Diagnostics sort by severity, diagnostic id, artifact path, source location,
   then message.
5. Generated views sort by view path.

## Source Artifact Entry

```json
{
  "path": "work/001-sdd-artifact-model/spec.md",
  "kind": "spec",
  "owner": "sdd",
  "schemaVersion": 1,
  "sourceDigest": {
    "algorithm": "sha256",
    "value": "..."
  }
}
```

Validation:

- Missing required sources emit `missingArtifact`.
- Malformed or unsupported schema versions emit schema diagnostics.
- Source digests must be calculated over normalized bytes specified by the
  implementation tasks.

## Work Item Entry

```json
{
  "id": "001-sdd-artifact-model",
  "title": "SDD Artifact Model",
  "stage": "plan",
  "changeTier": "tier1",
  "status": "draft"
}
```

Validation:

- Work id must match the work directory.
- Stage must be known.
- Tier controls required evidence but does not alter diagnostic truth.

## Requirement, Decision, Task, And Evidence Entries

Each graph entry carries:

- stable id;
- display title or summary;
- source artifact and source location when available;
- references to other stable ids;
- diagnostics attached to that entry.

References never use display names. Unknown references emit `unknownReference`
or `workModelInconsistent`.

## Generated View Entry

```json
{
  "path": "readiness/001-sdd-artifact-model/work-model.json",
  "kind": "workModel",
  "schemaVersion": 1,
  "generator": {
    "id": "FS.GG.SDD.Artifacts",
    "version": "0.1.0"
  },
  "sources": [
    {
      "path": "work/001-sdd-artifact-model/spec.md",
      "digest": {
        "algorithm": "sha256",
        "value": "..."
      }
    }
  ],
  "outputDigest": {
    "algorithm": "sha256",
    "value": "..."
  }
}
```

Validation:

- Any mismatch in source digest, schema version, generator version, or output
  digest emits `staleGeneratedView`.
- Generated view presence is never proof of currency.

## Diagnostic Entry

```json
{
  "id": "duplicateIdentifier",
  "severity": "error",
  "artifact": "work/001-sdd-artifact-model/tasks.yml",
  "location": {
    "line": 12,
    "column": 9
  },
  "message": "Task id T003 is declared more than once.",
  "correction": "Rename one task id and update references.",
  "relatedIds": ["T003"]
}
```

Required diagnostic families:

- `missingArtifact`
- `malformedSchemaVersion`
- `unsupportedSchemaVersion`
- `duplicateIdentifier`
- `unknownReference`
- `requirementNotTyped`
- `workModelInconsistent`
- `proseStructuredMismatch`
- `staleGeneratedView`
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
