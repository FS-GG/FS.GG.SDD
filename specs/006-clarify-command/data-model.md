# Data Model: Clarify Command

## ClarifyCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd clarify`
- **Fields**: command, project root token, selected work id, clarification
  answer input text, output format, dry-run flag, overwrite policy, generator
  version
- **Validation**: Command is `clarify`; work id is required and must satisfy
  the existing `WorkId` contract; unresolved blocking ambiguity requires
  enough answer input to resolve, defer, or explicitly leave questions open;
  project root is normalized for reports; behavior-affecting options are
  recorded in the report; absolute host paths are excluded from authoritative
  content.
- **Relationships**: Starts the command workflow and appears in the
  `ClarifyCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before clarification writes are planned; malformed
  or missing project settings block the command.
- **Relationships**: Supplies the target specification path, clarification
  path, readiness path, optional Governance compatibility facts, and
  generated-view policy.

## SpecifiedWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`, existing
  `work/<id>/clarifications.md`, and existing work-item source snapshots
- **Fields**: work id, normalized work root, specification path,
  clarification path, readiness path, specification status, existing
  lifecycle sources, duplicate logical id candidates
- **Validation**: Work id must match the selected path and specification front
  matter; specification must have `stage: specify`; duplicate logical ids or
  selected-id mismatches block before writes.
- **State transitions**: `specified` -> `clarified` when all blocking
  ambiguity has a concrete decision or accepted deferral; blocked states keep
  the prior filesystem state unchanged.
- **Relationships**: Owns the `SpecificationFacts`, `ClarificationArtifact`,
  generated work-model state, report context, and next action.

## SpecificationFacts

- **Source**: Parsed `work/<id>/spec.md`
- **Fields**: specification front matter, user story ids, requirement ids,
  acceptance scenario ids, scope boundary ids, ambiguity ids, unresolved
  ambiguity count, requirement and scenario references, source identity,
  diagnostics
- **Validation**: Front matter must match the selected work id and stage
  `specify`; ids must be unique and valid; references used by clarification
  answers must point to ids present in the selected specification.
- **Relationships**: Source context for clarification questions, answers,
  decisions, accepted deferrals, generated work-model state, and diagnostics.

## ClarificationIntent

- **Source**: `--input` text or library caller answer fields
- **Fields**: answer text, referenced ambiguity ids, referenced question ids,
  referenced requirement/story/acceptance/scenario ids, proposed decision text,
  proposed accepted-deferral text, still-open note text
- **Validation**: Answer references must target known source ambiguity or
  existing clarification question ids; concrete decisions require decision
  text; accepted deferrals require rationale and downstream visibility; blank
  input blocks only when unresolved blocking ambiguity remains.
- **Relationships**: Seeds a new `ClarificationArtifact` or proposes safe
  additions to an existing one.

## ClarificationArtifact

- **Authored source**: `work/<id>/clarifications.md`
- **Fields**: structured front matter, source specification relationship,
  clarification questions, answers, decisions, accepted deferrals, remaining
  ambiguity, lifecycle notes, original prose body
- **Validation**: Front matter must include `schemaVersion`, `workId`, `title`,
  `stage: clarify`, `status`, `sourceSpec`, and `changeTier`; body must
  contain standard sections or be safely completable; existing user-authored
  prose, answers, decisions, accepted deferrals, and stable ids must be
  preserved.
- **Relationships**: Main authored output of the command; contributes
  decision facts, remaining ambiguity, source identity, and source digest to
  generated-view refresh when enough lifecycle data exists.

## ClarificationFrontMatter

- **Source**: YAML front matter in `work/<id>/clarifications.md`
- **Fields**: schema version, work id, title, stage, status, source
  specification path, change tier, optional public or tool-facing impact flags
- **Validation**: Schema version uses the standard schema compatibility
  classifier; work id must equal the selected work id; stage must be
  `clarify`; source specification path must point to the selected
  `work/<id>/spec.md`; status must reflect clarified, needs answers,
  blocked, or an equivalent documented value.
- **Relationships**: Structured machine-readable clarification identity for
  reruns, reports, normalized work models, and later lifecycle commands.

## ClarificationQuestion

- **Source**: `## Clarification Questions` section in
  `work/<id>/clarifications.md` or generated from open specification ambiguity
- **Fields**: question id (`CQ-###`), prompt, source ambiguity ids, related
  requirement ids, related story ids, related acceptance scenario ids, blocking
  flag, state (`open`, `answered`, `deferred`), source location
