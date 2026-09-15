---
schemaVersion: 1
workId: 927-single-quint-lifecycle
title: Single Quint-backed workspace lifecycle
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .specify/memory/constitution.md
---

# Single Quint-backed workspace lifecycle Charter

## Identity
- Work id: `927-single-quint-lifecycle`
- Tracking authority: `FS-GG/FS.GG.SDD#927`

## Principles
- One accepted `WorkspaceModel` is semantic authority; prose and direct Quint are authoring projections.
- Issue creation never mutates accepted truth.
- Semantic acceptance is exact-base, human-authorized, evidence-bound, and fail-closed.
- Legacy workspaces remain inspectable, explicitly migratable, and rollback-capable.

## Scope Boundaries
- Implement the producer contract, canonical model, proposal reduction, semantic merge, migration planning, and installed guidance.
- Preserve explicit legacy lifecycle selections during the compatibility window.
- Package publication precedes downstream adoption; the omitted default changes only after `OperatingV2` evidence.
- GitHub retains issue, PR, commit, review, run, and merge identities. SDD owns their declared semantic meaning.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work 927-single-quint-lifecycle`.
