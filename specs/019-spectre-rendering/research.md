# Phase 0 Research: Rich Spectre.Console CLI Rendering

Feature: `019-spectre-rendering` · Date: 2026-06-21

This feature adds a third, human-oriented projection of the existing
`CommandReport` to the `fsgg-sdd` CLI, alongside the deterministic JSON contract
and the portable plain-text projection. All decisions below preserve the
repository invariant that JSON is the automation contract and that deterministic
output never depends on terminal wrapping or ANSI.

## Decision 1 — Where the rich renderer lives

**Decision**: Implement the rich renderer as a new module inside the
`FS.GG.SDD.Cli` executable project (`Rendering.fs` + `Rendering.fsi`), not in the
`FS.GG.SDD.Commands` library. Spectre.Console is referenced only by the CLI
project.

**Rationale**:

- `FS.GG.SDD.Commands` is a packable library (`FS.GG.SDD.Commands`). Adding a
  console-UI dependency (Spectre.Console) to it would force every downstream
  consumer of the command library to drag a presentation framework it does not
  need.
- Terminal capability detection and writing are edge I/O. Constitution Principle V
  (MVU boundary) places real I/O at the executable edge interpreter; the CLI is
  that edge. The pure `report -> rendered string` step stays pure and testable.
- The existing plain-text projection (`CommandRendering.renderText`) lives in the
  Commands library, but it has no third-party dependency. Symmetry of "projection
  over the same report object" is preserved at the *contract* level (both read
  `CommandReport`), without dragging Spectre into the core library.

**Alternatives considered**:

- *Put `renderRich` in `FS.GG.SDD.Commands` next to `renderText`* — rejected:
  leaks Spectre.Console into the core package surface and every consumer.
- *New dedicated `FS.GG.SDD.Cli.Rendering` library project* — rejected as
  over-engineering for one renderer (Constitution Principle IV, idiomatic
  simplicity); a module with an `.fsi` inside the CLI plus a CLI test project is
  sufficient and keeps the project count down.

## Decision 2 — Use Spectre.Console as the rich-rendering technology

**Decision**: Add `Spectre.Console` as a `PackageReference` on the CLI project,
with its version pinned centrally in `Directory.Packages.props`
(`ManagePackageVersionsCentrally=true`).

**Rationale**: The implementation plan ground rule names Spectre.Console
explicitly ("Plain text and Spectre.Console output are projections over the same
report objects"), and feature 018 deferred exactly "richer Spectre.Console human
rendering." Spectre provides panels, tables, rules, and color with built-in
capability detection, which is simpler and more robust than hand-rolled ANSI.

**Connectivity / restore**: Spectre.Console is **not** currently in the local
NuGet cache, but `api.nuget.org` is reachable from this environment (verified HTTP
200). Restore will fetch it once and cache it; the deterministic build flags
(`Deterministic`, `ContinuousIntegrationBuild`) are unaffected because Spectre is
a runtime presentation dependency, not part of any serialized artifact.

**Alternatives considered**: hand-rolled ANSI escape rendering (rejected:
reinvents capability detection, contradicts the named ground rule, more code to
test); a different TUI library (rejected: Spectre is the named, idiomatic .NET
choice).

## Decision 3 — Format selection and terminal-capability degradation

**Decision**: Extend `OutputFormat` (currently `Json | Text`) with a third case
`Rich`. CLI resolution:

| CLI input | Requested format | Effective rendering |
|---|---|---|
| (no format flag) | `Json` (unchanged default) | JSON |
| `--text` | `Text` | plain text |
| `--rich` | `Rich` | rich **iff** stdout is an interactive, color-capable TTY; otherwise plain text |
| `--json` (new explicit alias, optional) | `Json` | JSON |

`Rich` degrades to the plain-text projection when **any** of: stdout is
redirected/piped (not a TTY), the terminal is dumb/non-interactive, or color is
disabled (`NO_COLOR` present, or `TERM=dumb`). Degradation emits zero ANSI/color
sequences.

**Rationale**: JSON stays the unconditional default so every existing JSON fixture
and CLI smoke transcript is unchanged (spec Assumptions; SC-002). Rich is opt-in
and self-protecting against log corruption (SC-003). Reusing the existing
plain-text projection as the fallback avoids inventing a second degraded format
(spec Assumptions).

**Alternatives considered**: *Rich as the auto-default on interactive TTYs*
(rejected for now: risks surprising existing interactive users and any test that
runs against a PTY; left as a noted future option in the spec). *A separate
stripped "rich-but-no-color" format* (rejected: plain text already serves this).

## Decision 4 — Determinism and the automation contract

**Decision**: The rich projection is presentation-only and is **excluded** from
all deterministic/golden contracts. Determinism guarantees continue to apply to
the JSON projection alone. The `CommandReport` object and `serializeReport` output
are read-only inputs to rendering and are never mutated by it.

**Rationale**: Constitution Principle II + the plan ground rule "Deterministic
JSON must not depend on implicit clocks, terminal wrapping, or ANSI output." Rich
output legitimately varies with terminal width and color support, so it cannot be
byte-asserted in golden fixtures. Content (not styling) is what tests assert.

## Decision 5 — Testing approach for a non-deterministic renderer

**Decision**: Add a new `FS.GG.SDD.Cli.Tests` xUnit project that
`ProjectReference`s the CLI exe and tests the renderer by rendering to a
Spectre `IAnsiConsole` backed by a `StringWriter` with a **fixed profile**
(color system = NoColors, fixed width). Assertions cover:

1. **Projection completeness** — every populated `CommandReport` section/field is
   represented in the rich output (SC-004), mirroring the coverage style of the
   existing `TextProjectionTests`.
2. **Automation invariance** — for a battery of reports, `serializeReport` bytes
   and `renderText` output are identical before/after this feature (SC-002), and
   selecting `Rich` does not change the report object.
3. **No-ANSI-on-degradation** — when the renderer is invoked in degraded mode (no
   TTY / `NO_COLOR`), output contains zero ESC (``) sequences (SC-003).
4. **Stream + exit-code parity** — `Blocked` outcomes still route to stderr and
   yield the same exit code under `Rich` as under `Json`/`Text` (SC-005).

**Rationale**: Spectre's `AnsiConsole.Create` with an explicit `AnsiConsoleSettings`
profile makes rendering deterministic *enough* to assert content with color off,
satisfying Constitution Principle VI (real fixtures, behavior tested through the
public surface) without snapshotting volatile ANSI.

**Alternatives considered**: snapshotting raw ANSI output (rejected: volatile,
violates determinism discipline); testing only via end-to-end process capture
(kept as a CLI smoke check, but unit-level renderer tests give faster, finer
coverage).

## Decision 6 — Agent/human contract alignment

**Decision**: Update the human-facing format documentation and the Claude/Codex
agent guidance surfaces equivalently so both describe the JSON/text/rich choice
and the degradation rule (FR-010, Constitution Principle VII). No generated
machine artifact schema changes.

**Rationale**: Claude, Codex, CLI, and CI must share one contract; the new format
option is a user-visible behavior both agent surfaces should describe identically.

## Resolved unknowns

All Technical Context unknowns are resolved; no `NEEDS CLARIFICATION` markers
remain. The single default-format design decision (JSON stays default, rich is
opt-in) was settled by spec assumption and Decision 3.
