# Quickstart / Validation Guide: Governance Readiness Handoff Contract

This guide validates that `fsgg-sdd ship` emits a deterministic, declared-facts-only
`readiness/<id>/governance-handoff.json`, that `fsgg-sdd refresh` keeps it current,
and that it is produced with no Governance runtime or `.fsgg` config present.

See [contracts/governance-handoff.md](contracts/governance-handoff.md) for the
schema and [contracts/integration-requirements.md](contracts/integration-requirements.md)
for the field→consumer mapping.

## Prerequisites

- .NET SDK (`net10.0`).
- A disposable SDD project advanced to ship readiness (the command tests build one
  via `TestSupport`; manually, run `init` → … → `verify` → `ship`).

## Scenario 1 — Ship emits the handoff (no Governance)

```text
fsgg-sdd init      # empty dir, no .fsgg/policy.yml|capabilities.yml|tooling.yml
… charter → specify → … → verify
fsgg-sdd ship --json
```

Expected:

- `readiness/<id>/governance-handoff.json` exists with `schemaVersion: 1`,
  `contractVersion: "1.0.0"`, a `generatorVersion`, and `sources[]` with digests.
- `governanceConfig` is all-`false` with no pointers; the command still succeeds
  (FR-011, SC-002).
- `evidence.nodes[].state` values are only `pending|real|synthetic|failed|skipped`
  — no `autoSynthetic` (SC-005).
- `evidence.dependencies[]` endpoints all appear in `nodes`.
- No selected route, gate id, profile, severity, or pass/fail verdict appears
  (boundary-exclusion, SC-005).

## Scenario 2 — Determinism

```text
fsgg-sdd ship --json   # run twice over the identical source tree
```

Expected: the two `governance-handoff.json` outputs are byte-identical (SC-003).

## Scenario 3 — Evidence mapping and edges

Author evidence with mixed states (real, a `synthetic` entry, a deferral, a
missing/failed entry) and task dependencies, then ship.

Expected: each declared state maps per the
[mapping table](contracts/integration-requirements.md) (`synthetic` dominates;
deferral → `skipped`; missing → `pending`; failed → `failed`); the task topology
appears as `task:*`→`task:*` and `evidence:*`→`task:*` edges (SC-001, SC-004).

## Scenario 4 — Stale detection and refresh

```text
# modify a contributing source (e.g., add an evidence entry) WITHOUT re-shipping
fsgg-sdd refresh --json
```

Expected: before refresh the handoff is reported `stale` against the changed source
digest; after `refresh` it is `current` with updated `sources[]` (FR-012, SC-006).

## Scenario 5 — Authored sources preserved

Expected: `.fsgg/*`, `work/<id>/*`, and all authored sources are byte-identical
before and after `ship`/`refresh`; only generated views change (FR-015, SC-007).

## Scenario 6 — Governance can consume (cross-repo, informational)

A Governance reader pinned to `contractVersion` 1.x can feed `evidence.nodes` +
`evidence.dependencies` into `Kernel.Evidence.build` and run `Evidence.effective`
without reading any SDD authored source (SC-001). This validation lives in the
Governance repo; SDD only proves it emits the agreed shape.

## Automated coverage

`tests/FS.GG.SDD.Commands.Tests/GovernanceHandoffTests.fs` covers Scenarios 1–5
(projection, mapping table, edges, determinism, no-Governance, boundary-exclusion,
stale/refresh, byte-identity). A real CLI `ship`/`refresh` smoke is captured under
`specs/017-governance-handoff/readiness/` as Constitution VI evidence.
