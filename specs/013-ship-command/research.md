# Research: Ship Command

## Decision: Extend The Existing Lifecycle Command Stack

Use the existing `FS.GG.SDD.Commands` library and `FS.GG.SDD.Cli` host for
`fsgg-sdd ship`. Add `Ship` to the public command union, parse `ship`, make
`commandStage Ship` return `ship`, make `nextLifecycleCommand Verify` return
`Some Ship`, and keep `nextLifecycleCommand Ship` as `None` because the next
action is the Governance-owned protected-boundary handoff rather than another SDD
command.

**Rationale**: `ship` is a stateful lifecycle command with the same operational
shape as preceding command slices: load project context, validate one work item,
inspect source artifacts and upstream generated views, evaluate readiness,
refresh or diagnose generated views, and emit one deterministic report.

**Alternatives considered**:

- Add a separate ship executable. Rejected because it would split the lifecycle
  command contract and duplicate effect/report machinery.
- Treat ship as a pure artifact reader only. Rejected because the feature must
  generate the ship view, report the next action, and provide a CLI workflow.

## Decision: `ship.json` Is A Generated Readiness View, Not Authored Source

Represent SDD-owned merge-boundary readiness as the generated view
`readiness/<id>/ship.json`, with `schemaVersion: 1`, selected work identity,
source relationships, source digests, generator identity, aggregated lifecycle
stage readiness, verification readiness, evidence dispositions, generated-view
currency, ship-readiness disposition, optional boundary facts, and diagnostics.
The ship command authors no lifecycle source.

**Rationale**: Merge-boundary readiness is derived from authored specification,
clarification, checklist, plan, tasks, evidence intent plus the generated
analysis and verification views. It is a generated view over those sources,
mirroring how `verify` produces `verify.json`. Its presence alone is not proof of
currency, so it carries source digests and generator identity.

**Alternatives considered**:

- Author ship facts into `work/<id>`. Rejected because ship readiness is derived
  readiness, not user-authored lifecycle intent; FR-009 forbids creating or
  rewriting authored sources.
- Store ship readiness only in command output. Rejected because CI, agents,
  generated summaries, and optional Governance protected-boundary consumers need
  a durable generated readiness artifact.

## Decision: Ship Aggregates The Verification View, It Does Not Re-Derive It

Ship consumes the current `readiness/<id>/verify.json` view as its upstream gate
and aggregates its verification readiness, blocking findings, and evidence
dispositions into a single merge-boundary disposition. Ship does not re-run the
task graph, evidence, required-test, or required-skill checks that the verify
stage owns.

**Rationale**: Verification already owns and emits the task/evidence/test/skill
dispositions. Re-deriving them in ship would duplicate logic, risk divergence
between the two views, and blur the stage boundary. Ship's distinct job is to
aggregate verified readiness for the merge boundary and to detect staleness
between the verification view and the current sources.

**Alternatives considered**:

- Recompute task/evidence/test/skill dispositions in ship. Rejected because it
  duplicates verify-owned logic and can disagree with `verify.json`.
- Trust `verify.json` without currency checks. Rejected because a stale
  verification view would let ship readiness rest on outdated verification facts;
  ship must detect digest/schema/generator mismatches.

## Decision: Ship Readiness Is A Single Merge-Boundary Disposition

Map the selected work item to one current ship-readiness disposition of
`shipReady`, `blocked`, `stale`, or `advisory`, derived from aggregated lifecycle
stage readiness, verification readiness, evidence dispositions, and
generated-view currency. Blocking findings and warnings are surfaced with stable
ids and structured links to the affected stage, verification finding, evidence
disposition, generated view, accepted deferral, or source artifact.

**Rationale**: The merge boundary needs one clear SDD-owned answer plus the
underlying findings that justify it. A single disposition with surfaced findings
gives users actionable correction without asking SDD to compute Governance
freshness or enforcement.

**Alternatives considered**:

- Emit only a boolean ship/no-ship flag. Rejected because it cannot represent
  stale verification, advisory-only state, or accepted deferrals visible to later
  stages.
- Promote Governance gate verdicts into SDD ship. Rejected because
  protected-boundary enforcement, routing, profiles, and gates belong to
  FS.GG.Governance.

## Decision: Ship Is Non-Destructive And Generates Views Only

