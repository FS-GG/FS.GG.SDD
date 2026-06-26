# Feature Specification: Split `LifecycleArtifacts.fs` per artifact family

**Feature Branch**: `022-split-lifecycle-artifacts`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in @docs/reports/2026-06-26-074428-refactor-analysis.md"

> Implements roadmap item **R3** from
> `docs/reports/2026-06-26-074428-refactor-analysis.md`: split the
> 3,161-line single-module `LifecycleArtifacts.fs` (and its 722-line
> `.fsi`) into per-artifact-family source files, with **zero** change to
> the public contract, the deterministic JSON output, or runtime behavior.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Maintainer navigates to one artifact family without scanning a 3,000-line file (Priority: P1)

A maintainer (human or agent) needs to change how the **plan** artifact is
parsed. Today every artifact family — project/sdd/agents config, spec,
clarification, checklist, plan, task, analysis, evidence, verify, ship,
guidance — has its type definitions and parsers interleaved in one flat
`module LifecycleArtifacts` spanning 3,161 lines, so the maintainer must
scroll the whole file to find the ~280 lines that concern the plan and to
be sure no plan logic lives elsewhere.

After this change, the maintainer opens the plan family's own source file,
sees that family's types and parsers co-located, and can change it in
isolation.

**Why this priority**: This is the entire point of the refactor — the
report flags `LifecycleArtifacts.fs` as a "god module" (Severity: High)
whose flat structure is the primary maintainability cost. Delivering just
the file split delivers the value.

**Independent Test**: Pick one artifact family (e.g. plan), confirm its
types and parsers live together in a single file under a
`LifecycleArtifacts/` folder, that no other file in that folder references
plan-internal helpers, and that the full test suite stays green.

**Acceptance Scenarios**:

1. **Given** the refactored tree, **When** a maintainer lists the source
   files for lifecycle artifacts, **Then** each artifact family is in its
   own file and no single file exceeds a navigable size (target: ≤ ~700
   lines, well under the original 3,161).
2. **Given** the refactored tree, **When** the project is built, **Then**
   it compiles with the source files in a deterministic, repo-declared
   order and produces the same assembly surface as before.

---

### User Story 2 - Downstream consumers and the public contract are unaffected (Priority: P1)

The Commands layer (`FS.GG.SDD.Commands`) and everything downstream consume
lifecycle parsing exclusively through the published `LifecycleArtifacts`
signature. A consumer must observe **no** difference: the same qualified
names, the same types, the same parse results, the same diagnostics, and
byte-identical generated JSON artifacts.

**Why this priority**: The refactor is only safe if it is invisible. The
report rates R3 risk as "Very low" precisely because the 722-line `.fsi`
fully pins the contract; that guarantee must be preserved, not merely
intended.

**Independent Test**: Diff the effective public signature and a corpus of
generated artifacts before and after the change; both must be identical.

**Acceptance Scenarios**:

1. **Given** a caller referencing `LifecycleArtifacts.<member>`, **When**
   the refactor lands, **Then** every previously accessible qualified name
   remains accessible with the same type signature and no caller source
   changes.
2. **Given** the same input artifacts, **When** any lifecycle command runs
   before and after the change, **Then** the emitted JSON
   (`work-model.json`, `analysis.json`, `verify.json`, `ship.json`,
   readiness summaries) is byte-for-byte identical.
3. **Given** the existing test suite, **When** it runs against the
   refactored tree, **Then** all tests pass with no test source changes.

---

### User Story 3 - Nullness and incomplete-match hotspots are localized for follow-on work (Priority: P3)

The report notes that 144 of the 290 unique FS3261 nullness sites and all 4
FS0025 incomplete-match sites live inside this module's JSON view parsers.
Splitting per family confines those warnings to the Analysis/Verify/Ship
parser file, so the later roadmap items (R4 `parseJsonView`, R5 null-clean
helpers) have a small, well-bounded target.

**Why this priority**: This is a setup benefit, not the deliverable — R3
neither fixes warnings nor changes warning counts. It only relocates them.
Valuable for sequencing but not required for R3 to be "done."

**Independent Test**: After the split, confirm the warning sites reported
for the view parsers all originate from a single, identifiable family file
rather than being spread across the codebase.

