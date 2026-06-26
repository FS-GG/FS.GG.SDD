# Tasks: Split CommandWorkflow into facade + internal modules

**Input**: Design documents from `/specs/025-split-command-workflow/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Change Tier**: **Tier 2 (internal change)** — reorganization only. Public `.fsi`
and every command's deterministic JSON stay byte-identical; the existing 438-test
suite is the behavioral guard. No new behavioral tests are required (Principle VI:
behavior-preserving). An optional structural assertion on file size/layout is the
only candidate new test.

**Elmish/MVU applicability**: The facade preserves the `init`/`update` MVU boundary
verbatim (Principle V); `Model`/`Msg`/`Effect` live in `CommandTypes` and are not
touched. No new transitions or effects are introduced, so no new MVU contract or
emitted-effect test is in scope — equivalence is proven by the existing suite.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file).
- **[Story]**: US1 (navigability), US2 (contract/output byte-stability), US3 (build/layering).
- All line ranges below are against `CommandWorkflow.fs` on `main` (6,814 lines)
  per `data-model.md`; exact cut points settle at implementation under the
  ≤1,500-line cap (FR-004).

## Path Conventions

- Single F# project. Refactor is confined to `src/FS.GG.SDD.Commands/`.
- New folder: `src/FS.GG.SDD.Commands/CommandWorkflow/` (child namespace
  `FS.GG.SDD.Commands.Internal`).

---

## Phase 1: Setup (Baseline capture)

**Purpose**: Record the byte-stable baselines that every later gate diffs against.

> **Baseline must be immutable, not branch-relative.** Work proceeds on feature
> branch `025-split-command-workflow`; its merge base on `main` (`git merge-base
> main HEAD`, the pre-refactor commit) is the fixed reference. Do **not** diff
> against the bare ref `main` — once this branch is rebased or `main` advances,
> `main` is a moving target and the byte-diff gates become unverifiable. Capture
> the pre-refactor commit once as `BASE=$(git merge-base main HEAD)` (record the
> SHA) and diff every later gate against that recorded `$BASE`.

- [X] T001 Capture the immutable pre-refactor baseline at the merge base
  `BASE=$(git merge-base main HEAD)` (record the SHA): the `CommandWorkflow.fsi`
  blob hash (`git rev-parse "$BASE:src/FS.GG.SDD.Commands/CommandWorkflow.fsi"`,
  or `git show "$BASE:…/CommandWorkflow.fsi" | sha256sum`) for the FR-002
  zero-byte-diff gate, plus a saved pre-refactor `--json` output snapshot of a
  representative command (for the T024 byte-equivalence spot-check) so both gates
  hold regardless of subsequent branch/`main` movement.
- [X] T002 [P] Capture the FS3261 baseline: build `dotnet build -c Release
  --no-incremental` on the current tree and record the unique-site count
  (`grep -oE '[^ ]+\.fs\([0-9]+,[0-9]+\): warning FS3261' | sort -u | wc -l`,
  ~290 in `src`) for the FR-007 no-increase gate.
- [X] T003 [P] Capture the green-suite baseline: run `dotnet test` and record the
  passing count (438) and that `git status --porcelain` is empty, establishing the
  FR-003/FR-009 reference (no fixtures regenerated).

**Checkpoint**: `.fsi` hash, FS3261 site count, and 438-passing baseline recorded.

---

## Phase 2: Foundational (Child-namespace scaffold) — BLOCKS all extraction

**Purpose**: Establish the `FS.GG.SDD.Commands.Internal` child namespace and the
`.fsproj` compile-list shape so each extracted file slots in front of the facade
in dependency order. No bindings move yet.

**⚠️ CRITICAL**: F# is compile-order sensitive. The `.fsproj` ordering and namespace
scaffold must be in place before module bodies move, or the build cannot stay green.

- [X] T004 Create the folder `src/FS.GG.SDD.Commands/CommandWorkflow/` and confirm
  the layout/compile-order contract in `contracts/module-layout.md` matches the
  13-file + facade plan in `data-model.md` (Foundation → Parsing* → ViewGeneration
  → Prerequisites → Handlers* → facade).
- [X] T005 Rewrite the `<Compile Include>` list in
  `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj`: insert the 13 internal files
  (in dependency order T007–T019) immediately before `CommandWorkflow.fs`, keeping
  `CommandWorkflow.fsi` paired with the facade `CommandWorkflow.fs` and leaving the
  sibling `CommandEffects`/`CommandSerialization`/`CommandRendering` entries and the
  single `FS.GG.SDD.Artifacts` `ProjectReference` unchanged (FR-008).
- [X] T006 Establish the per-file header convention for the child namespace:
  `namespace FS.GG.SDD.Commands.Internal`, then `[<AutoOpen>] module internal <Name>`,
  plus the file-local `module X = FS.GG.SDD.Artifacts.Y` artifact-alias redeclarations
  each file needs (Decision 3 / research.md). Record which of the seven aliases each
  module requires so moved bodies stay byte-stable with zero call-site rewrites.

**Checkpoint**: Namespace + compile-order scaffold ready; extraction can begin.

---

## Phase 3: User Story 1 — Navigate the command workflow by concern (Priority: P1) 🎯 MVP

**Goal**: Replace the flat 6,814-line module with a thin facade over 13 cohesive,
concern-named internal files (none > ~1,500 lines), so any `compute*Plan` handler,
its prerequisites, and its view rendering are locatable by file name (FR-001,
FR-004, FR-005; SC-001, SC-005).

**Independent Test**: `wc -l src/FS.GG.SDD.Commands/CommandWorkflow/*.fs
src/FS.GG.SDD.Commands/CommandWorkflow.fs` shows the monolith gone, the facade
~150 lines, and no file over ~1,500 lines; each file is named for one concern and
the suite still passes.

> Each extraction below moves the named bindings **verbatim** (no reordering within
> a section that changes evaluation; I-2). Extract in dependency order — each file
> compiles green against the ones above it before moving to the next.

### Implementation for User Story 1

- [X] T007 [US1] Extract **Foundation** → `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`
  (source ~28–419): `normalizeRoot`, `initEffects`, the eleven `*ReadEffects`,
  `plan`, `effectKey`, `snapshot`, `appendNewEffects`, `splitFrontMatter`,
  `CharterFrontMatter`, plus paths/config text and base types/YAML helpers.
- [X] T008 [US1] Extract **ParsingEarly** → `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingEarly.fs`
  (source ~420–1439): Charter + Specification + Clarification parse/template/diagnostics
  (`parseCharterFrontMatter`, `charterTemplate`, `parseSpecificationForCommand`,
  `specification…Facts`, `clarificationTemplate`, `clarification…Facts`). After T007.
- [X] T009 [US1] Extract **ParsingMid** → `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingMid.fs`
  (source ~1440–2438): Checklist + Plan parse/template/diagnostics
  (`plannedChecklistReviews`, `checklistTemplate`, `checklist…Facts`,
  `plannedPlanEntries`, `planTemplate`, `plan…Facts`). After T008.
- [X] T010 [US1] Extract **ParsingTasks** → `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingTasks.fs`
  (source ~2439–3130): Tasks parse/template/diagnostics + evidence parse helper
  (`tasksSummary`, `plannedTasks`, `taskValidationDiagnostics`,
  `parseEvidenceForCommand`, `tasks…Facts`). After T009.
- [X] T011 [US1] Extract **ViewGeneration** → `src/FS.GG.SDD.Commands/CommandWorkflow/ViewGeneration.fs`
  (source ~3131–3712): `analysisSources`, `analysisJson`, `generatedViewState`,
  `generatedViewPlan`, `workModelSnapshots`, `charterWriteEffects`. After T010.
- [X] T012 [US1] Extract **Prerequisites** → `src/FS.GG.SDD.Commands/CommandWorkflow/Prerequisites.fs`
  (source ~3713–3805): `PrerequisiteResolution`, `resolvePrerequisites`, `runHandler`
  shell (the R1-landed cascade). After T011.
- [X] T013 [US1] Extract **HandlersEarly** → `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEarly.fs`
  (source ~3806–4041): `computeCharterPlan`, `computeSpecifyPlan`, `computeClarifyPlan`,
  `computeChecklistPlan`, `computePlanPlan`, `computeTasksPlan`,
  `workModelJsonFromGeneratedEffects`. After T012.
- [X] T014 [US1] Extract **HandlersAnalyze** → `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAnalyze.fs`
  (source ~4042–4638): `computeAnalyzePlan` + its JSON/view builders. After T012.
- [X] T015 [US1] Extract **HandlersEvidence** → `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs`
  (source ~4639–5094): `computeEvidencePlan` + obligations/artifact text. After T012.
- [X] T016 [US1] Extract **HandlersVerify** → `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs`
  (source ~5095–5626): `computeVerifyPlan` + verify JSON/views. After T012.
- [X] T017 [US1] Extract **HandlersShip** → `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersShip.fs`
  (source ~5627–5956): `computeShipPlan` + ship JSON / governance handoff. After T012.
- [X] T018 [US1] Extract **HandlersAgents** → `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAgents.fs`
  (source ~5957–6230): `computeAgentsPlan` + agents config/guidance. After T012.
- [X] T019 [US1] Extract **HandlersRefresh** → `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs`
  (source ~6231–6664): `computeRefreshPlan`, **preserving its self-contained guard
  verbatim** (it does not route through `runHandler` — FR-006 edge case). After T012.
- [X] T020 [US1] Reduce **CommandWorkflow.fs** to the facade (~150 lines):
  `module CommandWorkflow`, `open FS.GG.SDD.Commands.Internal`, retaining only
  `nextLifecycleEffects`, `init`, `update` (source ~6665–6814). Leave
  `CommandWorkflow.fsi` untouched. Depends on T013–T019.
- [X] T021 [US1] Confirm cohesion + size cap: `wc -l` over
  `src/FS.GG.SDD.Commands/CommandWorkflow/*.fs` and the facade shows no file
  > ~1,500 lines (SC-001) and each file name maps to one concern/handler family
  (SC-005). Optionally add a structural test asserting the file-size/layout invariant.

**Checkpoint**: Monolith replaced by facade + 13 concern files; navigability delivered.

---

## Phase 4: User Story 2 — Preserve the public contract and automation output exactly (Priority: P1)

**Goal**: Prove the public `.fsi` and every command's deterministic JSON are
byte-identical to `main` (FR-002, FR-003, FR-006; SC-002, SC-003, SC-004).

**Independent Test**: `git diff --exit-code "$BASE" -- .../CommandWorkflow.fsi`
(`$BASE` = the T001 merge-base baseline) is empty; `dotnet test` passes 438 with
`git status --porcelain` empty.

- [X] T022 [US2] Verify `.fsi` byte-identity against the recorded T001 baseline
  `$BASE` (the merge base, **not** the moving `main` ref):
  `git diff --exit-code "$BASE" -- src/FS.GG.SDD.Commands/CommandWorkflow.fsi`
  returns 0 with no output (FR-002, SC-002).
- [X] T023 [US2] Run the full suite: `dotnet test` passes all 438 tests and
  `git status --porcelain` is empty — no golden/baseline/surface/release-readiness
  file regenerated (FR-003, FR-009; SC-003).
- [X] T024 [US2] Spot-check behavioral equivalence per `quickstart.md` §6: diff a
  representative command's `--json` output after the refactor against the
  pre-refactor snapshot captured in T001 at `$BASE` (the merge base, not the
  moving `main` ref) for byte-identity, including `refresh` (whose guard diverges)
  — confirms SC-004 beyond the suite proxy.

**Checkpoint**: Public contract and JSON output proven byte-stable.

---

## Phase 5: User Story 3 — Build and dependency order remain valid (Priority: P2)

**Goal**: Release build is clean with no new warning categories / no FS3261
increase, one-way layering holds, and the `.fsproj` order is valid (FR-007, FR-008;
SC-006).

**Independent Test**: `dotnet build -c Release` succeeds; Commands still references
only Artifacts; FS3261 site count ≤ the T002 baseline.

- [X] T025 [US3] Verify clean Release build: `dotnet build -c Release --no-incremental`
  → "Build succeeded. 0 Error(s)", no new warning categories, and FS3261 unique-site
  count ≤ the T002 baseline (FR-007, SC-006).
- [X] T026 [US3] Verify layering: `grep ProjectReference
  src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` shows only the Artifacts
  reference (no new cross-layer/cyclic edge), and the new compile order is valid
  with no forward references (FR-008).

**Checkpoint**: Build, warnings, and layering gates all green.

---

## Phase 6: Polish & Roadmap closeout

**Purpose**: Record evidence and update the refactor roadmap.

- [X] T027 Run the full `quickstart.md` sequence (steps 1–6) end-to-end as the
  consolidated acceptance pass.
- [X] T028 Update `docs/reports/2026-06-26-074428-refactor-analysis.md`: mark R2 ✅
  with evidence (spec readiness / commit) and update the aggregate count (FR-010).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — baseline capture.
- **Foundational (Phase 2)**: after Setup — scaffold BLOCKS all extraction.
- **US1 (Phase 3)**: after Foundational. Extraction T007→T020 is sequential by F#
  compile order (Foundation first, facade last).
- **US2 (Phase 4)** and **US3 (Phase 5)**: after US1's facade lands (T020/T021);
  both are verification gates and can run in parallel with each other.
- **Polish (Phase 6)**: after US2 + US3 are green.

### Within User Story 1 (compile order is the dependency)

- T007 (Foundation) → T008 → T009 → T010 (Parsing chain) → T011 (ViewGeneration)
  → T012 (Prerequisites) → T013–T019 (handler families) → T020 (facade) → T021.
- T013–T019 are mutually independent once T012 lands (each owns one `compute*Plan`),
  but all share the `.fsproj` compile list and precede the facade — extract in the
  listed order to keep each commit green (incremental-landing edge case).

### Parallel Opportunities

- T002 and T003 (baseline captures) run in parallel after T001.
- T022 (`.fsi` diff) / T023 (suite) / T024 (spot-check) and the Phase 5 build/layering
  checks are independent reads — runnable together once T020/T021 land.
- The handler extractions T013–T019 are logically independent (distinct files, one
  handler each); kept sequential here only to serialize the shared `.fsproj` edit and
  guarantee a green build at every commit.

---

## Implementation Strategy

### MVP (User Story 1)

1. Phase 1 baselines → Phase 2 scaffold → Phase 3 extraction (T007→T021).
2. **STOP and VALIDATE**: monolith gone, facade ~150 lines, no file > ~1,500 lines,
   suite green. Navigability (the entire point of R2) is delivered.

### Incremental landing (edge case FR / spec §Edge Cases)

- Land each extraction (or small group) as its own commit. Every intermediate commit
  MUST keep the build green, the suite green, and the `.fsi`/JSON byte-stable. If the
  split ships in stages, run T022/T023/T025 at each stage, not just at the end.

---

## Notes

- This is a **behavior-preserving** Tier 2 refactor: move bindings verbatim; never
  reorder within a section in a way that changes evaluation (I-2). Preserve
  `computeRefreshPlan`'s divergent guard exactly (FR-006).
- No new public surface → no new baseline; `.fsi` stays byte-identical (FR-002).
- The existing 438-test deterministic/golden suite is the authoritative behavioral
  guard (Principle VI); only an optional structural file-size/layout test is new.
- Commit after each task or logical group; stop at any checkpoint to validate.
