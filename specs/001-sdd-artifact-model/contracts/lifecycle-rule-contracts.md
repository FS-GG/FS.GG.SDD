# Contract: Lifecycle Rule Contracts

## Scope

Lifecycle rule contracts describe SDD-owned checks over SDD artifacts. They are
Governance-compatible in shape, but this feature does not depend on Governance
runtime packages and does not implement route, profile, freshness, gate, audit,
or enforcement semantics.

## Rule Contract Shape

```yaml
schemaVersion: 1
id: requiredSpecSections
owner: sdd
stage: specify
inputs:
  - artifact: work/{workId}/spec.md
outputs:
  findingShape: diagnostic
diagnostics:
  - missingArtifact
  - requirementNotTyped
evidence:
  required:
    - specificationFixture
testObligations:
  - schemaValidationFixture
  - semanticPublicSurfaceTest
governanceCompatibility:
  routeAware: false
  profileAware: false
  freshnessAware: false
  enforceableBySdd: false
```

Required fields:

- `schemaVersion`
- `id`
- `owner`
- `stage`
- `inputs`
- `outputs.findingShape`
- `diagnostics`
- `evidence`
- `testObligations`
- `governanceCompatibility`

## Initial Rule Contracts

### `requiredSpecSections`

Validates that specification sources expose user value, scope, non-goals,
stories, requirements, acceptance criteria, ambiguity state, and impact
classification in the normalized model.

Diagnostics: `missingArtifact`, `requirementNotTyped`,
`malformedSchemaVersion`, `proseStructuredMismatch`.

### `planObligations`

Validates that technical plans declare architecture decisions, contracts, public
API impact, dependencies, risks, migration posture, verification strategy, and
requirement traceability.

Diagnostics: `missingArtifact`, `unknownReference`,
`proseStructuredMismatch`.

### `taskGraphShape`

Validates task ids, dependencies, owner or agent assumptions, required skills,
required evidence, status transitions, and requirement or decision references.

Diagnostics: `duplicateIdentifier`, `unknownReference`,
`workModelInconsistent`, `proseStructuredMismatch`.

### `evidenceDeclarations`

Validates declared implementation, verification, synthetic, accepted deferral,
and missing evidence records.

Diagnostics: `missingArtifact`, `unknownReference`, `malformedSchemaVersion`,
`workModelInconsistent`.

### `loadedAgentSkills`

Validates that task-required skill ids or capability tags can be resolved to
generated or installed Claude/Codex guidance before an agent is asked to
perform the work.

Diagnostics: `unknownReference`, `missingArtifact`, `staleGeneratedView`.

### `testObligations`

Validates the test evidence implied by Tier 1 or Tier 2 impact, public `.fsi`
changes, schema changes, generated-view changes, command output contracts,
agent guidance, task graph, and evidence declarations.

Diagnostics: `missingArtifact`, `unknownReference`,
`workModelInconsistent`, `staleGeneratedView`.

## Explicit Non-Responsibilities

These contracts intentionally exclude:

- route selection;
- changed-path analysis;
- Governance profile adjustment;
- evidence freshness policy;
- protected-branch gate enforcement;
- release enforcement;
- audit verdicts.

Those behaviors belong to FS.GG.Governance. SDD may emit readiness facts that
Governance can consume later.
