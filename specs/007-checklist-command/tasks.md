# Tasks: Checklist Command

**Feature branch**: `007-checklist-command`
**Spec**: `specs/007-checklist-command/spec.md`
**Plan**: `specs/007-checklist-command/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/007-checklist-command/`

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
Principle V applies: `.fsi` contracts must declare checklist ids, checklist
facts, command model, messages, effects, reports, and interpreter boundary
before `.fs` bodies harden; story completion requires pure transition tests,
emitted-effect assertions, and real interpreter evidence where safe.

---

## Phase 1: Setup

**Purpose**: Add the missing checklist test, fixture, and readiness roots
without adding new source projects.

- [X] T001 Update
  `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Artifacts.Tests/ChecklistArtifactTests.fs` after
  `tests/FS.GG.SDD.Artifacts.Tests/ClarificationArtifactTests.fs` and before
  `tests/FS.GG.SDD.Artifacts.Tests/WorkModelTests.fs`.
- [X] T002 [P] Create
  `tests/FS.GG.SDD.Artifacts.Tests/ChecklistArtifactTests.fs` with a
  `FS.GG.SDD.Artifacts.Tests.ChecklistArtifactTests` module skeleton for
  checklist front matter, source snapshot, item id, result id, stale result,
  and safe-rerun parser tests.
- [X] T003 Update
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` after
  `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs` and before
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T004 [P] Create
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` with a
  `FS.GG.SDD.Commands.Tests.ChecklistCommandTests` module skeleton.
- [X] T005 [P] Create `specs/007-checklist-command/readiness/README.md`
  documenting the expected build, focused checklist, MVU boundary,
  generated-view, deterministic-report, text-projection, Governance-boundary,
  dry-run, FSI, CLI smoke, performance, human-summary, traceability, and
  full-suite evidence files.

**Checkpoint**: Test and readiness files exist; foundation work can add public
contracts and failing tests.

---

## Phase 2: Foundation

**Purpose**: Declare public checklist contracts, command-report shapes,
diagnostics, MVU boundaries, and reusable test helpers before any checklist
implementation body is completed.

- [X] T006 Draft checklist id contract additions in
  `src/FS.GG.SDD.Artifacts/Identifiers.fsi` for `ChecklistItemId`,
  `ChecklistResultId`, `createChecklistItemId`, `createChecklistResultId`,
  `checklistItemIdValue`, and `checklistResultIdValue`.
- [X] T007 Mirror the checklist id contracts from T006 in
  `src/FS.GG.SDD.Artifacts/Identifiers.fs` with compiling placeholder
  validation for `CHK-###` and `CR-###` shapes.
- [X] T008 Draft checklist artifact parsing contract additions in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` for checklist front matter,
  source snapshots, checklist items, review results, accepted checklist
  deferrals, requirements-quality findings, advisory notes, lifecycle notes,
  parsed checklist facts, and `checklistStandardSections`.
- [X] T009 Mirror the checklist artifact contracts from T008 in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` with compiling placeholder
  behavior only.
- [X] T010 Draft checklist report contract additions in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`, including a checklist summary
  that can carry work id, stage, status, source spec, source clarifications,
  item ids, result ids, passed count, failed blocking count, accepted deferral
  count, stale result count, and advisory count.
- [X] T011 Mirror the checklist report contract additions from T010 in
  `src/FS.GG.SDD.Commands/CommandTypes.fs` with compiling placeholder behavior
  only.
- [X] T012 Draft checklist diagnostic builders in
  `src/FS.GG.SDD.Commands/CommandReports.fsi` for `missingWorkId`,
  `malformedWorkId`, `missingSpecificationPrerequisite`,
  `missingClarificationPrerequisite`, `specificationIdentityMismatch`,
  `clarificationIdentityMismatch`, `unresolvedBlockingAmbiguity`,
  `failedRequirementsQuality`, `checklistIdentityMismatch`,
  `malformedChecklistFrontMatter`, `duplicateChecklistId`,
  `unknownChecklistSourceReference`, `staleChecklistResult`,
  `unsafeChecklistResultChange`, `staleGeneratedView`,
  `malformedGeneratedView`, and `blockedGeneratedViewRefresh`.
- [X] T013 Mirror the checklist diagnostic builders from T012 in
  `src/FS.GG.SDD.Commands/CommandReports.fs` with stable ids and compiling
  placeholder behavior.
- [X] T014 [P] Add failing checklist id tests in
  `tests/FS.GG.SDD.Artifacts.Tests/IdentifierTests.fs` for valid and invalid
  `CHK-###` and `CR-###` values.
