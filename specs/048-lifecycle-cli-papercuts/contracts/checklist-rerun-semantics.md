# Contract: Checklist re-run semantics (§3.1 / FR-001)

**Surface**: `fsgg-sdd checklist --work <id>` and the generated `checklist.md`
result rows. No type-signature change; this is a behavioral contract over
`checklistDiagnosticsTextAndSummary` (`ParsingMid.fs:347-417`).

## Rule

On re-run, the checklist source snapshot is recomputed (`sourceSnapshotStale`,
`ParsingMid.fs:305-315`) by hashing the current `spec.md` + clarification sources
and comparing to the digests recorded in `## Source Snapshot`.

- **Snapshot current** → preserve all existing rows; emit no new derived rows;
  `outcome = noChange`; output byte-identical to the prior run.
- **Snapshot stale** → **purge every machine-derived result row** and re-derive the
  full row set from current sources (the `plannedChecklistReviews` derivation used
  by the fresh `checklistTemplate` path, with no `existingSourceIds` filter), then
  rewrite `## Source Snapshot` to the current digests. Authored, non-derived
  sections of `checklist.md` are preserved.

## Guarantees

- After a stale re-run, **zero** result rows reviewed against the superseded
  snapshot remain in `checklist.md` or the report (SC-001) — no manual file
  deletion required (FR-001).
- The reported `ChecklistSummary` counts (`FailedBlockingCount`, `StaleResultCount`,
  `PassedCount`, …) reflect the re-derived rows, not the superseded ones.
- Partial fix: a re-run after fixing one of several failures keeps still-failing
  requirements as `fail` and flips only the corrected ones (re-evaluation is
  per-requirement).
- Determinism: identical sources ⇒ byte-identical rows; an unchanged re-run still
  reports `noChange` (FR-012).

## Status semantics (unchanged)

Front-matter `status` is `checklistReady` when no `fail` rows remain, else
`needsCorrection`. A still-failing re-run therefore reports `needsCorrection`
truthfully (no longer a misleading `succeededWithWarnings` over stale `fail` rows).
Blocking of a failing checklist remains a downstream concern
(`failedChecklistPrerequisite`, unchanged).

## Test obligations

- Fail → fix source → re-run → no stale `fail` row; report matches current sources.
- Partial fix → still-failing stays `fail`, corrected flips to `pass`.
- Unchanged re-run → `noChange`, byte-identical (rewrite `ChecklistCommandTests.fs:186-200`).
