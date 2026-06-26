---
description: "Task list for 029-unify-view-state"
---

# Tasks: Unify generated-view-state construction

**Input**: Design documents from `/specs/029-unify-view-state/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: No new behavior tests. This is a Tier 2 behavior-preserving refactor;
per research R-4 / FR-009 the binding gate is the **existing** command-output
golden suite plus byte-identical `.fsi`, `PublicSurface.baseline`, and
deterministic `--json`/`--text` output. Do **not** add new internal unit tests;
only add a command-level golden if you find a view `Kind` not already pinned.

**Organization**: Grouped by user story (US1 P1, US2 P2, US3 P3) so each ships
independently. All work is inside `src/FS.GG.SDD.Commands/CommandWorkflow/`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in the phase
- **[Story]**: US1 / US2 / US3
- Tier: whole feature is Tier 2 (matches spec), so per-task `[T2]` is omitted
- Elmish/MVU: pure view-construction inside the existing `update`/effect path —
  no `Model`/`Msg`/`Effect`/interpreter change, no new I/O. Principle IV/V are
  satisfied by construction (net fewer functions, no boundary touched); no
  `.fsi` contract tasks apply (all bindings are `internal`/`[<AutoOpen>]`).

---

## Phase 1: Setup & Baseline

**Purpose**: Pin the pre-change ground truth to diff against. No source edits.

- [X] T001 Confirm clean `main` @ `7a6280f` and a green starting point: from a
  `main` checkout (or `git worktree add`), run `dotnet build -c Release
  --no-incremental` (0 errors, 0 warnings; FS3261/FS0025 ratchet at 0) and
  `dotnet test FS.GG.SDD.sln` (all pass). Record the test attribute count
  (expected 434) for the SC-005 comparison.
- [X] T002 [P] Capture the pre-change deterministic baseline per quickstart.md §1:
  for a representative fixture work item exercising every view kind
  (charter/workModel, analyze, verify, ship/governance-handoff, agents, refresh),
  save `--json` and `--text` bytes to `/tmp/base.<cmd>.{json,txt}` — or confirm
  the existing goldens under `tests/FS.GG.SDD.Commands.Tests/` already pin them.
- [X] T003 [P] Snapshot the public-surface baseline for the drift gate: record
  the current `**/*.fsi` and `**/PublicSurface.baseline` state (`git stash` point
  / `main` ref) so `git diff --stat main -- '**/*.fsi' '**/PublicSurface.baseline'`
  can be confirmed empty at the end (FR-006 / SC-006).

**Checkpoint**: Baseline outputs and surface snapshot are captured.

---

## Phase 2: User Story 1 - One canonical generated-view-state constructor (Priority: P1) 🎯 MVP

**Goal**: Replace the four `*generatedViewState` constructor definitions with one
`kind`-parameterized constructor in `Foundation.fs`; route every call site through
it. Largest drift-risk win; independently shippable.

**Independent Test**: Exactly one constructor definition remains; full suite
passes; `charter`/`analyze`/`verify`/`ship`/`agents`/`refresh` `--json`/`--text`
bytes equal the T002 baseline; `.fsi` and all four `PublicSurface.baseline` files
byte-identical to `main`.

- [X] T004 [US1] Add the canonical constructor to the `[<AutoOpen>] module internal
  Foundation` in `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`, with the
  ship signature `path -> kind -> generator -> sources -> outputDigest -> currency
  -> diagnosticIds : GeneratedViewState` and the body verbatim from data-model.md
  (`SchemaVersion = Some 1`, `Sources |> List.sortBy _.Path`, `DiagnosticIds |>
  List.distinct |> List.sort`). All record types are already in scope via the
  existing `open FS.GG.SDD.Artifacts.WorkModel`. This is the prerequisite for T005–T010.
- [X] T005 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/ViewGeneration.fs`:
  delete the old `generatedViewState` def (`:463`, `Kind = "workModel"`) and the
  `analysisGeneratedViewState` def (`:258`); re-point the workModel call sites
  (`:562,:579,:607`) to add the explicit `"workModel"` kind arg, and the analysis
  call site (`:448`) to call `generatedViewState … "analysis" …`. (After T004.)