- [X] T015 [P] Add failing checklist artifact parser tests in
  `tests/FS.GG.SDD.Artifacts.Tests/ChecklistArtifactTests.fs` for required
  front matter, standard sections, source snapshots, checklist item extraction,
  review result extraction, accepted deferral extraction, schema-version
  compatibility and diagnose-only migration posture, duplicate id detection,
  unknown-reference diagnostics, and stale source snapshot facts.
- [X] T016 Add checklist request, checklist runner, valid specification writer,
  valid clarification writer, existing checklist writer, dry-run digest, and
  checklist assertion helpers in
  `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.
- [X] T017 Add failing pure MVU boundary tests for `Checklist` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving project,
  specification, clarification, checklist, generated-view, and work-directory
  reads are requested before authored or generated writes, and that any existing
  task or evidence source reads are limited to work-model currency inputs.
- [X] T018 Add failing emitted-effect tests for `Checklist` in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving blocking
  diagnostics prevent `WriteFile` effects and dry-run interpretation does not
  mutate authored or generated artifacts.
- [X] T019 Update `scripts/prelude.fsx` to construct a `checklist-create`
  `Checklist` command request, call `CommandWorkflow.init` and
  `CommandWorkflow.update`, and print command name, outcome, changed artifact
  count, checklist item count, passed count, failed blocking count, accepted
  deferral count, stale result count, generated-view count, blocking
  diagnostic count, and next action.

**Checkpoint**: Checklist public surface is declared in `.fsi`, reusable tests
can run through the MVU boundary, and implementation can proceed story by
story.

---

## Phase 3: User Story 1 - Create Requirements-Quality Checklist (Priority: P1) - MVP

**Goal**: Create `work/<id>/checklist.md` for a clarified work item, record
requirements-quality items and results, report generated work-model state, and
point to `plan` without requiring Governance.

**Independent Test**: Run the checklist create tests against an initialized
clarified fixture and a temporary-directory interpreter path, confirming
checklist front matter, standard sections, stable ids, command report,
generated-view state, and next action.

### Tests First

- [-] T020 [P] [US1] Populate — skipped: static manifest superseded by
  real temporary-directory checklist command tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/checklist-create/manifest.yml` with
  initialized `.fsgg/` inputs, valid
  `work/007-checklist-command/spec.md` and
  `work/007-checklist-command/clarifications.md` prerequisites, expected
  `work/007-checklist-command/checklist.md`, and golden checklist command
  report paths for the create scenario.
- [X] T021 [US1] Add failing successful-create tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` for valid
  checklist front matter, required standard sections, created artifact path
  `work/007-checklist-command/checklist.md`, parsed item and result ids,
  successful outcome, and next action `plan`.
- [X] T022 [US1] Add failing no-Governance create tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` proving absent
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` do not
  block checklist creation.
- [X] T023 [US1] Add failing generated-view create tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` proving
  `Checklist` reports `readiness/007-checklist-command/work-model.json` and
  does not treat a missing generated file as current.
