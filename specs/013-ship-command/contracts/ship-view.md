# Contract: Ship View

## Artifact

```text
readiness/<id>/ship.json
```

`ship.json` is a generated readiness view, not an authored lifecycle source. Its
presence is not proof of currency; it carries source digests, schema version, and
generator identity so staleness is detectable.

## Schema

| Field | Type | Description |
|---|---|---|
| `schemaVersion` | integer | Current version is `1`. |
| `generator` | object | Generator id and version that produced the view. |
| `workId` | string | Selected work id; matches the readiness path. |
| `stage` | string | `ship`. |
| `status` | string | Ship readiness status for the work item. |
| `sources` | array | Source artifact relationships with project-relative paths, schema versions, and source digests. |
| `lifecycleReadiness` | object | Aggregated per-stage readiness for specification, clarification, checklist, plan, tasks, analysis, evidence, and verify. |
| `verificationReadiness` | object | Verification prerequisite summary: status, blocking finding ids, evidence disposition summary, and currency. |
| `evidenceDispositions` | array | Evidence disposition summary aggregated from the verification view; not re-derived by ship. |
| `generatedViews` | array | Generated-view currency for the work-model, analysis, verification, and ship views. |
| `disposition` | object | The single ship-readiness disposition with state, blocking/warning/advisory finding ids, contributing stages, and correction. |
| `findings` | array | Ship-readiness findings with stable ids, severity, category, structured links, message, and correction. |
| `governanceCompatibility` | array | Optional advisory Governance pointers; never interpreted as enforcement. |
| `diagnostics` | array | Ship diagnostics with stable ids, severity, artifact, message, and correction. |
| `readiness` | string | `shipReady` or `needsShipCorrection`. |

## Disposition States

- Ship readiness disposition: `shipReady`, `blocked`, `stale`, `advisory`.
- Lifecycle stage readiness: `ready`, `advisory`, `stale`, `blocked`,
  `notApplicable`.
- Finding severity: `ready`, `advisory`, `warning`, `blocking`.

## Aggregation Boundary

- Ship aggregates the verification view's verification readiness, blocking
  findings, and evidence dispositions; it MUST NOT re-derive task, evidence,
  required-test, or required-skill dispositions that the verify stage owns.
- Ship-readiness findings reference verify-owned ids by structured link rather
  than recomputing them.

## Determinism

- JSON is byte-stable for identical source trees.
- Lists sort by documented stable keys (ids, then paths).
- No timestamps, durations, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in the view.
- Source digests and generator identity are included so a stale view is
  detectable against current sources.

## Currency

- The view is `current` only when its source digests and generator version match
  the current authored and generated sources, including the verification view.
- A missing, stale, malformed, or blocked view is reported through generated-view
  diagnostics rather than treated as current.
- Schema version 1 is accepted; future, unsupported, malformed, or deprecated
  versions are diagnosed.
