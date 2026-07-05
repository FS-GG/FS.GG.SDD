---

description: "Task list for feature 077 — preserve refs on auto-generated evidence obligations"
---

# Tasks: Preserve refs on auto-generated evidence obligations

**Input**: Design documents from `specs/077-evidence-obligation-refs/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/evidence-scaffolding-refs.md

**Overall tier**: **T1** (command output contract / generated-view + public `.fsi` + new CLI flag).
Per-phase `[T2]` annotations mark tasks that carry no contract change; tier omitted where it
matches T1. Tests are REQUIRED (Constitution Principle VI), so test tasks are included and must
FAIL before their implementation task.

**MVU note (Principle V)**: both stories are **pure** transforms — no new `Model`/`Msg`/`Effect`.
Ref routing (US1) and `--from-tests` source seeding (US2) enter the existing evidence `update`;
`--from-tests` records a *declared* source pointer (no filesystem I/O — path existence is validated
downstream at verify), so the pure evidence core stays I/O-free.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file / independent)
- **[US1]/[US2]**: owning user story

---

## Phase 1: Setup

**Purpose**: Fixtures the stories exercise.

- [-] T001 Not needed — the existing harness `TestSupport.initializeAnalyzedProject` already
  produces the exact graph: T001 `Implement requirement FR-001` (sourceIds `["AC-001","FR-001"]`)
  and T002 `Implement plan decision PD-001` (sourceIds `["AC-001","FR-001","PD-001"]`). Verified by
  running the real CLI lifecycle. No new fixture authored.

**Checkpoint**: fixture available for tests (via existing harness).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: contract/`.fsi` changes both stories build on. **BLOCKS US1 and US2.**

- [X] T002 Added additive field `LinkedSourceIds: string list` to `EvidenceObligation` in
  `Evidence.fsi` (signature first) then `Evidence.fs`.
- [-] T003 No-op — `PublicSurface.baseline` enumerates public functions/values, not record fields,
  so adding a field to `EvidenceObligation` does not change the baseline (full suite green).
- [X] T004 [P] Added the pure ref-routing helper `routeSourceRefs` in
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs` that classifies a `string list` of
  ids by the `Identifiers` validators `createRequirementId` (`FR-`) and `createPlanDecisionId`
  (`PD-`) into the requirement/plan-decision buckets, leaving every other id unrouted (scope
  narrowed after review — see research Decision 3). Sort + `List.distinct` each bucket.

**Checkpoint**: obligation carries lineage; routing helper exists. Stories can begin.

---

## Phase 3: User Story 1 — Auto-generated obligations carry originating refs (P1) 🎯 MVP

**Goal**: scaffolded obligations expose their originating requirement/plan-decision/clarification
refs from the `evidence.yml` entry alone; no join back to `tasks.yml`.

**Independent Test**: run `fsgg-sdd evidence` on the T001 fixture; assert the plan-decision
obligation shows `planDecisionRefs: [PD-###]` (+ recovered `requirementRefs: [FR-###]`), the
requirement obligation still shows its `FR-###`, and the `DEC-###` routes to
`clarificationDecisionRefs` — all deterministic across re-runs.

### Tests for User Story 1 (write FIRST, ensure they FAIL)

- [X] T005/T006 [US1] Behavioral tests through the **public** evidence command in
  `EvidenceCommandTests.fs` (routing helper is internal, so tested end-to-end per the
  vertical-slice rule): plan-decision obligation preserves `PD-001` and recovers `FR-001`;
  requirement obligation unchanged (no regression); PD never misrouted into clarification refs.
  Note the seeded `evidence.yml` is deleted first (`scaffoldFreshEvidence`) to exercise scaffolding
  rather than the no-clobber path.
- [X] T007 [US1] Determinism test (SC-005): two fresh scaffolds route identical
  (taskId, requirement, acceptance, plan-decision) refs. Plus an FR-006 no-clobber test: a re-run
  does not inject `PD-001` into the authored T002 declaration.

### Implementation for User Story 1

- [X] T008 [US1] `evidenceObligations` now populates `LinkedSourceIds = task.SourceIds`.
- [X] T009 [US1] `skeletonEvidenceDeclaration` routes the union of `LinkedSourceIds` +
  `LinkedRequirementIds` + `LinkedDecisionIds` via `routeSourceRefs`, populating `RequirementRefs`
  and `PlanDecisionRefs`; the acceptance/clarification/checklist buckets stay `[]` on scaffolds
  (scope narrowed after review). No-clobber preserved (scaffolding runs only in the
  no-existing/no-input merge branch).
- [-] T010 [US1] No golden fixture to update — no committed golden asserts scaffolded `evidence.yml`
  refs (full suite green). Canonical `docs/examples` refresh handled in T017.

