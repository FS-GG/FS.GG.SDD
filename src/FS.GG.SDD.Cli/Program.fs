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

let hasFlag name args =
    args |> List.exists ((=) name)

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
    let generator = SchemaVersionModule.currentGeneratorVersion()

    let request =
        { Command = Init
          ProjectRoot = "."
          WorkId = None
          Title = None
          InputText = None
          OutputFormat = Json
          DryRun = true
          OverwritePolicy = RefuseUnsafe
          GeneratorVersion = generator
          Provider = None
          Parameters = []
          Force = false
          TemplateUpdate = true }

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
    let stdoutRendering = (resolveValidation format (detectCapabilities ()) report).Text

    // --out persists a deterministic projection only (never rich ANSI): the
    // canonical JSON for --json/default, else the portable plain text (FR-010).
    match optionValue "--out" rest with
    | Some path ->
        let persisted =
            match format with
            | Json -> FS.GG.SDD.Validation.ValidationContracts.serialize report
            | _ -> FS.GG.SDD.Validation.ValidationContracts.renderText report

        System.IO.File.WriteAllText(path, persisted)
    | None -> ()

    Console.Out.WriteLine(stdoutRendering)

    if report.Summary.OverallPassed then 0 else 1

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
    | "validate" :: rest -> printValidate rest
    | commandValue :: rest ->
        match parseCommand commandValue with
        | Error _ -> printUnknown commandValue
        | Ok command ->
            let format = outputFormat rest

            let request =
                { Command = command
                  ProjectRoot = optionValue "--root" rest |> Option.defaultValue "."
                  WorkId = optionValue "--work" rest
                  Title = optionValue "--title" rest
                  InputText = optionValue "--input" rest
                  OutputFormat = format
                  DryRun = hasFlag "--dry-run" rest
                  OverwritePolicy = (if command = Refresh then AllowGeneratedRefresh else RefuseUnsafe)
                  GeneratorVersion = SchemaVersionModule.currentGeneratorVersion()
                  Provider = optionValue "--provider" rest
                  Parameters = parseParams rest
                  Force = hasFlag "--force" rest
                  TemplateUpdate = not (hasFlag "--no-update" rest) }

            let model, effects = init request

            let rec interpretUntilIdle state pendingEffects =
                match pendingEffects with
                | [] -> state
                | effects ->
                    let results = interpretAll request.ProjectRoot request.DryRun effects

                    let nextState, nextEffects =
                        results
                        |> List.fold
                            (fun (currentState, accumulatedEffects) result ->
                                let updatedState, producedEffects = update (EffectInterpreted result) currentState
                                updatedState, accumulatedEffects @ producedEffects)
                            (state, [])

                    interpretUntilIdle nextState nextEffects

            let finalModel =
                interpretUntilIdle model effects
                |> fun state -> update BuildReport state |> fst

            let report = finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)

            // Resolve the effective rendering (Rich degrades to plain text when
            // non-interactive or color-disabled); stream routing and exit code are
            // unchanged across formats.
            let rendered = (resolve format (detectCapabilities ()) report).Text

            if report.Outcome = Blocked then Console.Error.WriteLine(rendered)
            else Console.Out.WriteLine(rendered)

            exitCodeForReport report

[<EntryPoint>]
let main argv =
    run (Array.toList argv)
