namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.TestShared
open Xunit

/// FS.GG.SDD#350 / ADR-0035, command level: `evidence --from-test-report <path>` records a receipt for
/// a run **SDD read**, and `observed` stops being structurally zero.
///
/// The counterpart tests in `VerifyCommandTests` / `ShipCommandTests` pin the other half — a work item
/// that records NO receipt behaves exactly as it did before this feature (FR-010). Together they are
/// the migration: the receipt is additive, and stage 3 (a pass with no receipt stops satisfying) is a
/// separate, deliberate, breaking flip that ADR-0035 gates on a schema major.
///
/// Joins ProcessGlobalEnv: the CLI smoke at the bottom spawns a PATH-resolved process, so it must not
/// run while a sibling mutates process-global PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module ObservedRunCommandTests =
    let workId = "011-evidence-command"
    let title = "Evidence Command"
    let evidencePath = $"work/{workId}/evidence.yml"

    let private initializedAnalyzedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        root

    let private trxWith passed failed =
        $"""<?xml version="1.0" encoding="UTF-8"?>
<TestRun id="a" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary outcome="Completed">
    <Counters total="{passed + failed}" executed="{passed + failed}" passed="{passed}" failed="{failed}" error="0" notExecuted="0" />
  </ResultSummary>
</TestRun>"""

    let private reportPath = "artifacts/test-results.trx"

    let private replaceFirst (needle: string) (replacement: string) (text: string) =
        match text.IndexOf(needle, System.StringComparison.Ordinal) with
        | -1 -> failwithf "Fixture drift: %s not found in evidence.yml." needle
        | index -> text.Remove(index, needle.Length).Insert(index, replacement)

    /// A work item whose five obligations all claim `kind: verification`, `result: pass`,
    /// `synthetic: false` — and which nothing ever ran.
    ///
    /// That is not a contrivance: it is what the shared fixture already emits, and it is FS.GG.SDD#350
    /// reproduced exactly. Five obligations reach `supported`, `ship` goes green, and the sole basis is
    /// that those three lines are present in a file the author wrote. This is the state the receipt
    /// exists to give an alternative to.
    let private evidencedProjectClaimingPass () =
        let root = initializedAnalyzedProject ()
        TestSupport.runEvidence root workId title |> ignore
        root

    let private runWithReport root report =
        { TestSupport.evidenceRequest root workId title with
            FromTestReport = report }
        |> TestSupport.runRequest

    let private parsedEvidence root =
        let text = TestSupport.readRelative root evidencePath

        match parseEvidenceArtifact { Path = evidencePath; Text = text } with
        | Ok artifact -> artifact
        | Error diagnostics -> failwith $"evidence.yml did not parse: {diagnostics}"

    // ---- US1: a receipt is READ from a report, not typed ----

    [<Fact>]
    let ``a passing report records a receipt on every obligation claiming a real pass`` () =
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 1630 0)

        let report = runWithReport root (Some reportPath)
        Assert.DoesNotContain(report.Diagnostics, fun d -> d.Severity = Diagnostics.DiagnosticError)

        let artifact = parsedEvidence root
        let claimants = artifact.Evidence |> List.filter claimsRealPass
        Assert.NotEmpty claimants

        for declaration in claimants do
            match declaration.ObservedRun with
            | None -> failwithf "%s carries no receipt" declaration.Id.Value
            | Some run ->
                Assert.Equal(reportPath, run.Source)
                Assert.Equal("passed", run.Outcome)
                Assert.Equal(1630, run.Passed)
                Assert.Equal(0, run.Failed)
                // The digest is SDD's, computed over the bytes it read — not a field the author
                // could supply. This is the single fact that separates a receipt from an assertion.
                Assert.Equal($"sha256:{(SchemaVersion.sha256Text (trxWith 1630 0)).Value}", run.Digest)

    [<Fact>]
    let ``recording a receipt is idempotent`` () =
        // Every field of the receipt is derived from the report, so a second run over the same report
        // must rewrite the same bytes. A receipt that churned would make `evidence` non-deterministic
        // and defeat the no-clobber merge.
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 12 0)

        runWithReport root (Some reportPath) |> ignore
        let first = TestSupport.readRelative root evidencePath

        runWithReport root (Some reportPath) |> ignore
        let second = TestSupport.readRelative root evidencePath

        Assert.Equal(first, second)

    [<Fact>]
    let ``no --from-test-report is inert - the artifact is byte-identical`` () =
        // FR-010, at the byte level: the receipt costs a work item that does not ask for one exactly
        // nothing. This is what makes the whole feature safe to land on a fleet mid-flight.
        let root = evidencedProjectClaimingPass ()

        let before = TestSupport.readRelative root evidencePath
        runWithReport root None |> ignore
        let after = TestSupport.readRelative root evidencePath

        Assert.Equal(before, after)
        Assert.All(parsedEvidence root |> _.Evidence, fun d -> Assert.True(d.ObservedRun.IsNone))

    [<Fact>]
    let ``the receipt is recorded ONLY on a verification obligation, never on a judgement`` () =
        // A suite run discharges a TEST obligation. It says nothing about a review, a deferral, or any
        // other judgement call — and stamping those would manufacture the appearance of observation,
        // which is the overclaim ADR-0035 explicitly warns against ("judgement is not observable, and
        // pretending otherwise re-creates the ceremony problem #351 names").
        //
        // EV001 is demoted to a `review` that still claims `result: pass`. It is the adversarial case:
        // a pass, in the same file, in the same run — and the run says nothing about it.
        let root = evidencedProjectClaimingPass ()

        TestSupport.readRelative root evidencePath
        |> fun text -> replaceFirst "    kind: verification\n" "    kind: review\n" text
        |> TestSupport.writeRelative root evidencePath

        TestSupport.writeRelative root reportPath (trxWith 5 0)
        runWithReport root (Some reportPath) |> ignore

        let byId =
            parsedEvidence root
            |> _.Evidence
            |> List.map (fun d -> d.Id.Value, d)
            |> Map.ofList

        Assert.True(
            byId["EV001"].ObservedRun.IsNone,
            "EV001 is a review — no test run discharged it, yet it carries a receipt"
        )

        // ...and the verification obligations beside it DID record one, so the test is not passing
        // vacuously on a run that recorded nothing at all.
        for id in [ "EV002"; "EV003"; "EV004"; "EV005" ] do
            Assert.True(byId[id].ObservedRun.IsSome, $"{id} is a verification pass and must carry the receipt")

    // ---- US2: `observed` rises, `selfAttested` falls, the invariant holds, readiness does not move ----

    [<Fact>]
    let ``a recorded receipt makes verify report observed, with the #398 invariant intact`` () =
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 1630 0)
        runWithReport root (Some reportPath) |> ignore

        let report = TestSupport.runVerify root workId title

        match report.Verification with
        | None -> failwith "verify produced no summary."
        | Some verification ->
            Assert.True(verification.EvidenceSupportedCount > 0, "fixture must supply supported obligations")

            // The whole feature, in one line: before #350 this was structurally 0.
            Assert.True(
                verification.EvidenceObservedCount > 0,
                "a recorded receipt must make `observed` rise — that is the entire point of #350"
            )

            // FR-010 / spec 101's standing obligation: `supported` did not move, and the two halves
            // still partition it. `observed` rose and `selfAttested` fell by the same amount, with NO
            // schema, projection, or consumer changed.
            Assert.Equal(
                verification.EvidenceSupportedCount,
                verification.EvidenceSelfAttestedCount + verification.EvidenceObservedCount
            )

            // The `TD-` mirror moves with it — same rule object, so the two cannot drift on what
            // "observed" means.
            Assert.True(verification.TestObservedCount > 0)

            Assert.Equal(
                verification.TestSatisfiedCount,
                verification.TestSelfAttestedCount + verification.TestObservedCount
            )

    [<Fact>]
    let ``a receipt does not change readiness - stage 2 gates nothing`` () =
        // THE regression guard for FR-010, and the reason this feature is landable on its own.
        // ADR-0035 stages the migration: `unobserved` does not stop satisfying until stage 3, on a
        // schema major, once the fleet is green. A fleet cannot GET green until receipts can be
        // recorded — so this must be provably non-gating, or it cannot ship first.
        let withReceipt =
            let root = evidencedProjectClaimingPass ()
            TestSupport.writeRelative root reportPath (trxWith 9 0)
            runWithReport root (Some reportPath) |> ignore
            TestSupport.runVerify root workId title

        let without =
            let root = evidencedProjectClaimingPass ()
            runWithReport root None |> ignore
            TestSupport.runVerify root workId title

        match withReceipt.Verification, without.Verification with
        | Some a, Some b ->
            Assert.Equal(b.Readiness, a.Readiness)
            Assert.Equal(b.EvidenceSupportedCount, a.EvidenceSupportedCount)
            Assert.Equal(b.TestSatisfiedCount, a.TestSatisfiedCount)

            // ...and yet the two are genuinely different runs: one observed, one did not.
            Assert.Equal(0, b.EvidenceObservedCount)
            Assert.True(a.EvidenceObservedCount > 0)
        | _ -> failwith "verify produced no summary."

    // ---- US3: a report that cannot be believed is REFUSED, and records nothing ----

    let private assertBlockedRecordingNothing root diagnosticId (report: string option) =
        let result = runWithReport root report

        Assert.Contains(result.Diagnostics, fun d -> d.Id = diagnosticId)

        Assert.All(
            parsedEvidence root |> _.Evidence,
            fun d -> Assert.True(d.ObservedRun.IsNone, "a refused report must record NOTHING")
        )

    [<Fact>]
    let ``an unparseable report blocks and records nothing`` () =
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath "this is not a test report"

        assertBlockedRecordingNothing root "evidence.testReportUnparseable" (Some reportPath)

    [<Fact>]
    let ``a report that is not on disk blocks and records nothing`` () =
        // Deliberately not a silent no-op: `--from-test-report` naming a report that is not there is
        // the author believing a run was observed when none was. Answering that with silence would
        // leave the obligation self-attested while looking recorded — a fail-open in a new place.
        let root = evidencedProjectClaimingPass ()

        assertBlockedRecordingNothing root "evidence.testReportNotFound" (Some "artifacts/never-produced.trx")

    [<Fact>]
    let ``a report path escaping the repository blocks and records nothing`` () =
        // Lexical containment, decided on the RAW value before normalization (FS.GG.SDD#185/#365):
        // no read effect is planned at all, so a `..` chain is never resolved at the edge.
        let root = evidencedProjectClaimingPass ()

        assertBlockedRecordingNothing root "evidence.testReportPathEscape" (Some "../../etc/passwd")

    [<Fact>]
    let ``a FAILING report blocks a claimed pass and records nothing`` () =
        // The artifact and the claim contradict each other, and the artifact is the one nobody typed.
        // Recording it would leave a receipt that `isObserved` rejects — indistinguishable, downstream,
        // from no receipt at all — so the author gets told, loudly, instead.
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 8 2)

        assertBlockedRecordingNothing root "evidence.observedRunFailed" (Some reportPath)

    [<Fact>]
    let ``a hand-authored incoherent receipt is refused`` () =
        // The receipt must not become a new, more official-looking place to type `pass`. `evidence.yml`
        // is a text file; `TestReport.parse` can never emit this, but a person can write it.
        let root = evidencedProjectClaimingPass ()
        let digest = "sha256:" + String.replicate 64 "a"

        let liar =
            "    result: pass\n"
            + "    observedRun:\n"
            + "      source: artifacts/test-results.trx\n"
            + $"      digest: {digest}\n"
            + "      outcome: passed\n"
            + "      passed: 3\n"
            + "      failed: 9\n" // <- contradicts `outcome: passed`
            + "      skipped: 0\n"

        TestSupport.readRelative root evidencePath
        |> fun text -> text.Replace("    result: pass\n", liar)
        |> TestSupport.writeRelative root evidencePath

        let result = runWithReport root None

        Assert.Contains(result.Diagnostics, fun d -> d.Id = "evidence.observedRunInconsistent")

    // ---- The real CLI: the flag has to actually be a flag ----

    /// Every test above drives the handler IN-PROCESS, through a `CommandRequest` record it builds
    /// itself. That bypasses argument parsing entirely — so if `--from-test-report` were never
    /// registered as a valued option, all of them would still pass while the shipped binary silently
    /// ignored the flag and recorded nothing.
    ///
    /// This is the only test that would catch that, which is exactly why it exists.
    [<Fact; Trait("tier", "slow")>]
    let ``the real CLI accepts --from-test-report and records the receipt`` () =
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 1630 0)

        let exitCode, stdout, stderr =
            [ "evidence"
              "--root"
              root
              "--work"
              workId
              "--from-test-report"
              reportPath
              "--text" ]
            |> TestSupport.runCliRaw 30000

        Assert.Equal("", stderr)
        Assert.Equal(0, exitCode)
        Assert.Contains("command: evidence", stdout)

        let evidence = TestSupport.readRelative root evidencePath
        Assert.Contains("observedRun:", evidence)
        Assert.Contains($"source: {reportPath}", evidence)
        Assert.Contains("outcome: passed", evidence)
        Assert.Contains("passed: 1630", evidence)
        // Quoted, because a bare YAML scalar cannot contain . The renderer is right; an
        // assertion that expected it bare would have been the thing that was wrong.
        Assert.Contains("digest: \"sha256:", evidence)

    /// The `--from-tests` path (feature 077) must keep working unchanged beside it. ADR-0035 proposed
    /// REUSING that flag for the receipt, having read it as already taking a report path; it does not —
    /// it takes the directory the tests live in, and this test passes it one. Overloading the flag
    /// would have turned this invocation into a blocking `testReportUnparseable`.
    [<Fact; Trait("tier", "slow")>]
    let ``--from-tests still takes a test LOCATION, not a report`` () =
        let root = evidencedProjectClaimingPass ()

        let exitCode, _, stderr =
            [ "evidence"
              "--root"
              root
              "--work"
              workId
              "--from-tests"
              "tests/FS.GG.SDD.Foo.Tests"
              "--text" ]
            |> TestSupport.runCliRaw 30000

        Assert.Equal("", stderr)
        Assert.Equal(0, exitCode)

        // ...and it recorded no receipt, because a directory is not a run.
        Assert.All(parsedEvidence root |> _.Evidence, fun d -> Assert.True(d.ObservedRun.IsNone))

    // ---- The committed verdict: the reason #350 is urgent rather than merely correct ----

    /// ADR-0026 commits a compact ship verdict to git history, and ADR-0035's "Why now" is precisely
    /// that: *"a verdict that certifies unverifiable claims is worse once it is permanent."* A reader in
    /// a year sees `shipReady` and reads it as "this works".
    ///
    /// So the receipt has to survive all the way to that artifact — not just to the console. It does,
    /// and #398 is why it costs nothing: `ship` reads the per-obligation basis `verify` recorded, so
    /// the day a receipt exists every counter moves without `ship` being touched.
    [<Fact>]
    let ``the receipt reaches ship, and the committed verdict says what its green is worth`` () =
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 1630 0)
        runWithReport root (Some reportPath) |> ignore
        TestSupport.runVerify root workId title |> ignore

        let report = TestSupport.runShip root workId title

        match report.Ship with
        | None -> failwith "ship produced no summary."
        | Some ship ->
            Assert.Equal("shipReady", ship.Readiness)
            Assert.True(ship.EvidenceSupportedCount > 0)

            // Before #350 this was structurally 0, and `selfAttested` structurally equal to
            // `supported`. Both now move — and the invariant that ties them still holds.
            Assert.True(ship.EvidenceObservedCount > 0, "the receipt must reach `ship`")

            Assert.Equal(ship.EvidenceSupportedCount, ship.EvidenceSelfAttestedCount + ship.EvidenceObservedCount)

    [<Fact>]
    let ``a recorded receipt SURVIVES a later evidence run made without the flag`` () =
        // The receipt is recorded once and then read back on every subsequent `evidence` run. If a
        // merge or codec regression dropped it on re-render, every obligation in the fleet would
        // silently revert to self-attested — and no other test here would notice, because they all
        // start from a work item that has no receipt and so cannot tell "preserved nothing" apart
        // from "dropped something".
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 1630 0)

        runWithReport root (Some reportPath) |> ignore
        let recorded = TestSupport.readRelative root evidencePath
        Assert.Contains("observedRun:", recorded)

        // Re-run with NO --from-test-report. The receipt must still be there, byte-for-byte.
        runWithReport root None |> ignore

        Assert.Equal(recorded, TestSupport.readRelative root evidencePath)
        Assert.All(parsedEvidence root |> _.Evidence, fun d -> Assert.True(d.ObservedRun.IsSome))

        // ...and it still counts: `verify` reads the receipt off disk, not out of the run that made it.
        match (TestSupport.runVerify root workId title).Verification with
        | Some verification -> Assert.True(verification.EvidenceObservedCount > 0)
        | None -> failwith "verify produced no summary."

    // ---- US4 / ADR-0035 stage 3: `verify --require-observed` — the failure leg #266 demands ----
    //
    // Stage 2 (above) made a receipt RECORDABLE and provably changed no verdict. Stage 3 makes the
    // absence of one COUNT — but only when asked, because ADR-0035 gates the default flip on "once
    // the fleet is green" and no `evidence.yml` in this repo yet carries a receipt. So the mechanism
    // and its proof land here, off by default; a human flips the default on a schema major.
    //
    // The four tests below are the whole contract, and each one fails a different way if it is wrong:
    // the fabricated lifecycle must be REFUSED, an observed one must still PASS (or the flag is just a
    // brick), the flipped DEFAULT must now block that fabricated pass while `--no-require-observed`
    // restores it (ADR-0035 stage 3b / FS.GG.SDD#497 — this replaced the old "default unchanged"
    // assertion), and an honest deferral must not be PUNISHED (or the gate reads "no receipt" as
    // "lying").

    let private runVerifyRequiringObserved root =
        { TestSupport.verifyRequest root workId title with
            RequireObserved = true }
        |> TestSupport.runRequest

    let private runShipRequiringObserved root =
        { TestSupport.shipRequest root workId title with
            RequireObserved = true }
        |> TestSupport.runRequest

    /// **The FS.GG.SDD#350 failure leg, committed.** This is the boilerplate probe from the issue —
    /// a lifecycle walked on pure scaffolding, `result: pass` / `synthetic: false` on every
    /// obligation, and nothing ever run — and under `--require-observed` it must NOT reach
    /// `shipReady`.
    ///
    /// #266's standing note is that a fix whose failure leg is untested is how this class of defect
    /// survives: the gate is asserted to pass on good input and never asserted to *fail* on bad. So
    /// the assertion that matters is the last one, and it is about `ship` — the merge boundary —
    /// not about `verify`.
    [<Fact>]
    let ``a fabricated lifecycle cannot reach shipReady under --require-observed`` () =
        let root = evidencedProjectClaimingPass ()

        let verified = runVerifyRequiringObserved root

        Assert.Contains(verified.Diagnostics, fun d -> d.Id = "verify.unobservedRequiredTest")

        match verified.Verification with
        | None -> failwith "verify produced no summary."
        | Some verification ->
            Assert.Equal("needsVerificationCorrection", verification.Readiness)
            Assert.True(verification.BlockingCount > 0, "an unobserved pass must block, not merely warn")

            // Not "some tests satisfied". NONE — every one of them was an authored word.
            Assert.Equal(0, verification.TestSatisfiedCount)

        // The one that closes the issue: paperwork alone does not cross the merge boundary.
        match (runShipRequiringObserved root).Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.NotEqual<string>("shipReady", ship.Readiness)

    /// The other half, and the one that makes the flag worth having rather than merely strict: work
    /// that DID run its suite still ships. A gate that blocks everything is not a gate, it is an
    /// outage — and it would be the fastest possible route to someone deleting this feature.
    [<Fact>]
    let ``an observed pass still reaches shipReady under --require-observed`` () =
        let root = evidencedProjectClaimingPass ()
        TestSupport.writeRelative root reportPath (trxWith 9 0)
        runWithReport root (Some reportPath) |> ignore

        let verified = runVerifyRequiringObserved root

        Assert.DoesNotContain(verified.Diagnostics, fun d -> d.Id = "verify.unobservedRequiredTest")

        match verified.Verification with
        | None -> failwith "verify produced no summary."
        | Some verification ->
            Assert.Equal("verificationReady", verification.Readiness)
            Assert.True(verification.TestSatisfiedCount > 0)

        match (runShipRequiringObserved root).Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.Equal("shipReady", ship.Readiness)

    /// **The fail-open a green test suite could not see, and a CLI walk caught in one command.**
    ///
    /// `verify --require-observed` blocks — and a blocked stage writes NOTHING, by constitutional
    /// design. So the `verify.json` from the last *unflagged*, green run is still on disk, and every
    /// source digest still matches it: nothing downstream can tell that the gate ever fired. `ship`
    /// read that record and certified `shipReady` over a lifecycle `verify` had just refused.
    ///
    /// That is #266's own rule — *compare against reality, not a record of reality* — failing inside
    /// the fix for #266. It is why `ship` re-asserts the receipt on the record rather than trusting
    /// `verificationReady`, and why the flag is on BOTH stages instead of the one that "obviously"
    /// needed it.
    ///
    /// The first line of this test is the entire bug: a plain, successful `verify`.
    [<Fact>]
    let ``a stale green verify.json does not launder an unobserved pass past ship`` () =
        let root = evidencedProjectClaimingPass ()

        // The green record. Written before anyone asked for a receipt, and never invalidated.
        match (TestSupport.runVerify root workId title).Verification with
        | Some verification -> Assert.Equal("verificationReady", verification.Readiness)
        | None -> failwith "verify produced no summary."

        // The gate fires, and writes nothing — so the green record above still stands.
        match (runVerifyRequiringObserved root).Verification with
        | Some verification -> Assert.Equal("needsVerificationCorrection", verification.Readiness)
        | None -> failwith "verify produced no summary."

        let shipped = runShipRequiringObserved root

        Assert.Contains(shipped.Diagnostics, fun d -> d.Id = "ship.unobservedEvidence")

        // It must NAME them. A blocking diagnostic that reports "3 obligations" and lists none sends
        // the operator back to verify to diff for the ids the tool already had in hand.
        Assert.All(
            shipped.Diagnostics |> List.filter (fun d -> d.Id = "ship.unobservedEvidence"),
            fun d -> Assert.NotEmpty d.RelatedIds
        )

        match shipped.Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.NotEqual<string>("shipReady", ship.Readiness)

    /// **ADR-0035 stage 3b, the flip — this was the tripwire, and it has now been tripped.**
    ///
    /// Until FS.GG.SDD#497, this test asserted the opposite: that the default was OPT-IN and a
    /// fabricated `result: pass` still sailed through `verify`/`ship` when nobody passed
    /// `--require-observed`. Its docstring named itself the executable marker — *"when a human
    /// decides the fleet is green and flips the default, THIS is the test that goes red —
    /// deliberately, and with the whole decision written down beside it."* A human decided
    /// (EHotwagner, 2026-07-17) to flip **ahead** of the fleet being green — a deliberate override
    /// of ADR-0035's second precondition, recorded in ADR-0035 § Migration and FS.GG.SDD#497. So
    /// the marker is inverted here, on purpose.
    ///
    /// The flip lives in the CLI arg parse (`Program.fs`: `RequireObserved = not (hasFlag
    /// "--no-require-observed" ...)`), NOT in the in-process harness default — which is left at the
    /// pre-flip opt-out on purpose (see TestSupport.fs) so orthogonal fixtures stay isolated from the
    /// gate. That is exactly why this test now drives the REAL CLI: it is the one place the flipped
    /// default is observable. The pre-flip behavior is not gone — it is one `--no-require-observed`
    /// away, which this test also pins so the migration escape hatch cannot silently rot.
    [<Fact; Trait("tier", "slow")>]
    let ``the flipped default blocks an unobserved pass - --no-require-observed restores it`` () =
        let root = evidencedProjectClaimingPass ()

        // The DEFAULT, at the real boundary: no flag. Post-#497 this must fail closed. Per
        // FS.GG.SDD#535 a blocked report routes to STDOUT (the automation contract), so the
        // diagnostic lands there; stderr stays empty and the exit code alone signals blocked.
        let defaultExit, defaultStdout, defaultStderr =
            [ "verify"; "--root"; root; "--work"; workId ] |> TestSupport.runCliRaw 30000

        Assert.NotEqual(0, defaultExit)
        Assert.Contains("verify.unobservedRequiredTest", defaultStdout)
        Assert.DoesNotContain("verificationReady", defaultStdout)
        Assert.Equal("", defaultStderr.Trim())

        // The escape hatch: `--no-require-observed` restores byte-for-byte the pre-flip opt-out,
        // so a work item that has not yet adopted receipts is not stopped dead mid-migration.
        let optOutExit, optOutStdout, _ =
            [ "verify"; "--root"; root; "--work"; workId; "--no-require-observed" ]
            |> TestSupport.runCliRaw 30000

        Assert.Equal(0, optOutExit)
        Assert.DoesNotContain("verify.unobservedRequiredTest", optOutStdout)
        Assert.Contains("verificationReady", optOutStdout)

    /// The gate says "no run was observed" — NOT "you are lying". A deferral claims no pass at all,
    /// so it asserts no run and cannot be caught failing to evidence one. The ladder encodes that by
    /// ORDERING (the deferral arms sit above `unobserved`), and ordering is exactly the kind of thing
    /// that rots silently — so it is pinned here rather than left to the reader of an `elif` chain.
    ///
    /// The deferral is written out in full — all four of `rationale`/`owner`/`scope`/
    /// `laterLifecycleVisibility` — on purpose. A bare `result: deferred` is REFUSED upstream as
    /// `evidence.missingDeferralRationale`, so a lazier fixture would still not be flagged
    /// `unobserved`, and this test would pass while proving nothing. It has to be an *honest*
    /// deferral for its exemption to mean anything.
    [<Fact>]
    let ``--require-observed does not punish an honest deferral`` () =
        let root = evidencedProjectClaimingPass ()

        let deferral =
            "result: deferred\n    rationale: The suite for this obligation lands with the follow-on work item.\n"
            + "    owner: rook-bd94\n    scope: the deferred obligation\n"
            + "    laterLifecycleVisibility: Re-open when the follow-on work item is specified."

        TestSupport.readRelative root evidencePath
        |> replaceFirst "result: pass" deferral
        |> TestSupport.writeRelative root evidencePath

        let deferredId =
            (parsedEvidence root).Evidence
            |> List.find (fun declaration -> normalizedEvidenceResult declaration.Result = "deferred")
            |> _.Id
            |> _.Value

        // The other four obligations still claim an unobserved pass, so the diagnostic IS raised —
        // and that is what makes the exemption legible: it names them, and it does not name this one.
        let unobserved =
            (runVerifyRequiringObserved root).Diagnostics
            |> List.filter (fun d -> d.Id = "verify.unobservedRequiredTest")

        Assert.NotEmpty unobserved
        Assert.All(unobserved, (fun d -> Assert.DoesNotContain(deferredId, d.RelatedIds)))
