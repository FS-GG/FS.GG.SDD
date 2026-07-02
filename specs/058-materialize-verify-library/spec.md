# Feature Specification: One materialize-and-verify library + content-aware skill drift

**Feature Branch**: `058-materialize-verify-library`

**Created**: 2026-07-02

**Input**: User description: "P1 (ADR-0014): collapse the three SDD skill fan-out
implementations into one shared `mirror`/`verify` library in `FS.GG.Contracts`; route
scaffold/refresh through it (byte-identical output); make doctor/upgrade content-aware over
process **and** product skills. Advisory first."

## Context

Resolves **FS-GG/FS.GG.SDD#61** (Phase **P1** of the skill-vendoring robustness epic
**FS-GG/.github#110**, decided in **ADR-0014**, extending ADR-0011). ADR-0014 replaces four
hand-maintained "materialize union → 3 roots" mirror implementations with **one**
content-addressed algorithm. Feature 057 (P0.D0.2) delivered the contract *shapes* —
`SkillManifest`/`SkillManifestEntry`/`SkillScope`, the `agentSkillRoots` constant, and the
additive per-path `Sha256` on `scaffold-provenance`. This feature delivers the *library and the
routing*: the pure `mirror`/`verify` functions, the reroute of every SDD lane through them, and
the content-addressed drift the audit's **F2** found missing.

Today three SDD implementations each hand-roll the same idea and each hardcodes the three roots:

- `HandlersScaffold.fs` — the post-instantiation provider-skill fan-out into `.claude`/`.codex`.
- `HandlersRefresh.fs` — the re-mirror on refresh.
- `Drift.fs` — the `doctor`/`upgrade` expected-artifact set (**presence-only**, seeded skills
  only; no content check, no provider skills).

And `SeededSkills.fs` fans one seeded body into three hardcoded roots. ADR-0014 §Decision 5 makes
the root set **one declared constant**; §Decision 2/3 make the algorithm **one shared,
content-addressed library**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One library materializes the union into every root (Priority: P1)

Every lane that writes skills — seeded fan-out, scaffold provider mirror, refresh re-mirror —
computes its destinations from the one `agentSkillRoots` constant through one shared `mirror`
function, instead of four hardcoded root lists.

**Why this priority**: ADR-0014 §Decision 2/5. Collapsing the implementations is the point of P1;
without it the invariant stays spread across four divergent code paths.

**Independent Test**: In `FS.GG.Contracts.Tests`, call `SkillMirror.mirror agentSkillRoots
[("s", "body")]` and assert it yields one write per root at `<root>/skills/s/SKILL.md` with the
same body; scaffold/refresh golden tests assert the materialized skill files are **byte-identical**
to today across all three roots.

### User Story 2 - Content-addressed drift detects divergence and skill loss (Priority: P1)

`doctor`/`upgrade` assert, for **every** skill in the union — process (SDD-seeded) **and** product
(provider) — that each copy is (a) present in each root, (b) byte-identical across roots, and (c)
matches its canonical `sha256`. A byte-drifted `.claude` copy, a provider skill missing from one
root, or a `.codex` that diverges from `.claude` are all detected; today they are invisible.

**Why this priority**: ADR-0014 §Decision 3 / audit F2 — "the apparatus that exists to guarantee
the three roots are the byte-identical union does not check that they are." This is the core of #61.

**Independent Test**: Scaffold a product with a provider skill; edit one root's copy of a skill
(content divergence) and delete another root's copy of the provider skill (skill loss); run
`doctor` and assert both are reported (not coherent) — the **red test** the Done criterion names.

### User Story 3 - Content-addressed provenance (Priority: P2)

`scaffold-provenance.json` records the `sha256` of every produced/mirrored skill copy (the field
057 added, now populated), so a later `verify` can hash-match a provider copy against the digest
recorded at scaffold time.

**Why this priority**: ADR-0014 §Decision 3. The recorded digest is the canonical reference for
product-skill hash-match in `verify`; process skills reference the embedded manifest digest.

**Independent Test**: Scaffold with a provider skill; parse provenance and assert every skill path
under `producedPaths`/`mirroredPaths` carries a non-empty `sha256` equal to the digest of the
materialized bytes; a non-skill produced path carries none.

### Edge Cases

- A provider that emits **no** skills: `mirror` yields no provider writes; the seeded set is still
  materialized to all three roots; drift is coherent. (Byte-identical to today.)
- A **pre-056 product** (third `.agents` root absent): the missing copies are detected as
  missing-in-a-root and re-seeded by `upgrade` (existing no-clobber behavior, unchanged).
- A **present-but-divergent** copy (wrong bytes): detected by `verify` and reported advisory; it is
  **not** clobbered in P1 (no-clobber invariant retained — enforcement/auto-repair is P4). Advisory
  first (roadmap §P1.S1.3).
- A product skill recorded in provenance **without** a digest (a pre-P1 provenance): hash-match is
  skipped for it; presence and cross-root byte-identity are still checked.
- Non-skill expected artifacts (`.fsgg/early-stage-guidance.md`): unchanged presence-only drift.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `FS.GG.Contracts` MUST publish a pure, BCL-only `Fsgg.SkillMirror` module with
  `mirror` (union × roots → writes) and `verify` (roots × expected × actual → drift), plus the
  content helpers (`sha256`, `skillPath`, `mirrorTargetRoots`, `retargetSkillPath`,
  `providerSourceRoot`, `skillIdOfPath`), all deriving their root set from `agentSkillRoots`.
- **FR-002**: The seeded fan-out (`SeededSkills.fs`) MUST compute its three-root destinations from
  `agentSkillRoots` via `SkillMirror`, deleting the hardcoded root list; the emitted `WriteFile`
  effects (paths, bodies, `AgentGuidanceTarget` kind, order) MUST be unchanged.
