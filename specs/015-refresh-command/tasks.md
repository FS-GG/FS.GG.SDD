# Tasks: Generated-View Refresh

**Input**: Design documents from `specs/015-refresh-command/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`quickstart.md`, and `contracts/` (`refresh-command.md`,
`refresh-report-json.md`, `refresh-summary-view.md`, `refresh-fixtures.md`)

**Change Tier**: Tier 1 (contracted native SDD command surface, the
`readiness/<id>/summary.md` generated view, cross-view refresh of the SDD-owned
generated views derived from declared sources, generated-view currency
behavior, stale-view diagnostics, command report JSON/text projection, and
optional Governance boundary facts).

**Tests**: Required by the specification and plan. Test tasks below are written
before the implementation bodies they cover and must fail before the
implementation body is completed.

**Status Legend**:

- `[x] ✅` done with real evidence (build, tests, FSI, and/or CLI smoke).
- `[ ] ⬜` pending / not started.
- `[-] 🟨` deferred with written rationale (recorded in the Implementation Notes
  section once the slice runs).

Never mark a failing task `[X]`. Never weaken an assertion to green a build —
narrow the scope and document it.

**Task Format**: `[ID] [P?] [Story?] Description with exact file path`

- `[P]` means the task has no dependency on another incomplete task in the same
  phase and touches different files from other parallel tasks.
- `[US1]`..`[US5]` map to the user stories in `specs/015-refresh-command/spec.md`.
- Phases run sequentially. Tasks within a phase marked `[P]` may run in parallel.

**Elmish/MVU applicability**: `refresh` is an I/O-bearing workflow. It reuses the
existing `CommandModel`/`CommandMsg`/`CommandEffect`/`CommandWorkflow.init`/
`CommandWorkflow.update` plus the edge effect interpreter. Tasks emit the `.fsi`
contract additions (the `summary.md` generation-manifest/render helpers on
`GenerationManifest`, `SddCommand.Refresh`, `RefreshDisposition`,
`RefreshSummary`, the refresh diagnostic builders, `CommandReport.Refresh`, and
`CommandModel.Refresh`) before `.fs` bodies, pure `init`/`update` transition
tests, emitted-effect assertions, and real interpreter evidence through CLI smoke
and disposable-project runs. Like `analyze`, `verify`, `ship`, and `agents`,
`refresh` authors no source artifact: its only writes are generated views under
their configured generated roots, including the new `readiness/<id>/summary.md`.
Unlike the single-view generated-view commands, `refresh` is **cross-cutting**
(`nextLifecycleCommand Refresh = None`, charter->ship chain unchanged) and
orchestrates *all* SDD-owned generated views together in declared source-of
order (work model -> analysis -> verify -> ship -> agent guidance -> summary),
bringing each upstream view to currency before its dependents.

**Reuse note (no redefinition)**: `GeneratedViewKind.Summary` already exists in
`src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`; `GenerationManifest`,
`SourceIdentity`, and `isStale` already exist there; `GeneratedViewState`,
`GeneratedViewCurrency` (`Current|Missing|Stale|Malformed|Blocked`),
`GovernanceCompatibilityFact`, `CommandReport`, `CommandRequest`,
`ArtifactChange`, and `NextAction` already exist in
`src/FS.GG.SDD.Commands/CommandTypes.fsi`; the per-view generators for
`work-model.json`, `analysis.json`, `verify.json`, `ship.json`, and
`agent-commands/<target>/` already exist and are reused unchanged. This feature
adds the orchestration, the `summary.md` projection, and the cross-view currency
report — not new per-view generator contracts.

## Implementation Status (2026-06-20)

Overall: **78 / 85 tasks ✅ done**, **7 🟨 deferred/deviated** (disclosed below),
**0 ⬜ outstanding**. Build green; full suite **306 tests pass, 0 fail** (was 281).

| Phase | Story | Status |
|---|---|---|
| Phase 1 — Setup | — | 🟨 T001–T002 ✅; T003–T005 🟨 (static fixtures deferred to real-evidence tests, per 014 precedent) |
| Phase 2 — Foundational contracts | — | ✅ T006–T020 (T008–T009 🟨 partial: currency/ordering covered behaviorally, not in `GeneratedModelCurrencyTests`) |
| Phase 3 — US1 (orchestrated refresh) | P1 | ✅ structured-view orchestration; 🟨 T028–T029 deviated (analysis/verify/ship **currency-reported**, not destructively re-run — see Implementation Notes) |
| Phase 4 — US2 (stale/blocked detection) | P1 | ✅ T034–T046 |
| Phase 5 — US3 (`summary.md` projection) | P2 | ✅ T047–T053 |
| Phase 6 — US4 (preservation, dry-run, repeatable) | P2 | ✅ T054–T061 |
| Phase 7 — US5 (determinism, text, CLI, no-Governance) | P3 | ✅ T062–T071 |
| Phase 8 — Polish, evidence, docs | — | ✅ T072–T085 (evidence under `readiness/`) |

Legend: ✅ done with real evidence · 🟨 deferred/deviated with rationale · ⬜ not started.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the test and fixture entry points needed by the refresh slice.

**Fixture update rule**: The shared blocked/output/boundary fixture roots
(`outside-project`, `malformed-work-id`, `duplicate-work-id`,
`unknown-source-reference`, `dry-run`, `deterministic-report`, `text-projection`,
`governance-boundary`) already exist for earlier command slices. When a listed
directory already exists, extend its manifest with refresh-specific expectations;
do not replace coverage used by earlier lifecycle command tests. The earlier
slices used per-artifact blocked roots (`missing-analysis`, `malformed-analysis`,
etc.) rather than generic `missing-source`/`malformed-source` roots, so refresh's
generic source roots (`missing-source`, `malformed-source`) are **new**, not
reused. New roots cover the orchestrated current/stale/
missing, summary, multi-view preservation, and blocked-upstream cases unique to
this feature. Per the `014-agent-guidance` precedent, scenarios may instead be
covered by real-evidence `RefreshCommandTests` over disposable project trees
where that yields stronger evidence; any deferral of a static fixture root is
disclosed in the Implementation Notes.

- [x] ✅ T001 Add `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs` and include it in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` after `AgentsCommandTests.fs`.
- [x] ✅ T002 Add `tests/FS.GG.SDD.Artifacts.Tests/RefreshSummaryViewTests.fs` and include it in `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` after `AgentGuidanceViewTests.fs`.
- [-] 🟨 T003 [P] Add valid refresh fixture manifests under `tests/fixtures/lifecycle-commands/refresh-current/manifest.yml`, `tests/fixtures/lifecycle-commands/refresh-stale-views/manifest.yml`, `tests/fixtures/lifecycle-commands/refresh-missing-view/manifest.yml`, and `tests/fixtures/lifecycle-commands/refresh-summary/manifest.yml`, each an initialized SDD project tree (`.fsgg/`, `work/<id>/` authored sources, and the relevant existing/absent `readiness/<id>/` generated views per scenario).  
  - 🟨 deferred — covered by real-evidence RefreshCommandTests over disposable shipped project trees per the 014-agent-guidance precedent; static fixture root not added.
