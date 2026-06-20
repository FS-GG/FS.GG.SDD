# Tasks: Ship Command

**Input**: Design documents from `specs/013-ship-command/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`quickstart.md`, and `contracts/` (`ship-view.md`, `ship-command.md`,
`ship-report-json.md`, `ship-fixtures.md`)

**Change Tier**: Tier 1 (contracted native SDD command surface, generated
`readiness/<id>/ship.json` view, merge-boundary readiness facts, aggregated
lifecycle/verification readiness, command report JSON/text, generated-view
currency behavior, diagnostics, and optional Governance boundary facts)

**Tests**: Required by the specification and plan. Test tasks below are written
before implementation tasks and must fail before the implementation body is
completed.

**Status Legend**:

- `[X]` done with real evidence (build, tests, FSI, and/or CLI smoke), or with
  disclosed synthetic evidence per Principle V
- `[ ]` pending / not started
- `[-]` skipped with written rationale on the task line

**Implementation status (2026-06-20)**: ✅ Complete and verified. Full suite green
(258 passed / 0 failed: 70 artifact + 188 command, including 23 new ship tests and
3 real CLI smoke invocations). Build is clean (`readiness/build-release.txt`); the
FSI public-surface exercise passes (`readiness/fsi-public-surface.txt`).

Honest deviation note: the granular per-file test tasks below (e.g., separate ship
assertions in `CommandWorkflowTests.fs`, `GeneratedViewCommandTests.fs`,
`GovernanceBoundaryCommandTests.fs`, `CommandReportJsonTests.fs`,
`TextProjectionTests.fs`, `GeneratedModelCurrencyTests.fs`,
`NormalizedWorkModelTests.fs`) were **consolidated** into `ShipCommandTests.fs` and
`ShipViewTests.fs`, which exercise the same substance end-to-end through the public
`init`/`update`/interpreter boundary, deterministic JSON/text, dry-run, no-Governance,
preservation, and blocked paths with real filesystem + CLI evidence (satisfying the
Elmish/MVU and vertical-slice rules). Surface baselines for both projects were
regenerated and pass.

**Task Format**: `[ID] [P?] [Story?] Description with exact file path`

- `[P]` means the task has no dependency on another incomplete task in the same
  phase and touches different files from other parallel tasks.
- `[US1]`, `[US2]`, `[US3]`, and `[US4]` map to the user stories in
  `specs/013-ship-command/spec.md`.
- Phases run sequentially. Tasks within a phase marked `[P]` may run in
  parallel.

**Elmish/MVU applicability**: `ship` is an I/O-bearing workflow. Tasks emit the
`.fsi` contract additions (`SddCommand.Ship`, ship summary, ship-readiness
finding/disposition/diagnostic types, aggregated lifecycle readiness,
`CommandReport.Ship`, `CommandModel.Ship`) before `.fs` bodies, pure
`CommandWorkflow.init`/`update` transition tests, emitted-effect assertions, and
real interpreter evidence through CLI smoke and fixture runs. Like `verify`,
`ship` authors no source artifact; its only writes are the generated
`readiness/<id>/ship.json` view and a valid `readiness/<id>/work-model.json`
refresh. Unlike `verify`, `ship` does **not** re-derive task/evidence/test/skill
dispositions: it aggregates the verification view that owns those facts into one
merge-boundary disposition.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the test and fixture entry points needed by the ship slice.

**Fixture update rule**: Many lifecycle fixture directories already exist for
earlier command slices (for example `outside-project`, `missing-specification`,
`missing-clarification`, `missing-checklist`, `missing-plan`, `missing-tasks`,
`missing-analysis`, `missing-evidence`, `malformed-work-id`,
`duplicate-work-id`, `unknown-source-reference`, `stale-analysis`,
`stale-evidence`, `undisclosed-synthetic-evidence`, `invalid-deferral`,
`stale-generated-view`, `dry-run`, `deterministic-report`, `text-projection`,
`governance-boundary`). When a listed directory already exists, extend its
manifest with ship-specific expectations; do not replace coverage used by
earlier lifecycle command tests.

- [X] T001 Add `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs` and include it in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` after `VerifyCommandTests.fs`.
- [X] T002 Add `tests/FS.GG.SDD.Artifacts.Tests/ShipViewTests.fs` and include it in `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` after `VerificationViewTests.fs`.
- [X] T003 [P] Add valid ship fixture manifests under `tests/fixtures/lifecycle-commands/ship-create/manifest.yml`, `tests/fixtures/lifecycle-commands/ship-rerun-current/manifest.yml`, `tests/fixtures/lifecycle-commands/ship-preserves-authored/manifest.yml`, `tests/fixtures/lifecycle-commands/ship-refreshes-work-model/manifest.yml`, `tests/fixtures/lifecycle-commands/ship-refreshes-verification/manifest.yml`, and `tests/fixtures/lifecycle-commands/ship-accepted-deferral/manifest.yml`. (The `ship-refreshes-verification` fixture asserts that ship reports `readiness/<id>/verify.json` currency as the prerequisite gate and does **not** regenerate the verification view; only the work-model and ship views are refreshed.)
- [X] T004 [P] Add or extend blocked ship prerequisite fixture manifests under `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-specification/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-clarification/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-checklist/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-plan/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-tasks/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-analysis/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-verification/manifest.yml`, `tests/fixtures/lifecycle-commands/failed-verification/manifest.yml`, `tests/fixtures/lifecycle-commands/not-verification-ready/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-ship-view/manifest.yml`, and `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml`.
- [X] T005 [P] Add or extend blocked ship readiness-defect fixture manifests under `tests/fixtures/lifecycle-commands/unknown-source-reference/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-analysis/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-verification/manifest.yml`, `tests/fixtures/lifecycle-commands/undisclosed-synthetic-evidence/manifest.yml`, `tests/fixtures/lifecycle-commands/invalid-deferral/manifest.yml`, and `tests/fixtures/lifecycle-commands/stale-generated-view/manifest.yml`; and add or extend output/boundary manifests under `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`, `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`, and `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml`.

