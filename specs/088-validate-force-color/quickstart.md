# Quickstart: Force-Color Override + Markdown Report Card

Runnable validation scenarios proving the feature end-to-end. Run from repo root.

## Prerequisites

```bash
dotnet build FS.GG.SDD.sln
CLI="dotnet run --project src/FS.GG.SDD.Cli --"
```

## Scenario 1 — Rich degrades on a pipe (baseline, unchanged)

```bash
$CLI validate --rich | cat            # stdout is a pipe → zero-ANSI plain text
```
Expected: no ANSI escape sequences (degraded to plain text).

## Scenario 2 — Force color through a pipe

```bash
FORCE_COLOR=1 $CLI validate --rich | cat
$CLI validate --rich --force-color | cat
```
Expected: both emit ANSI escape sequences (rich rendering) despite the pipe.

## Scenario 3 — NO_COLOR wins over force-color

```bash
NO_COLOR=1 FORCE_COLOR=1 $CLI validate --rich | cat
NO_COLOR=1 $CLI validate --rich --force-color | cat
```
Expected: zero ANSI in both — `NO_COLOR` overrides the force-color request.

## Scenario 4 — FORCE_COLOR non-forcing values

```bash
FORCE_COLOR=0 $CLI validate --rich | cat      # 0 → not forcing
FORCE_COLOR=  $CLI validate --rich | cat      # empty → not forcing
```
Expected: zero ANSI (degraded) in both.

## Scenario 5 — Force color applies to a non-validate command (uniform gate)

```bash
FORCE_COLOR=1 $CLI status --rich --work <some-id> | cat   # any --rich-capable command
```
Expected: ANSI present — the override is not validate-specific.

## Scenario 6 — Markdown report card

```bash
$CLI validate --markdown
$CLI validate --markdown | cat            # identical whether or not piped (deterministic)
```
Expected: a Markdown document with `# Validation Report`, `**Verdict:** …`, a Summary table, a Matrices table, and a Non-passing cells section — zero ANSI in every context. Two runs are byte-identical.

## Scenario 7 — Precedence

```bash
$CLI validate --markdown --text --json     # → Markdown (markdown beats text/json)
$CLI validate --rich --markdown | cat      # → Rich (rich beats markdown)
```

## Scenario 8 — Persist the card, exit on verdict only

```bash
$CLI validate --markdown --out /tmp/card.md ; echo "exit=$?"
cat /tmp/card.md
```
Expected: `/tmp/card.md` holds the Markdown card; exit code reflects only the report verdict (0 if passed, 1 if not).

## Scenario 9 — Invariants (contracts unchanged)

```bash
diff <($CLI validate) <($CLI validate --json)                 # default == --json
diff <($CLI validate --json) <(FORCE_COLOR=1 $CLI validate --json)   # force-color no-op on JSON
```
Expected: no differences.

## Automated coverage

- `tests/FS.GG.SDD.Validation.Tests/ValidationMarkdownTests.fs` — `renderMarkdown` determinism/parity/zero-ANSI/empty/optional-fields.
- `tests/FS.GG.SDD.Cli.Tests/ForceColorTests.fs` — capability mapping across the truth table.
- `tests/FS.GG.SDD.Cli.Tests/FormatSelectionTests.fs` — `selectValidationFormat` precedence.
- `tests/FS.GG.SDD.Cli.Tests/DegradationTests.fs` — force-color re-enables rich; NO_COLOR degrades.
- `tests/FS.GG.SDD.Cli.Tests/ValidationRichRenderingTests.fs` — validate markdown/`--out`/exit-code interplay.
```
