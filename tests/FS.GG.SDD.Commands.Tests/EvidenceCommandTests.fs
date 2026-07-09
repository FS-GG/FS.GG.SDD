namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
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

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = evidencePath && change.Kind = "authoredSource"
        )

        Assert.Contains(report.GeneratedViews, fun view -> view.Path = workModelPath)
        Assert.Equal(Some "evidence.next.verify", report.NextAction |> Option.map _.ActionId)

        match parseEvidenceArtifact { Path = evidencePath; Text = evidence } with
        | Ok artifact -> Assert.Equal("evidenceReady", artifact.Status)
        | Error diagnostics -> failwith $"Generated evidence artifact did not parse: {diagnostics}."

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
    let ``evidence plan-decision obligation preserves planDecisionRefs and recovers requirementRefs`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // T002 is the `Implement plan decision PD-001` task: its task.Requirements / task.Decisions
        // are empty, so before the fix both requirementRefs and planDecisionRefs scaffolded empty.
        let declaration = declarationForTask artifact "T002"

        Assert.Equal<string list>([ "PD-001" ], declaration.PlanDecisionRefs |> List.map _.Value)
        // The PD→FR linkage is recovered from the plan decision's own source lineage (FR-001).
        Assert.Equal<string list>([ "FR-001" ], declaration.RequirementRefs |> List.map _.Value)

    [<Fact>]
    let ``evidence requirement obligation still carries requirementRefs (no regression)`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // T001 is the `Implement requirement FR-001` task.
        let declaration = declarationForTask artifact "T001"

        Assert.Equal<string list>([ "FR-001" ], declaration.RequirementRefs |> List.map _.Value)
        Assert.Empty(declaration.PlanDecisionRefs)

    [<Fact>]
    let ``evidence obligation refs never misroute a plan decision into clarification refs`` () =
        let root = initializedAnalyzedProject ()
        let artifact = scaffoldFreshEvidence root

        // PD-001 is a plan decision, not a clarification decision (DEC-###) — it must land only in
        // planDecisionRefs, never clarificationDecisionRefs.
        let declaration = declarationForTask artifact "T002"

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
        // FR-003. Values are never omitted — only `None` is. Injected unquoted; the writer
        // re-renders quoted, which proves the round-trip actually ran.
        let root = initializedAnalyzedProject ()

        let injected =
            [ "    syntheticDisclosure:"
              "      standsInFor: a real headless render"
              "      reason: no GPU on the CI runner" ]

        let report, evidence = authorAfterSynthetic root "synthetic: true" injected

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains("    syntheticDisclosure:\n", evidence)
        Assert.Contains("      standsInFor: \"a real headless render\"", evidence)
        Assert.Contains("      reason: \"no GPU on the CI runner\"", evidence)

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
        Assert.Contains("    rationale: \"accepted deferral see DEC-004\"", evidence)
        Assert.Contains("    owner: \"platform\"", evidence)
        Assert.Contains("    scope: \"workspace\"", evidence)
        Assert.Contains("    laterLifecycleVisibility: \"verify\"", evidence)

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
        Assert.Contains("    owner: \"platform\"", evidence) // witness: the writer re-rendered
        Assert.Contains("    rationale: \"null\"", evidence)

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
        Assert.Contains("    rationale: \"no GPU on the CI runner\"", evidence)
        Assert.Contains("    owner: \"platform\"", evidence)
        Assert.Contains("    scope: \"the headless render check\"", evidence)
        Assert.Contains("    laterLifecycleVisibility: \"verify\"", evidence)

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

        match Task.parseTaskFacts { Path = $"work/{workId}/tasks.yml"; Text = text } with
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

        Assert.Equal<string list>(
            [ "T001"; "T002" ],
            obligation.LinkedTaskIds |> List.map _.Value |> List.sort
        )

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
