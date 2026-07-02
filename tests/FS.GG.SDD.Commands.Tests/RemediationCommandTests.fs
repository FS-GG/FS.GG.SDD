namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.Internal
open Xunit

/// Pure `Drift` model unit tests (feature 053, T011 / drift-model contract). Constructed
/// inputs only — no I/O.
module DriftTests =
    open RemediationSupport

    let private drift minimum installed present =
        Drift.compute (Some(record minimum)) (Some(descriptor minimum)) installed (Set.ofList present) (skillBodiesFor present)

    [<Fact>]
    let ``CLI axis is behind with a delta when installed is below the declared minimum`` () =
        let report = drift (Some farAheadMinimum) installedVersion Drift.expectedArtifactPaths
        Assert.Equal("behind", report.CliAxis)
        Assert.True(report.CliBehindBy.IsSome)

    [<Fact>]
    let ``CLI axis is atOrAbove when installed meets the declared minimum`` () =
        let report = drift (Some farBehindMinimum) installedVersion Drift.expectedArtifactPaths
        Assert.Equal("atOrAbove", report.CliAxis)
        Assert.True(report.CliBehindBy.IsNone)

    [<Fact>]
    let ``CLI axis is coherentByAbsence when the provider declares no minimum`` () =
        let report = drift None installedVersion Drift.expectedArtifactPaths
        Assert.Equal("coherentByAbsence", report.CliAxis)

    [<Fact>]
    let ``CLI axis is undeterminable when the installed version is unparseable`` () =
        let report = drift (Some farAheadMinimum) "not-a-version" Drift.expectedArtifactPaths
        Assert.Equal("undeterminable", report.CliAxis)

    [<Fact>]
    let ``missing artifacts are the sorted expected-minus-present set`` () =
        let present = Drift.expectedArtifactPaths |> List.skip 2
        let report = drift (Some farBehindMinimum) installedVersion present
        Assert.Equal<string list>(Drift.expectedArtifactPaths |> List.take 2 |> List.sort, report.MissingArtifactPaths)

    [<Fact>]
    let ``a behind scaffold with missing artifacts previews self-update and re-seed as wouldApply`` () =
        let report = drift (Some farAheadMinimum) installedVersion []
        let outcomeOf id = report.Steps |> List.find (fun s -> s.StepId = id) |> fun s -> s.Outcome
        Assert.Equal("wouldApply", outcomeOf "cliSelfUpdate")
        Assert.Equal("wouldApply", outcomeOf "artifactReSeed")
        Assert.Equal("noTarget", outcomeOf "templateRePin")
        Assert.False report.IsCoherent

    [<Fact>]
    let ``an at-or-above scaffold with all artifacts present is coherent`` () =
        let report = drift (Some farBehindMinimum) installedVersion Drift.expectedArtifactPaths
        Assert.True report.IsCoherent

    [<Fact>]
    let ``no provenance yields HasProvenance false, no steps, coherent-degradation`` () =
        let report = Drift.compute None None installedVersion Set.empty Map.empty
        Assert.False report.HasProvenance
        Assert.Empty report.Steps
        Assert.True report.IsCoherent


