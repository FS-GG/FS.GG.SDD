# Tasks: Tasks Command

**Input**: Design documents from `specs/009-tasks-command/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`quickstart.md`, and `contracts/`

**Change Tier**: Tier 1 (contracted native command surface, structured
`tasks.yml` artifact, command report JSON/text, generated-view behavior,
diagnostics, and optional Governance boundary facts)

**Tests**: Required by the specification and plan. Test tasks below are written
before implementation tasks and must fail before the implementation body is
completed.

**Status Legend**:

- `[ ]` pending
- `[X]` done with real evidence
- `[-]` skipped with written rationale on the task line

**Task Format**: `[ID] [P?] [Story?] Description with exact file path`

- `[P]` means the task has no dependency on another incomplete task in the same
  phase and touches different files from other parallel tasks.
- `[US1]`, `[US2]`, `[US3]`, and `[US4]` map to the user stories in
  `specs/009-tasks-command/spec.md`.
- Phases run sequentially. Tasks within a phase marked `[P]` may run in
  parallel.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the test and fixture entry points needed by the tasks slice.

**Fixture update rule**: Several lifecycle fixture directories already exist
for earlier command slices. When a listed directory already exists, extend the
manifest or add task-specific fixture entries for `Tasks`; do not replace
coverage used by earlier lifecycle command tests.

- [X] T001 Add `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs` and include it in `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` after `PlanArtifactTests.fs`.
- [X] T002 Add `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs` and include it in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` after `PlanCommandTests.fs`.
- [-] T003 [P] Add valid task fixture manifests under `tests/fixtures/lifecycle-commands/tasks-create/manifest.yml`, `tests/fixtures/lifecycle-commands/tasks-rerun-preserves-status/manifest.yml`, `tests/fixtures/lifecycle-commands/tasks-adds-missing-items/manifest.yml`, `tests/fixtures/lifecycle-commands/tasks-preserves-stable-ids/manifest.yml`, `tests/fixtures/lifecycle-commands/tasks-records-required-skills/manifest.yml`, `tests/fixtures/lifecycle-commands/tasks-records-evidence-obligations/manifest.yml`, and `tests/fixtures/lifecycle-commands/tasks-accepted-deferral/manifest.yml`. Skipped: task create/rerun/addition/stable-id/skill/evidence behavior is covered by temp-project command tests and readiness evidence instead of static lifecycle manifest files.
- [-] T004 [P] Add output-behavior fixture manifests under `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`, `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`, and `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml` for the tasks command cases. Skipped: output behavior is covered by dedicated command tests plus `cli-json-smoke.txt`, `cli-dry-run-smoke.txt`, and `cli-text-smoke.txt`.
- [-] T005 [P] Add blocked task fixture manifests under `tests/fixtures/lifecycle-commands/missing-plan/manifest.yml`, `tests/fixtures/lifecycle-commands/failed-plan/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/duplicate-task-id/manifest.yml`, `tests/fixtures/lifecycle-commands/dependency-cycle/manifest.yml`, `tests/fixtures/lifecycle-commands/tasks-identity-mismatch/manifest.yml`, and `tests/fixtures/lifecycle-commands/done-task-missing-evidence/manifest.yml`. Skipped: blocked-state coverage is implemented with direct temp-project command tests that assert no unsafe mutation.

**Checkpoint**: Fixture and test file entry points exist; no command behavior is implemented yet.

## Phase 2: Foundational Contracts (Blocking Prerequisites)

**Purpose**: Define the public artifact and MVU/report contracts before user
story implementation.