- [X] T024 [US1] Add failing real interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` proving
  `CommandEffects` creates `work/007-checklist-command/checklist.md` in a
  temporary initialized, chartered, specified, and clarified project.

### Implementation

- [X] T025 [US1] Route `Checklist` away from `unsupportedLifecycleCommand` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and plan the required project,
  specification, clarification, checklist, generated-view, and duplicate-id
  source reads, with any existing task or evidence source reads limited to
  work-model currency inputs rather than task or evidence behavior.
- [X] T026 [US1] Implement specification prerequisite validation for
  `Checklist` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, requiring
  `work/<id>/spec.md` front matter with matching work id and `stage: specify`
  before checklist writes.
- [X] T027 [US1] Implement clarification prerequisite validation for
  `Checklist` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, requiring
  `work/<id>/clarifications.md` front matter with matching work id, `stage:
  clarify`, matching `sourceSpec`, and no unresolved blocking ambiguity before
  checklist-ready can advance to planning.
- [X] T028 [US1] Implement requirements-quality evaluation in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` for testability, measurable
  success criteria, acceptance-scenario coverage, edge-case coverage, scope
  boundaries, dependency assumptions, remaining ambiguity, and absence of
  implementation-planning detail in the user-facing specification.
- [X] T029 [US1] Implement deterministic new-checklist template generation for
  `work/<id>/checklist.md` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, including YAML front matter,
  Source Specification, Source Clarifications, Source Snapshot, Checklist
  Items, Review Results, Accepted Deferrals, Blocking Findings, Advisory
  Notes, and Lifecycle Notes sections.
- [X] T030 [US1] Implement create-write planning for missing
  `work/<id>/checklist.md` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after T026 through T029.
- [X] T031 [US1] Implement checklist changed-artifact reporting, checklist
  summary reporting, generated-view placeholder state, and `plan` next-action
  required artifacts in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T032 [US1] Serialize the checklist `checklist` report object in
  documented order in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T033 [US1] Wire `checklist --work <id>` CLI dispatch and output selection
  through `src/FS.GG.SDD.Cli/Program.fs` without changing behavior for
  `init`, `charter`, `specify`, or `clarify`.
- [X] T034 [US1] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ChecklistCommand"` and
  capture successful-create evidence in
  `specs/007-checklist-command/readiness/checklist-create-tests.txt`.

**Checkpoint**: User Story 1 is independently testable and is the suggested
MVP scope for a first implementation slice.

---

## Phase 4: User Story 2 - Preserve And Refresh Checklist Review Decisions (Priority: P1)

**Goal**: Preserve authored checklist content on rerun, append compatible
items or results only when proven non-destructive, keep checklist ids stable,
and mark stale results when source facts change.

**Independent Test**: Run the safe-rerun fixtures and confirm existing items,
results, accepted deferrals, findings, advisory notes, and stable ids are
preserved; safe additions are deterministic; source changes mark affected
results stale; and unsafe conflicts produce blocking diagnostics with no
authored writes.

### Tests First

- [-] T035 [P] [US2] Populate — skipped: static manifest superseded by
  real temporary-directory rerun preservation tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/checklist-rerun-preserves-results/manifest.yml`
  with an existing checklist artifact containing authored items, results,
  accepted deferrals, blocking findings, advisory notes, lifecycle notes, and
  expected preserve or no-change report fixtures.
- [-] T036 [P] [US2] Populate — skipped: static manifest superseded by
  real temporary-directory safe-addition tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/checklist-adds-missing-items/manifest.yml`
  with changed specification or clarification facts that require deterministic
  append-only checklist item and result additions.
- [-] T037 [P] [US2] Populate — skipped: static manifest superseded by
  stable-id semantic tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/checklist-preserves-stable-ids/manifest.yml`
  with existing `CHK-###` and `CR-###` ids and expected append-only id
  allocation.
- [-] T038 [P] [US2] Populate — skipped: static manifest superseded by
  accepted-deferral parser/report coverage and readiness evidence.
  `tests/fixtures/lifecycle-commands/checklist-accepted-deferral/manifest.yml`
  with a requirements-quality concern resolved by an accepted deferral and
  expected durable deferral result facts.
- [-] T039 [P] [US2] Populate — skipped: static manifest superseded by
  stale-result semantic tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/checklist-stale-result/manifest.yml`
  with changed source snapshots and expected stale checklist result facts.
- [-] T040 [P] [US2] Populate — skipped: static manifest superseded by
  unsafe-result-change semantic tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/unsafe-checklist-result-change/manifest.yml`
  with an existing authored checklist result that a proposed rerun would
  semantically change and expected refusal diagnostics.
