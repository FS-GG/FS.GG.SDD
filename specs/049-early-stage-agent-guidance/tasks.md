---

description: "Task breakdown for Early-Stage Agent Guidance Bootstrap"
---

# Tasks: Early-Stage Agent Guidance Bootstrap

**Input**: Design documents from `/specs/049-early-stage-agent-guidance/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included. Constitution VI (Test Evidence Mandatory) and the plan both
require failing-first tests over real fixture trees for every behavior here, so
test tasks are first-class, not optional.

**Tier**: Tier 1 (contracted) per plan — one authored skeleton artifact, two
reclassified command branches, two advisory diagnostics + one `NextAction`,
agent-surface and release-schema updates.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase.
- **[Story]**: `US1`/`US2`/`US3` (cross-cutting tasks omit it).
- Phases run in sequence; tasks within a phase may run in parallel where marked.

## Elmish/MVU applicability

No new I/O edge (Constitution V, PASS in plan). The static seed reuses `init`'s
existing `WriteFile` effect; the `agents`/`refresh` changes are **pure transitions
over snapshots already loaded upstream**. So there is no new `.fsi` MVU contract to
introduce — the only public-surface deltas are the report-diagnostic constructors +
`NextAction` ActionId in `CommandReports.fsi` (T004). Emitted-effect coverage is the
existing `init` `WriteFile` assertion extended in T006; pure-transition coverage is
T010/T012/T018 over the reclassified T013b/T014 handlers.

---

## Phase 1: Setup & Shared Report Surface (Blocking Prerequisites)

**Purpose**: green baseline + the shared `CommandReport` contract that the
generated channel (US2) depends on. Per Constitution I (Spec → FSI → Semantic Tests
→ Implementation), the `.fsi` declaration lands before any test that consumes it.

- [X] T001 Confirm a clean baseline: `dotnet build FS.GG.SDD.sln` and
  `dotnet test FS.GG.SDD.sln` both green before any change (records the pre-feature
  state the SC-006 regression guard in T012/T014 compares against).
- [X] T002 [P] Capture the SC-006 pre-feature golden: with a buildable
  `readiness/<id>/work-model.json` fixture, run `fsgg-sdd agents --json` and save the
  generated `agent-commands/<target>/{guidance.json,commands.md,skills.md}` output as
  the regression baseline referenced by T014 (note the fixture id/path in the task
  body of T014).
- [X] T004 Declare the new report surface in
  `src/FS.GG.SDD.Commands/CommandReports.fsi`: the `agentsEarlyStageGuidance` and
  `refreshEarlyStageGuidance` advisory diagnostic constructors and the
  `earlyStageGuidance` `NextAction` ActionId. Signatures only — no body yet. Keep
  `serializeReport`/`renderText`/`resolve` signatures unchanged (plan, Constitution
  III). BLOCKS US2 test/impl tasks (T010–T014).

**Checkpoint**: solution builds with the new `.fsi` surface stubbed; US1 and US2 can
proceed (US1 is independent of T004).

---

## Phase 2: User Story 1 — Authoring help exists from an empty/early work item (Priority: P1) 🎯 MVP

**Goal**: `fsgg-sdd init` seeds one generic, deterministic, no-clobber
`.fsgg/early-stage-guidance.md` covering `charter`/`specify`/`clarify`/`checklist`
commands, required headings, stable-id formats, and the §1.1/§1.2 authoring
contracts — so authoring help exists from stage zero with zero decompilation.

**Independent Test**: from a freshly `init`-ed skeleton with no work model, the file
is present and complete; following its `charter` section yields a `charter` artifact
that passes the next lifecycle command (quickstart Scenario A).

### Tests for User Story 1 (write first; must FAIL before T007)

- [X] T005 [P] [US1] In `tests/FS.GG.SDD.Commands.Tests/InitCommandTests.fs`, add a
  failing test that `fsgg-sdd init` over a clean dir writes
  `.fsgg/early-stage-guidance.md` and that its content names, for each of `charter`,
  `specify`, `clarify`, `checklist`, the `fsgg-sdd` command, the required section
  headings, and the stable-id formats, **and** states the §1.1 coverage-line rule and
  the §1.2 `evidence.yml` rule (FR-001–003, SC-001, SC-005).
- [X] T006 [P] [US1] In `InitCommandTests.fs`, add a failing determinism test: two
  `init` runs over clean dirs produce **byte-identical** `.fsgg/early-stage-guidance.md`
  (SC-004 / FR-007), following the generate-twice convention. Also assert the new
  `WriteFile` effect is emitted by `initEffects` with `AgentGuidanceTarget` — the same
  `initEffects` that `scaffold` reuses, so the seed reaches the recommended
  `lifecycle=sdd` path and not only `spec-kit` (FR-009 path clause).

### Implementation for User Story 1

- [X] T007 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`, add the
  embedded `earlyStageGuidanceText` string literal and a `.fsgg/early-stage-guidance.md`
  path constant. Content per `contracts/early-stage-guidance-file.md` and the
  `data-model.md` heading/id table; **no** date/timestamp/random/repo/provider token
  (mirror `constitutionText`, `Foundation.fs:81-85`). Mirror exact live headings/ids so
  the T015 drift-guard passes.
