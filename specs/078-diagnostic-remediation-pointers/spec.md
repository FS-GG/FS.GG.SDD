# Feature Specification: Blocking diagnostics point to their shipped example / grammar section

**Feature Branch**: `078-diagnostic-remediation-pointers`

**Created**: 2026-07-05

**Status**: Draft

**Input**: User description: "Blocking diagnostics point to their shipped example / grammar section. Every blocking diagnostic emitted by fsgg-sdd (the ~101 error diagnostics in DiagnosticConstructors.fs, e.g. clarify's \"Clarification input is missing answers for blocking ambiguity: AMB-001\") should carry, in its Correction text, a resolving pointer to the relevant shipped example (docs/examples/lifecycle-artifacts/…) and/or the grammar section anchor in docs/reference/authoring-contracts.md, so an author can unblock without external help. Pairs with fsgg-sdd lint (#123, spec 076) and the authoring-contracts docs (#122, spec 075). Source: TD1 Bulwark field-feedback report FEEDBACK.md §3.6/§4.2/Rec #7; tracked as issue #125 under epic #127."

## User Scenarios & Testing *(mandatory)*

In the TD1 *Bulwark: Tower Defense* field run (a full charter→ship SDD pass, 57/57 tests,
0 synthetic evidence, on `fsgg-sdd` 0.6.0), the blocking diagnostics were **accurate but
under-actionable**: they named *what* was wrong without saying *how* to satisfy the grammar
that was violated. The canonical example was `clarify` reporting *"Clarification input is
missing answers for blocking ambiguity: AMB-001…"* — correct, but never mentioning the
required decision-with-`[AMB:AMB-001]`-tag mechanism that resolves it. Each such gap cost
the author 2–4 iterations of guessing, or a trip to external docs, before the block cleared.

This feature closes that gap for the **authoring-grammar** class of blocking diagnostics —
the ones an author trips by mis-formatting a load-bearing grammar (front matter, the FR→AC
coverage line, stable-id declarations and references, the clarify decision-tag rule, and the
`evidence.yml` declaration rules). Every such diagnostic's correction gains a **resolving
pointer**: a stable, in-repo reference to the shipped example artifact that demonstrates the
correct shape (`docs/examples/lifecycle-artifacts/<stage>`) and/or the grammar section anchor
in `docs/reference/authoring-contracts.md` that states the rule. To make an example pointer
available for **every** authoring stage, the three stages that lack one today — charter,
specify, plan — gain shipped, build-validated example artifacts alongside the existing four
(checklist, clarifications, evidence, tasks).

The pointer is added to the existing `Correction` field of the diagnostic; it therefore
flows unchanged into every projection (`--json`, `--text`, `--rich`) and into `fsgg-sdd lint`
/ `--explain` output, which already surface corrections. No new field, output stream, or exit
code is introduced.

## Clarifications

### Session 2026-07-05

- Q: Are grammar-rooted **aggregate readiness** blocks (e.g. `failedChecklistPrerequisite`,
  `failedPlanPrerequisite`, `evidence.missingRequiredEvidence`, `evidence.missingRequiredSkill`,
  `verify.missingRequiredTest`) in the covered authoring-grammar set? → A: Yes — include them;
  they carry pointers to the relevant example + grammar section (e.g. `failedChecklistPrerequisite`
  → checklist example + acceptance-coverage-line grammar).
- Q: How strong is the pointer requirement when a stage has **both** a shipped example and a
  grammar section? → A: MUST cite **both** the example path and the grammar anchor whenever both
  exist for that stage (not just at-least-one).

### User Story 1 - A blocked author self-unblocks from the diagnostic alone (Priority: P1)

An author runs a lifecycle stage and hits an authoring-grammar block (e.g. `clarify`'s
missing-answer, `checklist`'s failed coverage, an `evidence` synthetic-disclosure block, or a
malformed-front-matter block on any stage). The diagnostic's correction now names the shipped
example artifact and/or the grammar section that resolves it. The author opens that example or
section, mirrors the shape, reruns the stage, and the block clears — without leaving the repo
or asking another person.

