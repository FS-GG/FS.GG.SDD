open System
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.Cli.Rendering

module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

let rec optionValue name args =
    match args with
    | current :: value :: _ when current = name -> Some value
    | _ :: rest -> optionValue name rest
    | [] -> None

let hasFlag name args = args |> List.exists ((=) name)

/// Collect every value following a repeatable option (e.g. `--param k=v`).
let rec collectOptions name args =
    match args with
    | current :: value :: rest when current = name -> value :: collectOptions name rest
    | _ :: rest -> collectOptions name rest
    | [] -> []

/// Parse repeatable `--param key=value` into ordered (key, value) pairs. A value
/// with no `=` is treated as an empty-valued key.
let parseParams args =
    collectOptions "--param" args
    |> List.map (fun pair ->
        match pair.IndexOf '=' with
        | index when index >= 0 -> pair.Substring(0, index), pair.Substring(index + 1)
        | _ -> pair, "")

let outputFormat args =
    // Flag precedence --rich > --text > --json > default (Json); JSON stays the
    // unconditional default for every command (output-format-selection contract).
    selectFormat args

let printUnknown commandValue =
    let generator = SchemaVersionModule.currentGeneratorVersion ()

    let request =
        { Command = Init
          ProjectRoot = "."
          WorkId = None
          Title = None
          InputText = None
          OutputFormat = Json
          DryRun = true
          GeneratorVersion = generator
          Provider = None
          Parameters = []
          Force = false
          TemplateUpdate = true
          AssumeYes = false
          IsInteractive = false }

    let model =
        { Request = request
          PendingEffects = []
          InterpretedEffects = []
          Diagnostics = [ unknownCommand commandValue ]
          Specification = None
          Clarification = None
          Checklist = None
          Plan = None
          Tasks = None
          Analysis = None
          Evidence = None
          Verification = None
          Ship = None
          AgentGuidance = None
          Refresh = None
          Scaffold = None
          Doctor = None
          Upgrade = None
          GeneratedViews = []
          Report = None }

    let report = buildReport model
    Console.Error.WriteLine(serializeReport report)
    exitCodeForReport report

let printValidate (rest: string list) =
    // CLI-level command (peer of `--version`), dispatched before `parseCommand` so
    // `CommandReport`, `parseCommand`, and the per-command contracts stay untouched
    // (FR-011). Emits the deterministic validation-report to stdout; exits 0 iff the
    // report's overallPassed. Restricting to one matrix still reports the others as
    // notValidated, so a partial run never reads as a full pass.
    let options =
        { FS.GG.SDD.Validation.ValidationRunner.defaultOptions with
            OnlyMatrix = optionValue "--matrix" rest }

    let report = FS.GG.SDD.Validation.ValidationRunner.run options

    // Resolve the stdout rendering: --json (default) is the automation contract,
    // --text the portable plain text, and --rich the Spectre projection (degrading
    // to plain text when non-interactive or color-disabled). Pure projection over
    // the same report; stream routing and exit code are unchanged across formats.
    let format = selectFormat rest

    let stdoutRendering =
        (resolveValidation format (detectCapabilities Console.IsOutputRedirected) report).Text

    // --out persists a deterministic projection only (never rich ANSI): the
    // canonical JSON for --json/default, else the portable plain text (FR-010). A bad
    // path (missing directory, unwritable, malformed) is user input, not a tool defect:
    // it is caught here and surfaced as a stderr diagnostic + exit 1, never a raw stack
    // trace (#68). The stdout report contract is emitted regardless.
    let outWriteError =
        match optionValue "--out" rest with
        | Some path ->
            let persisted =
                match format with
                | Json -> FS.GG.SDD.Validation.ValidationContracts.serialize report
                | _ -> FS.GG.SDD.Validation.ValidationContracts.renderText report

            try
                System.IO.File.WriteAllText(path, persisted)
                None
            with
            | :? System.IO.IOException
            | :? UnauthorizedAccessException
            | :? System.Security.SecurityException
            | :? ArgumentException
            | :? NotSupportedException as ex -> Some(path, ex.Message)
        | None -> None

    Console.Out.WriteLine(stdoutRendering)

    match outWriteError with
    | Some(path, message) ->
        Console.Error.WriteLine($"fsgg-sdd validate: cannot write --out '{path}': {message}")
        1
    | None -> if report.Summary.OverallPassed then 0 else 1

let private helpRequest command format =
    { Command = command
      ProjectRoot = "."
      WorkId = None
      Title = None
      InputText = None
      OutputFormat = format
      DryRun = true
      GeneratorVersion = SchemaVersionModule.currentGeneratorVersion ()
      Provider = None
      Parameters = []
      Force = false
      TemplateUpdate = true
      AssumeYes = false
      IsInteractive = false }

