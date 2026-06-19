# Tasks: Native SDD Lifecycle Commands

**Feature branch**: `003-native-sdd-lifecycle-commands`
**Spec**: `specs/003-native-sdd-lifecycle-commands/spec.md`
**Plan**: `specs/003-native-sdd-lifecycle-commands/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/003-native-sdd-lifecycle-commands/`

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
`[P]`. This feature introduces stateful, filesystem-changing lifecycle
commands, so Principle V applies: public `.fsi` signatures must declare
`Model`, `Msg`, `Effect`, `init`, `update`, and interpreter boundaries before
`.fs` bodies harden; story completion requires pure transition tests,
emitted-effect assertions, and real interpreter evidence where safe.

Remediation tasks added after analysis keep appended task ids while remaining in
their execution phase, preserving existing task references.

---

## Phase 1: Setup

**Purpose**: Add command, CLI, test, fixture, and readiness roots required by
every user story.

- [X] T001 Create the command workflow library project
  `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` with package id
  `FS.GG.SDD.Commands`, `IsPackable=true`, a project reference to
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`, and compile includes
  for the planned `.fsi` files before matching `.fs` files.
- [X] T002 Create the CLI host project
  `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj` and `src/FS.GG.SDD.Cli/Program.fs`
  with a project reference to
  `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`.
- [X] T003 Add `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`,
  `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`, and the command test project to
  `FS.GG.SDD.sln` under the existing `src` and `tests` solution folders.
- [X] T004 Create the xUnit command test project
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` with
  project references to `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` and
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`.
- [X] T005 [P] Create command fixture roots with `manifest.yml` placeholders
  under `tests/fixtures/lifecycle-commands/init-empty-project/`,
  `tests/fixtures/lifecycle-commands/init-preserves-user-files/`,
  `tests/fixtures/lifecycle-commands/init-conflicting-lifecycle-path/`,
  `tests/fixtures/lifecycle-commands/lifecycle-through-analysis/`,
  `tests/fixtures/lifecycle-commands/outside-project/`,
  `tests/fixtures/lifecycle-commands/malformed-work-id/`,
  `tests/fixtures/lifecycle-commands/missing-prerequisites/`,
  `tests/fixtures/lifecycle-commands/malformed-artifact/`,
  `tests/fixtures/lifecycle-commands/unsafe-overwrite/`,
  `tests/fixtures/lifecycle-commands/unknown-reference/`,
  `tests/fixtures/lifecycle-commands/stale-generated-view/`,
  `tests/fixtures/lifecycle-commands/deterministic-report/`,
  `tests/fixtures/lifecycle-commands/text-projection/`, and
  `tests/fixtures/lifecycle-commands/governance-boundary/`.
- [X] T006 [P] Create `specs/003-native-sdd-lifecycle-commands/readiness/` with
  `specs/003-native-sdd-lifecycle-commands/readiness/README.md` documenting the
  build, test, FSI, CLI smoke, deterministic-report, text-projection, generated
  view, and Governance-boundary evidence files expected by this feature.
- [X] T007 [P] Confirm `Directory.Packages.props` contains the command feature
  package versions used by `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`,
  `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj`, and
  `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj`, adding only
  missing central versions.
- [X] T086 [P] Create the duplicate work-id command fixture root with a
  `manifest.yml` placeholder under
  `tests/fixtures/lifecycle-commands/duplicate-work-id/`.

**Checkpoint**: Project, fixture, and readiness roots exist; foundation
signatures and tests can be added without changing the planned layout.

---

## Phase 2: Foundation

**Purpose**: Declare public contracts, diagnostics, MVU boundaries, FSI usage,
baseline expectations, and shared test scaffolding before story implementation.

- [X] T008 Draft command identity, request, option, outcome, `Model`, `Msg`,
  `Effect`, safe-write, and next-action signatures in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`.
- [X] T009 Draft command report, artifact change, generated-view state,
  command diagnostic, and Governance compatibility signatures in
  `src/FS.GG.SDD.Commands/CommandReports.fsi`.
