# Feature Specification: Format gate — `.editorconfig` Fantomas config, one-time layout-only reformat, pinned CI `--check`

**Feature Branch**: `065-format-gate`

**Created**: 2026-07-03

**Status**: Draft

**Input**: FS.GG.SDD roadmap issue #85 §1 — the deferred **US3 of feature 064** (`specs/064-build-ci-hygiene/`), split out of #74 / PR #84 because the one-time reformat is large enough to warrant its own reviewable PR. Design already settled in 064: research Decision 3, contract C3, quickstart C3. Repo-local, not cross-repo.

**Change Tier**: Tier 2 (build / tooling / CI / repo-config change). This feature adds a repo-owned `.editorconfig` (which *is* the Fantomas 6+ configuration) and a **non-required** `format` job to `.github/workflows/gate.yml`, and reformats the existing F# tree **once, layout-only**. It introduces **no** change to any `fsgg-sdd` CLI output, JSON automation contract, persisted schema, or golden baseline. Fantomas may reformat existing source, but only whitespace/layout — no token, identifier, or runtime-behaviour change, and every deterministic/golden contract MUST stay byte-identical. The managed org files (`Directory.Build.props`, `Directory.Packages.props`, and specifically `.config/dotnet-tools.json`) MUST stay byte-identical to `FS-GG/.github` `dist/dotnet/`; the pinned Fantomas is installed in CI **without** touching the managed tool manifest (the `build-config-drift` gate enforces this).

## Overview

