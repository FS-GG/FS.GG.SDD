---
schemaVersion: 1
workId: 892-rendering-skills-v2-consumer
title: Rendering Skills V2 Consumer
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Rendering Skills V2 Consumer Specification

Prose status: specified

## User Value
Any SDD scaffold can consume the Rendering owner skill package without losing declared supporting files.

## Scope
- SB-001: Rendering Skills schema-v2 consumer materialization and compatibility only; producer publication remains owned by Rendering.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can any SDD scaffold can consume the Rendering owner skill package without losing declared supporting files.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given Rendering Skills V2 Consumer is available, when the user exercises it, then they can any SDD scaffold can consume the Rendering owner skill package without losing declared supporting files.

## Functional Requirements
- FR-001: Every predicate-selected schema-v2 row verifies its complete declared file set before writing every declared file to every agent root, and any missing, altered, or undeclared file writes nothing. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 892-rendering-skills-v2-consumer`.
