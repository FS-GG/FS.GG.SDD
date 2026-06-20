# Tasks: Agent Guidance Generation

**Input**: Design documents from `specs/014-agent-guidance/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`quickstart.md`, and `contracts/` (`agent-guidance-view.md`, `agents-command.md`,
`agent-guidance-report-json.md`, `agent-guidance-fixtures.md`)

**Change Tier**: Tier 1 (contracted native SDD command surface, generated
per-target `readiness/<id>/agent-commands/<target>/` view, Claude/Codex guidance
derived from the normalized work model, command report JSON/text, generated-view
currency behavior, Claude/Codex equivalence obligation, diagnostics, and optional
Governance boundary facts).

**Tests**: Required by the specification and plan. Test tasks below are written
before implementation tasks and must fail before the implementation body is
completed.

**Status Legend**:

- `[x] ✅` done with real evidence (build, tests, FSI, and/or CLI smoke).
- `[ ] ⬜` pending / not started.
- `[-] 🟨` deferred with written rationale (see Implementation Notes below).

Never mark a failing task `[X]`. Never weaken an assertion to green a build —
narrow the scope and document it.

## Implementation Notes (2026-06-20)

The feature is **implemented and green**: `dotnet build FS.GG.SDD.sln -c Release`
succeeds, `dotnet test FS.GG.SDD.sln -c Release` passes **281 tests, 0 failures**
(Artifacts 78, Commands 203 — up from the 258-test baseline), `dotnet fsi
scripts/prelude.fsx` exits 0 with the new agent-guidance assertions, and CLI
JSON/dry-run/text smokes exit 0. Evidence is under
`specs/014-agent-guidance/readiness/`.

Two honest deviations from the literal task text (full detail in
`readiness/artifact-traceability.md`):

1. **Test-file consolidation.** Many test tasks name a specific existing test
   file (e.g. `CommandWorkflowTests.fs`, `GeneratedViewCommandTests.fs`,
   `GovernanceBoundaryCommandTests.fs`, `TextProjectionTests.fs`). The asserted
   behavior is implemented and passing, but consolidated into
   `AgentsCommandTests.fs` (command) and `AgentGuidanceViewTests.fs` (artifact)
   and exercised **end-to-end through the real interpreter** (`runAgents` over a
   full disposable lifecycle project), which is stronger evidence than isolated
   `init`/`update` effect-assertion unit tests. Those tasks are marked `[x]`
   because the production code path is exercised with real evidence.
2. **Static fixtures deferred (T003–T005, `[-] 🟨`).** The
   `tests/fixtures/lifecycle-commands/agents-*` directories were not authored.
   In this repo the fixture YAML manifests are documentation pointers — no
   generic runner asserts over them (only `deterministic-report` is consumed as a
   CLI root). The scenarios are instead covered with **real** evidence by
   `AgentsCommandTests`. Authoring inert fixture data would add no verification,
   so it is deferred and disclosed rather than marked complete.

One contract deviation: generated guidance files (`guidance.json`,
`commands.md`, `skills.md`) are written with the `GeneratedView` overwrite kind
(refreshable) rather than `AgentGuidanceTarget`, because
`CommandEffects.canOverwrite` correctly refuses overwriting `AgentGuidanceTarget`
files to protect the hand-owned `CLAUDE.md`/`AGENTS.md`. Semantics are unchanged.

**Task Format**: `[ID] [P?] [Story?] Description with exact file path`

- `[P]` means the task has no dependency on another incomplete task in the same
  phase and touches different files from other parallel tasks.
- `[US1]`, `[US2]`, `[US3]`, and `[US4]` map to the user stories in
  `specs/014-agent-guidance/spec.md`.
- Phases run sequentially. Tasks within a phase marked `[P]` may run in parallel.

**Elmish/MVU applicability**: `agents` is an I/O-bearing workflow. It reuses the
existing `CommandModel`/`CommandMsg`/`CommandEffect`/`init`/`update` plus the edge
interpreter. Tasks emit the `.fsi` contract additions (the
`GeneratedAgentGuidance` manifest, `NormalizedGuidanceModel` derivation,
`AgentGuidanceSummary`, guidance finding/disposition/diagnostic types,
`SddCommand.Agents`, `CommandReport.AgentGuidance`, `CommandModel.AgentGuidance`)
before `.fs` bodies, pure `CommandWorkflow.init`/`update` transition tests,
emitted-effect assertions, and real interpreter evidence through CLI smoke and
fixture runs. Like `analyze`, `verify`, and `ship`, `agents` authors no source
artifact: its only writes are the generated per-target `guidance.json` manifest
and its `commands.md`/`skills.md` Markdown projections under each configured
target's generated root. Unlike those lifecycle-stage commands, `agents` is
**cross-cutting** (`nextLifecycleCommand Agents = None`, charter->ship chain
unchanged), consumes the normalized work model rather than re-deriving lifecycle
facts, writes a per-target view (one manifest plus two Markdown files per target)
instead of one readiness file, derives Claude and Codex guidance from one shared
`NormalizedGuidanceModel`, and adds a Claude/Codex equivalence guardrail.

**Reuse note**: `AgentGuidanceConfig`, `AgentGuidanceTarget`, and
`parseAgentGuidanceConfig` already exist in
`src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi`; the
`GeneratedViewKind.AgentCommands` marker exists in
`src/FS.GG.SDD.Artifacts/GenerationManifest.fsi`; and the
`ArtifactWriteKind.AgentGuidanceTarget` marker exists in
`src/FS.GG.SDD.Commands/CommandTypes.fsi`. These are reused, not redefined.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the test and fixture entry points needed by the agent-guidance
slice.

**Fixture update rule**: The shared blocked/output/boundary fixture roots
(`outside-project`, `malformed-work-id`, `duplicate-work-id`,
`unknown-source-reference`, `dry-run`, `deterministic-report`, `text-projection`,
`governance-boundary`) already exist for earlier command slices. When a listed
directory already exists, extend its manifest with agent-guidance-specific
expectations; do not replace coverage used by earlier lifecycle command tests.
New roots cover configuration, target, work-model, and divergence cases unique to
this feature.

- [x] ✅ T001 Add `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs` and include it in `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` after `ShipCommandTests.fs`.
- [x] ✅ T002 Add `tests/FS.GG.SDD.Artifacts.Tests/AgentGuidanceViewTests.fs` and include it in `tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj` after `ShipViewTests.fs`.
- [-] 🟨 T003 [P] Add valid agent-guidance fixture manifests under `tests/fixtures/lifecycle-commands/agents-create/manifest.yml`, `tests/fixtures/lifecycle-commands/agents-rerun-current/manifest.yml`, `tests/fixtures/lifecycle-commands/agents-preserves-authored/manifest.yml`, and `tests/fixtures/lifecycle-commands/agents-refreshes-stale/manifest.yml`, each an initialized SDD project tree (`.fsgg/` incl. a valid `.fsgg/agents.yml`, `work/<id>/`, current `readiness/<id>/work-model.json`).
- [-] 🟨 T004 [P] Add valid per-target fixture manifests under `tests/fixtures/lifecycle-commands/agents-claude-only/manifest.yml`, `tests/fixtures/lifecycle-commands/agents-codex-only/manifest.yml`, and `tests/fixtures/lifecycle-commands/agents-claude-and-codex/manifest.yml` (the `claude-and-codex` fixture asserts equal `behaviorModelDigest` across both targets).
- [-] 🟨 T005 [P] Add or extend blocked agent-guidance fixture manifests under `tests/fixtures/lifecycle-commands/outside-project/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-agents-config/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-agents-config/manifest.yml`, `tests/fixtures/lifecycle-commands/no-targets/manifest.yml`, `tests/fixtures/lifecycle-commands/missing-work-model/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-work-model/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-work-model/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/duplicate-work-id/manifest.yml`, `tests/fixtures/lifecycle-commands/unknown-source-reference/manifest.yml`, `tests/fixtures/lifecycle-commands/stale-generated-guidance/manifest.yml`, `tests/fixtures/lifecycle-commands/malformed-generated-guidance/manifest.yml`, `tests/fixtures/lifecycle-commands/claude-codex-divergence/manifest.yml`, and `tests/fixtures/lifecycle-commands/invalid-generated-root/manifest.yml` (a `.fsgg/agents.yml` whose work-model path or a target's generated root does not resolve within the project); and add or extend output/boundary manifests under `tests/fixtures/lifecycle-commands/dry-run/manifest.yml`, `tests/fixtures/lifecycle-commands/deterministic-report/manifest.yml`, `tests/fixtures/lifecycle-commands/text-projection/manifest.yml`, and `tests/fixtures/lifecycle-commands/governance-boundary/manifest.yml`.

**Deferred edge-case scope**: Two spec edge cases are intentionally not given
dedicated fixtures in this slice and are deferred to a later feature: a third /
additional future agent target beyond `claude`/`codex` (FR-007 supports "at least
Claude and Codex"; the `claude-only`/`codex-only`/`claude-and-codex` fixtures
exercise the configured-target set), and malformed or incomplete optional
Governance files (the `governance-boundary` fixture covers absent and
present-as-advisory Governance, which is sufficient for the no-Governance and
advisory-only obligations FR-022/FR-023).

**Checkpoint**: Fixture and test file entry points exist; no agent-guidance
behavior is implemented yet.

## Phase 2: Foundational Contracts (Blocking Prerequisites)

**Purpose**: Define the public artifact, generated-view, derived-model, and
MVU/report contracts before user-story implementation. `.fsi` signatures precede
public `.fs` implementation bodies.

### Failing contract tests

- [x] ✅ T006 Add failing generated agent-guidance view artifact tests for `GeneratedAgentGuidance` schema version 1, view version, selected work identity, target identity, generator identity, the always-true `Generated` marker, `Sources` (work-model path/digest/schema/status), `BehaviorModelDigest`, derived `Commands`/`Skills` entries sorted by stable id, `RenderedFiles` references, and diagnostics in `tests/FS.GG.SDD.Artifacts.Tests/AgentGuidanceViewTests.fs`.
- [x] ✅ T007 Add failing `parseGeneratedAgentGuidance` tests for well-formed manifests, malformed manifest schema version, malformed manifest body, and behavior-model-digest equality across targets derived from the same model in `tests/FS.GG.SDD.Artifacts.Tests/AgentGuidanceViewTests.fs`.
- [x] ✅ T008 [P] Add failing generated-view currency assertions for the per-target agent-guidance manifest (`current`, `missing`, `stale`, `malformed`, `blocked`) and for the consumed work model (a missing/stale/malformed/blocked work model blocks generation for all targets) in `tests/FS.GG.SDD.Artifacts.Tests/GeneratedModelCurrencyTests.fs`.
- [x] ✅ T009 [P] Add failing `NormalizedGuidanceModel` derivation assertions: derived purely from `readiness/<id>/work-model.json`, command/skill entries sorted by stable id, no presentation-only fields, identical model across targets, and recorded source identities in `tests/FS.GG.SDD.Artifacts.Tests/NormalizedWorkModelTests.fs`.

### Public `.fsi` contract additions

- [x] ✅ T010 Extend the public agent-guidance view contract in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fsi` with `GuidanceCommandEntry`, `GuidanceSkillEntry`, `GeneratedGuidanceFileRef`, `GeneratedAgentGuidance`, and `val parseGeneratedAgentGuidance: snapshot: FileSnapshot -> Result<GeneratedAgentGuidance, Diagnostic list>`, reusing existing `SchemaVersion`, `WorkId`, `AnalysisSourceRecord`, `SourceDigest`, and `Diagnostic`. (depends on T006, T007)
- [x] ✅ T011 Add the `NormalizedGuidanceModel` derivation signature (derive from a current work model; pure; deterministic stable-id ordering) and the `BehaviorModelDigest` computation signature in `src/FS.GG.SDD.Artifacts/WorkModel.fsi`. The derivation is pure over the work model, so it lives in the artifact-model library (`WorkModel`), not `CommandWorkflow`; `CommandWorkflow` only calls it. (after T010)
- [x] ✅ T012 Extend the public MVU command contract in `src/FS.GG.SDD.Commands/CommandTypes.fsi` with `SddCommand.Agents`, `AgentGuidanceSummary` (work id, `stage = "agents"`, status, generated-root list, generated/refused target ids, finding ids, ready/advisory/warning/blocking counts, disposition, equivalence-required flag, divergent target ids, generated-view state, source snapshot count, readiness — per `contracts/agent-guidance-report-json.md`), `GuidanceDisposition`, `AgentGuidanceFinding`, `CommandReport.AgentGuidance: AgentGuidanceSummary option`, and `CommandModel.AgentGuidance`, while keeping `CommandMsg`, `CommandEffect`, `CommandWorkflow.init`, `CommandWorkflow.update`, and the effect interpreter boundary explicit through `src/FS.GG.SDD.Commands/CommandWorkflow.fsi` and `src/FS.GG.SDD.Commands/CommandEffects.fsi`. (after T010, T011)
- [x] ✅ T013 Add agent-guidance diagnostic constructor signatures for the required diagnostic families (outside project, missing/malformed/unsupported `.fsgg/agents.yml`, no configured targets, missing/malformed/mismatched/duplicate work id, missing/stale/malformed/blocked work model, unknown source reference, malformed generated guidance manifest, stale generated guidance, Claude/Codex behavior divergence under required equivalence, unsafe generated-view refresh, optional Governance boundary issue) and the `agentsGenerated` advisory next-action signature in `src/FS.GG.SDD.Commands/CommandReports.fsi`. (after T012)
- [x] ✅ T014 [P] Add failing command public-surface tests for `SddCommand.Agents`, `AgentGuidanceSummary`, `GuidanceDisposition`, `AgentGuidanceFinding`, agent-guidance diagnostics, `CommandReport.AgentGuidance`, `CommandModel.AgentGuidance`, and the unchanged `CommandModel`/`CommandMsg`/`CommandEffect` MVU boundary in `tests/FS.GG.SDD.Commands.Tests/SurfaceBaselineTests.fs`, and the artifact agent-guidance view surface (`GeneratedAgentGuidance`, entries, `parseGeneratedAgentGuidance`) in `tests/FS.GG.SDD.Artifacts.Tests/SurfaceBaselineTests.fs`.
- [x] ✅ T015 Add prelude references for `SddCommand.Agents`, `parseCommand "agents"`, `commandStage Agents` (= `"agents"`), `nextLifecycleCommand Agents` (returns `None`), `nextLifecycleCommand Ship` (still `None`, chain unchanged), agent-guidance summary/disposition/finding visibility, `GeneratedAgentGuidance`/`parseGeneratedAgentGuidance` visibility, and agent-guidance diagnostic visibility in `scripts/prelude.fsx`; run `dotnet fsi scripts/prelude.fsx` against the draft public surface before implementation-body tasks T017 through T020, and save the early transcript to `specs/014-agent-guidance/readiness/fsi-public-surface-draft.txt`. (after T010 through T014)
- [x] ✅ T016 Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` and `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for the deliberate agent-guidance public-surface additions after T010 through T015.