- [X] T010 Draft pure workflow signatures for `init` and `update` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fsi`, using the `Model`, `Msg`, and
  `Effect` types from `src/FS.GG.SDD.Commands/CommandTypes.fsi`.
- [X] T011 Draft edge interpreter and test interpreter signatures in
  `src/FS.GG.SDD.Commands/CommandEffects.fsi`, including real filesystem
  interpretation of read, enumerate, create directory, write file, stdout,
  stderr, and exit-code effects.
- [X] T012 Draft deterministic report serialization and plain text projection
  signatures in `src/FS.GG.SDD.Commands/CommandSerialization.fsi` and
  `src/FS.GG.SDD.Commands/CommandRendering.fsi`.
- [X] T013 Draft CLI argument normalization signatures in
  `src/FS.GG.SDD.Cli/Program.fs` for `init`, `charter`, `specify`, `clarify`,
  `checklist`, `plan`, `tasks`, `analyze`, `--root`, `--work`, `--title`,
  `--json`, `--text`, and `--dry-run`; keep `Program.fs` a thin non-public host
  and expose any reusable normalization contract through the command library
  `.fsi` files instead, unless a future feature explicitly introduces a CLI
  public API and matching `Program.fsi`.
- [X] T014 [P] Add compiling placeholder bodies in
  `src/FS.GG.SDD.Commands/CommandTypes.fs`,
  `src/FS.GG.SDD.Commands/CommandReports.fs`,
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`,
  `src/FS.GG.SDD.Commands/CommandEffects.fs`,
  `src/FS.GG.SDD.Commands/CommandSerialization.fs`, and
  `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [ ] T015 [P] Add shared command fixture and assertion helpers in
  `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` for repository-relative path
  normalization, fixture copying, digest assertions, diagnostic assertions, and
  captured effect interpretation.
- [X] T016 [P] Add the initial command public surface baseline in
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` and a failing
  comparison test in
  `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`.
- [X] T017 [P] Add failing pure MVU baseline tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` proving `init` and
  `update` do not touch the filesystem directly, blocking diagnostics prevent
  write effects, and final reports are built for blocked commands.
- [ ] T018 [P] Add failing command diagnostic contract tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` for outside-project,
  unknown-command, malformed-work-id, missing-prerequisite, unsafe-overwrite,
  stale-generated-view, blocked-refresh, optional-Governance-boundary, and tool
  defect diagnostic ids.
- [ ] T019 [P] Add failing test-interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` proving interpreted
  effect results feed back through `EffectInterpreted` messages without host
  filesystem writes.
- [X] T020 Update `scripts/prelude.fsx` to construct a representative
  `CommandRequest`, call the public `CommandWorkflow.init` and
  `CommandWorkflow.update` functions, print command name, outcome,
  changed-artifact count, generated-view count, blocking diagnostic count, and
  next action.
- [X] T021 Run `dotnet fsi scripts/prelude.fsx` after T020 and capture the
  draft public-surface transcript in
  `specs/003-native-sdd-lifecycle-commands/readiness/fsi-draft-public-surface.txt`.
- [X] T022 Implement the minimum placeholder behavior needed for `dotnet build
  FS.GG.SDD.sln` to compile after the foundation signatures in
  `src/FS.GG.SDD.Commands/`, `src/FS.GG.SDD.Cli/`, and
  `tests/FS.GG.SDD.Commands.Tests/`.

**Checkpoint**: Public surface is declared, executable from FSI, covered by
baseline tests, and ready for story implementation.

---

## Phase 3: User Story 1 - Start An SDD Project Locally (Priority: P1) - MVP

**Goal**: Initialize an SDD-governed project skeleton without hidden FS.GG
repository knowledge or Governance runtime requirements.

**Independent Test**: Run `InitCommandTests` against the init fixtures and run
the CLI `init` smoke path in a disposable directory, confirming `.fsgg`
configuration, `work/`, `readiness/`, agent guidance targets, safe-write
behavior, and deterministic command reports.

### Tests First

- [ ] T023 [P] [US1] Populate
  `tests/fixtures/lifecycle-commands/init-empty-project/` with expected
  `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/`,
  `readiness/`, agent target, and golden command report fixture files.
- [ ] T024 [P] [US1] Populate
  `tests/fixtures/lifecycle-commands/init-preserves-user-files/` with
  unrelated user files plus expected preserve/no-overwrite command report
  fixtures.
- [ ] T025 [P] [US1] Populate
  `tests/fixtures/lifecycle-commands/init-conflicting-lifecycle-path/` with
  conflicting `.fsgg` and agent guidance files plus expected unsafe-overwrite
  diagnostics.
- [X] T026 [P] [US1] Add failing initialization smoke tests in
  `tests/FS.GG.SDD.Commands.Tests/InitCommandTests.fs` for empty-directory
  skeleton creation, no-Governance operation, expected artifact changes, and
  `succeeded` command reports.
- [X] T027 [P] [US1] Add failing safe-write preservation tests in
  `tests/FS.GG.SDD.Commands.Tests/InitCommandTests.fs` for unrelated user file
  preservation and conflicting lifecycle path refusal.
- [X] T028 [P] [US1] Add failing pure transition and emitted-effect tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` for `fsgg-sdd init`
  planning `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/`,
  `readiness/`, and agent guidance target effects before interpretation.
