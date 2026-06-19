# Tasks: Plan Command

**Feature branch**: `008-plan-command`
**Spec**: `specs/008-plan-command/spec.md`
**Plan**: `specs/008-plan-command/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/008-plan-command/`

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
Principle V applies: `.fsi` contracts must declare plan ids, plan facts, the
command model/report additions, effects, `init`, `update`, and interpreter
boundary before `.fs` bodies harden; story completion requires pure transition
tests, emitted-effect assertions, and real interpreter evidence where safe.

---

## Phase 1: Setup

**Purpose**: Add the missing plan test, fixture, and readiness roots without
adding new source projects.

- [X] T001 Update
  `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Artifacts.Tests/PlanArtifactTests.fs` after
  `tests/FS.GG.SDD.Artifacts.Tests/ChecklistArtifactTests.fs` and before
  `tests/FS.GG.SDD.Artifacts.Tests/WorkModelTests.fs`.
- [X] T002 [P] Create
  `tests/FS.GG.SDD.Artifacts.Tests/PlanArtifactTests.fs` with a
  `FS.GG.SDD.Artifacts.Tests.PlanArtifactTests` module skeleton for plan front
  matter, source snapshot, plan id, contract reference, verification
  obligation, migration note, generated-view impact, accepted deferral, and
  safe-rerun parser tests.
- [X] T003 Update
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` after
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` and before
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T004 [P] Create
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` with a
  `FS.GG.SDD.Commands.Tests.PlanCommandTests` module skeleton.
- [X] T005 [P] Create `specs/008-plan-command/readiness/README.md`
  documenting the expected build, focused plan, MVU boundary, generated-view,
  dry-run, deterministic-report, text-projection, Governance-boundary, FSI, CLI
  smoke, performance, human-summary, traceability, and full-suite evidence
  files.

**Checkpoint**: Test and readiness files exist; foundation work can add public
contracts and failing tests.

---

## Phase 2: Foundation

**Purpose**: Declare public plan contracts, command-report shapes,
diagnostics, MVU boundaries, and reusable test helpers before any plan
implementation body is completed.

- [X] T006 Draft plan id contract additions in
  `src/FS.GG.SDD.Artifacts/Identifiers.fsi` for `PlanDecisionId`,
  `PlanContractReferenceId`, `VerificationObligationId`,
  `PlanMigrationNoteId`, `GeneratedViewImpactId`, their `create*` functions,
  and their `*Value` functions.
- [X] T007 Mirror the plan id contracts from T006 in
  `src/FS.GG.SDD.Artifacts/Identifiers.fs` with compiling placeholder
  validation for `PD-###`, `PC-###`, `VO-###`, `PM-###`, and `GV-###` shapes.
- [X] T008 Draft plan artifact parsing contract additions in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` for plan front matter,
  source snapshots, plan decisions, contract references, verification
  obligations, migration notes, generated-view impacts, accepted deferrals,
  planning findings, advisory notes, lifecycle notes, parsed plan facts,
  `planStandardSections`, and `parsePlanFacts`.
- [X] T009 Mirror the plan artifact contracts from T008 in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` with compiling placeholder
  behavior only.
- [X] T010 Draft plan report contract additions in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`, including `PlanSummary`, a
  `Plan` field on `CommandReport`, and a `Plan` field on `CommandModel`.
- [X] T011 Mirror the plan report contract additions from T010 in
  `src/FS.GG.SDD.Commands/CommandTypes.fs` with compiling placeholder
  behavior only.
- [X] T012 Draft plan diagnostic builders in
  `src/FS.GG.SDD.Commands/CommandReports.fsi` for
  `missingChecklistPrerequisite`, `failedChecklistPrerequisite`,
  `planIdentityMismatch`, `malformedPlanFrontMatter`, `duplicatePlanId`,
  `unknownPlanSourceReference`, `stalePlanDecision`,
  `unsafePlanDecisionChange`, and plan-specific next-action corrections.
- [X] T013 Mirror the plan diagnostic builders from T012 in
  `src/FS.GG.SDD.Commands/CommandReports.fs` with stable ids and compiling
  placeholder behavior.
- [X] T014 [P] Add failing plan id tests in
  `tests/FS.GG.SDD.Artifacts.Tests/IdentifierTests.fs` for valid and invalid
  `PD-###`, `PC-###`, `VO-###`, `PM-###`, and `GV-###` values.
