# Tasks: Charter Command

**Feature branch**: `004-charter-command`
**Spec**: `specs/004-charter-command/spec.md`
**Plan**: `specs/004-charter-command/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/004-charter-command/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/`, `quickstart.md`

## Status Legend

- `[ ]` - pending
- `[X]` - done with real evidence, or with synthetic evidence disclosed per
  Principle VI
- `[-]` - skipped, with written rationale on the task line

Never mark a failing task `[X]`. Never weaken an assertion to green a build;
narrow the scope and document it.

## Task Annotations

- `[P]` - parallel-safe, with no dependency on another incomplete task in this
  phase
- `[US1]`, `[US2]`, `[US3]`, `[US4]` - user-story scope
- `[T1]` / `[T2]` - tier annotation, omitted here because the feature overall
  is Tier 1

Phases run in sequence. Tasks within a phase may run in parallel when marked
`[P]`. This feature is a stateful, filesystem-changing lifecycle command, so
Principle V applies: `.fsi` contracts must declare the command model, messages,
effects, reports, and interpreter boundary before `.fs` bodies harden; story
completion requires pure transition tests, emitted-effect assertions, and real
interpreter evidence where safe.

---

## Phase 1: Setup

**Purpose**: Add the missing charter test, fixture, and readiness roots without
adding new source projects.

- [X] T001 Update
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` and
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` before
  `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`.
- [X] T002 [P] Create `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs`
  with a `FS.GG.SDD.Commands.Tests.CharterCommandTests` module skeleton.
- [X] T003 [P] Create
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` with a
  `FS.GG.SDD.Commands.Tests.GeneratedViewCommandTests` module skeleton.
- [X] T004 [P] Create `specs/004-charter-command/readiness/README.md`
  documenting the expected build, focused test, generated-view,
  deterministic-report, text-projection, Governance-boundary, FSI, CLI smoke,
  and full-suite evidence files.

**Checkpoint**: Test and readiness files exist; foundation work can add public
contracts and failing tests.

---

## Phase 2: Foundation

**Purpose**: Declare public command/report contracts, shared diagnostics, MVU
boundaries, and reusable test helpers before any charter implementation body is
completed.

- [X] T005 Draft charter-facing request, selected-work, write-plan,
  generated-view, and safe-update contract additions in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`.
- [X] T006 Mirror the new public contract shapes from T005 in
  `src/FS.GG.SDD.Commands/CommandTypes.fs` with compiling placeholder behavior
  only.
- [X] T007 Draft charter diagnostic builders and report helper signatures for
  outside-project, missing or malformed project config, duplicate work id,
  charter identity mismatch, malformed charter front matter, unsafe overwrite,
  stale generated view, malformed generated view, and blocked generated-view
  refresh in `src/FS.GG.SDD.Commands/CommandReports.fsi`.
- [X] T008 Mirror the diagnostic and report helper signatures from T007 in
  `src/FS.GG.SDD.Commands/CommandReports.fs` with stable ids and compiling
  placeholder behavior.
- [X] T009 Draft workflow signatures needed for multi-step charter execution in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fsi`, including pure transition
  access through `init` and `update` without filesystem mutation.
- [X] T010 Draft any additional interpreter signatures needed for read,
  enumerate, write, dry-run, and real filesystem evidence in
  `src/FS.GG.SDD.Commands/CommandEffects.fsi`.
- [X] T011 Add shared fixture-copy, report-runner, relative-path,
  digest-assertion, diagnostic-assertion, and charter request helpers in
  `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [X] T012 Add failing pure MVU boundary tests for `Charter` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving project
  reads and work-item reads are requested as effects and no host filesystem
  writes happen inside `init` or `update`.
- [X] T013 Add failing emitted-effect tests for `Charter` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving blocking
  diagnostics prevent `WriteFile` effects and interpreted read snapshots feed
  back through `EffectInterpreted`.
- [X] T014 Update `scripts/prelude.fsx` to construct a `charter-create`
  `Charter` command request, call `CommandWorkflow.init` and
  `CommandWorkflow.update`, and print command name, outcome, changed artifact
  count, generated-view count, blocking diagnostic count, and next action.

**Checkpoint**: Charter public surface is declared in `.fsi`, reusable tests
can run through the MVU boundary, and implementation can proceed story by
story.

---

## Phase 3: User Story 1 - Create A Work Charter (Priority: P1) - MVP

**Goal**: Create `work/<id>/charter.md` in an initialized SDD project, report
the selected work item as chartered, and point to `specify` without requiring
Governance.

**Independent Test**: Run the charter create tests against an initialized
fixture and a temporary-directory interpreter path, confirming charter front
matter, standard sections, command report, generated-view state, and next
action.

### Tests First

