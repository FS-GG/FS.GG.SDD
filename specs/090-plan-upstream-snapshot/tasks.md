# Tasks: Plan Upstream Snapshot

**Feature branch**: `090-plan-upstream-snapshot`
**Spec**: `specs/090-plan-upstream-snapshot/spec.md`
**Plan**: `specs/090-plan-upstream-snapshot/plan.md`
**Tracks**: FS-GG/FS.GG.SDD#163 (epic #159)

## Status Legend

- `[ ]` — pending
- `[X]` — done with real evidence (or with synthetic evidence disclosed per Principle V)
- `[-]` — skipped (with written rationale)

Never mark a failing task `[X]`; never weaken an assertion to green a build.

## Task Annotations

- **[P]** — parallel-safe (no dependency on another incomplete task in this phase)
- **[US1]**..**[US4]** — user-story scope
- Feature tier is **T1** throughout; per-task tier annotations are omitted (they all match).

Phases run in sequence; tasks within a phase may run in parallel.

## Touch-set discipline (intra-repo parallel work)

FS.GG.SDD#174 runs concurrently and owns `HandlersEarly.fs`, `Prerequisites.fs`,
`EarlyStageAuthoring.fs`, and `tests/FS.GG.SDD.Commands.Tests/goldens/**`. **No task below may
edit those paths.** If one appears to require it, stop, widen the issue's `Paths:` line, and re-run
`scripts/fsgg-coord overlap FS.GG.SDD#174 FS.GG.SDD#163` before proceeding.

---

## Phase 1: Setup

- [X] T001 Record feature Tier (T1), affected layer (Commands + Cli), public-API impact
  (`CommandRequest.AcceptUpstream`, `stalePlanSnapshot` diagnostic id), Elmish/MVU applicability
  (yes — the `runHandler` effect gate is the boundary), and evidence obligations — in
  `specs/090-plan-upstream-snapshot/plan.md` Constitution Check. *(Satisfied by the plan commit; verify.)*

---

## Phase 2: Foundation (public surface first — Constitution I & III)

- [X] T002 Add `AcceptUpstream: bool` to the `CommandRequest` record in
  `src/FS.GG.SDD.Commands/CommandTypes.fsi`, then `src/FS.GG.SDD.Commands/CommandTypes.fs`.
- [X] T003 [P] Declare `stalePlanSnapshot: path:string -> changedPaths:string list -> Diagnostic`
  in `src/FS.GG.SDD.Commands/CommandReports.fsi`.
- [X] T004 Implement `stalePlanSnapshot` in
  `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs` using `errorDiagnostic`
  (NOT `markToolDefect` — a stale upstream is workspace state, so the blocked exit stays 1).
  `RelatedIds` = the changed paths, already sorted by the caller. Re-export from
  `src/FS.GG.SDD.Commands/CommandReports.fs`. After T003.
- [-] T005 **Skipped.** `RemediationPointers` is the *authoring-grammar* docs registry; its own header
  says it "Excludes pure sequencing/config/tool-defect blocks." `stalePlanSnapshot` is a sequencing
  block, not a grammar defect, and its registry entry would have to cite a docs example that does not
  describe it. FR-010 is instead served by (a) the diagnostic's own `Correction` ("…re-run with
  --accept-upstream") and (b) the new `plan.acceptUpstream` NextAction in
  `CommandReports/NextActionRouting.fs`, which is where sequencing recoveries already live.
- [X] T006 [P] Parse the flag: `AcceptUpstream = hasFlag "--accept-upstream" rest` in the
  `CommandRequest` construction in `src/FS.GG.SDD.Cli/Program.fs`. After T002.
- [X] T007 [P] Add the `--accept-upstream` row to `plan`'s help in
  `src/FS.GG.SDD.Commands/CommandHelp.fs` (+ `.fsi` if the surface moves).

**Phase 2 verification**: `dotnet build` succeeds; no behavior change yet.

---

## Phase 3: [US1] A backward spec edit no longer corrupts the plan (P1 — MVP)

Failing-first, per Constitution VI. Write T008 before T010; it must fail on `origin/main`.

- [X] T008 [US1] Add the regression test to `tests/FS.GG.SDD.Commands.Tests/PlanCommandTests.fs`
  (contract cell **C2**, SC-002): drive a real work item through `plan`, edit `spec.md`, re-run
  `plan`, assert `plan.md` is **byte-identical**, `outcome = Blocked`, `changedArtifacts = 0`,
  exit 1, and exactly one `stalePlanSnapshot` whose `relatedIds = ["work/<id>/spec.md"]`.
  *Must fail before T010.*
- [X] T009 [US1] [P] Add cells **C1** (clean re-run → `noChange`), **C7** (two sources changed →
  both named, ordinally sorted), **C11** (snapshot entry with no digest → not stale, FR-016),
  **C12** (recorded source now missing → the existing `missing…Prerequisite` error, **no**
  `stalePlanSnapshot`), and **C14** (no `PD-###` ever appended) to `PlanCommandTests.fs`.
