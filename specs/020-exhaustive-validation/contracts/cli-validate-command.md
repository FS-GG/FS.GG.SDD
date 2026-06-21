# Contract: `fsgg-sdd validate` CLI command

`validate` is a **CLI-level** command (a peer of `--version`), not a `SddCommand`
(research Decision 2). It is dispatched in `Program.fs` before lifecycle command
parsing, leaving `parseCommand`, `CommandReport`, and every per-command contract
unchanged (FR-011).

## Invocation

```
fsgg-sdd validate [--json | --text] [--matrix <name>] [--out <path>]
```

- `--json` (default) — emit the deterministic `validation-report` JSON to stdout (the
  automation contract). Default for the unknown-flag and no-flag paths, consistent
  with every other command.
- `--text` — emit a portable plain-text projection of the same report (human triage).
- `--matrix <name>` — optionally restrict the run to one declared matrix
  (`lifecycle-output | determinism | baseline-conformance | compatibility`); omitted
  runs all four. Restricting still reports the **other** matrices' cells as
  `notValidated` so a partial run never reads as a full pass (INV-1 / FR-007).
- `--out <path>` — optionally also write the report to a file (a scheduled CI run
  typically redirects stdout instead; either is supported).

`--rich` is accepted but **degrades to `--text`** for the report in this feature
(research Decision 6); it is not an error.

> **Note on the CLI three-way convention.** CLAUDE.md's "the CLI projects the same
> `CommandReport` three ways (`--rich` > `--text` > `--json`)" describes the
> **`CommandReport`** projection family. The `validation-report` is a *distinct*
> contract (not a `CommandReport`), so it intentionally offers `--json` + `--text`
> only, with `--rich` deferred (research Decision 6). This is a scope choice, not a
> divergence from the `CommandReport` convention, which is untouched (FR-011).

## Stream routing & exit code

- The report is written to **stdout** on success (`overallPassed = true`, exit `0`)
  and on a clean run that found failures (the report is still valid output) — failures
  are encoded **in** the report, not as a malformed-input error.
- A genuine tool defect (e.g. the harness cannot construct a fixture) writes a
  diagnostic to **stderr** and exits non-zero.
- Exit code: `0` iff `summary.overallPassed`; otherwise non-zero (FR-006 / contract
  validation-report C-3).

## Determinism

Two `fsgg-sdd validate` runs over an identical source tree produce a byte-identical
report once the `sensed` object is excluded (FR-007 / SC-004). The JSON carries no
ANSI; `--text` carries no ANSI when color is disabled or output is redirected,
reusing the feature-019 degradation rule.

## No-Governance

`fsgg-sdd validate` runs to completion and emits its report with no Governance runtime
installed (FR-010 / SC-006). No artifact it produces contains a Governance route,
profile, freshness, gate, or release verdict.

## Agent & doc surface (Principle VII / FR-011)

CLAUDE.md, AGENTS.md, both `fs-gg-sdd-project` skills, README, and the docs describe
`validate` identically: a scheduled/on-demand cross-cutting validation sweep, separate
from the inner loop, requiring no Governance, emitting one deterministic report.
