# Contract: Verification View

## Artifact

```text
readiness/<id>/verify.json
```

`verify.json` is a generated readiness view, not an authored lifecycle source.
Its presence is not proof of currency; it carries source digests, schema
version, and generator identity so staleness is detectable.

## Schema

| Field | Type | Description |
|---|---|---|
| `schemaVersion` | integer | Current version is `1`. |
| `generator` | object | Generator id and version that produced the view. |
| `workId` | string | Selected work id; matches the readiness path. |
| `stage` | string | `verify`. |
| `status` | string | Verification readiness status for the work item. |
| `sources` | array | Source artifact relationships with project-relative paths, schema versions, and source digests. |
| `lifecycleReadiness` | object | Per-stage readiness summary for specification, clarification, checklist, plan, tasks, analysis, and evidence. |
| `taskGraph` | object | Task graph readiness: counts, dependency validity, status validity, and finding ids. |
| `evidenceDispositions` | array | Evidence obligation dispositions with state, evidence ids, affected tasks, severity, and correction. |
| `testDispositions` | array | Required test obligation dispositions with state, evidence ids, affected tasks/requirements, severity, and correction. |
| `skillVisibility` | array | Required skill or capability-tag visibility facts with requiring tasks and visibility state. |
| `generatedViews` | array | Generated-view currency for the work-model, analysis, and verification views. |
| `findings` | array | Verification findings with stable ids, severity, category, structured links, message, and correction. |
| `governanceCompatibility` | array | Optional advisory Governance pointers; never interpreted as enforcement. |
| `diagnostics` | array | Verification diagnostics with stable ids, severity, artifact, message, and correction. |
| `readiness` | string | `verificationReady` or `needsVerificationCorrection`. |

## Disposition States

- Evidence dispositions: `supported`, `deferred`, `missing`, `stale`,
  `synthetic`, `invalid`, `advisory`, `blocking`.
- Required test dispositions: `satisfied`, `deferred`, `missing`, `stale`,
  `synthetic`, `invalid`, `advisory`, `blocking`.
- Skill visibility: `visible`, `missing`.
- Finding severity: `ready`, `advisory`, `warning`, `blocking`.

## Determinism

- JSON is byte-stable for identical source trees.
- Lists sort by documented stable keys (ids, then paths).
- No timestamps, durations, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in the view.
- Source digests and generator identity are included so a stale view is
  detectable against current sources.

## Currency

- The view is `current` only when its source digests and generator version match
  the current authored and generated sources.
- A missing, stale, malformed, or blocked view is reported through generated-view
  diagnostics rather than treated as current.
- Schema version 1 is accepted; future, unsupported, malformed, or deprecated
  versions are diagnosed.
