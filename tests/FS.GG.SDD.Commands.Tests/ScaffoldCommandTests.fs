namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

/// Fixture-driven scaffold semantics over a **real** `dotnet new` provider (no mocks):
/// the in-repo template fixtures drive real process + filesystem I/O. Serialized into
/// one collection so concurrent `dotnet new install` calls never race.
[<Collection("Scaffold")>]
module ScaffoldCommandTests =
    let private fixturesRoot =
        Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "scaffold-provider")

    /// Resolve a committed registry fixture's `__FIXTURE__` token to the absolute
    /// fixtures directory and install it as the project's `.fsgg/providers.yml`.
    let private writeRegistry root registryFile =
        let template = File.ReadAllText(Path.Combine(fixturesRoot, "registries", registryFile))
        let resolved = template.Replace("__FIXTURE__", fixturesRoot.Replace('\\', '/'))
        TestSupport.writeRelative root ".fsgg/providers.yml" resolved

    let private scaffoldRequest root provider parameters force dryRun =
        { TestSupport.request Scaffold root with
            Provider = provider
            Parameters = parameters
            Force = force
            DryRun = dryRun }

    /// Drive the real MVU loop, returning the final model (for effect assertions) and
    /// its report.
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

    let private scaffoldSummary (report: CommandReport) =
        report.Scaffold |> Option.defaultWith (fun () -> failwith "Expected a scaffold summary.")

    let private diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun d -> d.Id)

    // ---------- US1 ----------

    [<Fact>]
    let ``plan Scaffold emits the init skeleton then the provider RunProcess`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        // Dry-run plans every effect without spawning a child or writing files.
        let model, _ = runScaffoldModel (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true)

        let writeIndex =
            model.PendingEffects
            |> List.tryFindIndex (function WriteFile(".fsgg/project.yml", _, _) -> true | _ -> false)

        let createIndex =
            model.PendingEffects
            |> List.tryFindIndex (function RunProcess("dotnet", args, _) -> List.contains "-o" args | _ -> false)

        Assert.True(Option.isSome writeIndex, "Expected the init skeleton write to be planned.")
        Assert.True(Option.isSome createIndex, "Expected the provider RunProcess to be planned.")
        Assert.True(writeIndex.Value < createIndex.Value, "Skeleton must be established before the provider runs.")

    [<Fact>]
    let ``scaffold ok materializes a runnable product under SDD management`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        let summary = scaffoldSummary report
        Assert.Equal("providerSucceeded", summary.Outcome)
        Assert.True(summary.SkeletonCreated)
        Assert.True(summary.ProviderInvoked)
        Assert.Equal<string list>([ "App.fsproj"; "Program.fs" ], summary.ProducedPaths |> List.sort)
        Assert.Equal(0, exitCodeForReport report)
        // SDD skeleton + product + provenance all present.
        Assert.True(TestSupport.existsRelative root ".fsgg/project.yml")
        Assert.True(TestSupport.existsRelative root "App.fsproj")
        Assert.True(TestSupport.existsRelative root ".fsgg/scaffold-provenance.json")
        // Produced paths are recorded as externally-owned change entries.
        let productChange = report.ChangedArtifacts |> List.tryFind (fun c -> c.Path = "Program.fs")
        Assert.Equal(Some "generatedProduct", productChange |> Option.map (fun c -> c.Ownership))

    [<Fact>]
    let ``scaffold --dry-run plans without spawning, writing, or provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true)

        let summary = scaffoldSummary report
        Assert.False(summary.ProviderInvoked)
        Assert.Contains("dotnet new fsgg-fixture-app", summary.NextActionHint)
        Assert.False(TestSupport.existsRelative root "App.fsproj")
        Assert.False(TestSupport.existsRelative root ".fsgg/scaffold-provenance.json")
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

    // ---------- US2 ----------

    [<Fact>]
    let ``scaffold without a provider blocks pointing to init`` () =
        let root = TestSupport.tempDirectory ()
        let report = runScaffold (scaffoldRequest root None [] false false)

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("scaffold.providerMissing", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.False(summary.ProviderInvoked)
        Assert.False(summary.SkeletonCreated)

    [<Fact>]
    let ``scaffold without a provider leaves the target untouched (init path intact)`` () =
        let root = TestSupport.tempDirectory ()
        runScaffold (scaffoldRequest root None [] false false) |> ignore
        // No skeleton, no product, no provenance written on the no-provider path.
        Assert.False(TestSupport.existsRelative root ".fsgg/project.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/scaffold-provenance.json")

    // ---------- US3 ----------

    [<Fact>]
    let ``scaffold unknown provider blocks with providerUnknown`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "does-not-exist") [] false false)
        Assert.Contains("scaffold.providerUnknown", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        Assert.False((scaffoldSummary report).ProviderInvoked)

    [<Fact>]
    let ``scaffold unsupported contract version blocks before invocation`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "bad-version.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        Assert.Contains("scaffold.providerVersionUnsupported", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        Assert.False((scaffoldSummary report).ProviderInvoked)
        Assert.False(TestSupport.existsRelative root "App.fsproj")

    [<Fact>]
    let ``scaffold missing required param blocks with providerParamMissing`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)
        Assert.Contains("scaffold.providerParamMissing", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)

    [<Fact>]
    let ``scaffold into a non-empty target without --force blocks per-path`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        TestSupport.writeRelative root "existing.txt" "pre-existing product file"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        Assert.Contains("scaffold.targetCollision", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)

    [<Fact>]
    let ``scaffold empty provider succeeds with providerEmpty`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "empty.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)
        Assert.Contains("scaffold.providerEmpty", diagnosticIds report)
        Assert.Equal(0, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.Equal("providerSucceededEmpty", summary.Outcome)
        Assert.True(summary.ProviderInvoked)
        Assert.Empty(summary.ProducedPaths)

    [<Fact>]
    let ``scaffold provider failure is a provider defect with partial paths`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "fails-midway.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)
        Assert.Contains("scaffold.providerFailed", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.Equal("providerFailed", summary.Outcome)
        Assert.Contains("partial.txt", summary.ProducedPaths)
        // Provenance records the partial-path failure (provider actually ran).
        Assert.True(TestSupport.existsRelative root ".fsgg/scaffold-provenance.json")

    [<Fact>]
    let ``scaffold provider writing into SDD trees is a provider defect`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "writes-into-fsgg.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)
        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)

    [<Fact>]
    let ``repeat scaffold blocks on collision without clobbering provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let first = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        Assert.Equal(0, exitCodeForReport first)
        let provenanceBefore = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"

        let second = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        Assert.Contains("scaffold.targetCollision", diagnosticIds second)
        Assert.Equal(1, exitCodeForReport second)
        // The existing provenance is not overwritten by the blocked re-scaffold.
        Assert.Equal(provenanceBefore, TestSupport.readRelative root ".fsgg/scaffold-provenance.json")

    // ---------- US4 ----------

    [<Fact>]
    let ``provenance records provider identity, contract version, and produced owner`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false) |> ignore

        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        Assert.Contains("\"providerName\": \"fixture\"", provenance)
        Assert.Contains("\"providerContractVersion\": \"1.0.0\"", provenance)
        Assert.Contains("\"templateRef\": \"fsgg-fixture-app\"", provenance)
        Assert.Contains("\"owner\": \"generatedProduct\"", provenance)

    [<Fact>]
    let ``refresh excludes provider-produced paths and flags malformed provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false) |> ignore

        // Provider files never appear in the refresh generated-view ledger (SC-007).
        let workId = "030-scaffold-demo"
        TestSupport.writeValidWorkSources root workId "Scaffold demo"
        let refresh = TestSupport.runRefresh root workId

        match refresh.Refresh with
        | Some summary ->
            Assert.DoesNotContain("App.fsproj", summary.RefreshedViewIds)
            Assert.DoesNotContain("App.fsproj", summary.BlockedViewIds)
        | None -> failwith "Expected a refresh summary."

        // Malformed provenance surfaces scaffold.provenanceMalformed on refresh.
        TestSupport.writeRelative root ".fsgg/scaffold-provenance.json" "{ not valid json"
        let refreshMalformed = TestSupport.runRefresh root workId
        Assert.Contains("scaffold.provenanceMalformed", refreshMalformed.Diagnostics |> List.map (fun d -> d.Id))

    [<Fact>]
    let ``scaffold happy-path JSON is byte-stable and root-free (golden)`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        // Deterministic serialization: byte-identical across repeated emits, with no
        // absolute project root, no clock. (The project id digest derives from the
        // root name — an init property — so determinism is asserted per-report.)
        let first = CommandSerialization.serializeReport report
        let second = CommandSerialization.serializeReport report
        Assert.Equal(first, second)
        Assert.Contains("\"name\": \"scaffold\"", first)
        Assert.Contains("\"outcome\": \"providerSucceeded\"", first)
        Assert.Contains("\"providerName\": \"fixture\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``scaffold report facts are identical across json and text projections`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        let json = CommandSerialization.serializeReport report
        let text = CommandRendering.renderText report
        Assert.Contains("providerSucceeded", json)
        Assert.Contains("scaffoldOutcome: providerSucceeded", text)
        Assert.Contains("App.fsproj", json)
        Assert.Contains("scaffoldProducedPath: App.fsproj", text)
