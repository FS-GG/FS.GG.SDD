# Implementation Plan: Normalized Work Model

**Branch**: `002-normalized-work-model` | **Date**: 2026-06-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-normalized-work-model/spec.md`

**Note**: Spec Kit setup returned an empty script branch because this repository
is currently on Git branch `main`; the active feature context is persisted in
`.specify/feature.json` as `specs/002-normalized-work-model`.

## Summary

Complete the normalized SDD work-model generation path over the artifact model
introduced by `001-sdd-artifact-model`. This feature extends the existing
`FS.GG.SDD.Artifacts` package so authored `.fsgg` and `work/<id>` sources can
be normalized into deterministic `readiness/<id>/work-model.json` content with
source digests, schema compatibility status, generated-view currency checks,
stable diagnostics, migration posture, and optional Governance compatibility
facts. It does not introduce lifecycle authoring commands, route/profile logic,
evidence freshness evaluation, or protected-boundary enforcement.

## Technical Context

**Language/Version**: F# on .NET SDK targeting `net10.0`

**Primary Dependencies**: Existing package dependencies only: BCL,
FSharp.Core, System.Text.Json, and YamlDotNet for strict YAML parsing

**Storage**: Filesystem artifacts represented as repository-relative snapshot
data in the library; generated output target is
`readiness/<id>/work-model.json`

**Testing**: `dotnet test` with xUnit; public-surface tests through `.fsi`
signatures; FSI/prelude evidence; golden JSON and fixture diagnostics

**Target Platform**: Cross-platform .NET library and tests on
Linux/macOS/Windows

**Project Type**: Existing packable F# library with companion test project and
fixture corpus; no CLI project or command host in this feature

**Performance Goals**: Normalize and serialize a representative lifecycle
fixture in under 1 second on a normal developer machine; produce byte-identical
work-model JSON for identical inputs across three consecutive runs

**Constraints**: `.fsi` signatures before `.fs` implementation; structured
graph data wins executable decisions when prose disagrees; generated views are
outputs, not proof of currency; deterministic JSON must not depend on clocks,
terminal width, ANSI output, directory enumeration order, or host-specific path
separators; SDD remains useful without Governance installed

**Scale/Scope**: One selected work item at a time, including project-level SDD
sources, work-item lifecycle sources, generated work-model currency checks,
diagnostics, fixtures, and optional Governance boundary references

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Spec -> FSI -> Semantic Tests -> Implementation | Tasks must update or add `.fsi` signatures for normalization, generation, currency, and migration contracts before `.fs` bodies; FSI/prelude evidence must exercise the public shape before implementation hardens it. | PASS |
| II. Structured Artifacts Are the Machine Contract | Plan declares the authored sources, structured model, generated work-model view, stale behavior, diagnostics, and conflict precedence. | PASS |
| III. Visibility Lives in `.fsi`, Not in `.fs` | Existing public modules keep `.fsi` signatures; any new public module requires an `.fsi` and surface-baseline update. | PASS |
| IV. Idiomatic Simplicity Is the Default | Extend the existing library with records, discriminated unions, pure functions, BCL, System.Text.Json, and YamlDotNet; no framework or reflection-heavy machinery is planned. | PASS |
| V. Elmish/MVU Is the Boundary for Stateful or I/O Workflows | This feature is a pure artifact-model/generation library over file snapshots and returned output content; no lifecycle command or stateful I/O workflow is introduced. | PASS |
| VI. Test Evidence Is Mandatory | Plan requires failing semantic tests, golden JSON fixtures, invalid-state fixtures, stale generated-model fixtures, migration fixtures, FSI/prelude evidence, and surface-baseline coverage. | PASS |
| VII. Agent And Human Workflows Must Share One Contract | Agent guidance remains a future generated projection over the work model; this feature exposes model data without creating a second source of truth. | PASS |
| VIII. Observability And Safe Failure | Diagnostics are required for malformed input, missing artifacts, stale generated models, task graph conflicts, requirement typing gaps, schema compatibility, and optional Governance boundary failures. | PASS |

No constitution violations are present. Complexity tracking is not required.

## Project Structure

### Documentation (this feature)

```text
specs/002-normalized-work-model/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- normalization-api.md
|   |-- work-model-json.md
|   |-- schema-migration.md
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
    |-- LifecycleRuleContracts.fsi
    |-- LifecycleRuleContracts.fs
    |-- WorkModel.fsi
    |-- WorkModel.fs
    |-- Serialization.fsi
    `-- Serialization.fs

scripts/
`-- prelude.fsx

