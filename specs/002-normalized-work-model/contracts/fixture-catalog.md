# Contract: Fixture Catalog

## Scope

Fixtures prove the normalized work-model contract through real
filesystem-shaped source trees. They are stored under
`tests/fixtures/normalized-work-model/` and are consumed by semantic tests
through the public library surface.

## Fixture Manifest Shape

Each fixture directory includes `manifest.yml`:

```yaml
schemaVersion: 1
fixture:
  id: valid-work-item
  workId: 002-normalized-work-model
  purpose: Valid normalized work-model generation
expected:
  blockingDiagnostics: false
  diagnostics: []
  output: readiness/002-normalized-work-model/work-model.json
```

Required manifest fields:

- `fixture.id`
- `fixture.workId`
- `fixture.purpose`
- `expected.blockingDiagnostics`
- `expected.diagnostics`

Optional manifest fields:

- `expected.output`
- `expected.goldenJson`
- `expected.schemaStatus`
- `expected.governanceBoundaries`

## Required Fixtures

### `valid-work-item`

Purpose: proves a representative SDD work item normalizes with all required
facts and zero blocking diagnostics.

Expected diagnostics: none.

### `requirement-not-typed`

Purpose: Markdown contains a requirement or acceptance criterion id that is not
present in the normalized structured requirement set.

Expected diagnostics: `requirementNotTyped`.

### `work-model-inconsistent`

Purpose: structured tasks or evidence declarations reference unknown
requirements, decisions, tasks, evidence, artifacts, source digests, or
generator versions.

Expected diagnostics: `workModelInconsistent`.

### `prose-structured-mismatch`

Purpose: Markdown prose disagrees with structured lifecycle data for status,
dependencies, ownership, or required evidence.

Expected diagnostics: `proseStructuredMismatch`.

### `missing-generated-model`

Purpose: authored sources are sufficient to produce a work model, but the
expected `readiness/<id>/work-model.json` output is absent during currency
checking.

Expected diagnostics: `missingGeneratedWorkModel`.

### `stale-source-digest`

Purpose: generated work-model metadata records an outdated source digest.

Expected diagnostics: `staleGeneratedView`.

### `stale-generator-version`

Purpose: generated work-model metadata records an older generator version.

Expected diagnostics: `staleGeneratedView`.

### `malformed-generated-json`

Purpose: generated work-model output exists but cannot be parsed as valid JSON.

Expected diagnostics: `staleGeneratedView`.

### `deprecated-schema-version`

Purpose: source uses a readable but deprecated schema version.

Expected diagnostics: `deprecatedSchemaVersion` warning and no blocking
diagnostic.

### `unsupported-schema-version`

Purpose: source uses a known unsupported schema version.

Expected diagnostics: `unsupportedSchemaVersion` blocking diagnostic.

### `future-schema-version`

Purpose: source uses a schema version newer than this generator understands.

Expected diagnostics: `futureSchemaVersion` blocking diagnostic unless an
explicit compatibility rule is added later.

### `deterministic-ordering`

Purpose: proves sorting for sources, requirements, decisions, tasks, evidence,
generated views, diagnostics, and Governance boundaries.

Expected diagnostics: fixture-specific expected diagnostics in deterministic
order; three generated JSON runs are byte-identical.

## Evidence Requirements

Implementation evidence must include:

- failing tests for each fixture before implementation;
- passing tests for each fixture after implementation;
- a golden JSON comparison for valid and deterministic fixtures;
- FSI/prelude output proving public generation APIs are usable;
- surface-baseline update for any `.fsi` public API change;
- compatibility review confirming Governance semantics remain out of scope.
