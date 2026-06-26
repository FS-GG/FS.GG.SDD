---
description: "Task list for R7 — strip redundant `private` + give `failwith` escapes context"
---

# Tasks: Strip redundant `private` + give `failwith` escapes context

**Input**: Design documents from `/specs/028-strip-private-failwith/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Change Tier**: Tier 2 (internal). Both phases match the spec tier, so no per-task
`[T1]/[T2]` annotation is emitted.

**Tests**: No new test files. This row's evidence is the existing 437-test suite staying
green plus byte-identical `.fsi`/`PublicSurface.baseline`/`--json`/`--text` (the contract
under test). US2 adds an in-place no-throw assertion only if a path becomes provably total
(none expected — all 9 escapes stay context-bearing throws).

**Elmish/MVU applicability (Principle IV/V)**: No state or I/O boundary changes. US2 touches
the post-`update` `Option.defaultWith` escape in `ValidationRunner.fs` (message-only, on the
impossible branch); no `Model`/`Msg`/`Effect`/`init`/`update`/interpreter shape changes. The
`.fsi` contract task (normally required for stateful stories) is **N/A** — every `.fsi` is a
frozen read-only invariant for this row.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (distinct file).
- **[Story]**: `US1` (redundant `private`) or `US2` (`failwith` context).
- Each task names an exact path.

---

## Phase 1: Setup (Shared Gate)

**Purpose**: Establish the merge-base pre-image and the byte-identical-contract gate that
every later phase is verified against.

- [X] T001 Confirm clean tree on `028-strip-private-failwith` and record the merge base:
  `git merge-base HEAD main` → note the SHA in your working log (used by the baseline-diff
  gate in quickstart §4).
- [X] T002 [P] Capture the deterministic output pre-image per `specs/028-strip-private-failwith/quickstart.md` §1:
  for `charter`, `analyze`, `refresh` write `--json` and `--text` to `/tmp/r7-baseline/`
  (the stable pre-image for the SC-005 diff).
- [X] T003 [P] Capture the green baseline: `dotnet build -c Release FS.GG.SDD.sln` (0 errors,
  no FS3261/FS0025) and `dotnet test FS.GG.SDD.sln` (437 pass). Record the test count so
  regressions are detectable.

**Checkpoint**: pre-image + green baseline captured — edits can begin.

---

## Phase 2: Foundational (Site Inventory Lock)

**Purpose**: Pin the exact edit sites so removals/rewrites are mechanical, not exploratory.
Blocks both stories.

- [X] T004 Re-verify the `private` inventory against the current tree and record it in
  `specs/028-strip-private-failwith/data-model.md` (Site disposition record table):
  `grep -rn -E '\b(let|type|module) +private\b' --include='*.fs' src` — expect 81 sites across
  the 9 files listed in plan.md (`ValidationRunner.fs` 33, `ReleaseContract.fs` 20,
  `Rendering.fs` 8, `WorkModel.fs` 7, `GovernanceHandoff.fs` 5, `ValidationHarness.fs` 3,
  `LifecycleArtifacts/Verify.fs` 3, `SchemaVersion.fs` 1, `HandlersShip.fs` 1).
- [X] T005 [P] Re-verify the `failwith` escape inventory and confirm the `contextNamed` value
  per site in `data-model.md`: `grep -rn -E 'failwith|defaultWith failwith' --include='*.fs' src`
  — expect the 9 sites at `ParsingTasks.fs:91,96,101`, `HandlersEvidence.fs:220,259`,
  `ReleaseContract.fs:266,451`, `SchemaVersion.fs:166`, `ValidationRunner.fs:642`.

**Checkpoint**: inventory locked and matches the plan; any drift reconciled before editing.

---

## Phase 3: User Story 1 - Strip redundant `private` (Priority: P1) 🎯 MVP

**Goal**: Remove every redundant top-level `private` from `.fsi`-guarded modules so the `.fsi`
is the sole visibility arbiter (FR-001/FR-003, Principle III). Retain only `private` the
build/test gate proves load-bearing (FR-002).

**Independent Test**: After the gate (T015), `grep -rn -E '\b(let|type|module) +private\b'
--include='*.fs' src` returns only sites with a recorded `retentionReason`; build green, suite
green, empty `.fsi`/baseline diff, empty output diff.

### Implementation for User Story 1

Each task: in the named file, delete the `private` modifier from every top-level
`let private` / `type private` / `module private` whose `.fsi` already omits the binding.
Do not touch the `.fsi`. Files are distinct, so all are `[P]`.

- [X] T006 [P] [US1] Strip redundant `private` (33 sites) in `src/FS.GG.SDD.Validation/ValidationRunner.fs`.
- [X] T007 [P] [US1] Strip redundant `private` (20 sites) in `src/FS.GG.SDD.Artifacts/ReleaseContract.fs`.
- [X] T008 [P] [US1] Strip redundant `private` (8 sites) in `src/FS.GG.SDD.Cli/Rendering.fs`.
- [X] T009 [P] [US1] Strip redundant `private` (7 sites) in `src/FS.GG.SDD.Artifacts/WorkModel.fs`.
- [X] T010 [P] [US1] Strip redundant `private` (5 sites) in `src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs`.
- [X] T011 [P] [US1] Strip redundant `private` (3 sites) in `src/FS.GG.SDD.Validation/ValidationHarness.fs`.
- [X] T012 [P] [US1] Strip redundant `private` (3 sites) in `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Verify.fs`.
- [X] T013 [P] [US1] Strip redundant `private` (1 site) in `src/FS.GG.SDD.Artifacts/SchemaVersion.fs`.
- [X] T014 [US1] **Edge case** — `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersShip.fs` (1 site,
  no sibling `.fsi`, `[<AutoOpen>] module internal`): remove the `private` **only if** the
  full gate (T015) passes with it removed; otherwise retain it and record `retentionReason`
  (`AutoOpenCrossFile`) in `data-model.md`. Per spec Edge Cases / FR-002.
  **Resolution**: `Removed` — `parseShipReadinessFacts` is the sole binding of that name in
  the assembly; full gate green with `private` removed, so the `[<AutoOpen>]` re-export
  introduces no collision/shadow. No `retentionReason` needed.

### Gate for User Story 1

- [X] T015 [US1] Run the byte-identical-contract gate (depends on T006–T014):
  `dotnet build -c Release FS.GG.SDD.sln` (0 errors, no new warning, FS3261/FS0025 still 0),
  `dotnet test FS.GG.SDD.sln` (437 pass), `git diff --stat $(git merge-base HEAD main) -- '**/*.fsi' '**/PublicSurface.baseline'`
  (empty), and the quickstart §5 output diff for charter/analyze/refresh `--json`/`--text`
  (empty). Any build/test failure proves a removed `private` was load-bearing → restore it and
  record its `retentionReason` in `data-model.md`, then re-run.

**Checkpoint**: US1 complete and independently shippable — zero redundant `private`, all
contracts byte-identical.

---

## Phase 4: User Story 2 - `failwith` escapes carry context (Priority: P2)

**Goal**: Rewrite each of the 9 partial-function escapes to a context-bearing form whose
message names the offending id/path/value **and** the underlying error (FR-004a). Thread a
`Result`→diagnostic only if a site is reachable on bad input **and** output stays
byte-identical (FR-004b/FR-005 — none expected to qualify; otherwise record `DeferredOutOfScope`).

**Independent Test**: `grep -rn -E 'failwith message|defaultWith failwith|failwith "report not built"'
--include='*.fs' src` is empty; each former bare throw now names its context; suite passes
unchanged (happy paths untouched — rewrites change only the unreachable branch's message).

### Implementation for User Story 2

- [X] T016 [P] [US2] In `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingTasks.fs` rewrite the 3
  escapes (`:91` constructed `EV%03d` evidence id, `:96` tasks artifact path, `:101`
  constructed `T%03d` task id) to context-bearing throws (`failwithf`/`invalidOp`) naming the
  id/path + inner error.
- [X] T017 [P] [US2] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs` rewrite
  `:220` (evidence artifact path) and `:259` (`Result.defaultWith failwith` over the
  pre-validated `workId`) to name the path / offending `workId` + inner error.
