---
schemaVersion: 1
workId: typed-specification-kernel-p2
title: Typed Specification Kernel P2
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/typed-specification-kernel-p2/spec.md
publicOrToolFacingImpact: true
---

# Typed Specification Kernel P2 Clarifications

## Source Specification
- work/typed-specification-kernel-p2/spec.md

## Clarification Questions
- **CQ-001** [AMB:AMB-001]: Does the kernel require a new NuGet package,
  or should it extend the existing independently consumable Artifacts package?
- **CQ-002** [AMB:AMB-002]: Which envelope fields participate in semantic
  normalization, fingerprints, and diff?
- **CQ-003** [AMB:AMB-003]: Which Standard SDD Markdown constructs migrate
  losslessly, and how do unfamiliar or unresolved constructs fail?
- **CQ-004** [AMB:AMB-004]: Which release identity truthfully advertises a
  preview kernel without claiming that the later `typed-sdd` lifecycle exists?

## Answers
- CQ-001 → extend `FS.GG.SDD.Artifacts`. It already owns lifecycle artifacts,
  has an independent package boundary, and avoids an unproved second package.
- CQ-002 → identity, schema version, authoritative source path/revision, typed
  extension semantics, and evidence obligations are semantic. Agent/session,
  authored timestamp, and explanatory intent are provenance/authoring metadata
  and do not change normalized semantic bytes.
- CQ-003 → migrate the current Standard SDD stable-heading and stable-id list
  grammar when every supported fact and reference is explicit. Unresolved
  meaning or references return `Ambiguous`; an unknown heading containing
  semantic content or an unsupported version returns `Unsupported`. Both carry
  typed reason plus line/column and never write canonical source.
- CQ-004 → use coherent-set version `1.3.0-preview.1`, publishing the exact
  merged artifacts from the existing release workflow. Documentation calls the
  kernel preview-only and explicitly says no `typed-sdd` selector exists yet.

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-012] [AC-008]: The typed kernel is an
  additive public namespace in `FS.GG.SDD.Artifacts`; no new package is created.
- **DEC-002** [CQ-002] [AMB:AMB-002] [FR-003] [FR-005] [AC-002] [AC-003]: Normalization
  includes semantic identity/schema/source/extension/evidence and
  excludes agent, session, authored time, and explanatory intent.
- **DEC-003** [CQ-003] [AMB:AMB-003] [FR-008] [AC-005]: Migration supports the
  current explicit Standard SDD grammar only; unresolved content is
  `Ambiguous`, unfamiliar semantic content is `Unsupported`, locations are
  retained, and analysis is read-only.
- **DEC-004** [CQ-004] [AMB:AMB-004] [FR-015] [AC-010]: Publish the coherent
  FS.GG.SDD set as `1.3.0-preview.1` and describe the kernel as preview-only;
  lifecycle selection remains unchanged.

## Accepted Deferrals
- None. Later P3/P4 consumer adoption is outside scope, not an unresolved P2
  design choice.

## Remaining Ambiguity
- None. AMB-001 through AMB-004 are resolved by DEC-001 through DEC-004.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd checklist --work typed-specification-kernel-p2`.
