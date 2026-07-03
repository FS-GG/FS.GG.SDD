# Implementation Plan: Test-infrastructure hardening

**Branch**: `067-test-infra-hardening` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/067-test-infra-hardening/spec.md`

## Summary

Pay down the accumulated test-suite defects from roadmap #75 / the 2026-07-02
review so the suite is **deterministic, honest, self-cleaning, and
de-duplicated** — without changing any observable product contract. The work is
six independent clusters: serialize process-global env mutation (P1), resolve 106
orphaned fixture manifests (P2), unify the baseline-regeneration switch (P2),
make temp artifacts self-cleaning on both the test and `validate`-harness sides
(P2), make the validation-harness environment cells exercise genuinely distinct
conditions (P3), and consolidate triplicated test helpers via the repo's existing
linked-shared-file pattern (P3). Two clusters (temp cleanup for `validate`; the
env cells) touch internal `src/FS.GG.SDD.Validation` code but are contract-neutral
(all functions are `.fs`-internal; the `validation-report` is not byte-pinned).
Approach and rationale per cluster: [research.md](./research.md).

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`).

**Primary Dependencies**: xUnit (`Collection` / `CollectionBehavior` /
assembly-fixture semantics), the existing `<Compile Include>` linked-file sharing
pattern already used by Cli.Tests and Validation.Tests. No new dependency.

**Storage**: N/A (temp filesystem only; the whole point is to stop leaking it).

**Testing**: `dotnet test` across the six test projects; new guard/meta-tests
(fixture-manifest guard, `ProcessGlobalEnv` attribute guard).

**Target Platform**: Linux/macOS/Windows CI + developer machines.

**Project Type**: Single F# solution (CLI product + libraries + tests).

**Performance Goals**: No suite slowdown beyond the deliberate serialization of
the process/env test classes; the 27 pure Commands.Tests classes stay parallel.

**Constraints**: Tier 2, contract-neutral. No change to any `.fsi` surface,
committed baseline, golden/deterministic fixture, `validation-report` schema or
verdict structure, persisted schema, or agent-skill contract (see
[contracts/preserved-contracts.md](./contracts/preserved-contracts.md)).

**Scale/Scope**: 6 test projects, 1 product library (`FS.GG.SDD.Validation`
internals), 107 fixture manifests, 5 surface baselines, ~4 duplicated helpers.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec→FSI→Tests→Impl | ✅ | No new public surface; `.fsi` files unchanged (verified: helpers are `.fs`-internal). The "spec the outcome first" order is honored via this spec/plan. |
| II. Structured artifacts are the contract | ✅ | No artifact/schema change; the guardrail is that structured contracts stay byte-identical. |
| III. Visibility in `.fsi` | ✅ | Confirmed no `src/**/*.fsi` edit. `ValidationRunner.fsi` exposes only `run`/`RunnerOptions`/`defaultOptions`. |
| IV. Idiomatic simplicity | ✅ | Plain xUnit collections, one linked shared file, `try/finally` cleanup. No new abstraction. |
| V. Elmish/MVU boundary | ✅ | The `validate` harness keeps its existing structure; internal cleanup/env changes don't cross the boundary. No new stateful workflow. |
| VI. Test evidence mandatory | ✅ | New behavior (isolation, guards, cleanup) is itself proven by tests (meta-guards, temp-growth check). Real fixtures, no mocks. |
| VII. One contract for agents+humans | ✅ | No lifecycle-artifact or agent-surface change. |
| VIII. Observability & safe failure | ✅ | Cleanup is failure-safe (`finally`); guards fail loudly with actionable messages. |

**Change tier**: Tier 2 (internal). Justification for the internal
`src/FS.GG.SDD.Validation` edits is recorded in Complexity Tracking below and in
research Decisions 4–5 — they change no public surface and no emitted contract.

**Gate result**: PASS (no violations requiring waiver).

## Project Structure

### Documentation (this feature)

