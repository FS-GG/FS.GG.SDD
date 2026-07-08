# Output Contract: `refresh` Currency Matrix for `ship` / `ship-verdict`

**Feature**: 095 · **Date**: 2026-07-08 · **Baseline**: `39fa3e5`

The contract `fsgg-sdd refresh` exposes here is its **JSON report**: `generatedViews[].currency`, the
`refresh` summary's view-id buckets, the diagnostic ids/severities, the planned effects, and the exit
code. This document states that contract for every cell of the state space, **before** and **after**
this feature. It is the source for SC-001 and the regression table test (SC-004).

## State space

`{ship.json} × {ship-verdict.json}` = 5 × 2 = **10 cells**. All cells assume `wmClass ≠ Blocked`
(when the work model is blocked, `downstreamClass` short-circuits at `:439` and no validator runs).

**`ship.json` states**
- **S1 absent** — the fresh-clone state; `ship.json` is gitignored.
- **S2 invalid JSON** — e.g. `"{ not json"`.
- **S3 valid JSON, invalid ship view** — e.g. `{"schemaVersion": 99}`; a future schema, a bad `workId`,
  an unparseable `stage`. **The defect's home.**
- **S4 valid ship view, stale** — an authored source changed under it (`wmChanged`).
- **S5 valid ship view, current.**

**`ship-verdict.json` states**: **V0 absent** · **V1 present** (committed, well-formed).

## The contract

Legend: `AC` = `AlreadyCurrent`, `RF` = `Refreshed`. Diagnostic severities: `refresh.staleView` and
`refresh.malformedGeneratedView` are **warnings**; `refresh.blockedUpstreamView` and
`refresh.unrenderableSummary` are **errors**. Rows in **bold** change.

| # | `ship.json` | verdict | `ship` currency<br>before → after | `ship-verdict` currency<br>before → after | verdict diagnostic<br>before → after | exit |
|---|---|---|---|---|---|---|
| 1 | S1 absent | V1 | `missing` → `missing` | `blocked` → `blocked` | `blockedUpstreamView` → same | ≠0 |
| 2 | S1 absent | V0 | `missing` → `missing` | `missing` → `missing` | `blockedUpstreamView` → same | ≠0 |
| 3 | S2 invalid JSON | V1 | `malformed` → `malformed` | `blocked` → `blocked` | `blockedUpstreamView` → same | ≠0 |
| 4 | S2 invalid JSON | V0 | `malformed` → `malformed` | `missing` → `missing` | `blockedUpstreamView` → same | ≠0 |
| **5** | **S3 valid JSON, bad view** | **V1** | **`current` → `malformed`** | **`malformed` → `blocked`** | **`malformedGeneratedView`(verdict) → `blockedUpstreamView`(verdict)** | ≠0 |
| **6** | **S3 valid JSON, bad view** | **V0** | **`current` → `malformed`** | **`malformed` → `missing`** | **`malformedGeneratedView`(verdict) → `blockedUpstreamView`(verdict)** | ≠0 |
| 7 | S4 stale | V1 | `stale` → `stale` | `stale` → `stale` | `staleView` → same | ≠0 |
| **8** | **S4 stale** | **V0** | `stale` → `stale` | `missing` → `missing` | **`blockedUpstreamView` (ERROR) → `staleView` (warning)** | ≠0 |
| 9 | S5 current | V1 | `current` → `current` | `current` → `current` | none | 0 |
| 10 | S5 current | V0 | `current` → `current` | `current` (`RF`) → same | none | 0 |

**Exit codes are identical in every cell, before and after** (FR-007). Cells 1–8 are non-clean →
`refresh.unrenderableSummary` (error). Cells 9–10 are clean (`AC`/`RF` both clean) → exit 0. See
research R3 for the proof from `structuredClasses` membership.

## What each changed cell fixes

**Cells 5 and 6 (defect A).** `downstreamClass` gated `ship.json` on `parsesAsJson`, so S3 read as
`AlreadyCurrent`. Two false facts followed:

- `ship: current` — about a file that does not parse as a ship view.
- `ship-verdict: malformed` — about a file that is perfectly well-formed. In **cell 6** it is doubly
  false: the verdict does not even exist, yet was reported `malformed`.

After: `shClass = Malformed`, the `(AlreadyCurrent, _)` arm is no longer entered, and the verdict falls
to the arm that already had the right meaning — `Blocked` when present (cell 5), `Missing` when absent
(cell 6). The lone `malformedGeneratedView` now names **`ship.json`**, the file that needs repair.

> The oracle was never far away: `shipVerdictEmission` *already* calls `ShipModule.parseShipView`
> internally (`HandlersShip.fs:205`) and returns `None` when it fails. The bug is that its verdict was
> consulted one step too late — after `shClass` had already been decided by the weaker predicate. This
> feature hoists the same oracle up to `downstreamClass`, which is also why `:527`'s `Malformed` stamp
> becomes unreachable rather than merely unused.

**Cell 8 (defect B).** Cells 7 and 8 differ only in whether the verdict exists. Both mean "the source
moved; re-run `ship`." Cell 7 said so with a warning; cell 8 raised an error claiming the verdict
"cannot be refreshed until upstream view is current" — of the ordinary fresh-clone-then-edit path. The
currency word `missing` stays (the verdict *is* absent, FR-010); only the diagnostic changes.

Cells 2, 4, and 6 keep `blockedUpstreamView` (error) on an absent verdict, because their sources really
are unreadable (FR-011). The severity correction is scoped to a *stale* source alone.

## Invariants (SC-001 … SC-005)

- **SC-001**: In all 10 cells, each `currency` word is a true statement about the artifact it labels.
- **SC-002**: No cell reports `ship-verdict: malformed`. After this change the antecedent is
  unsatisfiable — `refresh` only ever *projects* the verdict, never *parses* it, so it can never
  observe the verdict to be malformed. Cells 5 and 6 were the only ones that claimed otherwise.
- **SC-003**: No cell reports `ship: current` while `parseShipView` fails. (Cells 5, 6 fixed.)
- **SC-004**: The `exit` column is unchanged, cell for cell.
- **SC-005**: Cells 7 and 8 — which differ only in verdict presence over a stale source — emit
  diagnostics of equal severity (`warning`).
- **FR-006**: `ship-verdict.json` is written in **cell 10 only** (`Refreshed`). Cells 5 and 6 plan no
  `WriteFile`; the committed bytes are never touched by a bad source.
- **FR-002**: `analysis.json` and `verify.json` remain on `parsesAsJson`. An analysis/verify file in
  state S3 still reports `current` — a known, deliberately-scoped-out weakness (spec §Out of Scope).

## Unaffected surfaces

- **Persisted schemas.** `ship.json`, `ship-verdict.json`, `work-model.json` unchanged on disk (FR-014).
- **Diagnostic id inventory.** No id added or removed; `docs/release/release-readiness.json` untouched
  (research R6).
- **Projections.** `--json` / `--text` / `--rich` report the same facts as each other; `--rich` changes
  no JSON byte (FR-015).
- **Goldens.** `tests/…/goldens/readiness` is produced from valid work items (cells 9–10), pinned
  byte-identical by FR-008.
