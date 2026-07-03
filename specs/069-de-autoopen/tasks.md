# Tasks: Drop the blanket `[<AutoOpen>]` in `CommandWorkflow/`

**Feature**: 069-de-autoopen | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Carries forward feature 068 tasks T027–T029 (US3), deferred there to this PR.

## Phase 1: Baseline

- [x] T001 Record the pre-change build baseline: `dotnet build FS.GG.SDD.sln`
  → **0 Warning(s), 0 Error(s)**. This is the no-new-warnings reference for T029.

## Phase 2: De-AutoOpen (US3)

- [x] T027 Remove `[<AutoOpen>]` from the sixteen `CommandWorkflow/*.fs` modules
  that carry it (all except `Drift`, `SeededSkills`, which already model the
  target). In compile order. **Done** — all 16 attributes removed; `grep AutoOpen`
  in `CommandWorkflow/` returns nothing (zero survivors, none needed a justified
  exception).
- [x] T028 Fix every resulting resolution error by adding a file-scoped explicit
  `open FS.GG.SDD.Commands.Internal.<Module>` at the top of each consuming file
  (preferred for ubiquitous modules like `Foundation`) or qualifying the call
  site where names collide. **Done** — the symbol index showed **zero top-level
  cross-module name collisions**, so a per-file explicit `open` for each
  referenced sibling (in compile order) restores resolution unambiguously; no
  call-site qualification was required. `SeededSkills`/`Drift` stay qualified
  (their existing codebase pattern). 65 `open` lines added across the 16 modules,
  the `CommandWorkflow.fs` orchestrator, and 4 test files; 16 attribute lines
  removed.

## Phase 3: Gate (T029 / SC-004 / AC-2)

- [x] T029a **Build gate**: `dotnet build FS.GG.SDD.sln` → **0 Warning(s), 0
  Error(s)** (unchanged vs T001 baseline); `grep -rn AutoOpen
  src/FS.GG.SDD.Commands/CommandWorkflow/` → no matches. **Pass.**
- [x] T029b **Test gate**: `dotnet test FS.GG.SDD.sln` → **888 passed, 0 failed,
  3 skipped** (the 3 skips are the network-gated acceptance tests, offline).
  **Pass.**
- [x] T029c **Contract-diff gate**: `git diff --name-only -- 'src/**/*.fsi'
  '**/*.baseline'` → empty; readiness/JSON golden fixtures unchanged (exercised
  green by the suite). CLI smoke-run emits the deterministic JSON report, exit 0.
  **Pass.**
