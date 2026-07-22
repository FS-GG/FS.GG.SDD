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

    /// The proving tests the example's evidence CITES. They are staged at the workspace root
    /// (`tests/ExampleApp.Tests/…`), not under `work/<id>/`, because that is where the evidence
    /// declarations point.
    ///
    /// FS.GG.SDD#349: before the existence check, the example cited all six of these and shipped
    /// none of them — the corpus this product publishes to teach evidence authoring was itself
    /// citing files that do not exist, and passed. Shipping them is what makes the example honest;
    /// staging them here is what lets the gate see them.
    let private exampleProvingTests =
        [ "ServeRuleTests.fs"
          "RallyScoreTests.fs"
          "CommandReportContractTests.fs"
          "CommandSmokeTests.fs"
          "SchemaVersionTests.fs"
          "WorkModelViewTests.fs" ]

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

        for name in exampleProvingTests do
            let text =
                File.ReadAllText(Path.Combine(sourceDir, "tests", "ExampleApp.Tests", name))

            TestSupport.writeRelative root $"tests/ExampleApp.Tests/{name}" text

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
    ///
    /// FS.GG.SDD#265: the very first `plan` over a *bare* copy legitimately *creates* the derived
    /// work model (`readiness/<id>/work-model.json`) and so reports `Succeeded`, not `NoChange`.
    /// Before #265 the example's authored `**DEC**` decisions never parsed, the work model was
    /// blocking, and `plan` took its no-write arm — reporting `NoChange` for the wrong reason (the
    /// silent-generation bug this ADR closes). Bring the derived views to steady state first, then
    /// assert the generators rewrite nothing: the *authored* plan.md/tasks.yml are the fixpoint, and
    /// a generator that re-derived an equivalent-but-different graph still surfaces here as a
    /// non-`NoChange` re-run.
    [<Fact>]
    let ``Shipped example is a fixpoint of its generators: plan and tasks report NoChange`` () =
        let root = exampleWorkspace ()

        // Settle the derived work model (first `plan` creates it; `tasks` then sees it current).
        TestSupport.runPlan root workId title |> ignore
        TestSupport.runTasks root workId title |> ignore

        Assert.Equal(CommandOutcome.NoChange, (TestSupport.runPlan root workId title).Outcome)
        Assert.Equal(CommandOutcome.NoChange, (TestSupport.runTasks root workId title).Outcome)

    /// FS.GG.SDD#310 (AC9) as narrowed by FS.GG.SDD#649 — the fold direction. The example's plan
    /// carries four `PD-###`, and `tasks` folds all four, each into the one obligation that already
    /// disposes it:
    ///   * `PD-001`→FR-001 and `PD-002`→FR-002 mirror a requirement's own refs, so they fold into the
    ///     requirement task (the original #310 fold).
    ///   * `PD-003`→DEC-002 and `PD-004`→CR-003 are the plan scaffold's PURE deferral mirrors
    ///     (`acceptedDeferral: Accepted deferral DEC-### remains visible to task generation.`) — every
    ///     ref an accepted-deferral id, text the fixed boilerplate — so each folds into that deferral's
    ///     keep-visible task instead of earning a second `Implement plan decision PD-###` obligation
    ///     (#649). One obligation per deferral, not two.
    [<Fact>]
    let ``Shipped example folds its FR-mirror and pure deferral-mirror PDs into their keep tasks`` () =
        let root = exampleWorkspace ()
        TestSupport.runTasks root workId title |> ignore
        let tasks = TestSupport.readRelative root $"work/{workId}/tasks.yml"

        // FR-mirror PDs: folded into the requirement task that subsumes them, no task of their own.
        Assert.DoesNotContain("Implement plan decision PD-001", tasks)
        Assert.DoesNotContain("Implement plan decision PD-002", tasks)
        Assert.Contains("sourceIds: [AC-001, FR-001, PD-001]", tasks)
        Assert.Contains("sourceIds: [AC-002, FR-002, PD-002]", tasks)

        // Pure deferral mirrors: each folded into its deferral's keep-visible task, no task of its own.
        Assert.DoesNotContain("Implement plan decision PD-003", tasks)
        Assert.DoesNotContain("Implement plan decision PD-004", tasks)
        Assert.Contains("Keep accepted deferral DEC-002 visible", tasks)
        Assert.Contains("sourceIds: [DEC-002, PD-003]", tasks)
        Assert.Contains("Keep accepted deferral CR-003 visible", tasks)
        Assert.Contains("sourceIds: [CR-003, PD-004]", tasks)

    /// FS.GG.SDD#310 (AC9), the protective direction #649 must NOT weaken — the over-collapse guard.
    /// It is the dangerous direction every other test misses: a predicate that folded on the deferral
    /// ref alone would satisfy "no duplicate PD-003 task" while silently discarding a real design
    /// decision.
    ///
    /// Take the shipped example's PD-003 — a pure boilerplate mirror that folds — and rewrite ONLY its
    /// prose into a real design decision, leaving its `[DEC-002]` ref untouched. Same accepted-deferral
    /// ref, opposite fate: because the text is no longer the fixed keep-visible boilerplate, it is no
    /// longer a pure mirror, so `tasks` leaves it its own `Implement plan decision PD-003` obligation
    /// and does NOT fold it into `DEC-002`'s keep-visible task. The pure-vs-authored line is exactly
    /// what #649 draws, and it is drawn on the prose, not the refs.
    [<Fact>]
    let ``A deferral PD rewritten with real design content is not folded`` () =
        let root = exampleWorkspace ()

        let planPath = $"work/{workId}/plan.md"

        let rewritten =
            (TestSupport.readRelative root planPath)
                .Replace(
                    "- PD-003 [DEC-002] acceptedDeferral: Accepted deferral DEC-002 remains visible to task generation.",
                    "- PD-003 [DEC-002] acceptedDeferral: Defer the match-end/win condition deliberately; the rally rules are provable without it and inventing one now would fix a scoring model we have not played against yet."
                )

        Assert.DoesNotContain("Accepted deferral DEC-002 remains visible to task generation.", rewritten) // the replace actually landed

        TestSupport.writeRelative root planPath rewritten

        // Derive fresh (no prior tasks.yml) so the merge cannot re-add the folded ref from the shipped,
        // pre-folded graph — the derivation, not the merge, must decide this.
        File.Delete(Path.Combine(root, "work", workId, "tasks.yml"))
        TestSupport.runTasks root workId title |> ignore
        let tasks = TestSupport.readRelative root $"work/{workId}/tasks.yml"

        // Real design content, not boilerplate: PD-003 keeps its own obligation, and DEC-002's
        // keep-visible task disposes DEC-002 ALONE — the fold did not happen. (When PD-003 IS folded,
        // as in the shipped example, no task carries the bare `sourceIds: [DEC-002]`; the keep-visible
        // task carries `[DEC-002, PD-003]` instead.)
        Assert.Contains("Implement plan decision PD-003", tasks)
        Assert.Contains("sourceIds: [DEC-002]", tasks)

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
