namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

/// Feature 052 (US2): the non-blocking CLI-coherence advisory, exercised end-to-end
/// over the real `dotnet new` provider (no mocks). Installed CLI version is the test
/// build's generator version (0.5.0); fixtures declare minimums above/at/below it.
[<Collection("Scaffold")>]
module ScaffoldCliCoherenceTests =
    let private fixturesRoot =
        Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "scaffold-provider")

    let private writeRegistry root registryFile =
        let template =
            File.ReadAllText(Path.Combine(fixturesRoot, "registries", registryFile))

        let resolved = template.Replace("__FIXTURE__", fixturesRoot.Replace('\\', '/'))
        TestSupport.writeRelative root ".fsgg/providers.yml" resolved

    let private scaffoldRequest root =
        { TestSupport.request Scaffold root with
            Provider = Some "fixture"
            Parameters = [ "productName", "Acme" ] }

    let private runScaffoldModel request =
        let model, effects = init request

        let rec loop state pending =
            match pending with
            | [] -> state
            | effects ->
                let results = interpretAll request.ProjectRoot request.DryRun effects

                let nextState, nextEffects =
                    results
                    |> List.fold
                        (fun (currentState, accumulated) result ->
                            let updated, produced = update (EffectInterpreted result) currentState
                            updated, accumulated @ produced)
                        (state, [])

                loop nextState nextEffects

        let finalModel = loop model effects |> fun state -> update BuildReport state |> fst
        finalModel, (finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel))

    let private runScaffold request = runScaffoldModel request |> snd

    let private diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun d -> d.Id)

    let private cliBehind (report: CommandReport) =
        report.Diagnostics |> List.filter (fun d -> d.Id = "scaffold.cliBehindMinimum")

    // US2 scenario 1 (SC-002): installed 0.5.0 < declared 0.6.0 ⇒ exactly one
    // scaffold.cliBehindMinimum (info) naming installed, minimum, and the gap.
    [<Fact>]
    let ``behind minimum emits exactly one cliBehindMinimum advisory naming installed minimum and gap`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-behind.providers.yml"
        let report = runScaffold (scaffoldRequest root)

        match cliBehind report with
        | [ diagnostic ] ->
            Assert.Equal("info", severityValue diagnostic.Severity)
            Assert.Contains("0.5.0", diagnostic.Message)
            Assert.Contains("0.6.0", diagnostic.Message)
            Assert.Contains("behind by 1 minor version", diagnostic.Message)
        | other -> Assert.True(false, $"expected exactly one cliBehindMinimum, got {List.length other}")

    // US2 scenario 3 / SC-004: a behind run's outcome and exit code are IDENTICAL to an
    // at/above run — the advisory is provably non-blocking (never reclassifies).
    [<Fact>]
    let ``behind minimum run has the same outcome and exit code as an at-or-above run`` () =
        let behindRoot = TestSupport.tempDirectory ()
        writeRegistry behindRoot "min-behind.providers.yml"
        let behind = runScaffold (scaffoldRequest behindRoot)

        let aboveRoot = TestSupport.tempDirectory ()
        writeRegistry aboveRoot "min-satisfied.providers.yml"
        let above = runScaffold (scaffoldRequest aboveRoot)

        Assert.Equal(above.Outcome, behind.Outcome)
        Assert.Equal(exitCodeForReport above, exitCodeForReport behind)
        // And the behind run still completed successfully (exit 0, non-blocking).
        Assert.Equal(0, exitCodeForReport behind)

    // US3 scenario 1 (FR-008 / SC-006 / D8): the behind advisory carries a next-action
    // pointer to the SUPPORTED re-seed path — `fsgg-sdd init`, NOT `refresh`.
    [<Fact>]
    let ``behind minimum carries a reseedSeededSkills next-action pointing at init not refresh`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-behind.providers.yml"
        let report = runScaffold (scaffoldRequest root)

        match report.NextAction with
        | Some action ->
            Assert.Equal("reseedSeededSkills", action.ActionId)
            Assert.Equal(Some Init, action.Command)
            Assert.Empty(action.BlockingDiagnosticIds)
            Assert.Contains("init", action.Reason)
            Assert.Contains("does not re-seed", action.Reason)
        | None -> Assert.True(false, "expected a reseedSeededSkills next-action in the behind case")

    // US3: at/above the minimum, no staleness ⇒ no reseed next-action is forced.
    [<Fact>]
    let ``at or above minimum does not emit a reseedSeededSkills next-action`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-satisfied.providers.yml"
        let report = runScaffold (scaffoldRequest root)

        match report.NextAction with
        | Some action -> Assert.False(action.ActionId = "reseedSeededSkills")
        | None -> ()

    // US2 scenario 2 (SC-003): installed at/above the minimum ⇒ no staleness advisory.
    [<Fact>]
    let ``at or above minimum emits no cliBehindMinimum advisory`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-satisfied.providers.yml"
        let report = runScaffold (scaffoldRequest root)
        Assert.Empty(cliBehind report)

    // US2 edge (boundary): installed exactly equal to the minimum is coherent — no advisory.
    [<Fact>]
    let ``equal to minimum emits no cliBehindMinimum advisory`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-equal.providers.yml"
        let report = runScaffold (scaffoldRequest root)
        Assert.Empty(cliBehind report)

    // US2 scenario 4 (SC-003): provider declares no minimum ⇒ nothing to compare, no advisory.
    [<Fact>]
    let ``no declared minimum emits no cliBehindMinimum advisory`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let report = runScaffold (scaffoldRequest root)
        Assert.Empty(cliBehind report)

    // Real coherent-set state today (FS.GG.Templates#43): the axis is declared but its
    // version is null (PENDING PUBLISH). SDD degrades to "no minimum" — no advisory, and
    // provenance records requiredMinimumCliVersion as null. This is the independently
    // shippable degradation ahead of the concrete-version publish.
    [<Fact>]
    let ``pending null minimum emits no advisory and records null provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-pending.providers.yml"
        let report = runScaffold (scaffoldRequest root)

        Assert.Empty(cliBehind report)
        Assert.DoesNotContain("scaffold.providerMinimumMalformed", diagnosticIds report)
        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        Assert.Contains("\"requiredMinimumCliVersion\": null", provenance)

    // US2 edge (D6): a malformed provider minimum surfaces scaffold.providerMinimumMalformed
    // (warning), never cliBehindMinimum, and is not silently ignored.
    [<Fact>]
    let ``malformed minimum emits providerMinimumMalformed warning and no cliBehindMinimum`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-malformed.providers.yml"
        let report = runScaffold (scaffoldRequest root)

        Assert.Contains("scaffold.providerMinimumMalformed", diagnosticIds report)
        Assert.Empty(cliBehind report)
        // Non-blocking: the scaffold still completes at exit 0.
        Assert.Equal(0, exitCodeForReport report)

    // US2 edge (D7 / U2): when the installed CLI version cannot be parsed, the comparison
    // is skipped (no advisory) AND provenance still records the producing CLI version
    // HONESTLY — the pre-existing generator value, never a fabricated version.
    [<Fact>]
    let ``unparseable installed version skips the comparison and records the CLI version honestly`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-behind.providers.yml"

        let request =
            { scaffoldRequest root with
                GeneratorVersion =
                    { Id = "fsgg-sdd"
                      Version = "not-a-version" } }

        let report = runScaffold request
        // No advisory: compare(unparseable, minimum) = None ⇒ skip (D7).
        Assert.Empty(cliBehind report)

        // Provenance records the producing CLI version honestly (the unparseable value as-is),
        // and does NOT persist the malformed *minimum* — it is a valid minimum here (0.6.0),
        // but the comparison was skipped because the *installed* side was unparseable.
        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        Assert.Contains("\"version\": \"not-a-version\"", provenance)
