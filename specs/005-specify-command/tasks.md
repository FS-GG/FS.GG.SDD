# Tasks: Specify Command

**Feature branch**: `005-specify-command`
**Spec**: `specs/005-specify-command/spec.md`
**Plan**: `specs/005-specify-command/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/005-specify-command/`

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
Principle V applies: `.fsi` contracts must declare the specification ids,
command model, messages, effects, reports, and interpreter boundary before
`.fs` bodies harden; story completion requires pure transition tests,
emitted-effect assertions, and real interpreter evidence where safe.

---

## Phase 1: Setup

**Purpose**: Add the missing specify test, fixture, and readiness roots without
adding new source projects.

- [X] T001 Update
  `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Artifacts.Tests/SpecificationArtifactTests.fs` before
  `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs`.
- [X] T002 [P] Create
  `tests/FS.GG.SDD.Artifacts.Tests/SpecificationArtifactTests.fs` with a
  `FS.GG.SDD.Artifacts.Tests.SpecificationArtifactTests` module skeleton for
  specification id and parser tests.
- [X] T003 Update
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` after
  `tests/FS.GG.SDD.Commands.Tests/CharterCommandTests.fs` and before
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T004 [P] Create
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` with a
  `FS.GG.SDD.Commands.Tests.SpecifyCommandTests` module skeleton.
- [X] T005 [P] Create `specs/005-specify-command/readiness/README.md`
  documenting the expected build, focused specify, MVU boundary,
  generated-view, deterministic-report, text-projection, Governance-boundary,
  dry-run, FSI, CLI smoke, performance, human-summary, traceability, and
  full-suite evidence files.

**Checkpoint**: Test and readiness files exist; foundation work can add public
contracts and failing tests.

---

## Phase 2: Foundation

**Purpose**: Declare public specification contracts, command-report shapes,
diagnostics, MVU boundaries, and reusable test helpers before any specify
implementation body is completed.

- [X] T006 Draft specification id contract additions in
  `src/FS.GG.SDD.Artifacts/Identifiers.fsi` for `UserStoryId`,
  `AcceptanceScenarioId`, `ScopeBoundaryId`, `AmbiguityId`, constructors, and
  value accessors.
- [X] T007 Mirror the specification id contracts from T006 in
  `src/FS.GG.SDD.Artifacts/Identifiers.fs` with compiling placeholder
  validation only.
- [X] T008 Draft specification artifact parsing contract additions in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` for front matter, standard
  sections, user stories, acceptance scenarios, scope boundaries, ambiguity
  records, structured requirement reference links to stories or acceptance
  scenarios, and parsed specification facts.
- [X] T009 Mirror the specification artifact contracts from T008 in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` with compiling placeholder
  behavior only.
- [X] T010 Draft specify report contract additions in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`, including a specification summary
  that can carry work id, stage, status, story ids, requirement ids,
  acceptance-scenario ids, ambiguity ids, and unresolved ambiguity count.
- [X] T011 Mirror the specify report contract additions from T010 in
  `src/FS.GG.SDD.Commands/CommandTypes.fs` with compiling placeholder
  behavior only.
- [X] T012 Draft specify diagnostic builders in
  `src/FS.GG.SDD.Commands/CommandReports.fsi` for
  `missingCharterPrerequisite`, `missingSpecificationIntent`,
  `specificationIdentityMismatch`, `malformedSpecificationFrontMatter`,
  `duplicateWorkId`, `duplicateSpecificationId`, `missingSpecificationId`,
  `unknownSpecificationReference`, and `staleGeneratedView`.
- [X] T013 Mirror the specify diagnostic builders from T012 in
  `src/FS.GG.SDD.Commands/CommandReports.fs` with stable ids and compiling
  placeholder behavior.
- [X] T014 [P] Add failing specification id tests in
  `tests/FS.GG.SDD.Artifacts.Tests/IdentifierTests.fs` for valid and invalid
  `US-###`, `AC-###`, `SB-###`, and `AMB-###` values.
