# Implementation Plan: Adopt Shared Build Config

**Branch**: `037-adopt-shared-build-config` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/037-adopt-shared-build-config/spec.md`

## Summary

Adopt the FS-GG org-shared `shared-build-config` contract (ADR-0006, `.github#19`)
by **syncing, not forking**: replace SDD's hand-authored root `Directory.Build.props`
and `Directory.Packages.props` with the canonical files from `FS-GG/.github`
`dist/dotnet/` (byte-identical), and move every SDD-specific property and
non-baseline package version into repo-owned `Directory.Build.local.props` /
`Directory.Packages.local.props` that the canonical files import last. Drop the
local `FSharp.Core` pin so it resolves to the org baseline (`10.1.301`). Adopt the
third managed file (`.config/dotnet-tools.json`, an unused `fake-cli` manifest)
verbatim so the upstream drift check (`scripts/sync-build-config.sh --check`) is
green, and wire that check into per-PR CI. This is a build-infrastructure refactor
of **where** settings live, not **what** they are: the effective evaluated build and
resolved package graph are unchanged, and no `fsgg-sdd` lifecycle artifact, CLI
behavior, or SDD-owned schema changes.

## Technical Context

**Language/Version**: MSBuild / NuGet Central Package Management (CPM); F# on
`net10.0` is unaffected (no F# source changes).

**Primary Dependencies**: The org `shared-build-config` + `lockfile-restore-enforcement`
contracts, distributed from `FS-GG/.github` `dist/dotnet/` via
`scripts/sync-build-config.sh` (`--adopt` / plain / `--check`). Drift-check CI
requires checking out `FS-GG/.github` (no org reusable workflow exists yet —
`.github#18` is unbuilt).

**Storage**: Files only — three canonical managed files
(`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`),
two repo-owned override files (`Directory.Build.local.props`,
`Directory.Packages.local.props`), and the per-project `packages.lock.json` files
(regenerated and committed if relocation changes them).

**Testing**: The existing test suite (xUnit) plus the build itself are the
verification. New checks: (1) clean offline restore + build + full test suite is
green; (2) `--check` exits 0 on synced files and non-zero on a deliberately edited
canonical file; (3) effective evaluated MSBuild values match the pre-adoption
baseline.

**Target Platform**: .NET 10 toolchain on Linux/CI (`ubuntu-latest`,
`GITHUB_ACTIONS`).

**Project Type**: Build-infrastructure refactor in a single multi-project F#
repository (5 `src/` projects, 6 `tests/` projects, scaffold fixtures).

**Performance Goals**: None (build-config only).

**Constraints**: Zero behavioral change (FR-006/SC-005); byte-identical canonical
files (FR-001/SC-001); no org-baseline package re-declared locally (FR-004/SC-003);
locked-restore gate condition unchanged (FR-005); no rendering/template/Governance
knowledge and no new lifecycle artifact/CLI/schema (FR-008); offline inner loop
preserved (canonical gate is `GITHUB_ACTIONS` + lockfile-exists, never forced
locally).

**Scale/Scope**: ~5 files written/moved at the repo root, ~10 `packages.lock.json`
files potentially refreshed, one CI workflow edited.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

This feature changes no F# source and produces no lifecycle artifact, so the
code-centric principles are **N/A by construction**; the relevant gates pass.

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → Tests → Implementation | **N/A** | No F# change. The verification ordering is build/test/drift-check, captured in quickstart. |
| II. Structured Artifacts Are the Machine Contract | **PASS** | No lifecycle artifact added (FR-008). The machine contract here is the MSBuild import seam + the `--check` drift gate, documented in `contracts/`. |
| III. Visibility in `.fsi` | **N/A** | No public F# surface change. |
| IV. Idiomatic Simplicity | **PASS** | Sync-not-fork is the simplest adoption; no new abstraction. |
| V. Elmish/MVU Boundary | **N/A** | The only "I/O tool" is the external upstream shell script; SDD adds no stateful workflow code. |
| VI. Test Evidence Is Mandatory | **PASS (scoped)** | No behavior-changing F#, so no new unit tests. Evidence = green offline build + full test suite + a demonstrated drift-check pass/fail (US4, SC-006). Spec assumption explicitly exempts this from evidence/readiness-artifact obligations beyond a green build/test. |
| VII. Agent & Human Workflows Share One Contract | **PASS** | No agent-surface or artifact contract changes. |
| VIII. Observability And Safe Failure | **PASS** | The drift check is the actionable diagnostic: it names the drifted file and exits non-zero (FR-007). Distinguishes "in sync" (exit 0) from "drift" (exit 1). |

**Engineering Constraints**: `net10.0` preserved (moves to `local.props`
`TargetFramework`); `FS.GG.SDD.*` namespace unchanged; no rendering/template/
Governance package IDs, templates, or docs URLs introduced (FR-008); SDD stays
buildable/usable without Governance.

**Change tier**: Build-infrastructure refactor with no public API / schema /
generated-view / command / artifact-layout / agent-skill change → **Tier 2**
(internal), per spec FR-008. No Tier 1 obligations triggered.

**Result**: PASS. No violations → Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/037-adopt-shared-build-config/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — decisions & risk analysis
├── data-model.md        # Phase 1 — file roles, ownership, import order, property/version inventory
├── quickstart.md        # Phase 1 — runnable adoption + verification scenarios
├── contracts/
│   └── adoption-contract.md   # Phase 1 — the sync/import/drift-check machine contract
└── tasks.md             # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

The change is confined to repo-root build files, a new `.config/`, and CI:

```text
.config/
└── dotnet-tools.json                 # NEW — canonical, verbatim (fake-cli 6.1.4; unused but required for green --check)

Directory.Build.props                 # REPLACED — canonical, byte-identical to FS-GG/.github dist/dotnet/
Directory.Packages.props              # REPLACED — canonical, byte-identical to FS-GG/.github dist/dotnet/
Directory.Build.local.props           # NEW — repo-owned; all SDD-specific MSBuild properties
Directory.Packages.local.props        # NEW — repo-owned; all non-baseline PackageVersion items

.github/workflows/gate.yml            # EDITED — add drift-check job (checkout FS-GG/.github; run --check)

**/packages.lock.json                 # REFRESHED IF NEEDED — committed if relocation changes content
```

**Structure Decision**: Single-repository build-infrastructure change. No `src/`
or `tests/` F# code is touched; the existing 5 `src/` + 6 `tests/` projects inherit
the new canonical → local import chain unchanged. The canonical files are owned
upstream (never edited locally); SDD owns only the two `*.local.props` overrides and
the CI wiring.

## Phase 0 — Research

See [research.md](./research.md). Resolved decisions:

1. **Adopt all three managed files verbatim** (incl. `.config/dotnet-tools.json`),
   not just the two props files — required so `--check` is green without forking the
   sync tool. (Confirmed with the user; extends the spec's "two files" framing.)
2. **`WarningsAsErrors` must append, not overwrite.** The canonical `Build.props`
   sets `WarningsAsErrors=$(WarningsAsErrors);NU1603;NU1608` *before* importing
   `local.props`. So `local.props` must set
   `WarningsAsErrors=$(WarningsAsErrors);FS3261;FS0025` (append) — a plain
   assignment would drop NU1603/NU1608.
3. **`CentralPackageTransitivePinningEnabled=true` is new** (canonical sets it in
   both files; SDD never had it). Treated as a behavioral-risk item: verify the
   resolved graph and `packages.lock.json` files are unchanged via
   `dotnet restore --force-evaluate` + diff; refresh/commit lockfiles if they change.
4. **Properties that move to the canonical file are dropped from `local.props`**
   (`Deterministic`, `ManagePackageVersionsCentrally`, `RestorePackagesWithLockFile`,
   `RestoreLockedMode`, NU1603/NU1608 promotion) to avoid redundant/conflicting
   re-declaration. `ContinuousIntegrationBuild=true` stays in `local.props` (not in
   canonical); the canonical gate is `GITHUB_ACTIONS`-based precisely so SDD's forced
   CIB does not fail-close a local clone.
5. **Drift-check CI wiring**: no org reusable workflow exists yet, so add a job to
   `gate.yml` that checks out `FS-GG/.github` and runs
   `<.github>/scripts/sync-build-config.sh --check <sdd-workspace>`.

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md) — the file-role model (canonical vs local vs
  org-baseline vs drift-check), import order semantics, and the exhaustive
  property/package inventory of what moves where (the SC-002 preservation checklist).
- [contracts/adoption-contract.md](./contracts/adoption-contract.md) — the machine
  contract: the sync-tool CLI surface (`--adopt` / plain / `--check`), the MSBuild
  last-write-wins import seam, the drift-gate exit-code contract, and the
  no-baseline-duplication rule.
- [quickstart.md](./quickstart.md) — runnable adoption + verification scenarios
  mapping 1:1 to the four user stories and SC-001…SC-006.