- [X] T018 [P] [US2] In `src/FS.GG.SDD.Artifacts/ReleaseContract.fs` rewrite `:266`
  (`CommandSerialization.fs` artifact path) and `:451` (re-parse of the just-serialized
  inventory) to name the artifact path + inner error.
- [X] T019 [P] [US2] In `src/FS.GG.SDD.Artifacts/SchemaVersion.fs` rewrite `:166`
  (`Result.defaultWith failwith` over the self-built generator version) to name the
  generator-version component + inner error.
- [X] T020 [P] [US2] In `src/FS.GG.SDD.Validation/ValidationRunner.fs` rewrite `:642`
  (`Option.defaultWith (fun () -> failwith "report not built")` after `BuildReport`) to a
  context-bearing throw naming the validation stage + the "report not built after BuildReport"
  invariant. Message-only on the impossible branch — MVU `update`/`init` shapes untouched.
- [X] T021 [US2] If any site in T016–T020 turned out reachable on malformed external input
  (FR-004b): thread the `Result.Error` into a diagnostic on the normal report path **only**
  when fixtures stay byte-identical; otherwise keep the context-bearing throw and record the
  conversion as `DeferredOutOfScope` in `data-model.md`. Add an in-place no-throw assertion in
  the owning test project for any path that became provably total (US2 Scenario 3). Expected: a
  no-op record — all 9 are unreachable-by-construction.
  **Resolution**: no-op as expected — all 9 sites are `UnreachableByConstruction`, none
  reachable on malformed external input, so no `Result` was threaded, no `DeferredOutOfScope`
  recorded, and no new test added. Happy-path output stayed byte-identical.

