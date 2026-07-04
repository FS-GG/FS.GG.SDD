---
schemaVersion: 1
workId: 001-example
title: Example Work Item
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/001-example/spec.md
publicOrToolFacingImpact: true
---

# Example Work Item Clarifications

<!--
A complete, valid `clarifications.md` you can copy-adapt. It is validated on every
build by the live clarification parser (ExampleArtifactsContractTests), so it can
never drift from the tool. See docs/reference/authoring-contracts.md for the
load-bearing grammars, and [[fs-gg-sdd-clarify]] for the stage guide.
-->

## Source Specification
- work/001-example/spec.md

## Clarification Questions
- **CQ-001** (AMB-001): Does a serve after a point go to the loser or alternate?
- **CQ-002** (AMB-002): Is there a win condition, or is the rally endless?

## Answers
- CQ-001 → the serve goes to the player who lost the prior rally (resolves AMB-001).
- CQ-002 → endless rally for this work item; scoring is tracked but there is no
  match end (resolves AMB-002 as an accepted deferral).

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]: The serve targets the
  prior-rally loser.

## Accepted Deferrals
- **DEC-002** [CQ-002] [AMB:AMB-002]: A match-end/win condition is deferred to a
  later work item — recorded, not dropped.

## Remaining Ambiguity
- None. AMB-001 and AMB-002 are resolved above.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 001-example`.
