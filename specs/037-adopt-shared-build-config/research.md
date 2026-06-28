# Phase 0 Research: Adopt Shared Build Config

All inputs are concrete and merged upstream (ADR-0006, `.github#19`), so there are
no open `NEEDS CLARIFICATION` items. This document records the decisions and the
behavioral-risk analysis that the canonical files introduce.

## Source-of-truth snapshot (`FS-GG/.github` `dist/dotnet/`, fetched 2026-06-28)

The contract distributes **three** managed files (per `scripts/sync-build-config.sh`
`FILES`):

| File | Canonical content (summary) |
|---|---|
| `Directory.Build.props` | `Deterministic`; CPM + `CentralPackageTransitivePinningEnabled`; `RestorePackagesWithLockFile`; `RestoreLockedMode` gated on `GITHUB_ACTIONS` + lockfile-exists; `WarningsAsErrors=$(WarningsAsErrors);NU1603;NU1608`; imports `Directory.Build.local.props` **last**. |
| `Directory.Packages.props` | CPM + transitive pinning; **org-baseline** `PackageVersion FSharp.Core 10.1.301`; imports `Directory.Packages.local.props` **last**. |
| `.config/dotnet-tools.json` | `isRoot` manifest pinning `fake-cli 6.1.4`. |

The drift check (`--check`) diffs all three; a missing or differing file is DRIFT
(exit 1). A `.props` file lacking the marker `Source of truth: FS-GG/.github` is
treated as hand-authored and refused unless `--adopt` moves it to `*.local.props`.

## Decision 1 — Adopt all three managed files verbatim

- **Decision**: Sync `Directory.Build.props`, `Directory.Packages.props`, **and**
  `.config/dotnet-tools.json` byte-identically. SDD currently has no `.config/` and
  does not use FAKE.
- **Rationale**: The drift gate (FR-007, SC-001, SC-006) is the upstream `--check`,
  which covers all three files. A missing `.config/dotnet-tools.json` makes `--check`
  permanently red, defeating the gate. The only way to scope it to two files is to
  fork the sync tool — which violates "sync, not fork." The unused tool manifest has
  zero restore/build impact and embeds no rendering/Governance knowledge. Confirmed
  with the user.
- **Alternatives considered**: (a) Adopt only the two props files and narrow the
  check via a wrapper/path filter — rejected as a partial fork of the org contract
  that drifts from every other repo's gate. (b) Petition upstream to drop the tool
  manifest from `FILES` — out of scope and slower than adopting a harmless file.
- **Spec note**: This extends the spec's "the two canonical build files" framing to
  three. Recommend a one-line spec/`Key Entities` amendment noting the tool manifest
  is adopted verbatim for gate coherence.

## Decision 2 — `WarningsAsErrors` must append in `local.props`, not overwrite

- **Decision**: In `Directory.Build.local.props`, set
  `<WarningsAsErrors>$(WarningsAsErrors);FS3261;FS0025</WarningsAsErrors>`.
- **Rationale**: The canonical `Build.props` sets
  `WarningsAsErrors=$(WarningsAsErrors);NU1603;NU1608` **before** importing
  `local.props` (import is last). At that point `$(WarningsAsErrors)` is empty, so it
  becomes `;NU1603;NU1608`. If `local.props` then *assigns*
  `WarningsAsErrors=FS3261;FS0025`, the NU1603/NU1608 promotions are **lost**.
  Appending preserves the pre-adoption effective set `{FS3261, FS0025, NU1603,
  NU1608}` (order/leading-`;` are insignificant to MSBuild). This directly protects
  FR-006/SC-005.
- **Alternatives considered**: Re-declaring NU1603/NU1608 in `local.props` — rejected
  as redundant duplication of an org-owned concern (and brittle if upstream changes
  the set).

## Decision 3 — `CentralPackageTransitivePinningEnabled=true` is a new behavior to verify

- **Decision**: Accept the canonical `CentralPackageTransitivePinningEnabled=true`
  (SDD never set it) and **empirically verify** the resolved graph is unchanged
  before declaring adoption complete: run `dotnet restore FS.GG.SDD.sln
  --force-evaluate`, then `git diff -- '**/packages.lock.json'`. If any lockfile
  changes, inspect, accept only graph-equivalent churn, and commit the refreshed
  lockfiles (spec Assumption: lockfile refreshed as part of adoption).
- **Rationale**: Transitive pinning changes *what CPM is allowed to pin*, not the
  default resolution, so with no transitive `PackageVersion` overrides the resolved
  versions should be identical. But it can alter `packages.lock.json` content/shape,
  and FR-006 forbids a graph change — so this must be checked, not assumed. This is
  the single highest-risk item in the change.