- **Validation**: Question ids are unique and stable across reruns; source ids
  must exist in the selected specification when present; blocking questions
  require a concrete decision or accepted deferral before the work item is
  clarified.
- **Relationships**: Connects source ambiguity to answers, decisions,
  remaining ambiguity, diagnostics, and text projection.

## ClarificationAnswer

- **Source**: `## Answers` section in `work/<id>/clarifications.md` plus
  current command input
- **Fields**: related question id, related ambiguity ids, answer text,
  answer kind (`decision`, `acceptedDeferral`, `stillOpen`, `note`), authoring
  note, source location
- **Validation**: Answers must reference known questions or source ambiguity
  ids; duplicate or conflicting answers for the same durable decision block
  unless the update is proven append-only and non-destructive.
- **Relationships**: Produces `ClarificationDecision` values or
  `RemainingAmbiguity` entries.

## ClarificationDecision

- **Source**: `## Decisions` and `## Accepted Deferrals` sections in
  `work/<id>/clarifications.md`
- **Fields**: decision id (`DEC-###`), title, decision kind
  (`concreteDecision`, `acceptedDeferral`), text, rationale, source question
  ids, source ambiguity ids, related requirement/story/acceptance ids, source
  location
- **Validation**: Decision ids use the existing `DecisionId` contract and are
  unique; concrete decisions must include decision text; accepted deferrals
  must include rationale and remaining visibility; existing decision ids are
  never renumbered or semantically changed by reruns.
- **Relationships**: Durable choices consumed by checklist, plan, tasks,
  evidence, generated work models, and diagnostics.

## RemainingAmbiguity

- **Source**: `## Remaining Ambiguity` section in
  `work/<id>/clarifications.md` plus unanswered source ambiguity records
- **Fields**: source ambiguity id, related question id, state (`blocking`,
  `nonBlocking`, `acceptedDeferral`), explanation, required correction,
  source location
- **Validation**: Blocking remaining ambiguity prevents a clarified lifecycle
  state and points next action to additional clarification; accepted deferrals
  remain visible to later stages and do not disappear from reports.
- **Relationships**: Included in reports, generated-view diagnostics, and text
  projection.

## ClarificationTemplate

- **Source**: Command library template constants or helper functions
- **Fields**: front matter shape, standard section headings, default question
  generation policy, deterministic newline policy, id allocation policy
- **Validation**: Output uses LF line endings, stable section order, no
  timestamps, no absolute paths, and no generated text that claims Governance
  enforcement.
- **Relationships**: Produces the proposed clarification artifact for new
  files and safe missing-section additions.

## ClarificationWritePlan

- **Source**: Existing clarification snapshot plus proposed
  `ClarificationArtifact`
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Authored content is written only for create, exact no-change,
  or proven-safe section/id/answer/decision additions; identity mismatch,
  malformed front matter, duplicate ids, unknown references, unsafe decision
  changes, or ambiguous section structure produces a blocking diagnostic before
  a write effect.
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

## ClarifyCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed clarification facts,
  generated views, diagnostics, Governance compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random
  values, absolute host paths, or directory enumeration order appear in
  authoritative output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## ClarifyDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification prerequisite, specification identity mismatch, malformed
  specification facts, missing clarification answer, clarification identity
  mismatch, malformed clarification front matter, duplicate clarification id,
  unknown clarification reference, unsafe decision change, unresolved blocking
  ambiguity, stale generated view, malformed generated view, blocked
  generated-view refresh, optional Governance boundary issue, tool defect
- **Validation**: Diagnostics distinguish user-correctable input from tool
  defects and sort by severity, id, artifact path, location, and message.
- **Relationships**: Appears in command reports, generated-view states, tests,
  and readiness evidence.

## GovernanceCompatibilityFact

- **Source**: Optional Governance pointers in SDD-owned configuration or
  clarification policy/impact notes
- **Fields**: path, relationship, required-by-SDD flag, observed state,
  diagnostic ids
- **Validation**: Missing Governance files do not block clarification;
  malformed Governance-owned files are reported only as optional boundary
  issues unless an SDD-owned artifact explicitly requires the pointer.
- **Relationships**: Included in command reports without route, profile,
  freshness, gate, audit, or enforcement verdicts.

## ClarifyNextAction

- **Fields**: action id, command, work id, reason, required artifacts,
  blocking diagnostic ids
- **Validation**: Successful clarification creation or safe update points to
  `checklist`; blocked reports point to the artifact or diagnostic that must
  be corrected. If blocking ambiguity remains, next action is additional
  clarification rather than checklist.
- **Relationships**: Included in JSON reports and text projections.
