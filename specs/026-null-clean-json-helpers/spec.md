# Feature Specification: Null-Clean JSON Access + Warnings-as-Errors Gate

**Feature Branch**: `026-null-clean-json-helpers`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-074428-refactor-analysis.md" — roadmap item **R5**: *Centralize JSON null-access helpers, then enable `WarningsAsErrors`* (refs §3 of the refactor analysis).

**Change Tier**: Tier 2 (internal change). No public API, schema, generated-view, command, artifact-layout, or agent-skill contract changes. Public `.fsi` signatures and surface baselines remain byte-stable; the only contract-adjacent change is the build configuration (`Directory.Build.props`), which alters how warnings are treated, not what the product produces.

## User Scenarios & Testing *(mandatory)*

The "users" of this change are the project's maintainers, contributors, and CI — the people and systems that build, extend, and trust the SDD codebase. Today the parsing layer emits ~290 unique nullness (FS3261) warnings that the build silently ignores, creating alert-blindness: a genuinely new correctness warning is invisible in the noise. The codebase otherwise prizes totality and determinism, so this is the one place discipline has slipped.

### User Story 1 - Null-clean the JSON access boundary (Priority: P1)

A maintainer reading or extending the lifecycle artifact parsers should see a JSON-access layer where null handling is resolved once, centrally, instead of defensive `if isNull value then ""` idioms scattered across hundreds of call sites. The shared JSON reader helpers (string, required-string, int, string-list, digest, and similar accessors over `System.Text.Json`) absorb the `string | null` boundary so that every call site downstream is null-clean by construction.

**Why this priority**: This is the foundational slice. Until the warnings are actually cleared, the regression gate (Story 2) cannot be enabled without breaking the build. Centralizing null handling at the helper boundary is also the highest-leverage move: the report estimates this clears the bulk of the 144 sites in `LifecycleArtifacts.fs` and the 53 in `WorkModel.fs` with a few dozen helper edits, rather than hundreds of call-site edits.

**Independent Test**: Run a clean Release build of `src` and confirm the unique FS3261 site count drops from ~290 to 0, while all existing tests pass and the deterministic JSON output of representative commands (e.g. charter, analyze, refresh) is byte-identical to the pre-change output. This slice delivers value on its own — a clean, trustworthy warning signal — even if the gate is never enabled.

**Acceptance Scenarios**:

1. **Given** the lifecycle artifact parsers and the work-model builder, **When** a clean Release build runs over `src`, **Then** zero FS3261 (nullness) warnings are emitted.
2. **Given** a JSON document with a missing or null string field, **When** it is read through the centralized accessor helper, **Then** the helper returns the same value (e.g. an empty string or `None`) it returns today, with no behavior change at any call site.
3. **Given** the full existing test suite, **When** it runs after the null-cleanup, **Then** all tests pass and the deterministic JSON artifact output is byte-identical to the pre-change baseline.

---

### User Story 2 - Enable the regression gate (Priority: P2)

Once the parsing layer is null-clean, a contributor who later introduces a new nullness defect should learn about it immediately from a failed build, not discover it months later buried in warning noise. The build configuration treats nullness warnings (FS3261) — and the already-cleared incomplete-match warnings (FS0025) — as errors, so the clean state cannot silently re-accumulate.

**Why this priority**: This is the durable payoff — the ratchet that keeps the codebase clean. It depends on Story 1 (the count must already be 0, or the build breaks immediately), so it ships second.

**Independent Test**: With the count at 0, flip the gate on and confirm a normal build still succeeds; then deliberately introduce a single nullness defect in a source file and confirm the build now fails with FS3261 reported as an error. Revert the defect and confirm the build is green again.

**Acceptance Scenarios**:

1. **Given** an FS3261 count of 0 in `src`, **When** the warnings-as-errors gate is enabled and a normal build runs, **Then** the build succeeds.
2. **Given** the gate is enabled, **When** a contributor introduces a new nullness (FS3261) warning, **Then** the build fails and the warning is reported as an error identifying the file and line.
3. **Given** the gate is enabled, **When** a new incomplete-match (FS0025) warning is introduced, **Then** the build fails for the same reason (the gate keeps the R4-cleared FS0025 sites at 0).

---

### Edge Cases