**Checkpoint**: Fixture and test file entry points exist; no ship behavior is
implemented yet.

## Phase 2: Foundational Contracts (Blocking Prerequisites)

**Purpose**: Define the public artifact, generated-view, and MVU/report
contracts before user-story implementation. `.fsi` signatures precede public
`.fs` implementation bodies.

### Failing contract tests

- [X] T006 Add failing ship view artifact tests for schema version 1, selected work identity, `stage = ship`, source artifact relationships, source digests, generator identity, aggregated lifecycle stage readiness, verification readiness summary, evidence disposition summary, generated-view currency, ship-readiness disposition, findings, diagnostics, optional boundary facts, and `shipReady`/`needsShipCorrection` readiness in `tests/FS.GG.SDD.Artifacts.Tests/ShipViewTests.fs`.
- [X] T007 Add failing disposition and finding tests for ship-readiness disposition states (`shipReady`, `blocked`, `stale`, `advisory`), lifecycle stage readiness states (`ready`, `advisory`, `stale`, `blocked`, `notApplicable`), and finding severities (`ready`, `advisory`, `warning`, `blocking`) in `tests/FS.GG.SDD.Artifacts.Tests/ShipViewTests.fs`.
- [X] T008 [P] Add failing generated-view currency assertions for the ship view (`current`, `missing`, `stale`, `malformed`, `blocked`) and the work-model/analysis/verification inputs aggregated by ship in `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`.
- [X] T009 [P] Add failing normalized work-model assertions for ship source relationships, aggregated lifecycle stage readiness, generated-view state, and deterministic source digests in `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`.

### Public `.fsi` contract additions

