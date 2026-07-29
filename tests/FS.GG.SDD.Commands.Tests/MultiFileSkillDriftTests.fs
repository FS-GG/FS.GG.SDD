namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Runtime.InteropServices
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
/// gate actually deliver the auxiliary bytes to the fold. For the same reason the divergence cases
/// assert the EXACT reported list rather than `Contains`: a copy that was never enumerated is
/// reported *missing* at the very path a `Contains` for *divergent* would accept, so `Contains`
/// alone cannot tell the feature working from the feature switched off.
module MultiFileSkillDriftTests =
    open RemediationSupport

    let private doctorSummary (report: CommandReport) =
        match report.Doctor with
        | Some summary -> summary
        | None -> failwith "expected a doctor summary"

    let private absolute root (path: string) =
        Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))

    /// The auxiliary file most cases below hang off — the exact shape #726 names.
    let private auxiliary = "references/deep-detail.md"

    let private auxiliaryPath root id = $"{root}/skills/{id}/{auxiliary}"

    let private skillMd root id = Fsgg.SkillMirror.skillPath root id

    /// Every root, in the declared order — the expected list for "reported at every present root".
    let private allRoots = Fsgg.Schemas.agentSkillRoots

    /// Write one auxiliary body per root for `id`. `bodies` is `(root, body)`.
    let private writeAuxiliaries fixtureRoot id bodies =
        for root, body in bodies do
            TestSupport.writeRelative fixtureRoot (auxiliaryPath root id) body

    /// A coherent multi-file skill: the same auxiliary body in all three roots.
    let private writeCoherentAuxiliaries fixtureRoot id =
        writeAuxiliaries fixtureRoot id (allRoots |> List.map (fun root -> root, "shared\n"))

    // ---------------------------------------------------------------------------------------
    // AC5 — the regression that reds on a drifted auxiliary.
    // ---------------------------------------------------------------------------------------

    [<Fact>]
    let ``doctor reports a divergent AUXILIARY file of a product skill, at every present root`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries
            fixtureRoot
            productSkillId
            [ ".agents", "canonical\n"; ".claude", "canonical\n"; ".codex", "DRIFTED\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        // EXACT, not `Contains`. Only `SKILL.md` carries a reference digest, so a divergent
        // auxiliary has nothing to arbitrate it and every present root is named — and asserting the
        // whole list is what distinguishes "`.codex` diverges" from "`.codex` was never read".
        Assert.Equal<string list>(
            allRoots
            |> List.map (fun root -> auxiliaryPath root productSkillId)
            |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.False summary.IsCoherent

    [<Fact>]
    let ``doctor reports a divergent AUXILIARY file of a SEEDED process skill, at every present root`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"

        writeAuxiliaries fixtureRoot id [ ".agents", "a\n"; ".claude", "a\n"; ".codex", "b\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        Assert.Equal<string list>(
            allRoots |> List.map (fun root -> auxiliaryPath root id) |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.False summary.IsCoherent

    [<Fact>]
    let ``a deeply nested auxiliary is observed and named at its full relative path`` () =
        let fixtureRoot = productCoherentFixture ()

        let nested root =
            $"{root}/skills/{productSkillId}/references/sub/deep.md"

        TestSupport.writeRelative fixtureRoot (nested ".agents") "a\n"
        TestSupport.writeRelative fixtureRoot (nested ".claude") "a\n"
        TestSupport.writeRelative fixtureRoot (nested ".codex") "b\n"

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.Equal<string list>(allRoots |> List.map nested |> List.sort, summary.SkillDriftPaths)

    // The pre-#726 blindness, stated as its own assertion: a drifted auxiliary must not be
    // laundered into a `SKILL.md` report. `SKILL.md` is byte-identical and digest-matching in this
    // fixture, so flagging it would misdirect the repair at a file that is not wrong.
    [<Fact>]
    let ``a drifted auxiliary is reported AND the coherent SKILL_md of the same skill is not`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries
            fixtureRoot
            productSkillId
            [ ".agents", "canonical\n"; ".claude", "canonical\n"; ".codex", "DRIFTED\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        // Positive first: without this the `DoesNotContain`s below are satisfied by an empty list.
        Assert.Contains(auxiliaryPath ".codex" productSkillId, summary.SkillDriftPaths)

        for root in allRoots do
            Assert.DoesNotContain(skillMd root productSkillId, summary.SkillDriftPaths)

    [<Fact>]
    let ``an auxiliary present in only one root is reported at the roots that LACK it`` () =
        let fixtureRoot = productCoherentFixture ()
        writeAuxiliaries fixtureRoot productSkillId [ ".claude", "only here\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        // The root that HAS the file is not the one to repair, so it is absent from an EXACT list.
        Assert.Equal<string list>(
            [ auxiliaryPath ".agents" productSkillId
              auxiliaryPath ".codex" productSkillId ]
            |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.False summary.IsCoherent

    // ---------------------------------------------------------------------------------------
    // A surviving auxiliary must not MASK a lost `SKILL.md`.
    // ---------------------------------------------------------------------------------------

    // `verifyFiles` derives skill-level `MissingRoots` from "this root carries no files", and its
    // per-file union from what the PRESENT roots carry. Feed it a root that kept a stray auxiliary
    // but lost `SKILL.md` and — unless `Drift` insists a copy is only a copy when it has `SKILL.md`
    // — that root counts as present, `SKILL.md` never enters the union, and its loss is never
    // checked. With every root in that state the whole skill reads as COHERENT, which is strictly
    // weaker than the surface #726 set out to strengthen. Product skills are not in
    // `expectedArtifactPaths`, so nothing else would catch it: `MissingArtifactPaths` stays empty.
    [<Fact>]
    let ``losing SKILL_md from every root is still reported when an auxiliary survives`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId

        for root in allRoots do
            File.Delete(absolute fixtureRoot (skillMd root productSkillId))

        let summary = doctorSummary (doctorReport fixtureRoot)

        Assert.Equal<string list>(
            allRoots |> List.map (fun root -> skillMd root productSkillId) |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.False summary.IsCoherent

    [<Fact>]
    let ``a root holding only an auxiliary is reported as carrying no copy of the skill`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        File.Delete(absolute fixtureRoot (skillMd ".codex" productSkillId))

        let summary = doctorSummary (doctorReport fixtureRoot)

        // One repair — "`.codex` has no copy" — named once, at the file that makes a directory a
        // skill. Not a per-file inventory of a directory that is not a skill.
        Assert.Equal<string list>([ skillMd ".codex" productSkillId ], summary.SkillDriftPaths)

    // ---------------------------------------------------------------------------------------
    // No false positives — the surface must stay silent where it should.
    // ---------------------------------------------------------------------------------------

    [<Fact>]
    let ``a multi-file skill identical across every root stays coherent`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId

        for root in allRoots do
            TestSupport.writeRelative fixtureRoot $"{root}/skills/{productSkillId}/agents/reviewer.yaml" "name: r\n"

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.Empty summary.SkillDriftPaths
        Assert.True summary.IsCoherent

    // A consumer's own skill, kept beside the seeded ones, is not this surface's business. Note what
    // enforces that: `verifyFiles` folds over `Drift.expectedSkills`, so an unexpected id cannot
    // reach a verdict however it was collected. `skillCopyFilePaths`'s `expectedIds` filter is a
    // READ-EFFICIENCY measure on top of that — removing it changes which bodies `doctor` reads, not
    // what it reports — so this pins the behaviour, deliberately not the filter.
    [<Fact>]
    let ``a user-authored skill id outside the expected union is never drift`` () =
        let fixtureRoot = productCoherentFixture ()
        TestSupport.writeRelative fixtureRoot ".claude/skills/my-own-skill/SKILL.md" "# mine\n"
        TestSupport.writeRelative fixtureRoot ".claude/skills/my-own-skill/notes.md" "notes\n"

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.Empty summary.SkillDriftPaths
        Assert.True summary.IsCoherent

    // A workspace that was never scaffolded has no provenance, and `Drift.compute`'s no-provenance
    // branch reports no skill drift AT ALL — there is no recorded expectation to judge copies
    // against. #726 does not change that; this pins the silence so a later change has to mean it.
    [<Fact>]
    let ``a workspace with no scaffold provenance reports no skill drift even when copies diverge`` () =
        let fixtureRoot = noProvenanceFixture ()
        let id = "fs-gg-sdd-plan"
        writeAuxiliaries fixtureRoot id [ ".claude", "a\n"; ".codex", "b\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.False summary.HasProvenance
        Assert.Empty summary.SkillDriftPaths

    // ---------------------------------------------------------------------------------------
    // The OWNER-SOURCED class — FS-GG/FS.GG.SDD#733. Cases live at the end of the file, where the
    // #736 advisory helpers they need are in scope.
    // ---------------------------------------------------------------------------------------

    /// An owner-sourced (ADR-0063 driver/game) auxiliary copy that `productCoherentFixture` really
    /// materializes, derived from the same plan rather than hardcoded. Empty in a build with no
    /// owner-skill package embedded (`Drift.ownerSourcedBackfill` degrades to empty), in which case
    /// the cases below have no subject.
    let private ownerSourcedAuxiliaries () =
        ownerSourcedCopies []
        |> List.map fst
        |> List.filter (fun path -> not (path.EndsWith("/SKILL.md", StringComparison.Ordinal)))
        |> List.sort

    // ---------------------------------------------------------------------------------------
    // The two-phase read gate must TERMINATE, in BOTH lanes that share it.
    // ---------------------------------------------------------------------------------------

    // #726 gave phase 2 a read set that overlaps the reads `Foundation.remediationReadEffects`
    // already plans — every seeded `SKILL.md` is in both — and a gate that asks "is any of these
    // already planned?" answers yes on the first pass and parks at "emit nothing" forever. The run
    // loop then goes idle with no drift computed, and the failure is SILENT: the command reports
    // `noChange`, exit 0, no summary and no diagnostic, which reads exactly like a healthy
    // workspace. Asserting the summary EXISTS is what distinguishes them.
    [<Theory>]
    [<InlineData "product">]
    [<InlineData "coherent">]
    [<InlineData "noProvenance">]
    [<InlineData "atOrAboveMissing">]
    [<InlineData "pre056">]
    let ``the skill read gate terminates with a summary in both lanes`` (shape: string) =
        let fixtureRoot =
            match shape with
            | "product" -> productCoherentFixture ()
            | "coherent" -> coherentFixture ()
            | "noProvenance" -> noProvenanceFixture ()
            | "atOrAboveMissing" -> atOrAboveMissingFixture ()
            | "pre056" -> pre056Fixture ()
            | other -> failwith $"unknown fixture shape {other}"

        Assert.True((doctorReport fixtureRoot).Doctor.IsSome, $"doctor produced no summary for {shape}")
        // `HandlersUpgrade` shares the gate verbatim, and a parked gate is just as silent there.
        Assert.True((upgradeNonInteractive fixtureRoot).Upgrade.IsSome, $"upgrade produced no summary for {shape}")

    // ---------------------------------------------------------------------------------------
    // FS-GG/FS.GG.SDD#736 — an EXTRA file under one root.
    //
    // Since #726 the surface compares the UNION of files across roots, so a file present in one
    // root and absent from the others is drift, reported at the roots that LACK it. That detection
    // is right (an inconsistently applied edit IS drift, by the same `claude ≡ codex ≡ agents` rule
    // — ADR-0011 / E7). What was wrong was the REPORT: `upgrade` closed with "some copies diverge
    // from their canonical body — re-scaffold or restore the canonical skill sources", which
    // describes a divergent BODY, and for an extra `.DS_Store` reads as "create `.DS_Store` here".
    // Nothing clears it either: the re-seed writes only MISSING SEEDED paths and there is no delete
    // effect, so `ResidualDrift` recurs forever under a hint about a different failure.
    //
    // These cases pin the two realistic triggers (§Observed) and the two things the fix owes them:
    // the advisory NAMES the not-mirrored condition, and it says plainly that the lane cannot
    // repair it — not the byte-divergence sentence, which must stay reserved for byte divergence.
    // ---------------------------------------------------------------------------------------

    /// The exact junk shape #736 reproduces with — an OS turd under a seeded process skill. Since
    /// FS-GG/FS.GG.SDD#747 this is the subject of the EXCLUSION cases, not the not-mirrored ones.
    let private junkFile = ".DS_Store"

    /// A stand-in for the OS turd's binary payload. Non-empty deliberately: an empty file would
    /// drag an unrelated "does a zero-byte read look absent?" question into a case about mirroring.
    let private junkBody = "Bud1\n"

    /// FS-GG/FS.GG.SDD#747: the not-mirrored subject that survives the ignore rule, and it is
    /// DOT-NAMED on purpose — two jobs in one file.
    ///
    /// 1. It is the #747 AC3 complement: an unusual-but-plausible producer filename that must still
    ///    be observed and still be reported. `.editorconfig` is on neither junk list and a provider
    ///    shipping one inside a skill directory is entirely ordinary.
    /// 2. It carries the ENUMERATION canary `CommandEffects.tryEnumerate` documents. The case that
    ///    used to hold it had `.DS_Store` as its subject, and #747 excludes `.DS_Store` — so that
    ///    case would now report empty whether or not the enumerator lists dot-named files, i.e. it
    ///    would have become a test that cannot fail at the thing it was pinning. A dot-named,
    ///    non-junk subject keeps a case that reds if `AttributesToSkip` ever hides dot-files on
    ///    Unix again.
    let private strayFile = ".editorconfig"

    let private strayBody = "root = true\n"

    /// The wording that must NEVER close a run whose only drift is a not-mirrored file: it is the
    /// pre-#736 hint, and it is an instruction to reconcile BODIES.
    let private divergenceWording = "diverge from their canonical body"

    /// Fragments the not-mirrored advisory owes an operator: what the condition is, and that
    /// re-running `upgrade` is not the repair (#736 AC1 / AC2).
    let private assertNotMirroredAdvisory (hint: string) =
        Assert.Contains("not mirrored", hint)
        Assert.Contains("another root carries", hint)
        Assert.Contains("cannot repair this class", hint)

    [<Fact>]
    let ``an EXTRA dot-named file in one root is advisory drift named at the roots that LACK it`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{strayFile}" strayBody

        let before = treeHash fixtureRoot
        let report = doctorReport fixtureRoot
        let summary = doctorSummary report

        // EXACT: the two roots that lack it, and NOT the root that has it.
        Assert.Equal<string list>(
            [ $".agents/skills/{id}/{strayFile}"; $".codex/skills/{id}/{strayFile}" ]
            |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.False summary.IsCoherent
        // AC3: `doctor` stays advisory and writes nothing.
        Assert.Equal(0, exitCode report)
        Assert.Empty report.ChangedArtifacts
        Assert.Equal(before, treeHash fixtureRoot)

    // #736 AC1 + AC2, on the shape that motivated that issue. The old hint told the operator their
    // copies diverged and to restore the canonical sources; both are false here — every root that
    // HAS the file agrees about it, and there is exactly one.
    [<Fact>]
    let ``upgrade --yes over an EXTRA dot-named file names the not-mirrored condition, not divergence`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{strayFile}" strayBody

        let summary = (upgradeYes fixtureRoot).Upgrade.Value

        assertNotMirroredAdvisory summary.NextActionHint
        Assert.DoesNotContain(divergenceWording, summary.NextActionHint)

    // #736 AC2's second branch, measured rather than asserted from the shape: the class is genuinely
    // not repairable, so `upgrade` must SAY so — and the run must be a true no-op, not a write that
    // fails to converge. Two consecutive `--yes` runs, tree byte-identical throughout.
    [<Fact>]
    let ``upgrade --yes over an EXTRA dot-named file is a stable no-op that says it cannot repair it`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{strayFile}" strayBody

        let before = treeHash fixtureRoot
        let first = upgradeYes fixtureRoot
        let afterFirst = treeHash fixtureRoot
        let second = upgradeYes fixtureRoot

        for report in [ first; second ] do
            let summary = report.Upgrade.Value
            Assert.True summary.ResidualDrift
            Assert.Contains($".codex/skills/{id}/{strayFile}", summary.SkillDriftPaths)
            assertNotMirroredAdvisory summary.NextActionHint
            Assert.Equal(0, exitCode report)

        // Unchanged after each run: nothing was mirrored, nothing was deleted, nothing converged.
        Assert.Equal(before, afterFirst)
        Assert.Equal(before, treeHash fixtureRoot)

    // The second realistic trigger from §Observed, with no user junk involved: a provider drops a
    // file from a multi-file skill and the no-clobber re-mirror never removes the stale copies, so
    // the roots disagree about whether the file exists. On disk this is the SAME condition as the
    // junk file — a file some roots carry and one does not — and it must get the same advisory.
    [<Fact>]
    let ``a provider-dropped file left in a subset of roots is not-mirrored, not divergent`` () =
        let fixtureRoot = productCoherentFixture ()

        // `.codex` was refreshed and lost the file; `.agents`/`.claude` still carry it, in
        // agreement with each other — so nothing here is a byte divergence.
        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "stale\n"; ".claude", "stale\n" ]

        let doctor = doctorSummary (doctorReport fixtureRoot)

        Assert.Equal<string list>([ auxiliaryPath ".codex" productSkillId ], doctor.SkillDriftPaths)
        Assert.False doctor.IsCoherent

        let summary = (upgradeYes fixtureRoot).Upgrade.Value
        assertNotMirroredAdvisory summary.NextActionHint
        Assert.DoesNotContain(divergenceWording, summary.NextActionHint)
        Assert.True summary.ResidualDrift

    // The reserved half of AC1: byte divergence must keep the byte-divergence advisory, unchanged.
    // Without this the fix could satisfy the junk case by simply rewording every advisory.
    [<Fact>]
    let ``byte divergence alone still closes with the canonical-body advisory`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "a\n"; ".codex", "b\n" ]

        let summary = (upgradeYes fixtureRoot).Upgrade.Value

        Assert.Equal<string>(
            "Skill content drift detected (advisory); some copies diverge from their canonical body — re-scaffold or restore the canonical skill sources.",
            summary.NextActionHint
        )

    // Both conditions at once: the advisory must state BOTH, because the repairs differ per path
    // and one sentence cannot describe two files in opposite states.
    [<Fact>]
    let ``both conditions at once are both named in the advisory`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"

        // Not mirrored: present in `.claude` only.
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{strayFile}" strayBody
        // Divergent: present in every root, bodies disagree.
        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "a\n"; ".codex", "b\n" ]

        let summary = (upgradeYes fixtureRoot).Upgrade.Value

        assertNotMirroredAdvisory summary.NextActionHint
        Assert.Contains(divergenceWording, summary.NextActionHint)

    // ---------------------------------------------------------------------------------------
    // FS-GG/FS.GG.SDD#747 — the OS/VCS junk ignore rule.
    //
    // #747 AC1 was answered by decision: adopt an ignore rule, add NO repair, NO delete effect, NO
    // per-path prompt, and keep `doctor` read-only. What remains testable is AC3 (the rule cannot
    // hide a file a producer actually ships), AC4 (`upgrade` now genuinely CONVERGES on the class
    // it claims to, asserted by two consecutive runs reaching zero residual drift), AC5 (the
    // advisory stays truthful), and the binding condition that the exclusion is STATED rather than
    // silent.
    // ---------------------------------------------------------------------------------------

    /// A skill id owned by neither the seeded process namespace nor the product fixture, so
    /// `Drift.ownerSourcedSkillFiles` picks it up from a hand-built provenance record — the only
    /// way to model "a producer DECLARED this file" in a unit test.
    let private declaringDriverId = "junk-shipper"

    /// A junk NAME a producer might genuinely ship — Windows really does use `desktop.ini` for
    /// folder settings, and a driver that ships one inside its skill directory means it.
    let private declarableJunkName = "desktop.ini"

    let private driverRow (path: string) : FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath =
        { FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath.Path = path
          Owner = FS.GG.SDD.Artifacts.ArtifactRef.ArtifactOwner.Driver
          // No reference digest: `verifyFileSet` reads an empty digest as "no authority to
          // arbitrate with", so presence and cross-root identity still decide. That is all this
          // case needs, and inventing a digest would test the digest path instead.
          Sha256 = Some "" }

    /// A provenance record DECLARING `<relatives>` for `declaringDriverId` in every root.
    let private recordDeclaring (relatives: string list) =
        { record None with
            DriverPaths =
                [ for root in allRoots do
                      for relative in relatives -> driverRow $"{root}/skills/{declaringDriverId}/{relative}" ] }

    /// Bodies for a coherent `declaringDriverId` copy in every root, plus whatever `extra` adds.
    let private declaringDriverBodies extra =
        let baseline =
            skillBodiesFor coherentPresent
            |> Map.toList
            |> List.append [ for root in allRoots -> $"{root}/skills/{declaringDriverId}/SKILL.md", "# junk-shipper\n" ]

        (baseline @ extra) |> Map.ofList

    // The core of the decision: a `.DS_Store` under ONE root is no longer drift at all. Before
    // #747 this reported two not-mirrored paths and left `ResidualDrift` true forever — "converged"
    // could never mean what it said on any machine that had opened a Finder window.
    [<Fact>]
    let ``an EXTRA junk file in one root is EXCLUDED from the comparison entirely`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{junkFile}" junkBody

        let before = treeHash fixtureRoot
        let report = doctorReport fixtureRoot
        let summary = doctorSummary report

        Assert.Empty summary.SkillDriftPaths
        Assert.True summary.IsCoherent
        // #736 AC3 / #747: `doctor` stays read-only and exit 0, ignore rule or no ignore rule.
        Assert.Equal(0, exitCode report)
        Assert.Empty report.ChangedArtifacts
        Assert.Equal(before, treeHash fixtureRoot)

    // #747 AC4, the clause the whole decision was worth having for: `upgrade` must CONVERGE on the
    // class it claims to repair, "asserted by two consecutive runs reaching zero residual drift".
    // Two runs, both `ResidualDrift = false`, tree byte-identical throughout — the second run is
    // what distinguishes convergence from a one-shot repair that re-dirties the tree.
    [<Fact>]
    let ``upgrade --yes over a junk-only tree CONVERGES across two consecutive runs`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"

        // Every junk shape the rule recognises, at once, and one of them nested under an auxiliary
        // directory — the last is what proves the match is on the FILE name, not the whole path.
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{junkFile}" junkBody
        TestSupport.writeRelative fixtureRoot $".codex/skills/{id}/Thumbs.db" "thumbs\n"
        TestSupport.writeRelative fixtureRoot $".agents/skills/{id}/SKILL.md.orig" "merge leftover\n"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/references/notes.md~" "backup\n"

        let before = treeHash fixtureRoot
        let first = upgradeYes fixtureRoot
        let afterFirst = treeHash fixtureRoot
        let second = upgradeYes fixtureRoot

        for report in [ first; second ] do
            let summary = report.Upgrade.Value
            Assert.False summary.ResidualDrift
            Assert.Empty summary.SkillDriftPaths
            Assert.Equal(0, exitCode report)

        // No repair was adopted, so convergence here must come from the SUBTRACTION and from
        // nothing else: not one byte moved, and in particular no junk file was deleted.
        Assert.Equal(before, afterFirst)
        Assert.Equal(before, treeHash fixtureRoot)
        Assert.True(File.Exists(absolute fixtureRoot $".claude/skills/{id}/{junkFile}"))

    // The binding condition on stating the exclusion: "this tree is converged" must never be
    // quietly conditional on an invisible subtraction. The advisory names the files it dropped AND
    // the complete rule that dropped them.
    [<Fact>]
    let ``the advisory STATES the junk it excluded, and the rule that excluded it`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{junkFile}" junkBody

        let hint = (upgradeYes fixtureRoot).Upgrade.Value.NextActionHint

        // The file, by its real path — not a count, and not "some files".
        Assert.Contains($".claude/skills/{id}/{junkFile}", hint)
        // The whole closed set, so a reader can tell what else would have been dropped.
        for name in Drift.junkFileNames do
            Assert.Contains(name, hint)

        for suffix in Drift.junkFileSuffixes do
            Assert.Contains(suffix, hint)

        Assert.Contains("not configurable", hint)

    // The complement, and the reason this is not merely the inverse of the case above: silence must
    // stay conditional on junk EXISTING. A hint that always recited the rule would satisfy the
    // assertions above while telling every operator their tree was filtered when it was not.
    [<Fact>]
    let ``a tree with no junk says nothing about the exclusion`` () =
        let hint = (upgradeYes (productCoherentFixture ())).Upgrade.Value.NextActionHint

        Assert.DoesNotContain("Excluded from the comparison", hint)

    // #747 AC3, condition 2 — "a test asserts the complement: a file with an unusual-but-plausible
    // producer name is still observed and still reported." Every name here is near a rule without
    // being on it, which is what makes this able to fail: widen any rule to a prefix, a substring,
    // a case-insensitive match or a glob and one of these goes quiet.
    [<Theory>]
    [<InlineData ".editorconfig">] // dot-named, on no list — also the enumeration canary
    [<InlineData "desktop.ini.md">] // a junk name that is a PREFIX of this one
    [<InlineData "thumbs.db">] // `Thumbs.db` in another casing: the rule is ordinal
    [<InlineData "origins.md">] // contains `orig`, but does not END in `.orig`
    [<InlineData ".orig">] // IS the suffix, so it is a file, not a backup of one
    [<InlineData "~">] // ditto
    [<InlineData "swap.swpx">] // `.swp` is a prefix of this extension, not a suffix
    let ``an unusual-but-plausible producer filename is still observed and still reported`` (producerFile: string) =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{producerFile}" "shipped by a producer\n"

        let summary = doctorSummary (doctorReport fixtureRoot)

        // EXACT: the two roots that lack it. `Contains` would accept a report that named the wrong
        // roots, and a file that was never enumerated is reported at exactly these paths too — so
        // the exact list is what separates "observed and reported" from "unobserved".
        Assert.Equal<string list>(
            [ $".agents/skills/{id}/{producerFile}"; $".codex/skills/{id}/{producerFile}" ]
            |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.False summary.IsCoherent

    // #747 AC3, condition 4 — "if a producer genuinely ships a file whose name is on the list, that
    // is a conflict to surface, not a silent skip." The declaration in provenance is the only
    // evidence available that a producer MEANT the file, so it overrides the name list.
    [<Fact>]
    let ``a junk-named file DECLARED in provenance is compared, not ignored`` () =
        let declared = recordDeclaring [ "SKILL.md"; declarableJunkName ]

        let bodies =
            declaringDriverBodies [ $".claude/skills/{declaringDriverId}/{declarableJunkName}", "[.ShellClassInfo]\n" ]

        let report =
            Drift.compute
                (Some declared)
                (Some(descriptor None))
                None
                installedVersion
                (Set.ofList coherentPresent)
                bodies

        // Surfaced: the two roots that lack the DECLARED file, exactly as for any other declared
        // file the roots disagree about.
        Assert.Equal<string list>(
            [ $".agents/skills/{declaringDriverId}/{declarableJunkName}"
              $".codex/skills/{declaringDriverId}/{declarableJunkName}" ]
            |> List.sort,
            report.SkillNotMirroredPaths
            |> List.filter (fun p -> p.EndsWith(declarableJunkName, StringComparison.Ordinal))
        )

        // And NOT skipped: nothing about it was subtracted.
        Assert.Empty report.IgnoredSkillJunkPaths

    // The other half of the pair, and the one that proves the assertion above is about the
    // DECLARATION rather than about the fixture: the identical tree with the identical file, minus
    // the provenance row, is ignored.
    [<Fact>]
    let ``the same junk-named file is ignored when provenance does NOT declare it`` () =
        let undeclared = recordDeclaring [ "SKILL.md" ]

        let junkPath = $".claude/skills/{declaringDriverId}/{declarableJunkName}"
        let bodies = declaringDriverBodies [ junkPath, "[.ShellClassInfo]\n" ]

        let report =
            Drift.compute
                (Some undeclared)
                (Some(descriptor None))
                None
                installedVersion
                (Set.ofList coherentPresent)
                bodies

        Assert.Empty report.SkillDriftPaths
        Assert.Equal<string list>([ junkPath ], report.IgnoredSkillJunkPaths)

    // `IgnoredSkillJunkPaths` names what was OBSERVED and dropped, never the rule: a workspace that
    // carries no junk must report an empty list, or the advisory above fires on every run.
    [<Fact>]
    let ``no junk on disk means nothing is reported as ignored`` () =
        let report =
            Drift.compute
                (Some(record None))
                (Some(descriptor None))
                None
                installedVersion
                (Set.ofList coherentPresent)
                (skillBodiesFor coherentPresent)

        Assert.Empty report.IgnoredSkillJunkPaths

    // The classification is a SPLIT of the existing surface, not a second opinion about it: an
    // invariant every hint branch depends on and nothing else states. If a later change lets a path
    // fall out of all three classes, the advisory silently stops describing it.
    [<Theory>]
    [<InlineData "notMirrored">]
    [<InlineData "divergent">]
    [<InlineData "mixed">]
    [<InlineData "missingRoot">]
    [<InlineData "lostEverywhere">]
    let ``the drift classes partition the reported paths`` (shape: string) =
        let id = "fs-gg-sdd-plan"
        let bodies = skillBodiesFor coherentPresent

        let bodies =
            match shape with
            | "notMirrored" -> bodies |> Map.add (auxiliaryPath ".claude" id) "only here\n"
            | "divergent" ->
                bodies
                |> Map.add (auxiliaryPath ".agents" id) "a\n"
                |> Map.add (auxiliaryPath ".claude" id) "a\n"
                |> Map.add (auxiliaryPath ".codex" id) "b\n"
            | "mixed" ->
                bodies
                |> Map.add (auxiliaryPath ".claude" id) "only here\n"
                |> Map.add (skillMd ".claude" "fs-gg-sdd-charter") "EDITED\n"
            | "missingRoot" -> bodies |> Map.remove (skillMd ".codex" id)
            | "lostEverywhere" -> allRoots |> List.fold (fun acc root -> Map.remove (skillMd root id) acc) bodies
            | other -> failwith $"unknown shape {other}"

        let report =
            Drift.compute
                (Some(record None))
                (Some(descriptor None))
                None
                installedVersion
                (Set.ofList coherentPresent)
                bodies

        Assert.NotEmpty report.SkillDriftPaths

        // Exhaustive: every reported path is in exactly one class, and the classes add nothing.
        Assert.Equal<string list>(
            report.SkillDriftPaths,
            report.SkillNotMirroredPaths
            @ report.SkillLostPaths
            @ report.SkillDivergentPaths
            |> List.distinct
            |> List.sort
        )

        // Pairwise disjoint, compared by POSITION rather than by value: two DIFFERENT classes that
        // happened to hold the same non-empty set would be the worst overlap there is, and a
        // `left <> right` guard is exactly the one that would skip it.
        let classes =
            [ "notMirrored", report.SkillNotMirroredPaths
              "lost", report.SkillLostPaths
              "divergent", report.SkillDivergentPaths ]
            |> List.map (fun (name, paths) -> name, Set.ofList paths)

        for i in 0 .. classes.Length - 1 do
            for j in i + 1 .. classes.Length - 1 do
                let leftName, left = classes[i]
                let rightName, right = classes[j]

                Assert.True(
                    Set.isEmpty (Set.intersect left right),
                    $"{leftName} and {rightName} both claim {Set.intersect left right |> Set.toList}"
                )

    // A root that carries no copy of the skill while ANOTHER root still has one is not-mirrored —
    // `.codex` has nothing to diverge FROM, and there is a sibling to copy from. This is one of the
    // two paths the classifier derives from skill-level `MissingRoots`, so it is pinned separately.
    [<Fact>]
    let ``a root with no copy of the skill classifies as not mirrored`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        Directory.Delete(absolute fixtureRoot $".codex/skills/{productSkillId}", true)

        let summary = (upgradeYes fixtureRoot).Upgrade.Value

        Assert.Equal<string list>([ skillMd ".codex" productSkillId ], summary.SkillDriftPaths)
        assertNotMirroredAdvisory summary.NextActionHint
        Assert.DoesNotContain(divergenceWording, summary.NextActionHint)

    // The other path from skill-level `MissingRoots`, and the reason the not-mirrored sentence
    // cannot simply absorb it: when NO root carries the skill, "another root carries this file" is
    // FALSE and "copy it from the root that has it" is an impossible instruction. Folding this into
    // the not-mirrored class would reintroduce, in the fix, the exact defect #736 reports — an
    // advisory whose text describes a condition the workspace is not in.
    //
    // The product skill is the one that can reach this state and stay there: it is not in
    // `expectedArtifactPaths`, so no re-seed step ever targets it.
    [<Fact>]
    let ``a skill lost from EVERY root is not called not-mirrored, and says what to restore`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId

        // Every root loses `SKILL.md`; the auxiliary survives, so the directories still exist.
        for root in allRoots do
            File.Delete(absolute fixtureRoot (skillMd root productSkillId))

        let summary = (upgradeYes fixtureRoot).Upgrade.Value

        Assert.Equal<string list>(
            allRoots |> List.map (fun root -> skillMd root productSkillId) |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.Contains("absent from every declared root", summary.NextActionHint)
        Assert.Contains("restore the canonical skill sources", summary.NextActionHint)
        // The two false statements, neither of which may appear.
        Assert.DoesNotContain("another root carries", summary.NextActionHint)
        Assert.DoesNotContain(divergenceWording, summary.NextActionHint)
        Assert.True summary.ResidualDrift

    // ---------------------------------------------------------------------------------------
    // The `upgrade` lane over auxiliary drift.
    // ---------------------------------------------------------------------------------------

    // Auxiliary drift is advisory, exactly as a divergent product `SKILL.md` already is: `upgrade`
    // must not dead-end at a non-interactive refusal over drift it has no step to repair. CI runs
    // `upgrade` non-interactively.
    [<Fact>]
    let ``upgrade non-interactive over auxiliary drift reports residual, not a refusal`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "a\n"; ".codex", "b\n" ]

        let report = upgradeNonInteractive fixtureRoot
        let summary = report.Upgrade.Value

        Assert.NotEqual<string>("refusedNonInteractive", summary.Mode)
        Assert.DoesNotContain("upgrade.nonInteractiveNoYes", diagnosticIds report)
        Assert.Contains(auxiliaryPath ".codex" productSkillId, summary.SkillDriftPaths)
        Assert.True summary.ResidualDrift
        Assert.Equal(0, exitCode report)

    // The re-seed repairs a missing SEEDED copy; the auxiliary drift is advisory and survives it.
    // The two must not be conflated: `upgrade` subtracts `MissingArtifactPaths` from the drift
    // surface to decide what it repaired, and that subtraction only works because a wholly-missing
    // root is still reported at its `SKILL.md`.
    [<Fact>]
    let ``upgrade --yes re-seeds the missing copy and leaves auxiliary drift residual`` () =
        let fixtureRoot = productCoherentFixture ()
        File.Delete(absolute fixtureRoot (skillMd ".agents" "fs-gg-sdd-plan"))

        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "a\n"; ".codex", "b\n" ]

        let report = upgradeYes fixtureRoot
        let summary = report.Upgrade.Value

        Assert.True(TestSupport.existsRelative fixtureRoot (skillMd ".agents" "fs-gg-sdd-plan"))
        Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.AppliedStepIds)
        // Re-seeded, so it is no longer reported...
        Assert.DoesNotContain(skillMd ".agents" "fs-gg-sdd-plan", summary.SkillDriftPaths)
        // ...while the advisory auxiliary drift is untouched and keeps the run residual.
        Assert.Contains(auxiliaryPath ".codex" productSkillId, summary.SkillDriftPaths)
        Assert.True summary.ResidualDrift
        Assert.Equal(0, exitCode report)

    // ---------------------------------------------------------------------------------------
    // AC3 — the root-selection rule is preserved, now per file.
    // ---------------------------------------------------------------------------------------

    [<Fact>]
    let ``a root missing the whole skill is reported once at SKILL_md, not per auxiliary`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        Directory.Delete(absolute fixtureRoot $".codex/skills/{productSkillId}", true)

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.Equal<string list>([ skillMd ".codex" productSkillId ], summary.SkillDriftPaths)

    // The recorded digest still pinpoints the offending root for `SKILL.md`. Note the auxiliary
    // branch of this rule is STRUCTURALLY always the "nothing to arbitrate" case FOR THIS CLASS:
    // `ExpectedSkill` carries one digest and `verifyFiles` applies it to `SKILL.md` alone, so no
    // auxiliary of a process or product skill can take the hash-mismatch branch. That gap is #727.
    // The owner-sourced class is the exception since #733 — it declares a digest per file, so its
    // auxiliaries DO take that branch; see the #733 section at the end of this file.
    [<Fact>]
    let ``a hash-mismatched SKILL_md still pinpoints only the offending root`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        TestSupport.writeRelative fixtureRoot (skillMd ".claude" productSkillId) "TAMPERED\n"

        let summary = doctorSummary (doctorReport fixtureRoot)
        Assert.Equal<string list>([ skillMd ".claude" productSkillId ], summary.SkillDriftPaths)

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
        // The directory enumerations #726 added are reads. `doctor` still touches no byte, and
        // `ChangedArtifacts` catches what a tree hash over FILES cannot — a created directory.
        Assert.Empty report.ChangedArtifacts
        Assert.Equal(before, treeHash fixtureRoot)

    [<Fact>]
    let ``skill drift paths are sorted and deduped`` () =
        let fixtureRoot = productCoherentFixture ()

        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "b\n"; ".codex", "c\n" ]
        writeAuxiliaries fixtureRoot "fs-gg-sdd-plan" [ ".agents", "a\n"; ".claude", "b\n" ]

        let summary = doctorSummary (doctorReport fixtureRoot)

        // Without this the two assertions below hold vacuously on an empty list.
        Assert.NotEmpty summary.SkillDriftPaths
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
        let report =
            computeWith (
                skillBodiesFor coherentPresent
                |> Map.add (skillMd ".claude" "fs-gg-sdd-plan") "EDITED\n"
            )

        // No reference digest for a process skill, so the canonical copy is unknowable and every
        // present root is reported — the pre-#726 rule, unchanged.
        Assert.Equal<string list>(
            allRoots |> List.map (fun root -> skillMd root "fs-gg-sdd-plan") |> List.sort,
            report.SkillDriftPaths
        )

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

        Assert.Equal<string list>(
            allRoots |> List.map (fun root -> auxiliaryPath root id) |> List.sort,
            report.SkillDriftPaths
        )

        Assert.False report.IsCoherent

    // `skillCopyOfPath` is the ONE parser both the collector and the fold use, so its confinement is
    // what keeps a skill-shaped product file out of the drift surface (058 review Finding 1) — and
    // what keeps a traversal out of the paths this surface CONSTRUCTS to report.
    [<Fact>]
    let ``skillCopyOfPath recognises only confined files under a declared root's skills directory`` () =
        Assert.Equal(
            Some(".claude", "demo", "references/deep-detail.md"),
            Drift.skillCopyOfPath ".claude/skills/demo/references/deep-detail.md"
        )

        Assert.Equal(Some(".agents", "demo", "SKILL.md"), Drift.skillCopyOfPath ".agents/skills/demo/SKILL.md")
        // Backslashes normalize, so a Windows-shaped path is the same copy.
        Assert.Equal(Some(".codex", "demo", "SKILL.md"), Drift.skillCopyOfPath ".codex\\skills\\demo\\SKILL.md")

        // Not under a DECLARED root — the decoy that must never be mistaken for an agent skill.
        // `SkillMirror.skillIdOfPath` answers `Some "widget"` here; this parser is root-anchored.
        Assert.Equal(None, Drift.skillCopyOfPath decoyAppSkillPath)
        Assert.Equal(None, Drift.skillCopyOfPath "app/content/skills/widget/references/x.md")
        // The skill directory itself is not a file within it.
        Assert.Equal(None, Drift.skillCopyOfPath ".claude/skills/demo")
        Assert.Equal(None, Drift.skillCopyOfPath ".fsgg/early-stage-guidance.md")

        // Lexical confinement, matching `SkillMirror`'s write-side guard: no `.`, `..`, or empty
        // segment may reach `skillFilePath` to be interpolated back into a reported path.
        Assert.Equal(None, Drift.skillCopyOfPath ".claude/skills/../../etc/passwd/x/SKILL.md")
        Assert.Equal(None, Drift.skillCopyOfPath ".claude/skills/x/../../../etc/passwd")
        Assert.Equal(None, Drift.skillCopyOfPath ".claude/skills/./x/SKILL.md")
        Assert.Equal(None, Drift.skillCopyOfPath ".claude/skills/x//SKILL.md")

    // ---------------------------------------------------------------------------------------
    // FS-GG/FS.GG.SDD#733 — the OWNER-SOURCED class is content-verified, per FILE.
    //
    // This is the strongest of the three classes and it asserted nothing. Process skills have no
    // declared digest at all; product skills have one covering `SKILL.md` (#727). The owner-sourced
    // driver/game class records a `sha256` for EVERY file it materializes
    // (`DriverSkills.plan`/`GameSkills.plan` -> `DriverPaths`/`GameSkillPaths`), and before #733 it
    // was in neither arm of `Drift.expectedSkills` — it reached `doctor` only on the presence-only
    // backfill axis, so a driver auxiliary edited away from its recorded digest read COHERENT.
    // ---------------------------------------------------------------------------------------

    /// The first owner-sourced auxiliary, split into `(root, id, relativePath)` by the SAME parser
    /// the fold uses. `None` in a build with no owner-skill package embedded, or one whose owner
    /// skills are single-file — in which case the cases below have lost their subject and say so
    /// rather than passing vacuously.
    let private ownerAuxiliaryTarget () =
        ownerSourcedAuxiliaries ()
        |> List.tryPick (fun path -> Drift.skillCopyOfPath path)

    /// The same owner-sourced auxiliary at every declared root.
    let private ownerAuxiliaryAtAllRoots (id: string) (relativePath: string) =
        allRoots
        |> List.map (fun root -> Fsgg.SkillMirror.skillFilePath root id relativePath)
        |> List.sort

    /// A build with no multi-file owner-sourced skill has no subject for these cases. Assert the
    /// STRONGER premise — that no owner-sourced copy is delivered AT ALL — so this branch cannot
    /// swallow the case that matters: owner skills that ARE delivered and have merely stopped being
    /// multi-file would satisfy `Assert.Empty(ownerSourcedAuxiliaries ())` and take every case below
    /// silently green. If a build ever delivers single-file owner skills, this reds and says so.
    let private assertNoOwnerSourcedSubject () = Assert.Empty(ownerSourcedCopies [])

    // AC2 + AC6, the case the issue names: an owner-sourced AUXILIARY edited in ONE root. The
    // recorded per-file digest arbitrates it to that root, so the report names one path — not the
    // "no digest to arbitrate, report every present root" fallback every other auxiliary gets.
    [<Fact>]
    let ``a tampered owner-sourced auxiliary is arbitrated to the offending root alone`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let fixtureRoot = productCoherentFixture ()
            let tampered = Fsgg.SkillMirror.skillFilePath ".codex" id relativePath
            TestSupport.writeRelative fixtureRoot tampered "TAMPERED\n"

            let summary = doctorSummary (doctorReport fixtureRoot)

            // EXACT: only `.codex`. A `Contains` would also pass on the every-present-root fallback,
            // which is precisely the weaker verdict this item replaces.
            Assert.Equal<string list>([ tampered ], summary.SkillDriftPaths)
            Assert.False summary.IsCoherent

    // The authenticity gain, and the one verdict cross-root identity can never reach: the SAME edit
    // applied to every root. All three copies agree, so `verifyFiles` — which holds an auxiliary by
    // presence + cross-root identity alone — reports nothing. The recorded per-file digest
    // contradicts all three.
    [<Fact>]
    let ``an owner-sourced auxiliary edited IDENTICALLY in every root is still drift`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let fixtureRoot = productCoherentFixture ()

            for root in allRoots do
                TestSupport.writeRelative fixtureRoot (Fsgg.SkillMirror.skillFilePath root id relativePath) "EDITED\n"

            let summary = doctorSummary (doctorReport fixtureRoot)

            Assert.Equal<string list>(ownerAuxiliaryAtAllRoots id relativePath, summary.SkillDriftPaths)
            Assert.False summary.IsCoherent

    /// The provenance a current-generator scaffold records for the owner-sourced class, as a real
    /// `ScaffoldProvenanceRecord` — the declaration `Drift` reads. `rewrite` maps each recorded
    /// `(path, sha256)` row, so a case can make one digest deliberately wrong.
    let private ownerRecordWith (rewrite: string * string -> string * string) =
        let driverRows, gameRows = ownerSourcedProvenanceRows []

        let produced owner rows =
            rows
            |> List.map rewrite
            |> List.map (fun (path, sha256) ->
                { FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath.Path = path
                  FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath.Owner = owner
                  FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath.Sha256 = Some sha256 })

        { record None with
            DriverPaths = produced FS.GG.SDD.Artifacts.ArtifactRef.ArtifactOwner.Driver driverRows
            GameSkillPaths = produced FS.GG.SDD.Artifacts.ArtifactRef.ArtifactOwner.GameSkill gameRows }

    /// The record a real scaffold writes — every digest the one it verified against.
    let private ownerRecord () = ownerRecordWith (fun row -> row)

    /// The same record with ONE file's recorded digest replaced, at every root — the shape a real
    /// record carries, since all three roots record the same digest for a file.
    let private ownerRecordWithCorruptDigest (skillId: string) (relativePath: string) (sha256: string) =
        let targets =
            allRoots
            |> List.map (fun root -> Fsgg.SkillMirror.skillFilePath root skillId relativePath)
            |> Set.ofList

        ownerRecordWith (fun (path, recorded) ->
            if Set.contains path targets then
                path, sha256
            else
                path, recorded)

    /// A fully coherent workspace's bodies: the seeded skeleton plus every owner-sourced copy, with
    /// the bytes this build materializes.
    let private coherentOwnerBodies () =
        ownerSourcedCopies []
        |> List.fold (fun acc (path, body) -> Map.add path body acc) (skillBodiesFor coherentPresent)

    let private computeOwner provenanceRecord bodies =
        Drift.compute
            (Some provenanceRecord)
            (Some(descriptor None))
            None
            installedVersion
            (Set.ofList coherentPresent)
            bodies

    // AC3: the three facts stay INDEPENDENT. Both shapes below report the SAME path — one
    // owner-sourced auxiliary at `.codex` — from different conditions, so only the classification
    // tells them apart. Collapse per-file absence into the digest verdict and the operator is told
    // to reconcile the bytes of a file that is not there; collapse it the other way and a tampered
    // file is reported as one to mirror back.
    [<Fact>]
    let ``a MISSING owner-sourced auxiliary is not-mirrored; a TAMPERED one is divergent`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let declared = ownerRecord ()
            let target = Fsgg.SkillMirror.skillFilePath ".codex" id relativePath

            let missing = computeOwner declared (coherentOwnerBodies () |> Map.remove target)
            Assert.Equal<string list>([ target ], missing.SkillNotMirroredPaths)
            Assert.Empty missing.SkillDivergentPaths
            Assert.Empty missing.SkillLostPaths

            let tampered =
                computeOwner declared (coherentOwnerBodies () |> Map.add target "TAMPERED\n")

            Assert.Equal<string list>([ target ], tampered.SkillDivergentPaths)
            Assert.Empty tampered.SkillNotMirroredPaths
            Assert.Empty tampered.SkillLostPaths

    // AC4: the presence/backfill axis is untouched. A tree that predates owner-sourced delivery
    // recorded NOTHING about the class, so there is no authority to content-verify against and the
    // owner lane correctly says nothing — while the copies are still previewed for no-clobber
    // re-seed. The honest degradation: absent declaration means unverified, not "verified clean".
    [<Fact>]
    let ``a pre-624 tree reports no owner-sourced content drift and still previews the backfill`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some _ ->
            let fixtureRoot = ownerMissingFixture ()
            let summary = doctorSummary (doctorReport fixtureRoot)

            Assert.Empty summary.SkillDriftPaths

            let reSeed =
                summary.PreviewSteps
                |> List.find (fun step -> step.StepId = ReconciliationStepId.ArtifactReSeed)

            Assert.Equal(ReconciliationOutcome.WouldApply, reSeed.Outcome)

            for path, _ in ownerSourcedCopies [] do
                Assert.Contains(path, reSeed.TargetPaths)

    // AC4's other half, measured end to end: content drift is ADVISORY and the no-clobber re-seed
    // does not clobber it — the same policy a divergent product `SKILL.md` already has. `doctor`
    // exits 0, and `upgrade --yes` leaves the edited bytes exactly as it found them.
    [<Fact>]
    let ``owner-sourced content drift is advisory and survives upgrade --yes unclobbered`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let fixtureRoot = productCoherentFixture ()
            let tampered = Fsgg.SkillMirror.skillFilePath ".codex" id relativePath
            TestSupport.writeRelative fixtureRoot tampered "TAMPERED\n"

            let doctor = doctorReport fixtureRoot
            Assert.Equal(0, exitCode doctor)
            Assert.Empty doctor.ChangedArtifacts

            let report = upgradeYes fixtureRoot
            Assert.Equal(0, exitCode report)
            Assert.True report.Upgrade.Value.ResidualDrift
            Assert.Equal<string>("TAMPERED\n", File.ReadAllText(absolute fixtureRoot tampered))

    // AC5: this is the skill-drift axis, not the seeded-skeleton axis. The owner-sourced class has
    // never been in `ExpectedArtifactCount`/`MissingArtifactPaths` and must not enter them now —
    // those two are golden-pinned facts about the SEEDED set.
    [<Fact>]
    let ``owner-sourced content drift disturbs neither the expected count nor the missing paths`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let fixtureRoot = productCoherentFixture ()
            TestSupport.writeRelative fixtureRoot (Fsgg.SkillMirror.skillFilePath ".codex" id relativePath) "TAMPERED\n"

            let summary = doctorSummary (doctorReport fixtureRoot)

            Assert.NotEmpty summary.SkillDriftPaths
            Assert.Equal(Drift.expectedArtifactCount, summary.ExpectedArtifactCount)
            Assert.Empty summary.MissingArtifactPaths

    // The declaration is read from PROVENANCE, never from the running binary's embedded bytes — the
    // same rule the product class follows, and for the same reason: hash-matching against an
    // embedded reference would flag every prior scaffold after any driver-skill text change across
    // CLI versions. Pinned on the pure fold, where the recorded digest can be made deliberately
    // wrong while the on-disk bytes stay exactly what this build would materialize.
    [<Fact>]
    let ``the owner-sourced expectation comes from the recorded digest, not the embedded body`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            // Every copy on disk carries exactly the bytes this build materializes. Only the
            // RECORDED digest of one file disagrees — and that is the authority this lane consults,
            // so all three copies are reported.
            let corrupted =
                ownerRecordWithCorruptDigest id relativePath (String.replicate 64 "0")

            let report = computeOwner corrupted (coherentOwnerBodies ())

            Assert.Equal<string list>(ownerAuxiliaryAtAllRoots id relativePath, report.SkillDivergentPaths)
            Assert.Equal<string list>(ownerAuxiliaryAtAllRoots id relativePath, report.SkillDriftPaths)
            Assert.False report.IsCoherent

    // ---------------------------------------------------------------------------------------
    // FS-GG/FS.GG.SDD#752 — ONE digest domain, and the removal of #751's either-domain allowance.
    //
    // #751 dropped a `HashMismatchRoots` entry whenever an un-normalized RAW comparison cleared it,
    // because `DriverPaths[].Sha256` could hold either domain and this fold could not tell which.
    // It said so and deferred the fix here. Both producers of the field now record
    // `SkillMirror.sha256` of the body they wrote, so the comparison is exact and the allowance is
    // gone. The three cases below pin what that buys and what it costs.
    // ---------------------------------------------------------------------------------------

    let private rawDigest (body: string) =
        Text.Encoding.UTF8.GetBytes body
        |> Security.Cryptography.SHA256.HashData
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    // AC5, on the pure fold. A CRLF file whose CANONICAL digest is recorded is coherent — which is
    // the case that used to need the allowance, now answered by the recorded value itself.
    [<Fact>]
    let ``a CRLF owner-sourced file matching its CANONICAL recorded digest is not drift`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let crlfBody = "line one\r\nline two\r\n"

            let declared =
                ownerRecordWithCorruptDigest id relativePath (Fsgg.SkillMirror.sha256 crlfBody)

            let bodies =
                allRoots
                |> List.fold
                    (fun acc root -> Map.add (Fsgg.SkillMirror.skillFilePath root id relativePath) crlfBody acc)
                    (coherentOwnerBodies ())

            let report = computeOwner declared bodies

            // Not vacuous: the two domains genuinely disagree on this body.
            Assert.NotEqual<string>(Fsgg.SkillMirror.sha256 crlfBody, rawDigest crlfBody)
            Assert.Empty report.SkillDriftPaths
            Assert.True report.IsCoherent

    // AC4: the surviving comparison REDS on the domain that is no longer accepted. This is the exact
    // shape #751 let through — a body that matches only the raw digest — and it is the reason the
    // allowance was weaker than knowing which domain to expect: nothing about a raw match says the
    // file is the one that was written, only that it hashes to a value in a domain nobody records.
    [<Fact>]
    let ``an owner-sourced file matching ONLY the rejected RAW domain is drift`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let crlfBody = "line one\r\nline two\r\n"
            let declared = ownerRecordWithCorruptDigest id relativePath (rawDigest crlfBody)

            let bodies =
                allRoots
                |> List.fold
                    (fun acc root -> Map.add (Fsgg.SkillMirror.skillFilePath root id relativePath) crlfBody acc)
                    (coherentOwnerBodies ())

            let report = computeOwner declared bodies

            Assert.Equal<string list>(ownerAuxiliaryAtAllRoots id relativePath, report.SkillDriftPaths)
            Assert.False report.IsCoherent

    // The other half of the same rule, unchanged from #751: a tampered body matches no domain at
    // all and still reports. Kept because it is the case the allowance was always at risk of
    // swallowing, and its removal must not be the only thing standing between the two.
    [<Fact>]
    let ``a tampered owner-sourced file matching NEITHER digest domain is still drift`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let target = Fsgg.SkillMirror.skillFilePath ".codex" id relativePath

            let report =
                computeOwner (ownerRecord ()) (coherentOwnerBodies () |> Map.add target "TAMPERED\r\n")

            Assert.Equal<string list>([ target ], report.SkillDriftPaths)
            Assert.False report.IsCoherent

    // AC5 END TO END, and the case a pure fold cannot make: a real scaffold, real files, a real
    // `doctor`. The provenance here is the one THIS BUILD recorded, not a hand-built record, so it
    // proves the producer and the consumer agree rather than that a test can spell one digest twice.
    //
    // The shape is not hypothetical — it is any Windows or `core.autocrlf` checkout of an
    // LF-authored driver file. Before #752 that workspace reported permanent drift on every such
    // file: `upgrade` never rewrites it (no-clobber, and it is not missing) and a re-scaffold
    // reproduces it. Now the recorded digest folds `\r\n`, so the workspace is coherent — and a
    // tampered CRLF file is still not.
    [<Fact>]
    let ``doctor reports a CRLF owner-sourced file coherent end to end, and a tampered one not`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let fixtureRoot = productCoherentFixture ()

            for root in allRoots do
                let path =
                    absolute fixtureRoot (Fsgg.SkillMirror.skillFilePath root id relativePath)

                let lf = File.ReadAllText(path).Replace("\r\n", "\n")
                File.WriteAllText(path, lf.Replace("\n", "\r\n"))

            Assert.Empty (doctorSummary (doctorReport fixtureRoot)).SkillDriftPaths

            // Same workspace, one root's CRLF copy edited: still caught, still arbitrated.
            let tampered = Fsgg.SkillMirror.skillFilePath ".codex" id relativePath
            File.WriteAllText(absolute fixtureRoot tampered, "TAMPERED\r\n")

            let summary = doctorSummary (doctorReport fixtureRoot)
            Assert.Equal<string list>([ tampered ], summary.SkillDriftPaths)
            Assert.False summary.IsCoherent

    // AC6, answered rather than deferred. The issue asked whether the read seam can express a
    // BOM-prefixed file at all, and expected the answer to be no — `File.ReadAllText` strips the
    // BOM, so `UTF8.GetBytes(readText)` is not the file's bytes and the RAW domain is unreachable
    // from a read. That is still true, and it is exactly why the raw domain is the wrong one to
    // record: the CANONICAL domain is defined as what that seam yields, so it is reproducible for
    // every file INCLUDING this one. No limitation is owed to #737/#748 after all.
    [<Fact>]
    let ``doctor reports a BOM-prefixed owner-sourced file coherent end to end`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let fixtureRoot = productCoherentFixture ()
            let bom = [| 0xEFuy; 0xBBuy; 0xBFuy |]

            for root in allRoots do
                let path =
                    absolute fixtureRoot (Fsgg.SkillMirror.skillFilePath root id relativePath)

                File.WriteAllBytes(path, Array.append bom (File.ReadAllBytes path))

            // The bytes really did change — otherwise this passes without testing anything.
            let path =
                absolute fixtureRoot (Fsgg.SkillMirror.skillFilePath ".codex" id relativePath)

            Assert.Equal<byte list>(List.ofArray bom, File.ReadAllBytes path |> Array.take 3 |> List.ofArray)

            let summary = doctorSummary (doctorReport fixtureRoot)
            Assert.Empty summary.SkillDriftPaths
            Assert.True summary.IsCoherent

    // FS-GG/FS.GG.SDD#733 regression on the UPGRADE side: `repairedMissing` subtracted only
    // `MissingArtifactPaths` — the SEEDED axis. Owner-sourced backfill paths are deliberately not in
    // that field, but the same `artifactReSeed` step writes them, so a copy this run just restored
    // was still closing the run as residual drift under a hint saying the lane cannot repair it.
    // Both false, and FR-013 inverted. The shape is every scaffolded product since #624: provenance
    // DECLARES the owner-sourced copies and one is missing on disk.
    [<Fact>]
    let ``upgrade --yes backfills a declared owner-sourced copy and does NOT report it residual`` () =
        match ownerAuxiliaryTarget () with
        | None -> assertNoOwnerSourcedSubject ()
        | Some(_, id, relativePath) ->
            let fixtureRoot = productCoherentFixture ()
            let deleted = Fsgg.SkillMirror.skillFilePath ".codex" id relativePath
            File.Delete(absolute fixtureRoot deleted)

            // Precondition: `doctor` sees it, so the assertions below are not vacuous.
            Assert.Contains(deleted, (doctorSummary (doctorReport fixtureRoot)).SkillDriftPaths)

            let report = upgradeYes fixtureRoot
            let summary = report.Upgrade.Value

            Assert.Contains(ReconciliationStepId.ArtifactReSeed, summary.AppliedStepIds)
            Assert.True(TestSupport.existsRelative fixtureRoot deleted)
            Assert.DoesNotContain(deleted, summary.SkillDriftPaths)
            Assert.False summary.ResidualDrift
            Assert.Equal(0, exitCode report)

            // And it converges: a second look is clean.
            Assert.True (doctorSummary (doctorReport fixtureRoot)).IsCoherent

    // ===================================================================================
    // FS.GG.SDD#745 (decision FS.GG.SDD#754) — `doctor`'s share of the same shape.
    //
    // `skillBodies` was a `List.choose (snapshot …)`, so an unread body was DROPPED from the map,
    // and `SkillMirror.verifyFiles` builds its per-file union from the OBSERVED rows — a file no
    // root contributed a row for is never compared at all. `presentArtifacts` had the mirror-image
    // shape (`Option.isSome`), so an unreadable expected artifact read as MISSING, which pointed
    // the operator at `upgrade` — which would then plan a re-seed write straight into it.
    //
    // `doctor` is the lane #754 used to REJECT refusing at the edge: it is documented read-only
    // and exit 0, and one permissions accident must not wedge a repo. So the correction here is
    // the VERDICT, not the exit code.
    // ===================================================================================

    [<Fact>]
    let ``FS.GG.SDD#745: an unreadable skill copy makes doctor incoherent, at exit 0, and is not "missing"`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let fixtureRoot = productCoherentFixture ()

            // The control leg: this fixture is coherent when every copy can be read. Without it a
            // green "incoherent" assertion below would prove nothing about the read edge.
            Assert.True((doctorSummary (doctorReport fixtureRoot)).IsCoherent)

            let target = skillMd ".claude" productSkillId
            let targetAbsolute = absolute fixtureRoot target
            File.SetUnixFileMode(targetAbsolute, enum<UnixFileMode> 0)

            try
                let report = doctorReport fixtureRoot
                let summary = doctorSummary report

                // The verdict may not be coherent over a subject the run did not read.
                Assert.False summary.IsCoherent

                // …but `doctor` still exits 0 and writes nothing: #754 rejected making one
                // unreadable file fatal to a documented read-only lane.
                Assert.Equal(0, RemediationSupport.exitCode report)

                // #745 AC4: the file is present. Reporting it missing would be the wrong finding
                // and the wrong remedy.
                Assert.DoesNotContain(target, summary.MissingArtifactPaths)

                // The finding names the file, and it is not a tool defect.
                Assert.Contains(
                    report.Diagnostics,
                    fun d -> d.Id = "unreadableFile" && List.contains target d.RelatedIds
                )

                Assert.DoesNotContain(report.Diagnostics, fun d -> d.IsToolDefect)

                // Visible in the projection an operator actually reads (#745 AC4).
                let text = FS.GG.SDD.Commands.CommandRendering.renderText report
                Assert.Contains("unreadableFile:", text)
                Assert.Contains(target, text)
            finally
                File.SetUnixFileMode(targetAbsolute, enum<UnixFileMode> 0o644)

    /// FS.GG.SDD#748 AC2, `doctor`'s half — proven at the LANE, not at the diagnostic.
    ///
    /// The pre-#748 seam read this body with `File.ReadAllText`, so the run got a string, hashed it,
    /// and compared it. A second undecodable copy whose invalid bytes substituted the same way would
    /// have compared EQUAL to this one and reported coherent.
    ///
    /// WHAT THIS DOES NOT ASSERT, deliberately: that the copy is not ALSO reported as skill drift.
    /// It still is. That is the residual `HandlersDoctor` documents at its head and
    /// FS.GG.SDD#760 owns — `SkillMirror.verifyFiles` builds its per-file union from the rows it
    /// OBSERVED, and a body that was never decoded contributes none, so the copy is additionally
    /// classified *not mirrored*. #748 does not close it and must not claim to: by then the verdict
    /// is already non-coherent and the path is already named by `undecodableFile`, so the cost is a
    /// misleading remedy hint rather than a wrong pass. The `surface --check` leg in
    /// `SurfaceCommandTests` DOES assert the not-drift half, because that lane's fold genuinely
    /// separates the two — which is why the pair is asserted asymmetrically rather than by copying
    /// one leg's assertions onto the other lane and calling it proven.
    ///
    /// No Unix guard: the subject is the bytes, which are the same everywhere.
    [<Fact>]
    let ``FS.GG.SDD#748: an undecodable skill copy makes doctor incoherent, at exit 0, and is never reported missing``
        ()
        =
        let fixtureRoot = productCoherentFixture ()

        // The control leg. Without it, a green "incoherent" below would prove nothing.
        Assert.True((doctorSummary (doctorReport fixtureRoot)).IsCoherent)

        let target = skillMd ".claude" productSkillId

        // `abc` + a lone continuation byte: invalid UTF-8 from byte 3.
        File.WriteAllBytes(absolute fixtureRoot target, [| 0x61uy; 0x62uy; 0x63uy; 0x80uy; 0x64uy |])

        let report = doctorReport fixtureRoot
        let summary = doctorSummary report

        // The verdict may not be coherent over a body the run never decoded.
        Assert.False summary.IsCoherent

        // Still exit 0 and still read-only: #754 rejected making one such file fatal to a lane
        // documented read-only, and #748 does not reopen that.
        Assert.Equal(0, RemediationSupport.exitCode report)

        // Not "missing" — the file is right there, and re-seeding it is not the repair.
        Assert.DoesNotContain(target, summary.MissingArtifactPaths)

        // Its OWN diagnostic, naming the file, and never a tool defect.
        Assert.Contains(report.Diagnostics, fun d -> d.Id = "undecodableFile" && List.contains target d.RelatedIds)

        Assert.DoesNotContain(report.Diagnostics, fun d -> d.IsToolDefect)

        // And distinguishable from the permissions refusal, whose remedy would be wrong here.
        Assert.DoesNotContain(report.Diagnostics, fun d -> d.Id = "unreadableFile")

        // Visible in the projection an operator actually reads.
        let text = FS.GG.SDD.Commands.CommandRendering.renderText report
        Assert.Contains("undecodableFile:", text)
        Assert.Contains(target, text)

    /// The load-bearing doctor leg, and the one the skill case above cannot stand in for.
    ///
    /// A skill copy that cannot be read at least still trips the CONTENT fold (its body drops out
    /// of `skillBodies`, so `verifyFiles` reports it as un-mirrored) — the wrong finding, but a
    /// finding. `.fsgg/project.yml` has no such backstop: it is not a skill and not a
    /// content-verified artifact, so before #745 an unreadable one dropped the workspace's
    /// `sdd.minToolVersion` floor silently and `cliAxis` flipped `behind` → `coherentByAbsence`.
    /// A workspace pinned to a CLI floor it does not meet reported itself perfectly healthy
    /// because the tool could not open the file that says so (#745 §4).
    [<Fact>]
    let ``FS.GG.SDD#745: an unreadable project.yml cannot turn an unmet tool floor into coherence`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            // Provider declares NO minimum, so the workspace floor is the only drift signal there
            // is — which is exactly what makes losing it a clean pass.
            let fixtureRoot =
                makeFixtureWithFloor None (Some farAheadMinimum) Drift.expectedArtifactPaths true

            let readable = doctorSummary (doctorReport fixtureRoot)
            Assert.False readable.IsCoherent
            Assert.Equal("behind", readable.CliAxis)

            let configAbsolute = absolute fixtureRoot ".fsgg/project.yml"
            File.SetUnixFileMode(configAbsolute, enum<UnixFileMode> 0)

            try
                let report = doctorReport fixtureRoot
                let summary = doctorSummary report

                // Before #745 this was `true`, with `cliAxis: coherentByAbsence`.
                Assert.False summary.IsCoherent
                Assert.Equal(0, RemediationSupport.exitCode report)

                Assert.Contains(
                    report.Diagnostics,
                    fun d -> d.Id = "unreadableFile" && List.contains ".fsgg/project.yml" d.RelatedIds
                )

                Assert.DoesNotContain(report.Diagnostics, fun d -> d.IsToolDefect)
            finally
                File.SetUnixFileMode(configAbsolute, enum<UnixFileMode> 0o644)

    // ===================================================================================
    // FS.GG.SDD#743 — the `EnumerateDirectory` sibling of #745, end to end.
    //
    // #745 stopped an unenumerable directory being a `toolDefect` at exit 2, but it could only
    // report the whole ROOT `Unreadable` — the listing was discarded. Measured on `main` with this
    // fixture, one `chmod 000` on `.claude/skills/fs-gg-demo/references` produced:
    //
    //     isCoherent=False   exit=0
    //     drift = .claude/skills/fs-gg-demo/references/deep-detail.md
    //             .claude/skills/padd-item/SKILL.md
    //             .claude/skills/work-board/SKILL.md
    //             .claude/skills/work-roadmap/SKILL.md
    //
    // Those last three are present, readable, byte-identical copies. They are DISCOVERED by the
    // root enumeration (`skillCopyFilePaths`), so when the enumeration returned nothing they
    // vanished from the observation set and `verifyFiles` read their absence as *not mirrored at
    // `.claude`* — whole-root phantom drift from one mode bit on an unrelated subdirectory. That
    // is #743 AC3: "one inaccessible subdirectory must not blank its siblings or its parent."
    // ===================================================================================

    [<Fact>]
    let ``FS.GG.SDD#743: an unlistable directory under a skill root does not blank the root, and is named`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let fixtureRoot = productCoherentFixture ()
            writeCoherentAuxiliaries fixtureRoot productSkillId

            // The control leg: coherent while every directory opens. Without it, the assertions
            // below would be satisfied by a fixture that was simply never coherent.
            Assert.True((doctorSummary (doctorReport fixtureRoot)).IsCoherent)

            let blocked = $".claude/skills/{productSkillId}/references"
            let blockedAbsolute = absolute fixtureRoot blocked
            File.SetUnixFileMode(blockedAbsolute, enum<UnixFileMode> 0)

            try
                let report = doctorReport fixtureRoot
                let summary = doctorSummary report

                // AC1 / AC4: `doctor` does not exit 2, and nothing accuses the tool of being
                // broken over a permissions accident.
                Assert.Equal(0, RemediationSupport.exitCode report)
                Assert.DoesNotContain(report.Diagnostics, fun d -> d.IsToolDefect)

                // AC2: the verdict is withheld — a partial listing is never a complete one — and
                // the finding names the DIRECTORY that could not be opened, not the root that
                // could. Naming the root would send the operator to `chmod` a readable directory.
                Assert.False summary.IsCoherent

                Assert.Contains(
                    report.Diagnostics,
                    fun d -> d.Id = "unlistableDirectory" && List.contains blocked d.RelatedIds
                )

                // AC3, and the assertion the whole item turns on. EXACT, not `DoesNotContain`:
                // this list is what distinguishes "the siblings survived" from "the enumeration
                // was switched off", and on `main` it had four members (see the block above).
                //
                // The one remaining member is the KNOWN RESIDUAL #745 named and #743 inherited:
                // `deep-detail.md` is under the directory that could not be opened, so no row was
                // observed for it at `.claude`, and `SkillMirror.verifyFiles` builds its per-file
                // union from observed rows with no state for "not observed" — so it classifies as
                // *not mirrored* rather than *unobserved*. It is a misleading remedy hint on an
                // already-non-coherent, already-named run, not a wrong pass. Filed at its root
                // cause as FS-GG/FS.GG.SDD#760; when that lands, this list becomes empty.
                Assert.Equal<string list>([ auxiliaryPath ".claude" productSkillId ], summary.SkillDriftPaths)

                // AC2 in the projection an operator actually reads — otherwise `--text` shows the
                // findings computed FROM the truncated listing with no sign that it was truncated.
                let text = FS.GG.SDD.Commands.CommandRendering.renderText report
                Assert.Contains("unlistableDirectory:", text)
                Assert.Contains(blocked, text)
            finally
                File.SetUnixFileMode(blockedAbsolute, enum<UnixFileMode> 0o755)
