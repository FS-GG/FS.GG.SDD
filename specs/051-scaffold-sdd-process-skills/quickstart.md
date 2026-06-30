# Quickstart / Validation: Seeded SDD-process skills

**Feature**: 051-scaffold-sdd-process-skills

Runnable scenarios proving the feature end-to-end. See
[contracts/seeded-skill-set.md](contracts/seeded-skill-set.md) for the declared
set and invariants, and [data-model.md](data-model.md) for entities.

## Prerequisites

- .NET SDK (`net10.0`), repo builds: `dotnet build`
- A scratch directory for seeding targets.

## Scenario 1 — `init` seeds the full skill set (US1, SC-001/SC-002)

```bash
mkdir -p /tmp/sdd-skill-init && cd /tmp/sdd-skill-init
fsgg-sdd init
# Expect: 15 skills × 2 surfaces present and non-empty
ls .claude/skills/fs-gg-sdd-* -d | wc -l   # 15
ls .codex/skills/fs-gg-sdd-*  -d | wc -l   # 15
test -s .claude/skills/fs-gg-sdd-lifecycle/SKILL.md && echo OK-claude
test -s .codex/skills/fs-gg-sdd-charter/SKILL.md   && echo OK-codex
```

**Expected**: each declared `fs-gg-sdd-*` skill present on both surfaces with a
non-empty body; no `fs-gg-sdd-project` skill is seeded.

## Scenario 2 — `scaffold` delivers the identical set via the shared seam (US1, FR-007/FR-008)

```bash
mkdir -p /tmp/sdd-skill-scaffold && cd /tmp/sdd-skill-scaffold
fsgg-sdd scaffold --provider <name>
# Same 15×2 skill files appear, produced by SDD's seam (not the provider):
diff <(cd /tmp/sdd-skill-init && ls .claude/skills/fs-gg-sdd-*/SKILL.md) \
     <(ls .claude/skills/fs-gg-sdd-*/SKILL.md)   # no differences in the set
```

**Expected**: identical skill set; the seeded skill paths are absent from
`.fsgg/scaffold-provenance.json` `producedPaths` (SDD-owned, not
`generatedProduct`).

## Scenario 3 — Re-run never clobbers author edits (US2, SC-003)

```bash
cd /tmp/sdd-skill-init
printf '\nLOCAL EDIT\n' >> .claude/skills/fs-gg-sdd-plan/SKILL.md
rm -f .claude/skills/fs-gg-sdd-tasks/SKILL.md       # simulate a missing one
fsgg-sdd init                                        # re-run
grep -q 'LOCAL EDIT' .claude/skills/fs-gg-sdd-plan/SKILL.md && echo PRESERVED
test -s .claude/skills/fs-gg-sdd-tasks/SKILL.md && echo REFILLED
```

**Expected**: the edited file is preserved verbatim; the deleted one is refilled
(partial-directory edge case); no error.

## Scenario 4 — Determinism (US3, SC-004 / FR-006)

```bash
mkdir /tmp/a /tmp/b
( cd /tmp/a && fsgg-sdd init ); ( cd /tmp/b && fsgg-sdd init )
diff -r /tmp/a/.claude/skills /tmp/b/.claude/skills && echo BYTE-IDENTICAL
# No run-varying content:
! grep -rqE '20[0-9]{2}-[0-9]{2}-[0-9]{2}|[0-9]{2}:[0-9]{2}:[0-9]{2}' \
    /tmp/a/.claude/skills/fs-gg-sdd-*/SKILL.md && echo NO-DATES
```

## Scenario 5 — Claude/Codex parity (US3, SC-004 / FR-002)

```bash
cd /tmp/sdd-skill-init
for d in .claude/skills/fs-gg-sdd-*; do n=$(basename "$d");
  cmp -s ".claude/skills/$n/SKILL.md" ".codex/skills/$n/SKILL.md" \
    && echo "PARITY $n" || echo "MISMATCH $n"; done
```

**Expected**: every declared skill identical across both surfaces.

## Automated coverage (maps to the test suite)

- `FS.GG.SDD.Commands.Tests/InitCommandTests` — Scenario 1 (skeleton shape, INV-1/INV-8)
- `FS.GG.SDD.Commands.Tests/SeededSkillsTests` (new) — INV-2/3/4/7 (parity, determinism, no-clobber, drift guard)
- `FS.GG.SDD.Acceptance.Tests/CompositionAcceptanceTests` — Scenario 2 (INV-5/INV-8, scaffold seam)
- refresh test — FR-005 (refresh preserves seeded skills)
