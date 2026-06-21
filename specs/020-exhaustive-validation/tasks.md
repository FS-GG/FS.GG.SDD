---
description: "Task list for 020-exhaustive-validation"
---

# Tasks: Scheduled Exhaustive Validation of Broad Matrices

**Input**: Design documents from `/specs/020-exhaustive-validation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tier**: Tier 1 (new public library + new `validation-report` JSON contract + new
CLI command). Tests are **mandatory** (Constitution VI), so test tasks are included
and written before implementation (Constitution I: spec → fsi → fsi-exercise → tests
→ impl).

## Format: `[ID] [P?] [Story] Description`

- `[P]` — no dependency on another incomplete task in this phase (different files).
- `[US#]` — the user story the task serves.
- Phases run in sequence; tasks within a phase may run in parallel where marked.

## Path conventions

Single repo: `src/FS.GG.SDD.Validation/`, `tests/FS.GG.SDD.Validation.Tests/`,
`src/FS.GG.SDD.Cli/`, per [plan.md](./plan.md) Project Structure.

---

## Phase 1: Setup (shared infrastructure)

- [X] T001 [P] Create `src/FS.GG.SDD.Validation/FS.GG.SDD.Validation.fsproj` —
  `net10.0`, central package management, `ProjectReference` to
  `FS.GG.SDD.Artifacts` and `FS.GG.SDD.Commands`; **no** Spectre.Console and **no**
  Governance package (research Decision 2/6). Compile order:
  `ValidationContracts` → `ValidationHarness` → `ValidationRunner`.
- [X] T002 [P] Create `tests/FS.GG.SDD.Validation.Tests/FS.GG.SDD.Validation.Tests.fsproj`
  — `ProjectReference` to `FS.GG.SDD.Validation`, `FS.GG.SDD.Commands`,
  `FS.GG.SDD.Artifacts`; `xunit`, `xunit.runner.visualstudio`,
  `Microsoft.NET.Test.Sdk`.
- [X] T003 Add both new projects to `FS.GG.SDD.sln` (under the `src` and `tests`
  solution folders, mirroring the existing entries).

**Checkpoint**: solution builds with empty modules.

---

## Phase 2: Foundational (blocking prerequisites — shared by all stories)

**⚠️ Pure types, signatures, and the Elmish surface that every matrix and the
report depend on. No user-story matrix logic yet.**

- [X] T004 [P] Author `src/FS.GG.SDD.Validation/ValidationContracts.fsi` — `Matrix`,
  `MatrixCell`, `CellStatus` (`Pass | Fail | SkippedWithReason | CoverageGap |
  NotValidated`), `EnvironmentClass`, `SensedMetadata`, `ReportSummary`,
  `ValidationReport`; declared-matrix accessors; `val serialize`, `val renderText`,
  `val parse` (round-trip). (data-model Core types; contracts/validation-report)
- [X] T005 [P] Author `src/FS.GG.SDD.Validation/ValidationHarness.fsi` — pure Elmish
  surface: `ValidationModel`, `ValidationMsg`, `ValidationEffect`, `val init`,
  `val update`. (data-model Harness state; Constitution V)
- [X] T006 [P] Author `src/FS.GG.SDD.Validation/ValidationRunner.fsi` —
  `RunnerOptions`, `val run: RunnerOptions -> ValidationReport`. (contracts/matrix-runner)
- [X] T007 Create `tests/FS.GG.SDD.Validation.Tests/PublicSurface.baseline` and
  `SurfaceBaselineTests.fs` asserting the new library's public surface (mirrors
  `tests/FS.GG.SDD.Cli.Tests/SurfaceBaselineTests.fs`). Fails until impl exists.
- [X] T008 Implement `src/FS.GG.SDD.Validation/ValidationContracts.fs` — types +
  **canonical** `serialize` (stable key order; sensed fence; no clock / duration /
  host path / ordering nondeterminism / ANSI — mirror `ReleaseContract.serialize`)
  + `renderText` projection + `parse`. FSI-exercise the serializer for byte-stability.
  (INV-2, INV-5; validation-report C-1)