```text
specs/067-test-infra-hardening/
├── plan.md                         # This file
├── research.md                     # Phase 0 — the six decisions
├── data-model.md                   # Phase 1 — test-support constructs
├── quickstart.md                   # Phase 1 — per-SC validation recipes
├── contracts/
│   └── preserved-contracts.md      # Phase 1 — the Tier-2 "must-not-change" guardrail
├── checklists/
│   └── requirements.md             # spec quality checklist (from /speckit-specify)
└── tasks.md                        # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
tests/
├── Shared/
│   └── TestShared.fs               # NEW — single home for findRepoRoot/repoRoot/
│                                   #        writeRelative/tempDirectory+cleanup/
│                                   #        SurfaceBaseline.verify/evidenceLadder
├── FS.GG.SDD.Commands.Tests/
│   ├── TestSupport.fs              # delegate to TestShared; drop duplicated bodies
│   ├── ScaffoldCommandTests.fs     # → [<Collection("ProcessGlobalEnv")>]
│   ├── ScaffoldCliCoherenceTests.fs# Scaffold → ProcessGlobalEnv
│   ├── RemediationCommandTests.fs  # Console → ProcessGlobalEnv
│   ├── <other process-spawning classes> # → ProcessGlobalEnv
│   ├── ProcessGlobalEnvGuardTests.fs   # NEW — meta-test: every spawner is in the collection
│   ├── SurfaceBaselineTests.fs     # → TestShared.SurfaceBaseline.verify
│   └── FixtureManifestGuardTests.fs    # NEW — no orphaned lifecycle-command manifest
├── FS.GG.SDD.Acceptance.Tests/
│   ├── AssemblyInfo.fs             # NEW/edit — DisableTestParallelization
│   └── AcceptanceSupport.fs        # drop duplicated findRepoRoot/writeRelative → TestShared
├── FS.GG.Contracts.Tests/
│   ├── TestSupport.fs              # delegate to TestShared
│   └── PublicSurfaceTests.fs       # → TestShared.SurfaceBaseline.verify
├── FS.GG.SDD.Artifacts.Tests/
│   ├── TestSupport.fs              # delegate to TestShared
│   └── SurfaceBaselineTests.fs     # → TestShared.SurfaceBaseline.verify
├── FS.GG.SDD.Cli.Tests/
│   └── SurfaceBaselineTests.fs     # → TestShared.SurfaceBaseline.verify
├── FS.GG.SDD.Validation.Tests/
│   └── SurfaceBaselineTests.fs     # → TestShared.SurfaceBaseline.verify
└── fixtures/lifecycle-commands/    # 106 orphans: wire-in-or-delete per Phase-2 inventory

src/FS.GG.SDD.Validation/
└── ValidationRunner.fs             # internal only: temp-cleanup nesting + delete in finally;
                                    # withPerturbedHost varies cwd; degradation cells set NO_COLOR/TERM
```

**Structure Decision**: Single-solution layout is unchanged. The one new
structural element is `tests/Shared/TestShared.fs`, linked via `<Compile Include>`
into all six test `.fsproj` files — extending the pattern the repo already uses
(Cli.Tests and Validation.Tests link `../FS.GG.SDD.Commands.Tests/TestSupport.fs`).
No new project, no `.sln` change.

## Delivery order (independent, testable slices)

Each user story is an independent slice — implementable and verifiable on its own.
Recommended order maximizes de-risking:

1. **US1 (P1)** — env-race isolation + `ProcessGlobalEnv` guard. Highest value
   (kills the only intermittent-CI defect); independent of the others.
2. **US6 (P3), then US3 (P2)** — land `tests/Shared/TestShared.fs` (helpers) first
   because US3's `SurfaceBaseline.verify` lives there; doing US6's shared-file
   plumbing first makes US3 a small rewire. (Ordering exception: a low-priority
   slice enables a higher one at near-zero cost.)
4. **US4 (P2)** — temp cleanup (test side uses `TestShared.runTempRoot`; product
   side is a self-contained `run` edit).
5. **US2 (P2)** — fixture inventory + wire/delete + guard test.
6. **US5 (P3)** — validation-harness cell genuineness.

Nothing forces this order except the US6→US3 file dependency; `/speckit-tasks`
will encode the true dependency graph.

## Complexity Tracking

> Filled because the Tier-2 boundary is deliberately stretched to cover two
> internal product-code clusters. This is a justification, not a violation.

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| Editing `src/FS.GG.SDD.Validation/ValidationRunner.fs` (product code) in a "test-infra" feature | The temp-leak (~350 copies/run) and the vacuous env cells are literally *in* the `validate` harness; #75 scopes them in. All edits are `.fs`-internal and contract-neutral (research Decisions 4–5). | Leaving them = the leak and the false coverage persist; the roadmap item stays unresolved. A separate feature would fragment one coherent "test/harness hardening" unit for no benefit. |
| New `tests/Shared/TestShared.fs` linked into 6 projects | Removes 4× `findRepoRoot` / 2× `writeRelative` duplication and gives US3's `SurfaceBaseline.verify` a single home. | A new shared `.fsproj` is heavier (sln + packaging) than the linked-file pattern the repo already uses; per-project duplication is exactly what we're removing. |
| `ProcessGlobalEnv` meta-guard test | Without it, the race silently returns the first time a new process-spawning class forgets the collection attribute. | A comment/convention is not enforced; the whole point of #75 is that silent test-infra drift caused this. |
| Keeping library `Rich→text` (not making Rich emit ANSI) | Emitting true Spectre ANSI would add a Spectre dependency to the validation library and reverse research Decision 6 — a Tier-1 change. The Rich ANSI guarantee is already held by `ValidateCommandTests`. | "Fully genuine" Rich cell isn't worth a Tier-1 architectural reversal that duplicates an existing guarantee. FR-009 narrowed to match. |
