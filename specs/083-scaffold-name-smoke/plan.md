# Implementation Plan: Composition Smoke — Hyphenated Scaffold Name Builds and Tests Green

**Branch**: `083-scaffold-name-smoke` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/083-scaffold-name-smoke/spec.md`

## Summary

The network-gated composition-acceptance lane scaffolds the real provider once, builds it,
and starts it — but only into a **sensed temp directory** (never forwarding a name that is an
illegal F# identifier) and it never runs the produced product's **test** suite. So the one
lane that exercises the real provider does not guard feature 080's name→identifier
sanitization (C1) from regressing — which is exactly the *Hollow Depths* footgun
(`Roquelike-DungeonCrawler` → `FS0010: Unexpected keyword 'open'`). This feature implements
feature 080 **FR-011**, the CI smoke deferred when #150 was blocked on FS.GG.Rendering#142.

The fix adds one **network-gated composition-acceptance fact** that:

- builds a scaffold request forwarding, on the **provider-declared name parameter**
  (`Fsgg.Provider.resolveNameParameter descriptor`, resolved from the registry copied into
  `.fsgg/providers.yml` — never a hardcoded `productName`), the hyphenated value
  `Roquelike-DungeonCrawler`. SDD's existing 080 `deriveIdentifierParameter` then derives and
  forwards the valid identifier automatically — the end-to-end path under test;
- asserts the produced product's `dotnet build` **and** `dotnet test` are green (reusing the
  existing bounded process-shell edge `runToCompletion`), failing the fact naming the first
  failing step;
- resolves an unreachable provider to the honest `skip-unavailable` verdict (existing mapping)
  and self-skips at discovery when `FSGG_SDD_ACCEPTANCE_REGISTRY` is unset (existing
  `RequiresRegistryFact`), so the offline inner loop stays green and touches no network.

It reuses the existing `composition-acceptance` CI workflow (`--filter
kind=composition-acceptance`) — **no new job** — and introduces **no persisted-schema change**
(the `composition-acceptance-result` v1 document and its golden/determinism contracts are
untouched; the new fact is an additional gated fact, not a schema change).

**Change tier**: **Tier 2** (internal test/CI-coverage addition). The feature adds a gated
xUnit fact and a test-edge test probe; it changes **no** public F# surface, **no** persisted
schema, **no** CLI command output, and **no** agent-skill contract. It delivers spec, plan,
tasks, tests, and migration notes; no `.fsi`/baseline change is expected. (It satisfies a
Tier-1 feature's deferred FR, but the *delivery itself* touches only test/CI code.)

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (per constitution).

**Primary Dependencies**: existing repo only — the acceptance harness in
`tests/FS.GG.SDD.Acceptance.Tests` (`AcceptanceSupport`, `CompositionAcceptanceTests`,
`CompositionResult`), `Fsgg.Provider` (`ProviderDescriptor`, `resolveNameParameter`),
`FS.GG.SDD.Commands` scaffold workflow (unchanged; the 080 derivation runs as-is), xUnit. No
new external dependencies. No product-source change.

**Storage**: files only — the sensed per-run temp product root and the (unchanged) sensed
`composition-acceptance.json` result document. No database, no persisted-schema change.

**Testing**: xUnit. The new fact is `[<Trait("kind","composition-acceptance")>]` +
`[<RequiresRegistryFact>]` — network-gated, discovery-time static skip offline, runs on the
scheduled/dispatched workflow. Offline companion coverage (below) runs always. Real
filesystem/process fixtures; no mocks; no Governance runtime (existing negative-invariant
guard extends to the new source).

**Target Platform**: cross-platform CLI/library + CI (GitHub Actions), same as the repo. The
gated fact runs on `ubuntu-latest` in the existing `composition-acceptance` workflow.

**Project Type**: single project — F# CLI/library with a test suite. Change confined to the
acceptance test project.

**Performance Goals**: not a hot path. The new gated fact adds one more real scaffold + build
+ test on the (nightly/dispatch) acceptance run; each probe is bounded (300 s build/test cap
via `runToCompletion`). Offline, the fact is skipped at discovery — negligible inner-loop cost.

**Constraints**: provider-neutral — no rendering package id / template id / path / docs URL in
generic SDD; the name-param **key** comes from the registry descriptor and only the **value**
`Roquelike-DungeonCrawler` is a generic author token (spec FR-006). Offline determinism/golden
contracts for `composition-acceptance-result` v1 stay byte-stable (spec FR-008). Honest
skip-on-unavailable (spec FR-005). No new CI job (spec FR-007).

**Scale/Scope**: 1 test project touched — `CompositionAcceptanceTests.fs` (new gated fact +
its offline request-shape companion) and `AcceptanceSupport.fs` (a `testProbe` helper + a
name-forwarding request builder). No workflow YAML change expected (verify the `--filter`
already selects the new fact). ~2 files changed; 0 product-source files; 0 schema files.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec→FSI→Tests→Impl | PASS | No product-source change; the deliverable *is* the test. The gated fact fails-before (a reverted 080 derivation, or the missing test-probe) / passes-after. No new public surface to sketch as `.fsi`. |
| II. Structured artifacts are the machine contract | PASS | The `composition-acceptance-result` v1 document is unchanged; the new fact resolves the same `Verdict`. No new authoritative artifact; no schema drift. |
| III. Visibility in `.fsi` | PASS | Test project has no `.fsi`/baseline. No product `.fsi` touched. |
| IV. Idiomatic simplicity | PASS | Reuses the existing `resolveProviderDescriptor`, `resolveNameParameter`, `runToCompletion`, `RequiresRegistryFact`, and verdict resolution. Adds a thin name-forwarding request builder + a `dotnet test` probe. No new abstractions. |
| V. Elmish/MVU boundary | PASS | Drives the existing scaffold MVU loop through `runRequest` (unchanged edge); the test probe is a process-shell call at the test edge, exactly like the existing build/run probes. |
| VI. Test evidence mandatory | PASS | This feature *is* test evidence. The gated fact exercises the real provider; an offline companion asserts the request forwards the descriptor-resolved name key without naming a provider token. Real fixtures; disclosed network gate. |
| VII. Agent+human share one contract | PASS | No agent-skill/authored-surface change (a test-coverage addition, not a lifecycle-behavior change). Dual-surface rule not engaged. |
| VIII. Observability & safe failure | PASS | Unreachable provider → honest `skip-unavailable` (never a false pass, never a false SDD fail); a real compile/test failure → `fail` naming the first failing step; registry unset → discovery-time skip. Distinguishes provider defect from SDD defect via the existing diagnostic-keyed verdict. |

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/083-scaffold-name-smoke/
├── plan.md              # This file
├── research.md          # Phase 0 — mechanism decisions (name-param resolution, test probe, gating reuse)
├── data-model.md        # Phase 1 — the fact's inputs/facts/verdict (all reused; what is new vs reused)
├── quickstart.md        # Phase 1 — how to run the gated smoke locally + prove the guard fails on regression
├── contracts/           # Phase 1 — the composition-acceptance fact contract (no schema change)
│   └── composition-smoke-fact.md
├── checklists/
│   └── requirements.md  # spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
tests/FS.GG.SDD.Acceptance.Tests/
├── AcceptanceSupport.fs           # + `namedScaffoldRequest root name`: like `scaffoldRequest` but also
│                                  #   forwards (resolveNameParameter descriptor, name) resolved from the
│                                  #   registry descriptor — no hardcoded `productName`. + `testProbe`
│                                  #   (dotnet test via the existing bounded `runToCompletion` edge).
├── CompositionAcceptanceTests.fs  # + gated fact `hyphenated scaffold name builds and tests green`:
│                                  #   scaffold w/ Roquelike-DungeonCrawler → build → test → assert green /
│                                  #   skip-unavailable. + offline companion asserting the request forwards
│                                  #   the descriptor-resolved name key + hyphenated value (no provider token,
│                                  #   FR-006). Extends the existing no-Governance-reference scan set.
└── (no CompositionResult.fs change — verdict/result schema reused unchanged)

.github/workflows/composition-acceptance.yml   # verify only: `--filter kind=composition-acceptance`
                                               #   already selects the new fact; no job/YAML change expected.
```

