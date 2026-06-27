---
description: "Task list for feature 032 — scaffold owns repo-init & script-executable post-instantiation steps"
---

# Tasks: Scaffold owns repo-init & script-executable post-instantiation steps

**Input**: Design documents from `/specs/032-scaffold-repo-init-chmod/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: REQUIRED. Constitution Principle I (Spec → FSI → FSI-exercise → Tests → Impl)
and Principle VI (real-evidence) are in force; the plan authors tests that fail before the
steps exist and pass after, over real `git`/filesystem fixtures through the public scaffold
surface. Test tasks are written FIRST within each story and must FAIL before implementation.

**Tier**: Tier 1 (contracted change) — implements SDD-side S1–S3 of the Accepted
`fs-gg-ui-template-generation` contract (registry `fs-gg-ui-template.behavior-break`). All
phases inherit Tier 1; no per-task `[T1]`/`[T2]` annotation needed.

**Organization**: Tasks are grouped by user story. Phases run in sequence; `[P]` tasks within
a phase touch different files and may run in parallel.

## MVU/Elmish applicability

This is a stateful, I/O-bearing feature. The `.fsi` contract (the new `SetExecutable` effect
case, `ScaffoldSummary` fields), the pure staged-transition driver, the emitted-effect
assertions (probe / `git init` / `SetExecutable` planned only on the success path), and real
interpreter evidence (a real `.git`, a real executable bit) are all explicit tasks below.

---

## Phase 1: Setup (Shared fixtures)

**Purpose**: The new test fixture both P1 stories exercise.

- [X] T001 [P] Add a `with-script` `dotnet new` fixture at
  `tests/fixtures/scaffold-provider/with-script/` — `.template.config/template.json`, an
  `App.fsproj`, and a neutral `run.sh` (a shell script with **no** provider identity in its
  name or body). Mirrors the existing `ok` fixture shape (see
  `tests/fixtures/scaffold-provider/ok/`). Drives US2 (a produced `.sh`).
- [X] T002 [P] Add `tests/fixtures/scaffold-provider/registries/with-script.providers.yml`
  pointing at the `with-script` fixture, following the existing registry fixtures under
  `tests/fixtures/scaffold-provider/registries/`. Provider name MUST be neutral (no
  `rendering`/`fs-gg-ui`) to also serve US4's non-rendering case.

---

## Phase 2: Foundational (Blocking prerequisites — shared public surface + staging)

**Purpose**: The typed contract and the post-instantiation staging machine that BOTH P1
stories build on. Declared in `.fsi` first (Principle III), then the interpreter edge and the
staged driver skeleton. No story-specific outcome logic yet.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

- [X] T003 [US1+US2] Declare the new public surface in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`: add `SetExecutable of path: string` to the
  `CommandEffect` DU (after `RunProcess`, ~`:381-389`) and add `RepoInitOutcome: string`,
  `ExecutableScriptCount: int`, `ExecutableScriptsSkipped: int` to `ScaffoldSummary`
  (~`:328-336`), per data-model.md §1–§2. Mirror in `CommandTypes.fs`. Fields are additive
  (FR-012).
- [X] T004 [US1] Declare the three advisory scaffold diagnostics in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` (~`:49-61`) —
  `scaffold.repoInitSkippedExistingRepository`, `scaffold.repoInitSkippedGitUnavailable`,
  `scaffold.scriptsNotMadeExecutable` — and implement them in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs` (~`:140-250`) as **advisory, non-fatal** facts
  (never change exit code), per data-model.md §6.
- [X] T005 [US2] Add the `SetExecutable` interpreter arm in
  `src/FS.GG.SDD.Commands/CommandEffects.fs` (`interpret`, ~`:112-161`):
  `File.SetUnixFileMode(absolute, existing ||| execute bits)` wrapped in `try`; on `dryRun`
  → success with no filesystem change (FR-008); on exception (read-only/non-Unix/missing) →
  a `Succeeded = false` result that never throws past the interpreter (FR-005, US2-AC3).
  Extend `effectPath` so `effectPath (SetExecutable p) = Some p` (data-model.md §1).
