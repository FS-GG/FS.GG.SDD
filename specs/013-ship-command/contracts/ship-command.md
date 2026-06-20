# Contract: Ship Command

## Command

```text
fsgg-sdd ship --work <work-id> [--dry-run] [--text] [--root <path>]
```

The command is also available through the command library by constructing a
`CommandRequest` with `Command = Ship`.

## Inputs

| Input | Required | Description |
|---|---:|---|
| `--work <work-id>` | Yes | Selects the verification-ready work item to ship. |
| `--dry-run` | No | Produces the same report shape without mutating generated artifacts. |
| `--text` | No | Renders a human-readable projection instead of JSON. |
| `--root <path>` | No | Selects the SDD project root. Defaults to the current directory. |

The command uses the existing overwrite policy default of refusing unsafe
authored-source changes. Authored-source changes are never planned by ship.
Generated-view refresh is allowed only when source facts are valid and dry-run is
false.

## Preconditions

The command requires:

- initialized SDD project settings;
- one valid selected work id;
- `work/<id>/spec.md` with matching work id and `stage: specify`;
- `work/<id>/clarifications.md` with matching work id and `stage: clarify`;
- `work/<id>/checklist.md` with matching work id and `stage: checklist`;
- `work/<id>/plan.md` with matching work id and `stage: plan`;
- `work/<id>/tasks.yml` with matching work id and `stage: tasks`;
- `work/<id>/evidence.yml` with matching work id and `stage: evidence`;
- `readiness/<id>/analysis.json` with matching work id and an implementation
  readiness state that permits ship;
- `readiness/<id>/verify.json` with matching work id, schema version 1, and a
  verification-ready status with no unresolved blocking findings;
- existing `readiness/<id>/ship.json`, when present, must parse as schema
  version 1 and match the selected work id.

If any required precondition fails, the command MUST report a blocked outcome and
MUST NOT write `readiness/<id>/ship.json`.

## Outputs

Generated outputs when refresh is valid:

```text
readiness/<id>/work-model.json
readiness/<id>/ship.json
```

Process output:

- JSON command report by default;
- text projection when `--text` is supplied;
- non-zero exit code when outcome is blocked.

Authored specification, clarification, checklist, plan, tasks, and evidence
artifacts are preserved. The verification view is preserved.

## Workflow Contract

The command follows the existing command workflow:

1. Load project configuration.
2. Load and validate the selected work item.
3. Load specification, clarification, checklist, plan, tasks, analysis, evidence,
   existing work-model, verification view, and existing ship view sources.
4. Build current structured facts and source snapshots.
5. Evaluate the verification prerequisite: schema version, work-id match,
   verification readiness, and currency against current sources.
6. Aggregate lifecycle stage readiness from the normalized work model, analysis
   view, and verification view without re-deriving verify-owned dispositions.
7. Refresh or diagnose generated work-model state.
8. Evaluate analysis and verification prerequisite currency and ship findings.
9. Build the single ship-readiness disposition and next action.
10. Plan generated ship view refresh or generated-view diagnostic.
11. Interpret filesystem effects unless dry-run is set.
12. Build the authoritative command report.
13. Render JSON or text from the report.

All filesystem work is requested through command effects. Pure workflow
transitions decide whether generated writes are valid before an interpreter
mutates files.

## Success Outcome

A successful ship-ready result MUST:

- preserve authored specification, clarification, checklist, plan, tasks, and
  evidence artifacts and the verification view;
- refresh or report current `readiness/<id>/work-model.json`;
- create, update, preserve, or report no change for `readiness/<id>/ship.json`;
- include aggregated lifecycle stage readiness, verification readiness, evidence
  disposition counts, the ship-readiness disposition, and finding counts in the
  command report;
- identify the protected-boundary handoff as the next lifecycle action through
  action id `ship.next.protectedBoundary` without implementing protected-boundary
  enforcement and with a null command pointer;
- avoid Governance route, freshness, profile, gate, audit, effective evidence,
  protected-boundary, or release decisions.

## Blocked Outcomes

The command blocks ship readiness when it detects:

- outside-project execution;
- missing or malformed project settings;
- missing or malformed work id;
- missing specification, clarification, checklist, plan, tasks, analysis,
  evidence, or verification prerequisite;
- selected-id mismatch;
- duplicate logical work ids;
- malformed prerequisite facts;
- analysis not implementation-ready or stale analysis;
- verification view missing, malformed, stale, mismatched, or not
  verification-ready;
- unresolved blocking verification findings surfaced by the verification view;
- unknown task, requirement, evidence declaration, generated view, or source
  reference in aggregated facts;
- stale or missing evidence relative to the verification view snapshot;
- synthetic evidence without disclosure surfaced by the verification view;
- accepted deferral no longer visible or no longer accepted at ship time;
- malformed existing ship view that prevents safe refresh;
- stale, malformed, or blocked generated-view refresh when refresh affects the
  command outcome;
- tool defects.

Blocked reports MUST include actionable diagnostics and a next action pointing to
verification rerun, evidence correction, prerequisite lifecycle correction,
generated-view refresh, or stale-source correction.

## Dry Run

Dry run MUST:

- produce the same report shape as a real run;
- include proposed generated artifact changes;
- include diagnostics, ship readiness state, and next action;
- avoid mutating authored lifecycle artifacts;
- avoid mutating `readiness/<id>/work-model.json`;
- avoid mutating `readiness/<id>/ship.json`.

## Text Projection

Text output MUST be a projection of the command report. It MUST include the
selected work id, outcome, generated ship artifact, ready finding count, advisory
count, warning count, blocking count, lifecycle stage readiness states,
verification readiness state, evidence disposition counts, generated-view state,
diagnostics, and next action when available. It MUST NOT introduce facts absent
from the JSON report.

## Governance Boundary

The command MAY expose optional Governance policy, capability, or tooling
pointers as compatibility facts. It MUST NOT parse Governance-owned schemas or
produce route, freshness, profile, gate, audit, protected-boundary, effective
evidence, or release verdicts. The protected-boundary handoff named as the next
action is an advisory pointer, not an SDD enforcement action.
