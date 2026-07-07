# Research: Force-Color Override + Markdown Report Card

Phase 0 decision log. All spec clarifications were pre-resolved (see `spec.md` §Clarifications); this records the technical decisions behind the plan.

## D1 — Where the force-color signal lives

**Decision**: Fold `forceColor` into `Rendering.detectCapabilities` and compute *effective* `IsInteractive`/`ColorEnabled`, leaving the two rich-degrade gates (`resolve`, `resolveValidation`, predicate `IsInteractive && ColorEnabled`) unchanged.

**Rationale**: The gate is a single shared decision reused by every `--rich`-capable command (`resolve` for command reports, `resolveValidation` for the validation-report, both via `detectCapabilities`). Placing the override in the sensing function makes it uniform (FR-005) with zero per-command wiring and keeps the `resolve*` bodies byte-identical, minimizing blast radius and the risk of the two gates drifting.

**Alternatives considered**:
- *Add a `ForceColor` field to `TerminalCapabilities` and change the gate predicate.* Rejected: touches both `resolve*` gates and every `TerminalCapabilities` construction site (incl. `DegradationTests` fixtures) for no behavioral gain over the effective-boolean approach.
- *Handle force-color only in `printValidate`.* Rejected: violates FR-005 (would make `validate` honor force-color but not other `--rich` commands).

**Consequence**: `detectCapabilities` arity changes `(outputRedirected)` → `(forceColor) (outputRedirected)`. Call sites: `Program.fs` 112/170/212/259, `RegistryValidate.fs` 156; test `HelpRenderingTests` (arity-1 calls) updated to pass `false`. `Width` keeps using the *raw* `outputRedirected` so `Console.WindowWidth` is never read on a redirected pipe.

## D2 — Precedence `NO_COLOR > force-color > capability sensing`

**Decision**: `NO_COLOR` is a hard override that always disables color; force-color overrides only the redirected/non-interactive gate and `TERM=dumb`.

**Rationale**: The issue directs us to "continue to honor `NO_COLOR`," and `NO_COLOR` is the user's explicit opt-out, whereas redirection/`TERM=dumb` are *capability guesses* that a caller can legitimately override. Encoded as `ColorEnabled = (not noColorPresent) && ((not dumbTerminal) || forceColor)` and `IsInteractive = (not outputRedirected) || forceColor`.

**Alternatives considered**: *force-color wins over NO_COLOR* (some ecosystems) — rejected; contradicts the issue and the FS-GG degrade-to-zero-ANSI safety convention.

## D3 — `FORCE_COLOR` value semantics (boolean-ish)

**Decision**: `FORCE_COLOR` unset, empty (`""`), or the literal `"0"` → does **not** force; any other value → forces. `NO_COLOR` keeps its own "present with any value (incl. empty) disables" standard semantics.

**Rationale**: Matches the widely-adopted `supports-color`/`chalk` convention callers already expect, and avoids a stray empty `FORCE_COLOR=` in an environment silently forcing color. The asymmetry with `NO_COLOR` is intentional and standard (disable-signals are presence-based; force-signals are value-based).

**Alternatives considered**: *presence-based like NO_COLOR* — rejected; an empty leftover would force color, surprising in shared shells/CI.

## D4 — Markdown projection placement & determinism

**Decision**: Implement `renderMarkdown : ValidationReport -> string` in `FS.GG.SDD.Validation/ValidationContracts.fs` (with `.fsi`), a peer of `serialize`/`renderText`. It is deterministic: matrices sorted by name, non-passing cells sorted by coordinate text, no wall-clock/sensed/width data, no ANSI.

**Rationale**: The Markdown card is a *deterministic text projection of the report* — exactly the role `serialize`/`renderText` already play in the Validation library. Co-locating keeps it library-unit-testable (golden coverage) and reusable, and keeps the CLI `Rendering` module about console sensing/Spectre. Determinism makes it eligible for `--out` persistence and golden tests (unlike `--rich`, which is width/ANSI-dependent and excluded from golden contracts).

**Alternatives considered**: *Render Markdown in `Rendering.fs` via Spectre* — rejected; Spectre is for ANSI/rich console output, not a deterministic Markdown string, and would drag width/capability concerns into a projection that must be context-free.

## D5 — `--markdown` selection without perturbing the shared `OutputFormat`

**Decision**: Leave `OutputFormat = Json | Text | Rich` (the shared machine-contract DU) unchanged. Add a `validate`-local `ValidationFormat = Standard of OutputFormat | MarkdownCard` and `selectValidationFormat` implementing `--rich > --markdown > --text > --json > default`.

**Rationale**: The Markdown card is `validate`-only (clarified scope). Adding a `Markdown` case to the shared `OutputFormat` would force every command's `resolve` to answer "what does Markdown mean here?" and risk a half-supported format leaking across the whole CLI. A validate-local selection contains the surface precisely where it belongs while still giving a clean, unit-testable precedence function.

**Alternatives considered**: *Add `Markdown` to `OutputFormat`* — rejected on scope/blast-radius grounds; revisit only if a future feature promotes Markdown to a general `CommandReport` projection.

## D6 — Docs & surface baseline

**Decision**: Document `--force-color`/`FORCE_COLOR`/`--markdown` in the validate command reference doc; refresh `tests/FS.GG.SDD.Cli.Tests/PublicSurface.baseline` for the changed `detectCapabilities` arity and the new public functions (`forceColorRequested`, `selectValidationFormat`, `renderMarkdown`, the `ValidationFormat` type).

**Rationale**: Tier 1 (public surface) change — Constitution III requires `.fsi`, baseline, tests, and docs to move together.
