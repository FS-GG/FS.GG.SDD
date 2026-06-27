# Implementation Plan: Scaffold Composition Acceptance (real rendering provider)

**Branch**: `034-scaffold-composition-acceptance` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/034-scaffold-composition-acceptance/spec.md`

## Summary

The `fsgg-sdd scaffold` command, its provider contract (`scaffold-provider` v1), its
provenance record (`scaffold-provenance` v1), its outcome/exit vocabulary, and its
post-instantiation steps (git init + `.sh` chmod) **already exist and are proven** — but
only against the neutral in-repo `dotnet new` fixture provider, which deliberately knows
nothing about rendering. Nothing yet proves the **real** `--provider rendering --param
lifecycle=sdd` composition is coherent end to end. This feature closes that gap: it adds a
**verification-only**, opt-in, network-gated **composition acceptance** that drives the real
published rendering provider once and emits a single deterministic PASS/FAIL/SKIP verdict.

This feature adds **no new contract surface** (spec Assumptions; constitution §Change
Classification). It consumes the existing `scaffold-provider` v1, `scaffold-provenance` v1,
and the established outcome/exit vocabulary unchanged. The only production-adjacent artifact
is the acceptance's own per-run **result document** (a new test-output JSON, not a lifecycle
artifact and not a release-catalog entry — the same exemption class as the `validate`
harness report, `docs/release/schema-reference.md`).

**Planning decision (spec Assumptions, resolved here and in [research.md](./research.md) D1):**
the acceptance lives as a **separate, gated acceptance** — a new opt-in xUnit acceptance
project — **not** as a new dimension inside `fsgg-sdd validate`. `validate` is contractually
offline and **byte-identical deterministic** (it even has a determinism matrix); a
network-bound, real-template dimension would collide with that determinism guarantee and pull
network into the validate contract. A separate gated acceptance keeps `validate` and the inner
loop untouched while satisfying "opt-in, network-gated, scheduled" (FR-010).

How each requirement falls out of the **existing** architecture (the acceptance *asserts*,
it does not *add* behavior):

1. **Real composition is driven through the existing command surface (FR-001/FR-002).** The
   acceptance runs the in-process `init`→…→`Scaffold` MVU loop (the same `runRequest`/
   `interpretAll` path the fixture tests use, which already executes real `dotnet new install`)
   with `Provider = Some "rendering"`, `Parameters = ["lifecycle","sdd"]`, against an
   **author-supplied registry** copied into `<productRoot>/.fsgg/providers.yml` from the path
   in the `FSGG_SDD_ACCEPTANCE_REGISTRY` env var. The registry — the only channel carrying the
   real template identity — is never committed to this repo (FR-009).
2. **Skeleton + constitution + provenance partition are already produced (FR-002/FR-005).**
   Scaffold reuses `initEffects` (so `.fsgg/constitution.md` and the skeleton appear), and
   computes `generatedProduct = after − before − skeletonFiles − provenance`, so provider
   paths are `generatedProduct` and skeleton paths never are. The acceptance reads
   `.fsgg/scaffold-provenance.json` and asserts the partition.
3. **Refresh exclusion is already implemented (FR-006).** `refresh` regenerates only its fixed
   `readiness/<id>/*` + agent-guidance targets and never touches `.fsgg/` root files or
   provenance-recorded `generatedProduct` paths. The acceptance runs `refresh` on the product
   and asserts the app code is byte-unchanged.
4. **Completeness + outcome/exit mapping are already enforced (FR-007/FR-008).** The acceptance
   reads the `--json` report `outcome` (one of exactly four: `providerSucceeded`,
   `providerSucceededEmpty`, `providerNotRun`, `providerFailed`) **and** its diagnostic code, and
   maps verdicts onto the existing vocabulary: `providerSucceeded` + all facts → PASS;
   `providerFailed` + `scaffold.providerUnavailable` → SKIP; `providerFailed` +
   `scaffold.providerWroteSddTree`/`scaffold.providerFailed` → FAIL (defect); `providerNotRun`
   (user-input/config) → FAIL; `providerSucceededEmpty` → FAIL (incomplete). The
   `(outcome, diagnostic)` pair is essential: unavailable and defect both surface as
   `providerFailed`, so keying on the outcome alone would collapse SKIP into FAIL and break
   SC-004. An incomplete scaffold is never read as complete.
5. **build/run is the one genuinely new check (FR-003).** Present files are not enough: the
   acceptance shells `dotnet build` and a bounded `dotnet run` (or `--help`/headless run) over
   the produced app at the `RunProcess` edge style, surfacing build/run failure distinctly from
   "files were produced."

The only real authoring work is therefore: **(a)** the acceptance harness that orchestrates
run → assert → emit, **(b)** the deterministic result-document contract (verdict + asserted
facts, sensed metadata normalized — [contracts/composition-acceptance-result.md](./contracts/composition-acceptance-result.md)),
**(c)** the env-gated SKIP wiring that keeps the offline inner loop green (FR-010), and **(d)**
a scheduled CI workflow that runs it with the registry set.

**Change tier**: **Tier 1 (contracted change)** — it introduces a new, schema-versioned
**result document** (a verification output contract) and a new opt-in test surface, even though
it changes **no** lifecycle artifact, **no** provider/provenance schema, and **no** command
behavior. Per constitution §Change Classification, a new tool-facing structured contract is
Tier 1. No `.fsi`/`PublicSurface.baseline` change (the acceptance lives in a test project and
adds no public library surface); the moved contract is the new result schema, baselined by its
own golden/shape test.

Decisions locked in [research.md](./research.md):

1. **Separate gated acceptance, not a `validate` dimension** (Summary above).
2. **Env-gated dynamic SKIP via `Assert.Skip`/`Assert.SkipWhen`** (xUnit 2.9.3, already the
   pinned version — `Directory.Packages.props`). Registry-env unset ⇒ the acceptance *skips*
   (not passes-as-noop, not fails), so the offline inner loop is green **and** honest.
3. **New isolated test project `tests/FS.GG.SDD.Acceptance.Tests`**, trait-tagged
   `[<Trait("kind","composition-acceptance")>]`. It is in the solution so it builds offline, but
   every fact self-skips without the registry; the scheduled workflow targets the trait with the
   env set. This keeps the existing three offline test projects untouched and network-free.
4. **The real provider identity is reached only through `FSGG_SDD_ACCEPTANCE_REGISTRY`.** No
   rendering package id / template id / path / docs URL enters SDD source, contracts, reports,
   or the acceptance code. The existing `ScaffoldGuardTests` deny-list (`fs-gg-ui`,
   `FS.GG.Rendering`) is **extended to scan the new acceptance project**, proving FR-009/SC-003.
   The generic owner value/provider name `"rendering"` (in `ArtifactRef.ArtifactOwner` /
   `ownerFromValue`) is *not* an identifier and stays allowed.
5. **The result document is not a release-catalog artifact.** Like the `validate`
   `validation-report`, it is sensed harness output, not a produced lifecycle artifact; it is
   added to the `docs/release/schema-reference.md` declared-exception list, leaving
   `release-readiness.json` and its golden baseline unchanged.

## Technical Context

**Language/Version**: F# on .NET, target `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: xUnit `2.9.3` + `Microsoft.NET.Test.Sdk` `17.14.1` (already pinned,
`Directory.Packages.props`). The acceptance references `FS.GG.SDD.Commands` (to drive the MVU
loop via `init`/`interpretAll`/`update`, exactly as `FS.GG.SDD.Commands.Tests` does) and
`FS.GG.SDD.Artifacts` (to parse `scaffold-provenance.json` via `ScaffoldProvenance.tryParse`).
The build/run check shells `dotnet build`/`dotnet run` via `System.Diagnostics.Process` at the
test edge. No new production package; no new effect case; no new external tool in generic SDD.

**Storage**: Filesystem + process only. The acceptance creates a temp empty product dir, copies
the author-supplied registry into `<root>/.fsgg/providers.yml`, and writes one result document
(default `<root>/composition-acceptance.json`, also echoable to a path via env/arg). It reads
the produced `.fsgg/scaffold-provenance.json`. No lifecycle artifact, schema, or release-catalog
change.

**Testing**: A new opt-in xUnit project `tests/FS.GG.SDD.Acceptance.Tests` (real filesystem +
real `dotnet new`/`dotnet build`/`dotnet run`, no mocks — constitution VI). Default
`dotnet test FS.GG.SDD.sln` with `FSGG_SDD_ACCEPTANCE_REGISTRY` **unset** ⇒ every acceptance
fact SKIPs and the existing offline suites are unchanged and pass with no network. The scheduled
workflow runs `dotnet test --filter kind=composition-acceptance` with the env set.

**Target Platform**: Linux/cross-platform CLI. The acceptance shells `git`, `dotnet new`,
`dotnet build`, `dotnet run`; it tolerates a missing `git` (skip-non-fatal, already scaffold
behavior) and a missing/unavailable feed (→ SKIP).

**Performance Goals**: N/A for the inner loop (acceptance SKIPs offline). A scheduled run is
bounded by `dotnet new install` + `dotnet build` + a short `dotnet run`; the run check uses a
timeout so a hung app fails rather than hangs.

**Project Type**: Single solution, CLI + libraries (`src/FS.GG.SDD.*`) plus test projects under
`tests/`. The reference rendering provider (a runnable UI app) lives in **FS.GG.Rendering** and
is **not** a dependency here — it is reached only at runtime through the author-supplied
registry, so generic SDD gains no rendering knowledge (constitution Engineering Constraints).

**Constraints**:
- **Offline inner loop stays green & network-free (FR-010/SC-003)**: env-unset SKIP + isolated
  trait-tagged project; no acceptance code path executes without the registry env var.
- **No rendering identifiers in generic SDD (FR-009/SC-003)**: enforced by extending the
  existing `ScaffoldGuardTests` deny-list scan to the acceptance project; the real identity
  lives only in the external registry.
- **Determinism modulo sensed metadata (FR-011/SC-005)**: two runs with the same inputs and an
  available provider produce byte-identical result documents except the explicitly **sensed**
  block (resolved provider/template version, availability, host/timestamp) — normalized exactly
  like the `validate` report's sensed metadata (INV-5 pattern in `ValidationContracts`).
- **No false PASS / no false complete (FR-007/SC-002/SC-004)**: PASS requires *all* asserted
  facts true; a `providerFailed` outcome carrying the `scaffold.providerUnavailable` diagnostic is
  SKIP, never PASS or a FAIL of SDD; an incomplete scaffold is never recorded complete.
- **No Governance runtime / verdict (FR-012)**: the acceptance neither references Governance nor
  computes a gate verdict; effective-evidence freshness stays a downstream concern.
- **`--rich` unaffected**: this feature touches no projection; the scaffold/refresh reports the
  acceptance consumes are read as `--json`. Rich stays excluded from deterministic contracts.
- `WarningsAsErrors` ratchet stays at 0; no `#nowarn` introduced.

**Scale/Scope**: +1 test project (`tests/FS.GG.SDD.Acceptance.Tests`, added to the sln); the
acceptance harness module + result-document writer/contract; +1 result schema
(`composition-acceptance-result` v1) documented in `contracts/` and listed as a
`schema-reference.md` exception; +1 `.github/workflows/composition-acceptance.yml` (schedule +
`workflow_dispatch`); the `ScaffoldGuardTests` deny-list scope extended by one directory. **0**
new `CommandEffect` cases, **0** `.fsi` edits, **0** lifecycle-artifact/provider/provenance
schema changes, **0** `release-readiness.json` change, **0** `validate` matrix change. Agent
surfaces (CLAUDE.md/AGENTS.md + 2× SKILL.md) gain a one-line "real-provider composition
acceptance is opt-in and network-gated" note.

### Grounded inventory (current tree, verified 2026-06-27 @ `58b4b0c`)

| Concern | Anchor | Disposition (this feature) |
|---|---|---|
| Scaffold MVU + RunProcess edge | `CommandWorkflow/HandlersScaffold.fs` (`resolveScaffold`/`scaffoldInvocationEffects`/`finalizeScaffold`/post-instantiation) | **reuse unchanged** — driven by the acceptance via the public command surface |
| In-process driver | `tests/FS.GG.SDD.Commands.Tests/TestSupport.fs` (`request`/`runRequest`/`interpretAll`) | **mirror** in the acceptance project (own copy: `request` with `Provider=Some "rendering"`, `Parameters=["lifecycle","sdd"]`) |
| Provider registry parse | `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` (`parseProviderRegistry`/`ProviderDescriptor`) | **reuse unchanged** — registry copied from `FSGG_SDD_ACCEPTANCE_REGISTRY` into the product `.fsgg/` |
| Provenance schema + parse | `src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs` (`ScaffoldProvenanceRecord`/`tryParse`/`ArtifactOwner`) | **reuse unchanged** — read & assert partition (`GeneratedProduct` vs skeleton) |
| Outcome/exit vocabulary | `HandlersScaffold.fs` (`ScaffoldOutcome`/`scaffoldOutcomeValue`) + diagnostics | **reuse unchanged** — map `--json` `outcome` → PASS/FAIL/SKIP (FR-008) |
| Refresh exclusion | `CommandWorkflow/HandlersRefresh.fs` (canonical views; `.fsgg/` untouched) | **reuse unchanged** — run refresh, assert app code byte-unchanged (FR-006) |
| Sensed-metadata normalization | `src/FS.GG.SDD.Validation/ValidationContracts.fs` (INV-5 null-normalized sensed block) | **mirror the pattern** in the result document (FR-011/SC-005) |
| Release exception list | `docs/release/schema-reference.md` (validate-report exception) | **extend** — add the composition-acceptance result as a second declared exception |
| Leak-scan guard | `tests/FS.GG.SDD.Commands.Tests/ScaffoldGuardTests.fs` (`forbiddenTokens`/`scanFiles`) | **extend scope** — include `tests/FS.GG.SDD.Acceptance.Tests/**` in the deny-list scan (FR-009) |
| Solution | `FS.GG.SDD.sln` | **add** the acceptance test project |
| CI | *(none today — `.github/workflows/` is empty)* | **add** `composition-acceptance.yml` (schedule + manual; sets the registry env) |
| Agent surfaces | `CLAUDE.md`, `AGENTS.md`, `.claude/skills/fs-gg-sdd-project/SKILL.md`, `.codex/skills/fs-gg-sdd-project/SKILL.md` | **one aligned line** each |

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Spec → FSI → FSI-exercise → Tests → Impl | ✅ | The feature *is* tests plus one new result-document contract; no new public library signature, so there is no `.fsi` to sketch. The verification is exercised through the public command surface (the `init`/`scaffold`/`refresh` MVU loop) and authored to fail first (no acceptance asserting the real composition exists today). |
| II. Structured artifacts are the contract | ✅ | The acceptance's machine contract is the schema-versioned **result document** (verdict + asserted facts + normalized sensed block), fixed in `contracts/`. Prose↔structured conflict: none — the result JSON is the single authoritative record; the human-readable run summary is a projection of it. The lifecycle's own contracts (`scaffold-provenance` v1, etc.) are byte-unchanged. |
| III. Visibility lives in `.fsi` | ✅ | No public module surface changes; `.fsi` and `PublicSurface.baseline` snapshots are unchanged. The new contract is the result schema, baselined by its own shape/golden test, not a signature. |
| IV. Idiomatic simplicity | ✅ | Plain F#: a small harness that drives the existing MVU loop, reads JSON via existing parsers, shells `dotnet` via `Process`, and writes one JSON document. No new abstraction, framework, reflection, or provider knowledge. |
| V. Elmish/MVU boundary | ✅ | The acceptance adds **no** I/O to any `update`. It composes the existing pure planner + edge interpreter and performs its own build/run/refresh probes at the **test edge** (the harness boundary), keeping the lifecycle MVU pure. |
| VI. Test evidence | ✅ | Real filesystem + real `dotnet new`/`build`/`run`/`git` over the **real** published provider — the strongest possible evidence, the opposite of mocks. Synthetic-free; when the provider is unavailable it SKIPs (disclosed verdict), never fabricates a pass. |
| VII. One contract for agents + humans | ✅ | One opt-in acceptance over the same lifecycle artifacts for CLI users, CI, Claude, and Codex; the result document is the shared record. CLAUDE/AGENTS/2× SKILL gain one aligned line. The acceptance is verification, not a second source of truth. |
| VIII. Observability & safe failure | ✅ | The verdict is an actionable diagnostic: PASS / FAIL (with the failing fact + surfaced scaffold/build/run diagnostic) / SKIP-unavailable are distinct. It distinguishes malformed input/config error from provider defect from SDD defect (FR-008, Edge Cases), and never reports an incomplete scaffold complete (FR-007). |

**Change tier**: **Tier 1 (contracted change)** — a new schema-versioned result contract and a
new opt-in tool-facing surface (constitution §Change Classification). No lifecycle/provider/
provenance schema moves; the new contract is justified by the epic deliverable (confirm the real
composition path). **Complexity Tracking is empty** — there is no constitution violation; reusing
the existing scaffold/provenance/refresh machinery and gating via env-skip is strictly simpler
than any alternative (and simpler than contaminating `validate`).

**Lifecycle-feature plan checklist** (constitution §Development Workflow):
- *Authored artifacts*: none new in the lifecycle; the acceptance is verification. The author-
  supplied registry is **external** (out-of-repo), reached only via `FSGG_SDD_ACCEPTANCE_REGISTRY`.
- *Structured machine contracts*: **one new** — the `composition-acceptance-result` v1 document
  (a verification output, declared exception in `schema-reference.md`, not a lifecycle artifact).
  `scaffold-provider` v1, `scaffold-provenance` v1, and the outcome/exit vocabulary are unchanged.
- *Generated views*: none; the result document is harness output, never refreshed by `refresh`.
- *Schema version & migration*: result schema is `v1` (new); no existing schema touched → no
  migration. `release-readiness.json` unchanged.
- *Agent behavior (Claude & Codex)*: one aligned one-line note added to both surfaces + both SKILLs.
- *Optional Governance integration*: none — no Governance runtime, no verdict (FR-012).
- *Tests/fixtures for stale/conflicting artifacts*: provider-unavailable SKIP, provider-defect
  FAIL, build/run FAIL, git-absent skip-non-fatal, registry-missing config error, determinism
  across two runs, and the FR-009 leak scan are all exercised (Edge Cases / Success Criteria).

## Project Structure

### Documentation (this feature)

```text
specs/034-scaffold-composition-acceptance/
├── plan.md              # This file
├── research.md          # Phase 0 — D1 separate-acceptance vs validate-dimension; D2 env-gated
│                        #   Assert.Skip; D3 isolated trait-tagged project; D4 registry-only
│                        #   identity + guard-scan extension; D5 result not in release catalog;
│                        #   D6 build/run probe shape & timeout; D7 outcome→verdict mapping;
│                        #   D8 determinism / sensed-metadata normalization
├── data-model.md        # Phase 1 — Composition acceptance run; result document fields; verdict
│                        #   DU; asserted-fact set; sensed block; outcome→verdict mapping table
├── quickstart.md        # Phase 1 — run the acceptance (offline SKIP path + registry-set PASS
│                        #   path); how to point FSGG_SDD_ACCEPTANCE_REGISTRY; expected verdicts
├── contracts/           # Phase 1 — verification contracts (no new lifecycle interface)
│   ├── acceptance-protocol.md            # inputs, gating, run sequence, outcome→verdict mapping, exits
│   └── composition-acceptance-result.md  # the result-document schema v1 (verdict, facts, sensed)
└── tasks.md             # Phase 2 — /speckit-tasks (NOT created here)
```

### Source Code (repository root)

```text
tests/FS.GG.SDD.Acceptance.Tests/                 # NEW opt-in, network-gated project (in sln)
├── FS.GG.SDD.Acceptance.Tests.fsproj             #   refs FS.GG.SDD.Commands + FS.GG.SDD.Artifacts
├── AcceptanceSupport.fs                           #   env gating (FSGG_SDD_ACCEPTANCE_REGISTRY),
│                                                  #     temp empty dir, registry copy, dotnet shell
├── CompositionResult.fs                           #   result-document record + deterministic
│                                                  #     serializer (sensed block normalized)
└── CompositionAcceptanceTests.fs                  #   [<Trait("kind","composition-acceptance")>]
                                                   #     US1 PASS (skeleton+constitution+build/run+
                                                   #     git+chmod+complete), US2 provenance+refresh,
                                                   #     US3 unavailable→SKIP + config-error FAIL

# Reused UNCHANGED (proof the rest is free):
#   CommandWorkflow/HandlersScaffold.fs     scaffold MVU + RunProcess edge + post-instantiation
#   CommandWorkflow/HandlersRefresh.fs      refresh exclusion of .fsgg/ + generatedProduct paths
#   Artifacts/ScaffoldProvenance.fs         ScaffoldProvenanceRecord + tryParse (partition read)
#   Artifacts/LifecycleArtifacts/Config.fs  provider registry parse
#   Validation/ValidationContracts.fs       sensed-metadata normalization pattern (mirrored)

tests/FS.GG.SDD.Commands.Tests/
└── ScaffoldGuardTests.fs                          # EXTEND: deny-list scan now includes the
                                                   #   tests/FS.GG.SDD.Acceptance.Tests/** tree (FR-009)

docs/release/
└── schema-reference.md                            # EXTEND: add composition-acceptance result as a
                                                   #   second declared release-catalog exception

.github/workflows/
└── composition-acceptance.yml                     # NEW: schedule + workflow_dispatch; sets
                                                   #   FSGG_SDD_ACCEPTANCE_REGISTRY; --filter kind=...

FS.GG.SDD.sln                                       # EXTEND: add the acceptance test project

# Agent surfaces (one aligned line each):
#   CLAUDE.md, AGENTS.md, .claude/skills/fs-gg-sdd-project/SKILL.md, .codex/skills/fs-gg-sdd-project/SKILL.md
```

**Structure Decision**: Single-solution F# layout retained. Unlike feature 033 (a one-line
skeleton emission with everything derived), this feature adds a **new isolated test project** and
**one new result contract** but **zero** lifecycle/provider/provenance/`validate` change. The work
concentrates in the acceptance harness, the deterministic result document, the env-gated SKIP that
protects the offline inner loop, the extended leak-scan, and the scheduled workflow — all
verification scaffolding around the *already-built* real composition path.

## Complexity Tracking

No Constitution Check violations. Reusing the existing scaffold/provenance/refresh machinery,
driving it through the public command surface, gating with an env-skip, and emitting one
deterministic result document is strictly simpler than the rejected alternative of extending the
`validate` harness with a network dimension (which would breach validate's determinism and
no-rendering-knowledge guarantees). This section is intentionally empty.
