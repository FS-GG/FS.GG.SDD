# Feature Specification: Scaffold co-tenant skills under the shared skill roots

**Feature Branch**: `055-scaffold-cotenant-skills`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "start the next sdd item on the coord board" → Coordination board item FS-GG/FS.GG.SDD#55: *scaffold guard over-matches: rejects provider co-tenant skills in `.claude/skills/` (blocks fs-gg-ui Feature 219)*

## Overview

`fsgg-sdd scaffold` seeds an SDD skeleton (including the 15 `fs-gg-sdd-*` process
skills under `.claude/skills/` and `.codex/skills/`), then invokes an external
template provider and treats anything the provider writes into an SDD-owned tree
as an intrusion (`scaffold.providerWroteSddTree`, exit 2). The intrusion guard
currently claims the **entire** `.claude/skills/` and `.codex/skills/` prefixes
as SDD-owned. A compliant rendering provider (FS.GG.Rendering Feature 219) must
emit its own UI skills (`fs-gg-elmish`, `fs-gg-scene`, `fs-gg-ui-widgets`, …)
into `.claude/skills/`, so every real scaffold blocks even though those skills
occupy a namespace disjoint from `fs-gg-sdd-*`. This feature narrows the guard so
the two contracts can coexist: SDD reserves only the `fs-gg-sdd-*` skill
namespace, and a provider may co-populate the rest of the shared skill roots as
product.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compliant provider scaffold completes with co-tenant skills (Priority: P1)

A product author runs `fsgg-sdd scaffold --provider rendering --param lifecycle=sdd`
in an empty directory. The provider emits both the SDD process skills' siblings
(its own UI skills) into `.claude/skills/`. The author expects a buildable,
runnable, SDD-managed product — not a blocked scaffold.

**Why this priority**: This is the whole point of the item — today every real
`fs-gg-ui` scaffold blocks (`outcome: blocked`, `scaffold.outcome: providerFailed`),
making SDD-lifecycle rendering products unscaffoldable. Without this, the
cross-repo composition path is broken end to end.

**Independent Test**: Run scaffold with a provider whose product writes one or
more skills under `.claude/skills/` outside the `fs-gg-sdd-*` namespace; confirm
the scaffold reports success (exit 0), the provider skills land on disk, and the
seeded `fs-gg-sdd-*` skills remain intact.

**Acceptance Scenarios**:

1. **Given** an empty directory and a provider that writes `.claude/skills/fs-gg-elmish/SKILL.md`, **When** the author runs scaffold, **Then** the run completes with a success outcome (exit 0), no `scaffold.providerWroteSddTree` diagnostic is emitted, and the provider skill exists on disk alongside the seeded `fs-gg-sdd-*` skills.
2. **Given** the same run, **When** the scaffold provenance and report are produced, **Then** the provider skill path is listed as provider-produced (`generatedProduct`) product, while the seeded `fs-gg-sdd-*` skill paths are not listed as product.
3. **Given** the same run, **When** the seeded `fs-gg-sdd-*` skills are inspected, **Then** they are byte-identical to what `fsgg-sdd init` seeds (co-tenancy did not clobber them).

---

### User Story 2 - Genuine SDD-tree intrusion is still rejected (Priority: P1)

A defective or malicious provider writes into a tree SDD owns — including a
`fs-gg-sdd-*` skill subtree, `.fsgg/`, `work/`, or `readiness/`. The author (or
CI) expects the scaffold to fail loudly and to never report an incomplete
scaffold as complete.

**Why this priority**: Narrowing the guard must not open a hole. FR-008/FR-011
still require that SDD process skills are produced by SDD alone and that no
provider writes into SDD-owned trees. The safety property is as important as the
co-tenancy it enables.

**Independent Test**: Run scaffold with a provider that writes into a
`fs-gg-sdd-*` skill subtree (and, separately, into `.fsgg/`); confirm each is
rejected as an intrusion at exit 2 with `scaffold.providerWroteSddTree`, and the
scaffold is reported failed.

**Acceptance Scenarios**:

1. **Given** a provider that writes `.claude/skills/fs-gg-sdd-lifecycle/SKILL.md`, **When** scaffold runs, **Then** it is rejected as an intrusion (`scaffold.providerWroteSddTree`, exit 2) and the outcome is `providerFailed`.
2. **Given** a provider that writes `.codex/skills/fs-gg-sdd-anything/SKILL.md`, **When** scaffold runs, **Then** it is rejected identically (symmetric across the two skill roots).
3. **Given** a provider that writes into `.fsgg/`, `work/`, or `readiness/`, **When** scaffold runs, **Then** it is rejected as an intrusion exactly as before this change.

