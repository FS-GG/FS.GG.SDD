# Tasks: SDD Artifact Model

**Feature branch**: `001-sdd-artifact-model`
**Spec**: `specs/001-sdd-artifact-model/spec.md`
**Plan**: `specs/001-sdd-artifact-model/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/001-sdd-artifact-model/`

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
- `[US1]`, `[US2]`, `[US3]` - user-story scope
- `[T1]` / `[T2]` - tier annotation, omitted here because the feature overall is
  Tier 1

Phases run in sequence. Tasks within a phase may run in parallel when marked
`[P]`. This feature is a pure artifact-model library, so Elmish/MVU task
obligations are not applicable; no stateful command, generator, or I/O workflow
is introduced.

Remediation tasks added after analysis keep appended task ids while remaining in
their execution phase, preserving existing task references.

---

## Phase 1: Setup

**Purpose**: Create the F# library, test project, dependency metadata, and
fixture roots required by every user story.

- [X] T001 Create `FS.GG.SDD.sln` containing solution folders for
  `src/FS.GG.SDD.Artifacts/` and `tests/FS.GG.SDD.Artifacts.Tests/`.
- [X] T002 Create `Directory.Build.props` with `net10.0`, deterministic build
  settings, nullable/analyzer posture where supported, and package metadata for
  `FS.GG.SDD.*`.
- [X] T003 Create `Directory.Packages.props` with central package versions for
  `FSharp.Core`, `YamlDotNet`, `System.Text.Json`, `xunit`, `xunit.runner.visualstudio`,
  and `Microsoft.NET.Test.Sdk`.
- [X] T004 Create the packable library project
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj` with `.fsi` files listed
  before matching `.fs` files.
- [X] T005 Create the xUnit project
  `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` referencing
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj`.
- [X] T006 [P] Create the public-surface exercise script
  `scripts/prelude.fsx` that references the library source or built assembly
  and sketches representative identifier, artifact, diagnostic, and work-model
  usage.
- [X] T007 [P] Create fixture directory roots and placeholder manifests under
  `tests/fixtures/sdd-artifact-model/valid-work-item/`,
  `tests/fixtures/sdd-artifact-model/malformed-schema-version/`,
  `tests/fixtures/sdd-artifact-model/missing-artifact/`,
  `tests/fixtures/sdd-artifact-model/duplicate-identifiers/`,
  `tests/fixtures/sdd-artifact-model/unknown-reference/`,
  `tests/fixtures/sdd-artifact-model/prose-structured-mismatch/`,
  `tests/fixtures/sdd-artifact-model/stale-generated-view/`, and
  `tests/fixtures/sdd-artifact-model/deterministic-ordering/`.
- [X] T008 Record feature evidence obligations, including "MVU not applicable:
  pure artifact-model library only", in
  `specs/001-sdd-artifact-model/tasks.md`.

**Checkpoint**: Project skeleton exists; foundation signatures and tests can be
added without changing the planned file layout.

---

## Phase 2: Foundation

**Purpose**: Establish public contracts, FSI usage, baseline expectations, and
common test helpers before story implementation.

- [X] T009 Draft identifier public signatures in
  `src/FS.GG.SDD.Artifacts/Identifiers.fsi` for `WorkId`, `LifecycleStage`,
  `RequirementId`, `DecisionId`, `TaskId`, and `EvidenceId`.
- [X] T010 Draft schema, digest, and artifact-reference signatures in
  `src/FS.GG.SDD.Artifacts/SchemaVersion.fsi` and
  `src/FS.GG.SDD.Artifacts/ArtifactRef.fsi`.
- [X] T011 Draft diagnostics and generation-manifest signatures in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` and
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`.
- [X] T012 Draft lifecycle artifact and work-model signatures in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` and
  `src/FS.GG.SDD.Artifacts/WorkModel.fsi`.
- [X] T013 Draft lifecycle rule contract and serialization signatures in
  `src/FS.GG.SDD.Artifacts/LifecycleRuleContracts.fsi` and
  `src/FS.GG.SDD.Artifacts/Serialization.fsi`.
- [X] T056 [P] Draft pure YAML parsing and file-snapshot loading signatures in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` and
  `src/FS.GG.SDD.Artifacts/Serialization.fsi` so parser ownership is explicit
  before implementation.
