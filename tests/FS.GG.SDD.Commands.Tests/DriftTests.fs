namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

/// FS-GG/FS.GG.SDD#726: `doctor`'s content-addressed skill-drift surface over the WHOLE skill
/// directory, not `SKILL.md` alone. Before this, `Drift` called `SkillMirror.verify` — the
/// single-body entry point — so a workspace whose `.claude/skills/<id>/references/deep-detail.md`
/// disagreed with its `.codex` copy was reported *coherent*: the surface was strictly weaker than
/// the invariant it claimed to report on.
///
/// The end-to-end cases here are the load-bearing ones. A pure `Drift.compute` test can be fed an
/// auxiliary body by hand and would pass even if `doctor` never looked at the file; only a real
/// on-disk fixture driven through the command proves the root enumeration and the two-phase read
/// gate actually deliver the auxiliary bytes to the fold.
module MultiFileSkillDriftTests =
    open RemediationSupport

    let private doctorSummary (report: CommandReport) =
        match report.Doctor with
        | Some summary -> summary
        | None -> failwith "expected a doctor summary"

    let private absolute root (path: string) =
        Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))

    /// The auxiliary file every case below hangs off — the exact shape #726 names.
    let private auxiliary = "references/deep-detail.md"

    let private auxiliaryPath root id = $"{root}/skills/{id}/{auxiliary}"

    /// Write one auxiliary body per root for `id`. `bodies` is `(root, body)`.
    let private writeAuxiliaries fixtureRoot id bodies =
        for root, body in bodies do
            TestSupport.writeRelative fixtureRoot (auxiliaryPath root id) body

    // ---------------------------------------------------------------------------------------
    // AC5 — the regression that reds on a drifted auxiliary.
    // ---------------------------------------------------------------------------------------

    [<Fact>]
    let ``doctor reports a divergent AUXILIARY file of a product skill, naming that file`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries
            fixtureRoot
            productSkillId
            [ ".agents", "canonical\n"; ".claude", "canonical\n"; ".codex", "DRIFTED\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        // The offending FILE is named — not merely the skill, and not merely its `SKILL.md`.
        Assert.Contains(auxiliaryPath ".codex" productSkillId, summary.SkillDriftPaths)
        Assert.False summary.IsCoherent

    [<Fact>]
    let ``doctor reports a divergent AUXILIARY file of a SEEDED process skill`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"

        writeAuxiliaries fixtureRoot id [ ".agents", "a\n"; ".claude", "a\n"; ".codex", "b\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.Contains(auxiliaryPath ".codex" id, summary.SkillDriftPaths)
        Assert.False summary.IsCoherent

    // The pre-#726 blindness, stated as its own assertion: a drifted auxiliary must not be
    // laundered into a `SKILL.md` report. `SKILL.md` is byte-identical and digest-matching in this
    // fixture, so flagging it would misdirect the repair at a file that is not wrong.
    [<Fact>]
    let ``a drifted auxiliary does not flag the coherent SKILL.md of the same skill`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries
            fixtureRoot
            productSkillId
            [ ".agents", "canonical\n"; ".claude", "canonical\n"; ".codex", "DRIFTED\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        for root in Fsgg.Schemas.agentSkillRoots do
            Assert.DoesNotContain(Fsgg.SkillMirror.skillPath root productSkillId, summary.SkillDriftPaths)

    [<Fact>]
    let ``an auxiliary present in only one root is reported at the roots that LACK it`` () =
        let fixtureRoot = productCoherentFixture ()
        writeAuxiliaries fixtureRoot productSkillId [ ".claude", "only here\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        Assert.Contains(auxiliaryPath ".codex" productSkillId, summary.SkillDriftPaths)
        Assert.Contains(auxiliaryPath ".agents" productSkillId, summary.SkillDriftPaths)
        // The root that HAS the file is not the one to repair.
        Assert.DoesNotContain(auxiliaryPath ".claude" productSkillId, summary.SkillDriftPaths)
        Assert.False summary.IsCoherent

    // ---------------------------------------------------------------------------------------
    // No false positives — the surface must stay silent on a coherent multi-file skill.
    // ---------------------------------------------------------------------------------------

    [<Fact>]
    let ``a multi-file skill identical across every root stays coherent`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries
            fixtureRoot
            productSkillId
            [ ".agents", "shared\n"; ".claude", "shared\n"; ".codex", "shared\n" ]

        TestSupport.writeRelative fixtureRoot ".agents/skills/fs-gg-demo/agents/reviewer.yaml" "name: r\n"
        TestSupport.writeRelative fixtureRoot ".claude/skills/fs-gg-demo/agents/reviewer.yaml" "name: r\n"
        TestSupport.writeRelative fixtureRoot ".codex/skills/fs-gg-demo/agents/reviewer.yaml" "name: r\n"

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.Empty summary.SkillDriftPaths
        Assert.True summary.IsCoherent

    // The two-phase read gate must TERMINATE. #726 gave phase 2 a read set that overlaps the reads
    // `Foundation.remediationReadEffects` already plans — every seeded `SKILL.md` is in both — and a
    // gate that asks "is any of these already planned?" answers yes on the first pass and parks at
    // "emit nothing" forever. The run loop then goes idle with no drift computed, and the failure is
    // SILENT: `doctor` reports `noChange`, exit 0, no summary and no diagnostic, which reads exactly
    // like a healthy workspace. Asserting the summary EXISTS is what distinguishes them.
    [<Fact>]
    let ``the skill read gate terminates and always produces a doctor summary`` () =
        for fixtureRoot in
            [ productCoherentFixture ()
              coherentFixture ()
              noProvenanceFixture ()
              atOrAboveMissingFixture ()
              pre056Fixture () ] do
            Assert.True((doctorReport fixtureRoot).Doctor.IsSome, "doctor produced no summary")

    // ---------------------------------------------------------------------------------------
    // AC3 — the root-selection rule is preserved, now per file.
    // ---------------------------------------------------------------------------------------

    // A root that carries NO copy of the skill is ONE repair, so it is reported once at the
    // `SKILL.md` that makes the directory a skill — not once per auxiliary the other roots carry.
    // This is also exactly what the surface reported before it could see auxiliaries at all.
    [<Fact>]
    let ``a root missing the whole skill is reported once at SKILL_md, not per auxiliary`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries
            fixtureRoot
            productSkillId
            [ ".agents", "shared\n"; ".claude", "shared\n"; ".codex", "shared\n" ]

        Directory.Delete(absolute fixtureRoot $".codex/skills/{productSkillId}", true)

        let summary = doctorSummary (doctorReport fixtureRoot)

        Assert.Contains(Fsgg.SkillMirror.skillPath ".codex" productSkillId, summary.SkillDriftPaths)
        Assert.DoesNotContain(auxiliaryPath ".codex" productSkillId, summary.SkillDriftPaths)

    // The recorded digest still pinpoints the offending root for `SKILL.md`; the byte-correct
    // copies are not flagged. `ExpectedSkill.Sha256` content-addresses `SKILL.md` alone, so the
    // auxiliaries are held by presence + cross-root identity and cannot be arbitrated this way.
    [<Fact>]
    let ``a hash-mismatched SKILL_md still pinpoints only the offending root`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries
            fixtureRoot
            productSkillId
            [ ".agents", "shared\n"; ".claude", "shared\n"; ".codex", "shared\n" ]

        TestSupport.writeRelative fixtureRoot (Fsgg.SkillMirror.skillPath ".claude" productSkillId) "TAMPERED\n"

        let summary = doctorSummary (doctorReport fixtureRoot)

        Assert.Contains(Fsgg.SkillMirror.skillPath ".claude" productSkillId, summary.SkillDriftPaths)
        Assert.DoesNotContain(Fsgg.SkillMirror.skillPath ".codex" productSkillId, summary.SkillDriftPaths)
        Assert.DoesNotContain(Fsgg.SkillMirror.skillPath ".agents" productSkillId, summary.SkillDriftPaths)

    // ---------------------------------------------------------------------------------------
    // AC4 — advisory, sorted, deduped. AC6 — the lane stays read-only.
    // ---------------------------------------------------------------------------------------

    [<Fact>]
    let ``auxiliary drift stays advisory - doctor exits 0 and writes nothing`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "b\n"; ".codex", "c\n" ]

        let before = treeHash fixtureRoot
        let report = doctorReport fixtureRoot
        let summary = doctorSummary report

        Assert.NotEmpty summary.SkillDriftPaths
        Assert.Equal(0, exitCode report)
        // The directory enumerations #726 added are reads. `doctor` still touches no byte.
        Assert.Equal(before, treeHash fixtureRoot)

    [<Fact>]
    let ``skill drift paths are sorted and deduped`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "b\n"; ".codex", "c\n" ]

        writeAuxiliaries fixtureRoot "fs-gg-sdd-plan" [ ".agents", "a\n"; ".claude", "b\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        Assert.Equal<string list>(summary.SkillDriftPaths |> List.sort, summary.SkillDriftPaths)
        Assert.Equal<string list>(summary.SkillDriftPaths |> List.distinct, summary.SkillDriftPaths)

    // ---------------------------------------------------------------------------------------
    // The migration is a STRICT GENERALIZATION: fed a file set that is exactly `SKILL.md`, the
    // multi-file fold reports precisely what the single-body fold reported. These are the pure
    // cases, so they pin the fold itself rather than the command that feeds it.
    // ---------------------------------------------------------------------------------------

    let private computeWith (bodies: Map<string, string>) =
        Drift.compute
            (Some(record None))
            (Some(descriptor None))
            None
            installedVersion
            (Set.ofList coherentPresent)
            bodies

    [<Fact>]
    let ``SKILL_md-only bodies still report a coherent scaffold`` () =
        let report = computeWith (skillBodiesFor coherentPresent)
        Assert.Empty report.SkillDriftPaths
        Assert.True report.IsCoherent

    [<Fact>]
    let ``SKILL_md-only bodies still report a divergent copy at every present root`` () =
        let target = Fsgg.SkillMirror.skillPath ".claude" "fs-gg-sdd-plan"

        let report =
            computeWith (skillBodiesFor coherentPresent |> Map.add target "EDITED\n")

        // No reference digest for a process skill, so the canonical copy is unknowable and every
        // present root is reported — the pre-#726 rule, unchanged.
        for root in Fsgg.Schemas.agentSkillRoots do
            Assert.Contains(Fsgg.SkillMirror.skillPath root "fs-gg-sdd-plan", report.SkillDriftPaths)

    [<Fact>]
    let ``a divergent auxiliary is reported at every present root when no digest arbitrates`` () =
        let id = "fs-gg-sdd-plan"

        let report =
            computeWith (
                skillBodiesFor coherentPresent
                |> Map.add (auxiliaryPath ".agents" id) "a\n"
                |> Map.add (auxiliaryPath ".claude" id) "a\n"
                |> Map.add (auxiliaryPath ".codex" id) "b\n"
            )

        for root in Fsgg.Schemas.agentSkillRoots do
            Assert.Contains(auxiliaryPath root id, report.SkillDriftPaths)

        Assert.False report.IsCoherent

    // `skillCopyOfPath` is the ONE parser both the collector and the fold use, so its confinement
    // is what keeps a skill-shaped product file out of the drift surface (058 review Finding 1).
    [<Fact>]
    let ``skillCopyOfPath recognises only files under a declared root's skills directory`` () =
        Assert.Equal(
            Some(".claude", "demo", "references/deep-detail.md"),
            Drift.skillCopyOfPath ".claude/skills/demo/references/deep-detail.md"
        )

        Assert.Equal(Some(".agents", "demo", "SKILL.md"), Drift.skillCopyOfPath ".agents/skills/demo/SKILL.md")
        // Backslashes normalize, so a Windows-shaped path is the same copy.
        Assert.Equal(Some(".codex", "demo", "SKILL.md"), Drift.skillCopyOfPath ".codex\\skills\\demo\\SKILL.md")

        // Not under a DECLARED root — the decoy that must never be mistaken for an agent skill.
        Assert.Equal(None, Drift.skillCopyOfPath decoyAppSkillPath)
        Assert.Equal(None, Drift.skillCopyOfPath "app/content/skills/widget/references/x.md")
        // The skill directory itself is not a file within it.
        Assert.Equal(None, Drift.skillCopyOfPath ".claude/skills/demo")
        Assert.Equal(None, Drift.skillCopyOfPath ".fsgg/early-stage-guidance.md")
