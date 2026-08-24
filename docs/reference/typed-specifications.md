---
title: Typed Specifications Preview
category: SDD
categoryindex: 6
index: 21
description: The preview typed specification kernel, requirements extension, canonical model rules, projections, evidence binding, and migration boundary.
---

# Typed Specifications Preview

`FS.GG.SDD.Artifacts` 1.4.0-preview.1 provides the namespace
`FS.GG.SDD.Artifacts.TypedSpecifications`. It is an additive package API; existing
Standard SDD Markdown, commands, schemas, defaults, and exit codes are unchanged.

The authority is `SpecificationModel<'extension>`. Builders are authoring
conveniences and Markdown/JSON projections are generated views. Neither is a
second semantic source.

## Extension boundary

A producer defines a concrete extension type and an explicit
`ExtensionContract<'extension>`. The contract validates, canonically encodes,
reads/writes versioned JSON, and projects Markdown. Registration is an ordinary
typed value passed to compiler functions: there is no `obj`, reflection discovery,
or platform-wide closed union.

P2 includes `RequirementsExtension.contract`, whose model covers user value,
scope and non-goals, stories, requirements, acceptance criteria,
ambiguity/decision state, public impact, lifecycle notes, and evidence references.
Gameplay, rule execution, registered algorithms, and coordination process remain
consumer-owned extensions.

## Canonical semantics

The compiler validates the whole envelope and extension, returns all detectable
diagnostics in deterministic path/code/message order, and length-frames canonical
bytes before computing a lowercase SHA-256 fingerprint. Identity, schema version,
authoritative source path/revision, evidence obligations, extension kind/version,
and extension bytes are semantic. Agent, session, authored time, and authoring
intent are deliberately excluded from semantic equality.

`SpecificationCodec` emits schema `fsgg.typed-specification/v1`, refuses unknown
envelope fields and unsupported versions, and delegates only the extension JSON
element to the concrete contract.

## Inspectable authoring loop

1. Inspect the current normalized model and diagnostics.
2. State intent and ask any unresolved semantic question.
3. Build a concrete typed proposal directly or through `RequirementsDraft`.
4. Validate with `SpecificationCompiler.validate`.
5. Compare with `SpecificationCompiler.semanticDiff`.
6. Bind evidence receipts with `SpecificationEvidence.validate`.
7. Revise the model, then regenerate projections.

`SpecificationProjection.generate` produces deterministic Markdown and JSON with
source and generated fingerprints. Its validators distinguish missing,
unreadable, malformed, unsupported-version, stale-source, and direct-edit states.

## Legacy migration

`RequirementsMigration.analyzeMarkdown` is read-only. It returns only
`Migrated`, `Ambiguous`, or `Unsupported`; non-migrated findings carry a typed
reason and line/column. P2 accepts the current Standard SDD schema-v1 heading and
stable-ID list grammar. Unknown non-empty semantic headings and malformed or
unsupported schema constructs are unsupported; unresolved references are
ambiguous. The API exposes no migration write function.

## Compatibility and adoption

The preview must be published and independently consumed before any registry or
S.I.R dependency flips. Consumers can adopt the namespace without Governance,
S.I.R, coordination, a source checkout, or local project references. Later
roadmap milestones own producer re-adoption and the optional `typed-sdd` lifecycle;
this preview does not imply either is available.

Preview.1 is retained as immutable release history but is not eligible for
adoption: its package metadata points at a nonexistent Contracts preview and can
restore only by falling forward with `NU1603`. Preview.2 preserves the package's
independent `FS.GG.Contracts` 7.5.2 dependency and is the first downstream-eligible
typed-kernel preview.
