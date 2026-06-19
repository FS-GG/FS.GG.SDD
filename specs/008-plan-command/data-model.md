# Data Model: Plan Command

## PlanCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd plan`
- **Fields**: command, project root token, selected work id, optional planning
  input text, output format, dry-run flag, overwrite policy, generator version
- **Validation**: Command is `plan`; work id is required and must satisfy the
  existing `WorkId` contract; optional input may add planning notes or accepted
  deferral rationale but cannot override source-derived blockers without a safe
  recorded plan decision; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths
  are excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `PlanCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before plan writes are planned; malformed or missing
  project settings block the command.
- **Relationships**: Supplies the target specification path, clarification
  path, checklist path, plan path, readiness path, optional Governance
  compatibility facts, and generated-view policy.

## ChecklistReadyWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, `work/<id>/checklist.md`, existing
  `work/<id>/plan.md`, and existing source snapshots
- **Fields**: work id, normalized work root, specification path, clarification
  path, checklist path, plan path, readiness path, specification status,
  clarification status, checklist status, existing lifecycle sources,
  duplicate logical id candidates
- **Validation**: Work id must match the selected path and specification,
  clarification, checklist, and plan front matter where present;
  specification must have `stage: specify`; clarification must have
  `stage: clarify`; checklist must have `stage: checklist` and a
  checklist-ready status; duplicate logical ids or selected-id mismatches block
  before writes.
- **State transitions**: `checklistReady` -> `planned` when all blocking
  planning prerequisites and plan entries are complete or have accepted
  deferrals that remain visible; `checklistReady` -> `needsCorrection` when
  planning findings remain; blocked states keep the prior filesystem state
  unchanged.
- **Relationships**: Owns the `SpecificationFacts`, `ClarificationFacts`,
  `ChecklistFacts`, `PlanArtifact`, generated work-model state, report
  context, and next action.

## SpecificationFacts

- **Source**: Parsed `work/<id>/spec.md`
- **Fields**: specification front matter, user story ids, requirement ids,
  acceptance scenario ids, scope boundary ids, success criteria, edge cases,
  assumptions, ambiguity ids, requirement and scenario references, source
  identity, source digest, diagnostics
- **Validation**: Front matter must match the selected work id and stage
  `specify`; ids must be unique and valid; source ids used by plan decisions,
  contract references, or verification obligations must point to facts present
  in the selected specification.
- **Relationships**: Source context for plan scope, contract impact,
  verification obligations, source snapshots, stale-decision diagnostics, and
  generated work-model state.

## ClarificationFacts

- **Source**: Parsed `work/<id>/clarifications.md`
- **Fields**: clarification front matter, question ids, answered question ids,
  decision ids, accepted deferral ids, remaining ambiguity entries, source
  specification path, source identity, source digest, diagnostics
- **Validation**: Front matter must match the selected work id, stage
  `clarify`, and selected source specification; question and decision ids must
  be unique and valid; accepted deferrals must remain visible to the plan and
  later stages.
- **Relationships**: Source context for planning decisions, accepted deferral
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
  or blocking findings prevent planned state.
- **Relationships**: Planning prerequisite and source context for plan
  decisions, accepted deferrals, verification obligations, and next action.

## PlanArtifact

- **Authored source**: `work/<id>/plan.md`
- **Fields**: structured front matter, source specification relationship,
  source clarification relationship, source checklist relationship, source
  snapshot facts, plan scope, plan decisions, contract references,
  verification obligations, migration notes, generated-view impacts, accepted
  deferrals, blocking findings, advisory notes, lifecycle notes, original
  prose body
- **Validation**: Front matter must include `schemaVersion`, `workId`, `title`,
  `stage: plan`, `status`, `sourceSpec`, `sourceClarifications`,
  `sourceChecklist`, and `changeTier`; body must contain standard sections or
  be safely completable; existing user-authored prose, plan decisions,
  contract references, verification obligations, migration notes, accepted
  deferrals, findings, notes, and stable ids must be preserved.
- **Relationships**: Main authored output of the command; contributes planning
  readiness facts, source identity, and source digest to generated-view refresh
  when enough lifecycle data exists.

## PlanFrontMatter

- **Source**: YAML front matter in `work/<id>/plan.md`
- **Fields**: schema version, work id, title, stage, status, source
  specification path, source clarification path, source checklist path, change
  tier, optional public or tool-facing impact flags
- **Validation**: Schema version uses the standard schema compatibility
  classifier; work id must equal the selected work id; stage must be `plan`;
  source paths must point to the selected `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, and `work/<id>/checklist.md`; status must
  reflect `planned`, `needsCorrection`, `needsReview`, `blocked`, or an
  equivalent documented value.
