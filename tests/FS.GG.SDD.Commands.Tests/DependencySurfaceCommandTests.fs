namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Artifacts.DependencySurface
open Xunit

/// `fsgg-sdd dependency-surface` command tests (feature 105, Phase 2; ADR-0004 D2). Real-filesystem
/// fixtures over a genuinely restored package: the harness runs the real edge interpreter, so
/// `ReadPackageSurface` reflects `Spectre.Console` from the nuget cache (a pinned dependency of this
/// repo, so it is always restored in the inner loop). `--check` blocks on drift; `--update`
/// refreshes/creates captures; an uncached package is advisory, never a false drift.
module DependencySurfaceCommandTests =
    open TestSupport

    // A package this repo restores, so its real surface is always readable in the inner loop.
    let private restoredPackage = "Spectre.Console"
    let private restoredVersion = "0.57.2"

    let private targetId packageId version = $"{packageId}@{version}"

    let private depSurfaceRequest update root parameters =
        { request DependencySurface root with
            SurfaceUpdate = update
            Parameters = parameters }
        |> runRequest

    let private summaryOf (report: CommandReport) =
        match report.DependencySurface with
        | Some summary -> summary
        | None -> failwith "expected a dependency-surface summary"

    let private targetParams =
        [ "packageId", restoredPackage; "version", restoredVersion ]

    let private capturePathRel =
        capturePath defaultBaselineRoot restoredPackage restoredVersion

    // --update a fresh workspace: read the real surface and write a canonical capture.
    let private capturedFixture () =
        let root = tempDirectory ()
        depSurfaceRequest true root targetParams |> ignore
        root

    // ---- US1: --update captures the real restored surface --------------------------------

    [<Fact>]
    let ``update reads the real surface, writes a capture, and exits 0`` () =
        let root = tempDirectory ()
        let report = depSurfaceRequest true root targetParams
        let summary = summaryOf report

        Assert.Equal("update", summary.Mode)
        Assert.Equal(1, summary.CheckedCount)
        Assert.True(existsRelative root capturePathRel)

        let entry = List.exactlyOne summary.Entries
        Assert.Equal("written", entry.Status)
        Assert.True(entry.ObservedSymbolCount > 0)
        Assert.Contains(targetId restoredPackage restoredVersion, summary.UpdatedPackages)
        Assert.Equal(0, exitCodeForReport report)

    [<Fact>]
    let ``the written capture parses and is content-addressed`` () =
        let root = capturedFixture ()
        let text = readRelative root capturePathRel

        match tryParse text with
        | Ok capture ->
            Assert.Equal(restoredPackage, capture.PackageId)
            Assert.Equal(restoredVersion, capture.Version)
            Assert.Equal(symbolDigest capture.Symbols, capture.Sha256)
            Assert.NotEmpty capture.Symbols
        | Error message -> Assert.Fail $"expected a valid capture, got: {message}"

    // ---- US2: --check matches a fresh capture, blocks on drift ---------------------------

    [<Fact>]
    let ``check on a fresh capture reports matched and exits 0`` () =
        let root = capturedFixture ()
        let report = depSurfaceRequest false root []
        let summary = summaryOf report

        Assert.Equal("check", summary.Mode)
        Assert.True summary.IsCoherent
        Assert.Empty summary.DriftedPackages
        Assert.Equal("matched", (List.exactlyOne summary.Entries).Status)
        Assert.Equal(0, exitCodeForReport report)

    [<Fact>]
    let ``check blocks when the committed digest disagrees with the real surface`` () =
        let root = capturedFixture ()

        // Simulate a stale capture: the recorded digest no longer matches the real surface.
        let stale =
            { create restoredPackage restoredVersion "nuget-cache" [ "Stale.only" ] with
                Sha256 = String.replicate 64 "0" }

        writeRelative root capturePathRel (serialize stale)

        let report = depSurfaceRequest false root []
        let summary = summaryOf report

        Assert.False summary.IsCoherent
        Assert.Contains(targetId restoredPackage restoredVersion, summary.DriftedPackages)
        Assert.Equal("drifted", (List.exactlyOne summary.Entries).Status)
        Assert.Contains(report.Diagnostics, fun d -> d.Id = "dependencySurface.drift")
        Assert.Equal(1, exitCodeForReport report)

    [<Fact>]
    let ``update reconciles a drifted capture rather than blocking`` () =
        let root = capturedFixture ()

        let stale =
            { create restoredPackage restoredVersion "nuget-cache" [ "Stale.only" ] with
                Sha256 = String.replicate 64 "0" }

        writeRelative root capturePathRel (serialize stale)

        let report = depSurfaceRequest true root []
        let summary = summaryOf report

        Assert.True summary.IsCoherent
        Assert.Empty summary.DriftedPackages
        Assert.Contains(targetId restoredPackage restoredVersion, summary.UpdatedPackages)
        Assert.Equal(0, exitCodeForReport report)

    // ---- US3: an unreadable surface is advisory, never a false drift ---------------------

    [<Fact>]
    let ``check on an uncached package is advisory and exits 0`` () =
        let root = tempDirectory ()

        let orphan = create "No.Such.Package.Fsgg" "9.9.9" "nuget-cache" [ "Ghost.member" ]

        writeRelative root (capturePath defaultBaselineRoot "No.Such.Package.Fsgg" "9.9.9") (serialize orphan)

        let report = depSurfaceRequest false root []
        let summary = summaryOf report

        Assert.True summary.IsCoherent // unavailable does not break coherence
        Assert.Empty summary.DriftedPackages
        Assert.Contains(targetId "No.Such.Package.Fsgg" "9.9.9", summary.UnavailablePackages)
        Assert.Equal("unavailable", (List.exactlyOne summary.Entries).Status)
        Assert.Contains(report.Diagnostics, fun d -> d.Id = "dependencySurface.unavailable")
        Assert.Equal(0, exitCodeForReport report)

    // ---- Containment (FS.GG.SDD#185 discipline) ------------------------------------------

    [<Fact>]
    let ``an escaping baselineRoot blocks and plans no write`` () =
        let root = tempDirectory ()

        let report =
            depSurfaceRequest
                true
                root
                [ "baselineRoot", "/etc"
                  "packageId", restoredPackage
                  "version", restoredVersion ]

        Assert.Contains(report.Diagnostics, fun d -> d.Id = "dependencySurface.rootEscape")
        Assert.Equal(1, exitCodeForReport report)
        // No capture was written anywhere under the workspace.
        Assert.False(existsRelative root capturePathRel)

    // ---- Feature 109: generated-consumer bootstrap and drift lifecycle -----------------

    [<Fact>]
    let ``authored framework references bootstrap a real capture and make missing capture drift visible`` () =
        let root = tempDirectory ()
        let workId = "109-dependency-surface-consumer"
        let title = "Dependency Surface Consumer"
        initializeTasksReadyProject root workId title

        // Read the genuinely restored package once to select a real, grammar-safe symbol for the
        // end-to-end assertion. Delete that explicit seed: the command under test must rediscover
        // the target from the generated product's authored plan, starting with no capture.
        depSurfaceRequest true root targetParams |> ignore

        let realSymbol =
            match tryParse (readRelative root capturePathRel) with
            | Ok capture ->
                capture.Symbols
                |> List.find (fun symbol -> symbol |> Seq.forall (System.Char.IsWhiteSpace >> not))
            | Error message -> failwith $"expected seed capture: {message}"

        System.IO.File.Delete(System.IO.Path.Combine(root, capturePathRel))

        let planPath = $"work/{workId}/plan.md"

        let withFrameworkReference =
            (readRelative root planPath)
                .Replace(
                    "## Contract Impact\n",
                    $"## Contract Impact\n- framework: {restoredPackage}@{restoredVersion}#{realSymbol} — generated-consumer use.\n"
                )

        writeRelative root planPath withFrameworkReference

        let updateReport = depSurfaceRequest true root []
        let updateSummary = summaryOf updateReport
        Assert.True(existsRelative root capturePathRel)
        Assert.Contains(targetId restoredPackage restoredVersion, updateSummary.UpdatedPackages)

        let presentReport = runAnalyze root workId title
        Assert.DoesNotContain(presentReport.Diagnostics, fun d -> d.Id = "frameworkApiSurfaceUnavailable")
        Assert.DoesNotContain(presentReport.Diagnostics, fun d -> d.Id = "frameworkApiDangling")

        writeRelative root planPath (withFrameworkReference.Replace(realSymbol, "Definitely.Missing.Symbol"))
        let missingReport = runAnalyze root workId title
        Assert.Contains(missingReport.Diagnostics, fun d -> d.Id = "frameworkApiDangling")

        // A clean-checkout/pin-refresh hole is not invisible: if the authored target is readable but
        // its capture is absent, parameter-free --check blocks and names it. --update repairs it.
        System.IO.File.Delete(System.IO.Path.Combine(root, capturePathRel))
        let checkReport = depSurfaceRequest false root []
        let checkSummary = summaryOf checkReport
        Assert.Contains(targetId restoredPackage restoredVersion, checkSummary.DriftedPackages)
        Assert.Equal("new", (List.exactlyOne checkSummary.Entries).Status)
        Assert.Contains(checkReport.Diagnostics, fun d -> d.Id = "dependencySurface.drift")
        Assert.Equal(1, exitCodeForReport checkReport)

        depSurfaceRequest true root [] |> ignore
        Assert.True(existsRelative root capturePathRel)
