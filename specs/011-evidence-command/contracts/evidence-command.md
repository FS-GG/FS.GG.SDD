# Contract: Evidence Command

## Command

```text
fsgg-sdd evidence --work <work-id> [--input <evidence-text>] [--dry-run] [--text] [--root <path>]
```

The command is also available through the command library by constructing a
`CommandRequest` with `Command = Evidence`.

## Inputs

| Input | Required | Description |
|---|---:|---|
| `--work <work-id>` | Yes | Selects the analyzed work item whose evidence is being declared. |
| `--input <evidence-text>` | No | Supplies compatible evidence declaration/update text through the existing command request input field. When omitted, the command creates or refreshes a safe evidence skeleton and reports missing dispositions. |
| `--dry-run` | No | Produces the same report shape without mutating authored or generated artifacts. |
| `--text` | No | Renders a human-readable projection instead of JSON. |
| `--root <path>` | No | Selects the SDD project root. Defaults to the current directory. |

The command uses the existing overwrite policy default of refusing unsafe
changes. Authored evidence changes are allowed only for safe create or
compatible update plans. Generated-view refresh is allowed only when source
facts are valid and dry-run is false.

## Preconditions

The command requires:

- initialized SDD project settings;
- one valid selected work id;
- `work/<id>/spec.md` with matching work id and `stage: specify`;
- `work/<id>/clarifications.md` with matching work id and `stage: clarify`;
- `work/<id>/checklist.md` with matching work id and `stage: checklist`;
- `work/<id>/plan.md` with matching work id and `stage: plan`;
- `work/<id>/tasks.yml` with matching work id and `stage: tasks`;
- `readiness/<id>/analysis.json` with matching work id and an implementation
  readiness state that permits evidence declaration;
- existing `work/<id>/evidence.yml`, when present, must parse as schema
  version 1 and match the selected work id.

If any required precondition fails, the command MUST report a blocked outcome
and MUST NOT create or update `work/<id>/evidence.yml`.

## Outputs

Authored output when safe:

```text
work/<id>/evidence.yml
```

Generated output when refresh is valid:

```text
readiness/<id>/work-model.json
```

Process output:

- JSON command report by default;
- text projection when `--text` is supplied;
- non-zero exit code when outcome is blocked.

Specification, clarification, checklist, plan, tasks, and analysis artifacts
are preserved.

## Workflow Contract

The command follows the existing command workflow:

1. Load project configuration.
2. Load and validate the selected work item.
3. Load specification, clarification, checklist, plan, tasks, analysis,
   existing work-model, and existing evidence sources.
4. Build current structured facts and source snapshots.
5. Parse optional evidence input and merge it with existing evidence facts.
6. Derive evidence obligations from task, plan, analysis, and lifecycle rule
   facts.
7. Match declarations to obligations and build evidence dispositions.
8. Plan safe authored evidence create, compatible update, preserve, no-change,
   or refusal.
9. Refresh or diagnose generated work-model state after valid evidence facts.
10. Interpret filesystem effects unless dry-run is set.
11. Build the authoritative command report.
12. Render JSON or text from the report.

All filesystem work is requested through command effects. Pure workflow
transitions decide whether authored and generated writes are valid before an
interpreter mutates files.

## Success Outcome

A successful evidence-ready result MUST:

- preserve authored specification, clarification, checklist, plan, tasks, and
  analysis artifacts;
- create, update, preserve, or report no change for `work/<id>/evidence.yml`;
- preserve existing evidence ids, task links, source references, result
  states, synthetic disclosures, deferral rationale, and lifecycle notes;
- refresh or report current `readiness/<id>/work-model.json`;
- include evidence readiness and disposition counts in the command report;
- identify `verify` as the next lifecycle action through action id
  `evidence.next.verify` without implementing verify;
- avoid Governance route, freshness, profile, gate, audit,
  protected-boundary, or release decisions.

## Blocked Outcomes

The command blocks evidence readiness when it detects:

- outside-project execution;
- missing or malformed project settings;
- missing or malformed work id;
- missing specification, clarification, checklist, plan, tasks, or analysis
  prerequisite;
- selected-id mismatch;
- duplicate logical work ids;
- malformed prerequisite facts;
- analysis not implementation-ready;
- malformed existing evidence artifact;
- duplicate evidence ids;
- unknown task, requirement, acceptance scenario, clarification decision,
  checklist result, plan decision, evidence obligation, generated view, or
  source reference;
- completed tasks without required evidence or accepted deferral;
- stale evidence source snapshots;
- missing required skills or capability tags for claimed completed work;
- synthetic evidence without disclosure;
- accepted deferral without rationale, owner, scope, or later lifecycle
  visibility;
- unsupported evidence result state;
- unsafe evidence update;
- stale, malformed, or blocked generated-view refresh when refresh affects the
  command outcome;
- tool defects.

Blocked reports MUST include actionable diagnostics and a next action pointing
to implementation continuation, evidence correction, task correction, analysis
rerun, or generated-view refresh.

## Dry Run

Dry run MUST:

- produce the same report shape as a real run;
- include proposed authored and generated artifact changes;
- include diagnostics, evidence readiness state, and next action;
- avoid mutating authored lifecycle artifacts;
- avoid mutating `work/<id>/evidence.yml`;
- avoid mutating `readiness/<id>/work-model.json`.

## Text Projection

Text output MUST be a projection of the command report. It MUST include the
selected work id, outcome, evidence artifact path, declaration count,
obligation count, supported count, deferred count, missing count, stale count,
synthetic count, invalid count, blocking count, generated-view state,
diagnostics, and next action when available. It MUST NOT introduce facts absent
from the JSON report.

## Governance Boundary

The command MAY expose optional Governance policy, capability, or tooling
pointers as compatibility facts. It MUST NOT parse Governance-owned schemas or
produce freshness, route, profile, gate, audit, protected-boundary, effective
evidence, or release verdicts.
