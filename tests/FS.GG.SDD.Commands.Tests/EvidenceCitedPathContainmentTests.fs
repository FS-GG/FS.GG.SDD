namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// FS.GG.SDD#359 and FS.GG.SDD#365 — the two halves of the cited-path containment rule, pinned at
/// the COMMAND boundary, which is where both defects were actually visible to an author.
///
/// #359: a `..` in `artifacts:` raised out of the pure `update`. The CLI caught the escaped
/// `ArgumentException` and rendered a *tool defect* — `command: init`, `nextAction:
/// reportToolDefect`, a pristine `charter=next …` stage footer on a workspace eight stages in, and
/// the advice "This is a tool defect, not a problem with your input. … report it to FS.GG.SDD."
/// The author wrote a bad path and was told to file a bug against the tool. That inverts
/// Constitution VIII ("failures must distinguish malformed user input from tool defects").
///
/// #365: `sourceRefs[].path` never reached the containment rule at all — it is a raw scalar — so a
/// `..` chain was resolved *out of the workspace* by the #349 existence probe. A file outside the
/// repository that happened to exist (`/etc/passwd`) therefore DISCHARGED the cited-artifact gate.
///
/// Both legs must stay red-if-broken, per #266: a fix whose failure leg is untested is how this
/// class survives.
module EvidenceCitedPathContainmentTests =

    let private workId = "011-evidence-command"
    let private title = "Evidence command"
    let private evidencePath = $"work/{workId}/evidence.yml"

    let private escaping = "../../../../../../../../etc/passwd"

    let private declaration (citedField: string) (path: string) =
        let cited =
            if citedField = "artifacts" then
                $"    artifacts: [{path}]"
            else
                $"    sourceRefs:\n      - kind: verification\n        path: {path}"

        "schemaVersion: 1\n"
        + "evidence:\n"
        + "  - id: EV001\n"
        + "    kind: verification\n"
        + "    subject:\n"
        + "      type: task\n"
        + "      id: T001\n"
        + "    result: pass\n"
        + "    synthetic: false\n"
        + cited
        + "\n"

    let private runWithEvidence citedField path =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeAnalyzedProject root workId title
        TestSupport.writeRelative root evidencePath (declaration citedField path)
        TestSupport.runEvidence root workId title

    let private malformedPathDiagnostics (report: CommandReport) =
        report.Diagnostics
        |> List.filter (fun d -> d.Id = "evidence.malformedArtifactPath")

    [<Theory>]
    [<InlineData("artifacts")>]
    [<InlineData("sourceRefs")>]
    let ``an escaping cited path blocks as USER INPUT, not as a tool defect`` (citedField: string) =
        // Reaching this line at all is half the assertion: before #359 this THREW out of `update`.
        let report = runWithEvidence citedField escaping

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)

        let malformed = malformedPathDiagnostics report
        Assert.NotEmpty malformed

        Assert.All(
            malformed,
            fun d ->
                // The defect, stated as an assertion: this is the author's path, not our bug.
                Assert.False d.IsToolDefect
                // And they are told WHICH path — the one fact the tool-defect message never gave.
                Assert.Contains(escaping, d.Message)
                Assert.Contains(escaping, d.RelatedIds)
        )

        // The stage is reported truthfully. The escaped exception used to be rendered against a
        // pristine `init` projection, telling an author eight stages in to go and run `init`.
        Assert.Equal(Evidence, report.Command)

    [<Fact>]
    let ``a file OUTSIDE the repository cannot discharge the cited-artifact gate`` () =
        // FS.GG.SDD#365, the sharp end. `/etc/passwd` exists on the test machine, so before the fix
        // the #349 existence probe resolved the `..` chain, found it, and reported no
        // `evidence.artifactNotFound` — a passing declaration "proved" by a file the repository does
        // not contain. The path must now be refused before it is ever probed.
        let report = runWithEvidence "sourceRefs" escaping

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.NotEmpty(malformedPathDiagnostics report)

    [<Fact>]
    let ``a contained cited path is unaffected`` () =
        // The green path still works: a legal repository-relative path is accepted by the parse and
        // handed to the existence probe, where #349's rule (correctly) reports it missing.
        let report = runWithEvidence "artifacts" "evidence/frame-ceiling-bounce.png"

        Assert.Empty(malformedPathDiagnostics report)