- [X] T029 [P] [US1] Add failing real interpreter evidence tests in
  `tests/FS.GG.SDD.Commands.Tests/InitCommandTests.fs` proving `CommandEffects`
  creates directories and writes skeleton files in a temporary directory while
  refusing unsafe authored-content overwrites.

### Implementation

- [X] T030 [US1] Implement project-root normalization, initial SDD skeleton
  planning, and no-Governance defaults for `Init` in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after T026 and T028.
- [X] T031 [US1] Implement safe-write decisions and artifact change reporting
  for `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/`,
  `readiness/`, `AGENTS.md`, and `CLAUDE.md` in
  `src/FS.GG.SDD.Commands/CommandReports.fs` after T027.
- [X] T032 [US1] Implement real filesystem directory creation, file writes,
  preserve decisions, and unsafe-write refusal in
  `src/FS.GG.SDD.Commands/CommandEffects.fs` after T029.
- [X] T033 [US1] Implement `fsgg-sdd init` CLI parsing, workflow invocation,
  effect interpretation, report emission, and exit-code mapping in
  `src/FS.GG.SDD.Cli/Program.fs` after T030 through T032.
- [X] T034 [US1] Update `scripts/prelude.fsx` to exercise the init request
  path and capture the real init interpreter transcript in
  `specs/003-native-sdd-lifecycle-commands/readiness/init-interpreter.txt`.
- [X] T035 [US1] Run the disposable-directory CLI init smoke path from
  `specs/003-native-sdd-lifecycle-commands/quickstart.md` and capture command
  output in `specs/003-native-sdd-lifecycle-commands/readiness/init-cli-smoke.txt`.

**Checkpoint**: User Story 1 is independently testable and is the suggested MVP
scope for implementation.

---

## Phase 4: User Story 2 - Author Work Through The Lifecycle (Priority: P1)

**Goal**: Provide native commands for `charter`, `specify`, `clarify`,
`checklist`, `plan`, `tasks`, and `analyze` so a work item can advance from
intent to implementation-ready analysis through one lifecycle contract.

**Independent Test**: Run `LifecycleCommandTests` over the lifecycle fixtures
and confirm each command creates or updates the expected artifact, reports
missing or malformed prerequisites without guessing, emits command diagnostics,
and leaves a selected work item ready for implementation planning after
`analyze`.

### Tests First

- [ ] T036 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/lifecycle-through-analysis/` with valid
  `.fsgg/` sources, `work/003-native-sdd-lifecycle-commands/` expected
  lifecycle artifacts, `work-model.json`, `analysis.json`, and golden command
  reports for `charter`, `specify`, `clarify`, `checklist`, `plan`, `tasks`,
  and `analyze`.
- [ ] T037 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/outside-project/`,
  `tests/fixtures/lifecycle-commands/malformed-work-id/`, and
  `tests/fixtures/lifecycle-commands/missing-prerequisites/` with expected
  blocked reports and diagnostics for commands after `init`.
