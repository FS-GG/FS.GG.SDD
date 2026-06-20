# Data Model: Ship Command

## ShipCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd ship`
- **Fields**: command, project root token, selected work id, output format,
  dry-run flag, overwrite policy, generator version
- **Validation**: Command is `ship`; work id is required and must satisfy the
  existing `WorkId` contract; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths are
  excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `ShipCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before generated writes are planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the target specification path, clarification path,
  checklist path, plan path, tasks path, analysis path, evidence path,
  verification view path, work-model path, ship view path, optional Governance
  compatibility facts, and generated-view policy.

## VerificationReadyWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`,
  `work/<id>/tasks.yml`, `work/<id>/evidence.yml`,
  `readiness/<id>/analysis.json`, `readiness/<id>/verify.json`, existing
  `readiness/<id>/ship.json`, and generated work-model state
- **Fields**: work id, normalized work root, prerequisite artifact paths,
  readiness path, source lifecycle statuses, duplicate logical id candidates,
  generated-view states, verification readiness state, ship readiness state
- **Validation**: Work id must match the selected path and source artifacts;
  prerequisite lifecycle stages must be complete enough for verification-ready
  state; `analysis.json` must belong to the selected work id and report
  implementation-ready or accepted-deferral state; `verify.json` must belong to
  the selected work id, parse as schema version 1, and report a
  verification-ready outcome with no unresolved blocking findings; duplicate
  logical ids or selected-id mismatches block ship readiness.
- **State transitions**: `verificationReady` -> `shipReady` when all blocking
  lifecycle, verification, evidence, and generated-view findings are resolved or
  have accepted deferrals visible to later stages; `verificationReady` ->
  `needsShipCorrection` when blocking defects remain; blocked states leave
  existing authored filesystem state and the verification view unchanged.
- **Relationships**: Owns the source fact set, generated ship view, generated
  work-model state, report context, and next action.

## AggregatedLifecycleReadiness

- **Source**: Normalized work model, analysis view, and verification view per
  lifecycle stage
- **Fields**: stage id (`charter`, `specify`, `clarify`, `checklist`, `plan`,
  `tasks`, `analyze`, `evidence`, `verify`), stage readiness state (`ready`,
  `advisory`, `stale`, `blocked`, `notApplicable`), source artifact path, source
  digest match, contributing diagnostic ids
- **Validation**: Each in-scope stage receives exactly one current readiness
  state; stages derived from stale or mismatched sources are marked stale or
  blocked rather than ready; readiness aggregation never re-derives
  verify-owned task/evidence/test/skill dispositions.
- **Relationships**: Drives the ship-readiness disposition, command report stage
  readiness, and text projection.

## ShipReadinessDisposition

- **Source**: Aggregated lifecycle readiness, verification readiness, evidence
  dispositions, and generated-view currency
- **Fields**: disposition id, state, blocking finding ids, warning finding ids,
  advisory finding ids, contributing stage ids, contributing generated views,
  severity, correction
- **States**: `shipReady`, `blocked`, `stale`, `advisory`
- **Validation**: Exactly one current ship-readiness disposition is produced;
  `shipReady` requires every blocking lifecycle, verification, evidence, and
  generated-view finding to be resolved or carry an accepted deferral visible to
  later stages; `stale` is produced when source facts changed after a
  prerequisite generated view or the verification view captured a snapshot;
  `blocked` is produced for any unresolved blocking finding; `advisory` is
  produced when only advisory findings remain.
- **Relationships**: Drives ship readiness, command report counts, text
  projection, next action, and optional Governance compatibility facts.

## ShipReadinessFinding

- **Source**: Aggregated lifecycle stages, the verification view, evidence
  dispositions, generated-view state, or optional boundary facts
- **Fields**: finding id, severity (`ready`, `advisory`, `warning`, `blocking`),
  category, affected artifact path, linked stage id, linked verification finding
  ids, linked evidence disposition ids, linked task ids, linked evidence
  declaration ids, linked generated views, linked accepted deferral ids, message,
  correction
- **Validation**: Finding id is stable for identical source facts; structured
  links are included when known; severity selects readiness blocking and
  exit-code basis; findings reference verify-owned ids rather than recomputing
  them.
- **Relationships**: Aggregated into the ship view and the ship summary counts.

## ShipView

- **Generated view**: `readiness/<id>/ship.json`
- **Fields**: schema version, report/generator identity, work id, stage, status,
  source artifact relationships, source digests, schema versions, aggregated
  lifecycle stage readiness, verification readiness summary, evidence disposition
  summary, generated-view currency, ship-readiness disposition, ship-readiness
  findings, optional boundary facts, diagnostics
- **Validation**: Schema version is current version 1; work id equals the
  selected work id; stage is `ship`; source paths are project-relative and match
  selected prerequisites; source digests match current sources or produce stale
  generated-view diagnostics; finding and disposition ids are unique and stable;
  JSON is byte-stable for identical sources.
