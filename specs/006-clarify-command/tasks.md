# Tasks: Clarify Command

**Feature branch**: `006-clarify-command`
**Spec**: `specs/006-clarify-command/spec.md`
**Plan**: `specs/006-clarify-command/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/006-clarify-command/`

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
Principle V applies: `.fsi` contracts must declare clarification ids,
clarification facts, command model, messages, effects, reports, and
interpreter boundary before `.fs` bodies harden; story completion requires
pure transition tests, emitted-effect assertions, and real interpreter evidence
where safe.

---

## Phase 1: Setup

**Purpose**: Add the missing clarify test, fixture, and readiness roots without
adding new source projects.

- [X] T001 Update
  `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Artifacts.Tests/ClarificationArtifactTests.fs` after
  `tests/FS.GG.SDD.Artifacts.Tests/SpecificationArtifactTests.fs` and before
  `tests/FS.GG.SDD.Artifacts.Tests/WorkModelTests.fs`.
- [X] T002 [P] Create
  `tests/FS.GG.SDD.Artifacts.Tests/ClarificationArtifactTests.fs` with a
  `FS.GG.SDD.Artifacts.Tests.ClarificationArtifactTests` module skeleton for
  clarification id, front matter, parser, and decision tests.
- [X] T003 Update
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` after
  `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs` and before
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T004 [P] Create
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` with a
  `FS.GG.SDD.Commands.Tests.ClarifyCommandTests` module skeleton.
- [X] T005 [P] Create `specs/006-clarify-command/readiness/README.md`
  documenting the expected build, focused clarify, MVU boundary,
  generated-view, deterministic-report, text-projection, Governance-boundary,
  dry-run, FSI, CLI smoke, performance, human-summary, traceability, and
  full-suite evidence files.

**Checkpoint**: Test and readiness files exist; foundation work can add public
contracts and failing tests.

---

## Phase 2: Foundation

**Purpose**: Declare public clarification contracts, command-report shapes,
diagnostics, MVU boundaries, and reusable test helpers before any clarify
implementation body is completed.

- [X] T006 Draft clarification question id contract additions in
  `src/FS.GG.SDD.Artifacts/Identifiers.fsi` for `ClarificationQuestionId`,
  `createClarificationQuestionId`, and `clarificationQuestionIdValue`.
- [X] T007 Mirror the clarification question id contracts from T006 in
  `src/FS.GG.SDD.Artifacts/Identifiers.fs` with compiling placeholder
  validation for the `CQ-###` shape.
- [X] T008 Draft clarification artifact parsing contract additions in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` for clarification front
  matter, standard sections, questions, answers, concrete decisions, accepted
  deferrals, remaining ambiguity, and parsed clarification facts.
- [X] T009 Mirror the clarification artifact contracts from T008 in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` with compiling placeholder
  behavior only.
- [X] T010 Draft clarify report contract additions in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`, including a clarification summary
  that can carry work id, stage, status, source spec, question ids, answered
  question ids, decision ids, accepted deferral ids, remaining ambiguity count,
  and blocking ambiguity count.
- [X] T011 Mirror the clarify report contract additions from T010 in
  `src/FS.GG.SDD.Commands/CommandTypes.fs` with compiling placeholder
  behavior only.
- [X] T012 Draft clarify diagnostic builders in
  `src/FS.GG.SDD.Commands/CommandReports.fsi` for
  `missingSpecificationPrerequisite`, `malformedSpecificationFacts`,
  `missingClarificationAnswer`, `clarificationIdentityMismatch`,
  `malformedClarificationFrontMatter`, `duplicateClarificationId`,
  `unknownClarificationReference`, `unsafeDecisionChange`, and
  `unresolvedBlockingAmbiguity`.
- [X] T013 Mirror the clarify diagnostic builders from T012 in
  `src/FS.GG.SDD.Commands/CommandReports.fs` with stable ids and compiling
  placeholder behavior.
- [X] T014 [P] Add failing clarification question id tests in
  `tests/FS.GG.SDD.Artifacts.Tests/IdentifierTests.fs` for valid and invalid
  `CQ-###` values.
