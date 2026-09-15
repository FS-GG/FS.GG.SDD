---
schemaVersion: 1
workId: 934-model-reconciliation
title: Quint model reconciliation and implementation correspondence
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/934-model-reconciliation/spec.md
publicOrToolFacingImpact: true
---

# Quint model reconciliation and implementation correspondence Clarifications

## Source Specification
- work/934-model-reconciliation/spec.md

## Clarification Questions
No clarification questions recorded.

## Answers
No clarification answers recorded.

## Decisions
- DEC-001 [FR-001] [FR-002]: Extend the public workspace lifecycle kernel with deterministic JSON codecs for `WorkspaceModel` and `ChangeProposal`; CLI workflows consume those exact bytes rather than reconstructing records from prose.
- DEC-002 [FR-004] [FR-005]: One correspondence observation names an obligation, observation kind, observed fingerprint, source bindings, test bindings, evidence references, and an explanation. The accepted model fingerprint and observation-set fingerprint bind the complete report.
- DEC-003 [FR-005]: Classification precedence is integrity-first: malformed or contradictory observations refuse the report globally; otherwise each obligation is classified distinctly without collapsing missing, stale, ambiguous, unsupported, or unobserved into failure.
- DEC-004 [FR-006]: Selective checking accepts changed subject ids and follows the compiled contract relationship/impact graph to obligations. Global catalogue/fingerprint diagnostics are always retained even when no selected obligation is impacted.
- DEC-005 [FR-007]: Stable JSON is the machine contract. Plain and rich output are presentation projections over the same report; the rich view adds navigation labels but no stored coverage state or percentage authority.
- DEC-006 [FR-008]: Ship two focused skills, `fs-gg-sdd-typed-reconcile` and `fs-gg-sdd-typed-correspond`, because their inputs, stopping conditions, and mutation boundaries differ materially.

## Accepted Deferrals
- The coherent `2.0.0` version bump, publication, registry adoption, and lifecycle-default activation remain separate release/cutover effects after this feature lands.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work 934-model-reconciliation`.
