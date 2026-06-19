# 0001: Separate SDD Product

## Status

Accepted, 2026-06-19.

## Decision

FS.GG.SDD is a separate repository from FS.GG.Governance.

FS.GG.SDD owns the spec-driven development lifecycle: project charter,
specification, clarification, checklist, plan, tasks, evidence declarations,
normalized work model, generated SDD views, and agent command/skill generation.

FS.GG.Governance owns rule evaluation, evidence freshness, routing, profiles,
gate enforcement, and audit reporting.

## Consequences

- SDD can be developed with Spec Kit and the F# constitution without depending
  on the governance rule engine.
- Governance can inspect or enforce SDD artifacts later through explicit
  optional contracts.
- The first SDD implementation should define artifact schemas and normalized
  work model contracts before building lifecycle commands.
- Rendering remains a customer, not the repository shape.
