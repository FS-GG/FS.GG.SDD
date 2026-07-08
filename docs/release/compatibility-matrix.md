---
title: Compatibility Matrix
category: SDD
categoryindex: 6
index: 17
description: For each FS.GG.SDD release line, the supported Spec Kit range and the optional FS.GG.Governance handoff contract-version range.
---

# Compatibility Matrix

This document is a human **projection** of the `compatibility[]` array in
[`release-readiness.json`](release-readiness.json). On any disagreement, the
machine contract is authoritative. (FR-002)

## Current matrix

| SDD version line | Spec Kit range | Governance handoff `contractVersion` range (optional) |
|---|---|---|
| `0.9.x` | `>=0.8.5` | `1.x` |

## How to read this

- **SDD version line** — the `0.9.x` release line covered by this record. The
  declared `identity.version` is `0.9.0` on the `preRelease` channel.
- **Spec Kit range** — the supported Spec Kit version range for this line:
  `>=0.8.5`.
- **Governance handoff `contractVersion` range** — the supported handoff
  `contractVersion` range, `1.x`, matching the `governance-handoff.json`
  contract at `contractVersion` `1.0.0`.

## Governance compatibility is optional

The Governance handoff `contractVersion` range is an **optional integration
fact**. It does **not** block SDD release readiness. FS.GG.SDD builds, installs,
and runs the full lifecycle through `fsgg-sdd ship` with **no Governance present**
— the range simply states which handoff contract version SDD interoperates with
when a Governance integration is adopted.

When SDD is not integrated with Governance, the
`governanceContractVersionRange` may be recorded as `null`; either way, readiness
is unaffected.

## Scaffold-produced files are out of Governance freshness scope

`fsgg-sdd scaffold` records provider-produced runtime files in
`.fsgg/scaffold-provenance.json` marked `owner: generatedProduct` (externally
owned). These paths are **out of scope** for SDD generated-view currency (refresh
excludes them, FR-007) and for Governance-owned effective evidence freshness and
gate enforcement: they are runtime product code owned outside SDD, not SDD lifecycle
artifacts. The scaffold capability adds **no** new Governance obligation and no new
handoff `contractVersion` — the Governance handoff range above is unchanged.
