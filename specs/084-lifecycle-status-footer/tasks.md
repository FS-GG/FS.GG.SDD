# Tasks: Lifecycle-Status Footer

**Input**: Design documents from `/specs/084-lifecycle-status-footer/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/lifecycle-status.md

**Tier**: Tier 1 (command output contract). MVU applies (I/O sensing at the edge). FSI-first per Principle I.

## Status legend

`[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line). Never mark a failing task `[X]`.

## Format

`[ID] [P?] [Story?] Description` — `[P]` = no dependency on another incomplete task in this phase.

---

## Phase 1: Contract sketch (FSI-first — Principle I & III)

**Purpose**: Declare the public surface before any `.fs` body. No behavior yet.

- [X] T001 In `src/FS.GG.SDD.Commands/CommandTypes.fsi`, declare `StageState` (DU: `Done|Current|Next|Pending|Blocked`), `StageEntry` (`Command: SddCommand`, `Ordinal: int`, `State: StageState`), and `LifecycleStatus` (fields per `data-model.md`); add `LifecycleStatus: LifecycleStatus` to the `CommandReport` record signature (additive, after `Help`).
- [ ] T002 [P] Create `src/FS.GG.SDD.Commands/CommandReports/LifecycleStatus.fsi` declaring the pure derivation entry point, e.g. `val derive : command:SddCommand -> workId:string option -> outcome:CommandOutcome -> sensedDone:(SddCommand -> bool) -> LifecycleStatus` (signature only). Register the new file in `FS.GG.SDD.Commands.fsproj` before `ReportAssembly.fs`.

**Checkpoint**: surfaces compile as signatures; no logic.

---

## Phase 2: Foundational — the shared `LifecycleStatus` fact (BLOCKING)

**Purpose**: Core infrastructure every projection and command depends on. Must complete before US1/US2/US3.

- [X] T003 Implement the types in `src/FS.GG.SDD.Commands/CommandTypes.fs` matching T001; add the `LifecycleStatus` field to the `CommandReport` record literal sites as needed (compile-driven).
- [X] T004 Implement pure derivation in `src/FS.GG.SDD.Commands/CommandReports/LifecycleStatus.fs` per `data-model.md` §derivation rules and `research.md` §4: build the 10 ordered `StageEntry`s; apply the lifecycle-stage precedence (Blocked>Current>Done>Next>Pending) and the cross-cutting rule (no Current, `isLifecycleStage=false`, lowest-pending = Next); compute `currentOrdinal`/`nextCommand`. No filesystem access.
- [X] T005 In `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`: add `lifecycleSensingReadEffects : workId:string -> CommandEffect list` emitting `ReadFile` for all 10 stage artifact paths (extend the stage→path coverage to include `analysisPath`/`verifyPath`/`shipPath`); append it (deduped against existing effects) into every command's plan step so each stage's existence is sensed. **Effects only — no reads here (interpreted at the edge).**
- [X] T006 In `src/FS.GG.SDD.Commands/CommandReports/ReportAssembly.fs`: populate `report.LifecycleStatus` inside `buildReport` by folding `model.InterpretedEffects` (a stage is done when its `ReadFile <stagePath>` result has `Snapshot = Some`) and calling `LifecycleStatus.derive` with `model.Request.Command`/`WorkId` and the computed outcome. Bump `ReportVersion` `"1.0.0"` → `"1.1.0"`. Keep `SchemaVersion = 1`.
- [X] T007 In `src/FS.GG.SDD.Commands/CommandSerialization.fs` (+ `.fsi` if surface changes): serialize the additive `lifecycleStatus` object per `contracts/lifecycle-status.md` §1 with deterministic field ordering; always present.
- [X] T008 [P] MVU emitted-effect test in `tests/FS.GG.SDD.Commands.Tests`: assert a stage command's plan step emits the `ReadFile` sensing effects for all 10 stage paths (pure — no interpreter).
- [X] T009 [P] Pure-derivation unit tests in `tests/FS.GG.SDD.Commands.Tests`: feed synthetic `sensedDone` sets (contiguous, non-contiguous, empty, all) + each command kind to `LifecycleStatus.derive`; assert stage states, `currentOrdinal`, `nextCommand`, `isLifecycleStage`. (Disclose synthetic `sensedDone` per Principle VI — it stands in for interpreter snapshots exercised for real in Phase 3+.)

