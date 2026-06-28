# Phase 1 Data Model: version authorities & coherence state

This feature is release-engineering / process; it introduces no F# types and no `.fsgg` schema.
The "data model" here is the set of **version authorities** that must agree and the **coherence
states** between them — the conceptual model the checklist (FR-005) and the coherence contract
operate over.

## Entities (version authorities)

### Contract source version
- **What**: the `FS.GG.Contracts` fsproj `<Version>` and the matching
  `Fsgg.ContractVersion.value` string.
- **Location**: `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` (`<Version>`),
  `src/FS.GG.Contracts/ContractVersion.fs` (`value`).
- **Current**: `1.1.0` (set by feature 042).
- **Authority role**: the source of truth every other layer tracks. `release.yml` reads it via
  `dotnet msbuild -getProperty:Version`.
- **Invariant**: fsproj `<Version>` == `Fsgg.ContractVersion.value` (held by 042; not changed
  here).

### Feed package version
- **What**: the newest `FS.GG.Contracts` version obtainable from the org GitHub Packages feed.
- **Location**: `nuget.pkg.github.com/FS-GG` (queryable via the packages API).
- **Current**: `1.0.1` (only version published). **Target**: `1.1.0`.
- **Authority role**: what downstream consumers and Renovate actually resolve.
- **Transition**: `1.0.1 → 1.0.1 + 1.1.0` on a successful publish (prior versions remain;
  publish is additive and `--skip-duplicate`-idempotent).

### Registry pins
- **What**: two fields under `fsgg-contracts` in the org dependency registry.
  - `version` — the source pin the `contract-coherence` gate asserts equals the SDD source.
  - `package-version` — the last version actually on the feed.
- **Location**: `FS-GG/.github` `registry/dependencies.yml` (+ its `docs/registry/compatibility.md`
  projection). **Not** owned or edited by this repo.
- **Current**: `version: 1.1.0` (already advanced by FS-GG/.github#42), `package-version: 1.0.1`.
  **Target**: `package-version: 1.1.0`.
- **Authority role**: the coordination-layer record; the gate enforces `version` == source.

### Contracts-bump release checklist (new artifact)
- **What**: the durable maintainer runbook enumerating the three same-change actions for any
  future contracts source bump.
- **Location**: `docs/release/contracts-version-bump-checklist.md` (NEW).
- **Role**: human projection of the coherence contract; cites the registry + gate + ADR-0001 as
  authoritative (not a second source of truth).

## Coherence states (between the three authorities)

| State | source | feed | registry.version | registry.package-version | Meaning |
|---|---|---|---|---|---|
| **Coherent** | v | v (newest) | v | v | the goal — all four agree |
| **Source ahead of registry** | v+1 | v | v | v | the 042 failure mode — `contract-coherence` gate **red** |
| **Registry tracking, feed behind** | v+1 | v | v+1 | v | **current state** — gate green, feed a version behind |
| **Target (this feature)** | 1.1.0 | 1.1.0 | 1.1.0 | 1.1.0 | reached after publish (US1) + registry sync (US2) |

State progression for this feature:
`Registry tracking, feed behind` → *(publish 1.1.0, US1)* → feed = 1.1.0 → *(notify .github, US2)*
→ `package-version` = 1.1.0 → **Coherent**.

## Ordering rules (validation invariants)

- **FR-007**: `package-version` advances **only after** the feed confirms the version is
  obtainable — never speculatively, never ahead of the feed.
- **Idempotency**: re-publishing an already-present feed version is a no-op success
  (`--skip-duplicate`), so the publish step is safely re-runnable.
- **Drift guard (inherited)**: any version-bearing trigger tag must equal the fsproj `<Version>`,
  and an unreadable `<Version>` is a hard failure, never a silent skip (feature 039).
- **Frozen fixture**: `tests/fixtures/registry/dependencies.yml` is a 042 test snapshot, not a
  live authority; it is excluded from the coherence transitions above (research Decision 5).
