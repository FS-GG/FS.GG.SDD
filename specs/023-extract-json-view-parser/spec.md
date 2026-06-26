# Feature Specification: Extract a shared JSON view-parser skeleton (total matches)

**Feature Branch**: `023-extract-json-view-parser`

**Created**: 2026-06-26

**Status**: Draft

**Change Tier**: Tier 2 (internal change) — implementation cleanup with no
user-visible or tool-visible contract change. Public `.fsi` signatures and
surface-area baselines remain unchanged; requires spec and tests only.

**Input**: User description: "next item in docs/reports/2026-06-26-074428-refactor-analysis.md" → roadmap item **R4** (§5.3 §4): *Extract `parseJsonView`, making the 4 matches total.*

## Context

The four JSON-backed lifecycle view parsers — `parseAnalysisView`,
`parseVerificationView`, `parseShipView`, and `parseGeneratedAgentGuidance`
(now living in `Analysis.fs` / `Verify.fs` / `Ship.fs` / `Guidance.fs` after
R3) — each repeat the same 7-step skeleton: parse the `JsonDocument`, read
`schemaVersion`, classify it, **match version/status**, parse identity fields
(`workId`/`stage`), map-and-sort each array field, and emit duplicated
malformed/unsupported/future error arms.

That shared `version, status` match is **non-exhaustive** in all four parsers
(the §4 finding, 4 × FS0025): the `(None, Current)` and `(None, Deprecated)`
combinations have no arm, so the match would raise `MatchFailureException` at
runtime. It is "safe" today only because `SchemaVersion.classifyRaw` is assumed
never to pair a `Current`/`Deprecated` status with a `None` version — an
invariant the types do not encode. This refactor removes the duplication **and**
the latent runtime throw in one move.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One shared view-parser skeleton (Priority: P1)

A maintainer changing how lifecycle views handle schema classification,
identity validation, or JSON error reporting edits **one** place, not four
near-identical parsers. The four public view parsers route through a single
parameterized skeleton that owns the parse → classify → identity → error-arm
structure; each parser supplies only its artifact-specific record builder and
field/entry parsers.

**Why this priority**: This is the core of R4 — it collapses ~70 lines of
copy-pasted structure and makes the schema/identity/error policy single-sourced,
which is the prerequisite for keeping the four parsers in lockstep going forward.

**Independent Test**: With the skeleton extracted, the existing view-parser test
suites (analysis, verify, ship, agent-guidance) pass unchanged, and the
parse/classify/error-arm logic appears exactly once in the source rather than
four times.

**Acceptance Scenarios**:

1. **Given** a valid `analysis.json` / `verify.json` / `ship.json` /
   guidance JSON fixture, **When** its view parser runs, **Then** it returns the
   same parsed view (identical fields, ordering, and diagnostics) it returned
   before the refactor.
2. **Given** the four parsers, **When** the source is inspected, **Then** the
   shared parse-document → classify-schema → identity-validation → error-arm
   skeleton is defined once and referenced by all four, with no copied skeleton
   bodies remaining.

---

### User Story 2 - Crash-proof, warning-clean schema handling (Priority: P1)

A developer building the solution sees **zero FS0025 incomplete-match warnings**,
and no lifecycle view parser can raise a `MatchFailureException` from an
unhandled `version, status` combination. The previously unreachable
`(None, Current)` / `(None, Deprecated)` state now degrades to a defined
malformed-schema diagnostic instead of a runtime exception.

**Why this priority**: Removing a latent runtime throw is a correctness/safety
win, and clearing the 4 FS0025 sites is a prerequisite for the later
`WarningsAsErrors` gate (R5). It is independently valuable even apart from the
deduplication.

**Independent Test**: The four FS0025 sites live in the four parser bodies that
US1 collapses onto the shared skeleton, so zero-FS0025 is reached **jointly with
US1** (once the parsers delegate to the total-match skeleton), not in isolation.
With that done, `dotnet build` reports 0 FS0025 warnings, and a constructed input
that forces the impossible `(version = None, status = current)` state returns a
malformed-schema-version `Error` rather than raising — the crash-proofing and the
totality assertion are independently verifiable even though the warning count is a
joint US1+US2 outcome.

**Acceptance Scenarios**:

1. **Given** the refactored parsers, **When** the solution is rebuilt clean,
   **Then** `dotnet build` emits 0 FS0025 warnings (down from 4).
2. **Given** a schema-version state classified as current/deprecated but with no
   parsed version, **When** any view parser evaluates it, **Then** it returns a
   malformed-schema-version diagnostic `Error` and never raises an exception.
3. **Given** missing/malformed, unsupported, or future schema versions, **When**
   any view parser runs, **Then** it returns the same respective diagnostic
   `Error` it returned before (behavior for the already-handled arms is
   unchanged).

---

### Edge Cases

- **Impossible schema state** `(None, Current)` / `(None, Deprecated)`: handled
  by a new total arm that returns a malformed-schema-version `Error`; never a
  match failure.
