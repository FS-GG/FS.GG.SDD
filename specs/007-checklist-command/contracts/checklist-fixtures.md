# Contract: Checklist Fixtures

## Scope

Checklist fixtures provide real filesystem-style inputs and expected outputs for
semantic tests, safe-write behavior, generated-view currency, dry-run behavior,
deterministic reports, stale review results, failed requirements quality,
accepted deferrals, and Governance boundary checks. Fixture data is synthetic
and fixture names disclose the scenario they represent.

Fixtures live under `tests/fixtures/lifecycle-commands/`.

## Shared Rules

- All paths are repository-relative.
- Fixture source files use current schema versions unless the scenario tests
  schema failure.
- Golden JSON uses UTF-8 without a byte order mark.
- Reports exclude timestamps, durations, absolute host paths, terminal styling,
  and nondeterministic ordering.
- Expected diagnostics use stable ids.
- Every successful checklist fixture includes valid `work/<id>/spec.md` and
  `work/<id>/clarifications.md` prerequisites with stable source ids.
- Shared fixture directories such as `deterministic-report`, `text-projection`,
  `dry-run`, `stale-generated-view`, and `governance-boundary` may contain
  command-specific inputs, but checklist expectations must not replace existing
  specify or clarify expectations.

## Required Fixtures

### `checklist-create`

Purpose: proves an initialized SDD project with a clarified work item can
create a new checklist artifact.

Expected result:

- `work/<id>/checklist.md` is created with valid front matter and standard
  sections.
- At least one `CHK-###` item and one `CR-###` result are present.
- Report outcome is `succeeded` when all blocking checks pass.
- Next action is `plan` when no blocking findings or stale results remain.
- Governance runtime is not required.

### `checklist-rerun-preserves-results`

Purpose: proves rerunning checklist preserves authored items and review
results.

Expected result:

- Existing user-authored checklist items, results, accepted deferrals,
  findings, advisory notes, and lifecycle notes remain unchanged.
- Existing `CHK-###` and `CR-###` ids remain unchanged.
- Report records `preserve`, `noChange`, or equivalent safe state.
- No destructive write occurs.

### `checklist-adds-missing-items`

Purpose: proves the command can safely add newly required checklist items from
current source facts.

Expected result:

- New required checklist items are appended in deterministic order.
- Existing authored sections are unchanged.
- New `CHK-###` and `CR-###` ids use the next available suffix.
- Report names exactly which artifact changed.

### `checklist-preserves-stable-ids`

Purpose: proves reruns do not renumber existing checklist ids.

Expected result:

- Existing `CHK-###` and `CR-###` ids remain unchanged.
- New ids, if any, use the next available suffix.
- The report identifies preserved and added ids.

### `checklist-accepted-deferral`

Purpose: proves accepted deferrals are durable checklist results and remain
visible to planning.

Expected result:

- A deferred quality concern is recorded as a `CR-###` result with
  accepted-deferral semantics.
- The report includes the accepted deferral count or ids.
- The checklist artifact names the source decision or checklist item tied to
  the deferral.
- Next action is `plan` only when no blocking finding or stale result remains.

### `checklist-stale-result`

Purpose: proves changed source facts mark existing review results stale.

Expected result:

- Existing results that reference changed source snapshots are marked stale or
  needing review.
- Stable ids are preserved.
- Next action points to checklist review correction rather than `plan`.
- The report names the changed source artifact and related result ids.

### `failed-requirements-quality`

Purpose: proves requirements-quality failures are authored checklist output.

Expected result:

- `work/<id>/checklist.md` is created or safely updated.
- Failed blocking results include correction guidance.
- Report includes `failedRequirementsQuality` or equivalent diagnostics.
- Next action points to specification, clarification, or checklist correction
  rather than `plan`.

### `unresolved-ambiguity`

Purpose: proves checklist does not advance when clarification still has
blocking ambiguity.

Expected result:

- No unsafe checklist write occurs.
- Report identifies the remaining ambiguity or clarification source to correct.
- Next action points to clarification correction.

### `missing-clarification`

Purpose: proves checklist requires the clarification prerequisite.

Expected result:

- No `checklist.md` path is created.
- Report includes `missingClarificationPrerequisite` or equivalent blocking
  diagnostic.
- Next action points to correction rather than `plan`.

### `missing-specification`

Purpose: proves checklist requires the specification prerequisite.

Expected result:

- No `checklist.md` path is created.
- Report includes `missingSpecificationPrerequisite` or equivalent blocking
  diagnostic.
- Next action points to correction rather than `plan`.

### `outside-project`

Purpose: proves checklist refuses to author lifecycle artifacts before
`fsgg-sdd init` has created an SDD project skeleton.

Expected result:

- No work directory or checklist artifact is created.
- Report includes `outsideProject` or an equivalent missing-project
  diagnostic.
- Next action points to project initialization or root correction.

### `malformed-work-id`

Purpose: proves work id validation happens before writes.

Expected result:

- No work directory is created.
- Report identifies the accepted work-id shape.
- Outcome is `blocked`.

