---

description: "Task list for feature 089 — Early-Stage Authoring Seeds"
---

# Tasks: Early-Stage Authoring Seeds

**Input**: Design documents from `/specs/089-early-stage-authoring-seeds/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

**Tests**: Required. Constitution Principle VI — behavior-changing code MUST include automated tests
that fail before the change and pass after. Real filesystem fixtures, no mocks.

**Tier**: Tier 2 (internal) overall — no `.fsi`, schema, diagnostic-code, or registry change.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallel-safe — no dependency on another incomplete task in this phase
- **[Story]**: `US1` (blocked-clarify skeleton), `US2` (safe re-run / retirement), `US3` (specify seed)

## Local build

This sandbox cannot restore against the committed lock file (research E1 — environment artifact, CI
is green). Append to every `dotnet build` / `dotnet test`:

```
-p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath="$SCRATCH/nolock.json"
```

**Never** commit a regenerated `packages.lock.json`. Verify with
`git status --short -- '*packages.lock.json'` before every commit.

---

## Phase 1: Baseline capture (Shared)

**Purpose**: pin the pre-change behavior so every later assertion is known to invert something real.

- [X] T001 Build the CLI and confirm the lock file is untouched, per `quickstart.md` Prerequisites.
- [X] T002 Reproduce the three baselines from `research.md` §D-Baseline in a scratch workspace: the
      meta seed text, the blocked `clarify` writing nothing, and the trap (`clarify` reporting
      `succeeded` with `blockingAmbiguities: 2` → `checklist` blocked, rc=1). Record actual output.
- [X] T003 [P] Inventory what must move with the behavior: `grep` for the meta story/scenario text,
      for `changedArtifacts: 0` blocked-clarify assertions, and for the placeholder strings across
      `tests/`, `docs/examples/lifecycle-artifacts/clarifications.md`, `docs/reference/authoring-contracts.md`,
      and `.fsgg/early-stage-guidance.md`. Note that `.fsgg/early-stage-guidance.md` is pinned by a
      drift-guard test (`tests/FS.GG.SDD.Commands.Tests/EarlyStageGuidanceContractTests.fs`) and
      `docs/reference/authoring-contracts.md` by `AuthoringDocsContractTests.fs` — if either
      documents the seed or skeleton wording, it moves in Phase 6, not silently.

---

## Phase 2: Foundational — the H-4 carve-out (Blocking prerequisite)

**Purpose**: `US1` cannot write anything until a blocked run is permitted to. Nothing else depends
on this, and nothing else may use it.

- [X] T004 In `src/FS.GG.SDD.Commands/CommandWorkflow/Prerequisites.fs`, generalize `runHandler` into
      `runHandlerWithBlockedSeed`, whose body continuation returns a 5-tuple ending in
      `blockedSeedEffects: CommandEffect list` (data-model §3). Change the effect gate to
      `if hasBlocking then blockedSeedEffects else writeEffects @ generatedEffects`, with a comment
      naming it the H-4 carve-out for feature 089 and stating that `generatedEffects` stay gated.
- [X] T005 Keep `runHandler` as a thin wrapper over `runHandlerWithBlockedSeed` that supplies `[]`,
      so the other eight handlers compile untouched. Confirm with
      `grep -rn "runHandler\b" src/` that only `computeClarifyPlan` moves to the new function.
- [X] T006 Add a guard test in `tests/FS.GG.SDD.Commands.Tests/CommandWorkflowTests.fs`: a blocked
      handler that returns `[]` for `blockedSeedEffects` still yields zero effects (the invariant
      holds for every command except the one carve-out).

**Checkpoint**: the solution builds; every existing test passes; no behavior has changed yet.

---

## Phase 3: US1 — A blocked clarify leaves an editable, truthful skeleton (P1) 🎯 MVP

**Goal**: `clarify` blocked on unanswered ambiguities writes a `clarifications.md` that tells the
truth. **Independent test**: `quickstart.md` Scenario 2.

### Tests first (must fail against `main`)

- [X] T007 [P] [US1] In `tests/FS.GG.SDD.Artifacts.Tests/ClarificationArtifactTests.fs`, assert the
      skeleton from `contracts/clarification-skeleton.md` parses under `parseClarificationFacts` and
      that `BlockingAmbiguityCount` equals the number of unresolved declared ambiguities (K5/FR-009).
- [X] T008 [US1] In `tests/FS.GG.SDD.Commands.Tests/ClarifyCommandTests.fs`, assert a blocked
      `clarify` (two declared ambiguities, no answers) writes `work/<id>/clarifications.md`, reports
      exactly one changed artifact, and keeps `outcome: blocked` with the identical diagnostic set
      and exit code as before (FR-006/FR-010, SC-001/SC-003).
- [X] T009 [US1] Assert the written skeleton has front-matter `status: needsAnswers`, one `CQ-###`
      per declared ambiguity, and every unanswered ambiguity under Remaining Ambiguity marked
      `blocking` — and never the `No blocking ambiguity remains.` sentinel (FR-007/FR-008, SC-002).
