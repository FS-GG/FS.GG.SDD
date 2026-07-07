# Implementation Plan: Force-Color Override + Capture-Safe Markdown Report Card

**Branch**: `088-validate-force-color` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/088-validate-force-color/spec.md`

## Summary

Two additive changes to the `fsgg-sdd` CLI presentation edge, resolving FS.GG.SDD#172:

1. **Force-color override (all `--rich`-capable commands).** A `FORCE_COLOR` env var (boolean-ish) and a `--force-color` flag re-enable rich ANSI even when the sink is redirected/non-interactive or `TERM=dumb`, so the human report survives an agent harness. `NO_COLOR` remains an unconditional override. Implemented by folding a `forceColor` signal into the single shared capability sensing (`detectCapabilities`), so the two rich-degrade gates (`resolve`, `resolveValidation`) stay byte-identical.

2. **Capture-safe Markdown report card (`validate`-only).** A new deterministic, ANSI-free `renderMarkdown` projection of the `validation-report`, selected by `--markdown`, with precedence `--rich > --markdown > --text > --json > default`. Lives in the `FS.GG.SDD.Validation` library alongside `serialize`/`renderText`, so it is unit-testable and reusable, and is persisted by `validate --out` when selected.

No JSON contract byte, exit code, stream-routing rule, or persisted schema changes. **Change tier: Tier 1** (public CLI command surface + new public functions + `.fsi` + surface baseline).

## Technical Context

**Language/Version**: F# on .NET, `net10.0` (constitution default).

**Primary Dependencies**: Spectre.Console (rich rendering, already present); no new packages.

**Storage**: N/A (CLI-local; `--out` writes a text projection to an author-named path, unchanged mechanism).

**Testing**: xUnit across `tests/FS.GG.SDD.Validation.Tests` (renderMarkdown unit tests) and `tests/FS.GG.SDD.Cli.Tests` (format selection, force-color capability mapping, validate markdown/`--out`/exit-code, degradation). Deterministic fixed-width Spectre harness pattern reused from `ValidationRichRenderingTests`.

**Target Platform**: Cross-platform CLI (Linux/macOS/Windows terminals, pipes, CI logs, agent harnesses).

**Project Type**: Single project — CLI (`FS.GG.SDD.Cli`) over the `FS.GG.SDD.Validation` contract library.

**Performance Goals**: N/A (interactive CLI; rendering is O(cells), unchanged order).

**Constraints**: Markdown projection MUST be deterministic (no wall-clock/width/sensed/ANSI). Force-color MUST NOT perturb JSON/text/default bytes, exit codes, or stream routing. `NO_COLOR` MUST always win.

**Scale/Scope**: ~2 source pairs (`ValidationContracts.fs`/`.fsi`, `Rendering.fs`/`.fsi`) + `Program.fs`/`RegistryValidate.fs` call-site threading; ~4 test files; docs + surface baseline.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation**: Followed. `.fsi` updates (`renderMarkdown` in `ValidationContracts.fsi`; `forceColor` param + `selectValidationFormat`/`forceColorRequested` in `Rendering.fsi`) are authored before `.fs` bodies; semantic tests through the public surface precede implementation. ✅
- **II. Structured Artifacts Are the Machine Contract**: The JSON `validation-report` remains the authoritative machine contract, byte-unchanged. Markdown is an added human projection (like `--text`/`--rich`), never authoritative. No prose/structured conflict introduced. ✅
- **III. Visibility Lives in `.fsi`**: New public functions declared in `.fsi`; `PublicSurface.baseline` (Cli.Tests) refreshed for the changed `detectCapabilities` arity + new functions. ✅
- **IV. Idiomatic Simplicity**: Plain functions, a small 2-case `ValidationFormat` DU, string building. No new abstraction, custom operators, or reflection. ✅
- **V. Elmish/MVU Boundary**: `validate` stays a thin CLI-level command with pure projections; env/flag reads are edge reads at `Program.fs`/`Rendering`. Rendering functions remain pure over their inputs (report + capabilities). No new stateful workflow. ✅
- **VI. Test Evidence Is Mandatory**: New tests fail before and pass after — force-color capability mapping, `FORCE_COLOR` boolean-ish, `NO_COLOR`-wins, 4-way precedence, Markdown determinism/parity/zero-ANSI/empty, `--out` persistence, exit-code/routing invariance. Golden coverage for the new deterministic Markdown projection. ✅
- **VII. Agent And Human Workflows Share One Contract**: The Markdown report card is a projection of the same `validation-report` all consumers see — no second source of truth. Directly serves the agent-harness use case in the issue. ✅
- **VIII. Observability And Safe Failure**: `--out` write failures already surface a stderr diagnostic + exit 1 (unchanged, now also for the Markdown projection). Force-color changes only presentation, never failure classification. ✅

**Result: PASS.** No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/088-validate-force-color/
├── plan.md              # This file
├── research.md          # Phase 0: decisions (gate model, FORCE_COLOR semantics, Markdown format, scope)
├── data-model.md        # Phase 1: capability/format/projection model
├── quickstart.md        # Phase 1: runnable validation scenarios
├── contracts/
│   ├── validate-output-format-selection.md   # 4-way precedence incl. --markdown
│   ├── color-gate.md                          # NO_COLOR > force-color > sensing
│   └── markdown-report-card.md                # deterministic Markdown projection shape
├── checklists/
│   ├── requirements.md                        # (from /speckit-specify)
│   └── output-projection.md                   # (from /speckit-checklist)
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/FS.GG.SDD.Validation/
├── ValidationContracts.fsi   # + val renderMarkdown: ValidationReport -> string
└── ValidationContracts.fs    # + renderMarkdown (deterministic, ANSI-free)

src/FS.GG.SDD.Cli/
├── Rendering.fsi             # detectCapabilities gains forceColor param;
│                             #   + forceColorRequested, ValidationFormat, selectValidationFormat
├── Rendering.fs              # fold force-color into effective capabilities; 4-way validate selector
├── Program.fs                # printValidate: markdown branch + --out persistence; thread forceColor
│                             #   into every detectCapabilities call site (validate/help/generic)
└── RegistryValidate.fs       # thread forceColor into its detectCapabilities call (uniform gate)

tests/FS.GG.SDD.Validation.Tests/
└── ValidationMarkdownTests.fs    # renderMarkdown determinism/parity/zero-ANSI/empty/optional-fields

tests/FS.GG.SDD.Cli.Tests/
├── FormatSelectionTests.fs       # + selectValidationFormat 4-way precedence
├── ForceColorTests.fs (new)      # detectCapabilities+forceColorRequested: env/flag/NO_COLOR/TERM=dumb
├── ValidationRichRenderingTests.fs # + validate markdown path selection & degradation interplay
├── DegradationTests.fs           # + force-color re-enables rich on redirected sink (resolve gate)
└── PublicSurface.baseline        # refreshed for the new/changed public surface

docs/
└── (the validate command reference doc)  # document --force-color, FORCE_COLOR, --markdown
```