- [X] T009 Implement `src/FS.GG.SDD.Validation/ValidationHarness.fs` — pure `init`
  (enumerate the declared cross-product into pending cells + emit effects), `update`
  (fold `CellEvaluated`/`SurfaceReconciled`), `BuildReport` (project `ValidationReport`
  + `ReportSummary`). No I/O. (INV-1, INV-6)

**Checkpoint**: report serializes byte-stably; harness folds cells purely; surface
baseline compiles.

---

## Phase 3: User Story 1 — Catch Combinatorial Regressions Before Release (P1) 🎯 MVP

**Goal**: Exhaustively exercise the lifecycle-output, determinism, and
baseline/compatibility matrices and emit one deterministic report that names the
exact failing matrix/cell.

**Independent Test**: seed a regression in one command/projection/state cell; the
report fails that exact cell with an actionable diagnostic while all others pass.

### Tests for User Story 1 (write first; must FAIL before impl)

- [X] T010 [P] [US1] `tests/FS.GG.SDD.Validation.Tests/LifecycleMatrixTests.fs` —
  every command × projection × representative state appears as a cell with a status
  (SC-001); a seeded single-cell divergence is `fail` with a diagnostic naming the
  matrix + coordinates + artifact while all other cells `pass` (US1 Independent Test,
  FR-002/FR-006). Real temp-dir runs, no mocks.
- [X] T011 [P] [US1] `tests/FS.GG.SDD.Validation.Tests/DeterminismMatrixTests.fs` —
  every generated view + `command-report (--json)` reproduces byte-identically over
  identical inputs; `--rich` under `ColorDisabled`/`TermDumb`/`NonInteractiveRedirected`
  emits zero ANSI and changes no JSON byte, stream routing, or exit code vs. default;
  and under `PerturbedHostEnvironment` (varied locale / time zone / working directory /
  ordering) the output is byte-identical to the neutral production (FR-003, INV-3/INV-3a;
  US1 scenarios 2-3 + spec Edge Cases).
- [X] T012 [P] [US1] `tests/FS.GG.SDD.Validation.Tests/BaselineMatrixTests.fs` —
  every release-catalog contract is validated for baseline + conformance via
  `ReleaseContract.evaluate`; a missing baseline/source/field is `notValidated`/`fail`,
  never pass-by-absence; the compatibility entry is exercised against the produced
  `governance-handoff.json` `contractVersion` as an optional fact, and the
  `specKitRange` is asserted **present and parseable** in `release-readiness.json` (a
  well-formedness check — no running Spec Kit to compare against; matrix-runner C-6)
  (FR-004/FR-005, INV-4/INV-8; US1 scenario 4, SC-003/SC-008).
- [X] T013 [P] [US1] `tests/FS.GG.SDD.Validation.Tests/ReportDeterminismTests.fs` —
  two `run`s over an identical tree are byte-identical after `sensed` is excluded; the
  serialized report carries no clock/duration/host path/ANSI (FR-007, INV-2/INV-5,
  SC-004).

### Implementation for User Story 1

- [X] T014 [US1] `src/FS.GG.SDD.Validation/ValidationRunner.fs` state-builders — build
  each representative work-item state (`fresh, specified, planReady, tasksReady,
  analyzed, evidenced, verified/shipped, blocked`) by driving the real
  `FS.GG.SDD.Commands.CommandWorkflow` over a disposable temp dir, reusing the
  `Commands` effect interpreter (research Decision 4; matrix-runner C-1).
- [X] T015 [US1] `ValidationRunner.fs` lifecycle-output matrix — for each command ×
  projection × state, invoke the command-under-test, record `Pass`/`Fail`; an invalid
  command-for-state is `SkippedWithReason` (matrix-runner matrix 1 / C-2; FR-002/FR-009).
- [X] T016 [US1] `ValidationRunner.fs` determinism matrix — reproduce each catalogued
  view + `command-report (--json)` twice and diff; run `--rich` degradation checks
  reusing the feature-019 `Cli.Rendering` behavior; add the `PerturbedHostEnvironment`
  check that re-produces each output under a varied locale/time zone/cwd/ordering and
  diffs against the neutral production (matrix-runner C-3/C-3a/C-4; FR-003 + spec Edge
  Cases / INV-3a).