- [X] T015 [P] Add failing specification artifact parser tests in
  `tests/FS.GG.SDD.Artifacts.Tests/SpecificationArtifactTests.fs` for required
  front matter, standard sections, stable id extraction, duplicate id
  detection, requirement-line compatibility with `- FR-###:` lines, structured
  story or acceptance reference extraction, and unknown-reference diagnostics.
- [X] T016 Add specify request, specify runner, valid-charter writer,
  valid-specification writer, dry-run digest, and specification assertion
  helpers in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [X] T017 Add failing pure MVU boundary tests for `Specify` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving project,
  charter, specification, task, evidence, generated-view, and work-directory
  reads are requested before authored or generated writes.
- [X] T018 Add failing emitted-effect tests for `Specify` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving blocking
  diagnostics prevent `WriteFile` effects and dry-run interpretation does not
  mutate authored or generated artifacts.
- [X] T019 Update `scripts/prelude.fsx` to construct a `specify-create`
  `Specify` command request, call `CommandWorkflow.init` and
  `CommandWorkflow.update`, and print command name, outcome, changed artifact
  count, parsed specification fact count, generated-view count, blocking
  diagnostic count, and next action.

**Checkpoint**: Specify public surface is declared in `.fsi`, reusable tests
can run through the MVU boundary, and implementation can proceed story by
story.

---

## Phase 3: User Story 1 - Create A Work Specification (Priority: P1) - MVP

**Goal**: Create `work/<id>/spec.md` for a chartered work item, report parsed
specification facts, report generated-view state, and point to `clarify`
without requiring Governance.

**Independent Test**: Run the specify create tests against an initialized
chartered fixture and a temporary-directory interpreter path, confirming
specification front matter, standard sections, stable ids, command report,
generated-view state, and next action.

### Tests First

- [X] T020 [P] [US1] Populate
  `tests/fixtures/lifecycle-commands/specify-create/manifest.yml` with
  initialized `.fsgg/` inputs, a valid `work/005-specify-command/charter.md`
  prerequisite, expected `work/005-specify-command/spec.md`, and golden specify
  command report paths for the create scenario.
- [X] T021 [US1] Add failing successful-create tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` for valid
  specification front matter, required standard sections, created artifact path
  `work/005-specify-command/spec.md`, parsed specification ids, successful
  outcome, and next action `clarify`.
- [X] T022 [US1] Add failing no-Governance create tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` proving absent
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` do not
  block specification creation.
- [X] T023 [US1] Add failing generated-view create tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` proving
  `Specify` reports `readiness/005-specify-command/work-model.json` and does
  not treat a missing generated file as current.
- [X] T024 [US1] Add failing real interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` proving
  `CommandEffects` creates `work/005-specify-command/spec.md` in a temporary
  initialized and chartered project.

### Implementation

- [X] T025 [US1] Route `Specify` away from `unsupportedLifecycleCommand` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and plan the required project,
  charter, specification, task, evidence, generated-view, and duplicate-id
  source reads.
- [X] T026 [US1] Implement charter prerequisite validation for `Specify` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, requiring
  `work/<id>/charter.md` front matter with matching work id and `stage:
  charter` before specification writes.
- [X] T027 [US1] Implement specification intent normalization in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, extracting user value, at least
  one in-scope statement, and at least one measurable requirement from
  `CommandRequest.InputText`.
- [X] T028 [US1] Implement deterministic new-specification template generation
  for `work/<id>/spec.md` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, including YAML front matter and
  the User Value, Scope, Non-Goals, User Stories, Acceptance Scenarios,
  Functional Requirements, Ambiguities, Public Or Tool-Facing Impact, and
  Lifecycle Notes sections.
- [X] T029 [US1] Implement create-write planning for missing
  `work/<id>/spec.md` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after T026
  through T028.
- [X] T030 [US1] Implement specify changed-artifact reporting, specification
  summary reporting, generated-view placeholder state, and `clarify`
  next-action required artifacts in
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T031 [US1] Serialize the specify `specification` report object in
  documented order in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T032 [US1] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~SpecifyCommand"` and
  capture successful-create evidence in
  `specs/005-specify-command/readiness/specify-create-tests.txt`.

