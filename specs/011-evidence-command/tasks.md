# Tasks: Evidence Command

**Input**: Design documents from `specs/011-evidence-command/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`quickstart.md`, and `contracts/`

**Change Tier**: Tier 1 (contracted native command surface, authored
`evidence.yml` declarations, evidence obligation dispositions, lifecycle
readiness facts, command report JSON/text, generated-view currency behavior,
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
  `specs/011-evidence-command/spec.md`.
- Phases run sequentially. Tasks within a phase marked `[P]` may run in
  parallel.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the test and fixture entry points needed by the evidence
slice.

**Fixture update rule**: Several lifecycle fixture directories already exist
for earlier command slices. When a listed directory already exists, extend the
manifest or add evidence-specific fixture entries; do not replace coverage used
by earlier lifecycle command tests.

- [X] T001 Add `tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs` and include it in `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` after `AnalysisViewTests.fs`.
- [X] T002 Add `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs` and include it in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` after `AnalyzeCommandTests.fs`.
- [X] T003 [P] Add valid evidence fixture manifests under `tests/fixtures/lifecycle-commands/evidence-create/manifest.yml`, `tests/fixtures/lifecycle-commands/evidence-rerun-current/manifest.yml`, `tests/fixtures/lifecycle-commands/evidence-preserves-existing/manifest.yml`, `tests/fixtures/lifecycle-commands/evidence-compatible-update/manifest.yml`, `tests/fixtures/lifecycle-commands/evidence-refreshes-work-model/manifest.yml`, `tests/fixtures/lifecycle-commands/evidence-accepted-deferral/manifest.yml`, and `tests/fixtures/lifecycle-commands/evidence-synthetic-disclosed/manifest.yml`.
- [X] T004 [P] Add or extend blocked evidence fixture manifests under `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-analysis/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-required-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/duplicate-evidence-id/manifest.yml`, `tests/fixtures/lifecycle-commands/unknown-evidence-reference/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/undisclosed-synthetic-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-deferral-rationale/manifest.yml`, `tests/fixtures/lifecycle-commands/unsafe-evidence-update/manifest.yml`, and `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml`.
- [X] T005 [P] Add or extend output and boundary fixture manifests under `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`, `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`, and `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml`.

**Checkpoint**: Fixture and test file entry points exist; no evidence behavior is
implemented yet.

## Phase 2: Foundational Contracts (Blocking Prerequisites)

**Purpose**: Define the public artifact, generated-view, and MVU/report
contracts before user-story implementation.

