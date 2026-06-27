# Feature Specification: SDD skeleton emits the lifecycle constitution at `.fsgg/constitution.md`

**Feature Branch**: `033-skeleton-constitution`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next non blocked sdd item on the project coordination board" — resolved to Coordination board item **"P2 · sdd — Implement constitution-ownership decision (ship F# lifecycle constitution in skeleton if P0 assigns to SDD)"** (phase **P2 SDD**), which was **Blocked** on the P0 gate *"Decision: constitution ownership for lifecycle=sdd products (Rendering vs SDD)"*. The gate was resolved this session in favour of **SDD** and recorded as **[ADR-0004](https://github.com/FS-GG/.github/blob/main/docs/adr/0004-constitution-ownership-for-lifecycle-sdd-products.md)** (`FS-GG/.github@02ba3c4`); the board item is now **Ready**, making it the next non-blocked SDD-owned item.

**Change Tier**: Tier 1 (contracted change) — this feature implements the SDD-side obligation of **ADR-0004**, which resolved the P0 gate left open in **ADR-0002 Decision 4**. It adds one new observable artifact to the SDD skeleton (`.fsgg/constitution.md`) and therefore **re-baselines the `init` byte-identical invariant**. It changes no provider contract, no provider invocation protocol, and no `scaffold-provenance.json` schema; the new artifact is generic SDD skeleton content, free of any provider-, template-, or rendering-specific knowledge.

## Context & Boundary

`fsgg-sdd init` establishes the SDD skeleton — today the `.fsgg/`-namespaced
config tree (`.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`), the
`work/` and `readiness/` roots, and the `CLAUDE.md` / `AGENTS.md` guidance
targets. `fsgg-sdd scaffold` reuses `init`'s effects **unchanged** to lay the
same skeleton before invoking an external, app-only template provider.

The skeleton emits **no constitution today**. ADR-0002 Decision 4 deferred *which
repo* should ship the F# lifecycle constitution for `lifecycle=sdd` products
(Rendering vs SDD) to a P0 gate. ADR-0004 resolves that gate: **SDD owns and
ships it**, at **`.fsgg/constitution.md`**, reusing the existing SDD skeleton
namespace — because `init` already owns that skeleton and the reference provider
is contractually app-only (it must never write into the SDD tree;
`providerWroteSddTree` is a provider defect). This is therefore **net-new
skeleton content**, not a relocation of an existing file.

This feature concerns **only** the SDD-owned `.fsgg/constitution.md`. The
provider/app-template's *own* `lifecycle`-gated Spec Kit emission (the `.specify/`
tree and any constitution it carries, per ADR-0002 Decision 2) is owned by
FS.GG.Rendering and is **out of scope and unchanged** here. The `.specify/` tree
visible in the FS.GG.SDD repo itself is standard Spec Kit dogfooding and is not
what the SDD product emits.

Because `scaffold` reuses `init`'s effects, satisfying `init` automatically
delivers the constitution on the scaffold path as well, with no scaffold-specific
logic. As SDD skeleton content, the constitution is **not** a `generatedProduct`
path: it must not appear in scaffold's app-only provenance, and `refresh` must not
treat it as externally owned.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - `init` lays down a ready-to-use lifecycle constitution (Priority: P1)

A product author runs `fsgg-sdd init` in an empty directory. Alongside the rest
of the skeleton, SDD writes `.fsgg/constitution.md` — a populated, valid starting
constitution for an SDD-managed product — so the author begins the lifecycle with
governing principles already in place rather than an absent or empty file.

**Why this priority**: This is the entire feature and the ADR-0004 obligation.
Without it the skeleton ships no constitution, every SDD-managed product starts
ungoverned, and the P2 SDD item cannot close. The other stories are consequences
of getting this one right.

**Independent Test**: Run `fsgg-sdd init` in a temporary empty directory and
confirm `.fsgg/constitution.md` exists, is non-empty, and is a structurally valid
constitution (recognizable title and principles), with no `[PLACEHOLDER]` tokens
left unfilled.

**Acceptance Scenarios**:

1. **Given** an empty directory, **When** `fsgg-sdd init` runs, **Then**
   `.fsgg/constitution.md` is created as part of the skeleton with populated
   constitution content and is reported among the artifacts the command produced.
2. **Given** the same empty directory, **When** `fsgg-sdd init` is run a second
   time on identical inputs, **Then** the emitted `.fsgg/constitution.md` bytes
   are identical to the first run (deterministic, no timestamps or volatile
   content).
3. **Given** an emitted constitution, **When** its content is inspected, **Then**
   it contains no FS.GG.SDD-repo-specific, provider-specific, template-specific,
   or rendering-specific text — it is generic to any SDD-managed product.

