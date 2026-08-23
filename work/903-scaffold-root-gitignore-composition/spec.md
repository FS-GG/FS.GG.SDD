---
schemaVersion: 1
workId: 903-scaffold-root-gitignore-composition
title: Scaffold Root Gitignore Composition
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Scaffold Root Gitignore Composition Specification

Prose status: specified

## User Value
Product template authors can provide their root ignore rules while scaffolded workspaces retain SDD lifecycle ignores.

## Scope
- SB-001: The generic SDD scaffold flow, provider process invocation, provenance and command/acceptance regression coverage; no provider-specific knowledge.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can product template authors can provide their root ignore rules while scaffolded workspaces retain SDD lifecycle ignores.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given Scaffold Root Gitignore Composition is available, when the user exercises it, then they can product template authors can provide their root ignore rules while scaffolded workspaces retain SDD lifecycle ignores.

## Functional Requirements
- FR-001: Given a provider that emits a root .gitignore, default scaffold preserves the SDD-generated and provider-authored entries without --force; a mutation that reintroduces direct overwrite exits with a conflict or loses a required entry; explicit --force remains separately observable. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 903-scaffold-root-gitignore-composition`.
