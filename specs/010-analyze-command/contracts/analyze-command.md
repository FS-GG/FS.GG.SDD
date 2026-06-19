# Contract: Analyze Command

## Command

```text
fsgg-sdd analyze --work <work-id> [--dry-run] [--text] [--root <path>]
```

The command is also available through the command library by constructing a
`CommandRequest` with `Command = Analyze`.

## Inputs

| Input | Required | Description |
|---|---:|---|
| `--work <work-id>` | Yes | Selects the tasks-ready work item to analyze. |
| `--dry-run` | No | Produces the same report shape without mutating generated artifacts. |
| `--text` | No | Renders a human-readable projection instead of JSON. |
| `--root <path>` | No | Selects the SDD project root. Defaults to the current directory. |

The command uses the existing overwrite policy default of refusing unsafe
authored-source changes. Authored-source changes are never planned by analyze.
Generated-view refresh is allowed only when source facts are valid and dry-run
is false.

## Preconditions

The command requires:

- initialized SDD project settings;
- one valid selected work id;
- `work/<id>/spec.md` with matching work id and `stage: specify`;
- `work/<id>/clarifications.md` with matching work id and `stage: clarify`;
- `work/<id>/checklist.md` with matching work id and `stage: checklist`;
- `work/<id>/plan.md` with matching work id and `stage: plan`;
- `work/<id>/tasks.yml` with matching work id, `stage: tasks`, and current or
  correctable tasks-ready state.

If any precondition fails, the command MUST report a blocked outcome and MUST
NOT write `readiness/<id>/analysis.json`.

## Outputs

Generated outputs when refresh is valid:

```text
readiness/<id>/work-model.json
readiness/<id>/analysis.json
```

Process output:

- JSON command report by default;
- text projection when `--text` is supplied;
- non-zero exit code when outcome is blocked.

Authored lifecycle artifacts are preserved.

## Workflow Contract

The command follows the existing command workflow:

1. Load project configuration.
2. Load and validate the selected work item.
3. Load specification, clarification, checklist, plan, tasks, existing
   work-model, and existing analysis view sources.
4. Build current structured facts and source snapshots.
5. Refresh or diagnose generated work-model state.
6. Evaluate cross-artifact consistency and task-readiness findings.
7. Build analysis readiness and next action.
8. Plan generated analysis view refresh or generated-view diagnostic.
9. Interpret filesystem effects unless dry-run is set.
10. Build the authoritative command report.
11. Render JSON or text from the report.

All filesystem work is requested through command effects. Pure workflow
transitions decide whether generated writes are valid before an interpreter
mutates files.

## Success Outcome

A successful implementation-ready result MUST:

- preserve authored specification, clarification, checklist, plan, and tasks
  artifacts;
- refresh or report current `readiness/<id>/work-model.json`;
- create, update, preserve, or report no change for
  `readiness/<id>/analysis.json`;
- include analysis readiness and finding counts in the command report;
- identify implementation as the next lifecycle action;
- avoid Governance route, freshness, profile, gate, audit, evidence freshness,
  protected-boundary, or release decisions.

## Blocked Outcomes

The command blocks implementation readiness when it detects:

- outside-project execution;
- missing or malformed project settings;
- missing or malformed work id;
- missing specification, clarification, checklist, plan, or tasks prerequisite;
- selected-id mismatch;
- duplicate logical work ids;
- malformed prerequisite facts;
- unresolved blocking ambiguity;
- failed or stale checklist results;
- incomplete or stale plan decisions;
- missing task dispositions;
- stale tasks;
- duplicate task ids;
- dependency cycle;
- dependency on unknown task id;
- unknown source references;
- unsupported task states;
- completed tasks without required evidence when evidence state is recorded;
- malformed existing analysis view that prevents safe refresh;
- stale, malformed, or blocked generated-view refresh when refresh affects
  the command outcome;
- tool defects.

Blocked reports MUST include actionable diagnostics and a next action pointing
to specification, clarification, checklist, plan, tasks, or generated-view
correction.

## Dry Run

Dry run MUST:

- produce the same report shape as a real run;
- include proposed generated artifact changes;
- include diagnostics, readiness state, and next action;
- avoid mutating authored lifecycle artifacts;
- avoid mutating `readiness/<id>/work-model.json`;
- avoid mutating `readiness/<id>/analysis.json`.

## Text Projection

Text output MUST be a projection of the command report. It MUST include the
selected work id, outcome, generated analysis artifact, ready finding count,
advisory count, warning count, blocking count, stale source count, missing
disposition count, generated-view state, diagnostics, and next action when
available. It MUST NOT introduce facts absent from the JSON report.

## Governance Boundary

The command MAY expose optional Governance policy, capability, or tooling
pointers as compatibility facts. It MUST NOT parse Governance-owned schemas or
produce route, freshness, profile, gate, audit, protected-boundary, evidence
freshness, or release verdicts.
