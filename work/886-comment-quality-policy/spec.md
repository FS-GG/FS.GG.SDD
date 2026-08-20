---
schemaVersion: 1
workId: 886-comment-quality-policy
title: Comment Quality Policy
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Comment Quality Policy Specification

Prose status: specified

## User Value
Newly initialized FS.GG workspaces receive a durable rationale-first comment-quality rule that agents and humans can apply consistently.

## Scope
- SB-001: The SDD-owned constitution seed, authoritative constitution contract, producer constitutions, and exact initialization tests; existing authored constitutions remain untouched.

## Non-Goals
- SB-002: Do not silently migrate or overwrite an existing authored constitution.
- SB-003: Do not introduce an automated semantic-comment linter or claim that semantic quality is completely machine-verifiable.
- SB-004: Do not pin fleet consumers to a new SDD version or publish packages in this work item.

## User Stories
- US-001 (P1): As an FS.GG maintainer, I want every newly initialized workspace to receive the same durable comment-quality policy so reviews can evaluate reasoning against one shared contract.
- US-002 (P1): As an existing workspace author, I want rerunning initialization to preserve my authored constitution so adoption remains explicit and non-destructive.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given an empty workspace, when `fsgg-sdd init` succeeds, then the emitted constitution contains the complete rationale-first policy.
- AC-002 [US-001] [FR-002]: Given the authoritative constitution contract, embedded seed, producer lifecycle constitution, and active workflow constitution, when their policy sections are compared, then they express the same obligations without drift.
- AC-003 [US-002] [FR-003]: Given a workspace with an authored constitution, when initialization is rerun, then its existing bytes remain unchanged.
- AC-004 [US-001] [FR-004]: Given the policy text, when a maintainer reads its enforcement boundary, then it requires human semantic judgement and makes no claim that a linter can prove comment quality.

## Functional Requirements
- FR-001: A fresh init emits a rule requiring comments to describe current code, explain non-obvious purpose, invariants, constraints, trade-offs, and why the implementation has its shape, while forbidding code narration and edit-history preservation. (Stories: US-001; Acceptance: AC-001)
- FR-002: The authoritative constitution contract, embedded seed, producer constitution, and active workflow constitution agree on the complete policy, including the distinction between caller-facing public documentation and implementation reasoning. (Stories: US-001; Acceptance: AC-002)
- FR-003: Initialization preserves an existing authored constitution byte-for-byte and performs no silent migration. (Stories: US-002; Acceptance: AC-003)
- FR-004: The policy states that issue references are supplementary context, comments must stand alone, and semantic quality remains a human-review concern rather than a fully lintable property. (Stories: US-001; Acceptance: AC-004)

## Ambiguities
- AMB-001: Whether the seed and producer constitutions must be byte-identical or may carry structurally equivalent policy wording.
- AMB-002: Whether the existing no-clobber behavior is sufficient or needs a new test specifically tied to this policy.

## Public Or Tool-Facing Impact
- The initialized `.fsgg/constitution.md` content changes for new workspaces.
- No F# public API or persisted JSON/YAML schema changes.
- Existing workspaces adopt explicitly rather than through `init` migration.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 886-comment-quality-policy`.
