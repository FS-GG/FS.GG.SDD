namespace FS.GG.SDD.Acceptance.Tests

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Tests
open Xunit
open AcceptanceSupport

/// Exercises one ordinary lifecycle over reports emitted by independently-run ASP.NET Core and
/// TypeScript/browser lanes, plus no-npm console and package-producing Fable binding witnesses.
/// The fixture is intentionally provider- and taxonomy-free: SDD reads TRX/JUnit bytes; it never
/// launches either suite as part of `evidence`.
module PolyglotLifecycleAcceptanceTests =
    let private fixtureRoot =
        Path.Combine(repoRoot, "tests", "fixtures", "polyglot-lifecycle")

    let private fixturePath path = Path.Combine(fixtureRoot, path)

    let private copyFile (source: string) (destination: string) =
        match Path.GetDirectoryName destination with
        | null -> ()
        | directory -> Directory.CreateDirectory directory |> ignore

        File.Copy(source, destination, true)

    let private assertGreen lane result =
        Assert.True(result.Started, $"{lane} did not start: {result.Diagnostic}")
        Assert.True(result.ExitCode = 0, $"{lane} failed: {result.Diagnostic}")

    let private assertServerEndpoint () =
        let start =
            ProcessStartInfo("dotnet", "run --no-restore --project server/Polyglot.Server.fsproj")

        start.WorkingDirectory <- fixtureRoot
        start.Environment["ASPNETCORE_URLS"] <- "http://127.0.0.1:51816"
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true

        match Process.Start start with
        | null -> failwith "could not start the ASP.NET Core fixture"
        | server ->
            use server = server
            Threading.Thread.Sleep 3000
            use client = new HttpClient()
            let body = client.GetStringAsync("http://127.0.0.1:51816/health").Result
            Assert.Equal("polyglot server ready", body)

            if not server.HasExited then
                server.Kill(true)

    let private assertBrowserRuntime () =
        let start = ProcessStartInfo("npm", "run serve --silent")
        start.WorkingDirectory <- fixturePath "client"

        match Process.Start start with
        | null -> failwith "could not start Vite"
        | vite ->
            use vite = vite
            Threading.Thread.Sleep 1500

            let browser =
                runToCompletion
                    "/usr/sbin/chromium"
                    [ "--headless"; "--no-sandbox"; "--dump-dom"; "http://127.0.0.1:51817" ]
                    fixtureRoot
                    30_000

            assertGreen "Vite browser runtime" browser

            if not vite.HasExited then
                vite.Kill(true)

    let private evidenceText root workId =
        TestSupport.readRelative root $"work/{workId}/evidence.yml"

    let private onlyFirstObligationClaimsPass (text: string) =
        let marker = "result: pass"
        let first = text.IndexOf(marker, StringComparison.Ordinal)

        if first < 0 then
            failwith "fixture evidence has no passing obligation"

        text.Substring(0, first + marker.Length)
        + text.Substring(first + marker.Length).Replace(marker, "result: missing")

    let private activateMissing (count: int) (text: string) =
        let marker = "result: missing"

        let rec replace (remaining: int) (offset: int) (current: string) =
            if remaining = 0 then
                current
            else
                let index = current.IndexOf(marker, offset, StringComparison.Ordinal)

                if index < 0 then
                    current
                else
                    replace
                        (remaining - 1)
                        (index + "result: pass".Length)
                        (current.Remove(index, marker.Length).Insert(index, "result: pass"))

        replace count 0 text

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
        assertServerEndpoint ()

        let console =
            runToCompletion
                "dotnet"
                [ "run"
                  "--project"
                  "no-npm-console/NoNpm.Console.fsproj"
                  "--disable-build-servers" ]
                fixtureRoot
                120_000

        assertGreen "no-npm console lane" console

        let installClient = runToCompletion "npm" [ "ci" ] (fixturePath "client") 120_000
        assertGreen "TypeScript client dependency restore" installClient

        let client =
            runToCompletion "npm" [ "--prefix"; "client"; "test"; "--silent" ] fixtureRoot 120_000

        assertGreen "Node client test lane" client
        assertBrowserRuntime ()

        let package =
            runToCompletion
                "dotnet"
                [ "pack"
                  "fable-bindings/FableBindings.fsproj"
                  "--output"
                  "fable-bindings/packages"
                  "--disable-build-servers" ]
                fixtureRoot
                120_000

        assertGreen "Fable bindings package lane" package
        Assert.NotEmpty(Directory.GetFiles(fixturePath "fable-bindings/packages", "*.nupkg"))

        let installBindings =
            runToCompletion "npm" [ "ci" ] (fixturePath "fable-bindings") 120_000

        assertGreen "Fable bindings npm restore" installBindings

        let bindings =
            runToCompletion "npm" [ "test"; "--silent" ] (fixturePath "fable-bindings") 120_000

        assertGreen "Fable bindings compile/runtime lane" bindings

        let trx = Path.Combine(reportsRoot, "server.trx")
        let junit = Path.Combine(reportsRoot, "client.junit.xml")
        let fableJunit = Path.Combine(reportsRoot, "fable.junit.xml")
        Assert.True(File.Exists trx, "dotnet test did not emit its TRX report.")
        Assert.True(File.Exists junit, "npm test did not emit its JUnit report.")
        Assert.True(File.Exists fableJunit, "Fable runtime did not emit its JUnit report.")

        let workId = "816-polyglot-lifecycle"
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId "Polyglot lifecycle acceptance"

        evidenceText root workId
        |> onlyFirstObligationClaimsPass
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        copyFile trx (Path.Combine(root, "artifacts", "server.trx"))
        copyFile junit (Path.Combine(root, "artifacts", "client.junit.xml"))
        copyFile fableJunit (Path.Combine(root, "artifacts", "fable.junit.xml"))

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
        |> activateMissing 3
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        importReport "artifacts/client.junit.xml" |> assertNoErrors

        evidenceText root workId
        |> activateMissing 1
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        importReport "artifacts/fable.junit.xml" |> assertNoErrors
        let finalEvidence = evidenceText root workId
        Assert.Contains("source: artifacts/server.trx", finalEvidence)
        Assert.Contains("source: artifacts/client.junit.xml", finalEvidence)
        Assert.Contains("source: artifacts/fable.junit.xml", finalEvidence)

        let verify = TestSupport.runVerify root workId "Polyglot lifecycle acceptance"
        let ship = TestSupport.runShip root workId "Polyglot lifecycle acceptance"

        let doctor =
            { TestSupport.request Doctor root with
                WorkId = Some workId }
            |> TestSupport.runRequest

        assertNoErrors verify
        assertNoErrors ship
        assertNoErrors doctor

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

            Assert.Equal<string Set>(
                set
                    [ "artifacts/server.trx"
                      "artifacts/client.junit.xml"
                      "artifacts/fable.junit.xml" ],
                sources
            )
