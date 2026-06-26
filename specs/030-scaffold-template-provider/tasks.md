---
description: "Task breakdown for Scaffold Runnable Products via Template Providers"
---

# Tasks: Scaffold Runnable Products via Template Providers

**Input**: Design documents from `specs/030-scaffold-template-provider/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tier**: Tier 1 (contracted change) for the whole feature — per-task tier
annotations are omitted because every phase matches the spec tier. Test evidence
is mandatory (constitution VI); the fixture provider drives real fs + process
I/O (no mocks).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: the user story a task serves (US1–US4); unlabeled = shared/foundational.
- Phases run in sequence; tasks within a phase may run in parallel where marked.
- Exact file paths are given; anchors reference `plan.md` §Grounded inventory.

## Status legend

- `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line)

---

## Phase 1: Setup — fixture provider & registry fixtures

**Purpose**: Stand up the real, in-repo `dotnet new` fixture provider that every
semantic test drives. No Rendering specifics may appear here (SC-005).

- [X] T001 [P] Create the happy-path fixture `dotnet new` template at
  `tests/fixtures/scaffold-provider/ok/` — `.template.config/template.json`
  (short name `fsgg-fixture-app`, a `productName` symbol) plus a minimal
  buildable app (`.fsproj` + `Program.fs`) that materializes only into the target.
- [X] T002 [P] Create the failure-mode fixture variants under
  `tests/fixtures/scaffold-provider/`: `empty/` (succeeds, writes nothing),
  `fails-midway/` (writes ≥1 file then exits nonzero), `bad-version/`
  (descriptor declares `contractVersion: "9.0.0"`), and `writes-into-fsgg/`
  (template that writes under `.fsgg/`/`work/`/`readiness/`).
- [X] T003 [P] Add `.fsgg/providers.yml` test fixtures (one valid registry naming
  the `ok` fixture with a required `productName` param; one per failure variant)
  under `tests/fixtures/scaffold-provider/registries/`.

**Checkpoint**: Fixtures exist and `dotnet new install tests/fixtures/scaffold-provider/ok` succeeds locally.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Declare the full public surface in `.fsi` FIRST (constitution I/III),
then implement the shared types/effects every story depends on. No story work may
begin until this phase is complete.

### `.fsi` contract surface (declare before any `.fs`)

- [X] T004 In `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fsi` declare
  `ProviderParameterSpec`, `ProviderDescriptor`, and the `.fsgg/providers.yml`
  registry-parse function signature (data-model §1).
- [X] T005 [P] Create new module `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fsi`
  declaring `ScaffoldProvenanceRecord`, `ScaffoldProducedPath`, and
  `serialize` / `tryParse` signatures (data-model §4; provenance schema contract).
- [X] T006 [P] In `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` declare the ten
  `scaffold.*` diagnostic factory signatures (data-model §Diagnostics catalog).
- [X] T007 In `src/FS.GG.SDD.Commands/CommandTypes.fsi` declare: `SddCommand.Scaffold`;
  `CommandEffect.RunProcess of command * args * workingDir` and its
  `CommandEffectResult` capture; `ScaffoldSummary`; `CommandReport.Scaffold` and
  `CommandModel.Scaffold`; updated `commandName`/`commandStage`/`parseCommand`/
  `nextLifecycleCommand` signatures (anchors: `CommandTypes.fsi:8,335,357`).
  Note: `ScaffoldRequest`/`ScaffoldResult`/`ScaffoldOutcome` are **internal** to
  Commands (derived, in-memory; data-model §2–3) and are **not** declared public —
  provenance persists only the `string` outcome value, so Artifacts gains no
  dependency on Commands.
- [X] T008 [P] Declare scaffold summary writer/renderer signatures as needed in
  `CommandReports.fsi`, `CommandSerialization.fsi`, and `src/FS.GG.SDD.Cli/Rendering.fsi`.

### Foundational implementation

