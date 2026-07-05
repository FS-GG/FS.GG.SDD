---
schemaVersion: 1
workId: 001-example
title: Example Work Item
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Example Work Item Specification

<!--
A complete, valid `spec.md` you can copy-adapt. Validated on every build by the live
specification parser (ExampleArtifactsContractTests via Specification.parseSpecificationFacts).
The stable US-/FR-/AC-/SB-/AMB- ids the rest of the lifecycle references are declared here.
See the `specify --input` intent-facts and stable-id grammars in
docs/reference/authoring-contracts.md and [[fs-gg-sdd-specify]].
-->

Prose status: specified

## User Value
create a native specify command

## Scope
- SB-001: one specified work item

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a maintainer, I can specify Example Work Item after chartering the work item.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a chartered work item, when specify runs with intent, then spec.md is created with stable ids.

## Functional Requirements
- FR-001: create a specification artifact with stable ids (Stories: US-001; Acceptance: AC-001)

## Ambiguities
- AMB-001 open: where should durable clarification decisions be recorded?

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 001-example`.