/// `doctor` command tests (T015–T017). Real-filesystem fixtures; doctor is a read-only
/// projection that always exits 0.
module DoctorCommandTests =
    open RemediationSupport

    let private doctor (report: CommandReport) =
        match report.Doctor with
        | Some summary -> summary
        | None -> failwith "expected a doctor summary"

    [<Fact>]
    let ``behind scaffold names installed, minimum, behind-by, missing skills, and previews upgrade`` () =
        let root = behindMissingFixture ()
        let report = doctorReport root
        let summary = doctor report
        Assert.Equal("behind", summary.CliAxis)
        Assert.Equal(installedVersion, summary.InstalledCliVersion)
        Assert.Equal(Some farAheadMinimum, summary.RequiredMinimumCliVersion)
        Assert.True summary.CliBehindBy.IsSome
        Assert.NotEmpty summary.MissingArtifactPaths
        Assert.Contains(summary.PreviewSteps, fun s -> s.StepId = "artifactReSeed" && s.Outcome = "wouldApply")
        Assert.False summary.IsCoherent
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``coherent scaffold reports nothing to reconcile and exits 0`` () =
        let root = coherentFixture ()
        let report = doctorReport root
        Assert.True (doctor report).IsCoherent
        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``no declared minimum reports coherentByAbsence on the CLI axis`` () =
        let root = noMinimumFixture ()
        let report = doctorReport root
        Assert.Equal("coherentByAbsence", (doctor report).CliAxis)
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``no scaffold provenance degrades to HasProvenance false, exit 0`` () =
        let root = noProvenanceFixture ()
        let report = doctorReport root
        Assert.False (doctor report).HasProvenance
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``doctor makes zero writes — the working tree is byte-identical before and after`` () =
        let root = behindMissingFixture ()
        let before = treeHash root
        let report = doctorReport root
        Assert.Empty report.ChangedArtifacts
        Assert.Equal(before, treeHash root)

    [<Fact>]
    let ``doctor json is deterministic across runs`` () =
        let root = behindMissingFixture ()
        Assert.Equal(serializeReport (doctorReport root), serializeReport (doctorReport root))

    // 056 T024 (US3 / FR-010 / P9): a pre-056 product missing the third `.agents/skills/` root
    // is reported as drift read-only (zero writes, exit 0).
    [<Fact>]
    let ``doctor reports the missing third .agents root read-only`` () =
        let root = pre056Fixture ()
        let before = treeHash root
        let report = doctorReport root
        let summary = doctor report
        Assert.False summary.IsCoherent
        Assert.NotEmpty summary.MissingArtifactPaths
        Assert.Contains(summary.MissingArtifactPaths, fun (p: string) -> p.StartsWith ".agents/skills/")
        // Strictly read-only.
        Assert.Empty report.ChangedArtifacts
        Assert.Equal(before, treeHash root)
        Assert.Equal(0, exitCode report)


