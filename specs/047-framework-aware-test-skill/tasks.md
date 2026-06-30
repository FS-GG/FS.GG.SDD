---
description: "Task list for Framework-aware required test skill"
---

# Tasks: Framework-aware required test skill

**Input**: Design documents from `/specs/047-framework-aware-test-skill/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md,
contracts/project-config-test-framework.md,
contracts/verification-obligation-skill.md

**Tests**: Included — the spec's User Stories define per-story Independent Tests
and FR-006 mandates updating golden fixtures. Tests are written failing-first.

**Change tier**: Tier 1 (contracted) — adds a config schema field, changes
generated work-model/tasks content, updates golden fixtures.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies in-phase)
- **[Story]**: US1 / US2 / US3 (omitted for shared phases)
- Phases run in sequence; tasks within a phase marked `[P]` may run in parallel

## Path Conventions

Single project — `src/` and `tests/` at repository root (per plan.md
Structure Decision).

---

## Phase 1: Setup

**Purpose**: Confirm the seam and baseline before changing anything.

- [X] T001 Confirm the defect seam: `obligationTasks` in
  `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingTasks.fs:244` emits the literal
  `[ "xunit"; "readiness-evidence" ]`, and the stray parser-input `xunit`
  fixtures at `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs:47` and
  `tests/FS.GG.SDD.Artifacts.Tests/VerificationViewTests.fs:66`. Run
  `dotnet build` to establish a green baseline.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The SDD-owned config signal + the pure resolver. Blocks all user
stories — every story reads the declared framework through this substrate.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

**Elmish/MVU note (Principle IV/V)**: No new I/O edge — the `.fsgg/project.yml`
read effect already exists in `tasksReadEffects`; `resolveTestSkill` is a pure,
total transition over the already-loaded snapshot. New public surface is limited
to the additive `TestFramework: string option` field (Principle I/III).

- [X] T002 [P] Add a failing-first parser test asserting
  `parseProjectConfig` populates `TestFramework` from `project.testFramework`
  (present → `Some "expecto"`; absent → `None`; blank/whitespace → `None`) in
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaContractTests.fs`.
  Must fail to compile/assert before T003/T004.
- [X] T003 Add `TestFramework: string option` to the `ProjectLifecycleConfig`
  record in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fsi`
  (after `GovernanceToolingPath`, line 21) — additive, `schemaVersion` stays `1`.
- [X] T004 Read the optional `project.testFramework` scalar in
  `parseProjectConfig` in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` via the existing
  `Internal.fs` optional-scalar helper (e.g. `tryScalarAt [ "project"; "testFramework" ]`);
  no new diagnostic codes. Makes T002 pass.
- [X] T005 [P] Add a failing-first resolver test for `resolveTestSkill` /
  `neutralTestSkill` covering the data-model table (`expecto`→`expecto`,
  `Expecto`→`expecto`, `NUnit`→`nunit`, `My Custom Runner`→`my-custom-runner`,
  `None`→`automated-tests`, `""`/`"   "`→`automated-tests`, and `xunit`→`xunit`
  only when explicitly declared) in
  `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`. Must fail before T006.
- [X] T006 Add `neutralTestSkill = "automated-tests"` and the pure total
  `resolveTestSkill : string option -> string` (trim → **invariant-culture**
  lowercase → collapse internal whitespace runs to `-`; blank/`None` → neutral;
  no closed framework allow-list) inside the internal `ParsingTasks` module in
  `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingTasks.fs`. Use
  `String.ToLowerInvariant` (not culture-sensitive `ToLower`) so byte-stability
  holds across locales (FR-006). Makes T005 pass.

**Checkpoint**: Config field parsed and resolver proven in isolation; the
generated seam is not yet rewired (still emits `xunit`).

---

## Phase 3: User Story 1 - Author of a non-xUnit product gets the right test skill (Priority: P1) 🎯 MVP

**Goal**: A product declaring `testFramework: expecto` gets a verification-
obligation task whose required test skill is `expecto`, never `xunit`, and the
`evidence.missingRequiredSkill` obligation re-keys to it.

**Independent Test**: Generate tasks for an Expecto-declaring product and assert
the verification-obligation task's `requiredSkills` contains the Expecto-matched
skill and no `xunit` token.

### Tests for User Story 1 (write first, ensure they FAIL)

- [X] T007 [P] [US1] Add a failing-first generation test: with a fixture product
  declaring `project.testFramework: expecto`, task generation produces a
  verification-obligation task with `requiredSkills = ["expecto", "readiness-evidence"]`
  and **zero** `xunit` tokens anywhere in the generated task metadata, in
  `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`. (SC-001/SC-003/SC-006)