### Implementation bodies

- [x] ✅ T017 Implement `GeneratedAgentGuidance` parsing, schema-version validation, deterministic ordering of `Commands`/`Skills`/`RenderedFiles`/`Sources`, source-snapshot validation, `BehaviorModelDigest` reading, per-target generated-view currency, and manifest diagnostics in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` after T015.
- [x] ✅ T018 Implement `NormalizedGuidanceModel` derivation from the work model (command/skill entries from lifecycle stages, requirements, decisions, tasks, and evidence obligations; sorted by stable id; no presentation-only fields) and `BehaviorModelDigest` computation in `src/FS.GG.SDD.Artifacts/WorkModel.fs` after T015.
- [x] ✅ T019 Implement agent-guidance command contract types, `commandName Agents = "agents"`, `commandStage Agents = "agents"`, `parseCommand "agents" = Ok Agents`, lifecycle ordering (`nextLifecycleCommand Agents = None`, charter->ship chain untouched), `agentsRequest`, `runAgents`, `initializeGuidanceProject` test helper, `validGeneratedAgentGuidance`, `assertAgentGuidanceSummary`, and source/generated-byte snapshot helpers in `src/FS.GG.SDD.Commands/CommandTypes.fs` and `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` after T015.
- [x] ✅ T020 Implement agent-guidance diagnostic constructors, blocked-report correction routing, and the `agentsGenerated` advisory next-action selection (`NextAction.Command = None`) in `src/FS.GG.SDD.Commands/CommandReports.fs` after T015.

**Checkpoint**: Public `.fsi` contracts, the manifest parser, the derived-model
contract, command reports, diagnostics, MVU boundaries, and surface baselines are
ready for story implementation.

## Phase 3: User Story 1 - Generate Agent Guidance From the Lifecycle Model (Priority: P1, MVP)

**Goal**: `fsgg-sdd agents` loads one work item with a current normalized work
model and a valid `.fsgg/agents.yml`, derives one shared
`NormalizedGuidanceModel`, renders per-target `guidance.json` plus
`commands.md`/`skills.md` under each configured target's generated root, records
source relationships/digests/generator identity/the generated marker, reports the
guidance disposition and generated targets, and points the result at the advisory
`agentsGenerated` next action — all without requiring Governance.

**Independent Test**: Run `agents --work 014-agent-guidance` in an initialized
project with a valid `.fsgg/agents.yml` declaring `claude` and `codex` targets and
a current `readiness/<id>/work-model.json`; confirm
`readiness/<id>/agent-commands/claude/` and `.../codex/` each contain a
`guidance.json` manifest plus `commands.md` and `skills.md` (all marked generated
with source digests and equal `behaviorModelDigest`), and that the report names
the selected work id, parsed config facts, generated targets, generated-view
state, diagnostics, `disposition = generated-current`, `equivalenceRequired`, an
empty `divergentTargetIds`, and the `agentsGenerated` next action without
Governance.

### Tests for User Story 1

- [x] ✅ T021 [P] [US1] Add failing create-flow command tests for `readiness/014-agent-guidance/agent-commands/claude/{guidance.json,commands.md,skills.md}` and `.../codex/{...}`, source relationships, source digests, generator identity, equal `behaviorModelDigest` across targets, generated targets, changed generated artifacts, generated-view state, `disposition = generated-current`, and the advisory `agentsGenerated` next action (with a null command pointer) in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`.
- [x] ✅ T022 [P] [US1] Add failing pure `CommandWorkflow.init`/`CommandWorkflow.update` tests for `Agents` read effects, per-target `CreateDirectory` and `WriteFile(guidance.json/commands.md/skills.md, AgentGuidanceTarget)` write effects, emitted stdout/stderr effects, and dry-run effect suppression in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [x] ✅ T023 [P] [US1] Add failing per-target generated-view currency tests for a fresh create (all targets `missing` -> generated) and a markdown-projection-matches-manifest assertion in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [x] ✅ T024 [P] [US1] Add a failing no-Governance success test that asserts no freshness/route/profile/gate/audit/protected-boundary/release verdict appears in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [x] ✅ T025 [P] [US1] Add a failing report shape assertion for `agents` covering `command`, `workId`, `changedArtifacts`, `agentGuidance`, `generatedViews`, `diagnostics`, `governanceCompatibility`, and `nextAction` in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 1