- [X] T010 Extend the public ship view contract in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` with `ShipView`, `ShipReadinessDisposition`, `ShipReadinessFinding`, `AggregatedLifecycleReadiness` (per-stage readiness), `VerificationReadinessSummary`, `EvidenceDispositionSummary`, `GeneratedViewCurrency` (reused), `OptionalBoundaryFact`, `ShipDiagnostic`, and parser/return types for `readiness/<id>/ship.json`. (depends on T006, T007)
- [X] T011 Extend ship source links, aggregated-readiness projection, and `ship.json` serialization signatures in `src/FS.GG.SDD.Artifacts/WorkModel.fsi` and `src/FS.GG.SDD.Artifacts/Serialization.fsi`, or record the deviation if the `ship.json` serializer lives in `CommandWorkflow.shipJson` per the `verify`/`analyze` precedent. (after T010)
- [X] T012 Extend the public MVU command contract in `src/FS.GG.SDD.Commands/CommandTypes.fsi` with `SddCommand.Ship`, `ShipSummary` (with the finding/disposition/evidence/lifecycle-stage counts from `contracts/ship-report-json.md`), `CommandReport.Ship`, and `CommandModel.Ship`, while keeping `CommandMsg`, `CommandEffect`, `CommandWorkflow.init`, `CommandWorkflow.update`, and the effect interpreter boundary explicit through `src/FS.GG.SDD.Commands/CommandWorkflow.fsi` and `src/FS.GG.SDD.Commands/CommandEffects.fsi`. (after T010, T011)
- [X] T013 Add ship diagnostic constructor signatures for the required diagnostic families (outside project, missing/malformed project config, missing/malformed work id, missing specification/clarification/checklist/plan/tasks/analysis/evidence/verification, analysis not ready, verification not ready, failed verification, ship identity mismatch, duplicate work id, malformed prerequisite artifact, unknown source reference, stale analysis, stale verification, stale evidence, undisclosed synthetic evidence, invalid deferral, malformed ship view, stale/missing/malformed/blocked generated view, tool defect) and the `ship.next.protectedBoundary` next-action signature in `src/FS.GG.SDD.Commands/CommandReports.fsi`. (after T012)
- [X] T014 [P] Add failing command public-surface tests for `SddCommand.Ship`, `ShipSummary`, ship finding/disposition counts, ship diagnostics, `CommandReport.Ship`, `CommandModel.Ship`, and the existing `CommandModel`/`CommandMsg`/`CommandEffect` MVU boundary in `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`, and the artifact ship view surface in `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs`.
- [X] T015 Add prelude references for `SddCommand.Ship`, `parseCommand "ship"`, `commandStage Ship`, `nextLifecycleCommand Verify` (returns `Ship`), `nextLifecycleCommand Ship` (returns `None` because the protected-boundary handoff is Governance-owned), ship summary visibility, ship view visibility, disposition visibility, and ship diagnostic visibility in `scripts/prelude.fsx`; run `dotnet fsi scripts/prelude.fsx` against the draft public surface before implementation-body tasks T017 through T020, and save the early transcript to `specs/013-ship-command/readiness/fsi-public-surface-draft.txt`. (after T010 through T014)
- [X] T016 Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` and `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the deliberate ship public-surface additions after T010 through T015.

### Implementation bodies

- [X] T017 Implement ship view parsing, schema-version validation, deterministic ordering, source snapshot validation, ship-readiness disposition/finding construction, aggregated lifecycle stage readiness, generated-view currency, and diagnostics in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` after T015.
- [X] T018 Implement ship source links, aggregated-readiness projection, and deterministic `ship.json` serialization in `src/FS.GG.SDD.Artifacts/WorkModel.fs` and `src/FS.GG.SDD.Artifacts/Serialization.fs` (or `CommandWorkflow.shipJson` per the recorded T011 deviation) after T015.
- [X] T019 Implement ship command contract types, command naming, command stage, parse support, lifecycle ordering (`Verify` -> `Ship`, `Ship` -> `None`), `shipRequest`, `runShip`, `initializeVerifiedProject` test helper, `validShipView`, `assertShipSummary`, and source-byte snapshot helpers in `src/FS.GG.SDD.Commands/CommandTypes.fs` and `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` after T015.
- [X] T020 Implement ship diagnostic constructors, blocked-report correction routing, and ship next-action selection (`ship.next.protectedBoundary` with a null command pointer) in `src/FS.GG.SDD.Commands/CommandReports.fs` after T015.

