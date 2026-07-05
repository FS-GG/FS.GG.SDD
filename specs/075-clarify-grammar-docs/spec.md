# Feature Specification: Document the clarify decision-tag grammar and per-stage front-matter

**Feature Branch**: `075-clarify-grammar-docs`

**Created**: 2026-07-05

**Status**: Draft

**Input**: TD1 *Bulwark: Tower Defense* field-feedback report (`FEEDBACK.md` §3.1, §4.1, Rec #3), tracked as FS-GG/FS.GG.SDD#122 under epic #127. A full charter→ship SDD run (57/57 tests, 0 synthetic evidence) blocked at `clarify` four times before the author discovered the real ambiguity-resolution grammar, which is present only in the shipped example artifact and not in any skill body or reference doc.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Resolve a blocking ambiguity from the skill alone (Priority: P1)

A first-time author reaches the `clarify` stage with a spec that carries open `AMB-###` ambiguities. Reading only the `fs-gg-sdd-clarify` skill body — never opening the shipped example artifact — the author learns that an ambiguity is resolved **only** by a decision or accepted deferral that carries an `[AMB:AMB-###]` tag, and authors a `clarifications.md` that passes `clarify` on the first attempt.

**Why this priority**: This is the top authoring blocker named in the feedback ("kills the top authoring blocker") — it cost four wasted iterations on a real run. Every other item in this feature is a refinement around it. Documenting the decision-tag resolution mechanism is the minimum viable slice that delivers the feature's core value.

**Independent Test**: Give a reader only the `fs-gg-sdd-clarify` skill body and a spec with an unresolved `AMB-001`; confirm they can author a `clarifications.md` that the live `clarify` stage accepts, without consulting `docs/examples/lifecycle-artifacts/clarifications.md` or decompiling the CLI.

**Acceptance Scenarios**:

1. **Given** the `fs-gg-sdd-clarify` skill body, **When** an author needs to resolve `AMB-001`, **Then** the skill states that a `DEC-###` decision or accepted deferral must carry an `[AMB:AMB-001]` tag and shows a worked line demonstrating the tag.
2. **Given** the skill's example block, **When** an author copy-adapts it, **Then** the example uses the same `[AMB:AMB-###]` tag form as the live parser accepts (i.e. the skill example no longer diverges from the shipped `clarifications.md`).
3. **Given** an author who keyed answers by `CQ-###`/`AMB-###` in the Answers section only, **When** they read the skill, **Then** the skill explains that resolution is carried by the decision **tag**, not by an entry in the Answers section, so an answer without a tagged decision still leaves the ambiguity blocking.

---

### User Story 2 - Know the required front-matter for each stage (Priority: P2)

An author whose stage artifact is rejected with an opaque "incomplete" error can consult the `fs-gg-sdd-authoring-contracts` skill and find the exact set of required front-matter fields for the stage they are authoring, add them, and clear the block.

**Why this priority**: The under-specified "incomplete" front-matter error was the second friction source (§4.1). It is independent of the decision-tag work — a reader can benefit from the front-matter field lists even if they never hit an ambiguity — but it is a refinement on top of the P1 blocker.

**Independent Test**: Give a reader only the `fs-gg-sdd-authoring-contracts` skill and the "incomplete" front-matter error; confirm they can enumerate the required fields for the failing stage and produce accepted front matter without reading source.

**Acceptance Scenarios**:

1. **Given** the `fs-gg-sdd-authoring-contracts` skill, **When** an author looks up front-matter requirements, **Then** the skill lists the required front-matter fields (`schemaVersion`, `workId`, `title`, `stage`, `changeTier`, `status`, `sourceSpec`, and any others the live parser requires) for each lifecycle stage that requires front matter.
2. **Given** the documented field list, **When** the author authors front matter matching it, **Then** the stage no longer reports "incomplete."

---

### User Story 3 - Avoid the duplicate-id and digest pitfalls (Priority: P3)

An author reading the clarify/authoring-contracts skills understands two remaining gotchas: (a) writing a `DEC-###` id inside *Accepted Deferrals* prose is counted by the parser as a second declaration and triggers `duplicateClarificationId`, and (b) whether the `Source Snapshot` `sha256:` field requires a real digest or accepts an example placeholder.

**Why this priority**: These are lower-frequency papercuts that cost time but were not the primary blocker. They round out the "author succeeds from the skills alone" goal.

**Independent Test**: Give a reader the skill body and confirm they can (a) predict that a stray `DEC-001` mention in prose will trigger `duplicateClarificationId` and know how to phrase deferrals to avoid it, and (b) state the correct `sha256:` requirement.

**Acceptance Scenarios**:

1. **Given** the clarify skill, **When** an author references an already-declared `DEC-###` id, **Then** the skill warns that any occurrence of a `DEC-###` id is treated as a declaration and shows the accepted way to record an accepted deferral without a duplicate.
2. **Given** the skill, **When** an author fills the `Source Snapshot` `sha256:` field, **Then** the skill states unambiguously whether a real digest is required or an example placeholder is acceptable.

---

### Edge Cases

- The seeded process skills are SDD-owned and must be **byte-identical** across all agent-skill roots (`.claude`, `.codex`, `.agents`) with a drift guard pinning them to the authored source. Any documentation change must land in the authored source of truth and be mirrored to every root so the drift guard stays green.
- The `fs-gg-sdd-authoring-contracts` skill and reference doc currently describe exactly **three** load-bearing grammars; adding a fourth (the decision-tag grammar) and the front-matter field lists must not misrepresent the count or break the existing drift-guarded examples that are run through the live parser on each build.
- If a stage's required front-matter set differs from `clarify`'s, the documentation must reflect the per-stage differences rather than implying one universal set.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `fs-gg-sdd-clarify` skill body MUST document the decision-tag resolution mechanism: an ambiguity is resolved by carrying its `AMB-###` id on a `DEC-###` line under `## Decisions` **or** `## Accepted Deferrals` (each accepted deferral its own uniquely-identified `DEC-###`), **and** not leaving that ambiguity as a blocking bullet under `## Remaining Ambiguity`. It MUST present `[AMB:AMB-###]` as the canonical tool-emitted form while being accurate that the load-bearing requirement is the `AMB-###` id appearing on the decision line in one of those two sections (the bracket/prefix is a convention, not a parser requirement).
- **FR-002**: The `fs-gg-sdd-clarify` skill's inline example MUST use the same decision-tag grammar the live parser accepts, so the skill example no longer diverges from the shipped `clarifications.md` example artifact.
- **FR-003**: The `fs-gg-sdd-clarify` skill MUST explain that resolution is carried by the decision **tag**, not by an entry in the Answers section, so keying answers by `CQ-###`/`AMB-###` alone still leaves the ambiguity blocking.
- **FR-004**: The `fs-gg-sdd-authoring-contracts` skill MUST document, per lifecycle stage, the front-matter fields that actually **gate** parsing (whose absence produces the "incomplete"/malformed diagnostic) distinct from fields the template includes but the parser **defaults**, faithfully to the live parser — e.g. `clarify` gates on `schemaVersion, workId, stage, sourceSpec` while `title/changeTier/status` are defaulted; `charter` requires all six; `checklist`/`plan` add `sourceClarifications`/`sourceChecklist`. It MUST also state the closed `stage` vocabulary and that `changeTier`/`status` are free strings (not an enforced vocabulary). The documentation MUST NOT claim a field is required, or a value is validated, when the parser does not enforce it.
- **FR-005**: The `fs-gg-sdd-clarify` (and/or `fs-gg-sdd-authoring-contracts`) skill MUST warn that any occurrence of a `DEC-###` id is treated by the parser as a declaration (so referencing an already-declared id in prose triggers `duplicateClarificationId`) and MUST show the accepted way to record an accepted deferral without a duplicate.
- **FR-006**: The documentation MUST state the truth about `Source Snapshot` `sha256:`: it is **not** a `clarify` concept (clarifications has no Source Snapshot section); it is an **optional** field of the `checklist`/`plan` Source Snapshot lines (and the `tasks`/`evidence` `sources.digest`), validated only as a 64-hex format when present and used solely for staleness detection — a non-conforming placeholder is silently ignored, never a blocking error, and a real digest is never required to author. This corrects the feedback's premise that clarify carries a required `sha256:`.
- **FR-007**: The documentation changes MUST land in the authored source of truth for the seeded skills and be mirrored byte-identically into every agent-skill root so the existing skill drift guard remains green.
- **FR-008**: The durable reference doc (`docs/reference/authoring-contracts.md`) MUST be kept coherent with the skills for the newly documented grammars, consistent with the skill's claim that it is the drift-guarded source; any newly documented grammar example that the build runs through the live parser MUST pass.

### Key Entities *(include if feature involves data)*

- **Decision tag** — the `[AMB:AMB-###]` marker on a `DEC-###` decision or accepted deferral in `clarifications.md`; the sole mechanism that resolves a carried ambiguity.
- **Per-stage front matter** — the YAML header block each lifecycle artifact requires; its required field set is stage-specific and currently undocumented in the skills.
- **Seeded process skills** — the SDD-owned `fs-gg-sdd-*` skill bodies, authored once and mirrored byte-identically across `.claude`/`.codex`/`.agents`, pinned by a drift guard.

## Success Criteria *(mandatory)*

- **SC-001**: A first-time author can satisfy the `clarify` stage using only the `fs-gg-sdd-clarify` and `fs-gg-sdd-authoring-contracts` skill bodies, without opening the shipped example artifact or inspecting the CLI (the epic-#122 acceptance criterion).
- **SC-002**: The number of failed `clarify` attempts attributable to an undocumented resolution grammar or front-matter set drops to zero for an author following the skills (down from the four wasted iterations observed on the TD1 run).
- **SC-003**: Every grammar and front-matter form newly documented in the skills or reference doc matches what the live parser accepts, verified by the existing drift-guard / live-parser tests staying green.
- **SC-004**: The seeded skill drift guard passes: the updated skill bodies are byte-identical across all agent-skill roots and pinned to the authored source.

## Assumptions

- The authored source of truth for the seeded `fs-gg-sdd-*` skills, and its mirroring to the three agent-skill roots plus the drift guard, is the mechanism through which these documentation edits must flow (identified during specification; the exact files are a planning concern).
- The required per-stage front-matter field sets are fixed by the current live parser; this feature documents them faithfully and does not change what the parser requires.
- This is a documentation feature: it changes skill/reference-doc prose and examples only, not the CLI's parsing or gating behavior.
- Both the Claude (`.claude`) and Codex (`.codex`) agent surfaces, and the neutral `.agents` root, are in scope for the byte-identical mirror, per the SDD skill-seeding policy.

## Dependencies

- The existing skill drift guard and the live-parser example tests (`AuthoringDocsContractTests`, `ExampleArtifactsContractTests`) are the coherence gates this feature must keep green.
- No cross-repo contract change: this is SDD-internal documentation of SDD-owned grammars; no registry or versioned-contract update is required.
