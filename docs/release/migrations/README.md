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

The `052-cli-version-coherence` change is additive: it adds one new optional field,
`requiredMinimumCliVersion`, to `.fsgg/scaffold-provenance.json` (schema **stays v1**),
the matching `requiredMinimumCliVersion` field/line to the scaffold report's json/text
projections, and two new non-blocking `scaffold.*` advisories
(`scaffold.cliBehindMinimum` info, `scaffold.providerMinimumMalformed` warning). The
field is **backward and forward compatible** — `tryParse` defaults absent/null to `None`
for provenance written before it, and readers that ignore unknown keys are unaffected. It
breaks no existing public contract: every other scaffold field, key order, stream, and
exit code is unchanged (the new advisories never set `hasBlocking`, so a behind-minimum
scaffold's outcome and exit code are identical to an up-to-date run, SC-004), and no
non-`scaffold` command output changes. The provider-registry read gains one optional
`minimumCliVersion` scalar (value-agnostic; no concrete value in generic SDD). Per this
policy an additive change carries **no `<version>.md` migration note** (the
`release-readiness.json` `migrations[]` array stays empty); this paragraph records the
change instead. The contract change is a **minor** package bump coordinated with the
registry per epic FS-GG/.github#85 (no handoff `contractVersion` is involved).

The `053-upgrade-doctor-remediation` change is additive: it adds two cross-cutting
commands (`fsgg-sdd doctor`, `fsgg-sdd upgrade`), two new optional `command-report`
(`--json`) blocks (`doctor`, `upgrade`, both `null` on every other command), one additive
optional field (`Confirmed: bool option`, default `null`) on each `CommandEffectResult`, and
a new `Confirm` effect case. It reads the feature-052 declarative minimum and the seeded
skeleton set; it introduces **no** new persisted schema (`scaffold-provenance.json` stays v1,
read-only here) and **no** new versioned cross-repo contract. It breaks no existing public
contract: every existing report block emits an unchanged shape (the two new blocks are
additive and default `null`), `fsgg-sdd doctor` is strictly read-only (exit 0 whenever it
reports), and only `fsgg-sdd upgrade` mutates for remediation — no other command's output,
stream, or exit code changes (FR-008). Per this policy an additive change carries **no
`<version>.md` migration note** (the `release-readiness.json` `migrations[]` array stays
empty); this paragraph records the change instead.

The `054-surface-provider-output` change is additive: on a provider-defect scaffold
failure it adds one new optional block, `providerInvocation`, to the scaffold report's
json/text/rich projections (the provider's invoked command line, captured stdout/stderr,
and exit code), plus one additive optional field `ProviderInvocation:
ProviderInvocationResult option` on `ScaffoldSummary`. The block is `null` on success,
dry-run, and every pre-invocation user-input block, and present only on the three
provider-defect outcomes (FR-006); `exitCode` is int-or-null so a never-launched provider
is never confused with a real `0` (FR-003). Each captured stream is bounded to
`providerOutputCapChars` (65 536) with a truncation flag (FR-005). It introduces **no** new
persisted schema (`.fsgg/scaffold-provenance.json` stays v1 and gains no captured-output key,
FR-010) and **no** new versioned cross-repo contract. It breaks no existing public contract:
every other scaffold field, key order, stream, and outcome string is unchanged, and the
exit-code taxonomy (defect ⇒ 2, user-input ⇒ 1, success ⇒ 0) is identical to today (FR-007) —
the block adds diagnostic visibility only. Per this policy an additive change carries **no
`<version>.md` migration note** (the `release-readiness.json` `migrations[]` array stays
empty); this paragraph records the change instead.

When a release introduces a breaking change, add its note here as
`<version>.md` and list it in this index.
