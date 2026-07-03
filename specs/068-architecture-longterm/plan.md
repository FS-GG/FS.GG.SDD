# Implementation Plan: Architecture longer-term cleanups

**Branch**: `068-architecture-longterm` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/068-architecture-longterm/spec.md`

## Summary

Close the final "longer-term" architecture bundle from roadmap #76 / the
2026-07-02 review (item #12; items #1–#11 shipped as features 059–067) **without
changing any observable product contract**. Six independent clusters over
`src/FS.GG.SDD.Commands` internals plus two agent-context docs: (1) extract one
`writeReadinessEnvelope` frame so `analysis`/`verify`/`ship` JSON cannot drift
structurally — P1; (2) DU-ify the stringly view-currency and upgrade-outcome state
in refresh/upgrade/drift — P2; (3) reconcile `CLAUDE.md`/`AGENTS.md` to identical
content behind a byte-identity drift guard — P2; (4) drop the blanket
`[<AutoOpen>]` across `CommandWorkflow/` — P2; (5) rename the `Parsing{Early,Mid,
Tasks}` slabs by responsibility — P3; and (6) fix two §1.5 purity soft spots and
document the third — P3. Approach and grounding per cluster:
[research.md](./research.md). The Tier-2 guardrail is
[contracts/preserved-contracts.md](./contracts/preserved-contracts.md).

## Technical Context

**Language/Version**: F# on .NET 10 (`net10.0`).

**Primary Dependencies**: `System.Text.Json` (`Utf8JsonWriter`) for the readiness
views; xUnit for the new byte-identity guard. No new dependency.

**Storage**: N/A (no persisted schema change; the readiness views' bytes are
frozen).

**Testing**: `dotnet test FS.GG.SDD.sln` across all projects; one new guard
(`CLAUDE.md == AGENTS.md`); DU exhaustiveness is compiler-enforced. Existing
refresh/upgrade/verify/ship/analysis facts are the regression net.

**Target Platform**: Linux/macOS/Windows CI + developer machines.

**Project Type**: Single F# solution (CLI product + libraries + tests).

**Performance Goals**: None — pure refactor; no hot-path or allocation change of
consequence.

**Constraints**: Tier 2, contract-neutral. No change to any `.fsi` surface,
committed baseline, readiness/JSON golden fixture, persisted schema/version,
exit-code taxonomy, stream routing, agent-skill contract, or the
`validation-report`. The one deliberate content change is `AGENTS.md` reconciled
up to equal `CLAUDE.md`. See [contracts/preserved-contracts.md](./contracts/preserved-contracts.md).

**Scale/Scope**: 1 product library (`FS.GG.SDD.Commands`, mainly its 17
`CommandWorkflow/` files) + 1 documented edge in `FS.GG.SDD.Artifacts`
(`RegistryDocument.load`) + 2 repo-root docs + 1 new test. ~450 lines of
readiness-writer duplication and ~30 stringly-state sites addressed.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec→FSI→Tests→Impl | ✅ | No new public surface, so no `.fsi` sketch needed; all new constructs are `.fs`-internal (verified: `CommandWorkflow/` modules carry no `.fsi`; `Drift.Step`, `RefreshViewState`, `UpgradeStepOutcome`, `writeReadinessEnvelope` are internal). Spec-first order honored via this spec/plan. |
| II. Structured artifacts are the contract | ✅ | No artifact/schema change; the guardrail *is* that the structured contracts stay byte-identical (FR-010). |
| III. Visibility in `.fsi` | ✅ | Zero `src/**/*.fsi` edit; PublicSurface baselines unchanged. The whole feature lives below the signature line. |
| IV. Idiomatic simplicity | ✅ | Replaces stringly state with plain DUs, one frame function, explicit `open`s — *reduces* cleverness. No new abstraction, no CE/SRTP/reflection. |
| V. Elmish/MVU boundary | ✅ | The pure-core/effect-edge boundary is *strengthened* (Decision 5 removes ambient-cwd + static-init throw from pure code). No new stateful workflow; the interpreter is untouched. |
| VI. Test evidence mandatory | ✅ | Behavior is unchanged, so the existing suite is the fail-before/pass-after net for the refactors; the one new behavior (docs byte-identity) gets its own guard. Real fixtures, no mocks. |
| VII. One contract for agents+humans | ✅ | Directly *serves* this principle: US6 makes the Claude and Codex context docs one identical contract (`claude ≡ codex`), matching the seeded-skill invariant. |
| VIII. Observability & safe failure | ✅ | Decision 5 converts an opaque static-init `TypeInitializationException` into an actionable diagnostic; the new guard fails loudly on doc drift. |

**Change tier**: Tier 2 (internal). The single deliberate content change
(`AGENTS.md`) adds no contract; every machine-facing surface is frozen and
diff-verified. Justification for touching product code (not just tests) is the
nature of the roadmap item — it *is* a source refactor — recorded in Complexity
Tracking.

**Gate result**: PASS (no violations requiring waiver).

## Project Structure

### Documentation (this feature)

```text
specs/068-architecture-longterm/
├── plan.md                         # This file
├── research.md                     # Phase 0 — six design decisions (grounded)
├── data-model.md                   # Phase 1 — internal DUs, envelope fn, purity ledger
├── quickstart.md                   # Phase 1 — per-SC validation recipes
├── contracts/
│   └── preserved-contracts.md      # Phase 1 — the Tier-2 "must-not-change" guardrail
├── checklists/
│   └── requirements.md             # spec quality checklist (from /speckit-specify)
└── tasks.md                        # Phase 2 — created by /speckit-tasks (NOT here)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Commands/CommandWorkflow/
├── ViewGeneration.fs        # NEW writeReadinessEnvelope + writeGovernanceReadinessTail;
│                            #   analysisJson becomes a thin caller
├── HandlersVerify.fs        # verifyJson → thin caller of the envelope + governance tail
├── HandlersShip.fs          # shipJson   → thin caller of the envelope + governance tail
├── HandlersRefresh.fs       # RefreshViewState DU replaces ~30 stringly sites
├── HandlersUpgrade.fs       # UpgradeStepOutcome DU replaces outcome literals
├── Drift.fs                 # Step.Outcome : UpgradeStepOutcome (was string)
├── SeededSkills.fs          # seededSkills → lazy/function; actionable missing-resource msg
├── Foundation.fs            # projectIdFromRoot no longer depends on ambient cwd
├── ParsingEarly.fs  → renamed by responsibility (e.g. SpecStageParsing)
├── ParsingMid.fs    → renamed by responsibility (e.g. PlanStageParsing)
├── ParsingTasks.fs  → renamed by responsibility (e.g. TaskGraphParsing)
└── <the other 12 files>     # drop [<AutoOpen>]; explicit open / qualified access
                             #   (Drift, SeededSkills already model the target)

