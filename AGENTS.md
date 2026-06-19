# FS.GG.SDD Agent Context

FS.GG.SDD is the spec-driven development lifecycle product for FS.GG. It is
separate from FS.GG.Governance.

Use standard Spec Kit for all non-trivial work. This repository is intentionally
source-empty until the first feature spec creates code.

Read before acting:

- `.specify/memory/constitution.md`
- `docs/initial-implementation-plan.md`
- `.codex/skills/fs-gg-sdd-project/SKILL.md`

Boundary rules:

- SDD owns charter/spec/clarify/checklist/plan/tasks/evidence lifecycle
  artifacts, normalized work models, generated views, and agent command/skill
  generation.
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
at specs/001-sdd-artifact-model/plan.md
<!-- SPECKIT END -->