- [ ] T038 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/malformed-artifact/`,
  `tests/fixtures/lifecycle-commands/unsafe-overwrite/`, and
  `tests/fixtures/lifecycle-commands/unknown-reference/` with malformed
  schema, authored-content conflict, and unknown-reference inputs plus expected
  diagnostics.
- [ ] T039 [P] [US2] Add failing lifecycle progression tests in
  `tests/FS.GG.SDD.Commands.Tests/LifecycleCommandTests.fs` for advancing one
  work item from `charter` through `analyze` and verifying each expected
  artifact path.
- [ ] T040 [P] [US2] Add failing prerequisite and input validation tests in
  `tests/FS.GG.SDD.Commands.Tests/LifecycleCommandTests.fs` for outside-project
  use, malformed work ids, missing prior artifacts, malformed artifacts,
  unsafe overwrites, and unknown references.
- [ ] T041 [P] [US2] Add failing pure transition tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs` for work-item
  commands loading project state, loading selected work-item sources, applying
  user intent, planning safe writes, and producing `NextAction` values.
- [ ] T042 [P] [US2] Add failing command report tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs` proving lifecycle
  command reports include command identity, selected work id, lifecycle stage,
  changed artifacts, diagnostics, outcome, and next expected action.
- [ ] T043 [P] [US2] Add failing real interpreter tests in
  `tests/FS.GG.SDD.Commands.Tests/LifecycleCommandTests.fs` proving authored
  files under `work/<id>/` and contracts under `work/<id>/contracts/` are
  created or preserved safely in a temporary directory.
- [ ] T087 [P] [US2] Populate
  `tests/fixtures/lifecycle-commands/duplicate-work-id/` with a selected work id
  that conflicts with existing work-item structured metadata, plus expected
  blocked command reports and duplicate work-id diagnostics.
- [ ] T088 [P] [US2] Add failing duplicate work-id tests in
  `tests/FS.GG.SDD.Commands.Tests/LifecycleCommandTests.fs` proving selected
  work id collisions, structured metadata mismatches, and duplicate logical
  work ids produce stable diagnostics before any write effect is planned.
- [ ] T089 [P] [US2] Add failing analyze artifact-family consistency tests in
  `tests/FS.GG.SDD.Commands.Tests/LifecycleCommandTests.fs` proving `analyze`
  reports consistency diagnostics for SDD project settings, work-item metadata,
  specification, clarifications, checklist results, plan, contracts, and task
  graph artifacts individually.

### Implementation

- [ ] T044 [US2] Implement SDD project discovery, `.fsgg/project.yml`,
  `.fsgg/sdd.yml`, `.fsgg/agents.yml` loading, and outside-project diagnostics
  in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after T040.
- [ ] T045 [US2] Implement work-id validation and selected work-item state
  loading for `work/<id>/` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`,
  including selected-id versus structured-metadata mismatch detection, after
  T040, T041, and T088.
- [ ] T046 [US2] Implement `charter`, `specify`, `clarify`, `checklist`,
  `plan`, `tasks`, and `analyze` command planning in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, preserving user-authored prose
  and refusing unsupported guesses after T039 through T043.