**Checkpoint**: User Story 1 is independently testable and is the suggested MVP
scope for a first implementation slice.

---

## Phase 4: User Story 2 - Preserve And Refine Existing Specification Content (Priority: P1)

**Goal**: Preserve authored specification content on rerun, append safe
missing sections or ids only when proven non-destructive, and refuse identity
mismatches or destructive updates before filesystem mutation.

**Independent Test**: Run the safe-rerun fixtures and confirm existing prose and
stable ids are preserved, safe additions are deterministic, and unsafe
conflicts produce blocking diagnostics with no authored writes.

### Tests First

- [X] T033 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/specify-rerun-preserves-content/manifest.yml`
  with an existing specification containing authored stories, requirements,
  acceptance scenarios, non-goals, and ambiguity records plus expected
  preserve or no-change report fixtures.
- [X] T034 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/specify-adds-missing-sections/manifest.yml`
  with an existing specification missing standard sections and expected
  deterministic safe-addition output.
- [X] T035 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/specify-preserves-stable-ids/manifest.yml`
  with existing `US-###`, `AC-###`, `FR-###`, `SB-###`, and `AMB-###` ids and
  expected append-only id allocation.
- [X] T036 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/specification-identity-mismatch/manifest.yml`
  with conflicting specification front matter and expected blocking report
  fixtures.
- [X] T037 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/unsafe-overwrite/manifest.yml` with an
  existing authored specification that would require destructive rewrite and
  expected refusal diagnostics.
- [X] T038 [US2] Add failing rerun preservation and no-change tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/specify-rerun-preserves-content/`.
- [X] T039 [US2] Add failing safe-section-addition and stable-id preservation
  tests in `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/specify-adds-missing-sections/` and
  `tests/fixtures/lifecycle-commands/specify-preserves-stable-ids/`.
- [X] T040 [US2] Add failing specification identity-mismatch and
  unsafe-overwrite tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs`, proving existing
  specification bytes are unchanged when reports are blocked.
- [X] T041 [US2] Add failing blocking-effect tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving
  `specificationIdentityMismatch`, `malformedSpecificationFrontMatter`,
  `duplicateWorkId`, `duplicateSpecificationId`, `missingSpecificationId`,
  `unknownSpecificationReference`, and `unsafeOverwrite` diagnostics prevent
  authored `WriteFile` effects.

### Implementation

- [X] T042 [US2] Implement specification front-matter parsing and validation in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` for `schemaVersion`,
  `workId`, `title`, `stage`, `changeTier`, `status`, and optional public or
  tool-facing impact flags.
- [X] T043 [US2] Implement specification body section scanning and parsed fact
  extraction in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` for user
  stories, acceptance scenarios, functional requirements, scope boundaries, and
  ambiguity records, including structured requirement links to known story and
  acceptance-scenario ids.
- [X] T044 [US2] Implement deterministic missing-section insertion for
  specification reruns in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` without
  rewriting existing section prose.
- [X] T045 [US2] Implement append-only stable id preservation and next-suffix
  allocation for specification facts in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T046 [US2] Implement safe rerun write-plan decisions `preserve`,
  `noChange`, `update`, and `refuse` for `work/<id>/spec.md` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T047 [US2] Implement specification identity, malformed front matter,
  duplicate logical work id, duplicate id, missing id, unknown reference, and
  specification-specific unsafe-overwrite diagnostics in
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T048 [US2] Ensure authored specification dry-run and real-write
  interpretation preserve existing content unless the specification write plan
  is safe in `src/FS.GG.SDD.Commands/CommandEffects.fs`.
- [X] T049 [US2] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~SpecifyCommand"` and
  capture safe-rerun evidence in
  `specs/005-specify-command/readiness/specify-rerun-tests.txt`.

