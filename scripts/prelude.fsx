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

let charterRequest =
    ({ Command = Charter
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let charterFinalModel =
    runCommand charterRequest

let specifyRequest =
    ({ Command = Specify
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = Some "value: create a native tasks command\nscope: one planned work item\nrequirement: create a traceable implementation task graph with stable ids"
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let specifyFinalModel =
    runCommand specifyRequest

let clarifyRequest =
    ({ Command = Clarify
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let clarifyFinalModel =
    runCommand clarifyRequest

let checklistRequest =
    ({ Command = Checklist
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let checklistFinalModel =
    runCommand checklistRequest

let planRequest =
    ({ Command = Plan
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let planFinalModel =
    runCommand planRequest

let tasksRequest =
    ({ Command = Tasks
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let commandFinalModel =
    runCommand tasksRequest

let commandReport =
    commandFinalModel.Report
    |> Option.defaultWith (fun () -> CommandReports.buildReport commandFinalModel)

printfn "command=%s" (CommandTypes.commandName commandReport.Command)
printfn "tasksCommandStage=%s" (CommandTypes.commandStage Tasks)
printfn "nextAfterTasks=%s" (CommandTypes.nextLifecycleCommand Tasks |> Option.map CommandTypes.commandName |> Option.defaultValue "none")
printfn "outcome=%s" (CommandTypes.outcomeValue commandReport.Outcome)
printfn "changedArtifacts=%d" commandReport.ChangedArtifacts.Length
printfn "tasks=%d" (commandReport.Tasks |> Option.map (fun summary -> summary.TaskIds.Length) |> Option.defaultValue 0)
printfn "taskDependencies=%d" (commandReport.Tasks |> Option.map (fun summary -> summary.DependencyCount) |> Option.defaultValue 0)
printfn "taskRequiredSkills=%d" (commandReport.Tasks |> Option.map (fun summary -> summary.RequiredSkillCount) |> Option.defaultValue 0)
printfn "taskRequiredEvidence=%d" (commandReport.Tasks |> Option.map (fun summary -> summary.RequiredEvidenceCount) |> Option.defaultValue 0)
printfn "taskBlockingFindings=%d" (commandReport.Tasks |> Option.map (fun summary -> summary.BlockingFindingCount) |> Option.defaultValue 0)
printfn "taskAdvisory=%d" (commandReport.Tasks |> Option.map (fun summary -> summary.AdvisoryCount) |> Option.defaultValue 0)
printfn "generatedViews=%d" commandReport.GeneratedViews.Length
printfn "blockingDiagnostics=%d" (commandReport.Diagnostics |> List.filter (fun diagnostic -> diagnostic.Severity = Diagnostics.DiagnosticSeverity.DiagnosticError) |> List.length)
printfn "taskDiagnostics=%s" ([ missingPlanPrerequisite "work/009-tasks-command/plan.md" "Plan is required."; staleTask "work/009-tasks-command/tasks.yml" [ "T001" ]; doneTaskMissingEvidence "work/009-tasks-command/tasks.yml" [ "T001" ] ] |> List.map _.Id |> String.concat ",")
printfn "nextAction=%s" (commandReport.NextAction |> Option.map _.ActionId |> Option.defaultValue "none")
printfn "createdProjectConfig=%b" (File.Exists(Path.Combine(commandRoot, ".fsgg", "project.yml")))
printfn "createdCharter=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "charter.md")))
printfn "createdSpecification=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "spec.md")))
printfn "createdClarification=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "clarifications.md")))
printfn "createdChecklist=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "checklist.md")))
printfn "createdPlan=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "plan.md")))
printfn "createdTasks=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "tasks.yml")))
