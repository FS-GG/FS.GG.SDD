# Quickstart: Normalized Work Model

## Purpose

This guide describes the validation path for the normalized work-model feature.
It is written for the implementation phase after `$speckit-tasks` generates the
task list.

## Prerequisites

- .NET SDK with `net10.0` support
- Repository root as the working directory
- No Governance runtime installation required

## Validate The Public Surface

```bash
dotnet build FS.GG.SDD.sln
```

Expected outcome:

- `FS.GG.SDD.Artifacts` builds successfully.
- Public `.fsi` signatures compile before implementation bodies.
- Surface-baseline tests identify any intentional public API change.

## Run The Normalized Work-Model Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~NormalizedWorkModel"
```

Expected outcome:

- `valid-work-item` produces zero blocking diagnostics.
- `requirement-not-typed` emits `requirementNotTyped`.
- `work-model-inconsistent` emits `workModelInconsistent`.
- Prose/structured mismatch keeps structured data authoritative and emits a
  warning.

## Run Generated-Model Currency Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~GeneratedModelCurrency"
```

Expected outcome:

- Missing generated model emits `missingGeneratedWorkModel`.
- Stale source digest emits `staleGeneratedView`.
- Stale generator version emits `staleGeneratedView`.
- Malformed generated JSON emits `staleGeneratedView` with a parse-focused
  correction.

## Run Schema Migration Tests

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~SchemaMigration"
```

Expected outcome:

- Current schema versions pass without schema diagnostics.
- Deprecated schema versions emit warnings without blocking normalization.
- Unsupported, malformed, and future schema versions emit blocking diagnostics.

## Prove Deterministic JSON

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~DeterministicJson"
```

Expected outcome:

- Three consecutive generations over identical snapshots produce byte-identical
  JSON.
- Sources, graph entries, generated views, diagnostics, and Governance
  boundaries sort by documented keys.

## Exercise The API From FSI

```bash
dotnet build src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj -c Release
dotnet fsi scripts/prelude.fsx
```

Expected outcome:

- The prelude loads fixture snapshots.
- The public generation or normalization API creates a work model.
- Output prints work id, model version, blocking diagnostic count, requirement
  ids, task ids, Governance boundary paths, and JSON byte count.

## Package Evidence

```bash
dotnet pack src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj -c Release
```

Expected outcome:

- The package remains packable.
- Package metadata still identifies `FS.GG.SDD.Artifacts`.
- No lifecycle CLI, Governance runtime, rendering template, or generated-product
  runtime dependency is introduced.
