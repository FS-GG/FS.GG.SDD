# Phase 1 Data Model: Correct capabilities schema version to 2

This feature touches three entities. None are new types; the change is to the
*value* of a declared constant, the *version* of the package identity, and an
external registry *pin*. No `.fsi` signature, record shape, or
`Schemas.entries` membership changes.

## Entity 1 — Declared schema-version constant (Governance-owned)

A value the package **declares** to the Governance published reference. It is
**not** a value SDD emits. The set has four members; only `capabilities` changes.

| Constant (`Fsgg.Schemas.*`) | Owner | Before | After | Source of truth (`Schemas.fs`) |
|-----------------------------|-------|:-----:|:----:|---------------------------------|
| `governanceVersion`   | Governance | 1 | 1 (unchanged) | line 162 |
| `policyVersion`       | Governance | 1 | 1 (unchanged) | line 163 |
| `capabilitiesVersion` | Governance | 1 | **2** | **line 164** |
| `toolingVersion`      | Governance | 1 | 1 (unchanged) | line 165 |

- **Type**: `int` — unchanged. `Schemas.fsi` declares `val capabilitiesVersion: int`
  and is **not** edited.
- **Validation rule (FR-001/FR-003)**: `capabilitiesVersion = 2`, grounded against
  the Governance validator's supported value; asserted by the verification suite.
- **Invariants (FR-002/FR-007)**: the three siblings and every SDD-owned constant
  retain their current values; the `Schemas.entries` enumeration (10 schemas,
  owners, contract versions) is byte-identical except for the `capabilities`
  `SchemaVersion` it derives from the constant.
- **No emitted-output coupling**: this value is never asserted against any
  SDD-emitted artifact and never appears in `release-readiness.json`.

## Entity 2 — FS.GG.Contracts package version

The NuGet identity/version of the shared contract package.

| Field | Before | After | Source of truth |
|-------|:-----:|:----:|-----------------|
| `<Version>` | `1.0.0` | **`1.0.1`** | `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` line 9 |
| `Fsgg.ContractVersion.value` / `.patch` | `"1.0.0"` / `0` | **`"1.0.1"` / `1`** | `src/FS.GG.Contracts/ContractVersion.fs` |

- **State transition**: `1.0.0` → `1.0.1` is a **forward-only, additive** bump.
  Both identities are immutable; `1.0.0` is never re-packed or mutated (FR-004).
- **Internal-coherence coupling (discovered at delivery)**: the org
  `contract-coherence` gate (`FS-GG/.github` `.github/workflows/contract-coherence.yml`)
  asserts the fsproj `<Version>` **equals** the self-describing `Fsgg.ContractVersion.value`
  (and uses that value as the "actual" version for the registry pin-drift check). The
  package version bump therefore **requires** an in-lockstep `ContractVersion` bump
  (`value "1.0.1"`, `patch 1`; `major`/`minor` unchanged). The `ContractVersion.fsi`
  signature and `PublicSurface.baseline` (binding names only, not values) are unchanged.
  `ContractVersionTests` asserts `"1.0.1"`/patch `1` (fail-before/pass-after).
- **Validation rule (FR-005/SC-002)**: a packed `FS.GG.Contracts.1.0.1.nupkg`
  resolves from the shared local folder feed and, when its `Schemas` are read,
  reports `capabilities = 2`.
- **Coupling note**: `release.yml` reads this `<Version>`; the bump means a future
  (un-deferred) GH Packages release would publish `1.0.1`. That is intended and
  coherent, not triggered here (FR-008).

## Entity 3 — Org dependency registry pin (`fsgg-contracts`)

The cross-repo registered version of the contract, owned by `sdd`, living in
`FS-GG/.github` (not this repo).

| Field | Before | After | Location |
|-------|:-----:|:----:|----------|
| `fsgg-contracts` pin | `1.0.0` | **`1.0.1`** | `FS-GG/.github` org registry (follow-on issue/PR) |

- **State transition**: advanced by the `owner: sdd` after `1.0.1` publishes
  (FR-006).
- **Validation rule (SC-003)**: the registry reads `1.0.1` and the
  contract-coherence workflow passes.
- **Scope**: tracked via the `cross-repo-coordination` protocol; out of this
  repo's tree.

## Supporting config — Shared local folder feed source

Not a domain entity, but the delivery substrate introduced in-repo.

| Artifact | Purpose |
|----------|---------|
| `nuget.config` (repo root) | Adds local folder source `fsgg-local` → `./.fsgg-local-feed`, alongside inherited sources (no `<clear/>`). |
| `.fsgg-local-feed/.gitkeep` | Commits the feed directory so the configured source path exists (restore-safe while empty). |
