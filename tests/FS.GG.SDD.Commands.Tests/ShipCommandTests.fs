namespace FS.GG.SDD.Commands.Tests

open System.Diagnostics
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// Joins ProcessGlobalEnv: the CLI smoke here spawns a PATH-resolved process, so it must not
// run while a sibling mutates process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module ShipCommandTests =
    let workId = "013-ship-command"
    let title = "Ship Command"
    let shipPath = $"readiness/{workId}/ship.json"
    let shipVerdictPath = $"readiness/{workId}/ship-verdict.json"
    let verifyPath = $"readiness/{workId}/verify.json"
    let workModelPath = $"readiness/{workId}/work-model.json"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let evidencePath = $"work/{workId}/evidence.yml"
    let tasksPath = $"work/{workId}/tasks.yml"
    let specPath = $"work/{workId}/spec.md"

    type CliResult =
        { ExitCode: int
          StdOut: string
          StdErr: string }

    let initializedVerifiedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        root

    let runShipCli root extraArgs =
        let exitCode, stdout, stderr =
            [ "ship"; "--root"; root; "--work"; workId ] @ extraArgs
            |> TestSupport.runCliRaw 60000

        { ExitCode = exitCode
          StdOut = stdout
          StdErr = stderr }

    // --- Feature 092 (ADR-0026): the committed compact ship verdict ---

    [<Fact>]
    let ``ship emits the compact verdict beside ship json`` () =
        let root = initializedVerifiedProject ()
        let report = TestSupport.runShip root workId title

        Assert.True(TestSupport.existsRelative root shipVerdictPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = shipVerdictPath && change.Kind = "generatedView"
        )

        Assert.Contains(report.GeneratedViews, fun view -> view.Path = shipVerdictPath && view.Kind = "ship-verdict")

    [<Fact>]
    let ``the verdict's facts equal the ship json it projects`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runShip root workId title |> ignore

        let shipJson = TestSupport.readRelative root shipPath
        let verdictJson = TestSupport.readRelative root shipVerdictPath

        match parseShipView { Path = shipPath; Text = shipJson } with
        | Error diagnostics -> failwith $"ship.json did not parse: {diagnostics}."
        | Ok view ->
            // The verdict is a projection: re-project the parsed ship view and demand byte equality
            // with what `ship` wrote. Anything else means two sources of truth.
            Assert.Equal(ShipVerdict.toJson (ShipVerdict.fromShipView view), verdictJson)

            use doc = System.Text.Json.JsonDocument.Parse verdictJson
            let root' = doc.RootElement
            Assert.Equal(workId, root'.GetProperty("workId").GetString())
            Assert.Equal("ship", root'.GetProperty("stage").GetString())
            Assert.Equal(view.Status, root'.GetProperty("status").GetString())
            Assert.Equal(view.Readiness, root'.GetProperty("readiness").GetString())
            Assert.Equal(view.Disposition, root'.GetProperty("disposition").GetProperty("state").GetString())

            Assert.Equal(
                view.VerificationReadiness.Status,
                root'.GetProperty("verificationReadiness").GetProperty("status").GetString()
            )

            Assert.Equal(
                (ShipVerdict.sourcesDigest view.Sources).Value,
                root'.GetProperty("sourcesDigest").GetProperty("value").GetString()
            )

    [<Fact>]
    let ``a blocked ship writes neither ship json nor the verdict`` () =
        // FR-005: the verdict inherits ship.json's `not hasBlocking` write gate. An incomplete
        // ship must never be recorded as a committed verdict.
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeTasksReadyProject root workId title // no evidence -> ship blocks

        let report = TestSupport.runShip root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root shipPath, "a blocked ship must not write ship.json")
        Assert.False(TestSupport.existsRelative root shipVerdictPath, "a blocked ship must not write the verdict")

    [<Fact>]
    let ``two ship runs produce a byte-identical verdict with no clock, path, or ANSI`` () =
        let root = initializedVerifiedProject ()

        TestSupport.runShip root workId title |> ignore
        let first = TestSupport.readRelative root shipVerdictPath

        TestSupport.runShip root workId title |> ignore
        let second = TestSupport.readRelative root shipVerdictPath

        Assert.Equal(first, second)

        // `Assert.DoesNotContain(string, string)` is culture-sensitive and ESC is zero-weight, so
        // searching for "ESC[" would match the bare `[` of an array. Test the char (ordinal).
        Assert.False(first.Contains '\u001b', "the verdict must carry no ANSI")

        Assert.DoesNotContain(root, first)

        // A bare year check false-positives on the sha256 hex; match an ISO-8601 stamp instead.
        Assert.False(
            System.Text.RegularExpressions.Regex.IsMatch(first, @"\d{4}-\d{2}-\d{2}T\d{2}:"),
            "the verdict must carry no timestamp"
        )

    // --- User Story 1: ship-ready a verification-ready work item ---

    [<Fact>]
    let ``ship creates generated ship view with real filesystem evidence`` () =
        let root = initializedVerifiedProject ()

        let report = TestSupport.runShip root workId title
        let shipJson = TestSupport.readRelative root shipPath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        // §3.4 (SC-004): a clean ship over a current work model is shipReady/shipReady — the
        // self-inflicted staleGeneratedView advisory is gone (it was the sole cause of the
        // prior advisory disposition).
        TestSupport.assertShipSummary report "shipReady" "shipReady"
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView")
        Assert.True(TestSupport.existsRelative root shipPath)
        Assert.Contains("\"stage\": \"ship\"", shipJson)
        Assert.Contains("\"lifecycleReadiness\"", shipJson)
        Assert.Contains("\"verificationReadiness\"", shipJson)
        Assert.Contains("\"evidenceDispositions\"", shipJson)
        Assert.Contains("\"disposition\"", shipJson)
        Assert.Contains(report.ChangedArtifacts, fun change -> change.Path = shipPath && change.Kind = "generatedView")
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath)
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = shipPath && view.Kind = "ship")
        Assert.Equal(Some "ship.next.protectedBoundary", report.NextAction |> Option.map _.ActionId)

        match parseShipView { Path = shipPath; Text = shipJson } with
        | Ok view ->
            Assert.Equal("shipReady", view.Readiness)
            Assert.Equal(workId, view.WorkId.Value)
        | Error diagnostics -> failwith $"Generated ship view did not parse: {diagnostics}."

    [<Fact>]
    let ``ship next action lists ship and work-model artifacts with null command`` () =
        let root = initializedVerifiedProject ()

        let report = TestSupport.runShip root workId title

        match report.NextAction with
        | Some action ->
            Assert.Equal("ship.next.protectedBoundary", action.ActionId)
            Assert.Equal(None, action.Command)
            Assert.Contains(shipPath, action.RequiredArtifacts)
            Assert.Contains(workModelPath, action.RequiredArtifacts)
        | None -> failwith "Expected a next action."

    [<Fact>]
    let ``ship report shape exposes ship summary`` () =
        let root = initializedVerifiedProject ()
        let report = TestSupport.runShip root workId title
        let json = serializeReport report

        Assert.Contains("\"ship\"", json)
        Assert.Contains("\"changedArtifacts\"", json)
        Assert.Contains("\"generatedViews\"", json)
        Assert.Contains("\"diagnostics\"", json)
        Assert.Contains("\"governanceCompatibility\"", json)
        Assert.Contains("\"nextAction\"", json)

    [<Fact>]
    let ``ship aggregates verification view without regenerating it`` () =
        let root = initializedVerifiedProject ()
        let verifyBefore = TestSupport.readRelative root verifyPath

        let report = TestSupport.runShip root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        // verification view is the prerequisite gate; ship reports its currency but never rewrites it
        Assert.Equal(verifyBefore, TestSupport.readRelative root verifyPath)
        Assert.Contains(report.GeneratedViews, fun view -> view.Path = verifyPath && view.Kind = "verification")
        Assert.DoesNotContain(report.ChangedArtifacts, fun change -> change.Path = verifyPath)

    // §3.4 genuine staleness (FR-007): editing an upstream authored source after generation
    // still flags staleGeneratedView on ship — real staleness is not suppressed.
    [<Fact>]
    let ``ship still flags genuine upstream staleness`` () =
        let root = initializedVerifiedProject ()
        TestSupport.runShip root workId title |> ignore

        let edited =
            (TestSupport.readRelative root specPath)
            + "\n\nAuthor edited the spec after generation.\n"

        TestSupport.writeRelative root specPath edited

        let report = TestSupport.runShip root workId title

        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView")

    // --- User Story 2: block merge-boundary readiness on lifecycle gaps ---

    [<Fact>]
    let ``ship missing verification blocks without ship write`` () =
        let root = initializedVerifiedProject ()
        System.IO.File.Delete(System.IO.Path.Combine(root, "readiness", workId, "verify.json"))

        let report = TestSupport.runShip root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "ship.missingVerificationPrerequisite")
        Assert.False(TestSupport.existsRelative root shipPath)

    [<Fact>]
    let ``ship not-verification-ready blocks without ship write`` () =
        let root = initializedVerifiedProject ()
        let verifyJson = TestSupport.readRelative root verifyPath

        let notReady =
            verifyJson.Replace("verificationReady", "needsVerificationCorrection")

        TestSupport.writeRelative root verifyPath notReady

        let report = TestSupport.runShip root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "ship.verificationNotReady")
        Assert.False(TestSupport.existsRelative root shipPath)

    [<Fact>]
    let ``ship blocks malformed existing ship view without overwrite`` () =
        let root = initializedVerifiedProject ()
        TestSupport.writeRelative root shipPath "{ not valid ship json"
        let before = TestSupport.readRelative root shipPath

        let report = TestSupport.runShip root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "ship.malformedShipView")
        Assert.Equal(before, TestSupport.readRelative root shipPath)

    [<Fact>]
    let ``ship missing analysis blocks without ship write`` () =
        let root = initializedVerifiedProject ()
        System.IO.File.Delete(System.IO.Path.Combine(root, "readiness", workId, "analysis.json"))

        let report = TestSupport.runShip root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root shipPath)

    [<Fact>]
    let ``ship outside project blocks`` () =
        let root = TestSupport.tempDirectory ()

        let report = TestSupport.runShip root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "outsideProject")

    // --- User Story 3: preserve authored lifecycle sources ---

    [<Fact>]
    let ``ship preserves authored lifecycle sources and verification view`` () =
        let root = initializedVerifiedProject ()
        let specBefore = TestSupport.readRelative root specPath
        let tasksBefore = TestSupport.readRelative root tasksPath
        let evidenceBefore = TestSupport.readRelative root evidencePath
        let verifyBefore = TestSupport.readRelative root verifyPath

        TestSupport.runShip root workId title |> ignore

        Assert.Equal(specBefore, TestSupport.readRelative root specPath)
        Assert.Equal(tasksBefore, TestSupport.readRelative root tasksPath)
        Assert.Equal(evidenceBefore, TestSupport.readRelative root evidencePath)
        Assert.Equal(verifyBefore, TestSupport.readRelative root verifyPath)

    [<Fact>]
    let ``ship dry run reports generated change without mutation`` () =
        let root = initializedVerifiedProject ()

        let request =
            { TestSupport.shipRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root shipPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = shipPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``ship rerun over unchanged sources reports no change`` () =
        let root = initializedVerifiedProject ()

        TestSupport.runShip root workId title |> ignore
        let first = TestSupport.readRelative root shipPath
        let rerun = TestSupport.runShip root workId title
        let second = TestSupport.readRelative root shipPath

        Assert.NotEqual(CommandOutcome.Blocked, rerun.Outcome)
        Assert.Equal(first, second)

        Assert.Contains(
            rerun.ChangedArtifacts,
            fun change -> change.Path = shipPath && change.Operation = ArtifactOperation.NoChange
        )

    // --- User Story 4: keep ship output traceable ---

    [<Fact>]
    let ``ship does not require Governance files`` () =
        let root = initializedVerifiedProject ()

        let report = TestSupport.runShip root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.DoesNotContain(serializeReport report, "\"route\"")
        Assert.DoesNotContain(serializeReport report, "\"freshness\"")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``ship deterministic JSON report is byte stable`` () =
        let root = initializedVerifiedProject ()

        let request =
            { TestSupport.shipRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"ship\"", first)
        Assert.Contains("\"ship\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``ship text projection uses report facts`` () =
        let root = initializedVerifiedProject ()
        let report = TestSupport.runShip root workId title
        let text = renderText report

        Assert.Contains("command: ship", text)
        Assert.Contains($"shipPath: {shipPath}", text)
        Assert.Contains("shipReadiness: shipReady", text)
        Assert.Contains("shipDisposition: shipReady", text)
        Assert.Contains("nextAction: ship.next.protectedBoundary", text)

    [<Fact>]
    let ``ship create and rerun complete under local harness budget`` () =
        let root = initializedVerifiedProject ()

        let createWatch = Stopwatch.StartNew()
        let createReport = TestSupport.runShip root workId title
        createWatch.Stop()

        let rerunWatch = Stopwatch.StartNew()
        let rerunReport = TestSupport.runShip root workId title
        rerunWatch.Stop()

        Assert.NotEqual(CommandOutcome.Blocked, createReport.Outcome)
        Assert.NotEqual(CommandOutcome.Blocked, rerunReport.Outcome)
        Assert.True(createWatch.Elapsed.TotalSeconds < 2.0, $"Create took {createWatch.Elapsed}.")
        Assert.True(rerunWatch.Elapsed.TotalSeconds < 2.0, $"Rerun took {rerunWatch.Elapsed}.")

    [<Fact>]
    let ``ship CLI JSON smoke creates ship view`` () =
        let root = initializedVerifiedProject ()

        let result = runShipCli root []

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"ship\"", result.StdOut)
        Assert.Contains("\"ship\"", result.StdOut)
        Assert.Contains("\"ship.next.protectedBoundary\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.True(TestSupport.existsRelative root shipPath)

    [<Fact>]
    let ``ship CLI dry run smoke avoids generated mutation`` () =
        let root = initializedVerifiedProject ()

        let result = runShipCli root [ "--dry-run" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"ship\"", result.StdOut)
        Assert.Contains("\"safeWriteDecision\": \"dryRunOnly\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.False(TestSupport.existsRelative root shipPath)

    [<Fact>]
    let ``ship CLI text smoke renders human projection`` () =
        let root = initializedVerifiedProject ()

        let result = runShipCli root [ "--text" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("command: ship", result.StdOut)
        Assert.Contains("shipReadiness: shipReady", result.StdOut)
        Assert.Contains("nextAction: ship.next.protectedBoundary", result.StdOut)
        Assert.Equal("", result.StdErr)