### User Story 2 - Scaffold delivers the constitution without polluting app-only provenance (Priority: P2)

A product author runs `fsgg-sdd scaffold --provider <name> --param lifecycle=sdd`.
The SDD skeleton — including `.fsgg/constitution.md` — is laid down via the reused
`init` effects, while the provider produces only the app tree. The constitution
appears in the product, but **not** among the `generatedProduct` paths recorded in
`.fsgg/scaffold-provenance.json`.

**Why this priority**: Correct ownership accounting is what makes the constitution
SDD-owned rather than externally owned. If it leaked into the app-only provenance,
`refresh` would treat author-authored governance as foreign generated product —
the exact boundary ADR-0004 exists to keep clean. It depends on Story 1 but is
separately observable.

**Independent Test**: Scaffold with any provider and `lifecycle=sdd` into a temp
directory; confirm `.fsgg/constitution.md` is present and that the
`generatedProduct` path set in `scaffold-provenance.json` does **not** include it.

**Acceptance Scenarios**:

1. **Given** a valid provider, **When** `fsgg-sdd scaffold --provider <name>`
   completes, **Then** `.fsgg/constitution.md` exists in the product and the
   scaffold report attributes it to the SDD skeleton, not to the provider.
2. **Given** the resulting `.fsgg/scaffold-provenance.json`, **When** its
   `generatedProduct` paths are read, **Then** `.fsgg/constitution.md` is absent
   from them.

### User Story 3 - Re-running and refreshing never clobbers an authored constitution (Priority: P3)

A product author edits `.fsgg/constitution.md` to ratify project-specific
principles, then later re-runs `fsgg-sdd init` or `fsgg-sdd refresh`. Their edits
survive: the skeleton seeds the constitution once and thereafter treats it as
authored content it neither overwrites nor regenerates.

**Why this priority**: A constitution the author cannot safely customize is worse
than none. This guarantees the artifact behaves like the other authored skeleton
files (`CLAUDE.md`, `AGENTS.md`) rather than a regenerable view, but it is a
robustness guarantee layered on the core emission in Story 1.

**Independent Test**: Init, modify `.fsgg/constitution.md`, re-run `init` and
`refresh`, and confirm the modified content is preserved (no overwrite, no
stale-view diagnostic treating it as a generated artifact).

**Acceptance Scenarios**:

1. **Given** an author-modified `.fsgg/constitution.md`, **When** `fsgg-sdd init`
   is re-run, **Then** the existing file is preserved under the same no-clobber
   policy applied to other authored skeleton files.
2. **Given** an author-modified `.fsgg/constitution.md`, **When** `fsgg-sdd
   refresh` runs, **Then** the constitution is left untouched and is not reported
   as a generated view, a stale view, or an externally-owned product path.

### Edge Cases

- **Constitution already present at init** (re-run or pre-seeded): treated as
  authored content and preserved, never silently overwritten (Story 3 / FR-008).
- **`lifecycle` other than `sdd`** passed to scaffold: the SDD skeleton (and thus
  `.fsgg/constitution.md`) is laid down regardless, because it is emitted by the
  always-run `init` effects, not gated on the provider's `lifecycle` parameter.
  The provider's own `lifecycle`-gated `.specify/` emission is separate and out of
  scope (see Context & Boundary / Assumptions).
- **Existing `init` golden / determinism fixtures**: must be re-baselined to
  include the new artifact; a stale fixture is a test-debt failure, not evidence
  of a regression (FR-006).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `fsgg-sdd init` MUST emit a constitution artifact at the fixed
  relative path `.fsgg/constitution.md` as part of the SDD skeleton it establishes.
- **FR-002**: The emitted constitution MUST be a populated, valid starting
  lifecycle constitution for an SDD-managed product — recognizable structure and
  principles, with no unfilled placeholder tokens — not an empty or stub file.
- **FR-003**: The constitution content MUST be generic: it MUST contain no
  FS.GG.SDD-repo-specific, provider-specific, template-specific, or
  rendering-specific names, paths, or URLs.
- **FR-004**: `fsgg-sdd scaffold` MUST deliver `.fsgg/constitution.md` via the
  **reused, unchanged** `init` skeleton effects — no scaffold-specific
  constitution logic and no change to the provider contract or invocation
  protocol.
- **FR-005**: `.fsgg/constitution.md` MUST NOT be recorded as a `generatedProduct`
  path in `.fsgg/scaffold-provenance.json`; the app-only provenance and its
  schema MUST be unaffected by this feature.
