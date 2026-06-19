# Contract: Clarification Artifact

## Scope

`work/<id>/clarifications.md` is the authored source for the native SDD
clarification stage. Markdown is the authoring surface. YAML front matter,
stable question and decision ids, and the command report are the machine
contracts.

## Path

```text
work/<work-id>/clarifications.md
```

The path work id and front matter `workId` must match the selected work id used
by `fsgg-sdd clarify`.

## Front Matter

Required fields:

```yaml
---
schemaVersion: 1
workId: 006-clarify-command
title: Clarify Command
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/006-clarify-command/spec.md
---
```

Rules:

- `schemaVersion` uses the current schema compatibility classifier.
- `workId` is a valid SDD work id and equals the selected work id.
- `stage` is `clarify`.
- `changeTier` records the contracted change tier from the feature spec.
- `status` is `clarified` when all blocking ambiguity has a concrete decision
  or accepted deferral, or a documented correction state when blocking
  diagnostics remain.
- `sourceSpec` points to the selected `work/<id>/spec.md`.
- Front matter is structured data; prose headings do not override it.

## Body Shape

Required standard sections:

```markdown
# Clarify Command Clarifications

Prose status: clarified

## Source Specification

## Clarification Questions

## Answers

## Decisions

## Accepted Deferrals

## Remaining Ambiguity

## Lifecycle Notes
```

Reruns may append missing standard sections in the listed order only when the
existing front matter is valid and no existing authored content would be
rewritten.

## Stable ID Shapes

| Fact | ID shape | Required when |
|---|---|---|
| Source ambiguity | `AMB-###` | Referenced from the specification or from a clarification question. |
| Clarification question | `CQ-###` | Every user-correctable question generated or recorded by clarify. |
| Requirement | `FR-###` | A question, answer, or decision references a requirement. |
| User story | `US-###` | A question, answer, or decision references a story. |
| Acceptance scenario | `AC-###` | A question, answer, or decision references a scenario. |
| Clarification decision | `DEC-###` | Every concrete decision or accepted deferral. |

Existing ids must be preserved byte-for-byte unless the command refuses the
update. New ids are appended using the next available numeric suffix within the
same id family.

## Question Lines

Clarification questions use the `CQ-###` id family and should include source
links when known:

```markdown
- CQ-001: Resolve AMB-001 for FR-002. What behavior should checklist treat as authoritative?
```

The implementation may add richer structured parsing, but question ids must
remain visible in the authored Markdown.

## Decision Lines

Concrete decisions and accepted deferrals use the existing `DEC-###` id family:

```markdown
- DEC-001: Treat answered clarification decisions as authoritative for checklist inputs. Sources: CQ-001, AMB-001, FR-002.
- DEC-002: Accepted deferral for AMB-003 until plan. Rationale: dependency choice is not needed for checklist readiness.
```

Accepted deferrals are decisions with deferral semantics. They must remain
visible to later lifecycle stages and generated views.

## Minimum Creation Content

A new clarification artifact can be created only when the selected
specification is valid and one of these conditions is true:

- the specification has open ambiguity records;
- the user supplies clarification answers;
- no artifact exists and the work item needs a durable clarified state before
  checklist.

If blocking ambiguity remains and no answer or accepted deferral is supplied,
the command blocks with a missing-answer or unresolved-ambiguity diagnostic and
writes no clarification artifact.

## Safe Rerun Behavior

Allowed safe operations:

- create a missing `clarifications.md`;
- report no change for an already valid clarification artifact;
- append missing standard sections;
- append new questions after existing question ids;
- append new answers and decisions after existing decision ids;
- fill empty placeholder sections without removing user-authored text.

Blocked operations:

- selected work id differs from front matter work id;
- `sourceSpec` does not point to the selected specification;
- front matter is missing or malformed;
- existing question or decision ids would be removed, rewritten, or renumbered;
- duplicate question or decision ids are present;
- answer or decision references point to unknown specification or
  clarification ids;
- user-authored sections would be overwritten;
- an existing concrete decision would be semantically changed without an
  explicit replacement path.

## Generated-View Relationship

`work/<id>/clarifications.md` contributes decision facts, accepted deferrals,
remaining ambiguity, source identity, and source digest to
`readiness/<id>/work-model.json`. The command reports generated-view currency
after planning the clarification change. Missing checklist, plan, tasks, or
evidence may keep the work model missing or blocked at this lifecycle stage.

## Explicit Non-Responsibilities

This artifact does not define:

- requirements-quality checklist output;
- technical plan decisions beyond clarification decisions;
- task graph state;
- evidence declarations;
- verify or ship readiness verdicts;
- generated agent command files;
- Governance route, freshness, profile, gate, protected-boundary, audit, or
  release semantics.
