# Tasks: Normalized Work Model

**Feature branch**: `002-normalized-work-model`
**Spec**: `specs/002-normalized-work-model/spec.md`
**Plan**: `specs/002-normalized-work-model/plan.md`
**Change Tier**: Tier 1

**Input**: Design documents from `specs/002-normalized-work-model/`

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
`[P]`. This feature is a pure library feature over
`LifecycleArtifacts.FileSnapshot` inputs and returned output records. Elmish/MVU
tasks are not applicable because no stateful lifecycle command, filesystem
writer, validator process, or external I/O workflow is introduced.

Remediation tasks added after analysis keep appended task ids while remaining in
their execution phase, preserving existing task references.

---

## Phase 1: Setup

**Purpose**: Add the normalized-work-model fixture, test, and readiness roots
needed by every user story.

- [X] T001 Create normalized fixture roots with `manifest.yml` placeholders
  under `tests/fixtures/normalized-work-model/valid-work-item/`,
  `tests/fixtures/normalized-work-model/selected-work-item-mismatch/`,
  `tests/fixtures/normalized-work-model/requirement-not-typed/`,
  `tests/fixtures/normalized-work-model/work-model-inconsistent/`,
  `tests/fixtures/normalized-work-model/prose-structured-mismatch/`,
  `tests/fixtures/normalized-work-model/duplicate-logical-id/`,
  `tests/fixtures/normalized-work-model/missing-generated-model/`,
  `tests/fixtures/normalized-work-model/stale-source-digest/`,
  `tests/fixtures/normalized-work-model/stale-generator-version/`,
  `tests/fixtures/normalized-work-model/malformed-generated-json/`,
  `tests/fixtures/normalized-work-model/deprecated-schema-version/`,
  `tests/fixtures/normalized-work-model/unsupported-schema-version/`,
  `tests/fixtures/normalized-work-model/future-schema-version/`, and
  `tests/fixtures/normalized-work-model/deterministic-ordering/`.
- [X] T002 Create `tests/fixtures/normalized-work-model/malformed-schema-version/`
  and update `specs/002-normalized-work-model/contracts/fixture-catalog.md` to
  list it as the malformed schema fixture required by `quickstart.md` and
  `spec.md`.
- [X] T003 Create `specs/002-normalized-work-model/readiness/` for restore,
  build, test, FSI, pack, traceability, schema, Governance boundary, and
  guidance evidence outputs.
- [X] T004 Update
  `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` to compile
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`,
  `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`, and
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaMigrationTests.fs`.
- [X] T005 [P] Update `tests/FS.GG.SDD.Artifacts.Tests/TestSupport.fs` with
  normalized-work-model fixture helpers that load snapshots from
  `tests/fixtures/normalized-work-model/` while keeping the existing
  `tests/fixtures/sdd-artifact-model/` helpers intact.
- [X] T006 [P] Record feature evidence obligations and the MVU non-applicability
  statement in `specs/002-normalized-work-model/tasks.md`.

**Checkpoint**: Fixture and evidence roots exist; normalized-work-model tests
can be added without changing project structure.

---

## Phase 2: Foundation

**Purpose**: Declare public contracts, diagnostics, and shared test scaffolding
before story implementation.

- [X] T007 Draft `WorkModelGenerationRequest`,
  `WorkModelGenerationResult`, `SchemaCompatibility`, expanded source entries,
  generated-view entries, and linked requirement/evidence fields in
  `src/FS.GG.SDD.Artifacts/WorkModel.fsi`.
- [X] T008 Draft generated-view currency contract additions in
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`, including currency status,
  parsed work-model metadata, expected output path, source digest comparison,
  generator version comparison, and output digest comparison.
- [X] T009 Draft schema classification helpers in
  `src/FS.GG.SDD.Artifacts/SchemaVersion.fsi` for current, deprecated,
  unsupported, malformed, and future schema versions.
- [X] T010 Draft diagnostic factories in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` for `requirementNotTyped`,
  `missingGeneratedWorkModel`, `deprecatedSchemaVersion`, and
  `futureSchemaVersion`, and confirm existing factories cover
  `workModelInconsistent`, `proseStructuredMismatch`, `staleGeneratedView`,
  `malformedSchemaVersion`, `unsupportedSchemaVersion`,
  `duplicateIdentifier`, `unknownReference`, and `malformedDigest`.