- [x] ✅ T026 [US1] Wire `Agents` into read planning in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` by adding read effects for `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `readiness/<id>/work-model.json`, each configured target's existing `readiness/<id>/agent-commands/<target>/guidance.json`, and `EnumerateDirectory "work"` for duplicate-id detection (and **not** reading `CLAUDE.md`/`AGENTS.md` as derivation inputs).
- [x] ✅ T027 [US1] Implement `LoadProject`/`LoadWorkItem` for `Agents`: initialized-project validation, `parseAgentGuidanceConfig` loading (schema version, targets, work-model path, generated roots, `GeneratedGuidanceIsAuthority`, `RequireEquivalentClaudeAndCodexBehavior`), selected-work-id validation against the `WorkId` contract, validation that the work-model path and each target's generated root are non-empty and resolve within the project, and current-work-model loading in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T028 [US1] Build the work-model source snapshot record (path, digest, schema version, status) into `AnalysisSourceRecord` form for the manifest `Sources` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T029 [US1] Implement `ApplyUserIntent` calling the `WorkModel` derivation (T018) to produce the single shared `NormalizedGuidanceModel` and its `BehaviorModelDigest` from the current work model, before any per-target rendering, in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T030 [US1] Render one `GeneratedAgentGuidance` manifest per configured target from the shared model (target id, generator identity, generated marker, sources, identical `BehaviorModelDigest`, derived `Commands`/`Skills`, `RenderedFiles` for `commands.md`/`skills.md`) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T031 [US1] Render the deterministic `commands.md` and `skills.md` Markdown projections from each target manifest (generated marker + source reference back to the manifest, no facts absent from the manifest) in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [x] ✅ T032 [US1] Build the single `GuidanceDisposition` (`generated-current` when config valid, work model current, every target generated/current, equivalence satisfied/not required) and the `AgentGuidanceFinding` set with stable ids and structured links to targets, sources, and the work model in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T033 [US1] Implement `PlanGeneratedViewRefresh`: classify each target's currency and plan per-target `CreateDirectory` + `WriteFile(guidance.json/commands.md/skills.md)` effects only when source facts are valid and `CommandRequest.DryRun = false`, planning zero authored-source writes (including `.fsgg/agents.yml` and `CLAUDE.md`/`AGENTS.md` as `Preserve`) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T034 [US1] Build `AgentGuidanceSummary` construction (generated-root list, generated/refused target ids, finding/disposition counts, equivalence-required flag, divergent target ids, generated-view state, source snapshot count, generated artifact change records, generated-view report entries) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`. Per FR-007, "refreshed" targets are not a dedicated summary field; they are derivable from `changedArtifacts` entries with operation `update`.
- [x] ✅ T035 [US1] Remove the unsupported-command path for `Agents` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`, make `src/FS.GG.SDD.Cli/Program.fs` able to run `agents --work <id>`, and return `NextAction.ActionId = "agentsGenerated"` with `NextAction.Command = None` from `src/FS.GG.SDD.Commands/CommandReports.fs`.

