---
name: fs-gg-sdd-project
description: Work on FS.GG.SDD, the FS.GG spec-driven development lifecycle product. Use when creating or editing SDD lifecycle specs, plans, schemas, generated views, agent commands, or Governance integration contracts.
---

# FS.GG.SDD Project Skill

Use this skill for work in the FS.GG.SDD repository.

## Boundaries

- FS.GG.SDD owns the lifecycle product: charter, specify, clarify, checklist,
  plan, tasks, analyze, evidence declarations, normalized work model, generated
  SDD views, and agent command/skill generation.
- FS.GG.Governance owns rule evaluation, evidence freshness, routing, profiles,
  and gate enforcement.
- FS.GG.Rendering is a possible customer, not the shape of this repository.

## Required Reading

Before changing behavior, read:

1. `.specify/memory/constitution.md`
2. `docs/initial-implementation-plan.md`
3. `README.md`

## Working Rules

- Use standard Spec Kit: specify -> clarify as needed -> plan -> tasks ->
  implement -> analyze.
- Do not add source projects to the scaffold without a feature spec.
- Treat Markdown as authoring surface and schema-versioned structured artifacts
  as the machine contract.
- For every lifecycle artifact, identify the authored source, structured model,
  generated views, stale-view behavior, and diagnostics.
- Keep Claude and Codex guidance equivalent when workflow behavior changes.
- Integrate Governance only through explicit optional contracts.

## First Feature Bias

The first implementation feature should define the SDD artifact model and
schemas before commands or generators are built.