---

### User Story 3 - Auditable, unchanged report/provenance contract (Priority: P2)

A downstream automation consumer (Governance handoff, release catalog tooling)
reads `scaffold-provenance.json` and the scaffold report. It expects the JSON
automation contract and the persisted schema to be unchanged except for the
additive appearance of the now-permitted provider skill paths among produced
product.

**Why this priority**: The provenance record and report are versioned cross-repo
surfaces. This change must be additive — no schema bump, no removed or reshaped
fields — so consumers are not forced to re-pin.

**Independent Test**: Diff the JSON report and provenance for an equivalent
scaffold before/after; confirm the only differences are additional produced-path
entries, and the provenance `schemaVersion` is still v1.

**Acceptance Scenarios**:

1. **Given** a successful co-tenant scaffold, **When** provenance is written, **Then** its schema version is unchanged (v1) and provider skill paths appear as `generatedProduct`.
2. **Given** the three report projections (json/text/rich), **When** they render, **Then** each lists the provider skill paths under produced product and none lists them as SDD-owned.

---

### Edge Cases

- A provider writes a skill whose name *begins* with `fs-gg-sdd-` but is not one SDD seeds (e.g. `.claude/skills/fs-gg-sdd-custom/`) → treated as a reserved-namespace intrusion (the namespace is reserved for SDD, not just the currently-seeded set), so the write is rejected.
- A provider writes a skill directly at the skill root with no subdirectory (e.g. a bare file `.claude/skills/leak`) → outside the `fs-gg-sdd-*` namespace, so it is now provider product, not an intrusion. (The existing intrusion fixture, which exercised exactly this shape, must be re-pointed to a genuine `fs-gg-sdd-*` collision to keep testing the guard — see Assumptions.)
- A provider re-writes one of the *already-seeded* `fs-gg-sdd-*` skills **at its existing seeded path** → the produced-path diff (`afterSet − beforePaths − skeletonFiles`) subtracts the seeded path before the intrusion partition, so this guard does not surface an in-place rewrite; the seeded skill's protection against clobber is the `AgentGuidanceTarget` no-clobber write-kind (unchanged by this feature), not the intrusion discriminator. What the reserved-namespace guard *does* catch is a write to a **new** `fs-gg-sdd-*` path SDD does not seed (previous bullet) — which is why the re-pointed negative fixture targets a new `fs-gg-sdd-custom` path (FR-009).
- Only one of the two skill roots receives a co-tenant write (provider targets `.claude/skills/` but not `.codex/skills/`) → permitted; the guard treats the two roots identically and does not require symmetry from the provider.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Scaffold MUST permit an external provider to write skill directories under `.claude/skills/` and `.codex/skills/` whose top-level skill name is **outside** the reserved `fs-gg-sdd-*` namespace, without flagging them as an intrusion.
- **FR-002**: Scaffold MUST continue to reject, as a `scaffold.providerWroteSddTree` intrusion, any provider write whose path targets a `fs-gg-sdd-*` skill subtree under `.claude/skills/` or `.codex/skills/`, preserving FR-008 (SDD process skills produced by SDD alone) and FR-011 (no provider write into SDD-owned trees).
- **FR-003**: Scaffold MUST continue to reject provider writes into the other SDD-owned trees (`.fsgg/`, `work/`, `readiness/`) and MUST continue to exclude the seeded skeleton files and provenance path from produced product, unchanged by this feature.
- **FR-004**: Permitted provider co-tenant skill paths MUST be recorded as provider product (`generatedProduct` provenance) in `scaffold-provenance.json` and listed among produced paths in all three report projections; the seeded `fs-gg-sdd-*` skill paths MUST remain excluded from produced product and provenance.
- **FR-005**: A provider co-populating the shared skill roots MUST NOT clobber the seeded `fs-gg-sdd-*` skills, and the seeded skills MUST NOT clobber provider co-tenant skills; the seeded skills remain byte-equivalent to the `fsgg-sdd init` seeding.
- **FR-006**: A scaffold whose only writes under the shared skill roots are permitted co-tenant skills MUST report a success outcome and exit 0 (not `blocked` / `providerFailed`).
- **FR-007**: The co-tenancy and reservation rules MUST apply symmetrically to `.claude/skills/` and `.codex/skills/`.
- **FR-008**: This feature MUST NOT change any persisted schema version (scaffold-provenance stays v1) and MUST keep the JSON automation contract additive — no removed, renamed, or reshaped fields.
- **FR-009**: The guard's existing negative-path test coverage MUST be preserved by re-pointing the current skills-intrusion fixture to a genuine SDD-owned skill collision (a `fs-gg-sdd-*` path), so a real intrusion is still exercised after the guard is narrowed.
- **FR-010**: `init` behavior MUST remain byte-identical — scaffold reuses `init`'s effects for the skeleton, and this feature changes only the intrusion discriminator, not the seeded skeleton.
- **FR-011**: An incomplete or intruded scaffold MUST still never be reported as complete (the FR-009 safety property of the scaffold feature is retained).