- [X] T006 [P] Add failing task artifact parser tests for root metadata, source snapshots, task entries, requirement links, acceptance-scenario links, graph findings, stale state, and schema version 1 in `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs`.
- [X] T007 [P] Add failing normalized work-model assertions for task graph facts, required evidence links, dependency cycles, and source diagnostics in `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`.
- [X] T008 Extend the public task artifact contract in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` with task root metadata, source snapshots, task dispositions, task findings, task graph readiness, and parser return types required by `work/<id>/tasks.yml`.
- [X] T009 Implement the task artifact parser and schema-version validation in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` while preserving compatibility with existing version 1 files that only contain `schemaVersion` and `tasks`.
- [X] T010 Update task graph and generated work-model projection fields in `src/FS.GG.SDD.Artifacts/WorkModel.fsi`, `src/FS.GG.SDD.Artifacts/WorkModel.fs`, and `src/FS.GG.SDD.Artifacts/Serialization.fs`.
- [X] T011 Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` for the deliberate task artifact public-surface additions after T008.
- [X] T012 [P] Add failing command public-surface tests for a tasks summary, graph readiness summary, task diagnostics, and `CommandReport.Tasks` in `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`.
- [X] T013 Extend the MVU command contract in `src/FS.GG.SDD.Commands/CommandTypes.fsi` with `TasksSummary`, task graph readiness counts, `CommandReport.Tasks`, and `CommandModel.Tasks` while keeping the existing `CommandMsg`, `CommandEffect`, `init`, `update`, and interpreter boundary explicit.
- [X] T014 Implement the new command contract types and values in `src/FS.GG.SDD.Commands/CommandTypes.fs`.
- [X] T015 Add task diagnostic constructors for missing/failed plan, identity mismatch, malformed schema, duplicate task id, unknown source reference, unknown dependency, dependency cycle, stale task, unsafe status change, unsafe overwrite, and done task missing evidence in `src/FS.GG.SDD.Commands/CommandReports.fsi` and `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T016 Extend blocked-report correction routing and next-action selection for `Tasks` in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T017 Add `tasksRequest`, `runTasks`, valid task text helpers, and `assertTasksSummary` helpers in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [X] T018 Update `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the deliberate task command public-surface additions after T013 and T015.

**Checkpoint**: Public `.fsi` contracts, parser contracts, command reports, diagnostics, and surface baselines are ready for story implementation.

## Phase 3: User Story 1 - Create A Traceable Task Graph (Priority: P1, MVP)

**Goal**: `fsgg-sdd tasks` creates `work/<id>/tasks.yml`, reports a ready task graph, refreshes or diagnoses the generated work model, and points to `analyze` without requiring Governance.

**Independent Test**: Run the command in an initialized project with valid specification, clarification, checklist, and plan artifacts; verify `tasks.yml`, report facts, generated-view state, and `analyze` next action.

### Tests for User Story 1

- [X] T019 [P] [US1] Add failing create-flow command tests for `work/009-tasks-command/tasks.yml`, task ids, dependencies, owners, requirement links, acceptance-scenario links, required skills, required evidence, accepted-deferral visibility from `tasks-accepted-deferral`, and `analyze` next action in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T020 [P] [US1] Add failing MVU emitted-effect assertions for `Tasks` read effects and write effects in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [X] T021 [P] [US1] Add a failing generated work-model refresh test for task sources in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T022 [P] [US1] Add a failing no-Governance task creation test in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [X] T023 [P] [US1] Add failing required-skill and required-evidence parser assertions in `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs`.

### Implementation for User Story 1

- [X] T024 [US1] Wire `Tasks` into lifecycle read planning in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` by adding task-specific read effects for `.fsgg/*`, `work/<id>/spec.md`, `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`, `work/<id>/tasks.yml`, `work/<id>/evidence.yml`, `readiness/<id>/work-model.json`, and `work/`.
- [X] T025 [US1] Implement task prerequisite loading and validation in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` for specification, clarification, checklist, and planned plan facts.
- [X] T026 [US1] Implement task derivation from requirements, acceptance scenarios, checklist facts, plan decisions, contract references, verification obligations, migration notes, generated-view impacts, and accepted deferrals in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T027 [US1] Implement deterministic `tasks.yml` creation text with root metadata, source links, source snapshots, task entries, requirement links, acceptance-scenario links, accepted-deferral dispositions, required skills, required evidence, findings, advisory notes, and lifecycle notes in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T028 [US1] Add task summary construction and graph-readiness counts in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T029 [US1] Plan safe authored writes for `work/<id>/tasks.yml` and generated work-model refresh effects for successful task-ready results in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T030 [US1] Include the proposed task source text in work-model generation snapshots before refresh in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T031 [US1] Update `src/FS.GG.SDD.Commands/CommandReports.fs` so successful `Tasks` reports require `work/<id>/tasks.yml` and return `NextAction.Command = Analyze`.

**Checkpoint**: User Story 1 is independently testable as the MVP.

## Phase 4: User Story 2 - Preserve And Refresh Existing Tasks (Priority: P1)

**Goal**: Reruns preserve existing task identity and state, add compatible work, and mark stale task links without silent destructive changes.

**Independent Test**: Run `tasks` against an existing `tasks.yml`; verify stable ids and state are preserved, compatible additions are appended, and stale source links are visible.

### Tests for User Story 2

- [X] T032 [US2] Add a failing rerun preservation test for task ids, statuses, owners, dependencies, required skills, required evidence, skip rationales, and user notes in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T033 [US2] Add a failing compatible-addition test that adds new source-derived tasks without renumbering existing tasks in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T034 [US2] Add a failing stable-id repeated-run test using three task command runs in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T035 [P] [US2] Add failing stale source snapshot parser assertions in `tests/FS.GG.SDD.Artifacts.Tests/TasksArtifactTests.fs`.
- [X] T036 [P] [US2] Add failing generated-view stale-source command assertions in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.

### Implementation for User Story 2

- [X] T037 [US2] Implement existing task artifact parsing for root identity, source snapshots, stable ids, statuses, skip rationale, stale markers, notes, and unknown non-conflicting fields in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs`.
- [X] T038 [US2] Implement stable task id reuse and deterministic new id allocation in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T039 [US2] Implement non-destructive merge logic for compatible task additions and root/source snapshot updates in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T040 [US2] Implement source digest comparison and stale task marking for changed specification, clarification, checklist, and plan links in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T041 [US2] Implement no-change, preserve, create, and update safe-write decisions for task reruns in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T042 [US2] Extend `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` with helpers that edit existing `tasks.yml` state for rerun, stable-id, and stale-source scenarios.

