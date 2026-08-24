---
schemaVersion: 1
workId: typed-kernel-p2-fable
title: Fable-Consumable Typed Kernel Package
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Fable-Consumable Typed Kernel Package Specification

Prose status: specified

## User Value
Published typed specification models compile and execute identically in real .NET and Fable consumers.

## Scope
- SB-001: Add package-owned Fable metadata and portable kernel source to FS.GG.SDD.Artifacts.
- SB-002: Publish coherent FS.GG.SDD.Artifacts and CLI 1.3.0-preview.3 packages.

## Non-Goals
- SB-003: Port JSON codec or requirements Markdown migration execution to Fable in this fix-forward.
- SB-004: Add a consumer runtime or gameplay dependency to FS.GG.SDD.Artifacts.

## User Stories
- US-001 (P1): As a user, I can published typed specification models compile and execute identically in real .NET and Fable consumers.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given Fable-Consumable Typed Kernel Package is available, when the user exercises it, then they can published typed specification models compile and execute identically in real .NET and Fable consumers.

## Functional Requirements
- FR-001: A clean Fable consumer constructs SpecificationModel records and runs validation, normalization, fingerprinting, compilation, and semantic diff without System.Text.Json references. (Stories: US-001; Acceptance: AC-001)
- FR-002: The same consumer model produces byte-identical normalized bytes and fingerprint in .NET and Fable. (Stories: US-001; Acceptance: AC-001)
- FR-003: The normal net10.0 package retains its existing codec, projection, evidence, requirements migration, and public API behavior. (Stories: US-001; Acceptance: AC-001)
- FR-004: The packed fable tree contains only producer-owned portable typed-kernel sources and no S.I.R. or gameplay semantics. (Stories: US-001; Acceptance: AC-001)
- FR-005: Fresh-cache locked restore from nuget.org resolves one deterministic preview.3 content hash. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- This specification is an SDD lifecycle artifact and command-report contract input.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work typed-kernel-p2-fable`.