**Checkpoint**: User Stories 1 and 2 both work independently; specification
creation is safe to rerun on authored sources.

---

## Phase 5: User Story 3 - Diagnose Invalid Or Incomplete Specification Requests (Priority: P2)

**Goal**: Fail invalid specify requests with stable, actionable diagnostics and
no unsafe writes.

**Independent Test**: Run invalid project, missing charter, missing intent,
malformed work id, malformed specification, duplicate id, identity mismatch,
unsafe overwrite, and generated-view fixtures, confirming every blocked report
names the affected artifact and correction.

### Tests First

- [X] T050 [P] [US3] Extend
  `tests/fixtures/lifecycle-commands/outside-project/manifest.yml` with the
  specify outside-project scenario and expected blocked report fixture.
- [X] T051 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/missing-charter/manifest.yml` with the
  specify missing-charter scenario and expected blocked report fixture.
- [X] T052 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/missing-intent/manifest.yml` with the
  new-specification missing-intent scenario and expected blocked report
  fixture.
- [X] T053 [P] [US3] Extend
  `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml` with the
  specify malformed work-id scenario and expected blocked report fixture.
- [X] T054 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/malformed-specification/manifest.yml`
  with malformed specification front matter, unsupported schema, malformed
  section, and missing required id cases.
- [X] T055 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml` with
  duplicated logical work-id cases and
  `tests/fixtures/lifecycle-commands/duplicate-spec-id/manifest.yml` with
  duplicate story, requirement, acceptance scenario, scope boundary, and
  ambiguity id cases.
- [X] T056 [P] [US3] Extend
  `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml` with
  specify source-digest, generator-version, malformed JSON, and blocked refresh
  cases.
- [X] T057 [US3] Add failing invalid-request tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` for outside project,
  missing project config, malformed project config, missing work id, malformed
  work id, missing charter prerequisite, missing specification intent, and the
  specification-correction next action for valid work contexts that still lack
  user value, scope, or a measurable requirement.
- [X] T058 [US3] Add failing malformed-specification and duplicate-id tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs`, proving malformed
  sources, duplicated logical work ids, duplicate ids, missing required ids, and
  unknown requirement references block before `work/<id>/spec.md` changes and
  report specification correction when applicable.
- [X] T059 [US3] Add failing generated-view reporting tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` for specify
  missing, stale, malformed, and blocked
  `readiness/<id>/work-model.json` states.

### Implementation

- [X] T060 [US3] Implement missing-charter prerequisite diagnostics for
  `Specify` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, including missing
  file, malformed charter front matter, charter identity mismatch, and wrong
  charter stage cases.
- [X] T061 [US3] Implement missing specification intent diagnostics in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` for absent user value, absent
  scope, absent measurable requirement input, and specification-correction
  next-action selection.
- [X] T062 [US3] Implement malformed specification, duplicate specification
  id, duplicated logical work-id, missing required id, and unknown reference
  detection in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs`.
- [X] T063 [US3] Implement generated work-model refresh and currency planning
  for specify in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, using charter,
  specification, task, evidence, and generated-view snapshots where source
  data is sufficient.
- [X] T064 [US3] Map specify generated-view currency, source digests, output
  digests, and diagnostic ids into command reports in
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T065 [US3] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GeneratedViewCommand"`
  and capture generated-view evidence in
  `specs/005-specify-command/readiness/generated-view-tests.txt`.
- [X] T066 [US3] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~SpecifyCommand"` and
  capture invalid-request evidence in
  `specs/005-specify-command/readiness/specify-diagnostics-tests.txt`.

**Checkpoint**: Invalid specify requests are diagnosed before mutation, and
generated-view presence is never treated as proof of currency.

---

## Phase 6: User Story 4 - Keep Specification Output Traceable (Priority: P3)

