---
title: Initial implementation plan
category: SDD
categoryindex: 6
index: 12
description: Initial implementation plan for the FS.GG spec-driven development lifecycle product, derived from the native SDD model in initial-design.md.
---

# Initial implementation plan

This is the implementation plan for **FS.GG.SDD**, the FS.GG
spec-driven development lifecycle product.

It replaces the copied Governance implementation roadmap that was previously in
this file. The source design is [initial-design.md](initial-design.md),
especially its native spec-driven development model, `.fsgg` plus `work/<id>`
source model, generated readiness views, command surface, and acceptance bar.

Standard Spec Kit remains the workflow used to develop this repository:

```text
specify -> clarify -> plan -> tasks -> implement -> analyze
```

That workflow is not the product being built. The product is native FS.GG SDD:
typed lifecycle artifacts, normalized work models, generated views, lifecycle
commands, and agent guidance that can later be inspected by optional
Governance tooling.

## Product boundary

FS.GG.SDD owns:

- project charter and policy workflow;
- specify, clarify, checklist, plan, tasks, analyze, implement, verify, and
  ship lifecycle artifacts;
- schema-versioned structured contracts for authored sources;
- normalized work model generation;
- generated readiness views;
- agent command and skill generation for Claude and Codex;
- optional integration contracts consumed by Governance.

FS.GG.SDD does not own:

- rule evaluation;
- evidence freshness policy;
- route/profile selection;
- protected branch gate enforcement;
- package release enforcement for non-SDD products;
- rendering templates, controls, or generated-product runtime behavior.

Those concerns belong to FS.GG.Governance, FS.GG.Rendering, or the consuming
product.

## Ground rules

- Markdown is an authoring surface. Schema-versioned structured artifacts are
  the machine contract.
- Product code is added only through a feature spec that defines artifact
  contracts and verification.
- The first implementation feature defines artifact identity and schemas before
  command implementation.
- Public F# modules get `.fsi` signatures first, semantic tests through the
  public surface, then `.fs` implementation.
- Stateful commands, generators, validators, and agent writers expose or wrap an
  Elmish-style boundary.
- Claude, Codex, CLI users, and CI operate over the same lifecycle artifacts.
- Governance integration is optional and versioned. SDD must remain usable
  without Governance installed.

## Command naming

`initial-design.md` uses `fsgg ...` as the broad FS.GG command shape. This
repository's constitution reserves the SDD command family as `fsgg-sdd` unless
a later release decision chooses otherwise.

The SDD plan therefore maps the native design commands as follows:

| Design command | SDD command |
|---|---|
| `fsgg init` / `fsgg new` | `fsgg-sdd init` |
| `fsgg charter` | `fsgg-sdd charter` |
| `fsgg work specify` | `fsgg-sdd specify` |
| `fsgg work clarify` | `fsgg-sdd clarify` |
| `fsgg work checklist` | `fsgg-sdd checklist` |
| `fsgg work plan` | `fsgg-sdd plan` |
| `fsgg work tasks` | `fsgg-sdd tasks` |
| `fsgg analyze` | `fsgg-sdd analyze` |
| `fsgg work update` | `fsgg-sdd evidence` or `fsgg-sdd update` |
| `fsgg verify` | `fsgg-sdd verify` |
| `fsgg ship` | `fsgg-sdd ship` |

The exact subcommand names are locked by the feature specs that introduce the
CLI surface.

## Source model

The native source model is `.fsgg` plus `work/<id>`.

Project-level authored sources:

| Artifact | Purpose |
|---|---|
| `.fsgg/project.yml` | Project identity, schema version, default work root, and product metadata. |
| `.fsgg/sdd.yml` | SDD lifecycle policy, artifact layout, generated-view policy, and schema migration posture. |
| `.fsgg/agents.yml` | Agent command and skill generation targets for Claude, Codex, and future agents. |

Work-item authored sources:

| Artifact | Purpose |
|---|---|
| `work/<id>/charter.md` | Project or work charter when a lifecycle slice needs local principles and boundaries. |
| `work/<id>/spec.md` | User value, scope, stories, requirements, non-goals, and acceptance criteria. |
| `work/<id>/clarifications.md` | Material ambiguity and explicit answers. |
| `work/<id>/checklist.md` | Requirements quality checks before planning. |
| `work/<id>/plan.md` | Technical plan, contracts, risks, verification, and migration posture. |
| `work/<id>/contracts/` | Public or tool-facing contracts described by the plan. |
| `work/<id>/tasks.yml` | Typed implementation task graph. |
| `work/<id>/evidence.yml` | Declared implementation, verification, and deferral evidence. |