- [X] T008 [US1] In `Foundation.fs` `initEffects`, add one
  `WriteFile(".fsgg/early-stage-guidance.md", earlyStageGuidanceText, AgentGuidanceTarget)`
  alongside `.fsgg/constitution.md` (reuses `canOverwrite` no-clobber,
  `CommandEffects.fs:42-48`). Run T005/T006 to green.

**Checkpoint**: MVP — early-stage authoring guidance exists from stage zero,
deterministic, and `init` stays byte-identical run-to-run. STOP and validate
quickstart Scenario A.

---

## Phase 3: User Story 2 — `agents` / `refresh` stop being an early-stage dead end (Priority: P2)

**Goal**: when `readiness/<id>/work-model.json` is absent, `agents` and `refresh`
emit a non-blocking (exit 0) advisory early-stage result — best-effort facts from
artifacts that actually exist + a `NextAction` to `.fsgg/early-stage-guidance.md` —
instead of `agents.missingWorkModel` / `refresh.blockedUpstreamView`. Genuine
malformed/stale states still block (FR-008).

**Independent Test**: run `agents` and `refresh` against an early-only fixture; both
exit 0 with an early-stage-labeled advisory + pointer and **no** digest-stamped view
written; a malformed work model still blocks (quickstart Scenario B).

**Depends on**: T004 (report surface). Independent of US1 impl, but the
`NextAction` it routes to is most useful once US1's file exists.

### Tests for User Story 2 (write first; must FAIL before T013/T014)

- [X] T010 [P] [US2] Rewrite the missing-work-model case in
  `tests/FS.GG.SDD.Commands.Tests/AgentsCommandTests.fs`: over an early-only fixture
  (charter only, no `work-model.json`), assert outcome is not `Blocked`, exit `0`, an
  advisory `agents.earlyStageGuidance` diagnostic, a `NextAction` to
  `.fsgg/early-stage-guidance.md`, best-effort facts naming which early artifacts
  exist + the next lifecycle command, and that **no** `readiness/*/agent-commands/**`
  file is written (FR-004/006/008/011, SC-002).
- [X] T011 [P] [US2] In `AgentsCommandTests.fs`, add/keep the negative-control test: a
  **malformed** `work-model.json` still yields `Blocked`, exit `1`,
  `agents.malformedWorkModel` (Observability VIII, FR-008) — proves only the *missing*
  case is reclassified.
- [X] T012 [P] [US2] Rewrite the early all-blocked path in
  `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`: over the early-only fixture,
  assert `refresh` reports a navigable advisory `refresh.earlyStageGuidance` (not only
  `refresh.blockedUpstreamView`), exit `0`, the early-stage label, and the pointer
  `NextAction`; assert no view is regenerated/written (FR-005/006/011).

### Implementation for User Story 2