**Checkpoint**: Public `.fsi` contracts, parser contracts, command reports,
diagnostics, MVU boundaries, and surface baselines are ready for story
implementation.

## Phase 3: User Story 1 - Ship-Ready a Verification-Ready Work Item (Priority: P1, MVP)

**Goal**: `fsgg-sdd ship` loads one verification-ready work item, validates
prerequisite lifecycle and verification state, aggregates SDD-owned
merge-boundary readiness over lifecycle stage readiness, verification readiness,
evidence dispositions, and generated-view currency, generates
`readiness/<id>/ship.json`, refreshes or diagnoses generated work-model state,
reports the ship-readiness disposition, and points ship-ready work to the
protected-boundary handoff without requiring Governance.

**Independent Test**: Run `ship --work 013-ship-command` in an initialized
project with valid specification, clarification, checklist, plan, tasks,
analysis, evidence, and a verification-ready `verify.json`; confirm the ship
view, summary, disposition, lifecycle stage readiness, evidence disposition
counts, generated-view state, diagnostics, and `ship.next.protectedBoundary`
next action are produced without Governance.

### Tests for User Story 1

- [X] T021 [P] [US1] Add failing create-flow command tests for `readiness/013-ship-command/ship.json`, source relationships, source digests, aggregated lifecycle stage readiness, verification readiness summary, evidence disposition counts, ship-readiness disposition, findings, changed generated artifacts, generated-view state, and `ship.next.protectedBoundary` next action (with a null command pointer) in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs`.
- [X] T022 [P] [US1] Add failing pure `CommandWorkflow.init`/`CommandWorkflow.update` tests for `Ship` read effects, generated `ship.json` write effect, generated `work-model.json` write effect, emitted stdout/stderr effects, and dry-run effect suppression in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [X] T023 [P] [US1] Add a failing generated work-model refresh and verification-view currency test for valid ship facts (work-model and ship views refresh; verification view is treated as the current prerequisite gate and is not regenerated) in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T024 [P] [US1] Add a failing no-Governance ship success test that asserts no freshness/route/profile/gate/audit/protected-boundary/release verdict appears in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [X] T025 [P] [US1] Add a failing ship report shape assertion for `ship`, `changedArtifacts`, `generatedViews`, `diagnostics`, `governanceCompatibility`, and `nextAction` in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 1

- [X] T026 [US1] Wire `Ship` into lifecycle read planning in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` by adding read effects for `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/<id>/spec.md`, `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`, `work/<id>/tasks.yml`, `work/<id>/evidence.yml`, `readiness/<id>/analysis.json`, `readiness/<id>/verify.json`, `readiness/<id>/work-model.json`, existing `readiness/<id>/ship.json`, and `work/`.
- [X] T027 [US1] Implement selected work-id, initialized-project, and verification-ready prerequisite loading for `Ship`, including the analysis implementation-ready gate and the `verify.json` schema-version/work-id/verification-ready gate, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T028 [US1] Build the current lifecycle source set and source snapshot records for specification, clarification, checklist, plan, tasks, analysis, evidence, verification view, and existing work-model/ship view inputs in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T029 [US1] Evaluate the verification prerequisite (schema version 1, work-id match, verification-ready status, no unresolved blocking findings, and currency against current sources) into a `VerificationPrerequisiteState` without regenerating the verification view in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T030 [US1] Aggregate per-stage lifecycle readiness (`charter`, `specify`, `clarify`, `checklist`, `plan`, `tasks`, `analyze`, `evidence`, `verify`) from the normalized work model, analysis view, and verification view, projecting evidence dispositions from the verification view **without re-deriving** verify-owned task/evidence/test/skill dispositions, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T031 [US1] Build the single `ShipReadinessDisposition` and `ShipReadinessFinding` set with stable ids and structured links to lifecycle stages, verification findings, evidence dispositions, generated views, accepted deferrals, and source artifacts in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T032 [US1] Build deterministic `readiness/<id>/ship.json` content with schema version 1, generator identity, source relationships, source digests, aggregated lifecycle stage readiness, verification readiness summary, evidence disposition summary, generated-view currency, disposition, findings, optional boundary facts, diagnostics, and readiness state in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T033 [US1] Plan generated write effects for `readiness/<id>/ship.json` and `readiness/<id>/work-model.json` only when source facts are valid and `CommandRequest.DryRun = false`, planning zero authored-source writes and no verification-view write, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T034 [US1] Add `ShipSummary` construction, finding/disposition counts, evidence disposition counts, lifecycle stage readiness states, source snapshot count, generated artifact change records, and generated-view report entries in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T035 [US1] Remove the unsupported-command path for `Ship` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, make `src/FS.GG.SDD.Cli/Program.fs` able to run `ship --work <id>`, and return `NextAction.ActionId = "ship.next.protectedBoundary"` with `NextAction.Command = None` and required artifacts `readiness/<id>/ship.json` plus refreshed `readiness/<id>/work-model.json` from `src/FS.GG.SDD.Commands/CommandReports.fs`.