- [ ] T006 Add failing evidence artifact parser tests for schema version 1, selected work identity, source artifact paths, source snapshots, declaration ids, evidence kinds, result states, source references, synthetic disclosure, deferral rationale, and lifecycle notes in `tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`.
- [ ] T007 Add failing evidence obligation and disposition tests for supported, deferred, missing, stale, synthetic, invalid, advisory, and blocking states in `tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`.
- [ ] T008 [P] Add failing generated-view currency assertions for evidence work-model inputs and malformed existing work-model evidence entries in `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`.
- [ ] T009 [P] Add failing normalized work-model assertions for evidence source relationships, evidence entries, obligation disposition counts, generated view state, and deterministic source digests in `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`.
- [ ] T010 Extend the public evidence artifact contract in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` with `EvidenceArtifact`, `EvidenceSourceSnapshot`, `EvidenceSourceReference`, `EvidenceObligation`, `EvidenceDisposition`, `EvidenceDiagnostic`, evidence declaration fields, and parser return types for `work/<id>/evidence.yml`.
- [ ] T011 Extend evidence entries, evidence source links, and evidence readiness projection signatures in `src/FS.GG.SDD.Artifacts/WorkModel.fsi` and `src/FS.GG.SDD.Artifacts/Serialization.fsi`.
- [ ] T012 Extend the public MVU command contract in `src/FS.GG.SDD.Commands/CommandTypes.fsi` with `SddCommand.Evidence`, `EvidenceSummary`, evidence readiness count fields, `CommandReport.Evidence`, and `CommandModel.Evidence` while keeping `CommandMsg`, `CommandEffect`, `CommandWorkflow.init`, `CommandWorkflow.update`, and the effect interpreter boundary explicit through `src/FS.GG.SDD.Commands/CommandWorkflow.fsi` and `src/FS.GG.SDD.Commands/CommandEffects.fsi`.
- [X] T013 Add evidence diagnostic constructor signatures for missing analysis prerequisite, analysis not ready, identity mismatch, malformed evidence artifact, duplicate evidence id, unknown evidence reference, missing required evidence, stale evidence, stale evidence source, undisclosed synthetic evidence, missing deferral rationale, missing required skill, unsupported result state, unsafe update, and generated-view blockers in `src/FS.GG.SDD.Commands/CommandReports.fsi`.
- [X] T014 [P] Add failing command public-surface tests for `SddCommand.Evidence`, `EvidenceSummary`, evidence readiness counts, evidence diagnostics, `CommandReport.Evidence`, `CommandModel.Evidence`, and the existing `CommandModel`/`CommandMsg`/`CommandEffect` MVU boundary in `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`.
- [ ] T015 Add prelude references for `SddCommand.Evidence`, `parseCommand "evidence"`, `commandStage Evidence`, `nextLifecycleCommand Analyze`, `nextLifecycleCommand Evidence`, evidence summary visibility, evidence declaration visibility, evidence disposition visibility, and evidence diagnostic visibility in `scripts/prelude.fsx`; run `dotnet fsi scripts/prelude.fsx` against the draft public surface before implementation-body tasks T017 through T020, and save the early transcript to `specs/011-evidence-command/readiness/fsi-public-surface-draft.txt`.
- [X] T016 Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` and `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the deliberate evidence public-surface additions after T010 through T015.
- [ ] T017 Implement evidence artifact parsing, schema-version validation, deterministic ordering, source snapshot validation, declaration validation, obligation matching, and diagnostics in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` after T015.
- [ ] T018 Implement evidence entries, evidence source links, and evidence readiness projection behavior in `src/FS.GG.SDD.Artifacts/WorkModel.fs` and `src/FS.GG.SDD.Artifacts/Serialization.fs` after T015.
- [X] T019 Implement evidence command contract types, command naming, command stage, parse support, lifecycle ordering, `evidenceRequest`, `runEvidence`, `initializeAnalyzedProject`, `validEvidenceArtifact`, `validEvidenceInput`, `assertEvidenceSummary`, and source-byte snapshot helpers in `src/FS.GG.SDD.Commands/CommandTypes.fs` and `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` after T015.
- [X] T020 Implement evidence diagnostic constructors, blocked-report correction routing, and evidence next-action selection in `src/FS.GG.SDD.Commands/CommandReports.fs` after T015.

**Checkpoint**: Public `.fsi` contracts, parser contracts, command reports,
diagnostics, MVU boundaries, and surface baselines are ready for story
implementation.

## Phase 3: User Story 1 - Declare Evidence For Completed Work (Priority: P1, MVP)

**Goal**: `fsgg-sdd evidence` loads one analyzed work item, creates or updates
`work/<id>/evidence.yml`, maps current obligations to dispositions, refreshes
or diagnoses generated work-model state, reports evidence readiness, and points
to verify without requiring Governance.

**Independent Test**: Run the command in an initialized project with valid
specification, clarification, checklist, plan, tasks, and analysis artifacts;
verify the evidence artifact, evidence summary, disposition counts,
generated-view state, diagnostics, and verify next action.

### Tests for User Story 1

- [X] T021 [P] [US1] Add failing create-flow command tests for `work/011-evidence-command/evidence.yml`, source snapshots, evidence declarations, obligation dispositions, changed artifacts, parsed source summaries, generated-view state, and verify next action in `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`.
- [ ] T022 [P] [US1] Add failing pure `CommandWorkflow.init`/`CommandWorkflow.update` tests for `Evidence` read effects, authored evidence write effects, generated work-model write effects, emitted stdout/stderr effects, and dry-run effect suppression in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [ ] T023 [P] [US1] Add a failing generated work-model refresh test for valid evidence facts in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [ ] T024 [P] [US1] Add a failing no-Governance evidence success test in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [ ] T025 [P] [US1] Add a failing evidence report shape assertion for `evidence`, `changedArtifacts`, `generatedViews`, `diagnostics`, `governanceCompatibility`, and `nextAction` in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 1

