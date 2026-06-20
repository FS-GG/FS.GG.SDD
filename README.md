# FS.GG.SDD

Spec-driven development lifecycle tooling for FS.GG.

FS.GG.SDD is the consumer-facing product a project team uses to develop its own
projects through SDD: initialize a lifecycle skeleton, author specs and plans,
generate typed task/evidence models, produce readiness views, and give humans,
agents, CLI tools, and optional Governance gates the same contract.
Its lifecycle checks are expressed as Governance-compatible rules for artifact
shape, required skills, evidence obligations, and expected tests.

This repository started scaffold-only. Spec Kit features have since added the
packable artifact-model library, normalized work-model generation, and native
command workflow slices through `fsgg-sdd ship`.

## Scope

FS.GG.SDD owns:

- project charter and policy workflow;
- specify, clarify, checklist, plan, tasks, analyze, implement, verify, and ship
  lifecycle commands;
- structured lifecycle artifact schemas;
- normalized work model generation;
- agent command and skill generation;
- generated readiness views for SDD artifacts;
- integration contracts with FS.GG.Governance.

FS.GG.SDD does not own the governance rule engine. Rule evaluation, evidence
freshness, routing, profiles, and gate enforcement belong in
[FS.GG.Governance](https://github.com/FS-GG/FS.GG.Governance).

## Current State

- Spec Kit initialized under `.specify/`.
- F# constitution ratified for this SDD product.
- Claude and Codex guidance files are present.
- `FS.GG.SDD.Artifacts` defines the first typed lifecycle artifact model.
- `FS.GG.SDD.Commands` and `FS.GG.SDD.Cli` provide the native command workflow
  slices through `fsgg-sdd ship`: a public MVU/report surface, SDD
  skeleton creation, charter/specification authoring, clarification decisions,
  requirements-quality checklist authoring, technical plan authoring, stable
  task graph authoring, authored evidence declarations, question/decision,
  checklist item/result, plan decision/contract/verification, task ids,
  cross-artifact analysis readiness, evidence readiness summaries,
  verification readiness (task/evidence/test/skill dispositions over a generated
  `readiness/<id>/verify.json` view pointing to ship), merge-boundary ship
  readiness (aggregated lifecycle/verification/evidence/generated-view state over a
  generated `readiness/<id>/ship.json` view pointing to the protected-boundary
  handoff), deterministic JSON/text reports, generated work-model, analysis-view,
  and verification-view refresh/diagnostics, and no required Governance runtime.
- The detailed implementation roadmap lives in
  [docs/initial-implementation-plan.md](docs/initial-implementation-plan.md).

## Workflow

Use standard Spec Kit:

```text
charter -> specify -> clarify -> checklist -> plan -> tasks -> analyze -> evidence -> verify -> ship
```

For the native SDD product lifecycle, `fsgg-sdd analyze` runs after
`fsgg-sdd tasks`, `fsgg-sdd evidence` records declared implementation,
verification, synthetic, and deferral evidence, `fsgg-sdd verify` evaluates
SDD-owned verification readiness over the task/evidence/test/skill obligations,
emits `readiness/<id>/verify.json`, and points verification-ready work to ship,
and `fsgg-sdd ship` aggregates SDD-owned merge-boundary readiness, emits
`readiness/<id>/ship.json`, and points ship-ready work to the Governance-owned
protected-boundary handoff. `fsgg-sdd agents` is a cross-cutting generator (not a
lifecycle stage) that derives per-target Claude/Codex command and skill guidance
from `readiness/<id>/work-model.json` into
`readiness/<id>/agent-commands/<target>/`, marked generated with source digests
and never a second source of truth.

The first implementation feature should create the structured SDD artifact model.
Markdown remains an authoring surface; schema-versioned structured artifacts are
the machine contract.

For a command-by-command walkthrough from `fsgg-sdd init` through `fsgg-sdd ship`
with no Governance installed, see the [Quickstart](docs/quickstart.md). To adopt
native SDD artifacts from an existing standard Spec Kit project additively, see
[Migration from Spec Kit](docs/migration-from-spec-kit.md).

## Agent Context

- Claude: read [CLAUDE.md](CLAUDE.md) and `.claude/skills/fs-gg-sdd-project/SKILL.md`.
- Codex: read [AGENTS.md](AGENTS.md) and `.codex/skills/fs-gg-sdd-project/SKILL.md`.

## License

MIT.
