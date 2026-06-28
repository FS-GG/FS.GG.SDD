# Feature Specification: Adopt Shared Build Config

**Feature Branch**: `037-adopt-shared-build-config`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board" → resolved to FS-GG/FS.GG.SDD#11 (H3 · sdd — Adopt shared-build-config; gate already GITHUB_ACTIONS; drop local FSharp.Core pin)

## Overview

FS.GG.SDD currently maintains its own root `Directory.Build.props` and
`Directory.Packages.props`. Several properties in those files are now owned by
the org-shared `shared-build-config` contract whose source of truth is
`FS-GG/.github` `dist/dotnet/` (ADR-0006, `.github#19`, merged). This feature
adopts that contract by **syncing, not forking**: the canonical org files are
copied in unchanged and import repo-owned `*.local.props` files, into which all
SDD-specific settings move so they survive future syncs. A drift check makes any
local edit to the canonical files fail fast, keeping every FS-GG repo's build
baseline coherent.

The change is build-infrastructure only. It produces no new `fsgg-sdd` lifecycle
artifact, alters no CLI behavior, and changes no SDD-owned schema.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Repo tracks the org build baseline without forking (Priority: P1)

A maintainer adopts the org-shared .NET build configuration so the repo's
canonical build files are byte-identical to `FS-GG/.github` `dist/dotnet/` and
all repo-specific settings live in repo-owned `*.local.props` overrides.

**Why this priority**: This is the core of the coordination item — without it the
repo keeps a forked build baseline that silently drifts from the org standard
(the failure class ADR-0006 exists to eliminate). It is the minimum that
delivers the contract's value.

**Independent Test**: After adoption, the repo's root `Directory.Build.props` and
`Directory.Packages.props` match the canonical `dist/dotnet/` files exactly, and
a clean build/restore plus the full test suite pass locally and in locked-mode CI.

**Acceptance Scenarios**:

1. **Given** the repo has been adopted, **When** the canonical build files are
   compared against `FS-GG/.github` `dist/dotnet/`, **Then** they are identical.
2. **Given** the adopted repo, **When** a clean restore + build + test runs
   locally (offline, unlocked), **Then** it succeeds with no behavior change from
   the pre-adoption build.
3. **Given** the adopted repo in CI, **When** restore runs in locked mode under
   `GITHUB_ACTIONS`, **Then** it succeeds and any dependency-graph drift fails the
   build as before.

---

### User Story 2 - Repo-specific build settings survive a sync (Priority: P1)

A maintainer re-runs the sync to pick up a future org baseline change and finds
that every SDD-specific build property and package version is preserved because
it lives in `Directory.Build.local.props` / `Directory.Packages.local.props`,
which the sync never overwrites.

**Why this priority**: Adoption is worthless if it drops SDD's version, language,
warning-promotion, or package-metadata settings. Preserving them via the
`*.local.props` override seam is what makes "sync, not fork" safe to repeat.

**Independent Test**: Every property and package version that existed in the
pre-adoption files and is not part of the org baseline is present in the
corresponding `*.local.props` file, and the effective evaluated MSBuild values
are unchanged from before adoption.

**Acceptance Scenarios**:

1. **Given** the pre-adoption `Directory.Build.props`, **When** adoption
   completes, **Then** the single version source of truth (`Version`),
   `TargetFramework`, `LangVersion=preview`, `ContinuousIntegrationBuild=true`,
   the F# warning promotions (FS3261, FS0025), and all package metadata
   (`Company`, `Authors`, `Product`, `RepositoryUrl`, license fields) appear in
   `Directory.Build.local.props`.
2. **Given** the pre-adoption `Directory.Packages.props`, **When** adoption
   completes, **Then** every non-baseline `PackageVersion` (YamlDotNet,
   System.Text.Json, Spectre.Console, xunit, xunit.runner.visualstudio,
   Microsoft.NET.Test.Sdk) appears in `Directory.Packages.local.props`.
3. **Given** the adopted repo, **When** effective MSBuild properties are
   evaluated for each project, **Then** they equal the pre-adoption values.