- [X] T011 Draft `generateWorkModel` and `checkGeneratedWorkModelCurrency` in
  `src/FS.GG.SDD.Artifacts/Serialization.fsi`, preserving the existing
  `normalizeSnapshotsToWorkModel` and `serializeWorkModel` public functions.
- [X] T012 [P] Update `scripts/prelude.fsx` to sketch the new public generation
  API over `tests/fixtures/normalized-work-model/valid-work-item/` before
  implementation bodies harden.
- [X] T076 Run `dotnet fsi scripts/prelude.fsx` after T012 and record draft
  public-surface evidence in
  `specs/002-normalized-work-model/readiness/fsi-draft-public-surface.txt`
  before T013 or any story implementation task hardens `.fs` bodies.
- [X] T013 Add compiling placeholder implementations for the new signatures in
  `src/FS.GG.SDD.Artifacts/WorkModel.fs`,
  `src/FS.GG.SDD.Artifacts/GenerationManifest.fs`,
  `src/FS.GG.SDD.Artifacts/SchemaVersion.fs`,
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs`, and
  `src/FS.GG.SDD.Artifacts/Serialization.fs`.
- [X] T014 [P] Add failing public API baseline entries for the new `.fsi`
  members in `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline`.

**Checkpoint**: Public surface is declared, exercised from FSI, and executable before
behavioral implementation begins.

---

## Phase 3: User Story 1 - Normalize Authored Lifecycle Work (Priority: P1) - MVP

**Goal**: Normalize SDD project-level and selected work-item sources into one
deterministic work model with source traceability, generated-view metadata,
optional Governance boundary facts, and zero blocking diagnostics for valid
input.

**Independent Test**: Run the US1 tests over
`tests/fixtures/normalized-work-model/valid-work-item/` and confirm
`generateWorkModel` returns `readiness/002-normalized-work-model/work-model.json`,
complete model facts, deterministic JSON, a valid output digest, and no blocking
diagnostics.

### Tests First

- [X] T015 [P] [US1] Populate
  `tests/fixtures/normalized-work-model/valid-work-item/` with
  `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`,
  `work/002-normalized-work-model/spec.md`,
  `work/002-normalized-work-model/plan.md`,
  `work/002-normalized-work-model/tasks.yml`,
  `work/002-normalized-work-model/evidence.yml`, and
  `tests/fixtures/normalized-work-model/valid-work-item/manifest.yml`.
- [X] T016 [P] [US1] Add failing generation-result tests in
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs` for
  `Serialization.generateWorkModel` output path, model, JSON text,
  output digest, and sorted diagnostics.
- [X] T017 [P] [US1] Add failing valid-fixture normalization tests in
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs` that assert
  project summary, source entries, work item, requirements, decisions, tasks,
  evidence declarations, generated views, and Governance boundary entries.
- [X] T018 [P] [US1] Add failing JSON contract tests in
  `tests/FS.GG.SDD.Artifacts.Tests/DeterministicJsonTests.fs` for top-level
  property order, stable collection ordering, absence of implicit clock data,
  and byte-identical output across three consecutive generations.
- [X] T019 [P] [US1] Add the valid golden output at
  `tests/fixtures/normalized-work-model/valid-work-item/readiness/002-normalized-work-model/work-model.json`
  and compare it from
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`.
- [X] T077 [P] [US1] Populate
  `tests/fixtures/normalized-work-model/selected-work-item-mismatch/` with
  `manifest.yml`, `.fsgg/` sources, and work-item sources where the requested
  work id is missing or does not match
  `work/002-normalized-work-model/spec.md`.