- [X] T015 [P] Add failing clarification artifact parser tests in
  `tests/FS.GG.SDD.Artifacts.Tests/ClarificationArtifactTests.fs` for required
  front matter, standard sections, `CQ-###` extraction, `DEC-###` extraction,
  accepted-deferral extraction, schema-version compatibility and diagnose-only
  migration posture, duplicate id detection, unknown-reference diagnostics,
  and remaining ambiguity counts.
- [X] T016 Add clarify request, clarify runner, valid-specification writer,
  valid-clarification writer, dry-run digest, and clarification assertion
  helpers in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [X] T017 Add failing pure MVU boundary tests for `Clarify` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving project,
  specification, clarification, task, evidence, generated-view, and
  work-directory reads are requested before authored or generated writes.
- [X] T018 Add failing emitted-effect tests for `Clarify` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving blocking
  diagnostics prevent `WriteFile` effects and dry-run interpretation does not
  mutate authored or generated artifacts.
- [X] T019 Update `scripts/prelude.fsx` to construct a `clarify-create`
  `Clarify` command request, call `CommandWorkflow.init` and
  `CommandWorkflow.update`, and print command name, outcome, changed artifact
  count, parsed clarification question count, decision count, accepted
  deferral count, generated-view count, blocking diagnostic count, and next
  action.

**Checkpoint**: Clarify public surface is declared in `.fsi`, reusable tests
can run through the MVU boundary, and implementation can proceed story by
story.

---

## Phase 3: User Story 1 - Resolve Specification Ambiguity (Priority: P1) - MVP

**Goal**: Create `work/<id>/clarifications.md` for a specified work item,
record questions and durable decisions for open ambiguity, report generated
work-model state, and point to `checklist` without requiring Governance.

**Independent Test**: Run the clarify create tests against an initialized
specified fixture and a temporary-directory interpreter path, confirming
clarification front matter, standard sections, stable ids, command report,
generated-view state, and next action.

### Tests First

- [X] T020 [P] [US1] Populate
  `tests/fixtures/lifecycle-commands/clarify-create/manifest.yml` with
  initialized `.fsgg/` inputs, a valid `work/006-clarify-command/spec.md`
  prerequisite containing `AMB-###` source ambiguity, expected
  `work/006-clarify-command/clarifications.md`, and golden clarify command
  report paths for the create scenario; also populate
  `tests/fixtures/lifecycle-commands/no-open-ambiguity/manifest.yml` for a
  specified work item with no open ambiguity records.
- [X] T021 [US1] Add failing successful-create tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` for valid
  clarification front matter, required standard sections, created artifact
  path `work/006-clarify-command/clarifications.md`, parsed question and
  decision ids, successful outcome, and next action `checklist`; include the
  no-open-ambiguity branch that reports ready-for-checklist or missing
  specification facts without inventing clarification questions.
- [X] T022 [US1] Add failing no-Governance create tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` proving absent
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` do not
  block clarification creation.
- [X] T023 [US1] Add failing generated-view create tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` proving
  `Clarify` reports `readiness/006-clarify-command/work-model.json` and does
  not treat a missing generated file as current.
- [X] T024 [US1] Add failing real interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` proving
  `CommandEffects` creates `work/006-clarify-command/clarifications.md` in a
  temporary initialized, chartered, and specified project.

### Implementation

- [X] T025 [US1] Route `Clarify` away from `unsupportedLifecycleCommand` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and plan the required project,
  specification, clarification, task, evidence, generated-view, and
  duplicate-id source reads.
- [X] T026 [US1] Implement specification prerequisite validation for `Clarify`
  in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, requiring
  `work/<id>/spec.md` front matter with matching work id and `stage: specify`
  before clarification writes.
- [X] T027 [US1] Implement clarification answer normalization and source
  ambiguity question generation in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`,
  extracting source ambiguity ids, related requirement/story/acceptance ids,
  answer text, concrete decisions, accepted deferrals, and still-open notes
  from `CommandRequest.InputText`.