- [X] T006 [P] [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs`:
  delete the `verifyGeneratedViewState` def (`:228`); re-point its call sites
  (`:510,:511,:533`) to `generatedViewState … "verification" …`, and the
  workModel site (`:479`) to add the `"workModel"` arg. (After T004.)
- [X] T007 [P] [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersShip.fs`:
  delete the `shipGeneratedViewState` def (`:29`); rename every call
  (`:172,:448,:449,:453,:454,:496`, and the workModel site `:430`) to
  `generatedViewState` — these already pass the kind explicitly, so only the
  symbol name changes (the workModel `:430` site adds `"workModel"`). (After T004.)
- [X] T008 [P] [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAgents.fs`:
  re-point the constructor call (`:345`, currently `shipGeneratedViewState`) to
  `generatedViewState`; and rename the unrelated local string binding (`:364`)
  `let generatedViewState = "blocked"|…` to `generatedViewStateLabel`, updating
  the single `GeneratedViewState = generatedViewState` field RHS to match
  (output-neutral; resolves the shadow). (After T004; see research R-2.)
- [X] T009 [P] [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEarly.fs`,
  `HandlersAnalyze.fs` (`:67`), and `HandlersEvidence.fs` (`:613`): re-point the
  remaining workModel `generatedViewState` call sites
  (HandlersEarly.fs `:56,:97,:140,:189,:240`) to add the explicit `"workModel"`
  kind arg. (After T004; these become `blockedWorkModelView` sites in US3 —
  leave them as direct calls here if US3 lands in the same pass.)
- [X] T010 [US1] Verify US1 in isolation: `dotnet build -c Release --no-incremental`
  clean, `dotnet test FS.GG.SDD.sln` green, and per quickstart.md §3–§5 confirm
  exactly **1** constructor definition (SC-001), no `let generatedViewState =`
  shadow remains, byte-identical `--json`/`--text` vs the T002 baseline, and empty
  `.fsi`/`PublicSurface.baseline` diff vs `main` (FR-006/FR-007).

**Checkpoint**: US1 complete — single constructor, all output and surfaces stable.
Feature is shippable here (P2/P3 optional).

---

## Phase 3: User Story 2 - Single blocking-diagnostic-id helper (Priority: P2)

**Goal**: Extract one `blockingDiagnosticIds` helper for the `Error`-severity
filter → `.Id` map and route the inline occurrences through it. Independent of US1.

**Independent Test**: No handler retains the inline `filter (… DiagnosticError) |>
List.map _.Id` form; suite passes; diagnostic-id ordering/content unchanged in
output (T002 baseline).

- [X] T011 [US2] Add `blockingDiagnosticIds (diagnostics: Diagnostic list) :
  string list` to `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs` exactly
  per data-model.md (`List.filter (fun d -> d.Severity =
  DiagnosticSeverity.DiagnosticError) |> List.map _.Id` — no added sort).
- [X] T012 [P] [US2] Route the inline occurrences through the helper, one file per
  edit (parallel-safe): `HandlersEarly.fs` (5 sites), `HandlersAnalyze.fs`,
  `HandlersEvidence.fs`, `HandlersVerify.fs`, `HandlersShip.fs`, and the
  matching site(s) in `ViewGeneration.fs` — the 10 sites named in plan.md §"Grounded
  inventory". Replace only the byte-identical `Error`-filter→`.Id`-map shape; do
  **not** touch the parsing/`HandlersRefresh` diagnostic uses that are not this
  exact projection. (After T011.)
- [X] T013 [US2] Verify US2: build clean, suite green, and per quickstart.md §5
  `grep -rnB1 'List.map _.Id' . | grep -c 'DiagnosticError'` returns **0**
  (SC-002); `--json`/`--text` byte-identical to the T002 baseline.

**Checkpoint**: US1 + US2 both independently functional.

---

## Phase 4: User Story 3 - Single blocked-view construction helper (Priority: P3)

**Goal**: Extract one `blockedWorkModelView` helper for the
`generatedViewState path generator [] None Blocked ids` shape and route the 9
handler sites through it. **Depends on US1** (the canonical constructor). Optional
within this feature — defer only if it would touch `computeRefreshPlan` (it does
not; research R-3).

> **Out of scope (do not route):** the inline `GeneratedViewState` record in
> `HandlersRefresh.fs` (`Kind = "summary"`, ~`:405`). Its `Sources = summarySources`
> follow `structuredSourcePaths` order (shared with the rendered summary Markdown)
> and are intentionally not `List.sortBy _.Path`-normalized, so routing it through
> the unified constructor would risk output drift. Leave it inline; it is not a
> named constructor, so SC-001 is unaffected (spec Edge Cases; data-model "Out of scope").

**Independent Test**: The 9 blocked-view sites route through the helper; suite
passes; blocked-view JSON byte-identical to baseline.

- [X] T014 [US3] Add `blockedWorkModelView (path: string) (generator:
  GeneratorVersion) (blockingIds: string list) : GeneratedViewState =
  generatedViewState path "workModel" generator [] None
  GeneratedViewCurrency.Blocked blockingIds` to
  `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`. (After T004.)
- [X] T015 [P] [US3] Route the 9 blocked-view sites through the helper:
  `HandlersEarly.fs` (`:56,:97,:140,:189,:240`), `HandlersAnalyze.fs` (`:67`),
  `HandlersEvidence.fs` (`:613`), `HandlersVerify.fs` (`:479`),
  `HandlersShip.fs` (`:430`). Do **not** convert `ViewGeneration.fs:562` — it is
  *similar but not identical* (non-empty sources, `blockingCommandIds`); it stays
  a direct `generatedViewState` call (research R-3). Never touch
  `computeRefreshPlan`, and **leave `HandlersRefresh.fs`'s inline `Kind = "summary"`
  record untouched** (out of scope; see the Goal note above). (After T014; supersedes
  the direct workModel calls left by T009.)
- [X] T016 [US3] Verify US3: build clean, suite green, and per quickstart.md §5
  `grep -rcE 'GeneratorVersion \[\] None GeneratedViewCurrency\.Blocked'` totals
  **0** inline forms (SC-003); blocked-view `--json`/`--text` byte-identical to baseline.

**Checkpoint**: All three stories independently functional.

---

## Phase 5: Polish & Final Validation

**Purpose**: Full-feature gate and the SC counters end-to-end (quickstart.md §2–§6).

- [X] T017 Run the full validation sweep: `dotnet build -c Release --no-incremental`
  (0 errors, no new warning category, FS3261/FS0025 ratchet at 0, no `#nowarn`
  added, `Directory.Build.props` unchanged — FR-008) and `dotnet test
  FS.GG.SDD.sln` (all 434 attributes green — SC-005/FR-009).
- [X] T018 [P] Confirm zero deterministic drift across every command and both
  projections per quickstart.md §3 (`diff` each `--json`/`--text` vs `/tmp/base.*`);
  expect **no** `DRIFT` lines (FR-007/SC-005). `--rich` excluded.
- [X] T019 [P] Confirm public surface untouched: `git diff --stat main --
  '**/*.fsi' '**/PublicSurface.baseline'` is **empty** (FR-006/SC-006).
- [X] T020 [P] Confirm the SC counters per quickstart.md §5–§6: 1 constructor def
  (SC-001), 0 inline blocking-id projections (SC-002), 0 inline blocked
  constructions (SC-003), 0 `generatedViewState` local shadows, and `git diff
  --shortstat main -- src/FS.GG.SDD.Commands/CommandWorkflow/` shows net deletion
  ≥ 60 LOC (SC-004).
- [X] T021 Add the R8 row + status detail/aggregate to
  `docs/reports/2026-06-26-074428-refactor-analysis.md` (quickstart.md "Done When").

---

## Dependencies & Execution Order

- **Phase 1 (Setup/Baseline)**: no dependencies; T002/T003 are `[P]`.
- **Phase 2 (US1)**: after Phase 1. T004 first (defines the canonical constructor);
  then T005–T009 in parallel (distinct files), T010 last. **MVP boundary.**
- **Phase 3 (US2)**: independent of US1 except both edit `Foundation.fs` (T011 vs
  T004 — sequence those two; helpers can otherwise proceed). T011 → T012 → T013.
- **Phase 4 (US3)**: depends on US1 (T004). T014 → T015 → T016. T015 supersedes the
  direct workModel calls left by T009, so run US3 after US1 lands (or fold T009/T015).
- **Phase 5 (Polish)**: after all desired stories. T017 first; T018–T020 `[P]`; T021 last.

### Parallel opportunities

- T002, T003 together.
- After T004: T005, T006, T007, T008, T009 across five distinct files.
- After T011: T012's per-file edits.
- T018, T019, T020 together.

## Summary

- **Total tasks**: 21 (US1: 7, US2: 3, US3: 3, setup: 3, polish: 5).
- **MVP scope**: Phase 1 + Phase 2 (US1) — the single canonical constructor;
  fully shippable on its own (FR-001–FR-003, FR-006–FR-009).
- **Parallel hotspots**: the five file re-point edits after T004; the four
  verification tasks in Phase 5.
- **No new tests** (Tier 2, behavior-preserving): the gate is the existing golden
  suite + byte-identical output/`.fsi`/baseline.
