namespace FS.GG.SDD.Commands.Tests

open System
open System.Text.Json
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

/// FS.GG.SDD#869. Adding a requirement to a package that already has `evidence.yml` used to
/// deadlock the lifecycle: `refresh` would not derive the work model until `evidence` had run,
/// `evidence` would not run until `analyze` was `implementationReady`, and `analyze` could not be
/// `implementationReady` while the work model would not derive. No ordering of the documented
/// commands escaped it, and the diagnostic that reported it accused `spec.md` — a hard-coded
/// placeholder — sending authors to re-lint an artifact `lint` had already reported clean.
///
/// The deadlock had TWO locks, which is the thing the report's either/or framing missed and the
/// thing `deadlock is opened by BOTH fixes` below measures directly:
///
///   1. the work model refused to derive from an `evidence.yml` that was merely INCOMPLETE, and
///   2. `evidence` refused to seed a declaration beside already-authored ones.
///
/// Opening either alone leaves the package stuck one step further along.
module RefreshEvidenceDeadlockTests =
    let private workId = "869-deadlock-fixture"
    let private title = "Deadlock Fixture"

    // ---------- helpers ----------

    /// The parsed `diagnostics` array of a generated view, as `(id, artifact, relatedIds)`.
    let private viewDiagnostics root relativePath =
        use document = JsonDocument.Parse(TestSupport.readRelative root relativePath)

        match document.RootElement.TryGetProperty "diagnostics" with
        | true, diagnostics ->
            diagnostics.EnumerateArray()
            |> Seq.map (fun (entry: JsonElement) ->
                let str (name: string) =
                    match entry.TryGetProperty name with
                    | true, value when value.ValueKind = JsonValueKind.String -> string (value.GetString())
                    | _ -> ""

                let related =
                    match entry.TryGetProperty "relatedIds" with
                    | true, value when value.ValueKind = JsonValueKind.Array ->
                        value.EnumerateArray()
                        |> Seq.map (fun (item: JsonElement) -> string (item.GetString()))
                        |> List.ofSeq
                    | _ -> []

                str "id", str "artifact", related)
            |> List.ofSeq
        | _ -> []

    let private diagnosticIds (report: CommandReport) = report.Diagnostics |> List.map _.Id

    let private relatedIdsOf (report: CommandReport) id =
        report.Diagnostics
        |> List.filter (fun diagnostic -> diagnostic.Id = id)
        |> List.collect _.RelatedIds

    /// A package whose authored sources are all present and whose `tasks.yml` requires
    /// [evidenceId], with an `evidence.yml` that declares nothing. This is the shape a package
    /// takes the instant a requirement is added to it: not absent, not malformed — INCOMPLETE.
    let private projectRequiring evidenceId =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeValidWorkSources root workId title

        TestSupport.writeRelative
            root
            $"work/{workId}/tasks.yml"
            ("schemaVersion: 1\n"
             + "tasks:\n"
             + "  - id: T001\n"
             + "    title: Implement selected lifecycle work\n"
             + "    status: pending\n"
             + "    owner: sdd\n"
             + "    dependencies: []\n"
             + "    requirements: [FR-001]\n"
             + "    decisions: []\n"
             + "    requiredSkills: []\n"
             + $"    requiredEvidence: [{evidenceId}]\n")

        root

    // ---------- FR-001 / AC-001: the incomplete case derives, and says what is missing ----------

    [<Fact>]
    let ``an evidence id no declaration names derives the work model and blames evidence.yml`` () =
        let root = projectRequiring "EV001"

        let report = TestSupport.runRefresh root workId

        // The load-bearing half: the view EXISTS. Before this change the generator planned no
        // write at all, and every downstream view went `blocked` behind it.
        Assert.True(
            TestSupport.existsRelative root $"readiness/{workId}/work-model.json",
            $"work-model.json was not written; refresh reported {diagnosticIds report}"
        )

        let undeclared =
            viewDiagnostics root $"readiness/{workId}/work-model.json"
            |> List.filter (fun (id, _, _) -> id = "undeclaredEvidenceObligation")

        match undeclared with
        | [ (_, artifact, related) ] ->
            // The artifact is where the failure LIVES (the file that must gain a declaration),
            // and `relatedIds` keeps where it was DETECTED. Losing either is `.github#266`.
            Assert.Equal($"work/{workId}/evidence.yml", artifact)
            Assert.Contains("EV001", related)
            Assert.Contains($"work/{workId}/tasks.yml", related)
        | other -> failwith $"Expected exactly one undeclaredEvidenceObligation, got {other}"

    [<Fact>]
    let ``an incomplete evidence.yml is not reported as a malformed or missing source`` () =
        let root = projectRequiring "EV001"

        let report = TestSupport.runRefresh root workId
        let ids = diagnosticIds report

        // Non-vacuity: three `DoesNotContain`s pass just as happily against a run that produced
        // nothing at all, so pin that this refresh genuinely did the work first.
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        Assert.True(
            TestSupport.existsRelative root $"readiness/{workId}/work-model.json",
            "Nothing was derived, so the absence of these diagnostics proves nothing."
        )

        Assert.DoesNotContain("refresh.malformedSource", ids)
        Assert.DoesNotContain("refresh.missingSource", ids)
        Assert.DoesNotContain("refresh.blockedUpstreamView", ids)

    // ---------- FR-002 / AC-002: the three UPSTREAM edges still block ----------

    /// The boundary this work draws is the DIRECTION of the reference, not the identity of the
    /// artifact. `requiredEvidence` points downstream at a file a later stage authors;
    /// `requirements`, `decisions` and `dependencies` point upstream at files that already exist,
    /// and an unresolved reference there has no later stage to close it. Without this test the
    /// change is indistinguishable from a blanket demotion of `unknownReference`.
    [<Theory>]
    [<InlineData("requirements", "FR-404")>]
    [<InlineData("decisions", "DEC-404")>]
    [<InlineData("dependencies", "T404")>]
    let ``an unresolved upstream reference still blocks the work model`` (field: string) (value: string) =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeValidWorkSources root workId title

        let line name =
            if name = field then $"[{value}]" else "[]"

        let dependencies = line "dependencies"
        let requirements = line "requirements"
        let decisions = line "decisions"

        TestSupport.writeRelative
            root
            $"work/{workId}/tasks.yml"
            ("schemaVersion: 1\n"
             + "tasks:\n"
             + "  - id: T001\n"
             + "    title: Implement selected lifecycle work\n"
             + "    status: pending\n"
             + "    owner: sdd\n"
             + $"    dependencies: {dependencies}\n"
             + $"    requirements: {requirements}\n"
             + $"    decisions: {decisions}\n"
             + "    requiredSkills: []\n"
             + "    requiredEvidence: []\n")

        let report = TestSupport.runRefresh root workId

        Assert.False(
            TestSupport.existsRelative root $"readiness/{workId}/work-model.json",
            $"An unresolved {field} reference must still refuse to derive the work model."
        )

        Assert.Contains("refresh.malformedSource", diagnosticIds report)

    // ---------- FR-004 / AC-004: the accusation is the source the diagnostics name ----------

    [<Fact>]
    let ``a blocked work model is blamed on the source its diagnostics name, never on spec.md`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeValidWorkSources root workId title

        // The unresolved reference lives in tasks.yml. The specification is untouched and clean.
        TestSupport.writeRelative
            root
            $"work/{workId}/tasks.yml"
            ("schemaVersion: 1\n"
             + "tasks:\n"
             + "  - id: T001\n"
             + "    title: Implement selected lifecycle work\n"
             + "    status: pending\n"
             + "    owner: sdd\n"
             + "    dependencies: []\n"
             + "    requirements: [FR-404]\n"
             + "    decisions: []\n"
             + "    requiredSkills: []\n"
             + "    requiredEvidence: []\n")

        let report = TestSupport.runRefresh root workId
        let blamed = relatedIdsOf report "refresh.malformedSource"

        Assert.Contains($"work/{workId}/tasks.yml", blamed)
        Assert.DoesNotContain($"work/{workId}/spec.md", blamed)

    // ---------- FR-004 / FR-005: every arm of the attribution rule ----------

    /// `blockedWorkModelAttribution` is exercised directly because one of its arms is not
    /// reachable from any lifecycle input: every work-model diagnostic carries its artifact, so
    /// `blockingSources` is never empty in production today. That arm is fail-safe against a
    /// future diagnostic that carries none, and it is precisely the arm that must never silently
    /// become "blame the spec" again — so it is measured here with real inputs rather than left
    /// unmeasured because production cannot currently produce it.
    [<Fact>]
    let ``attribution prefers an absent source over what a present one says`` () =
        let diagnostics =
            HandlersRefresh.blockedWorkModelAttribution
                "readiness/x/work-model.json"
                [ "work/x/tasks.yml" ]
                [ "work/x/spec.md" ]

        Assert.Equal<string list>([ "refresh.missingSource" ], diagnostics |> List.map _.Id)
        Assert.Contains("work/x/tasks.yml", diagnostics |> List.collect _.RelatedIds)

    [<Fact>]
    let ``attribution names every source the blocking diagnostics name`` () =
        let diagnostics =
            HandlersRefresh.blockedWorkModelAttribution
                "readiness/x/work-model.json"
                []
                [ "work/x/evidence.yml"; "work/x/tasks.yml" ]

        Assert.Equal<string list>(
            [ "refresh.malformedSource"; "refresh.malformedSource" ],
            diagnostics |> List.map _.Id
        )

        let blamed = diagnostics |> List.collect _.RelatedIds
        Assert.Contains("work/x/evidence.yml", blamed)
        Assert.Contains("work/x/tasks.yml", blamed)
        Assert.DoesNotContain("work/x/spec.md", blamed)

    [<Fact>]
    let ``attribution reports that it could not attribute rather than naming an arbitrary source`` () =
        let diagnostics =
            HandlersRefresh.blockedWorkModelAttribution "readiness/x/work-model.json" [] []

        Assert.Equal<string list>([ "refresh.unattributedBlockedView" ], diagnostics |> List.map _.Id)

        // The point of the arm: no declared source is named at all.
        Assert.DoesNotContain("work/x/spec.md", diagnostics |> List.collect _.RelatedIds)

    // ---------- FR-003 / AC-003: nothing was traded away ----------

    /// DEC-001 RELOCATES the undeclared-obligation check; an unwitnessed relocation is a deletion.
    /// `verify` must still refuse an obligation that carries no declaration, and must still name
    /// it, even though the work model now derives happily past the same fact.
    [<Fact>]
    let ``an obligation nothing declares still stops verify`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeTasksReadyProject root workId title
        TestSupport.writeRelative root $"work/{workId}/evidence.yml" "schemaVersion: 1\nevidence: []\n"

        let report = TestSupport.runVerify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

    // ---------- FR-006 / AC-006: the documented sequence recovers the package ----------

    /// Drive a package to a state that HAS an `evidence.yml`, add one requirement, and then run
    /// only documented commands. No hand-edit of `evidence.yml`, no deletion of an authored
    /// artifact, no flag that does not appear in the tool's own NextAction.
    let private driveThenAmend () =
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
        TestSupport.authorPlanProse root workId
        TestSupport.runTasks root workId title |> ignore
        TestSupport.writePassingTaskEvidenceFor root workId
        TestSupport.runAnalyze root workId title |> ignore
        TestSupport.runEvidence root workId title |> ignore
        TestSupport.runVerify root workId title |> ignore
        TestSupport.runShip root workId title |> ignore

        // The amendment: one more requirement and its acceptance scenario, authored into the
        // specification's own sections exactly as an author would write them — the load-bearing
        // coverage-line grammar, on one physical line each.
        let spec = TestSupport.readRelative root $"work/{workId}/spec.md"

        let amended =
            spec
                .Replace(
                    "## Functional Requirements\n",
                    "## Functional Requirements\n"
                    + "- FR-900: One requirement added after evidence.yml already existed. (Stories: US-001; Acceptance: AC-900)\n"
                )
                .Replace(
                    "## Acceptance Scenarios\n",
                    "## Acceptance Scenarios\n"
                    + "- AC-900 [US-001] [FR-900]: Given a package that already has evidence.yml, when a requirement is added, then the documented commands recover it.\n"
                )

        // The amendment must actually land, or every assertion below would pass vacuously against
        // a package nothing was added to.
        Assert.NotEqual<string>(spec, amended)
        TestSupport.writeRelative root $"work/{workId}/spec.md" amended

        root

    /// Every command below is documented, and each is the one the previous command's NextAction
    /// points at. `--accept-upstream` is the remedy `plan.acceptUpstream` names by id.
    let private runDocumentedRecovery root =
        TestSupport.runChecklist root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.planRequest root workId title with
                AcceptUpstream = true }
        |> ignore

        TestSupport.authorPlanProse root workId
        TestSupport.runTasks root workId title |> ignore
        let refresh = TestSupport.runRefresh root workId
        let analyze = TestSupport.runAnalyze root workId title
        let evidence = TestSupport.runEvidence root workId title
        refresh, analyze, evidence

    [<Fact>]
    let ``a requirement added after evidence.yml exists is recovered by documented commands alone`` () =
        let root = driveThenAmend ()
        let before = TestSupport.readRelative root $"work/{workId}/evidence.yml"

        let refresh, analyze, evidence = runDocumentedRecovery root

        Assert.NotEqual(CommandOutcome.Blocked, refresh.Outcome)
        Assert.NotEqual(CommandOutcome.Blocked, evidence.Outcome)

        match analyze.Analysis with
        | Some summary -> Assert.Equal("implementationReady", summary.Readiness)
        | None -> failwith "Expected an analysis summary."

        // `evidence` — not the author, and not this test — wrote the new declaration.
        let after = TestSupport.readRelative root $"work/{workId}/evidence.yml"
        Assert.NotEqual<string>(before, after)
        Assert.Contains("evidence.seededObligations", diagnosticIds evidence)

    /// The report was filed as an either/or. It is not one: each fix leaves the package stuck one
    /// step further along, and only both together converge. This test pins the CONJUNCTION by
    /// asserting the two distinct signals that only appear when both locks are open — the work
    /// model derived (lock 1) and `evidence` seeded into an authored file (lock 2). Restore either
    /// lock and exactly one of these assertions goes red.
    [<Fact>]
    let ``the deadlock is opened by BOTH fixes and neither is redundant`` () =
        let root = driveThenAmend ()
        let _, analyze, evidence = runDocumentedRecovery root

        // Lock 1 open: analyze got a derivable work model out of an incomplete evidence.yml.
        Assert.True(
            TestSupport.existsRelative root $"readiness/{workId}/work-model.json",
            "Lock 1 is shut: the work model still refuses to derive from an incomplete evidence.yml."
        )

        match analyze.Analysis with
        | Some summary -> Assert.Equal("implementationReady", summary.Readiness)
        | None -> failwith "Expected an analysis summary."

        // Lock 2 open: evidence added a declaration beside already-authored ones.
        Assert.Contains("evidence.seededObligations", diagnosticIds evidence)
        Assert.DoesNotContain("evidence.analysisNotReady", diagnosticIds evidence)
        Assert.DoesNotContain("evidence.missingRequiredEvidence", diagnosticIds evidence)

    /// A seeded declaration claims nothing. Seeding must make the gap AUTHORABLE, never
    /// verification-ready — otherwise the fix would have bought convergence by weakening verify.
    [<Fact>]
    let ``a seeded declaration is missing, so it cannot make an obligation verification-ready`` () =
        let root = driveThenAmend ()
        runDocumentedRecovery root |> ignore

        let evidenceYml = TestSupport.readRelative root $"work/{workId}/evidence.yml"
        Assert.Contains("result: missing", evidenceYml, StringComparison.Ordinal)

        let verify = TestSupport.runVerify root workId title
        Assert.Equal(CommandOutcome.Blocked, verify.Outcome)
