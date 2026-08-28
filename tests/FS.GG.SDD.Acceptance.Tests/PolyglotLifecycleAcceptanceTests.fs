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

    let private waitForHttp (child: Process) (url: string) (timeoutMs: int) =
        use client = new HttpClient()
        client.Timeout <- TimeSpan.FromSeconds 2.0
        let elapsed = Stopwatch.StartNew()
        let mutable body = None
        let mutable lastError = "endpoint did not answer"

        while body.IsNone
              && elapsed.ElapsedMilliseconds < int64 timeoutMs
              && not child.HasExited do
            try
                body <- Some(client.GetStringAsync(url).GetAwaiter().GetResult())
            with ex ->
                lastError <- ex.Message
                Threading.Thread.Sleep 250

        match body with
        | Some value -> value
        | None when child.HasExited -> failwith $"fixture process exited before {url} became ready"
        | None -> failwith $"fixture endpoint {url} was not ready after {timeoutMs} ms: {lastError}"

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

            try
                let body = waitForHttp server "http://127.0.0.1:51816/health" 60_000
                Assert.Equal("polyglot server ready", body)
            finally
                if not server.HasExited then
                    server.Kill(true)

    let private assertBrowserRuntime () =
        let start = ProcessStartInfo("npm", "run serve --silent")
        start.WorkingDirectory <- fixturePath "client"

        match Process.Start start with
        | null -> failwith "could not start Vite"
        | vite ->
            use vite = vite

            try
                waitForHttp vite "http://127.0.0.1:51817" 60_000 |> ignore

                // Hosted runners and developer machines install the same browser under different
                // prefixes. Let the process edge resolve it through PATH, just as a shell would, and
                // fall back only when a candidate could not be started at all. A browser that starts
                // but fails remains a real acceptance failure rather than being hidden by fallback.
                let rec runBrowser candidates diagnostics =
                    match candidates with
                    | [] ->
                        { Started = false
                          ExitCode = -1
                          Diagnostic =
                            "could not start a supported browser from PATH (tried chromium and google-chrome): "
                            + String.concat "; " (List.rev diagnostics) }
                    | executable :: remaining ->
                        let result =
                            runToCompletionCapturingOutput
                                executable
                                [ "--headless"
                                  "--no-sandbox"
                                  "--disable-dev-shm-usage"
                                  "--disable-background-networking"
                                  "--dump-dom"
                                  "http://127.0.0.1:51817" ]
                                fixtureRoot
                                60_000

                        if result.Started then
                            result
                        else
                            runBrowser remaining ($"{executable}: {result.Diagnostic}" :: diagnostics)

                let browser = runBrowser [ "chromium"; "google-chrome" ] []

                assertGreen "Vite browser runtime" browser
                Assert.Contains("data-executed=\"typescript\"", browser.Diagnostic)
                Assert.Contains("polyglot browser ready", browser.Diagnostic)
            finally
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

    let private clearObservedRuns (ids: Set<string>) (text: string) =
        let folder (currentId, skipping, kept) (line: string) =
            if line.StartsWith("  - id: ", StringComparison.Ordinal) then
                let id = line.Substring("  - id: ".Length).Trim()
                Some id, false, line :: kept
            elif line = "    observedRun:" && currentId |> Option.exists ids.Contains then
                currentId, true, kept
            elif skipping && line.StartsWith("      ", StringComparison.Ordinal) then
                currentId, true, kept
            else
                currentId, false, line :: kept

        text.Split('\n')
        |> Array.fold folder (None, false, [])
        |> fun (_, _, kept) -> kept |> List.rev |> String.concat "\n"

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

        let consoleTests =
            runToCompletion
                "dotnet"
                [ "test"
                  "no-npm-console.tests/NoNpm.Console.Tests.fsproj"
                  "--logger"
                  "trx;LogFileName=console.trx"
                  "--results-directory"
                  "results" ]
                fixtureRoot
                120_000

        assertGreen "no-npm console test lane" consoleTests

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
        let consoleJunit = Path.Combine(reportsRoot, "console.trx")
        Assert.True(File.Exists trx, "dotnet test did not emit its TRX report.")
        Assert.True(File.Exists junit, "npm test did not emit its JUnit report.")
        Assert.True(File.Exists fableJunit, "Fable runtime did not emit its JUnit report.")
        Assert.True(File.Exists consoleJunit, "no-npm console did not emit its JUnit report.")

        let workId = "816-polyglot-lifecycle"
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId "Polyglot lifecycle acceptance"

        evidenceText root workId
        |> onlyFirstObligationClaimsPass
        |> activateMissing 4
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        copyFile trx (Path.Combine(root, "artifacts", "server.trx"))
        copyFile junit (Path.Combine(root, "artifacts", "client.junit.xml"))
        copyFile fableJunit (Path.Combine(root, "artifacts", "fable.junit.xml"))
        copyFile consoleJunit (Path.Combine(root, "artifacts", "console.trx"))

        // Commit the same canonical evidence representation that later imports update. This keeps
        // the candidate immutable apart from generated execution receipts.
        TestSupport.runEvidence root workId "Polyglot lifecycle acceptance" |> ignore

        for args in
            [ [ "init"; "-q" ]
              [ "config"; "user.email"; "polyglot@example.invalid" ]
              [ "config"; "user.name"; "polyglot" ]
              [ "add"; "-A" ]
              [ "commit"; "-qm"; "tested candidate" ] ] do
            let exitCode, _ = FS.GG.SDD.TestShared.TestShared.ChildProcess.git root args
            Assert.Equal(0, exitCode)

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

        // All obligation semantics were committed before execution. Clear only generated receipt
        // blocks between imports so each real runner format occupies its own lane while every receipt
        // remains bound to the same immutable candidate.
        evidenceText root workId
        |> clearObservedRuns (set [ "EV002"; "EV003"; "EV004"; "EV005" ])
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        importReport "artifacts/client.junit.xml" |> assertNoErrors

        evidenceText root workId
        |> clearObservedRuns (set [ "EV003"; "EV004"; "EV005" ])
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        importReport "artifacts/fable.junit.xml" |> assertNoErrors

        evidenceText root workId
        |> clearObservedRuns (set [ "EV004"; "EV005" ])
        |> TestSupport.writeRelative root $"work/{workId}/evidence.yml"

        importReport "artifacts/console.trx" |> assertNoErrors
        let finalEvidence = evidenceText root workId
        Assert.Contains("source: artifacts/server.trx", finalEvidence)
        Assert.Contains("source: artifacts/client.junit.xml", finalEvidence)
        Assert.Contains("source: artifacts/fable.junit.xml", finalEvidence)
        Assert.Contains("source: artifacts/console.trx", finalEvidence)

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
                  Text = finalEvidence
                  RawBytes = None }

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
                      "artifacts/fable.junit.xml"
                      "artifacts/console.trx" ],
                sources
            )
