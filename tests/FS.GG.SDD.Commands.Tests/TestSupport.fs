namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow

module TestSupport =
    module SchemaVersionModule = FS.GG.SDD.Artifacts.SchemaVersion

    let rec findRepoRoot (directory: DirectoryInfo) =
        if File.Exists(Path.Combine(directory.FullName, "FS.GG.SDD.sln")) then
            directory.FullName
        else
            match directory.Parent with
            | null -> failwith "Could not locate repository root."
            | parent -> findRepoRoot parent

    let repoRoot = findRepoRoot (DirectoryInfo AppContext.BaseDirectory)

    let tempDirectory () =
        let path = Path.Combine(Path.GetTempPath(), "fsgg-sdd-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory path |> ignore
        path

    let request (command: SddCommand) (root: string) =
        { Command = command
          ProjectRoot = root
          WorkId = None
          Title = None
          InputText = None
          OutputFormat = Json
          DryRun = false
          OverwritePolicy = RefuseUnsafe
          GeneratorVersion = SchemaVersionModule.currentGeneratorVersion() }

    let readRelative (root: string) (path: string) =
        File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))

    let writeRelative (root: string) (path: string) (text: string) =
        let absolute = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))
        Directory.CreateDirectory(Path.GetDirectoryName absolute) |> ignore
        File.WriteAllText(absolute, text)

    let existsRelative (root: string) (path: string) =
        File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))

    let runRequest request =
        let model, effects = init request

        let rec interpretUntilIdle state pending =
            match pending with
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

        finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)

    let initializeProject root =
        request Init root |> runRequest |> ignore

    let charterRequest root workId title =
        { request Charter root with
            WorkId = Some workId
            Title = Some title }

    let runCharter root workId title =
        charterRequest root workId title |> runRequest

    let specifyIntent =
        "value: create a native specify command\nscope: one chartered work item\nrequirement: create a specification artifact with stable ids"

    let specifyRequest root workId title =
        { request Specify root with
            WorkId = Some workId
            Title = Some title
            InputText = Some specifyIntent }

    let runSpecify root workId title =
        specifyRequest root workId title |> runRequest

    let clarifyIntent =
        "AMB-001: Clarification decisions live in clarifications.md."

    let specifyIntentWithAmbiguity =
        "value: create a native clarify command\nscope: one specified work item\nrequirement: create a clarification artifact with stable ids\nambiguity: where should durable clarification decisions be recorded?"

    let clarifyRequest root workId title =
        { request Clarify root with
            WorkId = Some workId
            Title = Some title
            InputText = Some clarifyIntent }

    let runClarify root workId title =
        clarifyRequest root workId title |> runRequest

    let checklistRequest root workId title =
        { request Checklist root with
            WorkId = Some workId
            Title = Some title }

    let runChecklist root workId title =
        checklistRequest root workId title |> runRequest

    let planRequest root workId title =
        { request Plan root with
            WorkId = Some workId
            Title = Some title }

    let runPlan root workId title =
        planRequest root workId title |> runRequest

    let tasksRequest root workId title =
        { request Tasks root with
            WorkId = Some workId
            Title = Some title }

    let runTasks root workId title =
        tasksRequest root workId title |> runRequest

    let initializePlanReadyProject root workId title =
        initializeProject root
        runCharter root workId title |> ignore
        runSpecify root workId title |> ignore
        runRequest { clarifyRequest root workId title with InputText = None } |> ignore
        runChecklist root workId title |> ignore
        runPlan root workId title |> ignore

    let passingTaskEvidence =
        """schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    result: pass
  - id: EV002
    kind: verification
    subject:
      type: task
      id: T002
    result: pass
  - id: EV003
    kind: verification
    subject:
      type: task
      id: T003
    result: pass
  - id: EV004
    kind: verification
    subject:
      type: task
      id: T004
    result: pass
  - id: EV005
    kind: verification
    subject:
      type: task
      id: T005
    result: pass
  - id: EV006
    kind: verification
    subject:
      type: task
      id: T006
    result: pass
"""

    let writePassingTaskEvidenceFor root workId =
        writeRelative root $"work/{workId}/evidence.yml" passingTaskEvidence

    let validSpec workId title =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: charter
