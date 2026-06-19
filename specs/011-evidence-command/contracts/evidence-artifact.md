# Contract: Evidence Artifact

## Purpose

`work/<id>/evidence.yml` is the authored evidence declaration source for one
SDD work item. It records implementation and verification support for tasks,
requirements, decisions, obligations, generated-view impacts, synthetic
evidence disclosures, accepted deferrals, and lifecycle notes.

The evidence artifact is authored lifecycle data. It is not a generated
readiness view and must not be replaced by command output, `analysis.json`, or
Governance effective-evidence facts.

## Location

```text
work/<work-id>/evidence.yml
```

The path is resolved from the configured SDD work root. Reports use
project-relative paths only.

## Top-Level Shape

Required fields for version 1:

```yaml
schemaVersion: 1
workId: 011-evidence-command
stage: evidence
status: evidenceReady
sourceSpec: work/011-evidence-command/spec.md
sourceClarifications: work/011-evidence-command/clarifications.md
sourceChecklist: work/011-evidence-command/checklist.md
sourcePlan: work/011-evidence-command/plan.md
sourceTasks: work/011-evidence-command/tasks.yml
sourceAnalysis: readiness/011-evidence-command/analysis.json
sourceSnapshots:
  - label: tasks
    path: work/011-evidence-command/tasks.yml
    digest: sha256:...
    schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: [FR-001]
    decisionRefs: []
    obligationRefs: [VO-001]
    sourceRefs:
      - kind: test-output
        path: specs/011-evidence-command/readiness/evidence-command-tests.txt
        result: pass
    result: pass
    synthetic: false
    rationale: null
    notes: []
lifecycleNotes:
  - Next lifecycle action: verify.
```

Field rules:

- `schemaVersion` MUST be integer `1` for this feature.
- `workId` MUST equal the selected work id.
- `stage` MUST be `evidence`.
- `status` MUST be `draft`, `needsEvidence`, `evidenceReady`,
  `needsCorrection`, or `blocked`.
- Source paths MUST be project-relative and must point at the selected work
  item's prerequisite lifecycle artifacts.
- `sourceSnapshots` SHOULD record digests for source artifacts whose facts are
  used to determine stale evidence.
- Evidence ids MUST be stable and unique inside the selected evidence artifact.
- Lists MUST be deterministic when the command creates or safely appends
  entries.

## Evidence Declaration

Each declaration records one durable evidence fact:

```yaml
- id: EV002
  kind: synthetic
  subject:
    type: task
    id: T004
  taskRefs: [T004]
  requirementRefs: [FR-004]
  acceptanceScenarioRefs: [US1-AS2]
  clarificationDecisionRefs: []
  checklistResultRefs: []
  planDecisionRefs: [PD-002]
  obligationRefs: [VO-004]
  sourceRefs:
    - kind: transcript
      path: specs/011-evidence-command/readiness/synthetic-fsi.txt
      result: pass
  result: pass
  synthetic: true
  syntheticDisclosure:
    standsInFor: real CI smoke transcript for a not-yet-created fixture
    reason: fixture contract is planned before implementation exists
  rationale: Synthetic evidence is accepted for planning-only examples.
  notes: []
```

Allowed declaration kinds for this feature:

- `implementation`
- `verification`
- `review`
- `generated-view`
- `deferral`
- `synthetic`
- `note`

Allowed result states for this feature:

- `pass`
- `fail`
- `deferred`
- `missing`
- `stale`
- `advisory`
- `blocked`

Synthetic declarations MUST include `syntheticDisclosure`. Deferral
declarations MUST include rationale, owner, scope, and later lifecycle
visibility either directly or through source references.

## Obligation Disposition

The command derives evidence dispositions from declarations and current
obligations. The artifact MAY include explicit disposition notes, but the
authoritative disposition is computed by the command report from current
sources.

Disposition states are:

- `supported`
- `deferred`
- `missing`
- `stale`
- `synthetic`
- `invalid`
- `advisory`
- `blocking`

Every completed task with required evidence MUST have a visible supported or
deferred disposition before the command reports evidence-ready.

## Preservation Rules

The command MUST preserve existing authored content unless a safe compatible
update is planned. It MUST refuse to write when a proposed update would:

- remove or renumber existing evidence ids;
- change an existing declaration's task links, subject, result state,
  synthetic disclosure, source reference, or deferral rationale without a safe
  replacement path;
- duplicate an evidence id;
- reference unknown tasks, requirements, decisions, obligations, or sources;
- record synthetic evidence without disclosure;
- record deferral evidence without rationale, owner, scope, or later lifecycle
  visibility.

## Determinism Rules

Command-generated evidence content and report facts MUST:

- sort new declarations by stable evidence id;
- sort source snapshots and source references by path and id;
- normalize paths to `/`;
- include digests only when derived from normalized source bytes;
- omit wall-clock timestamps and durations;
- omit terminal width and ANSI styling;
- omit absolute host paths;
- omit process ids and random values.
