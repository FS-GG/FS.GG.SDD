# Implementation Plan: FS.GG.Contracts Package — Shared Schema, Provider & Registry Contracts

**Branch**: `036-fsgg-contracts-package` | **Date**: 2026-06-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/036-fsgg-contracts-package/spec.md`

## Summary

Create a new, versioned, **BCL-only** F# library — `FS.GG.Contracts` (SemVer `1.0.0`) —
that becomes the single typed source of truth for every `.fsgg` schema (each paired
with a named version constant), the extended template-provider descriptor (existing
identity fields plus optional `Build`/`Test`/`Run`/`Verify` commands and a canonical
name parameter defaulting to `name`), and the cross-repo dependency-registry types
with a pure validator. The work is **create-only and purely additive**: SDD does not
reference the package yet, no existing artifact changes, and every existing SDD test
output stays byte-identical (the re-type is the separate item FS-GG/FS.GG.SDD#9). The
package owns typed models and pure validators only — raw YAML/JSON (de)serialization
stays at each consumer's edge, so the library carries no third-party dependency. It is
packed to a local folder feed and the cross-repo dependency registry is updated to
register the new surface (contract-change, ADR-0001).

## Technical Context

**Language/Version**: F# on .NET, `net10.0`, `LangVersion=preview` (inherited from
`Directory.Build.props`).

**Primary Dependencies**: `FSharp.Core` only (the F# runtime/standard library). No
third-party packages — explicitly **no** `YamlDotNet`, `System.Text.Json`,
`Spectre.Console`, Governance runtime, or rendering library.

**Storage**: N/A — the package is pure types + pure functions; it performs no I/O and
reads/writes no files. Consumers feed it already-parsed models.

**Testing**: xunit (2.9.3), centrally versioned, in a new `tests/FS.GG.Contracts.Tests`
project. Plus the unchanged existing SDD suite as the additive-guarantee regression net.

**Target Platform**: Cross-platform .NET library (Linux/macOS/Windows); consumed by
SDD, Governance, Templates, and Rendering repos.

**Project Type**: Single F# class library (leaf/lowest layer), packable to NuGet,
distributed via a local folder feed until the org feed lands (H4).

**Performance Goals**: N/A — synchronous, in-memory contract types and a linear-pass
validator; no hot path.

**Constraints**: BCL-only (zero third-party in the dependency closure beyond
`FSharp.Core`); additive-only (zero behavioural or byte change to existing SDD); no
embedded provider-/rendering-/Governance-specific identity (FR-014); `.fsi` for every
public module; version constants must equal today's emitted values (FR-005).

**Scale/Scope**: 10 named `.fsgg` schemas (records + version constants), one extended
provider descriptor, one registry model + validator, one contract-version self-report.
Three module areas: `Schemas`, `Provider`, `Registry`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Change tier**: Tier 1 (contracted change) — new package, schema contract, and
cross-repo integration surface. Requires spec, plan, tasks, `.fsi`, tests, docs, and
the registry/migration registration.

| # | Principle | Status | How this plan satisfies it |
|---|-----------|--------|----------------------------|
| I | Spec → FSI → Semantic Tests → Implementation | PASS | Public surface authored as `.fsi` first (see `contracts/`), exercised/tested through that surface before `.fs` hardens. |
| II | Structured Artifacts Are the Machine Contract | PASS | The package *is* the typed machine contract; version constants are the single authoritative fact per schema. |
| III | Visibility Lives in `.fsi` | PASS | Every public module (`Schemas`, `Provider`, `Registry`) ships a paired `.fsi`; a surface baseline is added for the package. |
| IV | Idiomatic Simplicity | PASS | Records + DUs + module `let` constants + one pure validator function. No custom operators, SRTP, reflection, or CEs. |
| V | Elmish/MVU Boundary | PASS (N/A) | No state or I/O. Constitution exempts "simple pure parsers, data models, and validators" — this package is exactly that. Documented, no MVU ceremony. |
| VI | Test Evidence Is Mandatory | PASS | Tests assert version-constant equality with today's emitted values, descriptor defaults, validator coherent/incoherent/incomplete cases, BCL-only closure, and local-feed pack/consume. The full existing SDD suite is the additive-guarantee gate. |
| VII | Agent And Human Workflows Share One Contract | PASS (N/A) | No agent surface added; the cross-repo registry registration (FR-013) keeps the one-source-of-truth invariant. |
| VIII | Observability And Safe Failure | PASS | Validator diagnostics name the offending entry and the violated rule (missing field vs. coherence violation); empty-but-declared provider commands are surfaced as malformed, not silently defaulted. |

**Engineering-constraint deviations** (see Complexity Tracking):

- The org-shared package id `FS.GG.Contracts` (F# namespace `Fsgg`) vs. the default
  `FS.GG.SDD.*`. This is **sanctioned** by constitution v1.1.0, whose Engineering
  Constraints now carve out org-shared, SDD-owned contract packages; this plan
  supplies the required justification below.
- "No FS.GG.Rendering knowledge in generic SDD" is *upheld* (FR-014 forbids any
  provider-/rendering-/Governance-specific identity); noted only to confirm compliance.

No unjustified gate violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/036-fsgg-contracts-package/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── schemas.fsi      # Sketched public surface: Schemas module
│   ├── provider.fsi     # Sketched public surface: Provider module
│   └── registry.fsi     # Sketched public surface: Registry module
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created here)
```

