# Implementation Plan: Split CommandWorkflow into facade + internal modules

**Branch**: `025-split-command-workflow` | **Date**: 2026-06-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/025-split-command-workflow/spec.md`

## Summary

`src/FS.GG.SDD.Commands/CommandWorkflow.fs` is one flat `module CommandWorkflow`
of 6,814 lines (268 bindings) whose public surface is only `init`/`update` (a
7-line `.fsi`). This refactor reorganizes the flat module into a thin facade
(`module CommandWorkflow`, keeping `init`/`update`) over `[<AutoOpen>]` internal
concern modules placed in a **child namespace** `FS.GG.SDD.Commands.Internal`,
so each file stays ≤ ~1,500 lines, the `.fsi` stays byte-identical, every
command's deterministic JSON stays byte-for-byte identical, and the 438-test
suite passes unchanged. It is R2 in
`docs/reports/2026-06-26-074428-refactor-analysis.md`, mirroring the R3 split of
`LifecycleArtifacts.fs` (per-family `[<AutoOpen>]` files under a folder, ordered
for F#'s compiler).

The central technical choice (Phase 0): put the internal modules in a **child
namespace** so AutoOpen visibility is scoped to the workflow files and the facade
(`open FS.GG.SDD.Commands.Internal`), and does **not** leak ~260 internal
bindings into sibling files `CommandEffects`/`CommandSerialization`/
`CommandRendering` (same parent namespace). Within the child namespace the
AutoOpen modules see each other automatically, so the moved bodies need
near-zero call-site rewrites — the primary lever for byte-stable behavior.

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`)

**Primary Dependencies**: FSharp.Core; `FS.GG.SDD.Artifacts` project reference
(unchanged); YamlDotNet (transitive, via parsing helpers).

**Storage**: N/A (lifecycle artifacts are files on disk, written via `Effect`s;
this refactor changes no I/O).

**Testing**: xUnit-style suite under `tests/` (438 tests across
`FS.GG.SDD.{Artifacts,Commands,Cli,Validation}.Tests`), including
deterministic/golden JSON fixtures. The suite is the behavioral guard.

**Target Platform**: Linux/CLI (`fsgg-sdd`).

**Project Type**: Single solution; F# library/CLI. Refactor is confined to one
project (`FS.GG.SDD.Commands`).

**Performance Goals**: No change. Reorganization only; no hot-path edits.

**Constraints** (binding gates, from spec FR-001…FR-010):
- `CommandWorkflow.fsi` byte-identical to `main` (zero-byte diff).
- Every command's default/`--json` output byte-for-byte identical; no golden,
  surface-baseline, or `release-readiness` regeneration.
- No single resulting source file > ~1,500 lines (soft cap).
- `dotnet build -c Release` clean: no new errors, no new warning categories, and
  no increase in the existing FS3261 unique-site count (~290 in `src` per the
  R2 baseline) attributable to the reorganization.
- One-way layering `Artifacts → Commands → Cli`/`Validation` preserved; no new
  cycle; valid `.fsproj` compile order.
- `computeRefreshPlan`'s self-contained guard (does not route through
  `runHandler`) preserved verbatim.

**Scale/Scope**: ~6,814 LOC moved across ~13 new internal files + 1 facade; one
`.fsproj` compile-list rewrite; one report-roadmap update. No new bindings.

**Change Tier**: **Tier 2 (internal change)** — implementation cleanup with no
user- or tool-visible contract change. Signatures and baselines remain
unchanged. Requires spec + tests (the existing suite); no `.fsi`/baseline edits.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → tests → impl | ✅ | Public `.fsi` is fixed byte-for-byte (FR-002); no new surface to sketch. The existing semantic suite is the guard (FR-009). |
| II. Structured artifacts are the contract | ✅ N/A | No artifact/schema change; JSON contracts held byte-stable (FR-003). |
| III. Visibility lives in `.fsi` | ✅ | Only `init`/`update` stay public via the unchanged `CommandWorkflow.fsi`. Internal modules are `.fsi`-less `module internal` in a child namespace — the exact precedent set by R3's `LifecycleArtifacts/Internal.fs`. No new public surface, so no new baseline. |
| IV. Idiomatic simplicity | ✅ | Plain modules + `[<AutoOpen>]`, already idiomatic in this repo (R3). No custom operators, SRTP, reflection, or CEs introduced. |
| V. Elmish/MVU is the boundary | ✅ | The facade preserves the `init`/`update` MVU boundary exactly; `Model`/`Msg`/`Effect` types are untouched in `CommandTypes`. |
| VI. Test evidence is mandatory | ✅ | Behavior-**preserving** change: the existing 438-test deterministic/golden suite is the fail-if-regressed guard (FR-003/FR-009). An optional structural test (file ≤1,500 lines / facade-surface assertion) may be added but no new behavioral test is required. |
| VII. Agent + human one contract | ✅ N/A | No agent-surface or lifecycle-artifact behavior change. |
| VIII. Observability & safe failure | ✅ | Diagnostics, effects, and control flow are preserved verbatim (FR-006), including `computeRefreshPlan`'s divergent guard. |

**Result**: PASS. No violations → Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/025-split-command-workflow/
├── plan.md              # This file
├── research.md          # Phase 0: decomposition strategy decisions
├── data-model.md        # Phase 1: module/file decomposition model
├── quickstart.md        # Phase 1: byte-stability validation guide
├── contracts/
│   ├── public-contract.md   # Invariants: .fsi byte-diff, JSON byte-stable, build/warnings
│   └── module-layout.md     # Internal module/file layout + compile order contract
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

