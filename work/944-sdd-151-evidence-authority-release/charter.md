---
schemaVersion: 1
workId: 944-sdd-151-evidence-authority-release
title: SDD 1.5.1 evidence-authority release
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

# SDD 1.5.1 evidence-authority release Charter

## Identity
- Work id: `944-sdd-151-evidence-authority-release`
- Lifecycle stage: charter
- Status: chartered

## Principles
- Release immutable coherent bytes: the CLI and Artifacts package move together to stable 1.5.1 while FS.GG.Contracts remains 7.5.2.
- A protected-main anomaly must be diagnosed from the exact run and reproduced or bounded; qualification never weakens a non-vacuity assertion to obtain green.
- Release evidence binds the candidate commit, package bytes, clean installation, and candidate-owned local-evidence behavior.

## Scope Boundaries
- In: diagnose the one-off RefreshEvidenceDeadlockTests failure, prepare version 1.5.1 projections/docs/baselines, and prove package/API/release gates.
- Out: changing public Contracts version 7.5.2, publishing/tagging/releasing before independent review and host authorization, or implementing unrelated lifecycle behavior.

## Policy Pointers
- Constitution II makes structured artifacts authoritative; VI requires discriminating tests; VIII requires fail-closed diagnosis.
- The repository release contract and versioning policy define the coherent CLI/Artifacts set and stable dual-feed handoff.

## Lifecycle Notes
- Tier 1 release contract change: exact versions, package manifests, docs, fixtures, and evidence move together.
