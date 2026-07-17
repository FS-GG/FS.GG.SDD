namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.TestShared
open Xunit

/// Fixture-driven scaffold semantics over a **real** `dotnet new` provider (no mocks):
/// the in-repo template fixtures drive real process + filesystem I/O. This module also
/// mutates process-global `PATH` / `FSGG_SDD_PROCESS_TIMEOUT_MS`, so it joins the
/// `ProcessGlobalEnv` collection — every class that mutates process-global env OR spawns a
/// PATH-resolved process shares this collection, so a mutation is never observed by a
/// concurrent spawner (feature 067 / FR-001). `ProcessGlobalEnvGuardTests` enforces membership.
[<Collection("ProcessGlobalEnv")>]
module ScaffoldCommandTests =
    let private fixturesRoot =
        Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "scaffold-provider")

    /// Resolve a committed registry fixture's `__FIXTURE__` token to the absolute
    /// fixtures directory and install it as the project's `.fsgg/providers.yml`.
    let private writeRegistry root registryFile =
        let template =
            File.ReadAllText(Path.Combine(fixturesRoot, "registries", registryFile))

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
        report.Scaffold
        |> Option.defaultWith (fun () -> failwith "Expected a scaffold summary.")

    let private diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun d -> d.Id)

    // ---------- US1 ----------

    [<Fact>]
    let ``plan Scaffold emits the init skeleton then the provider RunProcess`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        // Dry-run plans every effect without spawning a child or writing files.
        let model, _ =
            runScaffoldModel (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true)

        let indexOf predicate =
            model.PendingEffects |> List.tryFindIndex predicate

        let installIndex =
            indexOf (function
                | RunProcess("dotnet", args, _) -> List.contains "install" args
                | _ -> false)

        let updateIndex =
            indexOf (function
                | RunProcess("dotnet", args, _) -> List.contains "update" args
                | _ -> false)

        let writeIndex =
            indexOf (function
                | WriteFile(".fsgg/project.yml", _, _) -> true
                | _ -> false)

        let createIndex =
            indexOf (function
                | RunProcess("dotnet", args, _) -> List.contains "-o" args
                | _ -> false)

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
            { scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true with
                TemplateUpdate = false }

        let model, _ = runScaffoldModel request

        let has predicate =
            model.PendingEffects |> List.exists predicate

        Assert.True(
            has (function
                | RunProcess("dotnet", args, _) -> List.contains "install" args
                | _ -> false),
            "install still planned."
        )

        Assert.True(
            has (function
                | RunProcess("dotnet", args, _) -> List.contains "-o" args
                | _ -> false),
            "create still planned."
        )

        Assert.False(
            has (function
                | RunProcess("dotnet", args, _) -> List.contains "update" args
                | _ -> false),
            "update must be skipped."
        )

    [<Fact; Trait("tier", "slow")>]
    let ``scaffold ok materializes a runnable product under SDD management`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

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
        let productChange =
            report.ChangedArtifacts |> List.tryFind (fun c -> c.Path = "Program.fs")

        Assert.Equal(Some "generatedProduct", productChange |> Option.map (fun c -> c.Ownership))

    [<Fact>]
    let ``scaffold --dry-run plans without spawning, writing, or provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true)

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

        let report =
            runScaffold (scaffoldRequest root (Some "does-not-exist") [] false false)

        Assert.Contains("scaffold.providerUnknown", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        Assert.False((scaffoldSummary report).ProviderInvoked)

    [<Fact>]
    let ``scaffold unsupported contract version blocks before invocation`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "bad-version.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

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

    [<Theory>]
    [<InlineData("force")>] // shadows the `dotnet new --force` flag
    [<InlineData("output")>] // shadows `--output`, redirecting the product out of the target
    [<InlineData("-o")>] // dash-prefixed: a malformed option token
    [<InlineData("")>] // empty key: renders to a bare `--` options terminator
    let ``scaffold rejects an author --param key that injects a dotnet new option`` (badKey: string) =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; badKey, "x" ] false false)

        Assert.Contains("scaffold.invalidParamKey", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        Assert.False((scaffoldSummary report).ProviderInvoked)
        Assert.False(TestSupport.existsRelative root "App.fsproj")

    [<Fact>]
    let ``scaffold does not reject a legitimate author --param key`` () =
        // A well-formed, non-option key must pass the injection guard. The block here is the
        // unrelated missing-required-param block (`productName` absent), which proves the key
        // itself was accepted rather than rejected as an injection.
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "EnableAudio", "true" ] false false)

        Assert.DoesNotContain("scaffold.invalidParamKey", diagnosticIds report)
        Assert.Contains("scaffold.providerParamMissing", diagnosticIds report)

    [<Fact>]
    let ``scaffold into a non-empty target without --force blocks per-path`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        TestSupport.writeRelative root "existing.txt" "pre-existing product file"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Contains("scaffold.targetCollision", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)

    [<Fact; Trait("tier", "slow")>]
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

    [<Fact; Trait("tier", "slow")>]
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

    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider writing into SDD trees is a provider defect`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "writes-into-fsgg.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)
        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)

    [<Fact; Trait("tier", "slow")>]
    let ``repeat scaffold blocks on collision without clobbering provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let first =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Equal(0, exitCodeForReport first)

        let provenanceBefore =
            TestSupport.readRelative root ".fsgg/scaffold-provenance.json"

        let second =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Contains("scaffold.targetCollision", diagnosticIds second)
        Assert.Equal(1, exitCodeForReport second)
        // The existing provenance is not overwritten by the blocked re-scaffold.
        Assert.Equal(provenanceBefore, TestSupport.readRelative root ".fsgg/scaffold-provenance.json")

    // ---------- US4 ----------

    [<Fact; Trait("tier", "slow")>]
    let ``provenance records provider identity, contract version, and produced owner`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        |> ignore

        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        Assert.Contains("\"providerName\": \"fixture\"", provenance)
        Assert.Contains("\"providerContractVersion\": \"1.0.0\"", provenance)
        Assert.Contains("\"templateRef\": \"fsgg-fixture-app\"", provenance)
        Assert.Contains("\"owner\": \"generatedProduct\"", provenance)

    // Feature 052 US1 scenario 1 (SC-001): provenance records BOTH the producing CLI
    // version (generator) and the provider-declared required minimum, side by side.
    [<Fact; Trait("tier", "slow")>]
    let ``provenance records the producing CLI version and the provider-declared required minimum`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-behind.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        |> ignore

        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        // Producing CLI version (generator) is present…
        Assert.Contains("\"generator\":", provenance)
        Assert.Contains("\"version\":", provenance)
        // …alongside the provider-declared required minimum, recorded verbatim. min-behind declares
        // one minor above the installed version, so it tracks the bump (installed 0.13.0 ⇒ 0.14.0).
        Assert.Contains("\"requiredMinimumCliVersion\": \"0.14.0\"", provenance)

    // Feature 052 US1 scenario 2: no provider minimum ⇒ the field is recorded as null
    // (absent, not fabricated); the producing CLI version is still recorded.
    [<Fact; Trait("tier", "slow")>]
    let ``provenance records requiredMinimumCliVersion as null when the provider declares none`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        |> ignore

        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        Assert.Contains("\"requiredMinimumCliVersion\": null", provenance)

    // Feature 052 US1 (D6): a malformed provider minimum is NOT persisted — recorded
    // as null (the raw malformed value never lands in provenance).
    [<Fact; Trait("tier", "slow")>]
    let ``provenance records requiredMinimumCliVersion as null when the provider minimum is malformed`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "min-malformed.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        |> ignore

        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        Assert.Contains("\"requiredMinimumCliVersion\": null", provenance)
        Assert.DoesNotContain("not-a-version", provenance)

    [<Fact; Trait("tier", "slow")>]
    let ``refresh excludes provider-produced paths and flags malformed provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        |> ignore

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

    [<Fact; Trait("tier", "slow")>]
    let ``scaffold happy-path JSON is byte-stable and root-free (golden)`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

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

    [<Fact; Trait("tier", "slow")>]
    let ``scaffold report facts are identical across json and text projections`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

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
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold forwards lifecycle=sdd to the provider verbatim`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)
        |> ignore

        let manifest = TestSupport.readRelative root "scaffold-manifest.txt"
        Assert.Contains("lifecycle=sdd", manifest)

    // T011 (US1.2): the same run reports the success outcome and that the provider ran.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold lifecycle run reports providerSucceeded and ProviderInvoked`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        let report =
            runScaffold (
                scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false
            )

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

        let request =
            scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false true

        // effective = {productName=Acme, lifecycle=sdd}; Map canonicalizes to sorted keys.
        let expected = [ "--lifecycle"; "sdd"; "--productName"; "Acme" ]
        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))

    // T013 (F3 / FR-008): the forwarded vector is independent of author `--param` order.
    [<Fact>]
    let ``scaffold forwarded create-arg vector is independent of --param order`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        let oneOrder =
            scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false true

        let otherOrder =
            scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd"; "productName", "Acme" ] false true

        Assert.Equal<string list>(plannedCreateArgs oneOrder, plannedCreateArgs otherOrder)

    // T014 (F4 / FR-007 / US3.2 companion C4): forwarding is value-agnostic — an
    // arbitrary nonce lifecycle value behaves identically to `sdd` modulo the echoed
    // value (the behavioral half of the US3 no-special-casing guard).
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold forwards an arbitrary lifecycle value identically to sdd`` () =
        let nonce = "q7-Zx_NONCE-42"

        // Plan-level: the create-arg vector differs only in the echoed lifecycle value.
        let planRoot = TestSupport.tempDirectory ()
        writeRegistry planRoot "lifecycle.providers.yml"

        let sddArgs =
            plannedCreateArgs (
                scaffoldRequest planRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false true
            )

        let nonceArgs =
            plannedCreateArgs (
                scaffoldRequest planRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", nonce ] false true
            )

        Assert.Equal<string list>(sddArgs |> List.map (fun a -> if a = "sdd" then nonce else a), nonceArgs)

        // End-to-end: same outcome, same produced-path set, same provenance owner shape.
        let runShape value =
            let root = TestSupport.tempDirectory ()
            writeRegistry root "lifecycle.providers.yml"

            let report =
                runScaffold (
                    scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", value ] false false
                )

            let summary = scaffoldSummary report
            summary.Outcome, (summary.ProducedPaths |> List.sort), TestSupport.readRelative root "scaffold-manifest.txt"

        let sddOutcome, sddPaths, sddManifest = runShape "sdd"
        let nonceOutcome, noncePaths, nonceManifest = runShape nonce
        Assert.Equal(sddOutcome, nonceOutcome)
        Assert.Equal<string list>(sddPaths, noncePaths)
        Assert.Contains($"lifecycle={nonce}", nonceManifest)
        // Manifests are identical once the echoed lifecycle value is normalized away.
        Assert.Equal(
            sddManifest.Replace("lifecycle=sdd", "lifecycle=X"),
            nonceManifest.Replace($"lifecycle={nonce}", "lifecycle=X")
        )

    // ---------- Phase 4 / US2: app-only provenance ----------

    /// Relative (forward-slash, sorted) file paths under a target directory, excluding the
    /// `.git` repository scaffold now initializes — never a scaffold-produced artifact.
    let private relativeFiles root =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun full -> Path.GetRelativePath(root, full).Replace('\\', '/'))
        |> Seq.filter (fun path -> not (path = ".git" || path.StartsWith(".git/", System.StringComparison.Ordinal)))
        |> Seq.sort
        |> Seq.toList

    /// A fresh target whose **leaf** name is fixed, so the init project-id digest
    /// (derived from the leaf — `Foundation.projectIdFromRoot`) is stable across
    /// targets. Lets us assert init byte-identity and cross-run determinism without
    /// the random temp-dir name leaking into the compared bytes.
    let private rootWithLeaf leaf =
        let dir =
            Path.Combine(Path.GetTempPath(), "fsgg-sdd-" + System.Guid.NewGuid().ToString("N"), leaf)

        Directory.CreateDirectory dir |> ignore
        dir

    let private provenancePath = ".fsgg/scaffold-provenance.json"

    /// Pinned here rather than imported: the CLI-pin path is a published consumer contract
    /// (FS.GG.SDD#315), so a silent change to the source constant must redden a test.
    let private toolManifestPath = ".config/dotnet-tools.json"

    // T015 (P1, P2 / FR-004 / SC-002,003 / US2.1,2.3): provenance.producedPaths equals
    // the app-only file set (diff of target vs a standalone init skeleton, minus the
    // provenance file itself), and every entry is owned `generatedProduct`.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provenance records exactly the app-only tree, all generatedProduct`` () =
        let appRoot = TestSupport.tempDirectory ()
        writeRegistry appRoot "lifecycle.providers.yml"

        let report =
            runScaffold (
                scaffoldRequest appRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false
            )

        let summary = scaffoldSummary report

        let initRoot = TestSupport.tempDirectory ()
        TestSupport.initializeProject initRoot
        let skeleton = relativeFiles initRoot |> Set.ofList

        // The app-only set is the target minus: the init skeleton, the provenance file
        // scaffold writes, the `.config/dotnet-tools.json` CLI pin scaffold writes as an
        // SDD-owned post-instantiation step (FS.GG.SDD#315 — SDD's file, not the provider's),
        // and the `.fsgg/providers.yml` registry the test pre-planted (an author input, not
        // provider output).
        let preexisting =
            Set.ofList [ provenancePath; toolManifestPath; ".fsgg/providers.yml" ]

        let producedExpected =
            relativeFiles appRoot
            |> List.filter (fun path -> not (Set.contains path skeleton) && not (Set.contains path preexisting))

        // 100% precision AND recall: the recorded set is exactly the provider's files.
        Assert.Equal<string list>([ "App.fsproj"; "Program.fs"; "scaffold-manifest.txt" ], producedExpected)
        Assert.Equal<Set<string>>(Set.ofList producedExpected, Set.ofList summary.ProducedPaths)

        // Every *producedPaths* entry is owned generatedProduct. Since FS.GG.SDD#315 the
        // document also carries `sddOwnedPaths` — SDD's own post-instantiation writes, owner
        // `sdd`. They are a disjoint list precisely so P1/P3 (producedPaths == exactly the
        // provider's tree, disjoint from the skeleton) keep holding, so the generatedProduct
        // count still equals the app-only set and the only other owner is that one `sdd` entry.
        let provenance = TestSupport.readRelative appRoot provenancePath

        let countOf (needle: string) =
            (provenance.Length - provenance.Replace(needle, "").Length) / needle.Length

        Assert.Equal(producedExpected.Length, countOf "\"owner\": \"generatedProduct\"")
        Assert.Equal(1, countOf "\"owner\": \"sdd\"")
        Assert.Equal(countOf "\"owner\":", producedExpected.Length + 1)

        let parsed =
            ScaffoldProvenance.tryParse provenance
            |> Option.defaultWith (fun () -> failwith "Expected parseable provenance.")

        Assert.Equal<string list>([ toolManifestPath ], parsed.SddOwnedPaths |> List.map (fun p -> p.Path))
        Assert.All(parsed.ProducedPaths, fun p -> Assert.Equal(ArtifactOwner.GeneratedProduct, p.Owner))
        Assert.All(parsed.SddOwnedPaths, fun p -> Assert.Equal(ArtifactOwner.Sdd, p.Owner))

        // P3 restated against the new list: SDD's own writes never leak into producedPaths.
        Assert.DoesNotContain(toolManifestPath, parsed.ProducedPaths |> List.map (fun p -> p.Path))

    // T016 (P3, P4 / FR-005 / SC-002 / US2.2): produced ∩ skeleton == ∅, and every
    // skeleton file a lifecycle=sdd scaffold writes is byte-identical to a standalone init.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold skeleton is disjoint from the product and byte-identical to init`` () =
        let leaf = "scaffold-skel"
        let appRoot = rootWithLeaf leaf
        writeRegistry appRoot "lifecycle.providers.yml"

        let report =
            runScaffold (
                scaffoldRequest appRoot (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false
            )

        let produced = (scaffoldSummary report).ProducedPaths |> Set.ofList

        let initRoot = rootWithLeaf leaf
        TestSupport.initializeProject initRoot
        // 085: exclude `.fsgg/scaffold-provenance.json` from the byte-identical comparison — init
        // writes a provider-less *dev-repo* provenance while scaffold writes its *provider*
        // provenance, so the anchor is intentionally different. The seeded skeleton is identical.
        let skeleton = relativeFiles initRoot |> List.filter (fun p -> p <> provenancePath)

        // No produced app path collides with the SDD skeleton.
        Assert.Empty(Set.intersect produced (Set.ofList skeleton))

        // The skeleton scaffold wrote is byte-for-byte the skeleton init writes.
        for path in skeleton do
            let fromScaffold =
                File.ReadAllBytes(Path.Combine(appRoot, path.Replace('/', Path.DirectorySeparatorChar)))

            let fromInit =
                File.ReadAllBytes(Path.Combine(initRoot, path.Replace('/', Path.DirectorySeparatorChar)))

            Assert.Equal<byte[]>(fromInit, fromScaffold)

    // T017 (P5, P6 / FR-006 / SC-004): two identical runs into clean targets yield
    // byte-identical provenance and byte-identical --json; provenance has sorted paths,
    // no clock, and no absolute path.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provenance and json are deterministic across identical runs`` () =
        let runOnce () =
            let root = rootWithLeaf "scaffold-det"
            writeRegistry root "lifecycle.providers.yml"

            let report =
                runScaffold (
                    scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false
                )

            root,
            scaffoldSummary report,
            TestSupport.readRelative root provenancePath,
            CommandSerialization.serializeReport report

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
    [<Fact; Trait("tier", "slow")>]
    let ``refresh excludes the lifecycle scaffold app-only produced paths`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)
        |> ignore

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
        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Contains("scaffold.providerParamMissing", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        Assert.False((scaffoldSummary report).ProviderInvoked)
        Assert.False(TestSupport.existsRelative root provenancePath)

    // T024 (FR-008 / SC-006): an empty-product provider under lifecycle=sdd succeeds empty
    // at exit 0 with no produced paths.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold empty product under lifecycle=sdd succeeds with providerEmpty`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle-empty.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerEmpty", diagnosticIds report)
        Assert.Equal(0, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.Equal("providerSucceededEmpty", summary.Outcome)
        Assert.True(summary.ProviderInvoked)
        Assert.Empty(summary.ProducedPaths)

    // T025 (FR-008 / SC-006): a provider that writes into SDD trees under lifecycle=sdd is a
    // provider defect (exit 2), reported incomplete, and its SDD-tree intrusions are never
    // laundered into provenance as app-only paths.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider writing SDD trees under lifecycle=sdd is a provider defect`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle-intrusion.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.NotEqual<string>("providerSucceeded", summary.Outcome)
        // The intruded SDD-owned paths are never recorded as app-only product.
        Assert.DoesNotContain("work/leak.txt", summary.ProducedPaths)
        Assert.DoesNotContain("readiness/leak.txt", summary.ProducedPaths)

    // 051 T020 (US3 / INV-6, FR-008): a provider that writes into the seeded skill trees
    // (.claude/skills/ or .codex/skills/) is a provider defect — rejected as
    // providerWroteSddTree (exit 2), and the skill subtrees never appear in the provenance
    // producedPaths. Exercises the T015 isSddTree guard end-to-end over a real provider.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider writing into the seeded skill trees is a provider defect`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "skills-intrusion.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.NotEqual<string>("providerSucceeded", summary.Outcome)
        // The intruded skill-tree paths are never laundered into provenance as app-only.
        Assert.DoesNotContain(".claude/skills/leak/SKILL.md", summary.ProducedPaths)
        Assert.DoesNotContain(".codex/skills/leak/SKILL.md", summary.ProducedPaths)

    // ===================================================================
    // 056 — orchestrator skill fan-out: union SDD + provider skills into all
    // three agent roots; strict, symmetric guard. Real dotnet new + filesystem.
    // ===================================================================

    let private bytesAt root (relativePath: string) =
        File.ReadAllBytes(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))

    let private threeRoots (name: string) =
        [ $".claude/skills/{name}/SKILL.md"
          $".codex/skills/{name}/SKILL.md"
          $".agents/skills/{name}/SKILL.md" ]

    let private assertByteIdenticalAcrossRoots root name =
        match threeRoots name |> List.map (bytesAt root) with
        | [ claude; codex; agents ] ->
            Assert.Equal<byte[]>(claude, codex)
            Assert.Equal<byte[]>(claude, agents)
        | _ -> failwith "expected exactly three roots"

    // 056 T012 (US2 / FR-001 / SC-002 / P1): a provider write into the whole-root-reserved
    // .claude/skills/ OR .codex/skills/ is a defect (exit 2), no fan-out, path never recorded.
    // Each per-root fixture is driven independently so each whole-root clause is proven alone.
    [<Theory; Trait("tier", "slow")>]
    [<InlineData("skills-intrusion-claude.providers.yml", ".claude/skills/leak/SKILL.md")>]
    [<InlineData("skills-intrusion-codex.providers.yml", ".codex/skills/leak/SKILL.md")>]
    let ``scaffold provider writing a whole-root-reserved skill tree is a defect``
        (registry: string)
        (intruded: string)
        =
        let root = TestSupport.tempDirectory ()
        writeRegistry root registry

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.NotEqual<string>("providerSucceeded", summary.Outcome)
        Assert.DoesNotContain(intruded, summary.ProducedPaths)
        Assert.DoesNotContain(intruded, summary.MirroredPaths)

    // 056 T013 (US2 / FR-002 / SC-002 / P2): the fs-gg-sdd-* namespace is reserved even in the
    // neutral .agents/skills/ root — a provider write there is a defect (exit 2), no fan-out.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider writing into the reserved namespace under .agents is a defect`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "skills-intrusion-agents.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.NotEqual<string>("providerSucceeded", summary.Outcome)
        Assert.DoesNotContain(".agents/skills/fs-gg-sdd-custom/SKILL.md", summary.ProducedPaths)
        Assert.DoesNotContain(".agents/skills/fs-gg-sdd-custom/SKILL.md", summary.MirroredPaths)

    // 056 T014 (US2 / P4 regression): the .fsgg/·work/·readiness/ intrusion behavior is unchanged.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider writing into lifecycle trees remains a defect`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle-intrusion.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)

    // 056 T016 (US1 / FR-005/006 / SC-001 / P6): a compliant provider that writes a co-tenant
    // skill into .agents/skills/ fans the byte-identical union into all three roots.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold fans out the union to all three roots byte-identically`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "skills-agents-cotenant.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.DoesNotContain("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(0, exitCodeForReport report)
        Assert.Equal("providerSucceeded", (scaffoldSummary report).Outcome)

        // Every seeded fs-gg-sdd-* skill AND the provider co-tenant fs-gg-elmish skill are
        // present and byte-identical across .claude, .codex, and .agents.
        for name in FS.GG.SDD.Commands.Internal.SeededSkills.skillNames do
            assertByteIdenticalAcrossRoots root name

        for path in threeRoots "fs-gg-elmish" do
            Assert.True(TestSupport.existsRelative root path, $"expected {path} to exist")

        assertByteIdenticalAcrossRoots root "fs-gg-elmish"

    // 056 T017 (US1 / FR-007 / P7): the provider's .agents canonical stays generatedProduct in
    // producedPaths; the .claude/.codex mirror copies are recorded in mirroredPaths (mirrored);
    // no fs-gg-sdd-* path appears in either; schemaVersion stays 1.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold attributes the fan-out mirror copies to SDD in provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "skills-agents-cotenant.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)
        |> ignore

        let provenance = TestSupport.readRelative root provenancePath

        match FS.GG.SDD.Artifacts.ScaffoldProvenance.tryParse provenance with
        | Some record ->
            Assert.Equal(1, record.SchemaVersion)
            let producedPaths = record.ProducedPaths |> List.map (fun p -> p.Path)
            let mirroredPaths = record.MirroredPaths |> List.map (fun p -> p.Path)

            Assert.Contains(".agents/skills/fs-gg-elmish/SKILL.md", producedPaths)

            let elmishProduced =
                record.ProducedPaths
                |> List.find (fun p -> p.Path = ".agents/skills/fs-gg-elmish/SKILL.md")

            Assert.Equal(FS.GG.SDD.Artifacts.ArtifactRef.GeneratedProduct, elmishProduced.Owner)

            // 058/ADR-0014 §Decision 3 / SC-004: the skill copy carries the content digest of its
            // materialized bytes; the mirror copies share the canonical `.agents` digest.
            let expectedDigest =
                Fsgg.SkillMirror.sha256 (
                    System.Text.Encoding.UTF8.GetString(bytesAt root ".agents/skills/fs-gg-elmish/SKILL.md")
                )

            Assert.Equal(Some expectedDigest, elmishProduced.Sha256)

            Assert.Contains(".claude/skills/fs-gg-elmish/SKILL.md", mirroredPaths)
            Assert.Contains(".codex/skills/fs-gg-elmish/SKILL.md", mirroredPaths)

            Assert.True(
                record.MirroredPaths
                |> List.forall (fun p -> p.Owner = FS.GG.SDD.Artifacts.ArtifactRef.Mirrored)
            )
            // Every mirror copy carries the same content digest as its `.agents` source.
            Assert.True(record.MirroredPaths |> List.forall (fun p -> p.Sha256 = Some expectedDigest))
            // A non-skill produced path (app source) carries no digest.
            Assert.True(
                record.ProducedPaths
                |> List.exists (fun p -> not (p.Path.Contains "/skills/") && p.Sha256 = None)
            )

            // No seeded fs-gg-sdd-* path is laundered into either array.
            Assert.DoesNotContain(producedPaths, fun (p: string) -> p.Contains "fs-gg-sdd-")
            Assert.DoesNotContain(mirroredPaths, fun (p: string) -> p.Contains "fs-gg-sdd-")
        | None -> failwith "Expected the scaffold provenance to parse."

    // 056 T018 (US1 acceptance #3): a provider that produces NO skills leaves all three roots
    // with the seeded fs-gg-sdd-* set byte-identical and mirroredPaths empty.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold with a provider that emits no skills mirrors nothing`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        let report =
            runScaffold (
                scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false
            )

        Assert.Equal(0, exitCodeForReport report)
        Assert.Empty((scaffoldSummary report).MirroredPaths)

        for name in FS.GG.SDD.Commands.Internal.SeededSkills.skillNames do
            assertByteIdenticalAcrossRoots root name

        let provenance = TestSupport.readRelative root provenancePath

        match FS.GG.SDD.Artifacts.ScaffoldProvenance.tryParse provenance with
        | Some record -> Assert.Empty(record.MirroredPaths)
        | None -> failwith "Expected the scaffold provenance to parse."

    // 056 T022 (FR-012 / P10): an incomplete fan-out is never reported complete. Real fault
    // injection: pre-plant a CONFLICTING .claude mirror target so the no-clobber
    // (AgentGuidanceTarget) mirror WriteFile is refused — the fan-out fails mid-mirror. The
    // scaffold finalizes non-success at exit 2 with scaffold.mirrorFailed, and neither the
    // report nor provenance records a completed fan-out.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold with a blocked mirror target fails the fan-out at exit 2`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "skills-agents-cotenant.providers.yml"
        // A conflicting, author-owned copy at a mirror target: no-clobber refuses to overwrite
        // it, so the mirror WriteFile fails. (.claude/skills/ is isSddOwned, so this pre-plant
        // does not trip the non-empty-target collision guard.)
        TestSupport.writeRelative root ".claude/skills/fs-gg-elmish/SKILL.md" "CONFLICTING AUTHOR CONTENT\n"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "lifecycle", "sdd" ] false false)

        Assert.Contains("scaffold.mirrorFailed", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.NotEqual<string>("providerSucceeded", summary.Outcome)
        Assert.Empty(summary.MirroredPaths)

        // Provenance (if written) records NO completed fan-out.
        if TestSupport.existsRelative root provenancePath then
            match FS.GG.SDD.Artifacts.ScaffoldProvenance.tryParse (TestSupport.readRelative root provenancePath) with
            | Some record ->
                Assert.NotEqual("providerSucceeded", record.Outcome)
                Assert.Empty(record.MirroredPaths)
            | None -> ()

    // ===================================================================
    // 032 — scaffold owns repo-init & script executability post-instantiation
    // Real `git` + real filesystem over the public scaffold surface (no mocks):
    // a fresh temp dir is outside any git work tree, so the success path
    // initializes a repo and chmods produced `.sh` scripts. US1/US2/US3/US4.
    // ===================================================================

    let private gitDirExists root =
        Directory.Exists(Path.Combine(root, ".git"))

    let private isExecutable root (relativePath: string) =
        let mode =
            File.GetUnixFileMode(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))

        mode &&& UnixFileMode.UserExecute = UnixFileMode.UserExecute

    /// Run real `git` in a directory and return (exitCode, trimmed stdout).
    let private git = TestShared.ChildProcess.git

    // ---------- Phase 3 / US1: scaffolded product lands in an initialized repo ----------

    // T008 (US1-AC1): a success into a fresh temp dir outside any work tree initializes a
    // real git repository at the product root and reports `RepoInitOutcome = initialized`.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold initializes a git repository at the product root`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.True(gitDirExists root, "Expected a real .git directory at the product root.")
        Assert.Equal("initialized", (scaffoldSummary report).RepoInitOutcome)
        Assert.Equal(0, exitCodeForReport report)
        // The probe + init effects are planned (and ran) only on the success path.
        let exit, inside = git root [ "rev-parse"; "--is-inside-work-tree" ]
        Assert.Equal(0, exit)
        Assert.Equal("true", inside)

    // T009 (US1-AC2 / FR-004): the initialized work tree captures the SDD skeleton, the
    // provider product files, and the scaffold-provenance record.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold repo captures the skeleton, product, and provenance`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
        |> ignore

        // The work-tree root is the product root, so the whole scaffolded tree is captured.
        let exit, _ = git root [ "rev-parse"; "--is-inside-work-tree" ]
        Assert.Equal(0, exit)
        Assert.True(TestSupport.existsRelative root ".fsgg/project.yml")
        Assert.True(TestSupport.existsRelative root "App.fsproj")
        Assert.True(TestSupport.existsRelative root ".fsgg/scaffold-provenance.json")
        // `git add -A` over the work tree stages every scaffolded path (skeleton + product
        // + provenance), proving they all live inside the initialized repository.
        git root [ "add"; "-A" ] |> ignore
        let _, staged = git root [ "ls-files" ]
        Assert.Contains(".fsgg/project.yml", staged)
        Assert.Contains("App.fsproj", staged)
        Assert.Contains(".fsgg/scaffold-provenance.json", staged)

    // T010 (Edge / FR-004): the empty-but-successful outcome still initializes a repo over
    // the skeleton + provenance and reports `initialized`.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold empty-but-successful still initializes a repository`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "empty.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        let summary = scaffoldSummary report
        Assert.Equal("providerSucceededEmpty", summary.Outcome)
        Assert.Equal("initialized", summary.RepoInitOutcome)
        Assert.True(gitDirExists root)
        Assert.True(TestSupport.existsRelative root ".fsgg/scaffold-provenance.json")
        Assert.Equal(0, exitCodeForReport report)

    // ---------- Phase 4 / US2: produced shell scripts are executable ----------

    // T013 (US2-AC1): the produced `run.sh` carries an executable bit and the run reports
    // exactly one script made executable, none skipped.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold makes a produced shell script executable`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "with-script.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "scripted") [] false false)

        let summary = scaffoldSummary report
        Assert.Contains("run.sh", summary.ProducedPaths)
        Assert.True(isExecutable root "run.sh", "Expected the produced run.sh to be executable.")
        Assert.Equal(1, summary.ExecutableScriptCount)
        Assert.Equal(0, summary.ExecutableScriptsSkipped)
        Assert.Equal(0, exitCodeForReport report)

    // T014 (US2-AC2): a provider with no shell scripts is a no-op — make-executable
    // succeeds with a zero count.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold with no shell scripts reports a zero executable count`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        let summary = scaffoldSummary report
        Assert.DoesNotContain(summary.ProducedPaths, fun p -> p.EndsWith ".sh")
        Assert.Equal(0, summary.ExecutableScriptCount)
        Assert.Equal(0, summary.ExecutableScriptsSkipped)

    // T015 (US2-AC3): the SetExecutable interpreter arm degrades gracefully. Driving it at a
    // guaranteed-unwritable path (a file that does not exist) on a real filesystem makes the
    // arm catch its exception and return `Succeeded = false` with NO diagnostic, never
    // throwing past the interpreter. Narrowed to the documented try/skip contract of the arm
    // (T005) because no in-process path forces a deterministic chmod failure for the file's
    // owner on a Unix CI host (Principle VI substitution, noted by name).
    [<Fact>]
    let ``SetExecutable arm reports a skip without throwing on an unwritable path`` () =
        let root = TestSupport.tempDirectory ()
        let result = interpret root false (SetExecutable "does-not-exist.sh")
        Assert.False(result.Succeeded)
        Assert.True(Option.isNone result.Diagnostic, "A make-executable skip must carry no (blocking) diagnostic.")
        // Dry-run is always a no-op success (FR-008).
        let dry = interpret root true (SetExecutable "does-not-exist.sh")
        Assert.True(dry.Succeeded)

    // ---------- Phase 5 / US3: safeguards keep the steps safe and non-fatal ----------

    // T018 (US3-AC1 / SC-002): scaffolding into an existing work tree creates no nested repo
    // and reports `skippedExistingRepository`; the scaffold still succeeds.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold inside an existing work tree skips repo-init non-fatally`` () =
        let root = TestSupport.tempDirectory ()
        git root [ "init" ] |> ignore
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] true false)

        let summary = scaffoldSummary report
        Assert.Equal("skippedExistingRepository", summary.RepoInitOutcome)
        Assert.Contains("scaffold.repoInitSkippedExistingRepository", diagnosticIds report)
        Assert.Equal("providerSucceeded", summary.Outcome)
        Assert.Equal(0, exitCodeForReport report)
        // No nested repository was created under the product subtree.
        Assert.False(Directory.Exists(Path.Combine(root, "App.fsproj", ".git")))

    // T018b (Edge re-run/--force / FR-013): the repo-init step is idempotent across re-runs.
    // The first run creates the repository; a second `--force` run detects the existing work
    // tree (no nesting) and the produced script's executable bit is stable. FR-013 governs the
    // repo-init/chmod steps only — the second run still hits the *pre-existing* provenance
    // clobber-guard (StructuredSource refuses an unsafe overwrite), so its overall exit is not
    // asserted here; that guard is unrelated to this feature.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold re-run resolves to the existing-repository case with a stable script bit`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "with-script.providers.yml"
        let first = runScaffold (scaffoldRequest root (Some "scripted") [] false false)
        Assert.Equal(0, exitCodeForReport first)
        Assert.Equal(1, (scaffoldSummary first).ExecutableScriptCount)
        Assert.True(isExecutable root "run.sh")

        let second = runScaffold (scaffoldRequest root (Some "scripted") [] true false)
        let summary = scaffoldSummary second
        // The first run initialized the repo, so the re-run skips repo-init (no nesting).
        Assert.Equal("skippedExistingRepository", summary.RepoInitOutcome)
        Assert.False(Directory.Exists(Path.Combine(root, "App.fsproj", ".git")))
        // The produced script's executable bit is stable across the re-run (safe re-apply).
        Assert.True(isExecutable root "run.sh", "run.sh must remain executable after a re-run.")

    /// A PATH containing `dotnet` but no `git`, so the `git` probe cannot launch
    /// (`Started = false`) while `dotnet new` still works. Computed from the live PATH so it
    /// is host-independent; empty only if no such directory exists (asserted by callers).
    let private pathWithoutGit () =
        let separator = Path.PathSeparator

        let current =
            System.Environment.GetEnvironmentVariable "PATH"
            |> Option.ofObj
            |> Option.defaultValue ""

        current.Split separator
        |> Array.filter (fun dir ->
            dir <> ""
            && File.Exists(Path.Combine(dir, "dotnet"))
            && not (File.Exists(Path.Combine(dir, "git"))))
        |> String.concat (string separator)

    // T019 (US3-AC2 / SC-004): with `git` unavailable to the child the probe reports
    // `Started = false`; repo-init is skipped non-fatally as `skippedGitUnavailable`, and the
    // scaffold otherwise succeeds at exit 0. Real env (a git-free PATH for the spawned
    // probe), restored in a finally.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold with git unavailable skips repo-init non-fatally`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        let gitFreePath = pathWithoutGit ()
        Assert.True((gitFreePath <> ""), "Test precondition: a dotnet-only, git-free PATH directory must exist.")

        let original =
            System.Environment.GetEnvironmentVariable "PATH"
            |> Option.ofObj
            |> Option.defaultValue ""

        let report =
            try
                System.Environment.SetEnvironmentVariable("PATH", gitFreePath)
                runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)
            finally
                System.Environment.SetEnvironmentVariable("PATH", original)

        let summary = scaffoldSummary report
        Assert.Equal("skippedGitUnavailable", summary.RepoInitOutcome)
        Assert.Contains("scaffold.repoInitSkippedGitUnavailable", diagnosticIds report)
        Assert.Equal("providerSucceeded", summary.Outcome)
        Assert.Equal(0, exitCodeForReport report)
        Assert.False(gitDirExists root, "No repository should be created when git is unavailable.")

    // T020 (US3-AC3 / FR-010): a successful scaffold with a skipped convenience step is not
    // reported failed or incomplete.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold with a skipped step is not reported failed or incomplete`` () =
        let root = TestSupport.tempDirectory ()
        git root [ "init" ] |> ignore
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] true false)

        Assert.Equal(0, exitCodeForReport report)
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal("providerSucceeded", (scaffoldSummary report).Outcome)

    // ---------- Phase 6 / US4: steps are generic and leak no provider specifics ----------

    // T022 (US4-AC1): a non-rendering provider gets identical repo-init + make-executable
    // behavior, driven only by the scaffolded tree.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold post-instantiation behavior is identical for a neutral provider`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "with-script.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "scripted") [] false false)

        let summary = scaffoldSummary report
        Assert.Equal("initialized", summary.RepoInitOutcome)
        Assert.True(gitDirExists root)
        Assert.Equal(1, summary.ExecutableScriptCount)
        Assert.True(isExecutable root "run.sh")
        Assert.Equal(0, exitCodeForReport report)

    // ---------- 315: the `.config/dotnet-tools.json` CLI pin ----------

    /// The version the running CLI would pin — never a literal, so the assertions cannot rot
    /// at the next version bump (the pattern ToolVersionTests established for #305).
    let private installedVersion =
        FS.GG.SDD.Artifacts.SchemaVersion.currentGeneratorVersion().Version

    // T031 (AC1 / AC4): a successful scaffold writes the tool manifest pinning the scaffolding
    // CLI's own version, reports `pinned`, and records the path in provenance as SDD-owned —
    // never in `producedPaths`, which stays exactly the provider's tree (P1/P3).
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold pins the fsgg-sdd CLI in a dotnet tool manifest`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        let summary = scaffoldSummary report
        Assert.Equal("pinned", summary.ToolManifestOutcome)
        Assert.True(TestSupport.existsRelative root toolManifestPath)

        let manifest = TestSupport.readRelative root toolManifestPath
        Assert.Contains("\"fs.gg.sdd.cli\"", manifest)
        Assert.Contains($"\"version\": \"{installedVersion}\"", manifest)
        Assert.Contains("\"fsgg-sdd\"", manifest)

        // It is real JSON, not a string that merely looks like one.
        use parsed = System.Text.Json.JsonDocument.Parse manifest
        let tool = parsed.RootElement.GetProperty("tools").GetProperty("fs.gg.sdd.cli")
        Assert.Equal(installedVersion, tool.GetProperty("version").GetString())
        Assert.Equal("fsgg-sdd", tool.GetProperty("commands").[0].GetString())
        Assert.True(parsed.RootElement.GetProperty("isRoot").GetBoolean())

        let provenance =
            TestSupport.readRelative root provenancePath
            |> ScaffoldProvenance.tryParse
            |> Option.defaultWith (fun () -> failwith "Expected parseable provenance.")

        Assert.Equal<string list>([ toolManifestPath ], provenance.SddOwnedPaths |> List.map (fun p -> p.Path))
        Assert.All(provenance.SddOwnedPaths, fun p -> Assert.Equal(ArtifactOwner.Sdd, p.Owner))
        Assert.DoesNotContain(toolManifestPath, provenance.ProducedPaths |> List.map (fun p -> p.Path))
        Assert.Equal(0, exitCodeForReport report)

    // T032 (AC3): no-clobber. An existing manifest is preserved byte-for-byte, the step reports
    // `skippedExisting` with a non-fatal advisory, and provenance claims no SDD ownership of a
    // file SDD did not write. (Reachable via `--force`, since any pre-existing non-SDD file is
    // otherwise a blocking collision, or when a provider produces the manifest itself.)
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold preserves an existing dotnet tool manifest`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let authored =
            "{\n  \"version\": 1,\n  \"isRoot\": true,\n  \"tools\": {\n    \"fake-cli\": {\n      \"version\": \"6.1.4\",\n      \"commands\": [\n        \"fake\"\n      ]\n    }\n  }\n}\n"

        TestSupport.writeRelative root toolManifestPath authored

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] true false)

        let summary = scaffoldSummary report
        Assert.Equal("skippedExisting", summary.ToolManifestOutcome)
        Assert.Contains("scaffold.toolManifestSkippedExisting", diagnosticIds report)

        // Byte-identical: the author's manifest was never rewritten, and no fsgg-sdd pin was
        // grafted into it.
        Assert.Equal(authored, TestSupport.readRelative root toolManifestPath)
        Assert.DoesNotContain("fs.gg.sdd.cli", TestSupport.readRelative root toolManifestPath)

        let provenance =
            TestSupport.readRelative root provenancePath
            |> ScaffoldProvenance.tryParse
            |> Option.defaultWith (fun () -> failwith "Expected parseable provenance.")

        Assert.Empty provenance.SddOwnedPaths

        // Advisory, never fatal (FR-010): a preserved manifest is a successful scaffold.
        Assert.Equal(0, exitCodeForReport report)

    // T033 (AC4 / FR-009): a write that cannot land must not be recorded as landed. `.config`
    // occupied by a regular file makes the manifest write throw at the edge; the step reports
    // `failed`, and provenance claims NO sdd ownership of the file that was never created.
    // Guards the ordering: provenance is planned only after the write is interpreted, so it can
    // never attest to a pin the filesystem refused.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold reports a failed tool-manifest write and claims no ownership`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"
        // `.config` as a FILE, so creating `.config/` as a directory fails.
        TestSupport.writeRelative root ".config" "occupied"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] true false)

        let summary = scaffoldSummary report
        Assert.Equal("failed", summary.ToolManifestOutcome)
        Assert.False(TestSupport.existsRelative root toolManifestPath)

        let provenance =
            TestSupport.readRelative root provenancePath
            |> ScaffoldProvenance.tryParse
            |> Option.defaultWith (fun () -> failwith "Expected parseable provenance.")

        // The whole point: no attestation to a file that does not exist.
        Assert.Empty provenance.SddOwnedPaths
        Assert.DoesNotContain(toolManifestPath, provenance.ProducedPaths |> List.map (fun p -> p.Path))

        // A failed SDD *artifact write* blocks, exactly as a failed `.fsgg/*` or provenance
        // write does — unlike the `git init` / chmod steps, which degrade over externally-owned
        // state. The interpreter's own error diagnostic carries the failure.
        Assert.NotEqual(0, exitCodeForReport report)

    // T035 (AC1 determinism): the manifest text is a pure function of the version — no clock,
    // no environment — so two runs of one CLI produce byte-identical bytes, and a different
    // version produces different bytes.
    [<Fact>]
    let ``tool manifest text is deterministic and version-addressed`` () =
        let a = HandlersScaffold.toolManifestText "1.2.3"
        let b = HandlersScaffold.toolManifestText "1.2.3"
        Assert.Equal(a, b)
        Assert.NotEqual(a, HandlersScaffold.toolManifestText "1.2.4")

        // Trailing newline; canonical two-space indent; the SDD-owned id and command only.
        Assert.EndsWith("}\n", a)
        Assert.Contains("  \"version\": 1,", a)
        Assert.Contains("    \"fs.gg.sdd.cli\": {", a)
        Assert.DoesNotContain("\r", a)

        use parsed = System.Text.Json.JsonDocument.Parse a
        Assert.Equal(1, parsed.RootElement.GetProperty("version").GetInt32())

    // T024 (US4-AC3 / FR-007): scaffold passes NO provider-specific git option to the
    // provider; the `dotnet new` create-arg vector carries no `initGit`/`allow-scripts` — SDD
    // performs the steps itself.
    [<Fact>]
    let ``scaffold passes no git options to the provider create-arg vector`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "with-script.providers.yml"

        let createArgs =
            plannedCreateArgs (scaffoldRequest root (Some "scripted") [] false true)

        for forbidden in [ "--initGit"; "initGit"; "--allow-scripts"; "allow-scripts"; "--allow-script" ] do
            Assert.DoesNotContain(forbidden, createArgs)

    // ---------- Phase 7 / cross-cutting: determinism, dry-run, failure paths ----------

    // T028 (FR-012): two identical runs in the same environment yield byte-identical
    // provenance AND report JSON; the sensed repo-init field is additive only.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provenance and json stay deterministic with the repo-init field`` () =
        let runOnce () =
            let root = rootWithLeaf "scaffold-032-det"
            writeRegistry root "ok.providers.yml"

            let report =
                runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

            TestSupport.readRelative root provenancePath, CommandSerialization.serializeReport report

        let firstProvenance, firstJson = runOnce ()
        let secondProvenance, secondJson = runOnce ()
        Assert.Equal(firstProvenance, secondProvenance)
        Assert.Equal(firstJson, secondJson)
        // The repo-init outcome is the additive sensed field; provenance never carries it.
        Assert.Contains("\"repoInitOutcome\": \"initialized\"", firstJson)
        Assert.DoesNotContain("repoInitOutcome", firstProvenance)

    // T029 (FR-008): under --dry-run nothing is performed — no repo, no chmod — and the
    // sensed fields are inert while the hint describes the planned steps.
    [<Fact>]
    let ``scaffold --dry-run performs no repo-init or chmod and stays inert`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "with-script.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "scripted") [] false true)

        let summary = scaffoldSummary report
        Assert.False(gitDirExists root)
        Assert.False(TestSupport.existsRelative root "run.sh")
        Assert.Equal("notApplicable", summary.RepoInitOutcome)
        Assert.Equal(0, summary.ExecutableScriptCount)
        Assert.Equal(0, summary.ExecutableScriptsSkipped)
        Assert.Contains("git repository", summary.NextActionHint)
        Assert.Contains("executable", summary.NextActionHint)
        // 315: the CLI pin is previewed and not written.
        Assert.Equal("notApplicable", summary.ToolManifestOutcome)
        Assert.False(TestSupport.existsRelative root toolManifestPath)
        Assert.Contains(toolManifestPath, summary.NextActionHint)

    // T030 (FR-009): on a provider failure the post-instantiation steps do not run —
    // `RepoInitOutcome = notApplicable`, no repo, and the existing failure diagnostic + exit
    // code are preserved.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider failure runs no post-instantiation steps`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "fails-midway.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        let summary = scaffoldSummary report
        Assert.Equal("notApplicable", summary.RepoInitOutcome)
        Assert.Equal(0, summary.ExecutableScriptCount)
        Assert.False(gitDirExists root)
        Assert.Contains("scaffold.providerFailed", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        // 315: an incomplete scaffold never leaves a pin claiming a reproducible toolchain.
        Assert.Equal("notApplicable", summary.ToolManifestOutcome)
        Assert.False(TestSupport.existsRelative root toolManifestPath)

    [<Fact; Trait("tier", "slow")>]
    let ``scaffold SDD-tree intrusion runs no post-instantiation steps`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "writes-into-fsgg.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        Assert.Equal("notApplicable", (scaffoldSummary report).RepoInitOutcome)
        Assert.False(gitDirExists root)
        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)

    // ===================================================================
    // 033 — the SDD skeleton's .fsgg/constitution.md rides the reused init
    // effects onto the scaffold path, but is never app-only provenance.
    // Real `dotnet new` + filesystem over the public scaffold surface.
    // ===================================================================

    /// The `generatedProduct` path set recorded in .fsgg/scaffold-provenance.json.
    let private provenanceGeneratedProductPaths root : string list =
        let json = TestSupport.readRelative root provenancePath
        use doc = System.Text.Json.JsonDocument.Parse json

        let str (element: System.Text.Json.JsonElement) (name: string) =
            match element.GetProperty(name).GetString() with
            | null -> ""
            | value -> value

        [ for entry in doc.RootElement.GetProperty("producedPaths").EnumerateArray() do
              if str entry "owner" = "generatedProduct" then
                  str entry "path" ]

    // T008 (US2-AC1 / FR-004): scaffold with lifecycle=sdd delivers the constitution
    // via the reused init skeleton; the report attributes it as the SDD skeleton's
    // authored agent-guidance artifact, not the provider's generatedProduct.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold delivers the constitution via the reused init skeleton`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        let report =
            runScaffold (
                scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false
            )

        Assert.True(TestSupport.existsRelative root ".fsgg/constitution.md")

        let change =
            report.ChangedArtifacts
            |> List.tryFind (fun c -> c.Path = ".fsgg/constitution.md")
            |> Option.defaultWith (fun () -> failwith "Expected a changed-artifact entry for the constitution.")

        Assert.Equal("agentGuidance", change.Kind)
        Assert.Equal("authored", change.Ownership)
        Assert.NotEqual("generatedProduct", change.Ownership)

    // T008 (US2-AC2 / FR-005/SC-002): the constitution is absent from the app-only
    // generatedProduct paths in scaffold-provenance.json.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provenance excludes the constitution from generatedProduct`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "sdd" ] false false)
        |> ignore

        Assert.DoesNotContain(".fsgg/constitution.md", provenanceGeneratedProductPaths root)

    // T008 (Edge / FR-004): the constitution rides the always-run init effects, so a
    // NON-sdd lifecycle param still produces it — emission is not gated on the provider's
    // lifecycle parameter.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold emits the constitution under a non-sdd lifecycle param`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        runScaffold (
            scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "lifecycle", "spec-kit" ] false false
        )
        |> ignore

        Assert.True(TestSupport.existsRelative root ".fsgg/constitution.md")
        Assert.DoesNotContain(".fsgg/constitution.md", provenanceGeneratedProductPaths root)

    // T009 (FR-006/SC-005): the init skeleton path set grew by EXACTLY the authored
    // skeleton seeds relative to the established (pre-033) skeleton — .fsgg/constitution.md
    // (033), .fsgg/early-stage-guidance.md (049), and the fs-gg-sdd-* process skill files
    // (051; 071 grew the set to 16). Asserted by removing those seeds from the current skeleton set and comparing to
    // the prior set.
    [<Fact>]
    let ``init skeleton set grew by exactly the constitution`` () =
        let initRoot = TestSupport.tempDirectory ()
        TestSupport.initializeProject initRoot
        let currentSkeleton = relativeFiles initRoot |> Set.ofList

        // The established (pre-033) skeleton: the .fsgg configs, the two agent-guidance
        // targets, and (since 032) the .git the scaffold path adds is excluded by
        // relativeFiles. init itself writes no .git, so this is the init skeleton set.
        let establishedSkeleton =
            Set.ofList
                [ ".fsgg/project.yml"
                  ".fsgg/sdd.yml"
                  ".fsgg/agents.yml"
                  "AGENTS.md"
                  "CLAUDE.md" ]

        // 051/056: the seeded process-skill files (16 declared skills × {.claude,.codex,.agents}).
        let seededSkillPaths =
            FS.GG.SDD.Commands.Internal.SeededSkills.skillNames
            |> List.collect (fun name ->
                [ $".claude/skills/{name}/SKILL.md"
                  $".codex/skills/{name}/SKILL.md"
                  $".agents/skills/{name}/SKILL.md" ])

        // 073/ADR-0018: the seeded regenerable-output `.gitignore` is an authored skeleton seed too.
        // 085: `init` also writes the dev-repo `.fsgg/scaffold-provenance.json` anchor.
        let authoredSeeds =
            Set.ofList (
                [ ".fsgg/constitution.md"
                  ".fsgg/early-stage-guidance.md"
                  ".gitignore"
                  provenancePath ]
                @ seededSkillPaths
            )

        Assert.True(Set.isSubset authoredSeeds currentSkeleton, "Expected the authored skeleton seeds in the init set.")
        Assert.Equal<Set<string>>(establishedSkeleton, Set.difference currentSkeleton authoredSeeds)

    // ===================================================================
    // 050 — honor a provider-declared default starter (value-agnostic).
    // The `fixture` provider's registry (default-declaring.providers.yml)
    // declares a NON-required `variant` with `default: alpha` alongside a
    // REQUIRED `productName`. Real `dotnet new` over abstract key/values
    // only — no rendering identity (FR-004/SC-003). US1 (default applied),
    // US2 (explicit override wins), and the precedence edge cases.
    // ===================================================================

    /// The recorded effective parameters from .fsgg/scaffold-provenance.json, as a
    /// sorted (key,value) list — the durable FR-003 audit record.
    let private provenanceEffectiveParameters root : (string * string) list =
        let json = TestSupport.readRelative root provenancePath
        use doc = System.Text.Json.JsonDocument.Parse json

        let str (element: System.Text.Json.JsonElement) (name: string) =
            match element.GetProperty(name).GetString() with
            | null -> ""
            | value -> value

        [ for entry in doc.RootElement.GetProperty("effectiveParameters").EnumerateArray() do
              str entry "key", str entry "value" ]
        |> List.sortBy fst

    // T008 (US1 / FR-001 / FR-003 / SC-001): omitting `variant` forwards the declared
    // default `alpha` to the provider verbatim, and both the report summary and provenance
    // record `variant=alpha` (plus the supplied `productName`) as effective, sorted by key.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold DefaultApplied forwards the declared default and records it effective`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "default-declaring.providers.yml"
        // productName supplied (required); variant omitted (declared default: alpha).
        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        // The provider was invoked with the declared default — the recording manifest echoes it.
        let manifest = TestSupport.readRelative root "scaffold-manifest.txt"
        Assert.Contains("variant=alpha", manifest)
        Assert.Contains("productName=Acme", manifest)

        // The report summary records the effective set, sorted ascending by key.
        let summary = scaffoldSummary report
        Assert.Equal<(string * string) list>([ "productName", "Acme"; "variant", "alpha" ], summary.EffectiveParameters)
        // Provenance records the same effective set for audit/reproducibility (FR-003).
        Assert.Equal<(string * string) list>(
            [ "productName", "Acme"; "variant", "alpha" ],
            provenanceEffectiveParameters root
        )

        Assert.Equal(0, exitCodeForReport report)

    // T009 (US1 / FR-008): the default-applied run projects effectiveParameters in json (an
    // array of {key,value} after producedPaths, sorted) and text (one sorted
    // `scaffoldEffectiveParam: key=value` line per entry), changing no other scaffold fact.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold DefaultApplied projects effectiveParameters in json and text`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "default-declaring.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        let json = CommandSerialization.serializeReport report
        // json: the array, sorted, after producedPaths and before repoInitOutcome.
        Assert.Contains("\"effectiveParameters\"", json)
        Assert.Contains("\"key\": \"productName\"", json)
        Assert.Contains("\"value\": \"Acme\"", json)
        Assert.Contains("\"key\": \"variant\"", json)
        Assert.Contains("\"value\": \"alpha\"", json)
        Assert.True(json.IndexOf "\"producedPaths\"" < json.IndexOf "\"effectiveParameters\"")
        Assert.True(json.IndexOf "\"effectiveParameters\"" < json.IndexOf "\"repoInitOutcome\"")
        Assert.True(json.IndexOf "\"key\": \"productName\"" < json.IndexOf "\"key\": \"variant\"")
        // Determinism: byte-identical across repeated emits.
        Assert.Equal(json, CommandSerialization.serializeReport report)

        // text: one sorted line per entry, after the producedPath lines.
        let text = CommandRendering.renderText report
        Assert.Contains("scaffoldEffectiveParam: productName=Acme", text)
        Assert.Contains("scaffoldEffectiveParam: variant=alpha", text)

        Assert.True(
            (text.IndexOf "scaffoldEffectiveParam: productName=Acme") < (text.IndexOf
                "scaffoldEffectiveParam: variant=alpha")
        )

    // T009 (FR-008): the effectiveParameters field is scaffold-scoped — a non-scaffold
    // command's json carries no effectiveParameters key and no scaffoldEffectiveParam line.
    [<Fact>]
    let ``effectiveParameters is absent from a non-scaffold command report`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        let workId = "050-non-scaffold"
        TestSupport.writeValidWorkSources root workId "Non scaffold"
        let refresh = TestSupport.runRefresh root workId

        let json = CommandSerialization.serializeReport refresh
        let text = CommandRendering.renderText refresh
        Assert.DoesNotContain("effectiveParameters", json)
        Assert.DoesNotContain("scaffoldEffectiveParam", text)

    // T010b(a) (Edge / precedence): a declared default never makes a REQUIRED param optional —
    // omitting required `productName` still surfaces scaffold.providerParamMissing, and the
    // blocked summary records NO effective parameters (productName absent).
    [<Fact>]
    let ``scaffold omitting a required param blocks and records no effective parameters`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "default-declaring.providers.yml"
        // productName (required) omitted; variant has a default but cannot rescue a required param.
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        Assert.Contains("scaffold.providerParamMissing", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        let summary = scaffoldSummary report
        Assert.False(summary.ProviderInvoked)
        Assert.Empty(summary.EffectiveParameters)
        Assert.DoesNotContain("productName", (summary.EffectiveParameters |> List.map fst))
        Assert.False(TestSupport.existsRelative root provenancePath)

    // T010b(c) (Edge / verbatim): the declared default is forwarded verbatim, never
    // interpreted — the planned create-arg vector carries exactly `--variant alpha`.
    [<Fact>]
    let ``scaffold forwards the declared default verbatim in the create-arg vector`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "default-declaring.providers.yml"

        let request =
            scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true
        // effective = {productName=Acme, variant=alpha}; Map canonicalizes to sorted keys.
        let expected = [ "--productName"; "Acme"; "--variant"; "alpha" ]
        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))

    // T010b(d) (Edge / no default): a provider/parameter that declares NO default and is
    // omitted forwards no key for it — the effective set holds only supplied values, and an
    // optional no-default param omitted leaves the set empty (behavior unchanged).
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold forwards no key for an omitted no-default optional parameter`` () =
        let root = TestSupport.tempDirectory ()
        // lifecycle-empty declares an OPTIONAL `lifecycle` with NO default.
        writeRegistry root "lifecycle-empty.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        let summary = scaffoldSummary report
        // No declared default + omitted ⇒ no effective entry recorded.
        Assert.Empty(summary.EffectiveParameters)
        Assert.Empty(provenanceEffectiveParameters root)
        Assert.Equal(0, exitCodeForReport report)

    // ---------- Phase 4 / US2: explicit override always wins ----------

    // T013 (US2 / FR-002 / FR-003 / SC-002): an explicit `--param variant=beta` is forwarded
    // (NOT the declared default `alpha`), and the report + provenance record `variant=beta`.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold Override forwards the author value over the declared default`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "default-declaring.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "variant", "beta" ] false false)

        // The provider was invoked with the override, not the declared default.
        let manifest = TestSupport.readRelative root "scaffold-manifest.txt"
        Assert.Contains("variant=beta", manifest)
        Assert.DoesNotContain("variant=alpha", manifest)

        let summary = scaffoldSummary report
        Assert.Equal<(string * string) list>([ "productName", "Acme"; "variant", "beta" ], summary.EffectiveParameters)

        Assert.Equal<(string * string) list>(
            [ "productName", "Acme"; "variant", "beta" ],
            provenanceEffectiveParameters root
        )

        Assert.Equal(0, exitCodeForReport report)

    // T014 (US2 / FR-008): the override run projects the override value (not the default) in
    // json and text; the effectiveParameters field stays scaffold-scoped.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold Override projects the override value in json and text`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "default-declaring.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme"; "variant", "beta" ] false false)

        let json = CommandSerialization.serializeReport report
        Assert.Contains("\"value\": \"beta\"", json)
        Assert.DoesNotContain("\"value\": \"alpha\"", json)
        let text = CommandRendering.renderText report
        Assert.Contains("scaffoldEffectiveParam: variant=beta", text)
        Assert.DoesNotContain("scaffoldEffectiveParam: variant=alpha", text)

    // ===================================================================
    // 054 — surface provider stdout/stderr/command-line/exit-code on a
    // provider-defect scaffold failure. Real `dotnet new` + real child
    // processes at the `RunProcess` edge (no mocks).
    // ===================================================================

    /// Drive the `RunProcess` edge directly with a controlled child so the bounded
    /// concurrent capture (T006) can be exercised deterministically, independent of any
    /// SDK-worded `dotnet new` output. Unix `/bin/sh` (the repo's test host).
    let private runShell (script: string) =
        let root = TestSupport.tempDirectory ()
        let result = interpret root false (RunProcess("/bin/sh", [ "-c"; script ], ""))

        result.Process
        |> Option.defaultWith (fun () -> failwith "Expected a process result.")

    // T005 (US1 / R1): the edge now returns the executed command line and the captured,
    // truncation-flagged stdout/stderr (content kept, drain retained) instead of discarding
    // them. This is the behavioral change the whole feature rests on.
    [<Fact; Trait("tier", "slow")>]
    let ``runProcess edge returns the command line and captured stdout and stderr`` () =
        let processResult = runShell "printf 'OUT-MARKER'; printf 'ERR-MARKER' >&2; exit 4"
        Assert.True(processResult.Started)
        Assert.Equal(4, processResult.ExitCode)
        Assert.Contains("/bin/sh", processResult.Command)
        Assert.Contains("ERR-MARKER", processResult.Command) // the resolved args are part of the line
        Assert.Equal("OUT-MARKER", processResult.StandardOutput)
        Assert.Equal("ERR-MARKER", processResult.StandardError)
        Assert.False(processResult.StandardOutputTruncated)
        Assert.False(processResult.StandardErrorTruncated)

    // T005 / SC-005: a stream larger than the per-stream cap is bounded and flagged, while
    // the smaller stream is untouched (drain is deadlock-safe under concurrent read).
    [<Fact; Trait("tier", "slow")>]
    let ``runProcess edge bounds an oversize stream and flags truncation`` () =
        let oversize = providerOutputCapChars + 4096

        let processResult =
            runShell $"head -c {oversize} /dev/zero | tr '\\0' 'x'; printf 'small' >&2"

        Assert.True(processResult.Started)
        Assert.Equal(providerOutputCapChars, processResult.StandardOutput.Length)
        Assert.True(processResult.StandardOutputTruncated)
        Assert.Equal("small", processResult.StandardError)
        Assert.False(processResult.StandardErrorTruncated)

    // T005 (US1 / R9): non-UTF-8 / binary bytes on a stream decode defensively (replacement
    // characters) — the edge returns without throwing and the captured text is representable
    // in valid JSON. SYNTHETIC: raw bytes emitted by `printf` stand in for a provider that
    // writes a binary blob; the real-evidence path is the same decode used for `dotnet new`.
    [<Fact; Trait("tier", "slow")>]
    let ``runProcess edge decodes binary bytes defensively and stays JSON-safe_Synthetic`` () =
        let processResult = runShell "printf '\\377\\376\\375\\000A'"
        Assert.True(processResult.Started)
        // Round-trips through System.Text.Json without throwing or corrupting the document.
        let json = System.Text.Json.JsonSerializer.Serialize(processResult.StandardOutput)
        use doc = System.Text.Json.JsonDocument.Parse json
        Assert.Equal(System.Text.Json.JsonValueKind.String, doc.RootElement.ValueKind)

    // T007 edge: a child that writes nothing to stderr and exits non-zero yields a
    // present-and-empty capture (fields emitted, not omitted).
    [<Fact; Trait("tier", "slow")>]
    let ``runProcess edge captures empty stderr on a silent nonzero exit`` () =
        let processResult = runShell "exit 7"
        Assert.True(processResult.Started)
        Assert.Equal(7, processResult.ExitCode)
        Assert.Equal("", processResult.StandardOutput)
        Assert.Equal("", processResult.StandardError)

    /// Navigate to the `scaffold.providerInvocation` element of a report's JSON, or `None`
    /// when it serialized as null.
    let private providerInvocationJson (report: CommandReport) =
        let json = CommandSerialization.serializeReport report
        let doc = System.Text.Json.JsonDocument.Parse json
        let scaffold = doc.RootElement.GetProperty("scaffold")
        let invocation = scaffold.GetProperty("providerInvocation")

        match invocation.ValueKind with
        | System.Text.Json.JsonValueKind.Null -> doc, None
        | _ -> doc, Some invocation

    // T007 (US1 / Scenario A + determinism, FR-001/002/009, SC-002): a provider defect
    // surfaces the invoked command line, exit code, and captured streams in a deterministic
    // JSON scaffold block; the outcome is still `providerFailed` at exit 2.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider failure surfaces the invocation facts deterministically`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "fails-midway.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        Assert.Equal("providerFailed", (scaffoldSummary report).Outcome)
        Assert.Equal(2, exitCodeForReport report)

        // Deterministic serialization: byte-identical across repeated emits.
        Assert.Equal(CommandSerialization.serializeReport report, CommandSerialization.serializeReport report)

        let doc, invocation = providerInvocationJson report
        use _ = doc

        let invocation =
            invocation
            |> Option.defaultWith (fun () -> failwith "Expected a providerInvocation block.")

        Assert.Contains("dotnet new fsgg-fixture-fail -o .", invocation.GetProperty("commandLine").GetString())
        Assert.True(invocation.GetProperty("processStarted").GetBoolean())
        Assert.Equal(System.Text.Json.JsonValueKind.Number, invocation.GetProperty("exitCode").ValueKind)
        Assert.NotEqual(0, invocation.GetProperty("exitCode").GetInt32())
        // Both stream fields (and their truncation flags) are always present on a defect.
        Assert.Equal(System.Text.Json.JsonValueKind.String, invocation.GetProperty("standardOutput").ValueKind)
        Assert.Equal(System.Text.Json.JsonValueKind.String, invocation.GetProperty("standardError").ValueKind)
        Assert.False(invocation.GetProperty("standardOutputTruncated").GetBoolean())
        Assert.False(invocation.GetProperty("standardErrorTruncated").GetBoolean())

    // T008 (US1 / Scenario B, SC-001): the FS.GG.SDD#35 reproduction. A template that does
    // NOT declare `productName`, invoked with `--param productName=Acme`, makes the real
    // `dotnet new` engine reject the option — its own wording is surfaced on `standardError`
    // with no PATH shim and no re-run. Assert-contains, not golden: the engine wording is SDK
    // data (R7) — a real engine message stands in for any provider's own rejection text.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold surfaces the dotnet-new invalid-option rejection on stderr_Synthetic`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "rejects-param.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Equal(2, exitCodeForReport report)
        let doc, invocation = providerInvocationJson report
        use _ = doc

        let invocation =
            invocation
            |> Option.defaultWith (fun () -> failwith "Expected a providerInvocation block.")

        let combined =
            invocation.GetProperty("standardError").GetString()
            + invocation.GetProperty("standardOutput").GetString()

        Assert.Contains("'--productName' is not a valid option", combined)

    // T009a (US1 / Scenario C, R4): a program that cannot start surfaces a real launch
    // failure at the edge — `Started = false`, the attempted command line, and the OS launch
    // error retained on `StandardError`. A truly-absent binary (real `Process.Start`
    // failure); this is the capture the handler maps to `providerUnavailable`. (A fully
    // end-to-end `providerUnavailable` is host-infeasible here: the create program is
    // `dotnet`, which resolves via the apphost even with an empty PATH — see T009b.)
    [<Fact; Trait("tier", "slow")>]
    let ``runProcess edge surfaces the launch error when the program cannot start`` () =
        let root = TestSupport.tempDirectory ()

        let result =
            interpret root false (RunProcess("/no/such/provider-binary-xyz", [ "new" ], ""))

        let processResult =
            result.Process
            |> Option.defaultWith (fun () -> failwith "Expected a process result.")

        Assert.False(processResult.Started)
        Assert.Contains("/no/such/provider-binary-xyz new", processResult.Command)
        Assert.NotEqual("", processResult.StandardError)

    // T009b (US1 / Scenario C, US1-AC3, FR-003): the report projects a never-launched
    // provider as `processStarted = false`, `exitCode = null` (never a spurious `0`), with
    // the attempted command line and the launch error preserved.
    // SYNTHETIC: the `ProcessStarted = false` invocation record stands in for a create
    // process that failed to launch — the create program is `dotnet`, which always resolves
    // on a dotnet-equipped host, so the launch-failure path cannot be driven end-to-end here.
    // The real-evidence path is the T009a edge launch failure, which produces exactly this
    // shape (`Started = false` + launch error on `StandardError`).
    [<Fact; Trait("tier", "slow")>]
    let ``provider-unavailable report projects a null exit code and the launch error_Synthetic`` () =
        // Start from a real provider-failure report, then swap in a never-launched
        // invocation record (the shape T009a produces at the edge).
        let root = TestSupport.tempDirectory ()
        writeRegistry root "fails-midway.providers.yml"
        let baseReport = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        let summary =
            { (scaffoldSummary baseReport) with
                ProviderInvocation =
                    Some
                        { CommandLine = "dotnet new fsgg-fixture-app -o . --productName Acme"
                          ProcessStarted = false // SYNTHETIC: create process failed to launch
                          ExitCode = None
                          StandardOutput = ""
                          StandardOutputTruncated = false
                          StandardError = "An error occurred trying to start process 'dotnet'."
                          StandardErrorTruncated = false } }

        let report =
            { baseReport with
                Scaffold = Some summary }

        let doc, invocation = providerInvocationJson report
        use _ = doc

        let invocation =
            invocation
            |> Option.defaultWith (fun () -> failwith "Expected a providerInvocation block.")

        Assert.False(invocation.GetProperty("processStarted").GetBoolean())
        Assert.Equal(System.Text.Json.JsonValueKind.Null, invocation.GetProperty("exitCode").ValueKind)
        Assert.Contains("dotnet new fsgg-fixture-app -o .", invocation.GetProperty("commandLine").GetString())
        Assert.Contains("start process 'dotnet'", invocation.GetProperty("standardError").GetString())

    // T010 (US1 / Scenario H, FR-010): provenance is untouched — after a provider failure it
    // still parses as schema v1 and carries no captured-output keys.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold provider failure leaves provenance at schema v1 with no captured output`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "fails-midway.providers.yml"
        runScaffold (scaffoldRequest root (Some "fixture") [] false false) |> ignore

        let provenance = TestSupport.readRelative root ".fsgg/scaffold-provenance.json"
        Assert.Contains("\"schemaVersion\": 1", provenance)

        for forbidden in [ "commandLine"; "standardOutput"; "standardError"; "providerInvocation" ] do
            Assert.DoesNotContain(forbidden, provenance)

    // T017 (US3 / Scenario E, FR-006, SC-004): success and every pre-invocation user-input
    // block carry no provider output — `providerInvocation` serializes as null.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold success and pre-invocation blocks carry a null providerInvocation`` () =
        // (a) success ⇒ null, exit 0, no captured content.
        let okRoot = TestSupport.tempDirectory ()
        writeRegistry okRoot "ok.providers.yml"

        let okReport =
            runScaffold (scaffoldRequest okRoot (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Equal(0, exitCodeForReport okReport)
        Assert.Equal(None, (scaffoldSummary okReport).ProviderInvocation)
        Assert.Contains("\"providerInvocation\": null", CommandSerialization.serializeReport okReport)

        // (b) providerMissing (no --provider) ⇒ exit 1, input diagnostic, null invocation.
        let missingRoot = TestSupport.tempDirectory ()
        writeRegistry missingRoot "ok.providers.yml"
        let missingReport = runScaffold (scaffoldRequest missingRoot None [] false false)
        Assert.Equal(1, exitCodeForReport missingReport)
        Assert.Contains("scaffold.providerMissing", diagnosticIds missingReport)
        Assert.Equal(None, (scaffoldSummary missingReport).ProviderInvocation)

        // (c) providerUnknown ⇒ exit 1, null invocation (provider never resolved/run).
        let unknownRoot = TestSupport.tempDirectory ()
        writeRegistry unknownRoot "ok.providers.yml"

        let unknownReport =
            runScaffold (scaffoldRequest unknownRoot (Some "does-not-exist") [] false false)

        Assert.Equal(1, exitCodeForReport unknownReport)
        Assert.Equal(None, (scaffoldSummary unknownReport).ProviderInvocation)

    // T017 (FR-006): dry-run plans the create but spawns nothing — no captured output.
    [<Fact>]
    let ``scaffold dry-run carries a null providerInvocation`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "ok.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Acme" ] false true)

        Assert.Equal(None, (scaffoldSummary report).ProviderInvocation)
        Assert.Contains("\"providerInvocation\": null", CommandSerialization.serializeReport report)

    // T018 (US3 / Scenario F, FR-005, SC-005): a real provider that floods a stream beyond the
    // cap is bounded in the report and flagged truncated. Driven at the edge (a `dotnet new`
    // template cannot deterministically emit > 64 KiB), asserting the same bound the report
    // surfaces.
    [<Fact; Trait("tier", "slow")>]
    let ``provider output beyond the cap is bounded and flagged in the report`` () =
        let oversize = providerOutputCapChars + 10000
        let processResult = runShell $"head -c {oversize} /dev/zero | tr '\\0' 'y' >&2"
        Assert.True(processResult.StandardError.Length <= providerOutputCapChars)
        Assert.Equal(providerOutputCapChars, processResult.StandardError.Length)
        Assert.True(processResult.StandardErrorTruncated)

    // T019 (US3 / Scenario G, edge case): an SDD-tree intrusion keeps `providerWroteSddTree`
    // as the primary diagnostic AND surfaces the invocation for consistency.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold SDD-tree intrusion still surfaces the provider invocation`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "writes-into-fsgg.providers.yml"
        let report = runScaffold (scaffoldRequest root (Some "fixture") [] false false)

        Assert.Contains("scaffold.providerWroteSddTree", diagnosticIds report)
        Assert.Equal(2, exitCodeForReport report)
        Assert.True((scaffoldSummary report).ProviderInvocation |> Option.isSome)

    // T020 (US3 / FR-007, SC-006): the exit-code taxonomy and outcome strings are unchanged —
    // success ⇒ 0, provider defect ⇒ 2, user-input ⇒ 1 — with the additive block present.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold exit-code taxonomy and outcomes are unchanged by the additive block`` () =
        let okRoot = TestSupport.tempDirectory ()
        writeRegistry okRoot "ok.providers.yml"

        let ok =
            runScaffold (scaffoldRequest okRoot (Some "fixture") [ "productName", "Acme" ] false false)

        Assert.Equal(0, exitCodeForReport ok)
        Assert.Equal("providerSucceeded", (scaffoldSummary ok).Outcome)

        let defectRoot = TestSupport.tempDirectory ()
        writeRegistry defectRoot "fails-midway.providers.yml"

        let defect =
            runScaffold (scaffoldRequest defectRoot (Some "fixture") [] false false)

        Assert.Equal(2, exitCodeForReport defect)
        Assert.Equal("providerFailed", (scaffoldSummary defect).Outcome)

        let inputRoot = TestSupport.tempDirectory ()
        writeRegistry inputRoot "ok.providers.yml"
        let input = runScaffold (scaffoldRequest inputRoot None [] false false)
        Assert.Equal(1, exitCodeForReport input)

    // #68: a wedged child (scaffold's `dotnet new`, upgrade's `dotnet tool update`, the git
    // probe) must be killed at the process-timeout bound and reported as a fail-closed nonzero
    // exit — never an indefinite hang. In the "Scaffold" collection so the tiny timeout env
    // override can never race a concurrent real `dotnet new`.
    [<Fact; Trait("tier", "slow")>]
    let ``a child exceeding the process-timeout bound is killed and reported as a nonzero exit`` () =
        if
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform
                System.Runtime.InteropServices.OSPlatform.Windows
        then
            () // POSIX `sleep` is the portable long-runner; the timeout edge itself is OS-agnostic.
        else
            let root = TestSupport.tempDirectory ()

            let original =
                System.Environment.GetEnvironmentVariable "FSGG_SDD_PROCESS_TIMEOUT_MS"

            try
                System.Environment.SetEnvironmentVariable("FSGG_SDD_PROCESS_TIMEOUT_MS", "500")
                let stopwatch = System.Diagnostics.Stopwatch.StartNew()
                let result = interpret root false (RunProcess("sleep", [ "30" ], ""))
                stopwatch.Stop()

                // Bounded well within the child's own 30s sleep — killed, not awaited.
                Assert.True(stopwatch.Elapsed.TotalSeconds < 15.0, $"kill took {stopwatch.Elapsed.TotalSeconds}s")

                let processResult =
                    result.Process
                    |> Option.defaultWith (fun () -> failwith "expected a process result")

                Assert.True processResult.Started
                Assert.NotEqual(0, processResult.ExitCode)
                Assert.Contains("timed out", processResult.StandardError)
            finally
                System.Environment.SetEnvironmentVariable("FSGG_SDD_PROCESS_TIMEOUT_MS", original)

    // §3 (post-kill reap must be bounded): killing a timed-out child does not close a redirected
    // pipe that a grandchild escaping the tree-kill still holds open, so the reader drains never
    // reach EOF. An un-timed reap (`WaitForExit()` / `GetResult()`) would relocate the very hang the
    // timeout exists to prevent from before the kill to after it. The child is killed at the tiny
    // bound, the reparented grandchild survives holding the write ends, and `runProcess` must still
    // return promptly with the fail-closed timeout result rather than waiting the grandchild out.
    [<Fact; Trait("tier", "slow")>]
    let ``a timed-out child whose grandchild still holds the pipes is reaped bounded, not waited out`` () =
        if
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform
                System.Runtime.InteropServices.OSPlatform.Windows
        then
            () // The reparent-to-init escape is POSIX; the bounded-reap edge itself is OS-agnostic.
        else
            let root = TestSupport.tempDirectory ()

            let original =
                System.Environment.GetEnvironmentVariable "FSGG_SDD_PROCESS_TIMEOUT_MS"

            try
                System.Environment.SetEnvironmentVariable("FSGG_SDD_PROCESS_TIMEOUT_MS", "500")

                // `(sleep 30 &)` backgrounds a sleep inside a subshell that exits at once, reparenting
                // it to init (ppid 1) so it escapes `Kill(entireProcessTree=true)`; it inherits — and
                // holds open — the child's redirected stdout+stderr. The trailing `sleep 30` keeps the
                // direct child alive past the 500 ms bound so the kill path fires.
                let script = "(sleep 30 &); sleep 30"

                let stopwatch = System.Diagnostics.Stopwatch.StartNew()
                let result = interpret root false (RunProcess("sh", [ "-c"; script ], ""))
                stopwatch.Stop()

                // Unbounded, the reap would wait the ~30 s grandchild out (or hang the whole run).
                // Bounded, it returns within the reap budget plus the kill — comfortably under.
                Assert.True(
                    stopwatch.Elapsed.TotalSeconds < 20.0,
                    $"expected a bounded reap; took {stopwatch.Elapsed.TotalSeconds}s"
                )

                let processResult =
                    result.Process
                    |> Option.defaultWith (fun () -> failwith "expected a process result")

                Assert.True processResult.Started
                Assert.Equal(124, processResult.ExitCode)
                Assert.Contains("timed out", processResult.StandardError)
            finally
                System.Environment.SetEnvironmentVariable("FSGG_SDD_PROCESS_TIMEOUT_MS", original)

    // ---------- Feature 080 / US1,US2,US4: name → valid F# identifier ----------

    // T012 (US1 / FR-005): with a provider declaring `identifierParameter`, a hyphenated
    // product name forwards BOTH the raw `--productName Roquelike-DungeonCrawler` (verbatim)
    // and the SDD-derived `--rootNamespace RoquelikeDungeonCrawler`.
    [<Fact>]
    let ``scaffold derives and forwards the identifier parameter alongside the raw name`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "identifier-declaring.providers.yml"

        let request =
            scaffoldRequest root (Some "fixture") [ "productName", "Roquelike-DungeonCrawler" ] false true

        // Map canonicalizes to sorted keys: productName then rootNamespace.
        let expected =
            [ "--productName"
              "Roquelike-DungeonCrawler"
              "--rootNamespace"
              "RoquelikeDungeonCrawler" ]

        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))

    // T012 (US1 / FR-003): a product name already valid as a namespace forwards the identifier
    // unchanged (derivation is a no-op).
    [<Fact>]
    let ``scaffold forwards an already-valid name unchanged as the identifier`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "identifier-declaring.providers.yml"

        let request =
            scaffoldRequest root (Some "fixture") [ "productName", "Acme.Foo" ] false true

        let expected = [ "--productName"; "Acme.Foo"; "--rootNamespace"; "Acme.Foo" ]
        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))

    // T014 (US2 / FR-006 / FR-007): a real run keeps the raw name verbatim in string contexts
    // (the produced Program.fs string literal + manifest), puts the derived valid identifier in
    // the namespace context, and records BOTH in provenance — schema unchanged.
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold real run preserves the raw name and uses the derived namespace`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "identifier-declaring.providers.yml"

        let report =
            runScaffold (
                scaffoldRequest root (Some "fixture") [ "productName", "Roquelike-DungeonCrawler" ] false false
            )

        Assert.Equal("providerSucceeded", (scaffoldSummary report).Outcome)

        // Identifier context: the produced namespace is the derived, valid form.
        let program = TestSupport.readRelative root "Program.fs"
        Assert.Contains("namespace RoquelikeDungeonCrawler", program)
        // String context: the raw hyphenated name is preserved verbatim.
        Assert.Contains("printfn \"Roquelike-DungeonCrawler\"", program)

        let manifest = TestSupport.readRelative root "scaffold-manifest.txt"
        Assert.Contains("productName=Roquelike-DungeonCrawler", manifest)
        Assert.Contains("rootNamespace=RoquelikeDungeonCrawler", manifest)

        // Both values recorded effective (sorted by key), raw name byte-identical.
        Assert.Equal<(string * string) list>(
            [ "productName", "Roquelike-DungeonCrawler"
              "rootNamespace", "RoquelikeDungeonCrawler" ],
            provenanceEffectiveParameters root
        )

    // T015 (US2 / FR-007): the recorded provenance schema stays v1 (additive rows only).
    [<Fact; Trait("tier", "slow")>]
    let ``scaffold identifier derivation keeps provenance schema at 1`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "identifier-declaring.providers.yml"

        runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "Roquelike-DungeonCrawler" ] false false)
        |> ignore

        let json = TestSupport.readRelative root provenancePath
        use doc = System.Text.Json.JsonDocument.Parse json
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32())

    // T016 (US4 / FR-008): an author `--param rootNamespace=...` override wins — SDD does not
    // derive over an explicitly supplied sink value.
    [<Fact>]
    let ``scaffold lets an author identifier override win over derivation`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "identifier-declaring.providers.yml"

        let request =
            scaffoldRequest
                root
                (Some "fixture")
                [ "productName", "Roquelike-DungeonCrawler"; "rootNamespace", "Explicit" ]
                false
                true

        let expected =
            [ "--productName"; "Roquelike-DungeonCrawler"; "--rootNamespace"; "Explicit" ]

        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))

    // T016 (US4 / FR-009): a name with no identifier character blocks with
    // scaffold.nameUnrepresentable at exit 1, before any provider invocation.
    [<Fact>]
    let ``scaffold blocks an unrepresentable name before invoking the provider`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "identifier-declaring.providers.yml"

        let report =
            runScaffold (scaffoldRequest root (Some "fixture") [ "productName", "---" ] false false)

        Assert.Contains("scaffold.nameUnrepresentable", diagnosticIds report)
        Assert.Equal(1, exitCodeForReport report)
        Assert.False((scaffoldSummary report).ProviderInvoked)

    // Feature 080 (misconfiguration guard): a provider that declares the sink equal to the
    // name key must NOT have the raw name silently overwritten by the derived identifier.
    [<Fact>]
    let ``scaffold does not overwrite the raw name when the sink equals the name key`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "identifier-equals-name.providers.yml"

        let request =
            scaffoldRequest root (Some "fixture") [ "productName", "Roquelike-DungeonCrawler" ] false true

        // Only the raw name is forwarded, verbatim — no derivation overwrites it.
        let expected = [ "--productName"; "Roquelike-DungeonCrawler" ]
        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))

    // Feature 080 backward compat: a provider that declares NO identifierParameter forwards
    // exactly as before — no derived param injected.
    [<Fact>]
    let ``scaffold without an identifier parameter forwards only the raw name`` () =
        let root = TestSupport.tempDirectory ()
        writeRegistry root "lifecycle.providers.yml"

        let request =
            scaffoldRequest root (Some "fixture") [ "productName", "Roquelike-DungeonCrawler" ] false true

        let expected = [ "--productName"; "Roquelike-DungeonCrawler" ]
        Assert.Equal<string list>(expected, forwardedParamArgs (plannedCreateArgs request))
