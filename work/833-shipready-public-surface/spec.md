---
schemaVersion: 1
workId: 833-shipready-public-surface
title: Empty Public Surface
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Empty Public Surface Specification

Prose status: specified

## User Value
Product authors cannot ship Tier-1 or public-impact F# work with a declared blocking public surface that contains no signatures.

## Scope
- SB-001: Surface matching, lifecycle readiness, diagnostics, guidance, and fixtures; no Governance enforcement or package reflection.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a product author, I receive a blocking, actionable readiness result before an undeclared public F# API ships.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a Tier-1 or public-impact work item and a block-on-ship F# surface whose configured glob matches zero `.fsi` files, when verify or ship runs, then the readiness verdict is not shipReady.
- AC-002 [US-001] [FR-002]: Given the empty configured F# surface, when the lifecycle reports the finding, then it names the work item, impact signal, configured glob, and `.fsi` declaration mechanism.
- AC-003 [US-001] [FR-003]: Given a valid explicitly non-applicable surface disposition or an internal Tier-2 item, when the configured surface has no signatures, then lifecycle readiness remains supported without fabricated signatures.
- AC-004 [US-001] [FR-004]: Given a populated compiled F# signature surface, when readiness runs, then the empty-surface finding is absent.
- AC-005 [US-001] [FR-005]: Given a public-impact F# work model, when generated agent guidance is produced, then it tells the implementer to author or update signatures before implementation hardens the surface.

## Functional Requirements
- FR-001: Verify and ship must block a declared block-on-ship F# surface that resolves to zero signatures unless a validated non-applicability disposition allows it. (Stories: US-001; Acceptance: AC-001)
- FR-002: The blocking diagnostic MUST identify the configured glob, work item, public-impact signal, and F# `.fsi` declaration mechanism. (Stories: US-001; Acceptance: AC-002)
- FR-003: The lifecycle MUST preserve explicit valid non-applicability and internal Tier-2 cases without fabricating a public signature surface. (Stories: US-001; Acceptance: AC-003)
- FR-004: A populated compiled F# signature surface MUST clear the empty-surface finding. (Stories: US-001; Acceptance: AC-004)
- FR-005: Generated Codex and Claude guidance MUST direct public-impact F# work to declare/update signatures before implementation hardens the surface. (Stories: US-001; Acceptance: AC-005)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This changes the typed surface-match and lifecycle readiness contracts, ship/verify diagnostics, and generated Claude/Codex guidance.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 833-shipready-public-surface`.