- [X] T013 [US2] Implement the constructors declared in T004 in
  `src/FS.GG.SDD.Commands/CommandReports.fs`: `agentsEarlyStageGuidance` /
  `refreshEarlyStageGuidance` (advisory severity, remedy text pointing to
  `.fsgg/early-stage-guidance.md`) and the `earlyStageGuidance` `NextAction`.
- [X] T013b [US2] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAgents.fs`
  (missing-work-model branch, ~`:211-212`), reclassify the **absent** work model from
  the blocking `agents.missingWorkModel` to the advisory early-stage result: gather
  which of charter/spec/clarifications/checklist exist + the next lifecycle command
  from the already-loaded snapshot, emit `agentsEarlyStageGuidance` + the pointer
  `NextAction`, write **no** view, exit 0. Leave malformed/stale/blocked arms
  untouched. Run T010/T011 to green.
- [X] T014 [US2] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs`,
  reclassify the missing-rooted early all-blocked path (~`:126-148`) and the
  `"missing"` downstream arm (~`:322-330`) to the navigable `refreshEarlyStageGuidance`
  advisory + pointer, exit 0; leave genuinely-blocked/`stale` arms unchanged. Verify
  the SC-006 regression: with the T002 buildable-work-model fixture, `agents`/`refresh`
  output stays **byte-identical** to the captured baseline (early-stage branch not
  taken). Run T012 to green.
- [X] T016 [US2] Update `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` for
  the `FS.GG.SDD.Commands` new constructors/ActionId, and add a CLI-level test in
  `tests/FS.GG.SDD.Cli.Tests/` that the early-stage report projects correctly through
  default/`--json`, `--text`, and `--rich` (rich adds/drops no facts, degrades to zero
  ANSI when non-interactive).

**Checkpoint**: US1 + US2 both work independently — early-stage guidance exists and
both entry points route to it instead of dead-ending. Validate quickstart Scenario B.

---

## Phase 4: User Story 3 — Early guidance is deterministic and self-consistent (Priority: P3)

**Goal**: the early-stage guidance has zero dangling references, is byte-identical
run-to-run (seed and report), and no-clobbers author edits — hardening US1/US2 so
the original "dangling skill reference" failure mode cannot recur.

**Independent Test**: produce the guidance twice under identical inputs → byte-
identical; scan every command/path/heading/id it names → all resolve; an author-edited
copy survives re-`init` (quickstart Scenario C).

### Tests for User Story 3

- [X] T015 [P] [US3] NEW drift-guard
  `tests/FS.GG.SDD.Commands.Tests/EarlyStageGuidanceContractTests.fs`, modeled on
  `AuthoringDocsContractTests.fs`, asserting against **live** sources (not copies) per
  `contracts/guidance-drift-guard.md`: every `fsgg-sdd <stage>` is a real
  `Identifiers.allStages` stage with ordering matching `nextLifecycleCommand`
  (`CommandTypes.fs:541-556`); each stage's heading list **equals** its live
  standard-section list (Charter `ParsingEarly.fs:288-313`,
  `Specification.specificationStandardSections`,
  `Clarification.clarificationStandardSections`,
  `Checklist.checklistStandardSections`); every id prefix
  (`FR`/`US`/`AC`/`SB`/`AMB`/`CQ`/`DEC`/`CHK`/`CR`) is a real `Identifiers` scoped-id
  prefix with the correct `^PREFIX-\d{3,}$` shape; every path resolves
  (`.fsgg/early-stage-guidance.md`, `docs/reference/authoring-contracts.md`, the
  `readiness/<id>/agent-commands/<target>/` location); the §1.1/§1.2 rules match
  `docs/reference/authoring-contracts.md` (SC-003 / FR-007).
- [X] T017 [P] [US3] In `InitCommandTests.fs`, add the no-clobber test (US3 AC3): an
  author-edited `.fsgg/early-stage-guidance.md` is **refused** on re-`init` — bytes
  preserved, `unsafeOverwrite` surfaced — while a byte-identical file is an idempotent
  `NoChange`/`preserveExisting` no-op.
