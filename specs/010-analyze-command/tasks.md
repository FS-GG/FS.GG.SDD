# Tasks: Analyze Command

**Input**: Design documents from `specs/010-analyze-command/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`quickstart.md`, and `contracts/`

**Change Tier**: Tier 1 (contracted native command surface, generated
`analysis.json` view, cross-artifact consistency diagnostics, lifecycle
readiness state, command report JSON/text, generated-view behavior, and
optional Governance boundary facts)

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
  `specs/010-analyze-command/spec.md`.
- Phases run sequentially. Tasks within a phase marked `[P]` may run in
  parallel.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the test and fixture entry points needed by the analyze slice.

**Fixture update rule**: Several lifecycle fixture directories already exist
for earlier command slices. When a listed directory already exists, extend the
manifest or add analyze-specific fixture entries; do not replace coverage used
by earlier lifecycle command tests.

- [X] T001 Add `tests/FS.GG.SDD.Artifacts.Tests/AnalysisViewTests.fs` and include it in `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` after `TasksArtifactTests.fs`.
- [X] T002 Add `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs` and include it in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` after `TasksCommandTests.fs`.
- [X] T003 [P] Add valid analysis fixture manifests under `tests/fixtures/lifecycle-commands/analysis-create/manifest.yml`, `tests/fixtures/lifecycle-commands/analysis-rerun-current/manifest.yml`, `tests/fixtures/lifecycle-commands/analysis-preserves-authored/manifest.yml`, `tests/fixtures/lifecycle-commands/analysis-refreshes-work-model/manifest.yml`, and `tests/fixtures/lifecycle-commands/analysis-accepted-deferral/manifest.yml`.
- [X] T004 [P] Add or extend blocked analysis fixture manifests under `tests/fixtures/lifecycle-commands/missing-specification/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-clarification/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-checklist/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-plan/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/failed-checklist/manifest.yml`, `tests/fixtures/lifecycle-commands/failed-plan/manifest.yml`, `tests/fixtures/lifecycle-commands/failed-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-analysis/manifest.yml`, `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/unknown-source-reference/manifest.yml`, `tests/fixtures/lifecycle-commands/dependency-cycle/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-plan/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/analysis-identity-mismatch/manifest.yml`, and `tests/fixtures/lifecycle-commands/done-task-missing-evidence/manifest.yml`.
- [X] T005 [P] Add output and boundary fixture entries for analyze under `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`, `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml`, `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml`, and `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`.

**Checkpoint**: Fixture and test file entry points exist; no analyze behavior is implemented yet.

## Phase 2: Foundational Contracts (Blocking Prerequisites)

**Purpose**: Define the public artifact, generated-view, and MVU/report
contracts before user-story implementation.