- [X] T014 [P] Add shared fixture-loading and assertion helpers in
  `tests/FS.GG.SDD.Artifacts.Tests/TestSupport.fs`.
- [X] T015 [P] Add the initial public surface baseline file
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` naming every public
  module and exported member expected from the `.fsi` files.
- [X] T016 Exercise the draft signatures through `scripts/prelude.fsx` and
  capture the transcript in `specs/001-sdd-artifact-model/readiness/fsi-session.txt`.
- [X] T017 Add compiling placeholder `.fs` module bodies in
  `src/FS.GG.SDD.Artifacts/Identifiers.fs`,
  `src/FS.GG.SDD.Artifacts/SchemaVersion.fs`,
  `src/FS.GG.SDD.Artifacts/ArtifactRef.fs`,
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs`,
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fs`,
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs`,
  `src/FS.GG.SDD.Artifacts/WorkModel.fs`,
  `src/FS.GG.SDD.Artifacts/LifecycleRuleContracts.fs`, and
  `src/FS.GG.SDD.Artifacts/Serialization.fs`, then add a failing
  surface-baseline test in
  `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs` that compares the
  compiled public API to
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`.

**Checkpoint**: Public surface is declared and executable from FSI before `.fs`
implementation begins.

---

## Phase 3: User Story 1 - Define The Lifecycle Contract (Priority: P1) - MVP

**Goal**: Define one explicit SDD artifact contract covering project-level
files, work-item files, generated views, authority rules, and agent guidance
targets.

**Independent Test**: Review and run the US1 tests to trace a valid work item
from `.fsgg/project.yml` through requirements, decisions, tasks, evidence, and
`readiness/<id>/work-model.json` with zero blocking diagnostics.

### Tests First

- [X] T018 [P] [US1] Add failing identifier and stage semantic tests in
  `tests/FS.GG.SDD.Artifacts.Tests/IdentifierTests.fs` for `WorkId`,
  `LifecycleStage`, `RequirementId`, `DecisionId`, `TaskId`, and `EvidenceId`
  validation from `specs/001-sdd-artifact-model/data-model.md`.
- [X] T019 [P] [US1] Add failing schema and artifact contract tests in
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaContractTests.fs` for
  `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, work-item front
  matter, `tasks.yml`, and `evidence.yml` using
  `tests/fixtures/sdd-artifact-model/valid-work-item/`.
- [X] T020 [P] [US1] Add failing work-model shape tests in
  `tests/FS.GG.SDD.Artifacts.Tests/WorkModelTests.fs` for the top-level
  `readiness/<id>/work-model.json` fields and deterministic collection ordering
  documented in `specs/001-sdd-artifact-model/contracts/work-model-json.md`.
- [X] T057 [P] [US1] Add failing lifecycle artifact inventory tests in
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaContractTests.fs` proving every
  SDD-owned artifact named in `docs/initial-implementation-plan.md` has an
  owner, purpose, source of truth, structured contract, generated-view
  relationship, stale behavior, and diagnostic family.

### Implementation

- [X] T021 [US1] Implement typed lifecycle identifiers and stage validation in
  `src/FS.GG.SDD.Artifacts/Identifiers.fs` after T018.
- [X] T022 [US1] Implement schema version, digest, and artifact reference types
  in `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` and
  `src/FS.GG.SDD.Artifacts/ArtifactRef.fs` after T019.
- [X] T023 [US1] Implement project-level and work-item lifecycle artifact
  records in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` from
  `specs/001-sdd-artifact-model/contracts/artifact-schemas.md`.
- [X] T024 [US1] Implement generation manifest records in
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fs` with source digest,
  generator version, output digest, and stale-view metadata fields.
