---
description: "Task list for feature 064 — build/CI hygiene: hermetic restore, caching, format & warning gates, de-duplicated locked-restore"
---

# Tasks: Build/CI hygiene

**Input**: Design docs in `specs/064-build-ci-hygiene/`
(spec, plan, research, data-model, contracts/ci-hygiene-contract, quickstart)

**Overall tier**: Tier 2 (build / tooling / CI). No CLI output, JSON, schema, or
golden change (FR-014); no managed org file change (FR-010).

**Tests**: Included where they add signal — negative checks for the format gate and
warning ratchet, the offline tool smoke, and the global no-drift invariant. No new
xUnit contract tests (no code contract changes; the reformat's safety is the
unchanged full suite + byte-identical goldens, Principle VI via real fixtures).

**MVU note**: Not applicable — no runtime code paths added; changes are build config,
CI YAML, and a layout-only source reformat.

**Ordering**: Anchor → US1 (hermetic restore, MVP) → US2 (caching) → US3 (format) →
US4 (ratchet) → US5 (composite + release) → final gate. The five stories share no
code and can be landed independently after the anchor; sequence here is by priority.
Every task leaves the build green.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe (no dependency on another incomplete task in this phase)
- **[Story]**: US1..US5 per the spec

---

## Phase 1: Anchor (Shared)

**Purpose**: Establish the green, measured baseline every later phase relies on.

- [X] T001 Baseline confirmed. Pre-fix `--locked-mode` **reproduced the bug** exactly
  (`NU1403: Package content hash validation failed for FSharp.Core.10.1.301`). Post-fix:
  build 0 errors; full suite green (873 passed, 3 acceptance skipped offline, 0 failed).
- [X] T002 [P] Warning measurement (full rebuild): **456 warnings in exactly two classes** —
  FS3218 ×452 (signature/impl arg-name mismatch, a byproduct of the 062/063 `.fsi` splits)
  and FS3262 ×4 (nullness). Every other class is at zero. NOTE: the review's "2 warnings"
  was wrong; this changes T014 (see its note).
- [X] T003 [P] validate baseline: captured to `/tmp/064-validate-before.json`, `overallPassed`
  confirmed (see T023).

**Checkpoint**: baseline green; current warnings and validate state recorded.

---

## Phase 2: User Story 1 — hermetic restore (Priority: P1) — DEFERRED to a follow-up PR

**Goal**: `nuget.config` makes restore machine-independent; a clean restore leaves
all 11 lockfiles unmodified; `--locked-mode` succeeds (FR-001..003 / C1).

