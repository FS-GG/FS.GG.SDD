namespace FS.GG.SDD.Cli.Tests

open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Cli.Rendering
open Xunit

/// Feature 053 (US1-AC4 / SC-007, T017/T037): the `doctor` and `upgrade` reports are
/// fact-identical across `--json`, `--text`, and `--rich`; `--rich` redirected equals
/// `--text` with zero ANSI; the rich path changes no JSON byte. Constructed reports.
module RemediationProjectionTests =
    let private interactiveColor = { IsInteractive = true; ColorEnabled = true; Width = Some 100; IsInputInteractive = true }
    let private nonInteractive = { interactiveColor with IsInteractive = false }

    let private step id outcome targets : ReconciliationStep =
        { StepId = id; Kind = id; DiffPreview = $"{id} preview"; Outcome = outcome; TargetPaths = targets }

    let private doctorSummary: DoctorSummary =
        { HasProvenance = true
          ProviderName = Some "rendering"
          InstalledCliVersion = "0.2.1"
          RequiredMinimumCliVersion = Some "9.9.9"
          CliAxis = "behind"
          CliBehindBy = Some "0.2.1 -> 9.9.9"
          ExpectedArtifactCount = 31
          MissingArtifactPaths = [ ".claude/skills/fs-gg-sdd-plan/SKILL.md" ]
          PreviewSteps =
            [ step "cliSelfUpdate" "wouldApply" []
              step "templateRePin" "noTarget" []
              step "artifactReSeed" "wouldApply" [ ".claude/skills/fs-gg-sdd-plan/SKILL.md" ] ]
          IsCoherent = false }

    let private upgradeSummary: UpgradeSummary =
        { HasProvenance = true
          Mode = "assumeYes"
          AlreadyCoherent = false
          Steps = [ step "artifactReSeed" "applied" [ ".claude/skills/fs-gg-sdd-plan/SKILL.md" ] ]
          AppliedStepIds = [ "artifactReSeed" ]
          SkippedStepIds = []
          FailedStepIds = []
          ResidualDrift = false
          NextActionHint = "Reconciliation complete; run fsgg-sdd doctor to confirm coherence." }

    let private doctorReport: CommandReport =
        { RichRenderingTests.sampleReport with
            Command = Doctor
            Outcome = CommandOutcome.SucceededWithWarnings
            Specification = None
            Doctor = Some doctorSummary }

    let private upgradeReport: CommandReport =
        { RichRenderingTests.sampleReport with
            Command = Upgrade
            Outcome = CommandOutcome.Succeeded
            Specification = None
            Upgrade = Some upgradeSummary }

    [<Fact>]
    let ``doctor json equals serializeReport and the rich path changes no byte`` () =
        let before = serializeReport doctorReport
        resolve Rich interactiveColor doctorReport |> ignore
        Assert.Equal(before, serializeReport doctorReport)
        Assert.Equal(serializeReport doctorReport, (resolve Json interactiveColor doctorReport).Text)

    [<Fact>]
    let ``doctor facts appear in every projection and rich redirected is zero-ANSI text`` () =
        let json = (resolve Json interactiveColor doctorReport).Text
        let text = (resolve Text nonInteractive doctorReport).Text
        let rich = (resolve Rich interactiveColor doctorReport).Text

        for projection in [ json; text; rich ] do
            Assert.Contains("behind", projection)
            Assert.Contains("fs-gg-sdd-plan", projection)

        Assert.Contains("\"cliAxis\": \"behind\"", json)
        Assert.Contains("doctorCliAxis: behind", text)

        let redirected = resolve Rich nonInteractive doctorReport
        Assert.False redirected.UsedRichRendering
        Assert.Equal(renderText doctorReport, redirected.Text)

    [<Fact>]
    let ``upgrade facts are identical across json text and rich, rich changes no byte`` () =
        let before = serializeReport upgradeReport
        resolve Rich interactiveColor upgradeReport |> ignore
        Assert.Equal(before, serializeReport upgradeReport)

        let json = (resolve Json interactiveColor upgradeReport).Text
        let text = (resolve Text nonInteractive upgradeReport).Text
        let rich = (resolve Rich interactiveColor upgradeReport).Text

        Assert.Contains("\"mode\": \"assumeYes\"", json)
        Assert.Contains("upgradeMode: assumeYes", text)

        for projection in [ json; text; rich ] do
            Assert.Contains("assumeYes", projection)
            Assert.Contains("artifactReSeed", projection)

        let redirected = resolve Rich nonInteractive upgradeReport
        Assert.False redirected.UsedRichRendering
        Assert.Equal(renderText upgradeReport, redirected.Text)
        Assert.DoesNotContain("[", redirected.Text)
