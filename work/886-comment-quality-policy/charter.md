---
schemaVersion: 1
workId: 886-comment-quality-policy
title: Durable comment-quality policy
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

# Durable comment-quality policy Charter

## Identity
- Work id: `886-comment-quality-policy`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Comments explain non-obvious purpose, invariants, constraints, and trade-offs in the code as it exists today.
- Public documentation defines the caller contract; implementation comments preserve reasoning that the code alone cannot express.
- Comments stand alone: issue references may add context but never replace the explanation.
- Semantic comment quality is a human-review obligation, not something this policy claims can be completely linted.

## Scope Boundaries
- Change the SDD-owned constitution seed and its authoritative policy projections for newly initialized workspaces.
- Keep existing authored constitutions no-clobber; do not silently migrate existing workspaces.
- Preserve the existing public F# surface and persisted schema.
- Publication and fleet adoption are downstream work and are not part of this implementation.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Change tier: Tier 1 because the seeded lifecycle constitution is a cross-repository agent and human workflow contract.
- Verification will pin the authoritative contract, emitted seed, producer constitutions, and no-clobber behavior with exact tests.
- Next lifecycle action: `fsgg-sdd specify --work 886-comment-quality-policy`.
