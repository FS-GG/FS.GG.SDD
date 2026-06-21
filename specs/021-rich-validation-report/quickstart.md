# Quickstart: Rich `validation-report` rendering

Runnable validation scenarios proving `fsgg-sdd validate --rich` renders the
`validation-report` richly in a terminal, degrades safely elsewhere, and leaves the
deterministic JSON/text contracts untouched. The `compatibility` matrix is the
cheapest (no per-state lifecycle builds), so it is used for fast smokes.

## Prerequisites

```bash
dotnet build -c Release FS.GG.SDD.sln
```

The CLI host is `src/FS.GG.SDD.Cli/bin/Release/net10.0/FS.GG.SDD.Cli.dll`
(invoke via `dotnet <dll>` or the installed `fsgg-sdd` tool).

## Scenario 1 — Rich triage in an interactive terminal (US1 / SC-001)

Run in a real, color-capable terminal:

```bash
fsgg-sdd validate --matrix compatibility --rich
```

**Expected**: a colored verdict (passed/not-passed), the summary counts
(passed / failed / skipped / coverageGaps / notValidated), a per-matrix rollup, and
each non-passing cell with its matrix, coordinates, and diagnostic. Because a
single-matrix run reports the other matrices as `notValidated`, the verdict is
**not passed** and those cells are emphasized as failing.

## Scenario 2 — Safe, clean output when redirected (US2 / SC-002 / SC-003)

```bash
fsgg-sdd validate --matrix compatibility --rich > out.txt
# zero ANSI escape bytes:
! grep -qP '\x1b' out.txt && echo "PASS: no ANSI"

# default JSON bytes + exit code unchanged from before this feature:
fsgg-sdd validate --matrix compatibility --json > report.json
grep -q '"schemaVersion": 1' report.json && echo "PASS: json contract"
grep -q '"startedAtUtc": null' report.json && echo "PASS: sensed fenced"
```

**Expected**: redirected rich output is the plain-text projection with no ESC
bytes; the `--json` projection (including the `sensed` null fence) is byte-stable.

## Scenario 3 — Color disabled (US2 / FR-005)

```bash
NO_COLOR=1 fsgg-sdd validate --matrix compatibility --rich | cat
TERM=dumb fsgg-sdd validate --matrix compatibility --rich | cat
```

**Expected**: both fall back to plain text with no color/ANSI while preserving the
information content.

## Scenario 4 — Choose the right format (US3 / SC-005)

```bash
fsgg-sdd validate --matrix compatibility --json; echo "exit=$?"
fsgg-sdd validate --matrix compatibility --text; echo "exit=$?"
fsgg-sdd validate --matrix compatibility --rich | cat; echo "exit=$?"
```

**Expected**: each prints the matching projection of the same report on **stdout**
with the **same exit code** (non-zero for a single-matrix run). `--rich` renders
richly in an interactive terminal and falls back to text when piped.

## Scenario 5 — `--out` stays deterministic with `--rich` (FR-010)

```bash
fsgg-sdd validate --matrix compatibility --rich --out persisted.txt
! grep -qP '\x1b' persisted.txt && echo "PASS: persisted file has no ANSI"
```

**Expected**: the persisted file is a deterministic projection (plain text) with no
ANSI; rich affects interactive stdout only.

## Automated coverage

- `tests/FS.GG.SDD.Cli.Tests/ValidationRichRenderingTests.fs` — projection
  completeness, status differentiation, verdict, no-ANSI, automation invariance,
  single-failing-cell isolation (color-off Spectre console).
- `tests/FS.GG.SDD.Cli.Tests/ValidateCommandTests.fs` — end-to-end `--rich`
  redirected (no ANSI), `--json` byte-stability, exit-code parity via the real
  host binary.
- `tests/FS.GG.SDD.Cli.Tests/SurfaceBaselineTests.fs` — the two new public
  functions match `PublicSurface.baseline`.

See [contracts/validation-rich-rendering-projection.md](./contracts/validation-rich-rendering-projection.md)
and [contracts/validation-output-format-selection.md](./contracts/validation-output-format-selection.md)
for the behavioral contracts and [data-model.md](./data-model.md) for the
projection mapping and invariants.
