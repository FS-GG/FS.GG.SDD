---
title: Migration Notes
category: SDD
categoryindex: 6
index: 20
description: The migration-note obligation for breaking FS.GG.SDD releases, and the index of published notes.
---

# Migration Notes

A migration note records, for one release, every backward-incompatible change to
a public contract and the consumer adaptation step it requires. This obligation
is part of the [versioning policy](../versioning-policy.md). (FR-009 / FR-010 /
SC-006)

## The obligation

- A release that makes **any** Breaking change to a public schema, generated-view
  shape, command-output (`--json`) contract, or CLI surface **MUST** ship a
  migration note at `docs/release/migrations/<version>.md`. The note enumerates
  each breaking public-contract change and the corresponding consumer adaptation
  step.
- An **additive-only** release **MUST NOT** carry a migration note. Its absence
  is consistent with the policy, not an omission.

This holds pre-1.0 as well: under the `0.x` line a breaking change MAY land on a
minor bump, but it still requires a migration note. See
[Pre-1.0 semantics](../versioning-policy.md#pre-10-0x-semantics).

Use [TEMPLATE.md](TEMPLATE.md) as the starting point for a new note.

## Index of published notes

**None.**

The current `0.2.0` release is **additive-only** and therefore intentionally
carries no migration note. This is consistent with the policy: the `migrations[]`
array in [`release-readiness.json`](../release-readiness.json) is empty.

When a release introduces a breaking change, add its note here as
`<version>.md` and list it in this index.
