# Data Model: Tasks Command

## TasksCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd tasks`
- **Fields**: command, project root token, selected work id, optional task
  input text, output format, dry-run flag, overwrite policy, generator version
- **Validation**: Command is `tasks`; work id is required and must satisfy the
  existing `WorkId` contract; optional input may add task notes or accepted
  deferral rationale where safe but cannot override graph blockers; project
  root is normalized for reports; behavior-affecting options are recorded in
  the report; absolute host paths are excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `TasksCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before task writes are planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the target specification path, clarification
  path, checklist path, plan path, tasks path, readiness path, optional
  Governance compatibility facts, and generated-view policy.

## PlannedWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, `work/<id>/checklist.md`,
  `work/<id>/plan.md`, existing `work/<id>/tasks.yml`, and current source
  snapshots
- **Fields**: work id, normalized work root, specification path,
  clarification path, checklist path, plan path, tasks path, readiness path,
  source lifecycle statuses, existing lifecycle sources, duplicate logical id
  candidates
- **Validation**: Work id must match the selected path and source artifacts;
  specification must have `stage: specify`; clarification must have
  `stage: clarify`; checklist must have `stage: checklist`; plan must have
  `stage: plan` and a planned status; duplicate logical ids or selected-id
  mismatches block before writes.
- **State transitions**: `planned` -> `tasksReady` when all blocking task
  prerequisites and required task dispositions are complete or have accepted
  deferrals that remain visible; `planned` -> `needsCorrection` when graph or
  source findings remain; blocked states keep the prior filesystem state
  unchanged.
- **Relationships**: Owns the `SpecificationFacts`, `ClarificationFacts`,
  `ChecklistFacts`, `PlanFacts`, `TasksArtifact`, generated work-model state,
  report context, and next action.

## SpecificationFacts

- **Source**: Parsed `work/<id>/spec.md`
- **Fields**: specification front matter, user story ids, requirement ids,
  acceptance scenario ids, scope boundary ids, success criteria, edge cases,
  assumptions, ambiguity ids, requirement and scenario references, source
  identity, source digest, diagnostics
- **Validation**: Front matter must match the selected work id and stage
  `specify`; ids must be unique and valid; source ids used by tasks must point
  to facts present in the selected specification.
- **Relationships**: Source context for task requirement links, source
  snapshots, stale-task diagnostics, generated work-model state, and report
  traceability.

## ClarificationFacts

- **Source**: Parsed `work/<id>/clarifications.md`
- **Fields**: clarification front matter, question ids, answered question ids,
  decision ids, accepted deferral ids, remaining ambiguity entries, source
  specification path, source identity, source digest, diagnostics
- **Validation**: Front matter must match the selected work id, stage
  `clarify`, and selected source specification; question and decision ids must
  be unique and valid; accepted deferrals must remain visible to tasks and
  later stages.
- **Relationships**: Source context for task decision links, accepted deferral
  visibility, stale source detection, generated work-model state, and
  diagnostics.

## ChecklistFacts

- **Source**: Parsed `work/<id>/checklist.md`
- **Fields**: checklist front matter, item ids, result ids, source snapshots,
  passed count, failed blocking count, accepted deferral count, stale result
  count, advisory count, blocking findings, lifecycle notes, source identity,
  source digest, diagnostics
- **Validation**: Front matter must match the selected work id, stage
  `checklist`, selected source specification, and selected clarification
  artifact; failed blocking results, stale results, missing required results,
  or blocking findings prevent task-ready state.
- **Relationships**: Source context for task graph prerequisites and task
  source links.

## PlanFacts

- **Source**: Parsed `work/<id>/plan.md`
- **Fields**: plan front matter, source specification relationship, source
  clarification relationship, source checklist relationship, source snapshots,
  plan decisions, contract references, verification obligations, migration
  notes, generated-view impacts, accepted deferrals, blocking findings,
  advisory notes, lifecycle notes, source identity, source digest,
  diagnostics
- **Validation**: Front matter must match the selected work id, stage `plan`,
  selected source artifacts, and a planned state; stale decisions, missing
  blocking dispositions, unknown references, or blocking findings prevent task
  generation.
- **Relationships**: Primary source for task derivation, required skills,
  evidence obligations, accepted deferrals, generated-view impacts, and
  migration tasks.

## TasksArtifact

- **Authored source**: `work/<id>/tasks.yml`
- **Fields**: schema version, work id, title, stage, status, source
  specification path, source clarification path, source checklist path, source
  plan path, source snapshots, task entries, task findings, advisory notes,
  lifecycle notes, original authored content where preserved
- **Validation**: Root schema must include `schemaVersion: 1`; if present,
  `workId` must equal the selected work id and `stage` must be `tasks`; source
  paths must point to the selected prerequisite artifacts; existing user task
  content, statuses, owners, dependencies, required skills, required evidence,
  skip rationales, findings, notes, and stable ids must be preserved unless a
  non-destructive update is proven safe.
- **Relationships**: Main authored output of the command; contributes task
  graph facts, source identity, and source digest to generated-view refresh
  when enough lifecycle data exists.

## TasksRootMetadata

- **Source**: Root YAML keys in `work/<id>/tasks.yml`
- **Fields**: schema version, work id, title, stage, status, source
  specification path, source clarification path, source checklist path, source
  plan path, public or tool-facing impact flag when carried forward
- **Validation**: Schema version uses the standard schema compatibility
  classifier; work id must equal the selected work id; stage must be `tasks`
  when present; status must reflect `tasksReady`, `needsCorrection`,
  `needsReview`, or `blocked`; source paths must be project-relative and point
  to selected lifecycle sources.
- **Relationships**: Structured machine-readable task identity for reruns,
  reports, normalized work models, and later lifecycle commands.

## TaskSourceSnapshot

- **Source**: `sources` records in `work/<id>/tasks.yml` and current parsed
  source facts
- **Fields**: source artifact path, source kind, source digest, schema version
  when known, referenced ids, snapshot status, generator version when produced
- **Validation**: Paths are project-relative; digests are lowercase SHA-256
  values over normalized source bytes; referenced ids must exist in the
  selected source facts; changed source digests mark related task entries
  stale or needing review unless the change is proven irrelevant.
- **Relationships**: Connects task entries to source specification,
  clarification, checklist, and plan facts for stale-task diagnostics and
  generated-view currency.

## TaskEntry

- **Source**: `tasks` records in `work/<id>/tasks.yml`
- **Fields**: task id (`T001`), title, status, owner, dependencies,
  requirement ids, source decision ids, source plan decision ids, contract
  reference ids, verification obligation ids, generated-view impact ids,
  accepted deferral ids, required skills or capability tags, required evidence
  ids, skip rationale, stale flag, source snapshot references, source location
- **Validation**: Task ids are unique and stable across reruns; status is one
  of `pending`, `inProgress`, `done`, `skipped`, `stale`, or a documented
  equivalent; dependencies must reference known tasks and must not form a
  cycle; source ids must exist in selected source facts; skipped tasks require
  rationale; done tasks require declared evidence when evidence facts are
  present or required.
- **Relationships**: Atomic implementation unit for analyze, implement,
  evidence, verify, ship, generated work models, reports, and text projection.

## TaskDependencyGraph

- **Source**: `dependencies` values on task entries
- **Fields**: nodes, directed edges, unknown dependency ids, self edges,
  cycle paths, sorted traversal order
- **Validation**: Every dependency id references a known task, no task depends
  on itself, and the graph is acyclic. Errors block before authored writes.
- **Relationships**: Produces task graph readiness and dependency diagnostics;
  supports deterministic task ordering for reports and generated views.

## TaskDisposition

- **Source**: Task entry status plus related source ids and accepted deferrals
- **Fields**: task id, disposition (`pending`, `inProgress`, `done`,
  `skipped`, `stale`, `missing`, `blocked`, `acceptedDeferral`), rationale,
  related source ids, related diagnostics
- **Validation**: Every in-scope requirement, plan decision, contract impact,
  verification obligation, migration note, generated-view impact, and accepted
  deferral has a visible task disposition or accepted deferral; missing
  blocking dispositions prevent tasks-ready state.
- **Relationships**: Summarizes readiness for command reports, next action,
  later analysis, and optional Governance-compatible rule inputs.

## EvidenceObligation

- **Source**: Task entry `requiredEvidence` values and plan verification
  obligations
- **Fields**: evidence id (`EV001`), related task ids, evidence kind, related
  requirements, related plan decisions, related contracts, synthetic evidence
  policy, acceptance threshold, source location
- **Validation**: Evidence ids are unique when declared in tasks; each required
  evidence obligation links to at least one task or source fact; done tasks
  require supporting evidence declarations when the selected work item already
  records completed task state.
- **Relationships**: Feeds later evidence declarations, verify readiness,
  ship readiness, and task-readiness diagnostics.

## TaskWritePlan

- **Source**: Existing task snapshot plus proposed `TasksArtifact`
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Authored content is written only for create, exact
  no-change, proven-safe additions, safe source-snapshot updates, or safe
  stale-task marking; identity mismatch, malformed schema, duplicate task ids,
  dependency cycles, unknown source references, unsupported destructive status
  changes, unsafe overwrite, or ambiguous structure produces a blocking
  diagnostic before a write effect.
- **Relationships**: Drives command effects and `ArtifactChange` report
  entries.

## TaskGraphReadiness

- **Source**: `TasksArtifact`, `TaskDependencyGraph`, source facts, and
  diagnostics
- **Fields**: task count, dependency count, required skill count, required
  evidence count, skipped task count, stale task count, missing disposition
  count, blocking finding count, advisory count, readiness status
- **Validation**: Readiness is `ready` only when graph blockers are absent,
  required source dispositions are visible, stale tasks are reviewed or
  explicitly accepted, and completed tasks have required evidence state when
  applicable.
- **Relationships**: Included in task summary reports and text projection;
  drives next action selection.

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

## TasksCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts, parsed
  clarification facts, parsed checklist facts, parsed plan facts, task summary
  facts, graph readiness, generated views, diagnostics, Governance
  compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random
  values, absolute host paths, or directory enumeration order appear in
  authoritative output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## TaskDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification, missing clarification, missing checklist, missing plan,
  failed plan, task identity mismatch, malformed task schema, duplicate task
  id, unknown source reference, unknown dependency, dependency cycle, unsafe
  overwrite, unsafe status change, stale task source, done task missing
  evidence, generated-view missing, generated-view stale, generated-view
  malformed, generated-view blocked, tool defect
- **Validation**: Diagnostics use stable ids, actionable corrections,
  project-relative paths, deterministic ordering, and severity appropriate to
  next-action blocking.
- **Relationships**: Diagnostics drive blocked outcomes, text projection,
  next action, readiness evidence, and optional Governance compatibility facts.

## NextLifecycleAction

- **Source**: Task graph readiness plus blocking diagnostics
- **Fields**: action id, command, work id, reason, required artifacts,
  blocking diagnostic ids
- **Validation**: Successful task-ready results point to `analyze`; blocked
  reports point to specification, clarification, checklist, plan, or task
  correction and include blocking diagnostic ids.
- **Relationships**: Consumed by humans, agents, CLI callers, and later
  lifecycle stages.
