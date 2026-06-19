# Contract: Normalization API

## Scope

This contract describes the public library surface needed to normalize SDD
lifecycle sources into a generated work-model output. The contract extends the
existing `FS.GG.SDD.Artifacts` package. It does not define a CLI command,
filesystem writer, route engine, evidence freshness engine, or Governance gate.

## Public Modules

The implementation should prefer extending existing modules:

- `LifecycleArtifacts` for snapshot parsing and source classification.
- `WorkModel` for normalized model types and validation helpers.
- `GenerationManifest` for generated-view metadata and currency checks.
- `Diagnostics` for stable diagnostic factories and sorting.
- `Serialization` for deterministic JSON output.

A new module is allowed only if it removes meaningful complexity from those
modules and receives a matching `.fsi` signature before implementation.

## Input Contract

```fsharp
type WorkModelGenerationRequest =
    { WorkId: string
      Snapshots: LifecycleArtifacts.FileSnapshot list
      GeneratorVersion: SchemaVersion.GeneratorVersion
      ExpectedOutputPath: string option }
```

Validation rules:

- `WorkId` must identify one selected work item.
- Snapshot paths must be repository-relative and normalized with `/`.
- Snapshot text is the complete authored or generated artifact content.
- `GeneratorVersion` is required for generated-view currency.
- `ExpectedOutputPath` defaults to `readiness/<workId>/work-model.json`.

## Output Contract

```fsharp
type WorkModelGenerationResult =
    { WorkId: string
      OutputPath: string
      Model: WorkModel.WorkModel
      Json: string
      OutputDigest: SchemaVersion.OutputDigest
      Diagnostics: Diagnostics.Diagnostic list }
```

Validation rules:

- `Json` must be the exact deterministic content represented by `Model`.
- `OutputDigest` is calculated from the emitted JSON bytes.
- `Diagnostics` mirrors the model diagnostics after final generated-output
  checks.
- The result does not write to disk.

## Required Functions

```fsharp
val generateWorkModel:
    request: WorkModelGenerationRequest -> WorkModelGenerationResult

val normalizeSnapshotsToWorkModel:
    snapshots: LifecycleArtifacts.FileSnapshot list -> workId: string -> WorkModel.WorkModel

val serializeWorkModel:
    model: WorkModel.WorkModel -> string

val checkGeneratedWorkModelCurrency:
    snapshots: LifecycleArtifacts.FileSnapshot list ->
    workId: string ->
    generatorVersion: SchemaVersion.GeneratorVersion ->
        Diagnostics.Diagnostic list
```

The existing `normalizeSnapshotsToWorkModel` and `serializeWorkModel` functions
remain available. New generation and currency helpers must preserve stable
ordering and diagnostics.

## Diagnostic Behavior

The API must emit stable diagnostics for:

- missing authored lifecycle artifacts;
- malformed, deprecated, unsupported, or future schema versions;
- Markdown requirements or acceptance criteria missing from the structured
  requirement model;
- task, decision, evidence, artifact, source digest, or generator references
  that cannot be resolved;
- prose/structured lifecycle mismatches;
- duplicate logical ids;
- stale, malformed, or missing generated work-model output.

Diagnostics must distinguish malformed user input from tool defects. Malformed
input returns diagnostics in the result; unexpected tool defects may fail fast.

## Determinism

The API must not depend on:

- filesystem enumeration order;
- platform path separators;
- process locale;
- terminal width or ANSI output;
- wall-clock time;
- untracked environment variables.

All lists in the returned model and JSON must use documented sort keys.

## Governance Boundary

The API may surface optional Governance boundary entries when SDD-owned sources
point to Governance files. It must not parse Governance policy, select routes,
adjust severities by profile, compute freshness, select gates, or emit
protected-boundary verdicts.