- [X] T010 [US1] In `src/FS.GG.SDD.Commands/CommandWorkflow/ChecklistPlanAuthoring.fs`:
  add `changedPlanSourcePaths` — the sibling of `planSourceSnapshotStale` returning the
  ordinally-sorted changed paths under the **identical** predicate (an absent recorded digest and an
  absent current digest are both not-stale; research R5).
- [X] T011 [US1] Equivalence pinned two ways: both functions now share one `currentPlanSourceDigests`
  helper and identical match arms, and the test `changed source paths agree with the staleness
  predicate` exercises both directions (stale → non-empty, accepted → empty) through the command surface.
- [X] T012 [US1] **Delete** `appendStalePlanDecision` from `ChecklistPlanAuthoring.fs` and remove its
  call site in `planDiagnosticsTextAndSummary` (FR-001). The re-run path now returns the existing,
  unmutated text when stale.
- [X] T013 [US1] In `planDiagnosticsTextAndSummary`, replace the `stalePlanDecision` warning with the
  `stalePlanSnapshot` error (FR-002). The `runHandler` effect gate then delivers FR-003's zero-write
  guarantee — **do not** add a suppression branch in `HandlersEarly.fs`. After T004, T010, T012.

**US1 checkpoint**: T008/T009 green. Exercised end-to-end via `quickstart.md` §2 against the real
CLI — not unit tests alone (vertical-slice rule).

---

## Phase 4: [US2] One gesture accepts the new upstream (P1)

- [X] T014 [US2] Add cells **C3** (accept → snapshot refreshed, no synthesized `stale:` line, no
  pre-existing line altered, `changedArtifacts = 1`, exit 0), **C4** (accept on a current snapshot → `noChange`, no flag-related diagnostic, FR-005),
  **C5** (creation path identical with and without the flag, FR-007), and **C6** (accept + an
  unrelated blocking diagnostic → still blocked, zero writes, FR-006) to `PlanCommandTests.fs`.
- [X] T015 [US2] SC-002 invariant test added — **and the invariant itself was corrected mid-flight.**
  The original wording ("only `## Source Snapshot` may differ") was wrong: `plan` has always appended
  derived rows for genuinely-new upstream ids (`plannedPlanEntries` diffs against existing facts; the
  pre-existing test `plan appends safe missing requirement …` asserts `FR-002` reaching `plan.md`).
  That append is intended behavior, not the defect. The shipped invariant: on the blocked path
  `plan.md` is byte-identical; under `--accept-upstream` no pre-existing line is altered or removed and
  no `PD-###` is ever synthesized. spec.md FR-004/SC-002, the contract, and data-model.md were amended.
- [X] T016 [US2] Implement `refreshPlanSnapshot workId specText clarificationText checklistText text`
  in `ChecklistPlanAuthoring.fs` — `replaceSectionBody "Source Snapshot" (sourceSnapshotLines …)`,
  mirroring `rederiveChecklist`. Touches no other section.
- [X] T017 [US2] Wire `request.AcceptUpstream` through `planDiagnosticsTextAndSummary`: when stale
  **and** the flag is set, return `refreshPlanSnapshot`-applied text and emit **no**
  `stalePlanSnapshot`. FR-006 needs no special-casing — an unrelated `DiagnosticError` still closes
  the effect gate. After T013, T016.

**US2 checkpoint**: T014/T015 green; `quickstart.md` §4 and §5 walked against the real CLI.

---

## Phase 5: [US3] Downstream stages stop inheriting a stale plan (P2)

- [X] T018 [US3] Add cells **C8** (`tasks` blocks), **C9** (`analyze` blocks), **C10**
  (`tasks --accept-upstream` still blocks — the flag is `plan`'s gesture alone), and **C13** (an
  operator-authored `stale:` decision line still yields `failedPlanPrerequisite`, FR-009) to
  `PlanCommandTests.fs`.
- [X] T019 [US3] In `planPrerequisiteDiagnosticsTextSummaryAndFacts` (`ChecklistPlanAuthoring.fs`),
  read the three upstream texts from `model` via `snapshot (specPath workId) model` etc. — as it
  already reads `plan.md` — and emit `stalePlanSnapshot` when stale, **ignoring** `AcceptUpstream`
  (FR-008). **No signature change**, therefore no edit to `Prerequisites.fs`. After T010.
- [X] T020 [US3] Confirm the `failedPlanPrerequisite "Plan contains stale decisions."` branch is
  retained (FR-009) and now reachable only from authored prose.

**US3 checkpoint**: T018 green; `quickstart.md` §3 walked.

---

## Phase 6: [US4] The authoring window is announced before it closes (P3 — droppable)