/// `upgrade` non-interactive command tests (`--yes`, refusal, no-op, step-defect, ownership).
module UpgradeCommandTests =
    open RemediationSupport

    let private upgrade (report: CommandReport) =
        match report.Upgrade with
        | Some summary -> summary
        | None -> failwith "expected an upgrade summary"

    [<Fact>]
    let ``--yes on an at-or-above behind scaffold re-seeds and reports coherent afterward`` () =
        let root = atOrAboveMissingFixture ()
        let report = upgradeYes root
        let summary = upgrade report
        Assert.Equal("assumeYes", summary.Mode)
        Assert.Contains("artifactReSeed", summary.AppliedStepIds)
        Assert.False summary.ResidualDrift
        Assert.Equal(0, exitCode report)
        Assert.True (match (doctorReport root).Doctor with Some d -> d.IsCoherent | None -> false)

    [<Fact>]
    let ``non-interactive without --yes refuses with zero writes, no hang, exit 1`` () =
        let root = atOrAboveMissingFixture ()
        let before = treeHash root
        let report = upgradeNonInteractive root
        Assert.Equal("refusedNonInteractive", (upgrade report).Mode)
        Assert.Contains("upgrade.nonInteractiveNoYes", diagnosticIds report)
        Assert.Equal(before, treeHash root)
        Assert.Equal(1, exitCode report)

    [<Fact>]
    let ``already-coherent scaffold is a clean no-op, exit 0`` () =
        let root = coherentFixture ()
        let before = treeHash root
        let report = upgradeYes root
        Assert.True (upgrade report).AlreadyCoherent
        Assert.Equal(before, treeHash root)
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``no scaffold provenance is a no-op with zero writes, exit 0`` () =
        let root = noProvenanceFixture ()
        let before = treeHash root
        let report = upgradeYes root
        Assert.False (upgrade report).HasProvenance
        Assert.Equal(before, treeHash root)
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``a confirmed step that fails to apply reports residual drift and exits 2`` () =
        let blocked = Drift.expectedArtifactPaths |> List.head
        let present = Drift.expectedArtifactPaths |> List.filter (fun p -> p <> blocked)
        let root = makeFixture (Some farBehindMinimum) present true
        // Block the one missing target by placing a directory where its file must be written:
        // the re-seed WriteFile then fails deterministically (real filesystem, no mocks).
        Directory.CreateDirectory(Path.Combine(root, blocked.Replace('/', Path.DirectorySeparatorChar))) |> ignore
        let report = upgradeYes root
        let summary = upgrade report
        Assert.Contains("artifactReSeed", summary.FailedStepIds)
        Assert.True summary.ResidualDrift
        Assert.Equal(2, exitCode report)

    // 056 T025 (US3 / SC-004 / P9): upgrade reconciles a pre-056 product by re-seeding the
    // missing third `.agents/skills/` root no-clobber to zero residual drift, preserving the
    // present (author-owned) copies in the other roots.
    [<Fact>]
    let ``upgrade re-seeds the missing third .agents root to zero residual drift`` () =
        let root = pre056Fixture ()
        // A present .claude copy (dummy author content) must be preserved (no-clobber).
        let presentClaude = ".claude/skills/" + List.head SeededSkills.skillNames + "/SKILL.md"
        let preservedBefore = TestSupport.readRelative root presentClaude

        let report = upgradeYes root
        let summary = upgrade report
        Assert.Contains("artifactReSeed", summary.AppliedStepIds)
        Assert.False summary.ResidualDrift
        Assert.Equal(0, exitCode report)

        // The third root is materialized for every seeded skill…
        for name in SeededSkills.skillNames do
            Assert.True(TestSupport.existsRelative root $".agents/skills/{name}/SKILL.md", $"expected .agents copy of {name}")
        // …the present copy is untouched…
        Assert.Equal(preservedBefore, TestSupport.readRelative root presentClaude)
        // …and doctor now reports coherence.
        Assert.True(match (doctorReport root).Doctor with Some d -> d.IsCoherent | None -> false)

    [<Fact>]
    let ``upgrade writes only consumer-owned seeded paths (no governed or registry writes)`` () =
        let root = atOrAboveMissingFixture ()
        let report = upgradeYes root

        for change in report.ChangedArtifacts do
            let allowed =
                change.Path.StartsWith(".claude/skills/")
                || change.Path.StartsWith(".codex/skills/")
                || change.Path.StartsWith(".agents/skills/")
                || change.Path = ".fsgg/early-stage-guidance.md"

            Assert.True(allowed, $"upgrade wrote an unexpected path: {change.Path}")

        // The consumer registry is not rewritten (re-pin is noTarget, R6).
        Assert.DoesNotContain(report.ChangedArtifacts, fun c -> c.Path = ".fsgg/providers.yml")

    [<Fact>]
    let ``re-seed is no-clobber — a present author-edited artifact is byte-unchanged`` () =
        let edited = Drift.expectedArtifactPaths |> List.head
        let missing = Drift.expectedArtifactPaths |> List.item 1
        let present = Drift.expectedArtifactPaths |> List.filter (fun p -> p <> missing)
        let root = makeFixture (Some farBehindMinimum) present true
        TestSupport.writeRelative root edited "AUTHOR EDIT\n"
        upgradeYes root |> ignore
        Assert.Equal("AUTHOR EDIT\n", TestSupport.readRelative root edited)

    [<Fact>]
    let ``only upgrade carries a remediation summary — doctor and refresh never do`` () =
        let root = atOrAboveMissingFixture ()
        Assert.True (doctorReport root).Upgrade.IsNone
        // A lifecycle/cross-cutting command never produces an upgrade summary or a doctor one.
        let initReport = TestSupport.request Init (TestSupport.tempDirectory ()) |> TestSupport.runRequest
        Assert.True initReport.Upgrade.IsNone
        Assert.True initReport.Doctor.IsNone