- [X] T025 [US1] Implement normalized work-model records and stable ordering
  functions in `src/FS.GG.SDD.Artifacts/WorkModel.fs`.
- [X] T026 [US1] Implement JSON serialization for the documented work-model
  property order in `src/FS.GG.SDD.Artifacts/Serialization.fs`.
- [X] T027 [US1] Populate the valid fixture sources in
  `tests/fixtures/sdd-artifact-model/valid-work-item/` with `.fsgg/project.yml`,
  `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/001-sdd-artifact-model/spec.md`,
  `work/001-sdd-artifact-model/tasks.yml`,
  `work/001-sdd-artifact-model/evidence.yml`, and
  `readiness/001-sdd-artifact-model/work-model.json`.
- [X] T028 [US1] Update `scripts/prelude.fsx` to exercise the US1 public
  surface and validate the valid fixture can produce a zero-blocking-diagnostic
  work model.

**Checkpoint**: User Story 1 is independently testable and provides the MVP
artifact contract.

---

## Phase 4: User Story 2 - Diagnose Invalid Or Conflicting Work (Priority: P2)

**Goal**: Produce stable, actionable diagnostics for malformed schema versions,
duplicate ids, unknown references, prose/structured mismatches, stale generated
views, malformed digests, and deterministic output ordering.

**Independent Test**: Run US2 tests over each invalid fixture directory and
confirm the expected diagnostic ids, affected artifacts, corrections, and stable
ordering.

### Tests First

- [X] T029 [P] [US2] Add failing diagnostic model tests in
  `tests/FS.GG.SDD.Artifacts.Tests/DiagnosticTests.fs` for diagnostic id,
  severity, artifact reference, source location, correction, related ids, and
  sorting.
- [X] T030 [P] [US2] Add failing malformed-schema fixture tests in
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaContractTests.fs` using
  `tests/fixtures/sdd-artifact-model/malformed-schema-version/`.
- [X] T058 [P] [US2] Add failing missing-artifact fixture tests in
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaContractTests.fs` using
  `tests/fixtures/sdd-artifact-model/missing-artifact/` to prove absent
  required lifecycle artifacts produce `missingArtifact` diagnostics with
  affected paths and corrections.
- [X] T031 [P] [US2] Add failing duplicate and unknown-reference fixture tests
  in `tests/FS.GG.SDD.Artifacts.Tests/WorkModelTests.fs` using
  `tests/fixtures/sdd-artifact-model/duplicate-identifiers/` and
  `tests/fixtures/sdd-artifact-model/unknown-reference/`.
- [X] T032 [P] [US2] Add failing prose/structured mismatch and stale-view tests
  in `tests/FS.GG.SDD.Artifacts.Tests/WorkModelTests.fs` using
  `tests/fixtures/sdd-artifact-model/prose-structured-mismatch/` and
  `tests/fixtures/sdd-artifact-model/stale-generated-view/`.
- [X] T033 [P] [US2] Add failing deterministic JSON tests in
  `tests/FS.GG.SDD.Artifacts.Tests/DeterministicJsonTests.fs` that run
  `tests/fixtures/sdd-artifact-model/deterministic-ordering/` three times and
  compare byte-identical output.

### Implementation

- [X] T034 [US2] Implement diagnostic ids, severity ordering, source locations,
  related ids, and correction helpers in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs` after T029.
- [X] T035 [US2] Implement strict schema-version validation and digest
  validation in `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` and
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fs` after T030.
- [X] T036 [US2] Implement duplicate-id, unknown-reference, and acyclic task
  dependency checks in `src/FS.GG.SDD.Artifacts/WorkModel.fs` after T031.