- [X] T078 [P] [US1] Add failing selected-work-item tests in
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs` for missing
  selected work item and work id mismatch diagnostics.
- [X] T079 [P] [US1] Add a failing representative performance test in
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs` proving
  normalization and serialization of
  `tests/fixtures/normalized-work-model/valid-work-item/` complete in under 1
  second without adding wall-clock data to generated JSON.

### Implementation

- [X] T020 [US1] Implement normalized source classification and selected work
  item assembly in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` after T015
  through T019 and T077 through T079.
- [X] T021 [US1] Implement the expanded normalized work-model records, linked
  task/evidence ids, source locations, generated-view entries, and optional
  Governance boundary entries in `src/FS.GG.SDD.Artifacts/WorkModel.fs` after
  T017.
- [X] T022 [US1] Implement `Serialization.generateWorkModel` in
  `src/FS.GG.SDD.Artifacts/Serialization.fs` after T016, including default
  `readiness/<workId>/work-model.json` output path and SHA-256 output digest.
- [X] T023 [US1] Implement deterministic work-model JSON emission in
  `src/FS.GG.SDD.Artifacts/Serialization.fs` after T018 and T019, matching
  `specs/002-normalized-work-model/contracts/work-model-json.md`.
- [X] T083 [US1] Keep representative normalization and serialization under 1
  second by avoiding repeated full-source scans or nondeterministic timing data
  in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Artifacts/Serialization.fs` after T079.
- [X] T024 [US1] Implement optional Governance compatibility facts from
  `.fsgg/project.yml` in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Artifacts/WorkModel.fs`, without parsing Governance policy or
  computing route, profile, freshness, gate, or audit behavior.
- [X] T025 [US1] Update `scripts/prelude.fsx` to print work id, model version,
  blocking diagnostic count, requirement ids, task ids, Governance boundary
  paths, and JSON byte count from the valid normalized fixture.

**Checkpoint**: User Story 1 is independently testable and is the suggested MVP
scope for implementation.

---

## Phase 4: User Story 2 - Diagnose Conflicts And Incomplete Typing (Priority: P2)

**Goal**: Emit stable diagnostics when Markdown requirements are not typed,
structured references are invalid, or prose disagrees with structured lifecycle
data while preserving structured graph data as executable authority.

**Independent Test**: Run US2 tests over `requirement-not-typed`,
`work-model-inconsistent`, and `prose-structured-mismatch` fixtures and confirm
the expected diagnostic ids, affected artifacts, corrections, related ids, and
stable ordering.

### Tests First

- [X] T026 [P] [US2] Populate
  `tests/fixtures/normalized-work-model/requirement-not-typed/` with
  `manifest.yml`, `.fsgg/` sources, and `work/002-normalized-work-model/spec.md`
  content that includes a Markdown requirement or acceptance criterion id absent
  from the structured requirement set.
- [X] T027 [P] [US2] Populate
  `tests/fixtures/normalized-work-model/work-model-inconsistent/` with
  `manifest.yml`, `work/002-normalized-work-model/tasks.yml`, and
  `work/002-normalized-work-model/evidence.yml` entries that reference unknown
  requirements, decisions, tasks, evidence, artifacts, source digests, or
  generator versions.
- [X] T028 [P] [US2] Populate
  `tests/fixtures/normalized-work-model/prose-structured-mismatch/` with
  `manifest.yml`, `work/002-normalized-work-model/spec.md`,
  `work/002-normalized-work-model/tasks.yml`, and
  `work/002-normalized-work-model/evidence.yml` entries that disagree on
  status, dependencies, owner, or required evidence.
- [X] T029 [P] [US2] Add failing fixture tests in
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs` for
  `requirementNotTyped`, `workModelInconsistent`, and
  `proseStructuredMismatch`.
- [X] T030 [P] [US2] Add failing diagnostic contract tests in
  `tests/FS.GG.SDD.Artifacts.Tests/DiagnosticTests.fs` for stable diagnostic
  id, severity, affected artifact, source location, message, correction, and
  related ids for the US2 diagnostics.
- [X] T031 [P] [US2] Add failing diagnostic ordering tests in
  `tests/FS.GG.SDD.Artifacts.Tests/DeterministicJsonTests.fs` using
  `tests/fixtures/normalized-work-model/deterministic-ordering/`.