Generated views:

| Artifact | Purpose |
|---|---|
| `readiness/<id>/work-model.json` | Deterministic normalized work model with source digests and diagnostics. |
| `readiness/<id>/analysis.json` | Cross-artifact consistency diagnostics. |
| `readiness/<id>/verify.json` | Selected local or CI verification results. |
| `readiness/<id>/ship.json` | Merge-boundary readiness summary for SDD and optional Governance consumers. |
| `readiness/<id>/summary.md` | Human-readable summary rendered from structured readiness data. |
| `readiness/<id>/agent-commands/` | Generated agent guidance derived from the same lifecycle model. |

Generated views are outputs. Their presence is not proof of currency. Every
generated view must identify its sources, source digests, generator version, and
stale-view diagnostics.

## Roadmap

Progress markers:

- [x] Scaffold empty repository with Spec Kit metadata, constitution, docs, and
  Claude/Codex guidance.
- [x] Create GitHub repository under `FS-GG`.
- [x] Update FS-GG org profile/site to list SDD as a separate product.
- [x] Copy development-relevant Governance and org reference docs into
  `docs/reference/`.
- [x] Replace copied Governance implementation plan with this SDD plan.

### Phase 1: SDD artifact model

Purpose: define the typed contract before commands or generators exist.

- [ ] Create the first feature spec for the artifact model.
- [ ] Define `WorkId`, `Stage`, `RequirementId`, `DecisionId`, `TaskId`,
  `EvidenceId`, `ArtifactRef`, `SchemaVersion`, and source digest types.
- [ ] Specify schemas for `.fsgg/project.yml`, `.fsgg/sdd.yml`, and
  `.fsgg/agents.yml`.
- [ ] Specify schemas for `work/<id>` metadata, structured front matter where
  used, `tasks.yml`, and `evidence.yml`.
- [ ] Define diagnostic ids for missing artifacts, malformed schema versions,
  duplicate ids, unknown references, stale generated views, and
  prose/structured mismatch.
- [ ] Add `.fsi` signatures before implementation.
- [ ] Add semantic tests for schema validation, id stability, and deterministic
  ordering.

Exit criteria:

- The repository has a packable SDD artifact-model library.
- Public signatures define the machine contract.
- Fixtures cover valid, malformed, duplicate-id, unknown-reference, and
  stale-view cases.

### Phase 2: Normalized work model

Purpose: turn authored lifecycle artifacts into the single machine-readable SDD
contract.

- [ ] Parse `.fsgg` and `work/<id>` authored sources into a `WorkModel`.
- [ ] Emit `readiness/<id>/work-model.json` with deterministic ordering.
- [ ] Include source paths, source digests, schema versions, and diagnostics.
- [ ] Define conflict behavior when Markdown prose and structured data disagree.
- [ ] Report stale or missing generated work models.
- [ ] Document schema migration behavior and compatibility rules.

Exit criteria:

- Given the same source tree, work-model JSON is byte-stable.
- Diagnostics explain malformed input without replacing user intent.
- Stale generated models are detectable from source digests and generator
  metadata.

### Phase 3: Lifecycle authoring commands

Purpose: expose the native spec-driven development stages as SDD commands over
the same model.

- [ ] Add `fsgg-sdd init` for `.fsgg` skeleton creation.
- [ ] Add `fsgg-sdd charter`.
- [ ] Add `fsgg-sdd specify`.
- [ ] Add `fsgg-sdd clarify`.
- [ ] Add `fsgg-sdd checklist`.
- [ ] Add `fsgg-sdd plan`.
- [ ] Add `fsgg-sdd tasks`.
- [ ] Add `fsgg-sdd analyze`.
- [ ] Keep command state behind `Model`, `Msg`, `Effect`, `init`, and `update`
  boundaries where commands perform stateful or I/O work.
- [ ] Ensure command output has JSON for automation and plain text for humans.

Exit criteria:

- A user can create and advance a work item from charter through analysis
  without product source code.
- Commands write authored sources and refresh or diagnose generated views.
- JSON output is deterministic and plain text is presentation only.

### Phase 4: Tasks, evidence, verify, and ship readiness

Purpose: make implementation work and merge readiness inspectable without
turning SDD into the Governance rule engine.

- [ ] Validate task graph structure, dependencies, ids, and status transitions.
- [ ] Parse and normalize evidence declarations.
- [ ] Distinguish real evidence, accepted deferrals, missing evidence, and
  synthetic evidence disclosures.
