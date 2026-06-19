# Research: Checklist Command

## Decision: Extend The Existing Native Command Stack

Implement `fsgg-sdd checklist` inside the existing `FS.GG.SDD.Commands`
workflow and `FS.GG.SDD.Cli` host.

**Rationale**: The command identity, lifecycle next-action mapping, report
types, effect interpreter, JSON serializer, text renderer, and generated-view
reporting already exist for `init`, `charter`, `specify`, and `clarify`.
Checklist has the same command shape: load project context, read lifecycle
sources, plan safe authored writes, refresh or diagnose generated views, and
emit one deterministic report.

**Alternatives considered**:

- Add a new project for checklist behavior. Rejected because it would duplicate
  the existing MVU/report/effect boundary and introduce packaging surface before
  the command contract needs it.
- Treat checklist as a pure artifact-model parser only. Rejected because the
  feature is a lifecycle authoring command with filesystem effects, dry-run
  behavior, and CLI output.

## Decision: Use An Authored Markdown Checklist With Structured Front Matter

`work/<id>/checklist.md` is the authored source. YAML front matter, stable item
ids, stable result ids, source links, and source snapshot facts are the machine
contract.

**Rationale**: This matches the repository rule that Markdown is the authoring
surface while schema-versioned structured data is the machine contract.
Checklist results must remain readable and editable by humans, but later plan,
task, evidence, and readiness stages need stable facts for pass/fail state,
accepted deferrals, stale results, and blocking findings.

**Alternatives considered**:

- Store checklist results only in generated JSON. Rejected because checklist is
  an authored lifecycle source and must survive regeneration.
- Store all checklist state only in prose checkboxes. Rejected because tools and
  diagnostics need stable ids, source links, and schema-versioned status.

## Decision: Use `CHK-###` For Checklist Items And `CR-###` For Results

Checklist items use `CHK-###`. Checklist results use `CR-###` and link back to
their checklist item and source ids.

**Rationale**: The id families are distinct from existing requirement
(`FR-###`), user-story (`US-###`), acceptance-scenario (`AC-###`), ambiguity
(`AMB-###`), clarification-question (`CQ-###`), and decision (`DEC-###`) ids.
Separate item and result ids allow a stable check definition to have a stale or
superseded result without renumbering the check.

**Alternatives considered**:

- Reuse requirement ids as checklist ids. Rejected because not every checklist
  item is tied to one requirement, and requirements already have their own
  meaning.
- Use one id for both item and result. Rejected because stale result tracking
  requires distinguishing the check from the recorded review outcome.

## Decision: Treat Failed Requirements Quality As A Safe Checklist Output

When valid source artifacts produce failed requirements-quality checks, the
command creates or safely updates `work/<id>/checklist.md`, records failed
blocking results, and points next action to correction rather than `plan`.

**Rationale**: Failed quality is the purpose of the checklist, not malformed
input. Users need the failed checklist artifact to see what must be fixed.
Unsafe or malformed states still block without mutation.

**Alternatives considered**:

- Block without writing any checklist when quality fails. Rejected because it
  hides the review facts that explain what to correct.
- Always advance to plan with warnings. Rejected because checklist is the
  requirements-readiness gate before planning.

## Decision: Mark Existing Results Stale When Source Snapshots Change

Checklist artifacts record source relationships and source snapshot facts for
the selected specification and clarification artifacts. On rerun, result state
is current only when the referenced source facts still match the current parsed
source snapshot; changed source facts mark related results stale or needing
review.

**Rationale**: Checklist results must not silently remain current after
requirements, acceptance scenarios, clarification decisions, or accepted
deferrals change. A source snapshot gives the command a deterministic way to
explain stale review state before planning.

**Alternatives considered**:

- Ignore source changes once checklist results exist. Rejected because later
  planning could rely on outdated readiness facts.
- Require manual deletion and recreation for every source change. Rejected
  because safe reruns should preserve unaffected ids and authored notes.

## Decision: Use Diagnose-Only Schema Migration For Version 1

Checklist front matter and checklist report JSON use `schemaVersion: 1`.
Missing, malformed, unsupported, future, or deprecated schema versions produce
blocking diagnostics before unsafe writes.

**Rationale**: This matches specification and clarification behavior and keeps
the first checklist command narrow. Explicit migration behavior can be added
later with contracts, fixtures, and compatibility notes.

**Alternatives considered**:

- Auto-migrate malformed or unsupported checklist files. Rejected because the
  migration contract does not exist yet and could rewrite authored content
  unsafely.
- Accept files without schema version. Rejected because structured artifacts
  are the machine contract.

## Decision: Reuse Deterministic Command Report And Text Projection Contracts

Checklist reports extend the existing command report shape with a checklist
summary, generated-view state, diagnostics, Governance compatibility facts, and
next action. Text output remains a projection from the same report value.

**Rationale**: Humans, agents, CLI users, CI, and optional Governance consumers
must read the same facts. Existing command-report determinism rules already
exclude timestamps, durations, terminal details, absolute host paths, random
values, and directory enumeration order.

**Alternatives considered**:

- Create a checklist-only report format. Rejected because it would make
  lifecycle commands harder for users and agents to consume consistently.
- Add facts only to text output. Rejected because JSON is the automation
  contract.

## Decision: Keep Governance Integration Advisory And Optional

Checklist may report optional Governance pointers as compatibility facts but
does not parse Governance-owned schemas, select routes, evaluate freshness,
apply profiles, select gates, enforce protected boundaries, emit audit
verdicts, or release policy.

**Rationale**: FS.GG.SDD owns lifecycle authoring and generated SDD readiness.
FS.GG.Governance owns rule evaluation, freshness, routing, profiles, gates, and
enforcement. The command must remain useful without Governance installed.

**Alternatives considered**:

- Fail checklist when Governance files are absent. Rejected because SDD must be
  independently usable.
- Evaluate Governance policy during checklist. Rejected because it crosses the
  repository ownership boundary and belongs to Governance features.

## Decision: Scope Checklist To One Work Item Per Invocation

The command operates on one selected work id per invocation and reports one
next action.

**Rationale**: Prior lifecycle commands use one selected work item. This keeps
safe writes, generated-view refresh, diagnostics, and deterministic reports
bounded and testable.

**Alternatives considered**:

- Batch checklist all work items. Rejected because batching would complicate
  conflict handling, generated-view state, and user correction paths before a
  single-work-item command exists.
