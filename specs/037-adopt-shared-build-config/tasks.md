---
description: "Task list for Adopt Shared Build Config"
---

# Tasks: Adopt Shared Build Config

**Input**: Design documents from `/specs/037-adopt-shared-build-config/`

**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓,
contracts/adoption-contract.md ✓, quickstart.md ✓

**Implementation note (necessary deviation, disclosed)**: The "no src/tests F#
change" expectation held for `src/` (zero changes) but **two existing test
helpers had to follow the relocated `<Version>`**: `ReleaseContractTests.fs` and
`ReleaseInstallTests.fs` each read the single `<Version>` source of truth and were
pointed from `Directory.Build.props` → `Directory.Build.local.props` (FR-002 moved
the property there; the canonical file now carries no `<Version>`). These are
faithful path updates — the assertions ("identity version == single Version source
== generator version") are unchanged, not weakened. Without them the suite is red
(reads `""` vs `0.2.0`). This is the only F# change.

**Baseline note**: `ScaffoldCommandTests."…identical for a neutral provider"` is a
**pre-existing flaky test** (a `git rev-parse` probe race under the full concurrent
suite): it fails intermittently in the whole-solution run but passes 50/50 in
isolation, both before and after adoption. Not caused by this feature.

**Tests**: No new unit/integration tests. This is a build-infrastructure refactor
with **zero behavioral change** (FR-006/SC-005) and no F# source change, so
constitution Principle I/VI are N/A by construction (plan Constitution Check) —
the spec's Assumptions explicitly exempt it from evidence/readiness obligations
beyond a normal green build/test. The *verification* is the build itself plus the
drift check: (1) clean offline restore + build + full xUnit suite green, (2)
effective evaluated MSBuild values identical to the pre-adoption baseline, (3)
drift check exits 0 synced / non-zero on a tampered canonical file. These are
captured as explicit verification tasks, not as new test code.

**Tier**: Whole feature is **Tier 2** (internal build-infra; no public API,
schema, generated-view, command, artifact-layout, or agent-skill change —
FR-008). Every phase matches that tier, so per-task `[T2]` tags are omitted
(skill: omit when it matches).

**Scope**: Repo-root build files only — three canonical managed files
(`Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`),
two repo-owned override files (`Directory.Build.local.props`,
`Directory.Packages.local.props`), the per-project `**/packages.lock.json`, and
one CI workflow (`.github/workflows/gate.yml`). **No** `src/`/`tests/` F# change,
no `fsgg-sdd` lifecycle artifact, CLI, or SDD-owned schema (FR-008). No
rendering/template/Governance package id, template, path, or docs URL.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (parallel-safe)
- **[Story]**: `[US1]`/`[US2]`/`[US3]`/`[US4]` — traceability to the spec's user stories

## Elmish/MVU applicability

**N/A.** No stateful or I/O-bearing SDD code is added. The only "I/O tool" is the
external upstream `sync-build-config.sh`, which SDD consumes but does not author
(plan Constitution Check V). No `.fsi`, no MVU loop, no transition tests apply.

## Conventions used below

- `<gh>` = a local checkout (or fetch) of `FS-GG/.github`; the sync tool is
  `<gh>/scripts/sync-build-config.sh`, the source of truth is `<gh>/dist/dotnet/`.
- "canonical" = `Directory.Build.props`, `Directory.Packages.props`,
  `.config/dotnet-tools.json` (org-owned, byte-identical to `<gh>/dist/dotnet/`,
  never edited locally). "local override" = `Directory.Build.local.props`,
  `Directory.Packages.local.props` (repo-owned, hold all SDD specifics).
- The authoritative move/drop/append inventory is **data-model.md** (the SC-002
  checklist); the CLI/import/exit-code contract is **contracts/adoption-contract.md**.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the pre-adoption baseline to diff against (SC-005) and the
upstream tooling the adoption consumes. No build files change yet.

- [X] T001 Capture the pre-adoption baseline (SC-005 / FR-006 reference state):
  from the repo root on branch `037-adopt-shared-build-config`, record the
  effective evaluated MSBuild values for every project and the baseline lockfiles
  per quickstart Step 0 —
  `for p in src/*/ tests/*/; do dotnet build "$p" -getProperty:TargetFramework,Version,LangVersion,Deterministic,ContinuousIntegrationBuild,Nullable,TreatWarningsAsErrors,WarningsAsErrors; done > /tmp/sdd-build-baseline.txt`
  then `dotnet restore FS.GG.SDD.sln --force-evaluate`. Also confirm the
  pre-change offline build+test is green (`dotnet build FS.GG.SDD.sln -c Debug`
  then `dotnet test FS.GG.SDD.sln --no-build`) so any post-adoption warning/error
  is attributable to this change. Keep a reference copy of the current
  `**/packages.lock.json`.
- [X] T002 [P] Ensure the upstream contract is available: a checkout/fetch of
  `FS-GG/.github` at `<gh>` exposing `<gh>/scripts/sync-build-config.sh` (with
  `--adopt`/plain/`--check`) and `<gh>/dist/dotnet/` containing the three
  canonical files (`Directory.Build.props`, `Directory.Packages.props`,
  `.config/dotnet-tools.json`). Confirm `<gh>/dist/dotnet/Directory.Build.props`
  carries the `Source of truth: FS-GG/.github` marker (the tool's
  canonical-vs-hand-authored discriminator — adoption-contract §1).

**Checkpoint**: Pre-adoption baseline recorded; sync tool + canonical source in hand.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Perform the structural move that every story builds on — relocate the
hand-authored props into `*.local.props` and copy the three canonical files in.
⚠️ Blocks US1–US4.

- [X] T003 Run the adoption move from the repo root:
  `<gh>/scripts/sync-build-config.sh --adopt .` (adoption-contract §1). This
  moves the marker-less hand-authored `Directory.Build.props` →
  `Directory.Build.local.props` and `Directory.Packages.props` →
  `Directory.Packages.local.props`, then copies the three canonical files in.
  Confirm the result: the two `*.local.props` now exist with the former contents,
  the canonical `Directory.Build.props`/`Directory.Packages.props` each end with
  an `<Import Project="…local.props" Condition="Exists(…)" />` as the **last**
  element (the override seam, adoption-contract §2), and `.config/dotnet-tools.json`
  was created. Do **not** hand-edit the canonical files (curation happens only in
  `*.local.props`, T007/T008/T010).

**Checkpoint**: The canonical → local import seam exists; the five files are in
place (local.props still verbatim-from-original, curation pending).

---

## Phase 3: User Story 1 - Repo tracks the org build baseline without forking (Priority: P1) 🎯 MVP

**Goal**: The repo's three canonical managed files are byte-identical to
`<gh>/dist/dotnet/` with no local edits, the override seam imports the repo's
`*.local.props` last, and a clean offline restore + build + full test suite is
green with no behavioral change (FR-001, FR-009, SC-001, SC-004).

**Independent Test**: Canonical files compare byte-identical to `<gh>/dist/dotnet/`
(drift check exits 0, T006); a clean offline `restore + build + test` passes
(T005). The byte-identity check (T004) is verifiable immediately after T003,
before any local.props curation.

- [X] T004 [US1] Verify the three canonical managed files are byte-identical to
  the org source of truth (FR-001 / SC-001): `diff Directory.Build.props
  <gh>/dist/dotnet/Directory.Build.props`, the same for `Directory.Packages.props`
  and `.config/dotnet-tools.json` — each must report no difference. Confirm none
  carries a local edit and each retains the `Source of truth: FS-GG/.github`
  marker. `.config/dotnet-tools.json` is the unused `fake-cli` manifest adopted
  verbatim so `--check` is green (research Decision 1) — it is **not** referenced
  by any SDD build logic.
- [X] T005 [US1] Clean offline build + full test suite green (FR-009 / SC-004),
  per quickstart Step 2 — **depends on the local.props curation in T007, T008,
  and T010 (US2/US3); run this after they land.** With `GITHUB_ACTIONS` unset:
  `dotnet restore FS.GG.SDD.sln` → `dotnet build FS.GG.SDD.sln -c Debug
  --no-restore` → `dotnet test FS.GG.SDD.sln --no-build`. Confirm green with **no
  new warnings or errors** versus the T001 baseline and no `NU1504`/`NU1011`
  duplicate-pin error (SC-003 cross-check).
- [X] T006 [US1] Confirm the drift check is green on the freshly synced files
  (SC-001): `<gh>/scripts/sync-build-config.sh --check .` → `ok:` ×3, exit 0
  (adoption-contract §3). This proves canonical byte-identity via the same gate CI
  will use (the red-path proof is T013, US4).

**Checkpoint**: MVP — canonical baseline tracked byte-for-byte and the offline
build/test is green. The repo no longer forks the org build baseline.

---

## Phase 4: User Story 2 - Repo-specific build settings survive a sync (Priority: P1)

**Goal**: Every SDD-specific MSBuild property and non-baseline package version
lives in the repo-owned `*.local.props` (which a sync never overwrites), with
owned-by-canonical properties dropped and the additive warning property appended —
so the effective evaluated values are unchanged (FR-002, FR-003, FR-006, SC-002,
SC-005). These tasks edit the files generated by T003.

**Independent Test**: Each property/version in the data-model.md inventory is
present in the correct `*.local.props`; none of the dropped properties remain;
re-capturing effective MSBuild values diffs clean against the T001 baseline (T009).

- [X] T007 [US2] Curate `Directory.Build.local.props` per the data-model.md
  property inventory (SC-002 checklist). **Keep/move** (MOVE rows): `Version`
  (`0.2.0`, single source of truth), `TargetFramework` (`net10.0`), `LangVersion`
  (`preview`), `ContinuousIntegrationBuild` (`true`), `Nullable` (`enable`),
  `TreatWarningsAsErrors` (`false`), and all package metadata (`Company`,
  `Authors`, `Product`, `RepositoryUrl`, `PackageLicenseExpression`,
  `PackageRequireLicenseAcceptance`). **Append, not assign** the F# warning
  promotions: `<WarningsAsErrors>$(WarningsAsErrors);FS3261;FS0025</WarningsAsErrors>`
  (research Decision 2 — a plain assignment would drop the canonical
  `;NU1603;NU1608`). **Drop** (now owned by canonical, research Decision 4):
  `Deterministic`, `ManagePackageVersionsCentrally`, `RestorePackagesWithLockFile`,
  the gated `RestoreLockedMode`, and the `;NU1603;NU1608` promotion line. Do not
  edit the canonical `Directory.Build.props`.
- [X] T008 [US2] Curate `Directory.Packages.local.props` per the data-model.md
  package inventory: it declares **only** the `<ItemGroup>` of the six non-baseline
  `PackageVersion` items — `YamlDotNet 16.3.0`, `System.Text.Json 10.0.0`,
  `Spectre.Console 0.57.0`, `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.5`,
  `Microsoft.NET.Test.Sdk 17.14.1`. It MUST NOT re-set
  `ManagePackageVersionsCentrally` (canonical owns it) and MUST NOT declare
  `FSharp.Core` (handled in T010, US3). Do not edit the canonical
  `Directory.Packages.props`.
- [X] T009 [US2] Verify effective-value equivalence (FR-006 / SC-005), per
  quickstart Step 3 — re-capture the same property set across all projects and
  `diff` against `/tmp/sdd-build-baseline.txt` from T001; it MUST be identical
  (zero diff). Confirm `WarningsAsErrors` evaluates to the union of the canonical
  (`NU1603`,`NU1608`) and local (`FS3261`,`FS0025`) promotions — the append, not a
  replace.

**Checkpoint**: All SDD specifics live in `*.local.props`; effective values match
the pre-adoption baseline exactly. A future sync can replace canonical files
without losing repo settings.

---

## Phase 5: User Story 3 - FSharp.Core stays in lockstep with the org (Priority: P2)

**Goal**: The repo declares no local `FSharp.Core` pin, so it resolves to the org
baseline (`10.1.301`) with no CPM duplicate error (FR-004, SC-003).

**Independent Test**: No `FSharp.Core` `PackageVersion` in any local file; restore
resolves `FSharp.Core` to `10.1.301` with no `NU1504`/`NU1011` (T011).

- [X] T010 [US3] Confirm `Directory.Packages.local.props` declares **no**
  `FSharp.Core` `PackageVersion` (data-model.md DROP row; adoption-contract §4 — a
  baseline package re-declared locally raises CPM `NU1504`/`NU1011`). The org
  baseline pins `FSharp.Core 10.1.301` in the canonical `Directory.Packages.props`;
  SDD's former local pin already equalled it (spec Assumption), so removing it is a
  no-op on the resolved graph. If T008 already omitted it, this is a guard check.
- [X] T011 [US3] Verify resolution (SC-003 / FR-004), per quickstart Step 4:
  `grep -R "FSharp.Core" Directory.Packages.local.props` returns nothing, and
  `dotnet list FS.GG.SDD.sln package | grep -i FSharp.Core` shows `10.1.301` (from
  the org baseline) with no `NU1504`/`NU1011` duplicate-pin error at restore.

**Checkpoint**: `FSharp.Core` is owned solely by the org baseline; no local pin can
silently diverge.

---

## Phase 6: User Story 4 - Drift from the org baseline fails fast (Priority: P2)

**Goal**: The drift check is wired into per-PR CI and is demonstrably live —
green on synced files, non-zero on a tampered canonical file (FR-007, SC-006).

**Independent Test**: `--check` exits 0 on clean files (already shown in T006);
after a deliberate edit to a canonical file it exits non-zero and names the
drifted file (T013); the CI job runs the same check (T012).

- [X] T012 [US4] Wire the drift check into per-PR CI by adding a job to
  `.github/workflows/gate.yml` (plan Phase 0 Decision 5; adoption-contract §3) —
  no org reusable workflow exists yet (`.github#18` unbuilt), so the job checks
  out `FS-GG/.github` into a side path (`.ci-build-config`, tracking `main` — a
  pinned SHA would itself become silent drift; revisit only if upstream churn
  causes flakiness, research Decision 5) and runs
  `"$GITHUB_WORKSPACE/.ci-build-config/scripts/sync-build-config.sh" --check "$GITHUB_WORKSPACE"`,
  failing the PR on a non-zero exit. Keep it additive: do not change the existing locked-restore
  `gate` job condition (FR-005 — `RestoreLockedMode` stays gated on
  `GITHUB_ACTIONS` + lockfile-exists, now owned by the canonical
  `Directory.Build.props`).
- [X] T013 [US4] Prove the gate is live (SC-006), per quickstart Step 5: with
  synced files `--check .` exits 0; then `printf '\n<!-- tamper -->\n' >>
  Directory.Build.props`, re-run `--check .` and confirm it reports
  `DRIFT (differs): Directory.Build.props` and exits non-zero; then
  `git checkout -- Directory.Build.props` to revert. Record the green→red→green
  demonstration as the FR-007/SC-006 evidence.

**Checkpoint**: All four stories independently green; "don't fork" is an enforced
CI gate, not just convention.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Lockfile currency, locked-mode CI parity, and final verification.

- [X] T014 Refresh and commit lockfiles only if the relocation changed them
  (research Decision 3 — `CentralPackageTransitivePinningEnabled=true` is newly set
  by canonical), per quickstart Step 3: `dotnet restore FS.GG.SDD.sln
  --force-evaluate` then `git diff -- '**/packages.lock.json'`. Accept only
  graph-equivalent churn and commit the updated `**/packages.lock.json` if changed;
  if unchanged, leave the committed lockfiles as-is. The resolved package graph MUST
  match the T001 baseline (FR-006).
- [X] T015 Verify locked-mode CI parity (FR-005 / FR-009), per quickstart Step 6:
  `GITHUB_ACTIONS=true dotnet restore FS.GG.SDD.sln --locked-mode` succeeds against
  the committed lockfiles, and any graph drift fails exactly as before adoption.
  Confirm the fresh-clone bootstrap path is preserved (locked mode requires
  `GITHUB_ACTIONS` AND an existing `packages.lock.json` — spec Edge Cases).
- [X] T016 Run the full quickstart.md (Steps 1–6) end-to-end and confirm the
  "Done when" criteria: three canonical files byte-identical to `<gh>/dist/dotnet/`,
  Steps 2–6 green, effective values + resolved graph unchanged, drift check proven
  live. Record this as the build-infrastructure evidence (plan Constitution Check
  VI scoped: green build + full test suite + demonstrated drift pass/fail; no new
  unit test, no readiness artifact — spec Assumptions). Confirm FR-008: no
  lifecycle artifact/CLI/schema change and no rendering/template/Governance token
  entered the build config.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** → **Phase 2 (Foundational)** are sequential and block all stories.
- **US1–US4** all depend on Phase 2 (the `--adopt` move in T003).
- **Phase 7 (Polish)** depends on the adoption being functionally complete (after US3).

### Cross-story coupling (this refactor is more coupled than a typical feature)

All five files are edited by one atomic adoption, so the stories share files and
do **not** parallelize cleanly:

- **US2 (T007/T008)** and **US3 (T010)** curate the same two `*.local.props` files
  that T003 generated. They are the substantive edits.
- **US1's green-build verification (T005) depends on US2 + US3** (T007, T008,
  T010): an uncurated local.props (redundant `ManagePackageVersionsCentrally`,
  retained `FSharp.Core` pin) would warn or raise `NU1504`/`NU1011`. US1's
  *byte-identity* checks (T004, T006) are independent and verifiable right after
  T003.
