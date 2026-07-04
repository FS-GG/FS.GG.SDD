# Feature Specification: Emit the `fs-gg-sdd-*` process skill-manifest

**Feature ID**: 072-process-skill-manifest
**Branch**: `072-process-skill-manifest`
**Date**: 2026-07-04
**Roadmap**: closes [#109](https://github.com/FS-GG/FS.GG.SDD/issues/109) (cross-repo request from `FS-GG/.github`; epic `.github#163`, ADR-0017 P2)
**Decision**: [ADR-0017](https://github.com/FS-GG/.github/blob/main/docs/adr/0017-skill-registry-condition-aware-materialization.md) — org skill registry, condition-aware materialization
**Contract**: `skill-registry` (`.github` `registry/skills.yml`) consumes this producer manifest.

## Context

ADR-0017 gives the org a single authoritative skill catalog,
`.github` `registry/skills.yml`, **reconciled from producer manifests** (never
hand-authored bytes). Each producer emits **one `skill-manifest`** enumerating
every skill it can supply, each entry `{ id, scope, sha256, materializes-when }`
(ADR-0014 `{id, scope, sha256}` extended with the ADR-0017 `materializes-when`
predicate; `resolvablePath`/`supplied-by` optional).

- **Product half — done.** Rendering emits its product manifest at
  `template/skill-manifest/skill-manifest.json` (Feature 238 / Rendering#76). The
  12 product rows in `registry/skills.yml` are reconciled verbatim from it —
  `sha256` + emission condition authoritative.
- **Process half — provisional, and this feature closes it.** SDD is the producer
  of the `fs-gg-sdd-*` process skills, but **emits no manifest for them**. The
  `SkillManifest` *contract types* already shipped (SDD#60 / spec
  `057-skill-manifest-contract`) — types only, no emission. So `.github`
  bootstrapped the process rows by digesting SDD's canonical
  `.claude/skills/fs-gg-sdd-*/SKILL.md` bodies itself, and marked them
  **PROVISIONAL** in `registry/skills.yml` and its changelog: "MUST be
  re-reconciled against SDD's process manifest once it emits one."

Until SDD emits the manifest, the typed `Fsgg.Registry` validator cannot assert
**registry = manifest = bytes** for process skills, `.github` cannot flip the
`skill-union-assert --params` process rows to *required*, and the coherence row
`skill-registry-published` stays `coherent: false` (the ADR-0017 enforcing flip on
`.github#163`). Publish-before-flip: the producer manifest must exist before the
registry claims coherence over it. **This feature is the emission step.**

### Reproduced against `HEAD` (per cross-repo discipline)

- SDD ships **16** consumer-relevant process skills today. `SeededSkills.skillNames`
  seeds exactly the 16 `fs-gg-sdd-*` names — the 10 stage skills plus the 6
  cross-cutting skills (`lifecycle`/`getting-started`/`authoring-contracts`/
  `refresh-agents`/`validate`/`troubleshooting`); it deliberately **excludes**
  `fs-gg-sdd-project` (product-internal). Confirmed: `.claude/skills/` holds 17
  `fs-gg-sdd-*` subtrees, 16 after removing `fs-gg-sdd-project`.
- **`.github`'s registry lists only 15 process rows — it is missing
  `fs-gg-sdd-troubleshooting`.** The registry was digested (`.github#168`, dated
  2026-07-04) from a snapshot before feature 071 added the troubleshooting skill
  (merged #108). ADR-0017 and issue #109 both say "15"; the on-disk truth is 16.
  This is a genuine cross-repo drift the manifest must surface: SDD emits 16, and
  `.github` reconciles the 16th (troubleshooting) row from it. **Emitting 16, not
  15, is a requirement of this feature, not an oversight.**
- No skill-manifest is emitted anywhere in the SDD repo today — this is greenfield
  emission over pre-existing contract types.

### Boundary this feature holds

SDD owns **emitting** its own process producer manifest and **pinning** it to the
seeded set with a drift guard. It does **not** own `registry/skills.yml`, the
`Fsgg.Registry` validator, the union gate, or the coherence flip — those are
`.github`'s, downstream, and out of scope. This feature makes the process manifest
*exist and be correct*; the reconciliation and flip are `.github`'s follow-up
(tracked on `.github#163`).

## User Stories

**US1 (P1)** — As `FS-GG/.github` reconciling `registry/skills.yml`, I want SDD to
emit a machine-readable `skill-manifest` (schema v1) enumerating **all 16**
`fs-gg-sdd-*` process skills, each with `scope: process`,
`materializes-when: always`, and a canonical-body `sha256` byte-equivalent to
`sha256sum SKILL.md`, so I can reconcile the provisional process rows to
authoritative (including the missing `fs-gg-sdd-troubleshooting` row) and the typed
validator can assert **registry = manifest = bytes**.

**US2 (P1)** — As an SDD maintainer, I want a drift guard that fails CI if the
emitted manifest ever diverges from the seeded skill set — its ids must equal
`SeededSkills.skillNames` exactly, and each `sha256` must equal the canonical
digest of the authored `SKILL.md` it names — so the manifest cannot silently go
stale when a skill is added, removed, or edited (the same failure class that left
`.github`'s registry stuck at 15).

**US3 (P2)** — As an SDD maintainer changing the skill set, I want the manifest to
be **generated/checkable**, not hand-maintained bytes, so adding or editing a skill
regenerates it deterministically and the guard confirms currency — mirroring
Rendering's `generate-skill-manifest.fsx --check` producer discipline.

## Requirements

### Functional

- **FR-001** — SDD emits a `skill-manifest` v1 document enumerating exactly the
  process skills named in `SeededSkills.skillNames` (the 16 `fs-gg-sdd-*` skills;
  `fs-gg-sdd-project` excluded). The manifest is the single producer source
  `.github` reconciles the process rows from.
- **FR-002** — Each manifest entry carries `id` (the `fs-gg-sdd-*` skill name),
  `scope: process`, `sha256` (canonical-body digest of that skill's authored
  `SKILL.md`, byte-equivalent to `sha256sum SKILL.md` — the `Fsgg.SkillMirror`
  algorithm already used for the seed/verify digest), and
  `materializes-when: always`.
- **FR-003** — The manifest is serialized in the **same `skill-manifest` v1 shape**
  the org consumes from Rendering's producer manifest: top-level
  `schemaVersion: 1` and a `skills` array of entries; field names byte-compatible
  with the product manifest (`id`, `scope`, `sha256`, `resolvablePath`,
  `materializes-when`; `supplied-by` where meaningful). Any process-specific field
  choices (e.g. `resolvablePath`, `supplied-by` source) are documented and stable.
- **FR-004** — `materializes-when` is written in the **ADR-0017 canonical grammar**
  (bare unquoted tokens; `==`/`!=`/`in [..]`; `and`/`or`; `always`; no parentheses,
  no `&&`/`||`, no quoted values) — the grammar the union gate and `Fsgg.Registry`
  validator parse. For all 16 process skills the value is the literal `always`. The
  manifest MUST NOT ship the C-style grammar that broke Rendering's product manifest
  (Rendering#77); a test asserts every predicate parses under the canonical grammar.
- **FR-005** — Manifest entries are **deterministically ordered** (by `id`) and the
  serialization is byte-deterministic across runs and platforms (stable key order,
  fixed number formatting, LF newlines) so it is a golden/reconcilable artifact.
- **FR-006** — The manifest is emitted in a form `.github` can consume without
  running SDD: it is **committed in the SDD repo** at a stable, documented canonical
  path (the process analog of Rendering's `template/skill-manifest/skill-manifest.json`).
  If the manifest is also surfaced through a CLI/scaffold path, that projection is
  byte-identical to the committed canonical file.
- **FR-007** — A **drift guard** (test) fails when: (a) the manifest's id set ≠
  `SeededSkills.skillNames`; (b) any entry's `sha256` ≠ the canonical digest of the
  authored `SKILL.md` it names; (c) any entry's `scope` ≠ `process` or
  `materializes-when` ≠ `always`; (d) the committed manifest bytes ≠ the freshly
  generated bytes (staleness). The guard pins the manifest to the on-disk authored
  set the way `SeededSkillsTests` pins the seeded roots.
- **FR-008** — The manifest emission reuses the **existing** `SkillManifest`
  contract types and canonical-hashing helper (SDD#60 / spec-057; the
  CRLF-normalized digest from feature 070). It introduces no second hashing
  algorithm and no second definition of the skill set — `SeededSkills.skillNames`
  remains the single source of the id list.

### Non-functional / Constraints

- **NFR-001 — No new source of truth.** The skill id list stays
  `SeededSkills.skillNames`; the bytes stay the authored `SKILL.md` files; the hash
  stays the one canonical `Fsgg.SkillMirror` digest. The manifest is a *projection*,
  never an independently editable list.
- **NFR-002 — Additive, backward-compatible.** No change to `init`/`scaffold` seeding
  behavior, no change to any persisted schema version (scaffold-provenance stays v1),
  no change to existing JSON automation contracts. If a `CommandReport` surface is
  touched, changes are additive and the default JSON byte stream for unrelated
  commands is unchanged.
- **NFR-003 — Determinism honored by the test harness.** The manifest is a
  deterministic artifact with a golden/round-trip test; rich/plain projections (if
  any) stay presentation-only and excluded from the golden contract.
- **NFR-004 — Canonical grammar only.** Do not embed provider-specific or C-style
  predicates. Process predicates are `always`; the grammar choice is generic.

## Acceptance Criteria

- **AC-001** — Running the emission produces a `skill-manifest` v1 document whose
  `skills` array has exactly 16 entries, one per `SeededSkills.skillNames` id,
  including `fs-gg-sdd-troubleshooting`; `fs-gg-sdd-project` is absent. (Covers
  FR-001.)
- **AC-002** — Every entry has `scope: process`, `materializes-when: always`, and a
  `sha256` equal to `sha256sum` of the named authored `SKILL.md`. A test recomputes
  each digest from disk and asserts equality. (Covers FR-002, FR-007b.)
- **AC-003** — The manifest deserializes under the same `skill-manifest` v1 reader
  the org/contract uses; `schemaVersion == 1`; field names match the product manifest
  shape. (Covers FR-003.)
- **AC-004** — Every `materializes-when` value parses under the ADR-0017 canonical
  grammar and equals `always`; no predicate contains `(`, `&&`, `||`, or quoted
  tokens. (Covers FR-004.)
- **AC-005** — Two emissions on the same inputs are byte-identical; entries are
  sorted by `id`; newlines are LF. (Covers FR-005.)
- **AC-006** — The manifest is committed at the documented canonical path and is
  byte-identical to a fresh generation (a `--check`-style staleness assertion in the
  guard). (Covers FR-006, FR-007d.)
- **AC-007** — The drift guard fails if a skill is added to / removed from
  `SeededSkills.skillNames` without regenerating the manifest, or if a seeded
  `SKILL.md` body changes without regeneration. Demonstrated RED-first: a mutated
  input reddens the guard; regeneration greens it. (Covers FR-007a/b/d.)
- **AC-008** — The full existing test suite stays green; no persisted schema version
  changes; `init`/`scaffold` seeding output is unchanged. (Covers NFR-002.)

## Out of Scope

- `.github`'s reconciliation of `registry/skills.yml` process rows
  (provisional → authoritative), adding the 16th `fs-gg-sdd-troubleshooting` row,
  the `Fsgg.Registry` typed validator asserting registry = manifest = bytes, the
  union-gate `--params` tightening, and the `skill-registry-published` coherence flip
  — all owned by `.github` / downstream, tracked on `.github#163`. This feature
  hands them a correct producer manifest (and flags the 15→16 drift); it does not
  perform their reconciliation.
- The `fs-gg-project` cross-producer seam (ADR-0017 §C2). SDD's half is the
  documented `SeededSkills` exclusion of `fs-gg-sdd-project`; the product-side
  `fs-gg-project` supply resolution is Rendering's (Rendering#76). Unchanged here.
- Rendering's C-style→canonical `materializes-when` grammar fix (Rendering#77).
- Any change to how skills are seeded, mirrored, or fanned into scaffolded products.

## Dependencies & Refs

- Consumes existing contract types: `SkillManifest` / `AGENT_SKILL_ROOTS` / per-skill
  `sha256` (SDD#60 / spec `057-skill-manifest-contract`) and the canonical
  CRLF-normalized skill digest (feature 070, issue #70).
- Skill id source: `SeededSkills.skillNames` (features 048/056/071).
- Reference shape: Rendering `template/skill-manifest/skill-manifest.json`
  (Feature 238 / Rendering#76) and `.github` `registry/skills.yml` process rows +
  `registry/skills.CHANGELOG.md`.
- Epic: `.github#163` · ADR-0017 · design `.github` `docs/coordination/skill-registry.md`.
- Sibling grammar blocker (product side, out of scope): Rendering#77.