- [ ] T047 [US2] Implement lifecycle artifact templates and structured
  `tasks.yml` generation for `work/<id>/charter.md`, `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, `work/<id>/checklist.md`,
  `work/<id>/plan.md`, `work/<id>/contracts/`, and `work/<id>/tasks.yml` in
  `src/FS.GG.SDD.Commands/CommandTypes.fs` and
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [ ] T048 [US2] Implement prerequisite diagnostics, malformed artifact
  diagnostics, duplicate work-id diagnostics, unsafe overwrite diagnostics, and
  unknown-reference diagnostics in `src/FS.GG.SDD.Commands/CommandReports.fs`,
  reusing `FS.GG.SDD.Artifacts.Diagnostics` ids where possible after T040 and
  T088.
- [ ] T049 [US2] Implement `analyze` result assembly and
  `readiness/<id>/analysis.json` effect planning in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, explicitly aggregating SDD
  project settings, work-item metadata, specification, clarifications,
  checklist results, plan, contracts, and task graph artifact diagnostics,
  without executing verify, ship, route, profile, freshness, gate, or
  enforcement behavior after T089.
- [ ] T050 [US2] Implement CLI parsing and dispatch for `charter`, `specify`,
  `clarify`, `checklist`, `plan`, `tasks`, and `analyze` in
  `src/FS.GG.SDD.Cli/Program.fs` after T044 through T049.
- [ ] T051 [US2] Capture lifecycle-through-analysis CLI evidence from a
  temporary directory in
  `specs/003-native-sdd-lifecycle-commands/readiness/lifecycle-cli-smoke.txt`
  using the commands from `specs/003-native-sdd-lifecycle-commands/quickstart.md`.

**Checkpoint**: User Story 2 is independently testable through lifecycle
fixtures and command smoke evidence.

---

## Phase 5: User Story 3 - Trust Command Reports And Generated-View Currency (Priority: P2)

**Goal**: Ensure every command returns deterministic machine-readable results
and human-readable text from the same facts, while reporting generated-view
currency accurately.

**Independent Test**: Run `CommandReportJsonTests`, `TextProjectionTests`, and
`GeneratedViewCommandTests` over deterministic, text projection, and stale-view
fixtures; compare three dry-run JSON reports byte-for-byte.

### Tests First

- [ ] T052 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/deterministic-report/` with stable source
  snapshots and expected byte-identical command report JSON outputs for three
  dry-run executions.
- [ ] T053 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/text-projection/` with command reports and
  expected plain text projection fixtures that contain no extra lifecycle
  facts.
- [ ] T054 [P] [US3] Populate
  `tests/fixtures/lifecycle-commands/stale-generated-view/` with missing,
  stale-source-digest, stale-generator-version, malformed, and blocked
  generated-view inputs plus expected diagnostics.
- [ ] T055 [P] [US3] Add failing deterministic JSON tests in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs` for top-level
  property order, sorted collections, lowercase SHA-256 digests, absence of
  timestamps and absolute paths, and byte-identical output across three dry-run
  runs.
- [ ] T056 [P] [US3] Add failing plain text projection tests in
  `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs` proving text output
  is rendered from `CommandReport` and introduces no facts absent from JSON.
- [ ] T057 [P] [US3] Add failing generated-view command tests in
  `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs` proving valid
  sources refresh `readiness/<id>/work-model.json`, `analyze` emits
  `readiness/<id>/analysis.json`, and invalid sources report blocked or stale
  view diagnostics.

### Implementation

- [ ] T058 [US3] Implement deterministic command report serialization in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs`, matching
  `specs/003-native-sdd-lifecycle-commands/contracts/command-report-json.md`
  after T055.
- [ ] T059 [US3] Implement plain text projection in
  `src/FS.GG.SDD.Commands/CommandRendering.fs`, rendering only command name,
  outcome, changed artifact summary, generated-view summary, diagnostics
  summary, and next action from the report after T056.
- [ ] T060 [US3] Implement generated `work-model.json` refresh planning through
  the existing `FS.GG.SDD.Artifacts.Serialization.generateWorkModel` API in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` after T057.
- [ ] T061 [US3] Implement generated-view state reporting for `current`,
  `missing`, `stale`, `malformed`, and `blocked` statuses in
  `src/FS.GG.SDD.Commands/CommandReports.fs` after T054 and T057.
- [ ] T062 [US3] Implement deterministic JSON and text output selection in
  `src/FS.GG.SDD.Cli/Program.fs`, including stdout, stderr, and exit-code
  effects interpreted from the same `CommandReport`.
- [ ] T063 [US3] Capture three-run dry-run report evidence in
  `specs/003-native-sdd-lifecycle-commands/readiness/deterministic-report.txt`
  using `tests/fixtures/lifecycle-commands/deterministic-report/`.
- [ ] T064 [US3] Capture generated-view and text projection evidence in
  `specs/003-native-sdd-lifecycle-commands/readiness/generated-view-and-text.txt`
  using `tests/fixtures/lifecycle-commands/stale-generated-view/` and
  `tests/fixtures/lifecycle-commands/text-projection/`.