**Checkpoint**: User Story 1 is independently testable as the MVP.

## Phase 4: User Story 2 - Block Merge-Boundary Readiness on Lifecycle Gaps (Priority: P1)

**Goal**: Ship identifies the exact lifecycle stage, verification finding,
evidence disposition, or generated view that is not ready, blocks merge-boundary
readiness, never re-derives Governance-owned enforcement, and never treats an
existing generated view as current when its sources are stale or malformed.

**Independent Test**: Run `ship` against fixtures with known readiness defects
(outside project, missing/malformed prerequisites, missing or not-ready
verification, failed verification, duplicate work id, unknown reference,
stale/missing evidence, stale analysis, stale verification, undisclosed
synthetic evidence, invalid deferral, malformed ship view, stale generated
view); confirm no ship view is treated as ready until the report names the
affected artifact, identifier, severity, and correction.

### Tests for User Story 2

- [X] T036 [US2] Add failing blocked-prerequisite and precondition tests for outside project, missing or malformed project config, project settings that point to missing lifecycle roots, missing work id, malformed work id, duplicate logical work id, ship identity mismatch, missing specification/clarification/checklist/plan/tasks/analysis/evidence, malformed prerequisite artifact, and analysis not implementation-ready in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs`.
- [X] T037 [US2] Add failing verification-gate tests for missing `verify.json`, malformed verification view, work-id mismatch, stale verification view, failed verification (unresolved blocking findings), and `not-verification-ready` status blocking ship readiness in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs`.
- [X] T038 [US2] Add failing aggregated-readiness blocking tests for unknown source reference, stale analysis, stale evidence, undisclosed synthetic evidence (surfaced by the verification view), invalid deferral no longer visible or accepted, and accepted-deferral-still-visible passing in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs`.
- [X] T039 [P] [US2] Add failing MVU assertions that blocked ship never emits generated `WriteFile` effects for `ship.json` or `work-model.json`, and never emits any write for the verification view, in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [X] T040 [P] [US2] Add failing generated-view diagnostic tests for missing, stale, malformed, and blocked work-model/analysis/verification views, including malformed existing `ship.json` refusing safe refresh, in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [X] T041 [P] [US2] Add failing ship diagnostic serialization assertions for all required diagnostic families in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 2

- [X] T042 [US2] Implement verification-gate blocking diagnostics (missing/malformed/stale/mismatched verification view, failed verification, `not-verification-ready`) that surface the underlying verification blocking findings by structured link rather than re-deriving them, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T043 [US2] Implement aggregated evidence-disposition blocking for missing, stale, undisclosed synthetic, invalid-deferral, and unknown-reference states sourced from the verification view in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T044 [US2] Implement accepted-deferral visibility checks that block when a deferral visible at verification is no longer visible or no longer accepted at ship time in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T045 [US2] Implement lifecycle stage readiness blocking that marks any stage derived from stale or mismatched sources as `stale` or `blocked` rather than `ready`, and map blocked stages into the ship-readiness disposition in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T046 [US2] Implement generated-view currency diagnostics (missing, stale, malformed, blocked) and malformed-`ship.json` safe-refresh refusal in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T047 [US2] Implement source-digest comparison and stale-analysis, stale-verification, and stale-evidence diagnostics that mark ship readiness `stale` or `blocked` when source facts changed after the prerequisite generated views or the verification view captured a snapshot, in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T048 [US2] Build blocking ship-readiness findings with stable ids and structured links to the affected lifecycle stage, verification finding, evidence disposition, generated view, accepted deferral, or source artifact in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T049 [US2] Route blocked ship next actions to verification rerun, evidence correction, prerequisite lifecycle correction, generated-view refresh, or stale-source correction with blocking diagnostic ids in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T050 [US2] Add blocked-scenario and finding/disposition assertion helpers for ship tests in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.

**Checkpoint**: Merge-boundary readiness gaps are identified precisely and block
ship readiness with stable diagnostics and no generated write.

## Phase 5: User Story 3 - Preserve Authored Lifecycle Sources (Priority: P2)

**Goal**: Ship is a non-destructive merge-boundary check; authored
specifications, plans, tasks, and evidence and the verification view are never
created, updated, reordered, normalized, or removed, and dry-run mutates zero
files.

**Independent Test**: Run `ship` in valid, blocked, and dry-run scenarios and
confirm authored lifecycle artifacts and the verification view remain
byte-identical while only generated ship and work-model output and reports
reflect the current source state.

### Tests for User Story 3

- [X] T051 [US3] Add failing authored-source preservation tests asserting byte-identical `work/<id>/spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, `tasks.yml`, `evidence.yml`, and `readiness/<id>/verify.json` after valid and blocked runs in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs`.
- [X] T052 [US3] Add failing dry-run tests asserting zero authored and generated file changes (including no `ship.json` or `work-model.json` mutation) while still reporting proposed generated artifacts, diagnostics, readiness state, and next action in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs`.
- [X] T053 [P] [US3] Add failing MVU assertions that `Ship` never emits an authored `WriteFile` effect for any `work/<id>/` source or for the verification view in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [X] T054 [P] [US3] Add failing generated-only refresh and rerun-current `noChange` tests for unchanged `ship.json` and current `work-model.json` in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.

