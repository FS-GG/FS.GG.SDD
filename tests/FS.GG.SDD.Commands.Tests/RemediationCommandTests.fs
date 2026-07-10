namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Commands.Internal.Foundation
open Xunit

/// Pure `Drift` model unit tests (feature 053, T011 / drift-model contract). Constructed
/// inputs only — no I/O.
module DriftTests =
    open RemediationSupport

    /// A scaffolded workspace declaring `minimum` on both the descriptor and the provenance,
    /// and (#313) `floor` as its `sdd.minToolVersion`.
    let private driftWithFloor minimum floor installed present =
        Drift.compute
            (Some(record minimum))
            (Some(descriptor minimum))
            floor
            installed
            (Set.ofList present)
            (skillBodiesFor present)

    let private drift minimum installed present =
        driftWithFloor minimum None installed present

    [<Fact>]
    let ``CLI axis is behind with a delta when installed is below the declared minimum`` () =
        let report =
            drift (Some farAheadMinimum) installedVersion Drift.expectedArtifactPaths

        Assert.Equal("behind", report.CliAxis)
        Assert.True(report.CliBehindBy.IsSome)

    [<Fact>]
    let ``CLI axis is atOrAbove when installed meets the declared minimum`` () =
        let report =
            drift (Some farBehindMinimum) installedVersion Drift.expectedArtifactPaths

        Assert.Equal("atOrAbove", report.CliAxis)
        Assert.True(report.CliBehindBy.IsNone)

    [<Fact>]
    let ``CLI axis is coherentByAbsence when the provider declares no minimum`` () =
        let report = drift None installedVersion Drift.expectedArtifactPaths
        Assert.Equal("coherentByAbsence", report.CliAxis)

    [<Fact>]
    let ``CLI axis is undeterminable when the installed version is unparseable`` () =
        let report =
            drift (Some farAheadMinimum) "not-a-version" Drift.expectedArtifactPaths

        Assert.Equal("undeterminable", report.CliAxis)

    [<Fact>]
    let ``missing artifacts are the sorted expected-minus-present set`` () =
        let present = Drift.expectedArtifactPaths |> List.skip 2
        let report = drift (Some farBehindMinimum) installedVersion present
        Assert.Equal<string list>(Drift.expectedArtifactPaths |> List.take 2 |> List.sort, report.MissingArtifactPaths)

    [<Fact>]
    let ``a behind scaffold with missing artifacts previews self-update and re-seed as wouldApply`` () =
        let report = drift (Some farAheadMinimum) installedVersion []

        let outcomeOf (id: ReconciliationStepId) =
            report.Steps |> List.find (fun s -> s.StepId = id) |> (fun s -> s.Outcome)

        Assert.Equal(ReconciliationOutcome.WouldApply, outcomeOf ReconciliationStepId.CliSelfUpdate)
        Assert.Equal(ReconciliationOutcome.WouldApply, outcomeOf ReconciliationStepId.ArtifactReSeed)
        Assert.Equal(ReconciliationOutcome.NoTarget, outcomeOf ReconciliationStepId.TemplateRePin)
        Assert.False report.IsCoherent

    [<Fact>]
    let ``an at-or-above scaffold with all artifacts present is coherent`` () =
        let report =
            drift (Some farBehindMinimum) installedVersion Drift.expectedArtifactPaths

        Assert.True report.IsCoherent

    [<Fact>]
    let ``no provenance yields HasProvenance false, no steps, coherent-degradation`` () =
        let report = Drift.compute None None None installedVersion Set.empty Map.empty
        Assert.False report.HasProvenance
        Assert.Empty report.Steps
        Assert.True report.IsCoherent

    // ---- 085: the provider-less dev-repo record engages reconciliation ----

    let private devRepoRecordOf () =
        let seeds =
            Drift.expectedArtifactPaths
            |> List.map (fun path ->
                { Path = path
                  Owner = ArtifactOwner.Sdd
                  Sha256 = None })

        devRepoRecord (FS.GG.SDD.Artifacts.SchemaVersion.currentGeneratorVersion ()) seeds

    let private rePinOutcome (report: Drift.DriftReport) =
        report.Steps
        |> List.find (fun s -> s.StepId = ReconciliationStepId.TemplateRePin)
        |> fun s -> s.Outcome

    [<Fact>]
    let ``a dev-repo record engages doctor with no provider and a coherent skeleton`` () =
        let report =
            Drift.compute
                (Some(devRepoRecordOf ()))
                None
                None
                installedVersion
                (Set.ofList Drift.expectedArtifactPaths)
                (skillBodiesFor Drift.expectedArtifactPaths)

        // Not the "no provenance — nothing to reconcile" hole: doctor/upgrade engage...
        Assert.True report.HasProvenance
        // ...but there is no provider (empty pin → reported as None, not an empty string)...
        Assert.Equal(None, report.ProviderName)
        // ...the CLI axis is coherent-by-absence (a dev-repo declares no minimum)...
        Assert.Equal("coherentByAbsence", report.CliAxis)
        // ...the template re-pin has no target (no provider descriptor)...
        Assert.Equal(ReconciliationOutcome.NoTarget, rePinOutcome report)
        // ...and a fully-seeded dev-repo is coherent.
        Assert.True report.IsCoherent

    [<Fact>]
    let ``a dev-repo with a missing seed re-seeds without inventing a provider`` () =
        let present = Drift.expectedArtifactPaths |> List.skip 1

        let report =
            Drift.compute
                (Some(devRepoRecordOf ()))
                None
                None
                installedVersion
                (Set.ofList present)
                (skillBodiesFor present)

        Assert.True report.HasProvenance
        Assert.Equal(None, report.ProviderName)
        Assert.False report.IsCoherent
        Assert.NotEmpty report.MissingArtifactPaths

        let reSeed =
            report.Steps
            |> List.find (fun s -> s.StepId = ReconciliationStepId.ArtifactReSeed)

        Assert.Equal(ReconciliationOutcome.WouldApply, reSeed.Outcome)


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

        Assert.Contains(
            summary.PreviewSteps,
            fun s ->
                s.StepId = ReconciliationStepId.ArtifactReSeed
                && s.Outcome = ReconciliationOutcome.WouldApply
        )

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
        Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.AppliedStepIds)
        Assert.False summary.ResidualDrift
        Assert.Equal(0, exitCode report)

        Assert.True(
            match (doctorReport root).Doctor with
            | Some d -> d.IsCoherent
            | None -> false
        )

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
        Directory.CreateDirectory(Path.Combine(root, blocked.Replace('/', Path.DirectorySeparatorChar)))
        |> ignore

        let report = upgradeYes root
        let summary = upgrade report
        Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.FailedStepIds)
        Assert.True summary.ResidualDrift
        Assert.Equal(2, exitCode report)

    // 056 T025 (US3 / SC-004 / P9): upgrade reconciles a pre-056 product by re-seeding the
    // missing third `.agents/skills/` root no-clobber to zero residual drift, preserving the
    // present (author-owned) copies in the other roots.
    [<Fact>]
    let ``upgrade re-seeds the missing third .agents root to zero residual drift`` () =
        let root = pre056Fixture ()
        // A present .claude copy (dummy author content) must be preserved (no-clobber).
        let presentClaude =
            ".claude/skills/" + List.head SeededSkills.skillNames + "/SKILL.md"

        let preservedBefore = TestSupport.readRelative root presentClaude

        let report = upgradeYes root
        let summary = upgrade report
        Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.AppliedStepIds)
        Assert.False summary.ResidualDrift
        Assert.Equal(0, exitCode report)

        // The third root is materialized for every seeded skill…
        for name in SeededSkills.skillNames do
            Assert.True(
                TestSupport.existsRelative root $".agents/skills/{name}/SKILL.md",
                $"expected .agents copy of {name}"
            )
        // …the present copy is untouched…
        Assert.Equal(preservedBefore, TestSupport.readRelative root presentClaude)
        // …and doctor now reports coherence.
        Assert.True(
            match (doctorReport root).Doctor with
            | Some d -> d.IsCoherent
            | None -> false
        )

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
                || change.Path = ".gitignore"

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
        let initReport =
            TestSupport.request Init (TestSupport.tempDirectory ())
            |> TestSupport.runRequest

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
        Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.AppliedStepIds)
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``Synthetic decline skips the step, surfaces residual drift, exits 0`` () =
        let root = atOrAboveMissingFixture ()
        let report = upgradeInteractive root "n\n"
        let summary = upgrade report
        Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.SkippedStepIds)
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
        Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.AppliedStepIds)
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

