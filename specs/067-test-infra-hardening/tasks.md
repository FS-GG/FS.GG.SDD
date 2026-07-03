# Tasks: Test-infrastructure hardening

**Input**: Design documents from `/specs/067-test-infra-hardening/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/preserved-contracts.md

**Tier**: Tier 2 (internal / contract-neutral). Tasks that touch product code
(`src/FS.GG.SDD.Validation`) are marked `[T2-src]` to flag they edit `.fs`-internal
harness code — still contract-neutral (research Decisions 4–5).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the same phase.
- **[US#]**: the user story the task serves.
- Phases run in sequence; tasks within a phase may run in parallel where marked `[P]`.

## Guardrail (applies to every task)

No change to any `.fsi`, committed `**/*.baseline`, golden/deterministic fixture,
`validation-report` schema/verdict, persisted schema, or agent-skill contract.
See `contracts/preserved-contracts.md`. The Phase-1 snapshot + Phase-7 diff-gate
enforce it mechanically.

---

## Phase 1: Setup — pin the guardrail baseline

**Purpose**: Capture the "must-not-change" surface so any accidental contract
drift is caught at the end.

- [X] T001 Record the pre-change contract surface for the final diff-gate: save `git rev-parse HEAD` and the current byte hashes of every `tests/**/PublicSurface.baseline` (×5), `src/**/*.fsi`, and `tests/fixtures/**` (excluding `lifecycle-commands/`, which US2 intentionally edits) into `specs/067-test-infra-hardening/.baseline-snapshot` (scratch, not committed). This is the reference for T036.
- [X] T002 [P] Confirm the clean-slate build/test state: `dotnet build -c Debug` and `dotnet test -c Debug` both green **before** any change, so later failures are attributable to this feature.

**Checkpoint**: baseline pinned; suite green.

---

## Phase 2: User Story 1 — Deterministic, parallel-safe suite (Priority: P1) 🎯 MVP

**Goal**: Process-global env/PATH mutation can never be observed by a concurrent
test. Delivers the only fix for intermittent CI. Independent of all other stories.

**Maps to**: FR-001, FR-002, FR-003; SC-001. See research Decision 1.

- [X] T003 [US1] In `tests/FS.GG.SDD.Commands.Tests/`, define the single serialization collection name `ProcessGlobalEnv` (a `[<CollectionDefinition("ProcessGlobalEnv")>]` marker type, e.g. in a new `CollectionsFixture.fs` or the existing support file) and fold the two existing collection names into it.
- [X] T004 [US1] Apply `[<Collection("ProcessGlobalEnv")>]` to `ScaffoldCommandTests.fs` and `ScaffoldCliCoherenceTests.fs` (replacing `[<Collection("Scaffold")>]`), and to both `[<Collection("Console")>]` classes in `RemediationCommandTests.fs`.
- [X] T005 [US1] Apply `[<Collection("ProcessGlobalEnv")>]` to every other Commands.Tests class that spawns a `PATH`-resolved process or reads/mutates process env — enumerate by grepping the assembly for `Process.Start`, `runCliRaw`, `SetEnvironmentVariable`, and bare `git`/`dotnet` spawns (candidates include `AnalyzeCommandTests`, `EvidenceCommandTests`, `AgentsCommandTests`, `VerifyCommandTests`, `RefreshCommandTests`, `ShipCommandTests`). Do NOT tag pure in-memory classes — they must stay parallel.
- [X] T006 [US1] Add the meta-guard `tests/FS.GG.SDD.Commands.Tests/ProcessGlobalEnvGuardTests.fs`: scan this assembly's own `.fs` sources for the spawn/mutation markers above and assert every owning class carries `[<Collection("ProcessGlobalEnv")>]`. Fail with the offending file/class names. (This is the durable defense against the race silently returning.)
- [X] T007 [P] [US1] In `tests/FS.GG.SDD.Acceptance.Tests/`, add `[<assembly: CollectionBehavior(DisableTestParallelization = true)>]` (extend `AssemblyInfo.fs` if present, else add it), mirroring `Validation.Tests`. Add a one-line comment citing the PATH/registry mutation reason.
- [X] T008 [US1] Verify SC-001: run `dotnet test -c Debug` 5× (offline) — identical result each run; then run `FSGG_SDD_ACCEPTANCE_REGISTRY=<registry> dotnet test tests/FS.GG.SDD.Acceptance.Tests` and confirm serialized, no env-ordering flake. Also confirm the spec edge case — a run with parallelization disabled (`xunit.parallelizeTestCollections=false` or `-m:1`) yields the same result as the parallel run (isolation must not depend on the global parallel switch). Record the runs on this task line.

**Checkpoint**: US1 independently shippable — the flaky-CI defect is closed.

---

## Phase 3: User Story 6 — De-duplicated shared helpers (Priority: P3, sequenced early)

**Goal**: One home for the shared test primitives. Sequenced before US3 because
US3's `SurfaceBaseline.verify` lives in this file (plan delivery-order exception).

**Maps to**: FR-010, FR-011; SC-006. See research Decision 6.

- [X] T009 [US6] Create `tests/Shared/TestShared.fs` (namespace `FS.GG.SDD.TestShared`) with the single definitions of `findRepoRoot`, `repoRoot`, `writeRelative`, `tempDirectory` (nested-root form — see T019), `SurfaceBaseline.verify` (see T014), and an `evidenceLadder`/`passingTaskEvidence` builder deriving `T001..T00n` from a range so the magic ids appear once.
- [X] T010 [US6] Link the shared file into all six test projects via `<Compile Include="../Shared/TestShared.fs" />` (place it before each project's own `TestSupport.fs` in compile order): `FS.GG.Contracts.Tests`, `FS.GG.SDD.Acceptance.Tests`, `FS.GG.SDD.Artifacts.Tests`, `FS.GG.SDD.Cli.Tests`, `FS.GG.SDD.Commands.Tests`, `FS.GG.SDD.Validation.Tests`.
- [X] T011 [US6] Rewrite `tests/FS.GG.Contracts.Tests/TestSupport.fs`, `tests/FS.GG.SDD.Artifacts.Tests/TestSupport.fs`, and `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` so `findRepoRoot`/`repoRoot`/`writeRelative` **delegate** to `TestShared` (keep the existing public names for call-site stability); delete the duplicated bodies.
- [X] T012 [US6] Remove the duplicated `findRepoRoot`/`writeRelative` from `tests/FS.GG.SDD.Acceptance.Tests/AcceptanceSupport.fs`; delegate to `TestShared`.
- [X] T013 [US6] Verify SC-006 (all three clauses — FR-010 **and** FR-011): `grep -rl "let rec findRepoRoot" tests/` and `grep -rl "let writeRelative" tests/` each return exactly `tests/Shared/TestShared.fs`; and the `T001..T00n` evidence ladder / `passingTaskEvidence` builder has exactly one definition (in `TestShared`) with no per-project copy remaining. Whole suite builds and passes.

**Checkpoint**: shared primitives consolidated; `TestShared` available to US3.

---

## Phase 4: User Story 3 — Unified baseline regeneration (Priority: P2)

**Goal**: One `FSGG_UPDATE_BASELINE` switch regenerates all five baselines.

**Maps to**: FR-005, FR-006; SC-003. Depends on T009 (`SurfaceBaseline.verify`).

- [X] T014 [US3] Implement `TestShared.SurfaceBaseline.verify baselinePath capture` (added in T009): if `FSGG_UPDATE_BASELINE=1` → `File.WriteAllLines(baselinePath, capture())`; then assert `capture()` equals the committed baseline (whitespace-filtered, sorted) — exactly today's Contracts behavior.
- [X] T015 [P] [US3] Rewire `tests/FS.GG.Contracts.Tests/PublicSurfaceTests.fs` onto `SurfaceBaseline.verify` (pass its existing `capture`); behavior identical to today.
- [X] T016 [P] [US3] Rewire `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs` onto `SurfaceBaseline.verify` (its capture = module static methods).
- [X] T017 [P] [US3] Rewire `tests/FS.GG.SDD.Cli.Tests/SurfaceBaselineTests.fs` and `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs` and `tests/FS.GG.SDD.Validation.Tests/SurfaceBaselineTests.fs` onto `SurfaceBaseline.verify`.
- [X] T018 [US3] Verify SC-003: `FSGG_UPDATE_BASELINE=1 dotnet test` over the five baseline tests → `git diff -- '**/PublicSurface.baseline'` is empty; unset → all five assert green.