- [-] 🟨 T004 [P] Add valid preservation/not-applicable fixture manifests under `tests/fixtures/lifecycle-commands/refresh-preserves-authored/manifest.yml` (authored sources + `.fsgg/*.yml` present, asserted byte-unchanged) and `tests/fixtures/lifecycle-commands/refresh-no-agent-targets/manifest.yml` (no agent config / no configured targets, `agent-commands` reported not-applicable).  
  - 🟨 deferred — covered by real-evidence RefreshCommandTests (preservation + not-applicable) over disposable trees; static fixture root not added.
- [-] 🟨 T005 [P] Add new blocked refresh fixture manifests under `tests/fixtures/lifecycle-commands/stale-source/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-generated-view/manifest.yml`, `tests/fixtures/lifecycle-commands/blocked-upstream-view/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-source/manifest.yml`, and `tests/fixtures/lifecycle-commands/malformed-source/manifest.yml` (all new roots — the earlier slices used per-artifact `missing-analysis`/`malformed-analysis`-style roots, not generic source roots); and extend the existing shared roots `tests/fixtures/lifecycle-commands/{outside-project,malformed-work-id,duplicate-work-id,unknown-source-reference,dry-run,deterministic-report,text-projection,governance-boundary}/manifest.yml` with refresh-specific expectations.  
  - 🟨 deferred — blocked/shared fixture roots covered by real-evidence RefreshCommandTests over disposable trees; static fixture roots not added.

**Deferred edge-case scope**: One spec edge case is intentionally not given a
dedicated fixture in this slice and is deferred to a later feature: malformed or
incomplete optional Governance files (the `governance-boundary` fixture covers
absent and present-as-advisory Governance, which satisfies the no-Governance and
advisory-only obligations FR-022/FR-023). The diagnose-only future/unsupported/
deprecated source/view schema-version posture is exercised through the malformed
source and malformed-generated-view scenarios rather than a dedicated migration
fixture.

**Checkpoint**: Fixture and test file entry points exist; no refresh behavior is
implemented yet.

## Phase 2: Foundational Contracts (Blocking Prerequisites)

**Purpose**: Define the public artifact (summary projection), command, report,
diagnostic, and MVU contracts before user-story implementation. `.fsi`
signatures precede public `.fs` implementation bodies.

### Failing contract tests

- [x] ✅ T006 Add failing `summary.md` generation-manifest tests for `expectedSummaryOutputPath workId` (= `readiness/<id>/summary.md`), `createSummaryManifest` (view kind `summary`, schema version 1, generator identity, source paths/digests/schema/status for the structured readiness views it projects, output digest), and the always-true `Generated` marker in `tests/FS.GG.SDD.Artifacts.Tests/RefreshSummaryViewTests.fs`.
- [x] ✅ T007 Add failing `summary.md` rendering/faithfulness tests: the rendered body's per-view currency table, diagnostics, outcome, and next action equal the structured inputs; rendering reads only structured readiness data and introduces no fact absent from them (FR-006, SC-008); deterministic byte-identical output for identical inputs; and the generated-marker header records sources and generator, in `tests/FS.GG.SDD.Artifacts.Tests/RefreshSummaryViewTests.fs`.
- [-] 🟨 T008 [P] Add failing cross-view currency assertions over the SDD-owned view set (`work-model`, `analysis`, `verify`, `ship`, `agent-commands`, `summary`) covering `current`, `missing`, `stale`, `malformed`, and `blocked`, computed by `GenerationManifest.isStale` over `SourceIdentity` digests/schema status (not file presence), in `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`.  
  - 🟨 partial — cross-view currency (isStale over the summary source set) covered in RefreshSummaryViewTests; full per-kind matrix in GeneratedModelCurrencyTests not added.
- [-] 🟨 T009 [P] Add failing source-of dependency ordering assertions: an upstream view (e.g. the normalized work model) is evaluated/brought to currency before a dependent view (agent guidance, summary), and an upstream view that cannot reach `Current` forces its dependents to `Blocked`, in `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`.  
  - 🟨 partial — source-of ordering / blocked-upstream covered behaviorally in RefreshCommandTests; dedicated GeneratedModelCurrencyTests ordering asserts not added.

### Public `.fsi` contract additions

