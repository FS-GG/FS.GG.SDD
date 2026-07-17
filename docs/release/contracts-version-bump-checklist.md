---
title: FS.GG.Contracts Version-Bump Checklist
category: SDD
categoryindex: 6
index: 20
description: The same-change checklist a FS.GG.Contracts source version bump must follow so source, feed, and the org registry stay coherent — bump source, publish to the feed, and advance the .github registry together.
---

# FS.GG.Contracts Version-Bump Checklist

This is the human-facing runbook for bumping the `FS.GG.Contracts` contract
version. It is a **projection** of the process contract at
`specs/043-publish-contracts-110/contracts/contracts-version-coherence.md`
("Durable bump protocol"); the registry and `.github`'s `source-coherence` gate
remain the authoritative sources of truth. Follow it on **every** `FS.GG.Contracts`
source bump so the failure mode of feature 042 — source bumped to `1.1.0` while
the feed and registry still served `1.0.1` — cannot recur unnoticed.

## Change class → bump (`FS.GG.Contracts` line)

**`FS.GG.Contracts` is not on the product version line.** The
[versioning policy](versioning-policy.md) governs the `FS.GG.SDD.*` packages and
the `fsgg-sdd` CLI — one shared `0.x` version from `Directory.Build.props`.
`FS.GG.Contracts` carries its **own** `<Version>` and its own SemVer, and its public
contract is a **.NET API surface**, not a `--json` shape or a CLI flag. This table is
that line's bump rule; the policy's table does not reach it.

| Change to the public API surface | Class | Bump | Break declaration |
|---|---|---|---|
| **Add a field to a public record** | **Breaking** | **major** | **required** |
| Remove/rename/retype a public member; change a signature | Breaking | major | required |
| **Add a case to a public discriminated union** | Additive (binary); **source-breaking** | minor | none — but say so in the release notes |
| Add a new module, type, or `val` | Additive | minor | none |
| Behaviour change with no surface change; docs; internals | Clarifying | patch | none |

> **The first row is the one that gets missed, and it is not intuitive.** An F# record
> generates a **positional primary constructor**, so *every* field — added at the end,
> in the middle, anywhere — changes its arity and **deletes the old constructor**.
> There is **no** additive way to add a field to a public F# record. It reads like an
> additive change, it is spelled like an additive change, and it is a binary break.
>
> This is not hypothetical: it is exactly how `IdentifierParameter` landed with no bump
> and then shipped as the `1.4.1` **patch**, forcing `2.0.0`. See the worked example,
> [contracts-2.0.0.md](contracts-2.0.0.md).

> **The DU row is the one ApiCompat cannot help you with — `surface --check` can.** Adding a case to a public DU is
> *binary*-compatible — every existing case constructor and tag survives — so it stays a
> **minor**. But it is **source**-breaking: a
> consumer whose `match` was exhaustive and carried no wildcard now gets `FS0025`
> (*incomplete pattern matches*). That is a warning by default, and this repo promotes it to
> an **error** (`Directory.Build.local.props` sets `TreatWarningsAsErrors` and names `FS0025`
> explicitly). A consumer that does the same gets a **build break from a minor bump**.
>
> **This is observable, not theoretical.** When feature 104 added `MalformedField` to
> `RegistryRule`, `src/FS.GG.SDD.Cli/RegistryValidate.fs` — which matches that DU exhaustively
> and carries no wildcard — had to gain a matching arm *in the same commit*, or the build would
> not have compiled. In-repo that is free: `ProjectReference`, so the break and its fix land
> together. **The package consumers get no such lockstep** (`consumers: [sdd, governance,
> templates]`): they meet the new case whenever they upgrade, and if they match exhaustively
> the upgrade breaks them.
>
> Minor is still the standing precedent, not an invention here: `RegistryRule` grew
> `DuplicateComponent` and `MalformedDocument` in feature 042 and shipped as the `1.1.0` minor.
> Keep it — but keep it *knowing* what it costs: prefer a wildcard arm in consumers, and **say
> so in the release notes**, since the bump number alone will not warn anyone.
>
> This row and the record row are **mirror images**, and neither is visible by reading the diff
> and thinking "I only added something".