- [X] T006 [US1+US2] Split `finalizeScaffold` in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` (~`:222-290`): keep the
  outcome + produced-path diff unchanged. On a **failure** create outcome, emit the provenance
  write + terminal `ScaffoldSummary` as today (no post-steps, FR-009). On a **success**
  outcome, defer **both** the provenance write **and** the final `ScaffoldSummary` to the
  post-instantiation phase — TICK A (T007) owns the single provenance write (before `git
  init`, FR-004), TICK C sets the summary — so provenance is written **exactly once** on the
  success path (no double write). Expose the produced (app-only, non-SDD) set (~`:268-273`) as
  the source of truth for product root + script set (FR-006).
- [X] T007 [US1+US2] Implement the three-tick post-instantiation staging in
  `computeScaffoldNext` (`HandlersScaffold.fs` ~`:294-324`) per data-model.md §5 / contracts/
  post-instantiation-staging.md, recomputing phase from `InterpretedEffects` each tick (no new
  staging field): **TICK A** (after success create) plans the **single** provenance write
  (the success path's only provenance emission, per T006) +
  `RunProcess("git",["rev-parse";"--is-inside-work-tree"],"")` probe + the `SetExecutable`
  batch; **TICK B** (probe interpreted) plans `git init` or skips; **TICK C** (init
  interpreted/skipped) computes and sets the final summary exactly once (FR-010). Story phases
  fill the per-step decision/outcome logic.

**Checkpoint**: Surface declared and re-baselined-ready; staging machine routes the three
ticks. US1 and US2 can now proceed (they share TICK A/C, so coordinate or sequence them).

---

## Phase 3: User Story 1 — Scaffolded product lands in an initialized git repository (Priority: P1) 🎯 MVP

**Goal**: After a successful provider instantiation, scaffold initializes a git repository at
the product root (unless already inside a work tree or git absent), capturing the complete
scaffolded tree, and reports the outcome.

**Independent Test**: Scaffold a fixture provider into a fresh temp dir outside any work tree
with git present; assert a `.git` exists at the product root, that it spans the SDD skeleton +
product files + `scaffold-provenance.json`, and that the report's `repoInitOutcome =
initialized`.

### Tests for User Story 1 (write FIRST, ensure they FAIL) ⚠️

- [X] T008 [P] [US1] In `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`, add a test
  driving the real MVU loop: scaffold into a fresh temp dir **outside** any work tree (git
  present) → assert a real `.git` directory at the product root and `RepoInitOutcome =
  initialized`. (US1-AC1)
- [X] T009 [P] [US1] In `ScaffoldCommandTests.fs`, add a test asserting FR-004 capture: the
  initialized work tree spans the SDD skeleton, the provider product files, and
  `.fsgg/scaffold-provenance.json`. (US1-AC2)
- [X] T010 [P] [US1] In `ScaffoldCommandTests.fs`, add tests for the empty-but-successful
  outcome (`empty` fixture): repo IS initialized over skeleton + provenance,
  `RepoInitOutcome = initialized` (Edge / FR-004), and assert the probe + `git init` effects
  are planned only on the success path.

### Implementation for User Story 1

- [X] T011 [US1] In `HandlersScaffold.fs` TICK B (per T007), decode the probe
  `ProcessRunResult` by **exit code alone** (Decision 1) and plan
  `RunProcess("git",["init"],"")` iff `Started=true && ExitCode≠0` (not-in-a-tree); otherwise
  plan nothing. (FR-001)
- [X] T012 [US1] In `HandlersScaffold.fs` TICK C, derive `RepoInitOutcome` from the probe +
  init results into the closed vocabulary (`initialized` / `skippedExistingRepository` /
  `skippedGitUnavailable` / `notApplicable`) per data-model.md §3, and emit the matching
  advisory diagnostic (T004) for the skip cases. (FR-001, US1-AC1)

**Checkpoint**: US1 fully functional — a one-command scaffold lands in an initialized repo and
reports it.

---

## Phase 4: User Story 2 — Generated shell scripts are executable without manual chmod (Priority: P1)

**Goal**: After a successful provider instantiation, scaffold sets the executable bit on each
produced `.sh` script and reports how many were made executable (zero when none).

**Independent Test**: Scaffold the `with-script` fixture and assert `run.sh` carries an
executable bit and `executableScriptCount = 1`; scaffold a no-script fixture and assert
`executableScriptCount = 0`.

### Tests for User Story 2 (write FIRST, ensure they FAIL) ⚠️

- [X] T013 [P] [US2] In `ScaffoldCommandTests.fs`, add a test scaffolding the `with-script`
  fixture (T001/T002) → assert the produced `run.sh` is executable and
  `ExecutableScriptCount = 1`, `ExecutableScriptsSkipped = 0`. (US2-AC1)
- [X] T014 [P] [US2] In `ScaffoldCommandTests.fs`, add a no-op test (`empty`/`ok` no-script
  fixture) → make-executable succeeds with `ExecutableScriptCount = 0`. (US2-AC2)
- [X] T015 [P] [US2] In `ScaffoldCommandTests.fs`, add a skip/partial test that forces the
  `SetExecutable` interpreter arm to fail **deterministically on a Unix CI host** (where
  `File.SetUnixFileMode` otherwise succeeds). Forcing mechanism: drive `SetExecutable` at a
  path that cannot be chmod'd — e.g. a script whose parent directory bit/ownership the test
  makes non-writable, or a path removed between discovery and interpretation — so the arm
  catches its exception and returns `Succeeded = false` without throwing. Assert the failure is
  reported via `ExecutableScriptsSkipped ≥ 1` + advisory `scaffold.scriptsNotMadeExecutable`,
  with the scaffold success outcome and exit code unchanged. If no in-process path can force a
  deterministic failure on the CI host, narrow the test to assert the interpreter's documented
  try/skip contract directly (the `SetExecutable` arm of `interpret`, T005) over a guaranteed-
  unwritable path, and note the substitution by name (Principle VI). (US2-AC3)
  DONE via the documented narrowing: a file-owner can always chmod its own files on a Unix
  host, so no in-process scaffold path forces a deterministic per-script chmod failure. The
  test drives the real `SetExecutable` interpreter arm (T005) over a guaranteed-unwritable
  path — a real, missing file on the real filesystem — and asserts `Succeeded = false` with no
  (blocking) diagnostic and no throw. Real-evidence substitution noted by name (Principle VI).

### Implementation for User Story 2

- [X] T016 [US2] In `HandlersScaffold.fs` TICK A (per T007), discover the produced shell
  scripts by `.sh` file shape over the produced (app-only, non-SDD) set and plan one
  `SetExecutable(p)` per script — generic, no provider-specific script name (FR-006,
  Decision 4).
- [X] T017 [US2] In `HandlersScaffold.fs` TICK C, compute `ExecutableScriptCount` (interpreted
  `SetExecutable` with `Succeeded=true`) and `ExecutableScriptsSkipped` (`Succeeded=false`),
  emitting the advisory `scaffold.scriptsNotMadeExecutable` diagnostic when ≥1 skipped, per
  data-model.md §4. (FR-005, FR-010)

**Checkpoint**: US1 AND US2 both work independently — scaffolded scripts are runnable and the
counts are reported.

---

## Phase 5: User Story 3 — Safeguards keep the steps safe and non-fatal (Priority: P2)

**Goal**: Inside an existing work tree no nested repo is created; with git absent repo-init is
skipped non-fatally; a skipped convenience step never flips success to failure.

**Independent Test**: Scaffold inside an existing work tree → no nested `.git`,
`repoInitOutcome = skippedExistingRepository`; scaffold with git removed from PATH → normal
success, `repoInitOutcome = skippedGitUnavailable`.

### Tests for User Story 3 (write FIRST, ensure they FAIL) ⚠️

- [X] T018 [P] [US3] In `ScaffoldCommandTests.fs`, add an existing-work-tree test: `git init`
  the temp dir, scaffold into it (`--force`), assert NO nested repository,
  `RepoInitOutcome = skippedExistingRepository`, advisory raised, scaffold still succeeds.
  (US3-AC1, SC-002)
- [X] T018b [P] [US3] In `ScaffoldCommandTests.fs`, add a **re-run idempotence** test covering
  FR-013's chmod half: scaffold the `with-script` fixture, then scaffold again into the same dir
  (`--force`). Assert each produced `.sh` remains executable, `ExecutableScriptCount` is stable,
  `ExecutableScriptsSkipped = 0`, the second run resolves to `RepoInitOutcome =
  skippedExistingRepository` (no nesting), and neither run errors. (Edge "re-run/--force";
  FR-013)
- [X] T019 [P] [US3] In `ScaffoldCommandTests.fs`, add a git-absent test that deterministically
  forces the `git rev-parse` probe to report `Started = false`. Forcing mechanism: run the
  scaffold with a child-process environment whose `PATH` is scrubbed of any `git` (an empty/
  sentinel `PATH` for the spawned probe), so `Process.Start("git", …)` fails to launch →
  `Started = false`. Confirm the `RunProcess` edge actually applies the test-supplied PATH/env
  to the child (if it does not today, that env-injection seam is a prerequisite — file it
  before writing the test rather than relying on mutating the suite's own `PATH`). Assert
  `RepoInitOutcome = skippedGitUnavailable`, advisory raised, normal success outcome and exit
  code preserved. (US3-AC2, SC-004)
- [X] T020 [P] [US3] In `ScaffoldCommandTests.fs`, add a no-false-incomplete assertion: a
  successful scaffold with a skipped convenience step (existing repo / git absent / no
  scripts) is NOT reported failed or incomplete. (US3-AC3, FR-010)

### Implementation for User Story 3

- [X] T021 [US3] Confirm/complete the `skippedExistingRepository` (probe `ExitCode=0`) and
  `skippedGitUnavailable` (`Started=false`) branches in TICK B/C (T011/T012) so neither plans
  `git init`, both set the correct outcome + advisory, and neither alters the exit code
  (FR-002/003). Wire FR-013 idempotence: a re-run / `--force` into an already-scaffolded dir
  resolves to `skippedExistingRepository` and re-applies executable bits safely.

**Checkpoint**: All three repo-init outcomes and the safe-failure invariants hold.

---

## Phase 6: User Story 4 — Steps are generic and leak no provider specifics (Priority: P2)

**Goal**: The post-instantiation behavior works for any provider and encodes no provider-,
template-, or rendering-specific identifier.

**Independent Test**: Scaffold a non-rendering fixture → identical repo-init + exec behavior;
the leak scan over the changed scaffold source finds no provider-specific token.

### Tests for User Story 4 (write FIRST, ensure they FAIL/extend) ⚠️

- [X] T022 [P] [US4] In `ScaffoldCommandTests.fs`, add a non-rendering-provider test (the
  neutral `with-script` provider, T002): assert identical repo-init + make-executable
  behavior, driven only by the scaffolded tree. (US4-AC1)
- [X] T023 [US4] In `tests/FS.GG.SDD.Commands.Tests/ScaffoldGuardTests.fs`, re-assert SC-005
  via the **C1** provider-identifier scan — the repo-wide `sourceFiles()` over `src/**/*.fs(i)`
  (~`:32-39`), which already covers every file this feature touches, including
  `CommandEffects.fs` and `Diagnostics.fs` (no curated-list edit needed for SC-005). Separately
  confirm the new handler/projection code falls inside the narrower curated
  `scaffoldSourceFiles()` union (~`:54-61`) that feeds the **C2** lifecycle-value scan. Assert
  no package id / template id / path / script name / docs URL leaked in either scope. (US4-AC2)

### Implementation for User Story 4

- [X] T024 [US4] Verify FR-007: scaffold passes NO provider-specific git options
  (`initGit`/`allow-scripts`) to the provider — the `dotnet new` create-arg vector is
  unchanged and the steps are performed by SDD itself. Add/extend an assertion to lock this.
  (US4-AC3)

---

## Phase 7: Report projections & polish (Cross-cutting)

**Purpose**: Surface the new facts in all three projections, regenerate baselines, align agent
surfaces, and validate determinism + quickstart.

- [X] T025 [US1+US2] Add the new fields to the JSON projection in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` `writeScaffold` (~`:291-311`) —
  `repoInitOutcome`, `executableScriptCount`, `executableScriptsSkipped` — keeping it the
  deterministic automation contract. (FR-011)