- **Relationships**: Generated by the command, included in the command report
  generated views, and consumed by CI, generated summaries, agents, and optional
  Governance protected-boundary consumers.

## GeneratedWorkModelState

- **Generated view**: `readiness/<id>/work-model.json`
- **Fields**: path, schema version when known, generator id/version, source
  identities, output digest when written, currency (`current`, `missing`,
  `stale`, `malformed`, `blocked`), diagnostic ids
- **Validation**: Current requires valid source digests and generator version;
  incomplete source data records `missing` or `blocked`; malformed existing
  generated JSON records `malformed`; digest or generator mismatches record
  `stale`; ship depends on a refreshed or diagnosed work-model state when ship
  readiness depends on the normalized model.
- **Relationships**: Included in the command report; may produce a generated
  write effect only when currency can be proven current after refresh.

## VerificationPrerequisiteState

- **Generated view**: `readiness/<id>/verify.json`
- **Fields**: path, work id, schema version, verification readiness status,
  blocking finding ids, evidence disposition summary, source relationships,
  generated-view state, diagnostics, next action
- **Validation**: Verification view must parse as schema version 1, match the
  selected work id, and report a verification-ready status with no unresolved
  blocking findings before ship can report ship-ready; missing, malformed, stale,
  mismatched, or not-ready verification produces next-action diagnostics and
  blocks ship readiness.
- **Relationships**: Provides the pre-ship lifecycle gate and aggregated facts
  for ship readiness without being regenerated by this feature.

## AnalysisPrerequisiteState

- **Generated view**: `readiness/<id>/analysis.json`
- **Fields**: path, work id, schema version, readiness status, source
  relationships, generated-view state, diagnostics, next action
- **Validation**: Analysis must parse as schema version 1, match the selected
  work id, and report implementation-ready or accepted-deferral state; missing,
  malformed, stale, or mismatched analysis produces next-action diagnostics.
- **Relationships**: Provides an upstream lifecycle gate and source facts for
  aggregated readiness without being regenerated by this feature.

## ShipSummary

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: work id, stage, status, ship path, finding ids, ready finding
  count, advisory count, warning count, blocking count, lifecycle stage readiness
  states, verification readiness state, evidence supported count, evidence
  deferred count, evidence missing count, evidence stale count, evidence
  synthetic count, evidence invalid count, generated-view state, ship readiness,
  source snapshot count
- **Validation**: Counts derive from parsed source facts, aggregated stages,
  findings, and the verification view; ids sort by stable value; paths are
  project-relative; no clocks, process ids, terminal details, random values,
  absolute host paths, or directory enumeration order appear in authoritative
  content.
- **Relationships**: Included in `ShipCommandReport`, text projection, exit-code
  selection, and next action.

## ShipCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts, parsed
  clarification facts, parsed checklist facts, parsed plan facts, parsed task
  facts, analysis prerequisite summary, verification prerequisite summary,
  evidence summary, ship summary, generated views, diagnostics, Governance
  compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in authoritative
  output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance protected-boundary consumers.

## ShipDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification, missing clarification, missing checklist, missing plan, missing
  tasks, missing analysis, missing evidence, missing verification, analysis not
  ready, verification not ready, failed verification, ship identity mismatch,
  duplicate work id, malformed prerequisite artifact, unknown source reference,
  stale analysis, stale verification, stale evidence, undisclosed synthetic
  evidence, invalid deferral, malformed ship view, stale generated view,
  generated-view missing, generated-view stale, generated-view malformed,
  generated-view blocked, tool defect
- **Validation**: Diagnostics use stable ids, actionable corrections,
  project-relative paths, deterministic ordering, and severity appropriate to
  next-action blocking.
- **Relationships**: Diagnostics drive blocked outcomes, text projection, next
  action, readiness evidence, and optional Governance compatibility facts.

## NextLifecycleAction

- **Fields**: action id, command when implemented, work id, reason, required
  artifacts, blocking diagnostic ids
- **Validation**: Successful ship-ready reports use `ship.next.protectedBoundary`,
  set `command` to null because the protected-boundary handoff is Governance-owned
  rather than another SDD command, and list `readiness/<id>/ship.json` plus the
  refreshed `readiness/<id>/work-model.json` as required artifacts. Blocked
  reports point to verification rerun, evidence correction, prerequisite lifecycle
  correction, generated-view refresh, or stale-source correction.
- **Relationships**: Guides humans, agents, CLI callers, and later workflow
  stages without implementing protected-boundary enforcement in this feature.

## OptionalBoundaryFact

- **Source**: Optional Governance policy, capability, tooling, freshness, route,
  profile, gate, audit, enforcement, and release pointers visible in SDD-owned
  sources
- **Fields**: pointer id, pointer kind, source path, evaluated flag (always not
  evaluated in this feature), advisory note
- **Validation**: Boundary facts are advisory only; SDD never interprets,
  computes, or enforces them; absence of Governance files never blocks ship.
- **Relationships**: Included in the ship view and command report as
  compatibility facts only.
