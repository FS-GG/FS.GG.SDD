---
schemaVersion: 1
workId: 001-example
title: Example Work Item
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

# Example Work Item Charter

<!--
A complete, valid `charter.md` you can copy-adapt. Validated on every build by the
live charter front-matter parser (CharterExampleContractTests). `status: chartered`
is written by `fsgg-sdd charter`; do not hand-edit the status. See the per-stage
front-matter grammar in docs/reference/authoring-contracts.md and [[fs-gg-sdd-charter]].
-->

## Identity
- Work id: `001-example`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Capture the work item's local principles before specification begins.

## Scope Boundaries
- Keep SDD lifecycle ownership separate from optional Governance enforcement.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work 001-example`.
