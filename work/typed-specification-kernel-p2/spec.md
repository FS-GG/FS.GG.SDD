---
schemaVersion: 1
workId: typed-specification-kernel-p2
title: Typed Specification Kernel P2
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Typed Specification Kernel P2 Specification

Prose status: specified

## User Value
FS-GG consumers can author, validate, diff, migrate, project, and consume one published typed specification model without depending on S.I.R.

## Scope
- SB-001: Extract the P1-proven generic specification envelope, compiler
  contracts, normal form, codecs, semantic diff, provenance/evidence contracts,
  projections, and authoring contract into `FS.GG.SDD.Artifacts`.
- SB-002: Add one SDD-owned requirements extension covering scope, user value,
  requirements, acceptance, ambiguities/decisions, and evidence obligations.
- SB-003: Publish and independently consume a preview package before any
  registry or S.I.R dependency flip.

## Non-Goals
- SB-004: Do not move S.I.R gameplay types, rule semantics, interpreters,
  `RuleDefinition`, or `RuleSpecification` into FS.GG.SDD.
- SB-005: Do not implement coordination mutation/process extensions, the
  `typed-sdd` lifecycle option, or S.I.R package re-adoption in this item.
- SB-006: Do not silently reinterpret legacy Markdown or make generated
  projections a second semantic authority.

## User Stories
- US-001 (P1): As an FS-GG author, I can construct an inspectable typed
  specification with a statically registered domain extension and receive all
  validation findings in stable machine-readable form.
- US-002 (P1): As a reviewer, I can compare normalized specifications and read a
  deterministic semantic diff and fingerprinted human projection without
  understanding builder mechanics.
- US-003 (P1): As an existing Standard SDD user, I can analyze legacy Markdown
  for migration and receive an explicit migrated, ambiguous, or unsupported
  result before any canonical source is written.
- US-004 (P1): As a package consumer, I can restore and use the preview contract
  without S.I.R or coordination dependencies.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001] [FR-002]: Given a valid requirements extension is
  registered, when a model is compiled, then the compiler returns the validated
  extension through its concrete type without `obj`, reflection discovery, or a
  platform-wide closed union.
- AC-002 [US-001] [FR-003] [FR-004]: Given direct-record and builder authoring
  forms with equal meaning, when normalized and encoded, then both produce
  byte-identical versioned payloads and fingerprints.
- AC-003 [US-002] [FR-005]: Given two models, when their authoring-only metadata
  differs, then semantic diff reports equivalent; when semantics differ, it
  reports stable paths and before/after fingerprints.
- AC-004 [US-001] [FR-006] [FR-007]: Given a requirements specification with
  scope, value, requirements, acceptance, decisions, and evidence obligations,
  when compiled, then every reference is validated and every omission,
  duplicate, and unresolved reference has a stable diagnostic.
- AC-005 [US-003] [FR-008]: Given losslessly representable Standard SDD
  Markdown, when migration is analyzed, then it returns `Migrated`; given an
  unresolved or unsupported construct, it returns `Ambiguous` or `Unsupported`
  with a typed reason and source location and performs no write.
- AC-006 [US-002] [FR-009] [FR-010]: Given a valid model, when Markdown and JSON
  views are generated and checked, then output is deterministic and stamped with
  model/source/generated fingerprints; missing, malformed, unreadable, stale,
  direct-edit, and unsupported-version controls fail distinctly.
- AC-007 [US-001] [FR-011]: Given declared evidence obligations, when evidence is
  validated, then satisfied, missing, duplicated, and wrongly bound evidence are
  distinguishable without Governance being installed.
- AC-008 [US-004] [FR-012] [FR-013]: Given a clean consumer fixture referencing
  only the packed preview, when restored and executed, then all public compiler,
  codec, diff, projection, migration, and evidence fixtures pass with no S.I.R or
  coordination assembly in the dependency closure.
- AC-009 [US-001] [FR-014]: Given the authoring skill contract, when an agent
  follows inspect → intent → question → typed proposal → validate → semantic
  diff → evidence → revise, then the normalized model remains canonical and the
  human view remains derived.
- AC-010 [US-004] [FR-015] [FR-016] [FR-017]: Given the merged producer commit,
  when the preview is packed, published, downloaded, and inspected, then the
  exact artifact is independently consumable, carries compatibility guidance,
  contains no S.I.R rule/game surface, and leaves existing Standard SDD behavior
  unchanged.