- [X] T026 [US1] Wire `Evidence` into lifecycle read planning in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` by adding evidence-specific read effects for `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/<id>/spec.md`, `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`, `work/<id>/tasks.yml`, `readiness/<id>/analysis.json`, `readiness/<id>/work-model.json`, `work/<id>/evidence.yml`, and `work/`.
- [X] T027 [US1] Implement selected work-id, initialized-project, and analyzed implementation-ready prerequisite loading for `Evidence` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T028 [US1] Build the current lifecycle source set and source snapshot records for specification, clarification, checklist, plan, tasks, analysis, existing work-model, and evidence inputs in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T029 [US1] Parse optional `CommandRequest.InputText`, merge it with existing evidence facts, and preserve omitted evidence declarations in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [ ] T030 [US1] Derive evidence obligations from task required evidence, plan verification obligations, required skills, generated-view impacts, accepted deferrals, and analysis findings in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T031 [US1] Match declarations to obligations and construct supported, deferred, missing, stale, synthetic, invalid, advisory, and blocking evidence dispositions in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T032 [US1] Build deterministic `work/<id>/evidence.yml` text with schema version 1, source relationships, source snapshots, declarations, lifecycle notes, and evidence-ready status in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T033 [US1] Plan safe authored write effects for `work/<id>/evidence.yml` and generated write effects for `readiness/<id>/work-model.json` when source facts are valid and `CommandRequest.DryRun = false` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T034 [US1] Add evidence summary construction, disposition counts, artifact change records, and generated-view report entries in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T035 [US1] Remove the unsupported-command path for `Evidence` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, keep `src/FS.GG.SDD.Cli/Program.fs` able to run `evidence --work <id>`, and return `NextAction.ActionId = "evidence.next.verify"` with `NextAction.Command = None` from `src/FS.GG.SDD.Commands/CommandReports.fs`.

**Checkpoint**: User Story 1 is independently testable as the MVP.

## Phase 4: User Story 2 - Preserve And Update Evidence Safely (Priority: P1)

**Goal**: Existing evidence declarations are preserved, compatible additions
are safe, stale evidence is visible, and destructive or semantically unsafe
updates block before any authored evidence is changed.

**Independent Test**: Run `evidence` against work items with existing evidence
declarations and verify that compatible additions preserve existing facts,
stale source links are reported, and conflicting updates block before writing.

### Tests for User Story 2

- [ ] T036 [US2] Add failing preserve, rerun-current, compatible-update, stale-evidence, and unsafe-update command tests in `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`.
- [ ] T037 [US2] Add failing existing evidence parser tests for duplicate ids, selected-work mismatch, stale source snapshots, source reference stability, synthetic disclosure preservation, deferral rationale preservation, and lifecycle note preservation in `tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`.
- [ ] T038 [P] [US2] Add failing MVU assertions that unsafe evidence updates never emit authored `WriteFile` effects in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [ ] T039 [P] [US2] Add failing command report assertions for evidence artifact operations `create`, `update`, `preserve`, `refuse`, and `noChange` in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [ ] T040 [P] [US2] Add failing generated-view assertions for stale evidence source snapshots and blocked evidence refresh outcomes in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.

### Implementation for User Story 2

- [X] T041 [US2] Implement existing evidence loading, selected-work identity validation, schema version validation, and source relationship validation in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T042 [US2] Implement safe merge behavior that preserves existing evidence ids, task links, source references, result states, synthetic disclosures, deferral rationales, and lifecycle notes in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T043 [US2] Implement source snapshot digest comparison and stale evidence disposition creation in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [ ] T044 [US2] Implement destructive update detection for removed ids, renumbered ids, reordered destructive content, changed source references, changed result meaning, changed synthetic disclosure, and changed deferral rationale in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [ ] T045 [US2] Block duplicate evidence ids, unknown task references, unknown requirement references, unknown acceptance scenario references, unknown clarification decision references, unknown checklist result references, unknown plan decision references, unknown obligation references, unknown generated views, unknown source references, and unsupported result states before writes in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T046 [US2] Preserve and validate synthetic disclosure, deferral rationale, deferral owner, deferral scope, later lifecycle visibility, evidence notes, and advisory notes in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs`.
- [ ] T047 [US2] Serialize evidence artifact operations and safe-write decisions for `create`, `update`, `preserve`, `refuse`, and `noChange` in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T048 [US2] Build preservation, compatible-update, stale-evidence, and unsafe-update diagnostics in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T049 [US2] Route blocked evidence updates to evidence correction, task correction, analysis rerun, or generated-view refresh next actions in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [ ] T050 [US2] Add source-byte snapshot and evidence artifact assertion helpers for preservation tests in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [ ] T051 [US2] Implement rerun-current no-change behavior for unchanged `work/<id>/evidence.yml` and current `readiness/<id>/work-model.json` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.

