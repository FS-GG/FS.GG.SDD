namespace FS.GG.SDD.Commands.Tests

open System
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

    /// The exact junk shape #736 reproduces with — an OS turd under a seeded process skill.
    let private junkFile = ".DS_Store"

    /// A stand-in for the OS turd's binary payload. Non-empty deliberately: an empty file would
    /// drag an unrelated "does a zero-byte read look absent?" question into a case about mirroring.
    let private junkBody = "Bud1\n"

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
    let ``an EXTRA junk file in one root is advisory drift named at the roots that LACK it`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{junkFile}" junkBody

        let before = treeHash fixtureRoot
        let report = doctorReport fixtureRoot
        let summary = doctorSummary report

        // EXACT: the two roots that lack it, and NOT the root that has it.
        Assert.Equal<string list>(
            [ $".agents/skills/{id}/{junkFile}"; $".codex/skills/{id}/{junkFile}" ]
            |> List.sort,
            summary.SkillDriftPaths
        )

        Assert.False summary.IsCoherent
        // AC3: `doctor` stays advisory and writes nothing.
        Assert.Equal(0, exitCode report)
        Assert.Empty report.ChangedArtifacts
        Assert.Equal(before, treeHash fixtureRoot)

    // AC1 + AC2, on the shape that motivated the issue. The old hint told the operator their copies
    // diverged and to restore the canonical sources; both are false here — every root that HAS the
    // file agrees about it, there is exactly one, and no canonical source ships a `.DS_Store`.
    [<Fact>]
    let ``upgrade --yes over an EXTRA junk file names the not-mirrored condition, not divergence`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{junkFile}" junkBody

        let summary = (upgradeYes fixtureRoot).Upgrade.Value

        assertNotMirroredAdvisory summary.NextActionHint
        Assert.DoesNotContain(divergenceWording, summary.NextActionHint)

    // AC2's second branch, measured rather than asserted from the shape: the class is genuinely not
    // repairable, so `upgrade` must SAY so — and the run must be a true no-op, not a write that
    // fails to converge. Two consecutive `--yes` runs, tree byte-identical throughout.
    [<Fact>]
    let ``upgrade --yes over an EXTRA junk file is a stable no-op that says it cannot repair it`` () =
        let fixtureRoot = productCoherentFixture ()
        let id = "fs-gg-sdd-plan"
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{junkFile}" junkBody

        let before = treeHash fixtureRoot
        let first = upgradeYes fixtureRoot
        let afterFirst = treeHash fixtureRoot
        let second = upgradeYes fixtureRoot

        for report in [ first; second ] do
            let summary = report.Upgrade.Value
            Assert.True summary.ResidualDrift
            Assert.Contains($".codex/skills/{id}/{junkFile}", summary.SkillDriftPaths)
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
        TestSupport.writeRelative fixtureRoot $".claude/skills/{id}/{junkFile}" junkBody
        // Divergent: present in every root, bodies disagree.
        writeAuxiliaries fixtureRoot productSkillId [ ".agents", "a\n"; ".claude", "a\n"; ".codex", "b\n" ]

        let summary = (upgradeYes fixtureRoot).Upgrade.Value

        assertNotMirroredAdvisory summary.NextActionHint
        Assert.Contains(divergenceWording, summary.NextActionHint)

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

    /// A build with no multi-file owner-sourced skill has no subject for these cases. Assert THAT,
    /// so the suite reports a lost premise instead of going quietly green.
    let private assertNoOwnerSourcedSubject () =
        Assert.Empty(ownerSourcedAuxiliaries ())

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