- [X] T010 [US1] Assert a blocked `clarify` still writes **no** `readiness/<id>/work-model.json` —
      the carve-out passes only the seed (plan §Complexity Tracking, `quickstart.md` Scenario 2).
- [X] T011 [P] [US1] Assert `clarify` does **not** write a skeleton when the source `spec.md` fails
      to parse (FR-012), and does not write one when a `clarifications.md` already exists in each of
      the unsafe-overwrite / malformed-front-matter / mismatched-work-id states, with the file's
      digest unchanged (FR-011, SC-006, `quickstart.md` Scenario 5).
- [X] T012 [P] [US1] Assert two blocked runs on unchanged inputs produce a byte-identical skeleton
      (FR-015, SC-010).

### Implementation

- [X] T013 [US1] In `EarlyStageAuthoring.fs`, make `clarificationTemplate` truthful: derive `status`
      and the Remaining Ambiguity body from the declared ambiguities carrying **no** concrete decision
      and no accepted deferral, rather than from the presence of a `stillOpen` answer (data-model §2,
      K1/K2). Keep a `stillOpen` answer's existing `renderRemainingLine` text; use the generic
      `Unanswered — …` explanation only for an ambiguity with no answer at all (K4, research D5), so
      the new rendering is a strict superset of the old.
- [X] T014 [US1] In `EarlyStageAuthoring.fs`, add the fourth `seedText: string option` result to
      `clarificationDiagnosticsTextAndSummary` per the table in data-model §4. Populate it only on the
      file-absent path, only when blocked, and only when the rendered skeleton itself parses. Leave
      the existing `text` result byte-for-byte as it is today so the blocked run's reported
      `GeneratedViewState` does not change (research D8).
- [X] T015 [US1] In `HandlersEarly.fs`, switch `computeClarifyPlan` to `runHandlerWithBlockedSeed` and
      return `blockedSeedEffects` = `[ WriteFile(clarificationPath workId, seedText, AuthoredSource) ]`
      when `seedText` is `Some`, else `[]`. Write kind stays `AuthoredSource`; no-clobber is guaranteed
      by construction (research D7). **Amended during implementation**: the planned `CreateDirectory`
      was dropped — the interpreter's `WriteFile` already creates parent directories, and including it
      added a second (`noChange`, directory) entry to `changedArtifacts`, contradicting FR-010's
      one-changed-artifact pin. Exactly one effect crosses the carve-out.

**Checkpoint**: `quickstart.md` Scenario 2 passes. US1 is independently shippable — but see the
Phase 4 warning before believing it.

---

## Phase 4: US2 — Re-running a blocked clarify is safe and still blocks (P1)

