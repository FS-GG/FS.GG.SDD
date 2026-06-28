# Feature Specification: Publish the `fsgg-sdd` CLI as a dotnet tool

**Feature Branch**: `044-publish-cli-tool`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "start the next unblocked sdd item on the coordination board." → resolved to Coordination board item **FS-GG/FS.GG.SDD#31** (`Ready`, P5 Versioning): *Publish FS.GG.SDD.Cli (fsgg-sdd) as a dotnet tool to the org feed — unblocks the typed registry-validator gate.*

## Context & Motivation *(why this feature exists)*

The reusable cross-repo `contract-coherence` gate (FS-GG/.github#18) is meant to validate every repo's `registry/dependencies.yml` with the typed SDD validator `fsgg-sdd registry validate` (feature 042), replacing the Python stand-in `scripts/validate-registry.py`. That swap (FS-GG/.github#49) is **blocked** because:

1. **The CLI is unpublished.** The org NuGet feed (`nuget.pkg.github.com/FS-GG`) serves `FS.GG.Contracts` and the `FS.GG.UI.*` set only. `release.yml` (features 039/043) packs and pushes only `src/FS.GG.Contracts`; nothing packs the `PackAsTool` CLI. `dotnet tool install FS.GG.SDD.Cli` has nothing to resolve.
2. **The published Contracts package cannot load the registry alone.** `FS.GG.Contracts 1.1.0` ships `Fsgg.Registry.validateDocument` but not the YAML loader (`RegistryDocument.load` lives in the unpublished `FS.GG.SDD.Artifacts`). A `.github`-only wrapper against the feed package still cannot parse `dependencies.yml`.

Running the typed validator from `.github` CI today would require building `FS.GG.SDD.Cli` from a full SDD source checkout — coupling the org-wide coherence gate to SDD's build health. Publishing the CLI as a self-contained dotnet tool removes that coupling: the tool package bundles its dependency assemblies (including the YAML loader), so a consumer needs only the feed and a run-scoped token.

This is producer-side release automation in the same lineage as features 039 (publish Contracts) and 043 (Contracts 1.1.0 + bump checklist). It extends the existing `release.yml` producer; it does not introduce a new lifecycle stage.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consumer CI installs and runs the validator without an SDD checkout (Priority: P1)

The `.github` `contract-coherence` gate (and any FS-GG repo CI that calls it) installs the published `fsgg-sdd` tool from the org feed and validates its `registry/dependencies.yml` with the typed validator, using only a run-scoped `GITHUB_TOKEN` and no FS.GG.SDD source checkout.

**Why this priority**: This is the entire point of #31 — it unblocks FS-GG/.github#49 (coherence id `registry-validator-typed`). Without it the gate is stuck on the Python stand-in. Everything else is in service of this consumer being able to install and run.

**Independent Test**: From a clean environment with only the org feed configured, run `dotnet tool install --global FS.GG.SDD.Cli --version <v> --add-source https://nuget.pkg.github.com/FS-GG/index.json` followed by `fsgg-sdd registry validate registry/dependencies.yml --text`, and observe the typed validator parse and validate the document and exit with the documented code — with no FS.GG.SDD repository present.

**Acceptance Scenarios**:

1. **Given** the org feed serves the published CLI tool package and a consumer with a run-scoped token, **When** the consumer runs `dotnet tool install --global FS.GG.SDD.Cli --version <v> --add-source <org feed>`, **Then** installation succeeds and exposes the `fsgg-sdd` command.
2. **Given** the tool is installed and a valid `registry/dependencies.yml`, **When** the consumer runs `fsgg-sdd registry validate registry/dependencies.yml --text`, **Then** the YAML document is loaded and validated and the command reports success — proving the tool is self-contained (the loader is bundled, not resolved from SDD source).
3. **Given** the tool is installed and an invalid/malformed `dependencies.yml`, **When** the consumer runs `fsgg-sdd registry validate`, **Then** the typed validator reports the violations and exits non-zero, so the coherence gate fails the build.
4. **Given** the published package, **When** a consumer fetches it with only a run-scoped `GITHUB_TOKEN` (no PAT), **Then** restore/install succeeds because the feed package is public (as `FS.GG.Contracts` is).

---

### User Story 2 - The release producer also packs and pushes the CLI tool (Priority: P2)

On a release of FS.GG.SDD (or a pushed `v*` tag, or a manual dispatch), the existing producer additionally packs the `PackAsTool` CLI and pushes it to the org feed — on the same triggers, behind the same test gate, idempotent, dry-run-capable, and canonical-repo-only — alongside the Contracts package.

**Why this priority**: P1 cannot be satisfied until the producer emits the package. It is P2 only because it is the mechanism behind P1's outcome; both must land together.

**Independent Test**: Trigger the release workflow via manual dispatch with no version input and observe the CLI tool package (`FS.GG.SDD.Cli.<version>.nupkg`) packed into the artifacts directory and **not** pushed; trigger it on a release/tag and observe both the Contracts package and the CLI tool package pushed to the org feed with `--skip-duplicate`.

**Acceptance Scenarios**:

1. **Given** a real publish event (release published, or `v*` tag), **When** the producer runs, **Then** it packs and pushes **both** `FS.GG.Contracts` and `FS.GG.SDD.Cli` to the org feed.
2. **Given** a manual dispatch with no version input, **When** the producer runs, **Then** it packs the CLI tool package but pushes nothing (a dry run), consistent with the Contracts dry-run behavior.
3. **Given** the publish-gate tests are red, **When** the producer runs, **Then** the push never executes for either package.
4. **Given** a version that already exists on the feed, **When** the producer pushes the CLI tool package, **Then** the push is an idempotent no-op success (`--skip-duplicate`) and does not fail the run.
5. **Given** a fork of FS.GG.SDD, **When** any release/tag/dispatch event fires, **Then** no publish path runs for the CLI (the canonical-repo guard holds, as for Contracts).
6. **Given** the CLI's effective package `<Version>` cannot be read on a real publish event, **When** the producer runs, **Then** it fails loudly and refuses to silently skip the CLI publish.

---

### User Story 3 - Any FS-GG developer installs the tool to run lifecycle commands (Priority: P3)

A developer on any FS-GG repo installs the published `fsgg-sdd` tool (globally or as a local tool) and runs any SDD lifecycle/cross-cutting command without cloning or building FS.GG.SDD.

**Why this priority**: A direct benefit of publishing, but not required to unblock #49. It broadens reach beyond the coherence gate.

**Independent Test**: Install the tool and run `fsgg-sdd --help` (and a representative command such as `fsgg-sdd verify`/`registry validate`), observing the same command behavior a source build produces.

**Acceptance Scenarios**:

1. **Given** the published tool, **When** a developer runs `dotnet tool install`/`dotnet tool run` for `FS.GG.SDD.Cli`, **Then** the `fsgg-sdd` command is available and runs commands identically to a source-built CLI.

---

### Edge Cases

- **Version line divergence**: The CLI carries the single SDD product-line version (currently `0.2.0`, from `Directory.Build.local.props`), distinct from the Contracts line (`1.1.0`). The two packages are published from one workflow run but are not required to share a version; a tag drift-guard must not falsely fail when only one line moves.
- **Version-bearing tag drift**: A `v<semver>` tag that matches **neither** evaluated version line (Contracts nor CLI) must fail the run and be reconciled (the fsproj/props are the source of truth), not silently overridden. A tag matching either line is valid and publishes both packages at their own evaluated versions (FR-005, "matches at least one line").
- **Empty pack**: A green test run that produces no CLI `.nupkg` MUST NOT be reported as a successful publish.
- **Partial publish**: If one of the two packages fails to push (for a reason other than `--skip-duplicate`), the run fails; an incomplete publish is never reported as complete.
- **First-publish visibility**: A package first pushed to GitHub Packages may default to private; the CLI package must end up public so consumer CI can restore it with a run-scoped token.
- **Self-containment regression**: A future change that makes the CLI rely on an unpublished assembly at runtime must surface as an install/run failure of `registry validate`, not as a silent partial tool.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The release producer MUST pack the `PackAsTool` CLI project (`src/FS.GG.SDD.Cli`, `PackageId=FS.GG.SDD.Cli`, `ToolCommandName=fsgg-sdd`) into a dotnet tool package as part of the existing release flow.
- **FR-002**: The producer MUST push the CLI tool package to the org GitHub Packages feed (`https://nuget.pkg.github.com/FS-GG/index.json`) with `--skip-duplicate`, using the run-scoped `GITHUB_TOKEN` with `packages: write` (no PAT), under least-privilege scoping consistent with the Contracts publish.
- **FR-003**: The producer MUST publish the CLI on the same trigger surface as Contracts: release published, `v*` tag push, and manual dispatch; and MUST gate the push on the existing publish-gate tests so a red run never pushes.
- **FR-004**: On a manual dispatch with no version input, the producer MUST pack the CLI tool package but push nothing (a dry run), consistent with the Contracts dry-run behavior.
- **FR-005**: The producer MUST resolve the CLI package version from the evaluated CLI package `<Version>` (the single SDD product-line value), independently of the Contracts version, and MUST publish that evaluated version rather than silently overriding it. Because the producer now emits two independently-versioned packages, the version-bearing (`v<semver>`) tag guard is the **generalized "matches at least one line" rule**: on a real publish event a version-bearing tag MUST equal at least one of the two evaluated versions (Contracts or CLI), else the run fails; each package still publishes its own evaluated version regardless of which line the tag named. (This generalizes the feature-039 single-package guard; see the plan's research Decision 2 and `contracts/release-workflow.md` "Version-resolution contract".)
- **FR-006**: On a real publish event, an unreadable CLI `<Version>` MUST fail the run loudly; the producer MUST NOT silently degrade a real publish to a no-op.
- **FR-007**: The producer MUST assert that a CLI `.nupkg` was actually produced; a green test plus an empty pack MUST NOT be reported as a successful publish.
- **FR-008**: A re-publish of an already-present CLI version MUST be an idempotent no-op success (`--skip-duplicate`), not a failure.
- **FR-009**: No CLI publish path MUST run from a fork or from any repository other than the canonical `FS-GG/FS.GG.SDD`.
- **FR-010**: The published CLI tool package MUST be self-contained — bundling the dependency assemblies it needs at runtime (including the registry YAML loader from `FS.GG.SDD.Artifacts`) so that `fsgg-sdd registry validate <path>` runs against the installed tool alone, with no FS.GG.SDD source checkout.
- **FR-011**: The CLI feed package MUST be public (as `FS.GG.Contracts` is), so consumer CI can restore/install it with a run-scoped `GITHUB_TOKEN`.
- **FR-012**: An incomplete publish MUST NOT be reported as complete: if either the Contracts or the CLI push fails (other than `--skip-duplicate`), the run MUST fail.
- **FR-013**: The release-automation contract document governing the producer MUST be updated to describe the CLI publish (triggers, version-resolution, gating, dry run, idempotency, visibility) so the workflow YAML remains the implementation of a written contract, not folklore.
- **FR-014**: The CLI publish MUST NOT alter the existing Contracts publish behavior, version line, or its golden/deterministic contracts; Contracts remains independently versioned and published — **with one documented exception**: the version-bearing-tag guard is generalized to the "matches at least one line" rule (FR-005), which only *relaxes* the prior Contracts rule (a tag matching the CLI line but not Contracts no longer fails the Contracts publish). Contracts' version source, gating, idempotency, least-privilege credentials, canonical-repo guard, and single-package scope are unchanged. (Reconciliation: plan research Decision 2; `contracts/release-workflow.md` "Supersession".)

### Key Entities *(include if feature involves data)*

- **CLI tool package**: The packed `FS.GG.SDD.Cli` dotnet tool (`ToolCommandName=fsgg-sdd`), self-contained, versioned on the SDD product line, published to the org feed.
- **Org GitHub Packages feed**: `nuget.pkg.github.com/FS-GG` — the shared NuGet source; the install/restore source for consumers.
- **Release-automation contract**: The written contract (`specs/039-publish-contracts-package/contracts/release-workflow.md` lineage) that the `release.yml` producer implements.
- **Consumer coherence gate**: The reusable `contract-coherence` workflow (FS-GG/.github#18/#49) that will install and run `fsgg-sdd registry validate`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From an environment with only the org feed and a run-scoped token (no FS.GG.SDD checkout), a consumer can install the tool and run `fsgg-sdd registry validate registry/dependencies.yml` in two commands and get a correct verdict.
- **SC-002**: After a release/tag publish, the org feed serves a `FS.GG.SDD.Cli` package at the released version (`gh api /orgs/FS-GG/packages?package_type=nuget` lists it), in addition to `FS.GG.Contracts`.
- **SC-003**: The published CLI tool validates both a well-formed and a malformed `dependencies.yml` correctly (success exit and non-zero exit respectively) without any SDD source present — confirming self-containment.
- **SC-004**: FS-GG/.github#49 can proceed: its `contract-coherence` gate can swap the Python stand-in for `fsgg-sdd registry validate` with no full SDD build wired into the org gate.
- **SC-005**: Re-running a publish for an already-published CLI version completes successfully without error and without creating a duplicate (idempotent).
- **SC-006**: No publish occurs on a fork or non-canonical repository, and no publish occurs when the publish-gate tests fail.

## Assumptions

- The CLI is versioned on its own SemVer (the single SDD product line, currently `0.2.0` from `Directory.Build.local.props`), not pinned to the Contracts wave (`1.1.0`). The issue permits either; the repo already establishes a distinct SDD product line, so this is the lower-friction default.
- A dotnet tool package produced by `dotnet pack` on a `PackAsTool` project bundles its referenced assemblies, satisfying the self-containment requirement (FR-010) without additional packaging work beyond verification.
- The existing `release.yml` producer (features 039/043) is the integration point; this feature extends it rather than adding a separate workflow, keeping triggers, gating, concurrency, and the canonical-repo guard shared.
- Making the feed package public is a one-time package-visibility action on GitHub Packages (as was done for `FS.GG.Contracts`); the feature owns ensuring it happens, even though visibility is a feed-side setting rather than workflow YAML.
- Consumer wiring of the `contract-coherence` gate to call the published tool is owned by FS-GG/.github#49 in the `.github` repo; this feature delivers and verifies the producer + published artifact only.
