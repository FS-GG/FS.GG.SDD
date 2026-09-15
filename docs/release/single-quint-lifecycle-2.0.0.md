---
title: Single Quint-backed lifecycle migration for 2.0.0
category: Release
---

# Single Quint-backed lifecycle migration for 2.0.0

FS.GG.SDD 2.0.0 makes `quint-specification-v1` the omitted Typed SDD backend and publishes the
revisioned `WorkspaceModel`, issue-bound `ChangeProposal`, deterministic reconciliation, explicit
human reduction, and dry-run legacy migration contracts.

Existing F# manifest-v1 authorities remain inspectable and migratable throughout the 2.x
compatibility window. To author or accept a legacy F# migration deliberately, pass:

```console
fsgg-sdd typed-sdd author ... --backend fsharp-specification-v1
fsgg-sdd typed-sdd migrate ... --backend fsharp-specification-v1
```

For the default Quint backend, pre-provision the qualified content-addressed cache and omit
`--backend`, or spell `--backend quint-specification-v1` explicitly. First run migration without
`--accept`; retain its original-byte digests and rollback inventory before applying it.

The provider lifecycle tokens `none`, `sdd`, `typed-sdd`, and `spec-kit` remain distinct. Templates
and wizard defaults do not change in this producer release; that rollout remains gated on actual
`OperatingV2` evidence and public package qualification.
