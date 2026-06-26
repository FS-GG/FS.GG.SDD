# Feature Specification: Split CommandWorkflow into facade + internal modules

**Feature Branch**: `025-split-command-workflow`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in @docs/reports/2026-06-26-074428-refactor-analysis.md" — R2 in the refactor roadmap: *Split `CommandWorkflow.fs` into facade + internal modules (§1.1)*.

## Overview

`src/FS.GG.SDD.Commands/CommandWorkflow.fs` is a single flat `module CommandWorkflow` of 6,814 lines holding 268 top-level bindings — 39% of all `src` implementation LOC and the largest single module in the codebase. Its public surface is only two values (`init`, `update`), as proven by its 7-line `.fsi`; the other 266 bindings are internal (prerequisite resolution, per-artifact parsing, the twelve `compute*Plan` handlers, view rendering, and MVU orchestration), all co-located with no nested-module structure beyond seven artifact-namespace aliases.

This refactor reorganizes that one flat module into a thin facade over internal modules (`Prerequisites`, `Parsing`, `Handlers`, `ViewRendering`, and orchestration) so no single file exceeds ~1,500 lines, while holding the public contract and runtime behavior exactly fixed. It is the R2 item in `docs/reports/2026-06-26-074428-refactor-analysis.md`, sequenced after R3/R4/R1 (now complete) so the handler extraction landed by R1 has a structured home.

It is a pure internal reorganization: the 7-line `.fsi` contract is unchanged and every command's deterministic JSON output stays byte-for-byte identical. The existing test suite (438 tests) is the behavioral guard.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Navigate the command workflow by concern (Priority: P1)

An SDD maintainer extending or debugging a single lifecycle stage (e.g. `verify`) opens the workflow code and finds the relevant handler, its prerequisite checks, and its view rendering in cohesive, separately-named modules/files rather than scrolling one 6,800-line file. Each concern (prerequisites, parsing, handlers, view rendering, orchestration) is independently locatable.

**Why this priority**: This is the entire point of R2 — navigability of the largest, most-edited module. It delivers value on its own even if no further refactor follows.

**Independent Test**: Verify that after the change `src/FS.GG.SDD.Commands/` contains multiple cohesive files (none exceeding ~1,500 lines) that collectively replace the monolith, that each is named for its concern, and that a maintainer can locate any given `compute*Plan` handler without reading unrelated code. Confirm the whole suite still passes.

**Acceptance Scenarios**:

1. **Given** the refactored tree, **When** a maintainer looks for the `verify` stage logic, **Then** its handler lives in a handler-family module distinct from the prerequisite-resolution and parsing modules.
2. **Given** the refactored tree, **When** any single source file is measured by line count, **Then** none exceeds ~1,500 lines.
3. **Given** the refactored tree, **When** the file-level structure is inspected, **Then** the public entry points (`init`/`update`) sit in a thin facade module over the internal concern modules.

### User Story 2 - Preserve the public contract and automation output exactly (Priority: P1)

A downstream automation consumer (and the broader build) depends on the `CommandWorkflow.init`/`update` surface and on the byte-exact deterministic JSON each command emits. After the refactor, the public signature file is unchanged and every command produces identical JSON bytes, exit codes, and stream routing for identical inputs.

**Why this priority**: R2's binding gate (unlike the relaxed R3) is that the public `.fsi` contract and deterministic JSON output remain byte-stable. A regression here breaks consumers; this guard is non-negotiable.

**Independent Test**: Diff the public `.fsi` before and after (must be byte-identical), and confirm the full deterministic/golden test suite passes with no golden-file regeneration.

**Acceptance Scenarios**:

1. **Given** the refactor, **When** `CommandWorkflow.fsi` is diffed against `main`, **Then** there is no change (still the two `val init` / `val update` lines).
2. **Given** identical command inputs, **When** any command's JSON (default/`--json`) output is compared before and after, **Then** the bytes are identical.
3. **Given** the existing 438-test suite, **When** it runs against the refactored tree, **Then** all tests pass with no golden/baseline file edits required.

### User Story 3 - Build and dependency order remain valid (Priority: P2)

The new files compile in F#'s required order with the existing one-way layering (`Artifacts → Commands → Cli`/`Validation`) intact and no dependency cycles introduced. The `.fsproj` compile list reflects the new files in correct order.

**Why this priority**: F# is order-sensitive; an incorrect split breaks the build. Necessary for the refactor to land but subordinate to navigability and contract preservation.

**Independent Test**: `dotnet build -c Release` succeeds; the Commands project still depends only on Artifacts (no new cross-layer or cyclic references).

**Acceptance Scenarios**:

1. **Given** the new file set, **When** the solution builds in Release, **Then** it compiles with no new errors and no new warning categories.
2. **Given** the refactored module graph, **When** dependencies are inspected, **Then** the established one-way layering holds and no cycle is introduced.

### Edge Cases

