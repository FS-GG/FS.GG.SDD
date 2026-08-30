---
schemaVersion: 1
workId: 942-local-evidence-authority
title: Local Evidence Authority
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/942-local-evidence-authority/spec.md
publicOrToolFacingImpact: true
---

# Local Evidence Authority Clarifications

## Source Specification
- work/942-local-evidence-authority/spec.md

## Clarification Questions
- **CQ-001**: What establishes that a local evidence path survives an exact-candidate checkout?
- **CQ-002**: How is non-local evidence represented without forcing it through Git path validation?

## Answers
- CQ-001 → Exact `HEAD` object membership at the candidate workspace is the durable local-authority test; filesystem existence, index-only membership, a Git archive without repository metadata, or a failed Git command is insufficient.
- CQ-002 → `sourceRefs[].uri` is the explicit external receipt form and is not a local path.

## Decisions
- **DEC-001** [CQ-001] [FR-001] [FR-002] [FR-004]: Add one Git-aware local-authority classification used by evidence, verify, and ship; a missing Git answer blocks rather than silently accepting the path.
- **DEC-002** [CQ-002] [FR-003]: Preserve schema version 1 and treat a non-empty `sourceRefs[].uri` as the explicit durable external receipt form.
- **DEC-003** [FR-005]: Prove the boundary with independently discriminating fixtures for ignored, untracked, staged-only, clean-checkout-missing, Git-archive/no-repository, unavailable-Git-command, tracked, and external-receipt cases.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 942-local-evidence-authority`.
