# Research: Clarify Command

## Decision: Implement `clarify` As A Narrow Extension Of The Existing Command Layer

The feature extends `FS.GG.SDD.Commands` and `FS.GG.SDD.Cli` rather than adding
a new command project or replacing the current workflow.

**Rationale**: The command layer already defines command identity, requests,
effects, reports, deterministic serialization, text projection, dry-run
execution, and a thin CLI. `Clarify` is already part of the public command
identity and lifecycle next-action chain but currently has no command behavior.
Reusing the same command layer keeps command behavior, tests, and reports
consistent with `init`, `charter`, and `specify`.

**Alternatives considered**:

- Add a dedicated clarify project. Rejected because it would duplicate command
  workflow, serialization, rendering, and interpreter contracts.
- Generate clarifications directly from the CLI host. Rejected because
  filesystem behavior would bypass the existing MVU/effect boundary and make
  semantic tests weaker.

## Decision: Require A Valid Specification Before Writing `clarifications.md`

`fsgg-sdd clarify` validates `.fsgg/` project settings, the selected work id,
and `work/<id>/spec.md` before planning clarification writes. The
specification front matter must match the selected work id and stage
`specify`; parsed specification facts provide the source ambiguity,
requirement, story, acceptance-scenario, and scope ids that clarification
answers may reference.

**Rationale**: `clarify` is the lifecycle step after `specify`. Clarification
answers are meaningful only when they are tied to a durable specification
source with stable ids and explicit ambiguity records.

**Alternatives considered**:

- Allow clarification creation without a specification. Rejected because it
  violates the declared lifecycle order and would create decisions without a
  source fact to trace.
- Infer specification state only from generated work-model JSON. Rejected
  because generated views are outputs and may be stale, missing, or malformed.

## Decision: Make `work/<id>/clarifications.md` The Authored Clarification Source

The clarification artifact uses YAML front matter followed by authored Markdown
sections. Required front matter fields are `schemaVersion`, `workId`, `title`,
`stage: clarify`, `status`, `sourceSpec`, and `changeTier`. The body contains
source specification context, clarification questions, answers, durable
decisions, accepted deferrals, remaining ambiguity, and lifecycle notes.

**Rationale**: The initial source model reserves
`work/<id>/clarifications.md` for clarification answers. The constitution
requires schema-versioned structured artifacts as the machine contract while
preserving Markdown as the authoring surface.

**Alternatives considered**:

- Store clarification state only in command report JSON. Rejected because
  reruns and later lifecycle commands need an authored source of truth on disk.
- Store clarification state only in a separate YAML file. Rejected because SDD
  uses Markdown as the user-facing authoring surface and front matter is enough
  for identity and lifecycle state.

## Decision: Use Stable Question IDs And Existing Decision IDs

The implementation should add a narrow `ClarificationQuestionId` contract with
`CQ-###` values. Durable decisions and accepted deferrals use the existing
`DecisionId` contract with `DEC-###` values and a decision kind that
distinguishes concrete decisions from accepted deferrals.

**Rationale**: The feature spec requires stable identifiers for clarification
questions and decisions. Existing `DecisionId` already represents durable
choices consumed by later plans, tasks, diagnostics, and generated views. A
separate question id avoids overloading ambiguity ids or decision ids for
user-correctable questions.

**Alternatives considered**:

- Reuse source ambiguity ids as question ids. Rejected because one ambiguity
  can produce multiple clarification questions, and some questions can be
  derived from missing lifecycle facts rather than an explicit `AMB-###`.
- Create a separate deferral id family. Rejected for this feature because an
  accepted deferral is still a durable decision; a decision kind captures the
  difference without adding another public id family.

## Decision: Preserve Existing Answers And Decision Identifiers On Rerun

Reruns preserve existing clarification prose, answers, decisions, accepted
deferrals, and stable ids. The command may add missing standard sections,
append new questions, add new answers, or add new decisions only when the
existing front matter matches the selected work id and the update does not
remove, rewrite, renumber, or semantically change existing decisions.

