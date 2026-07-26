---
title: FS.GG.Contracts 6.0.0 — verifiable performance evidence
category: SDD
categoryindex: 6
index: 26
description: Declares the GovernanceHandoffSchema constructor break introduced by typed, independently verifiable performance evidence.
---

# FS.GG.Contracts 6.0.0

`FS.GG.Contracts` moves `5.0.1` → **`6.0.0`**. This is a declared binary break,
not a suppressed compatibility finding.

## Breaking change

`Fsgg.Schemas.GovernanceHandoffSchema` gains the typed
`PerformanceEvidence: GovernanceHandoffPerformanceEvidence list` field. Because this
public F# record has a positional constructor, the new field changes that
constructor's arity. Consumers that construct the record must provide the new field;
consumers that only deserialize the governance-handoff JSON can adopt the new
`performanceEvidence[]` projection when ready.

The new DTO graph preserves each `performance-evidence-v1` artifact's raw duration and
catch-up samples, workload and environment bindings, and SDD-recomputed measurements.
It prevents Governance consumers from having to trust a producer-authored pass/fail
summary.

## Coordinated release

The [contracts version-bump checklist](contracts-version-bump-checklist.md) applies:

1. merge the source and `Fsgg.ContractVersion.value` bump to `6.0.0`;
2. publish the byte-identical `6.0.0` package through `release.yml`;
3. after the feed serves it, advance both `fsgg-contracts` version fields in the
   `.github` dependency registry and update consumers that pin the prior major.