**Goal**: Produce deterministic JSON, text projections from the same report
object, dry-run reports without mutation, and optional Governance compatibility
facts without evaluating Governance policy.

**Independent Test**: Run three identical dry-run specify reports, text
projection tests, dry-run tests, and Governance boundary tests; compare report
bytes and assert no route, freshness, profile, gate, audit,
protected-boundary, or release verdict appears.

### Tests First

- [X] T067 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml` with
  specify dry-run inputs, reset-fixture non-dry-run inputs, and expected
  byte-stable report fixture paths.
- [X] T068 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/text-projection/manifest.yml` with
  specify JSON and text projection expectations.
- [X] T069 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml` with
  absent, present, malformed, and incomplete optional Governance pointer
  scenarios for specify.
- [X] T070 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/dry-run/manifest.yml` with specify create,
  safe rerun, generated-view refresh, and blocked diagnostic dry-run cases.
- [X] T071 [US4] Add failing specify deterministic-report tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`, proving three
  dry-run executions over identical specify inputs and three non-dry-run
  executions over restored fixture inputs produce byte-identical JSON.
- [X] T072 [US4] Add failing specify text-projection tests in
  `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`, proving text output
  contains only facts from `CommandReport`.
- [X] T073 [US4] Add failing specify Governance-boundary tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`, proving
  optional Governance pointers are compatibility facts only.
- [X] T074 [US4] Add failing specify dry-run tests in
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs`, proving proposed
  authored and generated changes are reported and no `spec.md` or
  `work-model.json` mutation occurs.

### Implementation

- [X] T075 [US4] Ensure specify report JSON property order, specification id
  sorting, generated-view sorting, diagnostic sorting, UTF-8 output, digest
  formatting, and path normalization in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` match
  `specs/005-specify-command/contracts/specify-report-json.md`.
- [X] T076 [US4] Ensure specify text output in
  `src/FS.GG.SDD.Commands/CommandRendering.fs` renders command, outcome,
  changed artifact count, specification id counts, unresolved ambiguity count,
  generated-view count, diagnostic count, and next action from `CommandReport`
  only.
- [X] T077 [US4] Ensure specify Governance compatibility facts in
  `src/FS.GG.SDD.Commands/CommandReports.fs` expose optional policy,
  capabilities, and tooling pointers without route, freshness, profile, gate,
  protected-boundary, audit, or release verdicts.
- [X] T078 [US4] Ensure dry-run artifact changes for specify use
  `dryRunOnly` or equivalent safe-write decisions without filesystem mutation
  in `src/FS.GG.SDD.Commands/CommandEffects.fs`.
- [X] T079 [US4] Update
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` and
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` after intentional
  specify public API changes and keep both `SurfaceBaselineTests.fs` files
  passing.
- [X] T080 [US4] Run `dotnet fsi scripts/prelude.fsx` and capture FSI evidence
  in
  `specs/005-specify-command/readiness/fsi-specify-public-surface.txt`.
- [X] T081 [US4] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson|FullyQualifiedName~TextProjection|FullyQualifiedName~GovernanceBoundaryCommand"`
  and capture traceability evidence in
  `specs/005-specify-command/readiness/traceable-output-tests.txt`.

**Checkpoint**: Specify output is deterministic, text is a projection, dry-run
does not mutate disk, and Governance integration remains advisory.

---

## Phase 7: Polish And Evidence

**Purpose**: Run the quickstart validation path, record evidence, and verify
the feature stays inside the SDD/Governance boundary.

- [X] T082 Run `dotnet build FS.GG.SDD.sln -c Release` and capture build
  evidence in `specs/005-specify-command/readiness/build.txt`.
- [X] T083 Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandWorkflow"`
  and capture MVU boundary evidence in
  `specs/005-specify-command/readiness/command-workflow-tests.txt`.
- [X] T084 Run the disposable-directory CLI smoke path from
  `specs/005-specify-command/quickstart.md` and capture output in
  `specs/005-specify-command/readiness/specify-cli-smoke.txt`.
