# CLI smoke — `fsgg-sdd validate` (feature 020)

End-to-end against the Release `fsgg-sdd` binary
(`src/FS.GG.SDD.Cli/bin/Release/net10.0/FS.GG.SDD.Cli.dll`), real disposable
project trees driven through the actual `CommandWorkflow`, no mocks, no Governance
runtime installed. Env: `DOTNET_NOLOGO=1 NO_COLOR=1`.

## Full exhaustive sweep — `fsgg-sdd validate --json`

```
$ dotnet fsgg-sdd validate --json   # all four matrices
exit = 0   (overallPassed = true)
elapsed ≈ 5s
summary: passed=332  failed=0  skipped=91  coverageGaps=0  notValidated=0  overallPassed=true
matrices: lifecycle-output=351  determinism=50  baseline-conformance=20  compatibility=2   (423 cells total)
```

Every declared cell of every matrix is present with exactly one status (INV-1):
332 pass, 91 intentional skips (commands not applicable to a state, e.g. `ship`
on a fresh project), zero failures / coverage gaps / not-validated. The clean
sweep exits `0`.

## Projection parity & determinism

```
A. default == --json            → byte-IDENTICAL (both exit 1 on a partial --matrix run)
B. two --json runs (same tree)  → BYTE-IDENTICAL  (sensed fenced to null; SC-004)
C. --text (redirected)          → 0 ANSI escapes  (FR-003 degradation)
```

`--json` is the automation contract; `--text` is a portable ANSI-free projection
of the same facts; `--rich` is accepted and degrades to `--text` (research
Decision 6).

## Sensed fence & schema (FR-007 / INV-2 / INV-5)

```json
  "schemaVersion": 1,
  "sensed": {
    "startedAtUtc": null,
    "durationMs": null,
    "host": null
  }
```

The deterministic report carries `schemaVersion: 1`; all wall-clock / duration /
host facts are normalized to `null` inside the single fenced `sensed` object and
never affect `overallPassed` or the exit code.

## Partial run never reads as a full pass (INV-1 / FR-007)

```
$ dotnet fsgg-sdd validate --matrix compatibility --text
exit = 1   (overallPassed = false)
→ the compatibility matrix is evaluated (2 pass); the other three matrices' cells
  are reported notValidated, so a restricted run can never be mistaken for a full pass.
```

## No-Governance posture (FR-010 / INV-8 / SC-006)

The sweep runs to completion with no Governance runtime present, and the report
encodes no Governance route / profile / freshness / gate / verdict:

```
governance verdict tokens present: False
```

## Seeded single-cell regression (US1 Independent Test, FR-006)

Exercised by `tests/FS.GG.SDD.Validation.Tests/LifecycleMatrixTests.fs`: seeding a
divergence in one `command × projection × state` cell fails exactly that cell with
an actionable diagnostic naming the matrix + coordinates, while every other
declared cell passes or is skipped.
