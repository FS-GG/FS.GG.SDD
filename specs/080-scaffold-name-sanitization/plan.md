# Implementation Plan: Guarantee a freshly scaffolded product compiles (name → valid F# identifier)

**Branch**: `080-scaffold-name-sanitization` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/080-scaffold-name-sanitization/spec.md`

## Summary

Scaffold forwards the product name **verbatim** to the provider's `dotnet new`, so a
name that is a legal product name but an illegal F# identifier (`Roquelike-DungeonCrawler`)
lands in `module`/`namespace`/`let` positions and the workspace fails to compile. This plan
adds a **generic, language-level** name→valid-F#-identifier derivation to `fsgg-sdd scaffold`
and forwards the derived value **alongside** the raw name under a **provider-declared**
identifier parameter, so identifier contexts get the safe form while string-literal / path /
`.fsproj` / `.slnx` contexts keep the raw name. It activates the currently-dead
`ProviderDescriptor.NameParameter` field (the derivation source) and adds an additive
`IdentifierParameter` contract field (the derivation sink). Both values flow through the
existing `effectiveParameters` map, so provenance/report stay **schema v1**. A new
`scaffold.nameUnrepresentable` diagnostic guards the edge where a name reduces to no valid
identifier. A network-gated CI smoke scaffolds a hyphenated name against the real provider
and asserts `dotnet build` + `dotnet test` are green. Adoption by the reference provider
(FS.GG.Rendering) is a coordinated, additive, versioned cross-repo contract change.

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution Engineering Constraints).

**Primary Dependencies**: `FS.GG.Contracts` (`Fsgg.Provider` descriptor contract — the org-shared
contract package); `FS.GG.SDD.Commands` (scaffold MVU: `HandlersScaffold`); `FS.GG.SDD.Artifacts`
(provider-registry parse `Config.fs`, `Diagnostics`, `ScaffoldProvenance`); `FS.GG.SDD.Cli`
(`Program.fs` arg parsing); Spectre.Console (rich report projection, presentation only).

**Storage**: `.fsgg/providers.yml` (provider registry, read); `.fsgg/scaffold-provenance.json`
(schema v1, written — unchanged schema, additive parameter rows only).

**Testing**: xUnit. Offline unit/golden suites (`tests/FS.GG.SDD.Artifacts.Tests`,
`tests/FS.GG.SDD.Commands.Tests`, `tests/FS.GG.Contracts.Tests`) run in the deterministic gate;
the real-provider build/test smoke runs in the **network-gated** acceptance lane
(`tests/FS.GG.SDD.Acceptance.Tests`, `kind=composition-acceptance`, `FSGG_SDD_ACCEPTANCE_REGISTRY`).

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`).

**Project Type**: CLI + libraries (single solution `FS.GG.SDD.sln`).

**Performance Goals**: N/A — derivation is a one-shot pure string transform per scaffold.

**Constraints**: Derivation is generic and **provider-agnostic** — no provider package id,
template id, path, or docs URL in generic SDD (constitution; `030` FR-002). Deterministic
(same name → same identifier on every run/platform). Offline inner loop stays green and fast
(the real-provider smoke self-skips when the acceptance registry is unset).