### Key Entities

- **Reserved SDD skill namespace**: the `fs-gg-sdd-*` skill subtrees under each shared skill root (`.claude/skills/`, `.codex/skills/`) that SDD seeds and owns; no provider may write into them.
- **Provider co-tenant skill**: a provider-produced skill directory under a shared skill root whose name is outside the reserved namespace; classified as provider product.
- **Scaffold intrusion discriminator**: the rule that partitions provider-produced paths into intrusions (rejected) vs product (recorded), narrowed by this feature for the shared skill roots.
- **Scaffold provenance record**: the v1 `.fsgg/scaffold-provenance.json` that marks produced paths `generatedProduct`; gains additional co-tenant skill entries but no schema change.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A compliant `fs-gg-ui` provider scaffold with `lifecycle=sdd` that previously blocked with 8 leaked `.claude/skills/` paths now completes with a success outcome, exit 0, and **zero** intrusions flagged.
- **SC-002**: 100% of the seeded `fs-gg-sdd-*` skills survive a co-tenant scaffold byte-identical to the `fsgg-sdd init` seeding.
- **SC-003**: A provider write to any `fs-gg-sdd-*` skill subtree (either root) is still rejected 100% of the time at exit 2 with `scaffold.providerWroteSddTree`, and writes to `.fsgg/`/`work/`/`readiness/` remain rejected.
- **SC-004**: The scaffold JSON report and provenance for an equivalent scaffold differ from the pre-change output only by additive produced-path entries, with the provenance schema version unchanged (v1).

## Assumptions

- **Namespace reservation is by `fs-gg-sdd-*` name-prefix under each skill root**, chosen over an exact-collision-with-the-currently-seeded-set discriminator. The prefix reservation is forward-compatible (it protects `fs-gg-sdd-*` names SDD may seed in the future) and matches the guard's stated ownership scope. This is the crux the issue flags as "worth deciding here"; if `/speckit-clarify` prefers exact-collision semantics, FR-001/FR-002 adjust accordingly. Either way, co-tenant non-`fs-gg-sdd-*` skills are permitted.
- The current `skills-intrusion` fixture targets `.claude/skills/leak` / `.codex/skills/leak`, which under the narrowed guard become legitimate product; the fixture is therefore re-pointed to a `fs-gg-sdd-*` collision (FR-009) rather than deleted, so guard coverage is retained.
- The `.codex` mirror is in scope for the guard rule (both roots treated identically). Whether the rendering provider actually mirrors its UI skills into `.codex/skills/` is the provider's choice and out of scope for this SDD-side change; the "`.codex` asymmetry" question the issue raises is resolved on the SDD side by treating both roots symmetrically.
- No registry/ADR change is strictly required to ship the code fix, but recording the co-tenant `.claude/skills/` ownership model in the cross-repo coherence set (and, if it is a durable cross-repo choice, an ADR) is an expected follow-up per the issue's coherence note; that recording is tracked with the cross-repo companion FS-GG/FS.GG.Templates#47 and is out of scope for the code change itself.
- Existing scaffold reporting, exit-code semantics, and the git-init/chmod post-instantiation steps are unchanged.

## Dependencies

- Cross-repo companion: FS-GG/FS.GG.Templates#47 (validation of the `new-sdd-fullstack` scaffold path) and FS.GG.Rendering Feature 219 (`fs-gg-ui` emits framework skills to both `.agents/skills/` and `.claude/skills/`).
- Contract touched: `scaffold-provider` / `fs-gg-ui-template` (behavioral coherence only; no schema version change).
