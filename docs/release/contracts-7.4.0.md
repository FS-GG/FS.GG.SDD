---
title: FS.GG.Contracts 7.4.0 — the nupkg finally carries its own api-surface/*.fsi
---

# FS.GG.Contracts 7.4.0

`FS.GG.Contracts` moves `7.3.0` → **`7.4.0`**. This is an **additive minor**, and it is the first
release of this package whose justification is not a row of the
[version-bump checklist](contracts-version-bump-checklist.md)'s table at all.

**The .NET public API surface does not move by one member.** No type, `val`, discriminated-union
case or record field is added, removed, renamed or retyped, and
`docs/api-surface/FS.GG.Contracts/**` is byte-unchanged. What changes is what the **package** is
made of.

## What it adds

The `.nupkg` now carries the package's own signature files:

```
api-surface/ContractVersion.fsi
api-surface/Provider.fsi
api-surface/Registry.fsi
api-surface/Schemas.fsi
api-surface/SkillMirror.fsi
api-surface/Version.fsi
lib/net10.0/FS.GG.Contracts.dll      ← unchanged
lib/net10.0/FS.GG.Contracts.xml      ← unchanged
```

This is [FS-GG/FS.GG.Rendering#782](https://github.com/FS-GG/FS.GG.Rendering/issues/782)'s
**producer half**, established 2026-07-14 and never done here. Every packable project in the org
ships its own `.fsi` under `api-surface/` so consumers can generate their surface mirrors from the
*package* rather than from a hand-copy.

## Why the `.fsi` and not the metadata

A consumer's mirror cannot be generated from PE metadata. A metadata walk yields **names**; an
`.fsi` needs **signatures**, and F# type abbreviations (`type CommandId = string`) compile to
literally nothing in IL. Shipping the `.fsi` is faithful by construction: it *is* the signature
file, as the compiler checked it.

## Why it is a minor and not a patch

Read literally, the checklist's last row — "behaviour change with no surface change; docs;
internals" — says **patch**. That table's axis is "change to the public API surface", because for
seven majors that was the only thing about this package a consumer could depend on.

This release adds a **second axis**. `api-surface/` is a consumer-observable capability, not an
internal, and a consumer is about to depend on it:
[FS.GG.Rendering#1101](https://github.com/FS-GG/FS.GG.Rendering/issues/1101)'s
`scripts/refresh-api-surface-mirror.fsx` **hard-fails** on any pin whose package carries no
`api-surface/`, so **7.4.0 becomes the floor that repo pins against**. A patch bump announces
"nothing new to depend on"; that would be false, and a consumer reading only the number would have
no way to tell `7.3.0` from `7.4.0` on the one property it now requires.

## Why it is not a major

Nothing is removed, renamed, retyped or reshaped. `api-compatibility-gate` was run against the
published `7.3.0` baseline and reports `OK (compatible with 7.3.0)` — a real comparison, not a
`NoBaselineYet` or `Indeterminate` pass. `api-surface/` is an **inert** nupkg folder: NuGet
auto-consumes `lib/`, `build/`, `contentFiles/`, `tools/` and friends, so a custom top-level folder
is carried in the package and never fed to a consumer's compiler. **Nothing a restoring product
builds changes.**

## What it fixes, measured

All **fourteen** previously published versions carry no `api-surface/` — verified by downloading
every one from the flat container and reading the archive, not inferred from the `.fsproj`:

| versions | `api-surface/` entries |
| --- | --- |
| 1.2.0, 1.4.0, 1.4.1, 2.0.0, 2.0.1, 2.1.0, 3.0.0, 4.0.0, 5.0.0, 5.0.1, 6.0.0, 7.0.0, 7.1.0, 7.3.0 | **0** |
| **7.4.0** | **6** |

Because no published package could be read, `FS.GG.Rendering#1094` had to extend a
`legacyPre782Surfaces` bridge forward to Contracts `7.2.0` against a `7.0.0`-era hand-written
`.fsi`. That bridge is **a blind spot by construction**: while an entry exists the generator never
reads the package's real surface, so it cannot detect the taught type drifting — measured, the
generator's waiver-emitting mode emits 969 lines and **not one** is a Contracts member.

## The packed surface IS the compiled surface

The pack rule transforms `@(Compile)` filtered to `.fsi` — the compiler's own input list — so a
signature file added to the project packs automatically and a hand-maintained roster cannot drift
from it. A second hand-copy would have reproduced the very defect this closes, one layer over.

`tests/FS.GG.Contracts.Tests/PackedApiSurfaceTests.fs` runs the real `dotnet pack`, opens the real
`.nupkg`, and asserts **set equality in both directions** against the compile list plus
**byte-identity** of every entry against `src/FS.GG.Contracts/*.fsi`. It fails closed on an absent
subject. Both halves were driven red before being trusted: removing the pack rule reds both tests,
packing only two of six reds both, and packing the right *names* from a drifted hand-copy reds the
byte-identity test alone — which is the case set equality cannot see and the reason both exist.

`release.yml`'s `Pack + publish FS.GG.Contracts` job `needs: contracts-tests`, so a package missing
its `api-surface/` cannot be published at all.

## Adopting it

No source change. Consumers on `7.3.x` upgrade by moving the pin; nothing they compile against has
moved.

`FS.GG.Rendering#1101` discharges against this version: delete both `legacyPre782Surfaces` entries
and `scripts/legacy-api-surfaces/`, and let the generator read Contracts like every other package.

## Release sequence

Publish Contracts `7.4.0` to both feeds, confirm it is live, then advance `fsgg-contracts.version`
and `package-version` in `FS-GG/.github` `registry/dependencies.yml`. The registry flip for `7.3.0`
was filed as `FS-GG/.github#1659` and is separately owed; per `FS-GG/.github#741` the resulting
disagreement reds `.github` alone and holds no merges here.
