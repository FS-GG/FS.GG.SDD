# Feature Specification: Collapse Diagnostic Builder + Unify JSON Serializers

**Feature Branch**: `027-unify-diagnostic-serializers`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-074428-refactor-analysis.md" — roadmap item **R6**: *Collapse diagnostic builder + unify serializers* (refs §5.2 and §5.4 of the refactor analysis). This is the first remaining 🔴 row after R1–R5 landed.

**Change Tier**: Tier 2 (internal change). No public API, schema, generated-view, command, artifact-layout, or agent-skill contract changes. The existing public `.fsi` signatures (`CommandReports`, `CommandSerialization`, `Serialization`) and surface-area baselines remain byte-stable, and the deterministic `--json` output of every command and the serialized work-model JSON remain byte-identical. The only motion is the internal shape of the diagnostic constructors and the JSON writer helpers, neither of which is exposed today.

## User Scenarios & Testing *(mandatory)*

The "users" of this change are the project's maintainers and contributors — the people who add a new diagnostic, add a field to a serialized report, or read the serialization layer to understand the JSON contract. Today two structural duplications tax that work:

- **§5.2 — diagnostic constructors.** `CommandReports.fs` (~1,477 lines) holds ~113 named diagnostic functions. They already route through one shared `commandDiagnostic id severity path message correction relatedIds` helper, but every call site still re-spells the severity literal by hand (99 `DiagnosticError`, 14 `DiagnosticWarning`), and the structurally-identical families (`missing*`, `malformed*`, `duplicate*`, `unknown*`, `stale*`, `unsafe*`/`failed*`) each hand-repeat the same path/message/correction shape. There is no single place that guarantees "every command diagnostic is built one way."

- **§5.4 — serializer overlap.** `CommandSerialization.fs` (Commands, 455 lines) and `Serialization.fs` (Artifacts, 357 lines) independently implement overlapping low-level JSON writers: `writeDiagnostic` (a near-100% duplicate full implementation in both), `writeOutputDigest` (duplicate, differing only by `option` vs non-`option`), plus `writeStringList` / `writeDigest`(`writeSourceDigest`) / `writeLocation`(`writeSourceLocation`) variants that differ only by sort behavior or an extra name parameter. A maintainer fixing or extending one writer must remember the twin exists in the other assembly.

Neither duplication is a defect — the 438-test suite is green and output is byte-stable — but both are maintenance hazards: a diagnostic added without the shared shape, or a serializer fix applied to only one of the twins, drifts silently.

### User Story 1 - One way to build a command diagnostic (Priority: P1)

A maintainer adding or editing a command diagnostic should express only what varies — the stable id, the human message, the correction, the related ids, and (only when it differs from the common case) the severity — and get the path resolution, severity convention, and sort/ordering convention for free from a single builder. The structurally-identical families no longer each restate the full constructor call.

**Why this priority**: This is the higher-leverage, lower-risk half and is self-contained within one file (`CommandReports.fs`). It delivers the "one shape" guarantee — the property that makes future diagnostics correct by construction — independently of the serializer work.

**Independent Test**: Confirm that every command diagnostic is produced through the single shared builder (no constructor bypasses it), that the 99+14 hand-spelled severity literals collapse to the builder's convention, and that the emitted diagnostics — ids, severities, paths, messages, corrections, related ids, and sort order — are byte-identical to today in the deterministic `--json` output across representative commands. The named functions and their `.fsi` signatures are unchanged, so all existing call sites compile untouched.

**Acceptance Scenarios**:

1. **Given** the set of command diagnostic constructors, **When** the codebase is searched for diagnostic construction, **Then** every command diagnostic is built through the one shared builder and no constructor re-implements severity/path/sort handling inline.
2. **Given** a command run that emits diagnostics (e.g. a missing-prerequisite or duplicate-id path), **When** its `--json` report is produced, **Then** the `diagnostics` array is byte-identical to the pre-change baseline (same ids, severities, paths, messages, corrections, related ids, and order).
3. **Given** the public `CommandReports.fsi`, **When** it is compared to the pre-change baseline, **Then** it is byte-identical — every named diagnostic function retains its exact signature.

---

### User Story 2 - One shared set of JSON writer primitives (Priority: P2)

A maintainer fixing or extending a low-level JSON writer (string list, digest, location, diagnostic) should edit it in exactly one place and have both the work-model serializer (Artifacts) and the command-report serializer (Commands) pick up the change, with each caller's existing sort behavior and field-shape preserved by parameter rather than by a forked copy.

