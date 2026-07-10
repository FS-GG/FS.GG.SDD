---
schemaVersion: 1
workId: 001-example
title: Example Work Item
stage: checklist
changeTier: tier1
status: checklistReady
sourceSpec: work/001-example/spec.md
sourceClarifications: work/001-example/clarifications.md
publicOrToolFacingImpact: true
---

# Example Work Item Checklist

<!--
A complete, valid `checklist.md`, as `fsgg-sdd checklist` leaves it. Validated on every
build by the live checklist parser (ExampleArtifactsContractTests). Read it to see what
the stage produces — most of it is tool-owned and re-derived on every run, so it is not
a file to copy-adapt. `status: checklistReady` is written automatically by a clean
`fsgg-sdd checklist` review — there is no manual transition to author. See
[[fs-gg-sdd-checklist]] and the coverage-line grammar in
docs/reference/authoring-contracts.md.
-->

Prose status: checklistReady

## Source Specification
- work/001-example/spec.md

## Source Clarifications
- work/001-example/clarifications.md

## Source Snapshot
- spec: work/001-example/spec.md sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa schemaVersion:1
- clarifications: work/001-example/clarifications.md sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb schemaVersion:1

## Checklist Items
- **CHK-001** [FR-001] [AC-001] blocking: Every FR carries acceptance coverage on
  its own coverage line.
- **CHK-002** [FR-002] [AC-002] blocking: The serve owner is unambiguous.

## Review Results
- **CR-001** [CHK:CHK-001] [FR-001] [AC-001] pass: FR-001 is testable and has
  acceptance coverage.
- **CR-002** [CHK:CHK-002] [FR-002] [AC-002] pass: The serve owner is decided by
  DEC-001.

## Accepted Deferrals
- **CR-003** [CHK:CHK-002] [DEC-002] acceptedDeferral: The match-end condition is an
  explicit, still-visible deferral.

## Blocking Findings
- None.

## Advisory Notes
- Coverage is complete; both requirements pass.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd plan --work 001-example`.
