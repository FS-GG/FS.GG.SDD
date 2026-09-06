---
schemaVersion: 1
workId: 3247-roadmap-acceptance-authoring
title: Roadmap Single-PR Acceptance
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/3247-roadmap-acceptance-authoring/spec.md
publicOrToolFacingImpact: true
---

# Roadmap Single-PR Acceptance Clarifications

## Source Specification
- work/3247-roadmap-acceptance-authoring/spec.md

## Clarification Questions
No clarification questions recorded.

## Answers
No clarification answers recorded.

## Decisions
- DEC-001 [FR-001] [FR-002]: Shared-role identity is one explicit mode: the PR numbers match, both candidate values match, and both merge values match; distinct-role rules remain unchanged otherwise.
- DEC-002 [FR-003]: Applied unit registration is the sole repository authority for both unit bindings and lifecycle source checks.
- DEC-003 [FR-004]: A canonical routed work-cycle suffix is accepted only when a live repository comparison reports the final candidate exactly one commit ahead, the reviewed commit as merge base, and the canonical critique JSON as the sole `modified` file; an unrelated cycle, divergent history, extra or differently-statused file, or receipt naming the generated merge is refused.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 3247-roadmap-acceptance-authoring`.
