# Contract: Checklist Artifact

## Scope

`work/<id>/checklist.md` is the authored source for the native SDD
requirements-quality checklist stage. Markdown is the authoring surface. YAML
front matter, stable checklist item ids, stable checklist result ids, source
links, source snapshot facts, and the command report are the machine contracts.

## Path

```text
work/<work-id>/checklist.md
```

The path work id and front matter `workId` must match the selected work id used
by `fsgg-sdd checklist`.

## Front Matter

Required fields:

```yaml
---
schemaVersion: 1
workId: 007-checklist-command
title: Checklist Command
stage: checklist
changeTier: tier1
status: checklistReady
sourceSpec: work/007-checklist-command/spec.md
sourceClarifications: work/007-checklist-command/clarifications.md
---
```

Rules:

- `schemaVersion` uses the current schema compatibility classifier.
- `workId` is a valid SDD work id and equals the selected work id.
- `stage` is `checklist`.
- `changeTier` records the contracted change tier from the feature spec.
- `status` is `checklistReady` when all blocking checklist items pass or have
  accepted deferrals visible to planning.
- `status` is `needsCorrection`, `needsReview`, `blocked`, or an equivalent
  documented value when blocking findings or stale results remain.
- `sourceSpec` points to the selected `work/<id>/spec.md`.
- `sourceClarifications` points to the selected
  `work/<id>/clarifications.md`.
- Front matter is structured data; prose headings do not override it.

## Body Shape

Required standard sections:

```markdown
# Checklist Command Checklist

Prose status: checklistReady

## Source Specification

## Source Clarifications

## Source Snapshot

## Checklist Items

## Review Results

## Accepted Deferrals

## Blocking Findings

## Advisory Notes

## Lifecycle Notes
```

Reruns may append missing standard sections in the listed order only when the
existing front matter is valid and no existing authored content would be
rewritten.

## Stable ID Shapes

| Fact | ID shape | Required when |
|---|---|---|
| Checklist item | `CHK-###` | Every generated or authored requirements-quality check. |
| Checklist result | `CR-###` | Every recorded review outcome for a checklist item. |
| Requirement | `FR-###` | A checklist item or result references a requirement. |
| User story | `US-###` | A checklist item or result references a story. |
| Acceptance scenario | `AC-###` | A checklist item or result references a scenario. |
| Scope boundary | `SB-###` | A checklist item or result references an explicit scope boundary. |
| Source ambiguity | `AMB-###` | A checklist item or result references specification ambiguity. |
| Clarification question | `CQ-###` | A checklist item or result references a clarification question. |
| Clarification decision | `DEC-###` | A checklist item or result references a clarification decision or accepted deferral. |

Existing ids must be preserved byte-for-byte unless the command refuses the
update. New ids are appended using the next available numeric suffix within the
same id family.

## Source Snapshot Lines

Source snapshots record the source artifacts that checklist results were
reviewed against:

```markdown
- spec: work/007-checklist-command/spec.md sha256:<lowercase-sha256> schemaVersion:1
- clarifications: work/007-checklist-command/clarifications.md sha256:<lowercase-sha256> schemaVersion:1
```

If a source snapshot no longer matches the current parsed source facts, related
review results are stale or need review and cannot satisfy checklist-ready
state until updated.

## Checklist Item Lines

Checklist items use the `CHK-###` id family and include source links when
known:

```markdown
- CHK-001 [FR-001] blocking: Requirement must be testable and linked to acceptance coverage.
- CHK-002 [DEC-001] advisory: Accepted deferral remains visible to planning.
```

The implementation may add richer structured parsing, but checklist item ids
and source links must remain visible in the authored Markdown.

## Review Result Lines

Review results use the `CR-###` id family and link to checklist items:

```markdown
- CR-001 [CHK:CHK-001] pass: Requirement is testable and has acceptance coverage.
- CR-002 [CHK:CHK-003] fail: Requirement FR-004 has no acceptance scenario. Correction: add an acceptance scenario or narrow the requirement.
- CR-003 [CHK:CHK-006] acceptedDeferral [DEC-002]: Deferral is explicit and remains visible to plan.
- CR-004 [CHK:CHK-002] stale: Source specification changed since this result was recorded.
```

Failed blocking results and stale results prevent checklist-ready state.
Accepted deferrals are allowed only when the deferral is explicit, has
rationale, and remains visible to planning.

## Minimum Creation Content

A new checklist artifact can be created only when the selected specification
and clarification artifacts are valid enough to identify source facts and one
of these conditions is true:

- no checklist artifact exists;
- requirements-quality review produces passed, failed, advisory, stale, or
  accepted-deferral results;
- existing checklist state needs safe missing-section, missing-item, or
  stale-result updates.

Failed requirements-quality checks create or safely update the checklist
artifact with failed results and correction guidance. They do not advance next
action to `plan`.

If the specification prerequisite, clarification prerequisite, selected work
identity, source ids, or existing checklist data are malformed, the command
blocks with diagnostics and writes no checklist artifact.

## Safe Rerun Behavior

Allowed safe operations:

- create a missing `checklist.md`;
- report no change for an already valid checklist artifact;
- append missing standard sections;
- append new checklist items after existing item ids;
- append new review results after existing result ids;
- mark existing results stale when referenced source facts changed;
- add failed checklist results and correction guidance from current source
  facts;
- fill empty placeholder sections without removing user-authored text.

Blocked operations:

- selected work id differs from front matter work id;
- `sourceSpec` does not point to the selected specification;
- `sourceClarifications` does not point to the selected clarification artifact;
- front matter is missing or malformed;
- existing checklist item or result ids would be removed, rewritten, or
  renumbered;
- duplicate checklist item or result ids are present;
- checklist item or result references point to unknown specification or
  clarification ids;
- user-authored sections would be overwritten;
- an existing review result would be semantically changed without an explicit
  stale or replacement path.

## Generated-View Relationship

`work/<id>/checklist.md` contributes checklist readiness facts, failed and
stale result counts, accepted deferrals, blocking findings, source identity,
and source digest to `readiness/<id>/work-model.json`. The command reports
generated-view currency after planning the checklist change. Missing plan,
tasks, or evidence may keep the work model missing or blocked at this
lifecycle stage.

## Explicit Non-Responsibilities

This artifact does not define:

- technical plan decisions;
- task graph state;
- evidence declarations;
- verify or ship readiness verdicts;
- generated agent command files;
- Governance route, freshness, profile, gate, protected-boundary, audit, or
  release semantics.