## Functional Requirements
- FR-001: The package MUST expose a generic `SpecificationModel` envelope with stable identity, schema version, provenance, intent, evidence obligations, and a typed extension value. (Stories: US-001; Acceptance: AC-001)
- FR-002: Extension registration and compilation MUST be statically typed and explicit, and MUST NOT use `obj`, reflection discovery, or one platform-wide closed union. (Stories: US-001; Acceptance: AC-001)
- FR-003: Normalization MUST be deterministic, MUST canonicalize semantically unordered collections, and MUST produce a stable SHA-256 model fingerprint. (Stories: US-001, US-002; Acceptance: AC-002)
- FR-004: The model codec MUST be explicitly versioned, deterministic, and round-trip losslessly for supported versions while refusing unsupported versions and following a documented unknown-field policy. (Stories: US-001, US-004; Acceptance: AC-002)
- FR-005: Semantic diff MUST distinguish equivalent and changed models, ignore authoring-only noise, and report stable semantic paths plus before/after fingerprints. (Stories: US-002; Acceptance: AC-003)
- FR-006: The requirements extension MUST represent user value, scope, requirements, acceptance criteria, ambiguity/decision state, and evidence obligations with stable typed identifiers and references. (Stories: US-001; Acceptance: AC-004)
- FR-007: Compiler validation MUST report all detectable duplicate, missing, malformed, unresolved-reference, and extension errors in deterministic order with stable codes and paths. (Stories: US-001; Acceptance: AC-004)
- FR-008: Legacy Markdown migration MUST return only `Migrated`, `Ambiguous`, or `Unsupported`; every non-migrated result MUST carry a typed reason and source location, and analysis MUST perform no canonical-source write. (Stories: US-003; Acceptance: AC-005)
- FR-009: Generated Markdown and JSON projections MUST derive solely from the normalized model and carry model identity plus source and generated fingerprints. (Stories: US-002; Acceptance: AC-006)
- FR-010: Projection validation MUST distinguish missing, malformed, unreadable, stale-source, direct-edit/generated-fingerprint, and unsupported-version failures with actionable stable codes. (Stories: US-002; Acceptance: AC-006)
- FR-011: Evidence validation MUST bind evidence to declared obligation IDs and distinguish satisfied, missing, duplicate, and mismatched evidence without a Governance runtime dependency. (Stories: US-001; Acceptance: AC-007)
- FR-012: `FS.GG.SDD.Artifacts` MUST remain independently consumable and MUST NOT reference S.I.R, coordination, a source checkout, or a local-package shortcut. (Stories: US-004; Acceptance: AC-008)
- FR-013: Public contract tests MUST cover compiler, codec, normalization, semantic diff, projections, migration, evidence validation, extension isolation, deterministic ordering, and deliberate wrong-path mutations. (Stories: US-004; Acceptance: AC-008)
- FR-014: The SDD authoring skill contract MUST teach one inspectable typed loop shared by agents and humans and MUST identify the normalized model—not the builder or projection—as authority. (Stories: US-001; Acceptance: AC-009)
- FR-015: The merged producer commit MUST yield a preview package that is published before registry and consumer flips, downloaded from every required feed, byte-compared where the release policy requires, and executed by a clean consumption fixture. (Stories: US-004; Acceptance: AC-010)
- FR-016: The package MUST NOT expose S.I.R-owned gameplay, rule execution, or registered-algorithm semantics; those remain consumer extensions. (Stories: US-004; Acceptance: AC-010)
- FR-017: The change MUST be additive for existing Standard SDD artifacts, command output, exit codes, and package consumers until a later explicitly versioned adoption. (Stories: US-003, US-004; Acceptance: AC-010)

## Ambiguities
- AMB-001 open: Should the generic kernel be a new package or an additive public
  namespace inside the already independent `FS.GG.SDD.Artifacts` package?
- AMB-002 open: Which fields are semantic for model fingerprint/diff and which
  provenance/authoring fields are intentionally excluded?
- AMB-003 open: What exact Markdown subset can migrate losslessly in P2, and how
  are unknown headings and unresolved references classified?
- AMB-004 open: What preview version and publication identity carries the new
  contract without implying the later `typed-sdd` lifecycle option exists?

## Public Or Tool-Facing Impact
- Tier 1 additive package API and codec/projection/migration contracts.
- New public `.fsi` surface and committed API baseline in
  `FS.GG.SDD.Artifacts`.
- Updated authoring skill contract and release compatibility documentation.
- No existing CLI command, persisted SDD schema, or default lifecycle selection
  changes in P2.

## Lifecycle Notes
- Next lifecycle action: `fsgg-sdd clarify --work typed-specification-kernel-p2`.
