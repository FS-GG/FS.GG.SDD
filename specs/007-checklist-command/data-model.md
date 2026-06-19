# Data Model: Checklist Command

## ChecklistCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd checklist`
- **Fields**: command, project root token, selected work id, optional checklist
  review input text, output format, dry-run flag, overwrite policy, generator
  version
- **Validation**: Command is `checklist`; work id is required and must satisfy
  the existing `WorkId` contract; optional input may add review notes or
  accepted-deferral rationale but cannot override source-derived failures
  without a safe recorded result; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths
  are excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `ChecklistCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before checklist writes are planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the target specification path, clarification
  path, checklist path, readiness path, optional Governance compatibility
  facts, and generated-view policy.

## ClarifiedWorkItem

- **Source**: Selected work id plus `work/<id>/spec.md`,
  `work/<id>/clarifications.md`, existing `work/<id>/checklist.md`, and
  existing work-item source snapshots
- **Fields**: work id, normalized work root, specification path,
  clarification path, checklist path, readiness path, specification status,
  clarification status, existing lifecycle sources, duplicate logical id
  candidates
- **Validation**: Work id must match the selected path and specification,
  clarification, and checklist front matter where present; specification must
  have `stage: specify`; clarification must have `stage: clarify`; duplicate
  logical ids or selected-id mismatches block before writes.
- **State transitions**: `clarified` -> `checklistReady` when all blocking
  requirements-quality checks pass or have accepted deferrals that remain
  visible; `clarified` -> `needsCorrection` when checklist findings remain;
  blocked states keep the prior filesystem state unchanged.
- **Relationships**: Owns the `SpecificationFacts`, `ClarificationFacts`,
  `ChecklistArtifact`, generated work-model state, report context, and next
  action.

## SpecificationFacts

- **Source**: Parsed `work/<id>/spec.md`
- **Fields**: specification front matter, user story ids, requirement ids,
  acceptance scenario ids, scope boundary ids, success criteria, edge cases,
  assumptions, ambiguity ids, unresolved ambiguity count, requirement and
  scenario references, source identity, source digest, diagnostics
- **Validation**: Front matter must match the selected work id and stage
  `specify`; ids must be unique and valid; requirements must be testable
  enough for checklist review; source ids used by checklist items or results
  must point to facts present in the selected specification.
- **Relationships**: Source context for checklist items, review results,
  blocking findings, accepted deferrals, source snapshot facts, generated
  work-model state, and diagnostics.

## ClarificationFacts

- **Source**: Parsed `work/<id>/clarifications.md`
- **Fields**: clarification front matter, question ids, answered question ids,
  decision ids, accepted deferral ids, remaining ambiguity entries, blocking
  ambiguity count, source specification path, source identity, source digest,
  diagnostics
- **Validation**: Front matter must match the selected work id, stage
  `clarify`, and selected source specification; question and decision ids must
  be unique and valid; blocking remaining ambiguity prevents checklist-ready
  state; accepted deferrals must remain visible to the checklist and later
  stages.
- **Relationships**: Source context for checklist readiness, accepted deferral
  visibility, stale source detection, generated work-model state, and
  diagnostics.

## ChecklistArtifact

- **Authored source**: `work/<id>/checklist.md`
- **Fields**: structured front matter, source specification relationship,
  source clarification relationship, source snapshot facts, checklist items,
  checklist results, accepted deferrals, blocking findings, advisory notes,
  lifecycle notes, original prose body
- **Validation**: Front matter must include `schemaVersion`, `workId`, `title`,
  `stage: checklist`, `status`, `sourceSpec`, `sourceClarifications`, and
  `changeTier`; body must contain standard sections or be safely completable;
  existing user-authored prose, checklist items, review results, accepted
  deferrals, findings, notes, and stable ids must be preserved.
- **Relationships**: Main authored output of the command; contributes
  checklist readiness facts, source identity, and source digest to
  generated-view refresh when enough lifecycle data exists.

## ChecklistFrontMatter

- **Source**: YAML front matter in `work/<id>/checklist.md`
- **Fields**: schema version, work id, title, stage, status, source
  specification path, source clarification path, change tier, optional public
  or tool-facing impact flags
- **Validation**: Schema version uses the standard schema compatibility
  classifier; work id must equal the selected work id; stage must be
  `checklist`; source paths must point to the selected
  `work/<id>/spec.md` and `work/<id>/clarifications.md`; status must reflect
  `checklistReady`, `needsCorrection`, `needsReview`, `blocked`, or an
  equivalent documented value.
- **Relationships**: Structured machine-readable checklist identity for reruns,
  reports, normalized work models, and later lifecycle commands.

## SourceSnapshot

- **Source**: `## Source Snapshot` section in `work/<id>/checklist.md` and
  current parsed source facts
- **Fields**: source artifact path, source kind, source digest, schema version
  when known, referenced ids, snapshot status, generator version when produced
- **Validation**: Paths are project-relative; digests are lowercase SHA-256
  values over normalized source bytes; referenced ids must exist in the
  selected source facts; changed source digests mark related checklist results
  stale or needing review unless the change is proven irrelevant.
- **Relationships**: Connects checklist results to source specification and
  clarification facts for stale-result diagnostics and generated-view
  currency.

