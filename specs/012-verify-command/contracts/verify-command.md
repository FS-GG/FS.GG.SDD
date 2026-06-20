# Contract: Verify Command

## Command

```text
fsgg-sdd verify --work <work-id> [--dry-run] [--text] [--root <path>]
```

The command is also available through the command library by constructing a
`CommandRequest` with `Command = Verify`.

## Inputs

| Input | Required | Description |
|---|---:|---|
| `--work <work-id>` | Yes | Selects the evidence-ready work item to verify. |
| `--dry-run` | No | Produces the same report shape without mutating generated artifacts. |
| `--text` | No | Renders a human-readable projection instead of JSON. |
| `--root <path>` | No | Selects the SDD project root. Defaults to the current directory. |

The command uses the existing overwrite policy default of refusing unsafe
authored-source changes. Authored-source changes are never planned by verify.
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
- `work/<id>/tasks.yml` with matching work id and `stage: tasks`;
- `work/<id>/evidence.yml` with matching work id and `stage: evidence`;
- `readiness/<id>/analysis.json` with matching work id and an implementation
  readiness state that permits verification;
- existing `readiness/<id>/verify.json`, when present, must parse as schema
  version 1 and match the selected work id.

If any required precondition fails, the command MUST report a blocked outcome
and MUST NOT write `readiness/<id>/verify.json`.

## Outputs

Generated outputs when refresh is valid:

```text
readiness/<id>/work-model.json
readiness/<id>/verify.json
```

Process output:

- JSON command report by default;
- text projection when `--text` is supplied;
- non-zero exit code when outcome is blocked.

Authored specification, clarification, checklist, plan, tasks, and evidence
artifacts are preserved.

## Workflow Contract

The command follows the existing command workflow:

1. Load project configuration.
2. Load and validate the selected work item.
3. Load specification, clarification, checklist, plan, tasks, analysis,
   evidence, existing work-model, and existing verification view sources.
4. Build current structured facts and source snapshots.
5. Validate task graph structure, dependencies, ids, owners, source links,
   required skills, required evidence, required tests, and status transitions.
6. Derive required test, evidence, skill, and generated-view obligations from
   task, plan, analysis, evidence, and lifecycle rule facts.
7. Match declarations to obligations and build evidence, required-test, and
   skill-visibility dispositions.
8. Refresh or diagnose generated work-model state.
9. Evaluate analysis prerequisite currency and verification findings.
10. Build verification readiness and next action.
11. Plan generated verification view refresh or generated-view diagnostic.
12. Interpret filesystem effects unless dry-run is set.
13. Build the authoritative command report.
14. Render JSON or text from the report.

All filesystem work is requested through command effects. Pure workflow
transitions decide whether generated writes are valid before an interpreter
mutates files.

## Success Outcome

A successful verification-ready result MUST:

- preserve authored specification, clarification, checklist, plan, tasks, and
  evidence artifacts;
- refresh or report current `readiness/<id>/work-model.json`;
- create, update, preserve, or report no change for
  `readiness/<id>/verify.json`;
- include verification readiness, task/evidence/test/skill dispositions, and
  finding counts in the command report;
- identify `ship` as the next lifecycle action through action id
  `verify.next.ship` without implementing ship;
- avoid Governance route, freshness, profile, gate, audit, effective evidence,
  protected-boundary, or release decisions.

## Blocked Outcomes

The command blocks verification readiness when it detects:

- outside-project execution;
- missing or malformed project settings;
- missing or malformed work id;
- missing specification, clarification, checklist, plan, tasks, analysis, or
  evidence prerequisite;
- selected-id mismatch;
- duplicate logical work ids;
- malformed prerequisite facts;
- analysis not implementation-ready or stale analysis;
- failed analysis or failed task graph validation;
- duplicate task ids, dependency cycle, dependency on unknown task id,
  unsupported task status, missing owner, or missing requirement link;
- unknown task, requirement, acceptance scenario, clarification decision,
  checklist result, plan decision, evidence obligation, test obligation,
  generated view, or source reference;
- completed tasks without required evidence or accepted deferral;
- missing or stale required tests;
- missing required skills or capability tags for claimed completed work;
- stale or missing evidence;
- synthetic evidence without disclosure;
- accepted deferral without rationale, owner, scope, or later lifecycle
  visibility;
- malformed existing verification view that prevents safe refresh;
- stale, malformed, or blocked generated-view refresh when refresh affects the
  command outcome;
- tool defects.

Blocked reports MUST include actionable diagnostics and a next action pointing
to implementation continuation, evidence correction, task correction, analysis
rerun, generated-view refresh, missing-skill correction, required-test
correction, or prerequisite lifecycle correction.

## Dry Run

Dry run MUST:

- produce the same report shape as a real run;
- include proposed generated artifact changes;
- include diagnostics, verification readiness state, and next action;
- avoid mutating authored lifecycle artifacts;
- avoid mutating `readiness/<id>/work-model.json`;
- avoid mutating `readiness/<id>/verify.json`.

## Text Projection

Text output MUST be a projection of the command report. It MUST include the
selected work id, outcome, generated verification artifact, ready finding count,
advisory count, warning count, blocking count, evidence disposition counts, test
disposition counts, skill visibility counts, generated-view state, diagnostics,
and next action when available. It MUST NOT introduce facts absent from the JSON
report.

## Governance Boundary

The command MAY expose optional Governance policy, capability, or tooling
pointers as compatibility facts. It MUST NOT parse Governance-owned schemas or
produce route, freshness, profile, gate, audit, protected-boundary, effective
evidence, or release verdicts.
