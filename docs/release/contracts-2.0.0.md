---
title: FS.GG.Contracts 2.0.0 — break declaration and the 1.4.1 disposition
category: SDD
categoryindex: 6
index: 21
description: The declared binary break that forces FS.GG.Contracts to 2.0.0, the consumer adaptation step, and the recorded decision on the already-published 1.4.1.
---

# FS.GG.Contracts 2.0.0

`FS.GG.Contracts` moves `1.4.1` → **`2.0.0`**. This note is the **declaration** of
the break, per the migration-note obligation in the
[versioning policy](versioning-policy.md). The break is declared, not suppressed:
there is no `CompatibilitySuppressions.xml`, and there will not be one.

The bump itself follows the
[FS.GG.Contracts version-bump checklist](contracts-version-bump-checklist.md) —
source, feed, and the `.github` registry advance as one coordinated change.

> **This is the SemVer major the repo had already decided on, twice, and never
> made.** It was recorded in `src/FS.GG.Contracts/CompatibilitySuppressions.xml`
> (SDD#87) — which said, verbatim, that the honest resolution is *"a Contracts
> MAJOR bump (1.x -> 2.0.0) + republish"* — and re-litigated in FS.GG.SDD#384.
> The suppression file was then deleted by `f18877f` without the bump being made,
> and the tracking issue (#87) was closed. The debt lost its record twice.
> Tracked to resolution in FS.GG.SDD#393.

## The breaking change

`Fsgg.Provider.ProviderDescriptor` gained a field **in the middle of the record**:

```fsharp
    NameParameter: string
    IdentifierParameter: string option   // ← added mid-record, 4e6f8b7 (feature 080)
    MinimumCliVersion: string option
```

An F# record generates a **positional primary constructor**. Adding the field took
that constructor's arity from **11 to 12**, so the 11-argument `.ctor`
**ceased to exist**. ApiCompat reports it as `CP0002` against the `1.4.0` baseline:

```text
error CP0002: Member 'Fsgg.Provider.ProviderDescriptor.ProviderDescriptor(string, string, string,
  string, FSharpList<ProviderParameterSpec>, FSharpOption<DeclaredCommand>?, ×4…, string,
  FSharpOption<string>?)' exists on [Baseline] lib/net10.0/FS.GG.Contracts.dll but not on
  lib/net10.0/FS.GG.Contracts.dll
```

This is a **binary break**, and it is not fixable by moving the field. There is no
additive way to add a field to an F# record: *every* field changes the generated
constructor arity. (`MinimumCliVersion` was appended at the **end** in feature 052
and was a `CP0002` break too.) So this is a major version, or it is a lie.

`4e6f8b7` introduced it with **no version bump**. `f18877f` then shipped it as a
**patch** — `1.4.1` — because the API-compatibility gate had been blind for its
entire life (`Indeterminate` exited 0; FS.GG.SDD#381) and, once fixed, its finding
was read as a toolchain artifact and overruled.

## Who is actually affected

The break is real and shipped. The **exposure**, however, is narrower than a
removed constructor sounds, because NuGet resolves the **lowest** version that
satisfies a range, not the highest:

| Consumer pin | Resolves | Affected? |
|---|---|---|
| `Version="1.4.0"` (CPM; FS.GG.Governance today) | `1.4.0` — lowest applicable | **No** |
| `[1.0.0, 2.0.0)` | `1.0.1` — lowest in range | **No** |
| `1.4.*` / `1.*` (floating) | `1.4.1` | **Yes** — `MissingMethodException` |
| fresh `dotnet add package FS.GG.Contracts` | latest — was `1.4.1` | **Yes** |
| a regenerated lock file on a floating pin | `1.4.1` | **Yes** |

FS.GG.Rendering does **not** reference the `FS.GG.Contracts` package at all — it
authors a provider *descriptor* (the `scaffold-provider` YAML contract), which is a
different contract id.

The point is not the size of the blast radius. It is that the **version number
lied**: a `1.4.0 → 1.4.1` patch promised binary compatibility and did not deliver
it, so SemVer — the guard rail every one of these pins is trusting — stopped
protecting anyone who trusted it. `2.0.0` restores the promise.

## Consumer adaptation

Re-pin to the `2.x` line:

```xml
<PackageVersion Include="FS.GG.Contracts" Version="2.0.0" />
```

`2.0.0`'s public surface is **identical to `1.4.1`'s** — the 12-field
`ProviderDescriptor` and everything else. So for a consumer already building
against `1.4.1`, this is a version-number change and nothing else: **no source
edit is required**. A consumer still on `≤1.4.0` must add the two fields
(`IdentifierParameter`, `MinimumCliVersion`) to any `ProviderDescriptor` it
constructs positionally; record-expression construction (`{ Name = …; … }`) needs
each field named regardless.

## Decision: what happens to the already-published 1.4.1

`1.4.1` is live on **both** the org GitHub Packages feed and **nuget.org**
(published 2026-07-12). nuget.org packages **cannot be deleted** — only unlisted
and deprecated. The recorded decision (FS.GG.SDD#393):

- **Deprecate `1.4.1` on nuget.org**, reason *Critical Bugs*, alternate package
  `FS.GG.Contracts 2.0.0`, message naming the removed `ProviderDescriptor`
  constructor. A restore that resolves it then warns loudly.
- **Unlist `1.4.1` on nuget.org**, so floating (`1.4.*`) and range resolution stop
  selecting it. An exact pin and an existing lock file still restore it — unlisting
  hides a version, it does not revoke it.
- **Leave `1.4.1` on the org GitHub Packages feed.** Deleting it is destructive: it
  breaks restore for anyone who has already resolved or lock-filed it, and it is
  not cleanly reversible. Once `2.0.0` publishes, `2.0.0` is the newest version on
  that feed and the API-compatibility gate baselines against it.
- **Do not yank or re-publish `1.4.0`.** It is a legitimate release and remains the
  last honest `1.x`.

`1.4.1` is therefore **superseded and deprecated, not erased**. The only *guaranteed*
fix for a given consumer is re-pinning to `2.0.0`; deprecation and unlisting are
the loud, best-effort guard rails around the ones that have not yet.

## Why the gate cannot catch this class of break after the fact

The API-compatibility gate baselines against whatever is **newest on the feed**.
Publishing a break makes it the new baseline, and the gate is green against it
forever after. The window in which a break is catchable is exactly *"introduced but
not yet published"* — the PR that introduces it. A green gate run says nothing about
whether the **current baseline** was itself a legitimate release; here, it was not.

That is why this note exists as a written declaration rather than as a gate result.
See the `BASELINE RATCHET` note in `scripts/apicompat-check.sh`.