---

### User Story 3 - FSharp.Core stays in lockstep with the org (Priority: P2)

A maintainer relies on a single org-owned `FSharp.Core` baseline so the F# core
version can never silently diverge between FS-GG repos.

**Why this priority**: It removes one duplicated pin and is the concrete
coordination payoff of this item, but it is a single-package subset of the
broader "settings survive" guarantee, so it ranks just below the P1 stories.

**Independent Test**: The repo declares no local `FSharp.Core` `PackageVersion`;
the build resolves `FSharp.Core` to the org baseline (`10.1.301`) and central
package management raises no duplicate-version error.

**Acceptance Scenarios**:

1. **Given** the adopted repo, **When** the local package files are inspected,
   **Then** no `FSharp.Core` `PackageVersion` is declared locally.
2. **Given** the adopted repo, **When** restore runs, **Then** `FSharp.Core`
   resolves to the org baseline version with no `NU1504`/`NU1011` duplicate-pin
   error.

---

### User Story 4 - Drift from the org baseline fails fast (Priority: P2)

A maintainer (or CI) runs the drift check and any local edit to the canonical
files is rejected, so the org baseline cannot be silently forked again.

**Why this priority**: It converts the "don't fork" rule from convention into an
enforced gate, locking in the adoption — valuable but dependent on the adoption
already being in place.

**Independent Test**: With clean synced files the drift check passes (exit 0);
after a deliberate edit to a canonical file it fails (exit 1) and names the
drifted file.

**Acceptance Scenarios**:

1. **Given** freshly synced canonical files, **When** the drift check runs,
   **Then** it reports no drift and exits 0.
2. **Given** a canonical file edited locally, **When** the drift check runs,
   **Then** it reports the drift and exits non-zero.

---

### Edge Cases

- **First local clone, no lockfile**: locked-mode restore is gated on
  `GITHUB_ACTIONS` AND an existing `packages.lock.json`, so a fresh clone or first
  restore bootstraps the lockfile instead of being wedged. Adoption MUST preserve
  this behavior (the canonical gate matches the pre-adoption condition exactly).
- **Property defined both in canonical and local**: the canonical files import
  `*.local.props` last, so a local override wins (MSBuild last-write-wins). The
  outcome MUST match the pre-adoption effective value.
- **Package pinned both centrally (baseline) and locally**: a baseline package
  re-declared locally raises `NU1504`/`NU1011`; adoption MUST NOT re-declare any
  org-baseline package (currently only `FSharp.Core`) in the local files.
- **Re-running adoption / sync**: running the sync again on an already-adopted
  repo MUST be idempotent — canonical files unchanged, `*.local.props` untouched,
  drift check still green.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repo's three canonical managed files —
  `Directory.Build.props`, `Directory.Packages.props`, and
  `.config/dotnet-tools.json` — MUST be the canonical `shared-build-config` files
  from `FS-GG/.github` `dist/dotnet/`, byte-identical and carrying no local edits.
  (`.config/dotnet-tools.json` is the upstream `fake-cli` tool manifest, unused by
  SDD but adopted verbatim because the drift gate covers all three managed files;
  scoping the sync to only the two props files would fork the org sync tool.)
- **FR-002**: All SDD-specific MSBuild properties MUST be relocated into a
  repo-owned `Directory.Build.local.props`, imported by the canonical
  `Directory.Build.props`. This includes the single `Version` source of truth,
  `TargetFramework`, `LangVersion=preview`, `ContinuousIntegrationBuild=true`,
  the `Nullable`/`TreatWarningsAsErrors` settings, the FS3261/FS0025 warning
  promotions, and all package metadata.
- **FR-003**: All non-baseline `PackageVersion` items MUST be relocated into a
  repo-owned `Directory.Packages.local.props`, imported by the canonical
  `Directory.Packages.props`.
- **FR-004**: The local `FSharp.Core` `PackageVersion` MUST be removed so the
  package resolves to the org baseline; no org-baseline package may be
  re-declared locally.