- **Relationships**: Structured machine-readable plan identity for reruns,
  reports, normalized work models, and later lifecycle commands.

## PlanSourceSnapshot

- **Source**: `## Source Snapshot` section in `work/<id>/plan.md` and current
  parsed source facts
- **Fields**: source artifact path, source kind, source digest, schema version
  when known, referenced ids, snapshot status, generator version when produced
- **Validation**: Paths are project-relative; digests are lowercase SHA-256
  values over normalized source bytes; referenced ids must exist in the
  selected source facts; changed source digests mark related plan decisions
  stale or needing review unless the change is proven irrelevant.
- **Relationships**: Connects plan decisions to source specification,
  clarification, and checklist facts for stale-decision diagnostics and
  generated-view currency.

## PlanDecision

- **Source**: `## Plan Decisions` section in `work/<id>/plan.md` or generated
  from checklist-ready source facts
- **Fields**: decision id (`PD-###`), title, status, text, rationale, source
  requirement ids, source acceptance scenario ids, source clarification
  decision ids, source checklist result ids, accepted deferral ids, blocking
  flag, source snapshot reference, source location
- **Validation**: Decision ids are unique and stable across reruns; source ids
  must exist in the selected specification, clarification, or checklist facts
  when present; stale or incomplete blocking decisions prevent planned state.
- **Relationships**: Connects source facts to contract references,
  verification obligations, blocking findings, diagnostics, reports, generated
  work models, and text projection.

## PlanContractReference

- **Source**: `## Contract Impact` section in `work/<id>/plan.md`
- **Fields**: contract id (`PC-###`), contract kind, artifact path or logical
  surface, affected lifecycle source ids, compatibility impact, required
  migration note id, source location
- **Validation**: Contract ids are unique and stable; every contract reference
  links to at least one relevant requirement, decision, checklist result, or
  accepted deferral; public or tool-facing impacts must have a visible
  compatibility disposition.
- **Relationships**: Used by task generation, evidence obligations, generated
  view summaries, and optional Governance compatibility facts.

## VerificationObligation

- **Source**: `## Verification Obligations` section in `work/<id>/plan.md`
- **Fields**: obligation id (`VO-###`), title, required evidence kind, related
  requirement ids, related plan decision ids, related contract ids, synthetic
  evidence policy, acceptance threshold, source location
- **Validation**: Obligation ids are unique and stable; every blocking
  requirement, accepted deferral, and public or tool-facing contract impact has
  at least one verification obligation or an explicit accepted deferral.
- **Relationships**: Becomes source material for later tasks, evidence,
  verify, and ship readiness.

## PlanMigrationNote

- **Source**: `## Migration Posture` section in `work/<id>/plan.md`
- **Fields**: migration id (`PM-###`), schema or command surface affected,
  posture (`none`, `diagnoseOnly`, `compatible`, `breaking`), migration
  action, related contract ids, related decision ids, source location
- **Validation**: Migration ids are unique and stable; breaking or
  tool-facing changes require explicit notes, compatibility impact, and
  verification obligations.
- **Relationships**: Supports schema evolution, release notes, task
  generation, and optional Governance compatibility reports.

