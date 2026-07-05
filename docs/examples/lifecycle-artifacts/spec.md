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
specification parser (ExampleArtifactsContractTests) AND run through the real specify/
checklist gates by the skill↔gate doctest (SkillGateDoctestTests), so it can never
teach a form the gate rejects. The stable US-/FR-/AC-/SB-/AMB- ids the rest of the
lifecycle references are declared here. Note the coverage-line grammar: a non-bold
`- FR-###:` list item carrying its acceptance reference on the SAME physical line
(`(covers AC-###)`). A bold `**FR-###**`, a colon-less line, or an acceptance ref on
its own line is counted but NOT covered. See the `specify --input` intent-facts and
stable-id grammars in docs/reference/authoring-contracts.md and [[fs-gg-sdd-specify]].
-->

Prose status: specified

## User Value
Two players can rally a ball with paddles, and the serve after each point is
unambiguous, so a match feels fair.

## Scope
- SB-001: one specified work item covering serve direction and rally scoring

## Non-Goals
- SB-002: Do not implement a match-end/win condition or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a player, I can rally the ball and have the next serve go to the player who lost the prior rally.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a rally has just ended, when the next serve occurs, then the ball serves toward the player who lost the prior rally.
- AC-002 [US-001] [FR-002]: Given an ongoing match, when a point is scored, then the rally score updates and play continues without requiring a match-end condition.

## Functional Requirements
- FR-001: The system MUST serve the ball toward the player who lost the prior rally. (covers AC-001)
- FR-002: The system MUST record the rally score for each point without requiring a match-end condition. (covers AC-002)

## Ambiguities
- AMB-001 open: does the serve after a point go to the loser or alternate between players?
- AMB-002 open: is there a match-end/win condition, or is the rally endless?

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 001-example`.
