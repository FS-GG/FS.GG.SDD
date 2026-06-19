# Contract: Plan Artifact

## Purpose

`work/<id>/plan.md` is the authored planning source for one SDD work item. It
turns checklist-ready source facts into durable plan decisions, contract
references, verification obligations, migration posture, generated-view impact,
accepted deferrals, and lifecycle notes for later task generation and
readiness stages.

Markdown is the authoring surface. Structured front matter, stable ids, source
snapshots, and standard section records are the machine contract.

## Location

```text
work/<work-id>/plan.md
```

The path is resolved from the configured SDD work root. Reports use
project-relative paths only.

## Front Matter

Required fields:

```yaml
---
schemaVersion: 1
workId: 008-plan-command
title: Plan Command
stage: plan
status: planned
changeTier: Tier 1
sourceSpec: work/008-plan-command/spec.md
sourceClarifications: work/008-plan-command/clarifications.md
sourceChecklist: work/008-plan-command/checklist.md
publicOrToolFacingImpact: true
---
```

Field rules:

- `schemaVersion` MUST be integer `1` for this feature.
- `workId` MUST equal the selected work id.
- `stage` MUST be `plan`.
- `status` MUST be one of `planned`, `needsCorrection`, `needsReview`, or
  `blocked`.
- `sourceSpec`, `sourceClarifications`, and `sourceChecklist` MUST point to
  the selected work item's prerequisite artifacts.
- `changeTier` MUST remain visible because Tier 1 work drives public surface,
  schema, report, generated-view, and test obligations.
- `publicOrToolFacingImpact` is optional but MUST be preserved if present.

## Standard Sections

The command creates or safely completes these sections in order:

1. `## Source Snapshot`
2. `## Plan Scope`
3. `## Plan Decisions`
4. `## Contract Impact`
5. `## Verification Obligations`
6. `## Migration Posture`
7. `## Generated View Impact`
8. `## Accepted Deferrals`
9. `## Planning Findings`
10. `## Advisory Notes`
11. `## Lifecycle Notes`

The command MUST preserve user-authored prose in existing sections. Missing
standard sections may be added only when the existing artifact identity is
valid and the insertion is non-destructive.

## Stable Id Families

| Kind | Format | Purpose |
|---|---|---|
| Plan decision | `PD-###` | Durable planning choice or accepted deferral disposition |
| Contract reference | `PC-###` | Public, tool-facing, or generated artifact contract impact |
| Verification obligation | `VO-###` | Evidence obligation for later tasks, verify, or ship |
| Migration note | `PM-###` | Schema, command, or artifact migration posture |
| Generated-view impact | `GV-###` | Generated view affected by plan or source changes |

Ids MUST be unique within the plan artifact, stable across reruns, and sorted
deterministically in generated sections and reports.

## Source Snapshot Records

Each source snapshot records:

- source label;
- project-relative source path;
- source digest when known;
- schema version when known;
- referenced ids;
- snapshot status.

Source snapshots MUST cover the selected specification, clarification,
checklist, and existing plan when present. Changed source digests mark related
plan decisions stale or needing review unless the command can prove the change
does not affect that decision.

## Plan Decision Records

Each plan decision records:

- `PD-###` id;
- title;
- status: `complete`, `incomplete`, `acceptedDeferral`, `stale`, or
  `advisory`;
- decision text;
- rationale when known;
- related requirement, acceptance scenario, clarification decision, checklist
  result, accepted deferral, contract, and verification obligation ids;
- source snapshot reference.

Blocking decisions with `incomplete` or `stale` status prevent planned state.

## Contract Reference Records

Each contract reference records:

- `PC-###` id;
- contract kind;
- artifact path or logical surface;
- related source ids and plan decision ids;
- compatibility impact;
- related migration note id when applicable.

Public or tool-facing impacts MUST have either a compatibility-preserving
disposition or a migration note.

## Verification Obligation Records

Each verification obligation records:

- `VO-###` id;
- title;
- required evidence kind;
- related requirement ids, plan decision ids, and contract ids;
- synthetic evidence policy;
- acceptance threshold.

Every blocking requirement, accepted deferral, and public or tool-facing
contract impact MUST have a verification obligation or explicit accepted
deferral.

## Migration Posture Records

Each migration note records:

- `PM-###` id;
- affected schema, command surface, report, fixture, or generated view;
- posture: `none`, `diagnoseOnly`, `compatible`, or `breaking`;
- migration action;
- related contract and plan decision ids.

Breaking or tool-facing changes require explicit compatibility notes and
verification obligations.

## Generated View Impact Records

Each generated-view impact records:

- `GV-###` id;
- generated view path or kind;
- source artifacts and ids;
- expected currency behavior;
- stale diagnostic id;
- related plan decision ids.

Generated views remain outputs. The plan may describe refresh behavior but MUST
NOT treat existing generated files as proof of currency.

## Safe Rerun Rules

The command MAY write when it is creating a new plan, preserving exact content,
adding missing standard sections, adding new source-derived entries, or marking
affected decisions stale.

The command MUST refuse to write when it detects:

- selected work id mismatch;
- malformed or unsupported plan schema;
- duplicate plan ids;
- unknown source references;
- unsafe removal or renumbering;
- semantic change to an existing decision without an accepted replacement path;
- ambiguous section structure that prevents non-destructive insertion.

## Generated View Relationship

The plan artifact contributes source identity and digest facts to
`readiness/<id>/work-model.json`. The command refreshes that view when all
required source facts are valid, or reports a generated-view diagnostic when
refresh is missing, stale, malformed, or blocked.
