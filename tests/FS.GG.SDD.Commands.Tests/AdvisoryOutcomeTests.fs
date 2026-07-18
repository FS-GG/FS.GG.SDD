namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.TestShared
open Xunit

/// FS.GG.SDD#546: an "advisory-only" clean run must NOT read as `succeededWithWarnings`.
///
/// The retrospective that filed #546 saw `outcome: succeededWithWarnings` on a fully-ready
/// evidence run and attributed it to the standing `planAdvisory` — the "Optional Governance
/// pointers remain compatibility facts only." note the plan scaffold carries. That attribution
/// does not hold: `planAdvisory: N` is the READINESS COUNT of the plan's advisory notes, not a
/// diagnostic, so it never reaches `ReportAssembly.outcome` and never inflates the outcome.
///
/// `ReportAssembly.outcome` already reserves `SucceededWithWarnings` for a `DiagnosticWarning`
/// and leaves an advisory `DiagnosticInfo` at `Succeeded`/`NoChange` — the same design the
/// `planAuthoringWindow` DiagnosticInfo documents ("A DiagnosticInfo, so `ReportAssembly.outcome`
/// ... leaves the outcome ... untouched. It adds a fact, not an outcome."). Nothing pinned that
/// contract, though: every evidence outcome assertion in the suite only checks `<> Blocked`, so a
/// future change that promoted an advisory to a warning — the exact regression #546 fears — would
/// pass unnoticed. These tests pin it.
[<Collection("ProcessGlobalEnv")>]
module AdvisoryOutcomeTests =

    let private diag severity id =
        create id severity None None (sprintf "%s message" id) "correction" []

    let private change op : ArtifactChange =
        { Path = "work/x/artifact.md"
          Kind = "authoredArtifact"
          Ownership = "authored"
          Operation = op
          BeforeDigest = None
          AfterDigest = None
          SafeWriteDecision = "write"
          DiagnosticIds = [] }

    [<Fact>]
    let ``an advisory info diagnostic alone is a clean no-op, never succeededWithWarnings`` () =
        // An advisory carries a fact, not a fix. With nothing else recorded the run is a clean
        // no-op, not a warning to chase.
        Assert.Equal(CommandOutcome.NoChange, outcome [ diag DiagnosticInfo "planAuthoringWindow" ] [])

    [<Fact>]
    let ``an advisory info diagnostic beside a real change yields succeeded, not warnings`` () =
        Assert.Equal(
            CommandOutcome.Succeeded,
            outcome [ diag DiagnosticInfo "planAuthoringWindow" ] [ change ArtifactOperation.Create ]
        )

    [<Fact>]
    let ``a real warning still wins over an advisory`` () =
        // The distinction cuts one way only: an advisory never *inflates* the outcome, but a
        // genuine `DiagnosticWarning` beside one still reports `SucceededWithWarnings`.
        Assert.Equal(
            CommandOutcome.SucceededWithWarnings,
            outcome
                [ diag DiagnosticInfo "planAuthoringWindow"
                  diag DiagnosticWarning "proseStructuredMismatch" ]
                []
        )

    [<Fact>]
    let ``a blocking error still wins over an advisory`` () =
        Assert.Equal(
            CommandOutcome.Blocked,
            outcome
                [ diag DiagnosticInfo "planAuthoringWindow"
                  diag DiagnosticError "missingRequiredEvidence" ]
                []
        )

    /// The integration guard behind the unit rules above: a fully-ready evidence run that CARRIES
    /// the standing plan governance advisory (`planAdvisory >= 1`) reports `Succeeded` with no
    /// diagnostics — the exact run #546 claims reports `succeededWithWarnings`. It does not.
    [<Fact>]
    let ``a fully-ready evidence run carrying the standing plan advisory reports succeeded`` () =
        let root = TestSupport.tempDirectory ()
        let workId = "011-evidence-command"
        let title = "Evidence Command"
        TestSupport.initializeAnalyzedProject root workId title
        let report = TestSupport.runEvidence root workId title

        // The advisory the retrospective pointed at is present on this run...
        let advisoryCount =
            report.Plan
            |> Option.map (fun plan -> plan.AdvisoryCount)
            |> Option.defaultValue 0

        Assert.True(advisoryCount >= 1, "expected the standing plan governance advisory to be present")

        // ...yet the run is clean, so the outcome is `Succeeded`, never `SucceededWithWarnings`.
        Assert.Empty(report.Diagnostics)
        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
