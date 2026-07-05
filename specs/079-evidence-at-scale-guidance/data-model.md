# Data Model: fs-gg-sdd-evidence at-scale evidence guidance

This feature changes no persisted schema and adds no code type. The "data model" here is the
structure of the edited authored artifact and its derived, guarded surfaces.

## Artifact under change

### `fs-gg-sdd-evidence` skill body — `SKILL.md`

- **Frontmatter** (YAML): `name: fs-gg-sdd-evidence`, `description: <one line>`. The `name` is
  unchanged (it is the manifest/mirror key). The `description` is left as-is unless a small tweak
  is warranted; if edited it stays a single line within existing length/format conventions.
- **Body** (Markdown): the existing sections stay; three guidance blocks are added/strengthened.

### Guidance blocks (the additive content)

| Block | Satisfies | Placement intent | Must reference (already-shipped) |
|---|---|---|---|
| At-scale classification workflow | FR-001, FR-002 | after the satisfaction rule / origin-refs sections | the `result: pass ∧ synthetic: false` rule; the carried `requirementRefs`/`planDecisionRefs` (feat 077) as the per-obligation classification key |
| Deferrals are first-class, not failures | FR-003 | adjacent to the satisfaction rule / real-vs-synthetic discipline | `result: deferred` / `kind: deferral`; contrast vs `synthetic: true` pass; a shippable item may carry deferrals |
| Bulk-authoring pattern | FR-004 | new subsection near the command/`--from-tests` docs | `evidence --from-tests <path>`; origin refs; honesty caveat (no blanket `pass`) |

Non-goals for the blocks (FR-005): no new `kind`/`result` value, no new flag, no new field, no
new output stream/exit code, no unshipped behavior.

## Derived surfaces (re-pinned, not authored)

| Surface | Relationship to the body | Guard |
|---|---|---|
| `.codex/skills/fs-gg-sdd-evidence/SKILL.md` | byte-identical mirror of `.claude` body | skill-mirror guard (`.claude`≡`.codex`), `SkillMirrorTests` helpers |
| Embedded resource `SeededSkill.fs-gg-sdd-evidence` | compiled-in copy, linked from the `.claude` file; re-links on build | agent-surface-drift guard; seeding tests |
| `.agents/skills/skill-manifest.json` → `fs-gg-sdd-evidence.sha256` | `sha256` over CRLF→LF-normalized body bytes | `ProcessSkillManifestTests`; `registry skill-manifest --check` |

## Invariants (unchanged by this feature)

- **Skill set**: `SeededSkills.skillNames` (16 skills) and the manifest's row set are unchanged —
  no row added or removed (FR-009).
- **Manifest schema**: stays v1; `scope: process`, `materializes-when: always`,
  `resolvablePath: .agents/skills/fs-gg-sdd-evidence/SKILL.md` for this row — only the `sha256`
  value changes.
- **Other skill bodies**: untouched; their `sha256` rows are unchanged.

## Validation rules

- `sha256(.agents manifest row) == sha256sum(LF-normalized SKILL.md)` for `fs-gg-sdd-evidence`.
- `bytes(.claude/…/SKILL.md) == bytes(.codex/…/SKILL.md)`.
- Body references only already-shipped affordances (manual review against research Decision 3).
- No diff outside the three surfaces + the four spec-dir docs + `tasks.md` (and the `CLAUDE.md`
  SPECKIT plan pointer updated by the after_plan agent-context hook, if run).
