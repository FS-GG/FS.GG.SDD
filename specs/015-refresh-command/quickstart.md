# Quickstart: Generated-View Refresh

This guide validates `fsgg-sdd refresh` end-to-end. It is a run/validation guide;
implementation details live in `tasks.md` and the contracts under `contracts/`.

## Prerequisites

- .NET SDK targeting `net10.0`.
- An initialized SDD project (`fsgg-sdd init`) with a work item carried at least
  through `ship` so the structured readiness views exist.

## Build and test

```bash
dotnet build FS.GG.SDD.sln -c Release
dotnet test FS.GG.SDD.sln -c Release
dotnet fsi scripts/prelude.fsx        # FSI evidence for the public refresh surface
```

Expected: build succeeds; all tests pass (including the new
`RefreshCommandTests` and `RefreshSummaryViewTests`); prelude exits 0.

## Scenario 1 — Refresh all views from current sources (US1, P1)

```bash
fsgg-sdd refresh --work <id> --json
```

Expected: every refreshable SDD-owned view (`work-model.json`, `analysis.json`,
`verify.json`, `ship.json`, `agent-commands/<target>/`, `summary.md`) is current
after the run; each refreshed view records its source paths, source digests,
schema versions, and generator identity; the report names the work id, refreshed
views, already-current views, per-view state, outcome, and next action.

> **Implementation phasing note**: `summary.md` is owned by User Story 3 and is
> produced from US3 onward (Phase 5 in `tasks.md`). A US1-only MVP run refreshes
> the structured views (`work-model.json`, `analysis.json`, `verify.json`,
> `ship.json`, `agent-commands/<target>/`); the `summary.md` portion of this
> expectation is validated once US3 lands.

## Scenario 2 — Detect stale and blocked views (US2, P1)

Edit an authored source (e.g. the spec) so downstream views go stale, then:

```bash
fsgg-sdd refresh --work <id> --json
```

Expected: affected views are refreshed and reported; views already current are
reported as such; if a source is missing/malformed/blocked, the affected view is
reported `blocked`/`missing` with a diagnostic naming the view and source, and
the refreshable views still refresh. A dependent view whose upstream view could
not be brought to currency is reported `blocked` and names the upstream view.

## Scenario 3 — Human-readable summary (US3, P2)

```bash
fsgg-sdd refresh --work <id>
cat readiness/<id>/summary.md
```

Expected: `summary.md` is generated as a projection of the structured readiness
data, carries the generated-marker header with its sources and generator, and its
lifecycle state, per-view currency table, diagnostics, outcome, and next action
match the authoritative report — with no facts absent from the structured views.

## Scenario 4 — Authored sources preserved, dry run safe (US4, P2)

```bash
git status --porcelain               # baseline
fsgg-sdd refresh --work <id> --dry-run --json
git status --porcelain               # unchanged — dry run wrote nothing
fsgg-sdd refresh --work <id>
git diff --stat work/<id>            # authored sources byte-unchanged
```

Expected: dry run reports proposed generated-view changes, diagnostics, per-view
state, and next action without modifying any file; a normal run writes only
generated views under their generated roots and leaves authored artifacts and
`.fsgg/*.yml` byte-unchanged.

## Scenario 5 — Deterministic and Governance-independent (US5, P3)

```bash
for i in 1 2 3; do fsgg-sdd refresh --work <id> --json > out-$i.json; done
diff out-1.json out-2.json && diff out-2.json out-3.json   # identical
```

Expected: regenerated views and JSON reports are byte-identical across runs; the
command works with Governance absent; any Governance pointers appear only as
advisory compatibility facts and are never enforced (no stale-view blocking at a
protected boundary).

## Acceptance traceability

| Scenario | User stories | Requirements |
|---|---|---|
| 1 | US1 | FR-001..FR-004, FR-007, FR-017 |
| 2 | US2 | FR-008..FR-011, FR-020 |
| 3 | US3 | FR-005, FR-006, FR-013 |
| 4 | US4 | FR-012, FR-021 |
| 5 | US5 | FR-018, FR-019, FR-022..FR-024 |
