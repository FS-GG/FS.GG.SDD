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
          GeneratorVersion = generator }

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
          GeneratedViews = []
          Report = None }

    let report = buildReport model
    Console.Error.WriteLine(serializeReport report)
    exitCodeForReport report

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
                  GeneratorVersion = SchemaVersionModule.currentGeneratorVersion() }

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