- [X] T017 [US1] `ValidationRunner.fs` baseline + compatibility matrices — snapshot a
  real shipped project's produced artifacts and call
  `ReleaseContract.evaluate release produced` (each diagnostic → `Fail`/`NotValidated`
  cell); confirm produced handoff `contractVersion` vs. each `compatibility[]` entry
  and Spec Kit range as an optional integration fact, no Governance verdict
  (matrix-runner C-5/C-6; FR-004/FR-005, INV-4/INV-8).
- [X] T018 [US1] `ValidationRunner.fs` `run` — assemble all matrices through
  `ValidationHarness.init`/`update`, populate `sensed` (start/duration/host), build the
  report; exit/`overallPassed` per INV-6 (matrix-runner C-9; validation-report C-3/C-5).
- [X] T019 [US1] Wire the `validate` branch into `src/FS.GG.SDD.Cli/Program.fs` as a
  peer of `--version` (before `parseCommand`): parse `--json`(default)/`--text`/
  `--matrix <name>`/`--out <path>`; write the report to stdout; exit `0` iff
  `overallPassed`. Leave `parseCommand`/`CommandReport` untouched (FR-011;
  cli-validate-command).
- [X] T020 [P] [US1] Add a `validate` CLI smoke to `tests/FS.GG.SDD.Cli.Tests/`
  (assert `--json` emits a `schemaVersion:1` report, `--text` carries no ANSI when
  redirected, exit code matches `overallPassed`, and `--matrix <name>` runs one matrix
  while the others' cells are reported `notValidated` with a non-zero exit — a partial
  run never reads as a full pass; cli-validate-command).

**Checkpoint**: `fsgg-sdd validate --json` produces a complete deterministic report;
a seeded single-cell regression is caught exactly. MVP is independently testable.

---

## Phase 4: User Story 2 — No Public Surface Escapes Coverage (P2)

**Goal**: An uncovered public surface is a visible `coverageGap`, and a declared cell
naming a vanished surface is a detectable failure — the real produced surface is
authoritative.

**Independent Test**: add a command/view/contract no matrix references; the report
emits a `coverageGap` finding and does not pass.

### Tests for User Story 2 (write first; must FAIL before impl)

- [X] T021 [P] [US2] `tests/FS.GG.SDD.Validation.Tests/CoverageGapTests.fs` — an
  uncovered real surface ⇒ `coverageGap` finding naming it and `overallPassed = false`;
  a declared cell whose surface no longer exists ⇒ detectable failure with the real
  surface authoritative (FR-009/FR-012, INV-6/INV-7, SC-005; US2 scenarios 1-2).

### Implementation for User Story 2

- [X] T022 [US2] `ValidationRunner.fs` `ReconcileDeclaredSurface` — enumerate the real
  produced public surface from a source **independent of the declared matrix**, per
  dimension: commands via an **exhaustive `SddCommand` match** (a new case is a
  compile-time break — no reflection), catalog contracts via `release-readiness.json`,
  generated views via the produced `readiness/<id>/` directory listing. Diff against the
  declared cells, emit `CoverageGap`/stale-entry findings, feed `SurfaceReconciled` to
  the harness (matrix-runner C-7; data-model INV-7; FR-012). This independence is what
  makes a newly added surface detectable rather than a silent co-omission.
- [X] T023 [US2] Confirm `ValidationHarness`/`ReportSummary` make any
  `CoverageGap`/`NotValidated` set `overallPassed = false` and a non-zero exit (INV-6;
  extend T009 fold if needed).

**Checkpoint**: a seeded uncovered surface and a stale declared entry both fail the
report.

---

## Phase 5: User Story 3 — Keep the Inner Loop Cheap (P3)

**Goal**: The harness is scheduled/on-demand only, runs with no Governance present, and
adds no required step to the inner loop; agent/human surfaces document it identically.

**Independent Test**: run the fast lifecycle commands unchanged; confirm none require
or trigger `validate`, and `validate` runs separately with no Governance installed.

