namespace FS.GG.SDD.Acceptance.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Tests
open Xunit
open AcceptanceSupport

/// Exercises one ordinary lifecycle over reports emitted by independently-run F# and Node lanes.
/// The fixture is intentionally provider- and taxonomy-free: SDD reads TRX/JUnit bytes; it never
/// launches either suite as part of `evidence`.
module PolyglotLifecycleAcceptanceTests =
    let private fixtureRoot =
        Path.Combine(repoRoot, "tests", "fixtures", "polyglot-lifecycle")

    let private copyFile (source: string) (destination: string) =
        match Path.GetDirectoryName destination with
        | null -> ()
        | directory -> Directory.CreateDirectory directory |> ignore

        File.Copy(source, destination, true)

    let private assertGreen lane result =
        Assert.True(result.Started, $"{lane} did not start: {result.Diagnostic}")
        Assert.True(result.ExitCode = 0, $"{lane} failed: {result.Diagnostic}")

    let private evidenceText root workId =
        TestSupport.readRelative root $"work/{workId}/evidence.yml"

    [<Fact>]
    let ``one lifecycle imports real TRX and JUnit reports without a language taxonomy`` () =
        let reportsRoot = Path.Combine(fixtureRoot, "results")
        Directory.CreateDirectory reportsRoot |> ignore

        let server =
            runToCompletion
                "dotnet"
                [ "test"
                  "server.tests/Polyglot.Server.Tests.fsproj"
                  "--logger"
                  "trx;LogFileName=server.trx"
                  "--results-directory"
                  "results"
                  "--disable-build-servers" ]
                fixtureRoot
                300_000

        assertGreen "F# server test lane" server

        let client =
            runToCompletion "npm" [ "--prefix"; "client"; "test"; "--silent" ] fixtureRoot 120_000

        assertGreen "Node client test lane" client

        let trx = Path.Combine(reportsRoot, "server.trx")
        let junit = Path.Combine(reportsRoot, "client.junit.xml")
        Assert.True(File.Exists trx, "dotnet test did not emit its TRX report.")
        Assert.True(File.Exists junit, "npm test did not emit its JUnit report.")

        let workId = "816-polyglot-lifecycle"
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId "Polyglot lifecycle acceptance"
        copyFile trx (Path.Combine(root, "artifacts", "server.trx"))
        copyFile junit (Path.Combine(root, "artifacts", "client.junit.xml"))

        let importReport path =
            { TestSupport.evidenceRequest root workId "Polyglot lifecycle acceptance" with
                FromTestReport = Some path }
            |> TestSupport.runRequest

        let assertNoErrors (report: CommandReport) =
            Assert.DoesNotContain(
                report.Diagnostics,
                fun diagnostic -> diagnostic.Severity = Diagnostics.DiagnosticError
            )

        importReport "artifacts/server.trx" |> assertNoErrors
        Assert.Contains("source: artifacts/server.trx", evidenceText root workId)

        importReport "artifacts/client.junit.xml" |> assertNoErrors
        Assert.Contains("source: artifacts/client.junit.xml", evidenceText root workId)

        let verify = TestSupport.runVerify root workId "Polyglot lifecycle acceptance"
        let ship = TestSupport.runShip root workId "Polyglot lifecycle acceptance"
        assertNoErrors verify
        assertNoErrors ship

        let parsed =
            parseEvidenceArtifact
                { Path = $"work/{workId}/evidence.yml"
                  Text = evidenceText root workId }

        match parsed with
        | Error diagnostics -> failwithf "evidence did not parse after report import: %A" diagnostics
        | Ok artifact ->
            artifact.Evidence
            |> List.filter claimsRealPass
            |> List.iter (fun declaration ->
                match declaration.ObservedRun with
                | Some observed -> Assert.Equal("artifacts/client.junit.xml", observed.Source)
                | None -> failwithf "%s lost its observed report receipt" declaration.Id.Value)
