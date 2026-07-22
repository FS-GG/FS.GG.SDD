# Feature 098: Done-task evidence bootstrap

**Issue**: FS.GG.SDD#663
**Tier**: Tier 1 — lifecycle command and seeded-agent guidance contract

## Problem

The documented lifecycle says to analyze, implement, and then author evidence. An author who
records implementation truth by marking tasks `done` before the first `evidence.yml` exists is
blocked by `doneTaskMissingEvidence` before the evidence stage can create those declarations.
Recovery currently requires temporarily falsifying task status and regenerating several stages.

## User story

After analyzing and implementing work, an author can leave every completed task marked `done` and
run `fsgg-sdd evidence --work <id>` to create its missing stable evidence declarations.

## Requirements

- **FR-001**: The evidence command MUST treat `doneTaskMissingEvidence` as a scaffoldable bootstrap
  condition when the completed tasks carry stable `EV###` obligations.
- **FR-002**: Every other task diagnostic MUST retain its existing blocking behavior.
- **FR-003**: Bootstrap MUST NOT modify `tasks.yml` or any authored task status.
- **FR-004**: New declarations MUST use the existing no-clobber merge and MUST begin unsatisfied
  (`kind: missing`, `result: missing`).
- **FR-005**: Verify and ship MUST remain blocked until the declarations are honestly satisfied and
  verification passes carry an observed-run receipt.
- **FR-006**: Repeating bootstrap or receipt registration over unchanged inputs MUST be byte-idempotent.
- **FR-007**: Tasks/evidence guidance and the quickstart MUST show the direct sequence and forbid the
  temporary status rollback.

## Acceptance scenarios

- **AC-001**: Given current analysis, done tasks, and no `evidence.yml`, evidence writes a missing
  declaration for every stable obligation while `tasks.yml` remains byte-identical.
- **AC-002**: Verify blocks after scaffolding and again after an unobserved self-attested pass.
- **AC-003**: After the author types verification passes, materializes the cited test, and registers
  a passing TRX, verify can proceed without any task-status rewrite.
- **AC-004**: A second run with the same TRX leaves `evidence.yml` byte-identical.

## Out of scope

- Weakening the done-task evidence invariant at tasks, analyze, verify, or ship.
- Manufacturing passing evidence or observed-run receipts during scaffolding.
- Inventing obligations for hand-authored done tasks that declare no stable `requiredEvidence` id.
