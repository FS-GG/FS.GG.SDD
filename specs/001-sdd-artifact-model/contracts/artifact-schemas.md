# Contract: SDD Artifact Schemas

## Scope

This contract defines the first SDD-owned structured artifact shapes. Markdown
is still the authoring surface, but these schema-versioned fields are the
machine contract consumed by SDD tools, generated views, tests, agents, and
optional Governance integrations.

All structured artifacts use:

- `schemaVersion: 1` for the initial contract.
- camelCase field names.
- repository-relative paths.
- stable ids instead of display names for references.
- explicit diagnostics for missing, malformed, unsupported, duplicate,
  unknown, stale, and conflicting data.

## `.fsgg/project.yml`

Purpose: project identity and lifecycle roots.

Required SDD-owned fields:

```yaml
schemaVersion: 1
project:
  id: fs-gg-sdd
  defaultWorkRoot: work
sdd:
  schemaVersion: 1
  config: .fsgg/sdd.yml
  agents: .fsgg/agents.yml
governance:
  policy: .fsgg/policy.yml
  capabilities: .fsgg/capabilities.yml
  tooling: .fsgg/tooling.yml
```

Validation:

- `project.id` must be a stable lowercase id.
- `project.defaultWorkRoot` must be repository-relative.
- `sdd.config` and `sdd.agents` point to SDD-owned structured artifacts.
- `governance.*` paths are optional compatibility pointers. Their presence does
  not make Governance required for SDD-only validation.

## `.fsgg/sdd.yml`

Purpose: SDD lifecycle policy, artifact layout, generated-view policy, and
schema migration posture.

Required fields:

```yaml
schemaVersion: 1
lifecycle:
  stages:
    - charter
    - specify
    - clarify
    - checklist
    - plan
    - tasks
    - analyze
    - implement
    - evidence
    - verify
    - ship
artifacts:
  workRoot: work
  readinessRoot: readiness
  specFile: spec.md
  planFile: plan.md
  tasksFile: tasks.yml
  evidenceFile: evidence.yml
generatedViews:
  requireSourceDigests: true
  requireGeneratorVersion: true
  staleBehavior: diagnostic
migrations:
  unsupportedVersionBehavior: diagnostic
```

Validation:

- Stage ids must be known lifecycle stages.
- Artifact paths must stay within the repository.
- Generated-view policy must require source digests and generator versions.
- Unsupported schema versions must emit diagnostics instead of being ignored.

## `.fsgg/agents.yml`

Purpose: agent guidance targets for Claude, Codex, and future agents.

Required fields:

```yaml
schemaVersion: 1
agents:
  - id: claude
    guidancePath: CLAUDE.md
    generatedRoot: readiness/{workId}/agent-commands/claude
  - id: codex
    guidancePath: AGENTS.md
    generatedRoot: readiness/{workId}/agent-commands/codex
sourceModel:
  workModel: readiness/{workId}/work-model.json
policy:
  generatedGuidanceIsAuthority: false
  requireEquivalentClaudeAndCodexBehavior: true
```

Validation:

- Claude and Codex guidance targets must resolve to repository-relative paths.
- Generated guidance is a projection from lifecycle data, not authority.
- If one supported agent target changes, the equivalent target must be
  reviewed or a diagnostic records the divergence.

## Work Item Front Matter

Purpose: structured work-item metadata embedded in `work/<id>/spec.md` and any
stage document that needs stage-specific structured data.

Required fields for `work/<id>/spec.md`:

```yaml
---
schemaVersion: 1
workId: 001-sdd-artifact-model
title: SDD Artifact Model
stage: specify
changeTier: tier1
status: draft
---
```

Validation:

- `workId` must match the directory id.
- `stage` must be a known lifecycle stage.
- `changeTier` must be `tier1` or `tier2`.
- Missing front matter on a structured stage emits `missingArtifact` or
  `malformedSchemaVersion` depending on the failure.

## `work/<id>/tasks.yml`

Purpose: typed implementation graph.

Required fields:

```yaml
schemaVersion: 1
tasks:
  - id: T001
    title: Add public signatures for lifecycle identifiers
    status: pending
    owner: codex
    requirements: [FR-001]
    decisions: []
    dependencies: []
    requiredSkills: [fs-gg-sdd-project]
    requiredEvidence: [EV001]
```

Validation:

- Task ids must be unique within a work item.
- Requirement and decision refs must exist.
- Dependencies must reference known tasks and must not cycle.
- `done` tasks require evidence declarations.
- Synthetic evidence must be disclosed by evidence, not hidden in task status.

## `work/<id>/evidence.yml`

Purpose: declared implementation, verification, synthetic, deferral, and
missing evidence.

Required fields:

```yaml
schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    artifacts:
      - path: tests/FS.GG.SDD.Artifacts.Tests/IdentifierTests.fs
    result: pending
    synthetic: false
```

Validation:

- Evidence ids must be unique within a work item.
- Subjects must reference known tasks, requirements, decisions, or artifacts.
- `synthetic: true` requires a rationale and must be visible in tests or
  nearby comments.
- Deferrals require a reason and a removal condition.

## Optional Governance Files

`.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` are
compatibility boundaries. SDD may reference them as optional artifacts, but this
feature does not define their schema and does not evaluate route, profile,
freshness, gate, or enforcement behavior.