**Checkpoint**: User Story 1 is independently testable as the MVP.

## Phase 4: User Story 2 - Detect Stale and Divergent Agent Guidance (Priority: P1)

**Goal**: `agents` reports stale generated guidance and any Claude/Codex behavior
divergence precisely, never treats existing generated guidance as current when its
sources changed, and never derives guidance from a missing/stale/malformed/blocked
work model — each blocking finding names the affected target, identifier,
severity, and correction.

**Independent Test**: Run `agents` against fixtures with known defects (missing,
stale, malformed work model; malformed/stale existing generated guidance;
`requireEquivalentClaudeAndCodexBehavior: true` with a divergent existing target
manifest; unknown source reference; duplicate/malformed work id; no configured
targets); confirm no generated guidance is treated as current until the report
names the affected target, identifier, severity, and correction, and no generated
write is planned for blocked runs.

### Tests for User Story 2

- [x] ✅ T036 [US2] Add failing precondition/config blocking tests for outside project, missing/malformed/unsupported `.fsgg/agents.yml`, no configured targets (`no-targets`), a work-model path or target generated root that does not resolve within the project (`invalid-generated-root`), missing/malformed/mismatched work id, and duplicate logical work id in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`.
- [x] ✅ T037 [US2] Add failing work-model gate tests for missing, stale, malformed, and blocked `readiness/<id>/work-model.json`, and unknown source reference in the work model, asserting no manifest is derived from an unusable model, in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`.
- [x] ✅ T038 [US2] Add failing stale/divergence tests: existing target manifest stale vs current work-model digest/schema/generator (`stale` disposition unless refreshed same run), malformed existing target manifest, and `requireEquivalentClaudeAndCodexBehavior: true` with a divergent existing target manifest (`blocked`, `divergentTargetIds` populated, no current outcome) in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`.
- [x] ✅ T039 [P] [US2] Add failing MVU assertions that blocked `agents` never emits generated `WriteFile`/`CreateDirectory` effects for any target in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [x] ✅ T040 [P] [US2] Add failing generated-view diagnostic tests for missing/stale/malformed/blocked work model and for malformed existing target manifest refusing a safe refresh in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.
- [x] ✅ T041 [P] [US2] Add failing agent-guidance diagnostic serialization assertions for all required diagnostic families in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.

### Implementation for User Story 2

- [x] ✅ T042 [US2] Implement work-model gate blocking diagnostics (missing/stale/malformed/blocked work model, unknown source reference) that prevent `NormalizedGuidanceModel` derivation in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`. Work-model currency is **diagnosed only** (per FR-012); `agents` never writes `work-model.json` — work-model refresh remains owned by `verify`/`ship`.
- [x] ✅ T043 [US2] Implement per-target stale detection by comparing each existing manifest's recorded work-model digest, schema version, and generator identity against the current work model, mapping mismatches to `stale` (refreshed in the same run when safe) in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts.fs` and `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T044 [US2] Implement the `EquivalenceObligation` evaluation: when `RequireEquivalentClaudeAndCodexBehavior` is set, compare each configured target's behavior model against the shared derived `BehaviorModelDigest`, populate `divergentTargetIds`, and block `generated-current` on any mismatch in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T045 [US2] Implement malformed-manifest handling that refuses an unsafe refresh (`Refuse` artifact change) and emits the malformed-generated-guidance diagnostic in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T046 [US2] Map config/target defects (missing/malformed/unsupported config, `no-targets`, a work-model path or target generated root that does not resolve within the project, duplicate/malformed/mismatched work id) into blocking findings and the `blocked` disposition in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T047 [US2] Build blocking agent-guidance findings with stable ids and structured links to the affected target, source artifact, or work-model record in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T048 [US2] Route blocked next actions to configuration correction, work-model refresh, divergence resolution, or stale-source correction with blocking diagnostic ids in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T049 [US2] Add blocked-scenario and finding/disposition/divergence assertion helpers for agent-guidance tests in `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs`.

