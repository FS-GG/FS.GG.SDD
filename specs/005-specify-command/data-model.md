# Data Model: Specify Command

## SpecifyCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd specify`
- **Fields**: command, project root token, selected work id, optional title,
  specification input text, output format, dry-run flag, overwrite policy,
  generator version
- **Validation**: Command is `specify`; work id is required and must satisfy
  the existing `WorkId` contract; new specifications require enough
  specification intent; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths
  are excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `SpecifyCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before specification writes are planned; malformed
  or missing project settings block the command.
- **Relationships**: Supplies the target charter path, specification path,
  readiness path, optional Governance compatibility facts, and generated-view
  policy.

## CharteredWorkItem

- **Source**: Selected work id plus `work/<id>/charter.md` and existing
  work-item source snapshots
- **Fields**: work id, normalized work root, charter path, specification path,
  readiness path, charter status, existing lifecycle sources, duplicate
  logical id candidates
- **Validation**: Work id must match the selected path and charter front
  matter; charter must have `stage: charter`; duplicate logical ids or
  selected-id mismatches block before writes.
- **State transitions**: `chartered` -> `specified` after successful
  specification create or safe update; blocked states keep the prior
  filesystem state unchanged.
- **Relationships**: Owns the `SpecificationArtifact`, generated work-model
  state, report context, and next action.

## SpecificationIntent

- **Source**: `--input` text and optional `--title` value
- **Fields**: title, user value, scope statements, non-goals, candidate
  stories, candidate requirements, candidate acceptance scenarios, candidate
  ambiguity records, public or tool-facing impact notes
- **Validation**: New specifications require non-empty user value, at least one
  in-scope statement, and at least one measurable requirement candidate; blank
  or prose-only input that cannot seed those facts blocks with a missing-intent
  diagnostic.
- **Relationships**: Seeds a new `SpecificationArtifact` or proposes safe
  additions to an existing one.

## SpecificationArtifact

- **Authored source**: `work/<id>/spec.md`
- **Fields**: structured front matter, user value, scope boundaries,
  non-goals, user stories, acceptance scenarios, functional requirements,
  ambiguity records, public or tool-facing impact, lifecycle notes, original
  prose body
- **Validation**: Front matter must include `schemaVersion`, `workId`, `title`,
  `stage: specify`, `status`, and `changeTier`; body must contain standard
  sections or be safely completable; existing user-authored prose and stable
  ids must be preserved.
- **Relationships**: Main authored output of the command; contributes work
  metadata, requirements, stories, ambiguity state, and source identity to
  generated-view refresh when enough lifecycle data exists.

## SpecificationFrontMatter

- **Source**: YAML front matter in `work/<id>/spec.md`
- **Fields**: schema version, work id, title, stage, status, change tier,
  optional public or tool-facing impact flags
- **Validation**: Schema version uses the standard schema compatibility
  classifier; work id must equal the selected work id; stage must be
  `specify`; status must reflect specified, draft-with-corrections, or an
  equivalent documented value.
- **Relationships**: Structured machine-readable specification identity for
  reruns, reports, normalized work models, and later lifecycle commands.

## SpecificationSection

- **Source**: Markdown body in `work/<id>/spec.md`
- **Fields**: section id, heading, body text, source location when available,
  standard-section flag
- **Required standard sections**: User Value, Scope, Non-Goals, User Stories,
  Acceptance Scenarios, Functional Requirements, Ambiguities, Public Or
  Tool-Facing Impact, Lifecycle Notes
- **Validation**: Missing standard sections may be appended deterministically
  when front matter is valid and no prose conflict is detected; conflicting
  section content is preserved and may produce a diagnostic instead of a write.
- **Relationships**: Used by safe rerun planning, text projection, and
  semantic parser additions.

## UserStory

- **Source**: `## User Stories` section in `work/<id>/spec.md`
- **Fields**: story id (`US-###`), title or summary, priority, rationale,
  independent test guidance, linked requirement ids, linked acceptance
  scenario ids, source location
- **Validation**: Story ids are unique within the specification and stable
  across reruns; priority values sort deterministically; duplicate or missing
  story ids block safe writes when the story is referenced.
- **Relationships**: Links user value to requirements, acceptance scenarios,
  checklist questions, and future task/evidence obligations.

## Requirement

- **Source**: `## Functional Requirements` section in `work/<id>/spec.md`
- **Fields**: requirement id (`FR-###`), text, priority, related story ids,
  related acceptance scenario ids, source location
- **Validation**: Requirement ids use the existing `RequirementId` contract and
  are unique; at least one measurable requirement is required for a successful
  new specification; existing ids are never renumbered.
