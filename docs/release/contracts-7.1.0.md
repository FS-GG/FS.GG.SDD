---
title: FS.GG.Contracts 7.1.0 — the multi-file skill surface, written up after the fact
---

# FS.GG.Contracts 7.1.0

`FS.GG.Contracts` moves `7.0.0` → **`7.1.0`**. This is an **additive minor** — the version-bump
checklist's fourth row, "add a new module, type, or `val`". Nothing is removed, renamed or retyped,
no case is added to an existing public discriminated union, and **no public record gains a field**.

**This note is retrospective** ([FS.GG.SDD#734](https://github.com/FS-GG/FS.GG.SDD/issues/734)).
7.1.0 reached both feeds on 2026-07-27 and was superseded by 7.2.0 before it had a write-up. Every
other **major and minor** back to 2.0.0 carries one — `2.0.0`, `2.1.0`, `3.0.0`, `4.0.0`, `5.0.0`,
`6.0.0`, `7.0.0`, `7.2.0`, `7.3.0`, `7.4.0` — and `2.1.0` settles that this is not a majors-only
convention. (Patches ride their parent's note: `2.0.1` and `5.0.1` have none, by that same rule.)
7.1.0 was the one hole in the run. Nothing red depended on it — no gate reads `docs/release/` — but
these notes are where a version number's *meaning* is recorded, and the number alone cannot warn
anyone.

## What it adds

`Fsgg.SkillMirror`, and nothing else — no other `.fsi` in the package moved.

The **mirror** half ([#717](https://github.com/FS-GG/FS.GG.SDD/issues/717), PR #719 → `57f105f`):

- `skillFilePath` — the canonical on-disk path of a file *inside* a skill
  (`<root>/skills/<id>/<relativePath>`). A pure path builder that validates nothing;
  `skillFilePath root id "SKILL.md"` is exactly `skillPath root id`.
- `SkillFile` — one file of a skill: its body plus the path relative to the skill's own directory.
- `MultiFileSkill` — a skill as the ordered set of files it actually is on disk.
- `MirrorRefusalReason` — why a skill was refused: `UnsafeSkillId`, `DuplicateSkillId`,
  `MissingSkillFile`, `UnsafeRelativePath`, `DuplicateRelativePath`. Independent, named causes.
- `MirrorRefusal` — one refused skill with every reason it was refused for, in a stable order.
- `MirrorPlan` — the writes to perform and the skills refused, as two independent facts.
- `mirrorFiles` — every write placing each multi-file skill into every root, with confinement
  enforced here rather than in the path builder. A refused skill contributes **no** writes at all.

The **verify** half ([#721](https://github.com/FS-GG/FS.GG.SDD/issues/721), PR #725 → `52fb6ae`):

- `ActualSkillFiles` — the files found at `(Root, Id)`; `None` means the root carries no copy at all.
- `SkillFileDrift` — per-file drift as the same three independent facts `SkillDrift` keeps apart.
- `MultiFileSkillDrift` — the roots carrying no copy, plus the per-file drift naming the path.
- `verifyFiles` — the verify half of `mirrorFiles`, over a skill's whole file set.

## Why

`mirror`'s `(id, body)` model could express a skill as exactly one `SKILL.md`, and ADR-0014's "one
implementation" was therefore SKILL.md-only — while the org's own coordination-kit skills are five to
seven files each, so the driver hand-rolled the auxiliary cross-root check the library could not
express. `mirrorFiles` and `verifyFiles` are the generalization.

Both are **strict** generalizations, which is why `mirror` and `verify` are untouched rather than
replaced:

```fsharp
mirrorFiles roots [ { Id = id; Files = [ { RelativePath = "SKILL.md"; Body = b } ] } ]
// yields exactly
mirror roots [ id, b ]
```

and `verifyFiles`, fed copies whose file set is exactly `SKILL.md`, reports precisely what `verify`
reports — same missing roots, same divergence, same hash-mismatch roots.

## Two PRs shipped inside this number, and the bump commit assessed one

This is the part the diff does not explain, and it is worth the paragraph because it is the same
shape as the 2.0.1 mistake [`contracts-2.1.0.md`](contracts-2.1.0.md) exists to record.

[#720](https://github.com/FS-GG/FS.GG.SDD/issues/720) justified the minor with the `.fsi` delta
`a066e0b..57f105f`, **+55/−0**. That is a true statement about PR #719 alone, and it is the wrong
baseline: a package's surface delta is measured from the **publish point of the previous version**,
not from a tag and not from the bump commit's own PR. Measured that way:

```text
7ea65ac  (Contracts 7.0.0, published 2026-07-26, release run 30188162840)
0376309  (Contracts 7.1.0, published 2026-07-27, release run 30251799549)
git diff 7ea65ac 0376309 -- docs/api-surface/FS.GG.Contracts/
  → SkillMirror.fsi | 101 insertions(+), 0 deletions(-)
```

Nearly twice the filing's number. The extra **+46** is `52fb6ae` (#721, PR #725), which landed
**after** the bump commit `a372259` and **before** the publish — surface that shipped inside 7.1.0
which the bump commit never assessed.

**The conclusion survives the correction.** Across the whole publish-to-publish range the delta is
insertion-only — zero deletions on any `FS.GG.Contracts` `.fsi`, and `SkillMirror.fsi` is the only
one of them that moved — so not one existing signature was removed or retyped. 7.1.0 is the right
number; the filing was right for an incomplete reason. (Other `.fsi` in the repo did change in that
window — `FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands` — but those ship in `FS.GG.SDD.Cli`, not in
this package, and are not what this version number describes.)

Also in that range, and not a surface change: `src/FS.GG.Contracts/CompatibilitySuppressions.xml`
was **deleted** (#702, `d0f4514`), retiring the Contracts-7 transition suppression. That removes a
suppression, not a surface — 7.0.0 had already taken the major it recorded.

## Compatibility

**Upgrading from 7.0.0 to 7.1.0 requires no consumer action.**

`mirror`, `verify`, `sha256`, `skillPath`, `skillIdOfPath`, `mirrorTargetRoots`, `retargetSkillPath`,
`providerSourceRoot`, `MirrorWrite`, `ExpectedSkill`, `ActualCopy` and `SkillDrift` are **untouched**;
every existing caller keeps its byte-for-byte call shape.

**No pre-existing public discriminated union gained a case.** `MirrorRefusalReason` carries five
cases and is itself a new type, so the source-breaking `FS0025` *incomplete pattern matches* hazard
that [`contracts-2.1.0.md`](contracts-2.1.0.md) had to warn about does **not** apply here. No public
record gained a field either, so no positional primary constructor was regenerated (`CP0002`). Those
are the two rows of the change-class table that force a major, and neither fires — because this is
the first `FS.GG.Contracts` release since 1.4.0 whose growth is **only new types**. It is therefore
the first minor since 1.4.0 to carry any new public surface at all: `2.1.0` was a *corrective* minor
that added no code, and the `2.0.0` → `7.0.0` run was six majors, every one of them a public-record
shape change with no additive spelling.

Measured on the committed reflection baseline (`tests/FS.GG.Contracts.Tests/PublicSurface.baseline`),
the delta is **+41 lines, zero deletions**.

ApiCompat was green against the 7.0.0 baseline (the `API compatibility gate` check run on `a372259`
concluded `success`), and that is corroboration rather than the
classification: it is a *break* detector, structurally blind to every additive row of the checklist's
table, and `scripts/apicompat-check.sh` documents its own baseline ratchet. The insertion-only `.fsi`
and reflection-baseline deltas above are what actually classify this release.

## Provenance

| | |
| --- | --- |
| Bump | [#720](https://github.com/FS-GG/FS.GG.SDD/issues/720) → PR [#723](https://github.com/FS-GG/FS.GG.SDD/pull/723), merge `a372259` |
| Surface | [#717](https://github.com/FS-GG/FS.GG.SDD/issues/717) → PR #719 (`57f105f`); [#721](https://github.com/FS-GG/FS.GG.SDD/issues/721) → PR #725 (`52fb6ae`) |
| Publish point | `0376309f959a52401e6fe27b3fff05259993f852` |
| Publish | `release` run [30251799549](https://github.com/FS-GG/FS.GG.SDD/actions/runs/30251799549) — `workflow_dispatch`, conclusion `success`, 2026-07-27T08:55:56Z |
| Feeds | live on the org feed and nuget.org |
| This note | [#734](https://github.com/FS-GG/FS.GG.SDD/issues/734) |

At the publish point `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` `<Version>` and
`Fsgg.ContractVersion.value` both read `7.1.0`, as the in-repo two-facts-must-agree test requires.

## Release sequence, as it actually ran

Source moved first (`a372259`), then the feeds. The **registry flip was never made for this
version**: `FS-GG/.github` `registry/dependencies.yml` was pinned at `7.0.0` when 7.1.0 published and
advanced straight to `7.2.0`, so neither `fsgg-contracts.version` nor `package-version` ever held
`7.1.0` — that row records 7.1.0 as history rather than as a pin. Nothing is owed here as a result;
the pin has since moved on to `7.4.0`. This file is where 7.1.0's content is written down.