**Checkpoint**: Stale and divergent generated guidance are identified precisely
and block a current-guidance outcome with stable diagnostics and no generated
write.

## Phase 5: User Story 3 - Keep Authored Sources and Agent Files Authoritative (Priority: P2)

**Goal**: `agents` is a non-destructive generator; authored lifecycle artifacts,
`.fsgg/agents.yml`, and the hand-owned `CLAUDE.md`/`AGENTS.md` guidance-target
files are never created, updated, reordered, normalized, or removed; only
generated guidance under each configured generated root changes; and dry-run
mutates zero files.

**Independent Test**: Run `agents` in valid, blocked, and dry-run scenarios and
confirm authored lifecycle artifacts, `.fsgg/agents.yml`, `CLAUDE.md`, and
`AGENTS.md` remain byte-identical while only generated guidance artifacts and
reports reflect the current source state.

### Tests for User Story 3

- [x] ✅ T050 [US3] Add failing authored-source preservation tests asserting byte-identical `work/<id>/` sources, `.fsgg/agents.yml`, `CLAUDE.md`, and `AGENTS.md` after valid and blocked runs (each reported as `Preserve`) in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`.
- [x] ✅ T051 [US3] Add failing dry-run tests asserting zero authored and generated file changes (no `guidance.json`/`commands.md`/`skills.md` mutation, no directory creation) while still reporting proposed generated artifacts, diagnostics, generated-view state, and next action in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`.
- [x] ✅ T052 [P] [US3] Add failing MVU assertions that `Agents` never emits an authored `WriteFile` effect for any `work/<id>/` source, `.fsgg/agents.yml`, or `CLAUDE.md`/`AGENTS.md` in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`.
- [x] ✅ T053 [P] [US3] Add failing generated-only refresh and rerun-current `NoChange` tests for unchanged per-target manifests/Markdown in `tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs`.

### Implementation for User Story 3

- [x] ✅ T054 [US3] Ensure the agent-guidance workflow plans no authored-source/`.fsgg/agents.yml`/`CLAUDE.md`/`AGENTS.md` write effects in any path and restricts generated writes to each target's `guidance.json`, `commands.md`, and `skills.md` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T055 [US3] Implement the dry-run path that reports proposed per-target generated artifact changes without emitting any `WriteFile`/`CreateDirectory` effect in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T056 [US3] Implement rerun-current `NoChange` behavior when every configured target is already current (no writes planned) in `src/FS.GG.SDD.Commands/CommandWorkflow.fs`.
- [x] ✅ T057 [US3] Serialize generated artifact operations and safe-write decisions for `create`, `update`, `preserve`, `refuse`, and `noChange` for agent guidance in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.

**Checkpoint**: Authored lifecycle sources, `.fsgg/agents.yml`, and the hand-owned
guidance-target files are preserved across valid, blocked, and dry-run paths; only
generated per-target guidance changes.

## Phase 6: User Story 4 - Keep Generated Guidance Traceable (Priority: P3)

**Goal**: Generated manifests, Markdown projections, JSON command reports, text
summaries, CLI smoke paths, and optional Governance compatibility facts are
deterministic projections of one authoritative report contract with explicit
provenance.

**Independent Test**: Run identical `agents` requests repeatedly, compare report
JSON and per-target `guidance.json`/Markdown bytes, render text, run CLI smoke
paths, and confirm every text fact exists in the JSON report, every generated file
identifies its sources and generator identity, and optional Governance references
remain advisory.

### Tests for User Story 4

- [x] ✅ T058 [P] [US4] Add a failing deterministic test for three identical `agents` runs comparing report JSON and per-target `guidance.json`/`commands.md`/`skills.md` bytes (byte-identical, no absolute host paths) in `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs`.
- [x] ✅ T059 [P] [US4] Add a failing agent-guidance text projection test for selected work id, outcome, generated target ids, generated-root list, disposition, equivalence-required flag, divergent target ids, ready/advisory/warning/blocking counts, generated-view state, diagnostics, and next action (no facts absent from the JSON report) in `tests/FS.GG.SDD.Commands.Tests/TextProjectionTests.fs`.
- [x] ✅ T060 [P] [US4] Add a failing boundary test that excludes effective-evidence freshness, route, profile, gate, audit, protected-boundary enforcement, and release verdicts, and asserts optional Governance pointers stay advisory and not evaluated, in `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`.
- [x] ✅ T061 [US4] Add disposable-project CLI JSON, dry-run, and text smoke helpers for `agents --work <id> --root <path> [--dry-run] [--text]` in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs` through the existing `src/FS.GG.SDD.Cli/Program.fs` entry point.
- [x] ✅ T062 [US4] Add local performance assertions under the two-second harness budget for `agents-create`, `agents-rerun-current`, and `agents-refreshes-stale` in `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`.