changeTier: tier1
status: draft
---

# {title} Specification

- FR-001: The selected work item has one typed requirement.
"""

    let validTasks =
        """schemaVersion: 1
tasks:
  - id: T001
    title: Implement selected lifecycle work
    status: pending
    owner: sdd
    dependencies: []
    requirements: [FR-001]
    decisions: []
    requiredSkills: []
    requiredEvidence: []
"""

    let validEvidence =
        """schemaVersion: 1
evidence: []
"""

    let writeValidWorkSources root workId title =
        writeRelative root $"work/{workId}/spec.md" (validSpec workId title)
        writeRelative root $"work/{workId}/tasks.yml" validTasks
        writeRelative root $"work/{workId}/evidence.yml" validEvidence

    let writeValidTasksAndEvidence root =
        writeRelative root "work/005-specify-command/tasks.yml" validTasks
        writeRelative root "work/005-specify-command/evidence.yml" validEvidence

    let writeValidTasksAndEvidenceFor root workId =
        writeRelative root $"work/{workId}/tasks.yml" validTasks
        writeRelative root $"work/{workId}/evidence.yml" validEvidence

    let validClarification workId title =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/{workId}/spec.md
publicOrToolFacingImpact: true
---

# {title} Clarifications

## Source Specification
- work/{workId}/spec.md

## Clarification Questions
No clarification questions recorded.

## Answers
No clarification answers recorded.

## Decisions
No concrete decisions recorded.

## Accepted Deferrals
No accepted deferrals recorded.

## Remaining Ambiguity
No blocking ambiguity remains.

## Lifecycle Notes
- Next lifecycle action: checklist.
"""

    let writeValidClarification root workId title =
        writeRelative root $"work/{workId}/clarifications.md" (validClarification workId title)

    let writeExistingChecklist root workId text =
        writeRelative root $"work/{workId}/checklist.md" text

    let dryRunDigest text =
        SchemaVersionModule.sha256Text text

    let assertChecklistSummary (report: CommandReport) itemCount resultCount =
        match report.Checklist with
        | Some summary ->
            if summary.ItemIds.Length <> itemCount || summary.ResultIds.Length <> resultCount then
                failwith $"Expected checklist summary {itemCount}/{resultCount}, got {summary.ItemIds.Length}/{summary.ResultIds.Length}."
        | None -> failwith "Expected checklist summary."

    let assertPlanSummary (report: CommandReport) decisionCount contractCount obligationCount =
        match report.Plan with
        | Some summary ->
            if summary.DecisionIds.Length <> decisionCount
               || summary.ContractReferenceIds.Length <> contractCount
               || summary.VerificationObligationIds.Length <> obligationCount then
                failwith
                    $"Expected plan summary {decisionCount}/{contractCount}/{obligationCount}, got {summary.DecisionIds.Length}/{summary.ContractReferenceIds.Length}/{summary.VerificationObligationIds.Length}."
        | None -> failwith "Expected plan summary."

    let assertTasksSummary (report: CommandReport) taskCount dependencyCount requiredEvidenceCount =
        match report.Tasks with
        | Some summary ->
            if summary.TaskIds.Length <> taskCount
               || summary.DependencyCount <> dependencyCount
               || summary.RequiredEvidenceCount <> requiredEvidenceCount then
                failwith
                    $"Expected task summary {taskCount}/{dependencyCount}/{requiredEvidenceCount}, got {summary.TaskIds.Length}/{summary.DependencyCount}/{summary.RequiredEvidenceCount}."
        | None -> failwith "Expected tasks summary."
