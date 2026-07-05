# Feature Specification: Preserve refs on auto-generated evidence obligations

**Feature Branch**: `077-evidence-obligation-refs`

**Created**: 2026-07-05

**Status**: Draft

**Input**: User description: "Preserve requirementRefs and planDecisionRefs on auto-generated evidence obligations. When `fsgg-sdd evidence` scaffolds obligations from the task graph, plan-decision-derived obligations currently drop their originating plan-decision linkage (`planDecisionRefs: []` is hardcoded in the skeleton declaration) and, for tasks that link a plan decision rather than an FR directly, `requirementRefs` comes out empty — forcing authors to join back to tasks.yml by task title to classify honestly. Add `evidence --from-tests` to pre-map obligations to a test file. Source: TD1 Bulwark field-feedback report FEEDBACK.md §3.5/§4.2/Rec #6; tracked as issue #124 under epic #127."

## User Scenarios & Testing *(mandatory)*

The FS.GG SDD evidence stage scaffolds one obligation per task in the (often auto-expanded)
task graph. In the TD1 *Bulwark* field run, 18 authored tasks expanded to 85 tasks and the
evidence stage scaffolded 85 obligations. To classify each obligation honestly (real pass vs.
synthetic vs. deferral), the author must know **what each obligation is for** — which requirement
or plan decision it descends from. Today that lineage is partially lost on the scaffolded
declaration: obligations descended from a **plan decision** ("Implement plan decision PD-001")
carry an **empty `requirementRefs`** *and* the plan-decision id is dropped (`planDecisionRefs` is
always empty). The author's only recovery was to join the scaffolded `evidence.yml` back to
`tasks.yml` by task title — a manual, error-prone step that TD1 automated with a throwaway Python
transform. This feature restores the lineage on the scaffolded obligation itself so no back-join
is needed, and adds an optional way to pre-map obligations to their proving test file.

### User Story 1 - Auto-generated obligations carry their originating refs (Priority: P1)

An author runs `fsgg-sdd evidence` on a work item whose task graph includes plan-decision tasks
(and mixed requirement/decision tasks). Every scaffolded obligation records the ids of the
requirement(s) and/or plan decision(s) it descends from, so the author can classify each
obligation from the `evidence.yml` entry alone.

**Why this priority**: This is the issue's hard acceptance and the direct source of the field
friction. Without it, honest classification at scale requires a title-join back to `tasks.yml`.
It is independently valuable and shippable on its own.