- [X] T026 [US1+US2] Add the repo-init + exec lines to the text projection in
  `src/FS.GG.SDD.Commands/CommandRendering.fs` (~`:196-209`); `--rich` inherits via
  `renderText` with no JSON byte change. (FR-011)
- [X] T027 [P] [US1+US2] In `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs`, add
  three-projection fact parity: `--json`/`--text`/`--rich` carry the same repo-init outcome +
  exec counts; `--json` is byte-identical to default; `--rich` adds/drops no fact. (FR-011,
  SC-006)
- [X] T028 [P] [US1+US2] Add a determinism test in `ScaffoldCommandTests.fs`: two identical
  runs into clean temp dirs in the same environment yield byte-identical
  `scaffold-provenance.json` AND report JSON; assert provenance bytes are unchanged from
  schema v1 (sensed repo-init field is additive only). (FR-012)
- [X] T029 [P] [US1] Add a dry-run test (`--dry-run`) in `ScaffoldCommandTests.fs`: no `.git`,
  no chmod; `RepoInitOutcome = notApplicable`, counts `0`; the hint describes the planned
  steps. (FR-008)
- [X] T030 [P] [US1] Add provider-failure tests (`fails-midway`, `writes-into-fsgg` fixtures)
  in `ScaffoldCommandTests.fs`: post-instantiation steps do NOT run, `RepoInitOutcome =
  notApplicable`, the existing failure diagnostic + exit code are preserved. (FR-009)