- **FR-003**: The scaffold provider fan-out (`HandlersScaffold.fs`) MUST compute its mirror targets
  from `SkillMirror.mirrorTargetRoots agentSkillRoots` and materialize via `SkillMirror`, deleting
  the bespoke `mirrorTargetsFor`/root strings; the materialized skill files and `mirroredPaths`
  MUST be byte-identical to today.
- **FR-004**: The refresh re-mirror (`HandlersRefresh.fs`) MUST route through the same
  `SkillMirror` targets, deleting its inline `.claude`/`.codex` writes; output byte-identical.
- **FR-005**: `scaffold-provenance` per-path `Sha256` MUST be populated for every produced
  `.agents/skills/*` skill and every mirrored copy, computed by `SkillMirror.sha256`; non-skill
  produced paths keep `Sha256 = None`. Serialization stays additive (`sha256` key emitted only when
  present); `scaffoldProvenanceVersion` stays `1`.
- **FR-006**: `Drift.compute` MUST be content-aware via `SkillMirror.verify` over the union of
  **process** skills (SDD-seeded) and **product** skills (provider), detecting missing-in-a-root,
  cross-root divergence, and hash-mismatch. Reference-digest policy reconciles ADR-0014 §Decision 3
  with ADR-0011's no-clobber "author-edit-preserved" invariant: **product** skills hash-match the
  **stable seed-time digest recorded in provenance** (detects tampering even across identical
  roots, no cross-version volatility); **process** skills carry **no** reference digest and verify
  by presence + cross-root byte-identity only (a consistent consumer edit to every seeded root is
  preserved as coherent; only an inconsistent edit or a missing copy is drift — hash-matching the
  running binary's embedded body would flag every prior scaffold after any skill-text change).
  Product-skill ids MUST be confined to the provider source root's recorded skill copies
  (`.agents/skills/<id>/SKILL.md`), never an arbitrary product file that looks skill-shaped. The
  drift report MUST additively surface the drifted skill paths (pinpointing the offending root when
  a reference digest arbitrates), and a scaffold with such drift MUST be reported not-coherent.
- **FR-007**: `doctor` MUST report content/skill drift as a **non-blocking advisory** (exit 0,
  zero writes). `upgrade` MUST re-materialize the **missing** seeded copies (existing no-clobber
  re-seed, now covering product-skill-loss detection as advisory); a present-but-divergent copy is
  advisory in P1 and never clobbered. An incomplete reconciliation is never reported complete.
- **FR-008**: `doctor`/`upgrade` MUST read the product-skill copies (across all roots) they verify,
  via a provenance-driven read added to the remediation read plan; the reads stay read-only for
  `doctor`.
- **FR-009**: `FS.GG.Contracts` contract version MUST take an additive **minor** bump
  (`1.3.0` → `1.4.0`) for the new public `SkillMirror` surface, with the public-surface golden
  baseline updated additive-only; the coherent CLI/package version advances `0.4.0` → `0.5.0`.
- **FR-010**: The full build and test suite MUST be green, including a **red-then-green** test that
  proves `doctor` detects **both** content divergence **and** provider-skill loss.

### Key Entities

- **SkillMirror** — the pure library: `mirror`, `verify`, and content helpers over `agentSkillRoots`.
- **MirrorWrite** — one `{ Path; Body }` the fan-out materializes.
- **ExpectedSkill / ActualCopy / SkillDrift** — the `verify` input/output shapes: an expected skill
  with its canonical digest, an actual on-disk copy (body or absent), and the per-skill drift
  (`MissingRoots`, `Divergent`, `HashMismatchRoots`).
- **DriftReport** (extended) — gains the additive drifted-skill-paths surface.

## Success Criteria *(mandatory)*

- **SC-001**: `SkillMirror`'s public surface (`mirror`, `verify`, helpers, `MirrorWrite`,
  `ExpectedSkill`, `ActualCopy`, `SkillDrift`) appears in the `FS.GG.Contracts` baseline; the delta
  is additive only; contract version is `1.4.0`.
- **SC-002**: `SeededSkills`, `HandlersScaffold`, `HandlersRefresh` contain **no** hardcoded
  `.claude`/`.codex`/`.agents` skill-root string literal for fan-out destinations; all derive from
  `agentSkillRoots` through `SkillMirror`.
- **SC-003**: The scaffold/refresh skill-file output and `mirroredPaths` are byte-identical to the
  pre-refactor behavior (existing 056 fan-out tests stay green unchanged).
- **SC-004**: A scaffolded product's provenance records a non-empty, correct `sha256` for each
  produced/mirrored skill copy.
- **SC-005**: A red test proves `doctor` reports **content divergence** (edited root copy) **and**
  **provider-skill loss** (deleted root copy) as drift; the same drift, once `upgrade` re-seeds the
  missing copies, is reduced accordingly.
- **SC-006**: `doctor` still makes **zero** writes; the full suite is green at CLI `0.5.0` /
  Contracts `1.4.0`.

## Assumptions

- **Advisory first** (roadmap §P1.S1.3): P1 *detects* content divergence and *re-materializes
  missing* copies; clobber-repair of a present-but-divergent copy and the enforcing gate flip are
  P4. This preserves ADR-0011's no-clobber invariant.
- The `SkillMirror` library is BCL-only (`System.Security.Cryptography.SHA256` for hashing an
  in-memory body is not I/O), so the standalone lane (P2) can vendor it unchanged.
- Publish-before-flip: this feature lands the code and the coherent version-of-truth bump in-repo;
  cutting/pushing the `0.5.0` release to the feed and flipping the registry orchestrator-axis
  minimum is the separate release dance (tracked like #57 for 0.4.0), not this feature.
