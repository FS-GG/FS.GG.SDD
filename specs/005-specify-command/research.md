# Research: Specify Command

## Decision: Implement `specify` As A Narrow Extension Of The Existing Command Layer

The feature extends `FS.GG.SDD.Commands` and `FS.GG.SDD.Cli` rather than adding
a new command project or replacing the current workflow.

**Rationale**: The command layer already defines command identity, requests,
effects, reports, deterministic serialization, text projection, dry-run
execution, and a thin CLI. `Specify` is already part of the public command
identity but currently blocks as unsupported. Reusing the same command layer
keeps command behavior, tests, and reports consistent with `init` and
`charter`.

**Alternatives considered**:

- Add a dedicated specify project. Rejected because it would duplicate command
  workflow, serialization, rendering, and interpreter contracts.
- Generate specifications directly from the CLI host. Rejected because
  filesystem behavior would bypass the existing MVU/effect boundary and make
  semantic tests weaker.

## Decision: Require A Valid Charter Before Writing `spec.md`

`fsgg-sdd specify` validates `.fsgg/` project settings, the selected work id,
and `work/<id>/charter.md` before planning specification writes. The charter
front matter must match the selected work id and stage `charter`.

**Rationale**: `specify` is the lifecycle step after `charter`. The charter
records the local principles and scope boundaries that keep specification
intent from drifting into unrelated SDD or Governance work.

**Alternatives considered**:

- Allow specification creation without charter. Rejected because it violates
  the declared native lifecycle order and makes the charter command optional by
  accident.
- Infer charter state only from generated work-model JSON. Rejected because
  generated views are outputs and may be stale, missing, or malformed.

## Decision: Make `work/<id>/spec.md` The Authored Specification Source With Structured Front Matter

The specification artifact uses YAML front matter followed by authored
Markdown sections. Required front matter fields are `schemaVersion`, `workId`,
`title`, `stage: specify`, `status: specified`, and `changeTier`. The body
contains user value, scope, non-goals, stories, requirements, acceptance
scenarios, ambiguity records, public/tool-facing impact, and lifecycle notes.

**Rationale**: The constitution requires schema-versioned structured artifacts
as the machine contract while preserving Markdown as the authoring surface.
The existing artifact model already parses `work/<id>/spec.md` front matter
and `- FR-###:` requirement lines into the normalized work model.

**Alternatives considered**:

- Store specification state only in command report JSON. Rejected because
  reruns and later lifecycle commands need an authored source of truth on disk.
- Store specification state only in a separate YAML file. Rejected because SDD
  uses Markdown as the user-facing authoring surface and front matter is enough
  for identity and lifecycle state.

## Decision: Add Stable Specification IDs For Stories, Acceptance Scenarios, Scope Boundaries, And Ambiguities

The implementation should add narrow typed ids for specification concepts that
are not currently represented by the artifact model: `US-###`, `AC-###`,
`SB-###`, and `AMB-###`. Requirement ids continue to use the existing
`FR-###` contract.

**Rationale**: The feature spec requires stable identifiers for stories,
requirements, acceptance scenarios, ambiguity records, and relevant references.
Later clarify, checklist, plan, tasks, evidence, and diagnostics need to point
to the same facts without parsing prose-only labels.

**Alternatives considered**:

- Keep stories and ambiguity records as prose-only bullets. Rejected because
  diagnostics and later lifecycle stages would lack stable references.
- Reuse requirement ids for every specification fact. Rejected because stories,
  acceptance scenarios, scope boundaries, and ambiguities have different
  lifecycle meanings and validation rules.

## Decision: Preserve Existing Specification Prose And Stable IDs

Reruns preserve existing specification content and typed ids. The command may
add missing standard sections or append new facts only when the existing front
matter matches the selected work id and the update does not remove, rewrite,
or renumber existing user-authored content. Identity mismatches, duplicate ids,
malformed front matter, missing required identifiers, unsafe overwrite markers,
or ambiguous section structure block before write effects.

**Rationale**: A specification is an authored source and the contract for later
lifecycle stages. Non-destructive reruns make agent and human refinement
predictable.

**Alternatives considered**:

- Always overwrite the specification from a template. Rejected because it
  violates authored-source preservation and stable-id requirements.
- Never update existing specifications. Rejected because the spec requires safe
  completion of missing standard sections or identifiers.

## Decision: Treat Command Report JSON As The Immediate Automation Contract

Specify command reports record the selected work id, charter prerequisite
state, changed/preserved/refused artifacts, parsed specification facts,
generated-view state, diagnostics, optional Governance compatibility facts,
outcome, and `clarify` as the next lifecycle action after success.

**Rationale**: The command feature made reports authoritative and text output a
projection. Keeping that rule avoids separate human, agent, and CI truth
sources.

**Alternatives considered**:

- Make text output the primary specify result. Rejected because automation
  needs deterministic JSON and text wrapping/styling must not affect truth.
- Make generated work-model JSON the only success signal. Rejected because a
  newly specified work item may not yet have tasks or evidence, so the command
  report must explain partial generated-view state.

## Decision: Refresh Work-Model Views Only When Source Data Is Sufficient

The specify command attempts generated-view planning for
`readiness/<id>/work-model.json`. If project and work-item sources are valid
enough, the view is refreshed with source identities, generator version, and
output digest. If required inputs are missing, stale, malformed, or unsafe, the
report records `missing`, `stale`, `malformed`, or `blocked` generated-view
state and names the source artifact to correct.

**Rationale**: Generated views are outputs; file presence is not proof of
currency. At the specify stage, tasks and evidence may not exist yet, so the
command must report that lifecycle state honestly instead of faking a current
work model.

**Alternatives considered**:

- Always write `work-model.json` after specification creation. Rejected because
  it could make incomplete lifecycle state look current.
- Skip generated-view reporting for specify. Rejected because the feature spec
  requires refresh or explicit diagnostics when specification sources affect
  generated lifecycle state.

## Decision: Keep Governance Integration Advisory And Optional

The specify command may expose optional paths such as `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` as compatibility facts, but
does not parse their schemas or evaluate route, evidence freshness, profiles,
gates, protected-boundary verdicts, or release policy.

**Rationale**: FS.GG.SDD remains the lifecycle product. Governance owns rule
evaluation and enforcement. The specify command must be usable without
Governance installed.

**Alternatives considered**:

- Validate Governance policy during specification creation. Rejected because
  this would move Governance-owned semantics into SDD.
- Ignore Governance pointers completely. Rejected because optional
  compatibility facts help future Governance consumers without enforcing
  policy.

## Decision: Use Focused Specify Fixtures Under `tests/fixtures/lifecycle-commands/`

The implementation phase should add or expand fixtures for specification
creation, safe reruns, safe section additions, stable id preservation, missing
charter prerequisite, missing intent, malformed work ids, malformed
specification data, duplicate ids, unsafe overwrites, stale generated views,
deterministic reports, text projection, dry-run behavior, and Governance
boundaries.

**Rationale**: Existing lifecycle-command fixtures already reserve this corpus.
Real filesystem-style fixtures make command behavior observable without hiding
artifact layout or generated-view currency behind mocks.

**Alternatives considered**:

- Test specify behavior only with in-memory strings. Rejected because safe
  writes, path normalization, prerequisite files, and generated-view currency
  are filesystem-facing contracts.
- Put specify fixtures in a new fixture root. Rejected because the command
  test suite already uses `tests/fixtures/lifecycle-commands/`.
