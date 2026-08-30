---
schemaVersion: 1
workId: 942-local-evidence-authority
title: Clean-checkout-reproducible local evidence authority
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

# Clean-checkout-reproducible local evidence authority Charter

## Identity
- Work id: `942-local-evidence-authority`
- Lifecycle stage: charter
- Status: chartered

## Principles
- A ship-ready verdict must be reproducible from the reviewed Git candidate, not from ignored or untracked files in one checkout.
- Ordinary tracked evidence remains valid; durable external evidence must use an explicit receipt rather than masquerading as a local path.
- Validation belongs at the evidence authority boundary shared by `verify` and `ship`, with actionable diagnostics and fail-closed behavior.

## Scope Boundaries
- In: local evidence provenance, durable external receipts, command diagnostics, clean-checkout reproduction, and inversion tests.
- Out: Governance routing or gate enforcement, changing evidence satisfaction semantics, and rejecting ordinary tracked evidence.

## Policy Pointers
- Constitution II makes structured artifacts the machine contract; VI requires fail-before/pass-after evidence; VIII requires actionable safe failure.
- The SDD evidence and ship contracts remain independently usable without Governance.

## Lifecycle Notes
- Tier 1 contract change: diagnostics and accepted evidence authority are tool-visible behavior, so source, tests, documentation, and migration posture move together.
