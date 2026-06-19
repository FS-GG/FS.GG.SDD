# Data Model: Analyze Command

## AnalyzeCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd analyze`
- **Fields**: command, project root token, selected work id, output format,
  dry-run flag, overwrite policy, generator version
- **Validation**: Command is `analyze`; work id is required and must satisfy
  the existing `WorkId` contract; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths
  are excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `AnalyzeCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before analysis generation is planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the target specification path, clarification
  path, checklist path, plan path, tasks path, work-model path, analysis path,
  optional Governance compatibility facts, and generated-view policy.

## TasksReadyWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, `work/<id>/checklist.md`,
  `work/<id>/plan.md`, `work/<id>/tasks.yml`, generated work-model state, and
  current source snapshots
- **Fields**: work id, normalized work root, prerequisite artifact paths,
  readiness path, source lifecycle statuses, duplicate logical id candidates,
  generated-view states
- **Validation**: Work id must match the selected path and source artifacts;
  specification must have `stage: specify`; clarification must have
  `stage: clarify`; checklist must have `stage: checklist`; plan must have
  `stage: plan`; tasks must have `stage: tasks` and a tasks-ready or
  correctable task state; duplicate logical ids or selected-id mismatches block
  implementation readiness.
- **State transitions**: `tasksReady` -> `implementationReady` when all
  blocking analysis findings are absent or have accepted deferrals visible to
  later stages; `tasksReady` -> `needsCorrection` when blocking source,
  task, or generated-view findings remain; blocked states leave authored
  filesystem state unchanged.
- **Relationships**: Owns the source fact set, generated work-model state,
  analysis view, report context, and next action.

## LifecycleSourceSet

- **Source**: Parsed specification, clarification, checklist, plan, and tasks
  facts plus source identities
- **Fields**: source artifact paths, source kinds, source digests, schema
  versions, schema statuses, parsed ids, lifecycle stage statuses,
  source-to-source links
- **Validation**: Source paths are project-relative; digests are derived from
  normalized source bytes; downstream source snapshots must point to selected
  upstream artifacts; unknown or stale source links create analysis findings.
- **Relationships**: Primary input to generated work-model refresh, analysis
  source relationships, consistency findings, and command report summaries.

## SpecificationFacts

- **Source**: Parsed `work/<id>/spec.md`
- **Fields**: specification front matter, user story ids, requirement ids,
  acceptance scenario ids, scope boundary ids, success criteria, edge cases,
  assumptions, ambiguity ids, requirement and scenario references, source
  identity, source digest, diagnostics
- **Validation**: Front matter must match the selected work id and stage
  `specify`; ids must be unique and valid; every in-scope requirement and
  acceptance scenario must have a visible downstream disposition in
  clarification, checklist, plan, tasks, or accepted deferrals.
- **Relationships**: Source context for analysis requirement findings, missing
  disposition findings, generated work-model state, and report traceability.

## ClarificationFacts

- **Source**: Parsed `work/<id>/clarifications.md`
- **Fields**: clarification front matter, question ids, answered question ids,
  decision ids, accepted deferral ids, remaining ambiguity entries, source
  specification path, source identity, source digest, diagnostics
- **Validation**: Front matter must match the selected work id, stage
  `clarify`, and selected source specification; blocking ambiguity prevents
  implementation readiness unless accepted deferrals remain visible to later
  stages.
- **Relationships**: Source context for analysis ambiguity, decision,
  accepted-deferral, and missing-disposition findings.

## ChecklistFacts

- **Source**: Parsed `work/<id>/checklist.md`
- **Fields**: checklist front matter, item ids, result ids, source snapshots,
  passed count, failed blocking count, accepted deferral count, stale result
  count, advisory count, blocking findings, lifecycle notes, source identity,
  source digest, diagnostics
- **Validation**: Front matter must match the selected work id, stage
  `checklist`, selected source specification, and selected clarification
  artifact; failed blocking results, stale results, missing required results,
  or blocking findings prevent implementation readiness.
- **Relationships**: Source context for requirements-quality findings,
  accepted deferrals, stale source state, and report summaries.

## PlanFacts

- **Source**: Parsed `work/<id>/plan.md`
- **Fields**: plan front matter, source specification relationship, source
  clarification relationship, source checklist relationship, source snapshots,
  plan decisions, contract references, verification obligations, migration
  notes, generated-view impacts, accepted deferrals, blocking findings,
  advisory notes, lifecycle notes, source identity, source digest,
  diagnostics
- **Validation**: Front matter must match the selected work id, stage `plan`,
  and selected source artifacts; stale decisions, missing blocking
  dispositions, unknown references, incomplete decisions, or blocking findings
  prevent implementation readiness.
- **Relationships**: Source context for task dispositions, analysis source
  relationships, generated-view impacts, verification obligations, migration
  posture, and findings.

## TaskFacts

- **Source**: Parsed `work/<id>/tasks.yml`
- **Fields**: task front matter, source specification path, source
  clarification path, source checklist path, source plan path, source
  snapshots, task ids, dependencies, statuses, owners, requirement links,
  decision links, source ids, required skills, required evidence obligations,
  accepted deferrals, findings, advisory notes, lifecycle notes, source
  identity, source digest, diagnostics
- **Validation**: Root schema must include current version 1; work id must
  equal the selected work id; source paths must point to selected lifecycle
  sources; task ids must be unique; dependencies must reference known tasks
  and be acyclic; every in-scope requirement, decision, contract, verification
  obligation, migration note, generated-view impact, accepted deferral, and
  required evidence obligation must have a visible task disposition; done tasks
  require evidence state when required evidence is recorded.
- **Relationships**: Main task-readiness input to analysis findings and
  implementation-readiness state.

## GeneratedWorkModelState

- **Generated view**: `readiness/<id>/work-model.json`
- **Fields**: path, schema version when known, generator id/version, source
  identities, output digest when written, currency (`current`, `missing`,
  `stale`, `malformed`, `blocked`), diagnostic ids
- **Validation**: Current requires valid source digests and generator version;
  incomplete source data records `missing` or `blocked`; malformed existing
  generated JSON records `malformed`; digest or generator mismatches record
  `stale`; analysis depends on a current or explicitly refreshed work-model
  state when valid source data exists.
- **Relationships**: Included in the command report and analysis view; may
  produce a generated write effect only when currency can be proven current
  after refresh.

## AnalysisView

- **Generated view**: `readiness/<id>/analysis.json`
- **Fields**: schema version, report version, work id, stage, status,
  generated timestamp omission policy, generator identity, source
  relationships, source identities, lifecycle readiness, analysis findings,
  generated-view states, optional boundary facts, diagnostics, next action
- **Validation**: Schema version is 1; work id equals the selected work id;
  source paths are project-relative; lists sort by stable ids and paths; no
  clocks, process ids, terminal details, random values, absolute host paths, or
  directory enumeration order appear in authoritative content.
- **Relationships**: Generated analysis artifact consumed by humans, agents,
  CI, later implementation/evidence/verify/ship stages, and optional
  Governance-compatible tooling.

## AnalysisSourceRelationship

- **Source**: Current source facts and downstream source snapshots
- **Fields**: relationship id, source artifact path, target artifact path,
  source id, target id, relationship kind, current digest, recorded digest,
  state (`current`, `stale`, `missing`, `unknownReference`, `blocked`)
- **Validation**: Both artifact paths are project-relative; known ids must
  exist in their artifact scope; stale or missing relationships generate
  analysis findings with corrections.
- **Relationships**: Explains why requirements, decisions, results, plan
  obligations, tasks, and generated views are considered current or blocked.

## AnalysisFinding

- **Source**: Lifecycle source facts, task graph validation, generated-view
  state, and optional boundary facts
- **Fields**: finding id, category, severity, state, affected artifact path,
  related requirement ids, acceptance scenario ids, clarification decision ids,
  checklist result ids, plan decision ids, contract reference ids,
  verification obligation ids, task ids, dependency ids, generated view paths,
  accepted deferral ids, explanation, correction
- **Validation**: Finding ids are stable for identical inputs; severities are
  `ready`, `advisory`, `warning`, `blocking`, `staleSource`,
  `missingDisposition`, `malformedSource`, or `generatedView`; blocking
  findings prevent implementation readiness unless accepted deferrals remain
  visible to later stages.
- **Relationships**: Drives readiness counts, diagnostics, next action, text
  projection, and later readiness evidence.

## AnalysisReadiness

- **Source**: Analysis findings and generated-view states
- **Fields**: ready finding count, advisory count, warning count, blocking
  count, stale source count, missing disposition count, malformed source
  count, generated-view finding count, accepted deferral count, readiness
  status
- **Validation**: Readiness is `implementationReady` only when blocking,
  malformed, stale, and missing-disposition findings are absent or have
  accepted deferrals visible to later stages; otherwise readiness is
  `needsCorrection`, `blocked`, or `needsGeneratedViewRefresh`.
- **Relationships**: Included in `AnalysisView`, `AnalyzeCommandReport`, text
  projection, exit-code selection, and next action.

## AnalysisWritePlan

- **Source**: Existing analysis view snapshot plus proposed `AnalysisView`
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Generated analysis is written only for create, update,
  no-change, or preserve operations when source facts are valid and dry-run is
  false; malformed existing analysis, stale sources, blocked work-model state,
  or invalid prerequisites produce diagnostics before generated write effects.
- **Relationships**: Drives command effects and `ArtifactChange` report
  entries.

## AnalyzeCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts, parsed
  clarification facts, parsed checklist facts, parsed plan facts, parsed task
  facts, analysis summary, generated views, diagnostics, Governance
  compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random
  values, absolute host paths, or directory enumeration order appear in
  authoritative output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## AnalysisDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification, missing clarification, missing checklist, missing plan,
  missing tasks, failed checklist, failed plan, failed tasks, analysis identity
  mismatch, malformed analysis view, duplicate work id, unknown source
  reference, unknown task dependency, dependency cycle, unresolved ambiguity,
  stale checklist result, stale plan decision, stale task, missing disposition,
  done task missing evidence, generated-view missing, generated-view stale,
  generated-view malformed, generated-view blocked, tool defect
- **Validation**: Diagnostics use stable ids, actionable corrections,
  project-relative paths, deterministic ordering, and severity appropriate to
  next-action blocking.
- **Relationships**: Diagnostics drive blocked outcomes, text projection,
  next action, readiness evidence, and optional Governance compatibility facts.

## NextLifecycleAction

- **Source**: Analysis readiness plus blocking diagnostics
- **Fields**: action id, command, work id, reason, required artifacts,
  blocking diagnostic ids
- **Validation**: Successful implementation-ready results point to
  implementation with no command value until the implementation stage is
  introduced; blocked reports point to specification, clarification,
  checklist, plan, tasks, or generated-view correction and include blocking
  diagnostic ids.
- **Relationships**: Consumed by humans, agents, CLI callers, and later
  lifecycle stages.