**Structure Decision**: Single-project CLI over the Validation contract library. The Markdown renderer lives in `FS.GG.SDD.Validation` (a peer of `serialize`/`renderText`, keeping it deterministic and library-testable). The force-color signal lives in the CLI's shared `Rendering` sensing so it applies uniformly to every `--rich`-capable command without per-command wiring. `OutputFormat` (the shared machine-contract DU) is left at its three cases; `--markdown` is a `validate`-local `ValidationFormat` selection so the validate-only Markdown card does not leak a half-supported case into every command's `resolve`.

## Design Detail

### Color gate (force-color)

`detectCapabilities` gains a leading `forceColor: bool` parameter and computes **effective** capability booleans, so the two rich-degrade gates (`resolve`/`resolveValidation`, predicate `IsInteractive && ColorEnabled`) stay byte-identical:

```
noColorPresent = env "NO_COLOR" present (any value)     // unchanged, hard override
dumbTerminal   = env "TERM" = "dumb"                    // unchanged
IsInteractive  = (not outputRedirected) || forceColor            // force overrides redirect
ColorEnabled   = (not noColorPresent) && ((not dumbTerminal) || forceColor)  // force overrides dumb, never NO_COLOR
Width          = if outputRedirected then None else Some Console.WindowWidth  // uses RAW redirect (no WindowWidth read on a pipe)
```

Truth table (proves `NO_COLOR > force-color > sensing`):

| redirected | TERM=dumb | NO_COLOR | force | IsInteractive | ColorEnabled | rich? |
|---|---|---|---|---|---|---|
| no  | no  | no  | –   | true  | true  | **yes** (baseline) |
| yes | no  | no  | no  | false | true  | no (degrade, baseline) |
| yes | no  | no  | yes | true  | true  | **yes** |
| yes | yes | no  | yes | true  | true  | **yes** |
| yes | no  | yes | yes | true  | false | no (NO_COLOR wins) |
| no  | yes | no  | no  | true  | false | no (baseline) |