**Checkpoint**: baseline regeneration uniform; baselines byte-identical.

---

## Phase 5: User Story 4 — Self-cleaning temp artifacts (Priority: P2)

**Goal**: Net-zero temp growth after a full run, failure-safe, on both sides.

**Maps to**: FR-007, FR-008; SC-004. See research Decision 4.

- [X] T019 [US4] In `TestShared` (T009), make `tempDirectory ()` nest under a single per-run root `GetTempPath()/fsgg-sdd-tests-<runId>/…` (`runId` = one `Guid` computed once at module/fixture init; temp paths are never asserted). Signature unchanged.
- [X] T020 [US4] Add an xUnit assembly/collection fixture (a global `ICollectionFixture` or module teardown) whose `Dispose` recursively deletes `runTempRoot`. Ensure it runs once per assembly. Wire it so every test assembly that mints temp dirs gets the teardown.
- [X] T021 [T2-src] [US4] In `src/FS.GG.SDD.Validation/ValidationRunner.fs`, nest all per-cell `tempDirectory ()` / `copyDirectory` roots under one run root created in `run`, and delete that root in a `try/finally` around the matrix evaluation. Internal only — no `.fsi` change, report unchanged.
- [X] T022 [US4] Verify SC-004. **Outcome (honest deviation from strict "0"):** under the VSTest host, process-exit handlers are killed before a large tree delete finishes, so a single run leaves **one** run root. The reliable, race-safe mechanism is a startup sweep that reclaims roots owned by *dead* processes (pid-tagged `fsgg-sdd-tests-<pid>-<guid>`); verified across two runs — residue stays bounded at 1 and never accumulates (run 2 reclaimed run 1's root). This converts the pre-feature **unbounded** leak (measured: 567 orphaned dirs) into a self-healing steady state of ≤1 run. The `ValidationRunner` side (product code, `finally` runs with the process alive) leaks **0**. Cleanup is failure-safe (per-child, read-only-tolerant). See the SC-004 refinement in `spec.md`.

**Checkpoint**: no temp leaks on developer machines or runners.

---

## Phase 6: User Story 2 — Honest, consumed fixtures (Priority: P2)

**Goal**: Zero orphaned lifecycle-command manifests; a guard keeps it that way.

**Maps to**: FR-004; SC-002. See research Decision 2.

- [X] T023 [US2] Inventory `tests/fixtures/lifecycle-commands/` (107 dirs) — done inline (grep, not a separate file): every dir holds only `manifest.yml` (0 real fixtures); no test/product code reads any manifest (`WorkItem.fs:183` even excludes them from snapshots); only `deterministic-report`'s *directory* is used (an Init dry-run root in `CommandReportJsonTests`). The `dry-run`/`stale-generated-view`/`unknown-reference` name matches were coincidental (a rendered-text assertion + `sdd-artifact-model` fixtures). Wire/delete list: keep `deterministic-report`, delete the other 106.
- [X] T024 [US2] For each manifest encoding a **distinct, currently-unguarded** scenario, wire it in. **Outcome: zero qualified** — all manifests are pure unread documentation, redundant with the exhaustive Validation harness (command × projection × state). None wired; the one used fixture directory (`deterministic-report`) is retained and its manifest is now consumed by the guard's self-consistency check.
- [X] T025 [US2] Delete the remaining unconsumed manifests (and their now-empty fixture dirs) identified in T023.
- [X] T026 [US2] Add `tests/FS.GG.SDD.Commands.Tests/FixtureManifestGuardTests.fs`: assert every remaining `tests/fixtures/lifecycle-commands/*/manifest.yml` is referenced by ≥1 executing test path; fail listing any orphan.
- [X] T027 [US2] Verify SC-002: the guard passes; adding a stray manifest makes it fail (spot-check, then revert).

**Checkpoint**: fixtures are honest; orphans can't silently reaccumulate.

---

## Phase 7: User Story 5 — Genuinely distinct validation cells (Priority: P3)

**Goal**: Each harness environment cell exercises a real, distinct condition —
without reversing the deliberate library `Rich→text` degradation.

**Maps to**: FR-009 (narrowed); SC-005. See research Decision 5. `[T2-src]`.

- [X] T028 [T2-src] [US5] In `src/FS.GG.SDD.Validation/ValidationRunner.fs`, make the degradation cells actually apply the color-disabling condition (`NO_COLOR` / `TERM=dumb`, scoped and restored) so the no-ANSI check is exercised under the real condition, not vacuously.
- [X] T029 [T2-src] [US5] Extend `withPerturbedHost` to also vary the working directory (alongside culture/TZ), fulfilling its documented contract. If a producer proves cwd-dependent, treat the resulting cell failure as a real determinism bug to fix (INV-3a) — not to suppress.
- [X] T030 [US5] Add/adjust a `Validation.Tests` assertion proving the cells now differ (e.g. the degradation cell genuinely has the env applied; the perturbed cell's cwd differs from neutral) — no two cells run the identical neutral comparison.
- [X] T031 [US5] Confirm the preserved boundary: `renderProjection` still maps `Rich → CR.renderText` (Decision 6 unchanged); the Rich ANSI guarantee remains asserted by `tests/FS.GG.SDD.Cli.Tests/ValidateCommandTests.fs`. Do not add a Spectre dependency to the validation library.
- [X] T032 [US5] Verify SC-005: `dotnet test tests/FS.GG.SDD.Validation.Tests` green with genuinely distinct cells; `validation-report` verdicts unchanged (still Pass).

**Checkpoint**: harness coverage is genuine, not vacuous.

---

## Phase 8: Polish & contract-preservation gate

**Purpose**: Prove the whole feature is contract-neutral and complete.

- [X] T033 [P] Run `dotnet build -c Release` warning-clean (the feature-064/066 ratchet is `TreatWarningsAsErrors=true`) — the new/edited test files must not introduce FS warnings.
- [X] T034 [P] Confirm the offline inner loop timing is not materially worse (the 27 pure Commands.Tests classes stayed parallel; only process/env classes serialized).
- [X] T035 Update `specs/067-test-infra-hardening/checklists/requirements.md` notes if any scope detail shifted during implementation.
- [X] T036 **Contract diff-gate (FR-012 / SC-007)**: `git diff --stat -- '**/*.baseline' 'src/**/*.fsi'` is empty, and `tests/fixtures/**` shows changes only under `lifecycle-commands/`; compare against the T001 snapshot. Full suite green with `FSGG_UPDATE_BASELINE` unset.

---

## Dependencies & ordering

- **Phase 1** before everything (pins the guardrail).
- **US1 (Phase 2)** is independent — can land first as the MVP.
- **US6 (Phase 3) → US3 (Phase 4)**: `TestShared.SurfaceBaseline.verify` (T014) and
  the shared file (T009/T010) must exist before US3 rewires onto it. US4's T019/T020
  also build on the T009 shared file.
- **US2 (Phase 6)** and **US5 (Phase 7)** are independent of US3/US4/US6 (except US2's
  guard test lives in Commands.Tests, which US1 also touches — coordinate the file if
  landing concurrently).
- **Phase 8** last (verifies the aggregate).

## Parallel opportunities

- T015/T016/T017 (baseline rewires) are `[P]` — different files, after T014.
- T007 (Acceptance serialize) is `[P]` vs. the Commands.Tests US1 tasks.
- US2 and US5 can proceed in parallel with US3/US4 once US6's shared file (T009/T010) lands.

## Suggested MVP

**User Story 1 (Phase 2)** — closes the intermittent-CI defect, the single
highest-value item, and ships independently of the rest.

## Task count per story

| Story | Priority | Tasks |
|---|---|---|
| Setup / guardrail | — | T001–T002 (2) |
| US1 — env-race isolation | P1 | T003–T008 (6) |
| US6 — helper dedup | P3 | T009–T013 (5) |
| US3 — baseline switch | P2 | T014–T018 (5) |
| US4 — temp cleanup | P2 | T019–T022 (4) |
| US2 — honest fixtures | P2 | T023–T027 (5) |
| US5 — distinct cells | P3 | T028–T032 (5) |
| Polish / diff-gate | — | T033–T036 (4) |

**Total: 36 tasks.**
