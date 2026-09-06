---
schemaVersion: 1
workId: 3247-roadmap-acceptance-authoring
title: Roadmap Single-PR Acceptance
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
---

# Roadmap Single-PR Acceptance Charter

## Identity
- Work id: `3247-roadmap-acceptance-authoring`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Preserve the immutable roadmap-unit evidence already accepted by review and GitHub.
- Fail closed at every candidate-tree, merge-tree, live-PR, protected-main, and SDD-observer boundary.

## Scope Boundaries
- Specify the correction required for FS-GG/.github#3251 and its GS2-07.3 production pilot.
- Keep implementation ownership in the coordination engine repository.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work 3247-roadmap-acceptance-authoring`.
