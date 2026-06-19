#r "nuget: YamlDotNet, 16.3.0"
#r "../src/FS.GG.SDD.Artifacts/bin/Release/net10.0/FS.GG.SDD.Artifacts.dll"
#r "../src/FS.GG.SDD.Commands/bin/Release/net10.0/FS.GG.SDD.Commands.dll"

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow

let repoRoot =
    let rec findRoot (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "FS.GG.SDD.sln")) then
            directory.FullName
        elif isNull directory.Parent then
            failwith "Could not locate repository root."
        else
            findRoot directory.Parent

    findRoot (DirectoryInfo(Environment.CurrentDirectory))

let fixtureRoot =
    Path.Combine(repoRoot, "tests", "fixtures", "normalized-work-model", "valid-work-item")

let snapshots =
    Directory.EnumerateFiles(fixtureRoot, "*", SearchOption.AllDirectories)
    |> Seq.map (fun path ->
        ({ Path = Path.GetRelativePath(fixtureRoot, path).Replace('\\', '/')
           Text = File.ReadAllText path }
        : LifecycleArtifacts.FileSnapshot))
    |> Seq.toList

let workId =
    Identifiers.createWorkId "002-normalized-work-model"
    |> Result.defaultWith failwith

printfn "workId=%s" (Identifiers.workIdValue workId)

let request =
    ({ WorkId = Identifiers.workIdValue workId
       Snapshots = snapshots
       GeneratorVersion = SchemaVersion.currentGeneratorVersion()
       ExpectedOutputPath = None }
    : WorkModel.WorkModelGenerationRequest)

let result =
    Serialization.generateWorkModel request

let model = result.Model

printfn "modelVersion=%s" model.ModelVersion
printfn "blockingDiagnostics=%d" (WorkModel.blockingDiagnostics model |> List.length)
printfn "requirements=%s" (model.Requirements |> List.map _.Id |> String.concat ",")
printfn "tasks=%s" (model.Tasks |> List.map _.Id |> String.concat ",")
printfn "governanceBoundaries=%s" (model.GovernanceBoundaries |> List.map _.Path |> String.concat ",")
printfn "outputPath=%s" result.OutputPath
printfn "outputDigest=%s:%s" result.OutputDigest.Algorithm result.OutputDigest.Value
printfn "jsonBytes=%d" (Text.Encoding.UTF8.GetByteCount result.Json)

let commandRoot =
    Path.Combine(Path.GetTempPath(), "fsgg-sdd-prelude-" + Guid.NewGuid().ToString("N"))

Directory.CreateDirectory commandRoot |> ignore

let initRequest =
    ({ Command = Init
       ProjectRoot = commandRoot
       WorkId = None
       Title = None
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let runCommand request =
    let commandModel, commandEffects =
        CommandWorkflow.init request

    let rec interpretUntilIdle state pending =
        match pending with
        | [] -> state
        | effects ->
            let nextState, nextEffects =
                CommandEffects.interpretAll request.ProjectRoot request.DryRun effects
                |> List.fold
                    (fun (currentState, accumulatedEffects) result ->
                        let updatedState, producedEffects =
                            CommandWorkflow.update (EffectInterpreted result) currentState

                        updatedState, accumulatedEffects @ producedEffects)
                    (state, [])

            interpretUntilIdle nextState nextEffects

    interpretUntilIdle commandModel commandEffects
    |> fun state -> CommandWorkflow.update BuildReport state |> fst

let initFinalModel =
    runCommand initRequest

let commandRequest =
    ({ Command = Charter
       ProjectRoot = commandRoot
       WorkId = Some "004-charter-command"
       Title = Some "Charter Command"
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let commandFinalModel =
    runCommand commandRequest

let commandReport =
    commandFinalModel.Report
    |> Option.defaultWith (fun () -> CommandReports.buildReport commandFinalModel)

printfn "command=%s" (CommandTypes.commandName commandReport.Command)
printfn "outcome=%s" (CommandTypes.outcomeValue commandReport.Outcome)
printfn "changedArtifacts=%d" commandReport.ChangedArtifacts.Length
printfn "generatedViews=%d" commandReport.GeneratedViews.Length
printfn "blockingDiagnostics=%d" (commandReport.Diagnostics |> List.filter (fun diagnostic -> diagnostic.Severity = Diagnostics.DiagnosticSeverity.DiagnosticError) |> List.length)
printfn "nextAction=%s" (commandReport.NextAction |> Option.map _.ActionId |> Option.defaultValue "none")
printfn "createdProjectConfig=%b" (File.Exists(Path.Combine(commandRoot, ".fsgg", "project.yml")))
printfn "createdCharter=%b" (File.Exists(Path.Combine(commandRoot, "work", "004-charter-command", "charter.md")))