- [ ] Add `fsgg-sdd evidence` or equivalent update command.
- [ ] Add `fsgg-sdd verify` to run selected SDD-owned checks and emit
  `readiness/<id>/verify.json`.
- [ ] Add `fsgg-sdd ship` to produce SDD merge-boundary readiness in
  `readiness/<id>/ship.json`.
- [ ] Keep protected-branch enforcement decisions outside SDD unless a later
  Governance integration contract explicitly consumes the output.

Exit criteria:

- Work items can prove what tasks were completed and what evidence supports
  them.
- Verify and ship outputs are stable enough for CI, agents, and optional
  Governance consumers.
- Missing or stale evidence produces actionable diagnostics.

### Phase 5: Agent guidance generation

Purpose: keep human, Claude, Codex, and future-agent workflows on one contract.

- [ ] Generate Claude command and skill guidance from the normalized lifecycle
  model.
- [ ] Generate Codex skill guidance from the same lifecycle model.
- [ ] Mark generated agent files as generated and include source digests.
- [ ] Report stale generated agent guidance.
- [ ] Keep Claude and Codex behavior equivalent when workflow behavior changes.

Exit criteria:

- Agent guidance is generated from structured SDD data, not hand-maintained as a
  second source of truth.
- Stale guidance is detected when lifecycle contracts change.
- Agent instructions identify the same authored sources and generated views as
  the CLI.

### Phase 6: Bootstrap and migration experience

Purpose: make FS.GG.SDD useful for new products and existing Spec Kit projects.

- [ ] Add project templates for a new SDD-governed product skeleton.
- [ ] Provide migration guidance from existing Spec Kit projects to native SDD
  artifacts.
- [ ] Preserve standard Spec Kit as a valid development workflow for the SDD repo
  itself.
- [ ] Add quickstart docs for `fsgg-sdd init` through `fsgg-sdd ship`.
- [ ] Add smoke tests that create a temporary SDD project and run the lifecycle
  without Governance installed.

Exit criteria:

- A new project can initialize the SDD skeleton and continue through the native
  lifecycle.
- Existing Spec Kit users have a documented migration path.
- Bootstrap does not assume FS.GG.Rendering, Governance, or a monorepo checkout.

### Phase 7: Optional Governance integration

Purpose: expose SDD readiness to Governance without making Governance required.

- [ ] Define versioned readiness JSON consumed by FS.GG.Governance.
- [ ] Add contract tests or fixtures for Governance-facing outputs.
- [ ] Document version compatibility and failure modes.
- [ ] Keep route, profile, freshness, and gate enforcement in Governance.
- [ ] Ensure SDD degrades explicitly when Governance tooling is absent.

Exit criteria:

- Governance can inspect SDD artifacts through explicit structured contracts.
- SDD remains independently buildable, testable, and usable.
- Integration failures are reported as optional integration failures, not as
  core SDD lifecycle failures.

### Phase 8: Release readiness

Purpose: prepare SDD itself for distribution once the lifecycle surface is
stable.

- [ ] Add package identity and versioning policy for `FS.GG.SDD.*`.
- [ ] Add release checklist and compatibility matrix.
- [ ] Add CLI installation docs.
- [ ] Add generated artifact schema documentation.
- [ ] Add baseline fixtures for public schemas and command output.
- [ ] Add migration notes for breaking schema or command changes.

Exit criteria:

- SDD packages and CLI can be versioned and released with clear compatibility
  guarantees.
- Public schemas, generated views, and command JSON have documented stability
  rules.
- Breaking changes require explicit migration notes.

## First feature

The first implementation feature should be:

```text
001-sdd-artifact-model
```

It should not add lifecycle commands yet. It should define the artifact model,
schema versioning posture, id types, diagnostics, and deterministic JSON
fixtures that later commands and generators will use.

## Acceptance bar

FS.GG.SDD is useful when a new project can:

1. Initialize an SDD skeleton.
2. Author lifecycle artifacts in Markdown and structured files.
3. Produce a deterministic normalized work model.
4. Generate Claude and Codex guidance from the same contract.
5. Run lifecycle commands without Governance installed.
6. Optionally expose readiness artifacts that Governance can inspect.
7. Detect stale generated views.
8. Record task and evidence state in structured artifacts.
9. Produce verify and ship readiness JSON.
10. Evolve schemas with explicit migration notes.

The central constraint is that SDD must make the spec-driven development loop
executable without becoming the Governance rule engine.
