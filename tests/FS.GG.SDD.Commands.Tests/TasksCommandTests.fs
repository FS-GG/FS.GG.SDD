namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Commands.Internal.Foundation
open FS.GG.SDD.Commands.Internal.TaskGraphAuthoring
open Xunit

module TasksCommandTests =
    [<Theory>]
    [<InlineData("expecto", "expecto")>]
    [<InlineData("Expecto", "expecto")>]
    [<InlineData("NUnit", "nunit")>]
    [<InlineData("My Custom Runner", "my-custom-runner")>]
    [<InlineData("xunit", "xunit")>]
    let ``resolveTestSkill normalizes a declared framework`` (declared: string) (expected: string) =
        Assert.Equal(expected, resolveTestSkill (Some declared))

    [<Fact>]
    let ``resolveTestSkill yields the neutral skill when absent`` () =
        Assert.Equal(neutralTestSkill, resolveTestSkill None)
        Assert.Equal("automated-tests", resolveTestSkill None)

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("   ")>]
    let ``resolveTestSkill yields the neutral skill when blank`` (declared: string) =
        Assert.Equal(neutralTestSkill, resolveTestSkill (Some declared))
        Assert.Equal("automated-tests", resolveTestSkill (Some declared))

    let workId = "009-tasks-command"
    let title = "Tasks Command"
    let specPath = $"work/{workId}/spec.md"
    let tasksPath = $"work/{workId}/tasks.yml"
    let workModelPath = $"readiness/{workId}/work-model.json"

    let initializedPlanReadyProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root workId title |> ignore
        TestSupport.runPlan root workId title |> ignore
        root

    // Declare the SDD-owned `project.testFramework` on the already-initialized
    // `.fsgg/project.yml`; the tasks command reads it through the existing
    // project-config read effect. An empty/whitespace string declares nothing.
    let private declareTestFramework root (framework: string) =
        let projectYml = TestSupport.readRelative root ".fsgg/project.yml"

        let declared =
            projectYml.Replace("  defaultWorkRoot: work", $"  defaultWorkRoot: work\n  testFramework: {framework}")

        TestSupport.writeRelative root ".fsgg/project.yml" declared

    let private planReadyProjectDeclaring framework =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        declareTestFramework root framework
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root workId title |> ignore
        TestSupport.runPlan root workId title |> ignore
        root

    [<Fact>]
    let ``tasks obligation skill matches the declared Expecto framework`` () =
        let root = planReadyProjectDeclaring "expecto"
        // Evidence presence triggers the readiness work-model projection too.
        TestSupport.writePassingTaskEvidenceFor root workId

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath
        let workModel = TestSupport.readRelative root workModelPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        // The verification-obligation task carries the Expecto-matched skill.
        Assert.Contains("requiredSkills: [\"expecto\", \"readiness-evidence\"]", tasks)
        // No xunit token survives anywhere in the generated task metadata.
        Assert.DoesNotContain("xunit", tasks)
        Assert.DoesNotContain("xunit", workModel)
        Assert.Contains("expecto", workModel)

    [<Fact>]
    let ``tasks obligation skill is neutral when no framework is declared`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.writePassingTaskEvidenceFor root workId

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath
        let workModel = TestSupport.readRelative root workModelPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("requiredSkills: [\"automated-tests\", \"readiness-evidence\"]", tasks)
        // No framework-specific token leaks into the generated task metadata.
        Assert.DoesNotContain("xunit", tasks)
        Assert.DoesNotContain("expecto", tasks)
        Assert.DoesNotContain("xunit", workModel)
        Assert.DoesNotContain("expecto", workModel)

    [<Fact>]
    let ``tasks non-test category skills are unchanged by the framework-aware skill`` () =
        let root = planReadyProjectDeclaring "expecto"

        TestSupport.runTasks root workId title |> ignore
        let tasks = TestSupport.readRelative root tasksPath

        // Only the verification-obligation test skill is framework-aware; every
        // other task category keeps its exact skill list (SC-004 / FR-005).
        Assert.Contains("requiredSkills: [\"fsharp\", \"speckit-implement\"]", tasks)
        Assert.Contains("readiness-evidence", tasks)
        Assert.DoesNotContain("xunit", tasks)

    [<Fact>]
    let ``tasks generation is byte-identical across re-runs`` () =
        let root = planReadyProjectDeclaring "expecto"
        TestSupport.writePassingTaskEvidenceFor root workId

        TestSupport.runTasks root workId title |> ignore
        let firstTasks = TestSupport.readRelative root tasksPath
        let firstWorkModel = TestSupport.readRelative root workModelPath

        TestSupport.runTasks root workId title |> ignore
        let secondTasks = TestSupport.readRelative root tasksPath
        let secondWorkModel = TestSupport.readRelative root workModelPath

        Assert.Equal(firstTasks, secondTasks)
        Assert.Equal(firstWorkModel, secondWorkModel)

    [<Fact>]
    let ``tasks creates traceable task graph with real filesystem evidence`` () =
        let root = initializedPlanReadyProject ()

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("stage: tasks", tasks)
        Assert.Contains("status: tasksReady", tasks)
        Assert.Contains("sourcePlan: work/009-tasks-command/plan.md", tasks)
        Assert.Contains("id: T001", tasks)
        Assert.Contains("sourceIds:", tasks)
        Assert.Contains("requiredSkills:", tasks)
        Assert.Contains("requiredEvidence:", tasks)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = tasksPath && change.Operation = ArtifactOperation.Create
        )

        Assert.Equal(Some Analyze, report.NextAction |> Option.bind _.Command)
        Assert.True(report.Tasks.Value.TaskIds.Length >= 5)
        Assert.True(report.Tasks.Value.RequiredEvidenceCount >= report.Tasks.Value.TaskIds.Length)

    [<Fact>]
    let ``tasks creation does not require Governance files`` () =
        let root = initializedPlanReadyProject ()

        let report = TestSupport.runTasks root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``tasks missing plan blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root workId title |> ignore

        let report = TestSupport.runTasks root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingPlanPrerequisite")
        Assert.False(TestSupport.existsRelative root tasksPath)

    [<Fact>]
    let ``tasks rerun preserves existing authored task state`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let authored =
            (TestSupport.readRelative root tasksPath).Replace("owner: \"sdd\"", "owner: \"platform\"")

        TestSupport.writeRelative root tasksPath authored

        let report = TestSupport.runTasks root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root tasksPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = tasksPath && change.Operation = ArtifactOperation.NoChange
        )

    [<Fact>]
    let ``tasks marks existing tasks stale when source snapshots change`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let updatedSpec =
            (TestSupport.readRelative root specPath)
                .Replace(
                    "## Ambiguities",
                    "- FR-002: New task source requiring implementation. (Stories: US-001; Acceptance: AC-001)\n\n## Ambiguities"
                )

        TestSupport.writeRelative root specPath updatedSpec

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Contains("status: stale", tasks)
        Assert.Contains("FR-002", tasks)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "staleTask")
        Assert.Equal(Some "tasks.correctStaleTasks", report.NextAction |> Option.map _.ActionId)
        Assert.True(report.Tasks.Value.StaleCount > 0)

    [<Fact>]
    let ``tasks dependency cycle blocks without mutation`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let original =
            (TestSupport.readRelative root tasksPath).Replace("dependencies: []", "dependencies: [\"T001\"]")

        TestSupport.writeRelative root tasksPath original

        let report = TestSupport.runTasks root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "taskDependencyCycle")
        Assert.Equal(original, TestSupport.readRelative root tasksPath)

    [<Fact>]
    let ``tasks done without evidence blocks without mutation`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let original =
            (TestSupport.readRelative root tasksPath).Replace("status: pending", "status: done")

        TestSupport.writeRelative root tasksPath original

        let report = TestSupport.runTasks root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "doneTaskMissingEvidence")
        Assert.Equal(original, TestSupport.readRelative root tasksPath)

    [<Fact>]
    let ``tasks dry run reports proposed changes without mutation`` () =
        let root = initializedPlanReadyProject ()

        let request =
            { TestSupport.tasksRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.False(TestSupport.existsRelative root tasksPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = tasksPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``tasks refreshes generated work model when evidence source is present`` () =
        let root = initializedPlanReadyProject ()

        // The T001..T006 evidence ladder is derived once in TestShared (feature 067 / FR-011).
        TestSupport.writeRelative root $"work/{workId}/evidence.yml" TestSupport.passingTaskEvidence

        let report = TestSupport.runTasks root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Current
                && view.Sources |> List.exists (fun source -> source.Path = tasksPath)
        )

    [<Fact>]
    let ``tasks deterministic JSON is byte stable`` () =
        let root = initializedPlanReadyProject ()

        let request =
            { TestSupport.tasksRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"tasks\"", first)
        Assert.Contains("\"tasks\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``tasks text projection uses report facts`` () =
        let root = initializedPlanReadyProject ()
        let report = TestSupport.runTasks root workId title
        let text = renderText report

        Assert.Contains("command: tasks", text)
        Assert.Contains("tasks:", text)
        Assert.Contains("taskRequiredEvidence:", text)
        Assert.Contains("nextAction: nextLifecycleCommand", text)

    [<Fact>]
    let ``tasks create and rerun complete under local harness budget`` () =
        let root = initializedPlanReadyProject ()

        let createWatch = Stopwatch.StartNew()
        let createReport = TestSupport.runTasks root workId title
        createWatch.Stop()

        let rerunWatch = Stopwatch.StartNew()
        let rerunReport = TestSupport.runTasks root workId title
        rerunWatch.Stop()

        Assert.NotEqual(CommandOutcome.Blocked, createReport.Outcome)
        Assert.NotEqual(CommandOutcome.Blocked, rerunReport.Outcome)
        Assert.True(createWatch.Elapsed < TimeSpan.FromSeconds 2.0, $"Create took {createWatch.Elapsed}.")
        Assert.True(rerunWatch.Elapsed < TimeSpan.FromSeconds 2.0, $"Rerun took {rerunWatch.Elapsed}.")