**Why this priority**: This is the entire point of the issue (Rec #7, "self-service
unblocking"). It delivers value on its own: even before every last diagnostic is covered, each
covered diagnostic independently saves iterations.

**Independent Test**: Deliberately violate one covered grammar (e.g. remove the `[AMB:…]` tag
from a clarify decision), run the stage, and confirm the emitted correction contains a pointer
that, when followed, resolves the block.

**Acceptance Scenarios**:

1. **Given** a `clarifications.md` with a blocking `AMB-001` and no resolving decision,
   **When** the author runs `fsgg-sdd clarify`, **Then** the `missingClarificationAnswer`
   correction names both the shipped `clarifications.md` example path and the
   `authoring-contracts.md` clarify decision-tag grammar section.
2. **Given** a `checklist.md` whose FR→AC coverage line is mis-formatted so coverage fails,
   **When** the author runs `fsgg-sdd checklist`, **Then** the `failedChecklistPrerequisite`
   correction names the shipped checklist example and the acceptance-coverage-line grammar
   section.
3. **Given** an `evidence.yml` with a synthetic declaration missing its disclosure,
   **When** the author runs `fsgg-sdd evidence`, **Then** the
   `evidence.undisclosedSyntheticEvidence` correction names the shipped evidence example and
   the `evidence.yml` declarations grammar section.
4. **Given** a `plan.md` with malformed front matter, **When** the author runs `fsgg-sdd
   plan`, **Then** the `malformedPlanFrontMatter` correction names the (new) shipped plan
   example and the per-stage front-matter grammar section.

---

### User Story 2 - Every authoring stage has a shipped example to point at (Priority: P2)

An author on any of the seven authoring stages (charter, specify, clarify, checklist, plan,
tasks, evidence) can open a complete, correct, copy-adaptable example artifact for that stage
under `docs/examples/lifecycle-artifacts/`. Today only four exist; this story adds the missing
charter, spec, and plan examples so User Story 1 can cite an example for every stage.

**Why this priority**: US1 can partially ship using grammar anchors alone, but the example
pointer is only universally citable once every stage has one. The new examples are also
valuable independently as copy-adapt starting points.

**Independent Test**: Confirm `docs/examples/lifecycle-artifacts/` contains a build-validated
example for each of the seven authoring stages, and that each new example parses/validates
against the live stage parser exactly as the existing four do.

**Acceptance Scenarios**:

1. **Given** the shipped examples directory, **When** the build runs the example-artifacts
   contract test, **Then** charter, spec, and plan examples are present and validate against
   their live stage parsers with zero blocking diagnostics.
2. **Given** any of the seven authoring stages, **When** its example is opened, **Then** it is
   a complete artifact for that stage with the required front matter and sections, annotated
   with a header comment pointing back to `authoring-contracts.md` and the stage skill.

---

### User Story 3 - Pointers never dangle (Priority: P1)

Every remediation pointer emitted by a covered diagnostic resolves to a real target: the
example path exists on disk, and the grammar anchor exists as a heading in
`docs/reference/authoring-contracts.md`. A build-time guard fails if any covered diagnostic
cites an example path or anchor that does not exist, so the corrections cannot rot as docs are
renamed.

**Why this priority**: A pointer that 404s is worse than no pointer — it sends the author on a
dead-end. The guard is what lets the corrections be trusted as "resolves without external
help."

**Independent Test**: Rename a grammar heading in `authoring-contracts.md` and confirm the
guard test fails until the citing correction (or the heading) is fixed.

**Acceptance Scenarios**:

1. **Given** the set of covered diagnostics, **When** the pointer-resolution guard runs,
   **Then** every cited example path exists under `docs/examples/lifecycle-artifacts/` and
   every cited grammar anchor matches a heading in `authoring-contracts.md`.
2. **Given** a covered diagnostic, **When** its correction is inspected, **Then** it carries
   at least one resolving pointer (an example path, a grammar anchor, or both).

### Edge Cases

- **Diagnostics outside the authoring-grammar class** (config/system, pure
  sequencing/prerequisite that only mean "run the prior stage first", generated-view/tool-defect,
  scaffold, doctor/upgrade): out of scope for the pointer requirement. Their existing corrections
  (which already point to `init`, the prior stage command, or a regenerate/tool remediation) are
  unchanged. The guard only asserts over the covered set, so it does not force pointers onto
  these. Note the distinction from the **grammar-rooted aggregate** blocks (FR-001), which *are*
  covered: `missingChecklistPrerequisite` ("run checklist first") is pure sequencing and out;
  `failedChecklistPrerequisite` ("coverage failed") rolls up a grammar failure and is in.
- **A stage whose block spans multiple grammars** (e.g. malformed front matter *and* a bad id):
  the correction points to the most specific applicable grammar section; front-matter blocks
  point to the per-stage front-matter section.
- **`--rich` / `--text` projections**: the pointer is plain text inside the existing
  `Correction` field, so it renders identically across projections and adds no ANSI-only or
  JSON-only facts (determinism and golden contracts unaffected — the golden fixtures update to
  include the new correction text).
- **Anchor stability**: pointers cite section anchors derived from `authoring-contracts.md`
  headings; if a heading is renamed the guard (US3) fails, forcing the citation to move with
  it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define an explicit, enumerated set of *authoring-grammar
  blocking diagnostics* — the error-severity diagnostics an author trips by violating a
  load-bearing authoring grammar (per-stage front matter, the FR→AC coverage line, stable-id
  declarations/references, duplicate-id, the clarify `[AMB:…]` decision-tag rule, and the
  `evidence.yml` declaration/disclosure/deferral rules). The set **includes grammar-rooted
  aggregate readiness blocks** — those that summarize an underlying grammar failure, such as
  `failedChecklistPrerequisite` (coverage), `failedPlanPrerequisite`, `failedTasksPrerequisite`,
  `evidence.missingRequiredEvidence`, `evidence.missingRequiredSkill`, and
  `verify.missingRequiredTest` — each pointed at the example + grammar section of the grammar it
  rolls up. This set is the scope over which the pointer requirement and the guard apply.
- **FR-002**: Every diagnostic in the authoring-grammar set MUST carry, within its existing
  `Correction` text, a *resolving pointer* to the shipped example and/or grammar section for its
  stage. When **both** a shipped example path (under `docs/examples/lifecycle-artifacts/`) and a
  grammar section anchor (in `docs/reference/authoring-contracts.md`) exist for the relevant
  stage, the correction MUST cite **both**. When only one exists, it MUST cite that one. (After
  FR-004 lands, all seven authoring stages have an example, so the both-when-both-exist rule is
  the effective requirement for every stage-scoped diagnostic.)
- **FR-003**: The pointer MUST be carried in the existing `Correction` field only — no new
  diagnostic field, output stream, or exit code is introduced, and the JSON automation
  contract's shape is unchanged (only correction string values change).
- **FR-004**: The system MUST ship build-validated example artifacts for the charter,
  specify, and plan stages under `docs/examples/lifecycle-artifacts/`, so that all seven
  authoring stages (charter, specify, clarify, checklist, plan, tasks, evidence) have a shipped
  example a diagnostic can cite.
- **FR-005**: Each new example artifact MUST validate against its live stage parser with zero
  blocking diagnostics, under the same build-time contract test that validates the existing
  four examples (so examples cannot drift from the tool).
- **FR-006**: The system MUST enforce, at build time, that every pointer emitted by an
  authoring-grammar diagnostic resolves — each cited example path exists on disk and each cited
  grammar anchor corresponds to a real heading in `docs/reference/authoring-contracts.md`. A
  dangling pointer MUST fail the build.
- **FR-007**: The pointer text MUST be stable and deterministic (no timestamps, absolute
  paths, or environment-dependent content), so `--json`/`--text` golden fixtures and
  determinism checks remain reproducible.
- **FR-008**: Diagnostics **outside** the authoring-grammar set MUST NOT be required to carry
  an example/anchor pointer, and their existing corrections MUST remain unchanged by this
  feature.
- **FR-009**: The pointer MUST be carried in the `--json` automation contract's `correction` value
  for every covered diagnostic — the default output and the surface agents consume (the TD1 run was
  agent-driven and read JSON). The `--text` and `--rich` projections are summaries that render
  diagnostic counters (and, for `--rich`, a severity/id/message table) but **not** per-diagnostic
  corrections; they are unchanged by this feature. The pointer MUST stay **coherent** with the
  `fsgg-sdd lint` / `--explain` pre-flight (feature 076), which independently carries its own
  grammar-section pointer per defect class: both MUST cite the same
  `docs/reference/authoring-contracts.md` anchors, so the machine-blocking path and the human
  pre-flight guide to the same grammar. (Lint renders its fix from its own 076 defect model, not
  from the appended Correction; this feature does not re-plumb lint or the rich table.)

### Key Entities *(include if data involved)*

- **Authoring-grammar diagnostic set**: the enumerated subset of blocking (error-severity)
  diagnostics in scope for the pointer requirement; the domain of both FR-002 and the FR-006
  guard.
- **Remediation pointer**: a resolving reference embedded in a diagnostic's `Correction` —
  either a shipped example path (`docs/examples/lifecycle-artifacts/<stage>`), a grammar
  section anchor (`docs/reference/authoring-contracts.md#<section>`), or both.
- **Shipped example artifact**: a complete, copy-adaptable, build-validated authoring artifact
  for one stage under `docs/examples/lifecycle-artifacts/`; the target of an example pointer.
  This feature adds the charter, spec, and plan artifacts to the existing four.
- **Grammar section**: a heading in `docs/reference/authoring-contracts.md` stating a
  load-bearing grammar; the target of an anchor pointer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the authoring-grammar blocking diagnostics (including the grammar-rooted
  aggregate readiness blocks) carry a resolving pointer in their correction — both the example
  and the grammar anchor wherever both exist for the stage — verified by the FR-006 guard over
  the enumerated set.
- **SC-002**: All seven authoring stages have a shipped, build-validated example artifact under
  `docs/examples/lifecycle-artifacts/` (up from four).
- **SC-003**: Zero dangling pointers — every cited example path and grammar anchor resolves to
  a real target, enforced at build time (build fails otherwise).
- **SC-004**: An author who hits a covered block can resolve it using only the pointer named in
  the correction, with no reference to external/out-of-repo material — demonstrated for the
  canonical TD1 case (clarify missing-answer → `[AMB:…]` decision-tag grammar) and at least one
  case per covered stage.
- **SC-005**: The JSON automation contract byte-shape is unchanged except for the value of
  affected `correction` strings (no new keys, streams, or exit codes); existing golden fixtures
  update deterministically.

## Assumptions

- "Blocking diagnostic" means an **error-severity** diagnostic (the class that blocks a
  command / trips `hasBlocking`); non-blocking Info/Warning advisories are out of scope.
- The pointer lives in the diagnostic's existing `Correction` field; no schema/field change is
  needed. This preserves the JSON contract and requires no persisted-schema version bump.
- Grammar anchors reference `docs/reference/authoring-contracts.md` (the canonical grammar
  reference authored in feature 075/#122). `.fsgg/early-stage-guidance.md` remains the
  scaffolded read-only mirror; corrections cite the canonical doc, not the mirror.
- The three new example artifacts follow the established shipped-example conventions
  (front-matter block, a header comment linking `authoring-contracts.md` and the stage skill,
  validated by the existing `ExampleArtifactsContractTests`-equivalent build test).
- Pairs with — but does not depend on shipping alongside — `fsgg-sdd lint` (#123 / feature 076)
  and the authoring-contracts docs (#122 / feature 075), both already merged.
- The exact enumeration of the authoring-grammar set and the precise per-diagnostic pointer
  targets are refined during `/speckit-clarify` and `/speckit-plan`; this spec fixes the
  contract (every member carries a resolving pointer; no member dangles), not the final row list.
