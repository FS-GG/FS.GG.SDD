namespace FS.GG.SDD.Cli

open System
open System.IO
open System.Text
open Spectre.Console
open Fsgg
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering

module RegistryValidate =

    type ReportDiagnostic =
        { Entry: string
          Rule: string
          Message: string }

    type RegistryValidateReport =
        { Path: string
          Valid: bool
          Diagnostics: ReportDiagnostic list }

    let private ruleName (rule: Registry.RegistryRule) =
        match rule with
        | Registry.MissingField _ -> "MissingField"
        | Registry.UnknownComponent -> "UnknownComponent"
        | Registry.IncompatibleVersion -> "IncompatibleVersion"
        | Registry.MalformedVersion -> "MalformedVersion"
        | Registry.DuplicateComponent -> "DuplicateComponent"
        | Registry.MalformedDocument -> "MalformedDocument"

    let validate (path: string) : RegistryValidateReport =
        match RegistryDocument.load path with
        | Error error ->
            // Load/parse failure: a single MalformedDocument-class diagnostic, distinct
            // from content diagnostics (Constitution VIII), never a cascade or a crash.
            { Path = path
              Valid = false
              Diagnostics =
                [ { Entry = path
                    Rule = "MalformedDocument"
                    Message = error.Message } ] }
        | Ok document ->
            match Registry.validateDocument document with
            | Registry.Valid ->
                { Path = path
                  Valid = true
                  Diagnostics = [] }
            | Registry.Invalid diagnostics ->
                { Path = path
                  Valid = false
                  Diagnostics =
                    diagnostics
                    |> List.map (fun d ->
                        { Entry = d.Entry
                          Rule = ruleName d.Rule
                          Message = d.Message }) }

    let exitCode (report: RegistryValidateReport) = if report.Valid then 0 else 1

    // --- JSON automation contract (deterministic; properties in fixed order). ---

    let private jsonEscape (value: string) =
        let builder = StringBuilder()

        for ch in value do
            match ch with
            | '"' -> builder.Append "\\\"" |> ignore
            | '\\' -> builder.Append "\\\\" |> ignore
            | '\n' -> builder.Append "\\n" |> ignore
            | '\r' -> builder.Append "\\r" |> ignore
            | '\t' -> builder.Append "\\t" |> ignore
            | c when c < ' ' -> builder.AppendFormat("\\u{0:x4}", int c) |> ignore
            | c -> builder.Append c |> ignore

        builder.ToString()

    let private quote (value: string) = "\"" + jsonEscape value + "\""

    let serialize (report: RegistryValidateReport) : string =
        let builder = StringBuilder()
        let validText = if report.Valid then "true" else "false"
        builder.AppendLine "{" |> ignore
        builder.AppendLine "  \"tool\": \"fsgg-sdd registry validate\"," |> ignore
        builder.AppendLine($"  \"path\": {quote report.Path},") |> ignore
        builder.AppendLine($"  \"valid\": {validText},") |> ignore

        if report.Diagnostics.IsEmpty then
            builder.AppendLine "  \"diagnostics\": []" |> ignore
        else
            builder.AppendLine "  \"diagnostics\": [" |> ignore
            let lastIndex = report.Diagnostics.Length - 1

            report.Diagnostics
            |> List.iteri (fun index diagnostic ->
                let comma = if index = lastIndex then "" else ","

                builder.AppendLine(
                    $"    {{ \"entry\": {quote diagnostic.Entry}, "
                    + $"\"rule\": {quote diagnostic.Rule}, "
                    + $"\"message\": {quote diagnostic.Message} }}{comma}"
                )
                |> ignore)

            builder.AppendLine "  ]" |> ignore

        builder.Append "}" |> ignore
        builder.ToString()

    // --- Portable plain-text projection. ---

    let renderText (report: RegistryValidateReport) : string =
        let header =
            if report.Valid then
                $"registry validate: {report.Path} → valid (0 diagnostics)"
            else
                $"registry validate: {report.Path} → invalid ({report.Diagnostics.Length} diagnostics)"

        let lines =
            report.Diagnostics
            |> List.map (fun d -> $"  - {d.Rule} [{d.Entry}]: {d.Message}")

        String.Join("\n", header :: lines)

    // --- Rich Spectre projection (presentation only; excluded from golden contracts). ---

    let private renderRichTo (console: IAnsiConsole) (report: RegistryValidateReport) : unit =
        let esc (value: string) = Markup.Escape value
        let header = Rule(esc $"registry validate · {report.Path}")
        header.Justification <- Justify.Left
        console.Write header

        let verdictStyle, verdictText =
            if report.Valid then "green", "valid" else "red", "invalid"

        console.MarkupLine($"Verdict: [{verdictStyle}]{esc verdictText}[/] ({report.Diagnostics.Length} diagnostics)")

        if not report.Diagnostics.IsEmpty then
            let table = Table()
            table.Title <- TableTitle("Diagnostics")
            table.AddColumns("rule", "entry", "message") |> ignore

            for diagnostic in report.Diagnostics do
                table.AddRow($"[red]{esc diagnostic.Rule}[/]", esc diagnostic.Entry, esc diagnostic.Message)
                |> ignore

            console.Write table

    let private render (forceColor: bool) (format: OutputFormat) (report: RegistryValidateReport) : string =
        match format with
        | Json -> serialize report
        | Text -> renderText report
        | Rich ->
            // Always written to stdout (see the `Console.Out.WriteLine` sink below).
            // `forceColor` (FORCE_COLOR / --force-color) re-enables rich ANSI over a
            // redirected sink, uniformly with every other command (#172).
            let capabilities = detectCapabilities forceColor Console.IsOutputRedirected

            if capabilities.IsInteractive && capabilities.ColorEnabled then
                let console, writer = createCappedConsole capabilities

                renderRichTo console report
                writer.ToString()
            else
                // Degrade to the plain-text projection: zero ANSI (CLAUDE.md rich contract).
                renderText report

    let private argError (message: string) : RegistryValidateReport =
        { Path = ""
          Valid = false
          Diagnostics =
            [ { Entry = "<args>"
                Rule = "MissingField"
                Message = message } ] }

    // FS-GG/FS.GG.SDD#263 (Gap C finding 4 / #203): confine `<path>` so `registry validate` cannot
    // read outside the workspace. The path flows straight into `RegistryDocument.load path` →
    // `File.ReadAllText path`, so a bare `registry validate /etc/passwd` (or a `..` escape) reads an
    // arbitrary file. This is the lexical guard `surface` (#185) and `registry skill-manifest` (#239)
    // already use: check the RAW value, because a normalize-then-test would `TrimStart('/')` the
    // leading slash away and let `/etc` through as `etc`. It is a copy of the internal
    // `Commands.Internal.Foundation.escapesRoot` — unreachable from this assembly — kept small and
    // comment-linked with its `RegistrySkillManifest.escapesRoot` sibling so the two cannot drift;
    // the durable effect-edge containment primitive is #203/ADR-0002, not this predicate.
    let private escapesRoot (raw: string) =
        let trimmed = raw.Trim().Replace('\\', '/')

        String.IsNullOrWhiteSpace trimmed
        || Path.IsPathRooted trimmed // on the RAW string, before any TrimStart('/')
        || (trimmed.Split('/') |> Array.contains "..")

    // A containment failure is a user-input failure surfaced through this command's own verdict
    // channel (parity with the unrecognized-option / missing-path cases, #258): `valid:false` on
    // stdout + exit 1, no read. Names the offending path and mirrors the shared "escapes the
    // workspace root" phrasing (`registry skill-manifest`, `validate --out`).
    let private pathEscapeError (path: string) : RegistryValidateReport =
        { Path = path
          Valid = false
          Diagnostics =
            [ { Entry = path
                Rule = "PathEscape"
                Message =
                  $"'{path}' escapes the workspace root — "
                  + "pass a path inside the workspace (no absolute path or '..')." } ] }

    let private usage =
        "Usage: fsgg-sdd registry validate <path> [--json|--text|--rich]"

    // ADR-0002 Gap C finding 4 (#203, FS-GG/FS.GG.SDD#258): mirror #196 — an option this command
    // cannot honor blocks instead of being silently dropped by the `tryFind` positional scan (which
    // would also mistake a bare `-x` for the `<path>`). Recognized here: the format/color flags and
    // `--help`; the `<path>` is a positional (not `-`-prefixed) and the bare `--` separator (#246) is
    // not an option. Sibling copy of `RegistrySkillManifest.unknownOptions` — kept small and
    // comment-linked so the two cannot drift.
    let private recognizedOptions =
        set [ "--json"; "--text"; "--rich"; "--force-color"; "--help"; "-h" ]

    let private unknownOptions (args: string list) =
        args
        |> List.filter (fun token ->
            token.StartsWith("-", StringComparison.Ordinal)
            && token <> "--"
            && not (recognizedOptions.Contains token))

    let private formatOptions (options: string list) =
        options |> List.map (fun option -> $"'{option}'") |> String.concat ", "

    let run (args: string list) : int =
        let format = selectFormat args

        let report =
            match args with
            | "validate" :: rest ->
                match unknownOptions rest with
                // An unrecognized flag is a user-input failure surfaced through this command's own
                // verdict channel: an `argError` on stdout (valid:false) + exit 1, parity with the
                // missing-path / unknown-subcommand cases (a gate-callable validator always emits a verdict).
                | (_ :: _) as unknown -> argError $"unrecognized option {formatOptions unknown}. {usage}"
                | [] ->
                    match rest |> List.tryFind (fun token -> not (token.StartsWith "--")) with
                    | Some path when not (String.IsNullOrWhiteSpace path) ->
                        // Containment before the read: an absolute or `..` path plans no
                        // `RegistryDocument.load` (parity with `surface` #185 / skill-manifest #239).
                        if escapesRoot path then
                            pathEscapeError path
                        else
                            validate path
                    | _ -> argError usage
            | subcommand :: _ -> argError $"Unknown registry subcommand '{subcommand}'. {usage}"
            | [] -> argError usage

        // The verdict JSON is the automation contract — always to stdout; the exit code
        // carries pass/fail for a CI `--exit-code`-style gate (matches the Python stand-in).
        Console.Out.WriteLine(render (forceColorRequested args) format report)
        exitCode report