- [X] T041 [US2] Add failing rerun preservation and no-change tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/checklist-rerun-preserves-results/`.
- [X] T042 [US2] Add failing safe item addition and stable-id preservation
  tests in `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` for
  `tests/fixtures/lifecycle-commands/checklist-adds-missing-items/` and
  `tests/fixtures/lifecycle-commands/checklist-preserves-stable-ids/`.
- [X] T043 [US2] Add failing accepted-deferral tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`, proving
  deferrals use durable `CR-###` ids, remain visible in report facts, and
  allow `plan` only when no blocking finding or stale result remains.
- [X] T044 [US2] Add failing stale-result tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`, proving changed
  specification or clarification source snapshots mark related results stale
  and point next action to checklist review correction.
- [X] T045 [US2] Add failing unsafe-result-change tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`, proving existing
  checklist bytes are unchanged when a rerun would rewrite, renumber, or
  semantically change durable review results.

### Implementation

- [X] T046 [US2] Implement checklist front-matter parsing and validation in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` for `schemaVersion`,
  `workId`, `title`, `stage`, `changeTier`, `status`, `sourceSpec`, and
  `sourceClarifications`, including diagnose-only migration posture for
  missing, malformed, future, unsupported, or deprecated checklist schema
  versions.
- [X] T047 [US2] Implement checklist body section scanning and parsed fact
  extraction in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` for source
  snapshots, checklist items, review results, accepted deferrals, blocking
  findings, advisory notes, lifecycle notes, and source links to known
  specification or clarification ids.
- [X] T048 [US2] Implement source snapshot digest comparison and stale result
  classification in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` so changed
  source facts mark related `CR-###` results stale or needing review.
- [X] T049 [US2] Implement deterministic missing-section, missing-item, and
  missing-result append behavior in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` without rewriting existing
  section prose.
- [X] T050 [US2] Implement append-only `CHK-###` and `CR-###` stable id
  preservation and next-suffix allocation in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T051 [US2] Implement accepted checklist deferral modeling in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and summarize accepted
  deferral counts or ids in
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T052 [US2] Implement rerun conflict detection in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` for checklist identity mismatch,
  duplicate checklist ids, removed or renumbered ids, unknown source
  references, unsafe result changes, and destructive authored-section rewrites.
- [X] T053 [US2] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ChecklistCommand"` and
  capture rerun, stable-id, accepted-deferral, stale-result, and unsafe-result
  evidence in
  `specs/007-checklist-command/readiness/checklist-rerun-tests.txt`.

**Checkpoint**: User Story 2 is independently testable through fixture-backed
rerun behavior.

---

## Phase 5: User Story 3 - Diagnose Checklist Readiness Problems (Priority: P2)

**Goal**: Fail invalid checklist requests with stable, actionable diagnostics
and no unsafe writes when project context, work id, prerequisites, checklist
state, generated views, or requirements-quality facts are missing, malformed,
stale, or inconsistent.

**Independent Test**: Invoke checklist across blocked fixture families and
confirm authored checklist content is unchanged when writes are unsafe,
failed-quality findings are safely authored when source facts are valid,
generated-view state is diagnostic rather than assumed current, and each
blocked report contains a correction.

### Tests First