> **Sequenced last and explicitly droppable.** FR-011 adds a `DiagnosticInfo` to *every successful*
> `plan` report, and command diagnostics flow into `generatedViewPlan`. If it churns any file under
> `tests/FS.GG.SDD.Commands.Tests/goldens/` — inside **#174's** touch-set — do **not** merge across
> the overlap: mark T021–T023 `[-]` with that rationale, open a follow-up issue under epic #159, and
> ship US1–US3. Decide empirically.

- [X] T021 [US4] Add the advisory constructor (`DiagnosticInfo`, modeled on
  `agents.earlyStageGuidance`) to `DiagnosticConstructors.fs` + `CommandReports.fsi`.
- [X] T022 [US4] Emit it from the successful `plan` path in `ChecklistPlanAuthoring.fs` (FR-011).
- [X] T023 [US4] Test that it changes neither exit code, `outcome`, nor `changedArtifacts` versus the
  pre-feature behavior for the same inputs, and that exactly one is emitted.
- [X] T024 [US4] **Gate cleared — US4 ships.** Ran the full suite with FR-011 in place: zero golden
  churn, zero collateral test failures. The only files that moved were `PublicSurface.baseline`
  (two additive lines, no removals) and this feature's own test file. No file under
  `tests/**/goldens/` was touched, so nothing crossed the #174 boundary.

---

## Phase 7: Integration & Polish

- [X] T025 Run the full suite: `dotnet build && dotnet test`. Green, with no golden churn outside the
  declared touch-set. Investigate — do not re-baseline — any golden that moves.
- [X] T026 [P] Walk `quickstart.md` §1–§6 end-to-end against the real CLI on a scratch workspace.
  Capture the observed `outcome`/exit/`changedArtifacts` for §2 and §4 as the vertical-slice evidence
  for US1/US2.
- [X] T027 [P] Determinism (FR-014, SC-005): `plan --json` twice on both the blocked path and the
  `--accept-upstream` path; assert byte-identical.
- [X] T028 [P] `dotnet run --project src/FS.GG.SDD.Cli -- validate --markdown` — no new non-passing
  cell (SC-005).
- [X] T029 [P] `PublicSurface.baseline` regenerated via `FSGG_UPDATE_BASELINE=1`. Diff is purely
  additive: `+CommandReports.planAuthoringWindow`, `+CommandReports.stalePlanSnapshot`. No removals,
  so no public surface was broken.
- [-] T030 **Skipped with rationale.** `.claude/skills/fs-gg-sdd-plan/SKILL.md` documents the command's
  purpose and shows `fsgg-sdd plan --work <id>`; it does not enumerate flags, so there is no flag list
  to keep aligned. Editing it would pull `SeededSkills.fs`, the `.codex`/`.agents` mirrors, and the
  `skill-manifest.json` sha256 drift guard into this diff for no contract gain. Agent/human parity
  (Constitution VII) is preserved because the recovery travels in the diagnostic's `Correction` and the
  `plan.acceptUpstream` NextAction — the one report both read — and `plan --help` lists the flag.
  Follow-up if the skills ever grow a flag reference.
- [X] T031 Self-review the diff against the 16 FRs and the 14 contract cells; confirm no edit landed
  in `HandlersEarly.fs`, `Prerequisites.fs`, `EarlyStageAuthoring.fs`, or `goldens/**`.

---

## Dependencies

- Phase 2 (T002–T007) precedes all behavior work.
- T010 precedes T013 and T019 (both consume `changedPlanSourcePaths`).
- T012 precedes T013 (delete the injection before rerouting the diagnostic).
- T016 precedes T017.
- T013 precedes T017 (the accept path branches off the block path).
- T024 gates whether Phase 6 ships at all.

## Parallel opportunities

- Phase 2: T003/T005/T006/T007 are `[P]` once T002 lands.
- Phase 3: T009 runs alongside T008.
- Phase 7: T026–T030 are all `[P]`.
- US3 (T018/T019) is independent of US2 (T014–T017) once T010 exists — the two touch different
  functions in the same file, so sequence the edits but not the design.

## Task count per story

| Scope | Tasks |
|---|---|
| Setup | 1 (T001) |
| Foundation | 6 (T002–T007) |
| US1 (P1) | 6 (T008–T013) |
| US2 (P1) | 4 (T014–T017) |
| US3 (P2) | 3 (T018–T020) |
| US4 (P3, droppable) | 4 (T021–T024) |
| Integration & Polish | 7 (T025–T031) |
| **Total** | **31** |

## Suggested MVP scope

**US1 + US2** (T001–T017). US1 alone stops the corruption but leaves the operator hand-editing three
digests to escape a *blocked* command rather than a *warned* one — the spec says so explicitly. US3
closes the skip-`plan` hole and should ship in the same PR. US4 is preventive and droppable.
