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

    // #310 (AC8): the implement skill is declared, not hardcoded. Same normalization and the same
    // degrade-to-neutral contract as the test skill above.
    [<Theory>]
    [<InlineData("speckit-implement", "speckit-implement")>]
    [<InlineData("SpecKit-Implement", "speckit-implement")>]
    [<InlineData("My Custom Implementer", "my-custom-implementer")>]
    let ``resolveImplementSkill normalizes a declared skill`` (declared: string) (expected: string) =
        Assert.Equal(expected, resolveImplementSkill (Some declared))

    [<Fact>]
    let ``resolveImplementSkill yields the neutral skill when absent`` () =
        Assert.Equal(neutralImplementSkill, resolveImplementSkill None)
        Assert.Equal("implementation", resolveImplementSkill None)

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("   ")>]
    let ``resolveImplementSkill yields the neutral skill when blank`` (declared: string) =
        Assert.Equal(neutralImplementSkill, resolveImplementSkill (Some declared))
        Assert.Equal("implementation", resolveImplementSkill (Some declared))

    let workId = "009-tasks-command"
    let title = "Tasks Command"
    let specPath = $"work/{workId}/spec.md"
    let planPath = $"work/{workId}/plan.md"
    let tasksPath = $"work/{workId}/tasks.yml"
    let workModelPath = $"readiness/{workId}/work-model.json"

    /// Feature 090 (#163). Editing an upstream source after `plan` has run invalidates the plan's
    /// recorded `## Source Snapshot`, so `tasks` and `analyze` now block with `stalePlanSnapshot`
    /// until the operator re-baselines the plan (FR-008 / SC-004: no stale plan reaches task
    /// generation). Tests below that mutate a source mid-lifecycle therefore perform the same
    /// one-command re-baseline a real operator would. Before 090 they silently generated a task
    /// graph from a plan that no longer matched its sources.
    let private acceptUpstream root =
        TestSupport.runRequest
            { TestSupport.planRequest root workId title with
                AcceptUpstream = true }
        |> ignore

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

    // #310 (AC8): the same read effect also carries `project.implementSkill`.
    let private declareImplementSkill root (skill: string) =
        let projectYml = TestSupport.readRelative root ".fsgg/project.yml"

        let declared =
            projectYml.Replace("  defaultWorkRoot: work", $"  defaultWorkRoot: work\n  implementSkill: {skill}")

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
        Assert.Contains("requiredSkills: [expecto, readiness-evidence]", tasks)
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
        Assert.Contains("requiredSkills: [automated-tests, readiness-evidence]", tasks)
        // No framework-specific token leaks into the generated task metadata.
        Assert.DoesNotContain("xunit", tasks)
        Assert.DoesNotContain("expecto", tasks)
        Assert.DoesNotContain("xunit", workModel)
        Assert.DoesNotContain("expecto", workModel)

    // ---- #310 -----------------------------------------------------------------------------

    /// AC8: the implement skill comes from `.fsgg/project.yml`, not a `speckit-implement` literal.
    [<Fact>]
    let ``tasks implement skill matches the declared implementSkill`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        declareImplementSkill root "my-implementer"
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root workId title |> ignore
        TestSupport.runPlan root workId title |> ignore

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("requiredSkills: [fsharp, my-implementer]", tasks)
        // SDD's own authoring toolchain no longer leaks into a consumer's task graph.
        Assert.DoesNotContain("speckit-implement", tasks)

    /// AC8: with nothing declared, the derived skill is the neutral default.
    [<Fact>]
    let ``tasks implement skill is neutral when no implementSkill is declared`` () =
        let root = initializedPlanReadyProject ()

        TestSupport.runTasks root workId title |> ignore
        let tasks = TestSupport.readRelative root tasksPath

        Assert.Contains("requiredSkills: [fsharp, implementation]", tasks)
        Assert.DoesNotContain("speckit-implement", tasks)

    /// AC7: `requiredSkills` is authored state and must survive a re-derivation. Breakout1 lost 54
    /// hand-edited entries because `mergeAuthoredTaskState` carried every other authored field.
    [<Fact>]
    let ``tasks carries a hand-edited requiredSkills across a regeneration`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let generated = TestSupport.readRelative root tasksPath

        let authored =
            generated.Replace("requiredSkills: [fsharp, implementation]", "requiredSkills: [fsharp, hand-edited-skill]")

        TestSupport.writeRelative root tasksPath authored

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("hand-edited-skill", tasks)
        // Unioned, not replaced: the derived skills still appear alongside the authored one.
        Assert.Contains("requiredSkills: [fsharp, hand-edited-skill, implementation]", tasks)

    /// AC9: the plan scaffold derives one `PD-###` per FR mirroring that FR's own refs. `tasks`
    /// must not emit a second task over the identical FR/AC set — it folds the PD id into the
    /// requirement task, which keeps the decision disposed (analyze's `required` set demands it).
    [<Fact>]
    let ``tasks folds a plan decision subsumed by its requirement task`` () =
        let root = initializedPlanReadyProject ()

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        // No duplicate task for PD-001 ...
        Assert.DoesNotContain("Implement plan decision PD-001", tasks)
        // ... but the requirement task disposes it in its sourceIds.
        Assert.Contains("sourceIds: [AC-001, FR-001, PD-001]", tasks)

        // So `analyze` cannot report the decision as an undisposed obligation. This is the half of
        // the collapse that is easy to get wrong: dropping the task without folding the id would
        // block `analyze` with `missingDisposition` two stages downstream.
        TestSupport.writePassingTaskEvidenceFor root workId
        let analysis = TestSupport.runAnalyze root workId title

        Assert.DoesNotContain("missingDisposition", analysis.Diagnostics |> List.map (fun diagnostic -> diagnostic.Id))

    [<Fact>]
    let ``tasks non-test category skills are unchanged by the framework-aware skill`` () =
        let root = planReadyProjectDeclaring "expecto"

        TestSupport.runTasks root workId title |> ignore
        let tasks = TestSupport.readRelative root tasksPath

        // Only the verification-obligation test skill is framework-aware; every
        // other task category keeps its exact skill list (SC-004 / FR-005).
        Assert.Contains("requiredSkills: [fsharp, implementation]", tasks)
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
    let ``tasks re-run preserves an authored title and publicOrToolFacingImpact (#181)`` () =
        // FS.GG.SDD#181: the front-matter renderer hardcoded the request-derived title and
        // `publicOrToolFacingImpact: true`, so a re-run reverted a custom title to the humanized id
        // and flipped an authored `false` back to `true`. Both authored values must now survive.
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let customized =
            TestSupport.readRelative root tasksPath
            |> fun text -> text.Split('\n')
            |> Array.map (fun line ->
                if line.StartsWith("  title:") then
                    "  title: A deliberately custom work title"
                elif line.StartsWith("  publicOrToolFacingImpact:") then
                    "  publicOrToolFacingImpact: false"
                else
                    line)
            |> String.concat "\n"

        // Guard the fixture: the edit must have actually taken effect.
        Assert.Contains("  title: A deliberately custom work title", customized)
        Assert.Contains("  publicOrToolFacingImpact: false", customized)

        TestSupport.writeRelative root tasksPath customized

        let report = TestSupport.runTasks root workId title
        let reRun = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        // `yamlString` always double-quotes the FRONT-MATTER work title, so it comes back quoted
        // (only the task *records* adopt the codec's minimal quoting, FS.GG.SDD#260).
        Assert.Contains("title: \"A deliberately custom work title\"", reRun)
        Assert.Contains("publicOrToolFacingImpact: false", reRun)
        Assert.DoesNotContain("publicOrToolFacingImpact: true", reRun)

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
            (TestSupport.readRelative root tasksPath).Replace("owner: sdd", "owner: platform")

        TestSupport.writeRelative root tasksPath authored

        let report = TestSupport.runTasks root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root tasksPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = tasksPath && change.Operation = ArtifactOperation.NoChange
        )

    // Feature 082 (#147, FR-004/FR-005, SC-002): a re-run after an upstream source change
    // re-derives the graph IN PLACE — the new source's task appears and the run never reports
    // stale-and-unchanged. (Replaces the prior stale-relabel expectation.)
    [<Fact>]
    let ``tasks re-run re-derives the graph when a source adds a new task`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let updatedSpec =
            (TestSupport.readRelative root specPath)
                .Replace(
                    "## Ambiguities",
                    "- FR-002: New task source requiring implementation. (Stories: US-001; Acceptance: AC-001)\n\n## Ambiguities"
                )

        TestSupport.writeRelative root specPath updatedSpec
        acceptUpstream root

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("Implement requirement FR-002", tasks)
        Assert.DoesNotContain("status: stale", tasks)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "staleTask")
        Assert.Equal(0, report.Tasks.Value.StaleCount)

    // Feature 082 (#147, FR-007, SC-004): authored per-task `owner` survives a re-derive
    // triggered by a source change — the merge carries it onto the re-derived task by title.
    [<Fact>]
    let ``tasks re-run preserves authored owner across a re-derive`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let authored =
            (TestSupport.readRelative root tasksPath).Replace("owner: sdd", "owner: platform")

        TestSupport.writeRelative root tasksPath authored

        let updatedSpec =
            (TestSupport.readRelative root specPath)
                .Replace(
                    "## Ambiguities",
                    "- FR-002: Second requirement. (Stories: US-001; Acceptance: AC-001)\n\n## Ambiguities"
                )

        TestSupport.writeRelative root specPath updatedSpec
        acceptUpstream root

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("Implement requirement FR-002", tasks)
        Assert.Contains("owner: platform", tasks) // bare under minimal quoting (FS.GG.SDD#260)
        Assert.DoesNotContain("status: stale", tasks)

    // Feature 082 (#147, research Decision 3): a task whose source no longer exists is DROPPED
    // on re-derive (not relabeled stale and carried forward).
    [<Fact>]
    let ``tasks re-run drops a task whose source no longer exists`` () =
        let root = initializedPlanReadyProject ()
        let original = TestSupport.readRelative root specPath

        let withSecond =
            original.Replace(
                "## Ambiguities",
                "- FR-002: Second requirement. (Stories: US-001; Acceptance: AC-001)\n\n## Ambiguities"
            )

        TestSupport.writeRelative root specPath withSecond
        acceptUpstream root
        TestSupport.runTasks root workId title |> ignore
        Assert.Contains("Implement requirement FR-002", TestSupport.readRelative root tasksPath)

        // Remove FR-002 from the source and re-run.
        TestSupport.writeRelative root specPath original

        // 090 (#163): deleting a requirement leaves the plan carrying derived rows that reference
        // it, so `plan` blocks on `unknownPlanSourceReference` until the operator prunes them —
        // pre-existing plan behavior, now reached because `tasks` no longer runs against a plan
        // whose snapshot is stale. Prune, then re-baseline, exactly as an operator would.
        let prunedPlan =
            (TestSupport.readRelative root planPath).Replace("\r\n", "\n").Split('\n')
            |> Array.filter (fun line -> not (line.Contains "FR-002"))
            |> String.concat "\n"

        TestSupport.writeRelative root planPath prunedPlan
        acceptUpstream root

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.DoesNotContain("Implement requirement FR-002", tasks)
        Assert.DoesNotContain("status: stale", tasks)

    // Feature 082 (review fix): a task's evidence-obligation id (EV###) mirrors its stable
    // T### id, so a title-matched task keeps its EV### across a re-derive that reorders
    // derivation — an existing `evidence.yml` keyed to that EV### stays linked. Guards against
    // re-deriving EV ids position-first (which shifted them when a source was added ahead).
    [<Fact>]
    let ``tasks re-run keeps each task's evidence id coupled to its stable id`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore

        let updatedSpec =
            (TestSupport.readRelative root specPath)
                .Replace(
                    "## Ambiguities",
                    "- FR-002: Second requirement. (Stories: US-001; Acceptance: AC-001)\n\n## Ambiguities"
                )

        TestSupport.writeRelative root specPath updatedSpec
        TestSupport.runTasks root workId title |> ignore

        let tasks = TestSupport.readRelative root tasksPath

        let numbers pattern =
            [ for m in System.Text.RegularExpressions.Regex.Matches(tasks, pattern) -> m.Groups.[1].Value ]
            |> List.sort

        let idNumbers = numbers @"id: T0*(\d+)"
        let evidenceNumbers = numbers @"requiredEvidence: \[EV0*(\d+)\]"

        Assert.NotEmpty idNumbers
        Assert.Equal<string list>(idNumbers, evidenceNumbers)

    // Feature 082 (reclaim decision, research Q3): dependencies are tool-derived, so a
    // hand-injected dependency cycle is re-derived away (reclaimed), not persisted or blocked.
    // Genuine tool-derived cycles are still caught by validation on the re-derived graph.
    [<Fact>]
    let ``tasks re-run reclaims a hand-injected dependency cycle`` () =
        let root = initializedPlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore
        let original = TestSupport.readRelative root tasksPath

        let tampered = original.Replace("dependencies: []", "dependencies: [\"T001\"]")
        TestSupport.writeRelative root tasksPath tampered

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "taskDependencyCycle")
        Assert.Equal(original, tasks)

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

        // The T001..T005 evidence ladder is derived once in TestShared (feature 067 / FR-011).
        // #355: go through the helper, which writes the cited artifacts alongside the declaration —
        // writing `evidence.yml` alone would cite five proving tests that do not exist.
        TestSupport.writePassingTaskEvidenceFor root workId

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

    // #162: a RESOLVED clarify decision (`## Decisions` → `- DEC-###: …`) must be routed to a
    // disposing task by the tasks generator. Previously only accepted *deferrals* got a task, so
    // a real decision was stranded and `analyze` blocked two stages later with `missingDisposition`
    // — exactly when clarify had done its job. The decision is carried in the typed `decisions:`
    // field of a dedicated task.
    let private clarificationsPath = $"work/{workId}/clarifications.md"

    let private injectResolvedDecision root =
        let clarifications = TestSupport.readRelative root clarificationsPath

        let withDecision =
            clarifications.Replace(
                "## Accepted Deferrals",
                "- DEC-001: Record clarification decisions in clarifications.md.\n\n## Accepted Deferrals"
            )

        TestSupport.writeRelative root clarificationsPath withDecision
        // 090 (#163): the clarify edit moved the plan's recorded snapshot; re-baseline before tasks.
        acceptUpstream root
        // #351: `acceptUpstream` RE-RUNS `plan`, which scaffolds fresh prose for whatever the new
        // clarify decision added. That prose is unauthored, so `analyze` blocks — correctly. Author
        // it again, exactly as a human would after re-planning.
        TestSupport.authorPlanProse root workId

    [<Fact>]
    let ``tasks routes a resolved clarify decision to a disposing task`` () =
        let root = initializedPlanReadyProject ()
        injectResolvedDecision root

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("Implement clarification decision DEC-001", tasks)
        Assert.Contains("decisions: [DEC-001]", tasks)

    // The end-to-end regression: with the decision now disposed at `tasks`, `analyze` no longer
    // backtracks through three green stages to block on `missingDisposition` (#162).
    [<Fact>]
    let ``analyze no longer strands a resolved clarify decision`` () =
        let root = initializedPlanReadyProject ()
        injectResolvedDecision root

        TestSupport.runTasks root workId title |> ignore
        TestSupport.writePassingTaskEvidenceFor root workId

        let report = TestSupport.runAnalyze root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingDisposition")

    // #162 Part 2: the disposition-completeness check now fires at the stage that BUILDS the graph.
    // A required id the generator cannot derive a task for (here an acceptance scenario referenced
    // by no requirement) blocks `tasks` with `missingDisposition` naming the id — instead of
    // silently passing tasks and blocking `analyze` a stage later.
    [<Fact>]
    let ``tasks fails fast on a required disposition it cannot derive`` () =
        let root = initializedPlanReadyProject ()

        let withOrphanScenario =
            (TestSupport.readRelative root specPath)
                .Replace(
                    "## Acceptance Scenarios\n",
                    "## Acceptance Scenarios\n- AC-777: Scenario referenced by no requirement.\n"
                )

        TestSupport.writeRelative root specPath withOrphanScenario

        let report = TestSupport.runTasks root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        Assert.Contains(
            report.Diagnostics,
            fun diagnostic ->
                diagnostic.Id = "missingDisposition"
                && diagnostic.RelatedIds |> List.contains "AC-777"
        )

        Assert.False(TestSupport.existsRelative root tasksPath)

    // #311: `missingDisposition`'s `fix:` names a route the author can actually take. Pin the
    // route so the text cannot drift from the code: a `Plan Decisions` PD-### line tagged with
    // the stranded id disposes it. `planSourceIdsInLine` lifts every id on the line into the
    // decision's `SourceIds`, and `planDecisionTasks` forwards those into the generated task's
    // `sourceIds`, which is what `allTaskDispositionIds` reads.
    //
    // The tag is id-class agnostic — the same alternation matches `AC` and `DEC` alike. The
    // orphan acceptance scenario above is the case that reaches an author, because every other
    // required class already has its own generator (a resolved clarify DEC-### gets one from
    // `clarificationDecisionTasks`, per #162 above).
    [<Fact>]
    let ``a plan decision line tagged with a stranded id disposes it`` () =
        let root = initializedPlanReadyProject ()

        let withOrphanScenario =
            (TestSupport.readRelative root specPath)
                .Replace(
                    "## Acceptance Scenarios\n",
                    "## Acceptance Scenarios\n- AC-777: Scenario referenced by no requirement.\n"
                )

        TestSupport.writeRelative root specPath withOrphanScenario
        // 090 (#163): the spec edit moved the plan's recorded snapshot; re-baseline before tasks.
        acceptUpstream root

        let withDisposingDecision =
            (TestSupport.readRelative root planPath)
                .Replace(
                    "## Plan Decisions\n",
                    "## Plan Decisions\n- PD-900 [AC-777] complete: Dispose the orphan acceptance scenario.\n"
                )

        TestSupport.writeRelative root planPath withDisposingDecision

        let report = TestSupport.runTasks root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingDisposition")
        Assert.Contains("AC-777", TestSupport.readRelative root tasksPath)

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

    // ---- #306: the visual-inspection obligation -----------------------------------------------

    let private visualSurfacePlanReadyProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.declareVisualSurface root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root workId title |> ignore
        TestSupport.runPlan root workId title |> ignore
        root

    /// FR-002: the declaration derives exactly one render-and-look task, tagged, with one obligation.
    [<Fact>]
    let ``tasks derives a visual-inspection task when a visual surface is declared`` () =
        let root = visualSurfacePlanReadyProject ()

        let report = TestSupport.runTasks root workId title
        let tasks = TestSupport.readRelative root tasksPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains($"title: {visualInspectionTaskTitle}", tasks)
        Assert.Contains("requiredSkills: [implementation, visual-inspection]", tasks)
        // FR-002/§Design: it descends from no lifecycle fact id, so it declares none. Any id it
        // invented would be rejected by `taskValidationDiagnostics.unknownSources`.
        let visualTaskBlock = tasks.Substring(tasks.IndexOf(visualInspectionTaskTitle))
        Assert.Contains("sourceIds: []", visualTaskBlock)
        // FR-003: exactly one obligation, minted by the existing per-task mechanism.
        Assert.Equal(1, TestSupport.countOccurrences visualInspectionTaskTitle tasks)

    /// FR-002: with nothing declared, no such task exists. This is the whole of SC-001 at the tasks
    /// seam — an undeclaring workspace's graph is byte-identical to the graph before this feature.
    [<Fact>]
    let ``tasks derives no visual-inspection task when nothing is declared`` () =
        let root = initializedPlanReadyProject ()

        TestSupport.runTasks root workId title |> ignore
        let tasks = TestSupport.readRelative root tasksPath

        Assert.DoesNotContain(visualInspectionTaskTitle, tasks)
        Assert.DoesNotContain("visual-inspection", tasks)

    /// SC-004: the task's title is its identity across a re-derivation, so its `T###` — and the
    /// `EV###` obligation keyed to it — are stable and the second run is a byte-for-byte noChange.
    [<Fact>]
    let ``tasks re-derives the visual-inspection task idempotently`` () =
        let root = visualSurfacePlanReadyProject ()

        TestSupport.runTasks root workId title |> ignore
        let first = TestSupport.readRelative root tasksPath

        let report = TestSupport.runTasks root workId title
        let second = TestSupport.readRelative root tasksPath

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(first, second)

    /// The declaration flips off: the derived task covers no live disposition, so the existing
    /// orphan rule in `mergeAuthoredTaskState` reclaims it rather than stranding it forever.
    [<Fact>]
    let ``tasks drops the visual-inspection task when the declaration is withdrawn`` () =
        let root = visualSurfacePlanReadyProject ()
        TestSupport.runTasks root workId title |> ignore
        Assert.Contains(visualInspectionTaskTitle, TestSupport.readRelative root tasksPath)

        let projectYml = TestSupport.readRelative root ".fsgg/project.yml"
        TestSupport.writeRelative root ".fsgg/project.yml" (projectYml.Replace("  visualSurface: true\n", ""))

        TestSupport.runTasks root workId title |> ignore
        Assert.DoesNotContain(visualInspectionTaskTitle, TestSupport.readRelative root tasksPath)