- [X] T015 [P] Add failing plan artifact parser tests in
  `tests/FS.GG.SDD.Artifacts.Tests/PlanArtifactTests.fs` for required front
  matter, standard sections, source snapshots, plan decisions, contract
  references, verification obligations, migration notes, generated-view
  impacts, accepted deferrals, schema-version compatibility, diagnose-only
  migration posture, duplicate id detection, unknown-reference diagnostics,
  and stale source snapshot facts.
- [X] T016 Add plan request, plan runner, valid specification writer, valid
  clarification writer, valid checklist writer, existing plan writer, dry-run
  digest, and plan assertion helpers in
  `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [X] T017 Add failing pure MVU boundary tests for `Plan` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving project,
  specification, clarification, checklist, plan, generated-view, and
  work-directory reads are requested before authored or generated writes.
- [X] T018 Add failing emitted-effect tests for `Plan` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving blocking
  diagnostics prevent `WriteFile` effects and dry-run interpretation does not
  mutate `work/<id>/plan.md` or `readiness/<id>/work-model.json`.
- [X] T019 Update `scripts/prelude.fsx` to construct a `plan-create` `Plan`
  command request, call `CommandWorkflow.init` and `CommandWorkflow.update`,
  and print command name, outcome, changed artifact count, plan decision count,
  contract reference count, verification obligation count, accepted deferral
  count, stale decision count, generated-view count, blocking diagnostic
  count, and next action.
- [X] T020 [P] Update
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` with the new plan
  id and plan artifact contracts declared in `src/FS.GG.SDD.Artifacts/*.fsi`.