- [X] T085 Run `dotnet test FS.GG.SDD.sln` and capture full-suite evidence in
  `specs/005-specify-command/readiness/full-test-suite.txt`.
- [X] T086 Review specify reports and text output for SDD/Governance ownership
  boundaries and record the result in
  `specs/005-specify-command/readiness/governance-boundary-review.txt`.
- [X] T087 Record final task-to-artifact traceability for
  `work/<id>/spec.md`, `readiness/<id>/work-model.json`, command JSON,
  text projection, specification diagnostics, stable ids, and optional
  Governance facts in
  `specs/005-specify-command/readiness/artifact-traceability.txt`.
- [X] T088 Run the specify performance evidence path for the `specify-create`
  and `specify-rerun-preserves-content` fixture scenarios, confirm each
  command run completes under 2 seconds through the command test harness on
  the local development machine, and capture evidence in
  `specs/005-specify-command/readiness/performance.txt`.
- [X] T089 Review the human-readable specify summary against SC-006,
  confirming changed artifact, parsed requirements, unresolved ambiguity
  count, blocking diagnostic, generated-view state, and next action are
  identifiable from text-projection output, and capture evidence in
  `specs/005-specify-command/readiness/human-summary-review.txt`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup** has no dependencies.
- **Phase 2: Foundation** depends on Phase 1 and blocks all user-story work.
- **Phase 3: User Story 1** depends on Phase 2 and is the MVP slice.
- **Phase 4: User Story 2** depends on Phase 3 because rerun safety builds on
  create-write planning and specification parsing.
- **Phase 5: User Story 3** depends on Phase 3 and can run in parallel with
  Phase 4 after shared specification parsing contracts stabilize.
- **Phase 6: User Story 4** depends on report shapes from Phases 3 through 5.
- **Phase 7: Polish And Evidence** depends on the desired story phases being
  complete.

### User Story Dependencies

- **US1 - Create A Work Specification**: Starts after Foundation; no dependency
  on later stories.
- **US2 - Preserve And Refine Existing Specification Content**: Depends on US1
  specification create planning and adds preservation/refusal behavior.
- **US3 - Diagnose Invalid Or Incomplete Specification Requests**: Depends on
  Foundation and US1 command routing; generated-view refresh work can proceed
  once specify source snapshots exist.
- **US4 - Keep Specification Output Traceable**: Depends on report data from
  US1, US2, and US3.

### Parallel Opportunities

- T002, T004, and T005 can run in parallel after T001 and T003 are understood
  because they create separate files.
- T014 and T015 can run in parallel because they test different public
  contracts.
- Fixture population tasks T020, T033 through T037, T050 through T056, and T067
  through T070 are parallel-safe by fixture root.
- Test tasks that edit different files can run in parallel; tasks editing
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` should be serialized.
- Generated-view work in T023, T059, T063, and T064 can proceed in parallel
  with safe-rerun implementation once specification source snapshots are
  available.
- US4 fixture and test setup can start after report field names stabilize, but
  evidence tasks T080 and T081 wait for implementation.

---

## Implementation Strategy

### MVP First

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 for User Story 1.
3. Stop and validate with
   `specs/005-specify-command/readiness/specify-create-tests.txt`.

### Priority Completion

1. Add User Story 2 before treating specify reruns as safe for regular use.
2. Add User Story 3 before relying on specify in agent or CI workflows.
3. Add User Story 4 before publishing specify output as a stable automation
   contract.
4. Complete Phase 7 before merge.

## Notes

- Keep all public command and artifact behavior visible through `.fsi` files.
- Keep Markdown as the authoring surface and structured front matter/report JSON
  as the machine contracts.
- Do not add `clarify`, `checklist`, `plan`, `tasks`, `analyze`, evidence
  update, verify, ship, release, generated agent guidance, Governance route
  selection, freshness, profiles, gates, protected-boundary verdicts, audit, or
  release-policy behavior in this feature.