### Gate for User Story 2

- [X] T022 [US2] Re-run the full gate (depends on T016–T021): Release build green (no new
  warning, ratchet 0), `dotnet test FS.GG.SDD.sln` (all pass, none weakened), empty
  `.fsi`/baseline diff, empty charter/analyze/refresh `--json`/`--text` diff vs the T002
  pre-image. Confirm the old-form grep is empty
  (`grep -rn -E 'failwith message|defaultWith failwith|failwith "report not built"' --include='*.fs' src`)
  **and** run a broad audit — `grep -rn -E 'failwith\b|failwithf\b' --include='*.fs' src` — confirming
  every surviving throw names a context id/path/value + inner error (proves SC-002 positively, not just
  the absence of old forms).

**Checkpoint**: US1 + US2 complete — zero bare-string throws, all contracts byte-identical.

---

## Phase 5: Polish & Closeout

**Purpose**: Land the roadmap evidence and run the full quickstart verification.

- [X] T023 [US2] Update `docs/reports/2026-06-26-074428-refactor-analysis.md` (FR-008/SC-007):
  flip the R7 row and its status-detail entry to ✅ with landed evidence, and set the aggregate
  line to `7 / 7 complete · 0 in progress · 0 not started`.
- [X] T024 Run the full `specs/028-strip-private-failwith/quickstart.md` end to end (§2–§7) and
  confirm every expected outcome: green Release build, 437 tests pass, empty `.fsi`/baseline
  diff, empty output diff, no redundant `private` / no bare-string throw remaining, roadmap at
  `7 / 7 complete`. Verify `Directory.Build.props` is unchanged and no new `#nowarn` was added.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies — start immediately.
- **Phase 2 (Foundational)**: after Setup — locks the inventory both stories edit.
- **Phase 3 (US1)**: after Foundational. T006–T013 parallel; T014 (edge case) and T015 (gate)
  depend on the removals.
- **Phase 4 (US2)**: independent of US1 in principle, but `ReleaseContract.fs` (T007 US1 / T018
  US2) and `ValidationRunner.fs` (T006 US1 / T020 US2) are shared files — sequence US1 before
  US2 on those two files (or do both edits in one pass) to avoid same-file conflicts. T016–T020
  parallel across the other files; T021/T022 depend on them.
- **Phase 5 (Polish)**: after both story gates (T015, T022) are green.

### Within Each User Story

- US1: per-file `private` removals (T006–T013) are parallel; edge-case T014 then gate T015.
- US2: per-file rewrites (T016–T020) are parallel; contingency T021 then gate T022.

### Parallel Opportunities

- T002 ‖ T003 (Setup).
- T004 ‖ T005 (Foundational).
- T006–T013 all parallel (8 distinct files); T016, T017, T019 parallel (T018/T020 share files
  with US1 — serialize per the note above).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 Setup → Phase 2 Foundational.
2. Phase 3 US1 (strip redundant `private`) → **T015 gate**.
3. **STOP and VALIDATE**: zero redundant `private`, contracts byte-identical. US1 ships alone.

### Incremental Delivery

1. Setup + Foundational → gate ready.
2. US1 → gate green → shippable noise-removal increment.
3. US2 → gate green → shippable context increment.
4. Closeout: roadmap ✅ + full quickstart.

---

## Notes

- [P] = distinct file, no dependency on another incomplete task in the phase.
- Never weaken or remove a test to green the build; a failing gate after a `private` removal is
  *signal* — restore that `private` and record its `retentionReason` (FR-002), don't suppress.
- No `.fsi` edits, no `#nowarn`, no `Directory.Build.props` changes — these are read-only
  invariants for the whole row.
- Commit per logical group (e.g. per file, or per story) so a gate failure localizes cleanly.