- **FR-005**: Locked-mode CI restore MUST remain gated on `GITHUB_ACTIONS` and an
  existing lockfile (unchanged from today), so CI restores in locked mode and a
  fresh local clone is never blocked.
- **FR-006**: The effective evaluated build (target framework, version,
  determinism/CIB, warning promotions, lockfile enforcement, resolved package
  graph) MUST be unchanged from before adoption — adoption is a refactor of where
  settings live, not a change to what they are.
- **FR-007**: A drift check MUST be available that exits non-zero when any
  canonical file diverges from the org source of truth, and exit zero when in
  sync.
- **FR-008**: Adoption MUST NOT add any rendering-, template-, or
  Governance-specific knowledge to SDD build config, and MUST NOT introduce any
  new `fsgg-sdd` lifecycle artifact, CLI behavior, or SDD-owned schema change.
- **FR-009**: The per-PR CI gate (restore + build + test, locked mode) MUST pass
  green after adoption.

### Key Entities *(include if feature involves data)*

- **Canonical build files**: `Directory.Build.props`, `Directory.Packages.props`,
  and `.config/dotnet-tools.json` mirrored from `FS-GG/.github` `dist/dotnet/`;
  org-owned, never locally edited. The two props files import the local override
  files last; `.config/dotnet-tools.json` is the unused `fake-cli` manifest
  adopted verbatim for drift-gate coherence and is not referenced by any SDD
  build logic.
- **Local override files**: `Directory.Build.local.props` and
  `Directory.Packages.local.props`; repo-owned, hold every SDD-specific property
  and non-baseline package version, never overwritten by a sync.
- **Org baseline package set**: versions every FS-GG repo agrees on (currently
  `FSharp.Core 10.1.301`); pinned centrally upstream, never re-declared locally.
- **Drift check**: the `--check` mode of the upstream sync tool; the enforcement
  gate that fails when canonical files diverge from the source of truth.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The three canonical managed files (`Directory.Build.props`,
  `Directory.Packages.props`, `.config/dotnet-tools.json`) are 100% byte-identical
  to the `FS-GG/.github` `dist/dotnet/` source of truth (drift check exits 0).
- **SC-002**: 100% of pre-adoption SDD-specific properties and non-baseline
  package versions are present in the `*.local.props` files; none are lost.
- **SC-003**: The repo declares zero local `FSharp.Core` pins and zero
  org-baseline package duplications (no `NU1504`/`NU1011`).
- **SC-004**: A clean restore + build + full test suite passes both locally
  (offline, unlocked) and in locked-mode CI, with no new warnings or errors
  versus the pre-adoption baseline.
- **SC-005**: Effective evaluated MSBuild values for every project are identical
  before and after adoption (zero behavioral change).
- **SC-006**: The drift check fails (non-zero) on a deliberately edited canonical
  file, proving the gate is live.

## Assumptions

- The `shared-build-config` contract and its sync tooling (`dist/dotnet/` files,
  `scripts/sync-build-config.sh` with `--adopt` and `--check`, and the adoption
  guide at `docs/build/README.md`) are stable and merged in `FS-GG/.github`
  (ADR-0006, `.github#19`), and are the authoritative source of truth.
- The org baseline currently pins only `FSharp.Core` (`10.1.301`); SDD's existing
  local `FSharp.Core` pin already equals it, so removing the local pin is a no-op
  on the resolved graph.
- SDD's existing locked-restore gate condition already matches the canonical
  `GITHUB_ACTIONS`-based gate, so the gate change is structural (where it is
  defined) rather than behavioral.
- The committed `packages.lock.json` (if present) remains valid; if package
  relocation changes lockfile content, the lockfile is refreshed as part of
  adoption and committed.
- This is a build-infrastructure refactor; it requires no Governance runtime and
  produces no lifecycle artifact, so it is exempt from the SDD evidence/readiness
  artifact obligations beyond a normal green build/test verification.
- Wiring the drift check into a per-PR CI workflow, if not already present, is in
  scope only to the extent the coordination item requires the gate to be live;
  broader CI redesign is out of scope.