**Project Type / Change Tier**: **Tier 1 (contracted change)** — additive provider-descriptor
contract field, a new diagnostic id, and a cross-repo integration change. Requires spec, plan,
tasks, `.fsi` + surface baselines, tests, docs, and cross-repo coordination.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | How this plan satisfies it |
|---|---|---|
| I. Spec → FSI → tests → impl | PASS | `.fsi` first for the new pure `FsharpIdentifier` module and the `ProviderDescriptor.IdentifierParameter` field; semantic tests before `.fs`. |
| II. Structured artifacts are the contract | PASS | The provider descriptor (typed) is authoritative; `providers.yml` (Markdown-adjacent YAML) is the authoring surface. Provenance JSON records the forwarded values. |
| III. Visibility in `.fsi` + baselines | PASS | New module ships `.fs`+`.fsi`; `Provider.fsi` gains the additive field; public-surface baselines updated for `FS.GG.Contracts` + any touched SDD module. |
| IV. Idiomatic simplicity | PASS | A pure `string -> Result<identifier, reason>` transform; records/DU; no custom operators, reflection, SRTP, or type providers. |
| V. Elmish/MVU boundary | PASS | Derivation is a **pure** step inside the existing scaffold `update` (`resolveScaffold`); it requests **no new effect** (no I/O). The verbatim forward already happens at the `RunProcess` edge. |
| VI. Test evidence mandatory | PASS | Golden derivation tests (fail-before/pass-after), scaffold param-forwarding tests over real registry fixtures, provenance/report snapshot updates, and the real-provider build/test acceptance smoke. |
| VII. Agent + human share one contract | PASS | Claude + Codex scaffold guidance updated equivalently where the descriptor/param semantics they describe change; no second source of truth introduced. |
| VIII. Observability & safe failure | PASS | `scaffold.nameUnrepresentable` distinguishes malformed **user input** (exit 1) from a tool/provider defect; no incomplete scaffold reported as complete. |

**Engineering-constraint checks**: `net10.0` ✓; `FS.GG.SDD.*` namespace ✓ (the contract field
lives in the already-approved org-shared `FS.GG.Contracts`/`Fsgg` package, additive & value-agnostic);
`fsgg-sdd` CLI family ✓; no FS.GG.Rendering package id/template/path/docs URL in generic SDD ✓
(derivation is language-level; the identifier-param **key** is provider-declared).

**Result**: No violations. Complexity Tracking empty.

## Project Structure

### Documentation (this feature)

```text
specs/080-scaffold-name-sanitization/
├── plan.md              # This file
├── research.md          # Phase 0 — derivation policy, contract shape, precedence, CI/coordination
├── data-model.md        # Phase 1 — entities + validation rules
├── quickstart.md        # Phase 1 — runnable validation scenarios
├── contracts/           # Phase 1 — descriptor-field addition + derivation-module contract
│   ├── provider-descriptor-identifier-parameter.md
│   └── fsharp-identifier-derivation.md
├── checklists/
│   └── requirements.md  # spec quality checklist (done)
└── tasks.md             # Phase 2 (/speckit-tasks) — not created here
```

### Source Code (repository root)

```text
src/
├── FS.GG.Contracts/
│   ├── Provider.fs / Provider.fsi        # + IdentifierParameter: string option (additive)
├── FS.GG.SDD.Artifacts/
│   ├── FsharpIdentifier.fs / .fsi        # NEW pure derivation module (generic, language-level)
│   ├── LifecycleArtifacts/Config.fs      # parse `identifierParameter:` (additive, optional)
│   └── Diagnostics.fs / .fsi             # + scaffold.nameUnrepresentable
└── FS.GG.SDD.Commands/
    └── CommandWorkflow/HandlersScaffold.fs  # derive + inject the identifier param; block on unrepresentable

tests/
├── FS.GG.SDD.Artifacts.Tests/
│   ├── FsharpIdentifierTests.fs          # golden derivation cases (fail-before/pass-after)
│   └── ProviderRegistryParseTests.fs     # parse identifierParameter
├── FS.GG.Contracts.Tests/
│   └── ProviderDescriptorTests.fs        # field default/roundtrip
├── FS.GG.SDD.Commands.Tests/
│   └── ScaffoldCommandTests.fs           # both params forwarded; raw preserved; unrepresentable blocks
└── FS.GG.SDD.Acceptance.Tests/
    └── CompositionAcceptanceTests.fs     # hyphenated-name build+test smoke (network-gated)

tests/fixtures/scaffold-provider/registries/
└── *.providers.yml                       # + a fixture declaring nameParameter + identifierParameter
```

**Structure Decision**: Single-solution CLI + libraries. The **contract field** lives in the
org-shared `FS.GG.Contracts` (`Fsgg.Provider`) so the reference provider re-types onto the same
descriptor. The **derivation** is a self-contained pure module in `FS.GG.SDD.Artifacts` (generic,
independently testable, not part of the shared contract surface). Wiring is a pure step in the
existing scaffold MVU handler — no new command, no new effect, no new provenance schema.

## Complexity Tracking

> No constitution violations. No entries.
