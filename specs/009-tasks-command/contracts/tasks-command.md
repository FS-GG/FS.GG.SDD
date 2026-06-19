# Contract: Tasks Command

## Command

```text
fsgg-sdd tasks --work <work-id> [--input <text>] [--dry-run] [--text]
```

The command is also available through the command library by constructing a
`CommandRequest` with `Command = Tasks`.

## Inputs

| Input | Required | Description |
|---|---:|---|
| `--work <work-id>` | Yes | Selects the work item to turn into a task graph. |
| `--input <text>` | No | Adds task notes or accepted-deferral rationale where safe. |
| `--dry-run` | No | Produces the same report shape without mutating authored or generated artifacts. |
| `--text` | No | Renders a human-readable projection instead of JSON. |
| `--root <path>` | No | Selects the SDD project root. Defaults to the current directory. |

The command uses the existing overwrite policy default of refusing unsafe
authored-source changes. Generated-view refresh remains allowed only when the
source facts are valid.

## Preconditions

The command requires:

- initialized SDD project settings;
- one valid selected work id;
- `work/<id>/spec.md` with matching work id and `stage: specify`;
- `work/<id>/clarifications.md` with matching work id and `stage: clarify`;
- `work/<id>/checklist.md` with matching work id and `stage: checklist`;
- `work/<id>/plan.md` with matching work id, `stage: plan`, and a planned
  state;
- no blocking failed checklist results, stale checklist results, stale plan
  decisions, unknown source references, malformed prerequisite facts, or
  blocking plan findings.

If any precondition fails, the command MUST report a blocked outcome and MUST
NOT write `work/<id>/tasks.yml`.

## Outputs

Authored output:

```text
work/<id>/tasks.yml
```

Generated output when refresh is valid:

```text
readiness/<id>/work-model.json
```

Process output:

- JSON command report by default;
- text projection when `--text` is supplied;
- non-zero exit code when outcome is blocked.

## Workflow Contract

The command follows the existing command workflow:

1. Load project configuration.
2. Load and validate the selected work item.
3. Load specification, clarification, checklist, plan, and existing task
   sources.
4. Build current structured facts and source snapshots.
5. Derive required task dispositions from plan facts and source facts.
6. Validate existing and proposed task graph integrity.
7. Plan a safe authored-source write or refusal.
8. Plan generated work-model refresh or generated-view diagnostic.
9. Interpret filesystem effects unless dry-run is set.
10. Build the authoritative command report.
11. Render JSON or text from the report.

All filesystem work is requested through command effects. Pure workflow
transitions decide whether a write is safe before an interpreter mutates files.

## Success Outcome

A successful task-ready result MUST:

- create, preserve, or safely update `work/<id>/tasks.yml`;
- include a task summary and graph-readiness facts in the command report;
- preserve stable task ids and existing task state across reruns;
- include generated-view state;
- identify `analyze` as the next lifecycle action;
- avoid Governance route, freshness, profile, gate, audit, evidence freshness,
  protected-boundary, or release decisions.

## Blocked Outcomes

The command blocks before authored-source mutation when it detects:

- outside-project execution;
- missing or malformed project settings;
- missing or malformed work id;
- missing specification, clarification, checklist, or plan prerequisite;
- failed checklist readiness or failed planning state;
- selected-id mismatch;
- duplicate logical work ids;
- malformed existing task schema;
- duplicate task ids;
- dependency cycle;
- dependency on unknown task id;
- unknown source references;
- stale task entries that must be reviewed;
- unsafe task overwrite or destructive status change;
- completed tasks without required evidence;
- stale, malformed, or blocked generated-view refresh when refresh affects the
  command outcome;
- tool defects.

## Dry Run

Dry run MUST:

- produce the same report shape as a real run;
- include planned authored and generated artifact changes;
- include diagnostics and next action;
- avoid mutating `work/<id>/tasks.yml`;
- avoid mutating `readiness/<id>/work-model.json`.

## Text Projection

Text output MUST be a projection of the command report. It MUST include the
selected work id, outcome, changed artifacts, task count, dependency count,
required skill count, required evidence count, skipped task count, stale task
count, generated-view state, diagnostics, and next action when available. It
MUST NOT introduce facts absent from the JSON report.

## Governance Boundary

The command MAY expose optional Governance policy, capability, or tooling
pointers as compatibility facts. It MUST NOT parse Governance-owned schemas or
produce route, freshness, profile, gate, audit, protected-boundary, evidence
freshness, or release verdicts.
