# Data Model: Evidence Command

## EvidenceCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd evidence`
- **Fields**: command, project root token, selected work id, optional evidence
  input text, output format, dry-run flag, overwrite policy, generator version
- **Validation**: Command is `evidence`; work id is required and must satisfy
  the existing `WorkId` contract; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths
  are excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `EvidenceCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before evidence writes are planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the target specification path, clarification
  path, checklist path, plan path, tasks path, analysis path, evidence path,
  work-model path, optional Governance compatibility facts, and generated-view
  policy.

## AnalyzedWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, `work/<id>/checklist.md`,
  `work/<id>/plan.md`, `work/<id>/tasks.yml`,
  `readiness/<id>/analysis.json`, existing `work/<id>/evidence.yml`, and
  generated work-model state
- **Fields**: work id, normalized work root, prerequisite artifact paths,
  readiness path, source lifecycle statuses, duplicate logical id candidates,
  generated-view states, evidence readiness state
- **Validation**: Work id must match the selected path and source artifacts;
  prerequisite lifecycle stages must be complete enough for analyzed state;
  `analysis.json` must belong to the selected work id and report
  implementation-ready or accepted-deferral state; duplicate logical ids or
  selected-id mismatches block evidence readiness.
- **State transitions**: `implementationReady` -> `evidenceReady` when all
  blocking evidence obligations are supported by real evidence or accepted
  deferrals visible to later stages; `implementationReady` ->
  `needsEvidenceCorrection` when missing, stale, malformed, synthetic, or
  unsafe evidence facts remain; blocked states leave existing authored
  filesystem state unchanged.
- **Relationships**: Owns the source fact set, evidence artifact, generated
  work-model state, report context, and next action.

## EvidenceArtifact

- **Authored source**: `work/<id>/evidence.yml`
- **Fields**: schema version, work id, stage, status, source specification
  path, source clarification path, source checklist path, source plan path,
  source tasks path, source analysis path, source snapshots, evidence
  declarations, lifecycle notes
- **Validation**: Schema version is current version 1; work id equals the
  selected work id; stage is `evidence`; source paths are project-relative and
  match selected prerequisites; source snapshots match current source digests
  or produce stale evidence diagnostics; evidence ids are unique and stable.
- **Relationships**: Parsed into `EvidenceFacts`, contributes evidence entries
  to the normalized work model, and supplies evidence summaries in the command
  report.

## EvidenceDeclaration

- **Source**: One entry in `work/<id>/evidence.yml` or compatible command input
- **Fields**: evidence id, kind, subject, linked task ids, linked requirement
  ids, linked acceptance scenario ids, linked clarification decision ids,
  linked checklist result ids, linked plan decision ids, linked evidence
  obligation ids, source references, result state, synthetic disclosure,
  accepted deferral rationale, owner, scope, lifecycle notes, source location
- **Validation**: Evidence id is unique; linked ids exist when their source
  artifact is present; result state is supported; synthetic evidence has a
  disclosure explaining what real path it stands in for; deferrals have
  rationale, owner, scope, and later lifecycle visibility; source references
  are project-relative or explicit external references.
- **Relationships**: Satisfies or explains one or more `EvidenceObligation`
  records and appears in `EvidenceDisposition` results.

## EvidenceObligation

- **Source**: Tasks, plan verification obligations, required skills,
  generated-view impacts, analysis findings, and lifecycle rules
- **Fields**: obligation id, kind, source artifact path, source id, linked task
  ids, linked requirement ids, linked decision ids, expected evidence kinds,
  required skill or capability tags, blocking flag, correction
- **Validation**: Obligation id is stable for identical source facts; linked
  ids must exist in their artifact scope; obligations derived from stale
  sources are marked stale rather than current.
- **Relationships**: Matched against `EvidenceDeclaration` values to create
  `EvidenceDisposition` facts.

## EvidenceDisposition

- **Source**: Evidence declaration matching over current obligations
- **Fields**: disposition id, obligation id, state, evidence ids, affected task
  ids, affected source ids, severity, diagnostic ids, correction
- **States**: `supported`, `deferred`, `missing`, `stale`, `synthetic`,
  `invalid`, `advisory`, `blocking`
- **Validation**: Every completed task with required evidence receives exactly
  one current disposition per obligation; missing, stale, invalid, undisclosed
  synthetic, and unsafe states produce diagnostics and block evidence-ready
  status when required.
- **Relationships**: Drives evidence readiness, command report counts, text
  projection, and later verify/ship inputs.

## EvidenceSourceReference

- **Source**: Evidence declaration source references
- **Fields**: reference id, kind, path or URI, digest when available, related
  source id, result label, source location
- **Validation**: Project-local paths are normalized and must exist when the
  declaration claims real evidence; external or future references must be
  explicitly marked; digests are deterministic when included.
- **Relationships**: Supports evidence traceability and optional Governance
  freshness inputs without calculating freshness in SDD.

## EvidenceWritePlan

- **Source**: Existing evidence artifact plus proposed evidence facts
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Authored evidence is written only for create or compatible
  update operations when source facts are valid and dry-run is false; unsafe
  overwrite, duplicate ids, malformed existing evidence, stale prerequisite
  state, or invalid declaration data produce diagnostics before write effects.
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
  `stale`; evidence depends on a refreshed or diagnosed work-model state after
  authored evidence changes.
- **Relationships**: Included in the command report; may produce a generated
  write effect only when currency can be proven current after refresh.

## AnalysisPrerequisiteState

- **Generated view**: `readiness/<id>/analysis.json`
- **Fields**: path, work id, schema version, readiness status, source
  relationships, generated-view state, diagnostics, next action
- **Validation**: Analysis must parse as schema version 1, match the selected
  work id, and report implementation-ready or accepted-deferral state before
  evidence can report evidence-ready; missing, malformed, stale, or mismatched
  analysis produces next-action diagnostics.
- **Relationships**: Provides the pre-evidence lifecycle gate and source facts
  for evidence readiness without being regenerated by this feature.

## EvidenceSummary

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: work id, stage, status, evidence path, declaration ids,
  declaration count, obligation count, supported count, deferred count, missing
  count, stale count, synthetic count, invalid count, advisory count, blocking
  count, evidence readiness, source snapshot count
- **Validation**: Counts derive from parsed evidence facts and dispositions;
  ids sort by stable value; paths are project-relative; no clocks, process ids,
  terminal details, random values, absolute host paths, or directory
  enumeration order appear in authoritative content.
- **Relationships**: Included in `EvidenceCommandReport`, text projection,
  exit-code selection, and next action.

## EvidenceCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts, parsed
  clarification facts, parsed checklist facts, parsed plan facts, parsed task
  facts, analysis prerequisite summary, evidence summary, generated views,
  diagnostics, Governance compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in authoritative
  output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## EvidenceDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification, missing clarification, missing checklist, missing plan,
  missing tasks, missing analysis, analysis not ready, evidence identity
  mismatch, malformed evidence artifact, duplicate evidence id, unknown
  evidence reference, missing required evidence, stale evidence, stale
  evidence source, undisclosed synthetic evidence, missing deferral rationale,
  unsafe evidence update, missing required skill, unsupported evidence result,
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
- **Validation**: Successful evidence-ready reports use
  `evidence.next.verify`, set `command` to null until a later verify feature
  adds that command, and list `work/<id>/evidence.yml` plus refreshed
  `readiness/<id>/work-model.json` as required artifacts. Blocked reports
  point to implementation continuation, evidence correction, task correction,
  analysis rerun, or generated-view refresh.
- **Relationships**: Guides humans, agents, CLI callers, and later workflow
  stages without implementing verify in this feature.