### Source Code (repository root)

```text
src/
└── FS.GG.Contracts/                 # NEW — leaf library, no SDD project references
    ├── FS.GG.Contracts.fsproj       # net10.0, IsPackable=true, PackageId=FS.GG.Contracts,
    │                                #   PackageReference FSharp.Core only
    ├── ContractVersion.fsi/.fs      # Self-describing package contract version (FR-012)
    ├── Schemas.fsi/.fs              # Fsgg.Schemas: typed record + version constant per .fsgg schema (FR-004/005)
    ├── Provider.fsi/.fs            # Fsgg.Provider: extended provider descriptor + name default (FR-006/007)
    └── Registry.fsi/.fs            # Fsgg.Registry: dependency-registry types + pure validator (FR-008/009)

tests/
└── FS.GG.Contracts.Tests/           # NEW — xunit, IsPackable=false
    ├── FS.GG.Contracts.Tests.fsproj # ProjectReference FS.GG.Contracts
    ├── SchemaVersionConstantTests.fs   # version constants == today's emitted values (SC-001/002)
    ├── ProviderDescriptorTests.fs      # absent vs. declared commands; name default (SC-003)
    ├── RegistryValidatorTests.fs       # coherent / incoherent / incomplete (SC-007)
    ├── DependencyClosureTests.fs       # BCL-only closure assertion (SC-004)
    └── PublicSurface.baseline          # exported-surface golden (Principle III)

# Local feed + cross-repo registry (FR-011/013) — folder feed produced by `dotnet pack`;
# registry update filed against FS-GG/.github registry/dependencies.yml + docs/registry/compatibility.md.
```

**Structure Decision**: A single new leaf library `src/FS.GG.Contracts` plus a paired
`tests/FS.GG.Contracts.Tests`, added to `FS.GG.SDD.sln`. The library sits *at or below*
`FS.GG.SDD.Artifacts` in layering and has **no** project references (FR-001). It follows
every repo convention (central `Version`/package management, `.fsi`-before-`.fs` compile
ordering, no `<RootNamespace>`/`<AssemblyName>` override beyond `PackageId`) except the
two documented deviations below. No existing project, artifact, or test is modified.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| PackageId `FS.GG.Contracts` + F# namespace `Fsgg`, not `FS.GG.SDD.*` (constitution Engineering Constraints, now carved out in v1.1.0) | The package is the **org-shared** coherence backbone consumed by Governance, Templates, and Rendering — all four FS-GG repos re-type onto it. An `FS.GG.SDD.*` name would falsely scope a cross-repo contract to SDD and discourage non-SDD consumers from depending on it. SDD *owns* it (FR-001) but does not *namespace-scope* it. The two names are distinct and deliberate: **PackageId** = `FS.GG.Contracts` (NuGet identity), **F# namespace** = `Fsgg` (the item's mandated module breakdown `Fsgg.Schemas`/`Fsgg.Provider`/`Fsgg.Registry`). | `FS.GG.SDD.Contracts` rejected: it brands a four-repo shared contract as SDD-internal, contradicting the item's "one typed source of truth for every FS-GG repo" goal and the mandated `Fsgg.*` module breakdown. Constitution v1.1.0 sanctions this exception, so the deviation is named, justified, and no longer unsanctioned. |
| `FSharp.Core` present in the dependency closure despite "BCL-only" / "zero third-party" (SC-004) | `FSharp.Core` is the F# language runtime/standard library, shipped with the SDK and unavoidable for *any* F# assembly. It is the F# equivalent of the BCL, not an optional third-party choice. | Authoring in C# to achieve a literal zero-`FSharp.Core` closure rejected: it breaks Engineering-Constraint "F# on .NET is the default" and prevents idiomatic records/DUs. SC-004 is interpreted as "no third-party package beyond the F# runtime," verified by a dependency-closure test that allowlists only `FSharp.Core`. |