- [X] T080 [P] [US2] Populate
  `tests/fixtures/normalized-work-model/duplicate-logical-id/` with
  `manifest.yml`, `work/002-normalized-work-model/spec.md`,
  `work/002-normalized-work-model/tasks.yml`, and
  `work/002-normalized-work-model/evidence.yml` entries that duplicate
  requirement, task, evidence, or artifact ids.
- [X] T081 [P] [US2] Add failing duplicate logical-id fixture tests in
  `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs` for
  `duplicateIdentifier`, affected artifact, related ids, and expected
  correction.

### Implementation

- [X] T032 [US2] Implement Markdown requirement and acceptance-criterion id
  extraction in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and emit
  `requirementNotTyped` from `src/FS.GG.SDD.Artifacts/WorkModel.fs` after T026
  and T029.
- [X] T033 [US2] Implement structured reference validation for tasks, evidence,
  artifacts, source digests, and generator versions in
  `src/FS.GG.SDD.Artifacts/WorkModel.fs` after T027 and T029.
- [X] T034 [US2] Implement prose/structured mismatch detection for lifecycle
  status, task dependencies, owner, and required evidence in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Artifacts/WorkModel.fs` after T028 and T029.
- [X] T084 [US2] Implement duplicate logical-id detection for normalized
  requirements, decisions, tasks, evidence declarations, and artifact
  references in `src/FS.GG.SDD.Artifacts/WorkModel.fs` after T080 and T081.
- [X] T035 [US2] Implement `requirementNotTyped` and any missing US2 diagnostic
  factories in `src/FS.GG.SDD.Artifacts/Diagnostics.fs` after T030.
- [X] T036 [US2] Implement final diagnostic sorting and JSON diagnostic order in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs` and
  `src/FS.GG.SDD.Artifacts/Serialization.fs` after T031.
- [X] T037 [US2] Update expected fixture manifests in
  `tests/fixtures/normalized-work-model/requirement-not-typed/manifest.yml`,
  `tests/fixtures/normalized-work-model/work-model-inconsistent/manifest.yml`,
  `tests/fixtures/normalized-work-model/prose-structured-mismatch/manifest.yml`,
  `tests/fixtures/normalized-work-model/deterministic-ordering/manifest.yml`,
  and `tests/fixtures/normalized-work-model/duplicate-logical-id/manifest.yml`.

**Checkpoint**: User Story 2 is independently testable through invalid fixtures
and actionable diagnostics.

---

## Phase 5: User Story 3 - Detect Stale Or Missing Generated Models (Priority: P3)

**Goal**: Check existing generated work-model views for currency by source
digest, schema version, generator version, output digest, and JSON validity so
file presence is never treated as readiness.

**Independent Test**: Run US3 tests over missing, stale-source,
stale-generator, and malformed generated-model fixtures and confirm
`checkGeneratedWorkModelCurrency` returns the expected diagnostics without
writing to disk.

### Tests First

- [X] T038 [P] [US3] Populate
  `tests/fixtures/normalized-work-model/missing-generated-model/` with
  sufficient authored sources and no
  `readiness/002-normalized-work-model/work-model.json` output.
- [X] T039 [P] [US3] Populate
  `tests/fixtures/normalized-work-model/stale-source-digest/` with a generated
  `readiness/002-normalized-work-model/work-model.json` whose recorded source
  digest no longer matches an authored source.
- [X] T040 [P] [US3] Populate
  `tests/fixtures/normalized-work-model/stale-generator-version/` with a
  generated `readiness/002-normalized-work-model/work-model.json` whose
  generator version differs from the current
  `SchemaVersion.GeneratorVersion`.
- [X] T041 [P] [US3] Populate
  `tests/fixtures/normalized-work-model/malformed-generated-json/` with an
  invalid `readiness/002-normalized-work-model/work-model.json` payload.
