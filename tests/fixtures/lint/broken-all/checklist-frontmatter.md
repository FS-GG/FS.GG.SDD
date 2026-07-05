---
schemaVersion: 1
workId: 001-example
title: Example Work Item
stage: checklist
changeTier: tier1
status: checklistReady
sourceSpec: work/001-example/spec.md
publicOrToolFacingImpact: true
---

# Broken Example Checklist (incomplete front matter)

<!--
Deliberately broken fixture for feature 076 lint (SC-001, FrontMatter class): the
gating `sourceClarifications` field is MISSING from the front matter. Do NOT copy as a template.
-->

Prose status: checklistReady

## Source Specification
- work/001-example/spec.md

## Source Clarifications
- work/001-example/clarifications.md

## Checklist Items
- **CHK-001** [FR-001] [AC-001] blocking: Every FR carries acceptance coverage.

## Review Results
- **CR-001** [CHK:CHK-001] [FR-001] [AC-001] pass: FR-001 is testable.

## Blocking Findings
- None.

## Advisory Notes
- None.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd plan --work 001-example`.