- [X] T037 [US2] Implement prose/structured mismatch and stale generated-view
  checks in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fs` after T032.
- [X] T038 [US2] Implement deterministic JSON byte output and diagnostic
  ordering in `src/FS.GG.SDD.Artifacts/Serialization.fs` after T033.
- [X] T039 [US2] Populate fixture sources and expected diagnostic manifests in
  `tests/fixtures/sdd-artifact-model/malformed-schema-version/`,
  `tests/fixtures/sdd-artifact-model/missing-artifact/`,
  `tests/fixtures/sdd-artifact-model/duplicate-identifiers/`,
  `tests/fixtures/sdd-artifact-model/unknown-reference/`,
  `tests/fixtures/sdd-artifact-model/prose-structured-mismatch/`,
  `tests/fixtures/sdd-artifact-model/stale-generated-view/`, and
  `tests/fixtures/sdd-artifact-model/deterministic-ordering/`.
- [X] T040 [US2] Update `scripts/prelude.fsx` with an invalid-fixture example
  that prints diagnostic ids and corrections without depending on Governance
  packages.

**Checkpoint**: User Story 2 is independently testable through invalid fixtures
and deterministic output checks.

---

## Phase 5: User Story 3 - Expose Optional Governance Boundaries (Priority: P3)

**Goal**: Describe SDD lifecycle rule contracts and optional Governance
compatibility boundaries without depending on Governance runtime behavior.

**Independent Test**: Review and run Governance boundary tests proving that SDD
can expose compatibility facts while excluding route selection, profile
adjustment, freshness evaluation, protected-boundary enforcement, and audit
verdicts.

### Tests First

- [X] T041 [P] [US3] Add failing lifecycle rule contract tests in
  `tests/FS.GG.SDD.Artifacts.Tests/GovernanceBoundaryTests.fs` for
  `requiredSpecSections`, `planObligations`, `taskGraphShape`,
  `evidenceDeclarations`, `loadedAgentSkills`, and `testObligations`.
- [X] T042 [P] [US3] Add failing optional boundary tests in
  `tests/FS.GG.SDD.Artifacts.Tests/GovernanceBoundaryTests.fs` for
  `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` entries
  in the work model.
- [X] T043 [P] [US3] Add failing dependency-guard test in
  `tests/FS.GG.SDD.Artifacts.Tests/GovernanceBoundaryTests.fs` proving
  `src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj` has no FS.GG.Governance
  package or project reference.

### Implementation

- [X] T044 [US3] Implement lifecycle rule contract records in
  `src/FS.GG.SDD.Artifacts/LifecycleRuleContracts.fs` from
  `specs/001-sdd-artifact-model/contracts/lifecycle-rule-contracts.md`.
- [X] T045 [US3] Implement optional Governance boundary entries in
  `src/FS.GG.SDD.Artifacts/WorkModel.fs` with `owner = governance`,
  `requiredBySdd = false`, and `relationship = optionalCompatibilityBoundary`.
- [X] T046 [US3] Populate Governance boundary examples in
  `tests/fixtures/sdd-artifact-model/valid-work-item/.fsgg/project.yml` and
  expected JSON in
  `tests/fixtures/sdd-artifact-model/valid-work-item/readiness/001-sdd-artifact-model/work-model.json`.
- [X] T047 [US3] Update `scripts/prelude.fsx` with a Governance boundary review
  example that lists optional boundary paths without evaluating route,
  freshness, profile, gate, or audit behavior.

**Checkpoint**: User Story 3 is independently testable and preserves the
FS.GG.SDD / FS.GG.Governance ownership boundary.

---

## Phase 6: Integration & Polish

**Purpose**: Verify the full Tier 1 contract, refresh baselines, and prove the
quickstart path.

- [X] T048 Refresh `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`
  after all `.fsi` and `.fs` files are complete.
- [X] T049 Run `dotnet restore FS.GG.SDD.sln` and record the result in
  `specs/001-sdd-artifact-model/readiness/restore.txt`.
- [X] T050 Run `dotnet build FS.GG.SDD.sln --configuration Release` and record
  the result in `specs/001-sdd-artifact-model/readiness/build.txt`.
- [X] T051 Run `dotnet test FS.GG.SDD.sln --configuration Release` and record
  the result in `specs/001-sdd-artifact-model/readiness/test.txt`.
- [X] T052 Run `dotnet fsi scripts/prelude.fsx` and record the result in
  `specs/001-sdd-artifact-model/readiness/fsi-session.txt`.
- [X] T053 Run
  `dotnet pack src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj --configuration Release`
  and record the result in `specs/001-sdd-artifact-model/readiness/pack.txt`.
- [X] T054 Confirm `specs/001-sdd-artifact-model/quickstart.md` matches the
  verified command sequence and update it only if the implementation changed the
  executable validation path.
- [X] T059 Record the artifact traceability walkthrough for SC-007 in
  `specs/001-sdd-artifact-model/readiness/artifact-traceability.txt`, proving a
  contributor can identify each modeled artifact's source of truth, generated
  view relationship, stale behavior, and diagnostic family from the contract and
  fixtures.
- [X] T055 Review `AGENTS.md` and `CLAUDE.md` for guidance drift only if the
  implemented workflow behavior changes; otherwise record "no guidance change"
  in `specs/001-sdd-artifact-model/readiness/guidance-review.txt`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundation (Phase 2)**: Depends on Phase 1 and blocks all user stories.
- **US1 (Phase 3)**: Depends on Phase 2; provides the MVP contract.
- **US2 (Phase 4)**: Depends on Phase 3 because diagnostics attach to the US1
  contract and work model.
- **US3 (Phase 5)**: Depends on Phase 3 and can proceed in parallel with US2
  after the work-model contract exists.
- **Integration & Polish (Phase 6)**: Depends on the selected user stories.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on other user stories after Foundation.
- **User Story 2 (P2)**: Depends on US1 contract types and work-model shape.
- **User Story 3 (P3)**: Depends on US1 work-model shape; independent from US2
  diagnostic implementation except for shared diagnostic types.

### Parallel Opportunities

- Phase 1: T006 and T007 can run in parallel after T001-T005 are assigned.
- Phase 2: T014, T015, and T056 can run in parallel with signature drafting.
- US1: T018-T020 and T057 can be written in parallel; T021-T024 can be
  implemented in parallel after their matching tests exist, with T025-T026
  integrating them.
- US2: T029-T033 and T058 can be written in parallel; T034-T038 can be
  implemented in parallel where file ownership does not overlap.
- US3: T041-T043 can be written in parallel; T044 and T045 can be implemented
  in parallel after those tests exist.
- US2 and US3 can proceed in parallel after US1 is complete.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation.
3. Complete Phase 3: User Story 1.
4. Stop and validate US1 independently with `IdentifierTests.fs`,
   `SchemaContractTests.fs`, `WorkModelTests.fs`, and `scripts/prelude.fsx`.

### Incremental Delivery

1. Deliver US1 to establish the artifact contract.
2. Add US2 to make invalid states actionable and deterministic.
3. Add US3 to expose optional Governance compatibility without runtime
   dependency.
4. Run Phase 6 validation across the selected scope.

### Evidence Notes

- Tests must be written first and fail before the related implementation task is
  marked `[X]`.
- Use real fixture directories under `tests/fixtures/sdd-artifact-model/`.
- Synthetic evidence must be disclosed in the test name or nearby comment and
  recorded in the relevant readiness file under
  `specs/001-sdd-artifact-model/readiness/`.
- Generated views are outputs; their presence is not evidence of currency
  without matching source digests, generator version, schema version, and output
  digest.
- Implementation evidence for the completed slice is recorded in
  `specs/001-sdd-artifact-model/readiness/restore.txt`,
  `build.txt`, `test.txt`, `fsi-session.txt`, `pack.txt`,
  `artifact-traceability.txt`, and `guidance-review.txt`. MVU is not applicable
  for this feature because it introduces a pure artifact-model library only, not
  a stateful command, generator, validator process, or I/O workflow.
