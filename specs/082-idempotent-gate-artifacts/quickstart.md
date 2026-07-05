# Quickstart: Validating Idempotent Gate Artifacts

Runnable validation for the two defects and the invariants. All scenarios are offline,
require no Governance runtime, and use real filesystem fixtures through the in-process MVU
loop (`TestSupport`) or the CLI. See [contracts/rerun-outcomes.md](./contracts/rerun-outcomes.md)
for the exact expected outcomes and [spec.md](./spec.md) for SC-001..SC-006.

## Prerequisites

- `dotnet` SDK (net10.0), repo built: `dotnet build`
- A work item advanced through `tasks` (so `checklist.md` and `tasks.yml` exist), or use the
  test fixtures under `tests/FS.GG.SDD.Commands.Tests/`.

## Scenario 1 — Checklist stops re-ingesting its own rows (US1 / #146 / SC-001, SC-003)

Reproduces the not-stale re-ingestion directly (fails on `main`, passes after):

1. Build a `checklist.md` whose **Source Snapshot digests match** the current
   `spec.md`/`clarifications.md` (the "not stale" path) and that contains a tool-injected
   `CHK-###` "missing acceptance coverage" **blocking** row for `FR-002`, while the current
   `spec.md` **does** cover `FR-002` (`- FR-002: … (covers AC-00X)`).
2. Re-run: `fsgg-sdd checklist --json`.
3. **Expect**: the `FR-002` `CHK-###` blocking row is **gone** (re-derived away), the
   blocking count is `0`, the stage advances, and **no file was deleted**. On `main` the row
   is preserved and still blocks.
4. Idempotence: run `checklist` again with unchanged sources → `noChange`, byte-identical
   (SC-006).
5. Authored preservation: put text in *Advisory Notes*/*Lifecycle Notes* and repeat step 2 →
   those sections are byte-untouched (SC-004).

Test home: `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` (new re-ingestion test;
reconcile `checklist stale rerun purges…` and `checklist rerun preserves…`).

## Scenario 2 — Tasks regenerate in place after a plan edit (US2 / #147 / SC-002)

1. On a work item with a generated `tasks.yml`, edit `plan.md` to add a decision disposition
   for `DEC-002` (the edit that should clear `analyze`'s `missingDisposition`).
2. Re-run: `fsgg-sdd tasks --json`.
3. **Expect**: the task graph is **regenerated** — a task whose `SourceIds`/`Decisions`
   include `DEC-002` now exists; the outcome is **not** `status: stale` and there is **no**
   `staleTask`/`TF-001`/`StaleCount>0`. No file deletion.
4. Downstream: `fsgg-sdd analyze --json` → `missingDisposition` for `DEC-002` is **cleared**.

Test home: `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs` (new re-derive test;
**replace** `tasks marks existing tasks stale when source snapshots change`) and a cross-check
in `AnalyzeCommandTests.fs`.

## Scenario 3 — Authored task state survives regeneration (US2 / SC-004)

1. In `tasks.yml`, mark a task `Done` (or `Skipped: <reason>`) and set its `owner`.
2. Edit `plan.md` elsewhere and re-run `fsgg-sdd tasks --json`.
3. **Expect**: that task's `status` and `owner` are **preserved** on the re-derived task
   (same `T###` id), while its structural fields are recomputed from sources.
4. Edge — removed source: delete the task's source from `plan.md`, re-run → the task is
   **dropped** (not relabeled `Stale`). Documented behavior (research Decision 3).

Test home: `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs` (extend `tasks rerun
preserves existing authored task state`).

## Scenario 4 — Idempotence & determinism (SC-006)

1. `fsgg-sdd checklist --json` and `fsgg-sdd tasks --json` twice each with unchanged sources.
2. **Expect**: second run of each is `noChange` and byte-identical to the first.
3. Backstop: `tests/FS.GG.SDD.Validation.Tests/DeterminismMatrixTests.fs` byte-identical
   re-run matrix stays green.

> First-run note: an artifact produced by the *old* carry-forward path may be rewritten once
> on its first post-upgrade re-run (canonical re-derive), then stabilizes. Assert idempotence
> on the **second** re-run.

## Scenario 5 — Escape hatch preserved (US3 / FR-006, FR-009)

1. Add `<!-- fsgg-sdd: unsafe-overwrite -->` to `checklist.md` (or `tasks.yml`) and re-run.
2. **Expect**: the existing `unsafeOverwrite` diagnostic blocks the overwrite and names the
   exact file to delete and the command to re-run — unchanged by this feature.

## Docs / drift check (US3)

- The regeneration semantics ([contracts/regeneration-semantics.md](./contracts/regeneration-semantics.md))
  are documented in the `fs-gg-sdd-authoring-contracts` skill (mirrored `.claude`≡`.codex`≡
  `.agents`, manifest sha256 refreshed) and pinned to live behavior by a drift-guard test.