- [X] T028 [US1] Implement deterministic new-clarification template generation
  for `work/<id>/clarifications.md` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, including YAML front matter and
  the Source Specification, Clarification Questions, Answers, Decisions,
  Accepted Deferrals, Remaining Ambiguity, and Lifecycle Notes sections.
- [X] T029 [US1] Implement create-write planning for missing
  `work/<id>/clarifications.md` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after T026 through T028.
- [X] T030 [US1] Implement clarify changed-artifact reporting,
  clarification summary reporting, generated-view placeholder state, and
  `checklist` next-action required artifacts in
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T031 [US1] Serialize the clarify `clarification` report object in
  documented order in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T032 [US1] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ClarifyCommand"` and
  capture successful-create evidence in
  `specs/006-clarify-command/readiness/clarify-create-tests.txt`.

**Checkpoint**: User Story 1 is independently testable and is the suggested
MVP scope for a first implementation slice.

---

## Phase 4: User Story 2 - Preserve And Extend Clarification Decisions (Priority: P1)

**Goal**: Preserve authored clarification content on rerun, append compatible
answers or decisions only when proven non-destructive, keep decision ids
stable, and refuse semantic decision changes before filesystem mutation.

**Independent Test**: Run the safe-rerun fixtures and confirm existing answers,
decisions, accepted deferrals, and stable ids are preserved; safe additions are
deterministic; and unsafe conflicts produce blocking diagnostics with no
authored writes.

### Tests First

- [X] T033 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/clarify-rerun-preserves-decisions/manifest.yml`
  with an existing clarification artifact containing authored questions,
  answers, decisions, accepted deferrals, remaining ambiguity notes, and
  expected preserve or no-change report fixtures.
- [X] T034 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/clarify-adds-missing-sections/manifest.yml`
  with an existing clarification artifact missing standard sections and
  expected deterministic safe-addition output.
- [X] T035 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/clarify-preserves-stable-ids/manifest.yml`
  with existing `CQ-###` and `DEC-###` ids and expected append-only id
  allocation.
- [X] T036 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/clarify-accepted-deferral/manifest.yml`
  with an ambiguity resolved as an accepted deferral and expected durable
  deferral decision facts.
- [X] T037 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/unsafe-decision-change/manifest.yml` with
  an existing authored clarification decision that a proposed answer would
  semantically change and expected refusal diagnostics.
- [X] T038 [US2] Add failing rerun preservation and no-change tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/clarify-rerun-preserves-decisions/`.
- [X] T039 [US2] Add failing safe-section-addition and stable-id preservation
  tests in `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/clarify-adds-missing-sections/` and
  `tests/fixtures/lifecycle-commands/clarify-preserves-stable-ids/`.
- [X] T040 [US2] Add failing accepted-deferral tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs`, proving deferrals
  use durable `DEC-###` ids, remain visible in report facts, and allow
  `checklist` only when no blocking ambiguity remains.
- [X] T041 [US2] Add failing unsafe-decision-change tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs`, proving existing
  clarification bytes are unchanged when reports are blocked.

### Implementation

- [X] T042 [US2] Implement clarification front-matter parsing and validation in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` for `schemaVersion`,
  `workId`, `title`, `stage`, `changeTier`, `status`, `sourceSpec`, and
  optional public or tool-facing impact flags, including diagnose-only
  migration posture for missing, malformed, future, unsupported, or deprecated
  clarification schema versions.
- [X] T043 [US2] Implement clarification body section scanning and parsed fact
  extraction in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` for
  clarification questions, answers, concrete decisions, accepted deferrals,
  remaining ambiguity, and source links to known specification ids.
- [X] T044 [US2] Implement deterministic missing-section insertion for
  clarification reruns in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` without
  rewriting existing section prose.
