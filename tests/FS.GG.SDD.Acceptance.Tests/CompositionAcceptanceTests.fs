namespace FS.GG.SDD.Acceptance.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open Xunit
open AcceptanceSupport
open CompositionResult

/// The real-provider composition acceptance. The network-gated facts are tagged
/// `[<Trait("kind","composition-acceptance")>]` and self-skip via the registry guard when
/// `FSGG_SDD_ACCEPTANCE_REGISTRY` is unset; the offline facts (verdict mapping, result-schema
/// golden, env-gate proof, config-error mapping, the no-provider guards) always run and keep the
/// inner loop honest. This file carries no rendering package id / template id / path / docs URL
/// (FR-009): the real provider identity lives only in the external registry.
module CompositionAcceptanceTests =

    // The SDD skeleton (reused `init` effects) + the authored constitution — never provider
    // output. Used to prove the provenance partition (FR-005).
    let private skeletonPaths =
        set
            [ ".fsgg/project.yml"
              ".fsgg/sdd.yml"
              ".fsgg/agents.yml"
              "AGENTS.md"
              "CLAUDE.md"
              ".fsgg/constitution.md" ]

    let private gitDirExists root = Directory.Exists(Path.Combine(root, ".git"))

    let private isExecutable root (relativePath: string) =
        let mode = File.GetUnixFileMode(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)))
        mode &&& UnixFileMode.UserExecute = UnixFileMode.UserExecute

    let private provenanceRecord root =
        if existsRelative root ".fsgg/scaffold-provenance.json" then
            ScaffoldProvenance.tryParse (readRelative root ".fsgg/scaffold-provenance.json")
        else
            None

    let private resultPath root =
        Environment.GetEnvironmentVariable resultPathEnvVar
        |> Option.ofObj
        |> Option.map (fun value -> value.Trim())
        |> Option.filter (fun value -> value <> "")
        |> Option.defaultValue (Path.Combine(root, "composition-acceptance.json"))

    // ===================================================================
    // The orchestration spine (T010): run the real composition once, assert every fact, emit
    // the deterministic result document, return the record. Every US1/US2 fact hangs off this.
    // ===================================================================

    let private composeOnce () =
        let registry = requireRegistry ()
        let root = newProductRoot ()
        copyRegistry registry root

        let report = runScaffold root
        let summary = scaffoldSummary report
        let outcome = summary.Outcome
        let diagnostic = scaffoldDiagnostic report

        // The probes and fact assertions only matter on a clean success outcome; a non-success
        // outcome resolves its verdict from the (outcome, diagnostic) pair alone.
        let facts, factDiagnostic, sensedTemplate =
            if outcome = "providerSucceeded" then
                let provenance = provenanceRecord root
                let producedPaths =
                    provenance |> Option.map (fun record -> record.ProducedPaths) |> Option.defaultValue []

                // T011 (FR-002): skeleton + authored constitution present (not provider output).
                let skeletonPresent =
                    existsRelative root ".fsgg/project.yml" && existsRelative root ".fsgg/sdd.yml"
                let constitutionPresent = existsRelative root ".fsgg/constitution.md"

                // T012 (FR-003): the product builds, then a headless run smoke starts it without
                // crashing — distinguishing "files produced" from a working product.
                let build = buildProbe None root
                let appBuilds = build.ExitCode = 0

                let run =
                    if appBuilds then
                        runProbe None root
                    else
                        { Started = false; ExitCode = -1; Diagnostic = "build failed; run probe skipped." }

                let appRuns = run.Started && run.ExitCode = 0

                // T013 (FR-004): a repo exists OR repo-init was explicitly skipped-non-fatal, and
                // every produced `.sh` is executable (or none were produced).
                let gitInitialized =
                    [ "initialized"; "skippedExistingRepository"; "skippedGitUnavailable" ]
                    |> List.contains summary.RepoInitOutcome

                let producedScripts =
                    producedPaths |> List.map (fun path -> path.Path) |> List.filter (fun path -> path.EndsWith ".sh")

                let scriptsExecutable =
                    summary.ExecutableScriptsSkipped = 0 && producedScripts |> List.forall (isExecutable root)

                // T016 (FR-005): every provider-produced path is generatedProduct; no
                // skeleton/constitution path is recorded as generatedProduct.
                let provenancePartitioned =
                    match provenance with
                    | Some record ->
                        record.ProducedPaths |> List.forall (fun path -> path.Owner = ArtifactOwner.GeneratedProduct)
                        && record.ProducedPaths |> List.forall (fun path -> not (Set.contains path.Path skeletonPaths))
                    | None -> false

                // T017 (FR-006): refresh leaves the externally-owned app paths byte-unchanged.
                let appPaths = producedPaths |> List.map (fun path -> path.Path)
                let before = appPaths |> List.map (fun path -> path, fileDigest root path)
                writeValidWorkSources root "034-composition-acceptance" "Composition acceptance"
                runRefresh root "034-composition-acceptance" |> ignore
                let after = appPaths |> List.map (fun path -> path, fileDigest root path)
                let refreshExcludes = before = after

                // T014 (FR-007): the scaffold reported the success outcome marked complete.
                let reportedComplete = outcome = "providerSucceeded"

                let factDiagnostic =
                    if not appBuilds then build.Diagnostic
                    elif not appRuns then run.Diagnostic
                    else ""

                let facts =
                    { SkeletonPresent = skeletonPresent
                      ConstitutionPresent = constitutionPresent
                      AppBuilds = appBuilds
                      AppRuns = appRuns
                      GitInitialized = gitInitialized
                      ScriptsExecutable = scriptsExecutable
                      ProvenancePartitioned = provenancePartitioned
                      RefreshExcludes = refreshExcludes
                      ReportedComplete = reportedComplete }

                facts, factDiagnostic, (provenance |> Option.map (fun record -> record.ProviderContractVersion))
            else
                noFacts, "", None

        let sensed =
            { ResolvedTemplateVersion = sensedTemplate
              ProviderAvailable = Some(diagnostic <> Some "scaffold.providerUnavailable")
              Host = Some Environment.MachineName
              Timestamp = Some(DateTime.UtcNow.ToString("o")) }

        let record = makeRecord outcome diagnostic factDiagnostic facts sensed
        write (resultPath root) record
        record

    // ---------- US1 / US2: the green end-to-end PASS (network-gated) ----------

    // T010–T017 (US1 + US2): one invocation yields the runnable app AND the SDD skeleton +
    // authored constitution; the app builds and runs; git+chmod ran; provenance is partitioned;
    // refresh excludes the app paths; and the run is reported complete only if every part held.
    // The verdict resolves the whole composition; a Fail names the first failing fact.
    [<Trait("kind", "composition-acceptance")>]
    [<RequiresRegistryFact>]
    let ``real rendering composition is coherent end to end`` () =
        let record = composeOnce ()

        match record.Verdict with
        | Pass -> () // every P1/P2 fact true; the result document was emitted.
        | SkipUnavailable ->
            // SC-004 forbids failing SDD when the provider is merely unreachable. The honest
            // `skip-unavailable` verdict is recorded in the emitted result document; the fact is
            // not failed (and the adapter does not convert a mid-run dynamic skip).
            ()
        | Fail reason ->
            Assert.Fail
                $"Composition acceptance failed: %A{reason} (outcome={record.ScaffoldOutcome}, diagnostic=%A{record.ScaffoldDiagnostic})."

    // ---------- US3: opt-in, provider-neutral, honest SKIP/FAIL ----------

    // T018 (US3 / SC-003): with the registry env unset, the guard SKIPs (xUnit dynamic skip), so
    // the offline inner loop stays green and no result document is written. Offline.
    [<Fact>]
    let ``env-unset registry skips with no document`` () =
        let original = Environment.GetEnvironmentVariable registryEnvVar

        try
            Environment.SetEnvironmentVariable(registryEnvVar, null)

            let skipped =
                try
                    requireRegistry () |> ignore
                    false
                with ex ->
                    ex.GetType().Name.Contains "Skip"

            Assert.True(skipped, "Expected requireRegistry to skip when the registry env is unset.")
        finally
            Environment.SetEnvironmentVariable(registryEnvVar, original)

    // T019 (US3 / FR-008 / SC-004): a real run whose provider invocation cannot start surfaces
    // `providerFailed` + `scaffold.providerUnavailable`; the acceptance reads the DIAGNOSTIC code
    // (not the overloaded outcome) and maps it to SKIP — never a false PASS, never a FAIL of SDD.
    // Forced deterministically by stripping `dotnet` from the child PATH (real-path exercise).
    [<Trait("kind", "composition-acceptance")>]
    [<RequiresRegistryFact>]
    let ``unavailable provider resolves to skip-unavailable`` () =
        let registry = requireRegistry ()
        let root = newProductRoot ()
        copyRegistry registry root

        let emptyDir = Path.Combine(Path.GetTempPath(), "fsgg-sdd-nodotnet-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory emptyDir |> ignore
        let original = Environment.GetEnvironmentVariable "PATH"

        let report =
            try
                Environment.SetEnvironmentVariable("PATH", emptyDir)
                runScaffold root
            finally
                Environment.SetEnvironmentVariable("PATH", original)

        // When the invocation could not start, the outcome is the overloaded `providerFailed`
        // and the DIAGNOSTIC code is the discriminator that maps it to SKIP — never a false PASS,
        // never a FAIL of SDD (FR-008 / SC-004). Asserting the mapping on the real path is the
        // verification. Forcing `Started = false` is environment-dependent (some hosts resolve
        // `dotnet` without PATH); when it cannot be forced here, the mapping is still proven
        // deterministically offline by the verdict-resolution unit test (T008), so this best-effort
        // real exercise is a no-op rather than a false failure.
        let diagnostic = scaffoldDiagnostic report

        if diagnostic = Some "scaffold.providerUnavailable" then
            match resolveVerdict (scaffoldSummary report).Outcome diagnostic "" noFacts with
            | SkipUnavailable -> ()
            | other -> Assert.Fail $"Expected skip-unavailable, got %A{other}."

    // T020 (US3 / FR-009): an omitted registry cannot resolve the provider, so SDD blocks
    // pre-invocation (`providerNotRun`) → config-error FAIL, never a silent fallback to a fixture
    // or an embedded identifier. Offline — no network, no real provider invoked.
    [<Fact>]
    let ``omitted registry maps to a config-error fail`` () =
        let root = newProductRoot ()
        let report = runScaffold root
        let summary = scaffoldSummary report

        Assert.Equal("providerNotRun", summary.Outcome)

        match resolveVerdict summary.Outcome (scaffoldDiagnostic report) "" noFacts with
        | Fail(ConfigError _) -> ()
        | other -> Assert.Fail $"Expected a config-error fail, got %A{other}."

        Assert.False(existsRelative root "composition-acceptance.json")

    // T021a (US3 / FR-012, finding F5): the acceptance project carries NO Governance reference —
    // neither in the project file nor in its non-guard sources — proving it requires no
    // Governance runtime and computes no Governance verdict. Negative invariant; offline.
    [<Fact>]
    let ``acceptance project carries no Governance reference`` () =
        let token = "FS.GG." + "Governance"
        let symbol = "Govern" + "ance"
        let projectDir = Path.Combine(repoRoot, "tests", "FS.GG.SDD.Acceptance.Tests")

        // This guard file names the token, so (like ScaffoldGuardTests) it is excluded from its
        // own scan; the meaningful surfaces are the project file and the non-guard sources.
        let scanned =
            [ "AcceptanceSupport.fs"; "CompositionResult.fs"; "FS.GG.SDD.Acceptance.Tests.fsproj" ]
            |> List.map (fun name -> Path.Combine(projectDir, name))
            |> List.filter File.Exists

        let offenders =
            scanned
            |> List.filter (fun path ->
                let text = File.ReadAllText path
                text.Contains(token, StringComparison.OrdinalIgnoreCase)
                || text.Contains(symbol, StringComparison.Ordinal))

        Assert.True(
            List.isEmpty offenders,
            "Acceptance project must carry no Governance reference: " + String.Join("; ", offenders))

    // T024 (Polish / SC-005): two real runs with the same inputs and an available provider yield
    // byte-identical result-document bodies once the sensed block is null-normalized.
    [<Trait("kind", "composition-acceptance")>]
    [<RequiresRegistryFact>]
    let ``two runs with the same inputs yield byte-identical bodies`` () =
        let first = composeOnce ()
        let second = composeOnce ()

        // The deterministic body (everything but the null-normalized sensed block) is
        // byte-identical across same-input runs — including two equally-unavailable runs.
        let body record = serialize (normalizeSensed record)
        Assert.Equal(body first, body second)

    // ===================================================================
    // Offline result-schema contract: the verdict-mapping unit test (T008) and the byte-exact
    // golden (T009). These baseline the new `composition-acceptance-result` v1 contract — the
    // "moved contract" this Tier-1 feature introduces (Principle III), fixed by its own golden.
    // ===================================================================

    let private allTrueFacts =
        { SkeletonPresent = true
          ConstitutionPresent = true
          AppBuilds = true
          AppRuns = true
          GitInitialized = true
          ScriptsExecutable = true
          ProvenancePartitioned = true
          RefreshExcludes = true
          ReportedComplete = true }

    // T008: drive verdict resolution with synthetic (outcome, diagnostic) pairs and assert each
    // branch — unavailable→skip, wrote-SDD-tree→fail(defect), non-zero-exit→fail(defect),
    // providerNotRun→fail(config), empty→fail(incomplete), success+all-facts→pass,
    // success+fact-false→fail(first failing fact). Purely offline (finding F3).
    [<Fact>]
    let ``verdict resolution keys on the outcome+diagnostic pair`` () =
        Assert.Equal(SkipUnavailable, resolveVerdict "providerFailed" (Some "scaffold.providerUnavailable") "" noFacts)

        match resolveVerdict "providerFailed" (Some "scaffold.providerWroteSddTree") "" noFacts with
        | Fail(ProviderDefect "scaffold.providerWroteSddTree") -> ()
        | other -> Assert.Fail $"wrote-SDD-tree should be a provider defect, got %A{other}."

        match resolveVerdict "providerFailed" (Some "scaffold.providerFailed") "" noFacts with
        | Fail(ProviderDefect "scaffold.providerFailed") -> ()
        | other -> Assert.Fail $"non-zero exit should be a provider defect, got %A{other}."

        match resolveVerdict "providerNotRun" (Some "scaffold.providerUnknown") "" noFacts with
        | Fail(ConfigError "scaffold.providerUnknown") -> ()
        | other -> Assert.Fail $"providerNotRun should be a config error, got %A{other}."

        match resolveVerdict "providerSucceededEmpty" (Some "scaffold.providerEmpty") "" noFacts with
        | Fail(Incomplete "scaffold.providerEmpty") -> ()
        | other -> Assert.Fail $"empty should be incomplete, got %A{other}."

        Assert.Equal(Pass, resolveVerdict "providerSucceeded" None "" allTrueFacts)

        match resolveVerdict "providerSucceeded" None "build failed" { allTrueFacts with AppBuilds = false } with
        | Fail(FactFailed("appBuilds", "build failed")) -> ()
        | other -> Assert.Fail $"a false fact should fail naming the first failing fact, got %A{other}."

    // T008 companion: SKIP and FAIL are distinct — `providerFailed` alone is insufficient, the
    // diagnostic code is the discriminator (the SC-004 collapse this guards against).
    [<Fact>]
    let ``providerFailed splits into skip and fail by diagnostic`` () =
        let skip = resolveVerdict "providerFailed" (Some "scaffold.providerUnavailable") "" noFacts
        let fail = resolveVerdict "providerFailed" (Some "scaffold.providerWroteSddTree") "" noFacts
        Assert.Equal("skip-unavailable", verdictValue skip)
        Assert.Equal("fail", verdictValue fail)
        Assert.NotEqual<Verdict>(skip, fail)

    // T009: a fully-populated pass result serializes byte-exact with the sensed block normalized
    // to null, and two synthetic same-input bodies are byte-identical (SC-005). Offline.
    let private expectedGolden =
        """{
  "schemaVersion": 1,
  "generator": {
    "id": "fsgg-sdd-composition-acceptance",
    "version": "1.0.0"
  },
  "verdict": "pass",
  "inputs": {
    "provider": "rendering",
    "params": {
      "lifecycle": "sdd"
    }
  },
  "scaffoldOutcome": "providerSucceeded",
  "scaffoldDiagnostic": null,
  "facts": {
    "skeletonPresent": true,
    "constitutionPresent": true,
    "appBuilds": true,
    "appRuns": true,
    "gitInitialized": true,
    "scriptsExecutable": true,
    "provenancePartitioned": true,
    "refreshExcludes": true,
    "reportedComplete": true
  },
  "failure": null,
  "sensed": {
    "resolvedTemplateVersion": null,
    "providerAvailable": null,
    "host": null,
    "timestamp": null
  }
}"""

    [<Fact>]
    let ``result schema golden is byte-exact with the sensed block normalized`` () =
        let sensed =
            { ResolvedTemplateVersion = Some "9.9.9"
              ProviderAvailable = Some true
              Host = Some "some-host"
              Timestamp = Some "2026-06-28T00:00:00Z" }

        let record = makeRecord "providerSucceeded" None "" allTrueFacts sensed
        let golden = serialize (normalizeSensed record)

        Assert.Equal(expectedGolden, golden)
        // Determinism: a second same-input record produces a byte-identical body.
        let other = makeRecord "providerSucceeded" None "" allTrueFacts nullSensed
        Assert.Equal(golden, serialize (normalizeSensed other))
