# Contract: `fsgg-sdd clarify`

## Scope

`fsgg-sdd clarify` creates or safely updates the work-item clarification
artifact after the work item has a valid specification. The command emits the
same deterministic command report shape as other native SDD commands and
renders text only as a projection from that report.

## Invocation

```bash
fsgg-sdd clarify --root <project-root> --work <work-id> --input <clarification-answers> [--dry-run] [--text]
```

Defaults:

- `--root` defaults to `.`.
- JSON output is the default.
- `--text` selects the human projection.
- `--dry-run` plans effects and reports changes without writing authored or
  generated artifacts.
- The overwrite policy is `refuseUnsafe`.

`--input` is required when unresolved blocking ambiguity remains. Existing
complete clarification artifacts may be rerun without input and report
`noChange` when safe.

## Prerequisites

Required before write planning:

- `.fsgg/project.yml` exists and parses.
- `.fsgg/sdd.yml` exists and parses.
- `.fsgg/agents.yml` exists and parses.
- `--work` is present and valid.
- `work/<id>/spec.md` exists and has valid specification front matter for the
  selected work id.
- The specification facts are parseable enough to identify ambiguity,
  requirement, story, acceptance-scenario, and scope ids used by answers.
- Existing `work/<id>/clarifications.md`, when present, has readable content
  and matching clarification identity.
- New clarifications receive enough answer input to resolve, defer, or
  explicitly leave blocking questions open.

Failure behavior:

- Missing project settings produce a blocked report and no write effects.
- Malformed project settings produce a blocked report and no write effects.
- Missing or malformed work id produces a blocked report and no write effects.
- Missing or mismatched specification prerequisite blocks before clarification
  writes.
- Missing answers for blocking ambiguity block before clarification writes.
- Existing clarification identity mismatch blocks before writes.

## Workflow

1. Normalize request options.
2. Load project settings from `.fsgg/`.
3. Validate selected work id.
4. Load selected specification, existing clarification, later lifecycle source
   snapshots, existing work-model view, and duplicate-id candidates.
5. Validate specification prerequisite and parsed source facts.
6. Normalize clarification answers.
7. Build the proposed clarification artifact or safe update plan.
8. Plan generated-view refresh or generated-view diagnostics.
9. Interpret effects at the edge unless `--dry-run` is set.
10. Build the command report.
11. Render JSON or text from the report.

The pure workflow may request additional effects after read effects have been
interpreted. The CLI host must continue dispatching effects until the workflow
reaches a final report.

## Successful Result

A successful create or safe update:

- Creates or updates `work/<id>/clarifications.md`.
- Records `stage: clarify` in clarification front matter.
- Preserves existing authored prose, answers, decisions, accepted deferrals,
  and stable ids.
- Lists the clarification artifact in `changedArtifacts`.
- Lists generated work-model state in `generatedViews`.
- Emits no error diagnostics.
- Reports outcome `succeeded`, `succeededWithWarnings`, or `noChange`.
- Sets next action to `checklist` for the selected work id when no blocking
  ambiguity remains.
- Does not require Governance files or runtime.

## Blocked Result

A blocked result:

- Performs no unsafe authored writes.
- Leaves existing clarification content unchanged.
- Includes at least one error diagnostic with a correction.
- Points next action to `correctBlockingDiagnostics` or additional
  clarification.
- May include generated-view currency as `blocked`, `missing`, `stale`, or
  `malformed`.

## Dry-Run Result

A dry-run result:

- Plans the same reads, diagnostics, proposed authored changes, generated-view
  state, and next action as a non-dry-run result.
- Performs no `WriteFile` or `CreateDirectory` mutations at the edge.
- Reports proposed artifact changes with a safe-write decision such as
  `dryRunOnly` or the existing equivalent value.
- Produces deterministic JSON for identical input state.

## Generated-View Behavior

The command reports `readiness/<id>/work-model.json` state on every successful
or blocked clarify request after project/work-id validation.

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

- `fsgg-sdd checklist`, `plan`, `tasks`, or `analyze` behavior;
- task/evidence update commands;
- `fsgg-sdd verify` or `fsgg-sdd ship`;
- generated agent command or skill files;
- Governance route selection, evidence freshness, profiles, gates,
  protected-boundary verdicts, audit reports, or release policy.
