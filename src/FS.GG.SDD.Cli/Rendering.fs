namespace FS.GG.SDD.Cli

open System
open System.IO
open Spectre.Console
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes

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

    let private outcomeStyle (outcome: CommandOutcome) =
        match outcome with
        | CommandOutcome.Succeeded -> "green"
        | CommandOutcome.SucceededWithWarnings -> "yellow"
        | CommandOutcome.Blocked -> "red"
        | CommandOutcome.NoChange -> "grey"

    let private severityStyle (severity: DiagnosticSeverity) =
        match severity with
        | DiagnosticError -> "red"
        | DiagnosticWarning -> "yellow"
        | DiagnosticInfo -> "blue"

    let private currencyStyle (currency: GeneratedViewCurrency) =
        match currency with
        | GeneratedViewCurrency.Current -> "green"
        | GeneratedViewCurrency.Missing -> "yellow"
        | GeneratedViewCurrency.Stale -> "yellow bold"
        | GeneratedViewCurrency.Malformed -> "red"
        | GeneratedViewCurrency.Blocked -> "red"

    let private esc (value: string) = Markup.Escape value

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
            (renderText report).Replace("\r\n", "\n").Split('\n')
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

    let resolve
        (format: OutputFormat)
        (capabilities: TerminalCapabilities)
        (report: CommandReport)
        : RichRenderResult =
        match format with
        | Json -> { Text = serializeReport report; UsedRichRendering = false }
        | Text -> { Text = renderText report; UsedRichRendering = false }
        | Rich ->
            if capabilities.IsInteractive && capabilities.ColorEnabled then
                let writer = new StringWriter()
                let settings = AnsiConsoleSettings()
                settings.Ansi <- AnsiSupport.Yes
                settings.ColorSystem <- ColorSystemSupport.Standard
                settings.Out <- new AnsiConsoleOutput(writer)
                let console = AnsiConsole.Create settings

                match capabilities.Width with
                | Some width -> console.Profile.Width <- width
                | None -> ()

                renderRichTo console report
                { Text = writer.ToString(); UsedRichRendering = true }
            else
                // Degrade to the existing plain-text projection: zero ANSI, byte-identical
                // to `--text` for the same report (C-1, INV-2).
                { Text = renderText report; UsedRichRendering = false }
