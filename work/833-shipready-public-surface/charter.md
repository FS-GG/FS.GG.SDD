---
schemaVersion: 1
workId: 833-shipready-public-surface
title: Block ship readiness for declared but empty public signature surfaces
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .fsgg/policy.yml
  - .fsgg/capabilities.yml
  - .fsgg/tooling.yml
---

# Block ship readiness for declared but empty public signature surfaces Charter

## Identity
- Work id: `833-shipready-public-surface`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Honor constitution I and III: lifecycle semantics and the F# public surface are
  declared before implementation, with a regression fixture proving the contract.
- Preserve SDD's optional Governance boundary and fail closed only when a declared
  blocking public-surface obligation applies.

## Scope Boundaries
- In: normalized surface-match state, Tier-1/public-impact routing, diagnostics,
  ship/verify fixtures, and generated agent guidance.
- Out: Governance gate enforcement, package API reflection, and requiring signatures
  for explicitly internal or non-applicable products.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Tier 1 lifecycle contract change. The Rogue3-shaped fixture must remain red until
  a compiled F# signature surface exists or a validated non-applicability disposition
  is declared.