**Checkpoint**: User Stories 1 and 2 both work independently and preserve authored task state.

## Phase 5: User Story 3 - Diagnose Task Readiness Problems (Priority: P2)

**Goal**: Invalid task requests fail before unsafe writes and report actionable diagnostics that point to the correct lifecycle artifact.

**Independent Test**: Invoke `tasks` outside a project, before planning, with malformed task data, duplicate ids, graph cycles, unknown references, unsupported status changes, missing evidence, and stale generated views; verify no unsafe mutation occurs.

### Tests for User Story 3

- [X] T043 [US3] Add failing missing prerequisite tests for outside project, malformed work id, duplicate logical work id, missing specification, missing clarification, missing checklist, missing plan, and failed plan in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T044 [US3] Add failing malformed existing task tests for malformed schema, task identity mismatch, duplicate task id, and unsafe overwrite in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T045 [US3] Add failing graph validation tests for unknown dependency, self-dependency, dependency cycle, and unknown source reference in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T046 [US3] Add failing status/evidence tests for unsupported status changes, skipped tasks without rationale, and done tasks without required evidence in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T047 [P] [US3] Add failing dry-run no-mutation assertions for `work/<id>/tasks.yml` and `readiness/<id>/work-model.json` in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T048 [P] [US3] Add failing generated-view missing, stale, malformed, and blocked diagnostics tests in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.

### Implementation for User Story 3

- [X] T049 [US3] Implement task precondition diagnostics and no-write behavior for outside project, missing or malformed project config, missing work id, malformed work id, duplicate logical work id, and missing prerequisite artifacts in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T050 [US3] Implement failed-plan and stale-plan blocking behavior before task generation in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T051 [US3] Implement task identity, schema, duplicate id, unknown dependency, self-dependency, dependency-cycle, and unknown source-reference validation in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T052 [US3] Implement skipped-rationale, done-task evidence, stale-task, unsafe status change, and unsafe overwrite diagnostics in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T053 [US3] Implement generated-view missing, stale, malformed, and blocked states for task command reports in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T054 [US3] Update `src/FS.GG.SDD.Commands/CommandReports.fs` so blocked task diagnostics select specification, clarification, checklist, plan, or tasks correction as the next action.