/// FS-GG/FS.GG.SDD#313 — the two independent minimum-CLI-version floors, reconciled.
///
/// `Drift.compute` sourced the required minimum from the provider descriptor (falling back to
/// the value recorded in scaffold-provenance) and never read the workspace-declared
/// `sdd.minToolVersion` that FS-GG/FS.GG.SDD#305 added. An author who set the floor above the
/// running CLI therefore got `project.toolVersionBelowMinimum` warned on every lifecycle command
/// while `doctor` reported the CLI axis coherent and `upgrade` — the only command permitted to
/// mutate the CLI installation — declined to remediate it. Two floors, divergent verdicts.
///
/// The stricter floor now governs the CLI axis, and the report names the source that produced it.
module ToolVersionFloorDriftTests =
    open RemediationSupport

    /// A scaffolded workspace declaring `minimum` on both provider surfaces and `floor` as its
    /// `sdd.minToolVersion`.
    let private driftOf minimum floor =
        Drift.compute
            (Some(record minimum))
            (Some(descriptor minimum))
            floor
            installedVersion
            (Set.ofList Drift.expectedArtifactPaths)
            (skillBodiesFor Drift.expectedArtifactPaths)

    let private cliStep (report: Drift.DriftReport) =
        report.Steps
        |> List.find (fun s -> s.StepId = ReconciliationStepId.CliSelfUpdate)

    // ----- AC1: both floors are considered; the stricter one wins. -----

    /// The reported defect: the provider is silent (or satisfied) and only the workspace declares
    /// a floor. Before #313 this read `coherentByAbsence` / `atOrAbove` and reconciled nothing.
    [<Fact>]
    let ``a workspace floor alone drives the CLI axis when the provider declares no minimum`` () =
        let report = driftOf None (Some farAheadMinimum)
        Assert.Equal("behind", report.CliAxis)
        Assert.Equal(Some farAheadMinimum, report.RequiredMinimumCliVersion)
        Assert.True report.CliBehindBy.IsSome
        Assert.False report.IsCoherent

    [<Fact>]
    let ``the workspace floor wins when it is stricter than the provider minimum`` () =
        let report = driftOf (Some farBehindMinimum) (Some farAheadMinimum)
        Assert.Equal("behind", report.CliAxis)
        Assert.Equal(Some farAheadMinimum, report.RequiredMinimumCliVersion)
        Assert.Equal(Some Drift.workspaceFloorSource, report.RequiredMinimumCliVersionSource)

    [<Fact>]
    let ``the provider minimum wins when it is stricter than the workspace floor`` () =
        let report = driftOf (Some farAheadMinimum) (Some farBehindMinimum)
        Assert.Equal("behind", report.CliAxis)
        Assert.Equal(Some farAheadMinimum, report.RequiredMinimumCliVersion)
        Assert.Equal(Some Drift.providerDescriptorSource, report.RequiredMinimumCliVersionSource)

    /// An equal floor is inert, so the tie names the pre-existing authority rather than flapping.
    [<Fact>]
    let ``an equal workspace floor leaves the provider named as the source`` () =
        let report = driftOf (Some farAheadMinimum) (Some farAheadMinimum)
        Assert.Equal(Some farAheadMinimum, report.RequiredMinimumCliVersion)
        Assert.Equal(Some Drift.providerDescriptorSource, report.RequiredMinimumCliVersionSource)

    /// An unparseable floor is not this module's to report — `project.minToolVersionUnparseable`
    /// already warns at report assembly. It must not be mistaken for a real minimum.
    [<Fact>]
    let ``an unparseable workspace floor is ignored by the CLI axis`` () =
        let report = driftOf None (Some "not-a-version")
        Assert.Equal("coherentByAbsence", report.CliAxis)
        Assert.Equal(None, report.RequiredMinimumCliVersion)
        Assert.Equal(None, report.RequiredMinimumCliVersionSource)

    // ----- AC2: the effective minimum names its source. -----

    [<Fact>]
    let ``the source is the provider descriptor when only the provider declares a minimum`` () =
        let report = driftOf (Some farAheadMinimum) None
        Assert.Equal(Some Drift.providerDescriptorSource, report.RequiredMinimumCliVersionSource)

    /// The descriptor is silent, so the recorded provenance value governs — and says so.
    [<Fact>]
    let ``the source is scaffold provenance when only the recorded minimum declares one`` () =
        let report =
            Drift.compute
                (Some(record (Some farAheadMinimum)))
                (Some(descriptor None))
                None
                installedVersion
                (Set.ofList Drift.expectedArtifactPaths)
                (skillBodiesFor Drift.expectedArtifactPaths)

        Assert.Equal(Some farAheadMinimum, report.RequiredMinimumCliVersion)
        Assert.Equal(Some Drift.scaffoldProvenanceSource, report.RequiredMinimumCliVersionSource)

    [<Fact>]
    let ``no minimum anywhere names no source`` () =
        let report = driftOf None None
        Assert.Equal("coherentByAbsence", report.CliAxis)
        Assert.Equal(None, report.RequiredMinimumCliVersionSource)

    /// A descriptor that declares an unparseable minimum still *shadows* the recorded value, as it
    /// did before #313 — precedence is by presence, not by parseability.
    [<Fact>]
    let ``an unparseable descriptor minimum still shadows the recorded provenance value`` () =
        let report =
            Drift.compute
                (Some(record (Some farAheadMinimum)))
                (Some(descriptor (Some "not-a-version")))
                None
                installedVersion
                (Set.ofList Drift.expectedArtifactPaths)
                (skillBodiesFor Drift.expectedArtifactPaths)

        Assert.Equal("coherentByAbsence", report.CliAxis)
        Assert.Equal(None, report.RequiredMinimumCliVersionSource)

    // ----- AC3: the self-update step is reachable when only the workspace floor is unmet. -----

    [<Fact>]
    let ``an unmet workspace floor previews the CLI self-update as wouldApply`` () =
        let report = driftOf None (Some farAheadMinimum)
        Assert.Equal(ReconciliationOutcome.WouldApply, (cliStep report).Outcome)

    /// The CLI axis is a fact about the *installed tool*, not about the scaffold, so a workspace
    /// that was never scaffolded still reconciles its declared floor. With no floor this stays the
    /// unchanged "no provenance — nothing to reconcile" no-op (asserted above).
    [<Fact>]
    let ``a provenance-less workspace still previews the self-update for its declared floor`` () =
        let report =
            Drift.compute None None (Some farAheadMinimum) installedVersion Set.empty Map.empty

        Assert.False report.HasProvenance
        Assert.Equal("behind", report.CliAxis)
        Assert.Equal(ReconciliationOutcome.WouldApply, (cliStep report).Outcome)
        Assert.False report.IsCoherent

    // ----- AC1/AC2/AC4 end to end: doctor reads the floor, reports the source, stays read-only. -----

    let private doctorSummary (report: CommandReport) = report.Doctor.Value

    /// An otherwise fully coherent scaffold whose only drift is the floor it declared. This is the
    /// exact shape that used to report `coherent` while every other command warned.
    [<Fact>]
    let ``doctor reads the workspace floor, names it, and reports the drift it used to hide`` () =
        let root =
            makeFixtureWithFloor (Some farBehindMinimum) (Some farAheadMinimum) Drift.expectedArtifactPaths true

        let report = doctorReport root
        let summary = doctorSummary report
        Assert.Equal("behind", summary.CliAxis)
        Assert.Equal(Some farAheadMinimum, summary.RequiredMinimumCliVersion)
        Assert.Equal(Some Drift.workspaceFloorSource, summary.RequiredMinimumCliVersionSource)
        Assert.False summary.IsCoherent
        Assert.Contains("doctor.driftDetected", diagnosticIds report)
        // AC4: doctor stays strictly read-only and exits 0 whenever it reports.
        Assert.Equal(0, exitCode report)

    [<Fact>]
    let ``doctor over an unmet workspace floor makes zero writes`` () =
        let root =
            makeFixtureWithFloor (Some farBehindMinimum) (Some farAheadMinimum) Drift.expectedArtifactPaths true

        let before = treeHash root
        doctorReport root |> ignore
        Assert.Equal(before, treeHash root)

    /// The floor is opt-in: a workspace that declares none is unchanged by #313.
    [<Fact>]
    let ``doctor over a coherent scaffold with no declared floor is still coherent`` () =
        let root = coherentFixture ()
        let summary = doctorSummary (doctorReport root)
        Assert.Equal("atOrAbove", summary.CliAxis)
        Assert.Equal(Some Drift.providerDescriptorSource, summary.RequiredMinimumCliVersionSource)
        Assert.True summary.IsCoherent

    /// The doctor block's new fact survives the trip out of JSON into the text projection.
    [<Fact>]
    let ``the text projection names the source of the effective minimum`` () =
        let root =
            makeFixtureWithFloor (Some farBehindMinimum) (Some farAheadMinimum) Drift.expectedArtifactPaths true

        let text = FS.GG.SDD.Commands.CommandRendering.renderText (doctorReport root)
        Assert.Contains($"doctorRequiredMinimumCliSource: {Drift.workspaceFloorSource}", text)

    /// The JSON automation contract carries the source beside the minimum it explains. The
    /// full-shape golden pins `doctor` as `null`, so this is the doctor block's only json pin.
    [<Fact>]
    let ``the json contract emits requiredMinimumCliVersionSource beside the minimum`` () =
        let root =
            makeFixtureWithFloor (Some farBehindMinimum) (Some farAheadMinimum) Drift.expectedArtifactPaths true

        let json = serializeReport (doctorReport root)
        Assert.Contains($"\"requiredMinimumCliVersion\": \"{farAheadMinimum}\"", json)
        Assert.Contains($"\"requiredMinimumCliVersionSource\": \"{Drift.workspaceFloorSource}\"", json)

    /// ...and it is an explicit `null` — not an omitted key — when there is no effective minimum,
    /// so a consumer can tell "no floor declared" from "field dropped by an older CLI".
    [<Fact>]
    let ``the json contract emits a null source when no minimum is declared`` () =
        let json = serializeReport (doctorReport (noMinimumFixture ()))
        Assert.Contains("\"requiredMinimumCliVersionSource\": null", json)

    // ----- AC3 end to end: upgrade reaches the self-update step it used to decline. -----

    /// `upgrade` non-interactive without `--yes` refuses *because there is actionable work* — the
    /// fail-closed probe that the self-update step is reachable, without running `dotnet tool
    /// update` in a test. Before #313 the unmet floor was invisible and this exited 0 as a no-op.
    [<Fact>]
    let ``upgrade reaches the self-update step when only the workspace floor is unmet`` () =
        let root =
            makeFixtureWithFloor (Some farBehindMinimum) (Some farAheadMinimum) Drift.expectedArtifactPaths true

        let before = treeHash root
        let report = upgradeNonInteractive root
        let summary = report.Upgrade.Value
        Assert.Equal("refusedNonInteractive", summary.Mode)
        Assert.False summary.AlreadyCoherent

        Assert.Contains(
            summary.Steps,
            fun s ->
                s.StepId = ReconciliationStepId.CliSelfUpdate
                && s.Outcome = ReconciliationOutcome.WouldApply
        )

        Assert.Contains("upgrade.nonInteractiveNoYes", diagnosticIds report)
        Assert.Equal(before, treeHash root)
        Assert.Equal(1, exitCode report)

    /// ...and it is reachable in a workspace that was never scaffolded, where the old
    /// `not HasProvenance` short-circuit returned "nothing to reconcile" unconditionally.
    [<Fact>]
    let ``upgrade reaches the self-update step in a provenance-less workspace with a floor`` () =
        let root = makeFixtureWithFloor None (Some farAheadMinimum) [] false
        let before = treeHash root
        let report = upgradeNonInteractive root
        let summary = report.Upgrade.Value
        Assert.False summary.HasProvenance
        Assert.Equal("refusedNonInteractive", summary.Mode)

        Assert.Contains(
            summary.Steps,
            fun s ->
                s.StepId = ReconciliationStepId.CliSelfUpdate
                && s.Outcome = ReconciliationOutcome.WouldApply
        )

        Assert.Equal(before, treeHash root)
        Assert.Equal(1, exitCode report)