- [-] T054 [P] [US3] Populate or extend — skipped: static manifest updates
  superseded by invalid-context semantic tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`,
  `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml`,
  `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml`,
  `tests/fixtures/lifecycle-commands/missing-specification/manifest.yml`, and
  `tests/fixtures/lifecycle-commands/missing-clarification/manifest.yml` with
  checklist blocked-report expectations.
- [-] T055 [P] [US3] Populate — skipped: static manifest updates superseded
  by malformed/duplicate/unknown checklist semantic tests and readiness
  evidence.
  `tests/fixtures/lifecycle-commands/unresolved-ambiguity/manifest.yml`,
  `tests/fixtures/lifecycle-commands/failed-requirements-quality/manifest.yml`,
  `tests/fixtures/lifecycle-commands/malformed-checklist/manifest.yml`,
  `tests/fixtures/lifecycle-commands/duplicate-checklist-id/manifest.yml`,
  `tests/fixtures/lifecycle-commands/unknown-source-reference/manifest.yml`,
  and
  `tests/fixtures/lifecycle-commands/checklist-identity-mismatch/manifest.yml`
  with checklist diagnostic expectations.
- [-] T056 [P] [US3] Populate — skipped: static manifest updates superseded
  by unsafe-write and generated-view semantic tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/unsafe-overwrite/manifest.yml` and
  `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml` with
  checklist unsafe-write and generated-view expectations.
- [X] T057 [US3] Add failing invalid-context tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs` for outside
  project, malformed work id, duplicate work id, missing specification, and
  missing clarification scenarios.
- [X] T058 [US3] Add failing unresolved-ambiguity and
  failed-requirements-quality tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`, proving unresolved
  blocking ambiguity prevents planning and failed quality writes safe checklist
  findings with correction next action.
- [X] T059 [US3] Add failing malformed-checklist, duplicate-id,
  unknown-reference, identity-mismatch, and unsafe-overwrite tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`.
- [X] T060 [US3] Add failing generated-view diagnostic tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`, proving
  checklist distinguishes missing, stale, malformed, and blocked
  `readiness/<id>/work-model.json` states.
- [X] T061 [US3] Add failing blocking-effect tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`, proving
  `missingSpecificationPrerequisite`, `missingClarificationPrerequisite`,
  `unresolvedBlockingAmbiguity`, `duplicateChecklistId`,
  `unknownChecklistSourceReference`, `checklistIdentityMismatch`,
  `unsafeChecklistResultChange`, and `blockedGeneratedViewRefresh` diagnostics
  prevent unsafe authored `WriteFile` effects.

### Implementation

- [X] T062 [US3] Implement missing or malformed project, work id,
  duplicate-work-id, missing-specification, and missing-clarification
  diagnostics for `Checklist` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T063 [US3] Implement unresolved blocking ambiguity and failed
  requirements-quality diagnostics in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`, preserving the contract that
  failed quality creates or safely updates checklist output when source facts
  are valid.
- [X] T064 [US3] Implement malformed checklist front matter, duplicate
  checklist id, unknown source reference, checklist identity mismatch,
  unsafe-overwrite, and unsafe checklist result change diagnostics in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T065 [US3] Implement checklist generated-view currency classification in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs`, naming the source artifact for
  missing, stale, malformed, and blocked work-model states when available.
- [X] T066 [US3] Implement checklist next-action selection in
  `src/FS.GG.SDD.Commands/CommandReports.fs` for prerequisite correction,
  source correction, checklist review correction, and `plan` only when no
  blocking finding or stale result remains.
- [X] T067 [US3] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ChecklistCommand|FullyQualifiedName~GeneratedViewCommand|FullyQualifiedName~CommandWorkflow"`
  and capture invalid-context and generated-view evidence in
  `specs/007-checklist-command/readiness/checklist-diagnostics-tests.txt`.

**Checkpoint**: User Story 3 is independently testable through blocked fixture
families, safe failed-quality output, and generated-view diagnostics.

---

## Phase 6: User Story 4 - Keep Checklist Output Traceable (Priority: P3)

**Goal**: Emit deterministic checklist reports, render human text from the same
report, preserve dry-run immutability, and expose optional Governance pointers
only as compatibility facts.

**Independent Test**: Run identical checklist requests repeatedly against the
same fixture state, compare JSON bytes, compare text projection facts to JSON,
exercise dry-run mutation checks, and prove Governance files are optional.

### Tests First

