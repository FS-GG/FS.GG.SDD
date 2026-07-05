# Feature Specification: Bind SDD authoring skills to the CLI gate grammar

**Feature Branch**: `081-skill-gate-binding`

**Created**: 2026-07-05

**Status**: Draft

**Input**: Root-cause fix for epic FS-GG/FS.GG.SDD#140 (skill↔gate drift), covering child issues #141, #142, #143, #144. Source: FS.GG framework development-feedback report — *Hollow Depths* build (`001-hollow-depths`), 2026-07-05, where an agent drove the framework charter→ship and found that following the `fs-gg-sdd-*` skills verbatim produced artifacts the CLI gate rejected.

## Clarifications

### Session 2026-07-05

- Q: The `malformedChecklistFrontMatter` diagnostic fires for ~5 distinct causes (genuine front-matter/schema-version problems *and* the missing `[CHK:CHK-###]` back-ref case #144). How should the rename be scoped? → A: Split out **only** the back-ref case into a new diagnostic (e.g. `missingChecklistBackReference`); leave the genuine front-matter/schema-version causes as `malformedChecklistFrontMatter`. No deprecated alias (the diagnostic is SDD-internal, not a consumed cross-repo contract).
- Q: How should the stage-skill required-field lists be bound to the CLI's typed contract (FR-009)? → A: **Check-against** — a CI test asserts each skill's stated required fields match the typed contract and fails the build on drift; skills stay hand-authored (no codegen/generated regions in `SKILL.md`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Following a stage skill verbatim produces a gate-passing artifact (Priority: P1)

An author (human or agent) driving the SDD lifecycle reads a stage skill (e.g. `fs-gg-sdd-specify`), copies its worked example, adapts it to their work item, and runs the corresponding `fsgg-sdd` gate command. The artifact passes the gate on the first attempt — the author never has to decompile the CLI or grep the compiled command handlers to discover a required field or an accepted grammar form.

**Why this priority**: This is the whole point of the epic. The authoring surface (`fs-gg-sdd-*` skills) is what an author *writes from*; the gate grammar in the CLI is what *accepts or rejects*. When they drift, the framework silently breaks its own `.fsgg/early-stage-guidance.md` promise ("don't make you decompile the CLI"). An author following the documented path deterministically fails. Every other story is a facet of this one.

**Independent Test**: Take each stage skill's shipped worked example, feed it to the real gate command for that stage, and confirm a pass (no blocking diagnostics). Fully testable by exercising the shipped example corpus against the shipped CLI.

**Acceptance Scenarios**:

1. **Given** the `fs-gg-sdd-specify` skill's worked `spec.md` example, **When** it is run through the `checklist` coverage gate, **Then** every FR registers as covered (zero "counted but not covered" blocking findings).
2. **Given** the `fs-gg-sdd-evidence` skill's worked `evidence.yml` example containing a deferral, **When** it is run through the `evidence` gate, **Then** the gate accepts it (no `missingDeferralRationale` block).
3. **Given** the `fs-gg-sdd-clarify` skill's worked `clarifications.md` example, **When** it is run through the `clarify` gate, **Then** the front matter validates (no `malformedClarificationFrontMatter` block).
4. **Given** any stage skill whose worked example is modified to violate the gate grammar, **When** the skill↔gate check runs, **Then** the check fails and names the offending skill and diagnostic.

---

### User Story 2 - The build fails when a skill example would not pass its own gate (Priority: P1)

A maintainer edits a stage skill's worked example, or the CLI gate grammar changes, in a way that reintroduces drift. Continuous integration extracts every stage skill's fenced example, runs each through the actual gate it documents, and fails the build when any example would be rejected — so drift cannot land on `main` and reach authors.

**Why this priority**: Fixing the four reported instances is worthless if the class recurs the next time a skill or a gate is edited. This doctest is the durable barrier that keeps the two surfaces bound. Without it, the fixes are a snapshot that rots.

**Independent Test**: Introduce a deliberate drift (revert one skill example to a known-bad form) on a branch and confirm CI goes red with a message naming the skill and the failing gate; revert and confirm green.

**Acceptance Scenarios**:

1. **Given** all stage skills with correct examples, **When** the skill↔gate doctest runs in CI, **Then** it passes and reports each skill example exercised against its gate.
2. **Given** a stage skill whose worked example is edited to a form the gate rejects, **When** the doctest runs, **Then** the build fails with a message identifying the skill file and the blocking diagnostic.
3. **Given** a new stage skill added without a runnable example, **When** the doctest runs, **Then** the build fails, requiring the example (no silently unexercised skill).

---

### User Story 3 - Required-field lists in skills stay true to the typed contract (Priority: P2)

An author reads a skill's statement of an artifact's required fields (e.g. the evidence deferral fields, or a stage's required front-matter keys). A CI test checks those hand-authored lists against the CLI's typed contract, so a field the gate requires can never be missing from the skill, and a field the skill names can never be one the gate does not know.

**Why this priority**: The reported failures (undocumented deferral fields; a missing `sourceSpec` front-matter key) are hand-copied lists that fell behind the typed contract. Binding the *lists* (not just the *examples*) closes the second half of the drift surface. Slightly lower than the doctest because a correct, exercised example already covers most fields transitively; this catches fields an example happens not to use.

**Independent Test**: Add a required field to the typed contract without updating the corresponding skill and confirm the check fails naming the missing field; remove it and confirm green.

**Acceptance Scenarios**:

1. **Given** the evidence deferral contract requires `rationale`, `owner`, `scope`, and `laterLifecycleVisibility`, **When** the field-list check runs, **Then** the `fs-gg-sdd-evidence` skill is confirmed to name all four (build fails if any is absent).
2. **Given** the `clarify` stage front-matter contract requires `sourceSpec` (and its siblings), **When** the check runs, **Then** the `fs-gg-sdd-clarify` skill is confirmed to state that requirement.
3. **Given** a new required field is added to a stage's typed contract, **When** the check runs before the matching skill is updated, **Then** the build fails naming the field and the skill that must document it.

---

### User Story 4 - A blocking diagnostic names its real cause (Priority: P2)

An author whose `checklist` artifact is rejected reads the diagnostic and it names the actual problem — a missing `[CHK:CHK-###]` back-reference on a review line — rather than misdirecting to "front matter", which is not the cause. The author fixes the right thing on the first read.

**Why this priority**: `malformedChecklistFrontMatter` firing for a missing back-ref sent the reporter debugging the wrong part of the file. A misnamed diagnostic costs author time and erodes trust in every other diagnostic. It is user-facing accuracy, not a silent gate failure, so P2.

**Independent Test**: Author a checklist review line missing its `[CHK:CHK-###]` back-ref, run the gate, and confirm the emitted diagnostic names the missing back-reference (not front matter).

**Acceptance Scenarios**:

1. **Given** a `CR-###` review line with no `[CHK:CHK-###]` back-reference, **When** the `checklist` gate runs, **Then** the blocking diagnostic identifies the missing back-reference as the cause.
2. **Given** an actually malformed front-matter block, **When** the gate runs, **Then** the front-matter diagnostic still fires for that genuine case (the rename does not lose the real front-matter signal).
3. **Given** the new `missingChecklistBackReference` diagnostic, **When** an author consults `fs-gg-sdd-troubleshooting` or the remediation pointer, **Then** the guidance matches the new name and cause.

### Edge Cases

- A stage skill ships **more than one** worked example, or an example that is intentionally a *negative* (a "don't do this" counter-example): the doctest must exercise the positive examples against the gate and must not fail on examples explicitly marked as counter-examples.
- A skill example references a stable id (FR/AC/CHK/CQ/DEC) that only resolves in the context of a full artifact set: the corpus must supply a complete, coherent artifact set so cross-artifact gates (coverage, back-refs) can actually run, not just a fragment.
- A child symptom is **already** fixed by a prior feature (e.g. the `clarify` skill already documents `sourceSpec`): the feature must confirm current state rather than blindly re-edit, and the doctest must lock the fixed state against regression.
- The gate grammar legitimately changes (a new required field, a new accepted form): the failing build is the *intended* signal that the skill and corpus must be updated in the same change — the check must point at what to update, not merely fail.
- Splitting out the back-ref diagnostic must not break existing consumers (remediation pointers, troubleshooting skill, the `PublicSurface.baseline` and any golden/snapshot tests) — the new identifier is added and the back-ref call sites move to it together, and the `malformedChecklistFrontMatter` cases that remain keep their identifier.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each `fs-gg-sdd-*` **stage** skill that documents a gated authoring artifact MUST ship at least one worked example that passes the actual gate for that stage.
- **FR-002**: A skill↔gate check MUST extract the worked example(s) from each stage skill and run them through the real gate command for that stage, distinguishing positive examples (must pass) from any explicitly marked counter-examples (excluded).
- **FR-003**: The skill↔gate check MUST fail the build when any positive stage-skill example would be rejected by its gate, and the failure MUST name the offending skill file and the blocking diagnostic.
- **FR-004**: The skill↔gate check MUST fail when a stage skill that documents a gated artifact ships no runnable positive example (no silently unexercised stage skill).
- **FR-005**: A copyable, known-good **example corpus** MUST ship in-repo, comprising at minimum a full coverage-registering `spec.md` and a deferral-bearing `evidence.yml`, forming a coherent artifact set the skill↔gate check runs.
- **FR-006**: The `fs-gg-sdd-specify` skill's worked FR example MUST use the gate-accepted coverage form (single physical line, non-bold, `- FR-###: … (covers AC-###)`) so that following it verbatim yields a spec whose FRs all register as covered.
- **FR-007**: The `fs-gg-sdd-evidence` skill MUST document the required deferral fields (`rationale`, `owner`, `scope`, `laterLifecycleVisibility`) and ship a deferral-bearing example that the gate accepts.
- **FR-008**: The `fs-gg-sdd-clarify` skill MUST state every required `clarify` front-matter field (including `sourceSpec`); where already documented, the feature MUST confirm it and lock it against regression via the check.
- **FR-009**: The required-field statements in stage skills MUST be **checked against** the CLI's typed contract by a CI test that fails the build when a gate-required field is absent from its skill or a skill names a field the contract does not require. Skills remain hand-authored — no generated regions are introduced into `SKILL.md`.
- **FR-010**: The missing-`[CHK:CHK-###]`-back-reference case MUST emit its **own** diagnostic that names that cause (e.g. `missingChecklistBackReference`), split out from `malformedChecklistFrontMatter`; the genuine malformed-front-matter and schema-version causes MUST retain `malformedChecklistFrontMatter`, which continues to name *those* causes. No deprecated alias for the old identifier is required (it is SDD-internal, not a consumed cross-repo contract).
- **FR-011**: When the back-ref case is split to its own diagnostic, all surfaces that describe or assert on it (remediation pointers, the `fs-gg-sdd-troubleshooting` skill, the `PublicSurface.baseline`, and any golden/snapshot tests) MUST be updated in the same change so no surface still attributes the missing back-reference to `malformedChecklistFrontMatter`.
- **FR-012**: The authored `.claude/skills/fs-gg-sdd-*` sources MUST remain the single canonical bodies that are embedded and seeded (init/scaffold) byte-identically across all three agent-skill roots; the feature MUST NOT create a second source of truth for skill content, and existing seed/drift guards MUST stay green.
- **FR-013**: The example corpus and doctest MUST run in the default offline inner loop and in CI without requiring a Governance runtime or network access.

### Key Entities *(include if feature involves data)*

- **Stage skill**: An authored `fs-gg-sdd-<stage>/SKILL.md` describing how to author a gated lifecycle artifact; the surface authors write *from*. Carries worked example(s) and required-field statements.
- **Gate**: The `fsgg-sdd <stage>` command's accept/reject logic in the CLI; the surface that *judges* an artifact. The source of truth for accepted grammar and required fields.
- **Example corpus**: A coherent, copyable set of known-good lifecycle artifacts (full `spec.md`, deferral-bearing `evidence.yml`, and the supporting artifacts needed for cross-artifact gates to run) shipped in-repo and exercised by the check.
- **Skill↔gate check**: The CI-run doctest that binds the two surfaces by extracting skill examples, running them through the real gates, and failing on rejection.
- **Diagnostic**: A named, author-facing gate rejection message; its name must name its real cause.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of stage skills that document a gated artifact have at least one worked example that passes its gate, verified by the skill↔gate check.
- **SC-002**: An author following any single stage skill verbatim produces a gate-passing artifact on the first attempt — zero required fields or accepted grammar forms are discoverable *only* by reading compiled CLI source.
- **SC-003**: A deliberately reintroduced drift (a skill example reverted to a known-bad form, or a gate-required field removed from a skill) causes the build to fail with a message that names the skill and the cause; reverting restores green — demonstrated on a test branch.
- **SC-004**: The checklist back-reference diagnostic names the missing `[CHK:CHK-###]` back-reference; in author testing, the fix is applied to the correct line on first read with no detour through front matter.
- **SC-005**: All four child issues (#141, #142, #143, #144) are closed and their symptoms are covered by the durable check, not just a one-time edit.

## Assumptions

- The canonical source of every `fs-gg-sdd-*` skill body is the repo's own `.claude/skills/fs-gg-sdd-*/SKILL.md`, embedded as `SeededSkill.*` resources and seeded by init/scaffold; editing these files is how both the repo's agent surface and every scaffolded workspace receive the fix.
- Some child symptoms may be partially or fully addressed by prior features (the `clarify` skill already appears to document `sourceSpec` as of feature 075); this feature confirms current state and locks it, rather than assuming all four are still broken.
- The four required evidence deferral fields are `rationale`, `owner`, `scope`, `laterLifecycleVisibility`, as enforced by the evidence gate handler; the typed contract, not this spec, is the authority if they differ at implementation time.
- The feedback report's build predates the most recent grammar-doc features; the systemic value is the doctest/field-check that prevents regression regardless of which individual symptoms currently reproduce.
- "Stage skills" for the doctest are the lifecycle-stage authoring skills (charter/specify/clarify/checklist/plan/tasks/analyze/evidence/verify/ship); cross-cutting skills without a gated authoring artifact are out of scope for the example-runs-through-a-gate obligation.
- The diagnostic split is author-facing name/message only; it does not change gate accept/reject behavior or exit codes. It changes the deterministic JSON contract only by adding the new `missingChecklistBackReference` identifier and moving the back-ref case's emitted identifier — the `PublicSurface.baseline` moves in lockstep.
