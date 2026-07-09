namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// FS.GG.SDD#192 drift guard: the shipped worked example under
/// docs/examples/lifecycle-artifacts/ is copied verbatim into a real initialized workspace
/// and DRIVEN THROUGH THE LIFECYCLE STAGES it advertises — analyze -> evidence -> verify -> ship.
///
/// This is the check that was missing. ExampleArtifactsContractTests (Artifacts.Tests) validates
/// the example against the live *parsers*, and it passed the whole time the example was blocking
/// `analyze` on nine `missingDisposition` ids and a `stalePlanSnapshot`. A parser round-trip
/// cannot see a stage gate: `missingDispositionIds` and the plan-snapshot digest comparison both
/// live in FS.GG.SDD.Commands, a layer Artifacts.Tests cannot reach. Hence a second, complementary
/// example contract here — the same split CharterExampleContractTests documents.
///
/// If the example ever stops passing the lifecycle it demonstrates, this fails, so
/// "a complete, valid `tasks.yml` you can copy-adapt" can never again be un-runnable.
module ExampleLifecycleContractTests =

    let private workId = "001-example"
    let private title = "Example Work Item"

    /// The authored artifacts the example ships. `charter.md` is the identity stage;
    /// the rest are the authored sources every later stage reads.
    let private exampleArtifacts =
        [ "charter.md"
          "spec.md"
          "clarifications.md"
          "checklist.md"
          "plan.md"
          "tasks.yml"
          "evidence.yml" ]

    /// Copy the shipped example verbatim into `work/001-example/` of a freshly initialized
    /// workspace. Verbatim matters: the point is to exercise the bytes an author would copy,
    /// not a re-derived equivalent.
    let private exampleWorkspace () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let sourceDir =
            Path.Combine(TestSupport.repoRoot, "docs", "examples", "lifecycle-artifacts")

        for name in exampleArtifacts do
            let text = File.ReadAllText(Path.Combine(sourceDir, name))
            TestSupport.writeRelative root $"work/{workId}/{name}" text

        root

    let private blockingDiagnostics (report: CommandReport) =
        report.Diagnostics
        |> List.filter (fun diagnostic -> diagnostic.Severity = Diagnostics.DiagnosticSeverity.DiagnosticError)

    /// Fails with the offending diagnostic ids rather than a bare boolean, so a regression
    /// names its own cause (`missingDisposition`, `stalePlanSnapshot`, ...).
    ///
    /// Asserting on the diagnostics rather than on `report.Outcome` is deliberate, and it is the
    /// stronger check: `ReportAssembly.outcome` returns `Blocked` exactly when a `DiagnosticError`
    /// is present, so an `Outcome` assertion would be a restatement of this one — while a stage
    /// that emitted an error and still reported success (cf. #191) would slip past it.
    let private assertStagePasses stage (report: CommandReport) =
        match blockingDiagnostics report with
        | [] -> ()
        | blocking ->
            let ids =
                blocking
                |> List.map (fun diagnostic ->
                    let related = diagnostic.RelatedIds |> String.concat ", "
                    $"{diagnostic.Id} [{related}]")
                |> String.concat "; "

            failwith $"Shipped example blocks `fsgg-sdd {stage}`: {ids}"

    [<Fact>]
    let ``Shipped example passes the lifecycle it demonstrates: analyze -> evidence -> verify -> ship`` () =
        let root = exampleWorkspace ()

        assertStagePasses "analyze" (TestSupport.runAnalyze root workId title)
        assertStagePasses "evidence" (TestSupport.runEvidence root workId title)
        assertStagePasses "verify" (TestSupport.runVerify root workId title)
        assertStagePasses "ship" (TestSupport.runShip root workId title)

    /// FS.GG.SDD#211: the example must survive the *generating* stages that precede the gates,
    /// not just the gates themselves. `tasks` re-derives the whole task graph and `plan`
    /// re-authors its decisions on every run; before #211 the shipped example was a fixpoint of
    /// neither — `tasks` renumbered the authored `T001`/`T002`, `evidence.yml`'s `subject.id`
    /// refs then dangled, and `evidence` blocked on `unknownReference` two stages on. Copy
    /// verbatim, run plan -> tasks (the generators) FIRST, then walk the gates, so a regression
    /// that reintroduces the renumber-and-orphan surfaces here rather than in a user's workspace.
    [<Fact>]
    let ``Shipped example survives its own generators: plan -> tasks -> analyze -> evidence -> verify -> ship`` () =
        let root = exampleWorkspace ()

        assertStagePasses "plan" (TestSupport.runPlan root workId title)
        assertStagePasses "tasks" (TestSupport.runTasks root workId title)
        assertStagePasses "analyze" (TestSupport.runAnalyze root workId title)
        assertStagePasses "evidence" (TestSupport.runEvidence root workId title)
        assertStagePasses "verify" (TestSupport.runVerify root workId title)
        assertStagePasses "ship" (TestSupport.runShip root workId title)

    /// The fixpoint property behind the walk above: running each generator over the shipped bytes
    /// rewrites nothing, so it reports `NoChange`. This is the sharper guard — a generator that
    /// re-derived an *equivalent-but-different* graph (renumbered ids, reordered fields) would
    /// still pass the gates above yet fail here, catching the drift at its source.
    [<Fact>]
    let ``Shipped example is a fixpoint of its generators: plan and tasks report NoChange`` () =
        let root = exampleWorkspace ()

        Assert.Equal(CommandOutcome.NoChange, (TestSupport.runPlan root workId title).Outcome)
        Assert.Equal(CommandOutcome.NoChange, (TestSupport.runTasks root workId title).Outcome)

    /// The specific regression #192 filed: nine ids required a task disposition and the example
    /// authored none of them, because `AC-###`/`CR-###`/`GV-###`/`PC-###`/`PD-###`/`PM-###`/`VO-###`
    /// have no typed `tasks.yml` field and are expressible only through `sourceIds:`. Pinned
    /// separately from the walk above so the cause survives if a later stage starts failing.
    [<Fact>]
    let ``Shipped example disposes every required id, so analyze reports no missingDisposition`` () =
        let root = exampleWorkspace ()
        let report = TestSupport.runAnalyze root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingDisposition")

    /// The other half of #192: `plan.md`'s recorded Source Snapshot digests must agree with the
    /// committed spec/clarifications/checklist bytes. Content-digest based, so this is independent
    /// of file mtimes — editing any upstream artifact without re-recording the snapshot fails here.
    [<Fact>]
    let ``Shipped example plan snapshot is current, so analyze reports no stalePlanSnapshot`` () =
        let root = exampleWorkspace ()
        let report = TestSupport.runAnalyze root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "stalePlanSnapshot")