- [X] T042 [P] [US3] Add failing currency tests in
  `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs` for
  `missingGeneratedWorkModel`, stale source digest, stale generator version,
  malformed generated JSON, and sorted diagnostics.
- [X] T043 [P] [US3] Add failing manifest comparison tests in
  `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs` for source
  digest, schema version, generator version, and output digest comparisons.

### Implementation

- [X] T044 [US3] Implement expected work-model output path defaulting and
  generated-view snapshot discovery in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Artifacts/Serialization.fs` after T038 and T042.
- [X] T045 [US3] Implement generated work-model metadata parsing and comparison
  helpers in `src/FS.GG.SDD.Artifacts/GenerationManifest.fs` after T039 through
  T043.
- [X] T046 [US3] Implement
  `Serialization.checkGeneratedWorkModelCurrency` in
  `src/FS.GG.SDD.Artifacts/Serialization.fs` after T042 and T043.
- [X] T047 [US3] Implement `missingGeneratedWorkModel` diagnostics in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs` and ensure malformed generated JSON
  maps to `staleGeneratedView` with a parse-focused correction.
- [X] T048 [US3] Include generated-view currency status and diagnostics in
  `src/FS.GG.SDD.Artifacts/WorkModel.fs` and
  `src/FS.GG.SDD.Artifacts/Serialization.fs`.
- [X] T049 [US3] Update expected fixture manifests in
  `tests/fixtures/normalized-work-model/missing-generated-model/manifest.yml`,
  `tests/fixtures/normalized-work-model/stale-source-digest/manifest.yml`,
  `tests/fixtures/normalized-work-model/stale-generator-version/manifest.yml`,
  and `tests/fixtures/normalized-work-model/malformed-generated-json/manifest.yml`.

**Checkpoint**: User Story 3 is independently testable and proves generated
views are outputs, not authority.

---

## Phase 6: User Story 4 - Explain Schema Migration Posture (Priority: P3)

**Goal**: Classify lifecycle artifact schema versions as current, deprecated,
unsupported, malformed, or future and emit documented compatibility diagnostics.

**Independent Test**: Run US4 tests over current, deprecated, unsupported,
malformed, and future schema fixtures and confirm the recorded compatibility
status, blocking behavior, and expected correction.

### Tests First

- [X] T050 [P] [US4] Populate
  `tests/fixtures/normalized-work-model/deprecated-schema-version/` with
  `manifest.yml` and lifecycle sources that use a readable but deprecated
  schema version.
- [X] T051 [P] [US4] Populate
  `tests/fixtures/normalized-work-model/unsupported-schema-version/` with
  `manifest.yml` and lifecycle sources that use a known unsupported schema
  version.
- [X] T052 [P] [US4] Populate
  `tests/fixtures/normalized-work-model/future-schema-version/` with
  `manifest.yml` and lifecycle sources that use a schema version newer than the
  current generator understands.
- [X] T053 [P] [US4] Populate
  `tests/fixtures/normalized-work-model/malformed-schema-version/` with
  `manifest.yml` and lifecycle sources that omit or corrupt schema version
  values.
- [X] T054 [P] [US4] Add failing schema migration tests in
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaMigrationTests.fs` for current,
  deprecated, unsupported, malformed, and future statuses.
- [X] T055 [P] [US4] Add failing source-entry JSON tests in
  `tests/FS.GG.SDD.Artifacts.Tests/SchemaMigrationTests.fs` for
  `schemaVersion`, `schemaStatus`, raw version value, supported range, and
  migration hint fields where applicable.

### Implementation

- [X] T056 [US4] Implement schema compatibility classification in
  `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` after T050 through T055.
- [X] T057 [US4] Implement `deprecatedSchemaVersion` and
  `futureSchemaVersion` diagnostic factories in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs` after T054.
- [X] T058 [US4] Thread schema compatibility status through
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and
  `src/FS.GG.SDD.Artifacts/WorkModel.fs` after T056 and T057.
- [X] T059 [US4] Emit schema compatibility fields in deterministic JSON from
  `src/FS.GG.SDD.Artifacts/Serialization.fs` after T055 and T058.
