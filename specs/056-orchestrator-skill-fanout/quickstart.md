# Quickstart / Validation: Orchestrator skill fan-out

**Feature**: `056-orchestrator-skill-fanout` | Validates FR-001…FR-013, SC-001…SC-005, P1–P10.

Prereqs: .NET 10 SDK; `dotnet build FS.GG.SDD.sln`. Scaffold tests drive real `dotnet new`
providers under `tests/fixtures/scaffold-provider/` (no mocks), serialized by
`[<Collection("Scaffold")>]`.

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Scaffold
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~SeededSkills
dotnet test tests/FS.GG.SDD.Commands.Tests --filter "FullyQualifiedName~Refresh|FullyQualifiedName~Doctor|FullyQualifiedName~Upgrade"
dotnet test tests/FS.GG.SDD.Cli.Tests      --filter FullyQualifiedName~ScaffoldParity
```

## Scenario A — init seeds all three roots byte-identically (US3, SC-003, P5)

```bash
fsgg-sdd init    # in an empty temp dir
```

Expect `.claude/skills/`, `.codex/skills/`, and `.agents/skills/` each to contain the 15
`fs-gg-sdd-*` skills, byte-for-byte identical across roots. Two runs are byte-stable.

## Scenario B — compliant scaffold fans out the union (US1, SC-001, P6/P7)

Fixture: **new** `skills-agents-cotenant` — writes `.agents/skills/fs-gg-elmish/SKILL.md` + an
ordinary product file; declares the `lifecycle` symbol.

```bash
fsgg-sdd scaffold --provider fixture   # temp dir with the skills-agents-cotenant registry
```

Expect exit **0**, `providerSucceeded`, **no** `providerWroteSddTree`. All three roots hold
`fs-gg-sdd-*` **and** `fs-gg-elmish`, byte-identical. In `.fsgg/scaffold-provenance.json`:
`.agents/skills/fs-gg-elmish/SKILL.md` in `producedPaths` (`generatedProduct`);
`.claude/skills/fs-gg-elmish/SKILL.md` + `.codex/skills/fs-gg-elmish/SKILL.md` in `mirroredPaths`
(`mirrored`); no `fs-gg-sdd-*` path in either array; `schemaVersion` still `1`.

## Scenario C — strict guard rejects .claude/.codex intrusion (US2, SC-002, P1)

Fixtures: providers writing `.claude/skills/fs-gg-x/` and `.codex/skills/fs-gg-x/`.

```bash
fsgg-sdd scaffold --provider fixture
```

Expect exit **2**, `scaffold.providerWroteSddTree`, `providerFailed`, no fan-out; intruded paths
absent from `producedPaths`/`mirroredPaths`.

## Scenario D — reserved namespace rejected in the neutral root (US2, SC-002, P2)

Fixture: **new** `skills-intrusion-agents` — writes `.agents/skills/fs-gg-sdd-custom/SKILL.md`.

```bash
fsgg-sdd scaffold --provider fixture
```

Expect exit **2**, `scaffold.providerWroteSddTree` (the `fs-gg-sdd-*` namespace is reserved even in
`.agents/skills/`); the path is absent from produced/mirrored.

## Scenario E — refresh re-mirrors; doctor/upgrade reconcile drift (US3, SC-004, P8/P9)

After Scenario B, delete the `.agents/skills/` copies of the seeded skills (simulate a pre-056
product):

```bash
fsgg-sdd doctor     # reports three-root drift, read-only, exit 0
fsgg-sdd upgrade    # (confirmed) re-seeds the missing root no-clobber; residual drift = 0
fsgg-sdd refresh    # re-mirrors the union byte-identically across all three roots
```

Expect `doctor` to name the missing/divergent root without writing; `upgrade` to reconcile to zero
residual drift preserving author edits; `refresh` to leave `claude ≡ codex ≡ agents = union`.

## Scenario F — additive, auditable projections (US1/US3, SC-005, P10)

Diff the json report + provenance from Scenario B against an equivalent pre-mirror scaffold. Only
additive differences (the `mirroredPaths` entries; the extra root's files). `schemaVersion` still
`1`. All three projections (json/text/rich) carry the mirrored facts identically; the rich path
changes no JSON byte.

## Mapped tests

| Scenario | Requirement(s) | Test(s) |
|----------|----------------|---------|
| A | FR-004, SC-003, P5 | `SeededSkillsTests` init-three-roots byte-identity (extended) |
| B | FR-005/006/007, SC-001, P6/P7 | new `scaffold fans out the union to all three roots` (`ScaffoldCommandTests`) + provenance `mirroredPaths` guard |
| C | FR-001, SC-002, P1 | `.claude`/`.codex` intrusion negatives (kept strict) |
| D | FR-002, SC-002, P2 | new `.agents/skills/fs-gg-sdd-*` intrusion negative |
| E | FR-009/010, SC-004, P8/P9 | refresh re-mirror; doctor/upgrade three-root drift tests |
| F | FR-008, SC-005, P10 | `ScaffoldParityTests` mirrored-path parity; provenance schema-v1 additive guard |
| — (unit) | strict truth table | `isSddTree`/`isSddOwned` truth-table unit test (data-model E-table) |
