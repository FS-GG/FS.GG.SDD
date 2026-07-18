namespace FS.GG.SDD.Cli

open Spectre.Console
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts

module Rendering =
    /// Pure detection result for the active output environment.
    type TerminalCapabilities =
        {
            IsInteractive: bool
            ColorEnabled: bool
            Width: int option
            /// Whether stdin is an interactive terminal (feature 053): drives the
            /// `upgrade` confirm gate. Distinct from `IsInteractive`, which tracks
            /// output redirection for rich degradation.
            IsInputInteractive: bool
        }

    /// Result of choosing and producing a rendering for one report.
    type RichRenderResult =
        { Text: string
          UsedRichRendering: bool }

    /// Feature 084: the presentation-only Spectre style for a lifecycle stage state in the rich
    /// footer. Each of the five states maps to a distinct style; `Blocked` carries the emphasis.
    val stageStateStyle: state: StageState -> string

    /// Resolve the requested output format from CLI arguments, applying the
    /// precedence `--rich` > `--text` > `--json` > default (`Json`). Pure.
    val selectFormat: args: string list -> OutputFormat

    /// A `validate`-local projection selection (feature 088 / FS.GG.SDD#172). The
    /// Markdown report card is validate-only, so it stays out of the shared
    /// `OutputFormat`; `Standard` wraps one of the three shared projections.
    type ValidationFormat =
        | Standard of OutputFormat
        | MarkdownCard

    /// Resolve the `validate` projection with precedence
    /// `--rich` > `--markdown` > `--text` > `--json` > default (`Standard Json`). Pure.
    val selectValidationFormat: args: string list -> ValidationFormat

    /// Whether a force-color request is present — the `FORCE_COLOR` environment variable
    /// (boolean-ish: unset / empty / "0" do not force, any other value forces) OR the
    /// `--force-color` flag. Bypasses TTY/`TERM=dumb` sensing but never `NO_COLOR` (#172).
    val forceColorRequested: args: string list -> bool

    /// Detect capabilities from the process environment for the stream a report will be
    /// written to (impure edge step). `outputRedirected` is that sink's redirection state
    /// (`Console.IsOutputRedirected` for the CommandReport, which always routes to stdout
    /// including a Blocked outcome per FS.GG.SDD#535, and `Console.IsErrorRedirected` for the
    /// stderr-routed CLI-edge diagnostics — malformed invocation and tool defects) so Rich
    /// degrades to plain text on redirect.
    /// `forceColor` re-enables rich ANSI over a redirected sink or `TERM=dumb`, but never
    /// over `NO_COLOR`: precedence is `NO_COLOR` > force-color > capability sensing (#172).
    val detectCapabilities: forceColor: bool -> outputRedirected: bool -> TerminalCapabilities

    /// Render a report into the given Spectre console. Pure over the report: the
    /// only observable mutation is to the supplied console.
    /// Build an in-memory Spectre.Console honoring the width cap, with the backing writer its
    /// output lands in. The shared Ansi/color/width setup for every rich sink (feature 061 /
    /// issue #71).
    val createCappedConsole: capabilities: TerminalCapabilities -> IAnsiConsole * System.IO.StringWriter

    val renderRichTo: console: IAnsiConsole -> report: CommandReport -> unit

    /// Resolve the effective rendering for a requested format + capabilities,
    /// degrading Rich -> plain text when non-interactive or color-disabled.
    val resolve: format: OutputFormat -> capabilities: TerminalCapabilities -> report: CommandReport -> RichRenderResult

    /// Render a validation-report into the given Spectre console. Pure over the
    /// report: the only observable mutation is to the supplied console.
    val renderValidationRichTo: console: IAnsiConsole -> report: ValidationReport -> unit

    /// Resolve the effective stdout rendering for a requested format + capabilities,
    /// degrading Rich -> plain text when non-interactive or color-disabled. `Json`
    /// returns the canonical `serialize` JSON; `Text` returns `renderText`.
    val resolveValidation:
        format: OutputFormat -> capabilities: TerminalCapabilities -> report: ValidationReport -> RichRenderResult