- [X] T031 Regenerate the public-surface baselines and confirm the surface tests re-assert
  them. NOTE: the `SetExecutable` DU case and the additive `ScaffoldSummary` fields are not
  module static methods, so the Commands/Cli `PublicSurface.baseline` snapshots are unchanged
  (their surface tests still pass); only `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`
  gained the three new advisory diagnostics, and its surface test re-asserts them.
- [X] T032 [P] Add the one-line "scaffold now owns repo-init + script executability
  post-instantiation" behavior note to all four agent surfaces — `CLAUDE.md`, `AGENTS.md`,
  and the two SKILL files — keeping Claude and Codex aligned (Principle VII). No workflow-shape
  change.
- [X] T033 Run the full suite (`dotnet test FS.GG.SDD.sln`) with `WarningsAsErrors` at 0 (no
  `#nowarn`), then execute `quickstart.md` end-to-end (US1–US4 + edge/determinism/parity
  scenarios) and confirm every expectation.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies — start immediately.
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all user stories. T003 → T004/T005
  (independent, `[P]`-able across files) → T006 → T007.
- **US1 (Phase 3)** and **US2 (Phase 4)**: both depend on Phase 2. They share TICK A (probe +
  SetExecutable) and TICK C (final summary) in `HandlersScaffold.fs`, so their implementation
  tasks (T011/T012 and T016/T017) touch the same file — sequence them rather than parallelize.