## GeneratedViewImpact

- **Source**: `## Generated View Impact` section in `work/<id>/plan.md`
- **Fields**: impact id (`GV-###`), generated view path or kind, source
  artifact ids, expected currency behavior, stale diagnostic id, related plan
  decision ids, source location
- **Validation**: Impact ids are unique and stable; any generated view affected
  by plan sources must name source and stale behavior; generated views remain
  outputs and cannot be treated as authoritative sources.
- **Relationships**: Drives generated-view refresh or diagnostics and informs
  later `analyze`, verify, and ship readiness.

## AcceptedPlanDeferral

- **Source**: `## Accepted Deferrals` section in `work/<id>/plan.md`,
  clarification accepted deferrals, checklist accepted deferrals, or current
  command input
- **Fields**: deferral id or related decision id, source decision ids, source
  checklist result ids, rationale, scope, downstream visibility note, required
  follow-up action, source location
- **Validation**: Deferrals must be explicit, tied to source facts or plan
  decisions, and visible to tasks and evidence; a deferral cannot hide missing
  work identity, malformed source data, failed checklist blockers, or unsafe
  write conflicts.
- **Relationships**: Can satisfy planned state only when it is an accepted
  non-destructive deferral; later task and evidence stages must see it.

## PlanTemplate

- **Source**: Command library template constants or helper functions
- **Fields**: front matter shape, standard section headings, plan entry
  generation policy, deterministic newline policy, id allocation policy
- **Validation**: Output uses LF line endings, stable section order, no
  timestamps, no absolute paths, and no generated text that claims Governance
  enforcement.
- **Relationships**: Produces the proposed plan artifact for new files and
  safe missing-section or missing-entry additions.

## PlanWritePlan

- **Source**: Existing plan snapshot plus proposed `PlanArtifact`
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Authored content is written only for create, exact no-change,
  proven-safe section/id/decision/reference/obligation additions, or safe
  stale-decision marking; identity mismatch, malformed front matter, duplicate
  ids, unknown references, unsafe decision changes, or ambiguous section
  structure produces a blocking diagnostic before a write effect.
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

## PlanCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts, parsed
  clarification facts, parsed checklist facts, plan summary facts, generated
  views, diagnostics, Governance compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in authoritative
  output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## PlanDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification prerequisite, missing clarification prerequisite, missing
  checklist prerequisite, failed checklist prerequisite, specification
  identity mismatch, clarification identity mismatch, checklist identity
  mismatch, plan identity mismatch, malformed plan front matter, duplicate plan
  id, unknown plan source reference, stale plan decision, unsafe plan decision
  change, unsafe overwrite, stale generated view, malformed generated view,
  blocked generated-view refresh, optional Governance boundary issue, tool
  defect
- **Validation**: Diagnostics distinguish user-correctable input from tool
  defects and sort by severity, id, artifact path, location, and message.
- **Relationships**: Appears in command reports, generated-view states, tests,
  and readiness evidence.

## GovernanceCompatibilityFact

- **Source**: Optional Governance pointers in SDD-owned configuration or plan
  contract/impact notes
- **Fields**: path, relationship, required-by-SDD flag, observed state,
  diagnostic ids
- **Validation**: Missing Governance files do not block plan; malformed
  Governance-owned files are reported only as optional boundary issues unless
  an SDD-owned artifact explicitly requires the pointer.
- **Relationships**: Included in command reports without route, profile,
  freshness, gate, audit, or enforcement verdicts.

## PlanNextAction

- **Fields**: action id, command, work id, reason, required artifacts,
  blocking diagnostic ids
- **Validation**: Planned creation or safe update points to `tasks`;
  prerequisite failures, stale decisions, or blocking diagnostics point to the
  artifact or source fact that must be corrected. If blocking findings remain,
  next action is correction rather than task generation.
- **Relationships**: Included in JSON reports and text projections.