- [X] T006 [P] Add failing analysis view parser and shape tests for schema version 1, source records, source relationships, findings, readiness counts, generated-view states, optional boundary facts, diagnostics, and next action in `tests/FS.GG.SDD.Artifacts.Tests/AnalysisViewTests.fs`.
- [X] T007 [P] Add failing generated-view currency assertions for analysis work-model inputs and malformed existing analysis views in `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`.
- [X] T008 [P] Add failing normalized work-model assertions for analysis source relationships, task readiness facts, generated view state, and deterministic source digests in `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`.
- [X] T009 Extend the public analysis artifact contract in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` with `AnalysisView`, `AnalysisSourceRecord`, `AnalysisSourceRelationship`, `AnalysisFinding`, `AnalysisReadiness`, `AnalysisDiagnostic`, `AnalysisNextAction`, and parser return types for `readiness/<id>/analysis.json`.
- [X] T010 Implement analysis view parsing, schema-version validation, deterministic ordering, and diagnostics in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs`.
- [X] T011 Update generated-view and normalized work-model projection fields for analysis relationships with FSI-first ordering: extend `src/FS.GG.SDD.Artifacts/WorkModel.fsi` before adding the matching implementation in `src/FS.GG.SDD.Artifacts/WorkModel.fs` and serialization support in `src/FS.GG.SDD.Artifacts/Serialization.fs`.
- [X] T012 Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` for the deliberate analysis artifact public-surface additions after T009.
- [X] T013 [P] Add failing command public-surface tests for `AnalysisSummary`, analysis readiness counts, analysis diagnostics, `CommandReport.Analysis`, and `CommandModel.Analysis` in `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`.
- [X] T014 Extend the MVU command contract in `src/FS.GG.SDD.Commands/CommandTypes.fsi` with `AnalysisSummary`, analysis readiness count fields, `CommandReport.Analysis`, and `CommandModel.Analysis` while keeping `CommandMsg`, `CommandEffect`, `init`, `update`, and the interpreter boundary explicit.
- [X] T015 Implement the analysis command contract types and values in `src/FS.GG.SDD.Commands/CommandTypes.fs`.
- [X] T016 Add analysis diagnostic constructors for missing tasks prerequisite, failed checklist prerequisite, failed plan prerequisite, failed tasks prerequisite, identity mismatch, malformed analysis view, unknown source reference, unknown dependency, dependency cycle, unresolved ambiguity, failed checklist result, stale checklist result, incomplete plan decision, stale plan decision, missing disposition, stale task, unsupported task state, done task missing evidence, and generated-view blockers with FSI-first ordering: extend `src/FS.GG.SDD.Commands/CommandReports.fsi` before adding matching implementation in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T017 Extend blocked-report correction routing and analyze next-action selection in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T018 Add `analyzeRequest`, `runAnalyze`, `initializeTasksReadyProject`, `validAnalysisView`, and `assertAnalysisSummary` helpers in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`, and add prelude references for the new analyze public surface in `scripts/prelude.fsx` before implementation bodies depend on it.
- [X] T019 Update `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the deliberate analysis command public-surface additions after T014 and T016.

**Checkpoint**: Public `.fsi` contracts, parser contracts, command reports, diagnostics, and surface baselines are ready for story implementation.

## Phase 3: User Story 1 - Analyze Lifecycle Consistency (Priority: P1, MVP)

**Goal**: `fsgg-sdd analyze` loads one tasks-ready work item, refreshes or
diagnoses generated work-model state, creates `readiness/<id>/analysis.json`,
reports implementation readiness, and points to implementation without
requiring Governance.

**Independent Test**: Run the command in an initialized project with valid
specification, clarification, checklist, plan, and tasks artifacts; verify the
analysis view, report facts, generated-view state, and implementation next
action.

### Tests for User Story 1

- [X] T020 [P] [US1] Add failing create-flow command tests for `readiness/010-analyze-command/analysis.json`, source relationships, readiness counts, generated-view states, changed artifacts, parsed source summaries, and implementation next action in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T021 [P] [US1] Add failing MVU emitted-effect assertions for `Analyze` read effects and generated write effects in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [X] T022 [P] [US1] Add a failing generated work-model refresh test for analysis inputs in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T023 [P] [US1] Add a failing no-Governance analysis success test in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [X] T024 [P] [US1] Add failing generated analysis parser assertions for valid source records, source relationships, findings, generated views, diagnostics, and next action in `tests/FS.GG.SDD.Artifacts.Tests/AnalysisViewTests.fs`.

### Implementation for User Story 1

- [X] T025 [US1] Wire `Analyze` into lifecycle read planning in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` by adding analysis-specific read effects for `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/<id>/spec.md`, `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`, `work/<id>/tasks.yml`, `readiness/<id>/work-model.json`, `readiness/<id>/analysis.json`, and `work/`.
- [X] T026 [US1] Implement selected work-id, initialized-project, and tasks-ready prerequisite loading for `Analyze` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T027 [US1] Build the current lifecycle source set and source relationship records for specification, clarification, checklist, plan, tasks, and work-model inputs in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T028 [US1] Refresh or diagnose `readiness/<id>/work-model.json` before analysis uses generated work-model state in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T029 [US1] Implement success-case analysis findings and readiness count construction in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T030 [US1] Build deterministic `readiness/<id>/analysis.json` text with schema version 1, source digests, source relationships, readiness, findings, generated views, optional boundary facts, diagnostics, and next action in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T031 [US1] Plan safe generated write effects for `readiness/<id>/analysis.json` and any valid `readiness/<id>/work-model.json` refresh in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T032 [US1] Add analysis summary construction and generated-view report entries in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T033 [US1] Remove the unsupported-command path for `Analyze` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and keep the existing `src/FS.GG.SDD.Cli/Program.fs` command dispatch able to run `analyze --work <id>`.
- [X] T034 [US1] Update `src/FS.GG.SDD.Commands/CommandReports.fs` so successful `Analyze` reports return `NextAction.ActionId = "analysis.next.implement"` and `NextAction.Command = None`.

