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
    // The OWNER-SOURCED class is deliberately still excluded — FS-GG/FS.GG.SDD#733.
    // ---------------------------------------------------------------------------------------

    /// An owner-sourced (ADR-0063 driver/game) auxiliary copy that `productCoherentFixture` really
    /// materializes, derived from the same plan rather than hardcoded. Empty in a build with no
    /// owner-skill package embedded (`Drift.ownerSourcedBackfill` degrades to empty), in which case
    /// the case below has no subject and asserts nothing.
    let private ownerSourcedAuxiliaries () =
        ownerSourcedCopies []
        |> List.map fst
        |> List.filter (fun path -> not (path.EndsWith("/SKILL.md", StringComparison.Ordinal)))
        |> List.sort

    // `Drift.expectedSkills` is the SDD-seeded process union plus the provenance-recorded product
    // ids — the owner-sourced driver/game class is in neither, and reaches `doctor` only on the
    // presence/backfill axis. So its auxiliaries are NOT content-verified, even though it is the one
    // skill class in this product that is genuinely multi-file AND records a per-file digest.
    //
    // That is out of scope for #726 and is filed as #733. It is pinned here so the exclusion is a
    // recorded decision rather than an accident, and so #733 has a test to invert.
    [<Fact>]
    let ``owner-sourced auxiliaries are NOT yet content-verified - deliberate, see 733`` () =
        match ownerSourcedAuxiliaries () with
        | [] ->
            // No owner-skill package embedded in this build, so the class has no auxiliary to
            // diverge. Assert that premise rather than passing silently — if owner skills ARE
            // delivered and simply stopped being multi-file, this case has lost its subject and
            // should say so instead of going quietly green.
            Assert.Empty(ownerSourcedCopies [])
        | target :: _ ->
            let fixtureRoot = productCoherentFixture ()
            TestSupport.writeRelative fixtureRoot target "DRIFTED\n"

            let summary = doctorSummary (doctorReport fixtureRoot)
            Assert.Empty summary.SkillDriftPaths
            Assert.True summary.IsCoherent

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
    // branch of this rule is STRUCTURALLY always the "nothing to arbitrate" case: `ExpectedSkill`
    // carries one digest and `verifyFiles` applies it to `SKILL.md` alone, so no auxiliary can ever
    // take the hash-mismatch branch. That gap is #727, not a hole in this test.
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

    // ---------------------------------------------------------------------------------------
    // FS-GG/FS.GG.SDD#735 — an UNREADABLE file under an expected skill directory.
    //
    // #726 grew the set of files whose unreadability the lane notices from ~51 known `SKILL.md`
    // paths to every file under every expected skill directory across three roots. Before this,
    // `CommandEffects.interpret` turned the resulting IO exception into a `toolDefect` — outcome
    // `Blocked`, exit 2 — from a `chmod 000` on any one of them. The maintainer decision on #735
    // settles the classification: an unreadable file is a FINDING about the tree, not a defect in
    // the tool. Exit 2 keeps meaning "the tool itself failed", and the last case below is what
    // holds it to that.
    // ---------------------------------------------------------------------------------------

    /// `chmod 000` `relativePath`, run `body`, and restore the mode whatever happens.
    ///
    /// Returns `None` where the fixture cannot be built: Windows (no Unix mode) or a process that
    /// can read the file anyway (running as root, the usual container shape). The callers assert
    /// their premise in that case rather than passing silently — a case that quietly stops
    /// exercising an unreadable file has stopped testing #735 without going red.
    let private withUnreadable fixtureRoot (relativePath: string) (body: unit -> 'a) : 'a option =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            None
        else
            let target = absolute fixtureRoot relativePath
            let original = File.GetUnixFileMode target
            File.SetUnixFileMode(target, UnixFileMode.None)

            try
                let unreadable =
                    try
                        File.ReadAllText target |> ignore
                        false
                    with _ ->
                        true

                if unreadable then Some(body ()) else None
            finally
                File.SetUnixFileMode(target, original)

    /// The premise assertion for a `withUnreadable` that could not be built (see above).
    let private assertNoUnreadableFixture fixtureRoot relativePath =
        let readable =
            RuntimeInformation.IsOSPlatform OSPlatform.Windows
            || (try
                    File.ReadAllText(absolute fixtureRoot relativePath) |> ignore
                    true
                with _ ->
                    false)

        Assert.True(readable, $"{relativePath} is unreadable, so the case should have run")

    let private unreadableDiagnostics (report: CommandReport) =
        report.Diagnostics |> List.filter (fun d -> d.Id = "unreadableFile")

    [<Fact>]
    let ``an unreadable auxiliary is a named finding, not a tool defect, and doctor does not exit 2`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        let target = auxiliaryPath ".claude" productSkillId

        match withUnreadable fixtureRoot target (fun () -> doctorReport fixtureRoot) with
        | None -> assertNoUnreadableFixture fixtureRoot target
        | Some report ->
            // The whole point of the item: this was `outcome=Blocked exit=2 diagnostics=[toolDefect]`.
            Assert.DoesNotContain("toolDefect", diagnosticIds report)
            Assert.NotEqual(2, exitCode report)
            Assert.Equal(0, exitCode report)

            // ...and it is NAMED, so the operator can act without a log or an strace.
            match unreadableDiagnostics report with
            | [ diagnostic ] ->
                Assert.Contains(target, diagnostic.Message)
                Assert.Equal<string list>([ target ], diagnostic.RelatedIds)
                Assert.Equal<string option>(Some target, diagnostic.Artifact |> Option.map _.Path)
                Assert.False diagnostic.IsToolDefect
            | other -> failwith $"expected exactly one unreadableFile diagnostic, got {other.Length}"

    // Decision point 2 / `.github#266`, the half that is easy to lose: not exiting 2 must not become
    // reporting a pass. The file could not be verified, so it is reported as drift at the root that
    // holds it — and the `unreadableFile` diagnostic is what says WHICH of the two happened, so a
    // reader is never left inferring "drifted" from "could not be read".
    [<Fact>]
    let ``an unreadable auxiliary is never reported as coherent, and says so as a read failure`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        let target = auxiliaryPath ".claude" productSkillId

        match withUnreadable fixtureRoot target (fun () -> doctorReport fixtureRoot) with
        | None -> assertNoUnreadableFixture fixtureRoot target
        | Some report ->
            let summary = doctorSummary report
            Assert.False summary.IsCoherent
            Assert.Contains(target, summary.SkillDriftPaths)
            Assert.Contains("doctor.driftDetected", diagnosticIds report)
            // The two facts are separately stated, not conflated into one.
            Assert.Equal(1, (unreadableDiagnostics report).Length)

    // `SKILL.md` is the file that MAKES a directory a skill, so its unreadability is the strongest
    // version of the same question: the root reads as carrying no copy at all. Still a finding,
    // still named, still not exit 2.
    [<Fact>]
    let ``an unreadable SKILL_md is a named finding, not a tool defect`` () =
        let fixtureRoot = productCoherentFixture ()
        let target = skillMd ".codex" productSkillId

        match withUnreadable fixtureRoot target (fun () -> doctorReport fixtureRoot) with
        | None -> assertNoUnreadableFixture fixtureRoot target
        | Some report ->
            Assert.DoesNotContain("toolDefect", diagnosticIds report)
            Assert.Equal(0, exitCode report)
            Assert.Contains("unreadableFile", diagnosticIds report)
            Assert.Contains(target, (doctorSummary report).SkillDriftPaths)

    // AC5: the lanes share the read gate, so they must share the verdict. `upgrade` inherited the
    // exit 2 verbatim and must inherit the fix verbatim — including not dead-ending at the
    // non-interactive refusal, which CI would hit.
    [<Fact>]
    let ``upgrade inherits the unreadable-file finding rather than the tool defect`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        let target = auxiliaryPath ".claude" productSkillId

        match withUnreadable fixtureRoot target (fun () -> upgradeNonInteractive fixtureRoot) with
        | None -> assertNoUnreadableFixture fixtureRoot target
        | Some report ->
            Assert.DoesNotContain("toolDefect", diagnosticIds report)
            Assert.NotEqual(2, exitCode report)
            Assert.Contains("unreadableFile", diagnosticIds report)
            Assert.True(report.Upgrade.IsSome, "upgrade produced no summary")

    // The lane stays read-only under the new classification. A degradation that "recovered" by
    // rewriting the file's mode, or by touching anything at all, would be a worse cure.
    [<Fact>]
    let ``doctor over an unreadable file still writes nothing`` () =
        let fixtureRoot = productCoherentFixture ()
        writeCoherentAuxiliaries fixtureRoot productSkillId
        let target = auxiliaryPath ".claude" productSkillId

        match
            withUnreadable fixtureRoot target (fun () ->
                let report = doctorReport fixtureRoot
                report, File.GetUnixFileMode(absolute fixtureRoot target))
        with
        | None -> assertNoUnreadableFixture fixtureRoot target
        | Some(report, modeAfter) ->
            Assert.Empty report.ChangedArtifacts
            Assert.Equal(UnixFileMode.None, modeAfter)

    // The other half of the decision, and the one that keeps the first half honest: exit 2 still
    // means the tool itself failed. Nothing above widens or narrows that.
    //
    // Two legs, because two things could have gone wrong. The interpreter leg proves the new `try`
    // around `ReadFile` did not soften any OTHER effect: an effect the edge genuinely cannot
    // perform still yields a `toolDefect` carrying the exit-2 bit. The end-to-end leg proves the
    // bit still reaches the exit code from inside this very lane — `upgrade`, the lane that
    // inherited the #735 exit 2 — so a defect here is still exit 2 while a `chmod` is not.
    [<Fact>]
    let ``an effect the edge cannot perform is still a toolDefect`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = TestSupport.tempDirectory ()
            let locked = Path.Combine(root, "locked")
            Directory.CreateDirectory locked |> ignore
            File.SetUnixFileMode(locked, UnixFileMode.UserRead ||| UnixFileMode.UserExecute)

            try
                let result =
                    FS.GG.SDD.Commands.CommandEffects.interpret root false (CreateDirectory "locked/child")

                Assert.False result.Succeeded

                match result.Diagnostic with
                | Some diagnostic ->
                    Assert.Equal("toolDefect", diagnostic.Id)
                    Assert.True diagnostic.IsToolDefect
                | None -> failwith "expected a toolDefect diagnostic"
            finally
                File.SetUnixFileMode(
                    locked,
                    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                )

    [<Fact>]
    let ``a genuine tool defect in the remediation lane still exits 2`` () =
        let blocked = Drift.expectedArtifactPaths |> List.head
        let present = Drift.expectedArtifactPaths |> List.filter (fun p -> p <> blocked)
        let root = makeFixture (Some farBehindMinimum) present true
        // A directory where the re-seed's file must be written: the WriteFile fails deterministically.
        Directory.CreateDirectory(Path.Combine(root, blocked.Replace('/', Path.DirectorySeparatorChar)))
        |> ignore

        let report = upgradeYes root
        Assert.Contains(report.Diagnostics, (fun d -> d.IsToolDefect))
        Assert.Equal(2, exitCode report)

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
