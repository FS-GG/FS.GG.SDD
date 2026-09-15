---
schemaVersion: 1
workId: 934-model-reconciliation
title: Quint model reconciliation and implementation correspondence
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .specify/memory/constitution.md
---

# Quint model reconciliation and implementation correspondence Charter

## Identity
- Work id: `934-model-reconciliation`
- Tracking authority: `FS-GG/FS.GG.SDD#934`

## Principles
- Reconciliation is a pure exact-base decision over the canonical `WorkspaceModel`; it never mutates accepted authority.
- Correspondence derives from accepted model obligations and observed bindings, tests, and receipts; it never stores a second coverage model.
- Incomplete, stale, forged, contradictory, ambiguous, unsupported, and unobserved inputs remain distinct fail-closed outcomes.
- Machine JSON, plain text, rich text, and skill guidance project the same typed report.

## Scope Boundaries
- Add typed reconciliation/correspondence contracts, deterministic codecs and projections, and CLI operations.
- Add focused reconciliation and correspondence skills, embedded and seeded byte-identically for Claude and Codex-compatible roots.
- Preserve the accepted WorkspaceModel and Quint compiler as sole semantic authorities; no dashboard-owned percentages or hand-authored mirror is introduced.
- Keep the 2.0.0 version bump and package publication in a separate coherent release item after this feature lands.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work 934-model-reconciliation`.
