# Tasks: Surface Drift Classification (additive vs breaking)

**Feature**: `087-surface-drift-classification` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- Constitution ordering (Principle I): **FSI → semantic tests → implementation** per story.

## Path Conventions

Single .NET solution; paths are repo-relative.

---

## Phase 1: Foundational (shared report + pure core signatures)

These block every user story (all three stories read the same report fact and pure core).

- [ ] T001 [P] Define the report shapes in `src/FS.GG.SDD.Commands/CommandTypes.fsi`: add
  `ClassifiedEntry` and `SurfaceClassification` records (per data-model.md) and add the additive
  `Classification: SurfaceClassification` field to `SurfaceSummary`, with doc comments.
- [ ] T002 Mirror T001 in `src/FS.GG.SDD.Commands/CommandTypes.fs` (record definitions only; keep
  field order identical to the `.fsi`).
- [ ] T003 [P] Declare the pure classification core signatures in
  `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fsi` — a `SurfaceClassify` surface:
  `memberTokens: string -> Set<string>`, `classifyPair: baseline:string -> source:string ->
  ClassifiedEntry` (given the path), `rollup: ClassifiedEntry list -> SurfaceClassification`.
  (If the core lives inside `HandlersSurface` as `internal`, declare there instead and skip the
  `.fsi` — decide in T004.)

**Checkpoint**: Types compile; `SurfaceSummary` now carries `Classification`. Downstream code will
not build until the handler populates it — that is expected and drives the next phase.

---

## Phase 2: User Story 1 — classify additive vs breaking (Priority: P1) 🎯 MVP

**Goal**: A drifted baseline is classified additive/breaking with a per-file + run-level verdict and
recommended bump; exit code unchanged.

### Tests for User Story 1 ⚠️ write first (must fail before T008–T009)

- [ ] T004 [US1] In `tests/FS.GG.SDD.Commands.Tests/SurfaceCommandTests.fs`, add cases: (a) a
  drifted baseline that only *adds* a `val` → entry `additive`, bump `minor`, run verdict
  `additive`/`minor`, and `exitCodeForReport = 1` (still drift); (b) a drifted baseline that
  *removes* a prior `val` → `breaking`/`major`, exit 1; (c) a drifted baseline whose `val` *type
  changed* → `breaking`, exit 1; (d) a run with one additive + one breaking drifted file → each
  entry classified independently, run verdict `breaking`/`major` (most-severe wins, SC-006).
- [ ] T005 [P] [US1] Add focused unit tests for the pure core (`memberTokens`, `classifyPair`,
  `rollup`): added-only ⇒ additive; a removed token ⇒ breaking; equal token sets ⇒ cosmetic;
  severity rollup order breaking ≻ additive ≻ cosmetic ≻ none; bump mapping.

### Implementation for User Story 1

- [ ] T006 [US1] Implement the pure `SurfaceClassify` core (comment strip `//`/`///` + `(* *)`,
  blank-line drop, whitespace-collapse → token set; set-difference → classification; severity
  rollup + bump map). Place per T003's decision (`Foundation.fs` module or `HandlersSurface`
  internal); keep it total and side-effect-free.
