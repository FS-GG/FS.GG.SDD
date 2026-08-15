namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.TestShared
open Xunit

/// FS.GG.SDD#865, command level: an obligation discharged by a durable **record** can reach `ship`.
///
/// The defect these fixtures close is structural rather than a bug in any one gate. `Observed` had
/// exactly one true-maker — an `observedRun` receipt parsed from a runner's report — so an obligation
/// discharged by filing a row, recording a decision, or performing a routing was `observed: false` by
/// construction, `verify.unobservedRequiredTest` fired forever, and `ship` was unreachable. Two
/// `.github` items merged with `verify` blocked rather than fabricate a receipt.
///
/// Each test below names the acceptance criterion it discharges. The negative cases are not decoration:
/// a gate asserted only to pass on good input is the failure mode #266 keeps measuring, so every
/// positive case here has a negative twin proving the same gate can fail.
module RecordDischargedObligationTests =
    let private workId = "011-evidence-command"
    let private title = "Evidence Command"
    let private evidencePath = $"work/{workId}/evidence.yml"
    let private tasksPath = $"work/{workId}/tasks.yml"
    let private verifyPath = $"readiness/{workId}/verify.json"

    /// The repository-local decision record the `decision` receipts below bind. A real file, because the
    /// receipt binds its real bytes — that binding is the whole reason a local record is stronger
    /// evidence than a remote one, and a fixture that faked it would prove nothing.
    let private recordPath = "docs/decisions/record-865.md"
    let private recordText = "# DEC-865\n\nThe route decision is recorded here.\n"

    let private sha256Of (text: string) =
        "sha256:" + (SchemaVersion.sha256Bytes (Encoding.UTF8.GetBytes text)).Value

    let private replaceFirst (needle: string) (replacement: string) (text: string) =
        match text.IndexOf(needle, StringComparison.Ordinal) with
        | -1 -> failwithf "Fixture drift: %s not found." needle
        | index -> text.Remove(index, needle.Length).Insert(index, replacement)

    let private runVerify root =
        { TestSupport.verifyRequest root workId title with
            RequireObserved = true }
        |> TestSupport.runRequest

    let private runShip root =
        { TestSupport.shipRequest root workId title with
            RequireObserved = true }
        |> TestSupport.runRequest

    let private parsedEvidence root =
        let text = TestSupport.readRelative root evidencePath

        match
            Evidence.parseEvidenceArtifact
                { Path = evidencePath
                  Text = text
                  RawBytes = None }
        with
        | Ok artifact -> artifact
        | Error diagnostics -> failwith $"evidence.yml did not parse: {diagnostics}"

    /// Tag every task `record-discharge`, then re-derive downstream so the obligations are re-minted from
    /// the tagged tasks. `requiredSkills` is authored state the task generator unions across
    /// regeneration (#310), which is exactly why DEC-003 put the class there.
    let private recordDischargedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title

        TestSupport.readRelative root tasksPath
        |> fun text -> text.Replace("requiredSkills: [", "requiredSkills: [record-discharge, ")
        |> TestSupport.writeRelative root tasksPath

        TestSupport.runRefresh root workId |> ignore
        TestSupport.runAnalyze root workId title |> ignore
        TestSupport.writeRelative root recordPath recordText
        root

    /// Attach a `recordReceipt` block to every declaration in the file, immediately after its
    /// `result: pass` line — the same position `observedRun` occupies in an authored declaration.
    let private attachReceipts root (receiptYaml: string) =
        let text = TestSupport.readRelative root evidencePath
        let needle = "    result: pass\n"

        let replaced = text.Replace(needle, needle + receiptYaml)

        if replaced = text then
            failwith "Fixture drift: no `result: pass` line to attach a receipt to."

        TestSupport.writeRelative root evidencePath replaced

    let private decisionReceiptYaml digest =
        $"""    recordReceipt:
      kind: decision
      locator: {recordPath}
      locatorContract: durable-locator-v1
      digest: "{digest}"
      statement: "The route decision is recorded in the decision record."
      recordedAt: "2026-08-15T09:49:00Z"
"""

    let private coherentDecisionReceipt = decisionReceiptYaml (sha256Of recordText)

    /// A record-discharged package whose every obligation claims a real pass AND names its record.
    let private recordedAndPassing () =
        let root = recordDischargedProject ()
        TestSupport.writePassingTaskEvidenceFor root workId
        attachReceipts root coherentDecisionReceipt
        root

    let private diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map _.Id |> List.distinct |> List.sort

    // ===== AC-001 / FR-001: the obligation declares its class, and verify persists it =====

    [<Fact>]
    let ``a record-discharge tagged task mints a record-class obligation`` () =
        let root = recordDischargedProject ()

        match
            TestSupport.readRelative root tasksPath
            |> fun text ->
                Task.parseTaskFacts
                    { Path = tasksPath
                      Text = text
                      RawBytes = None }
        with
        | Error diagnostics -> failwith $"tasks.yml did not parse: {diagnostics}"
        | Ok facts ->
            let obligations = FS.GG.SDD.Commands.Internal.EvidenceDomain.obligations facts
            Assert.NotEmpty obligations

            for obligation in obligations do
                Assert.Equal(Evidence.recordDischargeClass, obligation.DischargeClass)
                // The correction must not send a record author to run a suite that cannot exist for
                // their obligation — naming a remedy that cannot be performed is how the original
                // defect stayed unresolved for two whole items.
                Assert.Contains("recordReceipt", obligation.Correction)
                Assert.DoesNotContain("from-test-report", obligation.Correction)

    [<Fact>]
    let ``verify writes recordRequirement onto both disposition arrays`` () =
        let root = recordedAndPassing ()
        runVerify root |> ignore

        // Asserted over the WRITTEN view, not the in-memory draft: the committed view is what `ship` and
        // the Governance handoff read, and a flag that never reached disk would leave `ship` unable to
        // tell the classes apart at the boundary that matters.
        let view = TestSupport.readRelative root verifyPath
        Assert.Contains("\"recordRequirement\": true", view)

        match
            Verify.parseVerificationView
                { Path = verifyPath
                  Text = view
                  RawBytes = None }
        with
        | Error diagnostics -> failwith $"verify.json did not parse: {diagnostics}"
        | Ok parsed ->
            Assert.NotEmpty parsed.EvidenceDispositions
            Assert.NotEmpty parsed.TestDispositions
            Assert.All(parsed.EvidenceDispositions, fun d -> Assert.True d.RecordRequirement)
            Assert.All(parsed.TestDispositions, fun d -> Assert.True d.RecordRequirement)

    // ===== AC-002 / FR-002: the positive fixture — a recorded obligation reaches ship =====

    [<Fact>]
    let ``a record obligation with a coherent receipt is observed, satisfied, and reaches shipReady`` () =
        let root = recordedAndPassing ()

        let verified = runVerify root
        Assert.DoesNotContain("verify.unrecordedRequiredRecord", diagnosticIds verified)
        Assert.DoesNotContain("verify.unobservedRequiredTest", diagnosticIds verified)

        match verified.Verification with
        | None -> failwith "verify produced no summary."
        | Some verification ->
            Assert.Equal("verificationReady", verification.Readiness)
            Assert.True(verification.TestSatisfiedCount > 0, "a recorded obligation must satisfy its TD- mirror")

        match
            Verify.parseVerificationView
                { Path = verifyPath
                  Text = TestSupport.readRelative root verifyPath
                  RawBytes = None }
        with
        | Error diagnostics -> failwith $"verify.json did not parse: {diagnostics}"
        | Ok parsed -> Assert.All(parsed.EvidenceDispositions, fun d -> Assert.True d.Observed)

        // The assertion that closes the issue: `ship`, which was unreachable, is reached.
        match (runShip root).Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.Equal("shipReady", ship.Readiness)

    // ===== AC-005 / FR-005: the negative twin — the author's word alone still does not suffice =====

    [<Fact>]
    let ``a record obligation resting on result pass alone does not satisfy and cannot ship`` () =
        let root = recordDischargedProject ()
        TestSupport.writePassingTaskEvidenceFor root workId
        // No receipt attached: `result: pass`, `synthetic: false`, and nothing naming a record.

        let verified = runVerify root
        Assert.Contains("verify.unrecordedRequiredRecord", diagnosticIds verified)

        match verified.Verification with
        | None -> failwith "verify produced no summary."
        | Some verification ->
            Assert.Equal("needsVerificationCorrection", verification.Readiness)
            Assert.True(verification.BlockingCount > 0, "an unrecorded pass must block, not merely warn")
            Assert.Equal(0, verification.TestSatisfiedCount)

        match (runShip root).Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.NotEqual<string>("shipReady", ship.Readiness)

    // ===== AC-003 / FR-003: a malformed receipt is invalid, not merely unrecorded =====

    [<Fact>]
    let ``a malformed receipt is reported as invalid, naming the field, rather than as a missing record`` () =
        let root = recordDischargedProject ()
        TestSupport.writePassingTaskEvidenceFor root workId
        attachReceipts root (coherentDecisionReceipt.Replace("kind: decision", "kind: tweet"))

        let verified = runVerify root
        let ids = diagnosticIds verified

        Assert.Contains("evidence.recordReceiptInvalid", ids)
        // Not `unrecordedRequiredRecord`: the author DID write a receipt, and telling them to add the
        // one they already wrote is the response that would waste their time.
        Assert.DoesNotContain("verify.unrecordedRequiredRecord", ids)

        Assert.Contains(
            verified.Diagnostics,
            fun d -> d.Id = "evidence.recordReceiptInvalid" && d.Message.Contains "kind"
        )

        match (runShip root).Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.NotEqual<string>("shipReady", ship.Readiness)

    // ===== AC-004 / FR-004: the record is byte-bound and probed =====

    [<Fact>]
    let ``editing the decision record after the receipt turns it stale at verify and at ship`` () =
        let root = recordedAndPassing ()

        // Prove the fixture is green BEFORE the mutation, so the red below is attributable to the edit
        // rather than to a fixture that never worked.
        runVerify root |> ignore

        match (runShip root).Ship with
        | Some ship -> Assert.Equal("shipReady", ship.Readiness)
        | None -> failwith "ship produced no summary before the mutation."

        TestSupport.writeRelative root recordPath (recordText + "\nAnd then it said something else.\n")

        // At `verify` a stale receipt turns its disposition `invalid` and blocks; the NAMED diagnostic
        // is raised at `ship`. That split is inherited from `observedRunStale` rather than invented
        // here, and keeping it is the point: the two receipt channels must degrade the same way, or a
        // reader learns two different failure vocabularies for one idea.
        match (runVerify root).Verification with
        | None -> failwith "verify produced no summary."
        | Some verification -> Assert.NotEqual<string>("verificationReady", verification.Readiness)

        let shipped = runShip root
        Assert.Contains("evidence.recordReceiptStale", diagnosticIds shipped)

        match shipped.Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.NotEqual<string>("shipReady", ship.Readiness)

    [<Fact>]
    let ``deleting the decision record turns the obligation invalid through the cited-artifact cascade`` () =
        let root = recordedAndPassing ()

        File.Delete(Path.Combine(root, recordPath.Replace('/', Path.DirectorySeparatorChar)))

        // No new gate was added for this: the decision locator is a cited path, so #349's existing
        // cascade catches it. Asserting the EXISTING diagnostic id is the point — a new one here would
        // mean the locator had not really joined the cited set.
        Assert.Contains("evidence.artifactNotFound", diagnosticIds (runVerify root))

        match (runShip root).Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.NotEqual<string>("shipReady", ship.Readiness)

    // ===== AC-006 / AC-007 / FR-006 / FR-007: the two classes are reported separately =====

    [<Fact>]
    let ``an unmet record obligation and an unmet test obligation are named by their own diagnostics`` () =
        // The discrimination this whole issue is about. Both classes are unmet in ONE package, so a
        // gate that merely renamed the old diagnostic would fail here: each id must name its own
        // obligations and only those.
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        TestSupport.runEvidence root workId title |> ignore

        // T001 alone becomes record-discharged; every other task stays test-discharged.
        TestSupport.readRelative root tasksPath
        |> replaceFirst "requiredSkills: [" "requiredSkills: [record-discharge, "
        |> TestSupport.writeRelative root tasksPath

        TestSupport.runRefresh root workId |> ignore
        TestSupport.runAnalyze root workId title |> ignore
        TestSupport.writePassingTaskEvidenceFor root workId

        let verified = runVerify root
        let ids = diagnosticIds verified

        Assert.Contains("verify.unrecordedRequiredRecord", ids)
        Assert.Contains("verify.unobservedRequiredTest", ids)

        let idsNamedBy diagnosticId =
            verified.Diagnostics
            |> List.filter (fun d -> d.Id = diagnosticId)
            |> List.collect _.RelatedIds
            |> List.distinct
            |> List.sort

        let recordIds = idsNamedBy "verify.unrecordedRequiredRecord"
        let testIds = idsNamedBy "verify.unobservedRequiredTest"

        Assert.Equal<string list>([ "EV001" ], recordIds)
        Assert.NotEmpty testIds
        Assert.DoesNotContain("EV001", testIds)

        // AC-007: `ship` partitions the same population the same way, over the record `verify` WROTE.
        //
        // The blocked run above wrote nothing (an incomplete run never reports complete), so the
        // scenario `ship`'s own gate exists for has to be constructed the way it actually arises: a
        // green `verify.json` recorded WITHOUT the requirement, still on disk and still digest-current,
        // which a later `ship` must refuse rather than inherit. That green record is where the
        // `recordRequirement` flags come from, so this also proves `ship` reads the class off the
        // committed view rather than re-deriving it.
        TestSupport.runVerify root workId title |> ignore

        let shipped = runShip root
        let shipIds = diagnosticIds shipped
        Assert.Contains("ship.unrecordedEvidence", shipIds)
        Assert.Contains("ship.unobservedEvidence", shipIds)

        let obligationsNamedBy diagnosticId =
            shipped.Diagnostics
            |> List.filter (fun d -> d.Id = diagnosticId)
            |> List.collect _.RelatedIds
            |> List.distinct
            |> List.sort

        Assert.Equal<string list>([ "EV001" ], obligationsNamedBy "ship.unrecordedEvidence")
        Assert.DoesNotContain("EV001", obligationsNamedBy "ship.unobservedEvidence")

        match shipped.Ship with
        | None -> failwith "ship produced no summary."
        | Some ship -> Assert.NotEqual<string>("shipReady", ship.Readiness)

    // ===== AC-008 / FR-008: neither receipt substitutes for the other =====

    [<Fact>]
    let ``a record receipt does not discharge a test-class obligation`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        TestSupport.runEvidence root workId title |> ignore
        TestSupport.writePassingTaskEvidenceFor root workId
        TestSupport.writeRelative root recordPath recordText
        attachReceipts root coherentDecisionReceipt

        // The receipt is coherent — so this asserts the DISPATCH, not an incidental malformation.
        let byId =
            parsedEvidence root
            |> _.Evidence
            |> List.map (fun d -> d.Id.Value, d)
            |> Map.ofList

        Assert.True(Evidence.isRecorded byId["EV001"])

        let ids = diagnosticIds (runVerify root)
        Assert.Contains("verify.unobservedRequiredTest", ids)
        Assert.DoesNotContain("verify.unrecordedRequiredRecord", ids)

    // ===== AC-009 / FR-009: a package with no receipts behaves exactly as before =====

    [<Fact>]
    let ``a package that never mentions the record channel keeps its previous verdict`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        TestSupport.runEvidence root workId title |> ignore
        TestSupport.writePassingTaskEvidenceFor root workId

        let verified = runVerify root
        let ids = diagnosticIds verified

        // The pre-#865 behavior, unchanged: an unobserved test pass blocks with the diagnostic it always
        // blocked with, and no record diagnostic appears anywhere.
        Assert.Contains("verify.unobservedRequiredTest", ids)
        Assert.DoesNotContain("verify.unrecordedRequiredRecord", ids)
        Assert.DoesNotContain("evidence.recordReceiptInvalid", ids)
        Assert.DoesNotContain("evidence.recordReceiptStale", ids)

        Assert.DoesNotContain("recordReceipt", TestSupport.readRelative root evidencePath)

    [<Fact>]
    let ``a verify view written before the record channel parses with recordRequirement false`` () =
        // AC-009's other half, over the persisted contract rather than the command: the field is
        // additiveOptional, and a pre-#865 view must read as what it meant, not throw.
        let legacy =
            """{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "011-evidence-command",
  "stage": "verify",
  "status": "verificationReady",
  "readiness": "verificationReady",
  "evidenceDispositions": [
    { "id": "ED-EV001", "obligationId": "EV001", "state": "supported", "observed": true,
      "severity": "ready", "correction": "" }
  ],
  "testDispositions": [
    { "id": "TD-EV001", "obligationId": "EV001", "state": "satisfied", "observed": true,
      "severity": "ready", "correction": "" }
  ]
}"""

        match
            Verify.parseVerificationView
                { Path = verifyPath
                  Text = legacy
                  RawBytes = None }
        with
        | Error diagnostics -> failwith $"a pre-#865 verify.json must still parse: {diagnostics}"
        | Ok parsed ->
            Assert.All(parsed.EvidenceDispositions, fun d -> Assert.False d.RecordRequirement)
            Assert.All(parsed.TestDispositions, fun d -> Assert.False d.RecordRequirement)
            Assert.All(parsed.EvidenceDispositions, fun d -> Assert.True d.Observed)