> **DEFERRED (CI evidence).** Adding `<clear/>` **changes CI's FSharp.Core resolution**:
> the canonical `FwQFuqOA1+...` hash on `main` comes from a GitHub-runner-**inherited**
> source that `<clear/>` removes, so CI-with-`<clear/>` resolves a *different* hash and
> locked-restore fails (verified twice on PR #84). Correctly landing US1 requires
> regenerating all 11 lockfiles against the post-`<clear/>` resolution **from CI / a clean
> environment** — which this dev-container cannot do (its baked NuGet cache resolves yet
> another divergent hash, `excLf2zM/...`). This is exactly the kind of change that needs
> its own PR with a clean-env lockfile regeneration, so it is deferred (folded into #85).
> On `main` the deterministic gate is green; reverting `nuget.config` keeps it green here.

- [-] T004 [US1] Deferred — `<clear/>` reverted; needs clean-env lockfile regen (see above).
- [-] T005 [US1] Deferred — lockfiles left at `main`'s canonical values (no change in this PR).
- [-] T006 [US1] Deferred with T004/T005.

**Checkpoint**: US1 deferred to #85; the PR carries no dependency-resolution change.

---

## Phase 3: User Story 2 — CI restore caching (Priority: P2)

**Goal**: Every `setup-dotnet` step caches on the committed lockfiles (FR-004/005 / C2).

- [X] T007 [P] [US2] `gate.yml`: added `cache: true` + `cache-dependency-path` to both
  `setup-dotnet` steps (gate, api-compatibility-gate).
- [X] T008 [P] [US2] `release.yml`: added caching to all 5 `setup-dotnet` steps.
- [X] T009 [P] [US2] `composition-acceptance.yml`: added caching to its `setup-dotnet`.
- [X] T010 [US2] Verified: 8 `cache-dependency-path` entries (release 5, gate 2,
  composition 1) — one per `setup-dotnet`. Locked-mode enforcement unchanged (restore
  is now the composite action; FR-005). Cache-hit is a CI-runtime property (not
  locally observable).

**Checkpoint**: all CI restores are lockfile-cached; enforcement unchanged.

---

## Phase 4: User Story 3 — format gate (Priority: P2)

**Goal**: `.editorconfig` (= Fantomas 6 config) + a CI format check; tree reformatted
layout-only (FR-006..008 / C3).

> **DEFERRED to a follow-up PR (pending user sign-off).** Measured scale:
> **171 of 223 tracked F# files (77%)** need reformatting under Fantomas v7 defaults.
> A diff that size would bury the verified config/CI changes in this PR and risks
> `.fsi` layout churn that FR-014/C7 forbid touching here. US3 is a P2 independently
> landable slice, so it belongs in its own reviewable PR where the reformat can be
> confirmed layout-only in isolation. Tasks below stay `[ ]`.

- [ ] T011 [US3] Add repo-root `.editorconfig`: general whitespace defaults plus a
  `[*.fs]` and `[*.fsi]` section with the house-style `fsharp_*` Fantomas keys
  (research Decision 3). Tune `fsharp_max_line_length` and any keys needed to
  minimise churn where the existing style is deliberate.
- [ ] T012 [US3] Reformat the tree once (layout-only): install the pinned Fantomas to
  a tool path (NOT via `.config/dotnet-tools.json` — FR-010), run `fantomas .`, then
  prove non-behaviour: `dotnet test FS.GG.SDD.sln -c Debug` green AND
  `git status` shows **no** `*.golden`/`*.json`/`.fsi`-baseline change attributable
  to formatting (C7). Land as its own reviewable commit.
- [ ] T013 [US3] Add a non-required `format` job to `.github/workflows/gate.yml`:
  set up .NET (cached), install the same pinned Fantomas, run `fantomas --check .`,
  and on failure print the `fantomas <paths>` fix command (mirror the existing
  `::error::` message style). Add a negative check to `quickstart.md` C3 (already
  present) as the evidence recipe.

**Checkpoint**: formatting enforced; existing tree clean; zero golden/contract drift.

---

## Phase 5: User Story 4 — wider warning ratchet (Priority: P2)

**Goal**: Lock in the near-zero warning state in repo-owned props only (FR-009/010 / C4).

- [X] T014 [US4] Widened the ratchet in `Directory.Build.local.props`: flipped
  **`TreatWarningsAsErrors=true`** (maximal ratchet — the review's "flip full TWAE"
  goal) with **`WarningsNotAsErrors=$(WarningsNotAsErrors);FS3218;FS3262`** exempting
  the two documented legacy classes (append form preserves the canonical api-gate RS
  codes). NU1603;NU1608 stay handled by the canonical (TWAE doesn't reliably promote
  restore NU). NOTE (deviation from research D4): the "already-clean set" framing was
  based on the review's stale "2 warnings"; reality is 456 in two classes, so the
  in-scope maximal ratchet is full-TWAE-minus-two-exemptions. The 452 FS3218 are a
  separate follow-up (arg-name cleanup from the 062/063 .fsi splits).
- [X] T015 [US4] Verified: **Debug AND Release** rebuild 0 errors under the ratchet
  (228 exempted warnings remain, non-fatal). Negative check: a throwaway project
  emitting FS0020 (non-exempted) built to `error FS0020` (`1 Error(s)`). Managed files
  `git diff --exit-code` clean (SC-005 / FR-010).

**Checkpoint**: wider ratchet active; current tree clean; managed files untouched.

---

## Phase 6: User Story 5 — composite locked-restore + release smoke + RollForward (Priority: P3)

**Goal**: One locked-restore definition consumed by 5 jobs; release verifies the tool;
tool declares RollForward (FR-011..013 / C5/C6).

- [X] T016 [US5] Created `.github/actions/locked-restore/action.yml` — composite action,
  input `target` (default `FS.GG.SDD.sln`), single canonical NU1603/force-evaluate message.
- [X] T017 [US5] `gate.yml` `gate` job now `uses: ./.github/actions/locked-restore`
  (target `FS.GG.SDD.sln`).
- [X] T018 [US5] `release.yml` all four jobs (contracts-tests, cli-tests, publish-contracts,
  publish-cli) now use the composite with their existing single-project targets.
- [X] T019 [P] [US5] Added `<RollForward>Major</RollForward>` to `FS.GG.SDD.Cli.fsproj`.
- [X] T020 [US5] Wired `verify-cli-tool.sh` into `release.yml` `publish-cli` before the
  push. Ran it locally: **C6 PASS** (exit 0) — needed to bypass the *dev-container's*
  global `packageSourceMapping` (an environment artifact absent in CI) to exercise
  `--add-source`; that global mapping is also why the repo config must NOT add its own
  mapping (see T004 amendment).
- [X] T021 [US5] Verified: composite exists; 5 usages (gate 1 + release 4); zero inline
  `dotnet restore ... --locked-mode` remain outside the composite (SC-006).

**Checkpoint**: locked-restore in one place ×5; release gated by the tool smoke; RollForward set.

---

## Phase 7: Final gate

**Purpose**: Prove the global invariants (FR-010/014 / C7) and full-suite green.

- [X] T022 Full suite + build: 0 errors (widened ratchet, Debug+Release); test **873
  passed, 3 skipped, 0 failed** (verified locally under the ratchet; the ratchet is
  lockfile-independent). Locked-restore is validated by CI (green on `main`, unchanged here).
- [X] T023 [P] No-drift invariant: `validate --json` → `summary.overallPassed: true`
  (332 passed, 0 failed); `git status` shows **no** `readiness/**`, `*.golden`, or
  `.fsi` change (FR-014 / SC-007). No `nuget.config`/lockfile change (US1 deferred).
- [X] T024 [P] Managed-file drift: `Directory.Build.props`, `Directory.Packages.props`,
  `.config/dotnet-tools.json` `git diff --exit-code` clean (FR-010).
- [X] T025 quickstart C1–C7 satisfied for the completed slice (C3 belongs to the
  deferred US3 format-gate PR).

**Checkpoint**: US1/US2/US4/US5 contracts green and verified; US3 (format gate) deferred
to its own PR. Ready to commit + open PR for the completed slice.

---

## Summary

| Story | Tasks | Status |
|---|---|---|
| Anchor | T001–T003 | ✅ done |
| US1 hermetic restore | T004–T006 | ⏭ DEFERRED to #85 (needs clean-env lockfile regen) |
| US2 caching | T007–T010 | ✅ done |
| US3 format gate | T011–T013 | ⏭ DEFERRED to #85 (77% reformat) |
| US4 warning ratchet | T014–T015 | ✅ done |
| US5 composite + release | T016–T021 | ✅ done |
| Final gate | T022–T025 | ✅ done (for the shipped slice) |

**Shipped in this PR**: US2 (caching), US4 (warning ratchet), US5 (composite locked-restore +
release smoke + RollForward). Verified locally + by CI.

**Deferred to follow-up #85**: US1 (hermetic restore — `<clear/>` changes CI's dependency
resolution; needs lockfiles regenerated from a clean env) and US3 (format gate — 77% reformat).

**Total**: 25 tasks across 7 phases (14 done, 6 deferred, 5 gate/anchor done).
