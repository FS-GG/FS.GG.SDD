# Phase 1 Data Model: Emit fs-gg-sdd-* process skills

**Feature**: 051-scaffold-sdd-process-skills

This feature adds **no new schema-versioned structured artifact** and **no new
JSON contract**. Its "data" is the in-memory declared skill set that drives
emission and the on-disk seeded files. The authoritative machine contract is the
declared membership list (see contracts/seeded-skill-set.md).

## Entities

### SeededSkill (in-memory, internal)

Represents one consumer-relevant `fs-gg-sdd-*` process skill to seed.

| Field        | Type     | Notes |
|--------------|----------|-------|
| `Name`       | string   | Stable skill directory name, e.g. `fs-gg-sdd-charter`. Sole identity. |
| `Body`       | string   | Canonical `SKILL.md` bytes, loaded from the embedded resource that links `.claude/skills/<Name>/SKILL.md`. Includes the `name:`/`description:` frontmatter. |

Validation / invariants:
- `Name` MUST be one of the declared in-scope set (D2): the 10 stage skills +
  the 5 cross-cutting skills; `fs-gg-sdd-project` is excluded.
- `Body` MUST be non-empty (SC-001) and MUST contain no run-varying content —
  no dates/timestamps/randomness (FR-006). Guaranteed by linking static source
  bytes; asserted by the drift guard.
- The declared set MUST exactly equal the on-disk authored set under both
  `.claude/skills/` and `.codex/skills/` (drift guard, D5).

### SeededSkillFile (on-disk, per (skill, agent surface))

The concrete file the seeding command writes; one per (skill, surface) pair.

| Field        | Type               | Notes |
|--------------|--------------------|-------|
| `Path`       | relative path      | `.claude/skills/<Name>/SKILL.md` or `.codex/skills/<Name>/SKILL.md`. |
| `Surface`    | `Claude \| Codex`  | Which agent surface this copy serves. |
| `Content`    | string             | Equals the `SeededSkill.Body` of the same `Name` (parity by construction). |
| `WriteKind`  | `AgentGuidanceTarget` | The no-clobber, authored-SDD-owned write-kind. |

Ownership / lifecycle classification (reuses existing types; nothing new):
- **Write-kind**: `ArtifactWriteKind.AgentGuidanceTarget` — same class as the
  seeded `.fsgg/constitution.md` and `.fsgg/early-stage-guidance.md`. Drives
  no-clobber via `CommandEffects.canOverwrite` (FR-004).
- **Provenance owner**: none. Seeded skills are NOT recorded in
  `scaffold-provenance.json` and are NOT `ArtifactOwner.GeneratedProduct`
  (FR-003, FR-008).
- **Refresh class**: neither a `refreshCanonicalView` nor a provenance path →
  refresh-preserved by construction (FR-005).

## State / transitions

The only state transition is the no-clobber decision at write time, already
implemented by `canOverwrite`:

```
write(path, content, AgentGuidanceTarget):
  existing = snapshot(path)
  if existing is None                  -> WRITE            (fresh seed)
  elif existing.Text == content        -> WRITE (idempotent, no-op bytes)
  else                                 -> PRESERVE existing (no-clobber)
```

- Fresh directory → all 30 files written (15 skills × 2 surfaces) (SC-001).
- Re-run with an author-edited skill file → that file preserved unchanged
  (SC-003); other missing files filled (partial-directory edge case).
- Re-run unchanged → idempotent (byte-identical, FR-006/SC-004).

## Relationships

- One `SeededSkill` ⟶ two `SeededSkillFile`s (Claude + Codex), with identical
  `Content` (FR-002).
- The declared `SeededSkill` set ⟶ contributes 30 additive `WriteFile` effects to
  `initEffects`, which `scaffold` reuses unchanged (FR-007).