**Checkpoint**: User Story 1 is independently testable as the MVP.

## Phase 4: User Story 2 - Report Cross-Artifact Defects (Priority: P1)

**Goal**: Invalid lifecycle state blocks implementation readiness and reports
actionable diagnostics that name the affected artifact or identifier.

**Independent Test**: Run `analyze` against known lifecycle defects and verify
that implementation readiness is blocked, no generated analysis is treated as
current, and the report names the correction target.

### Tests for User Story 2

- [X] T035 [US2] Add failing blocked prerequisite tests for outside project, missing work id, malformed work id, duplicate logical work id, missing specification, missing clarification, missing checklist, missing plan, and missing tasks in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T036 [US2] Add failing failed-prerequisite tests for unresolved ambiguity, failed checklist results, stale checklist results, incomplete plan decisions, stale plan decisions, stale tasks, and failed tasks state in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T037 [US2] Add failing cross-artifact finding tests for unknown requirements, acceptance scenarios, clarification decisions, checklist results, plan decisions, contract references, verification obligations, migration notes, generated-view impacts, accepted deferrals, required skills, required evidence obligations, and missing dispositions in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T038 [US2] Add failing task-readiness defect tests for duplicate task ids, unknown dependencies, dependency cycles, unsupported task states, skipped tasks without rationale, and completed tasks without required evidence in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T039 [P] [US2] Add failing generated-view missing, stale, malformed, and blocked diagnostics tests for work-model and analysis views in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T040 [P] [US2] Add failing malformed analysis and analysis identity mismatch parser assertions in `tests/FS.GG.SDD.Artifacts.Tests/AnalysisViewTests.fs`.

### Implementation for User Story 2

- [X] T041 [US2] Implement analyze precondition diagnostics and no-write behavior for outside project, missing or malformed project config, missing work id, malformed work id, duplicate logical work id, and missing prerequisite artifacts in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T042 [US2] Implement cross-artifact reference validation for requirements, acceptance scenarios, clarification decisions, checklist results, plan decisions, contract references, verification obligations, migration notes, generated-view impacts, accepted deferrals, required skills, and required evidence obligations in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T043 [US2] Implement unresolved ambiguity, failed checklist result, stale checklist result, and accepted-deferral visibility analysis in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T044 [US2] Implement incomplete plan decision, stale plan decision, missing contract reference, missing verification obligation, unresolved migration posture, and generated-view impact analysis in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T045 [US2] Implement task readiness validation for duplicate ids, unknown dependencies, dependency cycles, unsupported states, missing dispositions, stale task links, skipped rationale, and completed task evidence obligations in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T046 [US2] Implement generated-view currency diagnostics for missing, stale, malformed, and blocked work-model and analysis views in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T047 [US2] Map analysis findings and generated-view diagnostics into stable command report diagnostics in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T048 [US2] Route blocked analysis next actions to specification, clarification, checklist, plan, tasks, or generated-view correction in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T049 [US2] Implement malformed existing analysis and analysis identity mismatch handling in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs`.

**Checkpoint**: Cross-artifact defects are blocked with stable diagnostics and no unsafe mutation.

## Phase 5: User Story 3 - Preserve Authored Sources During Analysis (Priority: P2)

**Goal**: Analyze is non-destructive for authored lifecycle artifacts and
mutates only generated readiness views when source data is valid and the run is
not dry-run.

**Independent Test**: Run valid, blocked, and dry-run analysis scenarios and
verify authored lifecycle source bytes are unchanged while generated artifacts
and reports reflect the current source state.

### Tests for User Story 3

- [X] T050 [US3] Add failing authored-source checksum tests for valid and blocked analysis runs covering `work/<id>/spec.md`, `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`, and `work/<id>/tasks.yml` in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T051 [US3] Add failing dry-run no-mutation assertions for `readiness/<id>/work-model.json` and `readiness/<id>/analysis.json` in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T052 [P] [US3] Add failing command report assertions for analysis generated artifact operations `create`, `update`, `preserve`, `refuse`, and `noChange` in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [X] T053 [P] [US3] Add failing MVU assertions that `Analyze` never emits authored-source `WriteFile` effects in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.

### Implementation for User Story 3

- [X] T054 [US3] Enforce that `Analyze` never plans writes to authored lifecycle sources in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T055 [US3] Implement analysis generated-write planning for create, update, preserve, refuse, and no-change decisions in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T056 [US3] Report proposed generated changes without writing `readiness/<id>/work-model.json` or `readiness/<id>/analysis.json` when `CommandRequest.DryRun = true` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T057 [US3] Serialize analysis changed-artifact safe-write decisions and diagnostic ids in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T058 [US3] Add source-byte snapshot and generated-artifact assertion helpers for analysis preservation tests in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [X] T059 [US3] Ensure blocked analysis reports refuse generated writes before interpreter effects are produced in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.

**Checkpoint**: Analyze preserves authored sources and supports dry-run generated-view reporting.

## Phase 6: User Story 4 - Keep Analysis Output Traceable (Priority: P3)

**Goal**: Generated analysis data, JSON command reports, text summaries, and
optional Governance compatibility facts are deterministic projections of one
authoritative report contract.

**Independent Test**: Run identical analysis requests repeatedly, compare JSON
and generated analysis bytes, render text, and verify every text fact exists in
the JSON report.

### Tests for User Story 4

- [X] T060 [P] [US4] Add a failing deterministic JSON test for three identical analysis runs and proposed/generated analysis payload comparison in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [X] T061 [P] [US4] Add a failing analysis text projection test for selected work id, outcome, analysis path, readiness counts, generated-view state, diagnostics, and next action in `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.
- [X] T062 [P] [US4] Add a failing analysis Governance boundary test that excludes route, freshness, profile, gate, audit, evidence freshness, protected-boundary, and release verdicts in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [X] T063 [US4] Add a failing analysis report shape assertion for `analysis`, `changedArtifacts`, `generatedViews`, `diagnostics`, `governanceCompatibility`, and `nextAction` in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.

