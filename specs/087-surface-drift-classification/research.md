# Phase 0 Research: Surface Drift Classification

## Decision 1 — Member extraction: line-based token set, not a full F# parser

**Decision**: Extract "members" from a `.fsi` by a pure, deterministic line tokenizer:

1. Strip block comments `(* … *)` (simple, non-nested) and `//`/`///` line comments to end-of-line.
2. Drop blank / whitespace-only lines.
3. Trim each remaining line and collapse internal runs of whitespace to a single space → a
   *member token*.
4. Compare the **set** of tokens between the baseline text and the source text.

From the two sets:

- `removedOrChanged = baselineTokens \ sourceTokens`
- `added = sourceTokens \ baselineTokens`
- **breaking** if `removedOrChanged` is non-empty (a prior declaration is gone — removed, renamed,
  or its signature changed, since a changed signature is one token gone + one new token),
- else **additive** if `added` is non-empty (only new declarations),
- else **cosmetic** (sets equal but the raw bytes differ ⇒ comments / blank lines / ordering only).

**Why**: The spec fixes the `.fsi` **text** as the source of truth (consistent with feature 086);
no build, no reflection. A set comparison makes ordering and comments irrelevant for free, which is
exactly the additive/breaking/cosmetic contract. Every significant line (including `namespace` /
`module` headers) is a token, so a namespace/module rename correctly reads as breaking.

**Alternatives considered**:

- *FSharp.Compiler.Service AST diff* — most precise, but pulls a heavyweight dependency into a
  generic CLI, is slower, and can *fail* on a signature the workspace's own compiler would still
  accept (version skew). Rejected: over-engineered for an advisory verdict; the text is already the
  governed artifact.
- *Raw unified line diff (added/removed line counts)* — simpler, but order-sensitive and would call
  a pure reordering "changed", defeating the cosmetic carve-out. Rejected.
- *Structural per-declaration parse with brace/indent tracking* — marginally better member
  boundaries, materially more code and edge cases, no behavior change on the spec's fixtures.
  Deferred; can layer on later without changing the report shape.

## Decision 2 — Classify only the drifted set, inside `computeSummary`

**Decision**: Run classification inside `HandlersSurface.computeSummary`, over the existing
`classified` tuples `(source, baseline, sourceText, baselineText)` filtered to the **drifted**
predicate (`Some s, Some b when s <> b`). `missing-baseline` (`Some, None`), `matched`
(`Some s, Some b when s = b`), and `orphan` (baseline with no source) files are excluded (FR-005).

**Why**: The gate already snapshotted both bodies, so no new effect/read is required; the drifted
predicate is already computed for `DriftedSourcePaths`. Reusing it keeps the pure/effect split and
guarantees the classified set == the drifted set.

## Decision 3 — Conservative fallback for an unparseable signature (FR-011)

**Decision**: A drifted source whose raw text is non-empty (has non-whitespace bytes) but yields
**zero** member tokens after comment/blank stripping is treated as *unparseable* → classified
`breaking` with `unparseableFallback = true`, surfaced in the report. It never throws.

**Why**: A signature we cannot reduce to any member is exactly the "can't tell" case; the safe
default is to flag it breaking so the operator inspects it, rather than silently under-report. This
is a concrete, testable trigger (e.g. a source that is only `open` lines/comments) distinct from a
genuine all-removed surface (empty/whitespace source, which is legitimately breaking without the
fallback flag).

## Decision 4 — Advisory only: no diagnostic, no exit-code change (FR-008)

**Decision**: Classification adds report facts and the recommended-bump guidance but emits **no**
new diagnostic. The exit code stays governed solely by the feature-086 `surface.drift` /
`surface.orphanBaseline` diagnostics.

**Why**: ADR-0025 makes classification *advisory-but-loud*; the operator (and the downstream
publishing prompt #171 / `.github` reconcile) confirms. Introducing a blocking diagnostic here would
double-gate drift and break feature-086 exit-code contracts. Verified by exit-code assertions across
additive/breaking/cosmetic (SC-002).

## Decision 5 — String-valued enum-like fields for stable serialization

**Decision**: Represent `Classification` (`additive`/`breaking`/`cosmetic`), `Verdict`
(+`none`), and `RecommendedBump` (`major`/`minor`/`none`) as strings on the report records.

**Why**: Mirrors feature-086's `Mode` string and the existing surface JSON style; keeps the
automation contract stable and human-readable without a serializer for a DU. The severity ordering
and bump mapping live in the pure core, not in the wire format.
