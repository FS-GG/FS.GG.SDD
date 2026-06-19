# Research: Evidence Command

## Decision: Extend The Existing Lifecycle Command Stack

Use the existing `FS.GG.SDD.Commands` library and `FS.GG.SDD.Cli` host for
`fsgg-sdd evidence`. Add `Evidence` to the public command union, parse
`evidence`, make `nextLifecycleCommand Analyze` return `Evidence`, and keep
`nextLifecycleCommand Evidence` as `None` until a later verify feature adds a
public `Verify` command.

**Rationale**: `evidence` is a stateful lifecycle command with the same
operational shape as preceding command slices: load project context, validate
one work item, inspect source artifacts, plan safe filesystem changes, refresh
or diagnose generated views, and emit one deterministic report.

**Alternatives considered**:

- Add a separate evidence executable. Rejected because it would split the
  lifecycle command contract and duplicate effect/report machinery.
- Treat evidence as a pure artifact parser only. Rejected because the feature
  must create or update authored evidence, report next actions, and provide a
  CLI workflow.

## Decision: `evidence.yml` Is Authored SDD Lifecycle Source

Represent declared implementation and verification support as
`work/<id>/evidence.yml`, with `schemaVersion: 1`, selected work identity,
source relationships, source snapshots, evidence declarations, evidence
obligation dispositions, lifecycle notes, and diagnostics.

**Rationale**: Evidence declarations capture user or agent intent about what
supports task completion. They are not derived output. Humans and agents must
be able to preserve, review, and update them before later verify and ship
readiness stages consume them.

**Alternatives considered**:

- Store evidence in `readiness/<id>/evidence.json`. Rejected because this
  feature records authored declarations, not effective freshness or generated
  readiness.
- Store evidence only in command output. Rejected because verify, ship, agents,
  CI, and optional Governance consumers need durable lifecycle source data.

## Decision: Evidence Readiness Is A Disposition Over Obligations

Map required evidence obligations from tasks, plan verification obligations,
required skills, generated-view impacts, accepted deferrals, and analysis
findings to structured dispositions: `supported`, `deferred`, `missing`,
`stale`, `synthetic`, `invalid`, `advisory`, or `blocking`.

**Rationale**: Later verify and ship readiness need to distinguish completed
tasks with real support from tasks that are deferred, stale, synthetic,
invalid, or unsupported. A disposition model gives users actionable correction
without asking Governance to compute freshness.

**Alternatives considered**:

- Treat each evidence declaration as simply pass or fail. Rejected because it
  cannot represent missing obligations, accepted deferrals, stale source links,
  synthetic disclosures, or advisory notes.
- Promote Governance effective-evidence states into SDD. Rejected because
  freshness, routing, profiles, and enforcement belong to FS.GG.Governance.

## Decision: Preserve Existing Evidence And Refuse Unsafe Updates

The command may create a missing evidence artifact, append compatible evidence
declarations, or preserve current declarations. It must refuse duplicate ids,
selected-work mismatches, destructive reorder/removal, changed result meaning,
missing synthetic disclosure, missing deferral rationale, and unknown source
references before writing.

**Rationale**: Evidence becomes the durable bridge between implementation,
verification, ship readiness, agents, CI, and optional Governance consumers.
Silent rewrites would make later readiness facts untrustworthy.

**Alternatives considered**:

- Normalize and rewrite the entire evidence file on every run. Rejected
  because authored source must preserve stable ids, rationale, and user notes.
- Allow overwrite with a command flag in this slice. Rejected because the
  feature only needs safe create/update/refusal; destructive replacement can be
  designed later if needed.

## Decision: Refresh Work-Model State, Diagnose Analysis State

After valid evidence facts are known, refresh `readiness/<id>/work-model.json`
when source data is valid and the run is not dry-run. Treat
`readiness/<id>/analysis.json` as a prerequisite generated view: current
analysis permits evidence readiness evaluation, while missing, stale,
malformed, or mismatched analysis produces diagnostics and next-action
guidance. This feature does not generate verify or ship views.

**Rationale**: The normalized work model already includes evidence entries and
is the shared machine contract for later stages. Analysis remains the
prerequisite readiness view from the previous lifecycle stage; evidence should
not silently re-run analysis or implement verify semantics.

**Alternatives considered**:

- Skip generated-view refresh during evidence. Rejected because later stages
  need the normalized model to include evidence facts.
- Regenerate `analysis.json` after evidence writes. Rejected because analysis
  is the pre-implementation consistency gate; verify will own post-evidence
  readiness in a later feature.

## Decision: Command Report JSON Is The Immediate Automation Contract

Expose selected work id, changed artifacts, preserved artifacts, evidence
summary, obligation/disposition counts, generated-view states, diagnostics,
optional Governance compatibility facts, outcome, and next action in the
existing command-report JSON family. Text output remains a projection.

**Rationale**: Humans, CLI callers, CI, agents, and optional Governance
consumers need the same evidence facts without treating terminal text as
authoritative.

**Alternatives considered**:

- Emit only `evidence.yml` and no command report. Rejected because callers need
  immediate outcome, diagnostics, dry-run state, and exit-code basis.
- Add a separate report format for evidence. Rejected because existing
  lifecycle command reports already define the shared command surface.

## Decision: Diagnose-Only Schema Migration For Evidence Version 1

Use `schemaVersion: 1` for authored evidence and evidence command reports,
accept only current version 1 in this slice, and report future, unsupported,
malformed, or deprecated versions as evidence diagnostics.

**Rationale**: The first evidence command must establish a stable authored
source contract without inventing migration behavior before a second version
exists. This matches the preceding lifecycle command slices.

**Alternatives considered**:

- Attempt automatic migration of unknown evidence schemas. Rejected because no
  historical native evidence schema exists and authored source migration
  policy should be explicit.
- Ignore evidence schema versions. Rejected because schema-versioned artifacts
  are the machine contract.

## Decision: Keep Governance Integration Advisory And Optional

Report optional Governance policy, capability, or tooling pointers as
compatibility facts only. Do not parse Governance-owned schemas, compute
freshness, evaluate routes, select profiles, select gates, audit protected
boundaries, or enforce release policy.

**Rationale**: FS.GG.SDD must remain independently useful without Governance.
Governance owns rule evaluation, effective evidence freshness, routing,
profiles, and gate enforcement.

**Alternatives considered**:

- Block evidence declaration when Governance files are absent. Rejected
  because that violates the no-Governance workflow requirement.
- Evaluate Governance freshness in evidence. Rejected because that would cross
  the repository boundary defined by the constitution.
