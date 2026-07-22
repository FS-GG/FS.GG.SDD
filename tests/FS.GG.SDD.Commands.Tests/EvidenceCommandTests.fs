namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.TestShared
open Xunit

// Joins ProcessGlobalEnv: the CLI smoke here spawns a PATH-resolved process, so it must not
// run while a sibling mutates process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module EvidenceCommandTests =
    let workId = "011-evidence-command"
    let title = "Evidence Command"
    let evidencePath = $"work/{workId}/evidence.yml"
    let analysisPath = $"readiness/{workId}/analysis.json"
    let workModelPath = $"readiness/{workId}/work-model.json"

    type CliResult =
        { ExitCode: int
          StdOut: string
          StdErr: string }

    let initializedAnalyzedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        root

    let runEvidenceCli root extraArgs =
        let exitCode, stdout, stderr =
            [ "evidence"; "--root"; root; "--work"; workId ] @ extraArgs
            |> TestSupport.runCliRaw 30000

        { ExitCode = exitCode
          StdOut = stdout
          StdErr = stderr }

    let private passingTrx count =
        $"""<?xml version="1.0" encoding="UTF-8"?>
<TestRun id="done-task-bootstrap" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary outcome="Completed">
    <Counters total="{count}" executed="{count}" passed="{count}" failed="0" error="0" notExecuted="0" />
  </ResultSummary>
</TestRun>"""

    let undisclosedSyntheticInput =
        """schemaVersion: 1
workId: 011-evidence-command
stage: evidence
status: evidenceReady
evidence:
  - id: EV999
    kind: synthetic
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: []
    obligationRefs: [EV001]
    sourceRefs:
      - kind: transcript
        path: specs/011-evidence-command/readiness/synthetic-fsi.txt
        result: pass
    result: pass
    synthetic: true
"""

    [<Fact>]
    let ``evidence missingRequiredEvidence correction shows the non-synthetic pass form`` () =
        // FR-008: the surfaced unsatisfied-obligation diagnostic shows what satisfies it.
        let diagnostic =
            FS.GG.SDD.Commands.CommandReports.missingRequiredEvidence evidencePath [ "EV001" ]

        Assert.Equal("evidence.missingRequiredEvidence", diagnostic.Id)
        Assert.Contains("result: pass", diagnostic.Correction)
        Assert.Contains("synthetic: false", diagnostic.Correction)

    [<Fact>]
    let ``evidence creates authored evidence artifact with real filesystem evidence`` () =
        let root = initializedAnalyzedProject ()

        let report = TestSupport.runEvidence root workId title
        let evidence = TestSupport.readRelative root evidencePath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertEvidenceSummary report "evidenceReady"
        Assert.Contains("stage: evidence", evidence)
        Assert.Contains("status: evidenceReady", evidence)
        Assert.Contains($"sourceAnalysis: {analysisPath}", evidence)
        Assert.Contains("sourceSnapshots:", evidence)

        // `evidence.yml` is a hybrid: the author declares the obligations, and re-running the stage
        // re-derives the tool-owned header and `sourceSnapshots` around them (FS.GG.SDD#308).
        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = evidencePath && change.Kind = "hybridArtifact"
        )

        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath)
        Assert.Equal(Some "evidence.next.verify", report.NextAction |> Option.map _.ActionId)

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> Assert.Equal("evidenceReady", artifact.Status)
        | Error diagnostics -> failwith $"Generated evidence artifact did not parse: {diagnostics}."

    [<Fact>]
    let ``evidence bootstraps declarations after implementation without rolling task status backward`` () =
        let root = TestSupport.tempDirectory ()
        let tasksPath = $"work/{workId}/tasks.yml"
        let provingTest = "tests/DoneTaskBootstrap.Tests/FeatureTests.fs"
        let testReport = "artifacts/done-task-bootstrap.trx"

        // The supported lifecycle order: analyze while work is pending, implement, then record the
        // honest completed status before the first evidence scaffold exists.
        TestSupport.initializePlanReadyProject root workId title
        TestSupport.runTasks root workId title |> ignore
        TestSupport.runAnalyze root workId title |> ignore

        let doneTasks =
            TestSupport.readRelative root tasksPath
            |> fun text -> text.Replace("status: pending", "status: done")

        TestSupport.writeRelative root tasksPath doneTasks
        Assert.False(TestSupport.existsRelative root evidencePath)

        let bootstrap =
            { TestSupport.evidenceRequest root workId title with
                FromTests = Some provingTest }
            |> TestSupport.runRequest

        Assert.NotEqual(CommandOutcome.Blocked, bootstrap.Outcome)
        Assert.DoesNotContain(bootstrap.Diagnostics, fun diagnostic -> diagnostic.Id = "doneTaskMissingEvidence")
        Assert.Equal(doneTasks, TestSupport.readRelative root tasksPath)

        let scaffold = TestSupport.readRelative root evidencePath
        Assert.Contains("kind: missing", scaffold)
        Assert.Contains("result: missing", scaffold)
        Assert.DoesNotContain("observedRun:", scaffold)

        // Scaffolding is not a bypass: missing declarations still stop verify.
        let beforeAuthoring = TestSupport.runVerify root workId title
        Assert.Equal(CommandOutcome.Blocked, beforeAuthoring.Outcome)

        // Author the declarations honestly, materialize their cited test, then register the run SDD
        // actually observed. No task status is rewritten at any point.
        TestSupport.writeRelative root provingTest "module DoneTaskBootstrap.Tests.FeatureTests\n"

        scaffold
        |> fun text -> text.Replace("kind: missing", "kind: verification").Replace("result: missing", "result: pass")
        |> TestSupport.writeRelative root evidencePath

        TestSupport.runEvidence root workId title |> ignore

        let beforeReceipt =
            { TestSupport.verifyRequest root workId title with
                RequireObserved = true }
            |> TestSupport.runRequest

        Assert.Equal(CommandOutcome.Blocked, beforeReceipt.Outcome)

        TestSupport.writeRelative root testReport (passingTrx 1)

        let receiptReport =
            { TestSupport.evidenceRequest root workId title with
                FromTestReport = Some testReport }
            |> TestSupport.runRequest

        Assert.DoesNotContain(
            receiptReport.Diagnostics,
            fun diagnostic -> diagnostic.Severity = Diagnostics.DiagnosticError
        )

        let received = TestSupport.readRelative root evidencePath
        Assert.Contains("observedRun:", received)
        Assert.Equal(doneTasks, TestSupport.readRelative root tasksPath)

        let afterReceipt =
            { TestSupport.verifyRequest root workId title with
                RequireObserved = true }
            |> TestSupport.runRequest

        Assert.NotEqual(CommandOutcome.Blocked, afterReceipt.Outcome)

        // Repeating the same bootstrap/receipt operation is byte-idempotent and no-clobber.
        let repeat =
            { TestSupport.evidenceRequest root workId title with
                FromTestReport = Some testReport }
            |> TestSupport.runRequest

        Assert.DoesNotContain(repeat.Diagnostics, fun diagnostic -> diagnostic.Severity = Diagnostics.DiagnosticError)
        Assert.Equal(received, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence missing analysis blocks without authored evidence write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeTasksReadyProject root workId title
        let before = TestSupport.readRelative root evidencePath

        let report = TestSupport.runEvidence root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.missingAnalysisPrerequisite")
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence does not require Governance files`` () =
        let root = initializedAnalyzedProject ()

        let report = TestSupport.runEvidence root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")
        // Arguments were reversed: xUnit's (string, string) overload is
        // `DoesNotContain(expectedSubstring, actualString)`, so this asserted that the 5-char string
        // "route" does not contain the whole JSON report — vacuously true for every possible report.
        // The "no Governance leakage" guard this line exists for never fired. Corrected.
        Assert.DoesNotContain("route", serializeReport report)

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``evidence dry run reports authored update without mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let request =
            { TestSupport.evidenceRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        TestSupport.assertEvidenceSummary report "evidenceReady"
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = evidencePath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``evidence blocks undisclosed synthetic evidence without mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let request =
            { TestSupport.evidenceRequest root workId title with
                InputText = Some undisclosedSyntheticInput }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.undisclosedSyntheticEvidence")
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence deterministic JSON is byte stable`` () =
        let root = initializedAnalyzedProject ()

        let request =
            { TestSupport.evidenceRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"evidence\"", first)
        Assert.Contains("\"evidence\"", first)
        Assert.DoesNotContain(root, first)

    [<Fact>]
    let ``evidence text projection uses report facts`` () =
        let root = initializedAnalyzedProject ()
        let report = TestSupport.runEvidence root workId title
        let text = renderText report

        Assert.Contains("command: evidence", text)
        Assert.Contains($"evidencePath: {evidencePath}", text)
        Assert.Contains("evidenceReadiness: evidenceReady", text)
        Assert.Contains("nextAction: evidence.next.verify", text)

    [<Fact; Trait("tier", "slow")>]
    let ``evidence CLI JSON smoke creates evidence artifact`` () =
        let root = initializedAnalyzedProject ()

        let result = runEvidenceCli root []

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"evidence\"", result.StdOut)
        Assert.Contains("\"evidence.next.verify\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.True(TestSupport.existsRelative root evidencePath)

    [<Fact; Trait("tier", "slow")>]
    let ``evidence CLI dry run smoke avoids authored mutation`` () =
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let result = runEvidenceCli root [ "--dry-run" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("\"name\": \"evidence\"", result.StdOut)
        Assert.Contains("\"safeWriteDecision\": \"dryRunOnly\"", result.StdOut)
        Assert.Equal("", result.StdErr)
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact; Trait("tier", "slow")>]
    let ``evidence CLI text smoke renders human projection`` () =
        let root = initializedAnalyzedProject ()

        let result = runEvidenceCli root [ "--text" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("command: evidence", result.StdOut)
        Assert.Contains("evidenceReadiness: evidenceReady", result.StdOut)
        Assert.Contains("nextAction: evidence.next.verify", result.StdOut)
        Assert.Equal("", result.StdErr)

    // Feature 077 (issue #124): auto-generated obligations must carry their originating
    // requirement / plan-decision refs so an author can classify each obligation from the
    // evidence.yml entry alone — no join back to tasks.yml by title. The default lifecycle graph
    // produced by initializeAnalyzedProject includes a plan-decision task
    // (`Implement plan decision PD-001`, sourceIds ["AC-001","FR-001","PD-001"]).
    //
    // initializeTasksReadyProject seeds a passing evidence.yml (byte-compatible authored fixture),
    // which the evidence stage correctly no-clobbers; to exercise fresh *scaffolding* we remove
    // that seed first so the merge takes the scaffold-from-obligations path.
    let private scaffoldFreshEvidence root =
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))
        TestSupport.runEvidence root workId title |> ignore
        let evidence = TestSupport.readRelative root evidencePath

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> artifact
        | Error diagnostics -> failwith $"Scaffolded evidence artifact did not parse: {diagnostics}."

    let private declarationForTask (artifact: EvidenceArtifact) taskId =
        artifact.Evidence
        |> List.tryFind (fun declaration -> declaration.Subject.Id = taskId)
        |> Option.defaultWith (fun () -> failwith $"No scaffolded obligation for task {taskId}.")

    [<Fact>]
    let ``evidence scaffolds no separate obligation for a plan decision folded into its requirement`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // #310 (AC9): `PD-001` mirrors `FR-001`'s own refs, so `tasks` folds it into the requirement
        // task instead of deriving an `Implement plan decision PD-001` task over the identical
        // FR/AC set. There is therefore no obligation of its own to scaffold — the duplicate
        // obligation this fixture used to produce (its own T002) is exactly what #310 removed.
        let planDecisionTasks =
            artifact.Evidence
            |> List.filter (fun declaration ->
                declaration.PlanDecisionRefs |> List.exists (fun ref -> ref.Value = "PD-001"))
            |> List.map (fun declaration -> declaration.Subject.Id)

        Assert.DoesNotContain("Implement plan decision", TestSupport.readRelative root $"work/{workId}/tasks.yml")
        // PD-001 survives as a ref on the tasks whose sourceIds carry it, never as its own task.
        Assert.NotEmpty(planDecisionTasks)

    [<Fact>]
    let ``evidence requirement obligation carries requirementRefs and the folded planDecisionRefs`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // T001 is the `Implement requirement FR-001` task. Since #310 it also disposes PD-001,
        // the plan decision derived from FR-001's own refs, so its obligation carries both.
        let declaration = declarationForTask artifact "T001"

        Assert.Equal<string list>([ "FR-001" ], declaration.RequirementRefs |> List.map _.Value)
        Assert.Equal<string list>([ "PD-001" ], declaration.PlanDecisionRefs |> List.map _.Value)

    [<Fact>]
    let ``evidence obligation refs never misroute a plan decision into clarification refs`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // PD-001 is a plan decision, not a clarification decision (DEC-###) — it must land only in
        // planDecisionRefs, never clarificationDecisionRefs. T001 is the task that disposes it
        // since #310 folded it in.
        let declaration = declarationForTask artifact "T001"

        Assert.Empty(declaration.ClarificationDecisionRefs)
        Assert.Contains("PD-001", declaration.PlanDecisionRefs |> List.map _.Value)

    [<Fact>]
    let ``evidence scaffolding preserves refs deterministically across fresh runs`` () =
        let rootA = initializedAnalyzedProject ()
        let rootB = initializedAnalyzedProject ()

        let refsOf root =
            scaffoldFreshEvidence root
            |> fun artifact ->
                artifact.Evidence
                |> List.map (fun d ->
                    d.Subject.Id, d.RequirementRefs |> List.map _.Value, d.PlanDecisionRefs |> List.map _.Value)

        // Two fresh scaffolds from identical inputs route identical, sorted, de-duplicated refs.
        Assert.Equal<(string * string list * string list) list>(refsOf rootA, refsOf rootB)

    [<Fact>]
    let ``evidence does not clobber authored declarations on an existing evidence file`` () =
        // The seeded passing evidence.yml carries no ref fields; a re-run must preserve the
        // authored declarations (FR-006 no-clobber) rather than re-scaffolding populated refs over
        // them. sourceSnapshots legitimately refresh, so compare declarations, not raw bytes.
        let root = initializedAnalyzedProject ()

        let declarations () =
            match
                parseEvidenceArtifact
                    { Path = evidencePath
                      Text = TestSupport.readRelative root evidencePath }
            with
            | Ok artifact -> artifact.Evidence
            | Error diagnostics -> failwith $"Evidence did not parse: {diagnostics}."

        let planRefsForT002 () =
            declarations ()
            |> List.find (fun declaration -> declaration.Subject.Id = "T002")
            |> fun declaration -> declaration.PlanDecisionRefs

        // The authored T002 obligation has no plan-decision ref. A re-run must NOT re-scaffold
        // PD-001 over the authored declaration — scaffolding is confined to obligations with no
        // authored evidence (FR-006 no-clobber).
        Assert.Empty(planRefsForT002 ())
        TestSupport.runEvidence root workId title |> ignore
        Assert.Empty(planRefsForT002 ())

    // ---- Feature 091: slim the evidence declaration shape ----------------------------------
    // The writer omits these five always-null optional fields instead of emitting `<key>: null`.
    // FR-009 (omission must not silence the synthetic-disclosure diagnostic) is already covered by
    // `evidence blocks undisclosed synthetic evidence without mutation` above: its input carries no
    // `syntheticDisclosure` key at all, proving the diagnostic derives from the parsed model rather
    // than from a `null` line in the text.
    //
    // Anchored to the newline + the writer's 4-space declaration indent. An unanchored `"scope:"`
    // would also match a task title or note value that happens to contain the word, failing this
    // test for a reason that has nothing to do with optional-field omission.
    let private slimmedOptionalKeys =
        [ "\n    syntheticDisclosure:"
          "\n    rationale:"
          "\n    owner:"
          "\n    scope:"
          "\n    laterLifecycleVisibility:" ]

    [<Fact>]
    let ``evidence re-run over a scaffolded file is byte-idempotent`` () =
        // RENAMED (feature 091). This test used to be named for issue #161 ("bare null optional
        // scalars are not rewritten to \"null\"") and asserted `Contains("rationale: null", second)`
        // as its load-bearing precondition — that precondition is what made its sibling
        // `DoesNotContain("… \"null\"")` assertions mean anything.
        //
        // Post-091 the scaffolded file contains no bare-null token at all, so `isPlainNullScalar` is
        // never reached on this path and every "not rewritten to \"null\"" assertion here would be
        // satisfied by *absence* rather than by correct null handling — i.e. vacuous. Keeping the old
        // name would promise coverage this test no longer provides: reverting `tryScalarNonNullAt` to
        // `tryScalarAt` in Evidence.fs would leave it green.
        //
        // What it still guards, and all it claims to guard, is scaffold re-run byte-idempotence.
        // The actual #161 bare-null path is guarded by, and only by:
        //   • `evidence normalizes an authored bare-null declaration to the slim shape, then settles`
        //   • `EvidenceArtifactTests.parseEvidenceArtifact reads every plain null token and an omitted key as None`
        //   • `evidence preserves a quoted "null" rationale as a string value` (the other side)
        let root = initializedAnalyzedProject ()
        TestSupport.runEvidence root workId title |> ignore
        let first = TestSupport.readRelative root evidencePath
        TestSupport.runEvidence root workId title |> ignore
        let second = TestSupport.readRelative root evidencePath

        Assert.Equal(first, second)

        for key in slimmedOptionalKeys do
            Assert.DoesNotContain(key, second)

    /// `String.Replace` has no occurrence-count overload; splice at the first hit only, so exactly
    /// one declaration is mutated and the others stay in the slim shape.
    let private replaceFirst (needle: string) (replacement: string) (text: string) =
        match text.IndexOf(needle, System.StringComparison.Ordinal) with
        | -1 -> failwith $"Fixture drift: expected to find '{needle}' in the emitted evidence.yml."
        | index -> text.Substring(0, index) + replacement + text.Substring(index + needle.Length)

    /// Rewrite the first recorded snapshot digest to one no source can hash to, so the artifact's
    /// recorded digests no longer match the sources on disk. The writer emits `sourceSnapshots:`
    /// before `evidence:`, so the first `    digest: ` line is a snapshot's, not a `sourceRefs[]`
    /// entry's.
    let private staleFirstSnapshotDigest root =
        let lines = (TestSupport.readRelative root evidencePath).Split('\n')

        match
            lines
            |> Array.tryFindIndex (fun line -> line.StartsWith("    digest: ", System.StringComparison.Ordinal))
        with
        | None -> failwith "Fixture drift: expected a `    digest: ` snapshot line in the emitted evidence.yml."
        | Some index ->
            lines[index] <- "    digest: " + String.replicate 64 "0"
            lines |> String.concat "\n" |> TestSupport.writeRelative root evidencePath

    [<Fact>]
    let ``evidence reports staleEvidenceSource when a recorded source digest has drifted`` () =
        // #216: `evidence` re-stamps SourceSnapshots to the freshly computed ones before validating.
        // Validating the re-stamped artifact compared `currentSnapshots` against itself, so this
        // branch was structurally dead. It must compare against what the artifact *recorded*.
        let root = initializedAnalyzedProject ()
        TestSupport.runEvidence root workId title |> ignore
        staleFirstSnapshotDigest root

        let report = TestSupport.runEvidence root workId title

        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.staleEvidenceSource")

    [<Fact>]
    let ``evidence does not report staleEvidenceSource on a clean re-run`` () =
        // The other side of #216: the check must stay silent when nothing drifted, and must not fire
        // on the first run (where the recorded snapshot list is empty).
        let root = initializedAnalyzedProject ()

        let first = TestSupport.runEvidence root workId title
        let second = TestSupport.runEvidence root workId title

        for report in [ first; second ] do
            Assert.DoesNotContain(report.Diagnostics, fun d -> d.Id = "evidence.staleEvidenceSource")

    /// Author `injected` YAML directly after the first `synthetic:` line, then re-run `evidence` so
    /// the writer re-renders what the reader parsed.
    let private authorAfterSynthetic root (syntheticLine: string) (injectedLines: string list) =
        TestSupport.runEvidence root workId title |> ignore

        let replacement =
            (sprintf "    %s\n" syntheticLine)
            + (injectedLines |> List.map (sprintf "%s\n") |> String.concat "")

        TestSupport.readRelative root evidencePath
        |> replaceFirst "    synthetic: false\n" replacement
        |> TestSupport.writeRelative root evidencePath

        let report = TestSupport.runEvidence root workId title
        report, TestSupport.readRelative root evidencePath

    [<Fact>]
    let ``evidence omits the always-null optional declaration fields`` () =
        // FR-001, FR-002, SC-001.
        let root = initializedAnalyzedProject ()
        let report = TestSupport.runEvidence root workId title
        let evidence = TestSupport.readRelative root evidencePath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        for key in slimmedOptionalKeys do
            Assert.DoesNotContain(key, evidence)

        // The decision-bearing fields are untouched.
        Assert.Contains("    kind: ", evidence)
        Assert.Contains("    result: ", evidence)
        Assert.Contains("    synthetic: ", evidence)
        Assert.Contains("    notes: ", evidence)

        // SC-002 falls out of the assertions above rather than needing a line count. Between
        // `synthetic:` and `notes:` the writer emits *only* the five optional keys; asserting all
        // five are absent therefore proves `notes:` follows `synthetic:` directly for every
        // declaration, i.e. the file is exactly 5 × N lines shorter than the pre-091 rendering.
        // The adjacency check below documents that shape without counting declarations — counting
        // would couple the test to the fixture's `synthetic: false` value for no added coverage.
        Assert.Contains("    synthetic: false\n    notes: ", evidence)

    [<Fact>]
    let ``evidence emits no blank line or trailing whitespace when optional fields are omitted`` () =
        // FR-004. Guards the naive `| None -> ""` fix, which would leave five blank lines per
        // declaration: no `null` keys, but malformed-looking YAML.
        let root = initializedAnalyzedProject ()
        TestSupport.runEvidence root workId title |> ignore
        let evidence = TestSupport.readRelative root evidencePath

        let lines = evidence.Split('\n')

        let offenders =
            lines
            |> Array.indexed
            // The document's trailing newline yields one final empty element; ignore only that.
            |> Array.filter (fun (index, _) -> index < lines.Length - 1)
            |> Array.filter (fun (_, line) -> line = "" || line.TrimEnd() <> line)
            |> Array.map (fun (index, line) -> $"line {index + 1}: '{line}'")

        Assert.Empty(offenders)

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok _ -> ()
        | Error diagnostics -> failwith $"Slimmed evidence artifact did not parse: {diagnostics}."

    [<Fact>]
    let ``evidence preserves a populated syntheticDisclosure across a re-render`` () =
        // FR-003. Values are never omitted — only `None` is. The disclosure now renders through the
        // shared `EvidenceCodec.disclosureFields` (FS.GG.SDD#260), which quotes minimally: these
        // space-bearing values are safe YAML plain scalars, so they round-trip bare. (The generative
        // round-trip proof lives in EvidenceRoundTripPropertyTests; this confirms a populated
        // disclosure survives a real CLI re-render.)
        let root = initializedAnalyzedProject ()

        let injected =
            [ "    syntheticDisclosure:"
              "      standsInFor: a real headless render"
              "      reason: no GPU on the CI runner" ]

        let report, evidence = authorAfterSynthetic root "synthetic: true" injected

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("    syntheticDisclosure:\n", evidence)
        Assert.Contains("      standsInFor: a real headless render", evidence)
        Assert.Contains("      reason: no GPU on the CI runner", evidence)

    [<Fact>]
    let ``evidence preserves populated optional scalars across a re-render`` () =
        // FR-003 for the four scalar optionals.
        let root = initializedAnalyzedProject ()

        let injected =
            [ "    rationale: accepted deferral see DEC-004"
              "    owner: platform"
              "    scope: workspace"
              "    laterLifecycleVisibility: verify" ]

        let report, evidence = authorAfterSynthetic root "synthetic: false" injected

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        // The codec quotes minimally (FS.GG.SDD#260): these safe plain scalars round-trip bare.
        Assert.Contains("    rationale: accepted deferral see DEC-004", evidence)
        Assert.Contains("    owner: platform", evidence)
        Assert.Contains("    scope: workspace", evidence)
        Assert.Contains("    laterLifecycleVisibility: verify", evidence)

    [<Fact>]
    let ``evidence sourceRefs round-trip preserves id, digest and relatedSourceId (#181)`` () =
        // FS.GG.SDD#181: `id`, `digest` and `relatedSourceId` were read by the parser but never
        // re-emitted, so a re-render silently dropped them. Render a fully-populated declaration
        // and parse it back — every authored sourceRef field must survive parse ∘ render ∘ parse.
        let seed =
            "schemaVersion: 1\n"
            + "workId: 011-evidence-command\n"
            + "stage: evidence\n"
            + "status: evidenceReady\n"
            + "sourceSnapshots: []\n"
            + "evidence:\n"
            + "  - id: EV001\n"
            + "    kind: verification\n"
            + "    subject:\n      type: task\n      id: T001\n"
            + "    taskRefs: [T001]\n"
            + "    sourceRefs:\n"
            + "      - kind: test-output\n"
            + "        id: SR-001\n"
            + "        path: specs/x/proof.txt\n"
            + "        uri: https://ci/run/1\n"
            + "        digest: deadbeefcafe\n"
            + "        relatedSourceId: SRC-42\n"
            + "        result: pass\n"
            + "    result: pass\n"
            + "    synthetic: false\n"
            + "    notes: []\n"
            + "lifecycleNotes:\n  - Next lifecycle action: verify.\n"

        let seededArtifact =
            match parseEvidenceArtifact { Path = evidencePath; Text = seed } with
            | Ok artifact -> artifact
            | Error diagnostics -> failwith $"Seed evidence did not parse: {diagnostics}."

        let declaration = Assert.Single(seededArtifact.Evidence)

        // Guard the fixture: the seed itself must actually carry the three previously-dropped fields.
        let seeded = Assert.Single(declaration.SourceRefs)
        Assert.Equal(Some "SR-001", seeded.ReferenceId)
        Assert.Equal(Some "deadbeefcafe", seeded.Digest)
        Assert.Equal(Some "SRC-42", seeded.RelatedSourceId)

        // Re-render the whole artifact through the real writer (the `evidence` block is now a codec
        // recordList), then parse it back — parse ∘ render ∘ parse.
        let reRendered =
            HandlersEvidence.evidenceArtifactText
                "011-evidence-command"
                seededArtifact
                (HandlersEvidence.evidenceSummary "011-evidence-command" seededArtifact [])

        match
            parseEvidenceArtifact
                { Path = evidencePath
                  Text = reRendered }
        with
        | Ok artifact ->
            let reference = Assert.Single((Assert.Single(artifact.Evidence)).SourceRefs)
            Assert.Equal("test-output", reference.Kind)
            Assert.Equal(Some "SR-001", reference.ReferenceId)
            Assert.Equal(Some "specs/x/proof.txt", reference.Path)
            Assert.Equal(Some "https://ci/run/1", reference.Uri)
            Assert.Equal(Some "deadbeefcafe", reference.Digest)
            Assert.Equal(Some "SRC-42", reference.RelatedSourceId)
            Assert.Equal(Some "pass", reference.Result)
        | Error diagnostics -> failwith $"Re-rendered evidence did not parse: {diagnostics}."

    let bareNullDisclosureInput =
        """schemaVersion: 1
workId: 011-evidence-command
stage: evidence
status: evidenceReady
evidence:
  - id: EV999
    kind: synthetic
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: []
    obligationRefs: [EV001]
    sourceRefs:
      - kind: transcript
        path: specs/011-evidence-command/readiness/synthetic-fsi.txt
        result: pass
    result: pass
    synthetic: true
    syntheticDisclosure:
      standsInFor: null
      reason: null
"""

    [<Fact>]
    let ``evidence blocks a synthetic pass whose syntheticDisclosure is bare null (#180)`` () =
        // FS.GG.SDD#180: an explicit `standsInFor: null` / `reason: null` is *absence*, so the
        // undisclosed-synthetic gate must fire exactly as it does when the block is omitted. Before
        // the null-aware read these parsed to Some "null" and the gate was silently bypassed.
        let root = initializedAnalyzedProject ()
        let before = TestSupport.readRelative root evidencePath

        let request =
            { TestSupport.evidenceRequest root workId title with
                InputText = Some bareNullDisclosureInput }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.undisclosedSyntheticEvidence")
        Assert.Equal(before, TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``evidence preserves an authored lifecycleNotes across a re-run (#181)`` () =
        // FS.GG.SDD#181: the renderer hardcoded the canned "verify" note, clobbering whatever the
        // author wrote on every re-run. A custom note must now survive, and the canned default must
        // not be re-injected over it.
        let root = initializedAnalyzedProject ()
        TestSupport.runEvidence root workId title |> ignore

        TestSupport.readRelative root evidencePath
        |> replaceFirst "  - \"Next lifecycle action: verify.\"\n" "  - \"Investigate the flaky GPU probe first.\"\n"
        |> TestSupport.writeRelative root evidencePath

        let report = TestSupport.runEvidence root workId title
        let evidence = TestSupport.readRelative root evidencePath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("Investigate the flaky GPU probe first.", evidence)
        Assert.DoesNotContain("Next lifecycle action: verify.", evidence)

    [<Fact>]
    let ``evidence preserves a quoted "null" rationale as a string value`` () =
        // FR-006, the feature-161 boundary stated positively. A *quoted* "null" is a real string:
        // it parses to Some "null" and must be re-emitted quoted — never omitted, never unquoted.
        //
        // `rationale: "null"` alone would be self-satisfying: the assertion would hold even if the
        // writer never re-rendered the file, because those are the bytes we injected. The unquoted
        // `owner` is the witness — it can only come back quoted if the round-trip actually ran.
        let root = initializedAnalyzedProject ()

        let report, evidence =
            authorAfterSynthetic root "synthetic: false" [ "    rationale: \"null\""; "    owner: platform" ]

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("    owner: platform", evidence) // witness: the writer re-rendered (bare, #260)
        Assert.Contains("    rationale: \"null\"", evidence) // the null token stays quoted so it round-trips

    [<Fact>]
    let ``evidence round-trips a populated deferral declaration through the slim writer`` () =
        // FR-010. Feature 091 removes the four `rationale`/`owner`/`scope`/`laterLifecycleVisibility`
        // hint lines from the scaffold, and those four are exactly what `evidence.missingDeferralRationale`
        // requires of a `kind: deferral` declaration (RequiredKeys.requiredDeferralKeys).
        //
        // The *blocking* half of FR-010 is NOT re-tested here: `RequiredFieldContractTests`'
        // `Omitting any required deferral field blocks the evidence gate` is a registry-derived
        // [<Theory>] over all four fields that builds the deferral by omitting the key entirely, so
        // it already proves (a) absent key parses as None and (b) the gate blocks. Duplicating it
        // with a single hardcoded case would be weaker and would drift from the registry.
        //
        // What no test covers is the *positive* path: a fully populated deferral passes the gate,
        // reaches the writer, and comes back with all four fields intact. `renderEvidenceDeclaration`
        // does not branch on `kind`, so this is an integration guard rather than a distinct writer
        // path — it is what would catch a future kind-dependent rendering that silently drops them.
        let root = initializedAnalyzedProject ()

        let injected =
            [ "    rationale: no GPU on the CI runner"
              "    owner: platform"
              "    scope: the headless render check"
              "    laterLifecycleVisibility: verify" ]

        TestSupport.runEvidence root workId title |> ignore

        TestSupport.readRelative root evidencePath
        |> replaceFirst "    kind: verification\n" "    kind: deferral\n"
        |> replaceFirst "    result: pass\n" "    result: deferred\n"
        |> replaceFirst
            "    synthetic: false\n"
            ("    synthetic: false\n"
             + (injected |> List.map (sprintf "%s\n") |> String.concat ""))
        |> TestSupport.writeRelative root evidencePath

        let report = TestSupport.runEvidence root workId title
        let evidence = TestSupport.readRelative root evidencePath

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.DoesNotContain(report.Diagnostics, fun d -> d.Id = "evidence.missingDeferralRationale")
        // Minimal quoting (FS.GG.SDD#260): safe plain scalars render bare and round-trip.
        Assert.Contains("    rationale: no GPU on the CI runner", evidence)
        Assert.Contains("    owner: platform", evidence)
        Assert.Contains("    scope: the headless render check", evidence)
        Assert.Contains("    laterLifecycleVisibility: verify", evidence)

    [<Fact>]
    let ``evidence deferral diagnostic names the top-level shape when the four fields are nested`` () =
        // FS-GG/FS.GG.SDD#574 (RM4 retrospective). The four required fields belong FLAT on the
        // obligation, peers of `result`/`synthetic`. Authored under a reasonable-guess `deferral:`
        // key they are nested, the codec drops what it cannot contain, the top-level scalars read
        // `None`, and the gate blocks — correctly, but the old message named only the fields
        // ("missing rationale, owner, scope, …"), which is unactionable when they are present-but-
        // nested. This is the issue's falsifiable check: nest the four fields, run evidence, and the
        // diagnostic must name that they belong TOP-LEVEL, not nested.
        let root = initializedAnalyzedProject ()

        let nested =
            [ "    deferral:"
              "      rationale: no GPU on the CI runner"
              "      owner: platform"
              "      scope: the headless render check"
              "      laterLifecycleVisibility: verify" ]

        TestSupport.runEvidence root workId title |> ignore

        TestSupport.readRelative root evidencePath
        |> replaceFirst "    kind: verification\n" "    kind: deferral\n"
        |> replaceFirst "    result: pass\n" "    result: deferred\n"
        |> replaceFirst
            "    synthetic: false\n"
            ("    synthetic: false\n"
             + (nested |> List.map (sprintf "%s\n") |> String.concat ""))
        |> TestSupport.writeRelative root evidencePath

        let report = TestSupport.runEvidence root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let diagnostic =
            report.Diagnostics
            |> List.find (fun d -> d.Id = "evidence.missingDeferralRationale")

        // The shape, not just the field names — the actionable fact the author was missing.
        Assert.Contains("top-level", diagnostic.Message)
        Assert.Contains("top-level", diagnostic.Correction)
        Assert.Contains("nested", diagnostic.Correction)

    [<Fact>]
    let ``evidence normalizes an authored bare-null declaration to the slim shape, then settles`` () =
        // FR-005 + FR-007 + research.md R5: an evidence.yml written by an older CLI (explicit
        // `null` lines) parses, is rewritten once in the slim form, and is byte-stable thereafter.
        let root = initializedAnalyzedProject ()

        let injected =
            [ "    syntheticDisclosure: null"
              "    rationale: null"
              "    owner: null"
              "    scope: null"
              "    laterLifecycleVisibility: null" ]

        let report, normalized = authorAfterSynthetic root "synthetic: false" injected

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        for key in slimmedOptionalKeys do
            Assert.DoesNotContain(key, normalized)

        TestSupport.runEvidence root workId title |> ignore
        Assert.Equal(normalized, TestSupport.readRelative root evidencePath)

    // Feature 077 / US2: `evidence --from-tests <path>` pre-maps each newly scaffolded obligation
    // to a verification-kind source pointing at the proving test file. Additive and inert when
    // absent; the path is a declared pointer (existence/freshness is a verify-stage concern).
    let private scaffoldFreshWith root fromTests =
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))

        { TestSupport.evidenceRequest root workId title with
            FromTests = fromTests }
        |> TestSupport.runRequest
        |> ignore

        let evidence = TestSupport.readRelative root evidencePath

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> artifact
        | Error diagnostics -> failwith $"Scaffolded evidence artifact did not parse: {diagnostics}."

    [<Fact>]
    let ``evidence --from-tests seeds a verification source on every scaffolded obligation`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshWith root (Some "tests/FS.GG.SDD.Foo.Tests")

        Assert.NotEmpty(artifact.Evidence)

        for declaration in artifact.Evidence do
            Assert.Contains(
                declaration.SourceRefs,
                fun source -> source.Kind = "verification" && source.Path = Some "tests/FS.GG.SDD.Foo.Tests"
            )

    [<Fact>]
    let ``evidence without --from-tests seeds no source (inert)`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshWith root None

        for declaration in artifact.Evidence do
            Assert.Empty(declaration.SourceRefs)

    [<Fact>]
    let ``evidence --from-tests with a blank value is treated as absent`` () =
        // A blank value carries no proving path; it is inert rather than seeding an empty source.
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshWith root (Some "   ")

        for declaration in artifact.Evidence do
            Assert.Empty(declaration.SourceRefs)

    [<Fact; Trait("tier", "slow")>]
    let ``evidence CLI --from-tests threads the path onto scaffolded sources`` () =
        // Real CLI parse + handler: `evidence --from-tests <path>` reaches the scaffolded evidence.
        let root = initializedAnalyzedProject ()
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))

        let result = runEvidenceCli root [ "--from-tests"; "tests/FS.GG.SDD.Foo.Tests" ]

        Assert.Equal(0, result.ExitCode)
        Assert.Contains("tests/FS.GG.SDD.Foo.Tests", TestSupport.readRelative root evidencePath)

    // FS.GG.SDD#182. `renderEvidenceSourceSnapshot`'s absent-optional branches are unreachable
    // from the command path — the sole caller overwrites `SourceSnapshots` with freshly computed
    // snapshots whose Digest/SchemaVersion are always `Some`. They become live the moment any
    // path re-renders a *parsed* artifact's snapshots (e.g. a no-clobber merge preserving an
    // author's block). These tests pin the convention against that day, directly on the renderer.

    let private snapshotOf digest schemaVersion : EvidenceSourceSnapshot =
        { Label = "tasks"
          Path = $"work/{workId}/tasks.yml"
          Digest = digest
          SchemaVersion = schemaVersion
          SourceLocation = None }

    [<Fact>]
    let ``renderEvidenceSourceSnapshot omits an absent digest and schemaVersion`` () =
        // Absence is absence: never `digest: ` (a trailing-whitespace line, violating the FR-004
        // invariant above) and never a fabricated `schemaVersion: 1` for a source that declared none.
        let rendered = HandlersEvidence.renderEvidenceSourceSnapshot (snapshotOf None None)

        Assert.Equal($"  - label: tasks\n    path: work/{workId}/tasks.yml", rendered)
        Assert.DoesNotContain("digest", rendered)
        Assert.DoesNotContain("schemaVersion", rendered)

        let offenders =
            rendered.Split('\n')
            |> Array.filter (fun line -> line = "" || line.TrimEnd() <> line)

        Assert.Empty(offenders)

    [<Fact>]
    let ``renderEvidenceSourceSnapshot omits each absent optional independently`` () =
        let digestOnly =
            HandlersEvidence.renderEvidenceSourceSnapshot (snapshotOf (Some "0123456789abcdef") None)

        Assert.Contains("\n    digest: 0123456789abcdef", digestOnly)
        Assert.DoesNotContain("schemaVersion", digestOnly)

        let schemaOnly =
            HandlersEvidence.renderEvidenceSourceSnapshot (snapshotOf None (Some 1))

        Assert.DoesNotContain("digest", schemaOnly)
        Assert.Contains("\n    schemaVersion: 1", schemaOnly)

    [<Fact>]
    let ``renderEvidenceSourceSnapshot emits both optionals when present (live path bytes unchanged)`` () =
        // The reachable path always supplies both, so this pins that #182 changed no emitted byte.
        let rendered =
            HandlersEvidence.renderEvidenceSourceSnapshot (snapshotOf (Some "0123456789abcdef") (Some 1))

        Assert.Equal(
            $"  - label: tasks\n    path: work/{workId}/tasks.yml\n    digest: 0123456789abcdef\n    schemaVersion: 1",
            rendered
        )

    [<Fact>]
    let ``an absent snapshot digest survives render then parse as None`` () =
        // The structural guarantee the two halves of #182 buy together: a snapshot with no digest
        // renders to a document that parses back to `Digest = None` — not `Some ""`, which
        // `evidenceSourceSnapshotStale` would compare against the real digest as a mismatch,
        // producing a permanent, unfixable `evidence.staleEvidenceSource` on every run.
        let root = initializedAnalyzedProject ()
        TestSupport.runEvidence root workId title |> ignore
        let original = TestSupport.readRelative root evidencePath

        // Splice a digest-less snapshot block into the real, freshly generated document.
        let header = "sourceSnapshots:\n"
        let headerAt = original.IndexOf(header)
        let blockEnd = original.IndexOf("\nevidence:")
        Assert.True(headerAt >= 0, "fixture: no sourceSnapshots block in the generated evidence.yml")
        let blockStart = headerAt + header.Length
        Assert.True(blockEnd > blockStart, "fixture: sourceSnapshots block is not followed by evidence:")

        let strippedBlock =
            HandlersEvidence.renderEvidenceSourceSnapshot (snapshotOf None None)

        let spliced =
            original.Substring(0, blockStart) + strippedBlock + original.Substring(blockEnd)

        match parseEvidenceArtifact { Path = evidencePath; Text = spliced } with
        | Error diagnostics -> failwith $"Digest-less evidence artifact did not parse: {diagnostics}."
        | Ok artifact ->
            let snapshot = Assert.Single(artifact.SourceSnapshots)
            Assert.Equal(None, snapshot.Digest)
            Assert.Equal(None, snapshot.SchemaVersion)

    // --- Issue #225 finding 4 (spec 096 AC-005): duplicate obligation ids merge like `TD-` ---
    //
    // `evidenceObligations` is a `List.collect` over tasks that emits one obligation per
    // (task, requiredEvidence) pair. Two tasks sharing `requiredEvidence: [EV001]` used to yield two
    // obligations with the same `ObligationId`, which scaffold a duplicate `EV001` declaration and a
    // duplicate `ED-EV001` disposition row, and left AC-005's union-lineage fold unreachable. The fix
    // groups by the obligation id and unions the lineage — the shape `verifyTestDispositionViews`
    // already reaches with its `groupBy`, so `TD-`/`ED-` merge identically.
    let private twoTaskSharedEvidenceFacts () =
        let text =
            """schemaVersion: 1
tasks:
  - id: T001
    title: First task
    status: pending
    owner: codex
    requirements: [FR-001]
    decisions: [DEC-001]
    sourceIds: [AC-001]
    dependencies: []
    requiredSkills: [fs-gg-sdd-project]
    requiredEvidence: [EV001]
  - id: T002
    title: Second task
    status: pending
    owner: codex
    requirements: [FR-002]
    decisions: [DEC-002]
    sourceIds: [AC-002]
    dependencies: [T001]
    requiredSkills: [fs-gg-sdd-project]
    requiredEvidence: [EV001]
"""

        match
            Task.parseTaskFacts
                { Path = $"work/{workId}/tasks.yml"
                  Text = text }
        with
        | Ok facts -> facts
        | Error diagnostics -> failwith $"Two-task fixture did not parse: {diagnostics}."

    [<Fact>]
    let ``evidenceObligations merges an obligation shared by two tasks into one unioned obligation`` () =
        let facts = twoTaskSharedEvidenceFacts ()
        let obligations = HandlersEvidence.evidenceObligations facts

        let shared =
            obligations |> List.filter (fun obligation -> obligation.ObligationId = "EV001")

        // One obligation, not one-per-task: no duplicate `EV001` reaches scaffolding or dispositions.
        Assert.Equal(1, List.length shared)
        let obligation = List.head shared

        Assert.Equal<string list>([ "T001"; "T002" ], obligation.LinkedTaskIds |> List.map _.Value |> List.sort)

        Assert.Equal<string list>(
            [ "FR-001"; "FR-002" ],
            obligation.LinkedRequirementIds |> List.map _.Value |> List.sort
        )

        Assert.Equal<string list>([ "DEC-001"; "DEC-002" ], obligation.LinkedDecisionIds |> List.sort)
        Assert.Equal<string list>([ "AC-001"; "AC-002" ], obligation.LinkedSourceIds |> List.sort)

        // The two per-task obligations collapsed to exactly one row — no duplicate reaches the
        // scaffold or the disposition views. (Single-task pass-through is guarded by every existing
        // single-obligation fixture's golden, which the `List.distinct` union leaves byte-identical.)
        Assert.Equal(1, List.length obligations)

    // --- Issue #230: `ED-` matches an `EV###` obligation by obligation id only, mirroring `TD-` ---
    //
    // `evidenceDispositions` used to carry a third match clause `verifyTestDispositionViews` lacks: a
    // declaration referencing one of the obligation's `LinkedTaskIds` matched even without naming the
    // obligation. #225 unioned `LinkedTaskIds` across the tasks sharing an obligation, widening that
    // clause to span *all* of a shared obligation's tasks — so a declaration referencing only T001
    // silently satisfied the merged T001+T002 obligation and hid T002's uncovered gap (verify passed).
    // #230 drops the clause for `EV###` obligations: they now match by obligation id only, like `TD-`.
    //
    // These are the two-task, task-ref-only fixtures the issue asks for, with the literal assertion.
    // Only *hand-authored* declarations are affected — a scaffolded one always carries `obligationRefs`,
    // so the positive control below (same declaration, obligation id added back) still resolves. The
    // `task.{id}.completion` carve-out (which cannot be named by any declaration) is pinned separately
    // by ``evidenceDispositions still lets a task-ref declaration satisfy a completion obligation``.
    let private evidenceArtifactWith declaration =
        let text =
            $"""schemaVersion: 1
evidence:
{declaration}"""

        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> artifact
        | Error diagnostics -> failwith $"Task-ref-only evidence fixture did not parse: {diagnostics}."

    [<Fact>]
    let ``evidenceDispositions does not let a task-ref-only declaration satisfy a shared obligation`` () =
        let obligations =
            HandlersEvidence.evidenceObligations (twoTaskSharedEvidenceFacts ())

        // Declares evidence *about task T001* — no `id: EV001`, no `obligationRefs: [EV001]`. Pre-#230
        // the widened `TaskRefs` clause pulled it into EV001 (LinkedTaskIds = [T001; T002]) and reported
        // `supported`, silently covering the uncovered T002. It must now leave EV001 unmatched.
        let artifact =
            evidenceArtifactWith
                """  - id: EV999
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: []
    result: pass
    synthetic: false"""

        let ev001 =
            HandlersEvidence.evidenceDispositions obligations (fun _ -> true) artifact
            |> List.find (fun disposition -> disposition.ObligationId = "EV001")

        Assert.Equal("missing", ev001.State)
        Assert.Empty(ev001.EvidenceIds)
        Assert.Contains("evidence.missingRequiredEvidence", ev001.DiagnosticIds)

    [<Fact>]
    let ``evidenceDispositions supports a shared obligation once the declaration names the obligation`` () =
        let obligations =
            HandlersEvidence.evidenceObligations (twoTaskSharedEvidenceFacts ())

        // The positive control: the identical declaration that *names* EV001 (as every scaffolded
        // declaration does) resolves the one merged obligation for both T001 and T002 — satisfied once,
        // mirroring `TD-`. This is the id-first path #230 keeps.
        let artifact =
            evidenceArtifactWith
                """  - id: EV999
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: []
    obligationRefs: [EV001]
    result: pass
    synthetic: false"""

        let ev001 =
            HandlersEvidence.evidenceDispositions obligations (fun _ -> true) artifact
            |> List.find (fun disposition -> disposition.ObligationId = "EV001")

        Assert.Equal("supported", ev001.State)
        Assert.Equal<string list>([ "EV999" ], ev001.EvidenceIds)
        Assert.Equal<string list>([ "T001"; "T002" ], ev001.TaskIds |> List.sort)

    // A `task.{id}.completion` obligation is minted for a Done task with no `requiredEvidence`
    // (`evidenceObligations`, `List.isEmpty task.RequiredEvidence && Done`). It is the one obligation
    // kind `TD-` lacks and that no declaration can name: never scaffolded (the `StartsWith("EV")`
    // filter skips it), its id is not a valid evidence `id` (`^EV\d{3,}$`), and naming it in
    // `obligationRefs` trips `evidence.unknownReference`. Its only satisfaction route is a task
    // reference — so #230 keeps the `TaskRefs` clause scoped to completion obligations. Regression
    // guard: without the carve-out this obligation would be permanently `missing` with no clean fix.
    let private doneTaskNoEvidenceFacts () =
        let text =
            """schemaVersion: 1
tasks:
  - id: T050
    title: Done task without declared evidence
    status: done
    owner: codex
    requirements: [FR-001]
    decisions: []
    sourceIds: [AC-001]
    dependencies: []
    requiredSkills: [fs-gg-sdd-project]
    requiredEvidence: []
"""

        match
            Task.parseTaskFacts
                { Path = $"work/{workId}/tasks.yml"
                  Text = text }
        with
        | Ok facts -> facts
        | Error diagnostics -> failwith $"Done-task fixture did not parse: {diagnostics}."

    [<Fact>]
    let ``evidenceDispositions still lets a task-ref declaration satisfy a completion obligation`` () =
        let obligations = HandlersEvidence.evidenceObligations (doneTaskNoEvidenceFacts ())

        // The obligation exists, is completion-kind, and is single-task (its id embeds the task id).
        let completionId = "task.T050.completion"
        Assert.Contains(obligations, fun obligation -> obligation.ObligationId = completionId)

        // The only declaration shape that can satisfy it: references the task, names no obligation.
        let artifact =
            evidenceArtifactWith
                """  - id: EV010
    kind: verification
    subject:
      type: task
      id: T050
    taskRefs: [T050]
    requirementRefs: []
    result: pass
    synthetic: false"""

        let completion =
            HandlersEvidence.evidenceDispositions obligations (fun _ -> true) artifact
            |> List.find (fun disposition -> disposition.ObligationId = completionId)

        Assert.Equal("supported", completion.State)
        Assert.Equal<string list>([ "EV010" ], completion.EvidenceIds)
        Assert.Equal<string list>([ "T050" ], completion.TaskIds)

    // ---- #306: the visual-inspection obligation gate --------------------------------------------

    /// A visual-surface workspace, scaffolded through `evidence`. Unlike `initializeAnalyzedProject`
    /// this cannot reuse the fixed five-entry evidence ladder: the declaration derives a sixth task
    /// (`Inspect a rendered frame`), so the scaffold must come from the obligations themselves.
    let private visualSurfaceScaffoldedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.declareVisualSurface root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root workId title with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root workId title |> ignore
        TestSupport.runPlan root workId title |> ignore
        TestSupport.authorPlanProse root workId // #351: the scaffold's plan prose now blocks analyze
        TestSupport.runTasks root workId title |> ignore
        TestSupport.runAnalyze root workId title |> ignore
        TestSupport.runEvidence root workId title |> ignore
        root

    /// The obligation minted for the `visual-inspection`-tagged task, read off the generated graph
    /// rather than hardcoded, so a change to the derivation order cannot silently pass these tests.
    let private visualObligationId root =
        let tasks = TestSupport.readRelative root $"work/{workId}/tasks.yml"
        let marker = "title: Inspect a rendered frame"
        let block = tasks.Substring(tasks.IndexOf(marker))
        let evidenceLine = block.Substring(block.IndexOf("requiredEvidence: ["))
        evidenceLine.Substring("requiredEvidence: [".Length).Split(']').[0].Trim()

    /// Replace the visual obligation's scaffolded declaration with `body`, keeping every other
    /// obligation's declaration exactly as scaffolded.
    let private declareVisualEvidence root (body: string) =
        let obligationId = visualObligationId root
        let evidence = TestSupport.readRelative root evidencePath
        let start = evidence.IndexOf($"  - id: {obligationId}\n")
        let after = evidence.IndexOf("\nlifecycleNotes:", start)
        let replaced = evidence.Substring(0, start) + body + evidence.Substring(after)
        TestSupport.writeRelative root evidencePath replaced
        obligationId

    let private visualDeclaration obligationId taskId fields =
        $"  - id: {obligationId}\n    kind: verification\n    subject:\n      type: task\n      id: {taskId}\n    taskRefs: [{taskId}]\n    obligationRefs: [{obligationId}]\n{fields}"

    /// FS.GG.SDD#349. Actually produce the frame the declaration cites.
    ///
    /// Before #349 these fixtures cited `evidence/frame-ceiling-bounce.png` and never wrote it — the
    /// file appears nowhere in the repository — so the two "accepts a rendered artifact" tests were
    /// green while proving the exact opposite of their names. That committed pair *was* the field
    /// report's probe, already merged. The rendered frame now exists, and the gate is what makes the
    /// difference between the two states observable.
    let private renderFrame root (relativePath: string) =
        TestSupport.writeRelative root relativePath "not-a-real-png-but-a-real-file"

    /// FR-004 / SC-003: a non-synthetic `pass` that names no rendered artifact is exactly the shape
    /// of Breakout1's green suite over an invisible ball. It blocks.
    [<Fact>]
    let ``evidence blocks a visual-inspection pass that names no rendered artifact`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: []\n    sourceRefs: []\n    result: pass\n    synthetic: false\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        Assert.Contains(
            report.Diagnostics,
            fun diagnostic -> diagnostic.Id = "evidence.missingVisualInspectionArtifact"
        )

    /// FR-004: naming the rendered frame discharges it — and, since #349, only when the frame is
    /// actually there.
    [<Fact>]
    let ``evidence accepts a visual-inspection pass that names a rendered artifact`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root
        renderFrame root "evidence/frame-ceiling-bounce.png"

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: [evidence/frame-ceiling-bounce.png]\n    sourceRefs: []\n    result: pass\n    synthetic: false\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        Assert.DoesNotContain(
            report.Diagnostics,
            fun diagnostic -> diagnostic.Id = "evidence.missingVisualInspectionArtifact"
        )

    /// FR-004: a `sourceRefs[]` path is a rendered artifact too — the gate reads either bucket.
    /// #349: and both buckets are existence-checked, so this bucket is not an evasion route.
    [<Fact>]
    let ``evidence accepts a visual-inspection pass whose rendered artifact is a sourceRef`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root
        renderFrame root "evidence/frame-ceiling-bounce.png"

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: []\n    sourceRefs:\n      - kind: verification\n        path: evidence/frame-ceiling-bounce.png\n        result: pass\n    result: pass\n    synthetic: false\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

    // ---------------------------------------------------------------------------------------------
    // FS.GG.SDD#349 — a cited artifact must exist. The failure leg is asserted on the diagnostic id,
    // not on a bare exit code (FR-009): per epic #266's own open note, a fix whose failure leg is
    // untested is how this class of defect survives.
    // ---------------------------------------------------------------------------------------------

    /// FR-001 / SC-001 / SC-004: the field report's probe. A real, non-synthetic pass citing a file
    /// that is not on disk is refused, and the diagnostic names the path.
    [<Fact>]
    let ``evidence blocks a pass whose cited artifacts path does not exist`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root
        // Deliberately do NOT render the frame.

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: [evidence/frame-that-was-never-rendered.png]\n    sourceRefs: []\n    result: pass\n    synthetic: false\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let diagnostic =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "evidence.artifactNotFound")

        // The path is the actionable fact — it is what the author has to go and produce.
        Assert.Contains("evidence/frame-that-was-never-rendered.png", diagnostic.RelatedIds)

    /// FR-002 / SC-004: the evasion route. `namesRenderedArtifact` discharges an obligation from
    /// EITHER bucket, so a check that only reads `artifacts:` leaves the identical hole one field to
    /// the left. Writing the phantom path into `sourceRefs` must be refused identically.
    [<Fact>]
    let ``evidence blocks a pass whose cited sourceRefs path does not exist`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: []\n    sourceRefs:\n      - kind: verification\n        path: evidence/frame-that-was-never-rendered.png\n        result: pass\n    result: pass\n    synthetic: false\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.artifactNotFound")

    /// FR-002: a `uri` is not a local file. It is never probed, and it never blocks — otherwise the
    /// gate would refuse every declaration pointing at a CI run or a dashboard.
    [<Fact>]
    let ``evidence does not probe a sourceRefs uri`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: []\n    sourceRefs:\n      - kind: verification\n        uri: https://ci.example/run/1\n        result: pass\n    result: pass\n    synthetic: false\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.artifactNotFound")

    /// FR-001: the gate validates `merged` (on-disk ⊕ `InputText`), so the probe must see BOTH.
    ///
    /// Probing only the on-disk artifact left an input-supplied declaration unprobed — and an
    /// unprobed path is treated as present — so the gate failed OPEN on exactly the authoring route
    /// it polices. Caught in review of this feature; this is its regression leg. A gate against
    /// fail-open must not itself fail open.
    /// Mirrors `undisclosedSyntheticInput` above — the established shape for an InputText-supplied
    /// declaration that merges cleanly. It *adds* a declaration rather than overwriting an authored
    /// one, which the merge policy would refuse first as `evidence.unsafeUpdate`.
    let phantomArtifactInput =
        """schemaVersion: 1
workId: 011-evidence-command
stage: evidence
status: evidenceReady
evidence:
  - id: EV999
    kind: verification
    subject:
      type: task
      id: T001
    taskRefs: [T001]
    requirementRefs: []
    obligationRefs: [EV001]
    artifacts: [tests/PhantomSuppliedViaInput.fs]
    sourceRefs: []
    result: pass
    synthetic: false
"""

    [<Fact>]
    let ``evidence blocks a phantom cited path supplied through InputText, not just on disk`` () =
        let root = initializedAnalyzedProject ()

        let report =
            TestSupport.runRequest
                { TestSupport.evidenceRequest root workId title with
                    InputText = Some phantomArtifactInput }

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.artifactNotFound")

    /// FR-006 / US3: a deferral may legitimately cite an artifact that does not exist yet — that is
    /// what deferring *means*. Blocking it would teach authors to stop deferring, which is the
    /// failure mode #266 exists to cure, not to cause. Only `pass` ∧ ¬`synthetic` is held to the rule.
    [<Fact>]
    let ``evidence does not block a deferral citing an artifact that does not exist yet`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: [evidence/frame-not-yet-rendered.png]\n    sourceRefs: []\n    result: deferred\n    synthetic: false\n    rationale: the renderer is not wired up yet\n    owner: finch\n    scope: this work item\n    laterLifecycleVisibility: verify\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.artifactNotFound")

    /// FR-006: a disclosed synthetic pass does not satisfy, so it is not held to the existence rule
    /// either — it is already honest about being synthetic, and the cascade records it `synthetic`.
    [<Fact>]
    let ``evidence does not block a disclosed synthetic pass citing a missing artifact`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root

        declareVisualEvidence
            root
            (visualDeclaration
                obligationId
                "T006"
                "    artifacts: [evidence/frame-never-rendered.png]\n    sourceRefs: []\n    result: pass\n    synthetic: true\n    syntheticDisclosure:\n      reason: no renderer in this test workspace\n      realPath: a real frame rendered by the app\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.artifactNotFound")

    /// FR-005 / SC-002: a disclosed synthetic pass is honest, so it does not BLOCK — but it never
    /// satisfies. The gate must not reclassify it `invalid`; the existing cascade records it
    /// `synthetic`, which is unsatisfying by the satisfaction rule this feature inherits.
    [<Fact>]
    let ``evidence records a synthetic visual-inspection pass as unsatisfying, not invalid`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root

        declareVisualEvidence
            root
            ($"  - id: {obligationId}\n    kind: synthetic\n    subject:\n      type: task\n      id: T006\n    taskRefs: [T006]\n    obligationRefs: [{obligationId}]\n    artifacts: []\n    sourceRefs: []\n    result: pass\n    synthetic: true\n    syntheticDisclosure:\n      standsInFor: a real rendered frame\n      reason: the renderer is not wired yet\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        Assert.DoesNotContain(
            report.Diagnostics,
            fun diagnostic -> diagnostic.Id = "evidence.missingVisualInspectionArtifact"
        )

        match report.Evidence with
        | Some summary -> Assert.Equal(1, summary.SyntheticCount)
        | None -> failwith "Expected an evidence summary."

    /// FR-006: a deferral is a first-class disposition. Its `result` is `deferred`, never `pass`, so
    /// the artifact gate must not fire on it — only the existing four-field deferral gate applies.
    [<Fact>]
    let ``evidence lets a visual-inspection obligation be deferred without a rendered artifact`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root

        declareVisualEvidence
            root
            ($"  - id: {obligationId}\n    kind: deferral\n    subject:\n      type: task\n      id: T006\n    taskRefs: [T006]\n    obligationRefs: [{obligationId}]\n    artifacts: []\n    sourceRefs: []\n    result: deferred\n    synthetic: false\n    rationale: the renderer lands in the next cut\n    owner: platform\n    scope: the ceiling-bounce frame inspection\n    laterLifecycleVisibility: re-open when the renderer ships\n")
        |> ignore

        let report = TestSupport.runEvidence root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        Assert.DoesNotContain(
            report.Diagnostics,
            fun diagnostic -> diagnostic.Id = "evidence.missingVisualInspectionArtifact"
        )

    /// FR-004, defence in depth: `--from-tests` pre-maps each newly scaffolded obligation to a
    /// proving TEST path. `namesRenderedArtifact` cannot tell a test file from a rendered frame, so
    /// seeding one onto the visual-inspection obligation would pre-satisfy the artifact gate with the
    /// wrong kind of proof the instant the author flipped `result: pass` — the exact bypass this
    /// obligation exists to prevent. The visual obligation is left unseeded; every other one is not.
    [<Fact>]
    let ``evidence --from-tests does not seed a test path onto the visual-inspection obligation`` () =
        let root = visualSurfaceScaffoldedProject ()
        let obligationId = visualObligationId root
        System.IO.File.Delete(System.IO.Path.Combine(root, "work", workId, "evidence.yml"))

        { TestSupport.evidenceRequest root workId title with
            FromTests = Some "tests/Product.Tests/PhysicsTests.fs" }
        |> TestSupport.runRequest
        |> ignore

        let artifact =
            match
                parseEvidenceArtifact
                    { Path = evidencePath
                      Text = TestSupport.readRelative root evidencePath }
            with
            | Ok artifact -> artifact
            | Error diagnostics -> failwith $"Scaffolded evidence artifact did not parse: {diagnostics}."

        let visual =
            artifact.Evidence
            |> List.find (fun declaration -> declaration.Id.Value = obligationId)

        Assert.Empty(visual.SourceRefs)
        Assert.False(namesRenderedArtifact visual)

        // Every other obligation still gets its proving-test pointer.
        for declaration in artifact.Evidence |> List.filter (fun d -> d.Id.Value <> obligationId) do
            Assert.Contains(
                declaration.SourceRefs,
                fun source -> source.Path = Some "tests/Product.Tests/PhysicsTests.fs"
            )

    // ---------------------------------------------------------------------------------------------
    // FS.GG.SDD#355 — the canonical *passing* fixture must demonstrate a SATISFIED obligation, not a
    // merely declared one. Before #349/#355 it shipped `result: pass` with no `artifacts:` key at
    // all: the shape every smoke test taught was a bare self-attestation.
    // ---------------------------------------------------------------------------------------------

    /// The fixture cites an artifact for every obligation — it is no longer a bare `result: pass`.
    [<Fact>]
    let ``the canonical passing evidence fixture cites an artifact for every obligation`` () =
        let text = TestSupport.passingTaskEvidence

        for taskId in TestShared.EvidenceLadder.taskIds TestSupport.ladderTaskCount do
            Assert.Contains(TestShared.EvidenceLadder.artifactPath taskId, text)

    /// ...and every cited artifact is actually written into the workspace, so the citation resolves.
    /// This is the pairing #355 is about: a declaration and a file, together or not at all.
    [<Fact>]
    let ``the canonical passing evidence fixture materializes every artifact it cites`` () =
        let root = initializedAnalyzedProject ()

        TestSupport.writePassingTaskEvidenceFor root workId

        for path in TestShared.EvidenceLadder.artifactPaths TestSupport.ladderTaskCount do
            Assert.True(TestSupport.existsRelative root path, $"fixture cites {path} but never wrote it")

    /// The end-to-end proof, and the reason #355 was filed: drive the canonical fixture through the
    /// live gate. It must pass *honestly* — no `evidence.artifactNotFound` — which is only true
    /// because the artifacts are real. Before this, the same fixture passed while citing nothing.
    [<Fact>]
    let ``the canonical passing evidence fixture clears the artifact-existence gate`` () =
        let root = initializedAnalyzedProject ()

        TestSupport.writePassingTaskEvidenceFor root workId

        let report = TestSupport.runEvidence root workId title

        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "evidence.artifactNotFound")
        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

    // Roadmap §3 Low (Maintainability): `mergeEvidenceArtifacts` seeds a fresh `evidence.yml` on the
    // (None, None) path. It formerly `failwithf`'d if the plan-validated work id was somehow rejected
    // when re-derived — an opaque `StackOverflow`-class process abort. It now returns `None` plus a
    // typed `toolDefect` diagnostic, so a broken internal invariant degrades to a legible exit-2
    // report rather than a crash. `workIdDiagnostics` (Foundation) makes the guard unreachable in
    // production, so these unit tests bypass planning and call the internal helper directly.
    let private evidenceObligation id : Evidence.EvidenceObligation =
        { ObligationId = id
          Kind = "test"
          SourceArtifactPath = $"work/{workId}/tasks.yml"
          SourceId = None
          LinkedTaskIds = []
          LinkedRequirementIds = []
          LinkedDecisionIds = []
          LinkedSourceIds = []
          ExpectedEvidenceKinds = []
          RequiredEvidenceKinds = []
          RequiredSkillOrCapabilityTags = []
          Blocking = true
          Correction = "" }

    [<Fact>]
    let ``mergeEvidenceArtifacts seeds a fresh evidence skeleton for a valid work id`` () =
        let merged, diagnostics =
            HandlersEvidence.mergeEvidenceArtifacts workId None None None [ evidenceObligation "EV-001" ]

        Assert.Empty(diagnostics)

        match merged with
        | Some artifact ->
            Assert.Equal(workId, artifact.WorkId.Value)
            Assert.Equal(1, artifact.Evidence.Length)
        | None -> Assert.Fail("expected a seeded evidence artifact for a valid work id")

    [<Fact>]
    let ``mergeEvidenceArtifacts reports a toolDefect instead of crashing on a rejected work id`` () =
        // The malformed id (spaces) is rejected by `createWorkId`, driving the unreachable defect arm.
        let merged, diagnostics =
            HandlersEvidence.mergeEvidenceArtifacts "not a valid work id" None None None [ evidenceObligation "EV-001" ]

        Assert.True(merged.IsNone)
        Assert.Equal(1, List.length diagnostics)

        let diagnostic = List.head diagnostics
        Assert.Equal("toolDefect", diagnostic.Id)
        Assert.True(diagnostic.IsToolDefect)

    // ---- WI-4 (ADR-0048): the per-classified-FR gameplay obligation ------------------------------
    //
    // A classified `{gameplay}` FR mints an obligation carrying the gameplay-test tag and
    // `RequiredEvidenceKinds = [verification]`. It is discharged only by a real (non-synthetic) test
    // KIND; a synthetic pass or a non-test-kind pass leaves it unmet, and the count Governance binds
    // reflects that. An accepted deferral is a first-class outcome, as for visualSurface.

    let private gameplayObligation id : Evidence.EvidenceObligation =
        { evidenceObligation id with
            RequiredEvidenceKinds = Evidence.realTestEvidenceKinds
            RequiredSkillOrCapabilityTags = [ Evidence.gameplayTestCapability ] }

    let private gameplayDisposition (declaration: string) =
        let artifact = evidenceArtifactWith declaration

        HandlersEvidence.evidenceDispositions [ gameplayObligation "EV001" ] (fun _ -> true) artifact
        |> List.find (fun disposition -> disposition.ObligationId = "EV001")

    let private declarationOfKind (kind: string) (synthetic: bool) =
        $"""  - id: EV001
    kind: {kind}
    subject:
      type: task
      id: T001
    obligationRefs: [EV001]
    result: pass
    synthetic: {(if synthetic then "true" else "false")}"""

    [<Fact>]
    let ``a gameplay obligation is supported by a non-synthetic verification pass`` () =
        let disposition = gameplayDisposition (declarationOfKind "verification" false)
        Assert.Equal("supported", disposition.State)
        Assert.True(disposition.ClassifiedRequirement)
        Assert.Equal(0, HandlersEvidence.classifiedObligationsUnmetCount [ disposition ])

    [<Fact>]
    let ``a gameplay obligation is unmet by a non-test-kind pass`` () =
        // An `implementation` pass discharges any ordinary obligation, but not a gameplay one — only a
        // real test kind does. This is the injected `ED-` arm.
        let disposition = gameplayDisposition (declarationOfKind "implementation" false)
        Assert.Equal("invalid", disposition.State)
        Assert.Contains("evidence.classifiedRequirementTestObligationUnmet", disposition.DiagnosticIds)
        Assert.Equal(1, HandlersEvidence.classifiedObligationsUnmetCount [ disposition ])

    [<Fact>]
    let ``a gameplay obligation is unmet by a synthetic pass`` () =
        // Synthetic state can never discharge a gameplay obligation (the epic's core rule).
        let disposition = gameplayDisposition (declarationOfKind "verification" true)
        Assert.NotEqual("supported", disposition.State)
        Assert.Equal(1, HandlersEvidence.classifiedObligationsUnmetCount [ disposition ])

    [<Fact>]
    let ``classifiedObligationsUnmetCount counts the unmet classified obligation and not the supported one`` () =
        let supported = gameplayDisposition (declarationOfKind "verification" false)
        let unmet = gameplayDisposition (declarationOfKind "implementation" false)

        // An unclassified obligation left unmet must NOT contribute — the count is gameplay-specific.
        let unclassifiedUnmet =
            HandlersEvidence.evidenceDispositions
                [ evidenceObligation "EV001" ]
                (fun _ -> true)
                (evidenceArtifactWith (declarationOfKind "implementation" false))
            |> List.find (fun disposition -> disposition.ObligationId = "EV001")

        Assert.False(unclassifiedUnmet.ClassifiedRequirement)
        Assert.Equal(1, HandlersEvidence.classifiedObligationsUnmetCount [ supported; unmet; unclassifiedUnmet ])

    [<Fact>]
    let ``an accepted deferral leaves a gameplay obligation deferred, not counted`` () =
        // A deferral (with all four fields) is a first-class outcome the count does not touch, exactly
        // as for the visual-inspection obligation.
        let disposition =
            gameplayDisposition
                """  - id: EV001
    kind: deferral
    subject:
      type: task
      id: T001
    obligationRefs: [EV001]
    result: deferred
    rationale: covered by a follow-up work item
    owner: codex
    scope: this work item
    laterLifecycleVisibility: verify"""

        Assert.Equal("deferred", disposition.State)
        Assert.Equal(0, HandlersEvidence.classifiedObligationsUnmetCount [ disposition ])

    /// A gameplay task's obligation carries the real-test-kind restriction (the mint), even when a
    /// non-gameplay task shares the obligation id (Finding 2: the group-merge unions it).
    let private gameplayTaskFacts () =
        let text =
            """schemaVersion: 1
tasks:
  - id: T001
    title: Implement requirement FR-001
    status: pending
    owner: codex
    requirements: [FR-001]
    decisions: []
    sourceIds: [AC-001]
    dependencies: []
    requiredSkills: [fsharp]
    requiredEvidence: [EV001]
  - id: T002
    title: Cover gameplay requirement FR-001 with a non-synthetic test
    status: pending
    owner: codex
    requirements: [FR-001]
    decisions: []
    sourceIds: []
    dependencies: [T001]
    requiredSkills: [gameplay-test, automated-tests]
    requiredEvidence: [EV001]
"""

        match
            Task.parseTaskFacts
                { Path = $"work/{workId}/tasks.yml"
                  Text = text }
        with
        | Ok facts -> facts
        | Error diagnostics -> failwith $"gameplay task facts did not parse: {diagnostics}"

    [<Fact>]
    let ``evidenceObligations mints the real-test-kind restriction for a shared gameplay obligation`` () =
        let obligation =
            HandlersEvidence.evidenceObligations (gameplayTaskFacts ())
            |> List.find (fun obligation -> obligation.ObligationId = "EV001")

        // The gameplay task is listed SECOND, so head-winning would drop the restriction (Finding 2).
        Assert.Equal<string list>(Evidence.realTestEvidenceKinds, obligation.RequiredEvidenceKinds)
        Assert.True(Evidence.isGameplayTestTagged obligation.RequiredSkillOrCapabilityTags)

    [<Fact>]
    let ``verifyTestDispositionViews marks a gameplay obligation invalid for a non-test pass`` () =
        // Finding 1: the TD- cascade must mirror ED- — an implementation pass does not satisfy the
        // required gameplay test.
        let artifact =
            evidenceArtifactWith
                """  - id: EV001
    kind: implementation
    subject:
      type: task
      id: T001
    obligationRefs: [EV001]
    result: pass
    synthetic: false"""

        let view =
            HandlersVerify.verifyTestDispositionViews (gameplayTaskFacts ()) (fun _ -> true) false artifact
            |> List.find (fun view -> view.ObligationId = "EV001")

        Assert.Equal("invalid", view.State)
        Assert.Contains("evidence.classifiedRequirementTestObligationUnmet", view.DiagnosticIds)
