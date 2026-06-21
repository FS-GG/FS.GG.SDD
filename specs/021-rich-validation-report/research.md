# Phase 0 Research: Rich Rendering of the `validation-report`

All decisions below resolve the Technical Context unknowns; no `NEEDS
CLARIFICATION` remain.

## Decision 1 — Where the validation rich renderer lives

**Decision**: Extend the existing CLI `Rendering` module
(`src/FS.GG.SDD.Cli/Rendering.fs(i)`) with validation-report rendering. Do **not**
add Spectre.Console to `FS.GG.SDD.Validation`.

**Rationale**: Feature 020 deliberately gave `FS.GG.SDD.Validation` no Spectre
dependency so the `validation-report` contract stays dependency-free and
byte-stable (spec Assumption). Spectre.Console is already a CLI-project package
reference (feature 019) and `FS.GG.SDD.Cli` already references
`FS.GG.SDD.Validation`, so the renderer has both the report type and Spectre in
scope at the CLI edge — mirroring exactly how 019 placed the `CommandReport` rich
renderer.

**Alternatives considered**: (a) a new `FS.GG.SDD.Cli.Validation` rendering module
— rejected as unnecessary ceremony; the existing `Rendering` module already owns
CLI presentation and the shared capability/format primitives. (b) Adding Spectre
to the Validation library — rejected; it would contaminate the packable,
deliberately Spectre-free contract surface.

## Decision 2 — Reuse the format-selection and capability primitives

**Decision**: Reuse `selectFormat`, `detectCapabilities`, `TerminalCapabilities`,
and `RichRenderResult` verbatim. Add only two functions:
`renderValidationRichTo: IAnsiConsole -> ValidationReport -> unit` and
`resolveValidation: OutputFormat -> TerminalCapabilities -> ValidationReport ->
RichRenderResult`.

**Rationale**: The flag precedence (`--rich > --text > --json > default`),
TTY/`NO_COLOR`/`TERM=dumb` detection, and the degradation result type are
format-agnostic — they already serve `CommandReport`. Reusing them guarantees
`validate` selects and degrades identically to every other command (FR-004/FR-005)
and avoids a second, drifting copy of the rules.

**Alternatives considered**: A parallel `selectValidateFormat` / capability set —
rejected; it would risk divergent precedence or detection between the two
projections, contradicting the "one contract" intent.

## Decision 3 — stdout rendering vs. `--out` persistence split

**Decision**: `printValidate` resolves the **stdout** rendering via
`resolveValidation` (rich when interactive+color, else plain text), but the
`--out` file always receives a **deterministic** projection: `--text` →
`renderText`; `--json`/default → `serialize`; `--rich` → `renderText` (its
deterministic, non-interactive shadow).

**Rationale**: FR-010 and the spec edge case require the persisted bytes to remain
a deterministic projection (JSON or plain text) because rich is presentation-only
and not a persisted artifact. Mapping `--rich --out` to the plain-text projection
matches rich's own non-interactive fallback and preserves feature 020's current
behavior (where `--rich` wrote `renderText` to `--out`), so no existing `--out`
expectation regresses.

**Alternatives considered**: Persisting JSON for `--rich --out` — defensible but
changes 020's current `--rich`→text persistence; rejected to minimize behavior
churn. Persisting rich ANSI to a file — rejected outright (FR-005/FR-010).

## Decision 4 — Projection completeness over the structured report

**Decision**: Render the rich view directly from the structured `ValidationReport`
(verdict, summary, matrices, cells, diagnostics) and assert completeness with a
test that, for the same report, confirms every fact the **plain-text** projection
exposes (summary counts and every non-passing cell's matrix + coordinates +
status) also appears in the rich output, and that no fact absent from the report
appears.

**Rationale**: Unlike the `CommandReport` text projection (whose `": "` lines
019 scraped into a table), the validation text projection uses `key=value` and a
matrix/cell structure, so scraping is brittle. Reading the typed report directly is
simpler and the completeness *test* (rich ⊇ text facts) gives the same INV-5-style
guarantee 019 enforced. Envelope metadata (`schemaVersion`, `generatorVersion`) is
explicitly excluded from the completeness requirement (FR-002/SC-004).

**Alternatives considered**: Scraping `renderText` lines — rejected as brittle for
this report's format. A golden snapshot of rich output — rejected; rich is
explicitly outside every deterministic/golden contract (FR-008).

## Decision 5 — Status differentiation and the verdict/rollup layout

**Decision**: Emphasize the verdict with a colored rule/header (green = passed,
red = not passed); show a summary line/table of the five counts; render a
per-matrix rollup table (matrix name, dimensions, per-status counts); then list
the non-passing cells grouped by matrix with coordinates, status, and (for
`fail`) the diagnostic message. Style `fail`/`coverageGap`/`notValidated` as
failing (red family) and `skippedWithReason` distinctly (yellow/grey, non-failing);
`pass` cells are summarized, not enumerated.

**Rationale**: FR-007 requires the verdict, a per-matrix rollup, and visually
distinct statuses emphasizing the exit-driving categories. Summarizing passing
cells keeps a large cross-product scannable (spec edge case) while every
non-passing cell remains individually visible for triage (SC-001).

**Alternatives considered**: Enumerating every cell (including `pass`) — rejected;
defeats the "scannable" goal on dozens of cells. A single flat table — rejected;
the per-matrix rollup is explicitly required.

## Decision 6 — Determinism boundary and the optional `sensed` block

**Decision**: The rich projection is non-deterministic presentation, excluded from
every golden/snapshot contract. It MAY surface populated `sensed` fields
(`startedAtUtc`, `durationMs`, `host`) for human context but MUST NOT require them;
since `ValidationHarness` currently emits `emptySensed` (all `None`), the renderer
shows sensed only when a field is `Some`, so today nothing is shown and nothing is
invented.

**Rationale**: FR-002/FR-008 and the spec assumption permit sensed in the rich
view (it is outside the deterministic comparison) while the JSON contract fences it
to `null`. Rendering only `Some` fields keeps the door open without inventing facts
and without coupling to a future runner that populates sensed.

**Alternatives considered**: Always printing the three sensed labels (even when
`None`) — rejected as inventing empty facts. Forbidding sensed entirely — rejected;
the spec explicitly allows it.

## Decision 7 — Stream and exit-code parity

**Decision**: Keep `printValidate` writing the resolved rendering to `stdout` and
returning exit `0` iff `report.Summary.OverallPassed`, unchanged across all three
formats.

**Rationale**: FR-006/SC-005 require identical stream routing and exit code under
rich and the existing formats. `validate` already routes to stdout for both pass
and fail and derives the exit code solely from `OverallPassed`; the rich format
changes neither.

**Alternatives considered**: Routing failing rich output to stderr — rejected; it
would diverge from the JSON/text formats and break parity.