- **Handlers that intentionally diverge**: `computeRefreshPlan` keeps its own guard (it does not route through `runHandler`); the split must preserve that shape rather than force uniformity.
- **Mutual references across concern boundaries**: handlers depend on prerequisites, parsing, and view rendering. The chosen module/file ordering must satisfy F#'s top-down compile constraint without introducing forward references.
- **The seven artifact-namespace aliases** (`DiagnosticsModule`, `WorkModelModule`, …) currently scoped inside the flat module must remain available to every internal module that used them.
- **Shared internal helpers** (`resolvePrerequisites`, `runHandler`, path constants, read-effect builders) referenced by multiple handler families must live where all consumers can see them.
- **Incremental landing**: if the split ships in stages, each intermediate commit must still build and keep the suite green and the `.fsi`/JSON byte-stable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The implementation MUST replace the single flat `module CommandWorkflow` with a thin facade exposing `init`/`update` over internal modules organized by concern: prerequisite resolution, per-artifact parsing, command handlers, view rendering, and MVU orchestration.
- **FR-002**: The public signature file `CommandWorkflow.fsi` MUST remain byte-identical to its pre-refactor state (the two `val init` / `val update` declarations under `module CommandWorkflow`).
- **FR-003**: Every command's deterministic output (default/`--json` projection, exit code, and stdout/stderr routing) MUST remain byte-for-byte identical for identical inputs — no golden or surface-baseline file may require regeneration.
- **FR-004**: No single resulting source file may exceed approximately 1,500 lines.
- **FR-005**: Each resulting file MUST be cohesive — named for and containing one concern (or one handler family) — so any lifecycle stage's logic is locatable without reading unrelated concerns.
- **FR-006**: The change MUST be behavior-preserving: no observable change to diagnostics, effects, artifact contents, or control flow for any command, including the intentionally divergent `computeRefreshPlan` guard.
- **FR-007**: The solution MUST build cleanly (`dotnet build -c Release`) with no new errors and no new warning categories beyond those already present on `main` (e.g. the existing FS3261 site count must not increase as a result of the reorganization).
- **FR-008**: The established one-way layering (`Artifacts → Commands → Cli`/`Validation`) MUST be preserved with no new dependency cycle introduced, and the `.fsproj` compile order MUST be valid.
- **FR-009**: The full existing test suite (438 tests) MUST pass against the refactored tree without modification to test assertions.
- **FR-010**: The refactor analysis report's roadmap (`docs/reports/2026-06-26-074428-refactor-analysis.md`) MUST be updated to mark R2 ✅ with evidence (spec readiness / commit) once the work lands, and the aggregate count updated.

### Key Entities

- **CommandWorkflow facade**: the public module retaining exactly `init` and `update`, delegating to internal concern modules.
- **Prerequisites module**: prerequisite resolution (`resolvePrerequisites`, the `runHandler` shell, and related diagnostic/cascade helpers landed by R1).
- **Parsing module(s)**: per-artifact parse helpers feeding the handlers.
- **Handlers module(s)**: the twelve `compute*Plan` stage handlers (optionally one submodule per stage or family).
- **ViewRendering module**: generated-view-state construction and rendering helpers shared across handlers.
- **Orchestration**: `nextLifecycleEffects`, `init`, `update` MVU wiring.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The largest source file in `src/FS.GG.SDD.Commands/` (currently 6,814 lines) is reduced to ≤ ~1,500 lines, with the workflow split across multiple cohesive files.
- **SC-002**: `CommandWorkflow.fsi` shows a zero-byte diff against `main`.
- **SC-003**: 100% of the existing 438 tests pass with zero golden/baseline/surface files regenerated.
- **SC-004**: Every command's JSON output is byte-identical before and after for a representative input matrix (the deterministic suite is the proxy and shows no diff).
- **SC-005**: A maintainer can locate any of the twelve `compute*Plan` handlers and its prerequisite/view logic by file name alone, without scanning unrelated concerns.
- **SC-006**: The Release build succeeds with no new warning categories and no increase in the existing FS3261 site count attributable to the reorganization.

## Assumptions

- "~1,500 lines" is the target ceiling from the roadmap's R2 definition of done, treated as a soft cap (small overruns acceptable if a single binding is genuinely larger).
- There are no external consumers of internal `CommandWorkflow` bindings; only `init`/`update` are public, so internal reorganization carries near-zero blast radius — but, unlike R3, R2 is held to the stricter byte-stable `.fsi` + JSON gate per the roadmap.
- The R3 pattern (per-family `[<AutoOpen>]` module files under a folder, ordered for F#'s compiler, fronted by shared `Internal`/`Core` modules) is the reference precedent for how to structure the split; the exact module/file layout is an implementation decision deferred to `/speckit-plan`.
- The intentional design exceptions recorded by R1 (notably `computeRefreshPlan` keeping its own guard) are preserved verbatim, not "cleaned up" under cover of this move.
- No new functionality, diagnostics, effects, or output formatting is introduced; this is reorganization only.
- The existing test suite provides sufficient behavioral coverage to guarantee equivalence; no new behavioral tests are required (a structural assertion on file size/layout is optional and decided at planning).