- [X] T009 Implement `ProviderParameterSpec`/`ProviderDescriptor` + `.fsgg/providers.yml`
  parser in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` (schema v1;
  `malformedSchemaVersion`/`unsupportedSchemaVersion` consistent with sibling parsers).
- [X] T010 [P] Implement `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs`: record types,
  `serialize` (deterministic — `Json/JsonWriters.fs` conventions, `producedPaths`
  sorted by path, no clock/abs-path), `tryParse` (malformed → None, fail-safe).
- [X] T011 [P] Implement the ten `scaffold.*` diagnostic factories in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs` with the user-input vs provider-defect
  classification from data-model (exit 1 vs exit 2).
- [X] T012 Extend `src/FS.GG.SDD.Commands/CommandTypes.fs`: add `Scaffold` case +
  `scaffold` name/stage/parse mappings (`:399,415,420`), `RunProcess` effect +
  result capture (`:357`), `ScaffoldSummary`, `CommandReport.Scaffold`,
  `CommandModel.Scaffold`, and `nextLifecycleCommand Scaffold -> None` (`:491`, FR-015).
  Keep `ScaffoldRequest`/`ScaffoldResult`/`ScaffoldOutcome` as `internal` types.
- [X] T013 Implement the `RunProcess` edge interpreter in
  `src/FS.GG.SDD.Commands/CommandEffects.fs:61` via `System.Diagnostics.Process`,
  honoring `DryRun` (plans/reports without spawning), capturing exit code +
  stdout/stderr (excluded from deterministic contract).

### Foundational tests

- [X] T014 [P] Round-trip + determinism tests in `tests/FS.GG.SDD.Artifacts.Tests/`:
  `ProviderDescriptor` parse (valid/malformed/unsupported version) and
  `ScaffoldProvenanceRecord` serialize→tryParse byte-stability + sorted paths +
  malformed→None. Write to FAIL first against T009/T010 stubs, then pass.

**Checkpoint**: Public surface compiles, shared types round-trip, `RunProcess`
runs the fixture `ok` template. Story work can begin.

---

## Phase 3: User Story 1 — One command to a runnable, SDD-managed product (P1) 🎯 MVP

**Goal**: `fsgg-sdd scaffold --provider <name>` establishes the SDD skeleton, invokes
the provider, records what was produced, and reports it across all three projections.

**Independent Test**: quickstart Scenario A — scaffold the `ok` fixture into a temp
dir; assert skeleton present, fixture files present, summary lists them as
`generatedProduct`, `.fsgg/scaffold-provenance.json` exists, exit 0.

### Tests for US1 (write FIRST, ensure they FAIL)