`forceColor` is supplied by a new pure helper `forceColorRequested (args) = forceColorEnv() || List.contains "--force-color" args`, where `forceColorEnv()` reads `FORCE_COLOR` boolean-ish (`null`/`""`/`"0"` → false, else true). Every `detectCapabilities` call site passes `forceColorRequested <its args>`: `Program.fs` validate (112), help (170), generic (212/259), and `RegistryValidate.fs` (156).

### Format selection (Markdown)

`OutputFormat` (shared) is unchanged. A `validate`-local selector in `Rendering`:

```
type ValidationFormat = Standard of OutputFormat | MarkdownCard
let selectValidationFormat args =
    if   List.contains "--rich"     args then Standard Rich
    elif List.contains "--markdown" args then MarkdownCard
    elif List.contains "--text"     args then Standard Text
    elif List.contains "--json"     args then Standard Json
    else Standard Json
```

`printValidate` routes: `MarkdownCard -> ValidationContracts.renderMarkdown report`; `Standard fmt -> (resolveValidation fmt caps report).Text`. `--out` persists the deterministic projection for the selected format: `MarkdownCard -> renderMarkdown`, `Standard Json -> serialize`, otherwise `renderText` (Rich never persisted — unchanged rule). Exit code and stream routing unchanged.

### Markdown report card shape

`renderMarkdown : ValidationReport -> string` — pure, deterministic, ANSI-free. Mirrors the rich projection's fact set: an `# ...` title, a `**Verdict:** passed|not passed` line, a `## Summary` table of the five counts, a `## Matrices` per-matrix rollup table (matrices sorted by name), and a `## Non-passing cells` section listing each non-passing cell as a bullet (`- (dim=val, …) **token**: detail`) under a per-matrix subheading, with `All evaluated cells pass.` when a matrix has none. Passing cells are summarized, never enumerated. `schemaVersion`/`generatorVersion` are omitted (parity with rich; absence is not a failure). Sensed metadata is never emitted. Table-fragile text (matrix names, cell detail placed in tables) escapes `|`. See `contracts/markdown-report-card.md`.

## Verification Plan

- **Unit (Validation.Tests)**: `renderMarkdown` — all-pass verdict + five counts + zero non-passing; mixed report surfaces every non-passing cell with coordinates+token, passing summarized; byte-identical across two runs; zero ANSI (`` absent) regardless of env; absent `schemaVersion`/`generatorVersion` tolerated; empty/all-pass still well-formed (non-empty).
- **Format selection (Cli.Tests)**: `selectValidationFormat` returns the precedence winner for each single flag and for `--rich --markdown`, `--markdown --text`, `--markdown --json`; no-flag → `Standard Json`.
- **Force-color (Cli.Tests)**: `forceColorRequested`/`detectCapabilities` mapping across the truth table incl. `FORCE_COLOR` values `1`/`true`/`0`/empty/unset and `--force-color`; `NO_COLOR` + force → not interactive-color; `TERM=dumb` + force → color. Env-var tests set/restore `FORCE_COLOR`/`NO_COLOR`/`TERM` around the assertion.
- **Degradation interplay (DegradationTests)**: with a redirected-but-forced capability, `resolve`/`resolveValidation` `Rich` renders richly (ANSI present); with `NO_COLOR` it degrades to zero-ANSI plain text.
- **Invariants**: default/`--json`/`--text` bytes unchanged; force-color leaves JSON/text/markdown bytes and exit code and routing unchanged; `validate --markdown --out` persists Markdown and exits on verdict only.
- **Surface baseline**: `PublicSurface.baseline` refreshed and `SurfaceBaselineTests` green.

## Agent-facing behavior

Claude and Codex both invoke `fsgg-sdd validate`; the new `--markdown` projection and force-color flag are surfaced in the validate command reference doc and (if present) the `fs-gg-sdd-validate` skill note. No agent-command generation change (validate is a cross-cutting CLI verb, not a lifecycle stage in the work model).

## Governance integration

None. No Governance runtime, contract, or cross-repo package is involved; this satisfies the FS.GG.SDD#172 request entirely within generic SDD.

## Complexity Tracking

No constitution violations — section intentionally empty.