- [X] T015 [P] [US1] Populate
  `tests/fixtures/lifecycle-commands/charter-create/manifest.yml` with
  initialized `.fsgg/` inputs, expected `work/004-charter-command/charter.md`,
  and golden charter command report paths for the create scenario.
- [X] T016 [US1] Add failing successful-create tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` for valid front
  matter, required standard sections, changed artifact path
  `work/004-charter-command/charter.md`, successful outcome, and next action
  `specify`.
- [X] T017 [US1] Add failing no-Governance create tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` proving absent
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` do not
  block charter creation.
- [X] T018 [US1] Add failing pure transition tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` proving `Charter`
  plans reads for `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`,
  and `work/004-charter-command/charter.md` before planning any authored write.
- [X] T019 [US1] Add failing real interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` proving
  `CommandEffects` creates `work/004-charter-command/charter.md` in a
  temporary initialized project.

### Implementation

- [X] T020 [US1] Implement initialized-project loading and selected work-id
  validation for `Charter` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`
  after T016 through T018.
- [X] T021 [US1] Implement deterministic new-charter template generation for
  `work/<id>/charter.md` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`,
  including YAML front matter and the Identity, Principles, Scope Boundaries,
  Policy Pointers, and Lifecycle Notes sections.
- [X] T022 [US1] Implement create-write planning for missing
  `work/<id>/charter.md` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after
  T020 and T021.
- [X] T023 [US1] Implement charter changed-artifact reporting, generated-view
  placeholder state, and `specify` next-action required artifact
  `work/<id>/charter.md` in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T024 [US1] Implement CLI effect dispatch for multi-step charter
  workflows in `src/FS.GG.SDD.Cli/Program.fs`, continuing until the workflow
  reaches a final report instead of interpreting only the first effect batch.
- [X] T025 [US1] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CharterCommand"`
  and capture successful-create evidence in
  `specs/004-charter-command/readiness/charter-create-tests.txt`.

**Checkpoint**: User Story 1 is independently testable and is the suggested MVP
scope for a first implementation slice.

---

## Phase 4: User Story 2 - Re-Run Or Update A Charter Safely (Priority: P1)

**Goal**: Preserve authored charter content on rerun, add missing standard
sections only when proven safe, and refuse identity mismatches or destructive
updates before filesystem mutation.

**Independent Test**: Run the safe-rerun fixtures and confirm existing prose is
preserved byte-for-byte, safe additions are deterministic, and unsafe conflicts
produce blocking diagnostics with no authored writes.

### Tests First

- [X] T026 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/charter-rerun-preserves-content/manifest.yml`
  with an existing charter containing user-authored principles and expected
  preserve or no-change report fixtures.
- [X] T027 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/charter-adds-missing-sections/manifest.yml`
  with an existing charter missing standard sections and expected deterministic
  safe-addition output.
- [X] T028 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/charter-identity-mismatch/manifest.yml`
  with conflicting front matter and expected blocking report fixtures.
- [X] T029 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/unsafe-overwrite/manifest.yml` with an
  existing authored charter that would require destructive rewrite and expected
  refusal diagnostics.
- [X] T030 [US2] Add failing rerun preservation and no-change tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/charter-rerun-preserves-content/`.
- [X] T031 [US2] Add failing safe-section-addition tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/charter-adds-missing-sections/`.
- [X] T032 [US2] Add failing identity-mismatch and unsafe-overwrite tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` proving existing
  charter bytes are unchanged when reports are blocked.
- [X] T033 [US2] Add failing blocking-effect tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` proving
  `charterIdentityMismatch`, `malformedCharterFrontMatter`, and
  `unsafeOverwrite` diagnostics prevent authored `WriteFile` effects.

### Implementation

- [X] T034 [US2] Implement charter front-matter parsing and validation in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` for `schemaVersion`, `workId`,
  `title`, `stage`, `changeTier`, `status`, and optional `policyPointers`.
- [X] T035 [US2] Implement charter body section scanning and deterministic
  missing-section insertion in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`
  without rewriting existing section prose.
- [X] T036 [US2] Implement safe rerun write-plan decisions `preserve`,
  `noChange`, `update`, and `refuse` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T037 [US2] Implement `charterIdentityMismatch`,
  `malformedCharterFrontMatter`, and charter-specific `unsafeOverwrite`
  diagnostics in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T038 [US2] Ensure authored charter dry-run and real-write interpretation
  preserve existing content unless the charter write plan is safe in
  `src/FS.GG.SDD.Commands/CommandEffects.fs`.
- [X] T039 [US2] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CharterCommand"`
  and capture safe-rerun evidence in
  `specs/004-charter-command/readiness/charter-rerun-tests.txt`.

**Checkpoint**: User Stories 1 and 2 both work independently; charter creation
is safe to rerun on authored sources.