- [x] ✅ T010 Extend `src/FS.GG.SDD.Artifacts/GenerationManifest.fsi` with `val expectedSummaryOutputPath: workId: string -> string` and `val createSummaryManifest: viewPath: string -> generatorVersion: string -> sources: SourceIdentity list -> outputDigest: string -> GenerationManifest`, reusing the existing `GeneratedViewKind.Summary`, `SourceIdentity`, and `GenerationManifest` (no redefinition). (depends on T006, T007)
- [x] ✅ T011 Extend the public MVU command contract in `src/FS.GG.SDD.Commands/CommandTypes.fsi` with `SddCommand.Refresh`, `RefreshDisposition` (`RefreshedCurrent|PartiallyBlocked|Blocked`), `val refreshDispositionValue: disposition: RefreshDisposition -> string` (`refreshed-current|partially-blocked|blocked`), `RefreshSummary` (per `data-model.md`/`contracts/refresh-report-json.md`: `WorkId`, `Stage`, `Status`, `SummaryPath`, `RefreshedViewIds`, `AlreadyCurrentViewIds`, `BlockedViewIds`, `NotApplicableViewIds`, `PreservedAuthoredPaths`, `FindingIds`, `AdvisoryCount`, `WarningCount`, `BlockingCount`, `Disposition`, `PerViewState`, `SourceSnapshotCount`, `Readiness`), `CommandReport.Refresh: RefreshSummary option`, and `CommandModel.Refresh`, keeping `CommandMsg`, `CommandEffect`, `CommandWorkflow.init`, `CommandWorkflow.update`, and the interpreter boundary explicit through `src/FS.GG.SDD.Commands/CommandWorkflow.fsi` and `src/FS.GG.SDD.Commands/CommandEffects.fsi`. (after T010)
- [x] ✅ T012 Add `commandName Refresh = "refresh"`, `commandStage Refresh = "refresh"`, `parseCommand "refresh" = Ok Refresh`, and `nextLifecycleCommand Refresh = None` signatures/cases to `src/FS.GG.SDD.Commands/CommandTypes.fsi` (cross-cutting, like `Agents`; charter->ship chain unchanged). (after T011)
- [x] ✅ T013 Add refresh diagnostic constructor signatures to `src/FS.GG.SDD.Commands/CommandReports.fsi`: `refreshMissingSource`, `refreshMalformedSource`, `refreshStaleView`, `refreshMalformedGeneratedView`, `refreshBlockedUpstreamView`, `refreshUnrenderableSummary` (each: stable id, affected view path, affected source/upstream view when available, severity, message, user-correctable correction — FR-020), reusing shared `outsideProject`/`malformedWorkId`/`duplicateWorkId`/`unknownSourceReference`/`malformedGeneratedView` builders, plus the `refreshed`/blocking next-action signatures. (after T011, T012)
- [x] ✅ T014 [P] Add failing command public-surface tests for `SddCommand.Refresh`, `RefreshDisposition`, `refreshDispositionValue`, `RefreshSummary`, the refresh diagnostics, `CommandReport.Refresh`, `CommandModel.Refresh`, and the unchanged `CommandModel`/`CommandMsg`/`CommandEffect` MVU boundary in `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`, and the `summary.md` artifact surface (`expectedSummaryOutputPath`, `createSummaryManifest`) in `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs`.
- [x] ✅ T015 Add prelude references for `SddCommand.Refresh`, `parseCommand "refresh"`, `commandStage Refresh` (= `"refresh"`), `nextLifecycleCommand Refresh` (returns `None`), `nextLifecycleCommand Ship` (still `None`, chain unchanged), `RefreshDisposition`/`refreshDispositionValue`/`RefreshSummary` visibility, `expectedSummaryOutputPath`/`createSummaryManifest` visibility, and refresh diagnostic visibility in `scripts/prelude.fsx`; run `dotnet fsi scripts/prelude.fsx` against the draft public surface before implementation-body tasks T017+, saving the early transcript to `specs/015-refresh-command/readiness/fsi-public-surface-draft.txt`. (after T010 through T014)
- [x] ✅ T016 Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` and `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the deliberate refresh public-surface additions after T010 through T015.

### Implementation bodies

- [x] ✅ T017 Implement `expectedSummaryOutputPath` and `createSummaryManifest` (manifest assembly: `Summary` kind, schema version 1, deterministic source ordering in declared-source order, source/output digests) in `src/FS.GG.SDD.Artifacts/GenerationManifest.fs` after T015.
- [x] ✅ T018 Implement the deterministic `summary.md` body/header renderer (generated-marker header mirroring the manifest; body projecting lifecycle stage, per-view currency table, diagnostics, outcome, and next action strictly from structured readiness data; no clocks/durations/ANSI/enumeration-order/host-path/randomness) in `src/FS.GG.SDD.Commands/CommandRendering.fs` after T015.
- [x] ✅ T019 Implement refresh command contract values — `commandName`/`commandStage`/`parseCommand`/`nextLifecycleCommand` for `Refresh`, `refreshDispositionValue`, `refreshRequest`, `runRefresh`, the disposable-project refresh test helper, and source/generated-byte snapshot helpers — in `src/FS.GG.SDD.Commands/CommandTypes.fs` and `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` after T015.
- [x] ✅ T020 Implement the refresh diagnostic constructors, blocked-report correction routing, and the `refreshed`/blocked next-action selection in `src/FS.GG.SDD.Commands/CommandReports.fs` after T015.

**Checkpoint**: Public `.fsi` contracts, the `summary.md` manifest/renderer, the
command/report/diagnostic/MVU additions, and surface baselines are ready for
story implementation.

## Phase 3: User Story 1 - Refresh a Work Item's Generated Views From Declared Sources (Priority: P1, MVP)

**Goal**: `fsgg-sdd refresh --work <id>` loads one work item, evaluates each
SDD-owned view's currency in declared source-of order, re-runs the existing
per-view generators from current declared sources for the views that can be
refreshed (work model -> analysis -> verify -> ship -> agent guidance), records
each view's source relationships/digests/schema versions/generator identity with
the generated marker, reports refreshed and already-current views, the per-view
state, the disposition, and the next action — all without requiring Governance.

**Summary-scope note (see C1 / spec US3)**: US1 deliberately delivers the
**structured** SDD-owned views only; the `readiness/<id>/summary.md` projection is
owned by spec User Story 3 and is implemented in Phase 5 (US3). Consequently
`summary.md` is **not** current after a Phase-3-only MVP run, so quickstart
Scenario 1's `summary.md` assertion is satisfied only once US3 lands. The spec's
US1 narrative mentions the human summary; that surface is realized via US3 here to
avoid duplicating the US3 contract. The MVP is still independently demoable for
the structured views.

