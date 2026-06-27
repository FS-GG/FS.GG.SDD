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

        let indexOf predicate = model.PendingEffects |> List.tryFindIndex predicate
        let installIndex = indexOf (function RunProcess("dotnet", args, _) -> List.contains "install" args | _ -> false)
        let updateIndex = indexOf (function RunProcess("dotnet", args, _) -> List.contains "update" args | _ -> false)
        let writeIndex = indexOf (function WriteFile(".fsgg/project.yml", _, _) -> true | _ -> false)
        let createIndex = indexOf (function RunProcess("dotnet", args, _) -> List.contains "-o" args | _ -> false)

        Assert.True(Option.isSome installIndex, "Expected `dotnet new install` to be planned.")
        Assert.True(Option.isSome updateIndex, "Expected `dotnet new update` to be planned.")
        Assert.True(Option.isSome writeIndex, "Expected the init skeleton write to be planned.")
        Assert.True(Option.isSome createIndex, "Expected the provider RunProcess to be planned.")
        // install -> update -> skeleton -> create.
        Assert.True(installIndex.Value < updateIndex.Value, "install must precede update.")
        Assert.True(updateIndex.Value < writeIndex.Value, "update must precede the skeleton.")
        Assert.True(writeIndex.Value < createIndex.Value, "Skeleton must be established before the provider runs.")

    [<Fact>]
    let ``scaffold --no-update skips the update step but still installs and creates`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let request =
            { scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true with TemplateUpdate = false }
        let model, _ = runScaffoldModel request

        let has predicate = model.PendingEffects |> List.exists predicate
        Assert.True(has (function RunProcess("dotnet", args, _) -> List.contains "install" args | _ -> false), "install still planned.")
        Assert.True(has (function RunProcess("dotnet", args, _) -> List.contains "-o" args | _ -> false), "create still planned.")
        Assert.False(has (function RunProcess("dotnet", args, _) -> List.contains "update" args | _ -> false), "update must be skipped.")

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

    // ===================================================================
    // 031 — lifecycle=sdd pass-through & app-only provenance
    // The `fixture` provider is the rendering-agnostic recording template
    // under tests/fixtures/scaffold-provider/lifecycle/, driven over the real
    // `dotnet new` edge (no mocks). Neutral identifiers only (FR-001/SC-005).
    // ===================================================================

    /// The planned `dotnet new … -o . <params> [--force]` create-arg vector, read
    /// from the real MVU plan (dry-run, no child spawned).
    let private plannedCreateArgs request =
        let model, _ = runScaffoldModel request

        model.PendingEffects
        |> List.tryPick (function
            | RunProcess("dotnet", args, _) when List.contains "-o" args -> Some args
            | _ -> None)
        |> Option.defaultWith (fun () -> failwith "Expected a planned provider create effect.")

    /// The forwarded `--key value` parameter segment of a create-arg vector — i.e.
    /// everything after the `-o .` output target, with the optional `--force` removed.
    let private forwardedParamArgs createArgs =
        createArgs
        |> List.skipWhile (fun arg -> arg <> ".")
        |> List.skip 1
        |> List.filter (fun arg -> arg <> "--force")

    // ---------- Phase 3 / US1: lifecycle forwarded verbatim ----------

    // T010 (F1 / FR-002 / US1.1): a real run forwards `lifecycle=sdd` to the child
    // verbatim — the recording fixture echoes it back into the app-only manifest.
    [<Fact>]
    let ``scaffold forwards lifecycle=sdd to the provider verbatim`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"
        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)
        |> ignore

        let manifest = TestSupport.readRelative root "scaffold-manifest.txt"
        Assert.Contains("lifecycle=sdd", manifest)

    // T011 (US1.2): the same run reports the success outcome and that the provider ran.
    [<Fact>]
    let ``scaffold lifecycle run reports providerSucceeded and ProviderInvoked`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"
        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)

        let summary = scaffoldSummary report
        Assert.Equal("providerSucceeded", summary.Outcome)
        Assert.True(summary.ProviderInvoked)
        Assert.Equal(0, exitCodeForReport report)

    // T012 (F2 / FR-003 / SC-001 / US1.3): the forwarded `--k v` set equals
    // `effective` exactly — nothing added, dropped, renamed, or reinterpreted.
    [<Fact>]
    let ``scaffold forwards exactly the effective overlay set as --key value pairs`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"
        let request = scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false true

        // effective = {productName=Acme, lifecycle=sdd}; Map canonicalizes to sorted keys.
        let expected = [ "--lifecycle"; "sdd"; "--productName"; "Acme" ]
        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))

    // T013 (F3 / FR-008): the forwarded vector is independent of author `--param` order.
    [<Fact>]
    let ``scaffold forwarded create-arg vector is independent of --param order`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"
        let oneOrder = scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false true
        let otherOrder = scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd"; "productName", "Acme" ] false true

        Assert.Equal<string list>(plannedCreateArgs oneOrder, plannedCreateArgs otherOrder)

    // T014 (F4 / FR-007 / US3.2 companion C4): forwarding is value-agnostic — an
    // arbitrary nonce lifecycle value behaves identically to `sdd` modulo the echoed
    // value (the behavioral half of the US3 no-special-casing guard).
    [<Fact>]
    let ``scaffold forwards an arbitrary lifecycle value identically to sdd`` () =
        let nonce = "q7-Zx_NONCE-42"

        // Plan-level: the create-arg vector differs only in the echoed lifecycle value.
        let planRoot = TestSupport.tempDirectory ()
        writeRegistry planRoot "lifecycle.providers.yml"
        let sddArgs =
            plannedCreateArgs (scaffoldRequest planRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false true)
        let nonceArgs =
            plannedCreateArgs (scaffoldRequest planRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", nonce ] false true)
        Assert.Equal<string list>(sddArgs |> List.map (fun a -> if a = "sdd" then nonce else a), nonceArgs)

        // End-to-end: same outcome, same produced-path set, same provenance owner shape.
        let runShape value =
            let root = TestSupport.tempDirectory ()
            writeRegistry root "lifecycle.providers.yml"
            let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", value ] false false)
            let summary = scaffoldSummary report
            summary.Outcome, (summary.ProducedPaths |> List.sort), TestSupport.readRelative root "scaffold-manifest.txt"

        let sddOutcome, sddPaths, sddManifest = runShape "sdd"
        let nonceOutcome, noncePaths, nonceManifest = runShape nonce
        Assert.Equal(sddOutcome, nonceOutcome)
        Assert.Equal<string list>(sddPaths, noncePaths)
        Assert.Contains($"lifecycle={nonce}", nonceManifest)
        // Manifests are identical once the echoed lifecycle value is normalized away.
        Assert.Equal(sddManifest.Replace("lifecycle=sdd", "lifecycle=X"), nonceManifest.Replace($"lifecycle={nonce}", "lifecycle=X"))

    // ---------- Phase 4 / US2: app-only provenance ----------

    /// Relative (forward-slash, sorted) file paths under a target directory.
    let private relativeFiles root =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun full -> Path.GetRelativePath(root, full).Replace('\\', '/'))
        |> Seq.sort
        |> Seq.toList

    /// A fresh target whose **leaf** name is fixed, so the init project-id digest
    /// (derived from the leaf — `Foundation.projectIdFromRoot`) is stable across
    /// targets. Lets us assert init byte-identity and cross-run determinism without
    /// the random temp-dir name leaking into the compared bytes.
    let private rootWithLeaf leaf =
        let dir = Path.Combine(Path.GetTempPath(), "fsgg-sdd-" + System.Guid.NewGuid().ToString("N"), leaf)
        Directory.CreateDirectory dir |> ignore
        dir

    let private provenancePath = ".fsgg/scaffold-provenance.json"

    // T015 (P1, P2 / FR-004 / SC-002,003 / US2.1,2.3): provenance.producedPaths equals
    // the app-only file set (diff of target vs a standalone init skeleton, minus the
    // provenance file itself), and every entry is owned `generatedProduct`.
    [<Fact>]
    let ``scaffold provenance records exactly the app-only tree, all generatedProduct`` () =
        let appRoot = TestSupport.tempDirectory ()
        writeRegistry appRoot "lifecycle.providers.yml"
        let report = runScaffold (scaffoldRequest appRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)
        let summary = scaffoldSummary report

        let initRoot = TestSupport.tempDirectory ()
        TestSupport.initializeProject initRoot
        let skeleton = relativeFiles initRoot |> Set.ofList

        // The app-only set is the target minus: the init skeleton, the provenance file
        // scaffold writes, and the `.fsgg/providers.yml` registry the test pre-planted
        // (an author input, not provider output).
        let preexisting = Set.ofList [ provenancePath; ".fsgg/providers.yml" ]
        let producedExpected =
            relativeFiles appRoot
            |> List.filter (fun path -> not (Set.contains path skeleton) && not (Set.contains path preexisting))

        // 100% precision AND recall: the recorded set is exactly the provider's files.
        Assert.Equal<string list>([ "App.fsproj"; "Program.fs"; "scaffold-manifest.txt" ], producedExpected)
        Assert.Equal<Set<string>>(Set.ofList producedExpected, Set.ofList summary.ProducedPaths)

        // Every provenance entry is owned generatedProduct (no other owner appears).
        let provenance = TestSupport.readRelative appRoot provenancePath
        let countOf (needle: string) =
            (provenance.Length - provenance.Replace(needle, "").Length) / needle.Length
        Assert.Equal(countOf "\"owner\":", countOf "\"owner\": \"generatedProduct\"")
        Assert.Equal(producedExpected.Length, countOf "\"owner\": \"generatedProduct\"")

    // T016 (P3, P4 / FR-005 / SC-002 / US2.2): produced ∩ skeleton == ∅, and every
    // skeleton file a lifecycle=sdd scaffold writes is byte-identical to a standalone init.
    [<Fact>]
    let ``scaffold skeleton is disjoint from the product and byte-identical to init`` () =
        let leaf = "scaffold-skel"
        let appRoot = rootWithLeaf leaf
        writeRegistry appRoot "lifecycle.providers.yml"
        let report = runScaffold (scaffoldRequest appRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)
        let produced = (scaffoldSummary report).ProducedPaths |> Set.ofList

        let initRoot = rootWithLeaf leaf
        TestSupport.initializeProject initRoot
        let skeleton = relativeFiles initRoot

        // No produced app path collides with the SDD skeleton.
        Assert.Empty(Set.intersect produced (Set.ofList skeleton))

        // The skeleton scaffold wrote is byte-for-byte the skeleton init writes.
        for path in skeleton do
            let fromScaffold = File.ReadAllBytes(Path.Combine(appRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            let fromInit = File.ReadAllBytes(Path.Combine(initRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            Assert.Equal<byte[]>(fromInit, fromScaffold)

    // T017 (P5, P6 / FR-006 / SC-004): two identical runs into clean targets yield
    // byte-identical provenance and byte-identical --json; provenance has sorted paths,
    // no clock, and no absolute path.
    [<Fact>]
    let ``scaffold provenance and json are deterministic across identical runs`` () =
        let runOnce () =
            let root = rootWithLeaf "scaffold-det"
            writeRegistry root "lifecycle.providers.yml"
            let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)
            root, scaffoldSummary report, TestSupport.readRelative root provenancePath, CommandSerialization.serializeReport report

        let firstRoot, firstSummary, firstProvenance, firstJson = runOnce ()
        let _, _, secondProvenance, secondJson = runOnce ()

        Assert.Equal(firstProvenance, secondProvenance)
        Assert.Equal(firstJson, secondJson)

        // Sorted producedPaths, no clock, no absolute path inside the provenance.
        Assert.Equal<string list>(firstSummary.ProducedPaths |> List.sort, firstSummary.ProducedPaths)
        Assert.DoesNotContain("timestamp", firstProvenance)
        Assert.DoesNotContain(firstRoot, firstProvenance)
        Assert.DoesNotContain(Path.GetTempPath(), firstProvenance)

    // T018 (P7 / 030-FR-007 re-asserted): refresh never adopts the app-only produced
    // paths into its generated-view ledger — they are externally owned.
    [<Fact>]
    let ``refresh excludes the lifecycle scaffold app-only produced paths`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"
        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false) |> ignore

        let workId = "031-lifecycle-demo"
        TestSupport.writeValidWorkSources root workId "Lifecycle demo"
        let refresh = TestSupport.runRefresh root workId

        match refresh.Refresh with
        | Some summary ->
            for path in [ "App.fsproj"; "Program.fs"; "scaffold-manifest.txt" ] do
                Assert.DoesNotContain(path, summary.RefreshedViewIds)
                Assert.DoesNotContain(path, summary.BlockedViewIds)
        | None -> failwith "Expected a refresh summary."

    // ---------- Phase 6 / FR-008 edges under lifecycle=sdd ----------

    // T023 (FR-008 / SC-006): with `lifecycle` declared required and omitted, SDD blocks
    // pre-invocation at exit 1 and writes no provenance.
    [<Fact>]
    let ``scaffold blocks when the required lifecycle param is omitted`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle-required.providers.yml"
        // productName supplied; lifecycle (required by this registry) omitted.
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Contains("scaffold.providerParamMissing", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        Assert.False((scaffoldSummary report).ProviderInvoked)
        Assert.False(TestSupport.existsRelative root provenancePath)

    // T024 (FR-008 / SC-006): an empty-product provider under lifecycle=sdd succeeds empty
    // at exit 0 with no produced paths.
    [<Fact>]
    let ``scaffold empty product under lifecycle=sdd succeeds with providerEmpty`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle-empty.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerEmpty", diagnosticIds report)
        Assert.Equal(0, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.Equal("providerSucceededEmpty", summary.Outcome)
        Assert.True(summary.ProviderInvoked)
        Assert.Empty(summary.ProducedPaths)

    // T025 (FR-008 / SC-006): a provider that writes into SDD trees under lifecycle=sdd is a
    // provider defect (exit 2), reported incomplete, and its SDD-tree intrusions are never
    // laundered into provenance as app-only paths.
    [<Fact>]
    let ``scaffold provider writing SDD trees under lifecycle=sdd is a provider defect`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle-intrusion.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.NotEqual("providerSucceeded", summary.Outcome)
        // The intruded SDD-owned paths are never recorded as app-only product.
        Assert.DoesNotContain("work/leak.txt", summary.ProducedPaths)
        Assert.DoesNotContain("readiness/leak.txt", summary.ProducedPaths)