- [X] T045 [US2] Implement append-only `CQ-###` and `DEC-###` stable id
  preservation and next-suffix allocation in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T046 [US2] Implement accepted-deferral modeling in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and summarize accepted
  deferral ids in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T047 [US2] Implement rerun conflict detection in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` for clarification identity
  mismatch, duplicate clarification ids, removed or renumbered ids, and
  semantic decision changes.
- [X] T048 [US2] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ClarifyCommand"` and
  capture rerun, stable-id, accepted-deferral, and unsafe-decision evidence in
  `specs/006-clarify-command/readiness/clarify-rerun-tests.txt`.

**Checkpoint**: User Story 2 is independently testable through fixture-backed
rerun behavior.

---

## Phase 5: User Story 3 - Diagnose Missing Or Invalid Clarification Context (Priority: P2)

**Goal**: Fail invalid clarify requests with stable, actionable diagnostics and
no unsafe writes when project context, work id, specification prerequisites,
answer references, clarification data, or generated views are missing,
malformed, stale, or inconsistent.

**Independent Test**: Invoke clarify across blocked fixture families and
confirm authored clarification content is unchanged, write effects are absent,
generated-view state is diagnostic rather than assumed current, and each
blocked report contains a correction.

### Tests First

- [X] T049 [P] [US3] Populate or extend
  `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`,
  `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml`,
  `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml`,
  `tests/fixtures/lifecycle-commands/missing-specification/manifest.yml`, and
  `tests/fixtures/lifecycle-commands/missing-answer/manifest.yml` with clarify
  blocked-report expectations.
- [X] T050 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/malformed-clarification/manifest.yml`,
  `tests/fixtures/lifecycle-commands/duplicate-clarification-id/manifest.yml`,
  `tests/fixtures/lifecycle-commands/unknown-ambiguity-reference/manifest.yml`,
  `tests/fixtures/lifecycle-commands/clarification-identity-mismatch/manifest.yml`,
  `tests/fixtures/lifecycle-commands/unsafe-overwrite/manifest.yml`, and
  clarify expectations in
  `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml`.
- [X] T051 [US3] Add failing invalid-context tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` for outside project,
  malformed work id, duplicate work id, missing specification, and missing
  answer scenarios.
- [X] T052 [US3] Add failing malformed-clarification, duplicate-id,
  unknown-reference, identity-mismatch, and unsafe-overwrite tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs`.
- [X] T053 [US3] Add failing generated-view diagnostic tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`, proving
  clarify distinguishes missing, stale, malformed, and blocked
  `readiness/<id>/work-model.json` states.
- [X] T054 [US3] Add failing blocking-effect tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving
  `missingSpecificationPrerequisite`, `missingClarificationAnswer`,
  `unknownClarificationReference`, `duplicateClarificationId`,
  `clarificationIdentityMismatch`, `unsafeDecisionChange`, and
  `blockedGeneratedViewRefresh` diagnostics prevent authored `WriteFile`
  effects.

### Implementation

- [X] T055 [US3] Implement missing or malformed project, work id,
  duplicate-work-id, missing-specification, malformed-specification, and
  missing-answer diagnostics for `Clarify` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T056 [US3] Implement unknown source-reference, duplicate clarification
  id, malformed clarification front matter, clarification identity mismatch,
  unsafe decision change, and unresolved blocking ambiguity diagnostics in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T057 [US3] Implement clarify generated-view currency classification in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`, naming the source artifact for
  missing, stale, malformed, and blocked work-model states.
- [X] T058 [US3] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ClarifyCommand|FullyQualifiedName~GeneratedViewCommand|FullyQualifiedName~CommandWorkflow"`
  and capture invalid-context and generated-view evidence in
  `specs/006-clarify-command/readiness/clarify-diagnostics-tests.txt`.

**Checkpoint**: User Story 3 is independently testable through blocked fixture
families and generated-view diagnostics.

