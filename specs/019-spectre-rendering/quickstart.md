# Quickstart & Validation: Rich Spectre.Console CLI Rendering

Feature: `019-spectre-rendering`

Runnable scenarios that prove the feature works end-to-end. Detailed contracts are
in `contracts/`; the projection mapping is in `data-model.md`.

## Prerequisites

- .NET SDK with `net10.0` (repo default).
- Restore succeeds (Spectre.Console is fetched from nuget.org on first restore).
- An initialized SDD project tree (use `fsgg-sdd init` in a scratch directory, as
  the existing CLI smoke evidence does).

## Build & test

```bash
dotnet build -c Release FS.GG.SDD.sln
dotnet test  -c Release FS.GG.SDD.sln
```

Expected: clean Release build; full suite green, including the new
`FS.GG.SDD.Cli.Tests` rich-projection, automation-invariance, no-ANSI, and
stream/exit-parity tests, with every prior test still passing (SC-002).

## Scenario 1 — Rich output in an interactive terminal (US1)

In a real terminal (TTY), against an initialized project with an evidence-ready
work item:

```bash
fsgg-sdd verify --work <id> --rich
```

Expected: a paneled, color-coded rendering showing the command, work item,
outcome badge, any diagnostics grouped by severity, generated-view currency, and
the recommended next command — all derived from the same report JSON emits.

## Scenario 2 — Safe degradation when redirected (US2)

```bash
fsgg-sdd verify --work <id> --rich > out.txt
# inspect captured bytes for escape sequences:
grep -c $'\x1b' out.txt    # expected: 0
```

Expected: `out.txt` is the plain-text projection with **zero** ANSI sequences
(SC-003), byte-identical to `fsgg-sdd verify --work <id> --text`.

## Scenario 3 — Color disabled via NO_COLOR (US2)

```bash
NO_COLOR=1 fsgg-sdd verify --work <id> --rich
```

Expected: no color/ANSI sequences; information content preserved.

## Scenario 4 — Automation truth unchanged (US2 / SC-002)

```bash
fsgg-sdd verify --work <id>            > a.json   # default JSON
fsgg-sdd verify --work <id> --json     > b.json   # explicit JSON
diff a.json b.json                                 # expected: identical
```

Expected: default output is unchanged JSON; exit code matches the pre-feature
behavior for the same report.

## Scenario 5 — Explicit format selection (US3)

```bash
fsgg-sdd plan --work <id> --json   # deterministic JSON projection
fsgg-sdd plan --work <id> --text   # portable plain text
fsgg-sdd plan --work <id> --rich   # rich (or plain text if non-interactive)
```

Expected: all three project the same report with consistent outcome and exit code.

## Scenario 6 — Blocked outcome routing (SC-005)

Run a command that blocks (e.g. a verify on work missing required evidence) with
`--rich` in a terminal and with output redirected:

Expected: rendered output goes to **stderr** and the process exits with the same
blocked exit code as `--json` and `--text`.

## Done / acceptance

- Scenarios 1–6 pass.
- New CLI tests cover projection completeness, automation invariance, no-ANSI
  degradation, and stream/exit parity.
- No JSON fixture, schema, or lifecycle-ordering change.
