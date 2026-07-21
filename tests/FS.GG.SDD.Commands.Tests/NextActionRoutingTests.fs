namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// FS-GG/FS.GG.SDD#252 item 2: a tool-defect blocker (the exit-2 class — the `unhandledException`
// backstop, `scaffold.provider*`, `upgrade.selfUpdateFailed`/`stepFailed`) is NOT correctable by the
// agent. Routing it to the generic `correctBlockingDiagnostics` NextAction ("The command is blocked
// by diagnostics.") contradicts the diagnostic's own "this is a tool defect, not a problem with your
// input" text. When every blocking diagnostic is a tool defect the route is a no-action
// `reportToolDefect`; a *mixed* set (a correctable input error alongside a defect) keeps the generic
// action because the input error genuinely is correctable.
module NextActionRoutingTests =

    let private modelWith (command: SddCommand) (diagnostics: Diagnostic list) =
        { Request = TestSupport.request command "."
          PendingEffects = []
          InterpretedEffects = []
          Diagnostics = diagnostics
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
          Doctor = None
          Upgrade = None
          Lint = None
          Surface = None
          DependencySurface = None
          GeneratedViews = []
          Report = None }

    [<Fact>]
    let ``a sole tool-defect blocker routes to reportToolDefect, not correctBlockingDiagnostics`` () =
        let report = buildReport (modelWith Scaffold [ scaffoldProviderFailed "acme" 1 ])

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(2, exitCodeForReport report)

        let action = report.NextAction.Value
        Assert.Equal("reportToolDefect", action.ActionId)
        // A tool defect is uncorrectable: no command to run to fix it.
        Assert.True(action.Command.IsNone)
        // The blocking id is still surfaced so an agent can name the defect it hit.
        Assert.Contains("scaffold.providerFailed", action.BlockingDiagnosticIds)
        // The old generic wording (which told the agent to *correct* the block) is gone.
        Assert.DoesNotContain("blocked by diagnostics", action.Reason)

    // The `unhandledException` backstop (#250) is the CLI-edge tool defect that motivated this route:
    // its report used to advise `correctBlockingDiagnostics`, i.e. "correct" an internal crash.
    [<Fact>]
    let ``the unhandledException backstop report routes to reportToolDefect`` () =
        let report = buildReport (modelWith Init [ unhandledException "kaboom" ])

        Assert.Equal("reportToolDefect", report.NextAction.Value.ActionId)
        Assert.Equal(2, exitCodeForReport report)

    // A confirmed `upgrade` self-update failure is a tool defect too — previously it routed to the
    // generic action with `Command = Some Upgrade`; now it reports the defect (its own Correction
    // carries the manual remedy).
    [<Fact>]
    let ``a blocked upgrade tool defect routes to reportToolDefect`` () =
        let report = buildReport (modelWith Upgrade [ upgradeSelfUpdateFailed 1 ])

        Assert.Equal("reportToolDefect", report.NextAction.Value.ActionId)
        Assert.True(report.NextAction.Value.Command.IsNone)

    // A mixed set keeps the generic action: `unknownCommand` is an error-severity diagnostic that is
    // NOT a tool defect, so the agent can correct it even though a defect co-occurs. The exit code is
    // still 2 (any tool defect escalates it — unchanged in ReportAssembly).
    [<Fact>]
    let ``a tool defect mixed with a correctable input error keeps correctBlockingDiagnostics`` () =
        let report =
            buildReport (modelWith Scaffold [ scaffoldProviderFailed "acme" 1; unknownCommand "frobnicate" ])

        Assert.Equal("correctBlockingDiagnostics", report.NextAction.Value.ActionId)
        Assert.Equal(2, exitCodeForReport report)

    // FS-GG/FS.GG.SDD#642: an upstream artifact edit stales a downstream stage's recorded source
    // digest, and the reconcile ORDER — which stages to re-run, and in what sequence — was previously
    // learned by trial rather than surfaced. The stale-digest NextActions now name the ordered re-run
    // set: the stale stage plus each *materialized* downstream stage in canonical order. "Materialized"
    // is sensed from the lifecycle rail (a `work/`/`readiness/` directory enumeration), so a downstream
    // stage that was never run — carrying no digest to stale — is omitted.

    /// Build a model whose lifecycle sensing sees exactly `presentPaths` as materialized (the paths a
    /// `work`/`readiness` enumeration would list), under work id `workId`.
    let private modelWithPresence
        (command: SddCommand)
        (workId: string)
        (presentPaths: string list)
        (diagnostics: Diagnostic list)
        =
        let enumResult (dir: string) : CommandEffectResult =
            let listed =
                presentPaths
                |> List.filter (fun path -> path.StartsWith(dir + "/"))
                |> String.concat "\n"

            { Effect = EnumerateDirectory dir
              Succeeded = true
              Snapshot = Some { Path = dir; Text = listed }
              Process = None
              Confirmed = None
              Diagnostic = None }

        let baseModel = modelWith command diagnostics

        { baseModel with
            Request =
                { baseModel.Request with
                    WorkId = Some workId }
            InterpretedEffects = [ enumResult "work"; enumResult "readiness" ] }

    [<Fact>]
    let ``downstreamLifecycleStages walks the canonical order and is empty past ship`` () =
        Assert.Equal<SddCommand list>([ Analyze; Evidence; Verify; Ship ], downstreamLifecycleStages Tasks)
        Assert.Equal<SddCommand list>([], downstreamLifecycleStages Ship)
        // A cross-cutting command has no lifecycle successor, so no re-run set.
        Assert.Equal<SddCommand list>([], downstreamLifecycleStages Refresh)

    [<Fact>]
    let ``staleTask names the materialized downstream re-run set in order`` () =
        // plan.md was edited after tasks + evidence had run; analyze was never run. tasks is stale,
        // and evidence is the one materialized downstream stage — so "re-run tasks, then evidence".
        let report =
            modelWithPresence
                Tasks
                "demo"
                [ "work/demo/plan.md"; "work/demo/tasks.yml"; "work/demo/evidence.yml" ]
                [ staleTask "work/demo/tasks.yml" [ "T-001" ] ]
            |> buildReport

        let action = report.NextAction.Value
        Assert.Equal("tasks.correctStaleTasks", action.ActionId)
        Assert.Contains("re-run the recorded downstream stages in order: tasks, then evidence", action.Reason)
        // analyze was not materialized, so it is not named.
        Assert.DoesNotContain("analyze", action.Reason)

    [<Fact>]
    let ``staleTask with no materialized downstream stage leaves the base reason unchanged`` () =
        // Only tasks.yml exists; nothing downstream has been run, so there is nothing to reconcile.
        let report =
            modelWithPresence Tasks "demo" [ "work/demo/tasks.yml" ] [ staleTask "work/demo/tasks.yml" [ "T-001" ] ]
            |> buildReport

        let action = report.NextAction.Value
        Assert.Equal("tasks.correctStaleTasks", action.ActionId)
        Assert.DoesNotContain("re-run the recorded downstream stages", action.Reason)

    [<Fact>]
    let ``stalePlanSnapshot names the ordered downstream re-run set after the plan re-baseline`` () =
        // spec/clarify/checklist changed after plan/tasks/evidence had run: re-baselining the plan
        // re-stamps plan.md's digest, staling tasks then evidence. analyze was never run.
        let report =
            modelWithPresence
                Plan
                "demo"
                [ "work/demo/plan.md"; "work/demo/tasks.yml"; "work/demo/evidence.yml" ]
                [ stalePlanSnapshot "work/demo/plan.md" [ "work/demo/spec.md" ] ]
            |> buildReport

        let action = report.NextAction.Value
        Assert.Equal("plan.acceptUpstream", action.ActionId)

        Assert.Contains(
            "re-run the recorded downstream stages in order: plan, then tasks, then evidence",
            action.Reason
        )
