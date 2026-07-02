# Implementation Plan: Extract shared CommandWorkflow seams

**Feature Branch**: `061-extract-shared-seams`

**Spec**: `specs/061-extract-shared-seams/spec.md`

## Approach

A behavior-preserving refactor. Each seam is extracted into one shared helper placed in a
module compiled before its call sites, then every call site is rewired to it. The extracted
helpers emit the same token/effect/diagnostic sequences as the inline code they replace, so
the JSON contract bytes, diagnostic ids/messages/order, effect lists, and exit codes are
unchanged. The full offline test suite (golden JSON, determinism, per-stage diagnostics,
CLI/validation output, rich degradation) is the verification; no new tests are required
because the invariant is "identical observable behavior".

## Placement (F# compile order)

- **`CommandWorkflow/ViewGeneration.fs`** (compiled before the Verify/Ship handlers): the
  shared readiness-view writer helpers — `writeViewPreamble`, `writeSourcesArray`
  (parameterized by the source-kind classifier), `writeLifecycleReadiness`,
  `writeGeneratedViewsArray`, `writeBoundaryFacts` (parameterized by the array key),
  `writeViewDiagnostics`, `writeReadinessFindings`, `writeNextAction`. `analysisJson`,
  `verifyJson`, and `shipJson` are rewired to call them; each view keeps its own
  view-specific middle sections inline.
- **`CommandWorkflow/Foundation.fs`** (compiled first): `blockedWorkModelPlan`,
  `frontMatterIdentityDiagnostics` (constructors passed as parameters so it is agnostic to
  each artifact's front-matter record type), `sourceDigestsStale` (over projected
  `(path, digest)` pairs since the per-artifact snapshot types differ), and
  `preWorkModelReadEffects` (the charter → tasks read-effect frame).
- **`CommandEffects.fs`** (compiled last in Commands — it holds `interpretAll` and can see
  `CommandWorkflow.init/update` and `buildReport`): `driveToReport`, exposed in
  `CommandEffects.fsi`. `Program.fs` (CLI) and `ValidationRunner.fs` (Validation) both call it.
- **`Cli/Rendering.fs`** (+ `Rendering.fsi`, compiled before `RegistryValidate.fs`):
  `createCappedConsole`, called by the command, validation, and registry rich sinks.

## Risk & mitigation

- **Byte-exact JSON** — the readiness writers are golden-tested; helpers reproduce the exact
  write order. Verified by the analyze/verify/ship contract tests.
- **F# offside** — the front-matter rewrite appends artifact-specific source checks after the
  shared helper via `@ [ … ]`; indentation is site-relative so the `@` reads as an infix
  continuation. Verified by a clean build.
- **Read-order behavior** — only the charter → tasks family (a genuine shared frame) is
  unified; the divergently-ordered generators are untouched (FR-007).

## Verification

1. `dotnet build FS.GG.SDD.sln -c Debug` — clean.
2. `dotnet test FS.GG.SDD.sln -c Debug` — full offline suite green.
