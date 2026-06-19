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
command workflow slices through `fsgg-sdd evidence`.

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
  slices through `fsgg-sdd evidence`: a public MVU/report surface, SDD
  skeleton creation, charter/specification authoring, clarification decisions,
  requirements-quality checklist authoring, technical plan authoring, stable
  task graph authoring, authored evidence declarations, question/decision,
  checklist item/result, plan decision/contract/verification, task ids,
  cross-artifact analysis readiness, evidence readiness summaries,
  deterministic JSON/text reports, generated work-model and analysis-view
  refresh/diagnostics, and no required Governance runtime.
- The detailed implementation roadmap lives in
  [docs/initial-implementation-plan.md](docs/initial-implementation-plan.md).

## Workflow

Use standard Spec Kit:

```text
charter -> specify -> clarify -> checklist -> plan -> tasks -> analyze -> evidence
```

For the native SDD product lifecycle, `fsgg-sdd analyze` runs after
`fsgg-sdd tasks`, and `fsgg-sdd evidence` records declared implementation,
verification, synthetic, and deferral evidence before later verify/ship work.

The first implementation feature should create the structured SDD artifact model.
Markdown remains an authoring surface; schema-versioned structured artifacts are
the machine contract.

## Agent Context

- Claude: read [CLAUDE.md](CLAUDE.md) and `.claude/skills/fs-gg-sdd-project/SKILL.md`.
- Codex: read [AGENTS.md](AGENTS.md) and `.codex/skills/fs-gg-sdd-project/SKILL.md`.

## License

MIT.
