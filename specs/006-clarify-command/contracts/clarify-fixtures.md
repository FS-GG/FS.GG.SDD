# Contract: Clarify Fixtures

## Scope

Clarify fixtures provide real filesystem-style inputs and expected outputs for
semantic tests, safe-write behavior, generated-view currency, dry-run behavior,
deterministic reports, accepted deferrals, and Governance boundary checks.
Fixture data is synthetic and fixture names disclose the scenario they
represent.

Fixtures live under `tests/fixtures/lifecycle-commands/`.

## Shared Rules

- All paths are repository-relative.
- Fixture source files use current schema versions unless the scenario tests
  schema failure.
- Golden JSON uses UTF-8 without a byte order mark.
- Reports exclude timestamps, durations, absolute host paths, terminal
  styling, and nondeterministic ordering.
- Expected diagnostics use stable ids.
- Every successful clarify fixture includes a valid `work/<id>/spec.md`
  prerequisite with stable source ids.
- Shared fixture directories such as `deterministic-report`,
  `text-projection`, `dry-run`, `stale-generated-view`, and
  `governance-boundary` may contain command-specific inputs, but clarify
  expectations must not replace existing specify expectations.

## Required Fixtures

### `clarify-create`

Purpose: proves an initialized SDD project with a specified work item can
create a new clarification artifact.

Expected result:

- `work/<id>/clarifications.md` is created with valid front matter and
  standard sections.
- At least one `CQ-###` question and one `DEC-###` decision are present when
  the source specification has blocking ambiguity.
- Report outcome is `succeeded`.
- Next action is `checklist` when blocking ambiguity is resolved.
- Governance runtime is not required.

### `no-open-ambiguity`

Purpose: proves clarify handles a valid specification that has no open
ambiguity records without inventing clarification questions.

Expected result:

- If required specification facts are present, the report states the work item
  is ready for `checklist`.
- If required specification facts are missing, the report identifies the
  missing facts and points to correction.
- No synthetic `CQ-###` question is created solely because the command ran.
- Generated-view state is reported from current sources rather than from file
  presence.

### `clarify-rerun-preserves-decisions`

Purpose: proves rerunning clarify preserves authored answers and decisions.

Expected result:

- Existing user-authored questions, answers, decisions, accepted deferrals,
  remaining ambiguity notes, and lifecycle notes remain unchanged.
- Existing `CQ-###` and `DEC-###` ids remain unchanged.
- Report records `preserve`, `noChange`, or equivalent safe state.
- No destructive write occurs.

### `clarify-adds-missing-sections`

Purpose: proves the command can safely complete missing standard sections.

Expected result:

- Missing standard sections are added in deterministic order.
- Existing authored sections are unchanged.
- Report names exactly which artifact changed.

### `clarify-preserves-stable-ids`

Purpose: proves reruns do not renumber existing clarification ids.

Expected result:

- Existing `CQ-###` and `DEC-###` ids remain unchanged.
- New ids, if any, use the next available suffix.
- The report identifies preserved and added ids.

### `clarify-accepted-deferral`

Purpose: proves accepted deferrals are durable decisions and remain visible.

Expected result:

- A deferred ambiguity is recorded as a `DEC-###` decision with
  accepted-deferral semantics.
- The report includes the accepted deferral count or ids.
- Remaining ambiguity state shows the deferral is visible to later stages.
- Next action is `checklist` only when no blocking ambiguity remains.

### `missing-specification`

Purpose: proves clarify requires the specification prerequisite.

Expected result:

- No `clarifications.md` path is created.
- Report includes `missingSpecificationPrerequisite` or equivalent blocking
  diagnostic.
- Next action points to correction rather than `checklist`.

### `missing-answer`

Purpose: proves blocking ambiguity requires an answer or accepted deferral.

Expected result:

- No `clarifications.md` path is created for unresolved blocking ambiguity.
- Report identifies the missing question or source ambiguity.
- Outcome is `blocked`.

### `outside-project`

Purpose: proves clarify refuses to author lifecycle artifacts before
`fsgg-sdd init` has created an SDD project skeleton.

Expected result:

- No work directory or clarification artifact is created.
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

Purpose: proves clarify diagnoses duplicated logical work ids before writes.

Expected result:

