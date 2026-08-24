---
schemaVersion: 1
workId: 912-typed-kernel-preview2
title: Correct typed-kernel preview dependency identity
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Correct typed-kernel preview dependency identity Specification

Prose status: specified

## User Value
Published FS.GG.SDD.Artifacts 1.3.0-preview.2 restores warning-free downstream consumption.

## Scope
- SB-001: Correct release pack property semantics, package metadata regression coverage, coherent preview version, release documentation, and delivery evidence without changing typed protocol APIs.

## Non-Goals
- SB-002: Do not implement later lifecycle commands or Governance enforcement in this specification.

## User Stories
- US-001 (P1): As a user, I can published FS.GG.SDD.Artifacts 1.3.0-preview.2 restores warning-free downstream consumption.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given Correct typed-kernel preview dependency identity is available, when the user exercises it, then they can published FS.GG.SDD.Artifacts 1.3.0-preview.2 restores warning-free downstream consumption.

## Functional Requirements
- FR-001: Packing FS.GG.SDD.Artifacts from its source-resolved 1.3.0-preview.2 identity MUST retain an exact FS.GG.Contracts dependency of 7.5.2, and adding either a Version or PackageVersion override MUST fail the release contract tests. (Stories: US-001; Acceptance: AC-001)
- FR-002: Merged main MUST publish preview.2 to GitHub Packages and nuget.org with byte-identical unsigned payload entries, and a clean public consumer MUST restore and run with zero NU1603 warnings. (Stories: US-001; Acceptance: AC-001)
- FR-003: Preview.1 MUST remain immutable and MUST NOT be eligible for P3 S.I.R. adoption. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work 912-typed-kernel-preview2`.
