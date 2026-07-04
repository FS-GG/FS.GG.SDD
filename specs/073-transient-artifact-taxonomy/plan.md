# Implementation Plan: Transient SDD artifact taxonomy + seeded `.gitignore`

**Feature**: 073-transient-artifact-taxonomy
**Spec**: [spec.md](spec.md) · **Decision**: [ADR-0018](https://github.com/FS-GG/.github/blob/main/docs/adr/0018-transient-durable-sdd-artifact-taxonomy.md) · **Roadmap**: [#110](https://github.com/FS-GG/FS.GG.SDD/issues/110)

## Summary

Two deliverables, both additive and backward-compatible:

1. **Taxonomy doc** `docs/reference/artifact-taxonomy.md` — the canonical durable-vs-regenerable
   classification, with its regenerable `readiness/<id>/…` list **derived from** the
   `release-readiness.json` generated-view catalog and pinned by a drift guard.
2. **Seeded `.gitignore`** — `fsgg-sdd init` seeds a whole-file, no-clobber `.gitignore`
   (chosen seed shape, per spec's open decision) through the existing
   `AgentGuidanceTarget` effect, so a fresh scaffold ignores regenerable `readiness/<id>/`
   output at birth. Pinned by a drift guard; wired into the `doctor`/`upgrade` seeded set.

No new effect, no new schema, no changed command-report semantics.

## Ground truth (verified against `HEAD`)

- **Seed site:** `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs` → `initEffects`
  (lines 390–407). It emits `WriteFile(path, text, AgentGuidanceTarget)` for
  `.fsgg/constitution.md`, `.fsgg/early-stage-guidance.md`, `AGENTS.md`, `CLAUDE.md`, then
  `@ SeededSkills.skillEffects ()`. Adding one `WriteFile(".gitignore", …, AgentGuidanceTarget)`
  is the entire seed change. `scaffold` reuses `initEffects` unchanged (FR-004), so the seed
  fans out to scaffold automatically and is never `generatedProduct` provenance.
- **Doctor/upgrade expected set:** `CommandWorkflow/Drift.fs` → `expectedArtifactPaths`
  (line 22) = the three-root skill SKILL.md paths `@ [".fsgg/early-stage-guidance.md"]`,
  sorted. `doctor` reports missing members; `upgrade` no-clobber re-seeds them via `initEffects`.
  Adding `".gitignore"` here gives FR-004/AC-005 for free. (Note: `constitution.md`/`AGENTS.md`/
  `CLAUDE.md` are seeded but *not* currently in this set — a pre-existing choice; we add
  `.gitignore` because the spec requires doctor/upgrade coverage for it.)
- **Derivation source:** `docs/release/release-readiness.json` `catalog[]` — 9 entries with
  `sourceArtifact.kind == "generatedView"`, each a `readiness/<id>/…` path. This is the exact
  regenerable readiness set the taxonomy lists and the drift guard pins.
- **Refresh never rewrites seeds:** `HandlersRefresh.fs` re-mirrors only skills; authored
  `AgentGuidanceTarget` skeleton files are not regenerated (FR-004). No change needed there.

## Decisions

- **D1 — Whole-file, no-clobber `.gitignore` seed** (spec open-decision → option 1). Reuses
  `AgentGuidanceTarget`; lands cleanly on the empty-dir scaffold path; skipped no-clobber when a
  `.gitignore` already exists (existing repos adopt the fragment from the taxonomy doc). No new
  machinery.
- **D2 — Seeded content = SDD-transient rule (headline) + universally-generic build/tooling
  outputs.** The file leads with the regenerable `readiness/<id>/` role rule (the ADR's point),
  followed by the small generic-.NET output set (`bin/`, `obj/`, `artifacts/`, `TestResults/`,
  `.tmp/`, `nuget-cache/`) that every buildable scaffold needs. These are toolchain-generic, not
  provider-specific — they stay within CLAUDE.md's "no rendering-specific names/paths" boundary,
  and they make scaffold's "buildable, runnable at birth" goal (FR-015) hold. Exact bytes fixed
  as a module constant and pinned by the FR-005 drift guard.
- **D3 — Taxonomy doc is catalog-derived for the regenerable readiness list only.** The durable
  authored-source list (`spec.md`/`plan.md`/`tasks.yml`/`evidence.yml`/`charter.md`/
  `clarifications.md`/`checklist.md`/authored `contracts/`) is authored directly — the catalog
  does not enumerate authored input. The drift guard asserts the doc's regenerable readiness list
  equals the catalog generated-view paths; the durable list is a stable enumeration checked
  against the lifecycle stage paths.
- **D4 — Doctor presence, not content.** `doctor`/`upgrade` track `.gitignore` by presence (the
  existing `MissingArtifactPaths` semantics). Verifying that a *pre-existing* consumer `.gitignore`
  contains the SDD rule is out of scope; the FR-005 drift guard pins the *seeded* bytes in SDD's
  own test suite. Documented as a known limitation in the taxonomy doc.

## Technical Context

- **Language/build:** F# on net10.0; MVU/Elmish command workflow; effects interpreted at the edge.
  Tests: Expecto.
- **Purity:** the seed is a pure `WriteFile` effect appended to `initEffects`; the taxonomy doc is
  static authored Markdown. The drift guards are pure tests reading `release-readiness.json` +
  the seeded constant.
- **Determinism:** seeded `.gitignore` bytes are a compile-time constant; `initEffects` order stays
  deterministic (append after skills, or adjacent to the other `AgentGuidanceTarget` writes — place
  it with the constitution/early-stage block for readability, before `SeededSkills`).

## Approach

1. **Seed constant + write.** Add `gitignoreSeedText` (module constant, exact bytes per D2) near
   the other seed-body constants in `Foundation.fs`; add
   `WriteFile(".gitignore", gitignoreSeedText, AgentGuidanceTarget)` to `initEffects` in the
   `AgentGuidanceTarget` block.
2. **Doctor/upgrade coverage.** Add `".gitignore"` to `Drift.expectedArtifactPaths`. Confirm
   `doctor` dry-run preview and `upgrade` re-seed pick it up with zero other changes.
3. **Taxonomy doc.** Author `docs/reference/artifact-taxonomy.md` (FR-001): two classes with example
   paths, the role-based ignore convention, the D4 limitation, and a copy-paste fragment for
   existing repos. Link it from `docs/reference/README.md`'s starting points and the top-level
   `README.md`/quickstart where the other reference docs are indexed.
4. **Drift guards (FR-005).** Two Expecto tests:
   - `taxonomy regenerable list == release-readiness.json generatedView paths` (FR-002).
   - `seeded .gitignore bytes == authored gitignoreSeedText` and (belt-and-suspenders)
     `init emits a WriteFile(".gitignore", …, AgentGuidanceTarget)` (FR-003/FR-005).
5. **Regression sweep.** Update any `init`/`scaffold`/`doctor` golden fixtures that enumerate the
   seeded set or `expectedArtifactCount`; confirm no JSON-contract byte changes elsewhere (FR-006/
   AC-007).

## Constitution Check

- **Authored surface vs machine contract:** taxonomy is authored Markdown; the machine contract it
  mirrors (`release-readiness.json`) is unchanged — the doc is a drift-guarded *projection*, not a
  second source of truth (Principle: Markdown authoring / structured contracts).
- **No new effect / no new schema:** reuses `AgentGuidanceTarget` and existing `WriteFile`; persisted
  schemas untouched (scaffold-provenance stays v1). ✔
- **Claude ≡ Codex ≡ agents:** `.gitignore` is repo-root, agent-neutral — no per-agent surface, so no
  divergence risk. ✔
- **Determinism/goldens:** seeded bytes are constant; rich/text/json projections unaffected. ✔

## Risks

- **R1 — Scaffold provider also ships a `.gitignore`.** Under D1 no-clobber, whichever write lands
  first wins. `init` runs in the skeleton phase before the provider; SDD's focused-but-complete seed
  (D2) means the product is still buildable even if the provider omits its own. Mitigation: keep the
  seed generic (D2) and document; if a concrete provider conflict surfaces, it becomes a cross-repo
  child of #110, not a blocker here.
- **R2 — Golden/fixture churn.** `expectedArtifactCount` and any seeded-set snapshots shift by one.
  Mitigation: update fixtures in the same change; the drift guards make the new expected value
  self-documenting.
- **R3 — Doc/catalog drift over time.** Precisely what FR-005 guards against — a new generated view
  added to the catalog without updating the taxonomy fails the derivation test. Intended.
