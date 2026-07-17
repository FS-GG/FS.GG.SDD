---
title: FS.GG.Contracts 2.1.0 — the additive minor that 2.0.1 should have been
category: SDD
categoryindex: 6
index: 25
description: Why 2.1.0 republishes an identical public surface — the corrective minor for surface growth that reached the feed labelled as a patch, and the consumer note the new RegistryRule case owes.
---

# FS.GG.Contracts 2.1.0

`FS.GG.Contracts` moves `2.0.1` → **`2.1.0`**. This note exists because the diff
does not explain itself: **`2.1.0` adds no code.** Its public surface is
byte-identical to `2.0.1`'s. Only the number moves.

That is the point. The surface this number describes reached the feed **already**,
labelled a patch. `2.1.0` is the correction.

The bump follows the
[version-bump checklist](contracts-version-bump-checklist.md) — source, feed, and
the `.github` registry advance as one coordinated change.

## What happened

[#426](https://github.com/FS-GG/FS.GG.SDD/issues/426) (`80d0c28`, 2026-07-14) grew
the public surface of `Fsgg.Registry` by 78 lines and moved no version:

| added | kind |
|---|---|
| `SkillRegistryEntry`, `SkillRegistryDocument`, `MirrorDeclaration` | public types |
| `validateSkillRegistry` | public `val` |
| `MalformedField of fieldName: string` | **new case on the public DU `RegistryRule`** |

By this repo's own rule — *"add a new module, type, or `val`"* → additive →
**minor** — that owed `2.1.0`. It shipped in `2.0.1`, a **patch**.

So for a consumer, `2.0.1` claims *"patch — no new surface"* and delivers four new
public types, a new `val`, and a new DU case. The API is additive and nothing
breaks; the **number** is what was false, and it was false in the safe direction.

## Why both detectors were green, which is the part worth keeping

Neither gate was broken. Each was looking somewhere true.

- **ApiCompat passed, correctly.** It is a *break* detector. Additions are
  binary-compatible, and a new DU case doubly so — every existing case constructor
  and tag survives. It is structurally blind to every additive row of the
  checklist's table.
- **The 2.0.1 classification was made against the wrong baseline.** It recorded
  *"the package API surface (`Schemas.fsi`) unchanged"*. True of `Schemas.fsi` —
  and the growth was in `Registry.fsi`. Diffed **tag-to-tag** (`v0.11.0` →
  `v0.12.0`) the `.fsi` surface really is unchanged, because Contracts `2.0.0` was
  **published from `04dd742` (07-12), two days before `v0.11.0` was cut**. #426
  landed in the gap between the publish point and the next tag — precisely where a
  tag-to-tag diff cannot see it.

The measurement that settles it, against the published artifacts rather than the tags:

```text
04dd742  (Contracts 2.0.0, published 07-12)  — does NOT contain #426
v0.12.0  (Contracts 2.0.1, published 07-16)  — contains #426
git diff 04dd742 v0.12.0 -- src/FS.GG.Contracts/Registry.fsi
  → 78 insertions(+), 1 deletion(-)
```

The feed's `2.0.0` → `2.0.1` delta **is** that surface growth.

## The detector that sees this class

A committed `.fsi` baseline ([#475](https://github.com/FS-GG/FS.GG.SDD/issues/475),
PR #484). It is keyed on the **baseline**, not on a tag, which is exactly why it
cannot be fooled the same way. Replaying #426 against it:

```text
surfaceClassificationVerdict: additive   (minor)
surfaceVersionCurrent:   2.0.1
surfaceVersionSuggested: 2.1.0
```

It names this number on sight. Note it is **advisory** — `surface.versionBumpRequired`
is a warning that never changes the exit code (FS-GG/.github ADR-0025 §2:
*"advisory-but-loud; the operator confirms"*). What `--check` blocks on is baseline
drift, so growth cannot land **silently**; deciding the bump is still a human's job.

## Consumer note — `RegistryRule` gained a case

Required by the checklist's DU row, and it applies from **`2.0.1` onward**, not from
`2.1.0` — the case has been on the feed since 2026-07-16.

`Fsgg.Registry.RegistryRule` gained `MalformedField of fieldName: string`. That is
binary-compatible but **source**-breaking: a consumer whose `match` over
`RegistryRule` is exhaustive and carries no wildcard now gets `FS0025`
(*incomplete pattern matches*) — a warning by default, an **error** in any repo that
promotes it, as this one does.

Prefer a wildcard arm. Nothing has to change to *upgrade*; this bites only where a
`match` enumerates every case.

## Disposition of 2.0.1

**Left on the feed, not yanked.** It is a real release, it is what the growth
actually shipped in, and unpublishing breaks anyone who has already lock-filed it —
not cleanly reversible. `2.1.0` supersedes it; it does not erase it.

`2.0.1`'s own contribution stands and is unaffected: the
`governanceHandoffContractVersion` constant `1.0.0` → `1.1.0`, reconciling three
drifted hand-copies.

Tracked at [#432](https://github.com/FS-GG/FS.GG.SDD/issues/432).
