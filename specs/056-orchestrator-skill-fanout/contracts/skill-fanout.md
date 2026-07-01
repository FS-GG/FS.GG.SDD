# Contract: Orchestrator skill fan-out + strict guard

**Feature**: `056-orchestrator-skill-fanout` | Phase 1. The behavioral contract SDD upholds, as
properties P1–P10. This is the `scaffold-provider` behavioral-coherence surface (cross-repo) plus
the `init`/`refresh`/`doctor`/`upgrade` agent-skill invariant.

## Reservation (strict guard)

- **P1** — `isSddTree` reserves the **whole** `.claude/skills/` and `.codex/skills/` roots. A
  provider write into either is `scaffold.providerWroteSddTree` (exit 2, `providerFailed`). No
  narrowing.
- **P2** — `isSddTree` also reserves the `fs-gg-sdd-*` **namespace** under `.agents/skills/`. A
  provider write into `.agents/skills/fs-gg-sdd-*` is `scaffold.providerWroteSddTree` (exit 2).
- **P3** — `.agents/skills/<non-reserved>` is **product**: not an intrusion, recorded in
  `producedPaths` as `generatedProduct`.
- **P4** — `.fsgg/`·`work/`·`readiness/` reservation and `isSddOwned` (`+ AGENTS.md`/`CLAUDE.md`)
  are unchanged.

## Fan-out (SDD is the sole mirror authority)

- **P5** — `init` seeds every `fs-gg-sdd-*` skill into **all three** roots, byte-identical,
  no-clobber (`AgentGuidanceTarget`). Two runs are byte-stable.
- **P6** — After a successful provider invocation, `scaffold` writes the **union** (seeded ∪
  provider `.agents/skills/*` non-reserved) byte-identically into all three roots:
  `bytes(.claude/skills/s) == bytes(.codex/skills/s) == bytes(.agents/skills/s)` for every `s`.
- **P7** — The mirrored `.claude`/`.codex` copies of provider skills are recorded in provenance
  `mirroredPaths` with owner `mirrored`; the provider's canonical `.agents` skill stays in
  `producedPaths` as `generatedProduct`; no seeded `fs-gg-sdd-*` path appears in either array.
- **P8** — `refresh` re-mirrors the union to currency across all three roots (re-seed missing
  seeded copies no-clobber; re-write mirror copies from the canonical `.agents` source).

## Drift & safety

- **P9** — `doctor` detects (read-only, exit 0) any violation of `claude ≡ codex ≡ agents = union`
  (missing root, byte-divergent copy); `upgrade` reconciles it no-clobber (re-seed + re-mirror),
  leaving zero residual drift; author edits are preserved.
- **P10** — Determinism/additivity: `scaffold-provenance.json` stays **schema v1**; `mirroredPaths`
  is additive (default `[]`, `tryParse`-defaulted); `serialize` is byte-deterministic; the json ≡
  text ≡ rich projections carry the mirrored facts identically and the rich path changes no JSON
  byte. An incomplete fan-out is never reported complete (a mirror failure ⇒ diagnostic + non-success).

## Out of scope (contract boundary)

- In-place rewrite of an already-seeded `fs-gg-sdd-*` skill at its existing path (diff-invisible;
  no-clobber `AgentGuidanceTarget` policy, unchanged).
- The provider-side change (Rendering emits `.agents/skills/`, stops writing `.claude/`) — that is
  **FS-GG/FS.GG.Templates#47**, not this repo.
- No provider-specific package id, template id, path, or skill name in generic SDD.
