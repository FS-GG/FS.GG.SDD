# Data Model: Normalized Work Model

## WorkModelGenerationRequest

- **Fields**: `workId`, `snapshots`, `generatorVersion`, optional
  `expectedOutputPath`
- **Validation**: `workId` must be stable and match the selected work-item
  sources; snapshot paths must be repository-relative and normalized with `/`;
  generator version must be present and stable.
- **Relationships**: Consumed by the pure generation API to produce a
  `WorkModelGenerationResult`.

## WorkModelGenerationResult

- **Fields**: `workId`, `outputPath`, `model`, `json`, `outputDigest`,
  `diagnostics`
- **Validation**: `json` must be byte-stable for identical inputs;
  `outputDigest` is calculated from the exact emitted bytes; diagnostics are
  sorted by the diagnostic contract.
- **Relationships**: Later lifecycle commands will write `json` to
  `outputPath`; tests and agents can inspect `model` directly.

## WorkModel

- **Generated view**: `readiness/<id>/work-model.json`
- **Fields**: schema version, model version, work id, project summary, source
  entries, work-item summary, requirements, decisions, tasks, evidence
  declarations, generated-view manifests, diagnostics, and Governance boundary
  entries.
- **Validation**: Generated content must have deterministic property order,
  deterministic list ordering, no implicit clock data, stable source digests,
  and stable diagnostic ordering.
- **Relationships**: Source of normalized SDD facts for later analysis,
  verify, ship, summary, and agent-guidance views.

## ProjectSummary

- **Source**: `.fsgg/project.yml`
- **Fields**: project id, default work root, SDD config pointer, agent config
  pointer, optional Governance boundary pointers.
- **Validation**: Project id must be stable; paths must be repository-relative;
  optional Governance files are references only.
- **Relationships**: Contributes project context to every work model.

## SourceEntry

- **Source**: Every authored lifecycle source included in the selected work
  item.
- **Fields**: path, kind, owner, schema version, schema compatibility status,
  source digest, and optional source diagnostics.
- **Validation**: Paths are normalized and sorted; schema version is classified
  before normalization; digest is calculated over normalized source bytes.
- **Relationships**: Used by generated-view manifests and stale-view checks.

## WorkItemSummary

- **Source**: Work-item structured metadata, normally in `work/<id>/spec.md`
  front matter.
- **Fields**: work id, title, lifecycle stage, change tier, status, and
  optional prose status.
- **Validation**: Work id must match the selected work id; stage must be known;
  change tier must be recognized; structured status is authoritative when prose
  disagrees.
- **State transitions**: This feature records the current stage and status but
  does not implement lifecycle command transitions.

## RequirementEntry

- **Source**: Specification requirements and acceptance criteria.
- **Fields**: requirement id, title, text, acceptance criteria, priority,
  source path, source location, linked task ids, linked evidence ids.
- **Validation**: Requirement ids are unique; Markdown requirement ids missing
  from the structured requirement set emit `requirementNotTyped`; links must
  reference known tasks and evidence.
- **Relationships**: Tasks and evidence reference requirements by stable id.

## DecisionEntry

- **Source**: Clarification, plan, or structured decision sources.
- **Fields**: decision id, title, decision, rationale when present, source path,
  source location, linked task ids.
- **Validation**: Decision ids are unique; task decision references must point
  to known decisions.
- **Relationships**: Tasks reference decisions when implementation depends on a
  durable clarification or planning choice.

## TaskEntry

- **Source**: `work/<id>/tasks.yml`
- **Fields**: task id, title, status, owner, dependencies, requirements,
  decisions, required skills, required evidence, source path, source location.
- **Validation**: Task ids are unique; dependencies must reference known tasks
  and be acyclic; requirement and decision references must exist; `done` tasks
  require evidence declarations.
- **State transitions**: `pending` -> `in-progress` -> `done`;
  `pending` -> `skipped` only with rationale. This feature validates recorded
  state but does not advance it.

## EvidenceEntry

- **Source**: `work/<id>/evidence.yml`
- **Fields**: evidence id, kind, subject type, subject id, task references,
  requirement references, artifact references, result, synthetic flag, optional
  rationale, source path, source location.
- **Validation**: Evidence ids are unique; subjects and references must point
  to known work-model entries; synthetic evidence requires a visible rationale;
  deferrals require a removal condition.
- **Relationships**: Supports task and requirement readiness but does not
  compute Governance freshness or enforcement.

## GeneratedViewEntry

- **Source**: Existing generated view snapshots and generated output metadata.
- **Fields**: path, kind, schema version, generator id, generator version,
  source identities, output digest, currency status, diagnostics.
- **Validation**: Any mismatch in source digest, schema version, generator
  version, or output digest emits `staleGeneratedView`; a missing expected work
  model emits `missingGeneratedWorkModel`.
- **Relationships**: Represents `readiness/<id>/work-model.json` in this
  feature and leaves later readiness views for later phases.

## SchemaCompatibility

- **Fields**: raw version, parsed version when valid, status, message,
  expected range, migration hint.
- **Allowed statuses**: `current`, `deprecated`, `unsupported`, `malformed`,
  `future`.
- **Validation**: `current` continues; `deprecated` emits a warning;
  `unsupported`, `malformed`, and unsafe `future` versions emit blocking
  diagnostics.
- **Relationships**: Attached to source entries and diagnostics.

## Diagnostic

- **Fields**: diagnostic id, severity, artifact, source location when
  available, message, correction, related ids.
- **Validation**: Required diagnostic ids for this feature include
  `requirementNotTyped`, `workModelInconsistent`, `proseStructuredMismatch`,
  `staleGeneratedView`, `missingGeneratedWorkModel`,
  `malformedSchemaVersion`, `unsupportedSchemaVersion`,
  `deprecatedSchemaVersion`, `futureSchemaVersion`, `duplicateIdentifier`,
  `unknownReference`, and `malformedDigest`.
- **Relationships**: Appears in generated JSON, generated-view manifests, tests,
  readiness evidence, and later analysis/verify/ship projections.

## GovernanceBoundaryEntry

- **Source**: Optional Governance pointers in SDD-owned project configuration.
- **Fields**: path, owner, required-by-SDD flag, relationship, optional source
  pointer.
- **Validation**: Boundary paths are repository-relative; missing or invalid
  Governance files do not block SDD-only normalization unless an SDD source
  explicitly requires the boundary.
- **Relationships**: Provides optional facts for Governance without route,
  profile, freshness, or gate behavior.
