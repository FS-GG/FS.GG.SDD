# Contract: Seeded SDD-process skill set

**Feature**: 051-scaffold-sdd-process-skills
**Surface kind**: skeleton-shape contract (authored seed files) — not a
schema-versioned JSON artifact, not part of the `release-readiness.json` catalog.

This is the machine-facing contract the seeding command (`fsgg-sdd init`, reused
by `fsgg-sdd scaffold`) honors. It is enforced by the drift guard and the
skeleton-shape conformance tests; if prose and this contract disagree, this
contract and the on-disk authored source win.

## 1. Declared membership (the single source of truth for the set)

The seeding command MUST emit exactly these 15 skills and no others:

| # | Skill name | Class |
|---|------------|-------|
| 1 | `fs-gg-sdd-charter` | stage |
| 2 | `fs-gg-sdd-specify` | stage |
| 3 | `fs-gg-sdd-clarify` | stage |
| 4 | `fs-gg-sdd-checklist` | stage |
| 5 | `fs-gg-sdd-plan` | stage |
| 6 | `fs-gg-sdd-tasks` | stage |
| 7 | `fs-gg-sdd-analyze` | stage |
| 8 | `fs-gg-sdd-evidence` | stage |
| 9 | `fs-gg-sdd-verify` | stage |
| 10 | `fs-gg-sdd-ship` | stage |
| 11 | `fs-gg-sdd-lifecycle` | cross-cutting |
| 12 | `fs-gg-sdd-getting-started` | cross-cutting |
| 13 | `fs-gg-sdd-authoring-contracts` | cross-cutting |
| 14 | `fs-gg-sdd-refresh-agents` | cross-cutting |
| 15 | `fs-gg-sdd-validate` | cross-cutting |

Explicitly **excluded**: `fs-gg-sdd-project` (product-internal, not a
consumer-product process skill).

## 2. Emitted file paths

For each declared skill `<name>`, the command MUST write both:

```
.claude/skills/<name>/SKILL.md
.codex/skills/<name>/SKILL.md
```

→ 30 files total (15 × 2). The body of each is the canonical authored
`SKILL.md` (frontmatter + body), identical across the two surfaces.

## 3. Ownership and write semantics

- **Write-kind**: `AgentGuidanceTarget` (no-clobber). An existing target file
  with differing content is preserved, never overwritten; identical content is
  an idempotent no-op.
- **Provenance**: NOT recorded in `.fsgg/scaffold-provenance.json`; NOT
  `generatedProduct`. Authored, SDD-owned skeleton — same ownership class as
  `.fsgg/constitution.md` / `.fsgg/early-stage-guidance.md`.
- **refresh**: never regenerates, rewrites, or enumerates these files.

## 4. Invariants (enforced by tests)

- **INV-1 (completeness, SC-001)**: after seeding into an empty directory, all 30
  files exist and are non-empty, via both `init` and `scaffold`.
- **INV-2 (parity, FR-002/SC-004)**: for every declared skill, the `.claude` and
  `.codex` emitted bodies are byte-identical.
- **INV-3 (determinism, FR-006/SC-004)**: two seeding runs of the same input
  produce byte-identical files; no dates/timestamps/randomness in any body.
- **INV-4 (no-clobber, FR-004/SC-003)**: re-running over an author-edited skill
  file preserves it exactly; missing files are filled without disturbing present
  ones.
- **INV-5 (single seam, FR-007)**: `init` and `scaffold` deliver the identical
  set through the one shared skeleton seam; no path seeds the skeleton without
  the skills.
- **INV-6 (boundary, FR-008)**: the skills are produced by SDD's own seam; the
  external template provider is never their source and may not write into the
  seeded skill subtrees.
- **INV-7 (drift guard, FR-010/SC-005)**: the declared set equals the on-disk
  authored set under both `.claude/skills/` and `.codex/skills/`. The embedded
  resource links the `.claude/skills/<name>/SKILL.md` body as the **single
  canonical source**; the `.codex/skills/<name>/SKILL.md` on-disk copy is a
  drift-guarded mirror, not a second source of truth. The guard asserts the
  embedded bytes equal the `.claude` on-disk source **and** that the `.codex`
  on-disk body is byte-identical to it; any divergence fails before release.
- **INV-8 (skeleton-shape, FR-009)**: the skeleton-shape conformance surface
  (init/composition tests) accounts for the 30 seeded skill files.
