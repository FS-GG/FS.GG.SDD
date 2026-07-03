# Implementation Plan: Drop the blanket `[<AutoOpen>]` in `CommandWorkflow/`

**Branch**: `069-de-autoopen` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

## Summary

Execute the deferred US3 of feature 068: remove `[<AutoOpen>]` from the sixteen
`CommandWorkflow/*.fs` modules that carry it, restoring call-site provenance via
qualified access or explicit file-scoped `open`. Pure internal refactor, Tier-2,
contract-neutral (per [068 preserved-contracts](../068-architecture-longterm/contracts/preserved-contracts.md)).

## Technical Context

- **Language**: F# on .NET 10 (`net10.0`); warnings-as-errors is on (baseline
  build = **0 warnings, 0 errors**).
- **Blast radius**: one library, `FS.GG.SDD.Commands`, its `CommandWorkflow/`
  directory (18 files, ~11.5k lines). No `.fsi` touched.
- **Regression net**: the full `dotnet test FS.GG.SDD.sln` suite plus the
  contract-diff checks (`src/**/*.fsi`, `**/*.baseline`, readiness/JSON goldens).

## Approach (compiler-driven, incremental)

The compile order is fixed by the fsproj:

```
SeededSkills · Drift            (already de-AutoOpened — the target model)
Foundation                       (ubiquitous)
EarlyStageAuthoring · ChecklistPlanAuthoring · TaskGraphAuthoring
ViewGeneration · Prerequisites
HandlersEarly · HandlersAnalyze · HandlersEvidence · HandlersVerify ·
HandlersShip · HandlersAgents · HandlersRefresh · HandlersScaffold ·
HandlersDoctor · HandlersUpgrade
```

1. Remove `[<AutoOpen>]` from each of the sixteen carriers.
2. Build. The compiler reports every now-unresolved name, file by file, in compile
   order. For each consuming file, add the minimal set of file-scoped
   `open FS.GG.SDD.Commands.Internal.<Module>` at the top — preferred for
   ubiquitous modules (`Foundation`) — and **qualify** the specific call sites
   where two opened modules expose the same name (the ambiguity the spike flagged).
3. Rebuild after each file until green with **no new warnings**.

Adding a file-scoped `open` per referenced sibling reproduces the exact resolution
AutoOpen gave (namespace-order = compile-order = open-order), so behavior is
preserved by construction; the only real work is resolving same-name collisions by
qualification. Prefer `open` for low-collision ubiquitous modules and qualification
where names collide, so provenance is legible without gratuitous churn.

## Constitution Check

- **Contract-first / Markdown-vs-machine**: no machine contract changes; guardrail
  is the empty `.fsi`/baseline/golden diff. PASS.
- **Real evidence**: verification is a real green build + real full test run + real
  empty contract diff, not synthetic. PASS.
- **MVU boundaries**: refactor does not cross the pure `update` / effect edge; no
  effect or handler semantics change. PASS.

## Risks

- **Warnings-as-errors ambiguity** (the reason 068 deferred): mitigated by
  building incrementally in compile order and qualifying colliding sites rather
  than blanket-opening. If a collision cannot be resolved without behavior change,
  stop and document — never change semantics to satisfy the compiler.