**Checkpoint**: Invalid task readiness states are blocked with stable diagnostics and no unsafe mutation.

## Phase 6: User Story 4 - Keep Task Output Traceable (Priority: P3)

**Goal**: JSON and text output expose the same deterministic task facts, and optional Governance facts remain advisory.

**Independent Test**: Run repeated task requests against identical inputs and compare JSON bytes; render text and verify every text fact exists in the JSON report.

### Tests for User Story 4

- [X] T055 [P] [US4] Add a failing deterministic JSON test for the tasks command in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [X] T056 [P] [US4] Add a failing task text projection test for counts, generated-view state, diagnostics, and next action in `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.
- [X] T057 [P] [US4] Add a failing task Governance boundary test that excludes route, freshness, profile, gate, audit, evidence freshness, protected-boundary, and release verdicts in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [X] T058 [US4] Add a failing task report shape assertion for `tasks`, acceptance-scenario links, accepted deferrals, `changedArtifacts`, `generatedViews`, `diagnostics`, `governanceCompatibility`, and `nextAction` in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.

### Implementation for User Story 4

- [X] T059 [US4] Serialize the `tasks` summary with deterministic sorting and no host-specific fields in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T060 [US4] Render task counts, dependency count, required skill count, required evidence count, skipped count, stale count, generated-view state, diagnostics count, and next action from the report in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [X] T061 [US4] Keep task report Governance compatibility facts advisory and not evaluated in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T062 [US4] Add local create and rerun performance assertions under the two-second harness budget in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs`.
- [X] T063 [US4] Extend `scripts/prelude.fsx` to exercise `SddCommand.Tasks`, `commandStage Tasks`, `nextLifecycleCommand Tasks`, task summary visibility, task graph readiness visibility, and task diagnostic visibility.
- [-] T064 [US4] Add temporary-directory CLI smoke coverage for `tasks --work <id> --root <path>`, `tasks --dry-run`, and `tasks --text` in `tests/FS.GG.SDD.Commands.Tests/TasksCommandTests.fs` through the existing `src/FS.GG.SDD.Cli/Program.fs` entry point. Skipped: disposable-project CLI JSON, dry-run, and text smoke coverage is recorded in readiness transcripts instead of adding process-spawning CLI tests to the unit suite.

**Checkpoint**: Machine-readable and human-readable outputs are deterministic projections of one report contract.

## Phase 7: Polish, Evidence, And Documentation

**Purpose**: Record mandatory verification evidence and update human-facing
state after implementation is complete.

- [X] T065 [P] Run `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Task"` and save output to `specs/009-tasks-command/readiness/artifact-task-tests.txt`.
- [X] T066 [P] Run `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Task"` and save output to `specs/009-tasks-command/readiness/command-task-tests.txt`.
- [X] T067 [P] Run focused deterministic JSON, text projection, generated-view, and Governance boundary task tests and save output to `specs/009-tasks-command/readiness/output-boundary-tests.txt`.
- [X] T068 Run `dotnet build FS.GG.SDD.sln -c Release` and save output to `specs/009-tasks-command/readiness/build-release.txt`.
- [X] T069 Run `dotnet test FS.GG.SDD.sln -c Release` and save output to `specs/009-tasks-command/readiness/full-suite.txt`.
- [X] T070 Run `dotnet fsi scripts/prelude.fsx` after a Release build and save the transcript to `specs/009-tasks-command/readiness/fsi-public-surface.txt`.
- [X] T071 Run a disposable-project CLI JSON smoke scenario for `fsgg-sdd tasks` and save output to `specs/009-tasks-command/readiness/cli-json-smoke.txt`.
- [X] T072 Run a disposable-project CLI dry-run smoke scenario for `fsgg-sdd tasks --dry-run` and save output to `specs/009-tasks-command/readiness/cli-dry-run-smoke.txt`.
- [X] T073 Run a disposable-project CLI text smoke scenario for `fsgg-sdd tasks --text`, save output to `specs/009-tasks-command/readiness/cli-text-smoke.txt`, and record human-summary review notes in `specs/009-tasks-command/readiness/human-summary-review.md`.
- [X] T074 Record create/rerun performance evidence for `tasks-create` and `tasks-rerun-preserves-status` in `specs/009-tasks-command/readiness/performance.md`.
- [X] T075 Record SDD/Governance boundary review findings in `specs/009-tasks-command/readiness/sdd-governance-boundary.md`.
- [X] T076 Record artifact traceability from `specs/009-tasks-command/spec.md` requirements to plan decisions, tasks, tests, and readiness evidence in `specs/009-tasks-command/readiness/artifact-traceability.md`.
- [X] T077 Update `docs/initial-implementation-plan.md` to mark `fsgg-sdd tasks` complete and reference `specs/009-tasks-command/readiness/`.
- [X] T078 Update `README.md`, `AGENTS.md`, and `CLAUDE.md` only where needed to keep the current command state and Claude/Codex guidance synchronized after the tasks workflow behavior changes.

