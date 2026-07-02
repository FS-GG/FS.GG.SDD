# Tasks: Extract shared CommandWorkflow seams

**Feature Branch**: `061-extract-shared-seams`

**Spec**: `specs/061-extract-shared-seams/spec.md` · **Plan**: `specs/061-extract-shared-seams/plan.md`

Behavior-preserving refactor: after every task, `dotnet build FS.GG.SDD.sln` is clean and
the full offline suite stays green. No new tests — the invariant is identical observable
behavior.

- [x] **T001** Add the shared readiness-view writer helpers to `ViewGeneration.fs`
  (`writeViewPreamble`, `writeSourcesArray`, `writeLifecycleReadiness`,
  `writeGeneratedViewsArray`, `writeBoundaryFacts`, `writeViewDiagnostics`,
  `writeReadinessFindings`, `writeNextAction`). (FR-001)
- [x] **T002** Rewire `analysisJson`, `verifyJson`, `shipJson` to the shared helpers, keeping
  each view's specific middle sections inline. (FR-001, AC-001)
- [x] **T003** Add `blockedWorkModelPlan` to `Foundation.fs` and replace the 9 blocked-work-model
  fallbacks in `HandlersEarly`/`HandlersAnalyze`/`HandlersVerify`/`HandlersShip`/`HandlersEvidence`.
  (FR-002, AC-002)
- [x] **T004** Add `frontMatterIdentityDiagnostics` to `Foundation.fs` and replace the 10
  identity-check blocks in `ParsingEarly`/`ParsingMid`/`ParsingTasks`, preserving each
  artifact's constructors and appending its source-pointer checks. (FR-003, AC-003)
- [x] **T005** Add `sourceDigestsStale` to `Foundation.fs` and replace the 3 source-staleness
  blocks in `ParsingMid`/`ParsingTasks`. (FR-004, AC-004)
- [x] **T006** Add `driveToReport` to `CommandEffects.fs` (+ `.fsi`) and replace the duplicated
  MVU loops in `Program.fs` and `ValidationRunner.fs`. (FR-005, AC-005)
- [x] **T007** Add `createCappedConsole` to `Rendering.fs` (+ `.fsi`) and replace the 3 rich
  Spectre.Console setups in `Rendering.fs` and `RegistryValidate.fs`. (FR-006, AC-006)
- [x] **T008** Add `preWorkModelReadEffects` to `Foundation.fs` and rewire the charter →
  tasks read-effect lists; leave the divergently-ordered generators unchanged. (FR-007, AC-007)
- [x] **T009** Full verification: `dotnet build` clean, `dotnet test FS.GG.SDD.sln` green.
