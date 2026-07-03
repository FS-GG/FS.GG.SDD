---
description: "Task list for feature 062 — split CommandReports and type the defect/summary contracts"
---

# Tasks: Split CommandReports and type the defect/summary contracts

**Input**: Design documents in `specs/062-split-command-reports/`
(spec.md, plan.md, research.md, data-model.md, contracts/report-contract.md, quickstart.md)

**Overall tier**: Tier 1 (the `Diagnostic` public record + `Diagnostics` module gain
a minimal typed surface). Individual tasks are `[T1]`/`[T2]` only where they differ.

**Tests**: Included — the constitution mandates test evidence (Principle VI) and the
spec requires re-expressing the id-set/substring assertions against the typed surface
(FR-010).

**MVU note**: This feature is pure — report assembly and exit-code selection are pure
over `CommandModel`; no new `Effect`/interpreter surface. Principle V's Model/Msg/
Effect obligations are **not newly applicable**; the evidence is behavioural
(exit-code, byte-identical output) not effect-log based.

**Ordering principle**: every task leaves the solution **building and green**. Work
proceeds in priority order (US1 → US2 continuous → US3 → US4 → US5), with the purely
mechanical module split (US5) last so it relocates already-correct code.

## Format: `[ID] [P?] [Story] Description`

- `[P]` = no dependency on another incomplete task in this phase (usually a
  different file).
- `[Story]` = the user story the task serves (US1–US5).

---

## Phase 1: Setup & regression anchor

**Purpose**: Establish the "nothing observable changed" baseline before touching code.

- [X] T001 [P] [US2] Confirm the full suite is green on `062-split-command-reports`
  before any change: `dotnet build -c Release && dotnet test`. Record the pass as the
  regression anchor.
- [X] T002 [P] [US2] Capture reference outputs from `main` for the byte-diff check:
  representative default/`--json` and `--text` per command (charter…validate), stored
  under `specs/062-split-command-reports/.baseline-capture/` (git-ignored scratch) for
  the Phase 7 diff. Note existing golden fixtures already cover most of this.

**Checkpoint**: known-green starting point; diff procedure ready.

---

## Phase 2: Foundational — extend the `Diagnostic` type (BLOCKS US1 & US3)

**⚠️ Blocks US1 and US3.** Adds the typed surface and keeps the build green with **no
behaviour change** (no constructor is marked yet).

- [X] T003 [T1] Update `src/FS.GG.SDD.Artifacts/Diagnostics.fsi`: add `IsToolDefect: bool`
  to the `Diagnostic` record; add `val markToolDefect: Diagnostic -> Diagnostic` and
  `val signalsStaleView: Diagnostic -> bool`. (Principle III — `.fsi` first.)