---

## Phase 6: User Story 4 - Keep Clarification Output Traceable (Priority: P3)

**Goal**: Emit deterministic clarify reports, render human text from the same
report, preserve dry-run immutability, and expose optional Governance pointers
only as compatibility facts.

**Independent Test**: Run identical clarify requests repeatedly against the
same fixture state, compare JSON bytes, compare text projection facts to JSON,
exercise dry-run mutation checks, and prove Governance files are optional.

### Tests First

- [X] T059 [P] [US4] Extend
  `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`,
  `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`,
  `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, and
  `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml` with
  clarify-specific inputs and expected report facts without replacing existing
  specify expectations.
- [X] T060 [US4] Add failing deterministic clarify JSON tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`, proving three
  identical dry-run clarify executions over the same fixture produce
  byte-identical reports.
- [X] T061 [US4] Add failing clarify text projection tests in
  `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`, proving text mode
  includes only command-report facts for changed artifacts, decisions,
  accepted deferrals, remaining ambiguity, generated-view state, diagnostics,
  and next action.
- [X] T062 [US4] Add failing clarify dry-run mutation tests in
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs`, proving no authored
  or generated artifact changes occur when `CommandRequest.DryRun` is true.
- [X] T063 [US4] Add failing clarify Governance-boundary tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`, proving
  absent Governance files do not block and present pointers are reported only
  as compatibility facts.

### Implementation

- [X] T064 [US4] Harden clarify report serialization ordering in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` for clarification ids,
  artifact changes, generated views, diagnostics, Governance compatibility
  facts, and next-action required artifacts.
- [X] T065 [US4] Implement clarify text projection in
  `src/FS.GG.SDD.Commands/CommandRendering.fs`, rendering clarification
  question count, decision count, accepted deferral count, remaining ambiguity
  count, generated-view count, diagnostic count, and next action from the
  authoritative `CommandReport`.
- [X] T066 [US4] Implement clarify dry-run safe-write decisions in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs` so proposed authored and
  generated changes are reported without mutation.
- [X] T067 [US4] Implement optional Governance compatibility facts for clarify
  in `src/FS.GG.SDD.Commands/CommandReports.fs` without parsing Governance
  schemas or producing route, freshness, profile, gate, audit, protected
  boundary, or release verdicts.
- [X] T068 [US4] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson|FullyQualifiedName~TextProjection|FullyQualifiedName~GovernanceBoundaryCommand"`
  and capture deterministic-report, text-projection, dry-run, and
  Governance-boundary evidence in
  `specs/006-clarify-command/readiness/clarify-traceability-tests.txt`.

**Checkpoint**: User Story 4 is independently testable through deterministic
JSON, text projection, dry-run, and no-Governance evidence.

---

## Phase 7: Integration & Polish

**Purpose**: Refresh public baselines, capture vertical-slice evidence, and
record readiness without expanding scope beyond `fsgg-sdd clarify`.

- [X] T069 [P] Refresh artifact public surface baselines in
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` after intentional
  clarification id and parser signature changes.
- [X] T070 [P] Refresh command public surface baselines in
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` after intentional
  clarify summary, report, diagnostic, and workflow signature changes.
- [X] T071 Run `dotnet build FS.GG.SDD.sln -c Release` and capture output in
  `specs/006-clarify-command/readiness/build-release.txt`.
- [X] T072 Run `dotnet build src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj -c Release`
  followed by `dotnet fsi scripts/prelude.fsx`, and capture the clarify public
  surface transcript in `specs/006-clarify-command/readiness/fsi-session.txt`.
- [X] T073 Run the disposable-directory CLI smoke path from
  `specs/006-clarify-command/quickstart.md` and capture output in
  `specs/006-clarify-command/readiness/cli-smoke.txt`.
- [X] T074 Run the clarify create and rerun scenarios through the command test
  harness, verify each finishes under 2 seconds on the local development
  machine, and capture performance evidence in
  `specs/006-clarify-command/readiness/performance.txt`.