- **Relationships**: Existing normalized work-model requirement contract and
  future task/evidence obligations.

## AcceptanceScenario

- **Source**: `## Acceptance Scenarios` section in `work/<id>/spec.md`
- **Fields**: acceptance scenario id (`AC-###`), related story ids, related
  requirement ids, given state, when action, then result, source location
- **Validation**: Scenario ids are unique and stable; each scenario must name
  an observable result; references to stories or requirements must point to ids
  present in the same specification or produce diagnostics.
- **Relationships**: Demonstrates stories and requirements before checklist,
  plan, tasks, and evidence stages.

## ScopeBoundary

- **Source**: `## Scope` and `## Non-Goals` sections in `work/<id>/spec.md`
- **Fields**: boundary id (`SB-###`), kind (`inScope`, `outOfScope`,
  `ownershipBoundary`), text, related requirement ids, source location
- **Validation**: Boundary ids are unique and stable; out-of-scope boundaries
  must not be converted into requirements by reruns; Governance-owned
  boundaries remain non-goals or optional compatibility facts.
- **Relationships**: Keeps SDD lifecycle authoring separate from Governance
  rule evaluation and later unrelated product behavior.

## AmbiguityRecord

- **Source**: `## Ambiguities` section in `work/<id>/spec.md`
- **Fields**: ambiguity id (`AMB-###`), question, state (`open`,
  `resolved`, `deferred`), related story ids, related requirement ids,
  correction or clarification guidance, source location
- **Validation**: Ambiguity ids are unique and stable; material uncertainty is
  recorded here instead of hidden in prose; successful specify may point to
  `clarify` with unresolved ambiguity records.
- **Relationships**: Feeds the future clarify command and diagnostics.

## SpecificationTemplate

- **Source**: Command library template constants or helper functions
- **Fields**: front matter shape, standard section headings, default prompts,
  deterministic newline policy, id allocation policy
- **Validation**: Output uses LF line endings, stable section order, no
  timestamps, no absolute paths, and no generated text that claims Governance
  enforcement.
- **Relationships**: Produces the proposed specification artifact for new
  files and safe missing-section additions.

## SpecificationWritePlan

- **Source**: Existing specification snapshot plus proposed
  `SpecificationArtifact`
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Authored content is written only for create, exact no-change,
  or proven-safe section/id additions; identity mismatch, malformed front
  matter, duplicate ids, missing required ids, ambiguous section structure, or
  unsafe overwrite produces a blocking diagnostic before a write effect.
- **Relationships**: Drives command effects and `ArtifactChange` report
  entries.

## GeneratedWorkModelState

- **Generated view**: `readiness/<id>/work-model.json`
- **Fields**: path, schema version when known, generator id/version, source
  identities, output digest when written, currency (`current`, `missing`,
  `stale`, `malformed`, `blocked`), diagnostic ids
- **Validation**: Current requires valid source digests and generator version;
  incomplete source data records `missing` or `blocked`; malformed existing
  generated JSON records `malformed`; digest or generator mismatches record
  `stale`.
- **Relationships**: Included in the command report; may produce a generated
  write effect only when currency can be proven current after refresh.

## SpecifyCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts,
  generated views, diagnostics, Governance compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random
  values, absolute host paths, or directory enumeration order appear in
  authoritative output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## SpecifyDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  charter prerequisite, charter identity mismatch, missing specification
  intent, specification identity mismatch, malformed specification front
  matter, duplicate specification id, missing required id, unsafe overwrite,
  stale generated view, malformed generated view, blocked generated-view
  refresh, optional Governance boundary issue, tool defect
- **Validation**: Diagnostics distinguish user-correctable input from tool
  defects and sort by severity, id, artifact path, location, and message.
- **Relationships**: Appears in command reports, generated-view states, tests,
  and readiness evidence.

## GovernanceCompatibilityFact

- **Source**: Optional Governance pointers in SDD-owned configuration or
  specification policy/impact notes
- **Fields**: path, relationship, required-by-SDD flag, observed state,
  diagnostic ids
- **Validation**: Missing Governance files do not block specification creation;
  malformed Governance-owned files are reported only as optional boundary
  issues unless an SDD-owned artifact explicitly requires the pointer.
- **Relationships**: Included in command reports without route, profile,
  freshness, gate, audit, or enforcement verdicts.

## SpecifyNextAction

- **Fields**: action id, command, work id, reason, required artifacts,
  blocking diagnostic ids
- **Validation**: Successful specification creation or safe update points to
  `clarify`; blocked reports point to the artifact or diagnostic that must be
  corrected.
- **Relationships**: Included in JSON reports and text projections.
