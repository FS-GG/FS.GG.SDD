# Data Model: SDD Artifact Model

## Identity And Version Types

### WorkId

- **Fields**: `value`
- **Validation**: Lowercase kebab-case, starts with a digit sequence or a
  product-approved slug, unique within a project work root.
- **Relationships**: Owns one `WorkItem`; appears in readiness paths,
  diagnostics, generated views, task references, and evidence references.

### LifecycleStage

- **Fields**: `value`
- **Allowed values**: `charter`, `specify`, `clarify`, `checklist`, `plan`,
  `tasks`, `analyze`, `implement`, `evidence`, `verify`, `ship`.
- **Relationships**: Used by `WorkItem`, diagnostics, rule contracts, and task
  status transitions.

### RequirementId, DecisionId, TaskId, EvidenceId

- **Fields**: `value`, `scope`
- **Validation**: Stable within a work item; case-sensitive comparisons are
  forbidden; duplicates emit `duplicateIdentifier`.
- **Relationships**: Requirements link to tasks, decisions, evidence, and
  acceptance criteria. Decisions link to tasks and plan obligations. Tasks link
  to requirements, decisions, dependencies, skills, and evidence declarations.

### SchemaVersion

- **Fields**: `major`, `minor`, `raw`
- **Validation**: Present on structured artifacts; malformed values emit
  `malformedSchemaVersion`; unsupported values emit `unsupportedSchemaVersion`;
  future compatible versions emit a warning unless the artifact declares a
  migration requirement.
- **Relationships**: Used by every `.fsgg/*.yml`, `tasks.yml`,
  `evidence.yml`, structured front matter, generated view, and rule contract.

### SourceDigest, OutputDigest, GeneratorVersion

- **Fields**: `algorithm`, `value` for digests; `id`, `version` for generator.
- **Validation**: SHA-256 digests use lowercase hex; missing or malformed
  digests emit `malformedDigest`; generator versions must be stable strings.
- **Relationships**: `GenerationManifest` uses these to determine stale
  generated views.

## Project-Level Artifacts

### ProjectLifecycleConfig

- **Authored source**: `.fsgg/project.yml`
- **Fields**: `schemaVersion`, `projectId`, `defaultWorkRoot`, `sddSchema`,
  optional pointers to Governance policy or capability catalogs.
- **Validation**: Work root must be relative to the project root; optional
  Governance pointers are references only and do not make Governance required.
- **Generated views**: Contributes project identity and source digest to
  `readiness/<id>/work-model.json`.

### SddLifecyclePolicy

- **Authored source**: `.fsgg/sdd.yml`
- **Fields**: `schemaVersion`, artifact layout, generated-view policy,
  schema migration posture, diagnostic severity defaults.
- **Validation**: Layout paths must stay within the repository root; generated
  view policy must identify required source digests and generator versions.
- **Generated views**: Contributes lifecycle policy to work model, analysis,
  verify, and ship readiness outputs.

### AgentGuidanceConfig

- **Authored source**: `.fsgg/agents.yml`
- **Fields**: `schemaVersion`, targets, agent ids, output paths, source model
  references, generation policy.
- **Validation**: Claude and Codex targets must reference the same lifecycle
  contract; generated prompts are projections, not authority.
- **Generated views**: Contributes to `readiness/<id>/agent-commands/` and stale
  guidance diagnostics.

### GovernanceCompatibilityBoundary