- [-] T068 [P] [US4] Extend — skipped: static manifest updates superseded by
  deterministic-report, text-projection, dry-run, and Governance-boundary
  semantic tests and readiness evidence.
  `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`,
  `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`,
  `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, and
  `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml` with
  checklist-specific inputs and expected report facts without replacing
  existing specify or clarify expectations.
- [X] T069 [US4] Add failing deterministic checklist JSON tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`, proving three
  identical dry-run checklist executions over the same fixture produce
  byte-identical reports.
- [X] T070 [US4] Add failing checklist text projection tests in
  `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`, proving text mode
  includes only command-report facts for changed artifacts, checklist item
  count, passed count, failed blocking count, accepted deferral count, stale
  result count, generated-view state, diagnostics, and next action.
- [X] T071 [US4] Add failing checklist dry-run mutation tests in
  `tests/FS.GG.SDD.Commands.Tests/ChecklistCommandTests.fs`, proving no
  authored or generated artifact changes occur when `CommandRequest.DryRun` is
  true.
- [X] T072 [US4] Add failing checklist Governance-boundary tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`, proving
  absent Governance files do not block and present pointers are reported only
  as compatibility facts.

### Implementation

- [X] T073 [US4] Harden checklist report serialization ordering in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` for checklist ids, artifact
  changes, generated views, diagnostics, Governance compatibility facts, and
  next-action required artifacts.
- [X] T074 [US4] Implement checklist text projection in
  `src/FS.GG.SDD.Commands/CommandRendering.fs`, rendering checklist item
  count, passed count, failed blocking count, accepted deferral count, stale
  result count, advisory count, generated-view count, diagnostic count, and
  next action from the authoritative `CommandReport`.
- [X] T075 [US4] Implement checklist dry-run safe-write decisions in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs` so proposed authored and
  generated changes are reported without mutation.
- [X] T076 [US4] Implement optional Governance compatibility facts for
  checklist in `src/FS.GG.SDD.Commands/CommandReports.fs` without parsing
  Governance schemas or producing route, freshness, profile, gate, audit,
  protected-boundary, or release verdicts.
- [X] T077 [US4] Ensure checklist JSON and text rendering share the same
  `CommandReport` facts in `src/FS.GG.SDD.Commands/CommandRendering.fs` and
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` without adding separate
  lifecycle facts to text mode.
- [X] T078 [US4] Run
  `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson|FullyQualifiedName~TextProjection|FullyQualifiedName~GovernanceBoundaryCommand|FullyQualifiedName~ChecklistCommand"`
  and capture deterministic-report, text-projection, dry-run, and
  Governance-boundary evidence in
  `specs/007-checklist-command/readiness/checklist-traceability-tests.txt`.

**Checkpoint**: User Story 4 is independently testable through deterministic
JSON, text projection, dry-run, and no-Governance evidence.

---

## Phase 7: Integration & Polish

**Purpose**: Refresh public baselines, capture vertical-slice evidence, and
record readiness without expanding scope beyond `fsgg-sdd checklist`.

- [X] T079 [P] Refresh artifact public surface baselines in
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` after intentional
  checklist id and parser signature changes.
- [X] T080 [P] Refresh command public surface baselines in
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` after intentional
  checklist summary, report, diagnostic, and workflow signature changes.
- [X] T081 Run `dotnet build FS.GG.SDD.sln -c Release` and capture output in
  `specs/007-checklist-command/readiness/build-release.txt`.
- [X] T082 Run
  `dotnet build src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj -c Release`
  followed by `dotnet fsi scripts/prelude.fsx`, and capture the checklist
  public surface transcript in
  `specs/007-checklist-command/readiness/fsi-session.txt`.
- [X] T083 Run the disposable-directory CLI smoke path from
  `specs/007-checklist-command/quickstart.md` and capture output in
  `specs/007-checklist-command/readiness/cli-smoke.txt`.
- [X] T084 Run the `checklist-create` and `checklist-rerun-preserves-results`
  scenarios through the command test harness, verify each finishes under 2
  seconds on the local development machine, and capture performance evidence in
  `specs/007-checklist-command/readiness/performance.txt`.