**Checkpoint**: Existing evidence is preserved, compatible updates are safe,
and unsafe changes are blocked with stable diagnostics.

## Phase 5: User Story 3 - Diagnose Missing Or Invalid Evidence (Priority: P2)

**Goal**: Invalid lifecycle state, unsupported completion, stale task links,
undisclosed synthetic evidence, missing deferral rationale, and malformed
evidence block readiness with actionable diagnostics and no unsafe mutation.

**Independent Test**: Invoke `evidence` outside a project, before analysis, with
missing tasks, completed tasks without evidence, unknown references, malformed
evidence ids, stale evidence, undisclosed synthetic evidence, missing deferral
rationale, missing required skills, and unsafe update attempts; verify that no
unsafe write occurs and each result contains a stable diagnostic and correction.

### Tests for User Story 3

- [ ] T052 [US3] Add failing blocked prerequisite tests for outside project, missing work id, malformed work id, duplicate logical work id, missing specification, missing clarification, missing checklist, missing plan, missing tasks, missing analysis, malformed analysis, analysis identity mismatch, and analysis not ready in `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`.
- [ ] T053 [US3] Add failing missing evidence readiness tests for completed tasks without required evidence, unresolved required evidence obligations, missing required skills, stale task facts, and missing accepted deferrals in `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`.
- [ ] T054 [P] [US3] Add failing invalid evidence artifact tests for malformed evidence schema version, malformed evidence id, duplicate evidence id, unknown references, undisclosed synthetic evidence, missing deferral rationale, and unsupported result state in `tests/FS.GG.SDD.Artifacts.Tests/EvidenceArtifactTests.fs`.
- [ ] T055 [P] [US3] Add failing generated-view missing, stale, malformed, and blocked diagnostics tests for evidence work-model refresh in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [ ] T056 [P] [US3] Add failing evidence diagnostic serialization assertions for all required evidence diagnostic families in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 3

- [X] T057 [US3] Implement evidence precondition diagnostics and no-write behavior for outside project, missing or malformed project config, missing work id, malformed work id, duplicate logical work id, and missing prerequisite artifacts in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T058 [US3] Implement analysis prerequisite parsing, work-id matching, schema version validation, implementation-ready state validation, accepted-deferral state validation, and stale analysis diagnostics in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [ ] T059 [US3] Report completed tasks without required evidence, missing evidence obligations, missing required skills, and stale task links as evidence readiness defects in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T060 [US3] Implement diagnostics for unknown references, undisclosed synthetic evidence, missing deferral rationale, unsupported result state, malformed evidence schema, and malformed evidence ids in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T061 [US3] Ensure blocked evidence reports refuse authored evidence writes and generated work-model writes before interpreter effects are produced in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T062 [US3] Implement generated-view currency diagnostics for missing, stale, malformed, and blocked evidence work-model states in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T063 [US3] Route blocked evidence next actions to implementation continuation, evidence correction, task correction, analysis rerun, or generated-view refresh in `src/FS.GG.SDD.Commands/CommandReports.fs`.

**Checkpoint**: Missing or invalid evidence is blocked with stable diagnostics
and no unsafe mutation.

## Phase 6: User Story 4 - Keep Evidence Output Traceable (Priority: P3)

**Goal**: Evidence artifacts, JSON command reports, text summaries, CLI smoke
paths, and optional Governance compatibility facts are deterministic
projections of one authoritative report contract.

**Independent Test**: Run identical evidence requests repeatedly, compare JSON
and proposed evidence bytes, render text, run CLI smoke paths, and verify every
text fact exists in the JSON report while optional Governance references remain
advisory.

