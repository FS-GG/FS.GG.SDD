# Data Model: Verify Command

## VerifyCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd verify`
- **Fields**: command, project root token, selected work id, output format,
  dry-run flag, overwrite policy, generator version
- **Validation**: Command is `verify`; work id is required and must satisfy the
  existing `WorkId` contract; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths are
  excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `VerifyCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before generated writes are planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the target specification path, clarification path,
  checklist path, plan path, tasks path, analysis path, evidence path,
  work-model path, verification view path, optional Governance compatibility
  facts, and generated-view policy.

## EvidenceReadyWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, `work/<id>/checklist.md`, `work/<id>/plan.md`,
  `work/<id>/tasks.yml`, `work/<id>/evidence.yml`,
  `readiness/<id>/analysis.json`, existing `readiness/<id>/verify.json`, and
  generated work-model state
- **Fields**: work id, normalized work root, prerequisite artifact paths,
  readiness path, source lifecycle statuses, duplicate logical id candidates,
  generated-view states, verification readiness state
- **Validation**: Work id must match the selected path and source artifacts;
  prerequisite lifecycle stages must be complete enough for evidence-ready state;
  `analysis.json` must belong to the selected work id and report
  implementation-ready or accepted-deferral state; `evidence.yml` must belong to
  the selected work id and parse as schema version 1; duplicate logical ids or
  selected-id mismatches block verification readiness.
- **State transitions**: `evidenceReady` -> `verificationReady` when all blocking
  task, evidence, required-test, required-skill, generated-view, and prerequisite
  analysis findings are resolved or have accepted deferrals visible to later
  stages; `evidenceReady` -> `needsVerificationCorrection` when blocking defects
  remain; blocked states leave existing authored filesystem state unchanged.
- **Relationships**: Owns the source fact set, generated verification view,
  generated work-model state, report context, and next action.

## TaskGraphState

- **Source**: `work/<id>/tasks.yml` plus task source snapshots and the
  normalized work model
- **Fields**: task ids, dependencies, owners, statuses, requirement links,
  decision links, required skills, required evidence obligations, required test
  obligations, accepted deferrals, source snapshots
- **Validation**: Task ids are unique; dependencies reference known task ids and
  contain no cycles; statuses are supported transitions; owners and requirement
  links are present where required; source snapshots match current digests or
  produce stale-task diagnostics; missing owners, unknown dependencies,
  dependency cycles, unsupported status transitions, missing requirement links,
  missing required evidence, or missing required tests block verification
  readiness.
- **Relationships**: Drives required obligations and verification findings before
  ship readiness.

## RequiredVerificationObligation

- **Source**: Completed tasks, plan verification obligations, required skills,
  required tests, generated-view impacts, accepted deferrals, analysis findings,
  and lifecycle rules
- **Fields**: obligation id, kind (`test`, `evidence`, `skill`, `generatedView`,
  `lifecycle`), source artifact path, source id, linked task ids, linked
  requirement ids, linked decision ids, expected evidence kinds, required skill
  or capability tags, blocking flag, correction
- **Validation**: Obligation id is stable for identical source facts; linked ids
  must exist in their artifact scope; obligations derived from stale sources are
  marked stale rather than current.
- **Relationships**: Matched against evidence declarations, test declarations,
  and skill visibility to create disposition facts.

## EvidenceDisposition

- **Source**: Evidence declaration matching over current obligations
- **Fields**: disposition id, obligation id, state, evidence ids, affected task
  ids, affected source ids, severity, diagnostic ids, correction
- **States**: `supported`, `deferred`, `missing`, `stale`, `synthetic`,
  `invalid`, `advisory`, `blocking`
- **Validation**: Every completed task with required evidence receives exactly
  one current disposition per obligation; missing, stale, invalid, undisclosed
  synthetic, and invalid-deferral states produce diagnostics and block
  verification-ready status when required.
- **Relationships**: Drives verification readiness, command report counts, text
  projection, and later ship inputs.

## RequiredTestDisposition

- **Source**: Required test obligation matching over evidence declarations and
  generated-view impacts
- **Fields**: disposition id, obligation id, state, evidence ids, affected task
  ids, affected requirement ids, severity, diagnostic ids, correction
- **States**: `satisfied`, `deferred`, `missing`, `stale`, `synthetic`,
  `invalid`, `advisory`, `blocking`
- **Validation**: Every required test obligation receives exactly one current
  disposition; missing, stale, undisclosed synthetic, invalid, or
  unaccepted-deferral states produce diagnostics and block verification-ready
  status when required.
- **Relationships**: Drives verification readiness, command report counts, text
  projection, and later ship inputs.

## SkillVisibilityFact

- **Source**: Task required-skill and capability-tag declarations checked against
  skills and capability tags visible in the project's lifecycle artifacts
- **Fields**: skill or capability tag id, requiring task ids, visibility state
  (`visible`, `missing`), source artifact path, severity, diagnostic ids,
  correction
- **Validation**: Each declared required skill or capability tag is resolved to
  visible or missing from SDD lifecycle artifacts and declared agent/capability
  metadata, not from live network discovery or Governance enforcement; missing
  required skills block verification-ready status.
- **Relationships**: Drives verification readiness, skill visibility counts, and
  the affected-task trace in diagnostics.

## VerificationFinding

- **Source**: Lifecycle source artifacts, evidence declarations, task graph
  state, required tests, required skills, generated-view state, or optional
  boundary facts
- **Fields**: finding id, severity (`ready`, `advisory`, `warning`, `blocking`),
  category, affected artifact path, linked requirement ids, linked acceptance
  scenario ids, linked clarification decision ids, linked checklist result ids,
  linked plan decision ids, linked task ids, linked evidence obligation ids,
  linked test obligation ids, linked required skills, linked evidence
  declaration ids, linked generated views, linked accepted deferral ids,
  message, correction
- **Validation**: Finding id is stable for identical source facts; structured
  links are included when known; severity selects readiness blocking and
  exit-code basis.
- **Relationships**: Aggregated into the verification view and the verification
  summary counts.

## VerificationView

- **Generated view**: `readiness/<id>/verify.json`
- **Fields**: schema version, report/generator identity, work id, stage, status,
  source artifact relationships, source digests, schema versions, lifecycle
  stage readiness, task graph readiness, evidence dispositions, required test
  dispositions, skill visibility facts, generated-view currency, verification
  findings, optional boundary facts, diagnostics, verification readiness
- **Validation**: Schema version is current version 1; work id equals the
  selected work id; stage is `verify`; source paths are project-relative and
  match selected prerequisites; source digests match current sources or produce
  stale generated-view diagnostics; finding and disposition ids are unique and
  stable; JSON is byte-stable for identical sources.
- **Relationships**: Generated by the command, included in the command report
  generated views, and consumed by later ship readiness, generated summaries,
  agents, CI, and optional Governance consumers.

## GeneratedWorkModelState

- **Generated view**: `readiness/<id>/work-model.json`
- **Fields**: path, schema version when known, generator id/version, source
  identities, output digest when written, currency (`current`, `missing`,
  `stale`, `malformed`, `blocked`), diagnostic ids
- **Validation**: Current requires valid source digests and generator version;
  incomplete source data records `missing` or `blocked`; malformed existing
  generated JSON records `malformed`; digest or generator mismatches record
  `stale`; verify depends on a refreshed or diagnosed work-model state when
  verification depends on the normalized model.
- **Relationships**: Included in the command report; may produce a generated
  write effect only when currency can be proven current after refresh.

## AnalysisPrerequisiteState

- **Generated view**: `readiness/<id>/analysis.json`
- **Fields**: path, work id, schema version, readiness status, source
  relationships, generated-view state, diagnostics, next action
- **Validation**: Analysis must parse as schema version 1, match the selected
  work id, and report implementation-ready or accepted-deferral state before
  verify can report verification-ready; missing, malformed, stale, or mismatched
  analysis produces next-action diagnostics.
- **Relationships**: Provides the pre-verify lifecycle gate and source facts for
  verification readiness without being regenerated by this feature.

## VerificationSummary

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: work id, stage, status, verify path, finding ids, ready finding
  count, advisory count, warning count, blocking count, obligation count,
  evidence supported count, evidence deferred count, evidence missing count,
  evidence stale count, evidence synthetic count, evidence invalid count, test
  satisfied count, test deferred count, test missing count, test stale count,
  test invalid count, skill visible count, skill missing count, generated-view
  state, verification readiness, source snapshot count
- **Validation**: Counts derive from parsed source facts, findings, and
  dispositions; ids sort by stable value; paths are project-relative; no clocks,
  process ids, terminal details, random values, absolute host paths, or
  directory enumeration order appear in authoritative content.
- **Relationships**: Included in `VerifyCommandReport`, text projection,
  exit-code selection, and next action.

## VerifyCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts, parsed
  clarification facts, parsed checklist facts, parsed plan facts, parsed task
  facts, analysis prerequisite summary, evidence summary, verification summary,
  generated views, diagnostics, Governance compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in authoritative
  output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## VerificationDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification, missing clarification, missing checklist, missing plan, missing
  tasks, missing analysis, missing evidence, analysis not ready, failed analysis,
  failed tasks, verification identity mismatch, duplicate work id, malformed
  prerequisite artifact, unknown source reference, dependency cycle, unsupported
  task status, missing required skill, missing required test, missing required
  evidence, stale analysis, stale tasks, stale evidence, undisclosed synthetic
  evidence, invalid deferral, malformed verification view, stale generated view,
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
- **Validation**: Successful verification-ready reports use
  `verify.next.ship`, set `command` to null until a later ship feature adds that
  command, and list `readiness/<id>/verify.json` plus refreshed
  `readiness/<id>/work-model.json` as required artifacts. Blocked reports point
  to implementation continuation, evidence correction, task correction, analysis
  rerun, generated-view refresh, missing-skill correction, required-test
  correction, or prerequisite lifecycle correction.
- **Relationships**: Guides humans, agents, CLI callers, and later workflow
  stages without implementing ship in this feature.