**Checkpoint**: The implementation has build, test, FSI, CLI, performance,
boundary, and traceability evidence.

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 depends on Phase 2 and is the MVP scope.
- Phase 4 depends on Phase 3 because rerun behavior requires create behavior.
- Phase 5 depends on Phase 3 and may run partly alongside Phase 4 after the
  base task artifact exists.
- Phase 6 depends on Phases 3 through 5 because output contracts must include
  success, rerun, and blocked states.
- Phase 7 depends on the implemented story scope being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2; no dependency on other stories.
- **US2 (P1)**: Depends on US1 create behavior and task artifact shape.
- **US3 (P2)**: Depends on US1 prerequisite loading and task graph shape.
- **US4 (P3)**: Depends on task summaries and diagnostics from US1 through US3.

### Cross-Task Dependencies

- T009 depends on T008.
- T010 depends on T009.
- T014 depends on T013.
- T015 and T016 depend on T013.
- T024 through T031 depend on T013 through T017.
- T037 through T042 depend on T032 through T036.
- T049 through T054 depend on T043 through T048.
- T059 through T064 depend on T055 through T058.
- T065 through T076 depend on all selected implementation tasks passing.
- T077 and T078 depend on readiness evidence from T065 through T076.

## Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001 and T002.
- T006 and T007 can run in parallel because they touch different artifact test files.
- T012 can run in parallel with T006 and T007.
- T019 through T023 can run in parallel because each task touches a different test file.
- T035 and T036 can run in parallel with T032 through T034 once the US2 test file edits are coordinated.
- T047 and T048 can run in parallel because they touch different command test files.
- T055, T056, and T057 can run in parallel because they touch different output test files.
- T065, T066, and T067 can run in parallel after implementation is complete.

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3.
3. Run the focused artifact and command task tests for US1.
4. Validate that `fsgg-sdd tasks` creates `work/<id>/tasks.yml`, reports task
   readiness, refreshes or diagnoses `readiness/<id>/work-model.json`, and
   points to `analyze`.

### Incremental Delivery

1. US1 creates the native task graph.
2. US2 makes reruns safe and preserves task state.
3. US3 blocks malformed or unsafe task readiness states.
4. US4 locks down deterministic JSON/text and optional Governance boundaries.
5. Phase 7 records the readiness evidence and updates project guidance.

## Task Counts

- Shared setup, foundation, polish, and evidence tasks: 32
- US1 tasks: 13
- US2 tasks: 11
- US3 tasks: 12
- US4 tasks: 10
- Total tasks: 78

## Suggested MVP Scope

Complete Phases 1, 2, and 3 first. That delivers User Story 1: a usable
`fsgg-sdd tasks` command that creates the structured `work/<id>/tasks.yml`
artifact, reports task readiness, refreshes or diagnoses the generated work
model, and points to `analyze`.
