# Contract: `fsgg-sdd checklist`

## Scope

`fsgg-sdd checklist` creates or safely updates the work-item
requirements-quality checklist artifact after the work item has a valid
specification and clarification artifact. The command emits the same
deterministic command report shape as other native SDD commands and renders
text only as a projection from that report.

## Invocation

```bash
fsgg-sdd checklist --root <project-root> --work <work-id> [--input <review-notes>] [--dry-run] [--text]
```

Defaults:

- `--root` defaults to `.`.
- JSON output is the default.
- `--text` selects the human projection.
- `--dry-run` plans effects and reports changes without writing authored or
  generated artifacts.
- The overwrite policy is `refuseUnsafe`.
- `--input` is optional and may provide review notes or accepted-deferral
  rationale. Source-derived requirements-quality failures are still evaluated
  from the selected specification and clarification facts.

## Prerequisites

Required before write planning:

- `.fsgg/project.yml` exists and parses.
- `.fsgg/sdd.yml` exists and parses.
- `.fsgg/agents.yml` exists and parses.
- `--work` is present and valid.
- `work/<id>/spec.md` exists and has valid specification front matter for the
  selected work id.
- `work/<id>/clarifications.md` exists and has valid clarification front
  matter for the selected work id and source specification.
- The specification facts are parseable enough to identify requirements,
  stories, acceptance scenarios, success criteria, edge cases, assumptions,
  scope boundaries, and ambiguity records.
- The clarification facts are parseable enough to identify questions,
  decisions, accepted deferrals, and remaining ambiguity.
- Existing `work/<id>/checklist.md`, when present, has readable content and
  matching checklist identity.

Failure behavior:

- Missing project settings produce a blocked report and no write effects.
- Malformed project settings produce a blocked report and no write effects.
- Missing or malformed work id produces a blocked report and no write effects.
- Missing or mismatched specification prerequisite blocks before checklist
  writes.
- Missing or mismatched clarification prerequisite blocks before checklist
  writes.
- Unresolved blocking ambiguity in clarification facts blocks checklist
  readiness and prevents advancing to planning.
- Existing checklist identity mismatch blocks before writes.

## Workflow

1. Normalize request options.
2. Load project settings from `.fsgg/`.
3. Validate selected work id.
4. Load selected specification, clarification, existing checklist, later
   lifecycle source snapshots, existing work-model view, and duplicate-id
   candidates.
5. Validate specification and clarification prerequisites and parsed source
   facts.
6. Normalize optional checklist review input.
7. Evaluate requirements-quality checks for testability, measurable outcomes,
   acceptance-scenario coverage, edge-case coverage, scope boundaries,
   dependency assumptions, remaining ambiguity, and absence of planning detail
   in user-facing specification text.
8. Build the proposed checklist artifact or safe update plan.
9. Plan generated-view refresh or generated-view diagnostics.
10. Interpret effects at the edge unless `--dry-run` is set.
11. Build the command report.
12. Render JSON or text from the report.

The pure workflow may request additional effects after read effects have been
interpreted. The CLI host must continue dispatching effects until the workflow
reaches a final report.

## Successful Checklist-Ready Result

A successful checklist-ready create or safe update:

- Creates or updates `work/<id>/checklist.md`.
- Records `stage: checklist` in checklist front matter.
- Preserves existing authored prose, checklist items, review results, accepted
  deferrals, findings, notes, and stable ids.
- Lists the checklist artifact in `changedArtifacts`.
- Lists generated work-model state in `generatedViews`.
- Emits no error diagnostics.
- Reports outcome `succeeded`, `succeededWithWarnings`, or `noChange`.
- Sets next action to `plan` for the selected work id when no blocking
  checklist findings or stale results remain.
- Does not require Governance files or runtime.

## Failed Requirements-Quality Result

A valid checklist request with failed blocking quality checks:

- Creates or safely updates `work/<id>/checklist.md` with failed review
  results and correction guidance.
- Preserves existing authored content and stable ids.
- Reports outcome `succeededWithWarnings` or `blocked`, according to the
  existing command outcome policy for non-advancing lifecycle state.
- Includes checklist findings and diagnostics that identify the source facts to
  correct.
- Sets next action to `correctBlockingDiagnostics`, specification correction,
  clarification correction, or checklist review correction.
- Does not set next action to `plan`.

Failed quality is an authored checklist output. It is not treated like
malformed input unless the source or existing checklist data is unsafe to read
or update.

## Blocked Result

A blocked result:

- Performs no unsafe authored writes.
- Leaves existing checklist content unchanged unless the only planned update is
  a proven-safe result from valid source facts.
- Includes at least one error diagnostic with a correction.
- Points next action to `correctBlockingDiagnostics` or the prerequisite
  lifecycle command that must be corrected.
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
or blocked checklist request after project/work-id validation.

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

- `fsgg-sdd plan`, `tasks`, or `analyze` behavior;
- task/evidence update commands;
- `fsgg-sdd verify` or `fsgg-sdd ship`;
- generated agent command or skill files;
- Governance route selection, evidence freshness, profiles, gates,
  protected-boundary verdicts, audit reports, or release policy.
