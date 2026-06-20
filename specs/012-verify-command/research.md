# Research: Verify Command

## Decision: Extend The Existing Lifecycle Command Stack

Use the existing `FS.GG.SDD.Commands` library and `FS.GG.SDD.Cli` host for
`fsgg-sdd verify`. Add `Verify` to the public command union, parse `verify`,
make `nextLifecycleCommand Evidence` return `Verify`, and keep
`nextLifecycleCommand Verify` as `None` until a later ship feature adds a public
`Ship` command.

**Rationale**: `verify` is a stateful lifecycle command with the same
operational shape as preceding command slices: load project context, validate
one work item, inspect source artifacts, evaluate readiness, refresh or diagnose
generated views, and emit one deterministic report.

**Alternatives considered**:

- Add a separate verify executable. Rejected because it would split the
  lifecycle command contract and duplicate effect/report machinery.
- Treat verify as a pure artifact reader only. Rejected because the feature must
  generate the verification view, report next actions, and provide a CLI
  workflow.

## Decision: `verify.json` Is A Generated Readiness View, Not Authored Source

Represent SDD-owned verification readiness as the generated view
`readiness/<id>/verify.json`, with `schemaVersion: 1`, selected work identity,
source relationships, source digests, generator identity, lifecycle stage
readiness, task graph readiness, evidence dispositions, required test
dispositions, required skill visibility, generated-view currency, optional
boundary facts, and diagnostics. The verify command authors no lifecycle source.

**Rationale**: Verification readiness is derived from authored specification,
clarification, checklist, plan, tasks, analysis, and evidence intent. It is a
generated view over those sources, mirroring how `analyze` produces
`analysis.json`. Its presence alone is not proof of currency, so it carries
source digests and generator identity.

**Alternatives considered**:

- Author verification facts into `work/<id>`. Rejected because verification is
  derived readiness, not user-authored lifecycle intent; FR-012 forbids creating
  or rewriting authored sources.
- Store verification only in command output. Rejected because ship readiness,
  generated summaries, agents, CI, and optional Governance consumers need a
  durable generated readiness artifact.

## Decision: Verification Readiness Is A Disposition Over Obligations

Map required obligations from completed tasks, plan verification obligations,
required skills, required tests, generated-view impacts, accepted deferrals, and
analysis findings to structured dispositions. Evidence dispositions use
`supported`, `deferred`, `missing`, `stale`, `synthetic`, `invalid`,
`advisory`, or `blocking`; required test dispositions use `satisfied`,
`deferred`, `missing`, `stale`, `synthetic`, `invalid`, `advisory`, or
`blocking`; skill visibility is a visible/missing fact per task requirement.

**Rationale**: Ship readiness needs to distinguish completed work with real
support from work that is deferred, stale, synthetic, invalid, or unsupported,
and to distinguish satisfied required tests and visible required skills from
gaps. A disposition model gives users actionable correction without asking
Governance to compute freshness.

**Alternatives considered**:

- Treat verification as a single pass/fail. Rejected because it cannot represent
  missing obligations, accepted deferrals, stale source links, synthetic
  disclosures, missing tests, missing skills, or advisory notes.
- Promote Governance effective-evidence states into SDD. Rejected because
  freshness, routing, profiles, and enforcement belong to FS.GG.Governance.

## Decision: Verify Is Non-Destructive And Generates Views Only

The command never creates, updates, reorders, normalizes, or removes authored
specification, clarification, checklist, plan, tasks, or evidence artifacts. Its
only writes are the generated `readiness/<id>/verify.json` view and a refreshed
`readiness/<id>/work-model.json` when verification facts make the normalized
model refresh valid. Generated writes occur only when source facts are valid and
the run is not dry-run.

**Rationale**: Verification must be safe to run repeatedly during authoring,
review, and CI. It inspects authored intent and refreshes generated readiness
facts; it must not silently rewrite the user's task or evidence claims.

**Alternatives considered**:

- Let verify correct evidence or task defects in place. Rejected because each
  authored source has an owning lifecycle command; verify only reports the
  correction and blocks readiness.
