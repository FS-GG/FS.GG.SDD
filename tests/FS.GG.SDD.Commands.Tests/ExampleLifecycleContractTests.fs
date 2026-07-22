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

    /// FS.GG.SDD#310 (AC9) as narrowed by #649, then #646 — the fold direction. One accepted deferral
    /// (DEC-002) collapses into ONE keep-visible obligation; everything that merely echoes it folds in:
    ///   * `PD-001`→FR-001 and `PD-002`→FR-002 mirror a requirement's own refs, so they fold into the
    ///     requirement task (the original #310 fold).
    ///   * `PD-003`→DEC-002 is the plan scaffold's PURE deferral mirror (#649) and folds into DEC-002's
    ///     keep-visible task instead of earning its own `Implement plan decision` obligation.
    ///   * `CR-003`→DEC-002 is the checklist scaffold's PURE deferral echo (#646) and folds into the
    ///     SAME keep-visible task rather than earning a second keep-visible obligation.
    ///   * `PD-004`→CR-003 is the plan's pure mirror of that echo and folds transitively into it too.
    /// So the four echoes of one deferral land in one task, `sourceIds: [CR-003, DEC-002, PD-003, PD-004]`
    /// — one obligation per deferral, not four.
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

        // Pure deferral echoes (PD-003, CR-003, PD-004) all fold into DEC-002's ONE keep-visible task;
        // none earns an obligation of its own.
        Assert.DoesNotContain("Implement plan decision PD-003", tasks)
        Assert.DoesNotContain("Implement plan decision PD-004", tasks)
        Assert.DoesNotContain("Keep accepted deferral CR-003 visible", tasks)
        Assert.Contains("Keep accepted deferral DEC-002 visible", tasks)
        Assert.Contains("sourceIds: [CR-003, DEC-002, PD-003, PD-004]", tasks)

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

        // Real design content, not boilerplate: PD-003 keeps its own obligation and does NOT fold into
        // DEC-002's keep-visible task. DEC-002's task still folds in the checklist echo CR-003 and its
        // pure mirror PD-004 (#646) — that fold is unaffected — but NOT the now-real PD-003. (When PD-003
        // IS folded, as in the shipped example, the keep-visible task carries `[CR-003, DEC-002, PD-003,
        // PD-004]`; here PD-003 is absent from it.)
        Assert.Contains("Implement plan decision PD-003", tasks)
        Assert.Contains("sourceIds: [CR-003, DEC-002, PD-004]", tasks)

    /// FS.GG.SDD#646 (the residual #649 left): the `checklist` scaffold echoes every accepted CLARIFY
    /// deferral as one `CR-### [DEC-###] acceptedDeferral: …` review result. That echo keeps visible
    /// nothing the clarify deferral's own keep-visible task does not, so it FOLDS into that task instead
    /// of fanning a single deferral out into a second keep-visible obligation.
    ///
    /// Driven through the REAL generators end to end (checklist -> plan -> tasks in lifecycle order, so no
    /// upstream snapshot goes stale) — which is also what pins the fold against drift in the checklist
    /// scaffold. The shipped-example fold-walk above proves the same on the copied corpus; this proves it
    /// on freshly generated artifacts.
    [<Fact>]
    let ``A pure checklist deferral echo folds into its clarify deferral`` () =
        let root = exampleWorkspace ()

        for name in [ "checklist.md"; "plan.md"; "tasks.yml" ] do
            File.Delete(Path.Combine(root, "work", workId, name))

        TestSupport.runChecklist root workId title |> ignore

        // The native checklist carries the scaffold's pure deferral echo for DEC-002.
        let checklist = TestSupport.readRelative root $"work/{workId}/checklist.md"
        Assert.Contains("remains visible to planning", checklist)

        TestSupport.runPlan root workId title |> ignore
        TestSupport.authorPlanProse root workId

        let report = TestSupport.runTasks root workId title

        let ids =
            report.Diagnostics |> List.map (fun d -> $"{d.Severity}:{d.Id}") |> String.concat ", "

        Assert.True(
            File.Exists(Path.Combine(root, "work", workId, "tasks.yml")),
            $"tasks.yml not written; diagnostics: [{ids}]"
        )

        let tasks = TestSupport.readRelative root $"work/{workId}/tasks.yml"

        // The CR echo gets no keep-visible task of its own; it folds — with its own pure PD mirror — into
        // DEC-002's keep-visible task. One obligation for the deferral, not two.
        Assert.DoesNotContain("Keep accepted deferral CR-", tasks)
        Assert.Contains("Keep accepted deferral DEC-002 visible", tasks)
        Assert.Matches("sourceIds: \\[CR-\\d+, DEC-002, PD-\\d+, PD-\\d+\\]", tasks)

    /// FS.GG.SDD#646 over-collapse guard, the structural analogue of #649's prose guard. A checklist
    /// deferral echo folds because every one of its refs is an accepted clarify deferral. A checklist
    /// review that ALSO references a non-deferral id (here a requirement, FR-001) is not a pure echo —
    /// it disposes something the deferral's keep-visible task does not — so it KEEPS its own obligation
    /// and does not fold. The fold decision is drawn on the refs, which for a `CR` are dispositive (a
    /// checklist deferral review has no author-editable body that could carry a separable decision).
    [<Fact>]
    let ``A checklist deferral that also refs a requirement is not folded`` () =
        let root = exampleWorkspace ()

        let checklistPath = $"work/{workId}/checklist.md"

        // Add a requirement ref to CR-003's refs, leaving its accepted-deferral status. Its ref set is no
        // longer a subset of the accepted clarify deferrals, so it is not a pure echo.
        let rewritten =
            (TestSupport.readRelative root checklistPath)
                .Replace("[CHK:CHK-002] [DEC-002] acceptedDeferral:", "[CHK:CHK-002] [DEC-002] [FR-001] acceptedDeferral:")

        Assert.Contains("[DEC-002] [FR-001] acceptedDeferral:", rewritten) // the edit landed
        TestSupport.writeRelative root checklistPath rewritten

        // Editing the checklist staleifies plan.md's checklist snapshot; regenerate plan (and tasks)
        // fresh so the snapshot re-records against the edited checklist and the derivation decides anew.
        File.Delete(Path.Combine(root, "work", workId, "plan.md"))
        File.Delete(Path.Combine(root, "work", workId, "tasks.yml"))
        TestSupport.runPlan root workId title |> ignore
        TestSupport.authorPlanProse root workId
        TestSupport.runTasks root workId title |> ignore
        let tasks = TestSupport.readRelative root $"work/{workId}/tasks.yml"

        // Not a pure echo: CR-003 keeps its own keep-visible task rather than folding into DEC-002's.
        Assert.Contains("Keep accepted deferral CR-003 visible", tasks)

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