- [X] T021 [P] Update
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` with the new plan
  summary, command report, command model, and diagnostic contracts declared in
  `src/FS.GG.SDD.Commands/*.fsi`.

**Checkpoint**: Plan public surface is declared in `.fsi`, reusable tests can
run through the MVU boundary, and implementation can proceed story by story.

---

## Phase 3: User Story 1 - Create A Technical Plan (Priority: P1) - MVP

**Goal**: Create `work/<id>/plan.md` for a checklist-ready work item, record
source links, decisions, contract impact, verification obligations, migration
posture, generated-view impact, accepted deferrals, and next action `tasks`
without requiring Governance.

**Independent Test**: Run the plan create tests against an initialized
checklist-ready fixture and a temporary-directory interpreter path, confirming
plan front matter, standard sections, stable ids, command report,
generated-view state, no-Governance behavior, and next action.

### Tests First

- [X] T022 [P] [US1] Populate
  `tests/fixtures/lifecycle-commands/plan-create/manifest.yml` with
  initialized `.fsgg/` inputs, valid `work/008-plan-command/spec.md`,
  `work/008-plan-command/clarifications.md`,
  `work/008-plan-command/checklist.md` prerequisites, expected
  `work/008-plan-command/plan.md`, and golden plan command report paths for
  the create scenario.
- [X] T023 [US1] Add failing successful-create tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` for valid plan front
  matter, required standard sections, created artifact path
  `work/008-plan-command/plan.md`, parsed `PD-###`, `PC-###`, `VO-###`,
  `PM-###`, and `GV-###` ids, successful outcome, and next action `tasks`.
- [X] T024 [US1] Add failing no-Governance create tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` proving absent
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` do not
  block plan creation.
- [X] T025 [US1] Add failing generated-view create tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` proving `Plan`
  reports `readiness/008-plan-command/work-model.json`, refreshes it when
  source data is valid, and does not treat a missing generated file as current.
- [X] T026 [US1] Add failing real interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` proving
  `CommandEffects` creates `work/008-plan-command/plan.md` in a temporary
  initialized, chartered, specified, clarified, and checklist-ready project.

### Implementation

- [X] T027 [US1] Route `Plan` away from `unsupportedLifecycleCommand` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and plan the required project,
  specification, clarification, checklist, plan, generated-view, and
  duplicate-id source reads.
- [X] T028 [US1] Implement prerequisite validation for `Plan` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, requiring
  `work/<id>/spec.md` with `stage: specify`,
  `work/<id>/clarifications.md` with `stage: clarify`, and
  `work/<id>/checklist.md` with `stage: checklist` and checklist-ready state
  before plan writes.
- [X] T029 [US1] Implement source snapshot and plan-entry mapping in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` from parsed specification,
  clarification, and checklist facts to plan decisions, contract references,
  verification obligations, migration notes, generated-view impacts, accepted
  deferrals, and lifecycle notes.
- [X] T030 [US1] Implement deterministic new-plan template generation for
  `work/<id>/plan.md` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`,
  including YAML front matter and the Source Snapshot, Plan Scope, Plan
  Decisions, Contract Impact, Verification Obligations, Migration Posture,
  Generated View Impact, Accepted Deferrals, Planning Findings, Advisory
  Notes, and Lifecycle Notes sections.
- [X] T031 [US1] Implement create-write planning for missing
  `work/<id>/plan.md` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after
  T028 through T030.
- [X] T032 [US1] Implement plan changed-artifact reporting, plan summary
  reporting, generated-view state, Governance compatibility facts, and
  `tasks` next-action required artifacts in
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T033 [US1] Serialize the plan `plan` report object in documented order
  in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T034 [US1] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~PlanCommand"` and
  capture successful-create evidence in
  `specs/008-plan-command/readiness/plan-create-tests.txt`.

**Checkpoint**: User Story 1 is independently testable and is the suggested
MVP scope for a first implementation slice.

---

## Phase 4: User Story 2 - Preserve And Refresh Planning Decisions (Priority: P1)

**Goal**: Preserve authored plan content on rerun, append compatible planning
entries only when proven non-destructive, keep stable ids stable, carry
accepted deferrals forward, and mark source-dependent plan decisions stale
when source facts change.

**Independent Test**: Run the safe-rerun fixtures and confirm existing plan
decisions, contract references, verification obligations, migration notes,
generated-view impacts, accepted deferrals, advisory notes, lifecycle notes,
and stable ids are preserved; safe additions are deterministic; source changes
mark affected decisions stale; and unsafe conflicts produce blocking
diagnostics with no authored writes.

### Tests First

- [X] T035 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/plan-rerun-preserves-decisions/manifest.yml`
  with an existing plan artifact containing authored plan decisions, contract
  references, verification obligations, migration notes, generated-view
  impacts, accepted deferrals, advisory notes, lifecycle notes, and expected
  preserve or no-change report fixtures.
- [X] T036 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/plan-adds-missing-entries/manifest.yml`
  with compatible source additions and expected deterministic safe-addition
  output.
- [X] T037 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/plan-preserves-stable-ids/manifest.yml`
  with existing `PD-###`, `PC-###`, `VO-###`, `PM-###`, and `GV-###` ids and
  expected append-only id allocation.
- [X] T038 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/plan-accepted-deferral/manifest.yml` with
  clarification and checklist accepted deferrals and expected durable plan
  deferral facts.
- [X] T039 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/plan-stale-decision/manifest.yml` with a
  source requirement, clarification decision, or checklist result changed after
  an existing plan decision was recorded.
- [X] T040 [US2] Add failing rerun preservation and no-change tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/plan-rerun-preserves-decisions/`.
- [X] T041 [US2] Add failing safe-addition and stable-id tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/plan-adds-missing-entries/` and
  `tests/fixtures/lifecycle-commands/plan-preserves-stable-ids/`.
- [X] T042 [US2] Add failing accepted-deferral and stale-decision tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/plan-accepted-deferral/` and
  `tests/fixtures/lifecycle-commands/plan-stale-decision/`.

### Implementation

- [X] T043 [US2] Implement preservation of existing user-authored plan prose,
  standard sections, source links, plan decisions, contract references,
  verification obligations, migration notes, generated-view impacts, accepted
  deferrals, findings, notes, and stable ids in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T044 [US2] Implement deterministic append-only id allocation for missing
  plan decisions, contract references, verification obligations, migration
  notes, and generated-view impacts in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T045 [US2] Implement source snapshot digest comparison and stale decision
  marking in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` for changed
  specification, clarification, checklist, and accepted-deferral facts.
- [X] T046 [US2] Implement safe additions for new source-derived entries and
  accepted deferral visibility in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, preserving existing authored
  content and reporting update operations only when insertion is
  non-destructive.
- [X] T047 [US2] Implement unsafe removal, renumbering, or semantic decision
  change refusal in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and surface
  the blocking diagnostic through `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T048 [US2] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~PlanCommand"` and
  capture rerun, safe-addition, deferral, and stale-decision evidence in
  `specs/008-plan-command/readiness/plan-rerun-tests.txt`.

**Checkpoint**: User Stories 1 and 2 can be tested independently, and safe
reruns preserve durable planning decisions.

---

## Phase 5: User Story 3 - Diagnose Planning Readiness Problems (Priority: P2)

**Goal**: Block invalid or incomplete plan requests with actionable
diagnostics, preserve filesystem state on failure, and point the next action to
specification, clarification, checklist, or plan correction instead of task
generation.

**Independent Test**: Invoke `Plan` outside an SDD project, before checklist
readiness, with failed checklist results, malformed work ids, malformed plan
data, duplicate plan identifiers, unknown source references, stale generated
views, and unsafe overwrite situations, then confirm no unsafe write occurs and
each result contains a stable diagnostic and correction.

### Tests First

- [X] T049 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/missing-specification/manifest.yml` with
  a selected work item lacking `work/008-plan-command/spec.md` and expected no
  plan write.
- [X] T050 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/missing-clarification/manifest.yml` with
  a selected work item lacking `work/008-plan-command/clarifications.md` and
  expected no plan write.
- [X] T051 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/missing-checklist/manifest.yml` and
  `tests/fixtures/lifecycle-commands/failed-checklist/manifest.yml` with
  missing, failed blocking, and stale checklist prerequisite states.
- [X] T052 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`,
  `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml`, and
  `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml` with
  blocked plan request expectations.
- [X] T053 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/malformed-plan/manifest.yml`,
  `tests/fixtures/lifecycle-commands/duplicate-plan-id/manifest.yml`,
  `tests/fixtures/lifecycle-commands/unknown-source-reference/manifest.yml`,
  `tests/fixtures/lifecycle-commands/plan-identity-mismatch/manifest.yml`, and
  `tests/fixtures/lifecycle-commands/unsafe-overwrite/manifest.yml` with
  blocked existing-plan expectations.
- [X] T054 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml` with
  a generated work model whose source digest, schema version, or generator
  identity does not match the current authored sources.
- [X] T055 [US3] Add failing prerequisite tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` for outside-project
  execution, missing work id, malformed work id, duplicate logical work id,
  missing specification, missing clarification, and missing checklist.
- [X] T056 [US3] Add failing checklist-readiness tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` for failed blocking
  checklist results, stale checklist results, unresolved deferrals that block
  planning, and next action pointing back to checklist correction.
- [X] T057 [US3] Add failing existing-plan safety tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` for malformed plan
  front matter, duplicate plan ids, unknown source references, plan identity
  mismatch, unsafe overwrite, and unsafe decision change.
- [X] T058 [US3] Add failing generated-view diagnostic tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` for stale,
  malformed, and blocked `readiness/008-plan-command/work-model.json` states.

### Implementation

- [X] T059 [US3] Implement missing and malformed prerequisite diagnostics for
  `Plan` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`, covering outside-project,
  project config, work id, specification, clarification, checklist, and
  duplicate logical work id failures.
- [X] T060 [US3] Implement failed checklist, stale checklist result, missing
  required result, unknown checklist reference, and unresolved blocking
  deferral handling in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, ensuring
  `work/<id>/plan.md` is not written.
- [X] T061 [US3] Implement existing plan identity, front matter, duplicate id,
  missing required id, and unknown source reference validation in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T062 [US3] Implement unsafe overwrite and unsafe plan decision change
  refusal in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, preserving existing
  `work/<id>/plan.md` bytes on blocked outcomes.
- [X] T063 [US3] Implement plan generated-view currency diagnostics in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, distinguishing missing, stale,
  malformed, and blocked `readiness/<id>/work-model.json` states and naming
  the affected source artifact where available.
- [X] T064 [US3] Implement blocked-report next-action selection in
  `src/FS.GG.SDD.Commands/CommandReports.fs` so plan failures point to
  specification, clarification, checklist, or plan correction and do not point
  to `tasks` while blocking diagnostics remain.
- [X] T065 [US3] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~PlanCommand"` and
  capture blocked-readiness evidence in
  `specs/008-plan-command/readiness/plan-blocked-tests.txt`.

**Checkpoint**: User Story 3 can be tested independently, and all blocked
planning paths avoid unsafe filesystem mutation.

---

## Phase 6: User Story 4 - Keep Plan Output Traceable (Priority: P3)

**Goal**: Emit deterministic JSON and text projections from the same report
facts, keep optional Governance compatibility advisory only, and provide dry
run and CLI smoke evidence for humans, agents, CI, and optional Governance
consumers.

**Independent Test**: Run identical plan requests three times against the same
project state and confirm byte-identical JSON reports, text projections that
contain no facts absent from JSON, dry-run reports with no mutation, and
Governance compatibility facts without route, freshness, profile, gate, audit,
protected-boundary, or release verdicts.

### Tests First

- [X] T066 [P] [US4] Update
  `tests/fixtures/lifecycle-commands/dry-run/manifest.yml` with a plan dry-run
  scenario and `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`
  with a plan scenario that runs the same request three times over identical
  inputs.
- [X] T067 [P] [US4] Update
  `tests/fixtures/lifecycle-commands/text-projection/manifest.yml` with a plan
  text-output scenario whose expected facts are all present in the JSON report.
- [X] T068 [P] [US4] Update
  `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml` with a
  plan scenario covering absent and advisory optional Governance pointers.
- [X] T069 [US4] Add failing deterministic JSON tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs` proving repeated
  `Plan` reports are byte-identical, include the `plan` object, sort id lists,
  normalize paths to `/`, and exclude absolute host paths, timestamps, process
  ids, random values, terminal details, and directory enumeration order.
- [X] T070 [US4] Add failing text projection tests in
  `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs` proving plan text
  includes outcome, selected work id, changed artifacts, plan decision count,
  contract reference count, verification obligation count, accepted deferral
  count, stale decision count, generated-view state, diagnostics, and next
  action from the report only.
- [X] T071 [US4] Add failing Governance boundary tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs` proving
  `Plan` reports optional compatibility facts without route, freshness,
  profile, gate, audit, protected-boundary, or release verdict fields.
- [X] T072 [US4] Add failing dry-run mutation tests in
  `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs` proving `Plan` reports
  proposed authored and generated changes without mutating
  `work/008-plan-command/plan.md` or
  `readiness/008-plan-command/work-model.json`.

### Implementation

- [X] T073 [US4] Harden plan report serialization in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` so plan ids and generated
  view sources sort by stable keys and authoritative JSON excludes
  non-deterministic or host-specific values.
- [X] T074 [US4] Render plan text projection in
  `src/FS.GG.SDD.Commands/CommandRendering.fs` from `CommandReport.Plan`,
  `CommandReport.GeneratedViews`, `CommandReport.Diagnostics`, and
  `CommandReport.NextAction` only.
- [X] T075 [US4] Implement advisory-only Governance compatibility facts for
  `Plan` in `src/FS.GG.SDD.Commands/CommandReports.fs`, preserving
  no-Governance success and excluding Governance-owned route, freshness,
  profile, gate, audit, protected-boundary, and release semantics.
- [X] T076 [US4] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~Plan|FullyQualifiedName~CommandReportJson|FullyQualifiedName~TextProjection|FullyQualifiedName~GovernanceBoundary"`
  and capture deterministic-output, text-projection, dry-run, and
  Governance-boundary evidence in
  `specs/008-plan-command/readiness/plan-output-tests.txt`.

**Checkpoint**: All user stories are independently functional and report
through one deterministic command contract.

---

## Phase 7: Evidence, Readiness, And Cross-Cutting Verification

**Purpose**: Record release-quality evidence for the Tier 1 command surface and
ensure the feature does not introduce later lifecycle command behavior.

- [X] T077 Run `dotnet build FS.GG.SDD.sln -c Release` and capture output in
  `specs/008-plan-command/readiness/build-release.txt`.
- [X] T078 Run
  `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Plan"`
  and capture output in
  `specs/008-plan-command/readiness/artifact-plan-tests.txt`.
- [X] T079 Run
  `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Plan"`
  and capture output in
  `specs/008-plan-command/readiness/command-plan-tests.txt`.
- [X] T080 Run `dotnet test FS.GG.SDD.sln -c Release` and capture output in
  `specs/008-plan-command/readiness/full-suite.txt`.
- [X] T081 Run `dotnet fsi scripts/prelude.fsx` and capture public-surface
  evidence in `specs/008-plan-command/readiness/fsi-plan-surface.txt`.
- [X] T082 Run
  `dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- plan --work 008-plan-command --root <temporary-project-root>`
  against a disposable checklist-ready project and capture JSON smoke output
  in `specs/008-plan-command/readiness/cli-plan-json-smoke.txt`.
- [X] T083 Run
  `dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- plan --work 008-plan-command --root <temporary-project-root> --dry-run`
  against the same disposable project shape and capture dry-run output in
  `specs/008-plan-command/readiness/cli-plan-dry-run.txt`.
- [X] T084 Run
  `dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- plan --work 008-plan-command --root <temporary-project-root> --text`
  against the same disposable project shape and capture text output in
  `specs/008-plan-command/readiness/cli-plan-text-smoke.txt`.
- [X] T085 Record plan create and rerun performance evidence for the
  `plan-create` and `plan-rerun-preserves-decisions` fixture scenarios in
  `specs/008-plan-command/readiness/performance.txt`, confirming each scenario
  completes under 2 seconds in the command test harness.
- [X] T086 Record the SDD/Governance boundary review in
  `specs/008-plan-command/readiness/sdd-governance-boundary-review.md`,
  confirming `Plan` emits optional compatibility facts only and does not parse
  Governance schemas, select routes, evaluate freshness, adjust profiles,
  select gates, enforce protected boundaries, or produce release verdicts.
- [X] T087 Record the human text-summary review in
  `specs/008-plan-command/readiness/human-summary-review.md`, confirming the
  text projection exposes changed artifact count, plan decision count,
  contract reference count, verification obligation count, accepted deferral
  count, stale decision count, generated-view state, diagnostics, and next
  action from JSON report facts.
- [X] T088 Record artifact traceability in
  `specs/008-plan-command/readiness/artifact-traceability.md`, mapping
  `specs/008-plan-command/spec.md`, `specs/008-plan-command/plan.md`,
  `specs/008-plan-command/data-model.md`, `specs/008-plan-command/contracts/`,
  `work/<id>/plan.md`, `readiness/<id>/work-model.json`, command report JSON,
  and readiness evidence to the tasks that implemented them.
- [X] T089 Review `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs` to confirm this feature does not
  introduce `tasks`, `analyze`, evidence update, verify, ship, release,
  generated agent guidance, Governance route selection, freshness evaluation,
  profile adjustment, gate selection, or enforcement behavior.

**Checkpoint**: Build, tests, FSI/prelude, CLI smoke, performance, boundary,
human-summary, traceability, and out-of-scope evidence are recorded.

---

## Dependencies And Execution Order

### Phase Dependencies

- **Phase 1: Setup** has no dependencies.
- **Phase 2: Foundation** depends on Phase 1 and blocks all user-story
  implementation.
- **Phase 3: US1** depends on Phase 2 and is the MVP slice.
- **Phase 4: US2** depends on Phase 3 because reruns need a create path and
  parsed plan summary first.
- **Phase 5: US3** depends on Phase 2 for diagnostics and may begin once the
  relevant parser/report contracts exist; blocked cases should be completed
  before final readiness.
- **Phase 6: US4** depends on Phase 3 for a populated plan report and should
  finish after US2 and US3 so deterministic outputs cover success, warning,
  and blocked states.
- **Phase 7: Evidence** may record interim evidence after a selected
  user-story slice is complete, but final feature readiness evidence depends on
  US1 through US4 being complete.

### User Story Dependencies

- **US1 (P1)**: Starts after Foundation; no dependency on other user stories.
- **US2 (P1)**: Depends on US1 create behavior and plan parsing.
- **US3 (P2)**: Depends on Foundation diagnostics; blocked-prerequisite tests
  can run in parallel with US1, while existing-plan safety cases depend on
  plan parsing from US1.
- **US4 (P3)**: Depends on report shape from US1 and should include final US2
  and US3 outcomes before evidence is complete.

### Within Each User Story

- Tests must be written and fail before implementation.
- `.fsi` public contracts precede `.fs` implementation.
- Pure transition tests precede interpreter evidence.
- Story implementation must complete before recording that story's readiness
  evidence.

## Parallel Opportunities

- T002, T004, and T005 can run in parallel after T001 and T003 are assigned.
- T014, T015, T020, and T021 can run in parallel after T006 through T013
  declare the public surface.
- T022, T024, and T025 can run in parallel once TestSupport helpers from T016
  exist.
- T035 through T039 can run in parallel because each fixture family is isolated.
- T049 through T054 can run in parallel because each blocked fixture family is
  isolated.
- T066 through T068 can run in parallel because dry-run/deterministic, text, and
  Governance boundary fixtures occupy separate fixture scenarios.
- T077 through T084 must run after implementation, but T086 through T089 can
  be drafted in parallel once the relevant evidence files exist.

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 for User Story 1.
3. Stop and validate `fsgg-sdd plan` create behavior independently with
   focused tests and CLI smoke evidence.

### Incremental Delivery

1. Add US1 create behavior and prove `tasks` next action.
2. Add US2 rerun preservation, safe additions, accepted deferrals, and stale
   decision state.
3. Add US3 blocked diagnostics and no-mutation guarantees.
4. Add US4 deterministic output, text projection, dry-run, and Governance
   boundary evidence.
5. Run Phase 7 readiness evidence before implementation is marked complete.