### Implementation for User Story 4

- [X] T064 [US4] Serialize the `analysis` command summary with deterministic sorting and no host-specific fields in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T065 [US4] Render analysis outcome, analysis artifact path, readiness counts, generated-view state, diagnostics, and next action from the command report in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [X] T066 [US4] Keep analysis Governance compatibility facts advisory and not evaluated in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T067 [US4] Add local create and rerun performance assertions under the two-second harness budget in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs`.
- [X] T068 [US4] Finalize `scripts/prelude.fsx` assertions for `SddCommand.Analyze`, `parseCommand "analyze"`, `commandStage Analyze`, `nextLifecycleCommand Tasks`, `nextLifecycleCommand Analyze`, analysis summary visibility, analysis finding visibility, generated analysis view visibility, and analysis diagnostic visibility after the Phase 2 prelude references are backed by behavior.
- [X] T069 [US4] Add disposable-project CLI JSON, dry-run, and text smoke helpers for `analyze --work <id> --root <path>` in `tests/FS.GG.SDD.Commands.Tests/AnalyzeCommandTests.fs` through the existing `src/FS.GG.SDD.Cli/Program.fs` entry point.

**Checkpoint**: Machine-readable and human-readable outputs are deterministic projections of one report contract.

## Phase 7: Polish, Evidence, And Documentation

**Purpose**: Record mandatory verification evidence and update human-facing
state after implementation is complete.

- [X] T070 [P] Run `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Analysis"` and save output to `specs/010-analyze-command/readiness/artifact-analysis-tests.txt`.
- [X] T071 [P] Run `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Analyze"` and save output to `specs/010-analyze-command/readiness/command-analyze-tests.txt`.
- [X] T072 [P] Run focused deterministic JSON, text projection, generated-view, and Governance boundary analysis tests and save output to `specs/010-analyze-command/readiness/output-boundary-tests.txt`.
- [X] T073 Run `dotnet build FS.GG.SDD.sln -c Release` and save output to `specs/010-analyze-command/readiness/build-release.txt`.
- [X] T074 Run `dotnet test FS.GG.SDD.sln -c Release` and save output to `specs/010-analyze-command/readiness/full-suite.txt`.
- [X] T075 Run `dotnet fsi scripts/prelude.fsx` after a Release build and save the transcript to `specs/010-analyze-command/readiness/fsi-public-surface.txt`.
- [X] T076 Run a disposable-project CLI JSON smoke scenario for `fsgg-sdd analyze` and save output to `specs/010-analyze-command/readiness/cli-json-smoke.txt`.
- [X] T077 Run a disposable-project CLI dry-run smoke scenario for `fsgg-sdd analyze --dry-run` and save output to `specs/010-analyze-command/readiness/cli-dry-run-smoke.txt`.
- [X] T078 Run a disposable-project CLI text smoke scenario for `fsgg-sdd analyze --text`, save output to `specs/010-analyze-command/readiness/cli-text-smoke.txt`, and record human-summary review notes in `specs/010-analyze-command/readiness/human-summary-review.md`.
- [X] T079 Record create/rerun performance evidence for `analysis-create` and `analysis-rerun-current` in `specs/010-analyze-command/readiness/performance.md`.
- [X] T080 Record SDD/Governance boundary review findings in `specs/010-analyze-command/readiness/sdd-governance-boundary.md`.
- [X] T081 Record artifact traceability from `specs/010-analyze-command/spec.md` requirements to plan decisions, tasks, tests, and readiness evidence in `specs/010-analyze-command/readiness/artifact-traceability.md`.
- [X] T082 Update `docs/initial-implementation-plan.md` to mark `fsgg-sdd analyze` complete and reference `specs/010-analyze-command/readiness/`.
- [X] T083 Update `README.md`, `AGENTS.md`, and `CLAUDE.md` only where needed to keep the current command state and Claude/Codex guidance synchronized after the analyze workflow behavior changes.