**Checkpoint**: US1 fully functional, exercised end-to-end via CLI + public-command tests — **shippable MVP**. ✅

---

## Phase 4: User Story 2 — `evidence --from-tests <path>` (P2)

**Goal**: pre-map each newly scaffolded obligation to a proving test file; additive and inert when
absent.

**Independent Test**: `fsgg-sdd evidence --from-tests <path>` seeds a verification-kind source on
each new obligation; without the flag, no source is seeded (inert); a blank value is inert.

### Tests for User Story 2

- [X] T011/T012 [US2] Tests in `EvidenceCommandTests.fs`: `--from-tests <path>` seeds a
  verification source on every scaffolded obligation (US2.1); absent ⇒ no source (US2.2 / SC-004);
  blank value ⇒ inert (US2.3 / FR-009); plus a **real CLI** smoke (`runEvidenceCli … --from-tests
  <path>`) asserting the path threads through parse → handler → scaffolded `evidence.yml`.

### Implementation for User Story 2

- [X] T013 [US2] Added additive `FromTests: string option` to `CommandRequest` (`CommandTypes.fsi`
  then `.fs`); updated all 7 construction sites. Baselines unchanged (they enumerate
  functions/values, not record fields — full suite green).
- [X] T014 [US2] Parse `--from-tests <path>` via `optionValue "--from-tests" rest` in `Program.fs`.
- [X] T015 [US2] `skeletonEvidenceDeclaration` seeds one verification-kind `EvidenceSourceReference`
  (`fromTestsSourceRefs`) on each newly scaffolded skeleton when `FromTests = Some path`; blank ⇒
  inert; `None` ⇒ no change (FR-008). No-clobber preserved (scaffold-only branch). Path existence
  is a **verify-stage** concern (evidence declares, verify validates) — FR-009 reframed; no I/O
  added to the pure evidence core.
- [X] T016 [US2] Documented `--from-tests <path>` in the `evidence` help block in `CommandHelp.fs`.

**Checkpoint**: US1 + US2 both work independently. ✅

---

## Phase 5: Polish & Cross-Cutting

- [X] T017 [P] Enriched `docs/examples/lifecycle-artifacts/evidence.yml` — EV001/EV002 now show
  their origin refs and a header note on ref-carrying (validated live by `ExampleArtifactsContractTests`).
- [X] T018 [P] Updated the `fs-gg-sdd-evidence` skill (authored `.claude` source + byte-identical
  `.codex` mirror) with an "origin refs" note and `--from-tests`; regenerated the process
  `skill-manifest.json` sha256 (`registry skill-manifest --write`). Skill-mirror/manifest drift
  guards green. (No `.agents/skills/fs-gg-sdd-evidence` copy exists in-repo — seeded into consumers.)
- [X] T019 `dotnet build` + full `dotnet test` green (963 pass); quickstart scenarios A–E all
  exercised via real CLI runs + tests, incl. `--json`/`--text`/`--rich` parity (unchanged).
- [X] T020 Ran speckit-analyze — 0 critical/high findings, 100% requirement coverage, no
  constitution violations. Feature implementation-complete.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: no deps — start immediately.
- **Phase 2 (Foundational)**: after Setup — **blocks US1 and US2**. T002→T003; T004 [P] alongside.
- **Phase 3 (US1)**: after Phase 2. Tests T005–T007 FAIL first, then T008→T009→T010. T008 depends
  on T002; T009 depends on T004 + T008.
- **Phase 4 (US2)**: after Phase 2; independent of US1 but shares `HandlersEvidence.fs` (T015 vs
  T009 touch the same module — sequence US2 after US1 to avoid a merge conflict, or coordinate).
  T013→T014→T015; T016 [P].
- **Phase 5 (Polish)**: after the stories it documents/validates.

### Within each story

- Tests written and FAILING before implementation.
- `.fsi` + baseline before `.fs` body (Principle I/III).
- Pure routing/model changes before the handler wiring; core before docs.

## Parallel Opportunities

- T001 (setup) is standalone.
- T004 runs [P] with T002/T003 in Phase 2.
- US1 tests T005/T006/T007 run [P] together (all in one test file — coordinate edits).
- US2 tests T011/T012 run [P]; T016 [P] with US2 impl.
- Polish T017/T018 run [P].

## Implementation Strategy

**MVP = User Story 1** (Phases 1→2→3): the issue's hard acceptance. Stop and validate the
`evidence.yml` scaffolding after Phase 3 — that alone closes issue #124's acceptance box.
US2 (`--from-tests`) is an incremental add. Bulk-authoring is out of scope (epic #127 / #126).

## Task count

- Setup: 1 · Foundational: 3 · US1 (P1, MVP): 6 · US2 (P2): 6 · Polish: 4 — **20 tasks total**.
