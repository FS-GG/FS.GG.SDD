# Tasks: API Surface Drift Check

**Input**: Design documents from `/specs/086-api-surface-drift-check/`

**Prerequisites**: plan.md, spec.md

**Tier**: Tier 1 (new command + command-output contract). MVU applies (I/O at the edge). FSI-first per Principle I.

## Status legend

`[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line). Never mark a failing task `[X]`.

## Format

`[ID] [P?] [Story?] Description (FR-###/SC-###)` — `[P]` = no dependency on another incomplete task in this phase.

---

## Phase 1: Contract sketch (FSI-first — Principle I & III)

**Purpose**: Declare the public surface before any `.fs` body. No behavior yet.

- [X] T001 In `src/FS.GG.SDD.Commands/CommandTypes.fsi`: add `Surface` to the `SddCommand` DU and a `SurfaceSummary` record; add `Surface: SurfaceSummary option` to both the `CommandReport` and `CommandModel` record signatures (additive). (FR-001/FR-009) — **Design note**: implemented as a flat `SurfaceSummary` (`SourceRoot`, `BaselineRoot`, `Mode`, `CheckedCount`, `MissingBaselinePaths`, `DriftedSourcePaths`, `OrphanBaselinePaths`, `UpdatedBaselinePaths`, `IsCoherent`) rather than a `SurfaceEntry list` + `SurfaceStatus` DU — matching the existing `DoctorSummary`/`UpgradeSummary` house convention (sorted path lists + counts), which keeps the JSON deterministic without a nested-entry ordering rule.
- [X] T002 [P] In `src/FS.GG.SDD.Commands/CommandWorkflow.fsi`, `CommandSerialization.fsi`, `CommandRendering.fsi`, `CommandHelp.fsi`: declare the added surface entry points (dispatch arm exposure, `writeSurface`, the text-render helper, the help entry) as signatures only. (FR-009)
- [X] T003 [P] Decide and declare the `--check`/`--update` request shape in `CommandTypes.fsi`: add `SurfaceUpdate: bool` (and, if not reusing `Parameters`, `SurfaceCheck: bool`) to `CommandRequest`; roots are read from `Parameters` (`sourceRoot`/`baselineRoot`). (FR-008/FR-012)

**Checkpoint**: surfaces compile as signatures; no logic.

---

## Phase 2: Foundational — the shared drift core (BLOCKING)

**Purpose**: Core infrastructure every projection and story depends on. Must complete before US1/US2/US3.

- [X] T004 Implement the types in `src/FS.GG.SDD.Commands/CommandTypes.fs` matching T001/T003; add the `Surface` field to `CommandReport`/`CommandModel` record literal sites (compile-driven). (FR-001/FR-009)
- [X] T005 Add the `commandName`/`commandStage`/`parseCommand`/`nextLifecycleCommand` arms for `Surface` in `CommandTypes.fs`: name `"surface"`, `nextLifecycleCommand Surface = None`, and `parseCommand` handling for `--check`/`--update` (and `--param sourceRoot=…`/`baselineRoot=…` already carried in `Parameters`). (FR-001/FR-008/FR-012)
- [X] T006 In `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs`: add the `Surface` `plan` arm emitting the initial `EnumerateDirectory`(source root) + `ReadFile` effects, and exempt `Surface` in `workIdDiagnostics` so no `--work` is required (cross-cutting). **Effects only — no reads here (interpreted at the edge).** (FR-001/FR-002)
- [X] T007 Add `surface.*` diagnostic constructors in `src/FS.GG.SDD.Artifacts/Diagnostics.fs`: `surface.drift` and `surface.baselineMissing` as `DiagnosticError` (so `--check` exits 1), `surface.orphanBaseline` as `DiagnosticWarning`. (FR-013)
- [X] T008 Create `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersSurface.fs` (register in the `.fsproj` after `HandlersDoctor.fs`): pure `computeSurfaceNext` — from the interpreted source-root enumeration + read snapshots, map each `src/**/*.fsi` to its mirrored `docs/api-surface/**/*.fsi`, classify `matched`/`missing-baseline`/`drifted` by raw-byte compare (no normalization), detect orphan baselines, build `SurfaceSummary` (entries sorted by src-relative path), set `model.Surface`, and emit drift/orphan diagnostics. No filesystem access in this module. (FR-002/FR-003/FR-006/FR-014)
- [X] T009 In `src/FS.GG.SDD.Commands/CommandWorkflow.fs`: add the dispatch arm `| Surface, _ -> computeSurfaceNext`. (FR-001)
- [X] T010 In `src/FS.GG.SDD.Commands/CommandReports/ReportAssembly.fs`: copy `model.Surface` → `report.Surface`; map a drift verdict to exit 1 via the `surface.drift` `DiagnosticError`, coherent → exit 0. (FR-005/FR-013)
- [X] T011 In `src/FS.GG.SDD.Commands/CommandSerialization.fs` (+ `.fsi`): implement `writeSurface` and call it — serialize the `surface` object (source root, baseline root, ordered entries with status, counts, verdict) with deterministic field + entry ordering. (FR-009/FR-010)
- [X] T012 [P] Pure-derivation unit tests in `tests/FS.GG.SDD.Commands.Tests/SurfaceCommandTests.fs`: feed synthetic enumerate/read snapshots (all matched, one missing, one drifted, one orphan, mixed) to `computeSurfaceNext`; assert per-entry status, counts, `IsCoherent`, and the emitted diagnostics. (FR-002/FR-003/FR-006)
- [X] T013 [P] MVU emitted-effect test in `tests/FS.GG.SDD.Commands.Tests`: assert the `Surface` plan step emits the source-root enumeration + read effects and (for `--check`) emits zero `WriteFile` effects (pure — no interpreter). (FR-002/FR-004)

**Checkpoint**: `--json` carries a correct `surface` summary; the drift core is covered.

---

## Phase 3: User Story 1 — `--check` fails CI on drift (P1) 🎯 MVP

**Goal**: `surface --check` is read-only, reports the verdict, and exits 1 on any missing/drifted baseline.

- [X] T014 [US1] Ensure `computeSurfaceNext` emits NO `WriteFile` effects on the `--check` path (default) — read-only. (FR-004)
- [X] T015 [US1] Wire the exit-code mapping end-to-end in `ReportAssembly.fs`: any `missing-baseline`/`drifted` entry ⇒ `surface.drift` `DiagnosticError` ⇒ exit 1; all `matched` ⇒ exit 0. (FR-005)
- [X] T016 [US1][P] Real-filesystem test in `tests/FS.GG.SDD.Commands.Tests`: fixture with two `src/<Pkg>/<Name>.fsi` + byte-identical baselines → `surface --check` reports all `matched`, coherent, exit 0. (FR-002/FR-003/SC-001)
- [X] T017 [US1][P] Real-filesystem test: one baseline deleted + one baseline edited by a single byte → `--check` marks `missing-baseline`/`drifted`, names both paths, exit 1. (FR-003/FR-005/SC-002)
- [X] T018 [US1][P] Zero-write assertion: capture the workspace bytes before/after a `--check` run (coherent and drifted fixtures) and assert no file changed. (FR-004/SC-003)
- [X] T019 [US1][P] CRLF-vs-LF-only difference test: a baseline that differs from its source only in line endings is reported `drifted` (no normalization). (FR-003, edge case)

**Checkpoint (MVP)**: CI can gate on `surface --check`; drift fails the build, coherence passes.

---

## Phase 4: User Story 2 — `--update` refreshes baselines (P1)

**Goal**: `surface --update` reconciles missing/drifted baselines idempotently and exits 0.

- [X] T020 [US2] In `HandlersSurface.fs`: on the `--update` path, emit a `WriteFile` effect (source `.fsi` bytes → mirrored baseline path, creating directories) for every `missing-baseline`/`drifted` entry, and NONE for `matched` entries (no rewrite on unchanged content). Record the changed baseline paths in `SurfaceSummary`. (FR-007)
- [X] T021 [US2] Exit 0 for `--update` regardless of pre-write drift (the write reconciles it); `--update` takes precedence when both `--check`/`--update` are supplied. (FR-007/FR-008)
- [X] T022 [US2][P] Real-filesystem test: fixture with a missing baseline + a drifted baseline → `--update` writes both to match source byte-for-byte (creating the missing dir), reports the two changed paths, exit 0, and a following `--check` exits 0. (FR-007/SC-004)
- [X] T023 [US2][P] Idempotence test: an already-`matched` baseline is not rewritten by `--update` (byte-identical + mtime-agnostic assertion) and is not listed among changed paths. (FR-007/SC-004)
- [X] T024 [US2][P] Write-path guard test: assert every `WriteFile` effect from `--update` targets a path under the resolved baseline root only (never under the source root or out of tree). (FR-007/FR-014)

**Checkpoint**: an intentional surface change is reconciled with one command; unchanged baselines stay byte-stable.

---

## Phase 5: User Story 3 — projections, orphans & root overrides (P2)

**Goal**: Consistent json/text/rich facts, honest orphan warnings, and optional root overrides.

- [X] T025 [US3] Append the deterministic **text block** for the surface summary in `src/FS.GG.SDD.Commands/CommandRendering.fs` (source root, baseline root, per-file status lines, counts, verdict); the rich projection auto-derives from text at the CLI edge. (FR-009/FR-011)
- [X] T026 [US3] Orphan handling: a `docs/api-surface/**/*.fsi` with no source is reported `orphan` via `surface.orphanBaseline` (warning) and does NOT change the exit code; never deleted/modified. (FR-006/FR-013)
- [X] T027 [US3] Root overrides: resolve `sourceRoot`/`baselineRoot` from `Parameters`, defaulting to `src/`/`docs/api-surface/`, and echo both resolved roots in the report; the `<Pkg>/<Name>.fsi` mirroring rule is unchanged. (FR-012)
- [X] T028 [US3][P] Orphan test in `tests/FS.GG.SDD.Commands.Tests`: all sources matched + one orphan baseline → orphan listed as a warning in the report, verdict coherent, exit 0. (FR-006/SC-007)
- [X] T029 [US3][P] json↔text parity test: assert the fact set (roots, per-file statuses, counts, verdict) is identical across `--json` and `--text`. (FR-009/SC-005)
- [X] T030 [US3][P] Rich degradation test in `tests/FS.GG.SDD.Cli.Tests`: non-interactive / `NO_COLOR` rich output is byte-identical to `--text` with zero color/box control sequences. (FR-011/SC-005)
- [X] T031 [US3][P] Root-override test: `--param sourceRoot=lib --param baselineRoot=docs/surface` enumerates under `lib/`, maps to `docs/surface/`, and echoes both roots. (FR-012)
- [X] T032 [US3][P] Generic-purity test: a fixture whose package names the command has never seen produces correct statuses, and no provider/package literal appears in the summary shape. (FR-014/SC-006)

**Checkpoint**: the command is trustworthy in automation and human review across every projection and root.

---

## Phase 6: Polish & end-to-end verification

- [X] T033 Add the `surface` help entry + `--check`/`--update` flags (and the `--param sourceRoot/baselineRoot` note + the both-flags precedence) in `src/FS.GG.SDD.Commands/CommandHelp.fs` (+ `.fsi`). (FR-008/FR-012)
- [X] T034 [P] Empty/missing-source-root safety test: no `.fsi` under `src/` (and a missing `src/`) → coherent empty report, exit 0, no crash; orphans under the baseline root still surfaced. (FR-005, edge cases; Principle VIII)
- [X] T035 [P] Determinism test: `surface --check` twice against an unchanged fixture yields byte-identical `--json`. (FR-010)
- [-] T036 Add a `surface --check` step/job to `.github/workflows/gate.yml`. **Adapted**: the check belongs in a scaffolded **workspace's** CI, NOT the FS.GG.SDD **component** repo's gate — the component uses hand-authored `.fsi` + the internal reflection `PublicSurface.baseline` test and has no `docs/api-surface/` (running `surface --check` at its root would flag all 53 `.fsi` as missing and fail CI, contradicting the spec's non-goal). Delivered instead as the recommended workspace-CI snippet in `quickstart.md`; the reusable Templates MSBuild target is the cross-repo follow-up under FS-GG/.github#235. (FR-005/SC-002)
- [X] T037 [P] Refresh the public surface-area baselines via `FSGG_UPDATE_BASELINE=1` per Principle III. Only `tests/FS.GG.SDD.Artifacts.Tests/PublicSurface.baseline` changed (the two new public `Diagnostics` constructors); the new command wiring is behind `.fsi` signatures / `internal` modules, so the Commands/Cli reflection baselines are unchanged. (SC-005)
- [X] T038 [P] Update agent-surface boundary docs (`CLAUDE.md`/`AGENTS.md`) and any Claude/Codex command help kept in lockstep with the new `surface` verb. (FR-001)
- [X] T039 Full `dotnet build` green (solution) + all affected suites green (Commands, Cli, Artifacts). Confirm additive-only: `schemaVersion` stays `1`; the only reconciled fixtures are the intentional additive `Surface` field + version bump + refreshed surface baselines. (SC-005)

---

## Dependencies

- Phase 1 (T001–T003) → Phase 2 (T004–T013) → Phases 3–5 → Phase 6.
- T008 (`computeSurfaceNext`) is the pure core; T016/T017/T022/T028/T031 exercise it through the real interpreter.
- T014/T015 (check exit) depend on T010/T011 (field populated + serialized).
- T020/T021 (update writes) depend on T008.
- T025 (text render) depends on T004/T011 (types + json). T030 (rich degrade) depends on T025.
- T036/T037/T038 (gate/baselines/docs) after the surface stabilizes (post T004/T011).

## Parallel opportunities

- Phase 1: T002, T003 [P] after T001.
- Phase 2: T012, T013 [P] after T004–T011 land (different test files).
- Phase 3: T016–T019 [P] (independent fixtures). Phase 4: T022–T024 [P]. Phase 5: T028–T032 [P].
- Phase 6: T034, T035, T037, T038 [P] (independent files).

## Task counts

- Contract sketch (Phase 1): 3 · Foundational (Phase 2): 10 · US1: 6 · US2: 5 · US3: 8 · Polish: 7 — **39 tasks** (38 done `[X]`, 1 adapted `[-]` — T036 gate wiring scoped to workspace CI, see note).

## Suggested MVP

**Phase 1 → Phase 2 → Phase 3 (US1)** — delivers the read-only, CI-gating `surface --check` (drift → exit 1, coherent → exit 0) in json/text. US2 (`--update` reconciliation) is the P1 second half; US3 (projection parity, orphans, root overrides) layers on the same foundation.
