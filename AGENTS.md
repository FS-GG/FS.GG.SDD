# FS.GG.SDD Agent Context

FS.GG.SDD is the spec-driven development lifecycle product for FS.GG. It is
separate from FS.GG.Governance.

Use standard Spec Kit for all non-trivial work. Product source and tests now
exist; add or change them only through a Spec Kit feature that defines the
artifact contract and verification plan.

Read before acting:

- `.specify/memory/constitution.md`
- `docs/initial-implementation-plan.md`
- `.codex/skills/fs-gg-sdd-project/SKILL.md`

Boundary rules:

- SDD owns charter/spec/clarify/checklist/plan/tasks/evidence/verify/ship
  lifecycle artifacts, normalized work models, generated views, and agent
  command/skill generation.
- `fsgg-sdd evidence` owns declared authored evidence and SDD readiness
  summaries; `fsgg-sdd verify` evaluates SDD-owned verification readiness over
  task/evidence/test/skill obligations, emits `readiness/<id>/verify.json`, and
  points to ship; `fsgg-sdd ship` aggregates SDD-owned merge-boundary readiness,
  emits `readiness/<id>/ship.json`, and points ship-ready work to the
  Governance-owned protected-boundary handoff. Governance-owned effective evidence
  freshness and gate enforcement remain optional downstream concerns.
- Governance owns rule evaluation, evidence freshness, routing, profiles, and
  gate enforcement.
- Markdown is an authoring surface; schema-versioned structured artifacts are
  the machine contract.
- Keep Claude and Codex guidance synchronized when workflow behavior changes.

Do not add product source, package projects, or tests without a Spec Kit feature
that defines the artifact contract and verification plan.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/013-ship-command/plan.md
<!-- SPECKIT END -->
