namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.Json
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// Automated no-Governance lifecycle smoke (US2). Drives the existing command
/// workflow in-process over a disposable project (`init` → … → `ship` plus the
/// cross-cutting `agents` and `refresh` generators) and pins the documented
/// bootstrap experience to real command output. Adds no new public surface — it
/// only exercises the existing `TestSupport` run helpers.
module LifecycleSmokeTests =
    let private workId = "001-bootstrap-smoke"
    let private title = "Bootstrap Smoke"

    let private agentTargets = [ "claude"; "codex" ]

    /// Machine-readable readiness views compared for determinism (FR-006).
    let private readinessViews =
        [ $"readiness/{workId}/work-model.json"
          $"readiness/{workId}/analysis.json"
          $"readiness/{workId}/verify.json"
          $"readiness/{workId}/ship.json" ]

    /// The lifecycle stage commands in canonical order, paired with the report
    /// each emitted. Drives the known-green sequence the quickstart documents.
    type private Driven =
        { Root: string
          Charter: CommandReport
          Specify: CommandReport
          Clarify: CommandReport
          Checklist: CommandReport
          Plan: CommandReport
          Tasks: CommandReport
          Analyze: CommandReport
          Evidence: CommandReport
          Verify: CommandReport
          Ship: CommandReport
          Agents: CommandReport
          Refresh: CommandReport }

    /// A fixed project config so two disposable projects can have byte-identical
    /// authored inputs (init otherwise derives the project id from the random temp
    /// directory name, which would perturb source digests).
    let private pinnedProjectConfig =
        "schemaVersion: 1\n"
        + "project:\n"
        + "  id: bootstrap-smoke-fixed\n"
        + "  defaultWorkRoot: work\n"
        + "sdd:\n"
        + "  config: .fsgg/sdd.yml\n"
        + "  agents: .fsgg/agents.yml\n"

    /// Drive one disposable project through the full lifecycle plus generators.
    /// When [pinConfig] is set, the project config is fixed after init so two runs
    /// share identical inputs for the determinism comparison.
    let private driveLifecycleWith pinConfig =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        if pinConfig then
            TestSupport.writeRelative root ".fsgg/project.yml" pinnedProjectConfig

        let charter = TestSupport.runCharter root workId title
        let specify = TestSupport.runSpecify root workId title
        // specify carried no ambiguity, so clarify runs with no extra intent (the
        // same known-green path TestSupport.initializePlanReadyProject uses).
        let clarify =
            TestSupport.runRequest
                { TestSupport.clarifyRequest root workId title with
                    InputText = None }

        let checklist = TestSupport.runChecklist root workId title
        let plan = TestSupport.runPlan root workId title
        // #351: author the plan before `analyze`. The scaffold's prose now BLOCKS, so a smoke that
        // drove the lifecycle on untouched boilerplate was asserting exactly the defect #351 removes.
        TestSupport.authorPlanProse root workId
        let tasks = TestSupport.runTasks root workId title
        // Authored passing task evidence so analyze/evidence/verify/ship are ready.
        TestSupport.writePassingTaskEvidenceFor root workId
        let analyze = TestSupport.runAnalyze root workId title
        let evidence = TestSupport.runEvidence root workId title
        let verify = TestSupport.runVerify root workId title
        let ship = TestSupport.runShip root workId title
        let agents = TestSupport.runAgents root workId
        let refresh = TestSupport.runRefresh root workId

        { Root = root
          Charter = charter
          Specify = specify
          Clarify = clarify
          Checklist = checklist
          Plan = plan
          Tasks = tasks
          Analyze = analyze
          Evidence = evidence
          Verify = verify
          Ship = ship
          Agents = agents
          Refresh = refresh }

    // The read-only assertions (stage artifacts, next-action chain, no-Governance,
    // well-formed readiness) all observe one full drive; sharing it keeps the
    // in-process smoke well under the <10s budget.
    let private driveLifecycle () = driveLifecycleWith false

    let private driven = lazy (driveLifecycle ())

    let private lifecycleStages (d: Driven) =
        [ Charter, d.Charter
          Specify, d.Specify
          Clarify, d.Clarify
          Checklist, d.Checklist
          Plan, d.Plan
          Tasks, d.Tasks
          Analyze, d.Analyze
          Evidence, d.Evidence
          Verify, d.Verify
          Ship, d.Ship ]

    let private notBlocked label (report: CommandReport) =
        Assert.True(report.Outcome <> CommandOutcome.Blocked, $"Stage {label} was blocked: {report.Diagnostics}")

    let private nextCommand (report: CommandReport) =
        report.NextAction |> Option.bind (fun action -> action.Command)

    // --- T007: happy-path drive — every stage succeeds and writes its source/view ---

    [<Fact>]
    let ``smoke drives init through ship plus agents and refresh without blocking`` () =
        let d = driven.Value

        for (command, report) in lifecycleStages d do
            notBlocked (commandName command) report

        notBlocked "agents" d.Agents
        notBlocked "refresh" d.Refresh

    [<Fact>]
    let ``smoke writes each stage authored source`` () =
        let d = driven.Value

        let authored =
            [ "charter", $"work/{workId}/charter.md"
              "specify", $"work/{workId}/spec.md"
              "clarify", $"work/{workId}/clarifications.md"
              "checklist", $"work/{workId}/checklist.md"
              "plan", $"work/{workId}/plan.md"
              "tasks", $"work/{workId}/tasks.yml"
              "evidence", $"work/{workId}/evidence.yml" ]

        for (stage, path) in authored do
            let absolute = Path.Combine(d.Root, path.Replace('/', Path.DirectorySeparatorChar))
            Assert.True(File.Exists absolute || Directory.Exists absolute, $"Expected {stage} to write {path}.")

    [<Fact>]
    let ``smoke refreshes or reports each generated readiness view`` () =
        let d = driven.Value

        for path in readinessViews do
            Assert.True(TestSupport.existsRelative d.Root path, $"Expected generated readiness view {path}.")

        Assert.True(TestSupport.existsRelative d.Root $"readiness/{workId}/summary.md", "Expected refresh summary.md.")

        for target in agentTargets do
            for file in [ "guidance.json"; "commands.md"; "skills.md" ] do
                Assert.True(
                    TestSupport.existsRelative d.Root $"readiness/{workId}/agent-commands/{target}/{file}",
                    $"Expected agent guidance {target}/{file}."
                )

    // --- T008: canonical order + next-action chain (FR-014, family C) ---

    [<Fact>]
    let ``smoke confirms the canonical lifecycle order map`` () =
        // The authoritative ordering the quickstart documents (FR-014): charter →
        // specify → … → ship, with the two cross-cutting generators terminal.
        let expected =
            [ Charter, Some Specify
              Specify, Some Clarify
              Clarify, Some Checklist
              Checklist, Some Plan
              Plan, Some Tasks
              Tasks, Some Analyze
              Analyze, Some Evidence
              Evidence, Some Verify
              Verify, Some Ship
              Ship, None
              Agents, None
              Refresh, None ]

        for (command, successor) in expected do
            Assert.Equal(successor, nextLifecycleCommand command)

    [<Fact>]
    let ``smoke emits the documented next-action pointer per stage`` () =
        let d = driven.Value
        // Each stage's emitted next-action id, as documented in the quickstart.
        // A behavioral change to any pointer breaks this and forces a doc update.
        let expectedActionIds =
            [ d.Charter, "nextLifecycleCommand"
              d.Specify, "nextLifecycleCommand"
              d.Clarify, "nextLifecycleCommand"
              d.Checklist, "nextLifecycleCommand"
              d.Plan, "nextLifecycleCommand"
              d.Tasks, "nextLifecycleCommand"
              d.Analyze, "analysis.next.implement"
              d.Evidence, "evidence.next.verify"
              d.Verify, "verify.next.ship"
              d.Ship, "ship.next.protectedBoundary"
              d.Agents, "agentsGenerated" ]

        for (report, actionId) in expectedActionIds do
            Assert.Equal(Some actionId, report.NextAction |> Option.map (fun action -> action.ActionId))

        // The lifecycle-advancing stages also carry the next command pointer.
        Assert.Equal(Some Specify, nextCommand d.Charter)
        Assert.Equal(Some Clarify, nextCommand d.Specify)
        Assert.Equal(Some Checklist, nextCommand d.Clarify)
        Assert.Equal(Some Plan, nextCommand d.Checklist)
        Assert.Equal(Some Tasks, nextCommand d.Plan)
        Assert.Equal(Some Analyze, nextCommand d.Tasks)
        // ship and the cross-cutting generators advance to no lifecycle successor.
        Assert.Equal(None, nextCommand d.Ship)
        Assert.Equal(None, nextCommand d.Agents)
        Assert.Equal(None, nextCommand d.Refresh)

    // --- T009: no-Governance (FR-005, family A) + well-formed readiness ---

    [<Fact>]
    let ``smoke completes with no Governance files present or required`` () =
        let d = driven.Value

        for path in [ ".fsgg/policy.yml"; ".fsgg/capabilities.yml"; ".fsgg/tooling.yml" ] do
            Assert.False(
                TestSupport.existsRelative d.Root path,
                $"Governance file {path} must not be required or created."
            )
        // No stage evaluated Governance routing/profiles/freshness/gates/audit.
        for (_, report) in lifecycleStages d do
            Assert.All(report.GovernanceCompatibility, (fun fact -> Assert.Equal("notEvaluated", fact.State)))

    [<Fact>]
    let ``smoke readiness views parse and carry a generation manifest`` () =
        let d = driven.Value
        // Each machine-readable readiness view parses and records its sources (with
        // source digests), schema version, and generator identity (the work model
        // carries a modelVersion; analysis/verify/ship carry a generator string).
        for path in readinessViews do
            use parsed = JsonDocument.Parse(TestSupport.readRelative d.Root path)
            let root = parsed.RootElement
            let has (name: string) = root.TryGetProperty name |> fst
            Assert.True(has "schemaVersion", $"{path} missing schemaVersion.")
            Assert.True(has "sources", $"{path} missing sources.")
            let sources = root.GetProperty "sources"
            Assert.True(sources.GetArrayLength() > 0, $"{path} has empty sources.")

            for source in sources.EnumerateArray() do
                Assert.True(source.TryGetProperty "path" |> fst, $"{path} source missing path.")

                let hasDigest =
                    (source.TryGetProperty "digest" |> fst)
                    || (source.TryGetProperty "sourceDigest" |> fst)

                Assert.True(hasDigest, $"{path} source missing digest.")

            Assert.True((has "generator") || (has "modelVersion"), $"{path} missing generator identity.")

    [<Fact>]
    let ``smoke generated readiness views record sources and generator identity in the report`` () =
        let d = driven.Value
        // The originating stage report records each generated view's sources
        // and generator identity.
        let viewReports =
            [ $"readiness/{workId}/analysis.json", d.Analyze
              $"readiness/{workId}/verify.json", d.Verify
              $"readiness/{workId}/ship.json", d.Ship ]

        for (path, report) in viewReports do
            match report.GeneratedViews |> List.tryFind (fun v -> v.Path = path) with
            | Some v ->
                Assert.NotEmpty v.Sources
                Assert.True(v.Generator.IsSome, $"{path} generated view missing generator identity.")
            | None -> failwith $"Expected a generated-view entry for {path} in the {report.Command} report."

    [<Fact>]
    let ``smoke summary is marked generated and records its sources`` () =
        let d = driven.Value
        let summaryPath = $"readiness/{workId}/summary.md"
        let summaryText = TestSupport.readRelative d.Root summaryPath
        Assert.Contains("GENERATED by fsgg-sdd refresh", summaryText)
        Assert.Contains("DO NOT EDIT", summaryText)

        let summaryView =
            d.Refresh.GeneratedViews |> List.tryFind (fun v -> v.Path = summaryPath)

        match summaryView with
        | Some v -> Assert.NotEmpty v.Sources
        | None -> failwith "Expected a refresh summary generated-view entry."

    // --- T010: determinism (FR-006, family B) ---

    [<Fact>]
    let ``smoke yields byte-identical readiness across two runs over identical inputs`` () =
        let normalize (root: string) (text: string) =
            // Exclude the absolute host path and the random temp-derived project id
            // (the lowercased temp directory name) from the compared output.
            let projectId = (DirectoryInfo root).Name.ToLowerInvariant()
            text.Replace(root, "<ROOT>").Replace(root.Replace('\\', '/'), "<ROOT>").Replace(projectId, "<ID>")

        let first = driveLifecycleWith true
        let second = driveLifecycleWith true

        for path in readinessViews do
            let a = normalize first.Root (TestSupport.readRelative first.Root path)
            let b = normalize second.Root (TestSupport.readRelative second.Root path)
            Assert.Equal(a, b)

        // The refresh cross-view report (the serialized refresh report) is also
        // deterministic after path-normalizing the temp root.
        let firstRefresh = normalize first.Root (serializeReport first.Refresh)
        let secondRefresh = normalize second.Root (serializeReport second.Refresh)
        Assert.Equal(firstRefresh, secondRefresh)

    // --- T011: Governance present-but-incomplete (family D) + no-Rendering (family E) ---

    [<Fact>]
    let ``smoke stays usable with present-but-incomplete Governance files`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        // Deliberately incomplete Governance files: SDD never parses or enforces them.
        TestSupport.writeRelative root ".fsgg/policy.yml" "# incomplete governance policy\n"
        TestSupport.writeRelative root ".fsgg/capabilities.yml" "# incomplete capabilities\n"
        TestSupport.writeRelative root ".fsgg/tooling.yml" "# incomplete tooling\n"

        let reports =
            [ "charter", TestSupport.runCharter root workId title
              "specify", TestSupport.runSpecify root workId title ]

        let clarify =
            TestSupport.runRequest
                { TestSupport.clarifyRequest root workId title with
                    InputText = None }

        let reports =
            reports
            @ [ "clarify", clarify
                "checklist", TestSupport.runChecklist root workId title
                "plan",
                (let report = TestSupport.runPlan root workId title in
                 TestSupport.authorPlanProse root workId // #351
                 report)
                "tasks", TestSupport.runTasks root workId title ]

        TestSupport.writePassingTaskEvidenceFor root workId

        let reports =
            reports
            @ [ "analyze", TestSupport.runAnalyze root workId title
                "evidence", TestSupport.runEvidence root workId title
                "verify", TestSupport.runVerify root workId title
                "ship", TestSupport.runShip root workId title
                "agents", TestSupport.runAgents root workId
                "refresh", TestSupport.runRefresh root workId ]

        // Every command stays usable and performs no Governance evaluation/enforcement.
        for (label, report) in reports do
            notBlocked label report
            let json = serializeReport report

            for enforcementField in
                [ "\"route\""
                  "\"profile\""
                  "\"freshness\""
                  "\"gate\""
                  "\"audit\""
                  "\"protectedBranch\"" ] do
                Assert.DoesNotContain(enforcementField, json)

    [<Fact>]
    let ``smoke depends only on the SDD projects with no Rendering or monorepo`` () =
        let d = driven.Value
        // The run referenced no FS.GG.Rendering package, runtime product template,
        // or monorepo checkout: nothing of the sort is written or named in output.
        let names =
            Directory.GetFileSystemEntries d.Root
            |> Array.map (fun entry ->
                (Path.GetFileName entry |> Option.ofObj |> Option.defaultValue "").ToLowerInvariant())

        for name in names do
            Assert.DoesNotContain("rendering", name)
            Assert.DoesNotContain("template", name)

        for (_, report) in lifecycleStages d do
            let json = serializeReport report
            Assert.DoesNotContain("FS.GG.Rendering", json)
            Assert.DoesNotContain("monorepo", json)
