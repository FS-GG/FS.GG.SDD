# Research: Analyze Command

## Decision: Extend The Existing Lifecycle Command Stack

Use the existing `FS.GG.SDD.Commands` library and `FS.GG.SDD.Cli` host for
`fsgg-sdd analyze`. The command enum already includes `Analyze`, so the feature
should remove the unsupported-command path for that command and reuse the
existing request, effect, interpreter, report, serialization, rendering, and
fixture patterns.

**Rationale**: `analyze` is a stateful lifecycle command with the same
operational shape as preceding command slices: load project context, validate
one work item, inspect source artifacts, refresh or diagnose generated views,
and emit one deterministic report.

**Alternatives considered**:

- Add a separate analysis executable. Rejected because it would split the
  lifecycle command contract and duplicate effect/report machinery.
- Treat analysis as a pure artifact parser only. Rejected because the feature
  must refresh generated readiness data, report next actions, and provide a
  CLI workflow.

## Decision: `analysis.json` Is A Generated SDD Readiness View

Represent cross-artifact lifecycle analysis as
`readiness/<id>/analysis.json`, with `schemaVersion: 1`, selected work
identity, generator identity, source relationships, source digests, lifecycle
readiness, structured findings, generated-view currency, diagnostics, optional
boundary facts, and next action.

**Rationale**: The constitution requires schema-versioned structured artifacts
as the machine contract, while generated views remain outputs. Analysis is
derived from specification, clarification, checklist, plan, tasks, and generated
work-model facts, so it belongs in readiness rather than in authored source.

**Alternatives considered**:

- Store analysis results in `work/<id>/analysis.yml`. Rejected because analysis
  is generated readiness data, not authored lifecycle intent.
- Use text-only analysis output. Rejected because humans, agents, CI, and later
  verify/ship stages need stable structured data.

## Decision: Analyze Is Non-Destructive For Authored Sources

The command must not create, update, reorder, normalize, or remove
`spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, or `tasks.yml`.
Only generated readiness views may be created or refreshed, and dry-run writes
no generated artifacts.

**Rationale**: Analysis is the last consistency check before implementation.
It must explain source defects without silently changing lifecycle intent or
task state.

**Alternatives considered**:

- Auto-fix stale tasks or plan decisions during analysis. Rejected because that
  would mutate authored source and hide the responsible lifecycle command.
- Write corrective comments into source Markdown. Rejected because diagnostics
  already provide actionable correction without changing authoring surfaces.

## Decision: Work-Model Currency Is A Prerequisite Analysis Input

Analyze should refresh `readiness/<id>/work-model.json` when source facts are
valid and diagnose missing, stale, malformed, or blocked work-model state when
refresh is not safe. The analysis view records the work-model relationship and
does not treat an existing generated file as current unless source digests and
generator identity agree.

**Rationale**: The normalized work model is the shared machine-readable
lifecycle state consumed by humans, agents, CI, and optional Governance
tooling. Analysis must not build readiness over stale generated data.

**Alternatives considered**:

- Ignore work-model state and analyze source files directly only. Rejected
  because generated-view currency is part of the product contract and later
  stages depend on the normalized model.
- Require users to run a separate refresh command first. Rejected because
  preceding command slices already refresh or diagnose generated views where
  source data is valid.

## Decision: Analysis Findings Use Stable Structured Ids

Use stable analysis finding ids and diagnostic ids for source relationships,
missing dispositions, stale source links, blocking lifecycle state, generated
view currency, and optional boundary facts. Findings link to affected
requirements, acceptance scenarios, clarification decisions, checklist
results, plan decisions, contract references, verification obligations, tasks,
dependencies, generated views, accepted deferrals, or source artifacts when
known.

**Rationale**: Users need exact corrections before implementation starts, and
later evidence, verify, ship, and optional Governance consumers need stable
references into analysis facts.

**Alternatives considered**:

- Use free-form text findings only. Rejected because that weakens automation,
  next-action selection, and cross-artifact traceability.
- Reuse task ids as analysis finding ids. Rejected because one finding may
  span multiple source facts or generated views.

## Decision: Diagnose-Only Schema Migration For Analysis Version 1

Use `schemaVersion: 1` for `analysis.json` and analyze command reports, accept
only current version 1 in this slice, and report future, unsupported,
malformed, or deprecated versions as generated-view diagnostics.

**Rationale**: The first analysis command must establish a stable generated
view contract without inventing migration behavior before a second version
exists. This matches the preceding lifecycle command slices.

**Alternatives considered**:

- Attempt automatic migration of unknown analysis schemas. Rejected because no
  historical native analysis schema exists and generated view migration policy
  should be explicit.
- Ignore generated analysis schema versions. Rejected because
  schema-versioned artifacts are the machine contract.

## Decision: Command Report JSON Is The Immediate Automation Contract

Expose analysis context, generated artifact changes, parsed source summaries,
analysis readiness, finding counts, generated-view state, diagnostics, optional
Governance compatibility facts, and next action in the existing command-report
JSON family, with text output rendered as a projection.

**Rationale**: Humans, CLI callers, CI, agents, and optional Governance
consumers need the same facts without treating terminal text as authoritative.

**Alternatives considered**:

- Emit only `analysis.json` and no command report. Rejected because callers
  need immediate command outcome, diagnostics, dry-run state, and exit-code
  basis.
- Add a separate report format for analyze. Rejected because existing lifecycle
  command reports already define the shared command surface.

## Decision: Keep Governance Integration Advisory And Optional

Report optional Governance pointers as compatibility facts only. Do not parse
Governance-owned schemas, evaluate routes, compute freshness, select profiles,
select gates, verify evidence freshness, or enforce protected boundaries.

**Rationale**: FS.GG.SDD must remain independently useful without Governance.
Governance owns rule evaluation, evidence freshness, routing, profiles, and
gate enforcement.

**Alternatives considered**:

- Block analysis when Governance files are absent. Rejected because that
  violates the no-Governance workflow requirement.
- Evaluate Governance policy in analysis. Rejected because that would cross
  the repository boundary defined by the constitution.
