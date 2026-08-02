---
schemaVersion: 1
workId: 110-empty-public-surface
title: Empty Public Surface
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/110-empty-public-surface/spec.md
sourceClarifications: work/110-empty-public-surface/clarifications.md
sourceChecklist: work/110-empty-public-surface/checklist.md
publicOrToolFacingImpact: true
---

# Empty Public Surface Plan

Prose status: planned

## Source Snapshot
- spec: work/110-empty-public-surface/spec.md sha256:79109adf1ace08961ee34e2ead3269a739fa6038ba360e88917a9a1de02766c6 schemaVersion:1
- clarifications: work/110-empty-public-surface/clarifications.md sha256:8957ae94ed9ce33b96e9798d54f0eb7acdc8c80eefdecd9b6ed9b23b26641ef0 schemaVersion:1
- checklist: work/110-empty-public-surface/checklist.md sha256:be53c07ef055cc7ae7f752e2a5279e3f6819e67a0d08c1d7d88c2526b648c1ac schemaVersion:1

## Plan Scope
- Work item 110-empty-public-surface is planned from the current specification, clarification, and checklist facts.
- Requirement count: 5.
- Clarification decision count: 4.
- Checklist result count: 5.

## Plan Decisions
- PD-001 [AC-001] [FR-001] complete: Add an explicit zero/one/many configured-surface match model and route an applicable empty block-on-ship F# surface into verify and ship blocking findings.
- PD-002 [AC-002] [FR-002] complete: Project a stable diagnostic with the work id, impact signal, configured glob, and required `.fsi` declaration mechanism.
- PD-003 [AC-003] [FR-003] complete: Parse and validate non-applicability separately from missing configuration, preserving Tier-2 and explicitly non-applicable controls.
- PD-004 [AC-004] [FR-004] complete: Cover empty, malformed, non-applicable, and populated compiled-signature fixtures at lifecycle and CLI boundaries.
- PD-005 [AC-005] [FR-005] complete: Route the public-impact surface obligation into generated Claude and Codex guidance before implementation.

## Contract Impact
- PC-001 [PD-001] lifecycle artifact: Add a version-compatible surface-match cardinality and readiness finding contract in Artifacts.
- PC-002 [PD-002] command report: Preserve deterministic JSON/text diagnostic projection through Commands and CLI.
- PC-003 [PD-003] schema: Add only an explicit validated non-applicability disposition; malformed and unreadable policy remain no-verdict.
- PC-004 [PD-005] agent guidance: Keep generated Claude and Codex guidance byte-equivalent for the signature-first instruction.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Rogue3-shaped public-impact + zero `.fsi` fixture is red at verify and ship.
- VO-002 [PD-003] [PC-003] semanticTest: Valid non-applicable and internal Tier-2 fixtures remain green; malformed surface configuration is no-verdict.
- VO-003 [PD-004] [PC-001] semanticTest: A compiled signature fixture clears the empty-surface finding.
- VO-004 [PD-005] [PC-004] semanticTest: Generated Claude and Codex guidance contains the signature-first instruction.

## Performance Intent
No performance intent is declared for this work item.

## Migration Posture
- PM-001 [PC-001] additive: Keep persisted schema versions compatible; consumers that cannot inspect configured surfaces report no verdict rather than certify an empty one.

## Generated View Impact
- GV-001 [PD-001] workModel: The work model carries public impact into the configured-surface obligation and reports stale generated views.
- GV-002 [PD-005] agentCommands: `readiness/110-empty-public-surface/agent-commands/{claude,codex}` projects the same signature-first instruction.

## Accepted Deferrals
No accepted plan deferrals recorded.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd tasks --work 110-empty-public-surface`.