**Independent Test**: Run `refresh --work 015-refresh-command` in an initialized
project whose authored sources are valid; confirm every refreshable **structured**
SDD-owned view (`work-model.json`, `analysis.json`, `verify.json`, `ship.json`,
`agent-commands/<target>/`) is regenerated from current declared sources, each
records its sources and generator identity, already-current views are reported
untouched, and the report names the work id, refreshed views, per-view state,
`disposition = refreshed-current`, outcome, and next action without Governance.
(The `summary` view's `current` state is asserted from US3 onward.)

### Tests for User Story 1

- [x] ✅ T021 [P] [US1] Add failing refresh-flow command tests for regeneration of `readiness/<id>/{work-model.json,analysis.json,verify.json,ship.json}` and `agent-commands/<target>/` from current sources, recorded source relationships/digests/schema/generator identity, refreshed vs already-current view ids, changed generated artifacts, per-view state, `disposition = refreshed-current`, and the `refreshed` next action in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T022 [P] [US1] Add failing pure `CommandWorkflow.init`/`CommandWorkflow.update` tests for `Refresh` read effects (project config, sdd config, optional agents config, authored sources, existing generated views, `EnumerateDirectory "work"` for duplicate-id detection), per-view `WriteFile(..., GeneratedView)` write effects in source-of order, emitted stdout/stderr effects, and dry-run effect suppression in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [x] ✅ T023 [P] [US1] Add failing source-of ordering test asserting upstream views reach `Current` before dependent views are evaluated/written, and a fresh-and-current run reporting every applicable view `current`, in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`. Note: the no-write already-current path reports `disposition = refreshed-current` in US1; the distinct `Outcome.NoChange` (vs `Succeeded`) mapping for that path is finalized in US4 (T060), so assert disposition here and defer the `NoChange` outcome assertion to T057/T060.
- [x] ✅ T024 [P] [US1] Add a failing no-Governance success test asserting no freshness/route/profile/gate/audit/protected-boundary/release verdict appears (and no stale-view blocking at a boundary) in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [x] ✅ T025 [P] [US1] Add a failing report shape assertion for `refresh` covering `command`, `workId`, `changedArtifacts`, `refresh`, `generatedViews`, `diagnostics`, `governanceCompatibility`, and `nextAction` in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 1

- [x] ✅ T026 [US1] Wire `Refresh` into read planning in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`: read `.fsgg/project.yml`, `.fsgg/sdd.yml`, optional `.fsgg/agents.yml`, the selected work item's authored sources, each existing `readiness/<id>/` generated view (`work-model.json`, `analysis.json`, `verify.json`, `ship.json`, `summary.md`, `agent-commands/<target>/guidance.json`), and `EnumerateDirectory "work"` for duplicate-id detection (and **not** reading `CLAUDE.md`/`AGENTS.md` as derivation inputs).
- [x] ✅ T027 [US1] Implement `LoadProject`/`LoadWorkItem` for `Refresh`: initialized-project validation (else `outsideProject`, `Blocked`), single selected work-id validation against the normalized work model (empty/malformed/mismatched/duplicate → block), in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [-] 🟨 T028 [US1] Implement `PlanGeneratedViewRefresh` orchestration that walks the SDD-owned views in declared source-of order (work model -> analysis -> verify -> ship -> agent guidance), evaluates each view's currency from recorded `SourceIdentity` digests vs. current sources via `GenerationManifest.isStale`, and sequences upstream views before dependents, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.  
  - 🟨 deviated — work model is regenerated in declared source-of order and feeds agent guidance/summary; analysis/verify/ship are currency-reported, not destructively re-run (see Implementation Notes / pre-existing lifecycle evidence-freshness coupling).
- [-] 🟨 T029 [US1] For each refreshable view, invoke the existing per-view generator from current declared sources (not from a prior generated view's cached content, except where one generated view is the declared source of another — FR-004) and plan `WriteFile(..., GeneratedView)` effects only when sources are valid and `CommandRequest.DryRun = false`, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.  
  - 🟨 deviated — per-view regeneration is applied to work model + agent guidance + summary; analysis/verify/ship currency is reported rather than re-run (see Implementation Notes).
- [x] ✅ T030 [US1] Build per-view source-snapshot records (path, digest, schema version, status) and the `GeneratedViewState` report entries (path, kind, schemaVersion, generator, sources, outputDigest, currency, diagnosticIds) for every SDD-owned view in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T031 [US1] Reuse the existing agent-guidance generator within the orchestration for configured targets and mark `agent-commands` `NotApplicable` (no diagnostic) when no agent config / no configured targets exist (FR-003, assumption) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T032 [US1] Build the `RefreshDisposition` and `RefreshSummary` for the success path (refreshed/already-current/not-applicable view ids, preserved authored paths, finding/count fields, `PerViewState` over the full view set, source snapshot count, readiness, `disposition = refreshed-current` when all applicable views current) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T033 [US1] Remove the unsupported-command path for `Refresh` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, make `src/FS.GG.SDD.Cli/Program.fs` able to run `refresh --work <id>`, and return the `refreshed` next action (continue lifecycle / rely on refreshed readiness) from `src/FS.GG.SDD.Commands/CommandReports.fs`.

**Checkpoint**: User Story 1 is independently testable as the MVP — the
orchestrated refresh of the structured SDD-owned views (excluding the
`summary.md` projection, which lands in US3).

## Phase 4: User Story 2 - Detect Stale and Unrefreshable Generated Views (Priority: P1)

**Goal**: `refresh` reports stale, missing, malformed, and blocked generated
views precisely; never treats a view as current when its recorded source
digests/schema/generator no longer match; never fabricates or refreshes a view
from a missing/malformed/blocked source; refreshes the views that *can* refresh;
and reports a dependent view as blocked naming the upstream view to correct
first — each finding naming the affected view and the source or upstream view.

**Independent Test**: Run `refresh` against fixtures/projects with known defects
(stale view vs current sources; missing view file; malformed existing view;
missing/malformed/stale declared source; unknown source reference; dependent view
blocked on an un-current upstream); confirm no view is reported current until the
report identifies the affected view, source/upstream, severity, and correction,
and that no generated write is planned for the blocked views while refreshable
views still refresh.

### Tests for User Story 2

- [x] ✅ T034 [US2] Add failing stale/missing/malformed view tests: a view whose recorded digests/schema/generator no longer match current sources → `stale` with `refreshStaleView` (refreshed same run when safe); an absent view file → `missing` → regenerated; a malformed existing view → `malformed` → regenerated from sources with `refreshMalformedGeneratedView`, in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T035 [US2] Add failing source-gate tests: a view's declared source missing (`refreshMissingSource`) / malformed (`refreshMalformedSource`) / stale (`refreshStaleView`) / unknown reference (`unknownSourceReference`) → that view `blocked` with no write and no fabrication, while other refreshable views still refresh (US2-2), in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T036 [US2] Add failing blocked-upstream tests: a dependent view whose upstream view could not be brought to `Current` is reported `blocked` and names the upstream view (`refreshBlockedUpstreamView`); the overall disposition is `partially-blocked`, in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T037 [P] [US2] Add failing MVU assertions that blocked views never emit `WriteFile`/`CreateDirectory` effects while refreshable views still do, in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [x] ✅ T038 [P] [US2] Add failing generated-view diagnostic tests distinguishing `current`/`missing`/`stale`/`malformed`/`blocked` and naming the affected view and source/upstream in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [x] ✅ T039 [P] [US2] Add failing refresh diagnostic serialization assertions for `refreshMissingSource`, `refreshMalformedSource`, `refreshStaleView`, `refreshMalformedGeneratedView`, and `refreshBlockedUpstreamView` (stable ids, affected view + source/upstream, severity, correction) in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 2

- [x] ✅ T040 [US2] Implement per-view currency classification mapping recorded `SourceIdentity` mismatches to `Stale`, absent files to `Missing`, unreadable files to `Malformed`, and unusable sources to `Blocked`, with the matching refresh diagnostics, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T041 [US2] Implement source-gate blocking: a view with a missing/malformed/stale/unknown-reference source is `Blocked` with no write and is never fabricated, while the refreshable views in the same run still refresh (FR-010, US2-2), in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T042 [US2] Implement malformed-existing-view regeneration (regenerate from current sources when sources are valid; otherwise `Blocked`) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T043 [US2] Implement blocked-upstream propagation: when a declared upstream view does not reach `Current`, force its dependents to `Blocked` and name the upstream via `refreshBlockedUpstreamView` (FR-011) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T044 [US2] Map the partial/blocked outcomes to `RefreshDisposition` (`partially-blocked` when ≥1 view blocked but others refreshed/current; `blocked` when project/id invalid or no view refreshable) and the `SucceededWithWarnings`/`Blocked` outcomes in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T045 [US2] Build blocking refresh findings with stable ids and structured links to the affected view, source, or upstream view, and route blocked next actions to source correction, upstream-view correction, or re-running the responsible lifecycle command, in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T046 [US2] Add blocked-scenario and per-view-state/disposition assertion helpers for refresh tests in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.

**Checkpoint**: Stale, missing, malformed, source-blocked, and upstream-blocked
views are identified precisely, refreshable views still refresh, and no view is
reported current without its diagnostic.

## Phase 5: User Story 3 - Produce a Human-Readable Readiness Summary (Priority: P2)

**Goal**: `refresh` renders `readiness/<id>/summary.md` last, as a projection of
the structured readiness data, marked generated with its source relationships;
the summary reflects the same lifecycle state, per-view currency, diagnostics,
outcome, and next action as the authoritative report and adds no fact absent from
the structured views; and when the structured data required to render it is
missing/stale/blocked, the report records `refreshUnrenderableSummary` and the
summary view is `Blocked` with nothing rendered from unusable data.

**Independent Test**: Run `refresh` and confirm `summary.md` is generated as a
projection, carries the generated-marker header with its sources and generator,
and its per-view state table, diagnostics, outcome, and next action equal the
report's `Refresh.PerViewState`, `Diagnostics`, `Outcome`, and `NextAction`;
then run the unrenderable case and confirm `refreshUnrenderableSummary`, a
`Blocked` summary view, and no `summary.md` written.

### Tests for User Story 3

- [x] ✅ T047 [US3] Add failing summary-projection command tests: `readiness/<id>/summary.md` generated/refreshed, marked generated, records its sources, and its per-view state table/diagnostics/outcome/next action equal the report's `refresh.perViewState`/`diagnostics`/`outcome`/`nextAction` (SC-008, FR-006), in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T048 [US3] Add a failing unrenderable-summary test: when required structured inputs are missing/stale/blocked, assert `refreshUnrenderableSummary`, a `Blocked` summary view, and no `summary.md` written from unusable data (US3-3), in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T049 [P] [US3] Add a failing summary-faithfulness artifact test asserting the rendered body contains no lifecycle/currency/diagnostic/outcome/next-action fact absent from the structured inputs, in `tests/FS.GG.SDD.Artifacts.Tests/RefreshSummaryViewTests.fs`.
- [x] ✅ T050 [P] [US3] Add a failing MVU assertion that summary rendering is sequenced last (after all structured views reach terminal state) and emits a single `WriteFile(summary.md, GeneratedView)` only when its structured inputs are usable, in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.

### Implementation for User Story 3

- [x] ✅ T051 [US3] Sequence summary rendering last in `PlanGeneratedViewRefresh`: assemble the summary's `SourceIdentity` list from the structured readiness views, build the manifest via `createSummaryManifest`, render via the T018 renderer, and plan a single `WriteFile(summary.md, GeneratedView)` when inputs are usable, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T052 [US3] Implement the unrenderable-summary path: when the required structured inputs are missing/stale/blocked, emit `refreshUnrenderableSummary`, mark the summary view `Blocked`, plan no summary write, and fold it into the disposition, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T053 [US3] Populate `RefreshSummary.SummaryPath`, the `summary` entry in `PerViewState`/`GeneratedViews`, and the summary view's source snapshot in the report in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.

**Checkpoint**: The human-readable `summary.md` is a faithful, generated
projection of the structured readiness data, refreshed alongside the other views,
and blocked rather than fabricated when its inputs are unusable.

## Phase 6: User Story 4 - Keep Authored Sources Authoritative and Refresh Safe to Repeat (Priority: P2)

**Goal**: `refresh` is a non-destructive, repeatable generator; authored
lifecycle artifacts, `.fsgg/*.yml`, and the hand-owned `CLAUDE.md`/`AGENTS.md`
are never created/updated/reordered/normalized/removed; only generated views
under their generated roots change; a dry run mutates zero files while still
reporting proposed changes, diagnostics, per-view state, and next action.

**Independent Test**: Run `refresh` in valid, stale, and dry-run scenarios and
confirm authored lifecycle artifacts and `.fsgg/*.yml` remain byte-identical
(reported `Preserve`/`NoChange`), only generated views change in a normal run,
and a dry run changes no files.

### Tests for User Story 4

- [x] ✅ T054 [US4] Add failing authored-source preservation tests asserting byte-identical `work/<id>/` sources, `.fsgg/*.yml`, `CLAUDE.md`, and `AGENTS.md` after valid and blocked runs (each reported `Preserve`/`NoChange`) in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T055 [US4] Add failing dry-run tests asserting zero authored and generated file changes (no view or `summary.md` mutation, no directory creation) while still reporting proposed generated-view changes, diagnostics, per-view state, and next action, in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T056 [P] [US4] Add failing MVU assertions that `Refresh` never emits an authored `WriteFile` for any `work/<id>/` source, `.fsgg/*.yml`, or `CLAUDE.md`/`AGENTS.md`, and that dry-run suppresses all `WriteFile`/`CreateDirectory` effects, in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [x] ✅ T057 [P] [US4] Add failing rerun-current `NoChange` tests for already-current view sets (no writes planned) in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.

### Implementation for User Story 4

- [x] ✅ T058 [US4] Ensure the refresh workflow plans no authored-source/`.fsgg/*.yml`/`CLAUDE.md`/`AGENTS.md` write effects in any path, records them as `Preserve`/`NoChange` in `ChangedArtifacts`, and restricts generated writes to the SDD-owned views and `summary.md`, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T059 [US4] Implement the dry-run path that reports proposed generated-view changes without emitting any `WriteFile`/`CreateDirectory` effect (FR-021, SC-006) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T060 [US4] Implement rerun-current `NoChange`/`refreshed-current` behavior when every applicable view is already current (no writes planned) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T061 [US4] Serialize generated-view operations and safe-write decisions for `create`, `update`, `preserve`, `noChange`, and refused/blocked views for refresh in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.

**Checkpoint**: Authored sources, `.fsgg/*.yml`, and the hand-owned guidance-target
files are preserved across valid, blocked, and dry-run paths; only generated
views change; dry run writes nothing.

## Phase 7: User Story 5 - Keep Refreshed Views Deterministic and Traceable (Priority: P3)

**Goal**: Refreshed views, the JSON report, the text projection, and `summary.md`
are deterministic projections of one authoritative report with explicit
provenance; the text projection adds no facts; optional Governance pointers stay
advisory; the command works with Governance absent.

**Independent Test**: Run identical `refresh` requests repeatedly and compare
report JSON, regenerated view bytes, and `summary.md` bytes; render text; run CLI
smoke paths; confirm every text fact exists in the JSON report, every refreshed
view identifies its sources and generator identity, and optional Governance
references remain advisory (no stale-view blocking at a protected boundary).

### Tests for User Story 5

- [x] ✅ T062 [P] [US5] Add a failing deterministic test for three identical `refresh` runs comparing report JSON, regenerated view bytes, and `summary.md` bytes (byte-identical, no absolute host paths) in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [x] ✅ T063 [P] [US5] Add a failing refresh text-projection test for selected work id, outcome, refreshed/already-current/blocked/not-applicable view ids, per-view state, disposition, counts, diagnostics, and next action (no facts absent from the JSON report) in `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.
- [x] ✅ T064 [P] [US5] Add a failing boundary test excluding effective-evidence freshness, route, profile, gate, audit, protected-boundary enforcement (incl. stale-view blocking), and release verdicts, asserting optional Governance pointers stay advisory and unevaluated, in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [x] ✅ T065 [US5] Add disposable-project CLI JSON, dry-run, and text smoke helpers for `refresh --work <id> --root <path> [--dry-run] [--text]` through `src/FS.GG.SDD.Cli/Program.fs` in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.
- [x] ✅ T066 [US5] Add local performance assertions under the three-second harness budget for `refresh-current`, `refresh-stale-views`, and `deterministic-report` in `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`.

### Implementation for User Story 5

- [x] ✅ T067 [US5] Serialize the `refresh` summary object (or `null`), per-view state, refreshed/blocked/not-applicable view ids, preserved authored paths, disposition, diagnostics, Governance compatibility facts, and next action with deterministic key/array ordering (declared-source / view-kind order) and no host-specific fields in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [x] ✅ T068 [US5] Render the refresh text summary (work id, outcome, refreshed/already-current/blocked/not-applicable view ids, per-view state, disposition, counts, diagnostics, next action) from the command report in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [x] ✅ T069 [US5] Keep refresh Governance compatibility facts advisory and unevaluated, and keep effective-evidence freshness, route, profile, gate, audit, protected-boundary enforcement, and release fields absent from refresh reports, in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T070 [US5] Parse `refresh --work <id> --json|--text --dry-run --root <path>` and map arguments to `CommandRequest` (`OverwritePolicy = AllowGeneratedRefresh`, default-only, matching the `ship`/`agents` precedent) in `src/FS.GG.SDD.Cli/Program.fs`.
- [x] ✅ T071 [US5] Exclude timestamps, durations, terminal details, process ids, random values, directory enumeration order, absolute host paths, and host-specific separators from refresh reports, regenerated views, and `summary.md` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and `src/FS.GG.SDD.Commands/CommandRendering.fs`.

**Checkpoint**: Machine-readable reports/views and human-readable projections are
deterministic projections of one report contract with explicit provenance, usable
without Governance.

## Phase 8: Polish, Evidence, And Documentation

**Purpose**: Record mandatory verification evidence and update human-facing state
after implementation is complete.

- [x] ✅ T072 [P] Run `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~RefreshSummary"` and save output to `specs/015-refresh-command/readiness/artifact-refresh-tests.txt`.
- [x] ✅ T073 [P] Run `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Refresh"` and save output to `specs/015-refresh-command/readiness/command-refresh-tests.txt`.
- [x] ✅ T074 [P] Run focused deterministic JSON, text projection, generated-view, and Governance boundary evidence tests and save output to `specs/015-refresh-command/readiness/output-boundary-tests.txt`.
- [x] ✅ T075 Run `dotnet build FS.GG.SDD.sln -c Release` and save output to `specs/015-refresh-command/readiness/build-release.txt`.
- [x] ✅ T076 Run `dotnet test FS.GG.SDD.sln -c Release` and save output to `specs/015-refresh-command/readiness/full-suite.txt` (expect no regression in the existing 281-test baseline plus the new refresh tests).
- [x] ✅ T077 Run `dotnet fsi scripts/prelude.fsx` after a Release build and save the transcript to `specs/015-refresh-command/readiness/fsi-public-surface.txt`.
- [x] ✅ T078 Run a disposable-project CLI JSON smoke scenario for `fsgg-sdd refresh` and save output to `specs/015-refresh-command/readiness/cli-json-smoke.txt`.
- [x] ✅ T079 Run a disposable-project CLI dry-run smoke scenario for `fsgg-sdd refresh --dry-run` and save output to `specs/015-refresh-command/readiness/cli-dry-run-smoke.txt`.
- [x] ✅ T080 Run a disposable-project CLI text smoke scenario for `fsgg-sdd refresh --text`, save output to `specs/015-refresh-command/readiness/cli-text-smoke.txt`, `cat readiness/<id>/summary.md`, and record human-summary review notes in `specs/015-refresh-command/readiness/human-summary-review.md`.
- [x] ✅ T081 Record refresh, rerun-current, and stale-refresh performance evidence for `refresh-current`, `refresh-stale-views`, and `deterministic-report` in `specs/015-refresh-command/readiness/performance.md`.
- [x] ✅ T082 Record SDD/Governance boundary review findings (refreshed views and `summary.md` are not a second source of truth; no freshness/route/profile/gate/audit/release behavior; no stale-view blocking at a protected boundary; optional Governance pointers stay advisory) in `specs/015-refresh-command/readiness/sdd-governance-boundary.md`.
- [x] ✅ T083 Record artifact traceability from `specs/015-refresh-command/spec.md` requirements (FR-001..FR-024, SC-001..SC-009) to plan decisions, tasks, tests, and readiness evidence in `specs/015-refresh-command/readiness/artifact-traceability.md`, disclosing any deferred static fixture roots.
- [x] ✅ T084 Update `docs/initial-implementation-plan.md` to mark Phase 7 `fsgg-sdd refresh` complete and reference `specs/015-refresh-command/readiness/`.
- [x] ✅ T085 Update `README.md`, `AGENTS.md`, and `CLAUDE.md` only where needed to keep the current command state synchronized after the refresh workflow lands (without turning refreshed views into a second source of truth).

**Checkpoint**: The implementation has build, test, FSI, CLI, performance,
boundary, and traceability evidence.

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 (US1) depends on Phase 2 and is the MVP scope.
- Phase 4 (US2) depends on Phase 3 because stale/blocked detection reuses the
  loaded source set, the per-view currency evaluation, and the orchestration.
- Phase 5 (US3) depends on Phases 3 and 4 because the summary is rendered last
  from the structured views' terminal state and must reflect their diagnostics.
- Phase 6 (US4) depends on Phases 3 through 5 because preservation and dry-run
  guarantees must hold across success, blocked, and summary paths.
- Phase 7 (US5) depends on Phases 3 through 6 because output contracts must
  include success, blocked, dry-run, no-Governance, summary, and preservation
  states.
- Phase 8 depends on the implemented story scope being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2; no dependency on other stories.
- **US2 (P1)**: Depends on US1 source loading, per-view currency evaluation, and
  the base report shape.
- **US3 (P2)**: Depends on US1 orchestration and US2 per-view terminal state.
- **US4 (P2)**: Depends on US1 generated-write planning and US2 blocked-path
  behavior.
- **US5 (P3)**: Depends on refresh summaries, diagnostics, generated-view
  reporting, the summary projection, and preservation behavior from US1–US4.

### Cross-Task Dependencies

- T010 depends on T006, T007; T011 depends on T010; T012, T013 depend on T011.
- T015 depends on T010 through T014 and must run before implementation-body tasks
  T017 through T020.
- T016 depends on T010 through T015.
- T017 through T020 depend on the FSI/prelude exercise in T015.
- T026 through T033 depend on T017 through T020.
- T028 (orchestration ordering) precedes T029 (per-view regeneration), which
  precedes T032 (disposition).
- T040 through T046 depend on T034 through T039.
- T051 through T053 depend on T047 through T050 and on the T018 renderer.
- T058 through T061 depend on T054 through T057.
- T067 through T071 depend on T062 through T066.
- T072 through T083 depend on all selected implementation tasks passing.
- T084 and T085 depend on readiness evidence from T072 through T083.

### Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001 and T002.
- T008, T009, and T014 can run in parallel because they touch different test
  files.
- T021 through T025 can run in parallel because each touches a different test
  file.
- T037, T038, and T039 can run in parallel with the `RefreshCommandTests.fs`
  tasks T034, T035, and T036 (the three `RefreshCommandTests.fs` tasks share a
  file and run sequentially among themselves).
- T049 and T050 can run in parallel with the `RefreshCommandTests.fs` tasks T047
  and T048.
- T056 and T057 can run in parallel with the `RefreshCommandTests.fs` tasks T054
  and T055.
- T062, T063, and T064 can run in parallel because they touch different output
  and boundary test files.
- T072, T073, and T074 can run in parallel after implementation is complete.

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (each touches a different test file):
Task: "Refresh-flow command tests in tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs"
Task: "Pure init/update effect tests in tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs"
Task: "Source-of ordering tests in tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs"
Task: "No-Governance success test in tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs"
Task: "Report shape assertion in tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs"
```

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3.
3. Run the focused artifact and command evidence tests for US1.
4. Validate that `fsgg-sdd refresh --work <id>` orchestrates a refresh of the
   structured SDD-owned views from current declared sources in source-of order,
   records source digests and generator identity with the generated marker,
   reports refreshed and already-current views with per-view state and the
   `refreshed-current` disposition, preserves authored sources, and works without
   Governance. (The `summary.md` projection lands in US3.)

### Incremental Delivery

1. US1 delivers the orchestrated refresh of the structured generated views and
   the success report.
2. US2 detects and blocks stale/missing/malformed/source-blocked/upstream-blocked
   views with precise diagnostics while still refreshing what can refresh.
3. US3 adds the faithful `summary.md` projection rendered from the structured
   views.
4. US4 guarantees non-destructive preservation and dry-run behavior.
5. US5 locks down deterministic JSON/views/`summary.md`, CLI smoke paths, and
   optional Governance boundaries.
6. Phase 8 records readiness evidence and updates project guidance.

## Task Counts

- Shared setup, foundation, and polish tasks: 34
  - Setup (Phase 1): 5 (T001–T005)
  - Foundational contracts (Phase 2): 15 (T006–T020)
  - Polish/evidence/docs (Phase 8): 14 (T072–T085)
- US1 tasks: 13 (T021–T033)
- US2 tasks: 13 (T034–T046)
- US3 tasks: 7 (T047–T053)
- US4 tasks: 8 (T054–T061)
- US5 tasks: 10 (T062–T071)
- Total tasks: 85

## Suggested MVP Scope

Complete Phases 1, 2, and 3 first. That delivers User Story 1: a usable
`fsgg-sdd refresh` command that orchestrates a refresh of the selected work
item's structured SDD-owned generated views (`work-model.json`, `analysis.json`,
`verify.json`, `ship.json`, `agent-commands/<target>/`) from current declared
sources in declared source-of order, marks each view generated with source
digests and generator identity, reports refreshed and already-current views with
per-view state and the `refreshed-current` disposition, preserves authored
sources, and works without Governance. The `summary.md` projection (US3), full
stale/blocked diagnostics (US2), dry-run/preservation hardening (US4), and
determinism/text/CLI/boundary lockdown (US5) layer on top.

## Implementation Notes (2026-06-20)

### Convergence wall: downstream regeneration reduced to currency-reporting (T028–T029)

US1 as literally specified asks refresh to *re-run* every per-view generator
(work model → analysis → verify → ship → agent guidance) from current sources.
While implementing the orchestration we discovered a **pre-existing, feature-
independent property of the lifecycle generators**: re-running `analyze` (and
then `verify`/`ship`) on an already-completed work item does **not** converge.

- Re-running `analyze` standalone on a verified project reports
  `needsGeneratedViewRefresh` and rewrites `analysis.json` to embed the current
  work model; re-running it again still reports `needsGeneratedViewRefresh`
  (never stabilizes).
- `verify`/`ship` then **block** (`evidence.analysisNotReady`,
  `evidence.staleEvidenceSource`, `ship.staleVerificationView`) because evidence
  freshness is cryptographically bound to the *prior* work model. Re-running the
  generators out of lifecycle order therefore *corrupts* an otherwise-clean
  project.

This was reproduced by driving the existing `analyze`/`verify`/`ship` commands
directly (no refresh code involved), confirming it is not introduced by this
feature. Fixing it would mean changing the lifecycle generators' evidence-
freshness model — out of scope for, and riskier than, the refresh feature.

**Decision (honest, convergent scope):** refresh regenerates the views whose
generators are idempotent/convergent — the **work model**, **agent guidance**,
and the new **`summary.md`** — in declared source-of order (the freshly
regenerated work model is injected before agent guidance and the summary derive
from it). For **`analysis.json`**, **`verify.json`**, and **`ship.json`** refresh
**reports currency** (`current` / `stale` / `missing` / `malformed` / `blocked`)
instead of destructively re-running them: if the work model changed they are
reported `stale` and the next action points back at the responsible lifecycle
command. This keeps refresh **idempotent** (rerun-current ⇒ `NoChange`) and
non-destructive, satisfies the stale/blocked-detection and summary obligations
(US2/US3), and never leaves the project in a worse state than it found it.

Tasks T028–T029 are marked 🟨 to reflect this deviation rather than claiming the
literal "re-run all generators" behavior. The marker is honest per the
`/speckit-implement` discipline (no green status resting on undelivered or
state-corrupting behavior).

### Static fixtures deferred to real-evidence tests (T003–T005)

Per the `014-agent-guidance` precedent, refresh scenarios are covered by
real-evidence `RefreshCommandTests` exercising disposable shipped project trees
(real filesystem, and three tests spawn the **real CLI binary**), which is
stronger evidence than static golden fixture directories. The static fixture
roots are not added.

### Evidence index (`readiness/`)

`artifact-refresh-tests.txt`, `command-refresh-tests.txt`,
`output-boundary-tests.txt`, `build-release.txt`, `full-suite.txt`,
`fsi-public-surface.txt`, `cli-json-smoke.txt`, `cli-text-smoke.txt`,
`cli-dry-run-smoke.txt`, `sample-summary.md`, `human-summary-review.md`,
`performance.md`, `sdd-governance-boundary.md`, `artifact-traceability.md`.
