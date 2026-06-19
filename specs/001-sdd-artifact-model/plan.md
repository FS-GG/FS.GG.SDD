# Implementation Plan: SDD Artifact Model

**Branch**: `001-sdd-artifact-model` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-sdd-artifact-model/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/001-sdd-artifact-model`.

## Summary

Define the first machine contract for FS.GG.SDD lifecycle artifacts before any
lifecycle commands or generators exist. The implementation will introduce a
packable F# artifact-model library with `.fsi` signatures first, semantic tests
through the public surface, strict structured artifact contracts, deterministic
JSON-ready work-model types, stable diagnostics, fixture coverage, and optional
Governance-compatible rule contracts that do not depend on the Governance gate
runtime.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: BCL, FSharp.Core, System.Text.Json; YamlDotNet only
for strict YAML schema parsing; xUnit test packages for automated tests

**Storage**: Filesystem artifacts only: `.fsgg/*.yml`, `work/<id>/*`, fixture
directories, and generated readiness JSON contracts

**Testing**: `dotnet test` with xUnit; FSI/prelude evidence for public surface
shape; golden JSON fixtures for deterministic output and diagnostics

**Target Platform**: Cross-platform .NET library and tests on Linux/macOS/Windows

**Project Type**: Packable F# library with companion test project and fixture
corpus; no CLI or lifecycle authoring commands in this feature

**Performance Goals**: Deterministic validation of representative lifecycle
fixtures in under 1 second per fixture set on a normal developer machine;
byte-identical JSON output for identical inputs across three consecutive runs

**Constraints**: `.fsi` signature files before `.fs` implementation; public
surface baseline for Tier 1 API; structured graph data wins executable
decisions when prose disagrees; generated views are outputs, not proof of
currency; SDD remains usable without Governance installed

**Scale/Scope**: Initial contract covers project-level SDD artifacts,
work-item artifacts, lifecycle identifiers, diagnostics, generated-view
metadata, fixture catalog, and Governance compatibility boundaries for one
feature slice

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must require `.fsi` signatures, FSI/prelude usage evidence, public-surface tests, then `.fs` bodies. | PASS |
| II. Structured Artifacts Are the Machine Contract | Plan defines authored sources, structured models, generated views, stale behavior, diagnostics, and conflict precedence. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Source structure requires `.fsi` for every public module and a surface-baseline drift test. | PASS |
| IV. Idiomatic Simplicity Is the Default | Single artifact-model library starts with records, discriminated unions, modules, BCL, and FSharp.Core. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | This feature is a pure artifact/model library; no stateful command or I/O workflow is introduced. Future parsers expose pure functions over file snapshots. | PASS |
| VI. Test Evidence Is Mandatory | Plan requires valid/invalid fixtures, semantic tests, schema tests, stale-view tests, deterministic JSON snapshots, and Governance boundary review. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Agent guidance targets are modeled as projections from lifecycle data and not as authority. | PASS |
| VIII. Observability And Safe Failure | Diagnostics cover missing artifacts, schema errors, duplicate ids, unknown references, stale views, and prose/structured mismatches. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/001-sdd-artifact-model/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- artifact-schemas.md
|   |-- work-model-json.md
|   |-- lifecycle-rule-contracts.md
|   `-- fixture-catalog.md
`-- tasks.md                 # Created by $speckit-tasks, not this command
```

### Source Code (repository root)

```text
FS.GG.SDD.sln
Directory.Build.props
Directory.Packages.props

src/
`-- FS.GG.SDD.Artifacts/
    |-- FS.GG.SDD.Artifacts.fsproj
    |-- Identifiers.fsi
    |-- Identifiers.fs
    |-- SchemaVersion.fsi
    |-- SchemaVersion.fs
    |-- ArtifactRef.fsi
    |-- ArtifactRef.fs
    |-- Diagnostics.fsi
    |-- Diagnostics.fs
    |-- GenerationManifest.fsi
    |-- GenerationManifest.fs
    |-- LifecycleArtifacts.fsi
    |-- LifecycleArtifacts.fs
    |-- WorkModel.fsi
    |-- WorkModel.fs
    |-- LifecycleRuleContracts.fsi
    |-- LifecycleRuleContracts.fs
    |-- Serialization.fsi
    `-- Serialization.fs

scripts/
`-- prelude.fsx

tests/
|-- FS.GG.SDD.Artifacts.Tests/
|   |-- FS.GG.SDD.Artifacts.Tests.fsproj
|   |-- IdentifierTests.fs
|   |-- SchemaContractTests.fs
|   |-- WorkModelTests.fs
|   |-- DiagnosticTests.fs
|   |-- DeterministicJsonTests.fs
|   |-- GovernanceBoundaryTests.fs
|   `-- SurfaceBaselineTests.fs
`-- fixtures/
    `-- sdd-artifact-model/
        |-- valid-work-item/
        |-- malformed-schema-version/
        |-- missing-artifact/
        |-- duplicate-identifiers/
        |-- unknown-reference/
        |-- prose-structured-mismatch/
        |-- stale-generated-view/
        `-- deterministic-ordering/
```

**Structure Decision**: Use one packable library,
`FS.GG.SDD.Artifacts`, for the initial contract. It may contain modules for
identifiers, artifacts, diagnostics, generation manifests, work models,
lifecycle rule contracts, and serialization, but no CLI, command workflow, or
generator project. Later features can split packages only after this public
contract proves stable enough to justify the extra surface.

Strict YAML parsing and fixture/work-item loading are owned by the artifact
model library as pure functions over file snapshots. Public signatures should
make that responsibility visible through `LifecycleArtifacts.fsi` and
`Serialization.fsi`; this feature still does not introduce commands,
generators, or runtime I/O workflows.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/artifact-schemas.md](contracts/artifact-schemas.md)
- [contracts/work-model-json.md](contracts/work-model-json.md)
- [contracts/lifecycle-rule-contracts.md](contracts/lifecycle-rule-contracts.md)
- [contracts/fixture-catalog.md](contracts/fixture-catalog.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: implementation tasks must create `.fsi` signatures before `.fs` bodies. |
| Structured machine contract | PASS: every lifecycle artifact has an authored source, structured contract, generated-view relationship, stale behavior, and diagnostics. |
| Public API baseline | PASS: tests include a surface baseline drift check. |
| MVU boundary | PASS: no stateful/I/O command workflow is introduced in this slice. |
| Evidence | PASS: fixture and test obligations are mapped to every user story and invalid state. |
| Agent contract | PASS: Claude/Codex guidance targets are modeled as generated projections only. |
| Governance boundary | PASS: SDD exposes optional compatibility contracts without route, profile, freshness, or enforcement semantics. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