**Acceptance Scenarios**:

1. **Given** a clean rebuild after the split, **When** FS3261/FS0025
   warning locations are listed, **Then** the view-parser warnings resolve
   to the Analysis/Verify/Ship family file and the total unique-site counts
   are unchanged from the pre-refactor baseline (290 FS3261, 4 FS0025).

---

### Edge Cases

- **Module-name preservation in F#**: F# does not allow one named module to
  span multiple files. The split MUST still present consumers with the
  existing qualified access (e.g. `LifecycleArtifacts.<member>`). The
  feature MUST resolve this without altering any consumer call site or the
  published signature — whether by a re-exporting facade or another
  mechanism is an implementation choice, but the observable contract is
  fixed.
- **Compile ordering / forward references**: F# compiles files in declared
  order; a family that depends on shared types or on another family's types
  MUST be ordered after its dependencies. Any genuinely shared definitions
  (e.g. schema-version classification, common JSON helpers) MUST land in a
  shared file ordered ahead of the families that use them, with no
  duplication introduced.
- **Cyclic family dependencies**: if two families reference each other's
  types, the split MUST break the cycle (e.g. by hoisting the shared types)
  rather than collapsing the families back together.
- **Internal-but-cross-family helpers**: a private helper currently used by
  more than one family MUST be relocated to a shared file, not copied —
  duplication is explicitly out of scope for this refactor.
- **Determinism**: file reordering MUST NOT change any digest, sort order,
  or emitted-field order in produced artifacts.

## Requirements *(mandatory)*

### Functional Requirements

> **Planning amendments (authoritative).** `plan.md` §Summary records
> stakeholder decisions that **supersede** parts of FR-002/FR-003/FR-004/FR-005
> below (and SC-002/SC-003/SC-004): there are **no external consumers**, so the
> literal `LifecycleArtifacts.<member>` qualified name and exact `.fsi` shape are
> **not** preserved, in-repo consumers and tests **may be edited mechanically**
> (`open`/qualifier lines plus surface-baseline regeneration), and byte-identical
> output is **not** separately required. The single binding behavioral gate is:
> **build green + the existing test suite passes**. The affected items are tagged
> *(amended)* and restated in that light.

- **FR-001**: The lifecycle-artifact type definitions and parsers currently
  in `LifecycleArtifacts.fs` MUST be reorganized so that each artifact
  family (infra config: project/sdd/agents; specification; clarification;
  checklist; plan; task; analysis; evidence; verify; ship; guidance) has
  its types and parsers co-located in a dedicated source file under a
  `LifecycleArtifacts/` folder (or equivalent declared grouping).
- **FR-002** *(amended)*: The **aggregate** public surface of
  `FS.GG.SDD.Artifacts` (the set of public types, record fields, DU cases, and
  `val` signatures) MUST be preserved member-for-member — the old 722-line
  `.fsi` equals the union of the new per-family `.fsi` files. The **qualifying
  module name is not preserved**: `LifecycleArtifacts.<member>` becomes
  `<member>` via `open FS.GG.SDD.Artifacts`. Consumer-visible *signatures* are
  unchanged; the *namespace path* changes.
- **FR-003** *(amended)*: No source file outside this feature may require a
  **logic** change. In-repo consumers and tests (callers in Artifacts, Commands,
  and the test projects) **may receive mechanical edits only** — `open`/qualifier
  updates and surface-baseline regeneration — to follow the renamed modules.
- **FR-004** *(amended)*: The full existing test suite (437 tests as of the
  baseline) MUST pass after the refactor with **no test-logic edits**; mechanical
  `open`/qualifier updates and regeneration of the public-surface baseline
  snapshot (see FR-002) are expected and permitted.
- **FR-005** *(amended)*: Byte-for-byte identical artifact output is **no longer
  separately required** — the binding gate is "tests pass" (the suite asserts the
  behavior that matters). The implementation MUST still move record/DU and parser
  bodies **verbatim** (no field/case reorder) so determinism is preserved in
  practice; this is the cheapest way to keep tests green and honor FR-008.
- **FR-006**: Definitions shared across families (schema-version
  classification, common JSON-access helpers, shared types) MUST be placed
  in a single shared location ordered ahead of dependents; the refactor
  MUST NOT introduce any new duplication of logic.
