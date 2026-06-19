# Contract: Evidence Fixture Families

## Purpose

The evidence implementation must add fixture-backed tests that prove valid
evidence declaration, safe update behavior, blocked evidence states,
generated-view behavior, deterministic output, text projection, and
no-Governance operation.

Fixtures should use real temporary filesystem roots when testing command
behavior. Synthetic source content is acceptable only when the test name or
nearby comment discloses what real path it stands in for.

## Valid Fixture Families

| Family | Required behavior |
|---|---|
| `evidence-create` | Creates `work/<id>/evidence.yml`, emits evidence summary, refreshes or reports generated work-model state, preserves prerequisite sources, and points next action to verify. |
| `evidence-rerun-current` | Rerun over unchanged sources reports current evidence and work-model state with no unsafe authored mutations. |
| `evidence-preserves-existing` | Evidence does not remove, renumber, reorder destructively, or change meaning of existing declarations, source references, result states, synthetic disclosures, deferral rationale, or lifecycle notes. |
| `evidence-compatible-update` | Compatible new declarations are appended or merged without changing existing stable ids. |
| `evidence-refreshes-work-model` | Valid evidence facts refresh `readiness/<id>/work-model.json` so normalized work-model evidence entries are current. |
| `evidence-accepted-deferral` | Accepted deferrals remain visible in evidence dispositions and later lifecycle state rather than hiding missing real evidence. |
| `evidence-synthetic-disclosed` | Synthetic evidence with disclosure is reported distinctly from real evidence and remains traceable to the real path it stands in for. |
| `dry-run` | Reports proposed authored and generated changes without mutating `evidence.yml` or `work-model.json`. |
| `deterministic-report` | Three identical runs over identical input produce byte-identical JSON reports and proposed evidence payloads. |
| `text-projection` | Text output includes only facts present in the JSON report. |
| `governance-boundary` | Evidence works without Governance files and reports optional Governance pointers only as not-evaluated compatibility facts. |

## Blocked Fixture Families

| Family | Required behavior |
|---|---|
| `outside-project` | Blocks outside an initialized SDD project and writes no evidence artifact. |
| `missing-analysis` | Blocks when `readiness/<id>/analysis.json` is missing, malformed, mismatched, or not implementation-ready. |
| `missing-tasks` | Blocks when `work/<id>/tasks.yml` is missing or not valid enough to derive evidence obligations. |
| `missing-required-evidence` | Reports completed tasks without evidence or accepted deferral as evidence-readiness defects. |
| `malformed-evidence` | Reports malformed existing evidence and refuses unsafe writes. |
| `duplicate-evidence-id` | Blocks duplicate evidence ids and reports the affected id. |
| `unknown-evidence-reference` | Blocks unknown tasks, requirements, acceptance scenarios, clarification decisions, checklist results, plan decisions, obligations, generated views, or source references. |
| `stale-evidence` | Reports declarations whose source snapshots no longer match current source facts. |
| `undisclosed-synthetic-evidence` | Blocks synthetic evidence without disclosure of what real path it stands in for. |
| `missing-deferral-rationale` | Blocks accepted deferrals without rationale, owner, scope, or later lifecycle visibility. |
| `unsafe-evidence-update` | Blocks proposed updates that would remove, renumber, reorder destructively, or change existing evidence meaning. |
| `stale-generated-view` | Reports stale, malformed, missing, or blocked generated work-model state without treating the view as current. |

## Evidence Requirements

Implementation readiness evidence must include:

- clean Release build;
- focused artifact-model evidence parser and disposition tests;
- focused command workflow evidence tests;
- create/rerun/update/refusal/blocker test transcripts;
- authored-source preservation evidence;
- deterministic JSON and proposed evidence comparison evidence;
- generated work-model currency evidence;
- text projection human-summary review;
- disposable-directory CLI JSON, dry-run, and text smoke output;
- FSI or prelude transcript for public evidence surface;
- performance evidence for create and rerun fixtures;
- SDD/Governance boundary review;
- artifact traceability review mapping spec requirements to plan, tasks,
  tests, and readiness evidence.