- [X] T075 Review text output for changed artifact, parsed decisions, accepted
  deferral count, remaining ambiguity count, blocking diagnostic,
  generated-view state, and next action, then record the human summary review
  in `specs/006-clarify-command/readiness/human-summary-review.txt`.
- [X] T076 Record an SDD/Governance boundary review in
  `specs/006-clarify-command/readiness/sdd-governance-boundary-review.md`,
  confirming the feature introduces no checklist, plan, tasks, analyze,
  evidence, verify, ship, generated agent guidance, Governance route,
  freshness, profile, gate, audit, protected-boundary, or release behavior.
- [X] T077 Update `docs/initial-implementation-plan.md` and `README.md` after
  implementation evidence is green to mark `006-clarify-command` as complete
  and describe the implemented clarify command without claiming later
  checklist or Governance behavior.
- [X] T078 Run `dotnet test FS.GG.SDD.sln` and capture the full-suite result in
  `specs/006-clarify-command/readiness/full-suite.txt`.
- [X] T079 Record artifact traceability from spec requirements, plan contracts,
  tasks, fixtures, tests, and readiness evidence in
  `specs/006-clarify-command/readiness/artifact-traceability.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; creates test and readiness files.
- **Foundation (Phase 2)**: Depends on Setup; blocks user-story
  implementation because `.fsi` contracts, test helpers, and failing MVU tests
  must exist first.
- **User Story 1 (Phase 3)**: Depends on Foundation; provides the MVP clarify
  create path.
- **User Story 2 (Phase 4)**: Depends on Foundation and is easiest after US1
  write planning exists; preserves and extends existing decisions.
- **User Story 3 (Phase 5)**: Depends on Foundation; may be developed after or
  alongside US2 with coordination because it touches the same workflow and
  diagnostics files.
- **User Story 4 (Phase 6)**: Depends on report facts from US1 through US3;
  completes deterministic output, text projection, dry-run, and Governance
  compatibility.
- **Integration & Polish (Phase 7)**: Depends on all desired user stories.

### Story Dependencies

- **US1 (P1)**: MVP; no dependency on other stories after Foundation.
- **US2 (P1)**: Can be tested independently with existing-clarification
  fixtures, but implementation reuses the write-plan and parser contracts from
  US1.
- **US3 (P2)**: Can be tested independently with blocked fixtures, but
  implementation shares diagnostics and effect blocking with US1 and US2.
- **US4 (P3)**: Depends on clarify report facts from earlier stories.

### Parallel Opportunities

- T002, T004, and T005 can run in parallel during Setup.
- T014 and T015 can run in parallel with T016 through T019 after `.fsi`
  contracts exist.
- Fixture population tasks T020, T033 through T037, T049 through T050, and
  T059 can be split by fixture directory.
- Test additions in different files can run in parallel when they do not edit
  the same test file.
- Baseline refresh tasks T069 and T070 can run in parallel after public API
  changes are finalized.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation.
3. Complete Phase 3: User Story 1.
4. Stop and validate: run the focused clarify command tests and CLI smoke path
   for clarification creation.

### Incremental Delivery

1. Add US1 to create clarification artifacts and report `checklist` as next.
2. Add US2 to preserve and safely extend existing clarification decisions.
3. Add US3 to harden diagnostics and generated-view failure modes.
4. Add US4 to finalize deterministic output, text projection, dry-run, and
   optional Governance compatibility.
5. Complete Integration & Polish with surface baselines and readiness evidence.

### Scope Guardrails

- Do not add `fsgg-sdd checklist`, `plan`, `tasks`, `analyze`, evidence,
  verify, ship, release, generated agent guidance, or Governance enforcement
  behavior in this feature.
- Do not make generated views authoritative; report source and generator
  currency explicitly.
- Do not parse or enforce Governance-owned schemas; only expose optional
  compatibility facts.
