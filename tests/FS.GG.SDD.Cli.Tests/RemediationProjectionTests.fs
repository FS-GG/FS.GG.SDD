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
    let private interactiveColor =
        { IsInteractive = true
          ColorEnabled = true
          Width = Some 100
          IsInputInteractive = true }

    let private nonInteractive =
        { interactiveColor with
            IsInteractive = false }

    let private step (id: ReconciliationStepId) (outcome: ReconciliationOutcome) targets : ReconciliationStep =
        { StepId = id
          Kind = id
          DiffPreview = $"{reconciliationStepIdValue id} preview"
          Outcome = outcome
          TargetPaths = targets }

    let private doctorSummary: DoctorSummary =
        { HasProvenance = true
          ProviderName = Some "rendering"
          InstalledCliVersion = "0.2.1"
          RequiredMinimumCliVersion = Some "9.9.9"
          RequiredMinimumCliVersionSource = Some "workspaceFloor"
          CliAxis = "behind"
          CliBehindBy = Some "0.2.1 -> 9.9.9"
          ExpectedArtifactCount = 31
          MissingArtifactPaths = [ ".claude/skills/fs-gg-sdd-plan/SKILL.md" ]
          SkillDriftPaths = []
          PreviewSteps =
            [ step ReconciliationStepId.CliSelfUpdate ReconciliationOutcome.WouldApply []
              step ReconciliationStepId.TemplateRePin ReconciliationOutcome.NoTarget []
              step
                  ReconciliationStepId.ArtifactReSeed
                  ReconciliationOutcome.WouldApply
                  [ ".claude/skills/fs-gg-sdd-plan/SKILL.md" ] ]
          IsCoherent = false }

    let private upgradeSummary: UpgradeSummary =
        { HasProvenance = true
          Mode = "assumeYes"
          AlreadyCoherent = false
          Steps =
            [ step
                  ReconciliationStepId.ArtifactReSeed
                  ReconciliationOutcome.Applied
                  [ ".claude/skills/fs-gg-sdd-plan/SKILL.md" ] ]
          AppliedStepIds = [ ReconciliationStepId.ArtifactReSeed ]
          SkippedStepIds = []
          FailedStepIds = []
          SkillDriftPaths = []
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
            // FS-GG/FS.GG.SDD#313: the effective minimum is meaningless without the floor that
            // produced it, so the source travels with it into every projection.
            Assert.Contains("workspaceFloor", projection)

        Assert.Contains("\"cliAxis\": \"behind\"", json)
        Assert.Contains("doctorCliAxis: behind", text)
        Assert.Contains("\"requiredMinimumCliVersionSource\": \"workspaceFloor\"", json)
        Assert.Contains("doctorRequiredMinimumCliSource: workspaceFloor", text)

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

    // Feature 063: skillDriftPaths — the primary 058 content-drift surface — must be visible in
    // the text and rich projections (it was serialized to JSON but never rendered). JSON unchanged.
    let private driftedDoctorReport: CommandReport =
        { doctorReport with
            Doctor =
                Some
                    { doctorSummary with
                        SkillDriftPaths =
                            [ ".codex/skills/fs-gg-sdd-plan/SKILL.md"
                              ".claude/skills/fs-gg-sdd-ship/SKILL.md" ] } }

    let private driftedUpgradeReport: CommandReport =
        { upgradeReport with
            Upgrade =
                Some
                    { upgradeSummary with
                        SkillDriftPaths = [ ".agents/skills/fs-gg-sdd-verify/SKILL.md" ] } }

    [<Fact>]
    let ``skillDriftPaths appear in doctor text and rich, and JSON stays byte-identical`` () =
        // Text: full fact lines. Rich: the rich renderer splits `key: value` into table cells, so
        // assert distinctive value fragments (fs-gg-sdd-ship is unique to the drift set) instead.
        let text = (resolve Text nonInteractive driftedDoctorReport).Text
        Assert.Contains("doctorSkillDrifts: 2", text)
        Assert.Contains("doctorSkillDrift: .codex/skills/fs-gg-sdd-plan/SKILL.md", text)
        Assert.Contains("doctorSkillDrift: .claude/skills/fs-gg-sdd-ship/SKILL.md", text)
        let rich = (resolve Rich interactiveColor driftedDoctorReport).Text
        Assert.Contains("doctorSkillDrifts", rich)
        Assert.Contains("fs-gg-sdd-ship", rich)
        // JSON already carried skillDriftPaths; the render fix is projection-only.
        Assert.Contains("\"skillDriftPaths\"", (resolve Json interactiveColor driftedDoctorReport).Text)
        let before = serializeReport driftedDoctorReport
        resolve Rich interactiveColor driftedDoctorReport |> ignore
        Assert.Equal(before, serializeReport driftedDoctorReport)

    [<Fact>]
    let ``skillDriftPaths appear in upgrade text and rich`` () =
        let text = (resolve Text nonInteractive driftedUpgradeReport).Text
        Assert.Contains("upgradeSkillDrifts: 1", text)
        Assert.Contains("upgradeSkillDrift: .agents/skills/fs-gg-sdd-verify/SKILL.md", text)
        let rich = (resolve Rich interactiveColor driftedUpgradeReport).Text
        Assert.Contains("upgradeSkillDrifts", rich)
        Assert.Contains("fs-gg-sdd-verify", rich)

    [<Fact>]
    let ``empty skillDriftPaths emits only the count line`` () =
        // Parity with missingArtifacts: the count is always emitted, per-path lines only when non-empty.
        let text = (resolve Text nonInteractive doctorReport).Text
        Assert.Contains("doctorSkillDrifts: 0", text)
        Assert.DoesNotContain("doctorSkillDrift:", text)
