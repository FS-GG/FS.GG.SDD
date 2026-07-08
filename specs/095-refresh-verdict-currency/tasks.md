---

description: "Task list for feature 095 — refresh reports true facts about the committed ship verdict"
---

# Tasks: `refresh` Reports True Facts About the Committed Ship Verdict

**Input**: Design documents from `/specs/095-refresh-verdict-currency/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/refresh-currency-matrix.md](./contracts/refresh-currency-matrix.md)

**Tests**: Required. Constitution VI — behavior-changing (report-contract) code MUST include automated
tests that fail before and pass after.

**Tier**: Tier 1 throughout (command output-contract change). No task deviates, so no per-task `[T1]`
annotation is emitted.

**Issue**: FS.GG.SDD#188 · **Branch**: `item/188-sdd-refresh-ship-verdict-currency-report`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on another incomplete task in the phase)
- **[Story]**: `[US1]` / `[US2]` / `[US3]`

## Path Conventions

Single project tree. Only two files are touched:

- `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersRefresh.fs`
- `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs`

`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Ship.fs` is **read-only** here (research R7), even though
FS.GG.SDD#188's `Paths:` line names it. The realized touch-set is narrower than declared — always safe
under ADR-0021.

**MVU note (Constitution V)**: not applicable in the "add an `Effect`" sense. The change lives entirely
in the *pure classification* logic of the refresh handler. It plans no new effect and interprets no I/O.
The MVU obligation this feature does carry is the inverse — **T009 asserts the planned effect set is
unchanged** (no `WriteFile` for the verdict from a bad source, FR-006). No `.fsi` task exists because
`HandlersRefresh` has no signature file and is internal (research R2), so Constitution I's FSI step and
Constitution III are not triggered.

---

## Phase 1: Setup

- [ ] **T001** Build the worktree green at baseline: `dotnet build` from
  `/home/developer/projects/FS.GG.SDD-188`. If `NU1403` fires on `FSharp.Core`, run
  `dotnet restore --force-evaluate` then `git checkout -- '**/packages.lock.json'` to revert the lock
  churn. Confirms the branch starts from a clean `39fa3e5`.

- [ ] **T002** Capture the **pre-change** exit code and JSON report for each of the 10 matrix cells in
  [contracts/refresh-currency-matrix.md](./contracts/refresh-currency-matrix.md), into
  `/tmp/claude-1000/.../scratchpad/095-baseline/`. These become the expected values for T005's
  invariance table (SC-004) and the evidence for FR-007. **Do not skip**: FR-007 is the claim most
  likely to be wrong, and it cannot be checked after the source changes.

---

## Phase 2: Tests first (red) — Constitution I step 4

Semantic tests through the observable contract, before the `.fs` body hardens. All three tasks edit the
same file, so they are **not** `[P]` with respect to each other; sequence them T003 → T004 → T005.

- [ ] **T003** [US1] Correct the characterization test
  ``a ship json that is valid json but not a ship view blocks the verdict with a diagnostic``
  at `tests/FS.GG.SDD.Commands.Tests/RefreshCommandTests.fs:88-113` to assert the **true** facts
  (matrix cell 5):
  - `:105` `Assert.Equal("current", … "ship")` → `"malformed"` (FR-003)
  - `:106` `Assert.Equal("malformed", … "ship-verdict")` → `"blocked"` (FR-004)
  - add: `ship` ∈ `summary.BlockedViewIds`, `ship` ∉ `summary.AlreadyCurrentViewIds` (FR-003a)
  - add: the sole `refresh.malformedGeneratedView` diagnostic's path is `ship.json` (FR-005)
  - rewrite the `:90-93` comment: it currently documents the `parsesAsJson` hole as a known 092
    compromise; it must now state the invariant that closes it.
  Keep `Assert.Equal(original, … shipVerdictPath)` (FR-006) and the existing `BlockedViewIds` assertion
  (they pass before and after). **This test must go RED.**

- [ ] **T004** [US2] Add the missing test for matrix cell 8 — the state whose absence let the asymmetry
  survive review (research R5). In `RefreshCommandTests.fs`, on a `shippedProject ()`: delete
  `ship-verdict.json`, append to `work/<id>/spec.md` to make the source stale, run `refresh`, and assert
  - `ship-verdict` currency is `missing` (FR-010), and
  - the verdict's diagnostic is `refresh.staleView` with severity `warning` (FR-009), **not**
    `refresh.blockedUpstreamView`.
  Assert against the diagnostic **id and severity**, not the message text. **This test must go RED.**

- [ ] **T005** Add the exit-code invariance table test over all 10 matrix cells (SC-004, FR-007). Drive
  each `{ship.json} × {ship-verdict.json}` state, assert the exit code equals T002's captured baseline.
  **This test must be GREEN before and after** the source change — it is a regression lock, not a
  red-green test. If it is red before T006, the invariance claim in research R3 is wrong and the plan
  must be revised before any source edit.

---

## Phase 3: User Story 1 (P1) — attribute `malformed` to the artifact that is malformed 🎯 MVP

**Goal**: `ship: current` stops lying, and the well-formed committed verdict stops being called corrupt.
**Independent test**: T003 goes green; matrix cells 5 and 6 report `ship: malformed` /
`ship-verdict: blocked`/`missing`.

- [ ] **T006** [US1] In `HandlersRefresh.fs`, lift the JSON predicate to the validator shape. Keep
  `parsesAsJson : string -> bool` (`:350`) and add a thin `parsesAsJsonSnap : FileSnapshot -> bool`
  (= `fun snap -> parsesAsJson snap.Text`), so both validators share one signature.

- [ ] **T007** [US1] Add `parsesAsShipView : FileSnapshot -> bool`
  (= `ShipModule.parseShipView >> Result.isOk`) next to it. `parseShipView` accepts a `FileSnapshot`
  directly (`Ship.fsi:55`), so pass the snapshot verbatim — no re-read, no reconstruction (research R1).
  Note in a comment that it is strictly stronger than `parsesAsJsonSnap` (non-JSON fails inside
  `parseJsonView`), which is what makes US1-AS4 hold without a second check.

- [ ] **T008** [US1] Change `downstreamClass` (`:438`) to take the validator as its first parameter —
  `downstreamClass (isValid: FileSnapshot -> bool) path` — and replace the hardcoded
  `not (parsesAsJson snap.Text)` at `:444` with `not (isValid snap)`. Update the three call sites
  (`:451-453`):
  - `anClass` → `downstreamClass parsesAsJsonSnap (analysisPath workId)` (FR-002)
  - `veClass` → `downstreamClass parsesAsJsonSnap (verifyPath workId)` (FR-002)
  - `shClass` → `downstreamClass parsesAsShipView (shipPath workId)` (FR-001)
  Do **not** branch on `path = shipPath workId` inside the helper — rejected in research R2; it makes
  FR-002 a runtime accident instead of a call-site fact.

- [ ] **T009** [US1] Verify the consequences rather than assuming them. Run T003 and T005.
  - T003 green ⇒ FR-003, FR-003a, FR-004, FR-005.
  - T005 still green ⇒ FR-007 / SC-004 (exit codes did not move).
  - Assert the planned effect list contains **no** `WriteFile` for `ship-verdict.json` in cells 5 and 6
    (FR-006) — the MVU obligation noted in the header.
  If T005 goes red here, **stop**: research R3's `structuredClasses`-membership argument is unsound and
  the feature is a behavior change, not a re-attribution. Escalate rather than adjusting the test.

- [ ] **T010** [US1] Confirm `:527`'s `| None -> … Malformed` is now unreachable (the
  `(AlreadyCurrent, _)` arm is entered only when `parseShipView` returned `Ok`, and
  `shipVerdictEmission` re-derives from the same oracle at `HandlersShip.fs:205`). **Retain the arm**
  for match totality; add a comment saying so and why. Do not delete it — F# cannot prove the
  implication and the build breaks.

**Checkpoint**: US1 is independently shippable here. The headline defect in #188's title is fixed.

---

## Phase 4: User Story 2 (P2) — one underlying state, one severity

**Goal**: a stale `ship.json` reports the same severity whether or not the verdict is present.
**Independent test**: T004 goes green; matrix cells 7 and 8 emit equal severities.

- [ ] **T011** [US2] In `verdictDiags` (`:607-618`), split the `Missing` arm on the **source's** class:
  - `shClass = Stale` → `refreshStaleView verdictPath [ shipPath workId ]` (warning, FR-009)
  - otherwise → `refreshBlockedUpstreamView verdictPath (shipPath workId)` (error, FR-011)
  Leave the `Blocked`, `Stale`, and `Malformed` arms alone (FR-012). Reuse the existing constructors —
  **no new diagnostic id** (research R4/R6), so `docs/release/` baseline conformance is untouched.
  Comment why the verdict's diagnostic must consult its source's class while its *currency word* must
  not (FR-010): "the verdict is absent" does not by itself choose a severity.

- [ ] **T012** [US2] Run T004 (green) and re-run the cells 2/4/6 assertions: an absent verdict over a
  **non-stale** source must still emit `refresh.blockedUpstreamView` (error). FR-011 is the guardrail
  that keeps T011 from over-reaching into the genuinely-blocked states.

**Checkpoint**: US1 + US2 complete. Both machine-readable defects fixed.

---

## Phase 5: User Story 3 (P3) — a reader can tell a dead branch from a live one

- [ ] **T013** [US3] Comment the unreachable `| None -> … Missing` arm at `:528` (FR-013). State the
  invariant (`shClass = AlreadyCurrent` ⇒ `snapshot (shipPath workId) model = Some` ⇒ `textOf` cannot be
  `None`), name the establishing line (`:445`, `downstreamClass`'s `Some snap` branch), and record that
  after T008 the arm is *doubly* unreachable. Retain for match totality. **No behavior change; verified
  by inspection, not by a runtime test** — unreachable code cannot be asserted.

---

## Phase 6: Polish & verification

- [ ] **T014** [P] Full sweep: `dotnet test`. All green.

- [ ] **T015** [P] Regression evidence (research R5) — confirm these four tests were **not edited** and
  still pass, `git diff` clean on their line ranges:
  `refresh does not rewrite the verdict from a malformed ship json` (`:74`),
  `a fresh clone …` (`:116`),
  `an edited source makes the committed verdict stale, not blocked` (`:129`),
  `refresh does not write a verdict when both ship json and the verdict are missing` (`:149`).

- [ ] **T016** [P] FR-008 / SC-007: `refresh` twice against a valid, unchanged work item — byte-identical
  reports. Confirm no golden under `tests/FS.GG.SDD.Commands.Tests/goldens/readiness` moved
  (`git status`), which also keeps this branch's touch-set disjoint from the in-flight FS.GG.SDD#164.

- [ ] **T017** [P] FR-002 guardrail: an `analysis.json` in state S3 (valid JSON, invalid view) still
  reports `current`. Proves the stronger oracle did not leak beyond `ship.json`
  (quickstart Scenario C).

- [ ] **T018** Walk [quickstart.md](./quickstart.md) Scenarios A–D by hand against a real fixture,
  observing actual CLI output. Constitution VI prefers real filesystem/process evidence over
  transitive coverage; the CLI is the surface the operator actually reads.

- [ ] **T019** `git diff --stat` touches exactly `HandlersRefresh.fs` and `RefreshCommandTests.fs`
  (plus `specs/095-*`). Any other path means the touch-set widened and ADR-0021 requires re-running
  `scripts/fsgg-coord overlap FS.GG.SDD#188 FS.GG.SDD#164` and `… #171` before proceeding.

