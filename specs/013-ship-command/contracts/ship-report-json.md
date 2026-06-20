# Contract: Ship Command Report JSON

The ship command extends the existing `CommandReport` family. A `Ship` summary is
added alongside the existing specification, clarification, checklist, plan, tasks,
analysis, evidence, and verification summaries. JSON is the automation contract;
text is a projection.

## Top-Level Shape

| Field | Type | Notes |
|---|---|---|
| `schemaVersion` | integer | Report schema version (`1`). |
| `reportVersion` | string | Report contract version. |
| `command` | string | `ship`. |
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
| `verification` | object | Existing verification prerequisite summary. |
| `ship` | object | New ship summary (see below). |
| `generatedViews` | array | Work-model, analysis, verification, and ship view currency states. |
| `diagnostics` | array | Ship diagnostics. |
| `governanceCompatibility` | array | Optional advisory Governance facts. |
| `nextAction` | object | Next lifecycle action. |

## Ship Summary

| Field | Type | Notes |
|---|---|---|
| `workId` | string | Selected work id. |
| `stage` | string | `ship`. |
| `status` | string | Ship readiness status. |
| `shipPath` | string | `readiness/<id>/ship.json`. |
| `findingIds` | string array | Stable, sorted ship-readiness finding ids. |
| `readyFindingCount` | integer | Findings at `ready` severity. |
| `advisoryCount` | integer | Findings at `advisory` severity. |
| `warningCount` | integer | Findings at `warning` severity. |
| `blockingCount` | integer | Findings at `blocking` severity. |
| `disposition` | string | `shipReady`, `blocked`, `stale`, or `advisory`. |
| `lifecycleStageReadiness` | object | Per-stage readiness state for specification, clarification, checklist, plan, tasks, analysis, evidence, and verify. |
| `verificationReadiness` | string | Verification prerequisite status aggregated from `verify.json`. |
| `evidenceSupportedCount` | integer | Evidence dispositions in `supported` (from the verification view). |
| `evidenceDeferredCount` | integer | Evidence dispositions in `deferred`. |
| `evidenceMissingCount` | integer | Evidence dispositions in `missing`. |
| `evidenceStaleCount` | integer | Evidence dispositions in `stale`. |
| `evidenceSyntheticCount` | integer | Evidence dispositions in `synthetic`. |
| `evidenceInvalidCount` | integer | Evidence dispositions in `invalid`. |
| `generatedViewState` | string | Aggregated generated-view currency state. |
| `sourceSnapshotCount` | integer | Source snapshots recorded. |
| `readiness` | string | `shipReady` or `needsShipCorrection`. |

## Next Action

| Field | Type | Notes |
|---|---|---|
| `actionId` | string | `ship.next.protectedBoundary` on success; correction action ids when blocked. |
| `command` | string or null | Null; the protected-boundary handoff is Governance-owned, not another SDD command. |
| `workId` | string | Selected work id. |
| `reason` | string | Plain explanation. |
| `requiredArtifacts` | string array | `readiness/<id>/ship.json` and refreshed `readiness/<id>/work-model.json` on success. |
| `blockingDiagnosticIds` | string array | Stable ids for blocking diagnostics when blocked. |

## Determinism Rules

- Identical project state and ship input produce byte-identical JSON.
- All lists sort by documented stable keys.
- Authoritative content excludes timestamps, durations, terminal width, ANSI
  styling, process ids, random values, absolute host paths, and directory
  enumeration order.
- Text output is rendered from this report and introduces no new facts.

## Exit Codes

- `0` for `succeeded` or `noChange`.
- `0` for `succeededWithWarnings` unless a future policy elevates warnings.
- Non-zero for `blocked`.
