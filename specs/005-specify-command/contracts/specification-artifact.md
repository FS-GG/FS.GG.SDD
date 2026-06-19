# Contract: Specification Artifact

## Scope

`work/<id>/spec.md` is the authored source for the native SDD specification
stage. Markdown is the authoring surface. YAML front matter, stable ids, and
the command report are the machine contracts.

## Path

```text
work/<work-id>/spec.md
```

The path work id and front matter `workId` must match the selected work id used
by `fsgg-sdd specify`.

## Front Matter

Required fields:

```yaml
---
schemaVersion: 1
workId: 005-specify-command
title: Specify Command
stage: specify
changeTier: tier1
status: specified
---
```

Rules:

- `schemaVersion` uses the current schema compatibility classifier.
- `workId` is a valid SDD work id and equals the selected work id.
- `stage` is `specify`.
- `changeTier` records the contracted change tier from the feature spec.
- `status` is `specified` for successful creation or safe update, or a
  documented correction state when blocking diagnostics remain.
- Front matter is structured data; prose headings do not override it.

## Body Shape

Required standard sections:

```markdown
# Specify Command Specification

Prose status: specified

## User Value

## Scope

## Non-Goals

## User Stories

## Acceptance Scenarios

## Functional Requirements

## Ambiguities

## Public Or Tool-Facing Impact

## Lifecycle Notes
```

Reruns may append missing standard sections in the listed order only when the
existing front matter is valid and no existing authored content would be
rewritten.

## Stable ID Shapes

| Fact | ID shape | Required when |
|---|---|---|
| User story | `US-###` | The story is referenced by a requirement, acceptance scenario, ambiguity, task, or diagnostic. |
| Acceptance scenario | `AC-###` | The scenario is listed or referenced. |
| Requirement | `FR-###` | Every functional requirement. |
| Scope boundary | `SB-###` | A scope or non-goal boundary is referenced later. |
| Ambiguity record | `AMB-###` | Every material ambiguity or documented deferral. |

Existing ids must be preserved byte-for-byte unless the command refuses the
update. New ids are appended using the next available numeric suffix within the
same id family.

## Requirement Lines

Functional requirements use the existing normalized work-model convention:

```markdown
- FR-001: The specify command creates an authored specification artifact.
```

The implementation may add richer structured parsing, but it must continue to
emit `- FR-###:` lines so current requirement extraction remains compatible.

## Minimum Creation Content

A new specification can be created only when specification intent can seed:

- user value;
- at least one in-scope statement;
- at least one measurable functional requirement.

The command may accept those facts from labeled `--input` text, recognized
Markdown sections, or future library caller fields. If the input cannot seed
those facts, the command blocks with a missing-intent diagnostic and writes no
specification artifact.

## Safe Rerun Behavior

Allowed safe operations:

- create a missing `spec.md`;
- report no change for an already valid specification;
- append missing standard sections;
- append new ids and facts after existing ids;
- fill empty placeholder sections without removing user-authored text.

Blocked operations:

- selected work id differs from front matter work id;
- front matter is missing or malformed;
- existing ids would be removed, rewritten, or renumbered;
- duplicate ids are present;
- required ids are missing from referenced facts;
- user-authored sections would be overwritten;
- an unsafe-overwrite marker or equivalent conflict is present.

## Generated-View Relationship

`work/<id>/spec.md` contributes work metadata, requirements, stories, ambiguity
state, source identity, and source digest to
`readiness/<id>/work-model.json`. The command reports generated-view currency
after planning the specification change. Missing tasks or evidence may keep the
work model missing or blocked at this lifecycle stage.

## Explicit Non-Responsibilities

This artifact does not define:

- clarification answers;
- requirements-quality checklist output;
- technical plan decisions;
- task graph state;
- evidence declarations;
- verify or ship readiness verdicts;
- generated agent command files;
- Governance route, freshness, profile, gate, protected-boundary, audit, or
  release semantics.