**Structure Decision**: Single-project layout, unchanged. The change is confined to the
acceptance test project (two files); no product source, schema, workflow, or agent surface
changes. The new fact rides the existing network-gated lane and result contract.

## Phased delivery (maps to user stories)

- **Slice A (US1, P1) — the guard fact**: add `testProbe` + `namedScaffoldRequest` to
  `AcceptanceSupport`; add the gated fact that scaffolds `Roquelike-DungeonCrawler`, builds,
  tests, and asserts green (or honest `skip-unavailable`). Ships the #150 / 080-FR-011 guard.
- **Slice B (US2, P2) — gating + neutrality proof**: add the offline companion fact that
  proves the request forwards the descriptor-resolved name **key** with the hyphenated
  **value** and names no rendering token; extend the existing acceptance no-Governance /
  no-provider-identity scan to cover the new fact's request builder. Confirm the workflow
  `--filter` selects the fact with no YAML change.

Slice A is the deliverable; Slice B fences the architecture invariants A rides on. Both live
in the acceptance test project and are independently reviewable.

## Migration / compatibility notes

- **No persisted-schema bump**: `composition-acceptance-result` v1, its golden, and the
  determinism matrix are untouched — the new gated fact resolves the same `Verdict` type and
  writes the same (sensed, uncompared) result document shape.
- **No new CI job / no YAML change expected**: the new fact is selected by the existing
  `--filter "kind=composition-acceptance"`; verify this in Slice B rather than adding a job.
- **Offline inner loop unchanged**: the gated fact is statically skipped at discovery when the
  registry env is unset; only the always-on offline companion adds (negligible) inner-loop time.
- **Provider-neutrality preserved**: the name-param key is resolved from the registry
  descriptor at run time; generic SDD gains no `productName`/rendering literal. The existing
  no-Governance-reference guard is extended, not weakened.
- **Cross-repo**: closes FS.GG.SDD#150 (epic #148). No registry/contract change — the enabling
  `scaffold-provider 1.1.0` + `scaffold-provider-identifier` coherence flip already landed via
  Rendering#142. Board card flips to Done once the smoke goes green on its next run.

## Risks

- **The gated smoke only runs on the scheduled/dispatched lane**, so a regression is caught at
  next acceptance run, not on every PR. Mitigation: accepted — matches the existing
  network-gated posture (offline PRs can't reach the real provider). The `--filter` selection
  and honest gating are proven by the always-on offline companion.
- **`dotnet test` in the produced product may be slow or empty.** Mitigation: bound it with
  the existing 300 s `runToCompletion` cap; an empty-but-green test run (exit 0) satisfies
  FR-002 by design (documented in spec Edge Cases + quickstart).
- **Descriptor `nameParameter` absent** (a provider that declares none): `resolveNameParameter`
  falls back to the contract default `"name"`. Mitigation: acceptable — the reference provider
  declares `productName`; the fallback still forwards a valid key. Documented in research.md.
- **First-run flakiness from network/provider availability.** Mitigation: the honest
  `skip-unavailable` branch keeps a transient outage from failing SDD; the fact only *fails* on
  a real compile/test failure of an available provider.
