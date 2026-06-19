# Contract: Specify Fixtures

## Scope

Specify fixtures provide real filesystem-style inputs and expected outputs for
semantic tests, safe-write behavior, generated-view currency, dry-run behavior,
deterministic reports, and Governance boundary checks. Fixture data is
synthetic and fixture names disclose the scenario they represent.

Fixtures live under `tests/fixtures/lifecycle-commands/`.

## Shared Rules

- All paths are repository-relative.
- Fixture source files use current schema versions unless the scenario tests
  schema failure.
- Golden JSON uses UTF-8 without a byte order mark.
- Reports exclude timestamps, durations, absolute host paths, terminal
  styling, and nondeterministic ordering.
- Expected diagnostics use stable ids.
- Every successful specify fixture includes a valid `work/<id>/charter.md`
  prerequisite.

## Required Fixtures

### `specify-create`

Purpose: proves an initialized SDD project with a chartered work item can
create a new specification.

Expected result:

- `work/<id>/spec.md` is created with valid front matter and standard sections.
- At least one `FR-###` requirement is present.
- Report outcome is `succeeded`.
- Next action is `clarify`.
- Governance runtime is not required.

### `specify-rerun-preserves-content`

Purpose: proves rerunning specify preserves authored prose.

Expected result:

- Existing user-authored value, scope, stories, requirements, non-goals,
  acceptance scenarios, and ambiguity records remain unchanged.
- Report records `preserve`, `noChange`, or equivalent safe state.
- No destructive write occurs.

### `specify-adds-missing-sections`

Purpose: proves the command can safely complete missing standard sections.

Expected result:

- Missing standard sections are added in deterministic order.
- Existing authored sections are unchanged.
- Report names exactly which artifact changed.

### `specify-preserves-stable-ids`

Purpose: proves reruns do not renumber existing specification ids.

Expected result:

- Existing `US-###`, `AC-###`, `FR-###`, `SB-###`, and `AMB-###` ids remain
  unchanged.
- New ids, if any, use the next available suffix.
- The report identifies preserved and added ids.

### `missing-charter`

Purpose: proves specify requires the charter prerequisite.

Expected result:

- No `spec.md` path is created.
- Report includes `missingCharterPrerequisite` or equivalent blocking
  diagnostic.
- Next action points to correction rather than `clarify`.

### `missing-intent`

Purpose: proves new specifications require enough user intent.

Expected result:

- No `spec.md` path is created.
- Report identifies missing user value, scope, or measurable requirement input.
- Outcome is `blocked`.

### `malformed-work-id`

Purpose: proves work id validation happens before writes.

Expected result:

- No work directory is created.
- Report identifies the accepted work-id shape.
- Outcome is `blocked`.

### `malformed-specification`

Purpose: proves malformed specification front matter or section/id data blocks
progress.

Expected result:

- The malformed source is named.
- Generated-view refresh is blocked.
- No unsafe authored write occurs.

### `duplicate-spec-id`

Purpose: proves duplicate story, requirement, acceptance scenario, scope, or
ambiguity ids are diagnosed.

Expected result:

- Duplicate ids are identified before write.
- Existing specification content is unchanged.
- Report includes a blocking duplicate-id diagnostic.

### `specification-identity-mismatch`

Purpose: proves mismatched front matter blocks writes.

Expected result:

- Existing specification work id differs from selected work id.
- No write occurs.
- Report includes `specificationIdentityMismatch` or equivalent blocking
  diagnostic.

### `unsafe-overwrite`

Purpose: proves authored content is not clobbered.

Expected result:

- Planned conflicting write is refused.
- Existing file content remains byte-for-byte unchanged.
- Report includes before digest, diagnostic id, and correction guidance.

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
- No `spec.md` or refreshed generated view is written.
- Re-running without dry-run over the same input produces the planned writes.

### `deterministic-report`

Purpose: proves specify JSON reports are byte-stable.

Expected result:

- Three dry-run executions over identical snapshots produce byte-identical
  reports.
- Artifact changes, parsed specification facts, generated views, diagnostics,
  and Governance facts sort by documented keys.

### `text-projection`

Purpose: proves text output is a projection from the report.

Expected result:

- Text summary includes command, outcome, changed artifact count,
  specification id counts, unresolved ambiguity count, generated-view count,
  diagnostic count, and next action from the report.
- Text mode introduces no fact absent from JSON.

### `governance-boundary`

Purpose: proves optional Governance files do not become required.

Expected result:

- Absent Governance files do not block specification creation.
- Present Governance pointers appear only as compatibility facts.
- No route, profile, freshness, gate, protected-branch, audit, or release
  verdict is produced.

## Required Test Mapping

| Fixture | Required test focus |
|---|---|
| `specify-create` | successful create, report, parsed facts, next action |
| `specify-rerun-preserves-content` | preserve/no-change rerun behavior |
| `specify-adds-missing-sections` | safe non-destructive update |
| `specify-preserves-stable-ids` | id stability and append-only allocation |
| `missing-charter` | charter prerequisite diagnostic |
| `missing-intent` | minimum intent validation |
| `malformed-work-id` | work id validation |
| `malformed-specification` | schema/front matter/section diagnostics |
| `duplicate-spec-id` | duplicate id diagnostics |
| `specification-identity-mismatch` | blocking identity mismatch diagnostic |
| `unsafe-overwrite` | authored-content protection |
| `stale-generated-view` | generated-view currency checks |
| `dry-run` | proposed changes without mutation |
| `deterministic-report` | byte-stable specify JSON |
| `text-projection` | text from report only |
| `governance-boundary` | no-Governance and optional boundary behavior |
