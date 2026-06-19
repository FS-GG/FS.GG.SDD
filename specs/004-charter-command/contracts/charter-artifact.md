# Contract: Charter Artifact

## Scope

`work/<id>/charter.md` is the authored source created or safely updated by
`fsgg-sdd charter`. It records lifecycle principles and boundaries before
specification begins. Markdown is the authoring surface; YAML front matter is
the structured machine contract.

## Path

```text
work/<work-id>/charter.md
```

Validation:

- `<work-id>` must match the selected command work id.
- Paths in reports are repository-relative and use `/`.
- The command creates the `work/<work-id>/` directory only after the project
  context and work id are valid.

## Front Matter

Required shape:

```yaml
---
schemaVersion: 1
workId: 004-charter-command
title: Charter Command
stage: charter
changeTier: tier1
status: draft
policyPointers: []
---
```

Validation:

- `schemaVersion` is classified by the standard schema-version compatibility
  rules.
- `workId` must equal the selected command work id.
- `stage` must be `charter`.
- `changeTier` must be present; this feature accepts the existing tier strings
  used by the artifact model and specifications.
- `status` must be present and deterministic.
- `policyPointers` is optional; when present it is a list of references only.
  The command does not evaluate Governance policy.

## Body Sections

Required standard section order:

```text
# <title> Charter

## Identity
## Principles
## Scope Boundaries
## Policy Pointers
## Lifecycle Notes
```

Validation:

- A newly created charter includes all standard sections.
- Existing authored prose is preserved.
- Missing standard sections may be appended or inserted deterministically only
  when front matter is valid and no conflicting structure is detected.
- The command must not rewrite user-authored section bodies to match a
  template.
- Conflicting or malformed section structure produces diagnostics instead of
  destructive writes.

## Default Content Rules

- New charter files use LF line endings.
- Default prose is minimal and does not include timestamps, absolute host paths,
  user names, process ids, or machine-specific facts.
- Policy pointer text states optional compatibility only; it does not claim
  route, freshness, profile, gate, release, or protected-boundary enforcement.

## Safe Rerun Rules

Allowed operations:

- Create a new charter when no file exists.
- Report `noChange` when the existing file already matches the proposed
  deterministic result.
- Preserve existing prose and report `preserve` when no safe update is needed.
- Add missing standard sections when this can be proven non-destructive.

Blocking operations:

- Existing front matter work id differs from the selected work id.
- Existing front matter stage is not `charter`.
- Existing front matter is missing, empty, or malformed.
- Existing content would require rewriting authored prose.
- Existing path is a directory or otherwise cannot be read as a charter file.

## Relationship To Generated Views

The charter source contributes to generated-view planning for
`readiness/<id>/work-model.json`. A current generated view may be written only
when source data is valid enough for the artifact model. Otherwise the command
report records generated-view currency as `missing`, `stale`, `malformed`, or
`blocked` and names the source artifact to fix.