- [X] T008 [P] [US1] Add a failing-first verify-re-keying test: on an
  Expecto-declaring product with no supporting evidence, `evidence.missingRequiredSkill`
  lists the `expecto` skill (not `xunit`); supplying evidence covering the
  verification-obligation task clears it, in
  `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`. (FR-008)

### Implementation for User Story 1

- [X] T009 [US1] Thread the declared framework into the task seam: add a
  declared-framework parameter to `plannedTasks`
  (`src/FS.GG.SDD.Commands/CommandWorkflow/ParsingTasks.fs:143`) and update
  **both** internal call sites inside `tasksDiagnosticsTextAndSummary` — the
  first-pass call at `ParsingTasks.fs:564` (`existingFacts = None`) and the
  additions call at `ParsingTasks.fs:613` (`Some existingFacts`) — to pass it.
  Extract `TestFramework` from the parsed project-config snapshot in
  `computeTasksPlan` (`src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEarly.fs:185`)
  and supply it through `tasksDiagnosticsTextAndSummary` (`:546`, which already
  receives `model`). No new I/O edge; no call-site signatures change beyond the
  added parameter on these two `plannedTasks` calls.
- [X] T010 [US1] Replace the literal `[ "xunit"; "readiness-evidence" ]` in
  `obligationTasks` (`src/FS.GG.SDD.Commands/CommandWorkflow/ParsingTasks.fs:244`)
  with `[ resolveTestSkill declared; "readiness-evidence" ]` (existing
  `plannedTask` `List.distinct |> List.sort` is retained). Makes T007/T008 pass.
- [-] T011 [P] [US1] Add/adjust an on-disk fixture project declaring
  `testFramework: expecto` under `tests/fixtures/**` and update its
  `work-model.json` golden so the verification-obligation task carries
  `["expecto", "readiness-evidence"]`. (FR-006)
  **Skipped**: this repo commits no on-disk `work-model.json` goldens — work
  models are generated into temp dirs at test time. The FR-006 obligation is met
  by T007, which declares `testFramework: expecto`, writes evidence to force the
  readiness `work-model.json` projection, and asserts it carries `expecto` with
  zero `xunit` tokens. No committed golden file exists to edit.

**Checkpoint**: The #42 defect is fixed for declared Expecto — MVP complete and
independently testable.

---

## Phase 4: User Story 2 - Undeclared framework yields a neutral, non-misleading skill (Priority: P2)

**Goal**: A product with no declared framework gets the neutral
`automated-tests` test skill, never a framework-specific token.

**Independent Test**: Generate tasks for a product with no declared framework
and assert the verification-obligation task's required test skill is
`automated-tests` with no framework-specific token (`xunit`, `expecto`, …).

**Dependency**: Behavior is delivered by the shared implementation in
T006/T010; this phase adds its proving test and fixture.

### Tests for User Story 2 (write first, ensure they FAIL — fails until T010)

- [X] T012 [P] [US2] Add a generation test: with a fixture product that has no
  `project.testFramework` (or a blank value), the verification-obligation task's
  `requiredSkills` is `["automated-tests", "readiness-evidence"]` and no
  framework-specific token appears in the generated task metadata, in
  `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`. (SC-002)

### Implementation / fixtures for User Story 2

- [-] T013 [P] [US2] Update the default (no-framework) fixture's
  `work-model.json` golden under `tests/fixtures/**` so its verification-
  obligation task carries `["automated-tests", "readiness-evidence"]`. (FR-006)
  **Skipped**: no committed `work-model.json` golden exists (see T011). The
  no-framework projection is proven by T012, which forces the readiness
  `work-model.json` and asserts `["automated-tests", "readiness-evidence"]` with
  no framework-specific (`xunit`/`expecto`) token.

**Checkpoint**: Both declared and undeclared paths produce correct,
non-misleading skills.

---

## Phase 5: User Story 3 - No regression to non-test task skills or determinism (Priority: P3)

**Goal**: Only the verification-obligation test skill changes; all other task
categories' skills are untouched and re-runs are byte-identical.

**Independent Test**: Diff the generated work model before/after for the same
inputs — the only difference is the verification-obligation test skill; all other
categories' `requiredSkills` are identical; a second run is byte-identical.

### Tests for User Story 3 (write first)