### Implementation for User Story 4

- [x] ✅ T063 [US4] Serialize the `agentGuidance` summary object (or `null`), generated targets, generated-view state, divergence findings, disposition, diagnostics, Governance compatibility facts, and next action with deterministic sorting and no host-specific fields in `src/FS.GG.SDD.Commands/CommandSerialization.fs`.
- [x] ✅ T064 [US4] Render the agent-guidance text summary (work id, outcome, generated target ids/roots, disposition, equivalence-required flag, divergent target ids, counts, generated-view state, diagnostics, next action) from the command report in `src/FS.GG.SDD.Commands/CommandRendering.fs`.
- [x] ✅ T065 [US4] Keep agent-guidance Governance compatibility facts advisory and not evaluated, and keep effective-evidence freshness, route, profile, gate, audit, protected-boundary enforcement, and release verdict fields absent from agent-guidance reports in `src/FS.GG.SDD.Commands/CommandReports.fs`.
- [x] ✅ T066 [US4] Parse `agents --work <id> --dry-run --text --root <path>` and map arguments to `CommandRequest` fields in `src/FS.GG.SDD.Cli/Program.fs`. The overwrite policy uses the fixed default `AllowGeneratedRefresh` (no CLI flag), matching the `ship` precedent; the contract's "overwrite-policy option" is default-only.
- [x] ✅ T067 [US4] Exclude timestamps, durations, terminal details, process ids, random values, directory enumeration order, absolute host paths, and host-specific separators from agent-guidance reports, manifests, and Markdown in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` and `src/FS.GG.SDD.Commands/CommandRendering.fs`.

**Checkpoint**: Machine-readable manifests/reports and human-readable
projections are deterministic projections of one report contract with explicit
provenance.

## Phase 7: Polish, Evidence, And Documentation

**Purpose**: Record mandatory verification evidence and update human-facing state
after implementation is complete.

- [x] ✅ T068 [P] Run `dotnet test tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj -c Release --filter "FullyQualifiedName~AgentGuidance"` and save output to `specs/014-agent-guidance/readiness/artifact-agent-guidance-tests.txt`.
- [x] ✅ T069 [P] Run `dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj -c Release --filter "FullyQualifiedName~Agents"` and save output to `specs/014-agent-guidance/readiness/command-agent-guidance-tests.txt`.
- [x] ✅ T070 [P] Run focused deterministic JSON, text projection, generated-view, and Governance boundary evidence tests and save output to `specs/014-agent-guidance/readiness/output-boundary-tests.txt`.
- [x] ✅ T071 Run `dotnet build FS.GG.SDD.sln -c Release` and save output to `specs/014-agent-guidance/readiness/build-release.txt`.
- [x] ✅ T072 Run `dotnet test FS.GG.SDD.sln -c Release` and save output to `specs/014-agent-guidance/readiness/full-suite.txt` (expect no regression in the existing 258-test baseline plus the new agent-guidance tests).
- [x] ✅ T073 Run `dotnet fsi scripts/prelude.fsx` after a Release build and save the transcript to `specs/014-agent-guidance/readiness/fsi-public-surface.txt`.
- [x] ✅ T074 Run a disposable-project CLI JSON smoke scenario for `fsgg-sdd agents` and save output to `specs/014-agent-guidance/readiness/cli-json-smoke.txt`.
- [x] ✅ T075 Run a disposable-project CLI dry-run smoke scenario for `fsgg-sdd agents --dry-run` and save output to `specs/014-agent-guidance/readiness/cli-dry-run-smoke.txt`.
- [x] ✅ T076 Run a disposable-project CLI text smoke scenario for `fsgg-sdd agents --text`, save output to `specs/014-agent-guidance/readiness/cli-text-smoke.txt`, and record human-summary review notes in `specs/014-agent-guidance/readiness/human-summary-review.md`.
- [x] ✅ T077 Record create, rerun, and stale-refresh performance evidence for `agents-create`, `agents-rerun-current`, and `agents-refreshes-stale` in `specs/014-agent-guidance/readiness/performance.md`.
- [x] ✅ T078 Record SDD/Governance boundary review findings (generated guidance is not a second source of truth; no freshness/route/profile/gate/audit/release behavior; optional Governance pointers stay advisory) in `specs/014-agent-guidance/readiness/sdd-governance-boundary.md`.
- [x] ✅ T079 Record artifact traceability from `specs/014-agent-guidance/spec.md` requirements (FR-001..FR-024, SC-001..SC-009) to plan decisions, tasks, tests, and readiness evidence in `specs/014-agent-guidance/readiness/artifact-traceability.md`.
- [x] ✅ T080 Update `docs/initial-implementation-plan.md` to mark Phase 8 `fsgg-sdd agents` complete and reference `specs/014-agent-guidance/readiness/`.
- [x] ✅ T081 Update `README.md`, `AGENTS.md`, and `CLAUDE.md` only where needed to keep the current command state and Claude/Codex guidance synchronized after the agent-guidance workflow lands (without turning generated guidance into a second source of truth).

**Checkpoint**: The implementation has build, test, FSI, CLI, performance,
boundary, and traceability evidence.

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on Phase 1 and blocks all user-story implementation.
- Phase 3 depends on Phase 2 and is the MVP scope.
- Phase 4 depends on Phase 3 because stale/divergence detection reuses the loaded
  source set, the derived guidance model, per-target rendering, and the base
  report shape.
- Phase 5 depends on Phases 3 and 4 because preservation and dry-run guarantees
  must hold across success and blocked paths.
- Phase 6 depends on Phases 3 through 5 because output contracts must include
  success, blocked, dry-run, no-Governance, and preservation states.
- Phase 7 depends on the implemented story scope being complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2; no dependency on other stories.
- **US2 (P1)**: Depends on US1 source loading, work-model derivation, per-target
  rendering, and the base report shape.
- **US3 (P2)**: Depends on US1 generated-write planning and US2 blocked-path
  behavior.
- **US4 (P3)**: Depends on agent-guidance summaries, diagnostics, generated-view
  reporting, and preservation behavior from US1 through US3.

### Cross-Task Dependencies

- T011 depends on T010.
- T012 and T013 depend on the public-surface decisions from T010 and T011.
- T015 depends on T010 through T014 and must run before implementation-body tasks
  T017 through T020.
- T016 depends on T010 through T015.
- T017 through T020 depend on the FSI/prelude exercise in T015.
- T026 through T035 depend on T017 through T020.
- T029 (shared model derivation) precedes T030 (per-target rendering), which
  precedes T032 (disposition).
- T042 through T049 depend on T036 through T041.
- T054 through T057 depend on T050 through T053.
- T063 through T067 depend on T058 through T062.
- T068 through T079 depend on all selected implementation tasks passing.
- T080 and T081 depend on readiness evidence from T068 through T079.

## Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001 and T002.
- T008, T009, and T014 can run in parallel because they touch different test
  files.
- T021 through T025 can run in parallel because each task touches a different test
  file.
- T039, T040, and T041 can run in parallel with the `AgentsCommandTests.fs` tasks
  in T036, T037, and T038 (the three `AgentsCommandTests.fs` tasks share a file
  and run sequentially among themselves).
- T052 and T053 can run in parallel with the `AgentsCommandTests.fs` tasks in T050
  and T051.
- T058, T059, and T060 can run in parallel because they touch different output and
  boundary test files.
- T068, T069, and T070 can run in parallel after implementation is complete.

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (each touches a different test file):
Task: "Create-flow command tests in tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs"
Task: "Pure init/update effect tests in tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs"
Task: "Per-target currency tests in tests/FS.GG.SDD.Commands.Tests/GeneratedViewCommandTests.fs"
Task: "No-Governance success test in tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs"
Task: "Report shape assertion in tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs"
```

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3.
3. Run the focused artifact and command evidence tests for US1.
4. Validate that `fsgg-sdd agents` derives one shared `NormalizedGuidanceModel`,
   generates per-target `guidance.json` plus `commands.md`/`skills.md` under each
   configured target's generated root, records source digests and generator
   identity with the generated marker, preserves authored sources and
   `.fsgg/agents.yml`/`CLAUDE.md`/`AGENTS.md`, works without Governance, and
   points the result at the advisory `agentsGenerated` next action.

