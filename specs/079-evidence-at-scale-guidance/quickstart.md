# Quickstart: verify the fs-gg-sdd-evidence at-scale guidance edit

End-to-end validation guide for feature 079. Run from repo root.

## Prerequisites

- The branch `079-evidence-at-scale-guidance` is checked out.
- A working .NET 10 SDK (for `dotnet test` and the `fsgg-sdd` CLI).

## 1. Content review (C1 / FR-001..FR-005, FR-010)

Read `.claude/skills/fs-gg-sdd-evidence/SKILL.md` and confirm each obligation:

- [ ] An **at-scale classification** workflow: how to sweep an auto-expanded obligation graph
      (the 18→85 case) and mark each obligation real-pass vs deferral, keyed on the carried
      `requirementRefs`/`planDecisionRefs` — no `tasks.yml` title-join. *(FR-001, FR-002)*
- [ ] A **deferrals are first-class, not failures** statement: honest/accepted, a shippable item
      may carry deferrals, and preferable to a synthetic pass. *(FR-003)*
- [ ] A **bulk-authoring pattern** built only on shipped affordances (`evidence --from-tests`,
      origin refs) with an explicit honesty caveat. *(FR-004)*
- [ ] No unshipped flag/field/`kind`/`result`/stream/exit introduced; the `result: pass ∧
      synthetic: false` rule reinforced. *(FR-005)*
- [ ] Sibling skills linked, not restated (`fs-gg-sdd-authoring-contracts`, `-verify`, `-tasks`).
      *(FR-010)*

## 2. Mirror parity (C2 / FR-006)

```sh
diff .claude/skills/fs-gg-sdd-evidence/SKILL.md .codex/skills/fs-gg-sdd-evidence/SKILL.md
```

Expected: **no output** (byte-identical).

## 3. Manifest pin (C3 / FR-007, FR-009)

```sh
# regenerate after editing the body, then check:
dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --write
dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --check   # exit 0

# informative cross-check (LF checkout): equals the manifest row value
sha256sum .claude/skills/fs-gg-sdd-evidence/SKILL.md
```

Expected: `--check` exits 0; the `fs-gg-sdd-evidence` `sha256` in
`.agents/skills/skill-manifest.json` equals the `sha256sum` above; the manifest's row set and
schema are otherwise unchanged (`git diff` shows only the one `sha256` value differing).

## 4. Guards green (C4 / FR-008, SC-005)

```sh
dotnet test tests/FS.GG.Contracts.Tests        # AgentSurfaceDriftTests, SkillMirrorTests
dotnet test tests/FS.GG.SDD.Commands.Tests      # ProcessSkillManifestTests + seeding
```

Expected: all green — agent-surface-drift, skill-mirror, and process-skill-manifest guards pass.
(A full `dotnet test` at the solution root is the release-grade check.)

## 5. No collateral change (C5 / SC-005)

```sh
git diff --stat
```

Expected: changes limited to
`.claude/skills/fs-gg-sdd-evidence/SKILL.md`,
`.codex/skills/fs-gg-sdd-evidence/SKILL.md`,
`.agents/skills/skill-manifest.json` (one `sha256`),
the `specs/079-evidence-at-scale-guidance/` docs, and the `CLAUDE.md` SPECKIT plan pointer (if the
agent-context hook ran). No `src/**` behavior change, no other skill body, no schema/fixture churn.

## Done when

- [ ] Sections 1–5 pass.
- [ ] `fsgg-sdd` still builds and its evidence-stage behavior is unchanged (docs-only edit).