### Implementation for User Story 3

- [X] T055 [US3] Ensure the ship workflow plans no authored-source write effects and no verification-view write in any path, and restricts generated writes to `readiness/<id>/ship.json` and `readiness/<id>/work-model.json` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T056 [US3] Implement the dry-run path that reports proposed generated artifact changes without emitting any write effect in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T057 [US3] Implement rerun-current `noChange` behavior for unchanged `readiness/<id>/ship.json` and current `readiness/<id>/work-model.json` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [X] T058 [US3] Serialize generated artifact operations and safe-write decisions for `create`, `update`, `preserve`, `refuse`, and `noChange` for ship in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.

**Checkpoint**: Authored lifecycle sources and the verification view are
preserved across valid, blocked, and dry-run paths; only generated readiness
views change.

## Phase 6: User Story 4 - Keep Ship Output Traceable (Priority: P3)

**Goal**: Ship views, JSON command reports, text summaries, CLI smoke paths, and
optional Governance compatibility facts are deterministic projections of one
authoritative report contract.

**Independent Test**: Run identical ship requests repeatedly, compare JSON and
proposed `ship.json` bytes, render text, run CLI smoke paths, and confirm every
text fact exists in the JSON report while optional Governance references remain
advisory.

### Tests for User Story 4

- [X] T059 [P] [US4] Add a failing deterministic JSON test for three identical ship runs and proposed/generated `ship.json` payload comparison (byte-identical, no absolute host paths) in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [X] T060 [P] [US4] Add a failing ship text projection test for selected work id, outcome, ship path, ready/advisory/warning/blocking counts, ship-readiness disposition, lifecycle stage readiness states, verification readiness state, evidence disposition counts, generated-view state, diagnostics, and next action in `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.
- [X] T061 [P] [US4] Add a failing ship boundary test that excludes effective-evidence freshness, route, profile, gate, audit, protected-boundary enforcement, and release verdicts, and asserts optional Governance pointers stay advisory and not evaluated, in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [X] T062 [US4] Add disposable-project CLI JSON, dry-run, and text smoke helpers for `ship --work <id> --root <path> [--dry-run] [--text]` in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs` through the existing `src/FS.GG.SDD.Cli/Program.fs` entry point.
- [X] T063 [US4] Add local performance assertions under the two-second harness budget for `ship-create`, `ship-rerun-current`, and `ship-refreshes-work-model` in `tests/FS.GG.SDD.Commands.Tests/ShipCommandTests.fs`.