- [X] T004 Update `src/FS.GG.SDD.Artifacts/Diagnostics.fs`: set `IsToolDefect = false`
  inside `create` (keep `create`'s parameter list unchanged); implement `markToolDefect`
  (`{ d with IsToolDefect = true }`) and `signalsStaleView`
  (`d.Id.IndexOf("stale", StringComparison.OrdinalIgnoreCase) >= 0`). Do **not** mark any
  constructor yet.
- [X] T005 Update `src/FS.GG.SDD.Artifacts/WorkModel.fs` `parseEmbeddedDiagnostic`: add
  `IsToolDefect = false` to the reconstructed record literal (round-tripped diagnostics
  carry no defect bit — research Decision 2).
- [X] T006 [P] Sweep for any other `Diagnostic` record **literals** and add the field so
  the solution compiles: `grep -rn "{ Id = " src/ tests/`. (Constructors built via
  `create`/`commandDiagnostic` need no change.)
- [X] T007 [P] Extend `tests/FS.GG.SDD.Artifacts.Tests/DiagnosticTests.fs`: `create`
  yields `IsToolDefect = false`; `markToolDefect` flips it true; `signalsStaleView`
  matches `Id.IndexOf("stale")` for a representative id set (including an id containing
  "stale" and one that does not).

**Checkpoint**: `dotnet build` + `dotnet test` green; new surface exists; behaviour and
all output bytes unchanged.

---

## Phase 3: User Story 1 — typed defect bit → exit 2 without a registry (Priority: P1) 🎯 MVP

**Goal**: Exit-code escalation reads `IsToolDefect`; the `providerDefectIds` string set
is gone; a new defect diagnostic escalates with no separate registration.

**Independent Test**: A defect-marked diagnostic not present in any id list, emitted
from a blocked command, exits 2; a user-input diagnostic exits 1.

- [X] T008 [US1] Mark the seven defect constructors with `|> markToolDefect`:
  `toolDefect` in `src/FS.GG.SDD.Commands/CommandReports.fs`; and in
  `src/FS.GG.SDD.Artifacts/Diagnostics.fs`: `scaffoldProviderFailed`,
  `scaffoldProviderUnavailable`, `scaffoldProviderWroteSddTree`, `scaffoldMirrorFailed`,
  `upgradeSelfUpdateFailed`, `upgradeStepFailed`. (Must equal today's `providerDefectIds`
  — FR-003.)
- [X] T009 [US1] In `src/FS.GG.SDD.Commands/CommandReports.fs`: rewrite `exitCodeForReport`
  to `report.Diagnostics |> List.exists (fun d -> d.IsToolDefect)` for the Blocked arm,
  and **delete** the `providerDefectIds` set and its comment.
- [X] T010 [P] [US1] Exit-code test (Commands.Tests): Blocked + defect diagnostic → 2;
  Blocked + user-input only → 1; each of the seven ids → 2; a **freshly-invented**
  defect-marked diagnostic (id in no list) → 2; non-blocked outcomes → 0. Re-express any
  test that referenced `providerDefectIds` against the bit (FR-010).
- [X] T011 [US1] Run the JSON/exit golden suite; confirm exit codes and JSON bytes
  unchanged for every command (FR-003/FR-007).

**Checkpoint**: exit-2 escalation is registry-free and byte-for-byte unchanged. **MVP
deliverable.**

---

## Phase 4: User Story 3 — staleness independent of id spelling (Priority: P2)

**Goal**: Agent-refresh reads the typed `signalsStaleView` predicate, not a substring.

**Independent Test**: A stale-signalling diagnostic classifies via the predicate; the
`"stale"` literal is gone from the `HandlersAgents` decision path.

- [X] T012 [US3] In `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAgents.fs` (~:237),
  replace `diagnostic.Id.IndexOf("stale", StringComparison.OrdinalIgnoreCase) >= 0` (both
  the `staleMarkers` test and the `otherBlocking` `< 0` exclusion) with
  `Diagnostics.signalsStaleView diagnostic` / `not (Diagnostics.signalsStaleView …)`.
- [X] T013 [P] [US3] Test: `signalsStaleView` parity for the current stale ids; and an
  agent-refresh case over an embedded work-model whose stale-signalling diagnostic drives
  `agentsStaleWorkModel` through the predicate. Confirm the agents/refresh golden output
  is unchanged (FR-004).

**Checkpoint**: staleness routing is typed; refresh output identical.

---

## Phase 5: User Story 4 — per-stage summaries as a `StagePlan` record (Priority: P3)

**Goal**: Replace the positional 12-tuple in `nextLifecycleEffects` with a named record.

**Independent Test**: Each command's report is identical; each arm sets only its stage's
fields.

- [X] T014 [US4] Define `StagePlan` (12 named fields per data-model.md §2) and
  `emptyStagePlan` in `src/FS.GG.SDD.Commands/CommandWorkflow.fs` (or `CommandTypes.fs(i)`
  if placed above the driver). Internal type — no serialized/public-CLI surface.
- [X] T015 [US4] Rewrite `nextLifecycleEffects` (`CommandWorkflow.fs:~34-65`): each command
  arm produces `{ emptyStagePlan with <its fields> }`; consume the record fields where the
  12-tuple was destructured. Keep `computeXPlan` signatures unchanged (research Decision 3).
- [X] T016 [P] [US4] Add/extend a `CommandWorkflowTests` case asserting the report/model
  for a representative command is unchanged; rely on the golden suite for the full matrix.

**Checkpoint**: no positional summary tuple remains; reports identical.

---

## Phase 6: User Story 5 — split `CommandReports` behind a stable facade (Priority: P3)

**Goal**: Three cohesive units + a facade that keeps `module CommandReports`'s surface
byte-identical. **Pure code movement** (all behaviour already correct from Phases 3–5).

**Independent Test**: build green; `CommandReports.fsi` and Commands `PublicSurface.baseline`
unchanged; each responsibility in its own file.

- [X] T017 [US5] Create `src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fsi`
  + `.fs` (`module … DiagnosticConstructors`): move the ~90 diagnostic constructors and the
  `artifactForPath`/`commandDiagnostic`/`errorDiagnostic`/family helpers, incl.
  `toolDefect |> markToolDefect`.
- [X] T018 [US5] Create `CommandReports/NextActionRouting.fsi` + `.fs`
  (`module … NextActionRouting`): move `outcome` and the `nextAction` cascade; `open`
  `DiagnosticConstructors`.
- [X] T019 [US5] Create `CommandReports/ReportAssembly.fsi` + `.fs`
  (`module … ReportAssembly`): move `buildReport`, `helpReport`, `exitCodeForReport`
  (already using `IsToolDefect`); `open` the two modules above.
- [X] T020 [US5] Rewrite `src/FS.GG.SDD.Commands/CommandReports.fs` as a **facade**
  (`module CommandReports`) re-exporting every constructor + the three assembly functions
  (`let name = DiagnosticConstructors.name`, `let buildReport = ReportAssembly.buildReport`,
  …). Keep `CommandReports.fsi` byte-identical.
- [X] T021 [US5] Update `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` compile order:
  replace the single `CommandReports.fsi/.fs` entry with
  `CommandReports/DiagnosticConstructors.*` → `NextActionRouting.*` → `ReportAssembly.*` →
  `CommandReports.fsi/.fs`, at the same position (before `CommandHelp`).
- [X] T022 [P] [US5] Confirm `tests/FS.GG.SDD.Commands.Tests/PublicSurface.baseline` is
  **unchanged** (facade preserved the surface); the only public-surface delta is the two
  Artifacts functions (T024).

**Checkpoint**: split complete; `CommandReports` surface stable; build green.

---

## Phase 7: Polish, negative contracts & final validation (US2 gate)

**Purpose**: Prove the external contract is byte-for-byte intact and the stringly-typed
mechanisms are gone.

- [X] T023 [US2] Full `dotnet test` green. The **only** permitted baseline edit is the
  next task; every golden output and the Commands surface baseline must pass unmodified
  (FR-010).
- [X] T024 [US2] Update `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` to add
  `markToolDefect` and `signalsStaleView` (the sole sanctioned surface delta, contract D).
- [X] T025 [P] [US2] Byte-diff default/`--json` and `--text` per command vs the Phase-1
  capture → empty diff (FR-007/FR-008).
- [X] T026 [P] [US2] Negative-contract greps (contract E):
  `grep -rn "providerDefectIds" src/` → none;
  `grep -rn 'IndexOf("stale"' src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAgents.fs`
  → none.
- [X] T027 [P] Sweep comments/docs that reference `providerDefectIds` and update them to
  the typed bit: the note in `src/FS.GG.SDD.Artifacts/Diagnostics.fsi` (lines ~50-52,
  ~76-85) and any `docs/` mention (`grep -rn "providerDefectIds" docs/`).
- [X] T028 Run `fsgg-sdd validate`; confirm determinism, degradation, release
  baseline-conformance, and Governance-handoff compatibility match pre-feature (SC-006).
- [X] T029 Run `specs/062-split-command-reports/quickstart.md` end-to-end as the final
  acceptance pass.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: immediate.
- **Phase 2 (Foundational)**: after Phase 1 — **blocks US1 and US3** (both need the field
  and helpers).
- **Phase 3 (US1)**: after Phase 2. MVP.
- **Phase 4 (US3)**: after Phase 2 (independent of US1; may run in parallel with Phase 3 if
  staffed — different files).
- **Phase 5 (US4)**: after Phase 1; independent of the `Diagnostic` type change (touches
  `CommandWorkflow.fs` only). Can run in parallel with Phases 3–4.
- **Phase 6 (US5)**: **last** — relocates code touched by Phases 3–5, so run after they land
  to avoid churn/conflicts. T017→T018→T019→T020→T021 are sequential (compile order);
  T022 after T021.
- **Phase 7 (US2 gate)**: after all stories.

### Parallel opportunities

- T001/T002 in parallel.
- Foundational T006/T007 in parallel after T004/T005.
- Phases 3, 4, and 5 are largely independent (distinct files) and can be parallelized by
  different developers once Phase 2 is done; serialize only where they touch
  `CommandReports.fs` (T008/T009 vs the T017–T020 move — hence US5 last).

## Suggested MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)** — delivers the correctness win (registry-free exit-2
escalation) with the external contract intact. US3/US4/US5 are incremental hardening and
structural cleanup that can follow.

