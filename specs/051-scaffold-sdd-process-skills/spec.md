# Feature Specification: Emit fs-gg-sdd-* process skills into scaffolded products

**Feature Branch**: `051-scaffold-sdd-process-skills`

**Created**: 2026-06-30

**Status**: Draft

**Input**: User description: "start the next sdd owned item on the coordination board." → resolved to Coordination board item FS-GG/FS.GG.SDD#48: "Feature: emit fs-gg-sdd-* process skills into scaffolded products (initEffects)".

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A scaffolded product's agent can discover the SDD process (Priority: P1)

A product author runs `fsgg-sdd scaffold` (or `fsgg-sdd init`) to stand up an
SDD-managed product. When their coding agent opens the new product, it finds the
full `fs-gg-sdd-*` process skill set already present in the product's skill
directories, so the agent can discover and follow the lifecycle (charter →
specify → … → ship) without the author hand-copying skills out of the FS.GG.SDD
repo.

**Why this priority**: This is the entire point of the feature. Today a
`lifecycle=sdd` product receives the skeleton (`.fsgg/`, `work/`, `readiness/`,
constitution, early-stage guidance, agent guidance files) but **zero** SDD-process
skills, so the scaffolded product's agent cannot discover the process it is meant
to follow. Without this slice there is no value.

**Independent Test**: Run `init` (and separately `scaffold`) into an empty
directory and confirm the produced skeleton contains a populated skill directory
with each expected `fs-gg-sdd-*` skill present and non-empty. Delivers the core
value on its own.

**Acceptance Scenarios**:

1. **Given** an empty directory, **When** the author runs the skeleton-seeding
   command, **Then** the product contains a `fs-gg-sdd-*` process skill for each
   skill in the declared set, each with a non-empty body.
2. **Given** a freshly seeded product, **When** its agent loads available skills,
   **Then** the lifecycle-stage skills (charter, specify, clarify, checklist,
   plan, tasks, analyze, evidence, verify, ship) and the cross-cutting skills
   (lifecycle map, getting-started, authoring-contracts, refresh-agents, validate)
   are all discoverable.
3. **Given** a product seeded by `scaffold` via the external Rendering provider,
   **When** the seeded tree is inspected, **Then** the SDD process skills are
   present and were produced by SDD's own skeleton seam, not by the provider.

---

### User Story 2 - Re-running the seeding command never clobbers author edits (Priority: P2)

An author has seeded a product and then locally adjusted a seeded skill body (or
left it untouched). When they re-run the seeding command, their files are
preserved — the seeded skills follow the same no-clobber policy as the seeded
constitution and early-stage guidance.

**Why this priority**: The seeded skills are authored, SDD-owned skeleton files,
not regenerated product views. Honoring the established no-clobber/SDD-owned
policy is required for the feature to be safe to re-run and to integrate with
`refresh`, but the core discovery value (P1) is realizable before this is proven.

**Independent Test**: Seed a product, modify one seeded skill file, re-run the
seeding command, and confirm the modified file is left exactly as the author left
it (no overwrite, no error).

**Acceptance Scenarios**:

1. **Given** a product whose seeded skill file an author has edited, **When** the
   seeding command runs again, **Then** the edited file is preserved unchanged.
2. **Given** a seeded product, **When** the currency/regeneration generator runs,
   **Then** the seeded skills are preserved (treated as authored SDD-owned
   skeleton, never as externally-owned generated product output).

---

### User Story 3 - Seeding is deterministic and Claude/Codex stay equivalent (Priority: P3)

A maintainer relies on the skeleton-seeding command producing byte-identical
output across runs and machines, and on the Claude and Codex agent surfaces
carrying equivalent skill content.

**Why this priority**: Determinism and cross-agent parity are existing,
load-bearing properties of the seeded skeleton; this feature must not erode them.
They are guardrails on top of the delivered value rather than the value itself.

**Independent Test**: Seed the same input twice and diff the produced skill files
(must be identical, with no embedded dates or other nondeterministic content);
diff the Claude vs Codex skill set for equivalence.

**Acceptance Scenarios**:

1. **Given** identical inputs, **When** the seeding command runs twice, **Then**
   the produced skill files are byte-identical and contain no dates or other
   run-varying content.
2. **Given** a seeded product, **When** the Claude skill surface and the Codex
   skill surface are compared, **Then** each declared skill appears on both with
   equivalent content.

---

### Edge Cases

- **Pre-existing skill file**: If a target skill file already exists (author
  edit, prior run, or a name collision with an author-authored skill), the seed
  must not overwrite it — consistent with the constitution/early-stage no-clobber
  policy.
- **Partial skill directory**: If some `fs-gg-sdd-*` skills exist and others do
  not, seeding fills only the missing ones without disturbing the present ones.
- **Scaffold vs init parity**: Because `scaffold` reuses the same skeleton seam as
  `init`, both paths must deliver the identical skill set — there is no path that
  receives the skeleton but not the skills.
