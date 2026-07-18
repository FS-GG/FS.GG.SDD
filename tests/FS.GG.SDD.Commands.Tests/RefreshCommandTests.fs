namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.Json
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// Joins ProcessGlobalEnv: the CLI smoke here spawns a PATH-resolved process, so it must not
// run while a sibling mutates process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module RefreshCommandTests =
    let workId = "015-refresh-command"
    let title = "Generated-View Refresh"
    let summaryPath = $"readiness/{workId}/summary.md"
    let workModelPath = $"readiness/{workId}/work-model.json"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let verifyPath = $"readiness/{workId}/verify.json"
    let shipPath = $"readiness/{workId}/ship.json"
    let shipVerdictPath = $"readiness/{workId}/ship-verdict.json"
    let claudeGuidance = $"readiness/{workId}/agent-commands/claude/guidance.json"

    // A project whose structured SDD-owned views (work-model, analysis, verify, ship)
    // all exist and are mutually consistent.
    let shippedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore
        root

    // A project whose full SDD-owned generated-view set (incl. agent guidance and
    // summary) already exists and is current.
    let fullyGeneratedProject () =
        let root = shippedProject ()
        TestSupport.runAgents root workId |> ignore
        TestSupport.runRefresh root workId |> ignore
        root

    // --- Feature 095 (FS.GG.SDD#188): the currency state matrix ---
    //
    // The 10 cells of specs/095-refresh-verdict-currency/contracts/refresh-currency-matrix.md,
    // as executable fixtures. `ship.json` x `ship-verdict.json`.

    /// The five states of `ship.json`. S3 is the defect's home: valid JSON that is not a valid
    /// ship view, which `parsesAsJson` (weaker than `parseShipView`) waved through as "current".
    type ShipState =
        | S1_Absent
        | S2_InvalidJson
        | S3_ValidJsonInvalidView
        | S4_StaleValidView
        | S5_CurrentValidView

    let private absolute root (relative: string) =
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))

    /// Build a work item in the requested cell. `verdictPresent = false` deletes the committed verdict.
    let private cell shipState verdictPresent =
        let root = shippedProject ()

        match shipState with
        | S1_Absent -> File.Delete(absolute root shipPath)
        | S2_InvalidJson -> File.WriteAllText(absolute root shipPath, "{ not json")
        | S3_ValidJsonInvalidView -> File.WriteAllText(absolute root shipPath, "{ \"schemaVersion\": 99 }")
        | S4_StaleValidView ->
            // Leave ship.json a valid ship view; move an authored source under it so `wmChanged`.
            File.AppendAllText(absolute root $"work/{workId}/spec.md", "\n\n## Appended by the matrix\n")
        | S5_CurrentValidView -> ()

        if not verdictPresent then
            File.Delete(absolute root shipVerdictPath)

        root

    /// The three facts the matrix pins per cell: the two currency words and the exit code.
    let private observe root =
        let report = TestSupport.runRefresh root workId

        TestSupport.refreshViewState report "ship",
        TestSupport.refreshViewState report "ship-verdict",
        exitCodeForReport report

    let private matrixCells =
        [ 1, S1_Absent, true
          2, S1_Absent, false
          3, S2_InvalidJson, true
          4, S2_InvalidJson, false
          5, S3_ValidJsonInvalidView, true
          6, S3_ValidJsonInvalidView, false
          7, S4_StaleValidView, true
          8, S4_StaleValidView, false
          9, S5_CurrentValidView, true
          10, S5_CurrentValidView, false ]

    [<Fact>]
    let ``the exit code is invariant across the whole currency matrix`` () =
        // FR-007 / SC-004. THIS TEST IS GREEN BEFORE AND AFTER feature 095 — it is a regression lock,
        // not a red-green test. The expected values were captured from the unmodified handler (T002)
        // before any source edit; they are not derived from the new logic.
        //
        // Why it matters: feature 095 re-words two `generatedViews[].currency` values, and the obvious
        // fear is that a stricter `ship.json` validator starts failing runs that used to pass. It does
        // not. `verdictClass` already participates in `structuredClasses`, so cells 5 and 6 were ALREADY
        // non-clean and ALREADY emitted the `refresh.unrenderableSummary` error. The run always failed;
        // it just blamed the wrong file. If this test ever reddens, that reasoning is unsound and the
        // change is a behavior change, not a re-attribution.
        let expected = [ 1; 1; 1; 1; 1; 1; 1; 1; 0; 0 ]

        let actual =
            matrixCells
            |> List.map (fun (_, shipState, verdictPresent) ->
                let _, _, code = observe (cell shipState verdictPresent)
                code)

        Assert.Equal<int list>(expected, actual)

    [<Fact>]
    let ``governance-handoff never inherits malformed from its source`` () =
        // FR-017. `govClass` is `inheritShip ()` for every non-AlreadyCurrent source, which propagated
        // `shClass` verbatim. Correcting `shClass` in cells 5/6 moved the handoff from `blocked` to
        // `malformed` -- reintroducing, one artifact over, the false attribution this feature removes
        // from `ship-verdict`. Cells 3/4 carried the same falsehood before this feature ever ran.
        //
        // `Malformed` is the ONE class the handoff must not inherit: it is a statement about a file's
        // own bytes, and the handoff's bytes are fine. `Stale`/`Missing`/`Blocked` are all true of the
        // handoff as well as of its source, so they pass through.
        let expected =
            [ 1, "missing" // ship absent
              2, "missing"
              3, "blocked" // ship not JSON            -- was "malformed" (pre-existing falsehood)
              4, "blocked"
              5, "blocked" // ship valid JSON, bad view -- a naive fix regresses this to "malformed"
              6, "blocked"
              7, "stale" // ship stale
              8, "stale"
              9, "current"
              10, "current" ]

        let actual =
            matrixCells
            |> List.map (fun (n, shipState, verdictPresent) ->
                let report = TestSupport.runRefresh (cell shipState verdictPresent) workId
                n, TestSupport.refreshViewState report "governance-handoff")

        Assert.Equal<(int * string) list>(expected, actual)

    [<Fact>]
    let ``every currency word in the matrix is true of the artifact it names`` () =
        // SC-001, the whole feature. Cells 5 and 6 are the defect: `ship: current` about a file that
        // does not parse as a ship view, and `ship-verdict: malformed` about a file that is perfectly
        // well-formed -- in cell 6, about a file that does not even exist.
        let expected =
            [ 1, "missing", "blocked" // ship absent (fresh clone), verdict survives
              2, "missing", "missing"
              3, "malformed", "blocked" // not JSON at all
              4, "malformed", "missing"
              5, "malformed", "blocked" // WAS: ship=current, verdict=malformed
              6, "malformed", "missing" // WAS: ship=current, verdict=malformed (verdict is ABSENT)
              7, "stale", "stale"
              8, "stale", "missing"
              9, "current", "current"
              10, "current", "current" ]

        let actual =
            matrixCells
            |> List.map (fun (n, shipState, verdictPresent) ->
                let ship, verdict, _ = observe (cell shipState verdictPresent)
                n, ship, verdict)

        Assert.Equal<(int * string * string) list>(expected, actual)

    [<Fact>]
    let ``a stale ship json reports the same severity whether or not the verdict is present`` () =
        // FR-009 / SC-005, matrix cells 7 vs 8. The state is identical -- the source moved, re-run
        // `ship` -- and so is the remediation. Before feature 095 the absent-verdict case raised
        // `refresh.blockedUpstreamView` (ERROR), claiming the verdict "cannot be refreshed until
        // upstream is current", about the ordinary fresh-clone-then-edit path. The present-verdict
        // case emitted `refresh.staleView` (warning) for the same thing.
        //
        // No test covered the absent-verdict cell. That is exactly why the asymmetry survived review.
        let verdictDiagnosticOf root =
            let report = TestSupport.runRefresh root workId

            report.Diagnostics
            |> List.filter (fun d ->
                d.Artifact
                |> Option.map (fun a -> a.Path = shipVerdictPath)
                |> Option.defaultValue false)
            |> List.map (fun d -> d.Id, d.Severity)

        let present = verdictDiagnosticOf (cell S4_StaleValidView true)
        let absent = verdictDiagnosticOf (cell S4_StaleValidView false)

        Assert.Equal<(string * DiagnosticSeverity) list>(
            [ "refresh.staleView", DiagnosticSeverity.DiagnosticWarning ],
            present
        )

        Assert.Equal<(string * DiagnosticSeverity) list>(
            [ "refresh.staleView", DiagnosticSeverity.DiagnosticWarning ],
            absent
        )

        // FR-010: the currency word still describes the ARTIFACT, and the artifact is absent.
        let _, verdictWord, _ = observe (cell S4_StaleValidView false)
        Assert.Equal("missing", verdictWord)

    [<Fact>]
    let ``an absent verdict over a non-stale source still blocks with an error`` () =
        // FR-011, matrix cells 2/4/6. The guardrail on the fix above: the severity correction is scoped
        // to a STALE source. When the source is missing, unreadable, or unparseable, the verdict really
        // is blocked on an upstream it cannot assess, and that stays an error.
        let verdictDiagnosticOf root =
            let report = TestSupport.runRefresh root workId

            report.Diagnostics
            |> List.filter (fun d ->
                d.Artifact
                |> Option.map (fun a -> a.Path = shipVerdictPath)
                |> Option.defaultValue false)
            |> List.map (fun d -> d.Id, d.Severity)

        for shipState in [ S1_Absent; S2_InvalidJson; S3_ValidJsonInvalidView ] do
            let root = cell shipState false

            Assert.Equal<(string * DiagnosticSeverity) list>(
                [ "refresh.blockedUpstreamView", DiagnosticSeverity.DiagnosticError ],
                verdictDiagnosticOf root
            )

            // FR-006: refresh plans no WriteFile for the verdict from a source it cannot trust. Asserted
            // on the filesystem rather than the effect list — the observable fact, not the intent.
            Assert.False(
                TestSupport.existsRelative root shipVerdictPath,
                $"refresh must not synthesise a verdict from a {shipState} ship.json."
            )

    [<Fact>]
    let ``a deprecated but supported ship json schema stays current`` () =
        // FR-016. Adopting `parseShipView` adopts the artifact layer's compatibility policy EXACTLY.
        // `parseJsonView` builds the view for both `Current` and `Deprecated` status
        // (LifecycleArtifacts/Internal.fs), and `classifyRaw` calls major-0 `Deprecated`. So the stricter
        // oracle must NOT start calling every non-current schema malformed -- only the ones the artifact
        // layer already refuses to read (Unsupported / Future / missing / structurally broken).
        //
        // Without this test, a later tightening of `parseJsonView` would silently reclassify a working
        // workspace's ship.json as `malformed` and no one would notice until a consumer's CI went red.
        let root = shippedProject ()
        let shipFile = absolute root shipPath

        let deprecated =
            File.ReadAllText(shipFile).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 0")

        Assert.Contains("\"schemaVersion\": 0", deprecated) // the fixture actually mutated
        File.WriteAllText(shipFile, deprecated)

        let report = TestSupport.runRefresh root workId

        Assert.Equal("current", TestSupport.refreshViewState report "ship")

    [<Fact>]
    let ``the stronger ship-view oracle does not leak onto analysis or verify`` () =
        // FR-002. `downstreamClass` takes its validator as a PARAMETER precisely so that "analysis and
        // verify are unchanged" is a call-site fact, not a runtime accident. They keep the weaker
        // `parsesAsJson` gate: each would need its own oracle, state matrix, and regression sweep
        // (spec §Out of Scope). This test pins that scope -- if someone later widens `downstreamClass`
        // to schema-validate everything, this goes red and the decision gets made deliberately.
        for view, path in [ "analysis", analysisPath; "verify", verifyPath ] do
            let root = shippedProject ()
            File.WriteAllText(absolute root path, "{ \"schemaVersion\": 99 }")

            let report = TestSupport.runRefresh root workId

            Assert.Equal("current", TestSupport.refreshViewState report view)

    // --- Feature 092 (ADR-0026): refresh re-projects the committed verdict ---

    [<Fact>]
    let ``refresh leaves an unchanged verdict byte-identical and already-current`` () =
        // FR-006/FR-007: ship and refresh call one shared pure projection, so the bytes agree by
        // construction. If they ever diverge, the committed artifact churns on every refresh.
        let root = shippedProject ()
        let afterShip = TestSupport.readRelative root shipVerdictPath

        let report = TestSupport.runRefresh root workId

        Assert.Equal(afterShip, TestSupport.readRelative root shipVerdictPath)
        Assert.Equal("current", TestSupport.refreshViewState report "ship-verdict")

        match report.Refresh with
        | Some summary -> Assert.Contains("ship-verdict", summary.AlreadyCurrentViewIds)
        | None -> failwith "Expected refresh summary."

    [<Fact>]
    let ``refresh restores a deleted verdict from a current ship json`` () =
        let root = shippedProject ()
        let expected = TestSupport.readRelative root shipVerdictPath
        File.Delete(Path.Combine(root, shipVerdictPath.Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId

        Assert.True(TestSupport.existsRelative root shipVerdictPath)
        Assert.Equal(expected, TestSupport.readRelative root shipVerdictPath)

        match report.Refresh with
        | Some summary -> Assert.Contains("ship-verdict", summary.RefreshedViewIds)
        | None -> failwith "Expected refresh summary."

    [<Fact>]
    let ``refresh does not rewrite the verdict from a malformed ship json`` () =
        // FR-006 second half: re-projecting against a broken ship.json would commit a verdict for
        // inputs that never produced it. The verdict is present but unrefreshable -> blocked, and
        // the run must not report itself current.
        let root = shippedProject ()
        let original = TestSupport.readRelative root shipVerdictPath
        File.WriteAllText(Path.Combine(root, shipPath.Replace('/', Path.DirectorySeparatorChar)), "{ not json")

        let report = TestSupport.runRefresh root workId

        Assert.Equal(original, TestSupport.readRelative root shipVerdictPath)
        Assert.Equal("blocked", TestSupport.refreshViewState report "ship-verdict")
        Assert.NotEmpty report.Diagnostics

    [<Fact>]
    let ``a ship json that is valid json but not a ship view blocks the verdict with a diagnostic`` () =
        // Matrix cell 5. Feature 092 shipped this state knowingly, guarding only that SOME diagnostic
        // fired; it asserted `ship: current` and `ship-verdict: malformed`. Both were false, and this
        // test asserted them. Feature 095 (FS.GG.SDD#188) closes the hole:
        //
        //   `shClass` is now computed from `parseShipView`, not `parsesAsJson`. A future-schema
        //   ship.json is MALFORMED, not "already current" -- so `ship: current` stops lying, and the
        //   well-formed COMMITTED verdict stops being called corrupt. The verdict is `blocked`: present,
        //   but its source cannot be trusted, so refresh cannot tell whether it is current.
        //
        // The single `malformedGeneratedView` now names ship.json -- the file that needs repair.
        let root = shippedProject ()
        let original = TestSupport.readRelative root shipVerdictPath

        File.WriteAllText(
            Path.Combine(root, shipPath.Replace('/', Path.DirectorySeparatorChar)),
            "{ \"schemaVersion\": 99 }"
        )

        let report = TestSupport.runRefresh root workId

        Assert.Equal(original, TestSupport.readRelative root shipVerdictPath) // FR-006: never rewritten
        Assert.Equal("malformed", TestSupport.refreshViewState report "ship") // FR-003
        Assert.Equal("blocked", TestSupport.refreshViewState report "ship-verdict") // FR-004
        Assert.NotEmpty report.Diagnostics

        // FR-005: `malformed` is attributed to ship.json, and to nothing else.
        let malformedPaths =
            report.Diagnostics
            |> List.filter (fun d -> d.Id = "refresh.malformedGeneratedView")
            |> List.choose (fun d -> d.Artifact |> Option.map (fun a -> a.Path))

        Assert.Equal<string list>([ shipPath ], malformedPaths)

        match report.Refresh with
        | Some summary ->
            Assert.Contains("ship-verdict", summary.BlockedViewIds)
            Assert.DoesNotContain("ship-verdict", summary.AlreadyCurrentViewIds)
            // FR-003a: the bucket projection must tell the same story as the currency word. `ship`
            // sitting in `alreadyCurrentViewIds` was the same lie told through a second field.
            Assert.Contains("ship", summary.BlockedViewIds)
            Assert.DoesNotContain("ship", summary.AlreadyCurrentViewIds)
        | None -> failwith "Expected refresh summary."

    [<Fact>]
    let ``a fresh clone - committed verdict, gitignored ship json - never reports the verdict missing`` () =
        // The state ADR-0026 exists for. `ship.json` is gitignored; `ship-verdict.json` is committed.
        // Reporting the one artifact that survived the clone as "missing" would be a false fact.
        let root = shippedProject ()
        File.Delete(Path.Combine(root, shipPath.Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId

        Assert.True(TestSupport.existsRelative root shipVerdictPath)
        Assert.Equal("missing", TestSupport.refreshViewState report "ship")
        Assert.Equal("blocked", TestSupport.refreshViewState report "ship-verdict")

    [<Fact>]
    let ``an edited source makes the committed verdict stale, not blocked`` () =
        // The ordinary path: edit an authored source, then refresh. `ship.json` goes stale, so the
        // committed verdict no longer matches its inputs. It must report the SAME word as its source
        // ("stale" — re-run ship); "blocked" would claim refresh could not proceed at all.
        let root = shippedProject ()
        let original = TestSupport.readRelative root shipVerdictPath

        let spec =
            Path.Combine(root, $"work/{workId}/spec.md".Replace('/', Path.DirectorySeparatorChar))

        File.AppendAllText(spec, "\n\n## Appended by the test\n\nA new authored paragraph.\n")

        let report = TestSupport.runRefresh root workId

        Assert.Equal("stale", TestSupport.refreshViewState report "ship")
        Assert.Equal("stale", TestSupport.refreshViewState report "ship-verdict")
        // never rewritten from a stale source
        Assert.Equal(original, TestSupport.readRelative root shipVerdictPath)

    [<Fact>]
    let ``refresh does not write a verdict when both ship json and the verdict are missing`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, shipPath.Replace('/', Path.DirectorySeparatorChar)))
        File.Delete(Path.Combine(root, shipVerdictPath.Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId

        Assert.False(TestSupport.existsRelative root shipVerdictPath)
        Assert.Equal("missing", TestSupport.refreshViewState report "ship-verdict")

    // --- User Story 1: orchestrated refresh of the structured views ---

    [<Fact>]
    let ``refresh regenerates the structured SDD-owned views from current sources`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        TestSupport.assertRefreshDisposition report "refreshed-current"
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        for path in [ workModelPath; analysisPath; verifyPath; shipPath; claudeGuidance ] do
            Assert.True(TestSupport.existsRelative root path, $"Expected generated view {path}.")

        // agent-commands/summary did not exist on a shipped project -> refreshed this run.
        match report.Refresh with
        | Some summary ->
            Assert.Contains("agent-commands", summary.RefreshedViewIds)
            Assert.Contains("summary", summary.RefreshedViewIds)
            // the structured views already existed and are current -> already-current.
            Assert.Contains("work-model", summary.AlreadyCurrentViewIds)
            Assert.Contains("analysis", summary.AlreadyCurrentViewIds)
            Assert.Contains("verify", summary.AlreadyCurrentViewIds)
            Assert.Contains("ship", summary.AlreadyCurrentViewIds)
        | None -> failwith "Expected refresh summary."

    [<Fact>]
    let ``refresh reports every applicable view current after the run`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        for view in [ "work-model"; "analysis"; "verify"; "ship"; "agent-commands"; "summary" ] do
            Assert.Equal("current", TestSupport.refreshViewState report view)

    [<Fact>]
    let ``refresh records generated views with sources and generator identity`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        let summaryView =
            report.GeneratedViews |> List.tryFind (fun v -> v.Path = summaryPath)

        match summaryView with
        | Some view ->
            Assert.Equal("summary", view.Kind)
            Assert.NotEmpty view.Sources
            Assert.True(view.Generator.IsSome, "Expected a generator identity on the summary view.")
        | None -> failwith "Expected a summary generated-view entry."

    [<Fact>]
    let ``refresh succeeds without governance installed`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.All(report.GovernanceCompatibility, (fun fact -> Assert.Equal("notEvaluated", fact.State)))

    [<Fact>]
    let ``refresh report exposes the refresh block and generated views`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.True(report.Refresh.IsSome, "Expected report.Refresh.")
        Assert.NotEmpty report.GeneratedViews
        let json = serializeReport report
        Assert.Contains("\"refresh\"", json)
        Assert.Contains("\"perViewState\"", json)

    // --- 033 US3: the seeded constitution behaves like authored content ---

    let private editedConstitution =
        "# Our Ratified Constitution\n\nProject-specific principles ratified by this team.\n"

    // T010 (US3-AC1 / FR-008): an author-edited .fsgg/constitution.md survives a re-init
    // under the same no-clobber policy as the other authored skeleton files; the report
    // records it refused, not overwritten.
    [<Fact>]
    let ``re-init preserves an author-edited constitution`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.request Init root |> TestSupport.runRequest |> ignore
        TestSupport.writeRelative root ".fsgg/constitution.md" editedConstitution

        let report = TestSupport.request Init root |> TestSupport.runRequest

        Assert.Equal(editedConstitution, TestSupport.readRelative root ".fsgg/constitution.md")

        let change =
            report.ChangedArtifacts
            |> List.tryFind (fun c -> c.Path = ".fsgg/constitution.md")
            |> Option.defaultWith (fun () -> failwith "Expected a changed-artifact entry for the constitution.")

        Assert.Equal(ArtifactOperation.Refuse, change.Operation)
        Assert.Equal("refused", change.SafeWriteDecision)

    // T010 (US3-AC2 / FR-009/SC-004): refresh leaves an author-modified constitution
    // byte-unchanged and never reports it as a generated view, a blocked view, or a
    // generatedProduct path.
    [<Fact>]
    let ``refresh leaves the author-modified constitution untouched`` () =
        let root = TestSupport.tempDirectory ()
        let wid = "033-constitution-demo"
        TestSupport.request Init root |> TestSupport.runRequest |> ignore
        TestSupport.writeValidWorkSources root wid "Constitution demo"
        TestSupport.writeRelative root ".fsgg/constitution.md" editedConstitution

        let report = TestSupport.runRefresh root wid

        Assert.Equal(editedConstitution, TestSupport.readRelative root ".fsgg/constitution.md")
        Assert.DoesNotContain(report.GeneratedViews, fun v -> v.Path = ".fsgg/constitution.md")

        match report.Refresh with
        | Some summary ->
            Assert.DoesNotContain(".fsgg/constitution.md", summary.RefreshedViewIds)
            Assert.DoesNotContain(".fsgg/constitution.md", summary.BlockedViewIds)
        | None -> failwith "Expected a refresh summary."

    // --- 051: refresh preserves the seeded fs-gg-sdd-* process skills ---

    // T009 (US2 / FR-005): the seeded skills are authored SDD-owned skeleton, not a
    // refreshCanonicalView and not a provenance path. refresh leaves an author-edited
    // seeded skill byte-unchanged and never rewrites, enumerates, or blocks it.
    [<Fact>]
    let ``refresh leaves the seeded process skills untouched`` () =
        let root = TestSupport.tempDirectory ()
        let wid = "051-seeded-skills-demo"
        TestSupport.request Init root |> TestSupport.runRequest |> ignore
        TestSupport.writeValidWorkSources root wid "Seeded skills demo"
        let skillPath = ".claude/skills/fs-gg-sdd-plan/SKILL.md"
        let edited = TestSupport.readRelative root skillPath + "\n\nLOCAL EDIT\n"
        TestSupport.writeRelative root skillPath edited

        let report = TestSupport.runRefresh root wid

        Assert.Equal(edited, TestSupport.readRelative root skillPath)
        Assert.DoesNotContain(report.GeneratedViews, fun v -> v.Path = skillPath)

        match report.Refresh with
        | Some summary ->
            Assert.DoesNotContain(skillPath, summary.RefreshedViewIds)
            Assert.DoesNotContain(skillPath, summary.BlockedViewIds)
        | None -> failwith "Expected a refresh summary."

    // --- 049: early-stage navigable refresh (no work model yet) ---

    // An early-only fixture: a chartered work item with no work model and no authored
    // sources yet.
    let earlyStageProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        root

    // T012 (US2 / FR-005/006/011, SC-002): the pre-work-model state is a recognized,
    // navigable advisory — exit 0, refresh.earlyStageGuidance, a pointer NextAction, and
    // no view written — not a bare refresh.blockedUpstreamView dead end.
    [<Fact>]
    let ``refresh reports a navigable early-stage state when the work model is absent`` () =
        let root = earlyStageProject ()

        let report = TestSupport.runRefresh root workId

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.earlyStageGuidance"))
        Assert.DoesNotContain(report.Diagnostics, (fun d -> d.Id = "refresh.blockedUpstreamView"))
        TestSupport.assertRefreshDisposition report "early-stage"

        match report.NextAction with
        | Some action ->
            Assert.Equal("earlyStageGuidance", action.ActionId)
            Assert.Contains(".fsgg/early-stage-guidance.md", action.RequiredArtifacts)
        | None -> failwith "Expected an early-stage next action."

        // No view regenerated or written (FR-005/006/011).
        Assert.False(TestSupport.existsRelative root summaryPath)
        Assert.False(TestSupport.existsRelative root workModelPath)

        match report.Refresh with
        | Some summary -> Assert.Empty summary.RefreshedViewIds
        | None -> failwith "Expected a refresh summary."

    // T018 (US3 / SC-004): the early-stage refresh report is byte-identical across runs.
    [<Fact>]
    let ``refresh early-stage report is deterministic for identical state`` () =
        let root = earlyStageProject ()
        let first = serializeReport (TestSupport.runRefresh root workId)
        let second = serializeReport (TestSupport.runRefresh root workId)
        Assert.Equal(first, second)

    // --- User Story 2: detect stale / unrefreshable views ---

    [<Fact>]
    let ``refresh blocks views whose declared source is missing`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, $"work/{workId}/spec.md".Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.missingSource"))
        Assert.Equal("blocked", TestSupport.refreshViewState report "work-model")

    [<Fact>]
    let ``refresh names the upstream view when a dependent view is blocked`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, $"work/{workId}/spec.md".Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.blockedUpstreamView"))

    [<Fact>]
    let ``refresh refreshes a malformed existing generated view from current sources`` () =
        let root = shippedProject ()
        TestSupport.writeRelative root workModelPath "{ not valid json"

        let report = TestSupport.runRefresh root workId
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.malformedGeneratedView"))
        // The malformed view is regenerated from current sources, not left malformed.
        Assert.Equal("current", TestSupport.refreshViewState report "work-model")

    // --- User Story 3: human-readable summary projection ---

    [<Fact>]
    let ``refresh renders a generated summary projection of the structured readiness data`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.True(TestSupport.existsRelative root summaryPath, "Expected summary.md.")
        let summaryText = TestSupport.readRelative root summaryPath
        Assert.Contains("GENERATED by fsgg-sdd refresh", summaryText)
        Assert.Contains("DO NOT EDIT", summaryText)
        Assert.Contains($"# Readiness Summary — {workId}", summaryText)

    [<Fact>]
    let ``refresh summary per-view table matches the report per-view state`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId
        let summaryText = TestSupport.readRelative root summaryPath

        match report.Refresh with
        | Some summary ->
            for (view, state) in summary.PerViewState do
                Assert.Contains($"| {view} | {state} |", summaryText)
        | None -> failwith "Expected refresh summary."

    [<Fact>]
    let ``refresh blocks the summary and records a diagnostic when structured inputs are unusable`` () =
        let root = shippedProject ()
        File.Delete(Path.Combine(root, $"work/{workId}/spec.md".Replace('/', Path.DirectorySeparatorChar)))

        let report = TestSupport.runRefresh root workId
        Assert.Contains(report.Diagnostics, (fun d -> d.Id = "refresh.unrenderableSummary"))
        Assert.Equal("blocked", TestSupport.refreshViewState report "summary")
        Assert.False(TestSupport.existsRelative root summaryPath, "Summary must not be written from unusable data.")

    // --- User Story 4: authored sources authoritative, refresh repeatable ---

    [<Fact>]
    let ``refresh preserves authored sources and hand-owned guidance files`` () =
        let root = shippedProject ()

        let preserved =
            [ "CLAUDE.md"
              "AGENTS.md"
              ".fsgg/agents.yml"
              ".fsgg/project.yml"
              $"work/{workId}/spec.md"
              $"work/{workId}/tasks.yml"
              $"work/{workId}/evidence.yml" ]

        let before =
            preserved |> List.map (fun path -> path, TestSupport.readRelative root path)

        TestSupport.runRefresh root workId |> ignore

        for (path, text) in before do
            Assert.Equal(text, TestSupport.readRelative root path)

    [<Fact>]
    let ``refresh dry-run writes zero files but reports proposed changes`` () =
        let root = shippedProject ()

        let report =
            TestSupport.runRequest
                { TestSupport.refreshRequest root workId with
                    DryRun = true }

        // Views that did not yet exist must not be created by a dry run.
        Assert.False(TestSupport.existsRelative root summaryPath)
        Assert.False(TestSupport.existsRelative root claudeGuidance)
        Assert.NotEmpty report.ChangedArtifacts
        // Every proposed create/update is reported as a dry-run-only change (no file mutated).
        let mutations =
            report.ChangedArtifacts
            |> List.filter (fun change ->
                change.Operation = ArtifactOperation.Create
                || change.Operation = ArtifactOperation.Update)

        Assert.NotEmpty mutations
        Assert.All(mutations, (fun change -> Assert.Equal("dryRunOnly", change.SafeWriteDecision)))

    [<Fact>]
    let ``refresh rerun over a current project reports no change`` () =
        let root = fullyGeneratedProject ()
        let report = TestSupport.runRefresh root workId

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        // Refresh always re-plans deterministic generated writes; on a current project
        // every one resolves to NoChange/Preserve (nothing is actually rewritten).
        Assert.All(
            report.ChangedArtifacts,
            (fun change ->
                Assert.True(
                    change.Operation = ArtifactOperation.NoChange
                    || change.Operation = ArtifactOperation.Preserve,
                    $"Expected no mutation for {change.Path}, got {change.Operation}."
                ))
        )

        TestSupport.assertRefreshDisposition report "refreshed-current"

    // --- User Story 5: determinism and traceability ---

    [<Fact>]
    let ``refresh produces a byte-identical report across repeated runs`` () =
        let root = fullyGeneratedProject ()
        let first = serializeReport (TestSupport.runRefresh root workId)
        let second = serializeReport (TestSupport.runRefresh root workId)
        Assert.Equal(first, second)

    [<Fact>]
    let ``refresh produces a byte-identical summary across repeated runs`` () =
        let root = fullyGeneratedProject ()
        let first = TestSupport.readRelative root summaryPath
        File.Delete(Path.Combine(root, summaryPath.Replace('/', Path.DirectorySeparatorChar)))
        TestSupport.runRefresh root workId |> ignore
        let second = TestSupport.readRelative root summaryPath
        Assert.Equal(first, second)

    [<Fact>]
    let ``refresh text projection surfaces refresh facts present in the report`` () =
        let root = shippedProject ()
        let report = TestSupport.runRefresh root workId
        let text = renderText report

        Assert.Contains("refreshDisposition: refreshed-current", text)
        Assert.Contains("refreshView.work-model: current", text)

    // --- CLI smoke (real host entry point) ---

    let runRefreshCli root extraArgs =
        [ "refresh"; "--root"; root; "--work"; workId ] @ extraArgs
        |> TestSupport.runCliRaw 120000

    [<Fact; Trait("tier", "slow")>]
    let ``refresh CLI smoke regenerates views and exits zero`` () =
        let root = shippedProject ()
        let exitCode, stdout, _ = runRefreshCli root []
        Assert.Equal(0, exitCode)
        Assert.Contains("refresh", stdout)
        Assert.True(TestSupport.existsRelative root summaryPath)

    [<Fact; Trait("tier", "slow")>]
    let ``refresh CLI text smoke surfaces refresh disposition`` () =
        let root = shippedProject ()
        let exitCode, stdout, _ = runRefreshCli root [ "--text" ]
        Assert.Equal(0, exitCode)
        Assert.Contains("refreshDisposition: refreshed-current", stdout)

    [<Fact; Trait("tier", "slow")>]
    let ``refresh CLI dry-run smoke writes nothing`` () =
        let root = shippedProject ()
        let exitCode, _, _ = runRefreshCli root [ "--dry-run" ]
        Assert.Equal(0, exitCode)
        Assert.False(TestSupport.existsRelative root summaryPath)

    [<Fact; Trait("tier", "slow")>]
    let ``refresh CLI smoke names ship json as the malformed artifact`` () =
        // Feature 095 through the REAL host binary -- the JSON an operator or CI job actually reads.
        // Matrix cell 5, quickstart Scenario A. The in-process tests assert the CommandReport; this
        // asserts the bytes on stdout, which is the surface the contract is stated in.
        let root = shippedProject ()
        File.WriteAllText(absolute root shipPath, "{ \"schemaVersion\": 99 }")

        let exitCode, stdout, stderr = runRefreshCli root []

        // The exit code did not move (FR-007): the run always failed, it just blamed the wrong file.
        Assert.Equal(1, exitCode)

        // A `Blocked` report routes to STDOUT — the automation contract — not stderr, per
        // FS.GG.SDD#535, so an operator can `refresh | jq` a blocked run; stderr stays empty and the
        // exit code signals blocked. Asserted here so the stream routing stays part of what this smoke
        // pins (FR-015).
        Assert.Equal("", stderr.Trim())

        use document = JsonDocument.Parse stdout

        // `generatedViews[]` is keyed by `kind`, not by a `viewId` field.
        let currencyOf kind =
            document.RootElement.GetProperty("generatedViews").EnumerateArray()
            |> Seq.find (fun view -> view.GetProperty("kind").GetString() = kind)
            |> fun view -> view.GetProperty("currency").GetString()

        Assert.Equal("malformed", currencyOf "ship") // FR-003: was "current"
        Assert.Equal("blocked", currencyOf "ship-verdict") // FR-004: was "malformed"

        // FR-017. `malformed` must name exactly ONE artifact in this report -- ship.json, whose bytes
        // really do not parse. Every other view is `blocked` on it. Asserting the whole set (rather than
        // ship-verdict alone) is what caught `governance-handoff` inheriting `malformed` from its source.
        let malformedKinds =
            document.RootElement.GetProperty("generatedViews").EnumerateArray()
            |> Seq.filter (fun view -> view.GetProperty("currency").GetString() = "malformed")
            |> Seq.map (fun view -> string (view.GetProperty("kind").GetString()))
            |> List.ofSeq

        Assert.Equal<string list>([ "ship" ], malformedKinds)
        Assert.Equal("blocked", currencyOf "governance-handoff")

        // The committed verdict is untouched, and demonstrably well-formed -- it was never malformed.
        use _ = JsonDocument.Parse(TestSupport.readRelative root shipVerdictPath)
        ()

    // --- 056 US3 (T023 / FR-009 / P8): refresh re-mirrors the three-root union ---

    [<Fact>]
    let ``refresh re-mirrors the provider skill union across all three roots`` () =
        let root = shippedProject ()

        // Simulate a scaffolded product: a provider co-tenant skill in the neutral root,
        // already mirrored into .claude/.codex.
        let elmishBody = "# fs-gg-elmish\nprovider co-tenant skill\n"

        for r in [ ".agents"; ".claude"; ".codex" ] do
            TestSupport.writeRelative root $"{r}/skills/fs-gg-elmish/SKILL.md" elmishBody

        // Drift: delete a mirror copy of the provider skill AND a seeded .agents copy.
        let seededName = List.head FS.GG.SDD.Commands.Internal.SeededSkills.skillNames

        File.Delete(
            Path.Combine(root, ".claude/skills/fs-gg-elmish/SKILL.md".Replace('/', Path.DirectorySeparatorChar))
        )

        File.Delete(
            Path.Combine(root, $".agents/skills/{seededName}/SKILL.md".Replace('/', Path.DirectorySeparatorChar))
        )

        TestSupport.runRefresh root workId |> ignore

        let bytesAt (p: string) =
            File.ReadAllBytes(Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar)))

        // The provider skill is re-mirrored byte-identically across all three roots.
        for r in [ ".agents"; ".claude"; ".codex" ] do
            Assert.True(
                TestSupport.existsRelative root $"{r}/skills/fs-gg-elmish/SKILL.md",
                $"expected {r} co-tenant copy"
            )

        Assert.Equal<byte[]>(
            bytesAt ".agents/skills/fs-gg-elmish/SKILL.md",
            bytesAt ".claude/skills/fs-gg-elmish/SKILL.md"
        )

        Assert.Equal<byte[]>(
            bytesAt ".agents/skills/fs-gg-elmish/SKILL.md",
            bytesAt ".codex/skills/fs-gg-elmish/SKILL.md"
        )

        // The deleted seeded .agents copy is refilled and byte-identical to its .claude sibling.
        Assert.True(
            TestSupport.existsRelative root $".agents/skills/{seededName}/SKILL.md",
            "expected the seeded .agents copy refilled"
        )

        Assert.Equal<byte[]>(
            bytesAt $".claude/skills/{seededName}/SKILL.md",
            bytesAt $".agents/skills/{seededName}/SKILL.md"
        )
