---
schemaVersion: 1
workId: 857-post-evidence-analyze-replay
title: Post Evidence Analyze Replay
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Post Evidence Analyze Replay Specification

Prose status: specified

## User Value
Lifecycle owners can replay a ship-ready package without generated readiness artifacts
changing forever, while evidence still honestly detects changed sources.

## Scope
- SB-001: analyze, evidence provenance, generated work-model currency, regression coverage, and replay documentation

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can make ship-ready lifecycle replays converge without weakening stale evidence detection.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a ship-ready package with observed evidence, when `analyze -> evidence -> verify -> ship -> refresh -> agents` is run twice, then every command in the second replay reports noChange and the generated artifacts remain byte-identical.

## Functional Requirements
- FR-001: Analyze MUST retain the evidence-enriched work-model source after evidence exists; work-model source identity MUST omit only tool-owned evidence source-snapshot payload while preserving sourceAnalysis, evidence declarations, and stale-source detection. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 857-post-evidence-analyze-replay`.