- [X] T018 [P] [US3] In `AgentsCommandTests.fs`/`RefreshCommandTests.fs`, add the
  report-determinism test: two `agents` (and two `refresh`) runs over the early-only
  fixture produce **byte-identical** reports (SC-004), via the generate-twice
  convention (`AgentsCommandTests.fs:186-197`).

**Checkpoint**: all three stories independently functional; guidance is trustworthy,
deterministic, and self-consistent. Validate quickstart Scenario C.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T019 [P] Update `CLAUDE.md` and `AGENTS.md` in lockstep (Constitution VII): note
  `.fsgg/early-stage-guidance.md` as an authored skeleton seed (like
  `.fsgg/constitution.md`) and the navigable early-stage `agents`/`refresh` behavior.
- [X] T020 [P] Update `docs/release/schema-reference.md`: record the new
  `agents.earlyStageGuidance` / `refresh.earlyStageGuidance` advisory dispositions and
  state that `.fsgg/early-stage-guidance.md` is an authored skeleton seed (no
  release-catalog `catalog[]` entry, mirroring `.fsgg/constitution.md`).
- [X] T021 [P] (Optional) Cross-link the early-stage guidance from
  `docs/reference/authoring-contracts.md` for discoverability (no contract change).
- [X] T022 Run the full quickstart (`specs/049-early-stage-agent-guidance/quickstart.md`
  Scenarios A–D) end to end and record evidence; confirm SC-001..SC-006 all hold.
- [X] T023 Final gate: `dotnet build FS.GG.SDD.sln` + `dotnet test FS.GG.SDD.sln` green;
  run `fsgg-sdd validate` to confirm no broad-matrix/determinism regression.

---

## Dependencies & Execution Order

### Phase order

- **Phase 1 (Setup & Report Surface)** → **Phase 2 (US1)** and **Phase 3 (US2)** →
  **Phase 4 (US3)** → **Phase 5 (Polish)**. Phases are sequential.
- T004 (report `.fsi`) blocks all US2 test/impl tasks (T010–T014, T016).
- US1 (Phase 2) is independent of T004 and can run as soon as Phase 1's baseline
  (T001) is green.

### Within stories

- Tests before implementation (T005/T006 → T007/T008; T010–T012 → T013/T013b/T014).
- T013 (constructors) before T013b/T014 (handlers that emit them).
- T002 (baseline capture) before T014's SC-006 byte-identical assertion.
- T007 (the literal) before T015 (drift-guard over its content) and before T017
  (no-clobber of its bytes).

### Parallel opportunities

- T005/T006 [P] together (same file, distinct tests — coordinate edits).
- T010/T011/T012 [P] across `AgentsCommandTests.fs` / `RefreshCommandTests.fs`.
- T015/T017/T018 [P] (drift-guard is a new file; the others append to existing tests).
- T019/T020/T021 [P] (independent docs/surfaces).

## Story summary

- **US1 (P1, MVP)**: 4 tasks (T005, T006, T007, T008) — static seeded guidance.
- **US2 (P2)**: 7 tasks (T010, T011, T012, T013, T013b, T014, T016) — navigable
  early-stage `agents`/`refresh`.
- **US3 (P3)**: 3 tasks (T015, T017, T018) — determinism, drift-guard, no-clobber.
- **Shared/Polish**: T001, T002, T004 (setup/surface) + T019–T023 (cross-cutting).

**Suggested MVP scope**: Phase 1 + User Story 1 — early-stage authoring guidance
exists from stage zero, deterministic and no-clobber, removing the chicken-and-egg
dead end on the static channel even before the `agents`/`refresh` reclassification.

## Notes

- Never mark a failing task `[X]`; never weaken an assertion to green a build —
  narrow scope and document it on the task line.
- `[-]` = skipped with written rationale (e.g. T021 if cross-link is declined).
- Real fixture-tree evidence is required (Constitution VI); disclose any synthetic
  evidence per Principle V when marking `[X]`.