The command never creates, updates, reorders, normalizes, or removes authored
specification, clarification, checklist, plan, tasks, or evidence artifacts, and
never rewrites the verification view. Its only writes are the generated
`readiness/<id>/ship.json` view and a refreshed `readiness/<id>/work-model.json`
when ship facts make the normalized model refresh valid. Generated writes occur
only when source facts are valid and the run is not dry-run.

**Rationale**: Ship readiness must be safe to run repeatedly during authoring,
review, and CI. It inspects authored intent plus upstream generated views and
refreshes generated readiness facts; it must not silently rewrite the user's
lifecycle claims or the verify-owned view.

**Alternatives considered**:

- Let ship correct evidence, verification, or task defects in place. Rejected
  because each authored source and the verification view have owning lifecycle
  commands; ship only reports the correction and blocks readiness.
- Skip the work-model refresh during ship. Rejected because merge-boundary
  readiness should reflect the current normalized model; the existing
  verify-style refresh-or-diagnose posture is reused.

## Decision: Refresh Work-Model State, Diagnose Analysis And Verification State

After valid source facts are known, refresh `readiness/<id>/work-model.json` when
source data is valid and the run is not dry-run. Treat
`readiness/<id>/analysis.json` and `readiness/<id>/verify.json` as prerequisite
generated views: a current, matching analysis and a current, matching,
verification-ready verify view permit ship readiness, while missing, stale,
malformed, mismatched, or not-ready upstream views produce diagnostics and
next-action guidance. This feature does not regenerate analysis or verification
views.

**Rationale**: The normalized work model, analysis view, and verification view
are the shared machine contracts from earlier stages. Ship consumes a current
verification view as its upstream gate and must not silently re-run analysis or
verification.

**Alternatives considered**:

- Regenerate `verify.json` during ship. Rejected because verification is the
  pre-ship readiness gate owned by the verify command.
- Ignore verification currency. Rejected because shipping against a stale
  verification view would let merge-boundary readiness rest on outdated
  verification facts.

## Decision: Command Report JSON Is The Immediate Automation Contract

Expose selected work id, changed (generated) artifacts, preserved authored
artifacts, ship summary, aggregated lifecycle stage readiness, blocker counts,
warning counts, evidence disposition counts, generated-view states, diagnostics,
optional Governance compatibility facts, outcome, and next action in the existing
command-report JSON family. Text output remains a projection.

**Rationale**: Humans, CLI callers, CI, agents, and optional Governance
protected-boundary consumers need the same merge-boundary readiness facts without
treating terminal text as authoritative.

**Alternatives considered**:

- Emit only `ship.json` and no command report. Rejected because callers need the
  immediate outcome, diagnostics, dry-run state, and exit-code basis.
- Add a separate report format for ship. Rejected because existing lifecycle
  command reports already define the shared command surface.

## Decision: Diagnose-Only Schema Migration For Ship View Version 1

Use `schemaVersion: 1` for the generated ship view and ship command reports,
accept only current version 1 in this slice, and report future, unsupported,
malformed, or deprecated versions as generated-view diagnostics.

**Rationale**: The first ship command must establish a stable generated view
contract without inventing migration behavior before a second version exists.
This matches the preceding lifecycle command slices.

**Alternatives considered**:

- Attempt automatic migration of unknown ship schemas. Rejected because no
  historical native ship schema exists.
- Ignore ship view schema versions. Rejected because schema-versioned artifacts
  are the machine contract and stale generated views must be detectable.

## Decision: Keep Governance Integration Advisory And Optional

Report optional Governance policy, capability, tooling, freshness, route,
profile, gate, audit, enforcement, and release pointers as compatibility facts
only. Do not parse Governance-owned schemas, compute freshness, evaluate routes,
select profiles, select gates, audit protected boundaries, or enforce release
policy. The protected-boundary handoff named as ship's next action is an advisory
pointer, not an SDD enforcement action.

**Rationale**: FS.GG.SDD must remain independently useful without Governance.
Governance owns rule evaluation, effective evidence freshness, routing, profiles,
gate enforcement, and the protected-boundary verdict.

**Alternatives considered**:

- Block ship when Governance files are absent. Rejected because that violates the
  no-Governance workflow requirement.
- Evaluate Governance freshness, gates, or the protected-boundary verdict in
  ship. Rejected because that would cross the repository boundary defined by the
  constitution.