/// Interactive `upgrade` confirm-loop tests (T029). Synthetic scripted stdin (disclosed in
/// the test names) drives the real `Confirm` edge interpreter; serialized via the `Console`
/// collection because `Console.In`/`Console.Out` are process-global.
[<Collection("Console")>]
module UpgradeInteractiveTests =
    open RemediationSupport

    let private upgrade (report: CommandReport) = report.Upgrade.Value

    [<Fact>]
    let ``Synthetic confirm-all applies the re-seed step and exits 0`` () =
        let root = atOrAboveMissingFixture ()
        let report = upgradeInteractive root "y\n"
        let summary = upgrade report
        Assert.Equal("interactive", summary.Mode)
        Assert.Contains("artifactReSeed", summary.AppliedStepIds)
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``Synthetic decline skips the step, surfaces residual drift, exits 0`` () =
        let root = atOrAboveMissingFixture ()
        let report = upgradeInteractive root "n\n"
        let summary = upgrade report
        Assert.Contains("artifactReSeed", summary.SkippedStepIds)
        Assert.True summary.ResidualDrift
        Assert.Contains("upgrade.residualDrift", diagnosticIds report)
        Assert.Equal(0, exitCode report)


/// 058/ADR-0014 P1 — content-addressed skill drift over process + product skills. The red test
/// the #61 Done criterion names: `doctor` must detect BOTH content divergence AND provider-skill
/// loss, invisible before this feature. Real-filesystem fixtures; doctor stays read-only.
module SkillContentDriftTests =
    open RemediationSupport

    let private doctor (report: CommandReport) =
        match report.Doctor with
        | Some summary -> summary
        | None -> failwith "expected a doctor summary"

    let private absolute root (path: string) =
        Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))

    [<Fact>]
    let ``a coherent product with process + product skills reports no skill drift`` () =
        let root = productCoherentFixture ()
        let summary = doctor (doctorReport root)
        Assert.Empty summary.SkillDriftPaths
        Assert.True summary.IsCoherent

    [<Fact>]
    let ``doctor detects a byte-divergent product skill copy (content divergence)`` () =
        let root = productCoherentFixture ()
        // Edit ONE root's copy so it diverges from the others and its recorded digest.
        TestSupport.writeRelative root ".claude/skills/fs-gg-demo/SKILL.md" "TAMPERED\n"

        let summary = doctor (doctorReport root)
        Assert.Contains(".claude/skills/fs-gg-demo/SKILL.md", summary.SkillDriftPaths)
        // The recorded digest pinpoints the offending root — the byte-correct copies are NOT flagged.
        Assert.DoesNotContain(".codex/skills/fs-gg-demo/SKILL.md", summary.SkillDriftPaths)
        Assert.DoesNotContain(".agents/skills/fs-gg-demo/SKILL.md", summary.SkillDriftPaths)
        Assert.False summary.IsCoherent
        Assert.Contains("doctor.driftDetected", diagnosticIds (doctorReport root))

    [<Fact>]
    let ``doctor detects a product skill missing from one root (provider-skill loss)`` () =
        let root = productCoherentFixture ()
        // Delete the provider skill's copy in one root.
        File.Delete(absolute root ".codex/skills/fs-gg-demo/SKILL.md")

        let summary = doctor (doctorReport root)
        Assert.Contains(".codex/skills/fs-gg-demo/SKILL.md", summary.SkillDriftPaths)
        Assert.False summary.IsCoherent

    [<Fact>]
    let ``doctor detects a divergent SEEDED process skill copy`` () =
        let root = productCoherentFixture ()
        // A process (fs-gg-sdd-*) skill copy edited to diverge from its canonical body.
        let target = ".claude/skills/fs-gg-sdd-plan/SKILL.md"
        TestSupport.writeRelative root target "EDITED\n"

        let summary = doctor (doctorReport root)
        Assert.Contains(target, summary.SkillDriftPaths)
        Assert.False summary.IsCoherent

    [<Fact>]
    let ``doctor makes zero writes even when content drift is present`` () =
        let root = productCoherentFixture ()
        TestSupport.writeRelative root ".claude/skills/fs-gg-demo/SKILL.md" "TAMPERED\n"
        let before = treeHash root
        doctorReport root |> ignore
        Assert.Equal(before, treeHash root)

    [<Fact>]
    let ``upgrade --yes re-seeds a missing process copy and reports it no longer residual`` () =
        let root = productCoherentFixture ()
        // Delete a SEEDED copy (re-seedable) AND diverge a PRODUCT copy (advisory, not repaired).
        File.Delete(absolute root ".agents/skills/fs-gg-sdd-plan/SKILL.md")
        TestSupport.writeRelative root ".claude/skills/fs-gg-demo/SKILL.md" "TAMPERED\n"

        let report = upgradeYes root
        let summary = report.Upgrade.Value
        // The re-seed refilled the missing process copy...
        Assert.True(TestSupport.existsRelative root ".agents/skills/fs-gg-sdd-plan/SKILL.md")
        Assert.Contains("artifactReSeed", summary.AppliedStepIds)
        // ...but the divergent product copy is advisory (not clobbered) and stays residual.
        Assert.Contains(".claude/skills/fs-gg-demo/SKILL.md", summary.SkillDriftPaths)
        Assert.True summary.ResidualDrift
        Assert.Equal("TAMPERED\n", TestSupport.readRelative root ".claude/skills/fs-gg-demo/SKILL.md")

    // 058 review Finding 1: a provider product that ships a skill-shaped file OUTSIDE the provider
    // source root (`.agents/skills/`) must never be treated as an agent skill — else a perfectly
    // coherent scaffold is falsely reported incoherent with no repair path.
    [<Fact>]
    let ``doctor ignores a product file that merely looks skill-shaped`` () =
        let root = productCoherentFixtureWith [ decoyAppSkillPath ]
        let summary = doctor (doctorReport root)
        Assert.Empty summary.SkillDriftPaths
        Assert.True summary.IsCoherent

    // 058 review Finding 4: when the ONLY drift is advisory content drift (no applicable step),
    // a non-interactive `upgrade` without `--yes` must NOT dead-end at exit 1 — it reports the
    // advisory (exit 0, residual). CI runs upgrade non-interactively.
    [<Fact>]
    let ``upgrade non-interactive on advisory-only drift reports exit 0 not a refusal`` () =
        let root = productCoherentFixture ()
        // A byte-divergent PRODUCT copy — advisory only (no re-seed step covers product skills).
        TestSupport.writeRelative root ".claude/skills/fs-gg-demo/SKILL.md" "TAMPERED\n"

        let report = upgradeNonInteractive root
        let summary = report.Upgrade.Value
        Assert.True(summary.Mode <> "refusedNonInteractive")
        Assert.DoesNotContain("upgrade.nonInteractiveNoYes", diagnosticIds report)
        Assert.Contains(".claude/skills/fs-gg-demo/SKILL.md", summary.SkillDriftPaths)
        Assert.True summary.ResidualDrift
        Assert.Equal(0, exitCode report)


/// #68: the interactive `Confirm` prompt is written to **stderr**, never stdout, so a
/// redirected stdout (`fsgg-sdd upgrade > out.json` from a TTY) keeps the deterministic JSON
/// report contract uncorrupted. Serialized via the `Console` collection (Console.In/Out/Error
/// are process-global).
[<Collection("Console")>]
module ConfirmPromptTests =
    open System
    open FS.GG.SDD.Commands.CommandEffects

    [<Fact>]
    let ``Synthetic Confirm writes the prompt to stderr and nothing to stdout`` () =
        let originalIn = Console.In
        let originalOut = Console.Out
        let originalError = Console.Error
        use stdout = new StringWriter()
        use stderr = new StringWriter()

        try
            Console.SetIn(new StringReader("y\n"))
            Console.SetOut stdout
            Console.SetError stderr
            let prompt = "Apply this step? [y/N] "
            // Drive the real `Confirm` edge through the public interpreter.
            let result = interpret "." false (Confirm("step", prompt))

            Assert.Equal(Some true, result.Confirmed)
            Assert.Equal("", stdout.ToString())
            Assert.Contains("Apply this step?", stderr.ToString())
        finally
            Console.SetIn originalIn
            Console.SetOut originalOut
            Console.SetError originalError
