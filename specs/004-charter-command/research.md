# Research: Charter Command

## Decision: Implement `charter` As A Narrow Extension Of The Existing Command Layer

The feature extends `FS.GG.SDD.Commands` and `FS.GG.SDD.Cli` rather than adding
a new command project or replacing the current workflow.

**Rationale**: The first native command slice already introduced command
identity, requests, effects, reports, deterministic serialization, text
projection, and a thin CLI. `Charter` is already part of the public command
identity but currently blocks as unsupported. Reusing the same command layer
keeps command behavior, tests, and reports consistent.

**Alternatives considered**:

- Add a dedicated charter project. Rejected because it would duplicate command
  workflow, serialization, rendering, and interpreter contracts.
- Generate charter files directly from the CLI host. Rejected because
  filesystem behavior would bypass the existing MVU/effect boundary and make
  semantic tests weaker.

## Decision: Make `work/<id>/charter.md` An Authored Markdown Source With Structured Front Matter

The charter artifact uses YAML front matter followed by authored Markdown
sections. Required front matter fields are `schemaVersion`, `workId`, `title`,
`stage`, `status`, and `changeTier`. The stage for a successful charter is
`charter`.

**Rationale**: The constitution requires schema-versioned structured artifacts
as the machine contract while preserving Markdown as the authoring surface.
Structured front matter gives commands and generated views stable lifecycle
facts without turning prose sections into a parser contract.

**Alternatives considered**:

- Store charter state only in command report JSON. Rejected because reruns and
  later lifecycle commands need an authored source of truth on disk.
- Store charter state in a separate YAML file. Rejected for this feature
  because the existing lifecycle model uses Markdown for user-authored stages
  and front matter is enough for charter identity and lifecycle state.

## Decision: Preserve Existing Charter Prose And Allow Only Proven-Safe Additions

Reruns preserve existing charter content. The command may add missing standard
sections only when the existing front matter matches the selected work id and
the update appends or inserts empty standard sections without rewriting
user-authored prose. Identity mismatches, conflicting front matter, malformed
front matter, or ambiguous section structure block before write effects.

**Rationale**: A charter records human and agent decisions. The safest default
is no destructive overwrite, with a small deterministic safe-update path for
missing standard sections.

**Alternatives considered**:

- Always overwrite the charter from a template. Rejected because it violates
  authored-source preservation.
- Never update existing charter files. Rejected because the spec requires safe
  completion of missing standard sections.

## Decision: Treat Command Report JSON As The Immediate Automation Contract

Charter command reports record the selected work id, changed/preserved/refused
artifacts, generated-view state, diagnostics, outcome, optional Governance
compatibility facts, and `specify` as the next lifecycle action after success.

**Rationale**: The existing command feature made reports authoritative and text
output a projection. Keeping that rule avoids separate human, agent, and CI
truth sources.

**Alternatives considered**:

- Make text output the primary charter result. Rejected because automation
  needs deterministic JSON and text wrapping/styling must not affect truth.
- Make generated work-model JSON the only success signal. Rejected because
  charter-only work items may not yet have enough later-stage sources for a
  current work model.

## Decision: Refresh Work-Model Views Only When Source Data Is Sufficient

The charter command attempts generated-view planning for
`readiness/<id>/work-model.json`. If project and work-item sources are valid
enough, the view is refreshed with source identities, generator version, and
output digest. If required inputs are missing, stale, malformed, or unsafe, the
report records `missing`, `stale`, `malformed`, or `blocked` generated-view
state and names the source artifact to correct.

**Rationale**: Generated views are outputs; file presence is not proof of
currency. The current artifact model still derives some work-item metadata from
later sources, so the charter slice must not fake a current work model when it
cannot prove one.

**Alternatives considered**:

- Always write `work-model.json` after charter creation. Rejected because it
  would make incomplete lifecycle state look current.
- Skip generated-view reporting for charter. Rejected because FR-012 and
  FR-013 require refresh or explicit diagnostics when charter sources affect
  lifecycle state.

## Decision: Keep Governance Integration Advisory And Optional

The charter command may expose optional paths such as `.fsgg/policy.yml`,
`.fsgg/capabilities.yml`, and `.fsgg/tooling.yml` as compatibility facts, but
does not parse their schemas or evaluate route, evidence freshness, profiles,
gates, protected-boundary verdicts, or release policy.

**Rationale**: FS.GG.SDD remains the lifecycle product. Governance owns rule
evaluation and enforcement. The charter command must be usable without
Governance installed.

**Alternatives considered**:

- Validate Governance policy during charter creation. Rejected because this
  would move Governance-owned semantics into SDD.
- Ignore Governance pointers completely. Rejected because optional compatibility
  facts help future Governance consumers without enforcing policy.

## Decision: Use Focused Charter Fixtures Under `tests/fixtures/lifecycle-commands/`

The implementation phase should add or expand fixtures for charter creation,
safe reruns, safe section additions, identity mismatch, duplicate work ids,
outside-project use, malformed work ids, malformed artifacts, unsafe
overwrites, stale generated views, deterministic reports, text projection, and
Governance boundaries.

**Rationale**: Existing lifecycle-command fixtures already reserve this corpus.
Real filesystem-style fixtures make command behavior observable without hiding
artifact layout or generated-view currency behind mocks.

**Alternatives considered**:

- Test charter behavior only with in-memory strings. Rejected because safe
  writes, path normalization, and generated-view currency are filesystem-facing
  contracts.
- Put charter fixtures in a new fixture root. Rejected because the command test
  suite already uses `tests/fixtures/lifecycle-commands/`.