- **A nullness warning that cannot be cleared at the helper boundary** (e.g. a genuinely call-site-specific null path): it MUST still reach 0, either by a localized null check or an explicit, documented suppression at that single site — not by leaving the warning emitted. If any site is genuinely intractable, it MUST be recorded so the gate can be scoped to still fail on *new* sites.
- **Test-project warning sites** (~9 FS3261 sites reported in tests): the cleanup and gate scope must explicitly decide whether test projects are included, so the gate does not break the test build unexpectedly.
- **Unrelated warning categories**: enabling the gate must not silently promote dozens of unrelated, pre-existing warning categories to errors and balloon the change — the gate is scoped to the warnings this feature addresses.
- **Determinism**: any change to a null-handling helper must preserve byte-identical artifact output and digest stability; a helper that substitutes a different default for null would silently change output and MUST be caught by the byte-identical check.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shared JSON-access helpers over `System.Text.Json` (string, required-string, int, string-list, digest, and similar readers) MUST centralize `string | null` handling at the helper boundary, so downstream call sites do not each repeat defensive null guards.
- **FR-002**: All FS3261 (nullness) warning sites in `src` MUST be eliminated — the unique-site count MUST reach 0 — covering at minimum the parsing layer (`LifecycleArtifacts/*`) and the work-model builder (`WorkModel.fs`), which together hold the large majority of sites, and the remaining sites in the other `src` projects.
- **FR-003**: The cleanup MUST be behavior-preserving: all existing tests MUST continue to pass, and the deterministic JSON output of every command MUST remain byte-identical to the pre-change baseline (no changed null-default values, ordering, or digests).
- **FR-004**: The build MUST treat newly introduced nullness warnings (FS3261) as errors so that a new nullness defect fails the build instead of accruing silently.
- **FR-005**: The build MUST keep incomplete-match warnings (FS0025) — cleared to 0 by roadmap item R4 — enforced as errors under the same gate.
- **FR-006**: The gate MUST be scoped to the warning categories this feature addresses (FS3261 and FS0025) and MUST NOT silently promote unrelated, pre-existing warning categories to errors. (If a future decision adopts a global treat-all-warnings-as-errors posture, that is a separate change.)
- **FR-007**: The scope of the cleanup and the gate with respect to test projects MUST be stated explicitly: either test projects are also brought to 0 FS3261 and covered by the gate, or they are explicitly excluded with a recorded reason.
- **FR-008**: Public `.fsi` signatures and surface-area baselines MUST remain unchanged (Tier 2 — no public-surface change).
- **FR-009**: Any nullness site that cannot be resolved through a helper or a localized null check MUST be handled by an explicit, documented per-site suppression rather than by leaving the warning emitted; such sites (if any) MUST be enumerated so the gate still fails on genuinely new warnings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A clean Release build of `src` emits **0** FS3261 (nullness) warnings, down from ~290 unique sites.
- **SC-002**: A clean Release build emits **0** FS0025 (incomplete-match) warnings (the R4-cleared state is maintained).
- **SC-003**: With the gate enabled, a deliberately introduced single nullness defect causes the build to **fail** with FS3261 reported as an error; reverting it returns the build to green. (The gate is demonstrably effective, not merely configured.)
- **SC-004**: The full existing test suite passes after the change, and the deterministic `--json` output of representative commands (at minimum charter, analyze, refresh) is **byte-identical** to the pre-change baseline.
- **SC-005**: The number of call-site `if isNull …` defensive idioms in the parsing layer is reduced — null handling is observable only at the centralized helper boundary, not duplicated across call sites.
- **SC-006**: Enabling the gate adds **0** newly-promoted unrelated warning categories to the error set (the build does not start failing on warnings outside FS3261/FS0025).

## Assumptions

- **Gate scoping**: The warnings-as-errors gate is scoped to `WarningsAsErrors=FS3261;FS0025` rather than a global `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Rationale: the report offers both, and the scoped form matches the codebase's incremental discipline — it ratchets the two categories this feature actually clears without risking a build break from unrelated, un-audited warning categories. Adopting a global posture later is a separate, explicit decision.
- **Test projects included**: The ~9 FS3261 sites in test projects are cleaned and covered by the gate alongside `src`, so the discipline is uniform and a nullness defect in a test is caught too. (If a test-only site proves intractable, FR-009's documented-suppression path applies.)
- **Centralization is sufficient for the bulk**: Wrapping the JSON-access helpers clears the large majority of sites (the ~144 in the parsers and ~53 in the work-model builder cluster at the `System.Text.Json` `JsonElement`/`GetString()` boundary), with only a small remainder needing localized handling.
- **No public surface motion**: This is purely internal plus a build-config change; no `.fsi`, schema, generated view, or command contract changes, so surface baselines stay byte-stable.
- **Baseline numbers** are taken from the 2026-06-26 refactor analysis (build `0.2.0`, .NET SDK 10.0.301). The exact unique-site count is re-measured at implementation time against the then-current `main`; the target (0) is unaffected by minor baseline drift.
- **Sequencing within the feature**: Story 1 (null-cleanup) lands before Story 2 (gate) because enabling the gate while any FS3261 site remains would break the build.