**Checkpoint**: User Story 3 is independently testable through deterministic
report, text projection, and generated-view currency fixtures.

---

## Phase 6: User Story 4 - Keep SDD Separate From Governance Enforcement (Priority: P3)

**Goal**: Emit optional Governance compatibility facts without making SDD
responsible for route selection, freshness policy, profiles, gates, or
protected-boundary enforcement.

**Independent Test**: Run `GovernanceBoundaryCommandTests` with Governance
files absent, present, malformed, and incomplete; confirm SDD-only commands
continue to work and reports contain compatibility facts only.

### Tests First

- [ ] T065 [P] [US4] Populate
  `tests/fixtures/lifecycle-commands/governance-boundary/` with absent,
  present, malformed, and incomplete optional Governance file scenarios plus
  expected compatibility fact report entries.
- [ ] T066 [P] [US4] Add failing no-Governance tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs` proving
  lifecycle commands work when `.fsgg/policy.yml`, `.fsgg/capabilities.yml`,
  and `.fsgg/tooling.yml` are absent.
- [ ] T067 [P] [US4] Add failing optional-boundary tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs` proving
  present Governance pointers are reported as compatibility facts without
  policy parsing or enforcement.
- [ ] T068 [P] [US4] Add failing boundary-regression tests in
  `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs` proving
  command reports never include route, profile, freshness, gate, audit, release,
  protected-branch, or enforcement verdict fields.

### Implementation

- [ ] T069 [US4] Implement optional Governance compatibility fact discovery in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` from SDD-owned configuration
  paths only after T065 through T067.
- [ ] T070 [US4] Implement compatibility fact serialization and sorting in
  `src/FS.GG.SDD.Commands/CommandReports.fs` and
  `src/FS.GG.SDD.Commands/CommandSerialization.fs` after T067.
- [ ] T071 [US4] Add explicit tests and implementation guards in
  `src/FS.GG.SDD.Commands/CommandWorkflow.fs` so SDD does not parse Governance
  policy schemas, select routes, evaluate freshness, adjust profiles, select
  gates, or emit protected-boundary verdicts after T068.
- [ ] T072 [US4] Capture no-Governance and optional-boundary CLI evidence in
  `specs/003-native-sdd-lifecycle-commands/readiness/governance-boundary.txt`
  using `tests/fixtures/lifecycle-commands/governance-boundary/`.

**Checkpoint**: User Story 4 is independently testable and preserves the
FS.GG.SDD / FS.GG.Governance ownership boundary.

---

## Phase 7: Polish & Cross-Cutting Verification

**Purpose**: Validate the whole Tier 1 command surface, update guidance and
documentation projections, and capture implementation evidence.

- [X] T073 Refresh
  `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` after intentional
  public API changes and confirm
  `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs` passes.
- [X] T074 Run `dotnet build FS.GG.SDD.sln` and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/build.txt`.
- [X] T075 Run `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandWorkflow"`
  and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/command-workflow-tests.txt`.
- [X] T076 Run `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~InitCommand"`
  and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/init-command-tests.txt`.
- [ ] T077 Run `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~LifecycleCommand"`
  and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/lifecycle-command-tests.txt`.
- [X] T078 Run `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~CommandReportJson"`
  and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/command-report-json-tests.txt`.
- [X] T079 Run `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~TextProjection"`
  and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/text-projection-tests.txt`.
- [ ] T080 Run `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GeneratedViewCommand"`
  and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/generated-view-command-tests.txt`.
- [X] T081 Run `dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GovernanceBoundaryCommand"`
  and capture the output in
  `specs/003-native-sdd-lifecycle-commands/readiness/governance-boundary-command-tests.txt`.
- [X] T082 Run `dotnet test FS.GG.SDD.sln` and capture the full-suite output in
  `specs/003-native-sdd-lifecycle-commands/readiness/full-test-suite.txt`.
- [X] T083 Update `README.md` and `docs/initial-implementation-plan.md` to
  reflect the implemented native command feature without claiming verify, ship,
  release, generated agent guidance, or Governance enforcement behavior.