- [X] T015 [P] [US1] Pure transition / emitted-effect test in
  `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`: `plan Scaffold` emits
  `initEffects` verbatim **then** the provider plan (incl. `RunProcess`), with no I/O.
  (Also satisfies the constitution I.3 "exercise the public surface before `.fs`
  hardens" check for this feature.)
- [X] T016 [P] [US1] Fixture-driven happy-path semantic test (real fs + process) in
  `ScaffoldCommandTests.fs`: scaffold `ok` → `ProviderSucceeded`, produced paths
  enumerated, provenance written, skeleton intact (Scenario A).
- [X] T017 [P] [US1] `--dry-run` test in `ScaffoldCommandTests.fs`: scaffold `ok`
  with `--dry-run` plans the provider `RunProcess` and reports the planned command,
  but spawns no child process, writes no files, and writes no provenance
  (contracts/cli-scaffold.md §--dry-run).

### Implementation for US1

- [X] T018 [US1] Add `Scaffold` dispatch to `plan` in
  `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs:275` — reuse
  `Foundation.fs:81 initEffects` unchanged, then the provider plan (FR-004, SC-003).
- [X] T019 [US1] Implement `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs`
  (new): resolve descriptor → validate version/params → snapshot target →
  `RunProcess(dotnet new …)` → before/after diff for produced paths → SDD-tree guard
  → write provenance → build `ScaffoldResult`/`ScaffoldOutcome`. Provenance is
  written whenever the provider actually ran — including `ProviderFailed`, where the
  partial produced paths are recorded (data-model §4; provenance schema `outcome`).
- [X] T020 [US1] In `src/FS.GG.SDD.Commands/CommandReports.fs:1303 buildReport` add the
  `ScaffoldSummary` (truthful `SkeletonCreated`/`ProviderInvoked`) and produced paths
  as `ChangedArtifacts` with `Ownership = "generatedProduct"`, `NextAction = None`,
  charter hint; map `ScaffoldOutcome → CommandOutcome → exit` (data-model table).
- [X] T021 [P] [US1] Serialize the scaffold summary deterministically in
  `src/FS.GG.SDD.Commands/CommandSerialization.fs:354` (sorted paths, canonical keys).
- [X] T022 [P] [US1] Render the scaffold summary text projection in
  `src/FS.GG.SDD.Commands/CommandRendering.fs:7`.
- [X] T023 [P] [US1] Render the scaffold summary rich (Spectre) projection in
  `src/FS.GG.SDD.Cli/Rendering.fs:74` (pure projection; degrades per NO_COLOR/TERM=dumb).
- [X] T024 [US1] Wire CLI dispatch + option parsing in `src/FS.GG.SDD.Cli/Program.fs`
  (`:13-26,109-126`): `--provider`, repeatable `--param k=v` (new collector), `--force`,
  `--root`, `--dry-run`, projection precedence `--rich > --text > --json`.
- [X] T025 [US1] Byte-stable golden for the scaffold report in
  `tests/FS.GG.SDD.Commands.Tests/CommandReportJsonTests.fs` (happy path).

**Checkpoint**: US1 fully functional — one command yields a buildable, SDD-managed
product; report identical across projections. MVP deliverable.

---

## Phase 4: User Story 2 — SDD stays useful with no provider (P1)

**Goal**: `scaffold` with no `--provider` blocks with an actionable error pointing at
`fsgg-sdd init`; `init` remains byte-identical (skeleton-only path untouched).

**Independent Test**: quickstart Scenario B — `scaffold` with no provider → exit 1,
`scaffold.providerMissing`; `init` golden unchanged.

### Tests for US2 (write FIRST, ensure they FAIL)

- [X] T026 [P] [US2] Test in `ScaffoldCommandTests.fs`: `scaffold` without `--provider`
  → `ProviderNotRun`/Blocked, exit 1, `scaffold.providerMissing` pointing to `init`,
  `ProviderInvoked = false`.
- [X] T027 [P] [US2] Reassert the existing `init` skeleton golden is byte-identical
  (SC-003) — confirm no scaffold change touched the init path.

### Implementation for US2

- [X] T028 [US2] In `HandlersScaffold.fs` emit `scaffold.providerMissing` and a
  well-defined Blocked result when `--provider` is absent (no provider machinery,
  no Governance, no monorepo assumption). `init` left untouched (FR-005).

**Checkpoint**: No-provider path is actionable; `init` byte-equivalence holds.

---

## Phase 5: User Story 3 — Actionable diagnostics for missing/incompatible/failing providers (P2)

**Goal**: Every failure mode yields one distinct, actionable diagnostic and a
well-defined result; user-input (exit 1) vs provider-defect (exit 2) are split, and
no incomplete scaffold is shown as complete (FR-008/FR-009).

**Independent Test**: quickstart Scenario C — drive each fixture variant and assert
the diagnostic + exit code in the data-model outcome table.

### Tests for US3 (write FIRST, ensure they FAIL)

- [X] T029 [P] [US3] Fixture-driven tests in `ScaffoldCommandTests.fs` covering each
  case: `providerUnknown` (1), `providerVersionUnsupported` (1, not invoked),
  `providerParamMissing` (1), `targetCollision` no-`--force` (1, per-path),
  `providerEmpty` (0, info), `providerFailed` (2, partial paths listed + provenance
  records the partial paths with `outcome: providerFailed`), `providerUnavailable`
  (2, `dotnet` absent — environment-sensed), `providerWroteSddTree` (2, per-path;
  SDD state unmodified). Assert `skeletonCreated`/`providerInvoked` truthful.
  NOTE: 7 of the 8 modes are exercised by real fixture-driven tests.
  `providerUnavailable` (engine/`dotnet` absent → `RunProcess` `Started = false`) is
  **code-complete but not auto-tested**: the test host always has `dotnet` on PATH, so
  that branch cannot be reached hermetically without globally manipulating PATH (which
  would race the parallel suite). It is environment-sensed and omitted from quickstart
  Scenario C; the branch is covered by code review.
- [X] T030 [P] [US3] Repeat-scaffold test in `ScaffoldCommandTests.fs` (spec Edge:
  "Repeat scaffold"): re-running `scaffold` in an already-scaffolded project (existing
  skeleton + existing `.fsgg/scaffold-provenance.json`) is reported clearly and does
  not silently duplicate or clobber — without `--force` it blocks on
  `scaffold.targetCollision`; the existing skeleton and provenance are not overwritten.

### Implementation for US3

- [X] T031 [US3] In `HandlersScaffold.fs` wire pre-invocation guards in order:
  unknown provider, unsupported `contractVersion` (no invocation), missing required
  param, and non-empty-target collision without `--force` (per-path) — the same
  collision guard that makes repeat-scaffold (T030) safe.
- [X] T032 [US3] In `HandlersScaffold.fs` map post-invocation outcomes: nonzero exit →
  `providerFailed` (partial paths recorded in provenance), zero-with-no-files →
  `providerEmpty`, process/engine absent → `providerUnavailable`, and the
  SDD-tree-intrusion guard → `providerWroteSddTree`.
- [X] T033 [US3] In `CommandReports.fs` finalize the user-input vs provider-defect exit
  split via `exitCodeForReport` (errors→Blocked; provider-defect class→exit 2) and
  ensure each `scaffold.*` id carries its severity/class/correction from the catalog.

**Checkpoint**: All five+ failure modes distinct, actionable, correctly coded;
repeat-scaffold is safe and clearly reported.

---

## Phase 6: User Story 4 — Provenance recorded; refresh excludes provider output (P2)

**Goal**: Provider output is recorded as externally owned; SDD refresh never
regenerates or flags it; malformed provenance fails safe.

**Independent Test**: quickstart Scenario D — read `.fsgg/scaffold-provenance.json`
(names provider, contract version, paths, `generatedProduct`); a refresh run reports
zero provider paths as stale/regenerable.

### Tests for US4 (write FIRST, ensure they FAIL)

- [X] T034 [P] [US4] Test in `ScaffoldCommandTests.fs`/refresh tests: provenance content
  asserts provider name, contract version, produced paths, `generatedProduct` owner
  (for both `providerSucceeded` and `providerFailed` partial-path cases); malformed
  provenance → `scaffold.provenanceMalformed`, treated as absent.
- [X] T035 [P] [US4] Refresh-exclusion test (SC-007): after Scenario A, `refresh` reports
  zero provenance paths as stale/missing and they are absent from the generated-view ledger.

### Implementation for US4

- [X] T036 [US4] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs`
  `computeRefreshPlan` read `.fsgg/scaffold-provenance.json` and add every produced
  path to an externally-owned exclusion set; absent provenance → today's behavior
  (additive, FR-007). Emit `scaffold.provenanceMalformed` on unreadable provenance.

**Checkpoint**: Provenance is the machine-checkable boundary; refresh respects it.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Tier 1 obligations spanning all stories — surface baselines, agent
surfaces, release catalog, the SC-005 guard, parity, and quickstart validation.

- [X] T037 Update the `tests/**/PublicSurface.baseline` snapshots whose surface
  actually changed — Artifacts, Commands, and Cli are expected to shift; update the
  Validation baseline only if its surface moves (it touches no feature source, so it
  likely does not). Tier 1 requires all changed baselines be in sync.
- [X] T038 [P] Output-parity test (Scenario E / SC-006): run scaffold with `--json`,
  `--text`, `--rich`; assert fact-identical, `--rich` redirected == `--text`, and the
  `--json` bytes are unchanged by the rich path (`tests/FS.GG.SDD.Cli.Tests/`).
- [X] T039 [P] SC-005 grep guard test: zero FS.GG.Rendering package ids / template ids /
  paths / docs URLs in generic SDD source and generic-contract tests.
- [X] T040 [P] Update agent surfaces equivalently (SC-008): `CLAUDE.md`, `AGENTS.md`,
  `.claude/skills/fs-gg-sdd-project/SKILL.md`, `.codex/skills/fs-gg-sdd-project/SKILL.md`
  — describe `scaffold` + the provider contract, no behavioral divergence.
- [X] T041 [P] Update `docs/release/`: add `scaffold-provenance.json` (+ `providers.yml`)
  to `release-readiness.json`; document externally-owned posture in `schema-reference.md`;
  add the Governance-handoff note to `compatibility-matrix.md`; add the additive
  migration note (new command + artifacts; `init` unchanged) under `docs/release/migrations/`.
- [X] T042 Run quickstart Scenarios A–E in the SDD suite against the fixture provider
  (real fs + process) and record the run in the quickstart Done-when checklist.
- [X] T043 [P] Document the cross-repo deliverable: note in plan/quickstart that the real
  FS.GG.Rendering adapter + descriptor (`fs-gg-ui`) and **Scenario F (build + run, the
  SC-002 proof)** are owned and verified in the FS.GG.Rendering repo, not the SDD suite
  (FR-014, SC-002) — so SC-002 is explicitly cross-repo-owned, not unverified.

---

## Dependencies & Execution Order

### Phase order

1. **Phase 1 Setup** — no dependencies; fixtures unblock every semantic test.
2. **Phase 2 Foundational** — depends on Phase 1; BLOCKS all stories. `.fsi`
   tasks (T004–T008) precede their `.fs` implementations (T009–T013) per
   constitution I/III. T014 follows T009/T010.
3. **Phase 3 US1 (MVP)** — depends on Phase 2. T015/T016/T017 before T018–T025;
   T018 before T019; T019 before T020; T020 before the projection tasks
   (T021–T023) and the golden (T025); T024 after the report builds.
4. **Phase 4 US2**, **Phase 5 US3**, **Phase 6 US4** — each depends on Phase 2 and
   shares `HandlersScaffold.fs`/`CommandReports.fs` with US1, so they extend the
   US1 handler rather than run fully parallel to it (see note below).
5. **Phase 7 Polish** — after the stories whose surface it baselines/validates.

### Cross-story dependency note

US2–US4 add branches to the **same** `HandlersScaffold.fs` and `CommandReports.fs`
that US1 creates. Sequence them after US1's T019/T020 to avoid same-file conflicts;
their *tests* (T026/T029/T030/T034/T035) are independent and parallel-safe. The
repeat-scaffold test (T030) depends on the collision guard (T031). US4's refresh
change (T036) touches a different file (`HandlersRefresh.fs`) and is fully parallel.

### Parallel opportunities

- **Phase 1**: T001, T002, T003 all `[P]`.
- **Phase 2**: `.fsi` tasks T005/T006/T008 `[P]` (distinct files); impl T010/T011 `[P]`;
  T014 `[P]` once its types exist.
- **Phase 3**: tests T015/T016/T017 `[P]`; projection tasks T021/T022/T023 `[P]` (distinct files).
- **Phase 7**: T038/T039/T040/T041/T043 `[P]` (distinct files/surfaces).

---

## Summary

| Story | Priority | Tasks | Independent test |
|---|---|---|---|
| Foundational + Setup | — | T001–T014 (14) | surface compiles; types round-trip; fixture runs |
| US1 — one command to runnable product | P1 (MVP) | T015–T025 (11) | Scenario A (+ dry-run) |
| US2 — useful with no provider | P1 | T026–T028 (3) | Scenario B |
| US3 — actionable diagnostics | P2 | T029–T033 (5) | Scenario C (+ repeat-scaffold) |
| US4 — provenance & refresh exclusion | P2 | T034–T036 (3) | Scenario D |
| Polish & cross-cutting | — | T037–T043 (7) | Scenarios E + grep + quickstart |

**Total**: 43 tasks.

**Suggested MVP scope**: Phase 1 + Phase 2 + Phase 3 (US1) — delivers the headline
"one command to a runnable, SDD-managed product" and is independently testable via
Scenario A before any P2 work begins.

### Elmish/MVU applicability

This is a stateful, I/O-bearing feature: the `.fsi` contract (T004–T008), pure
transition / emitted-effect tests (T015, which also stands in for the constitution
I.3 FSI/prelude exercise step), and real interpreter evidence over a live fixture
provider (T016, T029) are all explicit. The new `RunProcess` effect keeps process
I/O at the edge interpreter (`CommandEffects.fs`), preserving the pure
`plan`/`update` boundary (constitution V).
