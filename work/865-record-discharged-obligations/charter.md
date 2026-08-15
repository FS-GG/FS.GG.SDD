---
schemaVersion: 1
workId: 865-record-discharged-obligations
title: Record Discharged Obligations
stage: charter
changeTier: tier1
status: chartered
policyPointers:
  - .fsgg/sdd.yml
  - .fsgg/agents.yml
  - .fsgg/policy.yml
  - .fsgg/capabilities.yml
  - .fsgg/tooling.yml
---

# Record Discharged Obligations Charter

## Identity
- Work id: `865-record-discharged-obligations`
- Lifecycle stage: charter
- Status: chartered

## Principles
- An obligation that no test run could ever discharge must not be gated as though a test run
  could discharge it. `verify` blocking forever is a defect in the gate's vocabulary, not a
  property of the work.
- Opening a second observable channel must not reopen the first one's hole. FS.GG.SDD#350 closed
  self-attestation for tests; a record channel that accepts `result: pass` alone would undo it.
- A receipt is worth having only if a later reader can re-check it. The receipt names the durable
  artifact; it does not summarise it.
- Hermeticity is a property worth paying for. A gate that reaches the network to decide is a gate
  that cannot be reproduced offline, and every other obligation pays that cost too.
- Consume one shared rule rather than restating it. `verify`, `ship`, and the committed verdict
  must not be able to drift on what discharges an obligation.

## Scope Boundaries
- Keep SDD lifecycle ownership separate from optional Governance enforcement. SDD reports whether
  an obligation was discharged and by what class of evidence; whether that is sufficient to cross a
  merge boundary remains Governance's question (ADR-0035 §3).
- SDD never dereferences a record locator. It reads what is on disk and judges the form of what is
  not.
- No new persisted schema major. The record channel is additive to `evidence.yml` and to the
  `verify`/`ship` views, and every committed package that predates it keeps parsing and keeps its
  current verdict.
- The obligation's discharge class is declared through the existing authored task capability tag.
  This work adds no new spec-level classification facet and does not touch the task generator.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work 865-record-discharged-obligations`.
