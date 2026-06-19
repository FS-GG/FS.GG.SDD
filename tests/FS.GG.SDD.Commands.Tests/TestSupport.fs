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
