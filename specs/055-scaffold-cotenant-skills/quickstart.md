# Quickstart / Validation: Scaffold co-tenant skills under the shared skill roots

**Feature**: `055-scaffold-cotenant-skills` | Validates FR-001…FR-011, SC-001…SC-004.

Prereqs: .NET 10 SDK; `dotnet build FS.GG.SDD.sln`. Scaffold tests drive real `dotnet new`
providers under `tests/fixtures/scaffold-provider/` (no mocks) and are serialized by
`[<Collection("Scaffold")>]`.

Run the feature's tests:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Scaffold
dotnet test tests/FS.GG.SDD.Cli.Tests      --filter FullyQualifiedName~ScaffoldParity
```

## Scenario A — Compliant co-tenant scaffold completes (US1, SC-001, SC-002)

Fixture: **new** `skills-cotenant` — a `dotnet new` template that writes a non-reserved skill
`.claude/skills/fs-gg-elmish/SKILL.md` plus an ordinary product file, and declares the
`lifecycle` symbol.

```bash
fsgg-sdd scaffold --provider fixture   # in a temp dir with the skills-cotenant registry
```

Expect exit **0**, outcome `providerSucceeded`, **no** `scaffold.providerWroteSddTree`. In the
JSON report and `.fsgg/scaffold-provenance.json`:
`.claude/skills/fs-gg-elmish/SKILL.md` appears in `producedPaths` as `Owner = generatedProduct`.
The 15 seeded `fs-gg-sdd-*` skills exist on disk and are **absent** from `producedPaths`
(they are skeleton, not product).

## Scenario B — Seeded skills survive byte-identical (US1-AC3, SC-002)

After Scenario A, compare each seeded `.claude/skills/fs-gg-sdd-*/SKILL.md` and
`.codex/skills/fs-gg-sdd-*/SKILL.md` against what `fsgg-sdd init` seeds:

```bash
# init a second temp dir and diff the seeded skill trees
diff -r <cotenant-root>/.claude/skills <init-root>/.claude/skills   # only fs-gg-elmish extra
```

Expect the `fs-gg-sdd-*` subtrees byte-identical; the only difference is the extra co-tenant
`fs-gg-elmish` directory in the scaffolded root.

## Scenario C — Reserved-namespace intrusion still rejected (US2-AC1/AC2, SC-003)

Fixture: **re-pointed** `skills-intrusion` — now writes `.claude/skills/fs-gg-sdd-custom/SKILL.md`
and `.codex/skills/fs-gg-sdd-custom/SKILL.md` (a *new* reserved-namespace path, research R3).

```bash
fsgg-sdd scaffold --provider fixture   # in a temp dir with the skills-intrusion registry
```

Expect exit **2**, `scaffold.providerWroteSddTree`, outcome `providerFailed`; the intruded skill
paths are **absent** from `producedPaths` on both roots (symmetric).

## Scenario D — Other SDD trees still rejected (US2-AC3, SC-003)

Fixture: existing `lifecycle-intrusion` (writes into `work/`, `readiness/`). Behavior unchanged:

```bash
fsgg-sdd scaffold --provider fixture   # lifecycle-intrusion registry
```

Expect exit **2**, `scaffold.providerWroteSddTree`, and `work/leak.txt` / `readiness/leak.txt`
absent from `producedPaths`.

## Scenario E — Auditable, additive report/provenance (US3, SC-004)

Diff the JSON report and provenance from Scenario A against an equivalent pre-change scaffold
(or the golden). Confirm the only differences are the additional co-tenant produced-path
entries, and `.fsgg/scaffold-provenance.json` `schemaVersion` is still `1`. Confirm all three
projections (json/text/rich) list the co-tenant path under produced product and none lists it as
SDD-owned.

## Mapped tests

| Scenario | Requirement(s) | Test(s) |
|----------|----------------|---------|
| A | FR-001/004/006, SC-001 | new `scaffold provider writing a co-tenant skill succeeds` (`ScaffoldCommandTests.fs`) |
| B | FR-005/010, SC-002 | new byte-identical seeded-skill assertion + `SeededSkillsTests` (unchanged) |
| C | FR-002/007/009/011, SC-003 | re-pointed `scaffold provider writing into the seeded skill trees is a provider defect` |
| D | FR-003, SC-003 | existing `scaffold provider writing SDD trees under lifecycle=sdd is a provider defect` (unchanged) |
| E | FR-004/008, SC-004 | `ScaffoldParityTests` co-tenant produced path; provenance schema-v1 guard |
| — (unit) | R1/R5 truth table | new `isSddTree`/`isSddOwned` reserved-namespace unit test |