---

## Dependencies

```
T001 ──► T002 ──► T003 ─► T004 ─► T005        (same file; sequential)
                            │
                            ▼
         T006 ─► T007 ─► T008 ─► T009 ─► T010     [US1]  ◄── MVP boundary
                                   │
                                   ▼
                          T011 ─► T012              [US2]
                                   │
                                   ▼
                                 T013               [US3]
                                   │
                                   ▼
                    T014 ─┬─ T015 ─┬─ T016 ─┬─ T017   [P]
                          └────────┴────────┴─► T018 ─► T019
```

- **T002 before any source edit** — the pre-change exit codes are unrecoverable afterward.
- **T005 before T008** — the invariance lock must be green *first*, or it proves nothing when it stays
  green after.
- **T008 before T010** — `:527`'s unreachability is a consequence of T008, not independent of it.
- **T011 after T009** — T011's `Missing`-arm split reads `shClass`, whose values T008 changed.
- Phases 3 → 4 → 5 are strictly sequential: all edit `HandlersRefresh.fs`.

## Parallel opportunities

Genuinely thin — the feature is two files. Real `[P]`: **T014–T017**, four independent verification
sweeps after the source settles. Everything before them is a chain on one source file and one test file.

The stories are *independently shippable* (US1 alone fixes #188's headline; US2 alone fixes the severity
asymmetry) but not *concurrently implementable* — they edit adjacent regions of the same match
expression. Sequence them; do not fan out.

## Suggested MVP

**Phase 1 + 2 + 3 (T001–T010) = User Story 1.** It fixes both false facts named in #188's title
(`ship-verdict: malformed` about a well-formed file, and `ship: current` about a file that does not
parse), and it is the only story touching a *committed* artifact's reported status. US2 and US3 are
correct, cheap, and strictly narrower.

## Task count

| Story | Tasks | Ids |
|---|---|---|
| Setup | 2 | T001–T002 |
| Tests (red) | 3 | T003–T005 |
| **US1** (P1) | **5** | **T006–T010** |
| US2 (P2) | 2 | T011–T012 |
| US3 (P3) | 1 | T013 |
| Polish | 6 | T014–T019 |
| **Total** | **19** | |