**Why this priority**: This removes the genuine cross-assembly drift hazard, but it is the more delicate half — the two serializers differ in sort behavior (the Commands writers sort string lists; the Artifacts writers do not) and in `option` vs non-`option` digest handling, so the unification must be parameterized carefully to keep output byte-identical. It ships second so it can be verified against the Story-1-stable baseline.

**Independent Test**: After the shared writer primitives are in place, confirm the serialized work-model JSON and every command `--json` report are byte-identical to the pre-change baseline (including string-list ordering and digest null-handling), and that the duplicated writer bodies (`writeDiagnostic`, `writeOutputDigest`, and the string-list/digest/location variants) now exist once and are consumed by both serializers.

**Acceptance Scenarios**:

1. **Given** the two serialization modules, **When** the codebase is searched for the duplicated writer bodies, **Then** each previously-duplicated writer exists in exactly one shared location and both serializers consume it.
2. **Given** a serializer whose string lists are emitted sorted today (Commands) and one whose lists are emitted in source order today (Artifacts), **When** both route through the shared string-list writer, **Then** each retains its current ordering (the sort behavior is a parameter, not a hard-coded default) and both outputs stay byte-identical.
3. **Given** the serialized work-model JSON and representative command `--json` reports, **When** they are produced after unification, **Then** every byte matches the pre-change baseline, including digest objects, null fields, and locations.
4. **Given** the existing public `.fsi` files of all three modules, **When** compared to baseline, **Then** the public entry points (`serializeReport`, `serializeWorkModel`, and the `Serialization` exports) are unchanged.

---

### Edge Cases

- **Sort-behavior divergence**: the Commands `writeStringList` sorts; the Artifacts one does not. A naive merge that picks one default would silently reorder one serializer's arrays and change output. The shared writer MUST make ordering an explicit parameter so each caller keeps its current behavior; the byte-identical check MUST catch any regression.
- **`option` vs non-`option` digest writers**: `writeOutputDigest`/`writeDigest` exist in both option-wrapped (Commands) and bare (Artifacts) forms. The unified primitive must serve both without changing how `None`/absent digests render today.
- **Severity is not uniform**: 14 of the command diagnostics are warnings, not errors. Collapsing the severity default MUST NOT silently promote a warning to an error or vice-versa; every diagnostic's severity must match today's.
- **Contract ids are load-bearing**: diagnostic ids and messages are part of the JSON contract consumed downstream. Collapsing the builder MUST keep every id/message/correction string exactly as emitted today — the collapse changes *how* a diagnostic is built, never *what* it says.
- **Cross-assembly visibility**: the Artifacts → Commands layering is one-way. If shared writer primitives live in Artifacts and are consumed by Commands, the mechanism for cross-assembly visibility must not add new public surface to the existing serializer `.fsi` entry points (it must not regress the byte-stable-`.fsi` constraint). The layering direction Artifacts → Commands MUST be preserved (no new dependency from Artifacts back onto Commands).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Every command diagnostic in `CommandReports` MUST be constructed through a single shared builder, so the severity convention, path resolution, and sort/ordering convention live in exactly one place.
- **FR-002**: The redundant per-call severity literals MUST be collapsed so that the common case (error severity) is supplied by the builder rather than re-spelled at each of the ~99 call sites, while the ~14 warning-severity diagnostics retain their warning severity exactly.
- **FR-003**: The structurally-identical diagnostic families (`missing*`, `malformed*`, `duplicate*`, `unknown*`, `stale*`, `unsafe*`/`failed*`) MUST share their common path/message/correction shape through the builder rather than each hand-repeating it, without merging or renaming the named functions that form the public surface.
- **FR-004**: The duplicated low-level JSON writers across `CommandSerialization` (Commands) and `Serialization` (Artifacts) — at minimum `writeDiagnostic` and `writeOutputDigest`, plus the `writeStringList` / digest / location variants — MUST be unified so each previously-duplicated writer body exists in exactly one shared location consumed by both serializers.
- **FR-005**: The unified writers MUST parameterize the points where the two serializers legitimately differ today — string-list ordering (sorted vs source-order) and `option` vs bare digest handling — so each caller's current behavior is preserved by parameter, not by a forked implementation.
- **FR-006**: The change MUST be behavior-preserving: all existing tests MUST continue to pass, the deterministic `--json` output of every command MUST remain byte-identical to the pre-change baseline, and the serialized work-model JSON MUST remain byte-identical (no changed ordering, null-handling, digests, or diagnostic strings).
- **FR-007**: The public `.fsi` signatures and surface-area baselines of `CommandReports`, `CommandSerialization`, and `Serialization` MUST remain byte-stable; in particular every named diagnostic function keeps its exact signature, and the serializer entry points (`serializeReport`, `serializeWorkModel`) are unchanged (Tier 2 — no public-surface change).
- **FR-008**: The cross-assembly mechanism for sharing writer primitives MUST preserve the one-way Artifacts → Commands layering and MUST NOT introduce a new public export on the existing serializer entry-point `.fsi` files. (If a new dedicated low-level shared writer module is introduced, it is internal-style plumbing, not a new public serializer contract; the plan records the chosen mechanism.)
- **FR-009**: No diagnostic id, message, correction, or related-id string emitted in any report may change as a result of the collapse — the diagnostic contract is held fixed.