- [X] T014 [P] [US3] Add a non-test-category invariance test asserting the
  `requiredSkills` of requirement/plan-decision (`["fsharp","speckit-implement"]`),
  contract (`["fsharp"]`), migration (`["schema-versioning"]`), generated-view
  (`["deterministic-json"]`), and deferral (`["traceability"]`) tasks are
  unchanged, in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`. (SC-004)
- [X] T015 [P] [US3] Add a determinism test asserting two task-generation runs on
  identical inputs produce byte-identical task metadata, in
  `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`. (SC-005)

**Checkpoint**: Surgical change proven; no collateral skill or determinism
regression.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T016 [P] Replace the stray parser-input `xunit` fixtures so no `xunit`
  token remains except where explicitly declared:
  `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs:47`
  (`requiredSkills: [xunit]`) and
  `tests/FS.GG.SDD.Artifacts.Tests/VerificationViewTests.fs:66`
  (`"skill": "xunit"`) → an SDD-neutral token (`automated-tests`). (FR-004) Also
  assert the generated task metadata contains no provider-/rendering-/template-
  specific token (FR-007 — negative check).
- [X] T017 [P] Re-check `PublicSurface.baseline` — only the additive
  `TestFramework: string option` field is added; no other new public symbols.
  (Principle III)
- [X] T018 Cross-projection check (FR-009): confirm the resolved skill is
  observable identically under `--json` (default), `--text`, and `--rich`, with
  JSON bytes unchanged beyond the skill value, by running the quickstart
  projections. (`specs/047-framework-aware-test-skill/quickstart.md`)
- [X] T019 Run the full `dotnet test` suite and the quickstart Scenarios 1–5;
  confirm zero `xunit` tokens in generated task metadata for the Expecto and
  no-declaration fixtures (SC-001/SC-002/SC-006).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories.
- **US1 (Phase 3)**: Depends on Foundational. Delivers the core seam change
  (T009/T010) the other stories rely on.
- **US2 (Phase 4)**: Depends on US1's T010 (shared implementation).
- **US3 (Phase 5)**: Depends on US1's T009/T010.
- **Polish (Phase 6)**: Depends on all desired stories.

### Within Each Story

- Tests are written first and must FAIL before the implementation task.
- T009 (threading) precedes T010 (seam rewrite).

### Parallel Opportunities

- T002 and T005 (independent test files) can run in parallel.
- T003/T004 (Config) are independent of T005/T006 (resolver) and can proceed in
  parallel once their tests exist.
- T007/T008 (US1 tests) in parallel; T011 fixture in parallel with US1 tests.
- T012/T013 (US2) and T014/T015 (US3) test/fixture tasks are all `[P]`.
- T016/T017 (Polish) are `[P]`.

---

## Summary

- **Total tasks**: 19
- **Per user story**: US1 = 5 (T007–T011), US2 = 2 (T012–T013), US3 = 2
  (T014–T015); shared Setup/Foundational/Polish = 10.
- **Parallel opportunities**: T002‖T005, T003/T004‖T005/T006, T007‖T008‖T011,
  T012‖T013, T014‖T015, T016‖T017.
- **Suggested MVP**: Phase 1 + Phase 2 + Phase 3 (User Story 1) — fixes the
  reported #42 defect for the declared-Expecto product end to end.

## Implementation notes

- The Phase-2 resolver test (T005) exercises the internal `resolveTestSkill` /
  `neutralTestSkill` directly, as the plan requires ("proven in isolation").
  This needed `<InternalsVisibleTo Include="FS.GG.SDD.Commands.Tests" />` in
  `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`. This grants the test
  assembly access to existing internals only — it adds no public symbol, so the
  `PublicSurface.baseline` (T017) is unchanged.
- T009 derives the declared framework inside `tasksDiagnosticsTextAndSummary`
  (which already receives `model`) from the existing `.fsgg/project.yml` read
  effect, keeping `tasksDiagnosticsTextAndSummary`'s signature unchanged (only
  `plannedTasks` gained the parameter), and adds no new I/O edge.
- Verification (T018/T019): full `dotnet test` green (Commands 346, Artifacts 125,
  Cli 56, Validation 18, Contracts 40; 3 acceptance tests network-skipped). Real
  `fsgg-sdd` CLI runs confirmed Scenario 1 (`expecto`→`expecto`), Scenario 2
  (none→`automated-tests`), Scenario 3 (`My Custom Runner`→`my-custom-runner`),
  and that `verify.json` re-keys the obligation to `expecto` with zero `xunit`
  tokens and is byte-identical across `--json`/`--text`/`--rich` (the latter
  degrading to zero ANSI under `NO_COLOR`/`TERM=dumb`).