- [X] T060 [US4] Update expected fixture manifests in
  `tests/fixtures/normalized-work-model/deprecated-schema-version/manifest.yml`,
  `tests/fixtures/normalized-work-model/unsupported-schema-version/manifest.yml`,
  `tests/fixtures/normalized-work-model/future-schema-version/manifest.yml`,
  and `tests/fixtures/normalized-work-model/malformed-schema-version/manifest.yml`.
- [X] T061 [US4] Record schema migration review notes in
  `specs/002-normalized-work-model/readiness/schema-migration-review.txt`,
  covering current, deprecated, unsupported, malformed, and future schema
  behavior.

**Checkpoint**: User Story 4 is independently testable and migration behavior
is explicit.

---

## Phase 7: Integration & Polish

**Purpose**: Verify the complete Tier 1 contract, refresh baselines, and record
readiness evidence.

- [X] T062 Refresh
  `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` after all `.fsi` and
  `.fs` files are complete.
- [X] T063 Run `dotnet restore FS.GG.SDD.sln` and record the result in
  `specs/002-normalized-work-model/readiness/restore.txt`.
- [X] T064 Run `dotnet build FS.GG.SDD.sln --configuration Release` and record
  the result in `specs/002-normalized-work-model/readiness/build.txt`.
- [X] T065 Run `dotnet test FS.GG.SDD.sln --configuration Release` and record
  the result in `specs/002-normalized-work-model/readiness/test.txt`.
- [X] T066 Run
  `dotnet test FS.GG.SDD.sln --configuration Release --filter "FullyQualifiedName~NormalizedWorkModel"`
  and record the result in
  `specs/002-normalized-work-model/readiness/normalized-work-model-tests.txt`.
- [X] T067 Run
  `dotnet test FS.GG.SDD.sln --configuration Release --filter "FullyQualifiedName~GeneratedModelCurrency"`
  and record the result in
  `specs/002-normalized-work-model/readiness/generated-model-currency-tests.txt`.
- [X] T068 Run
  `dotnet test FS.GG.SDD.sln --configuration Release --filter "FullyQualifiedName~SchemaMigration"`
  and record the result in
  `specs/002-normalized-work-model/readiness/schema-migration-tests.txt`.
- [X] T069 Run
  `dotnet test FS.GG.SDD.sln --configuration Release --filter "FullyQualifiedName~DeterministicJson"`
  and record the result in
  `specs/002-normalized-work-model/readiness/deterministic-json-tests.txt`.
- [X] T082 Run
  `dotnet test FS.GG.SDD.sln --configuration Release --filter "FullyQualifiedName~Performance"`
  and record the result in
  `specs/002-normalized-work-model/readiness/performance-tests.txt`.
- [X] T070 Run `dotnet fsi scripts/prelude.fsx` and record the result in
  `specs/002-normalized-work-model/readiness/fsi-session.txt`.
- [X] T071 Run
  `dotnet pack src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj --configuration Release`
  and record the result in `specs/002-normalized-work-model/readiness/pack.txt`.
- [X] T072 Record the artifact traceability walkthrough for SC-005 in
  `specs/002-normalized-work-model/readiness/artifact-traceability.txt`, proving
  a contributor can trace any normalized requirement to its source, tasks,
  evidence declarations, and generated-view metadata.
- [X] T073 Record the Governance boundary review for SC-006 in
  `specs/002-normalized-work-model/readiness/governance-boundary-review.txt`,
  confirming SDD exposes optional compatibility facts but does not implement
  route selection, profile adjustment, evidence freshness, gate selection, or
  protected-boundary enforcement.
- [X] T074 Confirm `specs/002-normalized-work-model/quickstart.md` matches the
  verified command sequence and update it only if the implementation changed
  the executable validation path.
- [X] T075 Review `AGENTS.md` and `CLAUDE.md` for guidance drift only if the
  implemented workflow behavior changes; otherwise record "no guidance change"
  in `specs/002-normalized-work-model/readiness/guidance-review.txt`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundation (Phase 2)**: Depends on Phase 1 and blocks all user stories.