- [ ] T007 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersSurface.fs` `computeSummary`,
  classify the **drifted** pairs (`Some s, Some b when s <> b`) using the core, build the sorted
  `Entries`, `rollup` the verdict, and attach `Classification` to the `SurfaceSummary`. Emit **no**
  new diagnostic and do not touch the exit path (FR-008).
- [ ] T008 [US1] Extend `writeSurface` in `src/FS.GG.SDD.Commands/CommandSerialization.fs` with the
  nested `classification` object (verdict, recommendedBump, sorted entries with sorted member
  lists). Always written (stable shape).
- [ ] T009 [US1] Add the `surfaceClassification*` / `surfaceClassified` `key: value` lines to
  `renderText` in `src/FS.GG.SDD.Commands/CommandRendering.fs` (rich auto-derives).

**Checkpoint**: US1 acceptance scenarios pass; feature-086 tests still green (exit codes unchanged).

---

## Phase 3: User Story 2 — scope to shipped-surface mutations only (Priority: P1)

**Goal**: `missing-baseline` (new surface), `matched`, and `orphan` files carry no classification;
a missing-only tree reports run verdict `none` while still exiting 1.

### Tests for User Story 2 ⚠️

- [ ] T010 [US2] Add cases to `SurfaceCommandTests.fs`: (a) a drifted (additive) file plus a
  baseline-less source → exactly one entry, the missing file has no entry, run verdict
  `additive`/`minor`, exit 1 (SC-003 scoping); (b) a tree whose only drift is `missing-baseline` →
  run verdict `none`/`none`, `Entries` empty, exit 1; (c) a fully `matched` tree → verdict
  `none`/`none`, exit 0.

### Implementation for User Story 2

- [ ] T011 [US2] Confirm/adjust the drifted-only filter in T007 so only `Some,Some,unequal` pairs
  are classified; ensure `matched`/`missing`/`orphan` are excluded. (Likely no code beyond T007 —
  this phase is primarily its verifying tests.)

**Checkpoint**: Scope tests pass; no spurious classification of new/matched surfaces.

---

## Phase 4: User Story 3 — projection parity, determinism, cosmetic + fallback (Priority: P2)

**Goal**: json/text/rich carry an identical classification fact set, json is deterministic, a
formatting-only drift is `cosmetic`, and an unparseable source falls back to `breaking`.

### Tests for User Story 3 ⚠️

- [ ] T012 [US3] In `tests/FS.GG.SDD.Cli.Tests/SurfaceProjectionTests.fs`, assert the three
  projections carry the same classification facts (verdict, bump, per-file class), the redirected
  rich output has zero ANSI/box sequences (SC-005), and repeated default-projection runs on an
  unchanged drifted tree are byte-identical (SC-008).
- [ ] T013 [P] [US3] Add a `cosmetic` case (drift by reordering members + adding a comment → entry
  `cosmetic`, bump `none`, still `drifted`/exit 1, SC-004) and a conservative-fallback case (drifted
  source with non-empty text but zero member tokens → `breaking`, `unparseableFallback = true`,
  FR-011) to `SurfaceCommandTests.fs`.

### Implementation for User Story 3

- [ ] T014 [US3] Implement the `cosmetic` branch (equal token sets, differing bytes) and the
  `unparseableFallback` branch in the core (T006) if not already covered; ensure member lists are
  sorted for determinism.
- [ ] T015 [US3] Verify rich auto-derivation renders the classification lines (no bespoke rich block
  needed); adjust `renderText` line format if a fact is missing from the rich table.

**Checkpoint**: All three user stories complete; SC-001…SC-008 covered.

---

## Phase 5: Polish & cross-cutting

- [ ] T016 [P] Update the internal reflection `PublicSurface.baseline` (via `FSGG_UPDATE_BASELINE=1`)
  only if the public static-method surface actually changed; record-field additions typically do
  not change it — confirm, don't assume.
- [ ] T017 [P] If a workspace-facing doc enumerates the `surface` report fields
  (`docs/reference/*` / the `surface` `CommandReport` block docs), add the `classification` fact.
- [ ] T018 Build the solution and run the surface + projection + baseline test suites; ensure
  feature-086 tests are unchanged-green and determinism holds. Update the SDD `.fsi` API-surface
  baseline (`fsgg-sdd surface --update` in this repo is N/A — the component uses hand-authored
  `.fsi`; ensure `CommandTypes.fsi` matches `.fs`).

---

## Dependencies & parallelism

- **Phase 1 (T001–T003)** blocks all stories. T001/T003 are `[P]` (different files); T002 mirrors
  T001 (same-type, do right after).
- **US1 (Phase 2)** is the MVP and must land before US2/US3 add value. Within it: T004/T005 (tests)
  before T006/T007; T008/T009 (projections) after T007. T005 is `[P]` with T004.
- **US2 (Phase 3)** depends on US1's handler (T007); mostly verifying tests.
- **US3 (Phase 4)** depends on US1's core + projections; T012/T013 are `[P]`.
- **Phase 5** last.

## Implementation strategy

MVP = Phase 1 + Phase 2 (US1): additive/breaking classification emitted and projected. US2 tightens
scope (mostly tests), US3 adds determinism/parity + the cosmetic & fallback branches. Ship
incrementally; each phase checkpoint keeps feature-086 behavior green.
