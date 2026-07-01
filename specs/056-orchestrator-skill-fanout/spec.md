# Feature Specification: Orchestrator skill fan-out — union SDD + provider skills into all three agent roots

**Feature Branch**: `056-orchestrator-skill-fanout`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "orchestrator skill fan-out: union SDD + provider skills into
.claude/.codex/.agents, keep guard strict"

## Context

Resolves the coordination decision recorded on **FS-GG/FS.GG.SDD#55** (**Option A —
orchestrator-owned fan-out**, cited as ADR-0011; the ADR file itself is owned separately and
still pending in `FS-GG/.github`). The decision **supersedes** the guard-narrowing approach
attempted in the reverted feature 055: `isSddTree` stays **strict**, and instead `fsgg-sdd` —
the orchestrator (ADR-0008 / ADR-0009) — becomes the **sole mirror authority** that populates
every agent-skill root with the same skill set.

Today an FS.GG product has two competing conventions: SDD seeds its `fs-gg-sdd-*` process
skills into `.claude/skills/` and `.codex/skills/`, while a rendering provider (FS.GG.Rendering
Feature 219) wants to ship its own `fs-gg-*` UI skills. A provider that writes into
`.claude/skills/` is (correctly) rejected as `scaffold.providerWroteSddTree`, and no single
root carries *both* skill families — so a Claude runtime and a Codex runtime see different
skills, and a neutral `.agents/`-based runtime sees none.

This feature makes SDD fan out the **byte-identical union** of SDD process skills ∪ provider UI
skills into all three roots (`.claude/skills/`, `.codex/skills/`, and the new `.agents/skills/`),
so the runtimes are interchangeable. Providers stay confined to the neutral `.agents/skills/`
root and never write into `.claude/`·`.codex/`; the strict intrusion guard is retained (and
extended to reserve `.agents/skills/fs-gg-sdd-*` so a provider cannot clobber SDD skills in the
root it does write to).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A scaffolded product carries the same skills in every agent root (Priority: P1)

A product author runs `fsgg-sdd scaffold --provider <rendering>` against a provider that emits
its UI skills into `.agents/skills/`. After scaffold completes, all three agent-skill roots hold
the identical union of the seeded `fs-gg-sdd-*` process skills and the provider's `fs-gg-*` UI
skills, so whichever agent runtime the author uses (Claude, Codex, or a neutral `.agents`
runtime) discovers the same lifecycle + UI skills.

