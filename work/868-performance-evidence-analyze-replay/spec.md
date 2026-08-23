---
schemaVersion: 1
workId: 868-performance-evidence-analyze-replay
title: Keep active performance evidence in post-evidence analyze replay
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Keep active performance evidence in post-evidence analyze replay Specification

Prose status: specified

## User Value
Direct CLI lifecycle replay preserves active performance-evidence-v1 artifacts after ordinary evidence has been generated.

## Scope
- SB-001: src/FS.GG.SDD.Commands/CommandWorkflow/ViewGeneration.fs, src/FS.GG.SDD.Commands/CommandWorkflow/HandlersAnalyze.fs, and tests/FS.GG.SDD.Commands.Tests.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can direct CLI lifecycle replay preserves active performance-evidence-v1 artifacts after ordinary evidence has been generated.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given Keep active performance evidence in post-evidence analyze replay is available, when the user exercises it, then they can direct CLI lifecycle replay preserves active performance-evidence-v1 artifacts after ordinary evidence has been generated.

## Functional Requirements
- FR-001: A ship-ready fixture runs analyze, verify, and ship twice with all tracked readiness bytes, including work-model.json, byte-identical after every command. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 868-performance-evidence-analyze-replay`.
