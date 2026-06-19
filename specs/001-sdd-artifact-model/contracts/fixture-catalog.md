# Contract: Fixture Catalog

## Scope

The first artifact model implementation must include fixtures that prove the
contract with real files. Fixtures live under
`tests/fixtures/sdd-artifact-model/` and are referenced by tests rather than
being embedded in test code.

## Required Fixtures

### `valid-work-item`

Purpose: a representative SDD work item with project config, SDD policy, agent
targets, specification metadata, task declarations, evidence declarations, and
generated view metadata.

Expected result: zero blocking diagnostics and a deterministic work model.

### `malformed-schema-version`

Purpose: structured artifacts with missing, malformed, unsupported, and future
schema versions.

Expected diagnostics: `malformedSchemaVersion`, `unsupportedSchemaVersion`.

### `missing-artifact`

Purpose: required SDD-owned lifecycle artifacts are absent from the project or
work item while referenced by the work model contract.

Expected diagnostic: `missingArtifact` with the required artifact path,
lifecycle stage, and correction expected.

### `duplicate-identifiers`

Purpose: duplicate requirements, decisions, tasks, and evidence ids.

Expected diagnostic: `duplicateIdentifier` with source locations for every
duplicate.

### `unknown-reference`

Purpose: task, evidence, and generated-view references to missing
requirements, decisions, artifacts, source digests, or generator versions.

Expected diagnostics: `unknownReference`, `workModelInconsistent`.

### `prose-structured-mismatch`

Purpose: Markdown prose and structured graph data disagree on status,
dependencies, ownership, required evidence, or requirement linkage.

Expected diagnostic: `proseStructuredMismatch`. Expected model behavior:
structured graph data is used for executable lifecycle decisions while prose is
kept visible.

### `stale-generated-view`

Purpose: generated readiness metadata records source, generator, or output
digests that no longer match the authored source tree.

Expected diagnostic: `staleGeneratedView`.

### `deterministic-ordering`

Purpose: inputs intentionally ordered differently across files to prove stable
normalization and JSON emission.

Expected result: three consecutive validation runs produce byte-identical JSON
and diagnostic ordering.

## Fixture Manifest Fields

Each fixture directory should include a manifest:

```yaml
schemaVersion: 1
id: duplicate-identifiers
purpose: Duplicate lifecycle identifiers produce actionable diagnostics.
expectedDiagnostics:
  - duplicateIdentifier
expectedBlocking: true
```

The manifest is test data. The artifact model library should not treat fixture
manifests as SDD lifecycle sources.
