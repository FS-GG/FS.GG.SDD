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

# Broken Example Clarifications (unresolved blocking ambiguity)

<!--
Deliberately broken fixture for feature 076 lint (SC-001, MissingDecisionTag class):
AMB-001 is NOT resolved by any [AMB:AMB-001]-tagged decision, and is listed as a
blocking remaining ambiguity. Do NOT copy this as a template.
-->

## Source Specification
- work/001-example/spec.md

## Clarification Questions
- **CQ-001** (AMB-001): Does a serve after a point go to the loser or alternate?

## Answers
- CQ-001 → undecided for now.

## Decisions
- None.

## Accepted Deferrals
- None.

## Remaining Ambiguity
- **AMB-001** blocking: The serve target after a point is still undecided.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 001-example`.