**Why this priority**: This is the whole point of the decision — interchangeable runtimes and an
unblocked `fs-gg-ui` scaffold (the reason #55 was filed). Without it, the compliant provider
scaffold either blocks or leaves runtimes divergent.

**Independent Test**: Scaffold against a fixture provider that writes `.agents/skills/fs-gg-elmish/`.
Confirm exit 0, and that `.claude/skills/`, `.codex/skills/`, and `.agents/skills/` each contain
byte-identical `fs-gg-sdd-*` **and** `fs-gg-elmish` skill trees.

**Acceptance Scenarios**:

1. **Given** a provider that writes `.agents/skills/fs-gg-elmish/SKILL.md`, **When** scaffold
   runs, **Then** exit 0, outcome `providerSucceeded`, and the same skill set (seeded
   `fs-gg-sdd-*` ∪ `fs-gg-elmish`) appears **byte-identical** under all three roots.
2. **Given** the same scaffold, **When** the report/provenance is read, **Then** every mirrored
   skill file is recorded and the fan-out is attributed to SDD (not laundered as unrelated
   provider product output).
3. **Given** a provider that produces **no** skills, **When** scaffold runs, **Then** all three
   roots carry the seeded `fs-gg-sdd-*` set byte-identically and no provider skill appears.

### User Story 2 - The intrusion guard stays strict and symmetric (Priority: P1)

A defective provider that writes into `.claude/skills/`, `.codex/skills/`, or the reserved
`fs-gg-sdd-*` namespace under `.agents/skills/` is still rejected as a provider defect.

**Why this priority**: The decision explicitly keeps the guard strict; the fan-out must not open
the hole feature 055 would have. SDD, not the provider, owns cross-root mirroring.

**Independent Test**: Scaffold against fixtures that write `.claude/skills/fs-gg-x/`,
`.codex/skills/fs-gg-x/`, and `.agents/skills/fs-gg-sdd-x/`; confirm each is rejected
(`scaffold.providerWroteSddTree`, exit 2) and none is mirrored or recorded as product.

**Acceptance Scenarios**:

1. **Given** a provider writing into `.claude/skills/` or `.codex/skills/`, **When** scaffold
   runs, **Then** exit 2, `scaffold.providerWroteSddTree`, no fan-out performed.
2. **Given** a provider writing `.agents/skills/fs-gg-sdd-custom/`, **When** scaffold runs,
   **Then** exit 2, `scaffold.providerWroteSddTree` (the reserved namespace is protected in the
   neutral root too).
3. **Given** a provider writing into `.fsgg/`·`work/`·`readiness/`, **When** scaffold runs,
   **Then** exit 2 (unchanged).

### User Story 3 - init seeds all three roots; refresh/upgrade bring them to currency (Priority: P2)

`fsgg-sdd init` seeds the `fs-gg-sdd-*` process skills into all three agent roots. `fsgg-sdd
refresh` re-mirrors the union to currency, and `fsgg-sdd doctor`/`upgrade` detect and reconcile a
product whose three roots have drifted out of the `claude ≡ codex ≡ agents = union` invariant
(e.g. scaffolded by an older CLI that only seeded two roots).

**Why this priority**: The invariant must hold for products created or maintained by any verb,
not just fresh scaffold — including existing products the fan-out CLI upgrades.

**Independent Test**: `init` a repo → all three roots carry the byte-identical `fs-gg-sdd-*` set.
Delete one root's copy → `doctor` reports the missing-root drift; `upgrade` re-seeds it no-clobber;
`refresh` re-mirrors the union.

**Acceptance Scenarios**:

1. **Given** an empty repo, **When** `init` runs, **Then** `.claude/skills/`, `.codex/skills/`,
   and `.agents/skills/` each carry the byte-identical `fs-gg-sdd-*` set.
2. **Given** a product missing the `.agents/skills/` copies, **When** `doctor` runs, **Then** it
   reports the drift read-only; **When** `upgrade` runs (confirmed), **Then** the missing root is
   re-seeded no-clobber and residual drift is zero.
3. **Given** a product whose authored sources changed, **When** `refresh` runs, **Then** the union
   is re-mirrored byte-identically to all three roots.

### Edge Cases

- **In-place rewrite of a seeded skill**: rewriting an *already-seeded* `fs-gg-sdd-*` skill at its
  existing path is diff-invisible (no-clobber `AgentGuidanceTarget`), unchanged here.
- **Provider skill name collides with a seeded `fs-gg-sdd-*` name in `.agents/skills/`**: rejected
  by the extended guard (reserved namespace), never silently overwritten.
- **A partial/older product** with only `.claude`+`.codex` seeded (pre-fan-out CLI): treated as
  drift by `doctor`/`upgrade`, not as corruption.
- **Byte-identity across roots**: the three copies must be byte-for-byte equal; a mismatch is
  drift, not an acceptable variant.
- **`.gitignore`/tooling that ignores `.agents/`**: the new root must be materialized on disk
  regardless of VCS ignore rules (out of SDD's control, but noted).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `isSddTree` MUST remain strict — a provider write into `.claude/skills/` or
  `.codex/skills/` is still an intrusion (`scaffold.providerWroteSddTree`, exit 2). No guard
  loosening.
- **FR-002**: The guard MUST also reserve the `fs-gg-sdd-*` namespace under `.agents/skills/` — a
  provider write into `.agents/skills/fs-gg-sdd-*` is an intrusion.
- **FR-003**: Providers MAY write non-reserved skills only into `.agents/skills/` (the neutral
  provider root); SDD, not the provider, mirrors them into `.claude/`·`.codex/`.
- **FR-004**: `fsgg-sdd init` MUST seed the `fs-gg-sdd-*` process skills into all three roots
  (`.claude/skills/`, `.codex/skills/`, `.agents/skills/`) byte-identically, reusing the
  no-clobber `AgentGuidanceTarget` write-kind.
- **FR-005**: After a successful provider invocation, `scaffold` MUST compute the **union** of the
  seeded `fs-gg-sdd-*` bodies and the provider's produced `.agents/skills/*` skills, and write
  byte-identical copies of that union into all three roots.
- **FR-006**: The mirror MUST be byte-identical across the three roots for every skill in the
  union (`claude ≡ codex ≡ agents = union`).
- **FR-007**: `scaffold-provenance.json` MUST record the mirrored skill files. Resolved (research
  R4, data-model §Provenance shape): an **additive** optional `mirroredPaths` array (each entry
  owner `mirrored`), schema **stays v1** — no schema bump; absent/null parses to `[]`.
- **FR-008**: The provenance/agent-surface **drift guard** MUST be extended from `claude ≡ codex`
  to `claude ≡ codex ≡ agents = union`, so a divergent or missing root is detected.
- **FR-009**: `fsgg-sdd refresh` MUST re-mirror the union to currency across all three roots.
- **FR-010**: `fsgg-sdd doctor` MUST report (read-only) a product whose three roots violate the
  invariant, and `fsgg-sdd upgrade` MUST reconcile it via no-clobber re-seed/re-mirror.
- **FR-011**: The fan-out grows the CLI's seeded-artifact surface, so it MUST advance the
  orchestrator-axis minimum CLI version (ADR-0008); the CLI release sequences **before** a clean
  scaffold consumes it.
- **FR-012**: An incomplete fan-out MUST NOT be reported as complete. A mirror I/O failure (a
  `ReadFile`/`WriteFile` fault during the post-instantiation mirror stage) surfaces the
  `scaffold.mirrorFailed` diagnostic and finalizes as a **non-success** scaffold at **exit 2** — the
  existing tool-defect class (the same exit code as `providerWroteSddTree`/`providerFailed`). No new
  outcome or exit code is introduced (the diagnostic id is additive observability only); the outcome
  is never `providerSucceeded` and the report/provenance never records the fan-out as complete.
- **FR-013**: Claude and Codex agent-surface guidance MUST be updated equivalently to describe the
  three-root union model (Principle VII).

### Key Entities

- **Agent-skill root**: one of `.claude/skills/`, `.codex/skills/`, `.agents/skills/` — a
  directory that must carry the union skill set.
- **Skill union**: the set `{ seeded fs-gg-sdd-* bodies } ∪ { provider-produced .agents/skills/* }`,
  each identified by skill name, mirrored byte-identically to every root.
- **Fan-out record**: the provenance entry(ies) attributing each mirrored file to the SDD
  orchestrator (not provider product).
- **Three-root drift**: a `doctor`/`upgrade` finding that a product violates
  `claude ≡ codex ≡ agents = union`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a compliant scaffold, all three agent-skill roots contain the byte-identical
  union of seeded `fs-gg-sdd-*` and provider `fs-gg-*` skills (0 byte differences across roots).
- **SC-002**: A provider write into `.claude/skills/`, `.codex/skills/`, or
  `.agents/skills/fs-gg-sdd-*` is rejected 100% of the time (exit 2, `providerWroteSddTree`).
- **SC-003**: `fsgg-sdd init` produces the `fs-gg-sdd-*` set in all three roots byte-identically;
  two runs are deterministic and byte-stable.
- **SC-004**: `doctor` detects a missing/ divergent third root and `upgrade` reconciles it to zero
  residual drift, no-clobber (authored edits preserved).
- **SC-005**: The provenance/report change is versioned coherently (additive or a declared bump),
  and the drift guard proves the three-root invariant.

## Assumptions

- The neutral provider root is **`.agents/skills/`**; providers (FS.GG.Rendering) stop writing
  into `.claude/`·`.codex/` (their side is tracked by **FS-GG/FS.GG.Templates#47**).
- **ADR-0011** (the formal decision record) is owned and written **separately** by the user in
  `FS-GG/.github`; this spec proceeds referencing it as pending and treats the #55 comment as the
  decision of record for detail.
- The union's provider members are discovered from the provider's produced `.agents/skills/*` set
  (the diff already computed by scaffold), not from any provider-specific manifest — SDD embeds no
  provider-specific skill name.
- Byte-identity reuses the existing embedded-resource seeding and `AgentGuidanceTarget` no-clobber
  semantics (`SeededSkills.fs`); one canonical body per skill fans out to all roots.
- The reference provider (a full runnable UI app emitting `.agents/skills/`) ships in
  FS.GG.Rendering; no Rendering-specific knowledge enters generic SDD.
- Cross-repo: this is behavioral coherence on the `scaffold-provider` contract plus the
  orchestrator version axis (ADR-0008); the coherence-set/ADR follow-up is tracked by
  **FS-GG/FS.GG.Templates#47**, and **#55** stays closed (this feature *implements* its decision).
