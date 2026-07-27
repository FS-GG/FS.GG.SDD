---
title: FS.GG.Contracts 7.3.0 — a skill body that does not decode is REFUSED, not hashed as U+FFFD
---

# FS.GG.Contracts 7.3.0

`FS.GG.Contracts` moves `7.2.0` → **`7.3.0`**. This is an **additive minor** — the version-bump
checklist's fourth row, "add a new module, type, or `val`". Nothing is removed, renamed or retyped,
no case is added to an existing public discriminated union, and **no public record gains a field**.

## What it adds

`Fsgg.SkillMirror`

- `BodyRefusalReason` — why a body's raw bytes are not a skill body. One case today,
  `NotDecodable of byteOffset: int`, carrying the offset *within the file* at which the first
  invalid sequence begins.
- `decodeBody: byte array -> Result<string, BodyRefusalReason>` — decode a body's raw bytes exactly
  as the read seam does, but **refuse** a body that does not decode instead of substituting
  `U+FFFD`.
- `sha256Bytes: byte array -> Result<string, BodyRefusalReason>` — `sha256` computed from raw bytes.

## Why

`sha256` takes text a caller has **already decoded**, and every caller in the org decodes with
`File.ReadAllText`, whose UTF-8 decoder substitutes `U+FFFD` for an invalid sequence **before** the
body reaches the library. So for a body that is not valid UTF-8 the digest addresses something the
file does not contain, and two **different** files collide under one digest:

| input bytes | `sha256` (decode, then hash) | raw-byte hash |
| --- | --- | --- |
| `0xFF` | `83d544ccc223c057d2bf80d3f2a32982c32c3c0db8e2674820da5064783fb097` | `a8100ae6…` |
| `0xFE` | `83d544ccc223c057d2bf80d3f2a32982c32c3c0db8e2674820da5064783fb097` | `aa687b58…` |

Under ADR-0014 §Decision 3 clause (c) — *"hash matches the manifest"* — that is a fail-open on the
**producing** side: two distinct bodies recorded under one digest, and nothing downstream able to
tell them apart. The consuming side fails *closed* (a raw-byte shell digest reports `[drifted]`),
which is exactly why it stayed invisible.

## The digest is NOT redefined

Rehashing over raw bytes was the other candidate and is **rejected**: it changes the digest of
*every* file, so every recorded manifest digest in every repo would need regenerating in one
coordinated act, and it is a behaviour change on a published surface. **Refusing costs no digest
change for any body that decodes**, so there is no manifest migration anywhere.

`decodeBody` returns character-for-character what `File.ReadAllText` returns — it reproduces the
same BOM detection (UTF-8, UTF-16 LE/BE, UTF-32 LE/BE), strips the same preamble, and only the
mangling case is refused. `sha256Bytes` is `decodeBody` composed with the **unchanged** `sha256`, so
"byte-identical to today's digests" holds by construction rather than by a test that could drift.

## Every encoding is held to this, not UTF-8 alone

`File.ReadAllText` BOM-detects, so it can select UTF-16 LE/BE or UTF-32 LE/BE — and it mangles those
too, on an odd byte length, an unpaired surrogate, or a scalar above `U+10FFFF`. The result is the
**same** `U+FFFD` and therefore the **same** `83d544cc…` digest: `FE FF 41` and `FE FF 42` collide
exactly as `0xFF` and `0xFE` do. A refusal that fired only on UTF-8 would close the front door and
leave that open behind a BOM, so all five decoders are the throwing ones.

**What stays out of scope** is the separate `FS-GG/.github#1589` disagreement about *which BOMs the
consuming shells strip* — there the library is the permissive side and the polarity is reversed. A
**well-formed** UTF-16/UTF-32 body decodes here and is not refused; refusing it would turn a file
that reads fine today into a hard failure, which is a different change and not this one.

## Adopting it

`sha256` is untouched and every existing caller keeps its byte-for-byte call shape; consumers on
`7.2.x` need no source change. **The fail-open is only closed where a caller swaps its read for the
byte seam** — this release makes the refusal *available* as ADR-0014 §Decision 2's one
implementation, so the shells and producers inherit it through the callable seam rather than each
growing their own check.

## Measured exposure

Across all **1881** tracked files in `FS-GG/FS.GG.SDD` — including all **103** `SKILL.md` — **zero**
contain invalid UTF-8 and **zero** carry a UTF-16/32 BOM. The one invalid-UTF-8 file is
`assets/icon.png` (a PNG, never read as a skill body) and the one UTF-8 BOM is on `FS.GG.SDD.sln`.
The equivalent measurement in `FS-GG/.github` (766 files, 39 `SKILL.md`) is also zero. **The refusal
turns no currently-green tree red in either repo.**

## Compatibility

`sha256`, `mirror`, `mirrorFiles`, `verify`, `verifyFiles`, `verifyFileSet` and every shipped type
are **untouched**. Measured on the committed reflection baseline
(`tests/FS.GG.Contracts.Tests/PublicSurface.baseline`), the delta is **+5 lines, zero deletions**.

## Release sequence

Publish Contracts 7.3.0 to the org feed, confirm it is live, then advance `fsgg-contracts.version`
and `package-version` in `FS-GG/.github` `registry/dependencies.yml`. Note that registry was already
pinned at `7.0.0` while source and feed were at `7.2.0` — neither the 7.1.0 nor the 7.2.0 flip was
ever made — so this bump widens an existing, separately owed gap rather than opening a new one.
Per `FS-GG/.github#741` the resulting disagreement reds `.github` alone.