- **Authored sources**: Optional `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, `.fsgg/tooling.yml`
- **Fields**: `artifactRef`, `owner`, `boundaryKind`, `requiredBySdd`
- **Validation**: SDD recognizes these as optional boundaries and reports
  malformed references only when SDD readiness explicitly points at them.
- **Generated views**: SDD readiness can expose references; Governance computes
  route, profile, freshness, evidence, audit, and enforcement separately.

## Work-Item Artifacts

### WorkItem

- **Authored source**: `work/<id>/spec.md` front matter plus stage artifacts
- **Fields**: `workId`, `title`, `stage`, `changeTier`, authored sources,
  structured artifacts, generated views, requirements, decisions, tasks,
  evidence declarations, diagnostics.
- **Validation**: `workId` must match the directory id; required artifacts emit
  `missingArtifact`; unknown references emit `unknownReference`.
- **State transitions**: Stages progress through standard SDD order. Earlier
  stages may be reopened, but the work model records stale downstream views.

### LifecycleArtifact

- **Authored sources**: `charter.md`, `spec.md`, `clarifications.md`,
  `checklist.md`, `plan.md`, `contracts/`, `tasks.yml`, `evidence.yml`
- **Fields**: `artifactRef`, `owner`, `purpose`, `stage`, `sourcePath`,
  `schemaVersion`, `sourceDigest`, `structuredContract`, `generatedViews`.
- **Validation**: Required artifacts must exist for the stage being checked;
  structured front matter must parse; duplicate ids and mismatches produce
  diagnostics.
- **Generated views**: Sources for `work-model.json`, `analysis.json`,
  `verify.json`, `ship.json`, `summary.md`, and agent guidance.

### Requirement

- **Authored source**: Specification structured front matter or parsed
  requirement sections
- **Fields**: `requirementId`, `title`, `text`, `acceptanceCriteria`,
  `priority`, `sourceLocation`, linked tasks, linked evidence.
- **Validation**: Requirements must be testable, stable, and unique. A Markdown
  requirement that is not represented in the normalized model emits
  `requirementNotTyped`.

### Decision

- **Authored source**: Clarification or plan structured data
- **Fields**: `decisionId`, `title`, `decision`, `rationale`,
  `alternativesConsidered`, `sourceLocation`, linked requirements and tasks.
- **Validation**: Referenced decisions must exist before task generation.

### Task

- **Authored source**: `work/<id>/tasks.yml`
- **Fields**: `taskId`, `title`, `status`, `owner`, dependencies,
  requirement references, decision references, required skills,
  required evidence, source location.
- **Validation**: Dependencies must reference existing tasks and must not form
  cycles; unknown requirements or decisions emit `workModelInconsistent`;
  status values follow the task state transition rules.
- **State transitions**: `pending` -> `in-progress` -> `done`; `pending` ->
  `skipped` only with rationale; `done` cannot be recorded without evidence.

### EvidenceDeclaration

- **Authored source**: `work/<id>/evidence.yml`
- **Fields**: `evidenceId`, `kind`, `subjectRef`, `taskRefs`,
  `requirementRefs`, `artifactRefs`, command or file reference, synthetic flag,
  deferral rationale, source location.
- **Validation**: Evidence must reference known tasks or requirements.
  Synthetic evidence must be disclosed in name or nearby rationale. Accepted
  deferrals must identify the condition for removal.

## Generated Views

### GenerationManifest

- **Fields**: `viewRef`, `schemaVersion`, `generatorId`, `generatorVersion`,
  source artifact refs and digests, output digest, generated diagnostics.
- **Validation**: If any source digest, schema version, generator version, or
  output digest differs from the current state, emit `staleGeneratedView`.

### WorkModel

- **Generated view**: `readiness/<id>/work-model.json`
- **Fields**: model version, project config summary, work item, source
  artifacts, requirements, decisions, tasks, evidence declarations, generation
  manifests, diagnostics, Governance boundary refs.
- **Validation**: Byte-stable serialization order; structured graph data wins
  executable lifecycle decisions; prose conflicts stay visible as diagnostics.

### Diagnostic

- **Fields**: `diagnosticId`, `severity`, `message`, `correction`,
  `artifactRef`, optional source location, related ids.
- **Validation**: Stable ids are required for missing artifacts, malformed
  schema versions, duplicate ids, unknown references, stale views, and
  prose/structured mismatches.
- **Relationships**: Appears in work model, analysis, verify, ship, generated
  view manifests, tests, and fixture expectations.

### LifecycleRuleContract

- **Fields**: `ruleId`, `schemaVersion`, `owner`, input artifact refs,
  finding shape, diagnostic ids, expected evidence, test obligations,
  optional Governance compatibility fields.
- **Validation**: Contracts cannot include route selection, profile adjustment,
  freshness evaluation, or protected-boundary enforcement semantics.

## Fixture Manifest

### FixtureCase

- **Fields**: `fixtureId`, `purpose`, source directory, expected diagnostics,
  expected blocking status, expected output digests where applicable.
- **Validation**: Fixture ids are stable; expected diagnostic ordering is
  deterministic.
- **Required cases**: Valid work item, malformed schema version, duplicate
  identifiers, unknown reference, prose/structured mismatch, stale generated
  view, deterministic ordering.
