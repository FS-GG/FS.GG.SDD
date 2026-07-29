---
title: FS.GG.Contracts 7.5.0 — the mirror fold gets a third observation state
---

# FS.GG.Contracts 7.5.0

`FS.GG.Contracts` moves `7.4.0` → **`7.5.0`**. This is an **additive minor** under the fourth row of
the [version-bump checklist](contracts-version-bump-checklist.md)'s table — *add a new module, type,
or `val`*. One type and two `val`s are added to `SkillMirror`; nothing is removed, renamed, retyped
or reshaped.

## The defect

`SkillMirror.verifyFiles` and `verifyFileSet` model a copy with two states: `ActualSkillFiles.Files =
Some files` ("this root's copy carries exactly these") and `= None` ("this root carries no copy").
Every absence they report is derived from the rows they were **given** — a file no present root
contributed a row for is `MissingRoots` at that root.

So **NOT OBSERVED and NOT THERE were the same fact**, and every caller that drops an unobservable
subject got it back classified as an absence. Measured on `FS.GG.SDD@main`, one `chmod 000` on a
skill's `references/` directory and `fsgg-sdd doctor` reported:

```
exit                 0
isCoherent           false
unlistableDirectory  .claude/skills/fs-gg-demo/references
skillDriftPaths      .claude/skills/fs-gg-demo/references/deep-detail.md
```

That last line is wrong. `deep-detail.md` is present at `.claude`, byte-identical to its siblings.
It is reported *not mirrored at `.claude`* — a class whose advisory sentence asserts that another
root carries a file this one does not.

This is the same shape [FS.GG.SDD#745](https://github.com/FS-GG/FS.GG.SDD/issues/745) closed one
layer up. #745 gave the **read** edge a state for "present but not obtained"; the **mirror** fold had
none, so the read edge's honesty was discarded by the fold that consumed it.

## What it adds

```fsharp
type UnobservedSkillFiles =
    { Root: string
      Id: string
      RelativePaths: string list }

val verifyObservedFiles:
    roots: string list -> expected: ExpectedSkill list -> actual: ActualSkillFiles list ->
    unobserved: UnobservedSkillFiles list -> MultiFileSkillDrift list

val verifyObservedFileSet:
    roots: string list -> expected: ExpectedSkillFiles list -> actual: ActualSkillFiles list ->
    unobserved: UnobservedSkillFiles list -> DeclaredSkillDrift list
```

An unobserved subject is **withheld** from `MissingRoots`, at the file level and at the skill level
alike. Every other fact is unchanged: facts 2 and 3 (cross-root identity, digest match) are computed
from bodies that are in hand, and an unobserved subject supplies none — so it can neither create a
divergence nor mask one.

- `RelativePaths` are relative to `<root>/skills/<id>/`, the same coordinates as
  `SkillFile.RelativePath`. Each names **either** a file that could not be obtained **or** a
  DIRECTORY whose listing could not be taken, in which case every path beneath it is unobserved too
  — a caller that could not open a directory cannot enumerate what is inside it.
- `"SKILL.md"` withholds the **whole copy**: it is what makes a directory a copy of the skill, so a
  caller that could not read it has not established that the root carries no copy.
- Several entries for one `(Root, Id)` **union**; none shadows another. The empty relative path is
  ignored.

## The third state is an INPUT, not an output

Deliberately. What an unobserved subject needs is for nothing to be **said** about it. The caller
already holds the fact — it is the only party that can, since it owns the reads — and it reports the
subject itself. **Withholding is not clearing**: a verdict that reads coherent over an unobserved
subject is the caller's defect, and this release makes no promise about it. In `fsgg-sdd doctor` the
run is still non-coherent and the subject is still named, by `unreadableFile` /
`unlistableDirectory` — the diagnostic whose remedy (`chmod +r`) is the true one, where the drift
advisory's (`fsgg-sdd upgrade`) never was: `upgrade` cannot repair a file it cannot read either.

## Why not a field on `SkillFileDrift`

That was the other candidate, and it is the checklist's **first** row. An F# record generates a
positional primary constructor, so any added field changes its arity and **deletes the old
constructor** — a binary break, and a coordinated major nobody authorised. Two new entry points and
one new type cost none of that.

`verifyFiles` and `verifyFileSet` are **defined as** the new folds with an empty unobserved set, so
"`verifyFiles` is `verifyObservedFiles` with nothing unobserved" is true by construction rather than
by a test that could drift. Both equivalences are also pinned in `SkillMirrorTests`.

## Why it is not a major

`scripts/apicompat-check.sh` was run against the published `7.4.0` baseline and reports
`OK (compatible with 7.4.0)` — a real comparison, not a `NoBaselineYet` or `Indeterminate` pass. The
committed `.fsi` under `docs/api-surface/FS.GG.Contracts/` shows **+62 / -0**: `surface --check`
classifies the delta `additive (minor)`. No existing caller changes a byte of its call shape.

## Adopting it

No source change. Consumers on `7.4.x` upgrade by moving the pin; nothing they compile against has
moved. A caller that knows which subjects it could not observe — because it owns its own reads — can
switch `verifyFiles` → `verifyObservedFiles` and pass them, and stop reporting absences it has no
evidence for.

Fixes [FS.GG.SDD#760](https://github.com/FS-GG/FS.GG.SDD/issues/760).

## Release sequence

Publish Contracts `7.5.0` to the org feed, confirm it is live, then advance `fsgg-contracts.version`
and `package-version` in `FS-GG/.github` `registry/dependencies.yml`. Per
[FS-GG/.github#741](https://github.com/FS-GG/.github/issues/741) the disagreement between the source
bump and the registry reds **`.github` alone** and holds no merges in this repo.
