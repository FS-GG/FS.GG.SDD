# Contract: Verify Command Report JSON

The verify command extends the existing `CommandReport` family. A
`Verification` summary is added alongside the existing specification,
clarification, checklist, plan, tasks, analysis, and evidence summaries. JSON is
the automation contract; text is a projection.

## Top-Level Shape

| Field | Type | Notes |
|---|---|---|
| `schemaVersion` | integer | Report schema version (`1`). |
| `reportVersion` | string | Report contract version. |
| `command` | string | `verify`. |
| `projectRoot` | string | Project-relative or normalized root token; no absolute host paths. |
| `outputFormat` | string | `json` or `text`. |
| `dryRun` | boolean | True when no files are mutated. |
| `overwritePolicy` | string | Existing overwrite policy value. |
| `outcome` | string | `succeeded`, `succeededWithWarnings`, `blocked`, or `noChange`. |
| `workId` | string | Selected work id. |
| `changedArtifacts` | array | Generated artifact operations only; authored sources never appear as changed. |
| `specification` | object | Existing specification summary. |
| `clarification` | object | Existing clarification summary. |
| `checklist` | object | Existing checklist summary. |
| `plan` | object | Existing plan summary. |
| `tasks` | object | Existing tasks summary. |
| `analysis` | object | Existing analysis prerequisite summary. |
| `evidence` | object | Existing evidence summary. |
| `verification` | object | New verification summary (see below). |
| `generatedViews` | array | Work-model, analysis, and verification view currency states. |
| `diagnostics` | array | Verification diagnostics. |
| `governanceCompatibility` | array | Optional advisory Governance facts. |
| `nextAction` | object | Next lifecycle action. |

## Verification Summary

| Field | Type | Notes |
|---|---|---|
| `workId` | string | Selected work id. |
| `stage` | string | `verify`. |
| `status` | string | Verification readiness status. |
| `verifyPath` | string | `readiness/<id>/verify.json`. |
| `findingIds` | string array | Stable, sorted verification finding ids. |
| `readyFindingCount` | integer | Findings at `ready` severity. |
| `advisoryCount` | integer | Findings at `advisory` severity. |
| `warningCount` | integer | Findings at `warning` severity. |
| `blockingCount` | integer | Findings at `blocking` severity. |
| `obligationCount` | integer | Total required obligations evaluated. |
| `evidenceSupportedCount` | integer | Evidence dispositions in `supported`. |
| `evidenceDeferredCount` | integer | Evidence dispositions in `deferred`. |
| `evidenceMissingCount` | integer | Evidence dispositions in `missing`. |
| `evidenceStaleCount` | integer | Evidence dispositions in `stale`. |
| `evidenceSyntheticCount` | integer | Evidence dispositions in `synthetic`. |
| `evidenceInvalidCount` | integer | Evidence dispositions in `invalid`. |
| `testSatisfiedCount` | integer | Required test dispositions in `satisfied`. |
| `testDeferredCount` | integer | Required test dispositions in `deferred`. |
| `testMissingCount` | integer | Required test dispositions in `missing`. |
| `testStaleCount` | integer | Required test dispositions in `stale`. |
| `testInvalidCount` | integer | Required test dispositions in `invalid`. |
| `skillVisibleCount` | integer | Required skills resolved as visible. |
| `skillMissingCount` | integer | Required skills resolved as missing. |
| `sourceSnapshotCount` | integer | Source snapshots recorded. |
| `readiness` | string | `verificationReady` or `needsVerificationCorrection`. |

## Next Action

| Field | Type | Notes |
|---|---|---|
| `actionId` | string | `verify.next.ship` on success; correction action ids when blocked. |
| `command` | string or null | Null until a later ship feature adds a public `Ship` command. |
| `workId` | string | Selected work id. |
| `reason` | string | Plain explanation. |
| `requiredArtifacts` | string array | `readiness/<id>/verify.json` and refreshed `readiness/<id>/work-model.json` on success. |
| `blockingDiagnosticIds` | string array | Stable ids for blocking diagnostics when blocked. |

## Determinism Rules

- Identical project state and verification input produce byte-identical JSON.
- All lists sort by documented stable keys.
- Authoritative content excludes timestamps, durations, terminal width, ANSI
  styling, process ids, random values, absolute host paths, and directory
  enumeration order.
- Text output is rendered from this report and introduces no new facts.

## Exit Codes

- `0` for `succeeded` or `noChange`.
- `0` for `succeededWithWarnings` unless a future policy elevates warnings.
- Non-zero for `blocked`.
