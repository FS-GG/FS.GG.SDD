# Quickstart: validating feature 063

## Prerequisites

- .NET SDK (`net10.0`); `dotnet restore` (see the build-env memory if NU1403 hits).

## 1. Drift is visible in the human projections (US1)

```bash
dotnet build -c Release
dotnet test tests/FS.GG.SDD.Cli.Tests/FS.GG.SDD.Cli.Tests.fsproj -c Release
```

Expected: `RemediationProjectionTests` asserts each `skillDriftPaths` entry appears
in `doctor`/`upgrade` **text** and **rich** output, and that the JSON is byte-identical
(FR-004). The empty-list case emits only the `…SkillDrifts: 0` count.

Manual smoke (real CLI, a state with drifted skills):

```bash
fsgg-sdd doctor --text | grep SkillDrift    # each drifted path listed
fsgg-sdd doctor --json | diff - <(prior)    # JSON unchanged
```

## 2. Report content tells the truth (US2)

- `unknownCommand` correction names all 18 commands:
  `fsgg-sdd frobnicate --json` → correction lists init…ship, agents, refresh,
  scaffold, doctor, upgrade, validate, registry. Pin test in `Commands.Tests`.
- reseed `NextAction` affected paths include `.agents/skills` (Commands test).
- `projectRoot` stays `"."` (B2) with a rationale comment at `ReportAssembly.fs`.

## 3. Docs match the product (US3)

```bash
grep -l "doctor" README.md docs/quickstart.md            # both mention doctor/upgrade
grep "reference/doctor-upgrade.md" docs/index.md         # linked
grep -c "empty Spec Kit product scaffold" docs/index.md  # 0
grep "FS.GG.Contracts" DEVELOPING.md                     # five projects listed
```

## 4. Full gate (US2 invariants + SC-006)

```bash
dotnet test                        # green; only enumerated text goldens changed (FR-012)
fsgg-sdd validate                  # overallPassed
```

Expected: `doctor`/`upgrade` **JSON** goldens unmodified; only the enumerated
**text** goldens (skillDrift lines), the `unknownCommand` correction, and the reseed
`NextAction` baselines change.