> ⚠️ **Do not ship Phase 3 without Phase 4.** Verified in research D4: a skeleton whose ambiguities
> are listed as blocking cannot be resolved by `clarify`, because the existing-file path only appends
> to Remaining Ambiguity. Shipping US1 alone converts "the operator hand-authors the file" into
> "`clarify` says succeeded, `checklist` blocks two stages later."

**Goal**: answering the skeleton actually unblocks the lifecycle. **Independent test**:
`quickstart.md` Scenarios 3 and 4.

### Tests first (must fail against `main`)

- [X] T016 [US2] In `ClarifyCommandTests.fs`, the regression test for the trap: from a skeleton, run
      `clarify --input` answering **every** declared ambiguity; assert `blockingAmbiguities: 0`, that
      Remaining Ambiguity holds only the `No blocking ambiguity remains.` sentinel, and that a
      subsequent `checklist` run **succeeds** with `next: fsgg-sdd plan` and rc=0 (FR-018, SC-005).
      Against `main` this fails: `clarify` reports `succeeded` with `blockingAmbiguities: 2` and
      `checklist` blocks at rc=1.
- [X] T017 [P] [US2] Assert a **partially** answered run retires only the answered ambiguities' lines,
      leaves the unanswered ones blocking, and still blocks (FR-018, SC-002, spec Edge Cases).
- [X] T018 [P] [US2] Assert operator-authored prose on an unresolved ambiguity's Remaining Ambiguity
      line is preserved verbatim across a `clarify` re-run (FR-014).
- [X] T019 [P] [US2] Assert a re-run with no new answers still blocks with the same diagnostic and does
      not duplicate the derived `CQ-###` questions (FR-013, SC-004, `quickstart.md` Scenario 4).
- [X] T020 [P] [US2] Assert no empty-state placeholder survives beside a real entry in the Questions,
      Answers, Decisions, or Accepted Deferrals sections, and that the
      `No blocking ambiguity remains.` sentinel is **never** treated as a placeholder (FR-019, SC-012,
      R2).

### Implementation

- [X] T021 [US2] In `EarlyStageAuthoring.fs`, add a post-append pass to `appendClarificationAnswers`
      implementing R1/FR-018: drop each Remaining Ambiguity line whose `AMB-###` id now carries a
      concrete decision or accepted deferral; if the section is left with no content line, insert the
      `No blocking ambiguity remains.` sentinel. Never remove the sentinel itself; never touch a line
      for a still-unresolved ambiguity.
- [X] T022 [US2] Add the second post-append pass implementing R2/FR-019: in the Questions, Answers,
      Decisions, and Accepted Deferrals sections only, drop the empty-state placeholder once the
      section holds a real content line. Enumerate the four placeholder strings from
      `contracts/clarification-skeleton.md` R2; exclude the sentinel.

**Checkpoint**: `quickstart.md` Scenarios 3 and 4 pass. The skeleton is now usable end-to-end.

---

## Phase 5: US3 — The specify seed reads as the feature, not the process (P2)

**Goal**: a feature-shaped `US-001`/`AC-001`. Independent of Phases 2–4; may run in parallel with
them. **Independent test**: `quickstart.md` Scenario 1.

### Tests first (must fail against `main`)

- [X] T023 [P] [US3] In `tests/FS.GG.SDD.Commands.Tests/SpecifyCommandTests.fs`, assert a `specify`
      run supplying no story seeds a `US-001` matching `As a <user>, I can <capability>` whose text
      contains none of `charter`, `specify`, `spec.md`, `stable ids` (case-insensitive) — FR-001/FR-002,
      SC-007 — and a `AC-001` Given/When/Then carrying `[US-001] [FR-001]` (FR-003).
- [X] T024 [P] [US3] Assert the ids and cross-references are unchanged: the `- US-001 (P1): ` prefix,
      the `- AC-001 [US-001] [FR-001]: ` prefix, and the `FR-001` line's
      `(Stories: US-001; Acceptance: AC-001)` trailer (FR-004, SC-008); and that a `checklist` run
      over the seeded spec computes the same FR→AC coverage (SC-008).
