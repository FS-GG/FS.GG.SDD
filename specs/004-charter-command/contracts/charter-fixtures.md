# Contract: Charter Fixtures

## Scope

Charter fixtures provide real filesystem-style inputs and expected outputs for
semantic tests, safe-write behavior, generated-view currency, deterministic
reports, and Governance boundary checks. Fixture data is synthetic and fixture
names disclose the scenario they represent.

Fixtures live under `tests/fixtures/lifecycle-commands/`.

## Shared Rules

- All paths are repository-relative.
- Fixture source files use current schema versions unless the scenario tests
  schema failure.
- Golden JSON uses UTF-8 without a byte order mark.
- Reports exclude timestamps, absolute host paths, terminal styling, and
  nondeterministic ordering.
- Expected diagnostics use stable ids.

## Required Fixtures

### `charter-create`

Purpose: proves an initialized SDD project can create a new work-item charter.

Expected result:

- `work/<id>/charter.md` is created with valid front matter and standard
  sections.
- Report outcome is `succeeded`.
- Next action is `specify`.
- Governance runtime is not required.

### `charter-rerun-preserves-content`

Purpose: proves rerunning charter preserves authored prose.

Expected result:

- Existing user-authored principles and boundaries remain unchanged.
- Report records `preserve`, `noChange`, or equivalent safe state.
- No destructive write occurs.

### `charter-adds-missing-sections`

Purpose: proves the command can safely complete missing standard sections.

Expected result:

- Missing standard sections are added in deterministic order.
- Existing authored sections are unchanged.
- Report names exactly which artifact changed.

### `charter-identity-mismatch`

Purpose: proves mismatched front matter blocks writes.

Expected result:

- Existing charter work id differs from selected work id.
- No write occurs.
- Report includes `charterIdentityMismatch` or equivalent blocking diagnostic.

### `duplicate-work-id`

Purpose: proves duplicate logical work ids are diagnosed.

Expected result:

- Duplicate work-item source candidates are identified before write.
- No authored charter content changes.
- Report includes a blocking duplicate-id diagnostic.

### `outside-project`

Purpose: proves charter fails clearly outside an initialized SDD project.

Expected result:

- No `work/<id>` path is created.
- Report names the missing project artifact or initialization correction.
- Outcome is `blocked`.

### `malformed-work-id`

Purpose: proves work id validation happens before writes.

Expected result:

- No work directory is created.
- Report identifies the accepted work-id shape.
- Outcome is `blocked`.

### `malformed-artifact`

Purpose: proves malformed project config or charter front matter blocks
progress.

Expected result:

- The malformed source is named.
- Generated-view refresh is blocked.
- No unsafe authored write occurs.

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

### `deterministic-report`

Purpose: proves charter JSON reports are byte-stable.

Expected result:

- Three dry-run executions over identical snapshots produce byte-identical
  reports.
- Artifact changes, generated views, diagnostics, and Governance facts sort by
  documented keys.

### `text-projection`

Purpose: proves text output is a projection from the report.

Expected result:

- Text summary includes command, outcome, changed artifact count,
  generated-view count, diagnostic count, and next action from the report.
- Text mode introduces no fact absent from JSON.

### `governance-boundary`

Purpose: proves optional Governance files do not become required.

Expected result:

- Absent Governance files do not block charter creation.
- Present Governance pointers appear only as compatibility facts.
- No route, profile, freshness, gate, protected-branch, audit, or release
  verdict is produced.

## Required Test Mapping

| Fixture | Required test focus |
|---|---|
| `charter-create` | successful create, report, next action |
| `charter-rerun-preserves-content` | preserve/no-change rerun behavior |
| `charter-adds-missing-sections` | safe non-destructive update |
| `charter-identity-mismatch` | blocking identity mismatch diagnostic |
| `duplicate-work-id` | duplicate logical id diagnostic |
| `outside-project` | missing initialized project diagnostic |
| `malformed-work-id` | work id validation |
| `malformed-artifact` | schema/front matter diagnostics |
| `unsafe-overwrite` | authored-content protection |
| `stale-generated-view` | generated-view currency checks |
| `deterministic-report` | byte-stable charter JSON |
| `text-projection` | text from report only |
| `governance-boundary` | no-Governance and optional boundary behavior |