- **FR-006**: Adding the constitution MUST re-baseline the `init` byte-identical
  invariant: the new `init` output (now including `.fsgg/constitution.md`) becomes
  the baseline, and all existing `init`/`scaffold` golden, determinism, and
  skeleton-set contracts MUST be re-baselined to include exactly this one new
  artifact and continue to pass — whether by an explicit fixture edit or by a
  contract that enumerates the skeleton dynamically and self-adjusts. Where a
  contract self-adjusts, no fixture edit is required and that is the re-baseline;
  a contract that does **not** self-adjust MUST be edited.
- **FR-007**: The constitution emission MUST be deterministic — byte-identical
  across repeated runs and across machines for a given project — consistent with
  the other skeleton files (no timestamps, randomness, or environment-derived
  content).
- **FR-008**: When `.fsgg/constitution.md` already exists, `fsgg-sdd init` MUST
  preserve it under the same no-clobber overwrite policy applied to authored
  skeleton files (it MUST NOT silently overwrite author edits).
- **FR-009**: `fsgg-sdd refresh` MUST treat `.fsgg/constitution.md` as authored
  content it neither regenerates nor reports as a generated view, a stale view, or
  an externally-owned product path.
- **FR-010**: The command report MUST attribute `.fsgg/constitution.md` to the SDD
  skeleton (the same surface that reports `project.yml` / `sdd.yml` / `agents.yml`
  / `CLAUDE.md` / `AGENTS.md`), so its provenance is observable to the author.

### Key Entities *(include if feature involves data)*

- **Lifecycle constitution (`.fsgg/constitution.md`)**: the SDD-owned, authored
  Markdown artifact stating the governing principles for an SDD-managed product's
  spec-driven lifecycle. Seeded once by the skeleton, thereafter author-owned.
  Classified as authored skeleton content (the plan selects the precise
  `ArtifactWriteKind` — e.g. `AuthoredSource` / `AgentGuidanceTarget` — and
  overwrite policy `RefuseUnsafe`), distinct from `GeneratedView` artifacts and
  from `generatedProduct` provider output.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After `fsgg-sdd init` in an empty directory, `.fsgg/constitution.md`
  exists and is a non-empty, placeholder-free constitution in 100% of runs.
- **SC-002**: After `fsgg-sdd scaffold --provider <name> --param lifecycle=sdd`,
  `.fsgg/constitution.md` is present in the product and absent from the
  `generatedProduct` paths in `scaffold-provenance.json` in 100% of runs.
- **SC-003**: Two consecutive `fsgg-sdd init` runs on identical inputs produce
  byte-identical `.fsgg/constitution.md` (determinism holds).
- **SC-004**: An author edit to `.fsgg/constitution.md` survives a subsequent
  `init` and `refresh` with zero unintended modifications.
- **SC-005**: 100% of the re-baselined `init`/`scaffold` contract, golden, and
  determinism tests pass, with the only skeleton-set delta being the single new
  `.fsgg/constitution.md` artifact.
- **SC-006**: Zero provider-, template-, or rendering-specific strings appear in
  the emitted constitution (generic-content check passes).

## Assumptions

- **Populated, not placeholder.** Consistent with the rest of the skeleton (which
  emits populated `project.yml` / `sdd.yml` / `agents.yml` and real guidance
  files, never blank stubs), the constitution ships as a populated, opinionated
  baseline suitable for an F# SDD-managed product, which the author then ratifies
  or amends. A bare fill-in template was considered and rejected for inconsistency
  with the skeleton convention; the exact baseline body is settled during
  planning.
- **Emitted by `init`, not gated on `lifecycle`.** The constitution is part of the
  always-run `init` skeleton effects, so it is present whenever SDD manages a
  product (`init` or `scaffold`). The provider's `lifecycle` parameter governs the
  *app template's own* Spec Kit emission (ADR-0002 Decision 2), which is a
  separate, Rendering-owned concern; reconciling a potential second
  template-emitted constitution when an author also opts the app into
  `spec-kit`/`sdd` is **out of scope** here and, if needed, tracked separately.
- **Namespace reuse.** Per ADR-0004 the file lives at `.fsgg/constitution.md`; no
  new `.sdd/` dotdir and no dependency on a `.specify/memory/` layout are
  introduced.
- **Authoring surface, not generated view.** The constitution is treated as
  authored content (no source digests, no generator version, not subject to
  stale-view diagnostics), so `refresh` leaves it alone.
- **Baseline move is intended.** The CLAUDE.md "`init` stays byte-identical"
  invariant refers to stability across releases for a fixed skeleton; this feature
  deliberately moves that baseline once to add the constitution, after which
  byte-identical stability resumes.