### Tests for User Story 4

- [ ] T064 [P] [US4] Add a failing deterministic JSON test for three identical evidence runs and proposed/generated evidence payload comparison in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [ ] T065 [P] [US4] Add a failing evidence text projection test for selected work id, outcome, evidence artifact path, declaration count, obligation count, disposition counts, generated-view state, diagnostics, and next action in `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.
- [ ] T066 [P] [US4] Add a failing evidence boundary test that excludes freshness, route, profile, gate, audit, protected-boundary, effective-evidence, release verdicts, verify readiness, ship readiness, `readiness/<id>/verify.json`, `readiness/<id>/ship.json`, and Verify/Ship command/report fields in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [X] T067 [US4] Add disposable-project CLI JSON, dry-run, and text smoke helpers for `evidence --work <id> --root <path> --input <evidence-text>` in `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs` through the existing `src/FS.GG.SDD.Cli/Program.fs` entry point.
- [ ] T068 [US4] Add local create, compatible-update, and rerun performance assertions under the two-second harness budget for `evidence-create`, `evidence-compatible-update`, and `evidence-rerun-current` in `tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs`.

### Implementation for User Story 4

- [X] T069 [US4] Serialize the `evidence` command summary, evidence dispositions, artifact changes, generated-view state, diagnostics, Governance compatibility facts, and next action with deterministic sorting and no host-specific fields in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T070 [US4] Render evidence outcome, evidence artifact path, declaration count, obligation count, supported count, deferred count, missing count, stale count, synthetic count, invalid count, blocking count, generated-view state, diagnostics, and next action from the command report in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [X] T071 [US4] Keep evidence Governance compatibility facts advisory and not evaluated, and keep verify readiness, ship readiness, effective-evidence freshness, protected-boundary enforcement, and release verdict fields absent from evidence reports in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T072 [US4] Parse `evidence --work <id> --input <evidence-text> --dry-run --text --root <path>` and map arguments to `CommandRequest` fields in `src/FS.GG.SDD.Cli/Program.fs`.
- [X] T073 [US4] Exclude timestamps, durations, terminal details, process ids, random values, directory enumeration order, absolute host paths, and host-specific separators from evidence reports in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.

**Checkpoint**: Machine-readable and human-readable evidence outputs are
deterministic projections of one report contract.

## Phase 7: Polish, Evidence, And Documentation

**Purpose**: Record mandatory verification evidence and update human-facing
state after implementation is complete.

- [X] T074 [P] Run `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Evidence"` and save output to `specs/011-evidence-command/readiness/artifact-evidence-tests.txt`.
- [X] T075 [P] Run `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Evidence"` and save output to `specs/011-evidence-command/readiness/command-evidence-tests.txt`.
- [X] T076 [P] Run focused deterministic JSON, text projection, generated-view, and Governance boundary evidence tests and save output to `specs/011-evidence-command/readiness/output-boundary-tests.txt`.
- [X] T077 Run `dotnet build FS.GG.SDD.sln -c Release` and save output to `specs/011-evidence-command/readiness/build-release.txt`.
- [X] T078 Run `dotnet test FS.GG.SDD.sln -c Release` and save output to `specs/011-evidence-command/readiness/full-suite.txt`.
- [X] T079 Run `dotnet fsi scripts/prelude.fsx` after a Release build and save the transcript to `specs/011-evidence-command/readiness/fsi-public-surface.txt`.
- [X] T080 Run a disposable-project CLI JSON smoke scenario for `fsgg-sdd evidence` and save output to `specs/011-evidence-command/readiness/cli-json-smoke.txt`.
- [X] T081 Run a disposable-project CLI dry-run smoke scenario for `fsgg-sdd evidence --dry-run` and save output to `specs/011-evidence-command/readiness/cli-dry-run-smoke.txt`.
- [X] T082 Run a disposable-project CLI text smoke scenario for `fsgg-sdd evidence --text`, save output to `specs/011-evidence-command/readiness/cli-text-smoke.txt`, and record human-summary review notes in `specs/011-evidence-command/readiness/human-summary-review.md`.
- [X] T083 Record create, compatible-update, and rerun performance evidence for `evidence-create`, `evidence-compatible-update`, and `evidence-rerun-current` in `specs/011-evidence-command/readiness/performance.md`.
- [X] T084 Record SDD/Governance boundary review findings in `specs/011-evidence-command/readiness/sdd-governance-boundary.md`.
- [X] T085 Record artifact traceability from `specs/011-evidence-command/spec.md` requirements to plan decisions, tasks, tests, and readiness evidence in `specs/011-evidence-command/readiness/artifact-traceability.md`.
- [X] T086 Update `docs/initial-implementation-plan.md` to mark `fsgg-sdd evidence` complete and reference `specs/011-evidence-command/readiness/`.
- [X] T087 Update `README.md`, `AGENTS.md`, and `CLAUDE.md` only where needed to keep the current command state and Claude/Codex guidance synchronized after the evidence workflow behavior changes.

