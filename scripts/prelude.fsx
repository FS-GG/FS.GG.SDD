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

let expectEqual label expected actual =
    if expected <> actual then
        failwithf "%s expected %A, got %A" label expected actual

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

expectEqual "parse analyze" (Ok Analyze) (CommandTypes.parseCommand "analyze")
expectEqual "parse evidence" (Ok Evidence) (CommandTypes.parseCommand "evidence")
expectEqual "analyze command stage" "analyze" (CommandTypes.commandStage Analyze)
expectEqual "evidence command stage" "evidence" (CommandTypes.commandStage Evidence)
expectEqual "next after tasks" (Some Analyze) (CommandTypes.nextLifecycleCommand Tasks)
expectEqual "next after analyze" (Some Evidence) (CommandTypes.nextLifecycleCommand Analyze)
expectEqual "next after evidence" None (CommandTypes.nextLifecycleCommand Evidence)

let analyzeRequest =
    ({ Command = Analyze
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = None
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let analysisFinalModel =
    runCommand analyzeRequest

let analysisReport =
    analysisFinalModel.Report
    |> Option.defaultWith (fun () -> CommandReports.buildReport analysisFinalModel)

let analysisSummary =
    analysisReport.Analysis
    |> Option.defaultWith (fun () -> failwith "Expected analysis summary.")

expectEqual "analysis command" Analyze analysisReport.Command
expectEqual "analysis readiness" "implementationReady" analysisSummary.Readiness

let evidenceInput =
    """schemaVersion: 1
workId: 009-tasks-command
stage: evidence
status: evidenceReady
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    obligationRefs: [EV001]
    result: pass
    synthetic: false
  - id: EV002
    kind: verification
    subject:
      type: task
      id: T002
    taskRefs: [T002]
    obligationRefs: [EV002]
    result: pass
    synthetic: false
  - id: EV003
    kind: verification
    subject:
      type: task
      id: T003
    taskRefs: [T003]
    obligationRefs: [EV003]
    result: pass
    synthetic: false
  - id: EV004
    kind: verification
    subject:
      type: task
      id: T004
    taskRefs: [T004]
    obligationRefs: [EV004]
    result: pass
    synthetic: false
  - id: EV005
    kind: verification
    subject:
      type: task
      id: T005
    taskRefs: [T005]
    obligationRefs: [EV005]
    result: pass
    synthetic: false
  - id: EV006
    kind: verification
    subject:
      type: task
      id: T006
    taskRefs: [T006]
    obligationRefs: [EV006]
    result: pass
    synthetic: false
"""

let parsedEvidence =
    LifecycleArtifacts.parseEvidenceArtifact
        ({ Path = "work/009-tasks-command/evidence.yml"
           Text = evidenceInput }
        : LifecycleArtifacts.FileSnapshot)
    |> Result.defaultWith (fun diagnostics -> failwithf "Expected evidence artifact: %A" diagnostics)

let evidenceRequest =
    ({ Command = Evidence
       ProjectRoot = commandRoot
       WorkId = Some "009-tasks-command"
       Title = Some "Tasks Command"
       InputText = Some evidenceInput
       OutputFormat = Json
       DryRun = false
       OverwritePolicy = RefuseUnsafe
       GeneratorVersion = SchemaVersion.currentGeneratorVersion() }
    : CommandRequest)

let evidenceFinalModel =
    runCommand evidenceRequest

let evidenceReport =
    evidenceFinalModel.Report
    |> Option.defaultWith (fun () -> CommandReports.buildReport evidenceFinalModel)

let evidenceSummary =
    evidenceReport.Evidence
    |> Option.defaultWith (fun () -> failwith "Expected evidence summary.")

expectEqual "evidence command" Evidence evidenceReport.Command
expectEqual "evidence readiness" "evidenceReady" evidenceSummary.Readiness

printfn "command=%s" (CommandTypes.commandName commandReport.Command)
printfn "tasksCommandStage=%s" (CommandTypes.commandStage Tasks)
printfn "nextAfterTasks=%s" (CommandTypes.nextLifecycleCommand Tasks |> Option.map CommandTypes.commandName |> Option.defaultValue "none")
printfn "analyzeCommandStage=%s" (CommandTypes.commandStage Analyze)
printfn "nextAfterAnalyze=%s" (CommandTypes.nextLifecycleCommand Analyze |> Option.map CommandTypes.commandName |> Option.defaultValue "none")
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
printfn "analysisCommand=%s" (CommandTypes.commandName analysisReport.Command)
printfn "analysisOutcome=%s" (CommandTypes.outcomeValue analysisReport.Outcome)
printfn "analysisPath=%s" analysisSummary.AnalysisPath
printfn "analysisReadiness=%s" analysisSummary.Readiness
printfn "analysisSources=%d" analysisSummary.SourceCount
printfn "analysisRelationships=%d" analysisSummary.SourceRelationshipCount
printfn "analysisGeneratedViews=%d" analysisReport.GeneratedViews.Length
printfn "analysisDiagnostics=%s" ([ missingTasksPrerequisite "work/009-tasks-command/tasks.yml" "Tasks are required."; malformedAnalysisView "readiness/009-tasks-command/analysis.json" "Analysis JSON is malformed."; analysisIdentityMismatch "readiness/009-tasks-command/analysis.json" "009-tasks-command" "other-work" ] |> List.map _.Id |> String.concat ",")
printfn "analysisNextAction=%s" (analysisReport.NextAction |> Option.map _.ActionId |> Option.defaultValue "none")
printfn "evidenceCommand=%s" (CommandTypes.commandName evidenceReport.Command)
printfn "evidenceCommandStage=%s" (CommandTypes.commandStage Evidence)
printfn "nextAfterEvidence=%s" (CommandTypes.nextLifecycleCommand Evidence |> Option.map CommandTypes.commandName |> Option.defaultValue "none")
printfn "evidenceOutcome=%s" (CommandTypes.outcomeValue evidenceReport.Outcome)
printfn "evidencePath=%s" evidenceSummary.EvidencePath
printfn "evidenceReadiness=%s" evidenceSummary.Readiness
printfn "evidenceDeclarations=%d" parsedEvidence.Evidence.Length
printfn "evidenceDeclarationIds=%s" (evidenceSummary.DeclarationIds |> String.concat ",")
printfn "evidenceObligations=%d" evidenceSummary.ObligationCount
printfn "evidenceSupported=%d" evidenceSummary.SupportedCount
printfn "evidenceDeferred=%d" evidenceSummary.DeferredCount
printfn "evidenceMissing=%d" evidenceSummary.MissingCount
printfn "evidenceSynthetic=%d" evidenceSummary.SyntheticCount
printfn "evidenceBlocking=%d" evidenceSummary.BlockingCount
printfn "evidenceDiagnostics=%s" ([ missingAnalysisPrerequisite "readiness/009-tasks-command/analysis.json" "Analysis is required."; analysisNotReady "readiness/009-tasks-command/analysis.json" "blocked"; malformedEvidenceArtifact "work/009-tasks-command/evidence.yml" "Evidence YAML is malformed."; missingRequiredEvidence "work/009-tasks-command/evidence.yml" [ "EV001" ]; undisclosedSyntheticEvidence "work/009-tasks-command/evidence.yml" [ "EV002" ]; unsafeEvidenceUpdate "work/009-tasks-command/evidence.yml" [ "EV003" ] ] |> List.map _.Id |> String.concat ",")
printfn "evidenceNextAction=%s" (evidenceReport.NextAction |> Option.map _.ActionId |> Option.defaultValue "none")
printfn "createdProjectConfig=%b" (File.Exists(Path.Combine(commandRoot, ".fsgg", "project.yml")))
printfn "createdCharter=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "charter.md")))
printfn "createdSpecification=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "spec.md")))
printfn "createdClarification=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "clarifications.md")))
printfn "createdChecklist=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "checklist.md")))
printfn "createdPlan=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "plan.md")))
printfn "createdTasks=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "tasks.yml")))
printfn "createdAnalysis=%b" (File.Exists(Path.Combine(commandRoot, "readiness", "009-tasks-command", "analysis.json")))
printfn "createdEvidence=%b" (File.Exists(Path.Combine(commandRoot, "work", "009-tasks-command", "evidence.yml")))