- **US4 (T012/T013)** depends only on the canonical files existing (T003/T004);
  it is independent of the local.props curation.

### Within each story

- T004/T006 (byte-identity) before T005 (build green, which also needs T007/T008/T010).
- T007 and T008 edit different files → parallel-safe with each other; both precede T009 (verify).
- T010 before T011 (verify).
- T012 (CI wiring) and T013 (live proof) before relying on the gate; T013 can run locally without T012.

### Parallel opportunities

- **T002** is `[P]` in Setup (independent of the baseline capture in T001).
- **T007** and **T008** edit different files and could run concurrently if staffed,
  but both must precede T009.
- Most other tasks serialize on the same five files or on a verification that needs
  prior edits — treat this feature as largely sequential.

## Task counts per user story

- **US1 (P1, MVP)**: 3 tasks (T004–T006) — byte-identity + seam + offline green build.
- **US2 (P1)**: 3 tasks (T007–T009) — curate both local.props + effective-value parity.
- **US3 (P2)**: 2 tasks (T010–T011) — drop the `FSharp.Core` pin + verify resolution.
- **US4 (P2)**: 2 tasks (T012–T013) — wire CI drift job + prove green→red.
- **Setup/Foundational/Polish (cross-cutting)**: 6 tasks (T001–T003, T014–T016).
- **Total**: 16 tasks.

## Suggested MVP scope

For a normal feature the MVP is US1 alone, but this is an **atomic build-infra
refactor**: a green build (US1's T005) is only reachable once the `*.local.props`
are curated (US2 T007/T008) and the `FSharp.Core` pin is dropped (US3 T010).
So the **shippable MVP increment is Setup + Foundational + US1 + US2 + US3**
(T001–T011) — the repo tracks the org baseline byte-for-byte, all SDD specifics
survive in `*.local.props`, `FSharp.Core` resolves upstream, and the offline
build + full test suite is green with zero behavioral change. **US4** (T012–T013)
then makes "don't fork" an enforced CI gate, and Polish (T014–T016) settles
lockfile currency, locked-mode parity, and final quickstart verification.