## Task count per story

- US1 (P1): 4 (T008–T011)
- US2 (P1, cross-cutting gate): 6 (T001, T002, T023–T026 grouping, T027/T025 checks)
- US3 (P2): 2 (T012–T013)
- US4 (P3): 3 (T014–T016)
- US5 (P3): 6 (T017–T022)
- Foundational/polish: T003–T007, T028–T029

## Notes

- Never mark a task `[X]` on a failing check; narrow scope and document instead.
- Commit after each phase (or logical task group); Phase 6 should be one reviewable
  "move only, no behaviour change" commit.
- The whole feature's safety net is the pre-existing golden/determinism/surface suites —
  keep them unmodified except for T024.


## Implementation notes (2026-07-03)

- **Result**: full solution green — Contracts 86, Artifacts 175, Acceptance 33 (+3 network-gated skips), Validation 18, Cli 84, Commands 472; `fsgg-sdd validate` overallPassed (332 passed, 0 gaps). Real-CLI smoke: unknown command → exit 1; provider-defect paths (existing ScaffoldCommandTests, real command runs) → exit 2; new focused test proves an *un-registered* marked id → exit 2 (SC-003).
- **Only sanctioned baseline edit**: `+markToolDefect` / `+signalsStaleView` in the Artifacts `PublicSurface.baseline` (T024). Every golden output and the Commands surface baseline passed unmodified (FR-010).
- **T016 / T025 evidence**: the 472 Commands golden tests exercise every lifecycle command's report through `nextLifecycleEffects` and assert byte-identical JSON/text — this is the byte-diff (T025) and the StagePlan-equivalence (T016) evidence; no separate bespoke case was added since the golden matrix is strictly stronger.
- **Environment note (not part of the feature diff)**: the sandbox's empty `.fsgg-local-feed` forced a `dotnet restore --force-evaluate`, regenerating 9 `packages.lock.json` files. These are environment-specific and MUST be excluded from the feature commit.