### Tests for User Story 3 (write first; must FAIL before impl)

- [X] T024 [P] [US3] `tests/FS.GG.SDD.Validation.Tests/IsolationTests.fs` — `run`
  completes and emits its report with no Governance runtime; no produced artifact
  carries a Governance route/profile/freshness/gate/release verdict; no `SddCommand`,
  `init`/`update`, or effect interpreter references `ValidationRunner` (FR-008/FR-010,
  INV-8, SC-006/SC-007; US3 scenarios 1-2).

### Implementation for User Story 3

- [X] T025 [US3] Verify and, if needed, refactor so `ValidationRunner` is reachable
  only via the CLI `validate` branch — no lifecycle command path imports it
  (one-directional dependency; matrix-runner §isolation).
- [X] T026 [P] [US3] Document `fsgg-sdd validate` identically across `CLAUDE.md`,
  `AGENTS.md`, `.claude/skills/fs-gg-sdd-project/SKILL.md`,
  `.codex/skills/fs-gg-sdd-project/SKILL.md`, `README.md`, and `docs/` — scheduled/
  on-demand cross-cutting sweep, separate from the inner loop, no Governance required,
  one deterministic report (Principle VII, FR-011).
- [X] T026a [P] [US3] Record the `validation-report` contract as a **declared exception**
  in `docs/release/schema-reference.md` — a short note that this public JSON contract is
  intentionally not catalogued (it carries sensed metadata and is harness output, not a
  produced lifecycle artifact), so the exclusion is documented rather than a silent
  omission (validation-report C-4; research Decision 6; addresses analyze finding I1).

**Checkpoint**: existing fast commands unchanged; `validate` runs standalone without
Governance.

---

## Phase 6: Polish & Evidence

- [X] T027 FSI public-surface transcript for `ValidationContracts`/`ValidationHarness`/
  `ValidationRunner` → `readiness/020-exhaustive-validation/fsi-validation-surface.txt`.
- [X] T028 Release build + full test suite green; disposable-directory CLI smoke for
  `validate --json`/`--text` including a seeded regression and a no-Governance run →
  `readiness/020-exhaustive-validation/cli-smoke.md`; byte-stable double-run check.
- [X] T029 Author `readiness/020-exhaustive-validation/evidence.yml` with traceability
  from FR/SC → tasks/tests (mirror `readiness/019-spectre-rendering/evidence.yml`).

---

## Dependencies & ordering

- Phase 1 → Phase 2 → Phases 3/4/5 (in priority order) → Phase 6.
- T014 (state-builders) precedes T015-T018 within US1.
- T022 (reconciliation) depends on T015/T018 (declared cells + report assembly).
- T019 (CLI wiring) depends on T018 (`run` exists).
- T026/T028 depend on the runner being functional (after Phase 3/4).

## Elmish/MVU applicability

Satisfied by T005/T009 (pure `Model`/`Msg`/`Effect` + `init`/`update`/`BuildReport`)
and T014-T018 (the `ValidationRunner` edge interpreter performing all real I/O), with
pure-fold tests (T009 via T010-T013) and real-interpreter evidence (T028).

## Summary

- **US1 (P1, MVP)**: 11 tasks (T010-T020) — lifecycle/determinism/baseline matrices,
  report, CLI.
- **US2 (P2)**: 3 tasks (T021-T023) — coverage gap + reconciliation.
- **US3 (P3)**: 4 tasks (T024-T026, T026a) — isolation, no-Governance, agent/doc
  alignment, schema-reference exception note.
- **Shared**: Setup 3 (T001-T003), Foundational 6 (T004-T009), Polish 3 (T027-T029).
- **Total**: 30 tasks.
- **Parallel opportunities**: T001/T002; T004/T005/T006; all test-authoring tasks
  (T010-T013, T021, T024) within their phase; T020, T026, T026a.
- **Suggested MVP**: Phases 1-2 + Phase 3 (User Story 1) — an end-to-end
  `fsgg-sdd validate` emitting a deterministic report that catches a seeded
  combinatorial regression.