- [X] T025 [P] [US3] Assert an invocation that **does** supply `story:`/`acceptance:` uses the author's
      text verbatim with no seed substituted (FR-005, SC-009, `quickstart.md` Scenario 6).
- [X] T026 [P] [US3] Assert grammar safety (FR-017): a user value containing an id-shaped token
      (`FR-002`) and one containing a colon and brackets both produce a spec that parses and introduces
      no spurious `AMB`/`CQ`/`FR`/`US`/`AC` cross-reference. Assert `decapitalizeFirst` leaves `MP4 …`
      alone (S3) and `trimTrailingPeriod` never yields `..` (S4).
- [X] T027 [P] [US3] Assert two `specify` runs on unchanged inputs produce a byte-identical `spec.md`
      (FR-015, SC-010).

### Implementation

- [X] T028 [US3] In `EarlyStageAuthoring.fs`, add the three total helpers from `contracts/specification-seed.md`:
      `trimTrailingPeriod`, `decapitalizeFirst` (S3), and `neutralizeIds` (S5, rewriting
      `[A-Z]{2,3}-\d{3,}` by replacing the hyphen with a space). Apply them to the seeded lines only —
      never to author-supplied scope/requirement/non-goal text.
- [X] T029 [US3] In `specificationTemplate`, replace the meta story fallback with
      `As a user, I can {cap}.` and the meta acceptance fallback with
      `Given {shown} is available, when the user exercises it, then they can {cap}.`, derived from
      `intent.UserValue` and `requestTitle` (data-model §1). Leave the scope, requirement, and non-goal
      fallbacks untouched (research D2 — the first two are dead code, the third is a genuine default).

**Checkpoint**: `quickstart.md` Scenarios 1 and 6 pass.

---

## Phase 6: Documentation, drift guards, and polish

- [-] T030 SKIPPED — verified no-op. `docs/examples/lifecycle-artifacts/clarifications.md` renders a fully
      *clarified* artifact; no section shape this feature changes appears in it. Left untouched.
      (Original: update `docs/examples/lifecycle-artifacts/clarifications.md` if it renders a section shape
      this feature changes.) It is referenced by the `missingClarificationAnswer` remediation pointer,
      so it must show the skeleton the operator will actually receive.
- [-] T031 SKIPPED — verified no-op. `docs/reference/authoring-contracts.md` already documents the clarify
      `status`→`needsAnswers` default; nothing to add. `AuthoringDocsContractTests` green.
      (Original: update it if its clarify row or `status` vocabulary
      needs to record `needsAnswers` as the blocked-skeleton state.) `AuthoringDocsContractTests.fs`
      pins this file — run it.
- [-] T032 SKIPPED — verified no-op. `grep` confirms `.fsgg/early-stage-guidance.md` documents neither the
      seed nor the skeleton wording, so the pinned mirror does not move. `EarlyStageGuidanceContractTests` green.
      (Original: update it **only if** it documents the seed or skeleton
      wording.) It is a read-only mirror pinned by `EarlyStageGuidanceContractTests.fs`; the guard test
      and the file move together or not at all.
- [X] T033 [P] Refresh any golden/snapshot fixture under `tests/FS.GG.SDD.Commands.Tests/goldens/`
      that carries the old meta seed or a `changedArtifacts: 0` blocked-clarify report. Per spec
      Assumptions, these updates **are** the evidence the behavior changed — review each diff, do not
      bulk-regenerate.
- [X] T034 Run `dotnet test FS.GG.SDD.sln` (with the lock bypass) and confirm green, then confirm
      `git status --short -- '*packages.lock.json'` prints nothing.
- [X] T035 Run `fsgg-sdd surface --check` and the repo's `PublicSurface.baseline` test. Expect **no**
      surface movement: every touched module is `internal` with no `.fsi` (plan, Constitution III).
      A surface diff here means the change leaked into public API and must be reverted.
