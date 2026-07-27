---
title: FS.GG.Contracts 7.2.0 — the skill manifest content-addresses the whole file set
---

# FS.GG.Contracts 7.2.0

`FS.GG.Contracts` moves `7.1.0` → **`7.2.0`**. This is an **additive minor** — the version-bump
checklist's fourth row, "add a new module, type, or `val`". Nothing is removed, renamed or retyped,
no case is added to an existing public discriminated union, and **no public record gains a field**.

## What it adds

`Fsgg.Schemas`

- `SkillManifestFile` — one file of a skill and its digest, keyed by the path relative to the
  skill's own directory (`SKILL.md`, `references/deep-detail.md`, …).
- `SkillManifestFileSet` — one skill in a schema-v2 manifest: the v1 `SkillManifestEntry` carried
  **whole**, plus that skill's complete file set.
- `SkillManifestV2` — the ADR-0017 producer manifest at schema v2.
- `skillManifestVersion` moves `1` → `2`.

`Fsgg.SkillMirror`

- `ExpectedSkillFiles` — the multi-file generalization of `ExpectedSkill`.
- `verifyFileSet` — `verifyFiles` with its third fact widened from `SKILL.md` to the whole declared
  file set.

## Why

`SkillManifestEntry.Sha256` content-addresses a skill's `SKILL.md` body and nothing else, and
`verifyFiles` therefore populated `SkillFileDrift.HashMismatchRoots` for `SKILL.md` alone, **by
construction**. Measured on `main` when this landed: 32 skills, 51 files, of which the producer
manifest declared a digest for **16**. For the other 35 files, `HashMismatchRoots = []` meant *no
digest was available* while reading as *hash checked, clean*.

Cross-root identity — which those files did have — is a **consistency** guarantee, not an
**authenticity** one. Three roots all materialized from one tampered producer copy are
byte-identical, so `verifyFiles` reports no drift and no digest anywhere contradicts it.

## What `verifyFileSet` states that `verifyFiles` cannot

The three facts (`MissingRoots`, `Divergent`, `HashMismatchRoots`) stay **independent** and keep
their meanings; what widens is the set fact 3 is computed over. The declared file set is an
**authority**, not a filter, so the compared set is `declared ∪ observed`:

- a **declared** file absent from a root that carries the skill is `MissingRoots` — *including when
  it is absent from every root*. `verifyFiles` compares the observed union, so a file deleted
  everywhere leaves nothing to compare and the skill reads coherent;
- an **observed** file the declaration does not cover is reported with every root carrying it, since
  no declared digest authorises those bytes.

An **empty** `Files` means "no declared file set" — the exact analogue of `ExpectedSkill.Sha256 = ""`
— and skips fact 3 entirely. That case is real: a root may vendor a co-tenant skill whose producer
manifest lives in another repository, and inventing an expectation for it would be a fabricated
authority.

## Compatibility

`verify`, `verifyFiles`, `ExpectedSkill`, `ActualCopy`, `ActualSkillFiles`, `SkillDrift`,
`SkillFileDrift`, `MultiFileSkillDrift`, `SkillManifest` and `SkillManifestEntry` are **untouched**.
Every existing caller keeps its byte-for-byte call shape; consumers on 7.1.x need no source change.

The additive spelling was a **constraint, not a coincidence**. The obvious way to express this
amendment is a `Files` field on `Schemas.SkillManifestEntry` or on `SkillMirror.ExpectedSkill`, and
either would regenerate that record's positional primary constructor and delete the old one
(`CP0002`) — a coordinated major that was not authorised. Expressing it as new types and a new entry
point beside the shipped ones is what makes it a minor.

Measured on the committed reflection baseline (`tests/FS.GG.Contracts.Tests/PublicSurface.baseline`),
the delta is **+14 lines, zero deletions**.

## The wire document is a superset of v1

`schemaVersion` moves to `2`, and every v1 property is retained **in its v1 position** with `files`
appended last. A v1 reader that tolerates unknown properties reads a v2 document unchanged and
correctly; `sha256` still carries the `SKILL.md` digest, and is the same value the `files[]`
`SKILL.md` row carries. All file digests use `Fsgg.SkillMirror.sha256` (CRLF-normalized), so one
digest rule governs the whole document.

The bump is nonetheless a **version bump and not a silent widening**: at v1 the absence of per-file
digests was a *true* statement that the auxiliaries carried no declared authority, and at v2 the
declared set is complete, so a file outside it is a file no digest authorises. Those are different
claims and a consumer must be able to tell which it holds.

## Release sequence

Publish Contracts 7.2.0 to the org feed, confirm it is live, then advance `fsgg-contracts.version`
and `package-version` in `FS-GG/.github` `registry/dependencies.yml`. Note that registry was already
pinned at `7.0.0` while source and feed were at `7.1.0` — the 7.1.0 flip was never made — so this
bump widens an existing, separately owed gap rather than opening a new one.