**Checkpoint**: The implementation has build, test, FSI, CLI, performance,
boundary, and traceability evidence.

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 depends on Phase 2 and is the MVP scope.
- Phase 4 depends on Phase 3 because defect analysis requires loaded source facts and generated analysis shape.
- Phase 5 depends on Phases 3 and 4 because preservation behavior must cover success and blocked outcomes.
- Phase 6 depends on Phases 3 through 5 because output contracts must include success, blocked, dry-run, and no-Governance states.
- Phase 7 depends on the implemented story scope being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2; no dependency on other stories.
- **US2 (P1)**: Depends on US1 source loading, analysis view shape, and generated-view state.
- **US3 (P2)**: Depends on US1 generated-view planning and US2 blocked outcomes.
- **US4 (P3)**: Depends on analysis summaries, diagnostics, and preservation behavior from US1 through US3.

### Cross-Task Dependencies

- T010 depends on T009.
- T011 performs `.fsi` signature edits before its matching `.fs` and serialization implementation.
- T015 depends on T014.
- T016 performs `CommandReports.fsi` signature edits before its matching `CommandReports.fs` implementation.
- T016 and T017 depend on T014.
- T025 through T034 depend on T014 through T018.
- T041 through T049 depend on T035 through T040.
- T054 through T059 depend on T050 through T053.
- T064 through T069 depend on T060 through T063.
- T070 through T081 depend on all selected implementation tasks passing.
- T082 and T083 depend on readiness evidence from T070 through T081.

## Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001 and T002.
- T006, T007, T008, and T013 can run in parallel because they touch different test files.
- T020 through T024 can run in parallel because each task touches a different test file.
- T039 and T040 can run in parallel with the `AnalyzeCommandTests.fs` tasks in T035 through T038.
- T052 and T053 can run in parallel because they touch different report and workflow test files.
- T060, T061, and T062 can run in parallel because they touch different output and boundary test files.
- T070, T071, and T072 can run in parallel after implementation is complete.

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3.
3. Run the focused artifact and command analysis tests for US1.
4. Validate that `fsgg-sdd analyze` creates `readiness/<id>/analysis.json`,
   reports implementation readiness, refreshes or diagnoses
   `readiness/<id>/work-model.json`, and points to implementation.

### Incremental Delivery

1. US1 creates the native analysis view and success report.
2. US2 blocks malformed or inconsistent lifecycle state.
3. US3 proves analysis is non-destructive and dry-run safe.
4. US4 locks down deterministic JSON/text and optional Governance boundaries.
5. Phase 7 records readiness evidence and updates project guidance.

## Task Counts

- Shared setup, foundation, polish, and evidence tasks: 33
- US1 tasks: 15
- US2 tasks: 15
- US3 tasks: 10
- US4 tasks: 10
- Total tasks: 83

## Suggested MVP Scope

Complete Phases 1, 2, and 3 first. That delivers User Story 1: a usable
`fsgg-sdd analyze` command that creates the generated
`readiness/<id>/analysis.json` view, reports lifecycle consistency and
generated-view state, preserves authored sources, works without Governance,
and points implementation-ready work to implementation.