- **US3 (Phase 5)**: depends on US1 (extends the repo-init outcome branches).
- **US4 (Phase 6)**: depends on US1 + US2 behavior existing.
- **Polish (Phase 7)**: T025/T026 depend on US1+US2; T031 depends on the surface from Phase 2;
  T033 is last.

### Within each story

- Test tasks (the `### Tests` block) are written FIRST and MUST FAIL before the story's
  implementation tasks.
- `[P]` = different file, no dependency on another incomplete task in this phase.

### Parallel opportunities

- T001 ∥ T002 (Setup, different files).
- Within Phase 2, T004 ∥ T005 (Diagnostics vs CommandEffects — different files) once T003 lands.
- All `### Tests` tasks within a story are `[P]` (all add cases to `ScaffoldCommandTests.fs` —
  parallel-safe as separate authoring units, but watch for merge conflicts in one file).
- Across stories: US1 and US2 implementation conflict in `HandlersScaffold.fs` — NOT parallel.
- In Phase 7, T027/T028/T029/T030/T032 are `[P]` (different files); T025/T026 are sequential
  story-completion edits; T031/T033 gate the end.

---

## Implementation strategy

### MVP (US1 + US2 — both P1)

1. Phase 1 (fixtures) → Phase 2 (surface + staging) → Phase 3 (US1 repo-init) → Phase 4
   (US2 exec).
2. **STOP and VALIDATE**: a one-command scaffold lands in an initialized repo with executable
   scripts and reports both — the headline contract parity (Scenario H / SC-003).

### Incremental delivery

3. Add US3 (safeguards) → validate the skip outcomes are non-fatal.
4. Add US4 (generic / no-leak) → validate the boundary invariant.
5. Phase 7 (projections, baselines, agent surfaces, determinism, quickstart) closes the
   feature.

---

## Notes

- `[P]` = different files, no dependency on another incomplete task in this phase.
- `[Story]` label maps each task to its user story (or `US1+US2` for shared infrastructure /
  projections).
- Never mark a failing task `[X]`; never weaken an assertion to green a build (narrow scope and
  document instead).
- Every assertion runs over real `git`/filesystem fixtures through the public scaffold surface
  (constitution VI) — no mocks of internal stages.
- The provider contract, invocation protocol, and `scaffold-provenance.json` schema (v1) stay
  unchanged; `init` stays byte-identical (post-steps are scaffold-only).