## The two detectors, and the class each one is blind to

There are **two**, they fail in different directions, and neither one alone covers this table.

| | `scripts/apicompat-check.sh` | `fsgg-sdd surface --check` |
|---|---|---|
| Compares | the built assembly vs the **newest feed baseline** | the authored `.fsi` vs its **committed baseline** under `docs/api-surface/` |
| Sees a **break** (removed/retyped member) | **yes** — this is its job | yes (`breaking` → major) |
| Sees **additive growth** (new type/`val`) | no — additions are binary-compatible, so it passes *correctly* | **yes** (`additive` → minor) |
| Sees a **new DU case** | **no** — binary-compatible, invisible *even before publish* | **yes** — the case is a member token in the `.fsi` |
| Sees a **behaviour/constant change** with no surface change | no | no — use the table's last row |
| Runs | `api-compatibility-gate` job | `gate` job (both axes), and locally |

**ApiCompat is a *break* detector.** It can only catch a break **before** it is published, because
it baselines against whatever is newest on the feed — once a break ships, the gate is green against
it forever. And it is structurally blind to every *additive* row of this table, which it passes
**correctly**: additions really are binary-compatible.

That blindness is not theoretical, and it is the whole reason `docs/api-surface/` exists.
[#426](https://github.com/FS-GG/FS.GG.SDD/issues/426) grew this package's public surface —
`SkillRegistryEntry`, `SkillRegistryDocument`, `MirrorDeclaration`, `validateSkillRegistry`, **and
the `MalformedField` case on `RegistryRule`** — under an already-published `2.0.0`. ApiCompat passed
(correctly), the version number never moved, and the `.nupkg` at `2.0.0` and the source at `2.0.0`
became different artifacts. See [#432](https://github.com/FS-GG/FS.GG.SDD/issues/432).

**`fsgg-sdd surface --check` is the one that sees that class**, because a committed `.fsi` baseline
*is* the public surface, exactly and by construction — DU cases included. Replaying #426 against it
classifies the delta `additive`, reads the axis at `2.0.1`, and names the bump:

```text
surfaceClassificationVerdict: additive
surfaceVersionCurrent: 2.0.1
surfaceVersionSuggested: 2.1.0
```

**Know exactly what it enforces.** `--check` **blocks** (exit 1) on baseline **drift** — a `.fsi`
you changed without re-committing its baseline. The version half
(`surface.versionBumpRequired`) is a **warning that never changes the exit code**, deliberately:
per FS-GG/.github ADR-0025 §2 the classification is *"advisory-but-loud; the operator confirms"*,
and SDD cannot see the previously *published* version, so it states an implication rather than an
accusation — the bump may already be applied in the change under review. **So nothing will red your
PR purely for growing the surface without bumping.** What the gate guarantees is that the growth
cannot land **silently**: you must re-commit the baseline, and that commit prints the classification
and the suggested version. **Reading it is still your job — this table is what you use to decide.**

A surface with **no** committed baseline is a *new* surface, not a mutation, and is out of scope for
the event (ADR-0025 step 1). That is why the baselines are committed: without them every file
classifies as "new" forever and the event can never fire.

## The coherence invariant

`FS.GG.Contracts` is **coherent** only when all four values agree:

```text
source == feed(newest) == registry.version == registry.package-version
```

- **source** — `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` `<Version>`, kept in
  lockstep with `Fsgg.ContractVersion.value` (`ContractVersion.fs`).
- **feed(newest)** — the newest `FS.GG.Contracts` package on the org feed
  (`nuget.pkg.github.com/FS-GG`).
- **registry.version** / **registry.package-version** — the `fsgg-contracts`
  entry in `FS-GG/.github` `registry/dependencies.yml` (cross-repo owned).

A bump that advances only the source — without publishing to the feed and
advancing the registry in the same coordinated change — breaks this invariant.

## Same-change checklist

When you bump the `FS.GG.Contracts` contract version, do all three in the same
coordinated change set:

1. **Bump the source.** Advance the fsproj `<Version>` **and**
   `Fsgg.ContractVersion.value` together (they must match — enforced by features
   036/042). These are the version of record the publish path reads.

2. **Publish the new version to the org feed.** Dispatch the existing publish
   workflow (feature 039), which gates on `FS.GG.Contracts.Tests`, packs, and
   `nuget push --skip-duplicate`es to the org feed. The manual-dispatch path is a
   **`version` override**: it packs and pushes **exactly the version you pass**
   (`-p:Version=<input>`), *not* the fsproj `<Version>`, and — unlike the
   release/tag path — applies **no source-vs-published drift guard**. So you must
   pass the value you set in step 1; a mismatch silently publishes an incoherent
   package:

   ```bash
   # <new> MUST equal the fsproj <Version> bumped in step 1 — confirm first:
   dotnet msbuild src/FS.GG.Contracts/FS.GG.Contracts.fsproj -getProperty:Version
   gh workflow run release.yml --repo FS-GG/FS.GG.SDD -f version=<new>
   ```

   Confirm the version is live before continuing:

   ```bash
   gh api /orgs/FS-GG/packages/nuget/FS.GG.Contracts/versions --jq '.[].name'
   # <new> must be listed (not 404)
   ```

3. **Advance the `.github` registry.** In `FS-GG/.github`
   `registry/dependencies.yml`, advance `fsgg-contracts.version` so the
   `source-coherence` gate goes green again, and — **only after step 2 confirms the
   feed serves `<new>`** — advance `fsgg-contracts.package-version`. The
   `package-version` must never run ahead of the feed. The registry advance is
   cross-repo: file or update the request via the `cross-repo-coordination`
   protocol against the `.github`-side registry issue.

## Why this is a single coordinated change

Per **ADR-0001**, a `FS.GG.Contracts` version bump must update the `.github`
registry in the same coordinated change, and `.github`'s **`source-coherence`** gate
enforces `registry.version == source` on `.github` PRs and `main` (it was
`contract-coherence` until FS-GG/.github#741 re-homed it). Skipping the
publish or the registry advance leaves the invariant broken: the source claims a
version no consumer can resolve from the feed and no registry record reflects.
Doing all three together keeps source, feed, and registry coherent on every bump.

> **The bump has a safe order again — this block used to say it did not.**
>
> Until **FS-GG/.github#741** (landed 2026-07-16) this section carried a STOP: the shared
> `contract-coherence` gate took a `contracts-ref` input defaulting to **`main`** and asserted, by
> strict string equality, that `registry.version` equalled the version in **`FS.GG.SDD@main`'s
> _source_** — an *unreleased branch*, not the published package. That coupled two repos' `main`
> branches, no PR spanned both, and so **both** orderings went red — including this repo's own
> merges, since SDD is itself a *caller* of that gate and not merely its subject.
>
> **#741 removed `contracts-ref` and the SDD-source assertion it fed.** The shared gate now asserts
> only pure functions of committed files. The registry-vs-source equality did not vanish — it moved
> to `source-coherence.yml`, which is **`.github`-local**. A disagreement now reds **`.github`
> alone**: the repo that owns `registry/dependencies.yml` and the only one that can flip it.
>
> So the order is simply:
>
> 1. bump `<Version>` + `ContractVersion.fs` **here**, and publish `<new>` to the feed;
> 2. flip `version` + `package-version` in `.github`'s registry.
>
> Between (1) and (2) `.github` is red and says exactly what is owed. **No other repo is affected,
> and this repo's merges are no longer held by `.github`'s registry.**
>
> **The `2.0.0 -> 2.1.0` debt is still open** and is now landable:
> [FS.GG.SDD#432](https://github.com/FS-GG/FS.GG.SDD/issues/432). #426 grew the public surface
> *after* `2.0.0` was cut and published, so a `.nupkg` labelled `2.0.0` and the source labelled
> `2.0.0` are different artifacts. Note `2.0.1` shipped 2026-07-16, so `source == feed ==
> registry.version == registry.package-version == 2.0.1` holds numerically **and** materially
> today; what remains owed is the **surface growth** shipped under a number that never moved.
> Delete this block once #432 lands.