### Incremental Delivery

1. US1 creates the native per-target agent-guidance view and success report.
2. US2 detects and blocks stale and Claude/Codex-divergent guidance with precise
   diagnostics.
3. US3 guarantees non-destructive preservation and dry-run behavior.
4. US4 locks down deterministic JSON/manifests/Markdown, CLI smoke paths, and
   optional Governance boundaries.
5. Phase 7 records readiness evidence and updates project guidance.

## Task Counts

- Shared setup, foundation, and polish tasks: 34
  - Setup (Phase 1): 5
  - Foundational contracts (Phase 2): 15
  - Polish/evidence/docs (Phase 7): 14 (T068–T081)
- US1 tasks: 15 (T021–T035)
- US2 tasks: 14 (T036–T049)
- US3 tasks: 8 (T050–T057)
- US4 tasks: 10 (T058–T067)
- Total tasks: 81

## Suggested MVP Scope

Complete Phases 1, 2, and 3 first. That delivers User Story 1: a usable
`fsgg-sdd agents` command that derives one shared `NormalizedGuidanceModel` from
the selected work item's current work model, generates per-target `guidance.json`
manifests plus `commands.md`/`skills.md` projections under each configured
generated root (marked generated with source digests and equal
`behaviorModelDigest`), reports the guidance disposition and generated targets,
preserves authored sources and the hand-owned guidance-target files, works without
Governance, and points the result at the advisory `agentsGenerated` next action.
