---
schemaVersion: 1
workId: 833-shipready-public-surface
title: Empty Public Surface
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/833-shipready-public-surface/spec.md
publicOrToolFacingImpact: true
---

# Empty Public Surface Clarifications

## Source Specification
- work/833-shipready-public-surface/spec.md

## Clarification Questions
No clarification questions recorded.

## Answers
No clarification answers recorded.

## Decisions
- DEC-001: Represent the configured-surface match cardinality explicitly as zero,
  one, or many; a zero match is a fact evaluated by readiness, not an omitted check.
- DEC-002: Route only Tier-1 or `publicOrToolFacingImpact: true` work through a
  declared `block-on-ship` public surface; malformed or unreadable configuration
  remains a no-verdict, fail-closed diagnostic.
- DEC-003: Permit a valid explicit non-applicability disposition and internal
  Tier-2 work to bypass the F# signature obligation without weakening a
  public-impact item's obligation.
- DEC-004: The public-impact guidance is generated from the work model for Claude
  and Codex, and it directs signature authoring before implementation.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 833-shipready-public-surface`.