- [X] T036 Walk `quickstart.md` Scenarios 1–6 by hand against the built CLI and paste the actual
      output into the PR body as evidence (Principle VI: real fixtures, disclosed evidence).

---

## Dependencies

- **Phase 2 (T004–T006)** blocks **T015** only. Nothing else may consume `blockedSeedEffects`.
- **T013** (truthful template) blocks **T014** (which seeds that template's output).
- **T014** blocks **T015**.
- **Phase 4 (T021, T022)** depends on Phase 3 shipping a skeleton to retire lines from, but its tests
  (T016–T020) can be written first against a hand-placed fixture.
- **Phase 5 (US3)** is fully independent of Phases 2–4 and may proceed in parallel.
- **Phase 6** depends on all implementation phases.

## Parallel opportunities

- T007, T011, T012 (US1 tests) — distinct assertions, distinct fixtures.
- T017, T018, T019, T020 (US2 tests).
- T023–T027 (US3 tests) — all in `SpecifyCommandTests.fs`; parallel to author, sequential to commit.
- **Phase 5 in its entirety** runs concurrently with Phases 2–4 (different functions, no shared state).

## Task count per story

| Story | Tests | Implementation | Total |
|---|---|---|---|
| Foundational (carve-out) | 1 | 2 | 3 |
| US1 — blocked skeleton | 6 | 3 | 9 |
| US2 — safe re-run / retirement | 5 | 2 | 7 |
| US3 — specify seed | 5 | 2 | 7 |
| Baseline + docs/polish | — | — | 10 |

## Suggested MVP scope

**US1 + US2 together** — not US1 alone. They are jointly the minimum shippable increment: US1 writes
the skeleton and US2 makes it resolvable. Research D4 shows that US1 alone regresses the operator
experience it is meant to fix. US3 is a genuinely independent, lower-priority slice and could ship in
a separate PR; here it rides along because both defects trace to the same issue and the same file.

## Elmish/MVU applicability

The feature is MVU-bearing: `blockedSeedEffects` is a new path through which a pure `update`-side plan
emits an `Effect` interpreted at the existing edge. Coverage:

- **Effect emission (pure)** — T008/T010 assert *which* effects a blocked `clarify` plan yields (the
  skeleton write, and crucially *not* the generated-view write), asserted on the plan, not on disk.
- **Pure transition** — T013/T014 change only pure rendering and plan computation; T007 exercises the
  parser as a pure function over text.
- **Interpreter boundary** — unchanged. `WriteFile`/`CreateDirectory` already exist; no new effect
  constructor, so `CommandEffects.fs` is not touched.
- **Real interpreter evidence** — T036 runs the real CLI against a real filesystem (safe: a scratch
  workspace).

Principle IV (idiomatic simplicity) needs no waiver: two rendering functions, one set-membership
filter, one list-of-lines transform. The single structural deviation — the H-4 carve-out — is
justified in `plan.md` §Complexity Tracking.


---

## Execution record (2026-07-08)

**Status**: implementation complete; 1,102 tests green across five suites
(Commands 635, Artifacts 227, Cli 127, Contracts 87, Validation 26). `fantomas --check` clean.
`packages.lock.json` untouched. No `.fsi` / `docs/api-surface/` movement (Tier 2 held).

**Scope grew during implementation, twice, both times because running the code disproved a
written assumption:**

1. **FR-018/FR-019** (anticipated in `plan.md`, from research D4) — the skeleton is unresolvable
   without retiring a resolved ambiguity's Remaining Ambiguity line.
2. **FR-020** (found while implementing) — `clarify` never rewrites front matter, so a fully
   answered artifact kept `status: needsAnswers` beside `No blocking ambiguity remains.` Scoped to
   rewriting only the literal value the tool itself writes.
3. **FR-021** (found by *running* the first implementation — the sharpest finding) — the skeleton's
   generated explanation *"provide a concrete decision, an accepted deferral, or an explicit
   still-open note"* was classified by `parseRemainingAmbiguity` as an **accepted deferral**, because
   that parser scans the line's prose for `defer` / `non-blocking`. The skeleton parsed with
   `BlockingAmbiguityCount: 0` and **`checklist` passed with both ambiguities unanswered** — the
   feature silently disabled the gate it exists to hold shut. Reworded; pinned in both directions by
   `ClarificationArtifactTests` and by an end-to-end `checklist`-blocks assertion.

**Assumptions corrected against the running CLI** (all recorded in `research.md`):

- D1 — the seed cannot derive from the *charter* title; `requestTitle` reads the invocation's
  `--title`, else the humanized work id. The **user value** carries the meaning.
- D3 — a blocked run writes nothing because of `runHandler`'s effect gate, not because of the
  `None` text the issue points at. The sibling blocked path returns `Some text` and still writes nothing.
- D10 — a *partially* answered `clarify` persists nothing. Pre-existing, correct, unchanged. An
  earlier draft of the spec claimed it "records the answers given"; that was inference, not observation.
- SC-007 — the no-process-vocabulary rule binds the seed's *own* phrasing, not author-supplied text
  it interpolates. Caught by a test failing on the fixture's own title, "Specify Command".

**Test evidence** (Principle VI). No synthetic evidence was used; every test drives real command
handlers over a real temporary filesystem, and `quickstart.md` Scenarios 1–6 were walked by hand
against the built CLI (T036). Behavior-changing assertions that inverted:

| Test | Before | After |
|---|---|---|
| `clarify missing answer blocks …` | asserted the artifact is **absent** | asserts the skeleton is **written**, outcome/diagnostic unchanged |
| `clarify unknown reference blocks …` | asserted absent | asserts seeded, and that the bogus `AMB-999` is **not** recorded |
| `ReadinessViewGoldenTests` ×4 | old meta-seed digests | new digests — **diff verified digest-only**, no structural change |

**New guards**: `BlockedEffectGateTests` pins the carve-out's blast radius (blocked `specify` /
`checklist` / `plan` / `tasks` still write nothing; blocked `clarify` writes the seed and **no**
`work-model.json`).