### `duplicate-work-id`

Purpose: proves checklist diagnoses duplicated logical work ids before writes.

Expected result:

- Duplicate work item paths are identified.
- No checklist artifact is created or updated.
- Report includes a blocking duplicate-work-id diagnostic.

### `malformed-checklist`

Purpose: proves malformed checklist front matter or section/id data blocks
unsafe progress.

Expected result:

- The malformed source is named.
- Generated-view refresh is blocked.
- No unsafe authored write occurs.

### `duplicate-checklist-id`

Purpose: proves duplicate item or result ids are diagnosed.

Expected result:

- Duplicate ids are identified before write.
- Existing checklist content is unchanged.
- Report includes a blocking duplicate-id diagnostic.

### `unknown-source-reference`

Purpose: proves checklist items and results cannot reference ids absent from
the selected specification or clarification sources.

Expected result:

- Unknown requirement, story, acceptance-scenario, ambiguity, question,
  decision, checklist item, or result ids are identified before unsafe write.
- Existing checklist content is unchanged.
- Report includes a blocking unknown-reference diagnostic.

### `checklist-identity-mismatch`

Purpose: proves mismatched front matter blocks writes.

Expected result:

- Existing checklist work id, source specification, or source clarification
  differs from the selected work id.
- No write occurs.
- Report includes `checklistIdentityMismatch` or equivalent blocking
  diagnostic.

### `unsafe-overwrite`

Purpose: proves authored checklist content is not clobbered by a proposed
rewrite.

Expected result:

- Planned destructive section or prose rewrite is refused.
- Existing file content remains byte-for-byte unchanged.
- Report includes before digest, diagnostic id, affected artifact, and
  correction guidance.

### `unsafe-checklist-result-change`

Purpose: proves durable review results are not silently changed.

Expected result:

- Planned conflicting result text, status, source links, or deferral semantics
  are refused unless represented as a safe stale/replacement path.
- Existing file content remains byte-for-byte unchanged when the path is
  unsafe.
- Report includes before digest, diagnostic id, affected result id, and
  correction guidance.

### `stale-generated-view`

Purpose: proves generated work-model currency is checked.

Expected result:

- Existing generated file presence is not enough.
- Stale source digest or generator version produces a stale generated-view
  diagnostic.
- The source artifact to correct is named when available.

### `dry-run`

Purpose: proves dry-run reports proposed changes without mutation.

Expected result:

- The report names proposed authored and generated changes.
- No `checklist.md` or refreshed generated view is written.
- Re-running without dry-run over the same input produces the planned writes.

### `deterministic-report`

Purpose: proves checklist JSON reports are byte-stable.

Expected result:

- Three dry-run executions over identical snapshots produce byte-identical
  reports.
- Artifact changes, parsed checklist facts, generated views, diagnostics, and
  Governance facts sort by documented keys.

### `text-projection`

Purpose: proves text output is a projection from the report.

Expected result:

- Text summary includes command, outcome, changed artifact count, checklist
  item count, passed count, failed blocking count, accepted deferral count,
  stale result count, generated-view count, diagnostic count, and next action
  from the report.
- Text mode introduces no fact absent from JSON.

### `governance-boundary`

Purpose: proves optional Governance files do not become required.

Expected result:

- Absent Governance files do not block checklist creation.
- Present Governance pointers appear only as compatibility facts.
- No route, profile, freshness, gate, protected-branch, audit, or release
  verdict is produced.

## Required Test Mapping

| Fixture | Required test focus |
|---|---|
| `checklist-create` | successful create, report, parsed facts, next action |
| `checklist-rerun-preserves-results` | preserve/no-change rerun behavior |
| `checklist-adds-missing-items` | safe non-destructive update |
| `checklist-preserves-stable-ids` | id stability and append-only allocation |
| `checklist-accepted-deferral` | deferral as durable visible result |
| `checklist-stale-result` | stale source snapshot and next-action correction |
| `failed-requirements-quality` | failed quality written as checklist output |
| `unresolved-ambiguity` | clarification remaining ambiguity diagnostic |
| `missing-clarification` | clarification prerequisite diagnostic |
| `missing-specification` | specification prerequisite diagnostic |
| `outside-project` | missing initialized project diagnostic |
| `malformed-work-id` | work id validation |
| `duplicate-work-id` | duplicate logical work id diagnostic |
| `malformed-checklist` | schema/front matter/section diagnostics |
| `duplicate-checklist-id` | duplicate id diagnostics |
| `unknown-source-reference` | unknown source/checklist reference diagnostics |
| `checklist-identity-mismatch` | blocking identity mismatch diagnostic |
| `unsafe-overwrite` | authored-content protection |
| `unsafe-checklist-result-change` | durable result protection |
| `stale-generated-view` | generated-view currency checks |
| `dry-run` | proposed changes without mutation |
| `deterministic-report` | byte-stable checklist JSON |
| `text-projection` | text from report only |
| `governance-boundary` | no-Governance and optional boundary behavior |
