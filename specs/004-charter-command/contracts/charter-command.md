# Contract: `fsgg-sdd charter`

## Scope

`fsgg-sdd charter` creates or safely updates the first work-item lifecycle
artifact after project initialization. The command emits the same deterministic
command report shape as other native SDD commands and renders text only as a
projection from that report.

## Invocation

```bash
fsgg-sdd charter --root <project-root> --work <work-id> [--title <title>] [--dry-run] [--text]
```

Defaults:

- `--root` defaults to `.`.
- JSON output is the default.
- `--text` selects the human projection.
- `--dry-run` plans effects and reports changes without writing.
- The overwrite policy is `refuseUnsafe`.

## Prerequisites

Required before write planning:

- `.fsgg/project.yml` exists and parses.
- `.fsgg/sdd.yml` exists and parses.
- `.fsgg/agents.yml` exists and parses.
- `--work` is present and valid.
- Existing `work/<id>/charter.md`, when present, has readable content and
  matching charter identity.

Failure behavior:

- Missing project settings produce a blocked report and no write effects.
- Malformed project settings produce a blocked report and no write effects.
- Missing or malformed work id produces a blocked report and no write effects.
- Existing charter identity mismatch blocks before writes.

## Workflow

1. Normalize request options.
2. Load project settings from `.fsgg/`.
3. Validate selected work id.
4. Load existing charter snapshot when present.
5. Build the proposed charter artifact.
6. Plan a safe create, no-change, preserve, safe-section-addition, or refused
   write.
7. Plan generated-view refresh or generated-view diagnostics.
8. Interpret effects at the edge unless `--dry-run` is set.
9. Build the command report.
10. Render JSON or text from the report.

The pure workflow may request additional effects after read effects have been
interpreted. The CLI host must continue dispatching effects until the workflow
reaches a final report.

## Successful Result

A successful create or safe update:

- Creates or updates `work/<id>/charter.md`.
- Records `stage: charter` in charter front matter.
- Lists the charter artifact in `changedArtifacts`.
- Lists generated work-model state in `generatedViews`.
- Emits no error diagnostics.
- Reports outcome `succeeded`, `succeededWithWarnings`, or `noChange`.
- Sets next action to `specify` for the selected work id.
- Does not require Governance files or runtime.

## Blocked Result

A blocked result:

- Performs no unsafe authored writes.
- Leaves existing charter content unchanged.
- Includes at least one error diagnostic with a correction.
- Points next action to `correctBlockingDiagnostics`.
- May include generated-view currency as `blocked`, `missing`, `stale`, or
  `malformed`.

## Generated-View Behavior

The command reports `readiness/<id>/work-model.json` state on every successful
or blocked charter request after project/work-id validation.

Allowed currency values:

- `current`: source data was valid and the view was refreshed or already
  current.
- `missing`: no generated file exists and current output cannot yet be created.
- `stale`: an existing generated file has stale source digest or generator
  version.
- `malformed`: an existing generated file cannot be parsed.
- `blocked`: source diagnostics prevent refresh.

The command must not treat generated file presence as proof of currency.

## Explicit Non-Responsibilities

This feature does not introduce:

- `fsgg-sdd specify`, `clarify`, `checklist`, `plan`, `tasks`, or `analyze`
  behavior;
- task/evidence update commands;
- `fsgg-sdd verify` or `fsgg-sdd ship`;
- generated agent command or skill files;
- Governance route selection, evidence freshness, profiles, gates,
  protected-boundary verdicts, or release policy.