- Duplicate work item paths are identified.
- No clarification artifact is created or updated.
- Report includes a blocking duplicate-work-id diagnostic.

### `malformed-clarification`

Purpose: proves malformed clarification front matter or section/id data blocks
progress.

Expected result:

- The malformed source is named.
- Generated-view refresh is blocked.
- No unsafe authored write occurs.

### `duplicate-clarification-id`

Purpose: proves duplicate question or decision ids are diagnosed.

Expected result:

- Duplicate ids are identified before write.
- Existing clarification content is unchanged.
- Report includes a blocking duplicate-id diagnostic.

### `unknown-ambiguity-reference`

Purpose: proves answers cannot reference ambiguity ids absent from the selected
specification.

Expected result:

- Unknown ambiguity, requirement, story, acceptance-scenario, or question ids
  are identified before write.
- Existing clarification content is unchanged.
- Report includes a blocking unknown-reference diagnostic.

### `clarification-identity-mismatch`

Purpose: proves mismatched front matter blocks writes.

Expected result:

- Existing clarification work id or source specification differs from the
  selected work id.
- No write occurs.
- Report includes `clarificationIdentityMismatch` or equivalent blocking
  diagnostic.

### `unsafe-overwrite`

Purpose: proves authored clarification content is not clobbered by a proposed
rewrite.

Expected result:

- Planned destructive section or prose rewrite is refused.
- Existing file content remains byte-for-byte unchanged.
- Report includes before digest, diagnostic id, affected artifact, and
  correction guidance.

### `unsafe-decision-change`

Purpose: proves durable decisions are not silently changed.

Expected result:

- Planned conflicting decision text or accepted-deferral semantics are refused.
- Existing file content remains byte-for-byte unchanged.
- Report includes before digest, diagnostic id, affected decision id, and
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
- No `clarifications.md` or refreshed generated view is written.
- Re-running without dry-run over the same input produces the planned writes.

### `deterministic-report`

Purpose: proves clarify JSON reports are byte-stable.

Expected result:

- Three dry-run executions over identical snapshots produce byte-identical
  reports.
- Artifact changes, parsed clarification facts, generated views, diagnostics,
  and Governance facts sort by documented keys.

### `text-projection`

Purpose: proves text output is a projection from the report.

Expected result:

- Text summary includes command, outcome, changed artifact count,
  clarification question count, decision count, accepted deferral count,
  remaining ambiguity count, generated-view count, diagnostic count, and next
  action from the report.
- Text mode introduces no fact absent from JSON.

### `governance-boundary`

Purpose: proves optional Governance files do not become required.

Expected result:

- Absent Governance files do not block clarification creation.
- Present Governance pointers appear only as compatibility facts.
- No route, profile, freshness, gate, protected-branch, audit, or release
  verdict is produced.

## Required Test Mapping

| Fixture | Required test focus |
|---|---|
| `clarify-create` | successful create, report, parsed facts, next action |
| `no-open-ambiguity` | ready-for-checklist or missing-spec-facts branch without invented questions |
| `clarify-rerun-preserves-decisions` | preserve/no-change rerun behavior |
| `clarify-adds-missing-sections` | safe non-destructive update |
| `clarify-preserves-stable-ids` | id stability and append-only allocation |
| `clarify-accepted-deferral` | deferral as durable visible decision |
| `outside-project` | missing initialized project diagnostic |
| `missing-specification` | specification prerequisite diagnostic |
| `missing-answer` | unresolved blocking ambiguity diagnostic |
| `malformed-work-id` | work id validation |
| `duplicate-work-id` | duplicate logical work id diagnostic |
| `malformed-clarification` | schema/front matter/section diagnostics |
| `duplicate-clarification-id` | duplicate id diagnostics |
| `unknown-ambiguity-reference` | unknown source/question reference diagnostics |
| `clarification-identity-mismatch` | blocking identity mismatch diagnostic |
| `unsafe-overwrite` | authored-content protection |
| `unsafe-decision-change` | durable decision protection |
| `stale-generated-view` | generated-view currency checks |
| `dry-run` | proposed changes without mutation |
| `deterministic-report` | byte-stable clarify JSON |
| `text-projection` | text from report only |
| `governance-boundary` | no-Governance and optional boundary behavior |