**Rationale**: Clarification decisions shape later checklist, plan, task, and
evidence artifacts. Non-destructive reruns make human and agent refinement
predictable and prevent downstream references from silently changing meaning.

**Alternatives considered**:

- Always overwrite the clarification artifact from a template. Rejected
  because it violates authored-source preservation and stable-id requirements.
- Never update existing clarification artifacts. Rejected because the spec
  requires safe completion of missing standard sections, missing identifiers,
  and compatible additional answers.

## Decision: Treat Command Report JSON As The Immediate Automation Contract

Clarify command reports record the selected work id, specification prerequisite
state, changed/preserved/refused artifacts, parsed clarification facts,
remaining ambiguity state, generated-view state, diagnostics, optional
Governance compatibility facts, outcome, and `checklist` as the next lifecycle
action after success.

**Rationale**: The command feature made reports authoritative and text output a
projection. Keeping that rule avoids separate human, agent, and CI truth
sources.

**Alternatives considered**:

- Make text output the primary clarify result. Rejected because automation
  needs deterministic JSON and text wrapping/styling must not affect truth.
- Make generated work-model JSON the only success signal. Rejected because a
  clarified work item may still lack checklist, plan, tasks, or evidence, so
  the command report must explain partial generated-view state.

## Decision: Refresh Work-Model Views Only When Source Data Is Sufficient

The clarify command attempts generated-view planning for
`readiness/<id>/work-model.json`. If project and work-item sources are valid
enough, the view is refreshed with source identities, generator version, and
output digest. If required inputs are missing, stale, malformed, or unsafe, the
report records `missing`, `stale`, `malformed`, or `blocked` generated-view
state and names the source artifact to correct.

**Rationale**: Generated views are outputs; file presence is not proof of
currency. At the clarify stage, later lifecycle artifacts may not exist yet, so
the command must report that lifecycle state honestly instead of faking a
current work model.

**Alternatives considered**:

- Always write `work-model.json` after clarification creation. Rejected
  because it could make incomplete lifecycle state look current.
- Skip generated-view reporting for clarify. Rejected because the feature spec
  requires refresh or explicit diagnostics when clarification sources affect
  generated lifecycle state.

## Decision: Keep Governance Integration Advisory And Optional

The clarify command may expose optional paths such as `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` as compatibility facts, but
does not parse their schemas or evaluate route, evidence freshness, profiles,
gates, protected-boundary verdicts, or release policy.

**Rationale**: FS.GG.SDD remains the lifecycle product. Governance owns rule
evaluation and enforcement. The clarify command must be usable without
Governance installed.

**Alternatives considered**:

- Validate Governance policy during clarification. Rejected because this would
  move Governance-owned semantics into SDD.
- Ignore Governance pointers completely. Rejected because optional
  compatibility facts help future Governance consumers without enforcing
  policy.

## Decision: Use Focused Clarify Fixtures Under `tests/fixtures/lifecycle-commands/`

The implementation phase should add or expand fixtures for clarification
creation, safe reruns, safe section additions, stable id preservation,
accepted deferrals, missing specification prerequisite, missing answers,
malformed work ids, malformed clarification data, duplicate ids, unknown
references, unsafe decision changes, stale generated views, deterministic
reports, text projection, dry-run behavior, and Governance boundaries.

**Rationale**: Existing lifecycle-command fixtures already reserve this corpus.
Real filesystem-style fixtures make command behavior observable without hiding
artifact layout or generated-view currency behind mocks.

**Alternatives considered**:

- Test clarify behavior only with in-memory strings. Rejected because safe
  writes, path normalization, prerequisite files, and generated-view currency
  are filesystem-facing contracts.
- Put clarify fixtures in a new fixture root. Rejected because the command
  test suite already uses `tests/fixtures/lifecycle-commands/`.