- **Missing / malformed `schemaVersion`**: unchanged — malformed-schema-version
  `Error`.
- **Unsupported / future `schemaVersion`**: unchanged — the respective
  unsupported/future diagnostic `Error`.
- **Malformed JSON document** (parse throws): unchanged — caught and surfaced as
  the existing `workModelInconsistent` diagnostic, not propagated.
- **Malformed identity fields** (`workId` / `stage` / `targetId` /
  `behaviorModelDigest`): unchanged — the existing identity-error arm, expressed
  once in the skeleton with the per-artifact identity check supplied by each
  parser.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The four JSON-backed view parsers (`parseAnalysisView`,
  `parseVerificationView`, `parseShipView`, `parseGeneratedAgentGuidance`) MUST
  route through a single shared parsing skeleton instead of each repeating the
  parse → classify → match → identity → array-map → error-arm structure.
- **FR-002**: The shared skeleton MUST be parameterized by each artifact's
  identity validation and record/field builders, with schema-version
  classification and the malformed/unsupported/future error arms expressed in
  exactly one place.
- **FR-003**: The schema-version/status decision MUST be **total** — every
  `version, status` combination, including `(None, Current)` and
  `(None, Deprecated)`, MUST have a defined outcome; no combination may fall
  through to a runtime match failure.
- **FR-004**: An unparsed-version-but-current/deprecated-status state MUST yield
  a malformed-schema-version diagnostic `Error` (the same diagnostic family used
  for a missing/malformed `schemaVersion`), not an exception.
- **FR-005**: A clean `dotnet build` MUST emit **zero FS0025** (incomplete
  pattern match) warnings across `src`.
- **FR-006**: Each of the four parsers MUST preserve its existing behavior for
  every input the test suite exercises — identical successful parse results,
  identical diagnostics, and identical deterministic JSON output in the
  downstream views.
- **FR-007**: The change MUST NOT alter any public `.fsi` contract (the four
  parse entrypoints keep their names and signatures) and MUST NOT introduce new
  logic duplication (the skeleton and its error arms exist once, not copied).
- **FR-008**: The existing test suite (437 tests) MUST pass unchanged; no test
  may be weakened, skipped, or rewritten to accommodate the refactor.
- **FR-009**: The refactor MUST NOT change FS3261 (nullness) warning counts as a
  side effect (those are R5 scope); any FS3261 movement MUST be relocation only,
  not new or removed sites.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet build` emits **0** FS0025 warnings (down from 4).
- **SC-002**: All **437** existing tests pass — equal to the pre-refactor
  baseline — with no test-source changes beyond mechanical call-site updates.
- **SC-003**: The parse-document → classify-schema → identity → error-arm
  skeleton is defined **once**; the four parsers contain no duplicated skeleton
  body, and net source shrinks by roughly the ~70 LOC the duplication occupied.
- **SC-004**: No public `.fsi` signature changes, and the deterministic JSON
  output for every existing view fixture is **byte-identical** before and after
  the refactor.
- **SC-005**: A constructed impossible schema state (`version = None` with a
  current/deprecated status) returns a malformed-schema-version `Error` rather
  than raising — demonstrating the shared match is total.

## Assumptions

- These four parsers are **internal** lifecycle parsers with no external
  consumers (consistent with the R3 finding); call sites are in-repo and may be
  updated mechanically.
- `SchemaVersion.classifyRaw` does not, in practice, return a `Current` or
  `Deprecated` status with a `None` version, so the `(None, Current/Deprecated)`
  arm is unreachable today. It is added defensively to make the function total;
  the chosen outcome for that arm is **"treat as a malformed schema version"**,
  which is the least-surprising error and matches the existing malformed arm.
- Byte-stable JSON output **is** required for this item (unlike R3's relaxed
  gate): per the roadmap, R4 holds the public `.fsi` contract and deterministic
  output byte-stable. The binding gate is build green + the 437-test suite green
  + zero FS0025 + the required SC-005 totality assertion (Principle VI) green.
- The shared skeleton may live in the existing shared helper layer introduced by
  R3 (e.g. the `Internal` / `Core` modules) or a new shared internal module;
  exact placement and the F# mechanism are planning/implementation details, not
  part of this contract.
- No new behavior is introduced for any input the existing suite already
  exercises, so no new fixtures are required and the existing 437-test suite is
  the regression gate for all real inputs. The one genuinely new arm — the
  previously-unreachable `(version = None, status = current/deprecated)` state now
  returning a malformed-schema-version `Error` instead of raising — is a behavior
  change, so per Constitution Principle VI it MUST be covered by a single
  **required** totality assertion (the impossible-state case in SC-005) that fails
  before the change (raises `MatchFailureException`) and passes after (returns the
  diagnostic `Error`). This is the only new test the spec permits; no existing
  test may be weakened, skipped, or rewritten.