### Implementation for User Story 4

- [X] T064 [US4] Serialize the `ship` command summary, aggregated lifecycle stage readiness, verification readiness, evidence disposition counts, ship-readiness disposition, findings, generated-view state, diagnostics, Governance compatibility facts, and next action with deterministic sorting and no host-specific fields in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [X] T065 [US4] Render ship outcome, ship artifact path, ready/advisory/warning/blocking counts, ship-readiness disposition, lifecycle stage readiness, verification readiness, evidence disposition counts, generated-view state, diagnostics, and next action from the command report in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [X] T066 [US4] Keep ship Governance compatibility facts advisory and not evaluated, and keep effective-evidence freshness, route, profile, gate, audit, protected-boundary enforcement, and release verdict fields absent from ship reports in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [X] T067 [US4] Parse `ship --work <id> --dry-run --text --root <path>` and map arguments to `CommandRequest` fields in `src/FS.GG.SDD.Cli/Program.fs`.
- [X] T068 [US4] Exclude timestamps, durations, terminal details, process ids, random values, directory enumeration order, absolute host paths, and host-specific separators from ship reports and the ship view in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.

**Checkpoint**: Machine-readable and human-readable ship outputs are
deterministic projections of one report contract.

## Phase 7: Polish, Evidence, And Documentation

**Purpose**: Record mandatory verification evidence and update human-facing
state after implementation is complete.

- [X] T069 [P] Run `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~Ship"` and save output to `specs/013-ship-command/readiness/artifact-ship-tests.txt`.
- [X] T070 [P] Run `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Ship"` and save output to `specs/013-ship-command/readiness/command-ship-tests.txt`.
- [X] T071 [P] Run focused deterministic JSON, text projection, generated-view, and Governance boundary evidence tests and save output to `specs/013-ship-command/readiness/output-boundary-tests.txt`.
- [X] T072 Run `dotnet build FS.GG.SDD.sln -c Release` and save output to `specs/013-ship-command/readiness/build-release.txt`.
- [X] T073 Run `dotnet test FS.GG.SDD.sln -c Release` and save output to `specs/013-ship-command/readiness/full-suite.txt`.
- [X] T074 Run `dotnet fsi scripts/prelude.fsx` after a Release build and save the transcript to `specs/013-ship-command/readiness/fsi-public-surface.txt`.
- [X] T075 Run a disposable-project CLI JSON smoke scenario for `fsgg-sdd ship` and save output to `specs/013-ship-command/readiness/cli-json-smoke.txt`.
- [X] T076 Run a disposable-project CLI dry-run smoke scenario for `fsgg-sdd ship --dry-run` and save output to `specs/013-ship-command/readiness/cli-dry-run-smoke.txt`.
- [X] T077 Run a disposable-project CLI text smoke scenario for `fsgg-sdd ship --text`, save output to `specs/013-ship-command/readiness/cli-text-smoke.txt`, and record human-summary review notes in `specs/013-ship-command/readiness/human-summary-review.md`.
- [X] T078 Record create, rerun, and work-model-refresh performance evidence for `ship-create`, `ship-rerun-current`, and `ship-refreshes-work-model` in `specs/013-ship-command/readiness/performance.md`.
- [X] T079 Record SDD/Governance boundary review findings (protected-boundary handoff stays advisory; no freshness/route/profile/gate/audit/release behavior) in `specs/013-ship-command/readiness/sdd-governance-boundary.md`.
- [X] T080 Record artifact traceability from `specs/013-ship-command/spec.md` requirements to plan decisions, tasks, tests, and readiness evidence in `specs/013-ship-command/readiness/artifact-traceability.md`.
- [X] T081 Update `docs/initial-implementation-plan.md` to mark `fsgg-sdd ship` complete and reference `specs/013-ship-command/readiness/`.
- [X] T082 Update `README.md`, `AGENTS.md`, and `CLAUDE.md` only where needed to keep the current command state and Claude/Codex guidance synchronized after the ship workflow behavior changes.