tests/
|-- FS.GG.SDD.Artifacts.Tests/
|   |-- DeterministicJsonTests.fs
|   |-- DiagnosticTests.fs
|   |-- GovernanceBoundaryTests.fs
|   |-- NormalizedWorkModelTests.fs        # planned
|   |-- SchemaMigrationTests.fs            # planned
|   |-- GeneratedModelCurrencyTests.fs     # planned
|   |-- PublicSurface.baseline
|   |-- SurfaceBaselineTests.fs
|   `-- WorkModelTests.fs
`-- fixtures/
    |-- sdd-artifact-model/
    `-- normalized-work-model/
        |-- valid-work-item/
        |-- requirement-not-typed/
        |-- work-model-inconsistent/
        |-- prose-structured-mismatch/
        |-- missing-generated-model/
        |-- stale-source-digest/
        |-- stale-generator-version/
        |-- malformed-generated-json/
        |-- deprecated-schema-version/
        |-- unsupported-schema-version/
        |-- future-schema-version/
        `-- deterministic-ordering/
```

**Structure Decision**: Extend the existing packable
`FS.GG.SDD.Artifacts` library and test project. The current package already
owns lifecycle identifiers, artifact contracts, diagnostics, generation
manifests, a first work-model shape, serialization, and fixtures. Creating a
new package or CLI would fragment the first public contract and violate this
feature's boundary. Public API deltas belong in existing `.fsi` files unless a
small new public module is clearly justified during tasks.

Strict YAML parsing, Markdown extraction, work-model assembly, generated-model
currency checks, migration classification, and JSON serialization stay pure
over `FileSnapshot` values and returned output records. Later lifecycle
commands will own real filesystem writes and command UX.

## Phase 0 Research

Research is recorded in [research.md](research.md). All planning unknowns are
resolved; no clarification markers remain.

## Phase 1 Design

Design artifacts generated by this plan:

- [data-model.md](data-model.md)
- [contracts/normalization-api.md](contracts/normalization-api.md)
- [contracts/work-model-json.md](contracts/work-model-json.md)
- [contracts/schema-migration.md](contracts/schema-migration.md)
- [contracts/fixture-catalog.md](contracts/fixture-catalog.md)
- [quickstart.md](quickstart.md)

## Post-Design Constitution Check

| Gate | Result |
|---|---|
| FSI-first public surface | PASS: normalization, currency, migration, and serialization contracts are planned as `.fsi` deltas before implementation. |
| Structured machine contract | PASS: authored sources, structured model, generated work-model view, stale behavior, schema posture, and diagnostics are specified. |
| Public API baseline | PASS: surface-baseline update is required when public signatures change. |
| MVU boundary | PASS: no command or stateful I/O workflow is introduced; generated output is returned for later commands to write. |
| Evidence | PASS: quickstart and fixture catalog define valid, invalid, stale, migration, deterministic JSON, FSI, and package evidence. |
| Agent contract | PASS: Claude/Codex guidance remains a consumer of the work model, not authority. |
| Governance boundary | PASS: SDD exposes optional compatibility facts only and does not implement route, profile, freshness, or enforcement semantics. |

No post-design constitution violations are present.

## Complexity Tracking

No constitution violations or complexity exceptions are justified for this
feature.