// §3.5: project a help report through the standard three views to stdout. Help carries no
// diagnostics and no changes → NoChange → exit 0 (never `unknownCommand`, FR-008/011).
let private emitHelp format (envelopeCommand: SddCommand) (summary: HelpSummary) =
    let report = helpReport (helpRequest envelopeCommand format) summary
    Console.Out.WriteLine((resolve format (detectCapabilities Console.IsOutputRedirected) report).Text)
    exitCodeForReport report

let private printTopLevelHelp format =
    emitHelp format Init (CommandHelp.topLevelHelp (SchemaVersionModule.currentGeneratorVersion ()))

let private printCommandHelp format command =
    emitHelp format command (CommandHelp.commandHelp command)

let printVersion () =
    // Single reconciled version source (Directory.Build.props <Version>), surfaced
    // through the generator version so the CLI reports the same number as every
    // FS.GG.SDD.* package (feature 018 / FR-011).
    Console.Out.WriteLine(SchemaVersionModule.currentGeneratorVersion().Version)
    0

let run args =
    match args with
    | [] -> printUnknown ""
    | ("--version" | "-v" | "version") :: _ -> printVersion ()
    // §3.5: top-level help (no command) — dispatched as a peer of `--version`/`validate`,
    // before `parseCommand`, so it never falls through to `printUnknown` (FR-008).
    | ("--help" | "-h" | "help") :: rest -> printTopLevelHelp (outputFormat rest)
    | "validate" :: rest -> printValidate rest
    // CLI-level cross-cutting command (peer of `validate`), dispatched before
    // `parseCommand` so the lifecycle CommandReport/parseCommand contracts stay
    // untouched. Composes the Artifacts YAML load edge + Fsgg.Registry.validateDocument
    // into a deterministic verdict; exit 0 iff Valid (feature 042 / FS.GG.SDD#12).
    // `registry skill-manifest` (ADR-0017 P2 / FS.GG.SDD#109) emits/checks SDD's process
    // producer manifest; peer of `registry validate`, also before `parseCommand`.
    | "registry" :: "skill-manifest" :: rest -> FS.GG.SDD.Cli.RegistrySkillManifest.run rest
    | "registry" :: rest -> FS.GG.SDD.Cli.RegistryValidate.run rest
    | commandValue :: rest ->
        match parseCommand commandValue with
        // Unknown command resolves to `unknownCommand` even with `--help` (FR-011): a
        // genuinely unknown command is never masked by a help flag.
        | Error _ -> printUnknown commandValue
        | Ok command when hasFlag "--help" rest || hasFlag "-h" rest ->
            // §3.5: `<known> --help` / `-h` → that command's help (FR-009).
            printCommandHelp (outputFormat rest) command
        | Ok command ->
            let format = outputFormat rest
            let capabilities = detectCapabilities Console.IsOutputRedirected

            let request =
                { Command = command
                  ProjectRoot = optionValue "--root" rest |> Option.defaultValue "."
                  WorkId = optionValue "--work" rest
                  Title = optionValue "--title" rest
                  InputText = optionValue "--input" rest
                  OutputFormat = format
                  DryRun = hasFlag "--dry-run" rest
                  GeneratorVersion = SchemaVersionModule.currentGeneratorVersion ()
                  Provider = optionValue "--provider" rest
                  Parameters = parseParams rest
                  Force = hasFlag "--force" rest
                  TemplateUpdate = not (hasFlag "--no-update" rest)
                  // Feature 053: `upgrade`'s explicit non-interactive apply flag, and the
                  // input-interactivity signal that gates the per-step confirm loop (FR-011/FR-012).
                  AssumeYes = hasFlag "--yes" rest
                  IsInteractive = capabilities.IsInputInteractive }

            let report = driveToReport request

            // Resolve the effective rendering against the stream this report actually routes
            // to — Blocked reports go to stderr, everything else to stdout — so Rich degrades
            // to plain text when *that* sink is redirected or color-disabled (#68). Stream
            // routing and exit code are unchanged across formats.
            let routesToStderr = report.Outcome = Blocked

            let sinkRedirected =
                if routesToStderr then
                    Console.IsErrorRedirected
                else
                    Console.IsOutputRedirected

            let rendered = (resolve format (detectCapabilities sinkRedirected) report).Text

            if routesToStderr then
                Console.Error.WriteLine(rendered)
            else
                Console.Out.WriteLine(rendered)

            exitCodeForReport report

[<EntryPoint>]
let main argv = run (Array.toList argv)
