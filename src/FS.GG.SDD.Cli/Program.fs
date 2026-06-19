open System
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow

module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

let rec optionValue name args =
    match args with
    | current :: value :: _ when current = name -> Some value
    | _ :: rest -> optionValue name rest
    | [] -> None

let hasFlag name args =
    args |> List.exists ((=) name)

let outputFormat args =
    if hasFlag "--text" args then Text else Json

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
          Report = None }

    let report = buildReport model
    Console.Error.WriteLine(serializeReport report)
    exitCodeForReport report

let run args =
    match args with
    | [] -> printUnknown ""
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
                  OverwritePolicy = RefuseUnsafe
                  GeneratorVersion = SchemaVersionModule.currentGeneratorVersion() }

            let model, effects = init request

            let interpreted =
                if request.DryRun then
                    interpretAll request.ProjectRoot true effects
                else
                    interpretAll request.ProjectRoot false effects

            let finalModel =
                interpreted
                |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model
                |> fun state -> update BuildReport state |> fst

            let report = finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)
            let rendered = if format = Text then renderText report else serializeReport report

            if report.Outcome = Blocked then Console.Error.WriteLine(rendered)
            else Console.Out.WriteLine(rendered)

            exitCodeForReport report

[<EntryPoint>]
let main argv =
    run (Array.toList argv)