Before — one flat module:

```text
src/FS.GG.SDD.Commands/
├── CommandTypes.fsi / .fs
├── CommandReports.fsi / .fs
├── CommandWorkflow.fsi          # 7 lines — UNCHANGED by this refactor
├── CommandWorkflow.fs           # 6,814 lines, one flat module  ← split target
├── CommandEffects.fsi / .fs
├── CommandSerialization.fsi / .fs
└── CommandRendering.fsi / .fs
```

After — facade over child-namespace internal modules (`.fsi`-less, AutoOpen,
mirroring `LifecycleArtifacts/`):

```text
src/FS.GG.SDD.Commands/
├── CommandTypes.fsi / .fs                  (unchanged)
├── CommandReports.fsi / .fs                (unchanged)
├── CommandWorkflow/                         ← new folder, child namespace
│   │                                          FS.GG.SDD.Commands.Internal
│   ├── Foundation.fs        # paths, config text, *ReadEffects, plan routing,
│   │                        #   effect tracking, model nav, YAML helpers, base types
│   ├── ParsingEarly.fs      # Charter + Specification + Clarification parse/template/diag
│   ├── ParsingMid.fs        # Checklist + Plan parse/template/diag
│   ├── ParsingTasks.fs      # Tasks parse/template/diag (+ evidence parse helper)
│   ├── ViewGeneration.fs    # analysis sources, generatedViewState/Plan, workModelSnapshots
│   ├── Prerequisites.fs     # resolvePrerequisites cascade + runHandler shell
│   ├── HandlersEarly.fs     # Charter/Specify/Clarify/Checklist/Plan/Tasks handlers
│   ├── HandlersAnalyze.fs   # computeAnalyzePlan (+ its JSON/view builders)
│   ├── HandlersEvidence.fs  # computeEvidencePlan (+ obligations/artifact text)
│   ├── HandlersVerify.fs    # computeVerifyPlan (+ verify JSON/views)
│   ├── HandlersShip.fs      # computeShipPlan (+ ship JSON / governance handoff)
│   ├── HandlersAgents.fs    # computeAgentsPlan (agents config + guidance)
│   └── HandlersRefresh.fs   # computeRefreshPlan (self-contained guard — preserved)
├── CommandWorkflow.fsi                      (UNCHANGED — byte-identical)
├── CommandWorkflow.fs       # facade: module CommandWorkflow; opens Internal;
│                            #   nextLifecycleEffects + init + update only (~150 ln)
├── CommandEffects.fsi / .fs                 (unchanged — NOT polluted: no `open Internal`)
├── CommandSerialization.fsi / .fs          (unchanged)
└── CommandRendering.fsi / .fs              (unchanged)
```

**Structure Decision**: Mirror the R3 precedent (`LifecycleArtifacts/` folder of
per-family `[<AutoOpen>]` modules + `.fsi`-less `Internal.fs`), with one
deliberate strengthening: internal modules live in the **child namespace**
`FS.GG.SDD.Commands.Internal` rather than the parent. This scopes AutoOpen so the
facade opts in with one `open` and sibling files are untouched. Line ranges in
the tree above are the current section boundaries (see `data-model.md`); the
exact cut points are settled during implementation under the ≤1,500-line cap.
The proposed 13-file split keeps the largest file (`ParsingMid` ≈ 1,000 ln,
`computeAnalyzePlan` family ≈ 600 ln) comfortably under the cap.

## Complexity Tracking

> No Constitution Check violations. No entries required.

## Phase 0 — Research

See [research.md](./research.md). Decisions resolved:
1. **Namespace scoping** — child namespace `FS.GG.SDD.Commands.Internal` (not
   parent) to bound AutoOpen visibility and avoid sibling-file pollution.
2. **AutoOpen vs explicit `open`** — `[<AutoOpen>] module internal` within the
   child namespace, so cross-internal references need no qualification and moved
   bodies are byte-stable.
3. **The seven artifact-namespace aliases** — module abbreviations are file-local
   in F#; redeclare the needed `module X = FS.GG.SDD.Artifacts.Y` header in each
   internal file that uses them (keeps body references unchanged).
4. **Compile order** — emit internal files in the original top-to-bottom
   dependency order, facade last; preserves F#'s definition-before-use.
5. **Collision / shadowing safety** — child-namespace scoping eliminates
   sibling-file leakage; intra-workflow collisions are impossible (one flat
   module split) and any residual is a hard build error caught by FR-007.
6. **Incremental landing** — each commit keeps the build green, suite green, and
   `.fsi`/JSON byte-stable (FR + edge case).

## Phase 1 — Design & Contracts

- [data-model.md](./data-model.md): the module decomposition model — every
  internal module, its concern, its source line range, and its dependency edges.
- [contracts/public-contract.md](./contracts/public-contract.md): the byte-stable
  invariants (`.fsi` diff = 0; JSON byte-identical; build clean; FS3261 not
  increased; layering preserved) and how each is verified.
- [contracts/module-layout.md](./contracts/module-layout.md): the internal
  layout + `.fsproj` compile-order contract.
- [quickstart.md](./quickstart.md): runnable validation sequence.
- Agent context: `CLAUDE.md` SPECKIT marker updated to point at this plan.

## Post-Design Constitution Re-check

Re-evaluated after Phase 1: no new public surface, no new dependencies, no
complex-feature introduction. Still **PASS**, Tier 2.
