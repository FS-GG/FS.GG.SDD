# Contract: Rich Rendering Projection Surface

Feature: `019-spectre-rendering`

Defines the new public surface added by the CLI for rich rendering. All signatures
are illustrative of the contract; exact `.fsi` is authored during implementation
(Constitution Principle III: visibility lives in `.fsi`).

## Public surface (new module `FS.GG.SDD.Cli.Rendering`)

```fsharp
namespace FS.GG.SDD.Cli

open FS.GG.SDD.Commands.CommandTypes
open Spectre.Console

module Rendering =

    /// Pure detection result for the active output environment.
    type TerminalCapabilities =
        { IsInteractive: bool
          ColorEnabled: bool
          Width: int option }

    type RichRenderResult =
        { Text: string
          UsedRichRendering: bool }

    /// Detect capabilities from the process environment (impure edge step).
    val detectCapabilities: unit -> TerminalCapabilities

    /// Render a report into the given Spectre console (testable: pass a
    /// StringWriter-backed IAnsiConsole with a fixed, color-off profile).
    val renderRichTo: console: IAnsiConsole -> report: CommandReport -> unit

    /// Resolve the effective rendering for a requested format + capabilities,
    /// degrading Rich -> plain text when non-interactive or color-disabled.
    val resolve:
        format: OutputFormat ->
        capabilities: TerminalCapabilities ->
        report: CommandReport ->
        RichRenderResult
```

## Changed surface (`FS.GG.SDD.Commands.CommandTypes`)

```fsharp
type OutputFormat =
    | Json
    | Text
    | Rich          // added

val outputFormatValue: format: OutputFormat -> string   // Rich -> "rich"
```

The existing `CommandRendering.renderText: CommandReport -> string` is unchanged
and is the degradation fallback for `resolve`.

## Behavioral contract

- **C-1**: `resolve Rich caps report` returns `UsedRichRendering=true` only when
  `caps.IsInteractive && caps.ColorEnabled`; otherwise `UsedRichRendering=false`
  and `Text = CommandRendering.renderText report` exactly.
- **C-2**: `resolve Json _ report` and `resolve Text _ report` ignore capabilities
  and return the existing JSON / plain-text projections respectively, with
  `UsedRichRendering=false`.
- **C-3**: `renderRichTo` writes a representation of **every** populated field of
  `report` (see data-model projection mapping) and introduces no fact absent from
  `report`.
- **C-4**: `renderRichTo` mutates nothing observable except the supplied console;
  it never touches `serializeReport`, the report object, or process exit state.
- **C-5**: With a color-off `IAnsiConsole` profile, `renderRichTo` output contains
  zero ANSI escape sequences.

## Out of scope (this contract)

- Governance route/audit/explain rendering and any `fsgg` (non-`fsgg-sdd`)
  command output.
- Any change to JSON schema, report fields, or lifecycle ordering.