**Independent Test**: Author a work item with a plan decision (PD-###) and a requirement (FR-###),
generate tasks, run `fsgg-sdd evidence`, and inspect the scaffolded `evidence.yml`: the
obligation for the plan-decision task lists that PD id under its plan-decision refs; the
obligation for the requirement task lists that FR id under its requirement refs; neither requires
consulting `tasks.yml` to know its origin.

**Acceptance Scenarios**:

1. **Given** a task graph containing a task `Implement plan decision PD-001` (which links plan
   decision PD-001 and no requirement), **When** `fsgg-sdd evidence` scaffolds its obligation,
   **Then** the scaffolded declaration records `PD-001` under its plan-decision refs (not an empty
   list).
2. **Given** a task that links requirement FR-002, **When** `fsgg-sdd evidence` scaffolds its
   obligation, **Then** the scaffolded declaration records `FR-002` under its requirement refs
   (unchanged from today's behavior — this must not regress).
3. **Given** a task whose lineage carries both a requirement FR-003 and a plan decision PD-004,
   **When** `fsgg-sdd evidence` scaffolds its obligation, **Then** the declaration records FR-003
   under requirement refs and PD-004 under plan-decision refs.
4. **Given** a scaffolded plan-decision obligation, **When** it is scaffolded, **Then** its
   clarification-decision and acceptance-scenario ref buckets stay empty (routing is limited to
   requirement/plan-decision), and the `PD-###` never leaks into any other bucket.
5. **Given** the scaffolded `evidence.yml`, **When** an author reads any single obligation entry,
   **Then** its originating requirement/plan-decision ids are present without any join back to
   `tasks.yml`.

---

### User Story 2 - Pre-map obligations to a proving test file (Priority: P2)

An author whose tests already exist runs `fsgg-sdd evidence --from-tests <path>` to pre-populate
each scaffolded obligation with a pointer to the test file that proves it, reducing the per-entry
authoring burden on large graphs.

**Why this priority**: Named as an optional additive in the issue ("`evidence --from-tests`"). It
reduces at-scale authoring effort but is not required for honest classification, which US1
already delivers. Ships independently after US1.

**Independent Test**: Run `fsgg-sdd evidence --from-tests tests/Foo.Tests` on a work item and
confirm the scaffolded obligations reference the supplied test path in their evidence sources,
while a run without the flag behaves exactly as before.

**Acceptance Scenarios**:

1. **Given** a valid test path, **When** `fsgg-sdd evidence --from-tests <path>` scaffolds
   obligations, **Then** each scaffolded obligation carries a verification-kind source pointing at
   `<path>`.
2. **Given** no `--from-tests` flag, **When** `fsgg-sdd evidence` runs, **Then** output is
   byte-identical to today's scaffolding (the flag is purely additive).
3. **Given** a blank `--from-tests` value, **When** the command runs, **Then** it is treated as
   absent (no source seeded) rather than scaffolding an empty-path source; a non-blank path is
   recorded as a declared verification source whose existence the verify stage later checks.

---

### Edge Cases

- A task whose source lineage contains **no** requirement and **no** decision id (e.g. a
  `task.<id>.completion` obligation for a Done task lacking authored evidence) → both requirement
  and plan-decision refs remain empty; behavior is unchanged and correct.
- A task linking **multiple** plan decisions → all of them appear in plan-decision refs, sorted
  and de-duplicated, matching how requirement refs are already handled.
- An **author-supplied** `evidence.yml` entry that already exists for an obligation is not
  overwritten by scaffolding; ref preservation applies only to newly scaffolded skeleton entries
  (no-clobber of authored content).
- Decision ids that match neither the plan-decision (`PD-`) nor clarification (`DEC-`/`CQ-`) shape
  → routed by the established id grammar; an unrecognized shape must not crash the stage.
- Re-running `evidence` after refs were added must be deterministic and idempotent (no spurious
  churn on already-correct entries).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When `fsgg-sdd evidence` scaffolds a skeleton obligation from a task, the scaffolded
  declaration MUST record the originating requirement id(s) under its requirement refs (preserving
  today's behavior).
- **FR-002**: When the originating task's source lineage carries one or more **plan decisions**
  (`PD-###`), the scaffolded declaration MUST record those plan-decision id(s) under its
  plan-decision refs instead of an empty list.
- **FR-003**: Scaffold routing MUST be limited to the requirement and plan-decision buckets (the
  origin the issue asks scaffolding to preserve). The acceptance-scenario, clarification-decision,
  and checklist-result buckets MUST remain empty on a scaffolded obligation (unchanged from prior
  behavior) — so scaffolding does not widen the evidence stage's unknown-reference validation
  surface. A `PD-###` id MUST never be routed into any bucket other than plan-decision refs.
- **FR-004**: An author MUST be able to classify any scaffolded obligation's origin
  (requirement/plan-decision) from the `evidence.yml` entry alone, with no join back to
  `tasks.yml`.
- **FR-005**: Preserved ref lists MUST be sorted and de-duplicated, consistent with the existing
  requirement-ref handling, so output is deterministic.
- **FR-006**: Ref preservation MUST apply only to **newly scaffolded** skeleton entries and MUST
  NOT overwrite or reorder refs on obligations an author has already authored (no-clobber).
- **FR-007**: `fsgg-sdd evidence` MUST accept an optional `--from-tests <path>` flag that
  pre-populates each newly scaffolded obligation with a verification-kind evidence source pointing
  at `<path>`.
- **FR-008**: Without `--from-tests`, `fsgg-sdd evidence` output MUST remain byte-identical to the
  pre-feature behavior aside from the added refs (the flag is additive and inert when absent).
- **FR-009**: A blank/whitespace `--from-tests` value MUST be treated as absent (inert — no source
  seeded), never seeding an empty-path source. The supplied path is recorded as a **declared**
  verification-source pointer; its on-disk existence and freshness are validated downstream at the
  verify stage — consistent with how every other evidence source is handled (the evidence stage
  *declares* sources; verify *validates* them). SDD does not duplicate that filesystem check at
  authoring time.
- **FR-010**: The change MUST hold across all three report projections (`--json` default,
  `--text`, `--rich`) without changing the JSON contract beyond the now-populated ref fields, and
  MUST be reflected in the readiness/work-model views that surface obligation refs.

### Key Entities *(include if feature involves data)*

- **Evidence obligation**: the unit of proof scaffolded one-per-task. Origin lineage of interest:
  requirement refs, plan-decision refs, clarification-decision refs. Today it internally captures
  the linked decision ids but does not surface plan decisions onto the scaffolded declaration.
- **Scaffolded (skeleton) evidence declaration**: the `evidence.yml` entry emitted for an
  as-yet-unauthored obligation. This is the artifact whose ref fields must be populated.
- **Task**: the task-graph node an obligation descends from; supplies the requirement ids and
  decision ids (plan and/or clarification) that seed the obligation's refs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a work item whose graph mixes requirement tasks and plan-decision tasks, 100% of
  scaffolded obligations expose their originating requirement and/or plan-decision id(s) on their
  own entry.
- **SC-002**: Zero scaffolded plan-decision obligations emit an empty plan-decision ref list when a
  plan-decision origin exists.
- **SC-003**: An author can classify every obligation without opening `tasks.yml` (no title-join
  step), eliminating the throwaway transform TD1 needed for its 85-obligation graph.
- **SC-004**: With `--from-tests` absent, evidence-stage output is unchanged relative to the prior
  release for the same inputs (regression-free additive change).
- **SC-005**: Re-running `evidence` on an unchanged work item produces identical output
  (deterministic, idempotent) with the new refs present.

## Assumptions

- Requirement, plan-decision, and clarification-decision ids are distinguishable by their
  established id grammar (`FR-###`, `PD-###`, `DEC-###`/`CQ-###`); the routing in FR-001..FR-003
  keys off that grammar rather than a new authored field.
- An obligation's origin lineage is the source-id set of the task it descends from. For a
  plan-decision task that source set carries the plan-decision id **and** any requirement/scenario
  ids that plan decision traces to — so recovering it also recovers the PD→FR linkage the field
  report called out (FR-004), not just the PD id. This feature threads that already-recorded task
  lineage onto the scaffolded declaration and routes it by grammar; it does not invent new lineage.
- `--from-tests` pre-maps a **single** test path applied to all newly scaffolded obligations for
  this run; per-obligation test mapping and a broader bulk-authoring affordance for very large
  graphs are out of scope for this feature (the issue lists bulk authoring as "consider", tracked
  separately if pursued — see epic #127 / sibling #126).
- No persisted schema version bump is required; the ref fields already exist in the evidence
  schema and are merely being populated where they were previously empty.
- Governance-owned evidence freshness and gate enforcement remain downstream and unaffected.