**Checkpoint**: The implementation has build, test, FSI, CLI, performance,
boundary, and traceability evidence.

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 depends on Phase 2 and is the MVP scope.
- Phase 4 depends on Phase 3 because preservation and update behavior requires loaded evidence facts and the base evidence report shape.
- Phase 5 depends on Phases 3 and 4 because diagnostics must cover success, blocked, and unsafe-update paths.
- Phase 6 depends on Phases 3 through 5 because output contracts must include success, blocked, dry-run, no-Governance, and preservation states.
- Phase 7 depends on the implemented story scope being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2; no dependency on other stories.
- **US2 (P1)**: Depends on US1 evidence loading, declaration shape, and generated-view state.
- **US3 (P2)**: Depends on US1 source loading and US2 safe-refusal behavior.
- **US4 (P3)**: Depends on evidence summaries, diagnostics, generated-view reporting, and preservation behavior from US1 through US3.

### Cross-Task Dependencies

- T011 depends on T010.
- T012 and T013 depend on the relevant public-surface decisions from T010 and T011.
- T015 depends on T010 through T013 and must run before implementation-body tasks T017 through T020.
- T016 depends on T010 through T015.
- T017 through T020 depend on the FSI/prelude exercise in T015.
- T026 through T035 depend on T017 through T020.
- T041 through T051 depend on T036 through T040.
- T057 through T063 depend on T052 through T056.
- T069 through T073 depend on T064 through T068.
- T074 through T085 depend on all selected implementation tasks passing.
- T086 and T087 depend on readiness evidence from T074 through T085.

## Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001 and T002.
- T008, T009, and T014 can run in parallel because they touch different test files.
- T021 through T025 can run in parallel because each task touches a different test file.
- T038, T039, and T040 can run in parallel with the `EvidenceCommandTests.fs` tasks in T036 and T037.
- T054, T055, and T056 can run in parallel with the `EvidenceCommandTests.fs` tasks in T052 and T053.
- T064, T065, and T066 can run in parallel because they touch different output and boundary test files.
- T074, T075, and T076 can run in parallel after implementation is complete.

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3.
3. Run the focused artifact and command evidence tests for US1.
4. Validate that `fsgg-sdd evidence` creates or updates
   `work/<id>/evidence.yml`, reports evidence readiness, refreshes or
   diagnoses `readiness/<id>/work-model.json`, works without Governance, and
   points evidence-ready work to verify.

### Incremental Delivery

1. US1 creates the native evidence declaration and success report.
2. US2 preserves existing declarations and blocks unsafe updates.
3. US3 diagnoses missing or invalid evidence without unsafe mutation.
4. US4 locks down deterministic JSON/text, CLI smoke paths, and optional
   Governance boundaries.
5. Phase 7 records readiness evidence and updates project guidance.

## Task Counts

- Shared setup, foundation, and polish tasks: 34
- US1 tasks: 15
- US2 tasks: 16
- US3 tasks: 12
- US4 tasks: 10
- Total tasks: 87

## Suggested MVP Scope

Complete Phases 1, 2, and 3 first. That delivers User Story 1: a usable
`fsgg-sdd evidence` command that creates or updates authored evidence
declarations, maps obligations to evidence dispositions, reports generated-view
state, preserves prerequisite sources, works without Governance, and points
evidence-ready work to verify.