- **FR-007**: The repository's declared compile order MUST be updated so the
  new files compile deterministically and forward references resolve, with
  the build succeeding with no new errors.
- **FR-008**: The refactor MUST NOT change the count or location-category of
  existing compiler warnings except by relocating a warning to the file
  that now owns the offending code; specifically it MUST NOT fix, suppress,
  or add FS3261/FS0025 warnings (those are R4/R5 scope).
- **FR-009**: No single resulting family file may approach the original
  module's size; the largest resulting file MUST be substantially smaller
  than 3,161 lines (target ≤ ~700 lines) to deliver the navigability goal.
- **FR-010**: Upon completion, the R3 row and status detail in
  `docs/reports/2026-06-26-074428-refactor-analysis.md` MUST be updated from
  🔴 to complete with a link to this feature's evidence/readiness.

### Key Entities *(include if feature involves data)*

- **Artifact family**: a cohesive group of lifecycle-artifact type
  definitions plus the parser(s) that read/produce them (e.g. "plan",
  "ship"). The unit of the split.
- **Shared lifecycle-artifacts core**: definitions used by more than one
  family (schema-version classification, JSON-access helpers, common
  types). Must compile before the families that depend on it.
- **Public lifecycle-artifacts signature**: the `.fsi` contract that pins
  the consumer-visible surface; the invariant this refactor must preserve.
- **Compile order manifest**: the repo's declared ordering of source files
  that guarantees deterministic, forward-reference-safe compilation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The largest lifecycle-artifacts source file after the split is
  ≤ ~700 lines (down from 3,161), and each of the ~11 artifact families is
  locatable in a single dedicated file.
- **SC-002** *(amended)*: The **aggregate** public surface is unchanged
  member-for-member (per FR-002); the public-surface baseline snapshot is
  regenerated to reflect the new module qualifiers, and the only consumer/test
  edits are mechanical (`open`/qualifier + baseline regen). The module-name
  reshape is expected, not a zero-diff guarantee.
- **SC-003** *(amended)*: 100% of the existing test suite passes after the
  change with no test-**logic** edits (mechanical `open`/qualifier updates and
  baseline regeneration excepted).
- **SC-004** *(amended — superseded)*: Byte-identical output is no longer
  separately verified (see FR-005); the test suite is the authoritative
  behavioral check.
- **SC-005**: The **unique-site** compiler-warning counts are unchanged from the
  baseline (290 FS3261, 4 FS0025), confirming the refactor relocated rather than
  altered them. Note: raw per-project warning-line counts differ from the
  unique-site figures and are **not** the comparison basis (see research.md
  Decision 5); the verification compares unique sites, and additionally confirms
  the view-parser sites now resolve to `Analysis.fs`/`Verify.fs`/`Ship.fs`.
- **SC-006**: A maintainer can identify the file owning any one artifact
  family in a single directory listing, without opening the file.

## Assumptions

- "Next item" resolves to **R3** because the report's suggested sequence is
  **R3 → R4 → R1 → R2 → R5 → R6 → R7** and, as of 2026-06-26, every roadmap
  row is 🔴 not started; R3 is the first unstarted item in that sequence.
- The baseline figures (3,161 `.fs` lines, 722 `.fsi` lines, 437 tests, 290
  FS3261, 4 FS0025) are taken from the report measured against `main` at
  build `0.2.0` / .NET SDK 10.0.301 and are treated as the regression
  reference.
- This is a pure internal reorganization: no new lifecycle behavior,
  artifact field, schema version, or CLI surface is added or removed.
- The `~700`-line target is a navigability guideline, not a hard gate; the
  binding requirements are the family-per-file split (FR-001) and a largest
  file substantially below 3,161 lines (FR-009).
- F#'s one-module-per-file constraint is handled by an
  implementation-chosen mechanism (e.g. a re-exporting facade or per-family
  modules surfaced through the existing name) that preserves the public
  contract; the spec fixes the observable contract, not the mechanism.
- Warning fixes, the `parseJsonView` extraction (R4), and
  `WarningsAsErrors` (R5) are explicitly **out of scope** and tracked as
  their own roadmap items.
