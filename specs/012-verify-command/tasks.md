# Tasks: Verify Command

**Input**: Design documents from `specs/012-verify-command/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`quickstart.md`, and `contracts/` (`verify-view.md`, `verify-command.md`,
`verify-report-json.md`, `verify-fixtures.md`)

**Change Tier**: Tier 1 (contracted native SDD command surface, generated
`readiness/<id>/verify.json` view, task/evidence/test/skill readiness
dispositions, lifecycle readiness facts, command report JSON/text,
generated-view currency behavior, diagnostics, and optional Governance boundary
facts)

**Tests**: Required by the specification and plan. Test tasks below are written
before implementation tasks and must fail before the implementation body is
completed.

**Status Legend**:

- `[x] ✅` done with real evidence (build, tests, FSI, and/or CLI smoke)
- `[~] 🟡` partial — production behavior implemented and exercised, but a named
  test-file placement or the full fixture/blocked-test matrix is not yet authored
  (rationale inline on the task line)
- `[ ] ⬜` pending / not started
- `[-]` skipped with written rationale on the task line

## Implementation Progress (2026-06-20)

> Verify is implemented end-to-end and green: `dotnet build FS.GG.SDD.sln -c
> Release` succeeds; `dotnet test FS.GG.SDD.sln -c Release` passes **235** tests
> (66 artifacts + 169 commands, including 17 verify command tests, 3 verification
> view tests, and the verify CLI JSON/dry-run/text smoke tests); `dotnet fsi
> scripts/prelude.fsx` exercises the public verify surface. Evidence is saved
> under `specs/012-verify-command/readiness/`.

| Phase | ✅ done | 🟡 partial | ⬜ pending |
|---|---:|---:|---:|
| 1 Setup | 3 | 2 | 0 |
| 2 Foundational contracts | 11 | 4 | 0 |
| 3 US1 Verify evidence-ready work | 14 | 1 | 0 |
| 4 US2 Blocking readiness gaps | 9 | 6 | 0 |
| 5 US3 Preserve authored sources | 4 | 2 | 0 |
| 6 US4 Traceable output | 10 | 0 | 0 |
| 7 Polish / evidence / docs | 14 | 0 | 0 |
| **Totals** | **67** | **15** | **0** |

**🟡 partial themes:** verify behavior for every story is implemented and runs in
production, but some tasks asked for assertions in specific sibling test files
(`CommandWorkflowTests`, `GeneratedViewCommandTests`, `CommandReportJsonTests`,
`GeneratedModelCurrencyTests`, `NormalizedWorkModelTests`) or the full
blocked-fixture matrix. Those behaviors are instead covered by consolidated tests
in `VerifyCommandTests.fs` / `VerificationViewTests.fs` and by reused prerequisite
validation that already has its own tests; the remaining dedicated placements and
blocked-fixture manifests are the open follow-ups. T011/T018 are deviations: the
`verify.json` serializer lives in `CommandWorkflow.verifyJson` (matching the
`analyze` precedent), so no `WorkModel`/`Serialization` public-surface change was
needed.

**Task Format**: `[ID] [P?] [Story?] Description with exact file path`

- `[P]` means the task has no dependency on another incomplete task in the same
  phase and touches different files from other parallel tasks.
- `[US1]`, `[US2]`, `[US3]`, and `[US4]` map to the user stories in
  `specs/012-verify-command/spec.md`.
- Phases run sequentially. Tasks within a phase marked `[P]` may run in
  parallel.

**Elmish/MVU applicability**: `verify` is an I/O-bearing workflow. Tasks emit
the `.fsi` contract additions (`SddCommand.Verify`, verification summary,
finding/disposition/diagnostic types, `CommandReport.Verification`,
`CommandModel.Verify`) before `.fs` bodies, pure `CommandWorkflow.init`/`update`
transition tests, emitted-effect assertions, and real interpreter evidence
through CLI smoke and fixture runs. Unlike `evidence`, `verify` authors no
source artifact; its only writes are the generated `readiness/<id>/verify.json`
view and a valid `readiness/<id>/work-model.json` refresh.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the test and fixture entry points needed by the verify slice.

**Fixture update rule**: Many lifecycle fixture directories already exist for
earlier command slices (for example `outside-project`, `dependency-cycle`,
`duplicate-work-id`, `failed-tasks`, `malformed-work-id`, `missing-*`,
`missing-required-evidence`, `undisclosed-synthetic-evidence`,
`stale-generated-view`, `dry-run`, `deterministic-report`, `text-projection`,
`governance-boundary`). When a listed directory already exists, extend its
manifest with verify-specific expectations; do not replace coverage used by
earlier lifecycle command tests.

- [x] ✅ T001 Add `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs` and include it in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` after `EvidenceCommandTests.fs`.
- [x] ✅ T002 Add `tests/FS.GG.SDD.Artifacts.Tests/VerificationViewTests.fs` and include it in `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` after `EvidenceArtifactTests.fs`.
- [x] ✅ T003 [P] Add valid verify fixture manifests under `tests/fixtures/lifecycle-commands/verify-create/manifest.yml`, `tests/fixtures/lifecycle-commands/verify-rerun-current/manifest.yml`, `tests/fixtures/lifecycle-commands/verify-preserves-authored/manifest.yml`, `tests/fixtures/lifecycle-commands/verify-refreshes-work-model/manifest.yml`, `tests/fixtures/lifecycle-commands/verify-refreshes-analysis/manifest.yml`, and `tests/fixtures/lifecycle-commands/verify-accepted-deferral/manifest.yml`. (The `verify-refreshes-analysis` fixture asserts that verify reports `readiness/<id>/analysis.json` currency as the prerequisite gate and does **not** regenerate analysis; only the work-model and verification views are refreshed. See FR-021's "or explicitly diagnose" branch.)
- [~] 🟡 T004 _(partial: Only `malformed-verify-view` blocked manifest authored; remaining blocked manifests pending.)_ [P] Add or extend blocked verify fixture manifests under `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-specification/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-clarification/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-checklist/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-plan/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-analysis/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/failed-analysis/manifest.yml`, `tests/fixtures/lifecycle-commands/failed-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-verify-view/manifest.yml`, `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/unknown-source-reference/manifest.yml`, `tests/fixtures/lifecycle-commands/dependency-cycle/manifest.yml`, and `tests/fixtures/lifecycle-commands/unsupported-task-status/manifest.yml`.
- [~] 🟡 T005 _(partial: `dry-run`/`deterministic-report`/`text-projection`/`governance-boundary` covered by VerifyCommandTests; remaining readiness-defect manifests pending.)_ [P] Add or extend blocked readiness-defect fixture manifests under `tests/fixtures/lifecycle-commands/missing-required-skill/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-required-test/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-required-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-analysis/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/undisclosed-synthetic-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/invalid-deferral/manifest.yml`, and `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml`; and add or extend output/boundary manifests under `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`, `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`, and `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml`.

**Checkpoint**: Fixture and test file entry points exist; no verify behavior is
implemented yet.

## Phase 2: Foundational Contracts (Blocking Prerequisites)

**Purpose**: Define the public artifact, generated-view, and MVU/report
contracts before user-story implementation. `.fsi` signatures precede public
`.fs` implementation bodies.

### Failing contract tests

- [x] ✅ T006 Add failing verification view artifact tests for schema version 1, selected work identity, `stage = verify`, source artifact relationships, source digests, generator identity, lifecycle readiness, task graph readiness, evidence/test dispositions, skill visibility facts, generated-view currency, findings, diagnostics, and `verificationReady`/`needsVerificationCorrection` readiness in `tests/FS.GG.SDD.Artifacts.Tests/VerificationViewTests.fs`.
- [x] ✅ T007 Add failing disposition and finding tests for evidence states (`supported`, `deferred`, `missing`, `stale`, `synthetic`, `invalid`, `advisory`, `blocking`), required-test states (`satisfied`, `deferred`, `missing`, `stale`, `synthetic`, `invalid`, `advisory`, `blocking`), skill visibility states (`visible`, `missing`), and finding severities (`ready`, `advisory`, `warning`, `blocking`) in `tests/FS.GG.SDD.Artifacts.Tests/VerificationViewTests.fs`.
- [~] 🟡 T008 _(partial: Verify behavior covered via parseVerificationView round-trip in VerificationViewTests; GeneratedModelCurrencyTests not extended.)_ [P] Add failing generated-view currency assertions for the verification view (`current`, `missing`, `stale`, `malformed`, `blocked`) and work-model inputs needed by verify in `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`.
- [~] 🟡 T009 _(partial: NormalizedWorkModelTests not extended for verify source relationships.)_ [P] Add failing normalized work-model assertions for verification source relationships, disposition counts, generated-view state, and deterministic source digests in `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`.

### Public `.fsi` contract additions

- [x] ✅ T010 Extend the public verification view contract in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` with `VerificationView`, `VerificationFinding`, `EvidenceDisposition`, `RequiredTestDisposition`, `SkillVisibilityFact`, `GeneratedViewCurrency`, `VerificationDiagnostic`, lifecycle/task-graph readiness fields, and parser/return types for `readiness/<id>/verify.json`. (depends on T006, T007)
- [~] 🟡 T011 _(partial: Verify view serialization lives in CommandWorkflow.verifyJson (analyze precedent); no WorkModel.fsi/Serialization.fsi surface change required.)_ Extend verification source links, disposition projection, and `verify.json` serialization signatures in `src/FS.GG.SDD.Artifacts/WorkModel.fsi` and `src/FS.GG.SDD.Artifacts/Serialization.fsi`. (after T010)
- [x] ✅ T012 Extend the public MVU command contract in `src/FS.GG.SDD.Commands/CommandTypes.fsi` with `SddCommand.Verify`, `VerificationSummary` (with the finding/disposition/skill counts from `contracts/verify-report-json.md`), `CommandReport.Verification`, and `CommandModel.Verify`, while keeping `CommandMsg`, `CommandEffect`, `CommandWorkflow.init`, `CommandWorkflow.update`, and the effect interpreter boundary explicit through `src/FS.GG.SDD.Commands/CommandWorkflow.fsi` and `src/FS.GG.SDD.Commands/CommandEffects.fsi`. (after T010, T011)
- [x] ✅ T013 Add verify diagnostic constructor signatures for the required diagnostic families (outside project, missing/malformed project config, missing/malformed work id, missing specification/clarification/checklist/plan/tasks/analysis/evidence, analysis not ready, failed analysis, failed tasks, verification identity mismatch, duplicate work id, malformed prerequisite artifact, unknown source reference, dependency cycle, unsupported task status, missing required skill, missing required test, missing required evidence, stale analysis, stale tasks, stale evidence, undisclosed synthetic evidence, invalid deferral, malformed verification view, stale/missing/malformed/blocked generated view, tool defect) and the `verify.next.ship` next-action signature in `src/FS.GG.SDD.Commands/CommandReports.fsi`. (after T012)
- [x] ✅ T014 [P] Add failing command public-surface tests for `SddCommand.Verify`, `VerificationSummary`, verification finding/disposition counts, verify diagnostics, `CommandReport.Verification`, `CommandModel.Verify`, and the existing `CommandModel`/`CommandMsg`/`CommandEffect` MVU boundary in `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`, and the artifact verification view surface in `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs`.
- [x] ✅ T015 Add prelude references for `SddCommand.Verify`, `parseCommand "verify"`, `commandStage Verify`, `nextLifecycleCommand Evidence`, `nextLifecycleCommand Verify`, verification summary visibility, verification view visibility, disposition visibility, and verify diagnostic visibility in `scripts/prelude.fsx`; run `dotnet fsi scripts/prelude.fsx` against the draft public surface before implementation-body tasks T017 through T020, and save the early transcript to `specs/012-verify-command/readiness/fsi-public-surface-draft.txt`. (after T010 through T014)
- [x] ✅ T016 Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` and `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the deliberate verify public-surface additions after T010 through T015.

### Implementation bodies

- [x] ✅ T017 Implement verification view parsing, schema-version validation, deterministic ordering, source snapshot validation, disposition/finding construction, generated-view currency, and diagnostics in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` after T015.
- [~] 🟡 T018 _(partial: Verify view serialization implemented in CommandWorkflow.verifyJson; WorkModel.fs/Serialization.fs unchanged (consistent with analyze).)_ Implement verification source links, disposition projection, and deterministic `verify.json` serialization in `src/FS.GG.SDD.Artifacts/WorkModel.fs` and `src/FS.GG.SDD.Artifacts/Serialization.fs` after T015.
- [x] ✅ T019 Implement verify command contract types, command naming, command stage, parse support, lifecycle ordering (`Evidence` -> `Verify`), `verifyRequest`, `runVerify`, `initializeEvidencedProject`, `validVerificationView`, `assertVerificationSummary`, and source-byte snapshot helpers in `src/FS.GG.SDD.Commands/CommandTypes.fs` and `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` after T015.
- [x] ✅ T020 Implement verify diagnostic constructors, blocked-report correction routing, and verify next-action selection in `src/FS.GG.SDD.Commands/CommandReports.fs` after T015.

**Checkpoint**: Public `.fsi` contracts, parser contracts, command reports,
diagnostics, MVU boundaries, and surface baselines are ready for story
implementation.

## Phase 3: User Story 1 - Verify Evidence-Ready Work (Priority: P1, MVP)

**Goal**: `fsgg-sdd verify` loads one evidence-ready work item, validates task
graph and lifecycle prerequisites, derives required obligations, maps
task/evidence/test/skill state to dispositions, generates
`readiness/<id>/verify.json`, refreshes or diagnoses generated work-model state,
reports verification readiness, and points to `ship` without requiring
Governance.

**Independent Test**: Run `verify --work 012-verify-command` in an initialized
project with valid specification, clarification, checklist, plan, tasks,
analysis, and evidence artifacts; confirm the verification view, summary,
disposition counts, generated-view state, diagnostics, and `verify.next.ship`
next action are produced without Governance.

### Tests for User Story 1

- [x] ✅ T021 [P] [US1] Add failing create-flow command tests for `readiness/012-verify-command/verify.json`, source relationships, source digests, lifecycle readiness, task graph readiness, evidence/test/skill dispositions, findings, changed generated artifacts, generated-view state, and `verify.next.ship` next action in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`.
- [~] 🟡 T022 _(partial: Dry-run effect suppression + interpreted-effect changes covered in VerifyCommandTests; dedicated pure init/update emitted-effect assertions in CommandWorkflowTests pending.)_ [P] [US1] Add failing pure `CommandWorkflow.init`/`CommandWorkflow.update` tests for `Verify` read effects, generated `verify.json` write effect, generated `work-model.json` write effect, emitted stdout/stderr effects, and dry-run effect suppression in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [x] ✅ T023 [P] [US1] Add a failing generated work-model and verification view refresh test for valid verify facts in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [x] ✅ T024 [P] [US1] Add a failing no-Governance verify success test that asserts no freshness/route/profile/gate/audit/release verdict appears in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [x] ✅ T025 [P] [US1] Add a failing verify report shape assertion for `verification`, `changedArtifacts`, `generatedViews`, `diagnostics`, `governanceCompatibility`, and `nextAction` in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 1

- [x] ✅ T026 [US1] Wire `Verify` into lifecycle read planning in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` by adding read effects for `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/<id>/spec.md`, `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`, `work/<id>/tasks.yml`, `work/<id>/evidence.yml`, `readiness/<id>/analysis.json`, `readiness/<id>/work-model.json`, existing `readiness/<id>/verify.json`, and `work/`.
- [x] ✅ T027 [US1] Implement selected work-id, initialized-project, and evidence-ready prerequisite loading for `Verify`, including the analysis implementation-ready gate, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T028 [US1] Build the current lifecycle source set and source snapshot records for specification, clarification, checklist, plan, tasks, analysis, evidence, existing work-model, and existing verification view inputs in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T029 [US1] Validate task graph structure, unique ids, dependencies, owners, requirement/decision links, required skills, required evidence, required tests, and status transitions into task graph readiness facts in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T030 [US1] Derive required verification obligations (`test`, `evidence`, `skill`, `generatedView`, `lifecycle`) from task metadata, plan verification obligations, analysis findings, accepted deferrals, generated-view impacts, and lifecycle rules in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T031 [US1] Match declarations to obligations and construct evidence dispositions, required-test dispositions, and skill-visibility facts with stable disposition ids in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T032 [US1] Build deterministic `readiness/<id>/verify.json` content with schema version 1, generator identity, source relationships, source digests, lifecycle/task-graph readiness, dispositions, skill visibility, generated-view currency, findings, diagnostics, and readiness state in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T033 [US1] Plan generated write effects for `readiness/<id>/verify.json` and `readiness/<id>/work-model.json` only when source facts are valid and `CommandRequest.DryRun = false`, planning zero authored-source writes in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T034 [US1] Add `VerificationSummary` construction, finding/disposition/skill counts, source snapshot count, generated artifact change records, and generated-view report entries in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T035 [US1] Remove the unsupported-command path for `Verify` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, make `src/FS.GG.SDD.Cli/Program.fs` able to run `verify --work <id>`, and return `NextAction.ActionId = "verify.next.ship"` with `NextAction.Command = None` and required artifacts `readiness/<id>/verify.json` plus refreshed `readiness/<id>/work-model.json` from `src/FS.GG.SDD.Commands/CommandReports.fs`.

**Checkpoint**: User Story 1 is independently testable as the MVP.

## Phase 4: User Story 2 - Find Blocking Readiness Gaps (Priority: P1)

**Goal**: Verification identifies the exact task, evidence declaration, required
test, skill requirement, generated view, or missing prerequisite that is not
ready, blocks verification readiness, and never treats an existing generated
view as current when its sources are stale or malformed.

**Independent Test**: Run `verify` against fixtures with known readiness defects
(outside project, missing/malformed prerequisites, duplicate task ids,
dependency cycle, unsupported status, unknown reference, missing/stale evidence,
undisclosed synthetic evidence, invalid deferral, missing required test, missing
required skill, stale/malformed generated view); confirm no verification view is
treated as current until the report names the affected artifact, identifier,
severity, and correction.

### Tests for User Story 2

- [~] 🟡 T036 _(partial: missing-spec/clarif/checklist/plan prerequisite blocking shares the tasks/evidence prerequisite chain; missing-evidence + missing-analysis authored; remaining precondition cases pending dedicated tests.)_ [US2] Add failing blocked-prerequisite and precondition tests for outside project, missing or malformed project config, missing work id, malformed work id, duplicate logical work id, missing specification, missing clarification, missing checklist, missing plan, missing tasks, missing analysis, missing evidence, malformed prerequisite artifact, analysis not implementation-ready, and failed analysis in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`.
- [~] 🟡 T037 _(partial: Task-graph blocking reuses tasksPrerequisite validation (own tests); dedicated verify task-graph blocked tests pending.)_ [US2] Add failing blocked task graph tests for duplicate task ids, unknown dependencies, dependency cycle, unsupported status transition, missing owner, missing requirement link, and failed task graph validation in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`.
- [~] 🟡 T038 _(partial: missing-evidence authored; stale/undisclosed-synthetic/invalid-deferral/missing-test/missing-skill blocked tests pending.)_ [US2] Add failing blocked readiness tests for missing evidence, stale evidence, stale analysis, stale tasks, undisclosed synthetic evidence, invalid deferral, missing required test, stale required test, missing required skill, unknown source reference, and completed task without required evidence in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`.
- [~] 🟡 T039 _(partial: Blocked verify suppresses generated writes (verified via missing-evidence/malformed-view producing no verify.json); dedicated MVU WriteFile assertions pending.)_ [P] [US2] Add failing MVU assertions that blocked verify never emits generated `WriteFile` effects for `verify.json` or `work-model.json` in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [~] 🟡 T040 _(partial: malformed existing verify.json refuses refresh (authored test); dedicated GeneratedViewCommandTests verify additions pending.)_ [P] [US2] Add failing generated-view diagnostic tests for missing, stale, malformed, and blocked work-model/analysis/verification views, including malformed existing `verify.json` refusing safe refresh, in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [~] 🟡 T041 _(partial: Verify diagnostic serialization covered via report shape test; full diagnostic-family serialization assertions pending.)_ [P] [US2] Add failing verify diagnostic serialization assertions for all required diagnostic families in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 2

- [x] ✅ T042 [US2] Implement task graph validation diagnostics (duplicate ids, unknown dependency, dependency cycle, unsupported status, missing owner, missing requirement link) and block verification readiness in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T043 [US2] Implement evidence disposition blocking for missing, stale, undisclosed synthetic, invalid-deferral, and unknown-reference states in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T044 [US2] Implement required-test disposition blocking for missing, stale, undisclosed synthetic, invalid, and unaccepted-deferral states in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T045 [US2] Implement skill-visibility resolution from lifecycle artifacts and declared agent/capability metadata, and block on missing required skills or capability tags in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T046 [US2] Implement generated-view currency diagnostics (missing, stale, malformed, blocked) and malformed-`verify.json` safe-refresh refusal in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T047 [US2] Implement source-digest comparison and stale-analysis, stale-tasks, and stale-evidence diagnostics that mark verification results stale or blocked in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T048 [US2] Build blocking verification findings with stable ids and structured links to affected requirements, acceptance scenarios, clarification decisions, checklist results, plan decisions, tasks, evidence/test obligations, required skills, evidence declarations, generated views, accepted deferrals, and source artifacts in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T049 [US2] Route blocked verify next actions to implementation continuation, evidence correction, task correction, analysis rerun, generated-view refresh, missing-skill correction, required-test correction, or prerequisite lifecycle correction with blocking diagnostic ids in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T050 [US2] Add blocked-scenario and finding/disposition assertion helpers for verify tests in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.

**Checkpoint**: Blocking readiness gaps are identified precisely and block
verification readiness with stable diagnostics and no generated write.

## Phase 5: User Story 3 - Preserve Authored Lifecycle Sources (Priority: P2)

**Goal**: Verification is a non-destructive readiness check; authored
specifications, plans, tasks, and evidence are never created, updated,
reordered, normalized, or removed, and dry-run mutates zero files.

**Independent Test**: Run `verify` in valid, blocked, and dry-run scenarios and
confirm authored lifecycle artifacts remain byte-identical while only generated
readiness output and reports reflect the current source state.

### Tests for User Story 3

- [x] ✅ T051 [US3] Add failing authored-source preservation tests asserting byte-identical `work/<id>/spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, `tasks.yml`, and `evidence.yml` after valid and blocked runs in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`.
- [x] ✅ T052 [US3] Add failing dry-run tests asserting zero authored and generated file changes while still reporting proposed generated artifacts, diagnostics, readiness state, and next action in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`.
- [~] 🟡 T053 _(partial: Authored-source preservation covered in VerifyCommandTests; dedicated MVU authored-WriteFile assertion in CommandWorkflowTests pending.)_ [P] [US3] Add failing MVU assertions that `Verify` never emits an authored `WriteFile` effect for any `work/<id>/` source in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [~] 🟡 T054 _(partial: Rerun noChange covered in VerifyCommandTests; dedicated GeneratedViewCommandTests additions pending.)_ [P] [US3] Add failing generated-only refresh and rerun-current `noChange` tests for unchanged `verify.json` and current `work-model.json` in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.

### Implementation for User Story 3

- [x] ✅ T055 [US3] Ensure the verify workflow plans no authored-source write effects in any path and restricts generated writes to `readiness/<id>/verify.json` and `readiness/<id>/work-model.json` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T056 [US3] Implement the dry-run path that reports proposed generated artifact changes without emitting any write effect in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T057 [US3] Implement rerun-current `noChange` behavior for unchanged `readiness/<id>/verify.json` and current `readiness/<id>/work-model.json` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T058 [US3] Serialize generated artifact operations and safe-write decisions for `create`, `update`, `preserve`, `refuse`, and `noChange` in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.

**Checkpoint**: Authored lifecycle sources are preserved across valid, blocked,
and dry-run paths; only generated readiness views change.

## Phase 6: User Story 4 - Keep Verification Output Traceable (Priority: P3)

**Goal**: Verification views, JSON command reports, text summaries, CLI smoke
paths, and optional Governance compatibility facts are deterministic projections
of one authoritative report contract.

**Independent Test**: Run identical verify requests repeatedly, compare JSON and
proposed `verify.json` bytes, render text, run CLI smoke paths, and confirm
every text fact exists in the JSON report while optional Governance references
remain advisory.

### Tests for User Story 4

- [x] ✅ T059 [P] [US4] Add a failing deterministic JSON test for three identical verify runs and proposed/generated `verify.json` payload comparison in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [x] ✅ T060 [P] [US4] Add a failing verify text projection test for selected work id, outcome, verify path, ready/advisory/warning/blocking counts, evidence disposition counts, test disposition counts, skill visibility counts, generated-view state, diagnostics, and next action in `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.
- [x] ✅ T061 [P] [US4] Add a failing verify boundary test that excludes effective-evidence freshness, route, profile, gate, audit, protected-boundary, release verdicts, ship readiness, `readiness/<id>/ship.json`, and any `Ship` command/report fields in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [x] ✅ T062 [US4] Add disposable-project CLI JSON, dry-run, and text smoke helpers for `verify --work <id> --root <path> [--dry-run] [--text]` in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs` through the existing `src/FS.GG.SDD.Cli/Program.fs` entry point.
- [x] ✅ T063 [US4] Add local performance assertions under the two-second harness budget for `verify-create`, `verify-rerun-current`, and `verify-refreshes-work-model` in `tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`.

### Implementation for User Story 4

- [x] ✅ T064 [US4] Serialize the `verify` command summary, evidence/test dispositions, skill visibility, findings, generated-view state, diagnostics, Governance compatibility facts, and next action with deterministic sorting and no host-specific fields in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [x] ✅ T065 [US4] Render verify outcome, verify artifact path, ready/advisory/warning/blocking counts, evidence disposition counts, test disposition counts, skill visibility counts, generated-view state, diagnostics, and next action from the command report in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [x] ✅ T066 [US4] Keep verify Governance compatibility facts advisory and not evaluated, and keep ship readiness, effective-evidence freshness, route, profile, gate, audit, protected-boundary, and release verdict fields absent from verify reports in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T067 [US4] Parse `verify --work <id> --dry-run --text --root <path>` and map arguments to `CommandRequest` fields in `src/FS.GG.SDD.Cli/Program.fs`.
- [x] ✅ T068 [US4] Exclude timestamps, durations, terminal details, process ids, random values, directory enumeration order, absolute host paths, and host-specific separators from verify reports and the verification view in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.

**Checkpoint**: Machine-readable and human-readable verify outputs are
deterministic projections of one report contract.

## Phase 7: Polish, Evidence, And Documentation

**Purpose**: Record mandatory verification evidence and update human-facing
state after implementation is complete.

- [x] ✅ T069 [P] Run `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Verification"` and save output to `specs/012-verify-command/readiness/artifact-verify-tests.txt`.
- [x] ✅ T070 [P] Run `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Verify"` and save output to `specs/012-verify-command/readiness/command-verify-tests.txt`.
- [x] ✅ T071 [P] Run focused deterministic JSON, text projection, generated-view, and Governance boundary evidence tests and save output to `specs/012-verify-command/readiness/output-boundary-tests.txt`.
- [x] ✅ T072 Run `dotnet build FS.GG.SDD.sln -c Release` and save output to `specs/012-verify-command/readiness/build-release.txt`.
- [x] ✅ T073 Run `dotnet test FS.GG.SDD.sln -c Release` and save output to `specs/012-verify-command/readiness/full-suite.txt`.
- [x] ✅ T074 Run `dotnet fsi scripts/prelude.fsx` after a Release build and save the transcript to `specs/012-verify-command/readiness/fsi-public-surface.txt`.
- [x] ✅ T075 Run a disposable-project CLI JSON smoke scenario for `fsgg-sdd verify` and save output to `specs/012-verify-command/readiness/cli-json-smoke.txt`.
- [x] ✅ T076 Run a disposable-project CLI dry-run smoke scenario for `fsgg-sdd verify --dry-run` and save output to `specs/012-verify-command/readiness/cli-dry-run-smoke.txt`.
- [x] ✅ T077 Run a disposable-project CLI text smoke scenario for `fsgg-sdd verify --text`, save output to `specs/012-verify-command/readiness/cli-text-smoke.txt`, and record human-summary review notes in `specs/012-verify-command/readiness/human-summary-review.md`.
- [x] ✅ T078 Record create, rerun, and work-model-refresh performance evidence for `verify-create`, `verify-rerun-current`, and `verify-refreshes-work-model` in `specs/012-verify-command/readiness/performance.md`.
- [x] ✅ T079 Record SDD/Governance boundary review findings in `specs/012-verify-command/readiness/sdd-governance-boundary.md`.
- [x] ✅ T080 Record artifact traceability from `specs/012-verify-command/spec.md` requirements to plan decisions, tasks, tests, and readiness evidence in `specs/012-verify-command/readiness/artifact-traceability.md`.
- [x] ✅ T081 Update `docs/initial-implementation-plan.md` to mark `fsgg-sdd verify` complete and reference `specs/012-verify-command/readiness/`.
- [x] ✅ T082 Update `README.md`, `AGENTS.md`, and `CLAUDE.md` only where needed to keep the current command state and Claude/Codex guidance synchronized after the verify workflow behavior changes.

**Checkpoint**: The implementation has build, test, FSI, CLI, performance,
boundary, and traceability evidence.

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 depends on Phase 2 and is the MVP scope.
- Phase 4 depends on Phase 3 because blocking detection reuses the loaded source
  set, task graph facts, dispositions, and base report shape.
- Phase 5 depends on Phases 3 and 4 because preservation and dry-run guarantees
  must hold across success and blocked paths.
- Phase 6 depends on Phases 3 through 5 because output contracts must include
  success, blocked, dry-run, no-Governance, and preservation states.
- Phase 7 depends on the implemented story scope being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2; no dependency on other stories.
- **US2 (P1)**: Depends on US1 source loading, prerequisite gating, task graph
  facts, and disposition construction.
- **US3 (P2)**: Depends on US1 generated-write planning and US2 blocked-path
  behavior.
- **US4 (P3)**: Depends on verify summaries, diagnostics, generated-view
  reporting, and preservation behavior from US1 through US3.

### Cross-Task Dependencies

- T011 depends on T010.
- T012 and T013 depend on the public-surface decisions from T010 and T011.
- T015 depends on T010 through T014 and must run before implementation-body
  tasks T017 through T020.
- T016 depends on T010 through T015.
- T017 through T020 depend on the FSI/prelude exercise in T015.
- T026 through T035 depend on T017 through T020.
- T042 through T050 depend on T036 through T041.
- T055 through T058 depend on T051 through T054.
- T064 through T068 depend on T059 through T063.
- T069 through T080 depend on all selected implementation tasks passing.
- T081 and T082 depend on readiness evidence from T069 through T080.

## Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001 and T002.
- T008, T009, and T014 can run in parallel because they touch different test
  files.
- T021 through T025 can run in parallel because each task touches a different
  test file.
- T039, T040, and T041 can run in parallel with the `VerifyCommandTests.fs`
  tasks in T036, T037, and T038 (the three `VerifyCommandTests.fs` tasks share a
  file and run sequentially among themselves).
- T053 and T054 can run in parallel with the `VerifyCommandTests.fs` tasks in
  T051 and T052.
- T059, T060, and T061 can run in parallel because they touch different output
  and boundary test files.
- T069, T070, and T071 can run in parallel after implementation is complete.

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3.
3. Run the focused artifact and command evidence tests for US1.
4. Validate that `fsgg-sdd verify` generates `readiness/<id>/verify.json`,
   reports verification readiness, refreshes or diagnoses
   `readiness/<id>/work-model.json`, preserves authored sources, works without
   Governance, and points verification-ready work to `ship`.

### Incremental Delivery

1. US1 creates the native verification view and success report.
2. US2 identifies and blocks readiness gaps with precise diagnostics.
3. US3 guarantees non-destructive preservation and dry-run behavior.
4. US4 locks down deterministic JSON/text, CLI smoke paths, and optional
   Governance boundaries.
5. Phase 7 records readiness evidence and updates project guidance.

## Task Counts

- Shared setup, foundation, and polish tasks: 34
- US1 tasks: 15
- US2 tasks: 15
- US3 tasks: 8
- US4 tasks: 10
- Total tasks: 82

## Suggested MVP Scope

Complete Phases 1, 2, and 3 first. That delivers User Story 1: a usable
`fsgg-sdd verify` command that generates the `readiness/<id>/verify.json` view,
maps task/evidence/test/skill state to dispositions, reports generated-view
state, preserves authored sources, works without Governance, and points
verification-ready work to `ship`.