- [-] T084 Skipped because this slice did not change standing native command
  workflow guidance beyond the already-synchronized active-plan pointers in
  `AGENTS.md` and `CLAUDE.md`; no additional guidance diff is required.
- [X] T085 Confirm every synthetic fixture or test dependency is disclosed in
  fixture names, test names, comments, or readiness notes, and record the
  synthetic-evidence review in
  `specs/003-native-sdd-lifecycle-commands/readiness/synthetic-evidence-review.txt`.
- [ ] T090 Capture a command-report traceability walkthrough in
  `specs/003-native-sdd-lifecycle-commands/readiness/traceability-walkthrough.txt`
  proving a representative command result can be traced from report fields to
  affected authored artifacts, generated views, source digests, diagnostics, and
  next action within the `SC-006` 10-minute target.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundation (Phase 2)**: Depends on Setup completion; blocks all user-story
  implementation.
- **User Story 1 (Phase 3)**: Depends on Foundation and is the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundation; can begin after US1 if the
  CLI init skeleton is needed for smoke evidence, but pure workflow tests can
  be drafted independently after Foundation.
- **User Story 3 (Phase 5)**: Depends on Foundation and integrates most
  naturally after US1 and US2 reports exist.
- **User Story 4 (Phase 6)**: Depends on Foundation and can be implemented in
  parallel with US3 after command reports exist.
- **Polish (Phase 7)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on other stories after Foundation.
- **User Story 2 (P1)**: Uses the same command workflow and interpreter
  contracts as US1; lifecycle progression smoke evidence depends on an
  initialized project.
- **User Story 3 (P2)**: Depends on command reports from US1 and US2 for full
  fixture coverage; serialization and text projection tests can be written
  earlier against synthetic reports.
- **User Story 4 (P3)**: Depends on command report compatibility facts and must
  remain independent of Governance runtime packages.

### Parallel Opportunities

- T005, T006, T007, and T086 can run in parallel during Setup.
- T014 through T019 can run in parallel during Foundation after signatures are
  drafted.
- T023 through T029 can run in parallel for US1 before implementation tasks.
- T036 through T043 and T087 through T089 can run in parallel for US2 before
  implementation tasks.
- T052 through T057 can run in parallel for US3 before implementation tasks.
- T065 through T068 can run in parallel for US4 before implementation tasks.
- US3 and US4 implementation can proceed in parallel after shared command report
  shapes are stable.

### Within Each User Story

- Tests and fixtures come before implementation.
- `.fsi` contracts come before `.fs` implementation.
- Pure `init` / `update` transition tests come before real interpreter wiring.
- Emitted-effect assertions come before CLI smoke evidence.
- Story tasks are only complete when the user-facing command path has real
  evidence under `specs/003-native-sdd-lifecycle-commands/readiness/`.

---

## Implementation Strategy

### MVP First

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation.
3. Complete Phase 3: User Story 1.
4. Validate `fsgg-sdd init` independently through tests, FSI, and CLI smoke
   evidence.

### Incremental Delivery

1. Add US1 init skeleton and safe-write behavior.
2. Add US2 lifecycle authoring commands and analysis.
3. Add US3 deterministic reports, text projection, and generated-view currency.
4. Add US4 optional Governance compatibility facts and boundary regression
   tests.
5. Run Phase 7 cross-cutting verification before analysis or merge.

### Parallel Team Strategy

After Foundation:

- One implementer can work on US1 init fixtures, workflow, and interpreter.
- A second implementer can draft US2 lifecycle fixtures and pure transition
  tests.
- A third implementer can draft US3 deterministic report and text projection
  tests against synthetic command reports.
- US4 can proceed once report compatibility facts are stable.

---

## Notes

- `[P]` tasks operate on separate files or independent fixtures and have no
  dependency on another incomplete task in the same phase.
- This task list intentionally does not create `tasks.deps.yml`, a DAG
  validator, or an evidence audit file.
- The feature does not introduce task/evidence update commands, `fsgg-sdd
  verify`, `fsgg-sdd ship`, full refresh commands, generated agent command
  files, product runtime templates, release behavior, or Governance enforcement.