- **Boundary preservation**: The skills must be produced by SDD's own seam; the
  external template provider must never be the source of these SDD-process skills
  (SDD↔provider boundary).
- **Skill set drift**: If the authored skill set in the FS.GG.SDD repo diverges
  from what is seeded, the divergence must be detectable (a parity/drift guard),
  so seeded content does not silently go stale relative to the source skills.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The skeleton-seeding command MUST emit the declared `fs-gg-sdd-*`
  process skill set into the seeded product so a scaffolded product's agent can
  discover the SDD lifecycle process.
- **FR-002**: Each seeded skill MUST be delivered for both supported agent
  surfaces (Claude and Codex), with equivalent content on each surface.
- **FR-003**: The seeded skills MUST be marked as authored, SDD-owned skeleton
  artifacts — the same ownership class as the seeded constitution and early-stage
  guidance — and MUST NOT be marked as externally-owned generated product output.
- **FR-004**: Re-running the seeding command MUST NOT overwrite any existing
  target skill file (no-clobber), matching the constitution/early-stage policy.
- **FR-005**: The currency/regeneration generator (`refresh`) MUST preserve the
  seeded skills and MUST NOT treat them as regenerable generated product output.
- **FR-006**: The seeding command MUST remain deterministic and byte-identical
  across runs and machines; seeded skill bodies MUST contain no dates or other
  run-varying content.
- **FR-007**: `scaffold` and `init` MUST deliver the identical skill set through
  the single shared skeleton seam; no skeleton-seeding path may omit the skills.
- **FR-008**: The SDD-process skills MUST be produced by SDD itself and MUST NOT
  be sourced from, or delegated to, the external template provider.
- **FR-009**: The release/skeleton-shape conformance surface MUST account for the
  newly seeded skill files so the produced skeleton's declared shape stays
  authoritative and verified.
- **FR-010**: A parity/drift guard MUST detect divergence between the authored
  `fs-gg-sdd-*` skill set (the source of truth in the FS.GG.SDD repo) and the
  content the seeding command emits, so seeded skills cannot silently go stale.

### Key Entities *(include if feature involves data)*

- **SDD-process skill set**: The collection of `fs-gg-sdd-*` skills that describe
  the SDD process (the per-stage lifecycle skills plus the cross-cutting process
  skills), each consisting of a stable skill name and an authored body, delivered
  per agent surface.
- **Seeded skeleton skill artifact**: A single seeded skill file at a stable path
  under the product's per-agent skill directory, owned as authored SDD skeleton
  (no-clobber, refresh-preserved), one per (skill, agent surface) pair.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After seeding into an empty directory, 100% of the declared
  `fs-gg-sdd-*` process skills are present and non-empty for each supported agent
  surface, via both `init` and `scaffold`.
- **SC-002**: A product author obtains the full SDD-process skill set with a
  single seeding command and zero manual copy steps out of the FS.GG.SDD repo.
- **SC-003**: Re-running the seeding command on a product whose seeded skills were
  edited preserves 100% of those edits (zero overwrites).
- **SC-004**: Two seeding runs of the same input produce byte-identical skill
  files (0 differing bytes), and the Claude and Codex skill sets are equivalent
  (every declared skill present on both surfaces).
- **SC-005**: The drift guard fails when the seeded content diverges from the
  authored source skill set, so divergence is caught before release rather than
  shipped silently.

## Assumptions

- **Skill-set scope**: The declared set is the consumer-relevant `fs-gg-sdd-*`
  process skills — the per-stage lifecycle skills (charter, specify, clarify,
  checklist, plan, tasks, analyze, evidence, verify, ship) plus the cross-cutting
  process skills (lifecycle map, getting-started, authoring-contracts,
  refresh-agents, validate). The `fs-gg-sdd-project` skill (which is about
  developing the FS.GG.SDD product itself, not using SDD inside a consumer
  product) is assumed **out of scope** for seeded products. The exact membership
  of the set is a good `/speckit-clarify` candidate.
- **Authoring-as-contract**: Per the issue and the constitution, the skill bodies
  are captured as a feature contract and transcribed into static content within
  the existing skeleton-seeding seam (the constitution/early-stage-guidance
  precedent), rather than read at runtime from the FS.GG.SDD repo. This keeps the
  seam self-contained and deterministic.
- **Codex skill location**: Codex skills are delivered under the established
  per-agent skill directory convention used by the rest of the seeded agent
  surfaces; the exact directory layout follows existing repo convention.
- **Single seam**: `init` is the single authoritative skeleton-seeding seam and
  `scaffold` reuses it unchanged, so adding the skills there reaches all paths;
  `init` byte-identicality is preserved aside from the additive skill files.
- **No provider routing**: Delivery does not pass through the FS.GG.Rendering
  template provider, preserving the SDD↔provider boundary.
- **Parity precedent**: A skill-parity/drift test does not exist in SDD today; the
  feature introduces one, mirroring the parity guard used in FS.GG.Rendering.