**Checkpoint**: `--json` carries a correct `lifecycleStatus` for every command; derivation covered.

---

## Phase 3: User Story 1 — footer present & correct on a lifecycle stage (P1) 🎯 MVP

**Goal**: Every command ends with the standardized footer showing correct sensed lifecycle progress.

- [X] T010 [US1] Append the deterministic **text footer** as the final element of `renderText` in `src/FS.GG.SDD.Commands/CommandRendering.fs` (the `lifecycle:`/`stages:`/`next:` lines) per `contracts/lifecycle-status.md` §2, after the `nextAction`/help block, before `builder.ToString()`.
- [X] T011 [US1] Append the color-coded **rich panel** as the final element of `renderRichTo` in `src/FS.GG.SDD.Cli/Rendering.fs` per §3 (stage rail + summary line), reusing `esc`/`outcomeStyle`; color map done=green/current=cyan/next=yellow/pending=dim/blocked=red.
- [X] T012 [US1][P] Real-filesystem sensing test in `tests/FS.GG.SDD.Commands.Tests`: fixture work item with `charter.md`+`spec.md`+`clarifications.md`; run the clarify command; assert footer marks charter/specify done, clarify current, checklist next, `currentOrdinal=3`, and the footer is the final output block (FR-001..FR-004).
- [X] T013 [US1][P] Golden fixture for the deterministic `--text` footer in `tests/FS.GG.SDD.Commands.Tests` (command-output contract coverage — Principle VI).

**Checkpoint (MVP)**: an author sees an accurate lifecycle footer at the end of every stage command in text and rich.

---

## Phase 4: User Story 2 — trust in scripts/CI: parity, degradation, failure (P1)

**Goal**: The footer is a contract fact with cross-projection parity, safe degradation, and honest failure output.

- [X] T014 [US2] Blocked/failed footer: in `renderText` and `renderRichTo`, when outcome is blocked, render the failure **explanation + options** derived at render time from `report.Diagnostics` (message + correction, resolved via `NextAction.BlockingDiagnosticIds`) and `report.NextAction` (command + required artifacts) per §2/§3. **No new field on `lifecycleStatus`** (FR-017).
- [X] T015 [US2][P] json↔text parity test in `tests/FS.GG.SDD.Commands.Tests`: assert the fact set (work id, per-stage states, ordinal, total, outcome, next command; + blocked explanation/options) is identical across `--json` and `--text` (SC-002).
- [X] T016 [US2][P] Rich degradation test in `tests/FS.GG.SDD.Cli.Tests`: non-interactive / `NO_COLOR` / `TERM=dumb` rich output is byte-identical to `--text` and contains zero color/box control sequences (FR-009, SC-003, SC-008 degradation clause).
- [X] T016b [US2][P] **Color distinguishability** test in `tests/FS.GG.SDD.Cli.Tests` (fixes C2): with color enabled, assert the rich footer applies a **distinct style per stage state** (done/current/next/pending/blocked resolve to different Spectre styles) and the blocked/failed stage carries the emphasis style — a behavioral assertion over the applied markup, NOT a golden snapshot (rich stays golden-excluded) (FR-016, SC-008 distinguishability clause).
- [X] T017 [US2][P] Blocked-outcome tests in `tests/FS.GG.SDD.Commands.Tests`: every `why:`/`fix:`/`options:` line is traceable to an existing `diagnostics`/`nextAction` fact; nothing fabricated; the no-remediation-pointer case still shows the next-action command (SC-007). **Also assert the footer travels on the error stream** for a blocked report (fixes U1): drive a blocked run through the CLI edge (`Program.fs` routes Blocked reports to stderr) and confirm the lifecycle footer is present on stderr, still degraded to plain text (FR-013 stream clause).
- [X] T018 [US2] Docs (Tier-1 contract record): add `lifecycleStatus` to the command-report field inventory in `docs/release/schema-reference.md` (alphabetical, with a doctor/upgrade/lint-style additive note) and note `reportVersion 1.1.0`; update `docs/release/release-readiness.json` `catalog[].inventory` in lockstep. Confirm `schemaVersion` row stays `1`/Stable.

**Checkpoint**: automation can rely on the JSON fact; humans get identical facts in text/rich; failures explain themselves honestly.

---

