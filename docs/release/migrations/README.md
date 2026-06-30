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

The `030-scaffold-template-provider` change is additive: it adds the cross-cutting
`fsgg-sdd scaffold` command, the new `command-report` `scaffold` field, and the new
project-level `.fsgg/scaffold-provenance.json` and `.fsgg/providers.yml` artifacts.
It breaks no existing public contract — `fsgg-sdd init` stays byte-identical (SC-003)
and every existing report field is unchanged — so no migration note is required.

The `050-scaffold-default-starter` change is additive: it adds one new optional field,
`effectiveParameters`, to `.fsgg/scaffold-provenance.json` (schema **stays v1**) and the
corresponding `effectiveParameters` field/line-group to the scaffold report's json/text
projections. The field is **backward and forward compatible** — `tryParse` defaults it to
`[]` for provenance written before it, and readers that ignore unknown keys are unaffected
(D3). It breaks no existing public contract: every other scaffold field, key order, stream,
and exit code is unchanged, and no non-`scaffold` command output changes (FR-008). Per this
policy an additive change carries **no `<version>.md` migration note** (the
`release-readiness.json` `migrations[]` array stays empty); this paragraph records the
change instead.

When a release introduces a breaking change, add its note here as
`<version>.md` and list it in this index.
