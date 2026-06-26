namespace FS.GG.SDD.Cli

open System
open System.IO
open Spectre.Console
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts

module Rendering =
    type TerminalCapabilities =
        { IsInteractive: bool
          ColorEnabled: bool
          Width: int option }

    type RichRenderResult =
        { Text: string
          UsedRichRendering: bool }

    let selectFormat (args: string list) : OutputFormat =
        let has flag = List.contains flag args
        // Precedence: --rich > --text > --json > default (Json).
        if has "--rich" then Rich
        elif has "--text" then Text
        elif has "--json" then Json
        else Json

    let detectCapabilities () : TerminalCapabilities =
        let isInteractive = not Console.IsOutputRedirected

        // NO_COLOR disables color when present with ANY value (including empty);
        // TERM=dumb also disables color.
        let noColorPresent = Environment.GetEnvironmentVariable "NO_COLOR" |> isNull |> not
        let dumbTerminal = Environment.GetEnvironmentVariable "TERM" = "dumb"
        let colorEnabled = not noColorPresent && not dumbTerminal

        let width =
            try
                if Console.IsOutputRedirected then None else Some Console.WindowWidth
            with _ ->
                None

        { IsInteractive = isInteractive
          ColorEnabled = colorEnabled
          Width = width }

    // ----- Presentation styling (color names; stripped on color-off consoles). -----

    let outcomeStyle (outcome: CommandOutcome) =
        match outcome with
        | CommandOutcome.Succeeded -> "green"
        | CommandOutcome.SucceededWithWarnings -> "yellow"
        | CommandOutcome.Blocked -> "red"
        | CommandOutcome.NoChange -> "grey"

    let severityStyle (severity: DiagnosticSeverity) =
        match severity with
        | DiagnosticError -> "red"
        | DiagnosticWarning -> "yellow"
        | DiagnosticInfo -> "blue"

    let currencyStyle (currency: GeneratedViewCurrency) =
        match currency with
        | GeneratedViewCurrency.Current -> "green"
        | GeneratedViewCurrency.Missing -> "yellow"
        | GeneratedViewCurrency.Stale -> "yellow bold"
        | GeneratedViewCurrency.Malformed -> "red"
        | GeneratedViewCurrency.Blocked -> "red"

    let esc (value: string) = Markup.Escape value

    let renderRichTo (console: IAnsiConsole) (report: CommandReport) : unit =
        // Header: command identity (work item, dry-run) as a rule.
        let workIdText =
            match report.WorkId with
            | Some workId -> $" · {workId}"
            | None -> ""

        let dryRunText = if report.DryRun then " · dry-run" else ""
        let header = Rule(esc $"{commandName report.Command}{workIdText}{dryRunText}")
        header.Justification <- Justify.Left
        console.Write header

        // Outcome badge.
        console.MarkupLine($"Outcome: [{outcomeStyle report.Outcome}]{esc (outcomeValue report.Outcome)}[/]")

        // Canonical per-stage facts, presented as a details table. Built from the
        // same plain-text projection so the rich view represents every text fact
        // and invents none (INV-5 / C-3).
        let detailLines =
            (FS.GG.SDD.Commands.CommandRendering.renderText report).Replace("\r\n", "\n").Split('\n')
            |> Array.filter (fun line -> line.Contains ": ")

        if detailLines.Length > 0 then
            let table = Table()
            table.AddColumns("field", "value") |> ignore

            for line in detailLines do
                let separator = line.IndexOf ": "
                let key = line.Substring(0, separator)
                let value = line.Substring(separator + 2)
                table.AddRow(esc key, esc value) |> ignore

            console.Write table

        // Changed artifacts: count plus each path (text projection has count only).
        if not report.ChangedArtifacts.IsEmpty then
            console.MarkupLine($"[bold]Changed artifacts[/] ({report.ChangedArtifacts.Length})")

            for change in report.ChangedArtifacts do
                console.MarkupLine($"  - {esc change.Path} ({esc (artifactOperationValue change.Operation)})")

        // Generated-view currency table (stale views emphasized).
        if not report.GeneratedViews.IsEmpty then
            let table = Table()
            table.Title <- TableTitle("Generated views")
            table.AddColumns("view", "currency") |> ignore

            for view in report.GeneratedViews |> List.sortBy (fun v -> v.Path) do
                let currency = generatedViewCurrencyValue view.Currency
                table.AddRow(esc view.Path, $"[{currencyStyle view.Currency}]{esc currency}[/]") |> ignore

            console.Write table

        // Diagnostics grouped (sorted) by severity.
        if not report.Diagnostics.IsEmpty then
            let table = Table()
            table.Title <- TableTitle("Diagnostics")
            table.AddColumns("severity", "id", "message") |> ignore

            for diagnostic in report.Diagnostics |> List.sortBy (fun d -> severityRank d.Severity) do
                let severity = severityValue diagnostic.Severity
                table.AddRow(
                    $"[{severityStyle diagnostic.Severity}]{esc severity}[/]",
                    esc diagnostic.Id,
                    esc diagnostic.Message)
                |> ignore

            console.Write table

        // Governance compatibility facts (presentation only).
        if not report.GovernanceCompatibility.IsEmpty then
            let table = Table()
            table.Title <- TableTitle("Governance compatibility")
            table.AddColumns("path", "relationship", "state") |> ignore

            for fact in report.GovernanceCompatibility do
                table.AddRow(esc fact.Path, esc fact.Relationship, esc fact.State) |> ignore

            console.Write table

        // Next lifecycle action callout.
        match report.NextAction with
        | Some action ->
            let nextCommand =
                match action.Command with
                | Some command -> commandName command
                | None -> "none"

            console.MarkupLine($"[bold]Next[/]: {esc action.ActionId} → {esc nextCommand}")
            console.MarkupLine($"  {esc action.Reason}")

            for artifact in action.RequiredArtifacts do
                console.MarkupLine($"  requires: {esc artifact}")
        | None -> console.MarkupLine("[dim]Next action: none[/]")

    // ----- validation-report rich projection (pure over the report) -----

    /// The visible token for a cell status (color-stripped consoles differentiate
    /// failing vs non-failing by this token alone — INV-6).
    let cellStatusToken (status: CellStatus) =
        match status with
        | Pass -> "pass"
        | Fail _ -> "fail"
        | SkippedWithReason _ -> "skipped"
        | CoverageGap _ -> "coverageGap"
        | NotValidated _ -> "notValidated"

    /// Status styling: `Fail`/`CoverageGap`/`NotValidated` are emphasized red-family
    /// (they drive the non-zero exit); `SkippedWithReason` is non-failing yellow;
    /// `Pass` is green (summarized only). Stripped on color-off consoles.
    let cellStatusStyle (status: CellStatus) =
        match status with
        | Pass -> "green"
        | Fail _ -> "red bold"
        | CoverageGap _ -> "red"
        | NotValidated _ -> "red"
        | SkippedWithReason _ -> "yellow"

    let cellDetail (status: CellStatus) =
        match status with
        | Fail diagnostic -> diagnostic.Message
        | SkippedWithReason reason -> reason
        | CoverageGap surface -> surface
        | NotValidated reason -> reason
        | Pass -> ""

    let coordinateText (coordinates: (string * string) list) =
        coordinates
        |> List.map (fun (dimension, value) -> $"{dimension}={value}")
        |> String.concat ", "

    let renderValidationRichTo (console: IAnsiConsole) (report: ValidationReport) : unit =
        let summary = report.Summary

        // Overall verdict: green "passed" vs red "not passed", mirroring the exit rule.
        let verdictStyle, verdictText =
            if summary.OverallPassed then "green", "passed" else "red", "not passed"

        let header = Rule("validation-report")
        header.Justification <- Justify.Left
        console.Write header
        console.MarkupLine($"Verdict: [{verdictStyle}]{esc verdictText}[/]")

        // The five summary counts.
        console.MarkupLine(
            $"Summary: passed={summary.Passed} failed={summary.Failed} "
            + $"skipped={summary.Skipped} coverageGaps={summary.CoverageGaps} "
            + $"notValidated={summary.NotValidated}")

        let sortedMatrices = report.Matrices |> List.sortBy (fun matrix -> matrix.Name)

        // Per-matrix rollup: one row per matrix with its per-status counts.
        let rollup = Table()
        rollup.Title <- TableTitle("Matrices")

        rollup.AddColumns("matrix", "pass", "fail", "skipped", "coverageGap", "notValidated")
        |> ignore

        for matrix in sortedMatrices do
            let countOf predicate =
                matrix.Cells |> List.filter (fun cell -> predicate cell.Status) |> List.length

            let passed = countOf (function Pass -> true | _ -> false)
            let failed = countOf (function Fail _ -> true | _ -> false)
            let skipped = countOf (function SkippedWithReason _ -> true | _ -> false)
            let gaps = countOf (function CoverageGap _ -> true | _ -> false)
            let notValidated = countOf (function NotValidated _ -> true | _ -> false)

            rollup.AddRow(
                esc matrix.Name,
                string passed,
                string failed,
                string skipped,
                string gaps,
                string notValidated)
            |> ignore

        console.Write rollup

        // Per-matrix section: name + dimensions, then every non-passing cell with its
        // coordinates, status token, and detail (the diagnostic message for `Fail`).
        // `Pass` cells are summarized only, keeping a large cross-product scannable.
        for matrix in sortedMatrices do
            let dimensions = matrix.Dimensions |> String.concat ", "
            console.MarkupLine($"[bold]{esc matrix.Name}[/] (dimensions: {esc dimensions})")

            let nonPassing =
                matrix.Cells
                |> List.filter (fun cell -> match cell.Status with Pass -> false | _ -> true)
                |> List.sortBy (fun cell -> coordinateText cell.Coordinates)

            if nonPassing.IsEmpty then
                console.MarkupLine("  [dim]all evaluated cells pass[/]")
            else
                for cell in nonPassing do
                    let token = cellStatusToken cell.Status
                    let style = cellStatusStyle cell.Status
                    let detail = cellDetail cell.Status
                    let detailText = if System.String.IsNullOrEmpty detail then "" else $": {esc detail}"
                    console.MarkupLine($"  ({esc (coordinateText cell.Coordinates)}) [{style}]{esc token}[/]{detailText}")

        // Sensed triage facts are surfaced only when populated (C-6); never required.
        let sensedLines =
            [ match report.Sensed.StartedAtUtc with
              | Some value -> $"startedAtUtc={value}"
              | None -> ()
              match report.Sensed.DurationMs with
              | Some value -> $"durationMs={value}"
              | None -> ()
              match report.Sensed.Host with
              | Some value -> $"host={value}"
              | None -> () ]

        if not sensedLines.IsEmpty then
            let sensedText = String.concat ", " sensedLines
            console.MarkupLine($"[dim]sensed: {esc sensedText}[/]")

    let resolve
        (format: OutputFormat)
        (capabilities: TerminalCapabilities)
        (report: CommandReport)
        : RichRenderResult =
        match format with
        | Json -> { Text = serializeReport report; UsedRichRendering = false }
        | Text -> { Text = FS.GG.SDD.Commands.CommandRendering.renderText report; UsedRichRendering = false }
        | Rich ->
            if capabilities.IsInteractive && capabilities.ColorEnabled then
                let writer = new StringWriter()
                let settings = AnsiConsoleSettings()
                settings.Ansi <- AnsiSupport.Yes
                settings.ColorSystem <- ColorSystemSupport.Standard
                settings.Out <- new AnsiConsoleOutput(writer)
                let console = AnsiConsole.Create settings

                match capabilities.Width with
                | Some width when width > 0 -> console.Profile.Width <- width
                | _ -> ()

                renderRichTo console report
                { Text = writer.ToString(); UsedRichRendering = true }
            else
                // Degrade to the existing plain-text projection: zero ANSI, byte-identical
                // to `--text` for the same report (C-1, INV-2).
                { Text = FS.GG.SDD.Commands.CommandRendering.renderText report; UsedRichRendering = false }

    let resolveValidation
        (format: OutputFormat)
        (capabilities: TerminalCapabilities)
        (report: ValidationReport)
        : RichRenderResult =
        match format with
        // `serialize` is the canonical automation contract; `renderText` is the
        // portable plain-text projection. Both are returned byte-for-byte (INV-1).
        | Json -> { Text = FS.GG.SDD.Validation.ValidationContracts.serialize report; UsedRichRendering = false }
        | Text -> { Text = FS.GG.SDD.Validation.ValidationContracts.renderText report; UsedRichRendering = false }
        | Rich ->
            if capabilities.IsInteractive && capabilities.ColorEnabled then
                let writer = new StringWriter()
                let settings = AnsiConsoleSettings()
                settings.Ansi <- AnsiSupport.Yes
                settings.ColorSystem <- ColorSystemSupport.Standard
                settings.Out <- new AnsiConsoleOutput(writer)
                let console = AnsiConsole.Create settings

                match capabilities.Width with
                | Some width when width > 0 -> console.Profile.Width <- width
                | _ -> ()

                renderValidationRichTo console report
                { Text = writer.ToString(); UsedRichRendering = true }
            else
                // Degrade to the exact plain-text projection: zero ANSI, byte-identical
                // to `--text` for the same report (C-4, INV-2).
                { Text = FS.GG.SDD.Validation.ValidationContracts.renderText report; UsedRichRendering = false }
