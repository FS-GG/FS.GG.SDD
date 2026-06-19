# Research: Plan Command

## Decision: Extend The Existing Lifecycle Command Stack

Use the existing `FS.GG.SDD.Commands` library and `FS.GG.SDD.Cli` host for
`fsgg-sdd plan`. The command enum already includes `Plan`, so the feature
should remove the unsupported-command path for that command and reuse the
existing request, effect, interpreter, report, serialization, rendering, and
fixture patterns.

**Rationale**: `plan` is a stateful lifecycle command with the same shape as
`specify`, `clarify`, and `checklist`: load project context, validate one work
item, plan safe authored-source changes, refresh or diagnose generated views,
and emit one deterministic report.

**Alternatives considered**:

- Add a separate planning project or CLI. Rejected because it would split the
  lifecycle command contract and duplicate effect/report machinery.
- Treat plan as a pure artifact parser only. Rejected because the feature must
  author `work/<id>/plan.md`, report next actions, and refresh generated-view
  state.

## Decision: Plan Markdown With Structured Front Matter Is The Authored Source

Represent `work/<id>/plan.md` as authored Markdown with schema-versioned front
matter, stable typed plan ids, source snapshot facts, and standard sections for
scope, decisions, contract impact, verification obligations, migration posture,
generated-view impact, accepted deferrals, findings, and lifecycle notes.

**Rationale**: The constitution requires Markdown to remain an authoring
surface while structured artifacts are the machine contract. A plan artifact
must be readable by humans and durable enough for later `tasks`, evidence,
analysis, verify, and ship stages.

**Alternatives considered**:

- Store plan facts only in generated JSON. Rejected because generated views are
  outputs and their presence is not proof of currency.
- Leave plan facts only in prose. Rejected because later tools need stable ids,
  source links, stale-state diagnostics, and deterministic report facts.

## Decision: Add Narrow Plan-Specific Identifier And Fact Types

Add narrow artifact-model contracts for plan decisions, contract references,
verification obligations, migration notes, generated-view impacts, and source
snapshots. Use stable ids such as `PD-###`, `PC-###`, `VO-###`, `PM-###`, and
`GV-###` in the authored plan and command reports.

**Rationale**: Existing identifiers cover requirements, clarification
decisions, checklist items/results, tasks, and evidence. Planning introduces
new references that later task and evidence stages need to cite without
overloading unrelated ids.

**Alternatives considered**:

- Use free-form strings for all plan references. Rejected because it weakens
  stale detection and task/evidence traceability.
- Reuse checklist result ids for planning decisions. Rejected because checklist
  results and plan decisions have different lifecycle meanings.

## Decision: Diagnose-Only Schema Migration For Plan Version 1

Use `schemaVersion: 1` for the plan artifact and plan command report, accept
only current version 1 in this slice, and block future, unsupported, malformed,
or deprecated versions with actionable diagnostics.

**Rationale**: The first plan command must establish a stable contract without
inventing migration behavior before a second version exists. This matches the
preceding lifecycle command slices.

**Alternatives considered**:

- Attempt automatic migration of unknown plan schemas. Rejected because no
  historical plan schema exists and unsafe rewrites would risk authored data.
- Ignore schema versions. Rejected because schema-versioned artifacts are the
  machine contract.

## Decision: Preserve Existing Plan Decisions And Mark Stale Source Links

On rerun, preserve existing plan decisions, contract references, verification
obligations, migration notes, accepted deferrals, source links, and stable ids
unless a safe non-destructive update can be proven. If referenced source facts
change, mark affected plan decisions stale or needing review rather than
treating them as current.

**Rationale**: Plans are durable decisions that later lifecycle artifacts will
reference. Silent rewrites would break traceability; stale markers keep drift
visible without overwriting user intent.

**Alternatives considered**:

- Regenerate the plan from current sources on every run. Rejected because it
  would discard or renumber human decisions.
- Never update an existing plan. Rejected because compatible additions from
  changed specs, clarifications, or checklists should be safe.

## Decision: Command Report JSON Is The Immediate Automation Contract

Expose plan context, artifact changes, plan summary, generated-view state,
diagnostics, optional Governance compatibility facts, and next action in the
existing command-report JSON family, with text output rendered as a projection.

**Rationale**: Humans, CLI callers, CI, and optional Governance consumers need
the same facts without treating terminal text as authoritative.

**Alternatives considered**:

- Emit only text for `plan`. Rejected because deterministic JSON is required
  for automation.
- Add a separate report format for plan. Rejected because existing lifecycle
  command reports already define the shared command surface.

## Decision: Keep Governance Integration Advisory And Optional

Report optional Governance pointers as compatibility facts only. Do not parse
Governance-owned schemas, evaluate routes, compute freshness, select profiles,
select gates, or enforce protected boundaries.

**Rationale**: FS.GG.SDD must remain independently useful without Governance.
Governance owns rule evaluation, evidence freshness, routing, profiles, and
gate enforcement.

**Alternatives considered**:

- Block plan creation when Governance files are absent. Rejected because that
  violates the no-Governance workflow requirement.
- Evaluate Governance route or gate state in plan. Rejected because that would
  cross the repo boundary defined by the constitution.