## ChecklistItem

- **Source**: `## Checklist Items` section in `work/<id>/checklist.md` or
  generated from the requirements-quality policy
- **Fields**: item id (`CHK-###`), title, check category, prompt, source
  requirement ids, source story ids, source acceptance scenario ids, source
  scope boundary ids, source ambiguity ids, source clarification decision ids,
  blocking flag, expected correction guidance, source location
- **Validation**: Item ids are unique and stable across reruns; source ids must
  exist in the selected specification or clarification facts when present;
  blocking items require a passed result or accepted deferral before the work
  item is checklist-ready.
- **Relationships**: Connects source facts to review results, blocking
  findings, diagnostics, reports, generated work models, and text projection.

## ChecklistResult

- **Source**: `## Review Results` section in `work/<id>/checklist.md` plus
  current command evaluation
- **Fields**: result id (`CR-###`), related checklist item id, status (`pass`,
  `fail`, `acceptedDeferral`, `stale`, `advisory`), source snapshot reference,
  rationale, correction, related source ids, source location
- **Validation**: Result ids are unique and stable across reruns; every result
  references a known checklist item; failed blocking results require
  user-correctable correction text; accepted deferrals require rationale and
  downstream visibility; stale results cannot satisfy checklist-ready state
  until reviewed.
- **Relationships**: Produces checklist summary counts, blocking findings,
  next action, generated-view facts, and report diagnostics.

## RequirementsQualityFinding

- **Source**: Evaluation of `SpecificationFacts`, `ClarificationFacts`, and
  existing `ChecklistArtifact`
- **Fields**: finding id or diagnostic id, related checklist item id, related
  result id when known, severity, blocking flag, artifact path, source ids,
  message, correction
- **Validation**: Findings use stable identifiers and sort deterministically;
  blocking findings prevent `plan` as next action; advisory findings remain
  visible without blocking checklist-ready state.
- **Relationships**: Included in checklist artifact, command diagnostics,
  text projection, and generated work-model state.

## AcceptedChecklistDeferral

- **Source**: `## Accepted Deferrals` section in `work/<id>/checklist.md`,
  clarification accepted deferrals, or current command input
- **Fields**: result id, related checklist item id, source decision ids,
  rationale, scope, downstream visibility note, source location
- **Validation**: Deferrals must be explicit, tied to source facts or checklist
  items, and visible to planning; a deferral cannot hide missing user value,
  missing work identity, malformed source data, or unsafe write conflicts.
- **Relationships**: Can satisfy checklist-ready state only when it is an
  accepted non-destructive deferral; later plan and task stages must see it.

## ChecklistTemplate

- **Source**: Command library template constants or helper functions
- **Fields**: front matter shape, standard section headings,
  requirements-quality item generation policy, deterministic newline policy,
  item id allocation policy, result id allocation policy
- **Validation**: Output uses LF line endings, stable section order, no
  timestamps, no absolute paths, and no generated text that claims Governance
  enforcement.
- **Relationships**: Produces the proposed checklist artifact for new files and
  safe missing-section or missing-item additions.

## ChecklistWritePlan

- **Source**: Existing checklist snapshot plus proposed `ChecklistArtifact`
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Authored content is written only for create, exact no-change,
  proven-safe section/id/item/result additions, or safe stale-result marking;
  identity mismatch, malformed front matter, duplicate ids, unknown references,
  unsafe result changes, or ambiguous section structure produces a blocking
  diagnostic before a write effect.
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

## ChecklistCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, parsed specification facts, parsed
  clarification facts, checklist summary facts, generated views, diagnostics,
  Governance compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in authoritative
  output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## ChecklistDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, missing
  specification prerequisite, missing clarification prerequisite,
  specification identity mismatch, clarification identity mismatch, unresolved
  blocking ambiguity, failed requirements quality, checklist identity mismatch,
  malformed checklist front matter, duplicate checklist id, unknown checklist
  source reference, stale checklist result, unsafe checklist result change,
  stale generated view, malformed generated view, blocked generated-view
  refresh, optional Governance boundary issue, tool defect
- **Validation**: Diagnostics distinguish user-correctable input from tool
  defects and sort by severity, id, artifact path, location, and message.
- **Relationships**: Appears in command reports, generated-view states, tests,
  and readiness evidence.

## GovernanceCompatibilityFact

- **Source**: Optional Governance pointers in SDD-owned configuration or
  checklist policy/impact notes
- **Fields**: path, relationship, required-by-SDD flag, observed state,
  diagnostic ids
- **Validation**: Missing Governance files do not block checklist; malformed
  Governance-owned files are reported only as optional boundary issues unless
  an SDD-owned artifact explicitly requires the pointer.
- **Relationships**: Included in command reports without route, profile,
  freshness, gate, audit, or enforcement verdicts.

## ChecklistNextAction

- **Fields**: action id, command, work id, reason, required artifacts,
  blocking diagnostic ids
- **Validation**: Checklist-ready creation or safe update points to `plan`;
  failed quality, stale results, or blocking diagnostics point to the artifact
  or source fact that must be corrected. If blocking findings remain, next
  action is correction rather than planning.
- **Relationships**: Included in JSON reports and text projections.
