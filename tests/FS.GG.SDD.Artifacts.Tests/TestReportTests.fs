namespace FS.GG.SDD.Artifacts.Tests

open System.Text
open FS.GG.SDD.Artifacts
open Xunit

/// FS.GG.SDD#350 / ADR-0035. The receipt's parse, and the rules that decide whether a receipt may
/// discharge an obligation.
///
/// These are the tests that make `isObserved` mean something. Before #350 it was the constant
/// `false`; the whole point of this feature is that it can now say `true` — so the truth table below
/// is the load-bearing assertion of the change, not a formality.
module TestReportTests =

    let private orFail label =
        function
        | Ok value -> value
        | Error(error: string) -> failwithf "%s: %s" label error

    // ---- TRX ----

    let private trx passed failed error notExecuted =
        $"""<?xml version="1.0" encoding="UTF-8"?>
<TestRun id="a" name="run" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary outcome="Completed">
    <Counters total="10" executed="10" passed="{passed}" failed="{failed}" error="{error}" notExecuted="{notExecuted}" />
  </ResultSummary>
</TestRun>"""

    [<Fact>]
    let ``TRX: a passing run parses to a passing receipt`` () =
        let run = TestReport.parse "artifacts/results.trx" (trx 1630 0 0 4) |> orFail "trx"

        Assert.Equal("artifacts/results.trx", run.Source)
        Assert.Equal("passed", run.Outcome)
        Assert.Equal(1630, run.Passed)
        Assert.Equal(0, run.Failed)
        Assert.Equal(4, run.Skipped)
        Assert.StartsWith("sha256:", run.Digest)

    [<Fact>]
    let ``TRX: outcome is DERIVED from the counts, not copied from the report`` () =
        // The TRX above says `outcome="Completed"` even when tests failed — which is why the receipt
        // must never copy it. This is the assertion that pins FR-005: a recorded receipt cannot be
        // self-inconsistent, because the field that could contradict the counts is computed from them.
        let run = TestReport.parse "artifacts/results.trx" (trx 8 2 0 0) |> orFail "trx"

        Assert.Equal("failed", run.Outcome)
        Assert.Equal(2, run.Failed)

    [<Fact>]
    let ``TRX: an errored test counts as failed, not as neither`` () =
        // A test that errored did not pass. Folding `error` into `failed` is what stops a receipt from
        // reporting a green run with a test silently missing from both columns.
        let run = TestReport.parse "artifacts/results.trx" (trx 8 0 2 0) |> orFail "trx"

        Assert.Equal("failed", run.Outcome)
        Assert.Equal(2, run.Failed)

    // ---- JUnit ----

    [<Fact>]
    let ``JUnit: a testsuites root sums its direct testsuite children`` () =
        // The aggregate attributes on <testsuites> are OPTIONAL and several emitters omit them. Reading
        // them instead of summing would yield a 0/0/0 receipt for a real run — a green receipt for a
        // suite nobody can prove ran. Note there are no aggregates on the root here, deliberately.
        let junit =
            """<?xml version="1.0"?>
<testsuites>
  <testsuite name="a" tests="10" failures="1" errors="0" skipped="2" />
  <testsuite name="b" tests="5" failures="0" errors="1" skipped="0" />
</testsuites>"""

        let run = TestReport.parse "artifacts/junit.xml" junit |> orFail "junit"

        Assert.Equal("failed", run.Outcome)
        Assert.Equal(2, run.Failed) // 1 failure + 1 error
        Assert.Equal(2, run.Skipped)
        Assert.Equal(11, run.Passed) // (10+5) - 2 failed - 2 skipped

    [<Fact>]
    let ``JUnit: a bare testsuite root parses`` () =
        let junit =
            """<testsuite name="a" tests="4" failures="0" errors="0" skipped="1" />"""

        let run = TestReport.parse "artifacts/junit.xml" junit |> orFail "junit"

        Assert.Equal("passed", run.Outcome)
        Assert.Equal(3, run.Passed)
        Assert.Equal(1, run.Skipped)

    [<Fact>]
    let ``JUnit: absent optional counts read as zero, not as an error`` () =
        // pytest/jest routinely omit `skipped`/`errors` when there are none.
        let run =
            TestReport.parse "artifacts/junit.xml" """<testsuite tests="3" failures="0" />"""
            |> orFail "junit"

        Assert.Equal("passed", run.Outcome)
        Assert.Equal(3, run.Passed)
        Assert.Equal(0, run.Skipped)

    [<Fact>]
    let ``JUnit: testcase-only output records executed tests`` () =
        // Node's built-in JUnit reporter omits aggregate attributes and writes testcase entries
        // directly below testsuites. It is observed runner output, not a synthetic summary.
        let junit =
            """<testsuites>
  <testcase name="server call" />
  <testcase name="browser render"><skipped /></testcase>
  <testcase name="client failure"><failure /></testcase>
</testsuites>"""

        let run = TestReport.parse "artifacts/node.junit.xml" junit |> orFail "node junit"

        Assert.Equal("failed", run.Outcome)
        Assert.Equal(1, run.Passed)
        Assert.Equal(1, run.Failed)
        Assert.Equal(1, run.Skipped)

    // ---- The failure legs: a report that cannot be believed is REFUSED, never recorded ----

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("   ")>]
    [<InlineData("this is not xml at all")>]
    [<InlineData("<unclosed>")>]
    [<InlineData("<somethingElse><nested/></somethingElse>")>]
    let ``an unbelievable report is an Error, never a silently-empty receipt`` (text: string) =
        // The whole class matters more than any member of it: a parse that DEGRADED to a 0/0/0 receipt
        // would fail open in a brand-new place, and "no receipt" is indistinguishable from the honest
        // state of an obligation that never asked for one.
        match TestReport.parse "artifacts/report.xml" text with
        | Ok run -> failwithf "expected refusal, recorded a receipt: %A" run
        | Error _ -> ()

    [<Fact>]
    let ``parse is total: a hostile document returns Error rather than raising`` () =
        // Artifacts is pure; an escaped exception from here would surface to the author as a tool
        // defect rather than as their malformed input.
        let hostile =
            """<!DOCTYPE foo [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><testsuite>&xxe;</testsuite>"""

        match TestReport.parse "artifacts/report.xml" hostile with
        | Ok _ -> () // parsed without resolving the entity is acceptable
        | Error _ -> () // refused is acceptable; RAISING is not, and reaching here proves it did not

    // ---- The digest ----

    [<Fact>]
    let ``the digest is stable across CRLF and LF`` () =
        // A receipt whose digest flipped between Windows and Linux CI would be useless to exactly the
        // audience it is for. `SchemaVersion.sha256Text` normalises, and this pins that we use it.
        let lf = "<testsuite tests=\"1\" failures=\"0\" />\n<!-- x -->\n"
        let crlf = lf.Replace("\n", "\r\n")

        let a = TestReport.parse "r.xml" lf |> orFail "lf"
        let b = TestReport.parse "r.xml" crlf |> orFail "crlf"

        Assert.Equal(a.Digest, b.Digest)

    [<Fact>]
    let ``the digest changes when the report changes`` () =
        let a =
            TestReport.parse "r.xml" """<testsuite tests="1" failures="0" />"""
            |> orFail "a"

        let b =
            TestReport.parse "r.xml" """<testsuite tests="2" failures="0" />"""
            |> orFail "b"

        Assert.NotEqual<string>(a.Digest, b.Digest)

    [<Fact>]
    let ``parseBytes attests BOM plus CRLF bytes exactly`` () =
        let text = trx 1 0 0 0 |> fun value -> value.Replace("\n", "\r\n")
        let bytes = Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] (Encoding.UTF8.GetBytes text)

        let run =
            TestReport.parseBytes "artifacts/results.trx" bytes text |> orFail "bom-crlf"

        Assert.Equal("exact-bytes-v1", run.DigestContract)
        Assert.Equal($"sha256:{(SchemaVersion.sha256Bytes bytes).Value}", run.Digest)
        Assert.NotEqual($"sha256:{(SchemaVersion.sha256Text text).Value}", run.Digest)

    [<Fact>]
    let ``exact byte digests are binary-safe and one byte changes the digest`` () =
        let original = [| 0x00uy; 0xFFuy; 0x0Duy; 0x0Auy; 0x80uy |]
        let mutated = Array.copy original
        mutated[2] <- 0x0Auy

        Assert.NotEqual((SchemaVersion.sha256Bytes original).Value, (SchemaVersion.sha256Bytes mutated).Value)

    // ---- isObserved: the seam #398 built, now load-bearing ----

    let private declarationWith result synthetic receipt =
        { EvidenceCodec.declarationSeed with
            Result = result
            Synthetic = synthetic
            ObservedRun = receipt }

    let private passingReceipt =
        Some
            { Source = "artifacts/results.trx"
              Digest = "sha256:" + String.replicate 64 "a"
              DigestContract = "exact-bytes-v1"
              CandidateCommit = String.replicate 40 "a"
              Outcome = "passed"
              Passed = 3
              Failed = 0
              Skipped = 0 }

    let private failingReceipt =
        Some
            { Source = "artifacts/results.trx"
              Digest = "sha256:" + String.replicate 64 "a"
              DigestContract = "exact-bytes-v1"
              CandidateCommit = String.replicate 40 "a"
              Outcome = "failed"
              Passed = 2
              Failed = 1
              Skipped = 0 }

    [<Fact>]
    let ``isObserved is true ONLY for a passing receipt`` () =
        Assert.True(isObserved (declarationWith "pass" false passingReceipt))

        // No receipt — the honest state of every obligation before this feature, and of every one that
        // never records a run. This is what `selfAttested` counts.
        Assert.False(isObserved (declarationWith "pass" false None))

        // A receipt whose run FAILED does not discharge anything, whatever the author typed beside it.
        Assert.False(isObserved (declarationWith "pass" false failingReceipt))

    [<Fact>]
    let ``a receipt claiming passed while carrying failures never discharges an obligation`` () =
        // `TestReport.parse` cannot produce this — `outcome` is derived. A hand-authored evidence.yml
        // can. It is blocked as `observedRunInconsistent`; `isObserved` refuses it independently, so
        // the rule stays true when read on its own.
        let liar =
            Some
                { Source = "artifacts/results.trx"
                  Digest = "sha256:" + String.replicate 64 "a"
                  DigestContract = "exact-bytes-v1"
                  CandidateCommit = String.replicate 40 "a"
                  Outcome = "passed"
                  Passed = 2
                  Failed = 7
                  Skipped = 0 }

        Assert.False(isObserved (declarationWith "pass" false liar))

    [<Fact>]
    let ``one observed run cannot launder a hand-asserted pass sitting beside it`` () =
        // `obligationIsObserved` requires EVERY declaration claiming a real pass to be observed. An
        // obligation discharged by two declarations, one with a receipt and one without, is NOT
        // observed — otherwise a single genuine run would launder any number of typed passes.
        let observed = declarationWith "pass" false passingReceipt
        let asserted = declarationWith "pass" false None

        Assert.True(obligationIsObserved [ observed ])
        Assert.False(obligationIsObserved [ observed; asserted ])

        // And the invariant #398 rests on: a real pass is either observed or self-attested, never both
        // and never neither.
        for declaration in [ observed; asserted ] do
            Assert.NotEqual(isObserved declaration, isSelfAttested declaration)

    // ---- observedRunInconsistency: the receipt cannot become a new place to type `pass` ----

    [<Fact>]
    let ``a coherent receipt is accepted`` () =
        Assert.True((observedRunInconsistency passingReceipt.Value).IsNone)
        Assert.True((observedRunInconsistency failingReceipt.Value).IsNone)

    [<Theory>]
    [<InlineData("passed", 2, 7, 0, "sha256")>] // passed, yet failures
    [<InlineData("failed", 2, 0, 0, "sha256")>] // failed, yet no failures
    [<InlineData("green", 2, 0, 0, "sha256")>] // not a known outcome
    [<InlineData("passed", -1, 0, 0, "sha256")>] // negative count
    [<InlineData("passed", 2, 0, 0, "md5")>] // not a sha256 digest
    [<InlineData("passed", 2, 0, 0, "bare")>] // hex with no algorithm prefix
    let ``an incoherent receipt is refused`` (outcome: string) passed failed skipped (digestKind: string) =
        let digest =
            match digestKind with
            | "sha256" -> "sha256:" + String.replicate 64 "a"
            | "bare" -> String.replicate 64 "a"
            | other -> other + ":" + String.replicate 64 "a"

        let run =
            { Source = "artifacts/results.trx"
              Digest = digest
              DigestContract = "exact-bytes-v1"
              CandidateCommit = String.replicate 40 "a"
              Outcome = outcome
              Passed = passed
              Failed = failed
              Skipped = skipped }

        Assert.True(
            (observedRunInconsistency run).IsSome,
            $"expected refusal for outcome={outcome} failed={failed} digest={digestKind}"
        )

    [<Fact>]
    let ``a receipt naming no report is refused`` () =
        let run =
            { Source = "  "
              Digest = "sha256:" + String.replicate 64 "a"
              DigestContract = "exact-bytes-v1"
              CandidateCommit = String.replicate 40 "a"
              Outcome = "passed"
              Passed = 1
              Failed = 0
              Skipped = 0 }

        Assert.True((observedRunInconsistency run).IsSome)

    // ---- The receipt's report is itself compared against reality (FR-009) ----

    [<Fact>]
    let ``the receipt's report is a CITED path, so the #349 existence cascade probes it`` () =
        // This is what makes the receipt *checked* rather than merely recorded: a report deleted after
        // recording turns its obligation invalid at `verify`, the merge boundary — with no new gate.
        let declaration = declarationWith "pass" false passingReceipt

        Assert.Contains("artifacts/results.trx", citedArtifactPaths declaration)

        let absent = missingCitedArtifacts (fun _ -> false) declaration
        Assert.Contains("artifacts/results.trx", absent)

        let present = missingCitedArtifacts (fun _ -> true) declaration
        Assert.Empty(present)

    // ---- A run in which nothing executed is not evidence (the fail-open found in review) ----

    [<Theory>]
    // A TRX whose every test was filtered out — a `--filter` typo produces exactly this.
    [<InlineData("<TestRun><ResultSummary><Counters passed=\"0\" failed=\"0\" /></ResultSummary></TestRun>")>]
    // The same, with tests present but all SKIPPED. A skipped test is one nobody ran.
    [<InlineData("<TestRun><ResultSummary><Counters passed=\"0\" failed=\"0\" notExecuted=\"12\" /></ResultSummary></TestRun>")>]
    // A JUnit root advertising 10 tests whose children carry no counts — several emitters do this.
    [<InlineData("<testsuites tests=\"10\"><testsuite name=\"a\" /></testsuites>")>]
    [<InlineData("<testsuite tests=\"0\" failures=\"0\" />")>]
    let ``a report with no EXECUTED tests is refused, never recorded as a pass`` (text: string) =
        // Without this rule `failed = 0` derives `outcome: passed`, the receipt records `passed: 0`,
        // and `isObserved` returns TRUE — an obligation discharged by a run in which nothing ran.
        // That is the fail-open this whole feature exists to close, rebuilt one level down.
        match TestReport.parse "artifacts/report.trx" text with
        | Ok run -> failwithf "a run that executed nothing was recorded as a receipt: %A" run
        | Error reason -> Assert.Contains("no executed tests", reason)

    [<Fact>]
    let ``a run with at least one executed test is still recorded`` () =
        // The guard must not swallow a real run that happens to skip most of its suite.
        let run =
            TestReport.parse
                "r.trx"
                "<TestRun><ResultSummary><Counters passed=\"1\" failed=\"0\" notExecuted=\"99\" /></ResultSummary></TestRun>"
            |> orFail "trx"

        Assert.Equal("passed", run.Outcome)
        Assert.Equal(1, run.Passed)
        Assert.Equal(99, run.Skipped)

    // ---- The two rules over `outcome` must agree on what the field says ----

    [<Fact>]
    let ``isObserved and observedRunInconsistency normalize outcome the same way`` () =
        // A hand-authored `outcome: Passed`. The consistency rule trims and lowercases before judging,
        // so it reports the receipt as coherent. If `isObserved` compared it raw it would answer
        // `false` — no diagnostic, no explanation, and an obligation quietly demoted to `selfAttested`
        // despite carrying a receipt the tool had just told the author was fine.
        let shouty =
            Some
                { Source = "artifacts/results.trx"
                  Digest = "sha256:" + String.replicate 64 "a"
                  DigestContract = "exact-bytes-v1"
                  CandidateCommit = String.replicate 40 "a"
                  Outcome = "  Passed  "
                  Passed = 3
                  Failed = 0
                  Skipped = 0 }

        Assert.True((observedRunInconsistency shouty.Value).IsNone, "the consistency rule accepts it")
        Assert.True(isObserved (declarationWith "pass" false shouty), "so isObserved must too")

    [<Fact>]
    let ``an authored receipt claiming a run that executed nothing is refused`` () =
        // The authored twin of the parse guard. Left unblocked this is the cheapest possible forgery:
        // it needs no report at all, just six lines of YAML.
        let empty =
            { Source = "artifacts/results.trx"
              Digest = "sha256:" + String.replicate 64 "a"
              DigestContract = "exact-bytes-v1"
              CandidateCommit = String.replicate 40 "a"
              Outcome = "passed"
              Passed = 0
              Failed = 0
              Skipped = 40 }

        Assert.True((observedRunInconsistency empty).IsSome)
        Assert.False(isObserved (declarationWith "pass" false (Some empty)))
