# CLI smoke — feature 021 (rich `validation-report`)

End-to-end against the Release host binary
(`src/FS.GG.SDD.Cli/bin/Release/net10.0/FS.GG.SDD.Cli.dll`), `--matrix
compatibility` (the cheapest matrix). Date: 2026-06-21.

| Scenario | Command | Result |
|---|---|---|
| JSON sensed fence | `validate --matrix compatibility --json` | `"startedAtUtc": null`, `"durationMs": null`, `"host": null` present; `schemaVersion: 1` |
| Text projection | `validate --matrix compatibility --text` | portable plain text, non-pass cells listed |
| Rich redirected | `validate --matrix compatibility --rich` > file | **byte-identical to `--text`**, **0 ESC bytes** (degrades) |
| NO_COLOR rich | `NO_COLOR=1 validate … --rich` | **0 ESC bytes** (degrades) |
| Rich `--out` | `validate … --rich --out <path>` | persisted file **0 ESC bytes**, equals `--text` (trailing newline aside) |
| Rich interactive (pty via `script`) | `validate … --rich` | real Spectre output: colored verdict (`not passed`), Matrices rollup table, per-matrix sections, red-emphasized `notValidated` tokens — captured in `rich-tty-capture.ansi` |

A latent crash was found and fixed during this smoke: under a pseudo-TTY
`Console.WindowWidth` reports `0`, and `Profile.Width <- 0` throws
`InvalidOperationException: Console width must be greater than zero`. Both
`resolve` (CommandReport, feature 019) and the new `resolveValidation` now guard
`Some width when width > 0`. Regression covered by the interactive resolve tests.

Exit code is identical across `--json` / `--text` / `--rich` and non-zero for the
single-matrix (partial) run — verified in `ValidateCommandTests.fs`.
