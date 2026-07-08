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

| Version | Note | Breaking changes |
|---|---|---|
| `0.9.0` | [`0.9.0.md`](0.9.0.md) | **(1)** Removed `specification.unresolvedAmbiguityCount` from the `--json` command-report contract — it gated nothing; the gate is `clarification.blockingAmbiguityCount`, on a **different** block. **(2)** `tasks` can now exit `1` (`missingDisposition`). **(3)** `plan` can now exit `1` (`stalePlanSnapshot`; use `--accept-upstream`). |

The current `0.9.0` release is **breaking** and therefore carries the note above.
The `migrations[]` array in [`release-readiness.json`](../release-readiness.json)
lists it, and a test asserts the referenced file exists — the obligation is a
file, not a claim. Under the `0.x` carve-out the changes ride a **minor** bump;
the note is still mandatory.

A migration note must enumerate **every** breaking change in its release. Two of
the three above are exit-code contract changes, not field removals — the policy
table classes both as Breaking, and a note that lists only the field removal is
the exact failure this obligation exists to prevent.

Releases `0.2.0` through `0.8.0` were additive-only and intentionally carry no
note. The paragraphs below record each of those additive changes; they are the
per-change classification record, not migration notes.

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

The `056-orchestrator-skill-fanout` change is additive. `fsgg-sdd` becomes the sole
mirror authority for agent-skill roots: `init` now seeds the 15 `fs-gg-sdd-*` process
skills into a **third** root `.agents/skills/` (in addition to `.claude/skills/` and
`.codex/skills/`), and after a successful provider invocation `scaffold` fans the
byte-identical **union** (seeded ∪ the provider's `.agents/skills/*` skills) into all
three roots (`claude ≡ codex ≡ agents`). The intrusion guard stays strict — `.claude/skills/`
and `.codex/skills/` remain whole-root reserved — and gains one clause: the `fs-gg-sdd-*`
namespace under `.agents/skills/` is reserved too. `.fsgg/scaffold-provenance.json` gains
one **additive** optional array `mirroredPaths` (each entry owner `"mirrored"`, the
`.claude`/`.codex` fan-out copies), sorted after `producedPaths`; absent/null parses to
`[]` and the schema **stays v1**. `ScaffoldProvenanceRecord` gains a `MirroredPaths` field,
`ScaffoldSummary` a `MirroredPaths` list (projected `mirroredPaths` in json/text/rich), and
`ArtifactOwner` a `Mirrored` case (`"mirrored"`). `refresh` re-mirrors the union to
currency; `doctor`/`upgrade` detect and reconcile a product whose three roots have drifted
(e.g. scaffolded by a two-root CLI); an incomplete fan-out is never reported complete
(a mirror I/O fault fails at exit 2 with the additive `scaffold.mirrorFailed` diagnostic —
no new outcome or exit code). `init`'s seeded set growing by the third root is a declared,
**version-gated** skeleton change (ADR-0008), not a schema migration — it advances the SDD
version-of-truth to **`0.4.0`** (the coherent-set minimum a provider requiring the fan-out
declares; publish `0.4.0` before Templates#47 flips to require it, FR-011). Per the
additive-change policy this carries **no `<version>.md` migration note**
(`release-readiness.json` `migrations[]` stays empty); this paragraph records the change instead.

The `057-skill-manifest-contract` change is additive: it adds the machine-readable contract
*shapes* the consolidated skill-mirror consumes (ADR-0014 P0.D0.2, FS-GG/FS.GG.SDD#60) — the
`SkillManifest`/`SkillManifestEntry`/`SkillScope` types, the `agentSkillRoots` constant
(`.claude`/`.codex`/`.agents`), and an additive optional per-path `Sha256` on
`.fsgg/scaffold-provenance.json` (schema **stays v1**). It is **types only** — it implements no
`mirror`/`verify`, routes no command through a new path, and computes or populates no digest
during scaffold (that is P1, feature 058). The provenance field is backward and forward
compatible: the runtime emitter **omits** `sha256` when absent so current output stays
**byte-identical**, and provenance written before this change still parses. The `FS.GG.Contracts`
package contract takes an additive **minor** bump (`1.2.0` → `1.3.0`) for the new public surface
and the `scaffold-provenance` contract a **minor** bump (`1.0.0` → `1.1.0`); the public-surface
golden baseline is updated additive-only. It breaks no existing public contract: every other
report field, key order, stream, and exit code is unchanged. Per this policy an additive change
carries **no `<version>.md` migration note** (`release-readiness.json` `migrations[]` stays
empty); this paragraph records the change instead.

The `058-materialize-verify-library` change is additive: it collapses the four hand-maintained
"materialize union → 3 roots" implementations into **one** content-addressed algorithm (ADR-0014
P1, FS-GG/FS.GG.SDD#61) — a pure `SkillMirror` `mirror`/`verify` library in `FS.GG.Contracts`,
computed over the single `agentSkillRoots` constant, through which every SDD skill-writing lane
(seeded fan-out, scaffold provider mirror, refresh re-mirror) now routes, plus content-aware
`doctor`/`upgrade` drift that asserts every union skill — process (SDD-seeded) **and** product
(provider) — is present in each root, byte-identical across roots, and matches its canonical
`sha256` (the check the audit's F2 found missing). The scaffold/refresh skill-file output and
`mirroredPaths` are **byte-identical to today** (SC-003), `doctor` still makes **zero** writes,
and no persisted schema changes (`.fsgg/scaffold-provenance.json` stays v1). The `FS.GG.Contracts`
package contract takes an additive **minor** bump (`1.3.0` → `1.4.0`) for the new public
`SkillMirror` surface (public-surface golden baseline updated additive-only), and the coherent
CLI/package version-of-truth advances **`0.4.0` → `0.5.0`**. That version-gated coherent-set bump
lands in-repo here; cutting and pushing the `0.5.0` release to the org feed and flipping the
registry orchestrator-axis minimum is the separate release dance (FS-GG/FS.GG.SDD#57), not this
feature. Per this policy an additive change carries **no `<version>.md` migration note**
(`release-readiness.json` `migrations[]` stays empty); this paragraph records the change instead.

When a release introduces a breaking change, add its note here as
`<version>.md` and list it in this index.
