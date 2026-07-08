namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module ClarifyCommandTests =
    let workId = "006-clarify-command"
    let title = "Clarify Command"
    let specPath = $"work/{workId}/spec.md"
    let clarificationPath = $"work/{workId}/clarifications.md"
    let workModelPath = $"readiness/{workId}/work-model.json"

    let initializedSpecifiedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore

        let specifyRequest =
            { TestSupport.specifyRequest root workId title with
                InputText = Some TestSupport.specifyIntentWithAmbiguity }

        TestSupport.runRequest specifyRequest |> ignore
        root

    /// Feature 089: the shared `specifyIntentWithAmbiguity` declares exactly one ambiguity, which
    /// cannot exercise "some answered, some not" or per-ambiguity line retirement.
    let initializedSpecifiedProjectWithTwoAmbiguities () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore

        let intent =
            TestSupport.specifyIntentWithAmbiguity
            + "\nambiguity: how long should a clarification decision remain valid?"

        let specifyRequest =
            { TestSupport.specifyRequest root workId title with
                InputText = Some intent }

        TestSupport.runRequest specifyRequest |> ignore
        root

    let initializedSpecifiedProjectWithoutAmbiguity () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore
        root

    let runClarifyWith input root =
        let request =
            { TestSupport.clarifyRequest root workId title with
                InputText = input }

        TestSupport.runRequest request

    [<Fact>]
    let ``clarify creates authored clarification with real filesystem evidence`` () =
        let root = initializedSpecifiedProject ()

        let report = TestSupport.runClarify root workId title
        let clarification = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("stage: clarify", clarification)
        Assert.Contains("## Clarification Questions", clarification)
        Assert.Contains("CQ-001", clarification)
        Assert.Contains("DEC-001", clarification)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = clarificationPath && change.Operation = ArtifactOperation.Create
        )

        Assert.Contains(
            report.GeneratedViews,
            fun view -> view.Path = workModelPath && view.Currency = GeneratedViewCurrency.Missing
        )

        Assert.Equal(Some Checklist, report.NextAction |> Option.bind _.Command)

        Assert.Equal(
            Some "CQ-001",
            report.Clarification
            |> Option.bind (fun summary -> summary.QuestionIds |> List.tryHead)
        )

        Assert.Equal(
            Some "DEC-001",
            report.Clarification
            |> Option.bind (fun summary -> summary.DecisionIds |> List.tryHead)
        )

    [<Fact>]
    let ``clarify creation does not require Governance files`` () =
        let root = initializedSpecifiedProject ()

        let report = TestSupport.runClarify root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``clarify handles specified work item with no open ambiguity`` () =
        let root = initializedSpecifiedProjectWithoutAmbiguity ()

        let report = runClarifyWith None root
        let clarification = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("No clarification questions recorded.", clarification)
        Assert.Empty(report.Clarification.Value.QuestionIds)
        Assert.Empty(report.Clarification.Value.DecisionIds)
        Assert.Equal(Some Checklist, report.NextAction |> Option.bind _.Command)

    [<Fact>]
    let ``clarify rerun preserves authored content and stable decisions`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore

        let authored =
            TestSupport.readRelative root clarificationPath
            + "\nUser-authored clarification prose stays here.\n"

        TestSupport.writeRelative root clarificationPath authored

        let report = TestSupport.runClarify root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root clarificationPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = clarificationPath && change.Operation = ArtifactOperation.NoChange
        )

    [<Fact>]
    let ``clarify safely appends missing standard sections`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore

        let partial =
            (TestSupport.readRelative root clarificationPath)
                .Replace("## Accepted Deferrals\nNo accepted deferrals recorded.\n\n", "")

        TestSupport.writeRelative root clarificationPath partial

        let report = TestSupport.runClarify root workId title
        let after = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("## Accepted Deferrals", after)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = clarificationPath && change.Operation = ArtifactOperation.Update
        )

    [<Fact>]
    let ``clarify records accepted deferral as durable decision fact`` () =
        let root = initializedSpecifiedProject ()

        let report =
            runClarifyWith
                (Some "AMB-001 accepted deferral: Defer checklist output naming to the checklist feature.")
                root

        let clarification = TestSupport.readRelative root clarificationPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("## Accepted Deferrals", clarification)
        Assert.Contains("DEC-001", clarification)

        Assert.Equal(
            Some "DEC-001",
            report.Clarification
            |> Option.bind (fun summary -> summary.AcceptedDeferralIds |> List.tryHead)
        )

        Assert.Empty(report.Clarification.Value.DecisionIds)

    // Feature 089 (FR-006/FR-010, §WD5). A blocked clarify still BLOCKS — same outcome, same
    // diagnostic — but no longer leaves an empty work directory: it seeds the skeleton the operator
    // is meant to fill in. Before 089 this test asserted `Assert.False(exists …)`; the flip to
    // `Assert.True` IS the behavior change. What must not move is the outcome and the diagnostic.
    [<Fact>]
    let ``clarify missing answer blocks but seeds the skeleton`` () =
        let root = initializedSpecifiedProject ()

        let report = runClarifyWith None root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationAnswer")
        Assert.True(TestSupport.existsRelative root clarificationPath)

        // Exactly one changed artifact: the seed, and nothing else. The generated work model stays
        // gated behind the blocking check (the H-4 carve-out passes only the seed).
        Assert.Equal(1, report.ChangedArtifacts.Length)
        Assert.Equal(clarificationPath, report.ChangedArtifacts.Head.Path)
        Assert.False(TestSupport.existsRelative root workModelPath)

        let skeleton = TestSupport.readRelative root clarificationPath

        // Truthful: never `clarified`, never "nothing remains", while the command blocks (FR-007/008).
        Assert.Contains("status: needsAnswers", skeleton)
        Assert.DoesNotContain("status: clarified", skeleton)
        Assert.DoesNotContain("No blocking ambiguity remains.", skeleton)
        Assert.Contains("- CQ-001 [AMB:AMB-001]", skeleton)

    // Feature 089 (FR-009/FR-021, K5/K9/K10). The skeleton's remaining-ambiguity entries must parse
    // as BLOCKING. `parseRemainingAmbiguity` classifies by scanning the line's prose for
    // `accepted deferral`/`defer` and `non-blocking`, so an explanation that merely names those
    // resolutions as the operator's options parses as one of them, zeroes the blocking count, and
    // lets `checklist` pass with every ambiguity unanswered. Assert the consequence, not the wording.
    [<Fact>]
    let ``seeded skeleton entries really block the next stage`` () =
        let root = initializedSpecifiedProject ()

        runClarifyWith None root |> ignore

        let reread = runClarifyWith None root

        // The fixture declares one ambiguity (AMB-001); it must count as blocking.
        Assert.Equal(
            Some 1,
            reread.Clarification
            |> Option.map (fun summary -> summary.BlockingAmbiguityCount)
        )

        let checklist = TestSupport.runChecklist root workId title

        Assert.Equal(CommandOutcome.Blocked, checklist.Outcome)
        Assert.Contains(checklist.Diagnostics, fun diagnostic -> diagnostic.Id = "unresolvedBlockingAmbiguity")

    // Feature 089 (FR-013/FR-015). Re-running over the seed neither duplicates the derived questions
    // nor reads the skeleton's own questions as answers; the skeleton is byte-stable.
    [<Fact>]
    let ``clarify re-run over the seeded skeleton still blocks and is byte-stable`` () =
        let root = initializedSpecifiedProject ()

        runClarifyWith None root |> ignore
        let first = TestSupport.readRelative root clarificationPath

        let report = runClarifyWith None root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationAnswer")
        Assert.Equal(first, TestSupport.readRelative root clarificationPath)

        let questionCount =
            first.Split('\n')
            |> Array.filter (fun line -> line.StartsWith("- CQ-"))
            |> Array.length

        Assert.Equal(1, questionCount)

    // Feature 089 (FR-018/FR-019/FR-020, R1/R2/R3, SC-005). THE regression test for the trap: before
    // 089 a skeleton plus a fully answering `clarify --input` recorded the decisions, left both
    // blocking lines standing, reported `succeeded` with `blockingAmbiguities: 2`, and then blocked at
    // `checklist` (rc=1) — replacing "hand-author the file" with a silent failure two stages later.
    [<Fact>]
    let ``answering the seeded skeleton retires the resolved ambiguities and unblocks checklist`` () =
        let root = initializedSpecifiedProject ()

        runClarifyWith None root |> ignore

        let report =
            runClarifyWith (Some "AMB-001: Record decisions in the clarifications artifact.") root

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)

        Assert.Equal(
            Some 0,
            report.Clarification
            |> Option.map (fun summary -> summary.BlockingAmbiguityCount)
        )

        let clarification = TestSupport.readRelative root clarificationPath

        // R1: the resolved ambiguities' lines are retired and the sentinel restored.
        Assert.Contains("No blocking ambiguity remains.", clarification)
        Assert.DoesNotContain("[CQ-001] blocking:", clarification)

        // R2: no empty-state placeholder stands beside a real entry...
        Assert.DoesNotContain("No concrete decisions recorded.", clarification)
        Assert.DoesNotContain("No clarification answers recorded.", clarification)
        // ...but a genuinely empty section keeps its placeholder.
        Assert.Contains("No accepted deferrals recorded.", clarification)

        // R3: the status the tool itself seeded is corrected once nothing blocks.
        Assert.Contains("status: clarified", clarification)
        Assert.DoesNotContain("status: needsAnswers", clarification)

        // And the whole point: the next stage now passes.
        let checklist = TestSupport.runChecklist root workId title
        Assert.Equal(CommandOutcome.Succeeded, checklist.Outcome)

    // Feature 089 (research D10). A PARTIALLY answered clarify blocks and persists nothing. This is
    // pre-existing, correct (never half-write an operator's artifact), and unchanged by 089.
    [<Fact>]
    let ``clarify partial answer blocks and leaves the seeded skeleton untouched`` () =
        let root = initializedSpecifiedProjectWithTwoAmbiguities ()

        runClarifyWith None root |> ignore
        let before = TestSupport.readRelative root clarificationPath

        // Answers AMB-001 but not AMB-002.
        let report =
            runClarifyWith (Some "AMB-001: Record decisions in the clarifications artifact.") root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingClarificationAnswer")
        Assert.Empty(report.ChangedArtifacts)
        Assert.Equal(before, TestSupport.readRelative root clarificationPath)

    // Feature 089 (FR-008). Every declared ambiguity, not just the first, is listed as blocking.
    [<Fact>]
    let ``seeded skeleton lists every declared ambiguity as blocking`` () =
        let root = initializedSpecifiedProjectWithTwoAmbiguities ()

        runClarifyWith None root |> ignore
        let skeleton = TestSupport.readRelative root clarificationPath

        Assert.Contains("- AMB-001 [CQ-001] blocking:", skeleton)
        Assert.Contains("- AMB-002 [CQ-002] blocking:", skeleton)
        Assert.DoesNotContain("No blocking ambiguity remains.", skeleton)

        Assert.Equal(
            Some 2,
            (runClarifyWith None root).Clarification
            |> Option.map (fun summary -> summary.BlockingAmbiguityCount)
        )

    // §3.3 (FR-003, SC-003): a bullet "none outstanding" note under ## Ambiguities does not
    // block clarify with a missing-id error; a genuine AMB-### ambiguity still blocks (the
    // `clarify missing answer blocks` test above covers the genuine-block case).
    [<Fact>]
    let ``clarify proceeds when ambiguities note is a none-outstanding bullet`` () =
        let root = initializedSpecifiedProjectWithoutAmbiguity ()

        let edited =
            (TestSupport.readRelative root specPath).Replace("No material ambiguities recorded.", "- None outstanding")

        TestSupport.writeRelative root specPath edited

        let report = runClarifyWith None root

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.DoesNotContain(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationId")

    // Feature 089. Still blocks on the unknown reference; the seed now lands too, because the run
    // also has unanswered declared ambiguities and no artifact exists to clobber. The bogus AMB-999
    // is NOT recorded — the skeleton enumerates only the ambiguities the specification declares.
    [<Fact>]
    let ``clarify unknown reference blocks and seeds only the declared ambiguities`` () =
        let root = initializedSpecifiedProject ()

        let report = runClarifyWith (Some "AMB-999: Use an unknown ambiguity.") root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownClarificationReference")
        Assert.True(TestSupport.existsRelative root clarificationPath)

        let skeleton = TestSupport.readRelative root clarificationPath

        Assert.DoesNotContain("AMB-999", skeleton)
        Assert.Contains("- CQ-001 [AMB:AMB-001]", skeleton)

    [<Fact>]
    let ``clarify identity mismatch blocks before authored write`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore

        let original =
            (TestSupport.readRelative root clarificationPath).Replace($"workId: {workId}", "workId: 999-other-work")

        TestSupport.writeRelative root clarificationPath original

        let report = TestSupport.runClarify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "clarificationIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root clarificationPath)

    [<Fact>]
    let ``clarify unsafe decision change blocks without mutating existing clarification`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.runClarify root workId title |> ignore
        let before = TestSupport.readRelative root clarificationPath

        let report =
            runClarifyWith (Some "AMB-001: Record the decision somewhere else.") root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unsafeDecisionChange")
        Assert.Equal(before, TestSupport.readRelative root clarificationPath)

    [<Fact>]
    let ``clarify dry run reports proposed changes without mutation`` () =
        let root = initializedSpecifiedProject ()

        let request =
            { TestSupport.clarifyRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.False(TestSupport.existsRelative root clarificationPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = clarificationPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``clarify refreshes generated work model when source data is valid`` () =
        let root = initializedSpecifiedProject ()
        TestSupport.writeValidTasksAndEvidenceFor root workId

        let report = TestSupport.runClarify root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Current
                && view.Sources |> List.exists (fun source -> source.Path = clarificationPath)
        )

    [<Fact>]
    let ``clarify deterministic JSON is byte stable`` () =
        let root = initializedSpecifiedProject ()

        let request =
            { TestSupport.clarifyRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"clarify\"", first)
        Assert.Contains("\"clarification\"", first)
        Assert.DoesNotContain(root, first)

    // ---------------------------------------------------------------------------------------
    // Feature 089 — regressions caught in code review of the first implementation. Each of these
    // was reproduced against the built CLI before the fix.
    // ---------------------------------------------------------------------------------------

    /// An operator's explanation for a STILL-unresolved ambiguity often mentions the ambiguity it
    /// waits on. The first implementation retired any line that merely *mentioned* a resolved id,
    /// so answering AMB-002 silently deleted AMB-001's explanation. Retirement must key off the
    /// line's subject (its first AMB id), exactly as `parseRemainingAmbiguity` classifies it.
    [<Fact>]
    let ``retirement keeps a still-open line that merely mentions a resolved ambiguity`` () =
        let root = initializedSpecifiedProjectWithTwoAmbiguities ()

        runClarifyWith None root |> ignore

        let seeded = TestSupport.readRelative root clarificationPath

        let edited =
            seeded.Replace(
                "- AMB-001 [CQ-001] blocking: Unanswered. Resolve source ambiguity AMB-001 before checklist.",
                "- AMB-001 [CQ-001] blocking: Cannot decide until the AMB-002 question is settled."
            )

        TestSupport.writeRelative root clarificationPath edited

        // Resolve AMB-002 only; AMB-001 stays open.
        runClarifyWith
            (Some "AMB-002: Decisions expire after one release.\nAMB-001 still open: waiting on AMB-002")
            root
        |> ignore

        let after = TestSupport.readRelative root clarificationPath

        Assert.Contains("Cannot decide until the AMB-002 question is settled.", after)
        Assert.DoesNotContain("No blocking ambiguity remains.", after)
        Assert.Contains("status: needsAnswers", after)

    /// The sentinel and a real blocking line cannot both be true (contract K3). Appending a
    /// still-open line to a section that held only the sentinel must retire the sentinel — which
    /// means the retirement runs AFTER the append, not before.
    [<Fact>]
    let ``a new blocking line retires the nothing-remains sentinel`` () =
        let root = initializedSpecifiedProjectWithoutAmbiguity ()

        // A spec with no ambiguity yields a clarified artifact carrying the sentinel.
        TestSupport.runClarify root workId title |> ignore
        Assert.Contains("No blocking ambiguity remains.", TestSupport.readRelative root clarificationPath)

        // The author then introduces an ambiguity and marks it still open.
        let spec =
            (TestSupport.readRelative root specPath)
                .Replace("No material ambiguities recorded.", "- AMB-001 open: Which format?")

        TestSupport.writeRelative root specPath spec

        runClarifyWith (Some "AMB-001 still open: waiting on legal") root |> ignore

        let after = TestSupport.readRelative root clarificationPath

        Assert.DoesNotContain("No blocking ambiguity remains.", after)
        Assert.Contains("- AMB-001 [CQ-001] blocking:", after)

    /// A retirement pass must remove the lines it targets and reformat nothing else: the operator's
    /// blank-line-separated prose survives, and the placeholder beside it does not.
    [<Fact>]
    let ``retirement preserves operator blank lines while dropping the placeholder`` () =
        let root = initializedSpecifiedProject ()

        runClarifyWith None root |> ignore

        let seeded = TestSupport.readRelative root clarificationPath

        let edited =
            seeded.Replace(
                "## Decisions\nNo concrete decisions recorded.",
                "## Decisions\nNo concrete decisions recorded.\n\nNote: first paragraph.\n\nSecond paragraph."
            )

        TestSupport.writeRelative root clarificationPath edited

        runClarifyWith (Some "AMB-001: Record decisions in the clarifications artifact.") root
        |> ignore

        let after = TestSupport.readRelative root clarificationPath

        Assert.DoesNotContain("No concrete decisions recorded.", after)
        Assert.Contains("Note: first paragraph.\n\nSecond paragraph.", after.Replace("\r\n", "\n"))

    /// `List.forall` is vacuously true on an empty list. An absent or hand-emptied Remaining
    /// Ambiguity section is not evidence that anything was resolved, and must never flip the status.
    [<Fact>]
    let ``an emptied remaining-ambiguity section does not flip the status`` () =
        let root = initializedSpecifiedProjectWithoutAmbiguity ()

        TestSupport.runClarify root workId title |> ignore

        let emptied =
            (TestSupport.readRelative root clarificationPath)
                .Replace("## Remaining Ambiguity\nNo blocking ambiguity remains.", "## Remaining Ambiguity")
                .Replace("status: clarified", "status: needsAnswers")

        TestSupport.writeRelative root clarificationPath emptied

        TestSupport.runClarify root workId title |> ignore

        Assert.Contains("status: needsAnswers", TestSupport.readRelative root clarificationPath)

    // ---------------------------------------------------------------------------------------------
    // Feature 093 / FS.GG.SDD#164: the clarify skeleton took its title from *this invocation's*
    // `--title`, falling back to the humanized work id — never from the `spec.md` its own
    // `sourceSpec:` line points at. Feature 089's blocked-seed path newly exposed it in exactly the
    // situation where an author has no reason to pass `--title`.
    // ---------------------------------------------------------------------------------------------

    /// A work item whose spec title differs from both the humanized work id and any `--title`.
    let private specTitle = "Ambient Audio Bed"

    let private initializedWithSpecTitle () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId specTitle |> ignore

        let specifyRequest =
            { TestSupport.specifyRequest root workId specTitle with
                InputText = Some TestSupport.specifyIntentWithAmbiguity }

        TestSupport.runRequest specifyRequest |> ignore
        root

    let private runClarifyWithoutTitle root =
        { TestSupport.clarifyRequest root workId specTitle with
            Title = None }
        |> TestSupport.runRequest

    [<Fact>]
    let ``clarify skeleton inherits the spec title when no --title is passed`` () =
        let root = initializedWithSpecTitle ()

        runClarifyWithoutTitle root |> ignore

        let clarifications = TestSupport.readRelative root clarificationPath

        Assert.Contains($"title: {specTitle}", clarifications)
        Assert.Contains($"# {specTitle} Clarifications", clarifications)
        // The pre-fix value: `titleFromWorkId "006-clarify-command"` = "Clarify Command".
        Assert.DoesNotContain("title: Clarify Command", clarifications)

    [<Fact>]
    let ``clarify --title still wins over the spec title`` () =
        let root = initializedWithSpecTitle ()

        { TestSupport.clarifyRequest root workId specTitle with
            Title = Some "Override" }
        |> TestSupport.runRequest
        |> ignore

        let clarifications = TestSupport.readRelative root clarificationPath

        Assert.Contains("title: Override", clarifications)
        Assert.DoesNotContain($"title: {specTitle}", clarifications)

    [<Fact>]
    let ``clarify falls back to the humanized work id when the spec title is blank`` () =
        let root = initializedWithSpecTitle ()

        let blanked =
            (TestSupport.readRelative root specPath).Replace($"title: {specTitle}", "title:   ")

        TestSupport.writeRelative root specPath blanked

        runClarifyWithoutTitle root |> ignore

        Assert.Contains("title: Clarify Command", TestSupport.readRelative root clarificationPath)

    /// The 089 blocked-seed path: unresolved blocking ambiguities, so `clarify` blocks — but it still
    /// seeds the skeleton, and the skeleton must carry the spec's title.
    [<Fact>]
    let ``the blocked clarify seed carries the spec title`` () =
        let root = initializedWithSpecTitle ()

        let report =
            { TestSupport.clarifyRequest root workId specTitle with
                Title = None
                InputText = None }
            |> TestSupport.runRequest

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.True(TestSupport.existsRelative root clarificationPath)
        Assert.Contains($"title: {specTitle}", TestSupport.readRelative root clarificationPath)

    // ---------------------------------------------------------------------------------------------
    // Feature 093 / FS.GG.SDD#164 (FS.GG.Game feedback §WD4). The reported symptom: with every
    // ambiguity resolved, `unresolvedAmbiguities: 4` sat next to `remaining/blocking = 0`, and an
    // author could not tell which one gated. The root cause was structural — the counter was a regex
    // over `spec.md`'s own body and never read `clarifications.md`, so no `clarify` run could move it.
    // ---------------------------------------------------------------------------------------------

    [<Fact>]
    let ``no ambiguity counter contradicts the gate once everything is resolved`` () =
        let root = initializedSpecifiedProject ()

        // The first run authors the artifact (no prior facts to summarize); the second reads it back.
        TestSupport.runClarify root workId title |> ignore
        let report = TestSupport.runClarify root workId title

        let clarification = report.Clarification.Value

        // The one declared ambiguity is answered, so the two counters that actually gate read 0.
        Assert.Equal(0, clarification.RemainingAmbiguityCount)
        Assert.Equal(0, clarification.BlockingAmbiguityCount)

        // And no third counter disagrees, in any projection. Before this feature the specify-stage
        // summary reported `unresolvedAmbiguities: 1` right here.
        Assert.DoesNotContain("unresolvedAmbiguities", FS.GG.SDD.Commands.CommandRendering.renderText report)
        Assert.DoesNotContain("unresolvedAmbiguityCount", serializeReport report)

    /// FR-003: the two counters that *do* gate keep their values. Two ambiguities, neither answered →
    /// both remaining, both blocking, and the run is blocked.
    ///
    /// The counts only exist from the second run onward: the first has no prior artifact to summarize.
    /// (The `unresolvedBlockingAmbiguity` diagnostic these counts drive fires at the *checklist* stage,
    /// not here — it is already pinned by `clarify then checklist` above and by `ChecklistCommandTests`.)
    [<Fact>]
    let ``the ambiguity counts that gate are unchanged by the counter removal`` () =
        let root = initializedSpecifiedProjectWithTwoAmbiguities ()

        runClarifyWith None root |> ignore
        let rerun = runClarifyWith None root

        Assert.Equal(CommandOutcome.Blocked, rerun.Outcome)
        Assert.Equal(2, rerun.Clarification.Value.RemainingAmbiguityCount)
        Assert.Equal(2, rerun.Clarification.Value.BlockingAmbiguityCount)

    /// FR-013. The undeclared-reference gate predates this feature (`unknownReferenceDiagnostics`
    /// resolves every AMB/CQ/FR/US/AC id in the `--input` lines against the spec's declared sets).
    /// Feature 093 threads a decision's *extra* refs through to the work model and the task graph —
    /// this pins that the threading cannot smuggle an undeclared ref past the gate.
    [<Fact>]
    let ``a multi-ref decision naming an undeclared requirement still blocks`` () =
        let root = initializedSpecifiedProject ()

        let report =
            runClarifyWith
                (Some "AMB-001: Settles FR-001 and FR-999 together.")
                root

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let diagnostic =
            report.Diagnostics
            |> List.find (fun diagnostic -> diagnostic.Id = "unknownClarificationReference")

        Assert.Contains("FR-999", diagnostic.Message)

        // The declared sibling ref does not rescue the line: `FR-001` resolves, `FR-999` does not, and
        // one unresolved ref is enough to block. Feature 089 still seeds a skeleton on a blocked run,
        // and that skeleton echoes the author's own `--input` verbatim — bad ref included, because it
        // is the text they must now correct. Blocking is the contract; a sanitized seed is not.
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

    // ---------------------------------------------------------------------------------------------
    // Feature 093: inheriting the spec's title (above) put a YAML-*decoded* value into an unquoted
    // `title:` slot. A spec whose front matter legally reads `title: "Plan: upstream snapshot"` then
    // emitted `title: Plan: upstream snapshot` — not a scalar — and clarify blocked on the file it had
    // just written, reporting "Clarification front matter is empty." A leading `#` was worse: it parsed
    // as a comment and the title vanished while the command reported success.
    // ---------------------------------------------------------------------------------------------

    let private specWithTitle (rawTitleLine: string) root =
        let spec = TestSupport.readRelative root specPath
        TestSupport.writeRelative root specPath (spec.Replace($"title: {specTitle}", rawTitleLine))
        root

    [<Theory>]
    // A colon would end the scalar; a leading `#` would start a comment; a quote would unbalance it.
    [<InlineData("title: \"Plan: upstream snapshot\"", "Plan: upstream snapshot")>]
    [<InlineData("title: \"#1 priority\"", "#1 priority")>]
    [<InlineData("title: \"Trailing colon:\"", "Trailing colon:")>]
    let ``an inherited spec title that needs quoting round-trips`` (rawTitleLine: string) (decoded: string) =
        let root = initializedWithSpecTitle () |> specWithTitle rawTitleLine

        let report = runClarifyWithoutTitle root

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)

        // The tool must be able to re-read what it wrote: a second run parses the artifact and reports
        // no change rather than blocking on malformed front matter.
        let rerun = runClarifyWithoutTitle root
        Assert.NotEqual(CommandOutcome.Blocked, rerun.Outcome)

        let clarifications = TestSupport.readRelative root clarificationPath
        Assert.Contains($"# {decoded} Clarifications", clarifications)

    /// A title that needs no quoting keeps its exact bytes — the fix must not churn every artifact.
    [<Fact>]
    let ``a plain inherited title is still emitted unquoted`` () =
        let root = initializedWithSpecTitle ()

        runClarifyWithoutTitle root |> ignore

        Assert.Contains($"title: {specTitle}", TestSupport.readRelative root clarificationPath)
        Assert.DoesNotContain($"title: \"{specTitle}\"", TestSupport.readRelative root clarificationPath)
