# Data Model: Bootstrap and Migration Experience

This feature introduces **no new structured schema and no new generated-view
contract**. It ships consumer documentation surfaces and one automated
verification harness over the existing `fsgg-sdd` lifecycle. The "entities" below
are the documentation and verification artifacts the feature produces and the
existing artifacts they describe or assert over. None is a new machine contract.

## No new machine contract

- No new `fsgg-sdd` command, lifecycle stage, authored-source schema, or
  generated-view schema is added (FR-012).
- No `.fsi` signature, public API baseline, or serialization contract changes.
- The smoke asserts over the schema versions the existing generators already
  emit; it does not define new ones.

## Documentation artifacts (authoring surfaces)

### Quickstart Walkthrough — `docs/quickstart.md`

The consumer-facing end-to-end walkthrough.

- **Required content**: a no-Governance path from `fsgg-sdd init` through
  `fsgg-sdd ship`; the canonical stage order (charter, specify, clarify,
  checklist, plan, tasks, analyze, evidence, verify, ship); for each stage, the
  authored source written and the generated readiness view refreshed or
  reported; where `fsgg-sdd agents` and `fsgg-sdd refresh` bring agent guidance
  and `summary.md` to currency; the resulting SDD-owned readiness artifacts.
- **Invariants**: runnable with no Governance gate runtime installed (FR-001);
  stage order and next-action pointers match command output (FR-014); generated
  views framed as outputs whose currency comes from `refresh`, not presence
  (FR-015); assumes no FS.GG.Rendering, monorepo checkout, or runtime templates
  (FR-013); FsDocs-style frontmatter.

### Migration Guide — `docs/migration-from-spec-kit.md`

The additive Spec Kit → native SDD mapping.

- **Required content**: additive steps to create the SDD skeleton and map an
  existing Spec Kit feature's `spec.md`, `plan.md`, clarifications, checklist,
  tasks, and evidence onto `work/<id>/` authored sources via the `fsgg-sdd`
  commands; guidance for Spec Kit artifacts with no direct SDD equivalent
  (represent-or-defer, never delete).
- **Invariants**: preserves standard Spec Kit as a valid workflow and leaves
  existing `specs/` and `.specify/` content unchanged (FR-008); additive and
  safe to re-apply, never destructive (FR-009).

### Optional Governance Adoption Note — `docs/adopting-governance.md`

The optional-Governance boundary documentation.

- **Required content**: how `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, and
  `.fsgg/tooling.yml` are added after `fsgg-sdd init` as an additive layer; the
  statement that SDD commands stay usable whether those files are present,
  absent, or incomplete; the boundary that SDD never evaluates or enforces
  Governance routing, freshness, profiles, gates, audit, or release decisions.
- **Invariants**: Governance references are advisory compatibility facts only
  (FR-011, FR-016); adoption is optional and additive.

### Index / README cross-links

- `docs/index.md` links the three new docs; `README.md` Workflow section links
  the quickstart and migration guide. No structural change to existing content.

## Verification artifact

### Lifecycle Smoke Run — `tests/FS.GG.SDD.Commands.Tests/LifecycleSmokeTests.fs`

The automated end-to-end check that pins the documented behavior to the commands.

- **Inputs**: a disposable temp project (via `TestSupport.tempDirectory()`); one
  work id; the existing per-stage run helpers.
- **Sequence**: `initializeProject` → `runCharter` → `runSpecify` →
  `runClarify` → `runChecklist` → `runPlan` → `runTasks` → `runAnalyze` →
  `runEvidence` → `runVerify` → `runShip`, then `runAgents` and `runRefresh`.
- **Assertions** (detailed in [contracts/lifecycle-smoke.md](contracts/lifecycle-smoke.md)
  and [contracts/bootstrap-assertions.md](contracts/bootstrap-assertions.md)):
  every stage succeeds and produces its authored source and generated readiness
  view; no Governance policy/capability/tooling file is created or required
  (FR-005); the emitted next-action chain matches the documented quickstart order
  (FR-014); two runs over identical inputs yield byte-identical machine-readable
  readiness (FR-006); commands stay usable with present-but-incomplete Governance
  files (FR-011); the run needs nothing beyond the SDD projects (FR-013).
- **Invariants**: writes no repository files; depends on no surrounding-repository
  Governance state; deterministic (no clocks, durations, ANSI, enumeration order,
  random values, or absolute host paths in asserted output).

## Existing artifacts referenced (not modified)

- **SDD Project Skeleton**: `.fsgg/project.yml`, `.fsgg/sdd.yml`,
  `.fsgg/agents.yml`, `work/`, `readiness/`, and agent guidance targets created
  by `fsgg-sdd init`.
- **Authored lifecycle sources**: `work/<id>/{charter.md, spec.md,
  clarifications.md, checklist.md, plan.md, contracts/, tasks.yml, evidence.yml}`.
- **Generated readiness views**: `readiness/<id>/{work-model.json, analysis.json,
  verify.json, ship.json, summary.md, agent-commands/<target>/}`.
- **Governance-owned (optional, out of scope to enforce)**: `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, `.fsgg/tooling.yml`.
