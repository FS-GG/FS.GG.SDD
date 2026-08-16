---
schemaVersion: 1
workId: 869-refresh-evidence-deadlock
title: Break The Refresh Evidence Deadlock
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

# Break The Refresh Evidence Deadlock Charter

## Identity
- Work id: `869-refresh-evidence-deadlock`
- Lifecycle stage: charter
- Status: chartered

## Principles
- A lifecycle must not require its own output as its own input. `refresh` refusing until
  `evidence` has run, while `evidence` refuses until `refresh` has run, is not a strict gate; it
  is a state no documented command can leave.
- A generated view is downstream of the artifacts it normalizes. The work model demanding a
  declaration that only a later stage authors inverts the direction the lifecycle already
  declares, and the inversion is the defect.
- An incomplete artifact and an inconsistent one are different facts and must not share a
  verdict. "This reference will be satisfied by a stage that has not run" is progress; "this
  reference names something that will never exist" is a defect.
- A diagnostic names where the failure LIVES, not where it was DETECTED. A hard-coded artifact
  argument is an accusation the tool cannot support, and it costs the author the whole
  investigation (`.github#266`).
- Where the tool already computes the fact the author needs, refusing to say it is the bug. The
  command that blocks here already names the exact missing declaration in the same report.

## Scope Boundaries
- Keep SDD lifecycle ownership separate from optional Governance enforcement. SDD reports whether
  a lifecycle is derivable and what remains undeclared; whether that may cross a merge boundary
  remains Governance's question.
- No enforcement is deleted, only relocated to the stage that owns it. An evidence obligation that
  is never declared must still block `evidence`, `verify` and `ship`.
- No new persisted schema major. The change is additive to the diagnostic vocabulary and to the
  work model's `diagnostics` array; every committed package keeps parsing and keeps its verdict.
- The remedy is reachable through the documented commands with no hand-editing of `evidence.yml`
  and no deletion of an authored artifact.

## Policy Pointers
- SDD policy comes from `.fsgg/sdd.yml` and `.fsgg/agents.yml`.
- Governance files are optional compatibility pointers and are not evaluated by this command.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd specify --work 869-refresh-evidence-deadlock`.