- **Alternatives considered**: Disabling transitive pinning in `local.props` to force
  parity — rejected: that re-forks an org default and is the wrong direction
  (the org wants transitive pinning on). Verify-and-accept is correct.

## Decision 4 — Property/version partition (what moves, what drops, what stays)

- **Decision**:
  - **Dropped from SDD entirely** (now owned by the canonical file; do **not**
    re-declare in `local.props`): `Deterministic`, `ManagePackageVersionsCentrally`,
    `RestorePackagesWithLockFile`, `RestoreLockedMode`, the `NU1603;NU1608`
    promotion, and the local `FSharp.Core` `PackageVersion` (org baseline).
  - **Moved to `Directory.Build.local.props`**: `Version` (single source of truth),
    `TargetFramework=net10.0`, `LangVersion=preview`,
    `ContinuousIntegrationBuild=true`, `Nullable=enable`,
    `TreatWarningsAsErrors=false`, `WarningsAsErrors` (appended — Decision 2), and all
    package metadata (`Company`, `Authors`, `Product`, `RepositoryUrl`,
    `PackageLicenseExpression`, `PackageRequireLicenseAcceptance`).
  - **Moved to `Directory.Packages.local.props`**: `YamlDotNet 16.3.0`,
    `System.Text.Json 10.0.0`, `Spectre.Console 0.57.0`, `xunit 2.9.3`,
    `xunit.runner.visualstudio 3.1.5`, `Microsoft.NET.Test.Sdk 17.14.1`. Do **not**
    re-set `ManagePackageVersionsCentrally` (canonical owns it).
- **Rationale**: Each canonical-owned property must not be re-declared locally to
  avoid drift/redundancy; each SDD-specific property must be preserved so the
  effective build is unchanged (FR-002/FR-003/FR-006). `ContinuousIntegrationBuild`
  stays local because it is **not** in the canonical file — and the canonical
  `RestoreLockedMode` gate is deliberately `GITHUB_ACTIONS`-based (not CIB) so SDD's
  unconditional CIB does not fail-close a fresh local clone. SDD's pre-adoption gate
  condition is already identical to the canonical one, so this is structural, not
  behavioral (spec Assumption confirmed by reading both files).
- **FSharp.Core**: SDD's local pin `10.1.301` already equals the org baseline, so
  removing it is a no-op on resolution and avoids the CPM duplicate-pin error
  (`NU1504`/`NU1011`) — FR-004/SC-003.

## Decision 5 — Wire the drift check into per-PR CI

- **Decision**: Add a `drift-check` job to `.github/workflows/gate.yml` that checks
  out `FS-GG/.github` into a side path and runs
  `"$GITHUB_WORKSPACE/.ci-build-config/scripts/sync-build-config.sh" --check "$GITHUB_WORKSPACE"`
  against the SDD checkout.
- **Rationale**: No org reusable workflow exists yet (`.github#18` is unbuilt — the
  `dist/dotnet/.github/workflows` path 404s), so the gate must be self-contained. The
  sync tool resolves its source relative to its own location and takes the consumer
  repo as `TARGET`, so a second checkout is the minimal wiring. This makes US4/SC-006
  live without a broader CI redesign (spec scopes wiring to "the gate is live" only).
- **Alternatives considered**: Vendoring a copy of `sync-build-config.sh` into SDD —
  rejected (forks the tool, drifts). Waiting for `.github#18` — rejected (would leave
  the gate non-live, failing US4). Pinning the `FS-GG/.github` checkout to a SHA vs.
  `main`: prefer `main` to track the source of truth (a stale pin would itself become
  silent drift); revisit if upstream churn causes flakiness.

## Behavioral-equivalence verification plan (feeds tasks/quickstart)

1. Capture a pre-adoption baseline of effective MSBuild values per project
   (`dotnet build -getProperty:...` or `msbuild -pp`) for the SC-005 comparison.
2. After adoption: clean offline restore + build + full test suite must be green
   (SC-004), with no new warnings/errors vs. baseline.
3. `dotnet restore --force-evaluate` then diff `**/packages.lock.json` (Decision 3);
   commit any graph-equivalent refresh.
4. Drift check green on synced files (exit 0, SC-001); edit a canonical file and
   confirm non-zero + named file (SC-006); revert.
5. Confirm no local `FSharp.Core` pin and no `NU1504`/`NU1011` on restore (SC-003).