## Post-implementation code review (high effort)

Six defects found in 089's own first implementation, every one reproduced against the built CLI
before fixing and covered by a regression test (`research.md` D12):

| # | Defect | Harm | Regression test |
|---|---|---|---|
| 1 | Retirement matched any AMB id the line *mentions*, not its subject | Deleted an operator's explanation of a still-blocking ambiguity (FR-014) | `retirement keeps a still-open line that merely mentions a resolved ambiguity` |
| 2 | `retireStaleSentinel` composed before the append, not after | Sentinel coexisted with a blocking line (K3) | `a new blocking line retires the nothing-remains sentinel` |
| 3 | `neutralizeIds` case-sensitive while every parser is IgnoreCase | Seed leaked a phantom ambiguity: `unresolvedAmbiguityCount: 3` vs "No material ambiguities recorded." | `specify seed neutralizes lowercase id-shaped tokens too` + `…a capitalized id-shaped token…` |
| 4 | `transformSectionBody` stripped operator blank lines, even on no-op passes | Reflowed authored prose on every `clarify --input` (FR-014) | `retirement preserves operator blank lines while dropping the placeholder` |
| 5 | Retirement could not reach a question-id-only blocking line | Stale line survived its own resolution | covered by the anchor-id fix + `retireResolvedRemaining` question fallback |
| 6 | `List.forall isSentinelLine` vacuously true on `[]` | Emptied section flipped `status: clarified` on no evidence | `an emptied remaining-ambiguity section does not flip the status` |

Final: **1,108 tests green** (Commands 641, Artifacts 227, Cli 127, Contracts 87, Validation 26).
`fantomas --check src/ tests/` clean. `packages.lock.json` untouched.
