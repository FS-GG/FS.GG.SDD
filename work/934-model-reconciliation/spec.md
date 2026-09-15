---
schemaVersion: 1
workId: 934-model-reconciliation
title: Quint model reconciliation and implementation correspondence
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Quint model reconciliation and implementation correspondence Specification

Prose status: specified

## User Value
Reviewers can reconcile concurrent model proposals and trace every accepted obligation to implementation and evidence from one canonical authority.

## Scope
- SB-001: Add pure typed reconciliation and correspondence reports, stable machine and human projections, focused seeded skills, and package-only parity checks.
- SB-002: Reuse the accepted `WorkspaceModel`, `ChangeProposal`, compiled Quint contract, source bindings, tests, and evidence receipts as inputs.
- SB-003: Materialize equivalent workflows into both supported agent-skill roots from one embedded producer source.

## Non-Goals
- SB-004: Do not mutate accepted authority, invent semantics for ambiguous prose, or create a second dashboard-owned coverage model.
- SB-005: Do not change the product version, publish packages, or activate the omitted lifecycle default in this feature branch.

## User Stories
- US-001 (P1): As a model reviewer, I can reconcile exact-base concurrent proposals into one deterministic candidate or a complete readable refusal.
- US-002 (P1): As an implementation reviewer, I can trace each accepted obligation to exact source, test, and evidence observations with distinct coverage outcomes.
- US-003 (P2): As a workspace agent, I receive the same bounded reconciliation and correspondence workflow on Claude and neutral Codex-compatible surfaces.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-002]: Given two valid disjoint proposals at the exact accepted fingerprint, when reconciliation runs in either proposal order, then canonical candidate bytes and stable semantic changes are identical.
- AC-002 [US-001] [FR-002] [FR-003]: Given overlapping declarations, stale bases, changed assumptions, dangling identities, rename/delete conflicts, duplicate declarations, or ambiguous dispositions, when reconciliation runs, then no candidate is returned and every deterministic diagnostic is retained.
- AC-003 [US-002] [FR-004] [FR-005]: Given accepted obligations and complete implementation observations, when correspondence runs, then each obligation is classified as satisfied, missing, stale, contradicted, ambiguous, unsupported, or unobserved with exact bindings and an explanation.
- AC-004 [US-002] [FR-005] [FR-006]: Given forged fingerprints, partial catalogues, hand-edited generated projections, or unreadable inputs, when correspondence runs, then the report fails closed and selective impact never hides the blocking finding.
- AC-005 [US-003] [FR-007] [FR-008]: Given a clean package-only workspace, when SDD skills are seeded, then both agent roots contain byte-identical focused skills whose commands produce the same stable JSON/plain/rich report semantics.

## Functional Requirements
- FR-001: Reconciliation MUST consume one valid accepted `WorkspaceModel` and revision-bound `ChangeProposal` values and MUST remain pure. (Stories: US-001; Acceptance: AC-001)
- FR-002: Reconciliation MUST compute an order-independent semantic three-way merge and MUST distinguish compatible, duplicate, overlapping, stale, assumption-conflicting, rename/delete, dangling, and ambiguous inputs. (Stories: US-001; Acceptance: AC-001, AC-002)
- FR-003: A reconciliation refusal MUST return no candidate model and MUST retain stable path-addressed diagnostics for every detectable conflict. (Stories: US-001; Acceptance: AC-002)
- FR-004: Correspondence MUST derive obligation coverage solely from the accepted model and fingerprinted observations of generated contracts, declared source bindings, tests, and evidence receipts. (Stories: US-002; Acceptance: AC-003)
- FR-005: Correspondence MUST distinguish satisfied, missing, stale, contradicted, ambiguous, unsupported, and unobserved results and MUST fail closed on incomplete or forged input. (Stories: US-002; Acceptance: AC-003, AC-004)
- FR-006: Selective correspondence MUST derive impacted obligations from the compiled impact graph and MUST never suppress a global integrity diagnostic. (Stories: US-002; Acceptance: AC-004)
- FR-007: Machine JSON and plain/rich human projections MUST derive from one typed report and preserve source locations, fingerprints, bindings, explanations, and provenance. (Stories: US-002, US-003; Acceptance: AC-003, AC-005)
- FR-008: The producer MUST embed and seed focused reconciliation and correspondence skills byte-identically into `.claude/skills` and `.agents/skills`, with no copied semantic implementation. (Stories: US-003; Acceptance: AC-005)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 934-model-reconciliation`.