---

## Phase 5: User Story 3 - Diagnose Invalid Charter Requests (Priority: P2)

**Goal**: Fail invalid charter requests with stable, actionable diagnostics and
no unsafe writes.

**Independent Test**: Run invalid project, malformed work id, malformed
artifact, duplicate work id, and generated-view fixtures, confirming every
blocked report names the affected artifact and correction.

### Tests First

- [X] T040 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/outside-project/manifest.yml` with the
  charter outside-project scenario and expected blocked report fixture.
- [X] T041 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml` with the
  charter malformed work-id scenario and expected blocked report fixture.
- [X] T042 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/malformed-artifact/manifest.yml` with
  malformed `.fsgg/` config and malformed charter front matter cases.
- [X] T043 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml` with
  duplicate logical work-id source candidates for charter.
- [X] T044 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml` with
  missing, stale, malformed, and blocked `readiness/<id>/work-model.json`
  cases.
- [X] T045 [US3] Add failing invalid-request tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` for outside project,
  missing project config, malformed project config, missing work id, and
  malformed work id.
- [X] T046 [US3] Add failing duplicate-work-id tests in
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` proving duplicate
  logical ids block before `work/<id>/charter.md` changes.
- [X] T047 [US3] Add failing generated-view reporting tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` for missing,
  stale, malformed, and blocked `readiness/<id>/work-model.json` states.

### Implementation

- [X] T048 [US3] Implement project prerequisite reads and parsing diagnostics
  for `.fsgg/project.yml`, `.fsgg/sdd.yml`, and `.fsgg/agents.yml` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` using
  `FS.GG.SDD.Artifacts.LifecycleArtifacts` parsers.
- [X] T049 [US3] Implement duplicate logical work-id detection for charter
  sources under the configured work root in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T050 [US3] Implement generated work-model refresh and currency planning
  for `readiness/<id>/work-model.json` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` using
  `FS.GG.SDD.Artifacts.Serialization.generateWorkModel` and
  `checkGeneratedWorkModelCurrency` where source data is sufficient.
- [X] T051 [US3] Map generated-view currency, source digests, output digests,
  and diagnostic ids into charter command reports in
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T052 [US3] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GeneratedViewCommand"`
  and capture generated-view evidence in
  `specs/004-charter-command/readiness/generated-view-tests.txt`.
- [X] T053 [US3] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CharterCommand"`
  and capture invalid-request evidence in
  `specs/004-charter-command/readiness/charter-diagnostics-tests.txt`.

**Checkpoint**: Invalid charter requests are diagnosed before mutation, and
generated-view presence is never treated as proof of currency.

---

## Phase 6: User Story 4 - Keep Charter Output Traceable (Priority: P3)

**Goal**: Produce deterministic JSON, text projections from the same report
object, and optional Governance compatibility facts without evaluating
Governance policy.

**Independent Test**: Run three identical dry-run charter reports, text
projection tests, and Governance boundary tests; compare report bytes and
assert no route, freshness, profile, gate, audit, protected-boundary, or
release verdict appears.

### Tests First

- [X] T054 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml` with
  charter dry-run inputs and expected byte-stable report fixture paths.
- [X] T055 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/text-projection/manifest.yml` with
  charter JSON and text projection expectations.
- [X] T056 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml` with
  absent, present, malformed, and incomplete optional Governance pointer
  scenarios for charter.
- [X] T057 [US4] Add failing charter deterministic-report tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs` proving three
  dry-run executions over identical charter inputs produce byte-identical JSON.
- [X] T058 [US4] Add failing charter text-projection tests in
  `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs` proving text output
  contains only facts from `CommandReport`.
- [X] T059 [US4] Add failing charter Governance-boundary tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs` proving
  optional Governance pointers are compatibility facts only.

### Implementation

- [X] T060 [US4] Ensure charter report JSON property order, list sorting, UTF-8
  output, digest formatting, and path normalization in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` match
  `specs/004-charter-command/contracts/charter-report-json.md`.
- [X] T061 [US4] Ensure charter text output in
  `src/FS.GG.SDD.Commands/CommandRendering.fs` renders command, outcome,
  changed artifact count, generated-view count, diagnostic count, and next
  action from `CommandReport` only.
- [X] T062 [US4] Ensure charter Governance compatibility facts in
  `src/FS.GG.SDD.Commands/CommandReports.fs` expose optional policy,
  capabilities, and tooling pointers without route, freshness, profile, gate,
  protected-boundary, audit, or release verdicts.
