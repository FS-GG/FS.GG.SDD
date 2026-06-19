# Contract: Fixture Catalog

## Scope

Command fixtures provide real filesystem-style inputs for semantic tests,
golden command reports, safe-write behavior, generated-view currency, and
Governance boundary checks. Fixture data is synthetic by necessity, and fixture
names disclose the scenario they represent.

Fixtures live under `tests/fixtures/lifecycle-commands/` unless implementation
tasks choose a narrower path that keeps the same scenario names and contracts.

## Shared Fixture Rules

- All paths are repository-relative.
- Fixture source files use current schema versions unless the scenario is about
  schema failure.
- Golden JSON uses UTF-8 without a byte order mark.
- Reports exclude timestamps, absolute host paths, terminal styling, and
  nondeterministic ordering.
- Fixture expected diagnostics use stable ids.

## `init-empty-project`

Purpose: proves `fsgg-sdd init` can create a minimum SDD skeleton in an empty
target.

Expected result:

- `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `work/`, and
  `readiness/` are planned or created.
- Claude and Codex guidance targets are created or reported as safe targets.
- No Governance files are required.
- Command report outcome is `succeeded`.

## `init-preserves-user-files`

Purpose: proves unrelated files survive initialization.

Expected result:

- Existing unrelated files are reported as preserved or absent from changes.
- SDD skeleton artifacts are created.
- No overwrite diagnostics are emitted.

## `init-conflicting-lifecycle-path`

Purpose: proves init refuses unsafe lifecycle path overwrites.

Expected result:

- Existing conflicting `.fsgg` or guidance files are not overwritten.
- `unsafeOverwrite` or equivalent command diagnostic is emitted.
- Command report outcome is `blocked`.

## `lifecycle-through-analysis`

Purpose: proves one work item can advance from charter through analyze.

Expected result:

- Each command produces the expected authored artifact.
- `work-model.json` is current after valid lifecycle sources exist.
- `analyze` emits `analysis.json`.
- Final report has no blocking diagnostics and points to implementation
  planning.

## `outside-project`

Purpose: proves lifecycle commands after init fail clearly outside an SDD
project.

Expected result:

- No write effects are requested.
- Diagnostic identifies missing `.fsgg/project.yml` or equivalent project root.
- Outcome is `blocked`.

## `malformed-work-id`

Purpose: proves work id validation happens before writes.

Expected result:

- No `work/<id>` path is created.
- Diagnostic identifies the malformed work id and expected correction.
- Outcome is `blocked`.

## `missing-prerequisites`

Purpose: proves later commands report missing prior lifecycle artifacts.

Expected result:

- `clarify`, `checklist`, `plan`, `tasks`, or `analyze` identifies the missing
  prerequisite artifact.
- No guessed content is written for missing prior stages.
- Next action points to the missing prerequisite command.

## `malformed-artifact`

Purpose: proves malformed front matter or YAML blocks command progress.

Expected result:

- Existing diagnostics such as `malformedSchemaVersion` are reused where
  applicable.
- Generated-view refresh is blocked.
- The report names the artifact that must be fixed.

## `unsafe-overwrite`

Purpose: proves command updates do not clobber user-authored content.

Expected result:

- Planned write is refused.
- Report includes before/proposed digests and correction guidance.
- No authored file content changes.

## `unknown-reference`

Purpose: proves structured references are validated.

Expected result:

- Unknown requirement, decision, task, evidence, artifact, or skill references
  emit stable diagnostics.
- `work-model.json` refresh is blocked or stale as appropriate.

## `stale-generated-view`

Purpose: proves stale generated files are detected by source digest or
generator version.

Expected result:

- Existing generated file presence is not treated as currency.
- Report emits stale generated-view diagnostics and expected correction.

## `deterministic-report`

Purpose: proves JSON reports are byte-stable.

Expected result:

- Three dry-run executions over identical snapshots produce byte-identical
  reports.
- Artifact changes, generated views, diagnostics, and Governance facts sort by
  documented keys.

## `text-projection`

Purpose: proves plain text output is derived from command report facts.

Expected result:

- Text summary includes command, outcome, changed artifacts, diagnostics, and
  next action from the report.
- Text mode introduces no fact absent from the JSON report.

## `governance-boundary`

Purpose: proves optional Governance files do not become required.

Expected result:

- Absent Governance files do not block SDD-only commands.
- Present Governance pointers appear only as compatibility facts.
- Malformed Governance files are reported as optional boundary issues unless an
  SDD source explicitly requires the boundary.
- No route, profile, freshness, gate, or enforcement verdict is produced.

## Required Test Mapping

| Fixture | Required test focus |
|---|---|
| `init-empty-project` | initialization smoke test and command report golden JSON |
| `init-preserves-user-files` | safe-write preservation |
| `init-conflicting-lifecycle-path` | unsafe overwrite refusal |
| `lifecycle-through-analysis` | end-to-end lifecycle command progression |
| `outside-project` | missing project diagnostic |
| `malformed-work-id` | work id validation |
| `missing-prerequisites` | lifecycle prerequisite diagnostics |
| `malformed-artifact` | schema and parser diagnostics |
| `unsafe-overwrite` | authored-content protection |
| `unknown-reference` | cross-artifact reference diagnostics |
| `stale-generated-view` | generated-view currency checks |
| `deterministic-report` | byte-stable JSON |
| `text-projection` | text from report only |
| `governance-boundary` | no-Governance and optional boundary behavior |
