# Contract: Plan Command

## Command

```text
fsgg-sdd plan --work <work-id> [--input <text>] [--dry-run] [--text]
```

The command is also available through the command library by constructing a
`CommandRequest` with `Command = Plan`.

## Inputs

| Input | Required | Description |
|---|---:|---|
| `--work <work-id>` | Yes | Selects the work item to plan. |
| `--input <text>` | No | Adds planning notes or accepted-deferral rationale where safe. |
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
- `work/<id>/checklist.md` with matching work id, `stage: checklist`, and a
  checklist-ready state;
- no blocking failed checklist results, stale checklist results, unknown source
  references, or malformed prerequisite facts.

If any precondition fails, the command MUST report a blocked outcome and MUST
NOT write `work/<id>/plan.md`.

## Outputs

Authored output:

```text
work/<id>/plan.md
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
3. Load specification, clarification, checklist, and existing plan sources.
4. Build current structured facts and source snapshots.
5. Plan a safe authored-source write or refusal.
6. Plan generated work-model refresh or generated-view diagnostic.
7. Interpret filesystem effects unless dry-run is set.
8. Build the authoritative command report.
9. Render JSON or text from the report.

All filesystem work is requested through command effects. Pure workflow
transitions decide whether a write is safe before an interpreter mutates files.

## Success Outcome

A successful planned result MUST:

- create, preserve, or safely update `work/<id>/plan.md`;
- include a plan summary in the command report;
- preserve stable plan ids across reruns;
- include generated-view state;
- identify `tasks` as the next lifecycle action;
- avoid Governance route, freshness, profile, gate, audit, or enforcement
  decisions.

## Blocked Outcomes

The command blocks before authored-source mutation when it detects:

- outside-project execution;
- missing or malformed project settings;
- missing or malformed work id;
- missing specification, clarification, or checklist prerequisite;
- failed or stale checklist readiness;
- selected-id mismatch;
- duplicate logical work ids;
- malformed existing plan front matter;
- duplicate plan ids;
- unknown source references;
- stale plan decisions that must be reviewed;
- unsafe plan decision changes;
- stale, malformed, or blocked generated-view refresh when refresh affects the
  command outcome;
- tool defects.

## Dry Run

Dry run MUST:

- produce the same report shape as a real run;
- include planned authored and generated artifact changes;
- include diagnostics and next action;
- avoid mutating `work/<id>/plan.md`;
- avoid mutating `readiness/<id>/work-model.json`.

## Text Projection

Text output MUST be a projection of the command report. It MUST include the
selected work id, outcome, changed artifacts, plan decision count, contract
reference count, verification obligation count, accepted deferral count, stale
decision count, generated-view state, diagnostics, and next action when
available. It MUST NOT introduce facts absent from the JSON report.

## Governance Boundary

The command MAY expose optional Governance policy, capability, or tooling
pointers as compatibility facts. It MUST NOT parse Governance-owned schemas or
produce route, freshness, profile, gate, audit, protected-boundary, or release
verdicts.