- **US1 (Phase 3)**: Depends on Phase 2 and provides the MVP normalized model.
- **US2 (Phase 4)**: Depends on US1 because diagnostics attach to normalized
  model entities and structured/prose authority rules.
- **US3 (Phase 5)**: Depends on US1 because currency checks compare generated
  work-model views to normalized source identities.
- **US4 (Phase 6)**: Depends on Phase 2 and can proceed after US1 source-entry
  shape is stable.
- **Integration & Polish (Phase 7)**: Depends on the selected user stories.

### User Story Dependencies

- **User Story 1 (P1)**: No dependency on other user stories after Foundation.
- **User Story 2 (P2)**: Depends on US1 normalized entities and source
  traceability.
- **User Story 3 (P3)**: Depends on US1 generated-view path, source digest, and
  generator version contracts.
- **User Story 4 (P3)**: Depends on Foundation schema signatures and US1 source
  entry shape; otherwise independent from US2 and US3.

### Within Each User Story

- Tests and fixtures are written before implementation.
- `.fsi` signatures are updated before `.fs` implementations.
- Public surface baseline changes follow intentional signature changes.
- Golden JSON updates follow deterministic serialization changes.
- A task is marked `[X]` only after real evidence exists or synthetic evidence
  is disclosed.

### Parallel Opportunities

- Phase 1: T005 and T006 can run in parallel with fixture root creation.
- Phase 2: T007 through T011 can be drafted in parallel by file owner; T012 and
  T014 can run in parallel after signatures are known; T076 must run after T012
  and before T013.
- US1: T015 through T019 and T077 through T079 can be written in parallel; T020,
  T021, T024, and T083 can proceed in parallel after their tests exist; T022 and
  T023 integrate the resulting model.
- US2: T026 through T031, T080, and T081 can be written in parallel; T032
  through T036 and T084 can be implemented in parallel where file ownership does
  not overlap.
- US3: T038 through T043 can be written in parallel; T044 through T047 can be
  implemented in parallel before T048 integrates currency status into the
  generated model.
- US4: T050 through T055 can be written in parallel; T056 and T057 can be
  implemented in parallel before T058 and T059 thread compatibility through the
  model and JSON.
- US2, US3, and US4 can proceed in parallel after US1 and Foundation contracts
  are stable, with coordination around shared `.fsi`, `Diagnostics.fs`,
  `WorkModel.fs`, and `Serialization.fs` edits.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundation.
3. Complete Phase 3: User Story 1.
4. Stop and validate US1 independently with
   `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`,
   `tests/FS.GG.SDD.Artifacts.Tests/DeterministicJsonTests.fs`, and
   `scripts/prelude.fsx`.

### Incremental Delivery

1. Deliver US1 to establish normalized work-model generation.
2. Add US2 to make conflicting and incomplete lifecycle data actionable.
3. Add US3 to prove generated-model currency without trusting file presence.
4. Add US4 to make schema migration posture explicit.
5. Run Phase 7 validation across the selected scope.

### Evidence Notes

- Tests must be written first and fail before the related implementation task is
  marked `[X]`.
- Use real fixture directories under `tests/fixtures/normalized-work-model/`.
- Synthetic evidence must be disclosed in the test name or nearby comment and
  recorded in the relevant readiness file under
  `specs/002-normalized-work-model/readiness/`.
- Generated views are outputs; their presence is not evidence of currency
  without matching source digests, generator version, schema version, and output
  digest.
- Implementation evidence for the completed slice is recorded in
  `specs/002-normalized-work-model/readiness/restore.txt`,
  `build.txt`, `test.txt`, `normalized-work-model-tests.txt`,
  `generated-model-currency-tests.txt`, `schema-migration-tests.txt`,
  `deterministic-json-tests.txt`, `performance-tests.txt`,
  `fsi-draft-public-surface.txt`, `fsi-session.txt`, `pack.txt`,
  `artifact-traceability.txt`, `schema-migration-review.txt`,
  `governance-boundary-review.txt`, and `guidance-review.txt`.
