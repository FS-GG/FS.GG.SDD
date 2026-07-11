namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// FS.GG.SDD#351 (epic .github#417 — the SDD lifecycle fails open).
///
/// `plan` scaffolds a decision per requirement — and it carries that requirement's own
/// `[FR-###] [AC-###]` refs BY CONSTRUCTION. So the FR→plan→task→evidence traceability chain used to
/// close with **zero human authorship**: the gates verified that the ids lined up, and the scaffold
/// generated ids that lined up. The check and the thing being checked had the same author.
///
/// The field report drove `charter → specify → clarify → checklist → plan → tasks → analyze` on pure
/// tool-generated boilerplate and every stage went green, `analysisBlocking: 0`.
///
/// These are that walk, committed. Per epic #266's standing note — *an untested failure leg is how
/// this class of defect survives* — the refusal is asserted on the diagnostic id, not on an exit code.
module UnauthoredScaffoldTests =

    let private workId = "030-unauthored-scaffold"
    let private title = "Unauthored Scaffold"

    /// A lifecycle driven to `analyze` with the plan left exactly as `plan` wrote it.
    let private scaffoldOnlyProject () =
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
        // Deliberately NOT `authorPlanProse`. This is the whole point.
        TestSupport.runTasks root workId title |> ignore
        root

    /// SC-001 / FR-001. The acceptance criterion, stated as a test: untouched scaffold blocks.
    [<Fact>]
    let ``analyze blocks a plan that still holds the prose the scaffold wrote`` () =
        let root = scaffoldOnlyProject ()

        let report = TestSupport.runAnalyze root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let diagnostic =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "unauthoredScaffoldContent")

        // It names the entries, so the author knows what to go and write — every scaffolded family,
        // not just the decision: the contract, the obligation, the migration note, the view impact.
        Assert.Contains("PD-001", diagnostic.RelatedIds)
        Assert.Contains("PC-001", diagnostic.RelatedIds)
        Assert.Contains("VO-001", diagnostic.RelatedIds)
        Assert.Contains("PM-001", diagnostic.RelatedIds)
        Assert.Contains("GV-001", diagnostic.RelatedIds)

    /// SC-001 / FR-003. The acceptance criterion in full: it cannot reach `shipReady`.
    ///
    /// One `DiagnosticError` at `analyze` means no `readiness/<id>/analysis.json` is written (H-4:
    /// no handler writes on a blocking run), so `evidence` refuses on its missing prerequisite and
    /// `verify` and `ship` never get a verdict to aggregate. The gate is enforced at one point
    /// rather than re-litigated at four.
    [<Fact>]
    let ``a lifecycle on untouched scaffold cannot reach ship`` () =
        let root = scaffoldOnlyProject ()

        TestSupport.runAnalyze root workId title |> ignore

        Assert.False(
            TestSupport.existsRelative root $"readiness/{workId}/analysis.json",
            "analyze wrote analysis.json over unauthored scaffold content"
        )

        let evidence = TestSupport.runEvidence root workId title
        Assert.Equal(CommandOutcome.Blocked, evidence.Outcome)

        let ship = TestSupport.runShip root workId title
        Assert.Equal(CommandOutcome.Blocked, ship.Outcome)
        Assert.False(TestSupport.existsRelative root $"readiness/{workId}/ship.json")

    /// FR-002. The green path is unaffected: the moment a human writes the decision, it clears.
    /// The id, the refs, and the kind token are untouched — only the prose moves — so this also
    /// proves the gate keys on the *prose*, not on the machine contract around it.
    [<Fact>]
    let ``authoring the plan prose clears the block`` () =
        let root = scaffoldOnlyProject ()

        Assert.Equal(CommandOutcome.Blocked, (TestSupport.runAnalyze root workId title).Outcome)

        TestSupport.authorPlanProse root workId

        let report = TestSupport.runAnalyze root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unauthoredScaffoldContent")
        Assert.True(TestSupport.existsRelative root $"readiness/{workId}/analysis.json")

    /// The gate is conservative, and that is deliberate. It fires ONLY on prose that is byte-identical
    /// to what the scaffold would have written, so a partially-authored plan blocks on exactly the
    /// entries the author has not reached yet — and never on one they have touched.
    [<Fact>]
    let ``a partially authored plan blocks only on the entries still untouched`` () =
        let root = scaffoldOnlyProject ()

        let path = $"work/{workId}/plan.md"

        TestSupport.readRelative root path
        |> fun text ->
            text.Replace(
                "complete: Plan requirement FR-001 through the plan command contract.",
                "complete: Serve direction is derived from the finished rally's loser, so replay and live agree."
            )
        |> TestSupport.writeRelative root path

        let report = TestSupport.runAnalyze root workId title

        let diagnostic =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "unauthoredScaffoldContent")

        Assert.DoesNotContain("PD-001", diagnostic.RelatedIds) // authored — left alone
        Assert.Contains("PC-001", diagnostic.RelatedIds) // still the tool's words

    /// The gate must not key on the entry ID, and this is the failure leg that says so.
    ///
    /// We re-derive from a blank slate, so our ids always count from `PD-001`. `plan` numbers its ids
    /// incrementally (`nextScopedIndex` over the plan that already exists) and appends only for
    /// sources it has not yet covered — so a plan grown over several runs legitimately carries an id
    /// that a fresh derivation would never produce. Insert a requirement above an existing one, re-run
    /// `plan`, and the plan holds `PD-002 [FR-001] …` where we derive `PD-001 [FR-001] …`.
    ///
    /// Compare whole lines and NOTHING matches: no diagnostic, and a plan that is scaffold top to
    /// bottom ships. The gate would fail open in exactly the shape it was built to close, and it would
    /// do it silently — green, not red. So: same prose, different id, still blocked.
    [<Fact>]
    let ``a renumbered entry is still scaffold — the gate does not key on the id`` () =
        let root = scaffoldOnlyProject ()

        let path = $"work/{workId}/plan.md"

        // Exactly what an incremental re-plan leaves behind: the scaffold's refs and the scaffold's
        // prose, under an id a from-scratch derivation would never assign.
        TestSupport.readRelative root path
        |> fun text -> text.Replace("- PD-001 ", "- PD-014 ")
        |> TestSupport.writeRelative root path

        let report = TestSupport.runAnalyze root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let diagnostic =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "unauthoredScaffoldContent")

        // Named by the id the PLAN carries, so the author can actually go and find the entry.
        Assert.Contains("PD-014", diagnostic.RelatedIds)
        Assert.DoesNotContain("PD-001", diagnostic.RelatedIds)
