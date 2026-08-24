---
schemaVersion: 1
workId: typed-specification-kernel-p2
title: Typed specification kernel producer preview
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

# Typed specification kernel producer preview Charter

## Identity
Extract the generic specification substrate proven by S.I.R's P1 pilot into the
SDD-owned `FS.GG.SDD.Artifacts` package and publish it as a preview contract.
This is roadmap milestone P2 and the producer half of the publish-before-flip
sequence tracked by FS.GG.SDD#910.

## Principles
- Preserve one semantic authority: normalized structured models are canonical;
  Markdown and JSON human views are derived and freshness-bound.
- Extract only vocabulary demonstrated by the S.I.R pilot and current SDD
  requirements artifacts; do not generalize from hypothetical consumers.
- Keep extension registration statically typed, explicit, and independent of
  `obj`, reflection discovery, or a platform-wide closed union.
- Preserve legacy Markdown as authored evidence: migration may report ambiguity
  or unsupported content, but must never guess silently.
- Publish the producer artifact before registry or S.I.R consumer adoption.

## Scope Boundaries
In scope: the generic specification envelope, stable identity, schema/provenance
and evidence contracts, deterministic normal form and fingerprint, versioned
codec, semantic diff, typed extension registration/compiler boundary, generated
projection/freshness validation, a requirements extension, explicit Markdown
migration outcomes, public contract tests, authoring guidance, and preview
package publication.

Out of scope: S.I.R gameplay types, `RuleDefinition`, `RuleSpecification`, rule
execution, coordination mutation/process extensions (M-series), `typed-sdd`
lifecycle selection (P4), S.I.R re-adoption (P3), and any consumer flip before
the package is available from its feeds.

## Policy Pointers
- Constitution principles I-III require spec → `.fsi` → semantic tests → `.fs`,
  structured machine authority, and explicit public signatures.
- Principles VI-VIII require mutation-capable regression evidence, one shared
  agent/human contract, and distinct actionable failures.
- The typed-protocol-kernel roadmap governs P2 acceptance and deletion/cutover
  sequencing; S.I.R PR #348 is the evidence-bearing pilot source.
- Cross-repo coordination requires publish-before-flip and a registry change
  after the producer release is live.

## Lifecycle Notes
Tier 1 public package and schema contract. The implementation uses the existing
independently consumable `FS.GG.SDD.Artifacts` boundary, sketches `.fsi` first,
and records public package consumption plus wrong-path controls before shipping.
Next: `fsgg-sdd specify --work typed-specification-kernel-p2`.