- [X] T085 Review text output for changed artifact count, checklist item count,
  passed count, failed blocking count, accepted deferral count, stale result
  count, generated-view state, diagnostic count, and next action, then record
  the human summary review in
  `specs/007-checklist-command/readiness/human-summary-review.txt`.
- [X] T086 Record an SDD/Governance boundary review in
  `specs/007-checklist-command/readiness/sdd-governance-boundary-review.md`,
  confirming the feature introduces no `plan`, `tasks`, `analyze`, evidence
  update, verify, ship, release, generated agent guidance, Governance route,
  freshness, profile, gate, audit, protected-boundary, or release behavior.
- [X] T087 Update `docs/initial-implementation-plan.md` and `README.md` after
  implementation evidence is green to mark `007-checklist-command` as complete
  and describe the implemented checklist command without claiming later plan,
  tasks, evidence, verify, ship, generated agent guidance, or Governance
  behavior.
- [X] T088 Run `dotnet test FS.GG.SDD.sln` and capture the full-suite result in
  `specs/007-checklist-command/readiness/full-suite.txt`.
- [X] T089 Record artifact traceability from spec requirements, plan contracts,
  tasks, fixtures, tests, and readiness evidence in
  `specs/007-checklist-command/readiness/artifact-traceability.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; creates test and readiness files.
- **Foundation (Phase 2)**: Depends on Setup; blocks user-story
  implementation because `.fsi` contracts, test helpers, and failing MVU tests
  must exist first.
- **User Story 1 (Phase 3)**: Depends on Foundation; provides the MVP
  checklist create path.
- **User Story 2 (Phase 4)**: Depends on Foundation and is easiest after US1
  write planning exists; preserves and refreshes existing review decisions.
- **User Story 3 (Phase 5)**: Depends on Foundation; may be developed after or
  alongside US2 with coordination because it touches the same workflow and
  diagnostics files.
- **User Story 4 (Phase 6)**: Depends on report facts from US1 through US3;
  completes deterministic output, text projection, dry-run, and Governance
  compatibility.
- **Integration & Polish (Phase 7)**: Depends on all desired user stories.

### Story Dependencies

- **US1 (P1)**: MVP; no dependency on other stories after Foundation.
- **US2 (P1)**: Can be tested independently with existing-checklist fixtures,
  but implementation reuses the write-plan and parser contracts from US1.
- **US3 (P2)**: Can be tested independently with blocked fixtures, but
  implementation shares diagnostics and effect blocking with US1 and US2.
- **US4 (P3)**: Depends on checklist report facts from earlier stories.

### Parallel Opportunities

- T002, T004, and T005 can run in parallel during Setup.
- T014 and T015 can run in parallel with T016 through T019 after `.fsi`
  contracts exist.
- Fixture population tasks T020, T035 through T040, T054 through T056, and
  T068 can be split by fixture directory.
- Test additions in different files can run in parallel when they do not edit
  the same test file.
- US2 and US3 fixture/test work can begin in parallel after Foundation, but
  implementation changes in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and
  `src/FS.GG.SDD.Commands/CommandReports.fs` need coordination.
- Baseline refresh tasks T079 and T080 can run in parallel after public API
  changes are finalized.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation.
3. Complete Phase 3: User Story 1.
4. Stop and validate: run the focused checklist command tests and CLI smoke
   path for checklist creation.

### Incremental Delivery

1. Add US1 to create checklist artifacts and report `plan` as next.
2. Add US2 to preserve and safely refresh existing checklist review decisions.
3. Add US3 to harden diagnostics and generated-view failure modes.
4. Add US4 to finalize deterministic output, text projection, dry-run, and
   optional Governance compatibility.
5. Complete Integration & Polish with surface baselines and readiness evidence.

### Scope Guardrails

- Do not add `fsgg-sdd plan`, `tasks`, `analyze`, evidence update, verify,
  ship, release, generated agent guidance, or Governance enforcement behavior
  in this feature.
- Do not make generated views authoritative; report source and generator
  currency explicitly.
- Do not parse or enforce Governance-owned schemas; only expose optional
  compatibility facts.
