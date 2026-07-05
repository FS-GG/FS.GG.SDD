# Research: fs-gg-sdd-evidence at-scale evidence guidance

Phase 0 for feature 079. No open `NEEDS CLARIFICATION` remained after the spec; this record
consolidates the surface/guard mechanics and the content sourcing the edit relies on, so the
body describes only already-true behavior (FR-005).

## Decision 1 — Edit the canonical `.claude` body; the rest are derived surfaces

- **Decision**: Make the guidance change in `.claude/skills/fs-gg-sdd-evidence/SKILL.md` (the
  single authored source), then re-derive: copy byte-for-byte to
  `.codex/skills/fs-gg-sdd-evidence/SKILL.md`, rebuild so the embedded resource re-links, and
  regenerate the manifest `sha256`.
- **Rationale**: `SeededSkills.fs` documents the `.claude` files as the canonical bodies, linked
  into `FS.GG.SDD.Commands` as embedded resources (`LogicalName SeededSkill.<name>`); the
  `.codex` copy is a mirror and the `.agents` fan-out into scaffolded workspaces is produced by
  the unchanged seeding path. There is exactly one place to author.
- **Alternatives considered**: Editing `.codex` or a scaffolded `.agents` copy — rejected: those
  are projections; the mirror/manifest guards would redden and the embedded resource (linked from
  `.claude`) would still ship the old body.

## Decision 2 — Re-pin the manifest with the shipped generator, not by hand

- **Decision**: Regenerate `.agents/skills/skill-manifest.json` with `fsgg-sdd registry
  skill-manifest --write`, and verify with `--check`.
- **Rationale**: `src/FS.GG.SDD.Cli/RegistrySkillManifest.fs` is the producer of record; `--write`
  recomputes every row's `sha256` from disk and `--check` fails non-zero with an actionable
  message when the committed manifest is stale. `ProcessSkillManifestTests` pins the committed
  file to a fresh serialization, so a hand-edited `sha256` risks a formatting mismatch.
- **Nuance**: the `sha256` is computed over **CRLF→LF-normalized** bytes (feature 070), so a
  Windows `core.autocrlf` checkout does not spuriously drift. On an LF checkout `sha256sum
  SKILL.md` equals the manifest value (verified against the current body:
  `1136bf6b…1111`). The verification in quickstart uses `--check` (authoritative) and `sha256sum`
  (informative).

## Decision 3 — Guidance content is sourced only from already-shipped behavior

The three guidance blocks must reference only behavior `fsgg-sdd` ships today:

- **At-scale classification (FR-001/FR-002)**: the satisfaction rule (`result: pass ∧
  synthetic: false`) and the `kind`/`result` vocabularies are already in the body; feature 077
  (#124, merged) made scaffolded obligations carry `requirementRefs`/`planDecisionRefs`, and the
  body already documents that (see "Auto-scaffolded obligations carry their origin refs"). The new
  block *builds on* that: it turns the existing facts into a sweep-the-graph workflow. Source:
  `docs/reference/authoring-contracts.md` (§ evidence.yml), the current body, TD1 §3.5.
- **Deferrals first-class (FR-003)**: the body already states `result: deferred` / `kind:
  deferral` is "an accepted deferral, not a satisfaction". The new framing elevates it: a
  deferral is honest and first-class, a shippable item may carry deferrals, and it is preferable
  to a synthetic pass. No new disposition — only sharper framing of the existing one. Source: TD1
  §3.5/§4.1 (77 pass / 8 deferral / 0 synthetic).
- **Bulk authoring (FR-004)**: `evidence --from-tests <path>` already exists (documented in the
  current body) and pre-maps newly scaffolded obligations to a proving test file; combined with
  the carried origin refs it is the supported spine of a bulk pattern. The block documents that
  pattern + ordinary scripted edits, with the honesty caveat (never a blanket `pass`). No new
  flag. Source: current body ("`--from-tests`"), TD1 §3.5 (the throwaway Python transform is the
  anti-pattern this replaces).

- **Rationale**: FR-005 forbids implying unshipped behavior; anchoring every block to a verified
  existing affordance keeps the skill a guide, not a promise.
- **Alternatives considered**: Proposing a `--bulk` flag or a scaffold template — rejected in the
  spec's clarifications (documentation-only scope); those would be separate CLI features.

## Decision 4 — Coherence with sibling skills (FR-010)

- **Decision**: Link, don't restate. The full grammar/drift-guard reference stays in
  `fs-gg-sdd-authoring-contracts`; downstream evaluation stays in `fs-gg-sdd-verify`; obligation
  origin stays in `fs-gg-sdd-tasks`. The evidence body's existing `[[…]]` links to these remain
  and the new blocks cross-link rather than duplicating divergent wording.
- **Rationale**: Divergent restatement is how sibling skills rot out of sync; the wiki-links are
  the established coherence mechanism in these bodies.

## Open questions

None. The spec's two clarifications (documentation-only scope; surfaces = the existing seeded
roots) fixed the boundaries; the content maps to verified existing behavior.
