namespace FS.GG.SDD.Artifacts.Tests

open System
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Evidence
open Xunit

/// FS.GG.SDD#865, rule level: the record channel's coherence rule and its kind-directed discharge rule.
///
/// These are the cheap, exhaustive half of the acceptance evidence. Every branch of
/// `recordReceiptInconsistency` gets its own case here, so the command-level fixtures in
/// `RecordDischargedObligationTests` can stay about what `verify` and `ship` DO with a malformed
/// receipt rather than about how many ways a receipt can be malformed.
module RecordReceiptTests =

    let private sha (c: string) = "sha256:" + String.replicate 64 c

    /// The reference coherent receipt every negative case below mutates ONE field of. Written once so a
    /// case cannot pass by accident — if the base is not coherent, the first assertion fails and every
    /// derived case becomes vacuous, which is the failure mode a per-case literal invites.
    let private decisionReceipt =
        { Kind = "decision"
          Locator = "docs/decisions/adr-0035.md"
          LocatorContract = "durable-locator-v1"
          Digest = sha "a"
          Statement = "ADR-0035 records that SDD never runs a test."
          RecordedAt = "2026-08-15T09:49:00Z" }

    let private issueReceipt =
        { decisionReceipt with
            Kind = "issue"
            Locator = "https://example.invalid/rows/865"
            Digest = "" }

    let private commitReceipt =
        { decisionReceipt with
            Kind = "commit"
            Locator = String.replicate 40 "a"
            Digest = "" }

    let private declarationWith receipt =
        { EvidenceCodec.declarationSeed with
            Result = "pass"
            Synthetic = false
            RecordReceipt = receipt }

    // ===== coherence: the three kinds that ARE coherent =====

    [<Fact>]
    let ``all three record kinds are coherent in their canonical form`` () =
        for receipt in [ decisionReceipt; issueReceipt; commitReceipt ] do
            Assert.Null(Option.toObj (recordReceiptInconsistency receipt))

    // ===== coherence: one case per refusal branch =====
    //
    // Each mutates exactly one field of a receipt proven coherent above, so a green case attributes the
    // refusal to that field and nothing else.

    [<Fact>]
    let ``an unrecognized kind is refused`` () =
        let reason = recordReceiptInconsistency { decisionReceipt with Kind = "tweet" }

        Assert.True(Option.isSome reason)
        Assert.Contains("kind", Option.get reason)

    [<Fact>]
    let ``a legacy or absent locatorContract is refused rather than reinterpreted`` () =
        for contract in [ ""; "durable-locator-v0"; "exact-bytes-v1" ] do
            let reason =
                recordReceiptInconsistency
                    { decisionReceipt with
                        LocatorContract = contract }

            Assert.True(Option.isSome reason, $"locatorContract '{contract}' must be refused")
            Assert.Contains("locatorContract", Option.get reason)

    [<Fact>]
    let ``each kind rejects a locator in another kind's form`` () =
        // The point of the closed set: a `decision` locator that is a URL is not a decision record in
        // this repository, and a `commit` locator that is a path is not an object name. Checking only
        // for non-emptiness would make `locator` a free-text field wearing a schema.
        let wrong =
            [ { decisionReceipt with
                  Locator = "https://example.invalid/rows/865" }
              { decisionReceipt with
                  Locator = "../outside/adr.md" }
              { issueReceipt with
                  Locator = "docs/decisions/adr-0035.md" }
              { issueReceipt with
                  Locator = "http://example.invalid/rows/865" }
              { commitReceipt with
                  Locator = "docs/decisions/adr-0035.md" }
              { commitReceipt with
                  Locator = String.replicate 39 "a" } ]

        for receipt in wrong do
            Assert.True(
                Option.isSome (recordReceiptInconsistency receipt),
                $"{receipt.Kind} locator '{receipt.Locator}' must be refused"
            )

    [<Fact>]
    let ``a decision receipt without a well-formed byte digest is refused`` () =
        // The digest is what makes a repository-local record STRONGER evidence than a remote one, so a
        // decision receipt that omits it has given up the only property SDD can check itself.
        for digest in
            [ ""
              "deadbeef"
              "sha1:" + String.replicate 40 "a"
              "sha256:" + String.replicate 63 "a" ] do
            let reason = recordReceiptInconsistency { decisionReceipt with Digest = digest }

            Assert.True(Option.isSome reason, $"decision digest '{digest}' must be refused")
            Assert.Contains("digest", Option.get reason)

    [<Fact>]
    let ``an issue or commit receipt carrying a digest is refused`` () =
        // The opposite direction, and it is not pedantry: there are no local bytes behind an `issue` or
        // `commit` locator, so a digest offered for one is a number nothing can ever check. Accepting it
        // would let a receipt LOOK byte-bound while binding nothing.
        for receipt in [ issueReceipt; commitReceipt ] do
            let reason = recordReceiptInconsistency { receipt with Digest = sha "b" }

            Assert.True(Option.isSome reason, $"a {receipt.Kind} receipt must not carry a digest")
            Assert.Contains("digest", Option.get reason)

    [<Fact>]
    let ``a blank statement is refused`` () =
        // Without a statement the receipt says only "a record exists", leaving a reader who opens the
        // locator nothing to check the record against. The statement is what makes it refutable.
        for statement in [ ""; "   " ] do
            let reason =
                recordReceiptInconsistency
                    { decisionReceipt with
                        Statement = statement }

            Assert.True(Option.isSome reason)
            Assert.Contains("statement", Option.get reason)

    [<Fact>]
    let ``a missing or unparseable recordedAt is refused`` () =
        for recorded in [ ""; "   "; "last tuesday"; "2026-13-45T99:99:99Z" ] do
            let reason =
                recordReceiptInconsistency
                    { decisionReceipt with
                        RecordedAt = recorded }

            Assert.True(Option.isSome reason, $"recordedAt '{recorded}' must be refused")
            Assert.Contains("recordedAt", Option.get reason)

    // ===== isRecorded / obligationIsRecorded =====

    [<Fact>]
    let ``a declaration is recorded only when it carries a coherent receipt`` () =
        Assert.True(isRecorded (declarationWith (Some decisionReceipt)))
        Assert.False(isRecorded (declarationWith None))
        Assert.False(isRecorded (declarationWith (Some { decisionReceipt with Kind = "tweet" })))

    [<Fact>]
    let ``obligationIsRecorded is forall over the real passes, so one receipt cannot launder a bare pass`` () =
        // The anti-laundering reading inherited from `obligationIsObserved`, asserted here rather than
        // assumed: an obligation backed by one recorded pass AND one hand-asserted pass is NOT recorded.
        let recorded = declarationWith (Some decisionReceipt)
        let bare = declarationWith None

        Assert.True(obligationIsRecorded [ recorded ])
        Assert.False(obligationIsRecorded [ recorded; bare ])
        Assert.False(obligationIsRecorded [])

    [<Fact>]
    let ``a deferral alongside a recorded pass does not un-record the obligation`` () =
        // Only declarations claiming a REAL pass are consulted — a `supported` obligation may carry a
        // deferral beside the pass that supports it, and folding it in would report every mixed
        // obligation as unrecorded regardless of what was recorded.
        let deferral =
            { EvidenceCodec.declarationSeed with
                Kind = EvidenceKind.Deferral
                Result = "deferred" }

        Assert.True(obligationIsRecorded [ declarationWith (Some decisionReceipt); deferral ])

    // ===== the kind-directed discharge rule (DEC-002) =====

    [<Fact>]
    let ``a record receipt never discharges a test-class obligation`` () =
        Assert.False(obligationDischarged testDischargeClass [ declarationWith (Some decisionReceipt) ])

    [<Fact>]
    let ``an observed run never discharges a record-class obligation`` () =
        let observed =
            { declarationWith None with
                ObservedRun =
                    Some
                        { Source = "artifacts/test-results.trx"
                          Digest = sha "c"
                          DigestContract = "exact-bytes-v1"
                          CandidateCommit = String.replicate 40 "c"
                          Outcome = "passed"
                          Passed = 12
                          Failed = 0
                          Skipped = 0 } }

        // The run really is observed — so this asserts the DISPATCH, not an incidental failure of the
        // observed-run rule.
        Assert.True(isObserved observed)
        Assert.True(obligationDischarged testDischargeClass [ observed ])
        Assert.False(obligationDischarged recordDischargeClass [ observed ])

    [<Fact>]
    let ``an unrecognized discharge class reads as test-class, which is the fail-closed direction`` () =
        // Fail-closed because test-class carries the stricter, longer-standing requirement: a record
        // receipt will not satisfy it. A garbled class must never become a way to relax the gate.
        Assert.False(isRecordDischargeClass "")
        Assert.False(isRecordDischargeClass "recordish")
        Assert.False(obligationDischarged "recordish" [ declarationWith (Some decisionReceipt) ])

    [<Fact>]
    let ``the discharge class comes from the record-discharge capability tag`` () =
        Assert.Equal(recordDischargeClass, dischargeClassFromTags [ "fsharp"; recordDischargeCapability ])
        Assert.Equal(testDischargeClass, dischargeClassFromTags [ "fsharp"; "implementation" ])
        Assert.Equal(testDischargeClass, dischargeClassFromTags [])

    // ===== the decision locator is a cited path (FR-004) =====

    [<Fact>]
    let ``a decision locator joins the cited artifact paths and issue and commit locators do not`` () =
        // This is what makes a deleted record `invalid` through the existing #349 cascade with no new
        // gate — and what keeps a URI and an object name, which are not local files, from ever being
        // probed.
        Assert.Contains("docs/decisions/adr-0035.md", citedArtifactPaths (declarationWith (Some decisionReceipt)))

        Assert.Empty(citedArtifactPaths (declarationWith (Some issueReceipt)))
        Assert.Empty(citedArtifactPaths (declarationWith (Some commitReceipt)))
