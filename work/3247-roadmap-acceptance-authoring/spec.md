---
schemaVersion: 1
workId: 3247-roadmap-acceptance-authoring
title: Roadmap Single-PR Acceptance
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Roadmap Single-PR Acceptance Specification

Prose status: specified

## User Value
Roadmap workers can seal one already-merged unit PR without inventing a second PR, rewriting lifecycle history, or discarding the independent critique that preceded the final evidence-only commit.

## Scope
- SB-001: Correct roadmap acceptance identity, repository-authority, and critique-ancestry joins for the existing GS2-07.3 unit.

## Non-Goals
- SB-002: Do not weaken exact candidate-tree, merge-tree, live-PR, protected-main, structured-review, lifecycle, qualification, or independent SDD observation checks.
- SB-003: Do not mutate FS-GG/FS.GG.Coordination#304 or PR #305 evidence to fit the validator.

## User Stories
- US-001 (P1): As a roadmap worker, I can use one immutable unit PR as both the implementation and acceptance identity when both roles name the same candidate and merge.
- US-002 (P1): As a reviewer, I can preserve a canonical work-cycle critique of the implementation commit when the final candidate adds only the durable review artifact.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-002]: Given one applied unit PR supplies both roles, when acceptance is inspected, then the shared PR is valid only when candidate and merge identities agree exactly and each candidate differs from its merge.
- AC-002 [US-001] [FR-003]: Given the compiler authority issue lives in a different repository, when the unit binding is checked, then repository authority comes from the applied unit registration.
- AC-003 [US-002] [FR-004]: Given a canonical roadmap work-cycle critique reviewed a predecessor implementation commit, when the cycle suffix binds the routed SDD work identity, then a live comparison proves the final candidate is exactly one commit ahead, uses that reviewed commit as its merge base, and has the canonical critique artifact as its sole `modified` file; unrelated cycles, merge-claiming receipts, divergent history, and any extra final-candidate change are refused.
- AC-004 [US-001] [US-002] [FR-005]: Given the immutable GS2-07.3 facts from #304/#305, when the production route runs at exact engine candidate `8b19392392bc9071bf68335cef8b98ad64304787`, then identity and critique inspection passes and execution reaches the deliberately omitted operation-output controls.

## Functional Requirements
- FR-001: A single pull request MAY fulfill both implementation and acceptance roles when both roles name one exact candidate and one exact merge. (covers AC-001)
- FR-002: Shared-role acceptance MUST still refuse candidate/merge collapse and MUST refuse divergent candidate or merge values for the shared PR. (covers AC-001)
- FR-003: The implementation repository MUST be derived from the applied unit registration, not from the compiler plan-authority issue. (covers AC-002)
- FR-004: A critique cycle MAY use the canonical routed work-cycle identity and review a predecessor implementation commit, but MUST bind the routed work suffix and MUST prove through the live repository comparison that the final candidate is exactly one commit ahead, the reviewed commit is its merge base, and the canonical critique artifact is its sole changed file with status `modified`. It MUST refuse divergent history, merge-claiming receipts, and any additional or differently-statused final-candidate change. (covers AC-003)
- FR-005: The exact GS2-07.3 production reproduction MUST clear all identity and critique refusals without changing its immutable issue, PR, lifecycle, candidate, or merge facts. (covers AC-004)

## Key Entities
- Shared unit PR identity: one PR number, candidate SHA, merge SHA, and repository used by both acceptance roles.
- Applied unit authority: the registration receipt naming the repository and issue that own the unit.
- Work-cycle critique ancestry: the routed work-cycle id and reviewed implementation commit preserved by the durable critique receipt.

## Assumptions
- A final candidate may modify the independent critique artifact after the critic reviewed the implementation commit only when live repository history proves a one-commit, artifact-only delta; this is evidence ancestry, not an unreviewed implementation change.

## Success Criteria
- SC-001: The exact #304/#305 reproduction produces zero identity, repository-authority, or critique-cycle findings.
- SC-002: All positive and inversion tests pass with zero failures.
- SC-003: Analyze, verify, and ship all report ready for this work package.

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Corrects existing roadmap acceptance semantics without changing the acceptance input schema or command vocabulary.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 3247-roadmap-acceptance-authoring`.
