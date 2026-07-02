# Feature Specification: Harden malformed-reference handling & digest normalization

**Feature Branch**: `060-harden-malformed-reference-handling`

**Created**: 2026-07-02

**Input**: Remediation #6 of the 2026-07-02 code-quality & architecture review (§2.5).
"Silently dropped malformed ids; divergent schema-version policies; CRLF-sensitive skill
digest hashing."

## Context

Resolves **FS-GG/FS.GG.SDD#70** (roadmap item from the 2026-07-02 review, child of the
remediation epic FS-GG/.github#124). The review found three classes of defect where
lifecycle parsers **silently accept** malformed input or apply **divergent policies** for
the same concept:

1. **Malformed references vanish.** `Internal.parseTaskIds/parseRequirementIds/…` use
   `Result.toOption`, so a malformed reference in `tasks.yml`/`evidence.yml` (e.g.
   `dependencies: [T01]` — not the canonical `T001`) disappears with no diagnostic and
   `WorkModel.referenceDiagnostics` never sees the edge. A dropped dependency can flip
   verify readiness. Task/evidence entries with bad ids skip wholesale.
2. **Three schema-version policies disagree.** `SchemaVersion.classifyRaw` is the canonical
   classifier, but `WorkModel.parseWorkModel` accepts any `version >= 1` (so a
   schemaVersion-3 work model parses here yet blocks everywhere else), and
   `ScaffoldProvenance.tryParse` uses a Major-only `isSupported` check. Same concept, three
   grammars.
3. **Digest normalization mismatch.** `SchemaVersion.sha256Text` normalizes `CRLF→LF` before
   hashing; `Fsgg.SkillMirror.sha256` hashes raw bytes. A CRLF checkout hash-mismatches the
   057/058 per-skill sha256 manifest for logically identical content.

This feature is a **behavioral hardening**. No public signature changes for the shared
grammars, no schema-version bump, no JSON contract byte changes for valid inputs.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Malformed authored ids are diagnosed, not dropped (Priority: P1)

A product author writes a malformed reference into `tasks.yml` or `evidence.yml` — a
dependency `T01` (missing a digit), a requirement `FR1`, an evidence ref `EV_bad`. Running a
lifecycle command reports a blocking diagnostic naming the malformed value; it never silently
succeeds with the reference dropped.

**Why this priority**: A silently-dropped dependency or requirement reference can flip verify
readiness from blocked to ready — the highest-consequence silent-accept in the set.

**Independent Test**: `parseTaskFacts` over a `tasks.yml` whose task declares
`dependencies: [T01]` returns a work item whose diagnostics contain a `malformedReference`
error naming `T01`; a well-formed `dependencies: [T001]` produces no such diagnostic.

### User Story 2 - One schema-version policy governs every artifact (Priority: P2)

A work-model.json (or scaffold-provenance.json) carrying `schemaVersion: 2`/`3` is rejected
with a schema diagnostic, exactly as the authored-artifact parsers already reject it — not
accepted by a looser local check.

**Why this priority**: The divergence is latent today (SDD only ever writes schemaVersion 1),
but a single canonical grammar removes a class of future drift where "parses here, blocks
there" hides a real incompatibility.

**Independent Test**: `WorkModel.parseWorkModel` over a work model with `schemaVersion: 3`
returns `Error` (previously `Ok`); `schemaVersion: 1` still returns `Ok`.
`ScaffoldProvenance.tryParse` over `schemaVersion: 2` returns `None`.

### User Story 3 - Content-identical skills hash-match across line endings (Priority: P2)

Two byte-for-byte-logically-identical `SKILL.md` files that differ only in line endings
(`CRLF` vs `LF`) produce the **same** `Fsgg.SkillMirror.sha256`, so a CRLF checkout does not
spuriously flag skill drift against the 057/058 per-skill manifest.

**Why this priority**: A false drift signal on a CRLF checkout undermines the skill-mirror
verification the 057/058 work established.

**Independent Test**: `SkillMirror.sha256 "a\r\nb"` equals `SkillMirror.sha256 "a\nb"`, and
both equal `SchemaVersion.sha256Text "a\nb"`'s value.

## Requirements *(mandatory)*

- **FR-001**: The `tasks.yml` and `evidence.yml` parsers MUST emit a blocking
  `malformedReference` diagnostic for each declared reference (task dependency, requirement,
  decision, required-evidence, evidence task/requirement/decision ref) whose value fails its
  id smart constructor, instead of silently dropping it via `Result.toOption`. Well-formed
  references still parse into the typed model unchanged. *(covers AC-001)*
- **FR-002**: `WorkModel.parseWorkModel` MUST accept a work-model `schemaVersion` only when
  the canonical `SchemaVersion` classifier deems it non-blocking, and reject otherwise with a
  schema diagnostic — replacing the local `version >= 1` check. *(covers AC-002)*
- **FR-003**: `ScaffoldProvenance.tryParse` MUST gate its `schemaVersion` through the same
  canonical `SchemaVersion` classifier rather than a Major-only `isSupported` check. *(covers AC-002)*
- **FR-004**: `Fsgg.SkillMirror.sha256` MUST normalize `CRLF→LF` before hashing, matching
  `SchemaVersion.sha256Text`, so content-identical bodies hash identically regardless of line
  endings. *(covers AC-003)*
- **FR-005**: No public API signature change for the shared grammars, no schema-version bump,
  and no JSON/golden contract byte change for valid inputs. *(covers AC-004)*

## Acceptance Criteria

- **AC-001**: A malformed id in `tasks.yml`/`evidence.yml` yields a `malformedReference`
  diagnostic (blocking), not a silent drop; valid ids are unaffected.
- **AC-002**: A `schemaVersion: 2`/`3` work model or provenance record is rejected by the
  same canonical classifier the authored parsers use; `schemaVersion: 1` still parses.
- **AC-003**: `SkillMirror.sha256` is line-ending-insensitive and agrees with
  `SchemaVersion.sha256Text` for LF-normalized content.
- **AC-004**: Existing tests and JSON/golden contracts are byte-unchanged for valid inputs.

## Out of Scope

- **The review's "Also:" bullet (§2.5, remediation #6, last paragraph).** Two items, both
  API-redesign/infrastructure shaped, tracked as a follow-up on #70:
  - Making `Identifiers` id `Value` fields **private** (non-bypassable smart constructors).
    The bypasses at `Task.fs`/`WorkItem.fs` are graceful fallbacks for a malformed *workId*;
    sealing `Value` is a library-wide API change touching every construction site and the
    `.fsi`, with its own golden-contract pass.
  - Replacing `Serialization.canonicalizeOutputDigestForHash`'s regex with a structural
    serialize-with-`None`. The regex canonicalizes arbitrary on-disk JSON text in
    `outputDigestStale`; a structural replacement needs a full work-model JSON deserializer
    that does not exist yet.
- `Config.parseProviderRegistry` incomplete-entry drop: already surfaced downstream as
  `scaffold.providerUnknown` when a `--provider` names a dropped entry (documented behavior);
  not a silent readiness-affecting drop, so unchanged here.
- Reconciling the other loose version call sites named in the review (tracked with #66's
  out-of-scope list).