- Skip the work-model refresh during verify. Rejected because later ship
  readiness needs the normalized model to reflect current verification-relevant
  facts; the existing analyze-style refresh-or-diagnose posture is reused.

## Decision: Refresh Work-Model State, Diagnose Analysis State

After valid source facts are known, refresh `readiness/<id>/work-model.json`
when source data is valid and the run is not dry-run. Treat
`readiness/<id>/analysis.json` as a prerequisite generated view: a current,
matching, implementation-ready (or accepted-deferral) analysis permits
verification, while missing, stale, malformed, or mismatched analysis produces
diagnostics and next-action guidance. This feature does not generate ship views.

**Rationale**: The normalized work model and analysis view are the shared
machine contracts from earlier stages. Verify consumes a current analysis as its
upstream gate and must not silently re-run analysis or implement ship semantics.

**Alternatives considered**:

- Regenerate `analysis.json` during verify. Rejected because analysis is the
  pre-implementation consistency gate owned by the analyze command.
- Ignore analysis currency. Rejected because verifying against a stale analysis
  view would let ship readiness rest on outdated consistency facts.

## Decision: Derive Required Test And Skill Obligations From Lifecycle Rules

Derive required SDD-owned test obligations from change type, artifact impact,
public contracts, plan verification obligations, task metadata, generated-view
impacts, and evidence declarations, following the artifact-model lifecycle rule
contracts. Derive required skill visibility from task required-skill and
capability-tag declarations, checked against the skills and capability tags
visible in the project's lifecycle artifacts.

**Rationale**: The lifecycle rule pack already defines test-obligation triggers
(public surface, schema/generated-view, command output, agent generation, task
graph/evidence). Verify evaluates whether those obligations and declared skills
are visibly satisfied before ship readiness, without inventing a separate rule
engine.

**Alternatives considered**:

- Hardcode a fixed list of required tests. Rejected because obligations depend on
  the work item's declared impact and planning decisions.
- Discover skills from the live environment or network. Rejected because skill
  visibility is evaluated from SDD lifecycle artifacts and declared
  agent/capability metadata, not runtime discovery or Governance enforcement.

## Decision: Command Report JSON Is The Immediate Automation Contract

Expose selected work id, changed (generated) artifacts, preserved authored
artifacts, verification summary, task/evidence/test/skill disposition counts,
generated-view states, diagnostics, optional Governance compatibility facts,
outcome, and next action in the existing command-report JSON family. Text output
remains a projection.

**Rationale**: Humans, CLI callers, CI, agents, and optional Governance
consumers need the same verification facts without treating terminal text as
authoritative.

**Alternatives considered**:

- Emit only `verify.json` and no command report. Rejected because callers need
  immediate outcome, diagnostics, dry-run state, and exit-code basis.
- Add a separate report format for verify. Rejected because existing lifecycle
  command reports already define the shared command surface.

## Decision: Diagnose-Only Schema Migration For Verification View Version 1

Use `schemaVersion: 1` for the generated verification view and verify command
reports, accept only current version 1 in this slice, and report future,
unsupported, malformed, or deprecated versions as generated-view diagnostics.

**Rationale**: The first verify command must establish a stable generated view
contract without inventing migration behavior before a second version exists.
This matches the preceding lifecycle command slices.

**Alternatives considered**:

- Attempt automatic migration of unknown verification schemas. Rejected because
  no historical native verification schema exists.
- Ignore verification view schema versions. Rejected because schema-versioned
  artifacts are the machine contract and stale generated views must be
  detectable.

## Decision: Keep Governance Integration Advisory And Optional

Report optional Governance policy, capability, tooling, freshness, route,
profile, gate, audit, enforcement, and release pointers as compatibility facts
only. Do not parse Governance-owned schemas, compute freshness, evaluate routes,
select profiles, select gates, audit protected boundaries, or enforce release
policy.

**Rationale**: FS.GG.SDD must remain independently useful without Governance.
Governance owns rule evaluation, effective evidence freshness, routing,
profiles, and gate enforcement.

**Alternatives considered**:

- Block verification when Governance files are absent. Rejected because that
  violates the no-Governance workflow requirement.
- Evaluate Governance freshness or enforcement in verify. Rejected because that
  would cross the repository boundary defined by the constitution.