**Checkpoint**: The implementation has build, test, FSI, CLI, performance,
boundary, and traceability evidence.

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 depends on Phase 2 and is the MVP scope.
- Phase 4 depends on Phase 3 because blocking detection reuses the loaded source
  set, aggregated readiness, and base report shape.
- Phase 5 depends on Phases 3 and 4 because preservation and dry-run guarantees
  must hold across success and blocked paths.
- Phase 6 depends on Phases 3 through 5 because output contracts must include
  success, blocked, dry-run, no-Governance, and preservation states.
- Phase 7 depends on the implemented story scope being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2; no dependency on other stories.
- **US2 (P1)**: Depends on US1 source loading, prerequisite gating, verification
  evaluation, and aggregated-readiness construction.
- **US3 (P2)**: Depends on US1 generated-write planning and US2 blocked-path
  behavior.
- **US4 (P3)**: Depends on ship summaries, diagnostics, generated-view
  reporting, and preservation behavior from US1 through US3.

### Cross-Task Dependencies

- T011 depends on T010.
- T012 and T013 depend on the public-surface decisions from T010 and T011.
- T015 depends on T010 through T014 and must run before implementation-body
  tasks T017 through T020.
- T016 depends on T010 through T015.
- T017 through T020 depend on the FSI/prelude exercise in T015.
- T026 through T035 depend on T017 through T020.
- T029 (verification gate) precedes T030 (aggregation), which precedes T031
  (disposition).
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
- T039, T040, and T041 can run in parallel with the `ShipCommandTests.fs` tasks
  in T036, T037, and T038 (the three `ShipCommandTests.fs` tasks share a file
  and run sequentially among themselves).
- T053 and T054 can run in parallel with the `ShipCommandTests.fs` tasks in T051
  and T052.
- T059, T060, and T061 can run in parallel because they touch different output
  and boundary test files.
- T069, T070, and T071 can run in parallel after implementation is complete.

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3.
3. Run the focused artifact and command evidence tests for US1.
4. Validate that `fsgg-sdd ship` generates `readiness/<id>/ship.json`,
   aggregates lifecycle/verification/evidence/generated-view readiness into one
   merge-boundary disposition, refreshes or diagnoses
   `readiness/<id>/work-model.json`, preserves authored sources and the
   verification view, works without Governance, and points ship-ready work to
   the protected-boundary handoff.

### Incremental Delivery

1. US1 creates the native ship view and success report.
2. US2 identifies and blocks merge-boundary readiness gaps with precise
   diagnostics.
3. US3 guarantees non-destructive preservation and dry-run behavior.
4. US4 locks down deterministic JSON/text, CLI smoke paths, and optional
   Governance boundaries.
5. Phase 7 records readiness evidence and updates project guidance.

## Task Counts

- Shared setup, foundation, and polish tasks: 34
  - Setup (Phase 1): 5
  - Foundational contracts (Phase 2): 15
  - Polish/evidence/docs (Phase 7): 14
- US1 tasks: 15
- US2 tasks: 15
- US3 tasks: 8
- US4 tasks: 10
- Total tasks: 82

## Suggested MVP Scope

Complete Phases 1, 2, and 3 first. That delivers User Story 1: a usable
`fsgg-sdd ship` command that generates the `readiness/<id>/ship.json` view,
aggregates lifecycle/verification/evidence/generated-view readiness into one
merge-boundary disposition, reports generated-view state, preserves authored
sources and the verification view, works without Governance, and points
ship-ready work to the protected-boundary handoff.
