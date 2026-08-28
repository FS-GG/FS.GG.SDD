---
schemaVersion: 1
workId: 937-sdd-modernization
title: Modernize SDD evidence and CI
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Modernize SDD evidence and CI Specification

Prose status: specified

## User Value
Reduce delivery latency while preserving trustworthy readiness.

## Scope
- SB-001: Evidence readiness semantics, generated guidance, and pull-request CI selection in FS.GG.SDD.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can reduce delivery latency while preserving trustworthy readiness.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given Modernize SDD evidence and CI is available, when the user exercises it, then they can reduce delivery latency while preserving trustworthy readiness.

## Functional Requirements
- FR-001: A passing declaration with a current observed-run receipt satisfies an obligation regardless of synthetic provenance metadata. (Stories: US-001; Acceptance: AC-001)
- FR-002: CI classifies changed paths as Small, Normal, or High and defaults unknown or ambiguous input to High. (Stories: US-001; Acceptance: AC-001)
- FR-003: High changes run full tests plus formal, API, build-configuration, and dependency-surface controls. (Stories: US-001; Acceptance: AC-001)
- FR-004: Required job names remain stable and report a successful classified skip when a control is not applicable. (Stories: US-001; Acceptance: AC-001)
- FR-005: High changes require exact-candidate independent critic review before merge. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 937-sdd-modernization`.
