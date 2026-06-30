# Contract: Work-model currency snapshot-set parity (§3.4 / FR-005–007)

**Surface**: `existingGeneratedViewDiagnostic` (`ViewGeneration.fs:445-474`),
which computes the `staleGeneratedView` advisory that `verify`/`ship` (and other
generating stages) report. No type-signature change.

## Rule

The set of source snapshots passed to `checkGeneratedWorkModelCurrency`
(`existingGeneratedViewDiagnostic.currentSnapshots`, `ViewGeneration.fs:452-461`)
MUST equal the authored-source set used to **generate** the work model
(`workModelSnapshots`, `ViewGeneration.fs:476-502`). Concretely, `currentSnapshots`
gains `planPath workId` and `charterPath workId` (the two it currently omits),
alongside `.fsgg/{project,sdd,agents}.yml`, `spec`, `clarification`, `checklist`,
`tasks`, `evidence`, and the existing `work-model.json` snapshot.

A work-model *source* is, by definition (`WorkItem.fs:158-164`), any input snapshot
that is **not** `*.json` and **not** `manifest.yml`; readiness outputs
(`verify.json`/`ship.json`/`work-model.json`) are therefore never sources.

## Guarantees

- A clean `verify`/`ship` run — no authored source changed since the work model was
  generated — reports **no** `staleGeneratedView` and ends in a clean result; no
  trailing `refresh` is needed to reach clean (FR-005, FR-006, SC-004).
- A genuinely stale work model — a recorded source's digest changed, or a recorded
  source is absent for a real reason, or the generator version/output digest
  mismatches — still reports `staleGeneratedView` (FR-007). Real staleness is not
  suppressed.
- The fix changes only the currency-check **input set**, not the staleness predicate
  (`generatorStale || sourceStale || outputDigestStale`, `Serialization.fs:278-281`).

## Test obligations

- Clean verify on a current work model → no `staleGeneratedView`; `Outcome`
  not `SucceededWithWarnings` for that cause.
- Clean ship on a current work model → `shipReady` disposition (rewrite
  `ShipCommandTests.fs:80-83`, which currently expects `advisory`).
- Edit an upstream authored source (e.g. `spec.md`) after generation, run verify →
  `staleGeneratedView` still emitted (FR-007).
- Determinism matrix unaffected (`DeterminismMatrixTests.fs`).