The 2026-07-02 review (§5.2, remediation #10) found the repo has **no format
gate**: there is no `.editorconfig` and no Fantomas configuration, so formatting
consistency is convention-borne and unenforced. A drifted format can land
silently and accrete into noisy, reviewer-time-wasting diffs.

Feature 064 landed the other four hygiene gaps (hermetic restore, restore
caching, widened warning ratchet, de-duplicated locked-restore) but deferred this
one because it is the only gap whose fix touches source: satisfying the gate
requires reformatting the existing tree once, and at split time **171 of 223
tracked F# files (77%)** needed reformatting under Fantomas v7 defaults. A tuned
`.editorconfig` should shrink that, but the diff is still large enough to deserve
its own PR where the layout-only claim can be reviewed on its own.

This feature closes the gap: a single repo-root `.editorconfig` that is
simultaneously the editor config and the Fantomas config, a one-time layout-only
reformat of the tree, and a non-required CI `format` job that runs a **pinned**
Fantomas `--check` and points a contributor at the exact command that fixes a
mis-formatted PR.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Formatting is enforced, not convention-borne (Priority: P1)

A contributor opens a PR with F# formatted differently from the house style.
Today nothing catches it; the divergence lands and accretes.

After this feature the repo carries a `.editorconfig` (the Fantomas
configuration), and a CI gate fails a PR whose tracked source is not
fantomas-clean, pointing at the one command that fixes it. Applying the gate's
formatter to the existing tree is part of this feature and MUST NOT change any
runtime behaviour or golden output.

**Why this priority**: This is the whole feature — it prevents a whole class of
noisy, reviewer-time-wasting style drift. It is P1 because without it there is no
deliverable; the reformat (US2) exists only to make this gate pass on the
existing tree.

**Independent Test**: Introduce a deliberately mis-formatted `.fs` change and
confirm the format gate fails and names the fix command; run that command and
confirm the gate passes.

**Acceptance Scenarios**:

1. **Given** a repo-root `.editorconfig` with `[*.fs]`/`[*.fsi]` Fantomas
   settings, **When** inspected, **Then** it defines the F# formatting rules for
   the tree (it is both the editor config and the Fantomas config — no separate
   `fantomas.json`).
2. **Given** a PR whose tracked source is not fantomas-clean, **When** the format
   gate runs, **Then** it fails and its output names the command that reformats
   (`fantomas <paths>`).
3. **Given** a fantomas-clean tree, **When** the format gate runs, **Then** it
   passes (exit 0).
4. **Given** the pinned Fantomas is installed for the gate, **When** the install
   runs, **Then** `.config/dotnet-tools.json` is left byte-identical to the
   managed org source (the `build-config-drift` gate still passes).

---

### User Story 2 - The existing tree is fantomas-clean, provably layout-only (Priority: P1)

The existing F# tree does not currently satisfy the new gate. As part of this
feature the whole tree is reformatted once so the gate passes, and that reformat
is proven to change layout only — no behaviour, no golden, no signature.

**Why this priority**: The gate (US1) cannot be turned on until the tree is
clean, and the reformat's safety is the load-bearing risk of the feature. It is
co-P1 with US1: neither ships without the other.

**Independent Test**: Reformat the tree, then run the full suite and confirm it
is green and every deterministic/golden baseline and every `.fsi` signature is
byte-identical; confirm `fsgg-sdd validate` stays `overallPassed`.

**Acceptance Scenarios**:

1. **Given** the one-time reformat applied to the existing tree, **When** the
   full suite runs, **Then** it is green.
2. **Given** the one-time reformat, **When** the deterministic and golden
   baselines are compared, **Then** every one is byte-identical (the reformat is
   layout-only).
3. **Given** the one-time reformat, **When** the public-surface / `.fsi`
   signatures are compared, **Then** they are unchanged.

### Edge Cases

- **Fantomas version drift**: an unpinned Fantomas could reformat differently
  across machines/CI. The gate MUST install a pinned version so its verdict is
  deterministic, and the same pinned command MUST be documented for
  contributors.
- **Generated / vendored / non-source files**: the gate MUST scope to tracked F#
  source and MUST NOT flag generated artifacts, fixtures, or golden `.json`
  baselines.
- **A deliberately non-house-style construct** (e.g. a hand-aligned table) that
  Fantomas would reflow: tuned `.editorconfig` keys minimise this, but where
  Fantomas and a deliberate style conflict, the feature accepts Fantomas' output
  (the reformat is layout-only and the style is not load-bearing).
- **The `format` job failing**: it is **non-required**, so a red `format` job
  MUST NOT block merge; it is advisory signal, not a hard gate. (The tree is kept
  clean by the reformat + contributor discipline, not by making the job
  required.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repo MUST carry a repo-root `.editorconfig` that contains a
  `[*.fs]`/`[*.fsi]` section with the Fantomas (`fsharp_*`) settings defining the
  house F# formatting style, and MUST NOT introduce any separate Fantomas
  configuration file (Fantomas 6+ reads all configuration from `.editorconfig`).
- **FR-002**: CI MUST run a `fantomas --check` over the tracked F# tree using a
  **pinned** Fantomas version.
- **FR-003**: The pinned Fantomas MUST be installed **without** editing the
  managed `.config/dotnet-tools.json`, which MUST stay byte-identical to the
  `FS-GG/.github` org source (so the `build-config-drift` gate continues to
  pass).
- **FR-004**: A tree that is not fantomas-clean MUST fail the format check, and
  the failure output MUST name the command that reformats it (`fantomas
  <paths>`).
- **FR-005**: The format job MUST be **non-required** — a red format job MUST NOT
  block merge.
- **FR-006**: The existing tree MUST be reformatted once to satisfy the gate,
  and that reformat MUST be layout-only: the full test suite MUST stay green,
  every deterministic/golden baseline MUST be byte-identical, every `.fsi`
  signature MUST be unchanged, and `fsgg-sdd validate` MUST stay `overallPassed`.
- **FR-007**: The same pinned-Fantomas install-and-run commands used by CI MUST
  be documented for contributors (so a contributor can reproduce the gate's
  verdict and the fix locally), in the repo's developer documentation.

### Key Entities

- **`.editorconfig`**: the single repo-root file that is both the editor
  configuration and the Fantomas configuration; carries general whitespace rules
  plus the `[*.fs]`/`[*.fsi]` `fsharp_*` house-style keys.
- **`format` CI job**: a non-required job in `.github/workflows/gate.yml` that
  installs pinned Fantomas out-of-manifest and runs `fantomas --check`, emitting
  the `fantomas <paths>` fix hint on failure.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A PR whose tracked source is not fantomas-clean is failed by the
  format check, and the failure output names the reformat command; a
  fantomas-clean tree passes (exit 0).
- **SC-002**: After the one-time reformat, the full test suite is green with
  **zero** golden/deterministic baseline changes and **zero** `.fsi` signature
  changes attributable to the reformat.
- **SC-003**: `.config/dotnet-tools.json` and the other managed org files remain
  byte-identical to the `FS-GG/.github` source (the `build-config-drift` gate
  passes unchanged) — the pinned Fantomas never enters the managed manifest.
- **SC-004**: The Fantomas verdict is deterministic across a contributor machine
  and CI because both install the same pinned version, and the install/run
  commands are documented.
- **SC-005**: A contributor can run one documented command to reformat and make
  the gate pass.

## Assumptions

- **Fantomas style**: the house F# style follows Fantomas defaults, tuned only by
  a small set of `fsharp_*` `.editorconfig` keys (e.g. `fsharp_max_line_length`)
  chosen to minimise churn where the existing style is deliberate. The exact key
  set is a planning/implementation detail, not a spec constraint.
- **Pinned version**: a specific Fantomas version is pinned; the exact version is
  an implementation detail settled in planning. The critical constraint is only
  that it is pinned and installed out-of-manifest.
- **Job placement**: the format job lives in `gate.yml` alongside the existing
  gate/drift/api-compatibility jobs, as its own non-required job. This reuses the
  settled 064 design (research Decision 3, contract C3).
- **Scope inheritance**: this feature is exactly US3 + FR-006..008 of feature
  064; the other 064 user stories already shipped in PR #84 and are out of scope
  here.
- **No CLI/schema surface**: this feature touches only repo tooling config, CI
  workflow, developer docs, and F# source layout — no `fsgg-sdd` command,
  automation contract, or persisted schema changes.
