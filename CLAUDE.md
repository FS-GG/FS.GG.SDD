# FS.GG.SDD Agent Context

FS.GG.SDD is the spec-driven development lifecycle product for FS.GG. It is not
the governance rule engine.

Use standard Spec Kit for work in this repo. Product code must be introduced
through feature specs and plans; this scaffold intentionally starts without
`src/` or `tests/`.

Core boundary:

- FS.GG.SDD owns lifecycle artifacts, normalized work models, generated SDD
  views, lifecycle CLI contracts, and agent command/skill generation.
- FS.GG.Governance owns rule evaluation, evidence freshness, routing, profiles,
  and gate enforcement.
- Integrations between them must be explicit, versioned, and optional until
  adopted by a feature spec.

When working here:

- Follow the constitution at `.specify/memory/constitution.md`.
- Treat Markdown as authoring surface and structured artifacts as machine
  contracts.
- Keep Claude and Codex behavior aligned; update both agent surfaces when the
  workflow changes.
- Do not add rendering-specific package names, templates, paths, or docs URLs to
  generic SDD behavior.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/007-checklist-command/plan.md
<!-- SPECKIT END -->