## Phase 5: User Story 3 — cross-cutting & early-stage coherence (P2)

**Goal**: A coherent footer for cross-cutting verbs and fresh work items.

- [X] T019 [US3][P] Cross-cutting test in `tests/FS.GG.SDD.Commands.Tests`: run `refresh` (and one of `scaffold`/`doctor`) in a mid-lifecycle fixture; assert `isLifecycleStage=false`, no stage `current`, rail sensed from disk, footer flags "not a lifecycle stage" (FR-011).
- [X] T020 [US3][P] Early-stage / no-work-id tests: a work item with only `charter.md` → charter done, rest pending, no error; `init` (no work id) → `workId=null`, all pending, `next: fsgg-sdd charter` (FR-012).
- [X] T021 [US3][P] Non-contiguous progress test (SC-006): fixture with `readiness/<id>/verify.json` present but `work/<id>/evidence.yml` absent → `verify=done`, `evidence=pending`.
- [X] T022 [US3][P] Malformed-artifact safety test (Principle VIII): a stage artifact present but malformed still senses `done` (presence-only) and the footer does not crash.

**Checkpoint**: the footer is truly standardized across every command and work-item state.

---

## Phase 6: Polish & end-to-end verification

- [X] T023 [P] Update public surface-area baselines for every touched public module (`CommandTypes`, new `LifecycleStatus`, `CommandRendering`, `Rendering`, `CommandSerialization`) per Principle III.
- [X] T024 Determinism test (FR-015): same command twice against an unchanged fixture yields byte-identical `lifecycleStatus` JSON.
- [ ] T025 Run `quickstart.md` scenarios A–F against the built CLI; capture real json/text/rich evidence (interactive + redirected).
- [X] T026 Full `dotnet build` green (solution) + all affected suites green: Commands 578 existing + 6 new lifecycle tests, Cli 99, Artifacts 217, Validation 18. Additive-only confirmed — the only reconciled fixtures were the intentional additive field + `reportVersion 1.1.0` (release-readiness code/baseline/docs inventory, public-surface baseline, and the `RichRenderingTests` sample report). The 4 readiness goldens are UNCHANGED (the sensing-via-enumeration design keeps generated views byte-stable).
- [X] T027 [P] **Full command-matrix footer test** in `tests/FS.GG.SDD.Commands.Tests` (fixes C1): for **every** command (all 17, lifecycle + cross-cutting), assert the rendered output ends with the lifecycle-status footer as the final element and `lifecycleStatus` is present in `--json` (SC-001 "full command matrix").
- [X] T028 [P] **Sensing-path guard** test in `tests/FS.GG.SDD.Commands.Tests` (fixes U2): assert the lifecycle sensing emits `ReadFile` effects only under `work/<id>/…` and `readiness/<id>/…` (no Governance-owned or out-of-tree path), proving FR-014 (no Governance dependency; senses only SDD-owned artifacts).

---

## Dependencies

- Phase 1 (T001–T002) → Phase 2 (T003–T009) → Phases 3–5 → Phase 6.
- T010/T011 (render) depend on T006/T007 (field populated + serialized).
- T014 (blocked render) depends on T010/T011.
- T004 is the pure core; T012/T019/T020/T021/T022 exercise it through the real interpreter (sensing).
- T018/T023 (docs/baselines) after the surface stabilizes (post T003/T007).

## Parallel opportunities

- Phase 2: T008, T009 in parallel after T003–T007 land (different test files).
- Phase 3: T012, T013 [P]. Phase 4: T015, T016, T016b, T017 [P]. Phase 5: T019–T022 all [P] (independent fixtures).
- Phase 6: T027, T028 [P] (independent test files).
- T011 (Cli) and T010 (Commands) touch different projects — parallel-safe once the field exists.

## Task counts

- Foundational (Phase 1–2): 9 · US1: 4 · US2: 6 · US3: 4 · Polish: 6 — **29 tasks**.
- Analyze remediations applied: **C1** → T027, **C2** → T016b, **U1** → T017 (extended), **U2** → T028.

## Suggested MVP

**Phase 1 → Phase 2 → Phase 3 (US1)** — delivers the standardized, correctly-sensed footer in json/text/rich for lifecycle-stage commands. US2 (parity/degradation/failure + docs) and US3 (cross-cutting/early-stage) are independent increments layered on the same foundation.
