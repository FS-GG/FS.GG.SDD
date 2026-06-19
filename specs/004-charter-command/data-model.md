# Data Model: Charter Command

## CharterCommandRequest

- **Source**: CLI arguments or library caller input for `fsgg-sdd charter`
- **Fields**: command, project root token, selected work id, optional title,
  optional input text, output format, dry-run flag, overwrite policy, generator
  version
- **Validation**: Command is `charter`; work id is required and must satisfy
  the existing `WorkId` contract; project root is normalized for reports;
  behavior-affecting options are recorded in the report; absolute host paths
  are excluded from authoritative content.
- **Relationships**: Starts the command workflow and appears in the
  `CharterCommandReport` invocation section.

## SDDProjectContext

- **Source**: `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`
- **Fields**: project id, default work root, readiness root, SDD config path,
  agents config path, generated-view policy, optional Governance pointers
- **Validation**: Required SDD files must exist and parse with current or
  accepted schema versions before charter writes are planned; malformed or
  missing project settings block the command.
- **Relationships**: Supplies the target charter path, readiness path, optional
  Governance compatibility facts, and generated-view policy.

## SelectedWorkItem

- **Source**: Selected work id plus existing `work/<id>/` source snapshots
- **Fields**: work id, normalized work root, charter path, readiness path,
  known existing lifecycle sources, duplicate logical id candidates
- **Validation**: Work id must match the selected path and any existing charter
  front matter; duplicate logical ids or selected-id mismatches block before
  writes.
- **State transitions**: `none` -> `chartered` after successful charter create
  or safe update; blocked states keep the prior filesystem state unchanged.
- **Relationships**: Owns the `CharterArtifact`, generated work-model state,
  report context, and next action.

## CharterArtifact

- **Authored source**: `work/<id>/charter.md`
- **Fields**: structured front matter, identity section, principles, scope
  boundaries, policy pointers, lifecycle notes, original prose body
- **Validation**: Front matter must include `schemaVersion`, `workId`, `title`,
  `stage: charter`, `status`, and `changeTier`; body must contain standard
  sections or be safely completable; existing user-authored prose must be
  preserved.
- **Relationships**: Main authored output of the command; contributes source
  identity to generated-view refresh when enough lifecycle data exists.

## CharterFrontMatter

- **Source**: YAML front matter in `work/<id>/charter.md`
- **Fields**: schema version, work id, title, stage, status, change tier,
  optional policy pointer list
- **Validation**: Schema version uses the standard schema compatibility
  classifier; work id must equal the selected work id; stage must be
  `charter`; policy pointers are references only and do not trigger Governance
  enforcement.
- **Relationships**: Structured machine-readable charter data for reruns,
  reports, and later lifecycle commands.

## CharterSection

- **Source**: Markdown body in `work/<id>/charter.md`
- **Fields**: section id, heading, body text, source location when available,
  standard-section flag
- **Required standard sections**: Identity, Principles, Scope Boundaries,
  Policy Pointers, Lifecycle Notes
- **Validation**: Missing standard sections may be appended deterministically
  when front matter is valid and no prose conflict is detected; conflicting
  section content is preserved and may produce a diagnostic instead of a write.
- **Relationships**: Used by safe rerun planning and human text projection.

## CharterTemplate

- **Source**: Command library template constants or helper functions
- **Fields**: front matter shape, standard section headings, default prompts,
  deterministic newline policy
- **Validation**: Output uses LF line endings, stable section order, no
  timestamps, no absolute paths, and no generated text that claims Governance
  enforcement.
- **Relationships**: Produces the proposed charter artifact for new files and
  safe missing-section additions.

## CharterWritePlan

- **Source**: Existing charter snapshot plus proposed `CharterArtifact`
- **Fields**: path, operation (`create`, `update`, `preserve`, `refuse`,
  `noChange`), before digest, proposed digest, safe-write decision, related
  diagnostics
- **Validation**: Authored content is written only for create, exact no-change,
  or proven-safe section additions; identity mismatch, malformed front matter,
  ambiguous section structure, or unsafe overwrite produces a blocking
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

## CharterCommandReport

- **Generated view**: JSON stdout or the in-memory report rendered to text
- **Fields**: schema version, report version, command identity, context,
  invocation, outcome, changed artifacts, generated views, diagnostics,
  Governance compatibility facts, next action
- **Validation**: JSON is byte-stable for identical inputs; lists sort by
  documented keys; no timestamps, terminal details, process ids, random values,
  absolute host paths, or directory enumeration order appear in authoritative
  output; text output is rendered from this value.
- **Relationships**: Immediate automation contract for humans, agents, CLI
  callers, CI, and optional Governance consumers.

## CharterDiagnostic

- **Fields**: diagnostic id, severity, artifact path, source location when
  available, message, correction, related ids
- **Required diagnostic families**: outside project, missing project config,
  malformed project config, missing work id, malformed work id, duplicate work
  id, charter identity mismatch, malformed charter front matter, missing
  standard section, unsafe overwrite, stale generated view, malformed generated
  view, blocked generated-view refresh, optional Governance boundary issue,
  tool defect
- **Validation**: Diagnostics distinguish user-correctable input from tool
  defects and sort by severity, id, artifact path, location, and message.
- **Relationships**: Appears in command reports, generated-view states, tests,
  and readiness evidence.

## GovernanceCompatibilityFact

- **Source**: Optional Governance pointers in SDD-owned configuration or
  charter policy pointers
- **Fields**: path, relationship, required-by-SDD flag, observed state,
  diagnostic ids
- **Validation**: Missing Governance files do not block charter creation;
  malformed Governance-owned files are reported only as optional boundary
  issues unless an SDD-owned artifact explicitly requires the pointer.
- **Relationships**: Included in command reports without route, profile,
  freshness, gate, audit, or enforcement verdicts.

## CharterNextAction

- **Fields**: action id, command, work id, reason, required artifacts, blocking
  diagnostic ids
- **Validation**: Successful charter creation or safe update points to
  `specify`; blocked reports point to the artifact or diagnostic that must be
  corrected.
- **Relationships**: Included in JSON reports and text projections.