- [X] T063 [US4] Update
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` after intentional
  charter public API changes and keep `SurfaceBaselineTests.fs` passing.
- [X] T064 [US4] Run `dotnet fsi scripts/prelude.fsx` and capture FSI evidence
  in
  `specs/004-charter-command/readiness/fsi-charter-public-surface.txt`.
- [X] T065 [US4] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson|FullyQualifiedName~TextProjection|FullyQualifiedName~GovernanceBoundaryCommand"`
  and capture traceability evidence in
  `specs/004-charter-command/readiness/traceable-output-tests.txt`.

**Checkpoint**: Charter output is deterministic, text is a projection, and
Governance integration remains advisory.

---

## Phase 7: Polish And Evidence

**Purpose**: Run the quickstart validation path, record evidence, and verify
the feature stays inside the SDD/Governance boundary.

- [X] T066 Run `dotnet build FS.GG.SDD.sln -c Release` and capture build
  evidence in `specs/004-charter-command/readiness/build.txt`.
- [X] T067 Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandWorkflow"`
  and capture MVU boundary evidence in
  `specs/004-charter-command/readiness/command-workflow-tests.txt`.
- [X] T068 Run the disposable-directory CLI smoke path from
  `specs/004-charter-command/quickstart.md` and capture output in
  `specs/004-charter-command/readiness/charter-cli-smoke.txt`.
- [X] T069 Run `dotnet test FS.GG.SDD.sln` and capture full-suite evidence in
  `specs/004-charter-command/readiness/full-test-suite.txt`.
- [X] T070 Review charter reports and text output for SDD/Governance ownership
  boundaries and record the result in
  `specs/004-charter-command/readiness/governance-boundary-review.txt`.
- [X] T071 Record final task-to-artifact traceability for
  `work/<id>/charter.md`, `readiness/<id>/work-model.json`, command JSON,
  text projection, diagnostics, and optional Governance facts in
  `specs/004-charter-command/readiness/artifact-traceability.txt`.
- [X] T072 Run the charter performance evidence path for the `charter-create`
  and `charter-rerun-preserves-content` fixture scenarios, confirm each command
  run completes under 2 seconds through the command test harness on the local
  development machine, and capture evidence in
  `specs/004-charter-command/readiness/performance.txt`.
- [X] T073 Review the human-readable charter summary against SC-006, confirming
  changed artifact, blocking diagnostic, generated-view state, and next action
  are identifiable from text-projection output, and capture evidence in
  `specs/004-charter-command/readiness/human-summary-review.txt`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup** has no dependencies.
- **Phase 2: Foundation** depends on Phase 1 and blocks all user-story work.
- **Phase 3: User Story 1** depends on Phase 2 and is the MVP slice.
- **Phase 4: User Story 2** depends on Phase 3 because rerun safety builds on
  create-write planning.
- **Phase 5: User Story 3** depends on Phase 3 and can run in parallel with
  Phase 4 after shared charter parsing contracts stabilize.
- **Phase 6: User Story 4** depends on report shapes from Phases 3 through 5.
- **Phase 7: Polish And Evidence** depends on the desired story phases being
  complete.

### User Story Dependencies

- **US1 - Create A Work Charter**: Starts after Foundation; no dependency on
  later stories.
- **US2 - Re-Run Or Update A Charter Safely**: Depends on US1 charter create
  planning and adds preservation/refusal behavior.
- **US3 - Diagnose Invalid Charter Requests**: Depends on Foundation and US1
  command routing; generated-view refresh work can proceed once project-loading
  snapshots exist.
- **US4 - Keep Charter Output Traceable**: Depends on report data from US1,
  US2, and US3.

### Parallel Opportunities

- T002, T003, and T004 can run in parallel after T001 is understood because
  they create separate files.
- Fixture population tasks T015, T026 through T029, T040 through T044, and
  T054 through T056 are parallel-safe by fixture root.
- Test tasks that edit different files can run in parallel; tasks editing
  `CharterCommandTests.fs` should be serialized.
- Generated-view work in T047, T050, and T051 can proceed in parallel with
  safe-rerun implementation once charter source snapshots are available.
- US4 fixture and test setup can start after report field names stabilize, but
  evidence tasks T064 and T065 wait for implementation.

---

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 for User Story 1.
3. Stop and validate with
   `specs/004-charter-command/readiness/charter-create-tests.txt`.

### Priority Completion

1. Add User Story 2 before treating charter reruns as safe for regular use.
2. Add User Story 3 before relying on charter in agent or CI workflows.
3. Add User Story 4 before publishing charter output as a stable automation
   contract.
4. Complete Phase 7 before merge.

## Notes

- Keep all public command behavior visible through `.fsi` files.
- Keep Markdown as the authoring surface and structured front matter/report JSON
  as the machine contracts.
- Do not add `specify`, `clarify`, `checklist`, `plan`, `tasks`, `analyze`,
  evidence update, verify, ship, release, generated agent guidance, or
  Governance enforcement behavior in this feature.