src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj   # <Compile Include> updated for renames

src/FS.GG.SDD.Artifacts/LifecycleArtifacts/
└── RegistryDocument.fs      # documenting comment on the intentional load IO edge

CLAUDE.md                    # authored source (unchanged content)
AGENTS.md                    # reconciled to byte-identical copy of CLAUDE.md

tests/FS.GG.Contracts.Tests/ (or FS.GG.SDD.Commands.Tests/)
├── AgentSurfaceDriftTests.fs   # NEW — asserts CLAUDE.md == AGENTS.md
└── SeededSkillsTests.fs        # re-point drift guard at the new seededSkills accessor
```

**Structure Decision**: Single-solution layout unchanged — no new project, no
`.sln` edit. All structural change is internal to `FS.GG.SDD.Commands`
(module attributes, module names, internal types, one extracted frame function),
plus one documenting comment in Artifacts, the `AGENTS.md` content reconciliation,
and one new test file. The de-AutoOpen and Parsing-rename passes are mechanical
and compiler-checked; F# compile order is preserved.

## Delivery order (independent, testable slices)

De-risked so the mechanical high-churn passes land against stable boundaries:

1. **US1 (P1)** — `writeReadinessEnvelope` + governance tail; three callers made
   thin. Verify byte-identity via the view fact-tests + contract diff. Independent.
2. **US2 (P2)** — `RefreshViewState` / `UpgradeStepOutcome` DUs + `toToken`
   projections. Independent; guarded by existing refresh/upgrade tests.
3. **US6 (P2)** — reconcile `AGENTS.md` to `CLAUDE.md`; add byte-identity guard.
   Fully independent of the code clusters; can land any time.
4. **US5 (P3)** — purity soft spots (SeededSkills lazy + message; projectIdFromRoot
   edge resolution; RegistryDocument comment). Small, contained.
5. **US4 (P3)** — Parsing renames (name-only; `.fsproj` order preserved).
6. **US3 (P2, but sequenced last)** — drop `[<AutoOpen>]` across the 15 modules.
   Highest churn; do it **after** 1/2/4/5 so it rebases over settled module names
   and new symbols, as one mechanical pass behind a green build+suite gate.

Only US3-after-the-rest and US1-before-nothing are true ordering constraints;
`/speckit-tasks` encodes the real dependency graph. Each slice ends with the
contract-diff gate empty and the suite green.

## Complexity Tracking

> Filled because the feature edits product code (not just tests) and deliberately
> changes one doc's content. Both are the nature of the roadmap item, not scope
> creep — justifications, not violations.

| Item | Why needed | Simpler alternative rejected because |
|---|---|---|
| Editing `src/FS.GG.SDD.Commands` internals broadly | Roadmap #76 *is* a source refactor (envelope, DUs, de-AutoOpen, renames). All edits are `.fs`-internal and contract-diff-verified. | Not doing it leaves the roadmap item open; a "docs-only" reading would abandon the item's substance. |
| `writeReadinessEnvelope` owns only the frame (not the full body) | The three views' tail ordering genuinely differs (`analysis` emits findings before generatedViews; verify/ship after; different boundary key). Owning only the invariant frame keeps output byte-identical. | A fuller data-driven envelope would have to re-derive the exact byte layout from a generic serializer — high risk against the byte-identity constraint (research Decision 1). |
| Reconciling `AGENTS.md` content (a real doc change in a "no-contract-change" feature) | The two agent surfaces carry identical agent-agnostic doctrine and `AGENTS.md` is missing ~26 lines; fixing it *is* US6. It adds no machine contract. | Leaving them divergent keeps a Codex agent under-informed and violates the repo's own `claude ≡ codex` doctrine (Principle VII). |
| Documenting (not relocating) `RegistryDocument.load` IO | Relocating the read to the host crosses the layering/host boundary → Tier-1, rippling into CLI + registry-validate call sites. | Forcing a Tier-1 architectural move inside a Tier-2 batch would enlarge blast radius for a low-risk, already-contained edge (research Decision 5). Deferred as a follow-up. |
| `[<AutoOpen>]` removal across 15 files (large diff) | It is the review's explicit item (call-site provenance); `Drift`/`SeededSkills` already prove the target pattern compiles. | A comment/convention doesn't restore provenance; leaving it keeps the flat-scope trap the review flagged. Sequenced last to minimize rebase churn. |

## Notes for `/speckit-tasks`

- Every task must end at the **contract-diff gate**: `git diff` over
  `**/*.baseline`, `src/**/*.fsi`, and the readiness/JSON golden fixtures is empty.
- The DU `toToken` functions are the *only* place the wire tokens may appear after
  US2; a task should grep-assert no stray literals remain.
- The Parsing-rename task is name-only and must update
  `FS.GG.SDD.Commands.fsproj` compile order in lockstep (F# is order-sensitive).
- The de-AutoOpen task is the last code task and should be a single mechanical
  pass with the build+suite as its gate.
