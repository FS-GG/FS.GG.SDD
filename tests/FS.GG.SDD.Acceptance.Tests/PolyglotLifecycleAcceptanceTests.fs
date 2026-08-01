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

    let private onlyFirstObligationClaimsPass (text: string) =
        let marker = "result: pass"
        let first = text.IndexOf(marker, StringComparison.Ordinal)

        if first < 0 then
            failwith "fixture evidence has no passing obligation"

        text.Substring(0, first + marker.Length)
        + text.Substring(first + marker.Length).Replace(marker, "result: missing")

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

        evidenceText root workId
        |> onlyFirstObligationClaimsPass
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

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

        // Reactivate the client obligations. EV001 retains the TRX receipt, while the newly
        // pass-claiming obligations are still unobserved and can receive the JUnit receipt.
        evidenceText root workId
        |> fun text -> text.Replace("result: missing", "result: pass")
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        importReport "artifacts/client.junit.xml" |> assertNoErrors
        let finalEvidence = evidenceText root workId
        Assert.Contains("source: artifacts/server.trx", finalEvidence)
        Assert.Contains("source: artifacts/client.junit.xml", finalEvidence)

        let verify = TestSupport.runVerify root workId "Polyglot lifecycle acceptance"
        let ship = TestSupport.runShip root workId "Polyglot lifecycle acceptance"
        assertNoErrors verify
        assertNoErrors ship

        let parsed =
            parseEvidenceArtifact
                { Path = $"work/{workId}/evidence.yml"
                  Text = finalEvidence }

        match parsed with
        | Error diagnostics -> failwithf "evidence did not parse after report import: %A" diagnostics
        | Ok artifact ->
            let sources =
                artifact.Evidence
                |> List.filter claimsRealPass
                |> List.choose _.ObservedRun
                |> List.map _.Source
                |> Set.ofList

            Assert.Equal<string Set>(set [ "artifacts/server.trx"; "artifacts/client.junit.xml" ], sources)