### Key Entities

- **Command diagnostic builder**: the single function through which every `CommandReports` diagnostic is constructed; owns the severity default, path-to-artifact resolution, and ordering convention. The ~113 named diagnostic functions remain as thin, contract-stable call sites over it.
- **Shared JSON writer primitives**: the deduplicated low-level writers (string list, source/output digest, location, diagnostic) used by both the work-model serializer and the command-report serializer, with ordering and digest-shape as parameters.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of command diagnostics are constructed through the one shared builder — zero diagnostics bypass it with inline severity/path/sort handling.
- **SC-002**: The previously-duplicated JSON writer bodies (`writeDiagnostic`, `writeOutputDigest`, and the string-list/digest/location variants) exist in exactly one shared location each — the cross-assembly duplicate count for these writers drops to **0**.
- **SC-003**: The full existing test suite (438 tests at baseline) passes after the change.
- **SC-004**: The deterministic `--json` output of representative commands (at minimum charter, analyze, refresh, and a diagnostic-emitting failure path — pinned to the `duplicate-work-id` fixture, a duplicate-id diagnostic with non-empty `relatedIds`) and the serialized work-model JSON are **byte-identical** to the pre-change baseline.
- **SC-005**: The public `.fsi` files of `CommandReports`, `CommandSerialization`, and `Serialization`, and the surface-area baselines, are **byte-identical** to the pre-change baseline.
- **SC-006**: Net `src` line count is reduced (the analysis estimates ≈90 LOC across the two halves — ≈50 from the diagnostic collapse and ≈40 from the serializer unification); the change removes duplication without adding equivalent new scaffolding.
- **SC-007**: The Release build remains green with no new warning category introduced (the R5 FS3261/FS0025 gate continues to pass).

## Assumptions

- **Named functions are the contract; the builder is the mechanism.** The ~113 named diagnostic functions (e.g. `missingProjectConfig`, `duplicateWorkId`) are kept as-is because their ids/messages are the downstream contract and their `.fsi` signatures are part of the public surface. R6 collapses only *how* they are built, not their names or signatures. (Refactor analysis §5.2 endorses "keep the named functions as thin call-sites … route them through one generic builder.")
- **`commandDiagnostic` already exists** as the shared helper and is public in `CommandReports.fsi`; R6 completes the collapse on top of it (severity default + family shape sharing) rather than introducing the first shared builder. Its public signature is held stable, so any new convenience builders are additive-but-internal or layered beneath it without changing existing call-site signatures.
- **Serializer writers are already internal.** The individual writers (`writeStringList`, `writeDiagnostic`, `writeOutputDigest`, `writeLocation`, etc.) are not exposed in any `.fsi` today — only the top-level `serializeReport`/`serializeWorkModel` entry points are. Unifying them therefore does not, by itself, alter the existing public serializer surface; the plan chooses where the shared primitives live (a new internal-style shared writer module in Artifacts is the expected home, consistent with the one-way Artifacts → Commands layering).
- **Byte-identical output is the binding gate**, alongside the green test suite and byte-stable public `.fsi`/surface baseline — matching the contract guarantees stated for R6 in the refactor roadmap ("R1/R2/R4–R7 additionally hold the public `.fsi` contract and deterministic JSON output byte-stable"). This is stricter than R3's relaxed gate and is intentional: there is no schema or member reshape here, only an internal dedup.
- **Baseline numbers** (≈113 diagnostic functions, 99/14 severity split, two 455/357-line serializer modules, ≈90 LOC reduction, 438 tests) are taken from the 2026-06-26 refactor analysis and the current `main`; they are re-measured at implementation time, and the targets (one builder, zero duplicate writers, byte-identical output) are unaffected by minor drift.
- **Sequencing within the feature**: Story 1 (diagnostic collapse, single file, low risk) lands before Story 2 (serializer unification, cross-assembly, more delicate) so the serializer work is verified against an already-stable baseline.
- **Out of scope**: the remaining R7 cleanups (redundant `private` modifiers, `failwith` context) and the scattered §5.5 micro-duplication are explicitly deferred to their own roadmap rows and are not addressed here.
