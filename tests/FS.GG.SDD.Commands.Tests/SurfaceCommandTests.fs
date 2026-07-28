namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Runtime.InteropServices
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.Commands.Internal
open Xunit

/// `fsgg-sdd surface` command tests (feature 086). Real-filesystem fixtures: authored `.fsi`
/// signatures under `src/` and their committed baselines under `docs/api-surface/`. `--check` is
/// read-only and exits 1 on drift; `--update` refreshes the baselines and exits 0.
module SurfaceCommandTests =
    open TestSupport

    // A tiny but real F# signature body; the `ret` type distinguishes drift.
    let private signature ret =
        $"namespace Foo\nmodule Bar =\n    val baz: int -> {ret}\n"

    let private surfaceReport update root =
        { request Surface root with
            SurfaceUpdate = update }
        |> runRequest

    let private summaryOf (report: CommandReport) =
        match report.Surface with
        | Some summary -> summary
        | None -> failwith "expected a surface summary"

    /// One matched source/baseline pair.
    let private coherentFixture () =
        let root = tempDirectory ()
        writeRelative root "src/Foo/Bar.fsi" (signature "int")
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature "int")
        root

    // ---- US1: --check gates on drift -------------------------------------------------

    [<Fact>]
    let ``check on a coherent workspace reports matched and exits 0`` () =
        let root = coherentFixture ()
        let report = surfaceReport false root
        let summary = summaryOf report
        Assert.Equal("check", summary.Mode)
        Assert.Equal(1, summary.CheckedCount)
        Assert.True summary.IsCoherent
        Assert.Empty summary.MissingBaselinePaths
        Assert.Empty summary.DriftedSourcePaths
        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)

    [<Fact>]
    let ``check names a missing baseline and a drifted baseline and exits 1`` () =
        let root = coherentFixture ()
        // Drift the existing baseline, and add a source with no baseline at all.
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature "string")
        writeRelative root "src/Foo/Extra.fsi" (signature "unit")
        let report = surfaceReport false root
        let summary = summaryOf report
        Assert.Equal(2, summary.CheckedCount)
        Assert.False summary.IsCoherent
        Assert.Equal<string list>([ "docs/api-surface/Foo/Extra.fsi" ], summary.MissingBaselinePaths)
        Assert.Equal<string list>([ "src/Foo/Bar.fsi" ], summary.DriftedSourcePaths)
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(1, exitCodeForReport report)
        // The drift diagnostic is a plain user-input error (exit 1), never a tool defect (exit 2).
        Assert.Contains(report.Diagnostics, fun d -> d.Id = "surface.drift")
        Assert.DoesNotContain(report.Diagnostics, fun d -> d.IsToolDefect)

    [<Fact>]
    let ``check writes zero files — the tree is byte-identical before and after`` () =
        let root = coherentFixture ()
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature "string") // force drift

        let before =
            Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            |> Array.map (fun f -> f, File.ReadAllText f)

        let report = surfaceReport false root
        Assert.Empty report.ChangedArtifacts

        let after =
            Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            |> Array.map (fun f -> f, File.ReadAllText f)

        Assert.Equal<(string * string) array>(before, after)

    [<Fact>]
    let ``check ignores .fsi under obj and bin (generated signatures are not the public surface)`` () =
        let root = coherentFixture ()
        writeRelative root "src/Foo/obj/Debug/Generated.fsi" (signature "obj")
        writeRelative root "src/Foo/bin/Debug/Generated.fsi" (signature "bin")
        let summary = summaryOf (surfaceReport false root)
        Assert.Equal(1, summary.CheckedCount) // only the authored src/Foo/Bar.fsi
        Assert.True summary.IsCoherent

    [<Fact>]
    let ``empty source tree is coherent and exits 0`` () =
        let root = tempDirectory ()
        let report = surfaceReport false root
        let summary = summaryOf report
        Assert.Equal(0, summary.CheckedCount)
        Assert.True summary.IsCoherent
        Assert.Equal(0, exitCodeForReport report)

    [<Fact>]
    let ``check json is deterministic across runs`` () =
        let root = coherentFixture ()
        Assert.Equal(serializeReport (surfaceReport false root), serializeReport (surfaceReport false root))

    // ---- US2: --update refreshes the baselines ---------------------------------------

    [<Fact>]
    let ``update writes the missing and drifted baselines and exits 0`` () =
        let root = coherentFixture ()
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature "string") // drifted
        writeRelative root "src/Foo/Extra.fsi" (signature "unit") // missing baseline
        let report = surfaceReport true root
        let summary = summaryOf report
        Assert.Equal("update", summary.Mode)

        Assert.Equal<string list>(
            [ "docs/api-surface/Foo/Bar.fsi"; "docs/api-surface/Foo/Extra.fsi" ],
            summary.UpdatedBaselinePaths
        )

        Assert.Equal(0, exitCodeForReport report)
        // Update emits no blocking drift diagnostic — it reconciles instead.
        Assert.DoesNotContain(report.Diagnostics, fun d -> d.Id = "surface.drift")
        // Baselines are now byte-identical to their sources.
        Assert.Equal(readRelative root "src/Foo/Bar.fsi", readRelative root "docs/api-surface/Foo/Bar.fsi")
        Assert.Equal(readRelative root "src/Foo/Extra.fsi", readRelative root "docs/api-surface/Foo/Extra.fsi")
        // The reconciled tree passes a subsequent check.
        Assert.Equal(0, exitCodeForReport (surfaceReport false root))

    [<Fact>]
    let ``update leaves an already-matched baseline untouched (no spurious rewrite)`` () =
        let root = coherentFixture ()
        let report = surfaceReport true root
        let summary = summaryOf report
        Assert.Empty summary.UpdatedBaselinePaths
        // A no-op WriteFile is recorded as NoChange, never Update/Create.
        Assert.DoesNotContain(
            report.ChangedArtifacts,
            fun c -> c.Operation = ArtifactOperation.Update || c.Operation = ArtifactOperation.Create
        )

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)

    // ---- US3: orphans and root overrides ---------------------------------------------

    [<Fact>]
    let ``an orphan baseline is a warning that never changes the exit code`` () =
        let root = coherentFixture ()
        writeRelative root "docs/api-surface/Foo/Stale.fsi" (signature "orphan")
        let report = surfaceReport false root
        let summary = summaryOf report
        Assert.Equal<string list>([ "docs/api-surface/Foo/Stale.fsi" ], summary.OrphanBaselinePaths)
        Assert.True summary.IsCoherent // orphan alone is not drift
        Assert.Contains(report.Diagnostics, fun d -> d.Id = "surface.orphanBaseline")
        Assert.Equal(CommandOutcome.SucceededWithWarnings, report.Outcome)
        Assert.Equal(0, exitCodeForReport report)
        // The orphan is never removed.
        Assert.True(existsRelative root "docs/api-surface/Foo/Stale.fsi")

    [<Fact>]
    let ``root overrides are honored and echoed in the report`` () =
        let root = tempDirectory ()
        writeRelative root "lib/Pkg/Api.fsi" (signature "int")
        writeRelative root "docs/surface/Pkg/Api.fsi" (signature "int")

        let report =
            { request Surface root with
                Parameters = [ "sourceRoot", "lib"; "baselineRoot", "docs/surface" ] }
            |> runRequest

        let summary = summaryOf report
        Assert.Equal("lib", summary.SourceRoot)
        Assert.Equal("docs/surface", summary.BaselineRoot)
        Assert.Equal(1, summary.CheckedCount)
        Assert.True summary.IsCoherent
        Assert.Equal(0, exitCodeForReport report)

    // ---- Feature 087: additive-vs-breaking classification of the drifted set -----------

    // A two-member signature; `extra` is present only when `withExtra`, `ret` types the first `val`.
    let private signature2 ret withExtra =
        let extra = if withExtra then "    val extra: unit -> unit\n" else ""
        $"namespace Foo\nmodule Bar =\n    val baz: int -> {ret}\n{extra}"

    let private classificationOf (report: CommandReport) = (summaryOf report).Classification

    let private entryFor path (report: CommandReport) =
        (classificationOf report).Entries
        |> List.tryFind (fun e -> e.Path = path)
        |> Option.defaultWith (fun () -> failwithf "expected a classification entry for %s" path)

    [<Fact>]
    let ``a drifted baseline that only adds a member is classified additive (minor), still exits 1`` () =
        let root = coherentFixture ()
        // Baseline stays one-member; source gains a second member ⇒ additive.
        writeRelative root "src/Foo/Bar.fsi" (signature2 "int" true)
        let report = surfaceReport false root
        let entry = entryFor "src/Foo/Bar.fsi" report
        Assert.Equal("additive", entry.Classification)
        Assert.Equal("minor", entry.RecommendedBump)
        Assert.Empty entry.RemovedOrChangedMembers
        Assert.NotEmpty entry.AddedMembers
        Assert.Equal("additive", (classificationOf report).Verdict)
        Assert.Equal("minor", (classificationOf report).RecommendedBump)
        Assert.Equal(1, exitCodeForReport report) // classification never changes the exit code

    [<Fact>]
    let ``a drifted baseline that removes a member is classified breaking (major)`` () =
        let root = tempDirectory ()
        // Baseline has two members; source drops one ⇒ breaking.
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature2 "int" true)
        writeRelative root "src/Foo/Bar.fsi" (signature2 "int" false)
        let report = surfaceReport false root
        let entry = entryFor "src/Foo/Bar.fsi" report
        Assert.Equal("breaking", entry.Classification)
        Assert.Equal("major", entry.RecommendedBump)
        Assert.NotEmpty entry.RemovedOrChangedMembers
        Assert.Equal(1, exitCodeForReport report)

    [<Fact>]
    let ``a drifted baseline whose member signature changed is classified breaking`` () =
        let root = coherentFixture ()
        writeRelative root "src/Foo/Bar.fsi" (signature "string") // baseline is `int`
        let report = surfaceReport false root
        Assert.Equal("breaking", (entryFor "src/Foo/Bar.fsi" report).Classification)
        Assert.Equal(1, exitCodeForReport report)

    [<Fact>]
    let ``the run verdict is the most severe of a mixed additive+breaking run (breaking wins)`` () =
        let root = tempDirectory ()
        // Bar: additive (source gains a member). Qux: breaking (source drops a member).
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature2 "int" false)
        writeRelative root "src/Foo/Bar.fsi" (signature2 "int" true)
        writeRelative root "docs/api-surface/Foo/Qux.fsi" (signature2 "int" true)
        writeRelative root "src/Foo/Qux.fsi" (signature2 "int" false)
        let report = surfaceReport false root
        Assert.Equal("additive", (entryFor "src/Foo/Bar.fsi" report).Classification)
        Assert.Equal("breaking", (entryFor "src/Foo/Qux.fsi" report).Classification)
        Assert.Equal("breaking", (classificationOf report).Verdict)
        Assert.Equal("major", (classificationOf report).RecommendedBump)

    [<Fact>]
    let ``a missing baseline (new surface) carries no classification and does not inflate the verdict`` () =
        let root = coherentFixture ()
        writeRelative root "src/Foo/Extra.fsi" (signature "unit") // no baseline ⇒ new surface
        let report = surfaceReport false root
        let classification = classificationOf report
        // The only "drift" is a missing baseline; no shipped-surface mutation ⇒ verdict none.
        Assert.Empty classification.Entries
        Assert.Equal("none", classification.Verdict)
        Assert.Equal("none", classification.RecommendedBump)
        Assert.Equal(1, exitCodeForReport report) // still drift (missing baseline)

    [<Fact>]
    let ``a matched tree has no classification and a none verdict`` () =
        let report = surfaceReport false (coherentFixture ())
        Assert.Empty (classificationOf report).Entries
        Assert.Equal("none", (classificationOf report).Verdict)
        Assert.Equal(0, exitCodeForReport report)

    [<Fact>]
    let ``a formatting-only drift is classified cosmetic (no bump), still exits 1`` () =
        let root = coherentFixture ()
        // Same single member, but reordered whitespace + an added comment ⇒ member set unchanged.
        writeRelative root "src/Foo/Bar.fsi" "namespace Foo\nmodule Bar =\n    // a note\n    val baz:  int -> int\n"
        let report = surfaceReport false root
        let entry = entryFor "src/Foo/Bar.fsi" report
        Assert.Equal("cosmetic", entry.Classification)
        Assert.Equal("none", entry.RecommendedBump)
        Assert.Empty entry.AddedMembers
        Assert.Empty entry.RemovedOrChangedMembers
        Assert.Equal(1, exitCodeForReport report) // still byte-level drift

    [<Fact>]
    let ``an unparseable drifted source falls back to breaking`` () =
        let root = coherentFixture ()
        // Non-empty source that yields no member token (comment-only) ⇒ conservative breaking.
        writeRelative root "src/Foo/Bar.fsi" "// intentionally unreadable\n// no declarations here\n"
        let report = surfaceReport false root
        let entry = entryFor "src/Foo/Bar.fsi" report
        Assert.Equal("breaking", entry.Classification)
        Assert.True entry.UnparseableFallback
        Assert.Equal(1, exitCodeForReport report)

    // ---- Feature 094: the two load-bearing research claims, characterized ---------------
    //
    // These two tests assert properties of the *pre-094* handler. They are what makes the 094
    // design valid: R1 lets `--update` prompt with a real verdict (US2) without restructuring the
    // handler, and R2 lets an absent axis file degrade to `undeterminable` without an `Exists`
    // probe (US3). If either regresses, the version-bump prompt is built on sand.

    /// R1: `--update` computes the classification from the snapshots read *before* the baseline
    /// writes are applied, so the run that erases the drift still reports what the drift was.
    [<Fact>]
    let ``R1 — update classifies from the pre-write snapshots, not the reconciled tree`` () =
        let root = coherentFixture ()
        writeRelative root "src/Foo/Bar.fsi" (signature2 "int" true) // additive drift
        let report = surfaceReport true root
        let summary = summaryOf report

        // The write happened...
        Assert.Equal<string list>([ "docs/api-surface/Foo/Bar.fsi" ], summary.UpdatedBaselinePaths)
        Assert.Equal(signature2 "int" true, readRelative root "docs/api-surface/Foo/Bar.fsi")

        // ...and yet the verdict is the pre-write one. `none` here would mean 094's US2 is impossible.
        Assert.Equal("additive", summary.Classification.Verdict)
        Assert.Equal("minor", summary.Classification.RecommendedBump)
        Assert.Equal(0, exitCodeForReport report)

    /// R2: a `ReadFile` of a path that does not exist still *interprets* — it yields a result whose
    /// `Snapshot` is `None`. Absence is therefore observable from the pure handler without probing
    /// the filesystem, which is what makes the axis read a plain first-wave effect.
    [<Fact>]
    let ``R2 — a ReadFile of a nonexistent path interprets to a None snapshot`` () =
        let root = tempDirectory ()
        let missing = "Directory.Build.props" // never written
        let model, _ = init (request Surface root)
        let effect = ReadFile missing

        let interpreted =
            interpretAll root false [ effect ]
            |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model

        Assert.True(Foundation.hasInterpreted (Foundation.effectKey effect) interpreted)
        Assert.True((Foundation.snapshot missing interpreted).IsNone)

    // ---- Feature 094: the coherent-set version-bump prompt ------------------------------
    //
    // `surface` classifies a shipped-surface mutation (087) and now tells the operator what that
    // classification costs on the workspace's *declared* version axis. Advisory only: it never
    // changes an exit code and never writes the axis (ADR-0009 detect-and-remediate).

    /// A real `Directory.Build.props`-shaped document with one property.
    let private propsWith (property: string) (value: string) =
        $"<Project>\n  <PropertyGroup>\n    <{property}>{value}</{property}>\n  </PropertyGroup>\n</Project>\n"

    let private writeAxis root property value =
        writeRelative root "Directory.Build.props" (propsWith property value)

    let private bumpOf (report: CommandReport) = (summaryOf report).VersionBump

    let private versionWarnings (report: CommandReport) =
        report.Diagnostics
        |> List.filter (fun d -> d.Id = "surface.versionBumpRequired")

    /// Every effect the workflow plans across the whole run — the first wave plus everything
    /// produced by interpreting it. FR-012/SC-005 are asserted here rather than on file mtimes.
    let private allPlannedEffects request =
        let model, firstWave = init request

        let rec loop state pending accumulated =
            match pending with
            | [] -> accumulated
            | _ ->
                let results = interpretAll request.ProjectRoot request.DryRun pending

                let nextState, produced =
                    results
                    |> List.fold
                        (fun (currentState, acc) result ->
                            let updated, producedEffects = update (EffectInterpreted result) currentState
                            updated, acc @ producedEffects)
                        (state, [])

                loop nextState produced (accumulated @ produced)

        loop model firstWave firstWave

    /// Source gains a member the baseline lacks ⇒ additive ⇒ minor.
    let private additiveFixture () =
        let root = coherentFixture ()
        writeRelative root "src/Foo/Bar.fsi" (signature2 "int" true)
        root

    /// Baseline has a member the source dropped ⇒ breaking ⇒ major.
    let private breakingFixture () =
        let root = tempDirectory ()
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature2 "int" true)
        writeRelative root "src/Foo/Bar.fsi" (signature2 "int" false)
        root

    /// Byte-level drift with an unchanged member set ⇒ cosmetic ⇒ no bump.
    let private cosmeticFixture () =
        let root = coherentFixture ()
        writeRelative root "src/Foo/Bar.fsi" "namespace Foo\nmodule Bar =\n    // a note\n    val baz:  int -> int\n"
        root

    // ---- US1: the operator is told what the mutation costs (V1–V4) ---------------------

    /// V1: additive drift on a `0.8.0` axis ⇒ `minor`, suggesting `0.9.0`, with exactly one warning.
    [<Fact>]
    let ``V1 — additive drift prompts a minor bump off the resolved axis`` () =
        let root = additiveFixture ()
        writeAxis root "Version" "0.8.0"
        let report = surfaceReport false root
        let bump = bumpOf report

        Assert.Equal("Directory.Build.props", bump.AxisFile)
        Assert.Equal("Version", bump.AxisProperty)
        Assert.Equal("resolved", bump.AxisState)
        Assert.Equal(Some "0.8.0", bump.CurrentVersion)
        Assert.Equal("minor", bump.RequiredBump)
        Assert.Equal(Some "0.9.0", bump.SuggestedVersion)

        let warning = Assert.Single(versionWarnings report)
        Assert.Equal(FS.GG.SDD.Artifacts.Diagnostics.DiagnosticWarning, warning.Severity)
        Assert.False warning.IsToolDefect
        // FR-009: the verdict, the axis, the bump and both versions are named, as a prompt.
        Assert.Contains("additive", warning.Message)
        Assert.Contains("Directory.Build.props:Version", warning.Message)
        Assert.Contains("0.8.0", warning.Message)
        Assert.Contains("0.9.0", warning.Message)
        Assert.Contains("already applied", warning.Message)

    /// V2: breaking drift ⇒ `major`, `0.8.0` → `1.0.0` (minor and patch reset).
    [<Fact>]
    let ``V2 — breaking drift prompts a major bump and resets minor and patch`` () =
        let root = breakingFixture ()
        writeAxis root "Version" "0.8.0"
        let bump = bumpOf (surfaceReport false root)

        Assert.Equal("resolved", bump.AxisState)
        Assert.Equal("major", bump.RequiredBump)
        Assert.Equal(Some "1.0.0", bump.SuggestedVersion)

    /// V3 (I3, I4): a cosmetic reformat implies no release — the bump is the identity and the
    /// prompt stays silent, even though the tree is byte-drifted and still exits 1.
    [<Fact>]
    let ``V3 — cosmetic drift implies no bump and emits no warning`` () =
        let root = cosmeticFixture ()
        writeAxis root "Version" "0.8.0"
        let report = surfaceReport false root
        let bump = bumpOf report

        Assert.Equal("cosmetic", (summaryOf report).Classification.Verdict)
        Assert.Equal("none", bump.RequiredBump)
        Assert.Equal(bump.CurrentVersion, bump.SuggestedVersion) // I3: the identity bump
        Assert.Empty(versionWarnings report)
        Assert.Equal(1, exitCodeForReport report) // byte drift still blocks, as in 086

    /// V4: a coherent tree is inert — `none`, no warning, exit 0.
    [<Fact>]
    let ``V4 — a coherent tree implies no bump and emits no warning`` () =
        let root = coherentFixture ()
        writeAxis root "Version" "0.8.0"
        let report = surfaceReport false root
        let bump = bumpOf report

        Assert.Equal("resolved", bump.AxisState)
        Assert.Equal("none", bump.RequiredBump)
        Assert.Equal(Some "0.8.0", bump.SuggestedVersion)
        Assert.Empty(versionWarnings report)
        Assert.Equal(0, exitCodeForReport report)

    /// V5 (FR-013, SC-004): the prompt changes no exit code, in any tree state, in either mode.
    /// The expected codes are feature 086 + 087's, unchanged.
    [<Fact>]
    let ``V5 — the prompt never changes an exit code in either mode`` () =
        let cases =
            [ "additive", additiveFixture, 1
              "breaking", breakingFixture, 1
              "cosmetic", cosmeticFixture, 1
              "coherent", coherentFixture, 0 ]

        for name, fixture, expectedCheckExit in cases do
            let checkRoot = fixture ()
            writeAxis checkRoot "Version" "0.8.0"
            let checkExit = exitCodeForReport (surfaceReport false checkRoot)
            Assert.Equal((name, expectedCheckExit), (name, checkExit))

            // `--update` reconciles the drift, so it exits 0 in every state.
            let updateRoot = fixture ()
            writeAxis updateRoot "Version" "0.8.0"
            let updateExit = exitCodeForReport (surfaceReport true updateRoot)
            Assert.Equal((name, 0), (name, updateExit))

    // ---- US2: `--update` does not silently consume the governed event (V6–V8) -----------

    /// V6 (FR-011, SC-002): the run that *erases* the drift is the run that reports its cost.
    [<Fact>]
    let ``V6 — update rewrites the baselines and still prompts the major bump`` () =
        let root = breakingFixture ()
        writeAxis root "Version" "0.8.0"
        let report = surfaceReport true root
        let bump = bumpOf report

        Assert.Equal<string list>([ "docs/api-surface/Foo/Bar.fsi" ], (summaryOf report).UpdatedBaselinePaths)
        Assert.Equal(signature2 "int" false, readRelative root "docs/api-surface/Foo/Bar.fsi")
        Assert.Equal("major", bump.RequiredBump)
        Assert.Equal(Some "1.0.0", bump.SuggestedVersion)
        Assert.Single(versionWarnings report) |> ignore
        Assert.Equal(0, exitCodeForReport report)

    /// V7: a second `--update` over the now-reconciled tree is inert — no drift, no prompt.
    [<Fact>]
    let ``V7 — a second update is idempotent and emits no warning`` () =
        let root = breakingFixture ()
        writeAxis root "Version" "0.8.0"
        surfaceReport true root |> ignore
        let second = surfaceReport true root

        Assert.Equal("none", (summaryOf second).Classification.Verdict)
        Assert.Equal("none", (bumpOf second).RequiredBump)
        Assert.Empty(versionWarnings second)
        Assert.Equal(0, exitCodeForReport second)

    /// V8 (FR-012, SC-005, I5): `surface` never *writes* the version axis, in either mode. Asserted
    /// on the planned effect set — a structural argument is not a regression test, and file mtimes
    /// would not catch a planned-but-unexecuted write. The axis is read (that is the whole point);
    /// what must never exist is a mutating effect that targets it.
    [<Fact>]
    let ``V8 — no planned effect ever writes the version axis`` () =
        for update in [ false; true ] do
            let root = breakingFixture ()
            writeAxis root "Version" "0.8.0"

            let effects =
                allPlannedEffects
                    { request Surface root with
                        SurfaceUpdate = update }

            let mutating =
                effects
                |> List.filter (function
                    | WriteFile(path, _, _) -> Foundation.normalizeRelativePath path = "Directory.Build.props"
                    | CreateDirectory path
                    | SetExecutable path -> Foundation.normalizeRelativePath path = "Directory.Build.props"
                    | _ -> false)

            Assert.Empty mutating

            // The only effect that touches the axis at all is the read the prompt is derived from.
            let axisEffects =
                effects
                |> List.filter (fun effect -> (Foundation.effectKey effect).EndsWith "Directory.Build.props")

            Assert.All(
                axisEffects,
                fun effect ->
                    Assert.True(
                        (match effect with
                         | ReadFile _ -> true
                         | _ -> false)
                    )
            )

            // And the tree still holds the authored axis text, byte for byte.
            Assert.Equal(propsWith "Version" "0.8.0", readRelative root "Directory.Build.props")

    // ---- US3: an unresolvable axis degrades honestly (V9–V15, V19) ----------------------

    /// V9 (SC-007, FR-010): no axis file at all. The required bump still lands — it depends on the
    /// classification, not the axis — and the remediation names the override that would resolve it.
    [<Fact>]
    let ``V9 — an absent axis file is undeterminable and still reports the required bump`` () =
        let root = breakingFixture () // no Directory.Build.props written
        let report = surfaceReport false root
        let bump = bumpOf report

        Assert.Equal("undeterminable", bump.AxisState)
        Assert.Equal(None, bump.CurrentVersion)
        Assert.Equal(None, bump.SuggestedVersion)
        Assert.Equal("major", bump.RequiredBump) // never dead-ends

        let warning = Assert.Single(versionWarnings report)
        Assert.Contains("--param versionAxisFile", warning.Correction)
        Assert.Equal(1, exitCodeForReport report) // unchanged: the drift error, not the warning

    /// V10 (FR-010): the file exists but declares no such property.
    [<Fact>]
    let ``V10 — a missing property element is undeterminable and names the property override`` () =
        let root = breakingFixture ()
        writeAxis root "SomethingElse" "0.8.0"
        let report = surfaceReport false root
        let bump = bumpOf report

        Assert.Equal("undeterminable", bump.AxisState)
        Assert.Equal(None, bump.CurrentVersion)
        Assert.Equal("major", bump.RequiredBump)

        let warning = Assert.Single(versionWarnings report)
        Assert.Contains("--param versionAxisProperty", warning.Correction)

    /// V11: a present-but-nonsense value is `unparseable`, and the bad text is NOT echoed — the
    /// report must never present a non-version as a version.
    [<Fact>]
    let ``V11 — an unparseable axis value is reported without echoing the bad text`` () =
        let root = breakingFixture ()
        writeAxis root "Version" "not-a-version"
        let report = surfaceReport false root
        let bump = bumpOf report

        Assert.Equal("unparseable", bump.AxisState)
        Assert.Equal(None, bump.CurrentVersion)
        Assert.Equal(None, bump.SuggestedVersion)
        Assert.Equal("major", bump.RequiredBump)

        let warning = Assert.Single(versionWarnings report)
        Assert.DoesNotContain("not-a-version", warning.Message)
        Assert.DoesNotContain("not-a-version", warning.Correction)

    /// V12: malformed XML degrades to `undeterminable`; no `XmlException` escapes the handler.
    [<Fact>]
    let ``V12 — malformed props XML degrades to undeterminable without throwing`` () =
        let root = breakingFixture ()
        writeRelative root "Directory.Build.props" "<Project><PropertyGroup><Version>0.8.0</Project>"
        let report = surfaceReport false root

        Assert.Equal("undeterminable", (bumpOf report).AxisState)
        Assert.Equal("major", (bumpOf report).RequiredBump)
        Assert.Equal(1, exitCodeForReport report)

    /// V13: a prerelease triple is not the `major.minor.patch` grammar ⇒ `unparseable`.
    [<Fact>]
    let ``V13 — a prerelease version is unparseable`` () =
        let root = breakingFixture ()
        writeAxis root "Version" "1.2.3-beta"
        Assert.Equal("unparseable", (bumpOf (surfaceReport false root)).AxisState)

    /// V14: the `.Trim()` is load-bearing for the usual pretty-printed element, and `XElement.Value`
    /// ignores comment nodes.
    [<Fact>]
    let ``V14 — surrounding whitespace and comments do not defeat the axis read`` () =
        let root = breakingFixture ()

        writeRelative
            root
            "Directory.Build.props"
            "<Project>\n  <PropertyGroup>\n    <Version>\n      0.8.0 <!-- pinned -->\n    </Version>\n  </PropertyGroup>\n</Project>\n"

        let bump = bumpOf (surfaceReport false root)
        Assert.Equal("resolved", bump.AxisState)
        Assert.Equal(Some "0.8.0", bump.CurrentVersion)
        Assert.Equal(Some "1.0.0", bump.SuggestedVersion)

    /// V15: two declarations of the axis property ⇒ the first in document order wins (deterministic).
    [<Fact>]
    let ``V15 — a duplicated axis property resolves to the first in document order`` () =
        let root = breakingFixture ()

        writeRelative
            root
            "Directory.Build.props"
            "<Project>\n  <PropertyGroup>\n    <Version>0.8.0</Version>\n  </PropertyGroup>\n  <PropertyGroup>\n    <Version>9.9.9</Version>\n  </PropertyGroup>\n</Project>\n"

        Assert.Equal(Some "0.8.0", (bumpOf (surfaceReport false root)).CurrentVersion)

    // ---- US4: a consumer declares a non-default axis (V16–V18) --------------------------

    /// V16: a product points `surface` at its own property. Generic SDD learns no axis name.
    [<Fact>]
    let ``V16 — a non-default axis property is honored and echoed`` () =
        let root = breakingFixture ()
        writeAxis root "FsGgAudioVersion" "2.3.1"

        let report =
            { request Surface root with
                Parameters = [ "versionAxisProperty", "FsGgAudioVersion" ] }
            |> runRequest

        let bump = bumpOf report
        Assert.Equal("FsGgAudioVersion", bump.AxisProperty)
        Assert.Equal("resolved", bump.AxisState)
        Assert.Equal(Some "2.3.1", bump.CurrentVersion)
        Assert.Equal(Some "3.0.0", bump.SuggestedVersion) // breaking ⇒ major

    /// V17: a non-default axis *file*, resolved relative to the workspace root.
    [<Fact>]
    let ``V17 — a non-default axis file is honored and echoed`` () =
        let root = breakingFixture ()
        writeRelative root "build/Versions.props" (propsWith "Version" "1.4.2")

        let report =
            { request Surface root with
                Parameters = [ "versionAxisFile", "build/Versions.props" ] }
            |> runRequest

        let bump = bumpOf report
        Assert.Equal("build/Versions.props", bump.AxisFile)
        Assert.Equal("resolved", bump.AxisState)
        Assert.Equal(Some "1.4.2", bump.CurrentVersion)
        Assert.Equal(Some "2.0.0", bump.SuggestedVersion)

    /// V18 (FR-003, SC-003): the constitutional guard. No product's version-axis name may appear in
    /// generic SDD source. This is a source-tree assertion, not a behavior test — it is what makes
    /// the feature safe to generalize. Fixture *test* files legitimately name `FsGgAudioVersion`
    /// (see V16), so the assertion is scoped to `src/` only.
    [<Fact>]
    let ``V18 — no product version-axis literal appears anywhere in src`` () =
        let sourceRoot = Path.Combine(repoRoot, "src")

        let offenders =
            Directory.GetFiles(sourceRoot, "*.fs*", SearchOption.AllDirectories)
            |> Array.filter (fun path ->
                let normalized = path.Replace('\\', '/')
                not (normalized.Contains "/obj/") && not (normalized.Contains "/bin/"))
            |> Array.filter (fun path ->
                System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText path, @"FsGg[A-Za-z]+Version"))

        Assert.Empty offenders

    /// V19a (FR-017): a relative path that climbs out of the workspace resolves to `undeterminable`
    /// and — the load-bearing half — plans **no read at all**. Nothing outside the root is opened.
    [<Fact>]
    let ``V19a — a parent-escaping axis path is undeterminable and plans no read`` () =
        let root = breakingFixture ()

        let escaping =
            { request Surface root with
                Parameters = [ "versionAxisFile", "../outside.props" ] }

        Assert.Equal("undeterminable", (bumpOf (runRequest escaping)).AxisState)

        Assert.DoesNotContain(
            allPlannedEffects escaping,
            fun effect ->
                match effect with
                | ReadFile path -> path.Contains "outside.props"
                | _ -> false
        )

    /// V19b (FR-017): NOT redundant with V19a. `normalizeRelativePath` ends in `.TrimStart('/')`, so
    /// a guard that normalizes *before* testing `Path.IsPathRooted` passes V19a and still happily
    /// opens `/etc/passwd`. The guard must run on the raw param; this row is what proves it does.
    [<Fact>]
    let ``V19b — an absolute axis path is undeterminable and plans no read`` () =
        let root = breakingFixture ()

        let escaping =
            { request Surface root with
                Parameters = [ "versionAxisFile", "/etc/passwd" ] }

        Assert.Equal("undeterminable", (bumpOf (runRequest escaping)).AxisState)

        Assert.DoesNotContain(
            allPlannedEffects escaping,
            fun effect ->
                match effect with
                | ReadFile path -> path.Contains "passwd"
                | _ -> false
        )

    // ---- Root containment: `sourceRoot`/`baselineRoot` may not escape the workspace ----------
    //
    // FS-GG/FS.GG.SDD#185. `surface` documents two roots that stay inside the workspace root, and
    // enforced neither. `escapesRoot` (feature 094, FR-017) is lifted here onto the two pre-existing
    // roots: an escaping root plans NO effect of any kind and blocks with `surface.rootEscape`.
    //
    // Each vector is asserted on the **planned effect set**, not on the report alone: a guard that
    // suppressed the summary while still planning the read would pass a report-only assertion and
    // still open the file.

    let private surfaceWith update parameters root =
        { request Surface root with
            SurfaceUpdate = update
            Parameters = parameters }

    let private rootEscapes (report: CommandReport) =
        report.Diagnostics |> List.filter (fun d -> d.Id = "surface.rootEscape")

    // Both modes, both params, both vectors. Asserting `allPlannedEffects` is *empty* is strictly
    // stronger than checking no effect path contains the value: it rejects EVERY planned effect —
    // read, enumerate, and (under `--update`) write — so a guard that suppressed the summary while
    // still planning any effect fails here. The `--update` rows also carry forced drift, so a
    // regression that restored planning would certainly plan a write and redden this test.
    //
    // The `/etc` × `baselineRoot` row is the asymmetric one: `baselinePathFor` normalizes its
    // argument, so `TrimStart('/')` already contained the *write* — but never the enumerate/read.
    // Only a raw-param guard closes both, which is what "no effect at all" proves.
    [<Theory>]
    [<InlineData(false, "sourceRoot", "../OUTSIDE")>]
    [<InlineData(false, "sourceRoot", "/etc")>]
    [<InlineData(false, "baselineRoot", "../OUTSIDE")>]
    [<InlineData(false, "baselineRoot", "/etc")>]
    [<InlineData(true, "baselineRoot", "../OUTSIDE")>]
    [<InlineData(true, "baselineRoot", "/etc")>]
    [<InlineData(true, "sourceRoot", "../OUTSIDE")>]
    let ``an escaping root blocks, names the param, and plans no effect`` (update: bool) (key: string) (value: string) =
        let root = coherentFixture ()
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature "string") // force drift, so `--update` would plan a write
        let escaping = surfaceWith update [ key, value ] root
        let report = runRequest escaping

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(1, exitCodeForReport report)

        // A user-input error (exit 1), never a tool defect (exit 2).
        Assert.DoesNotContain(report.Diagnostics, fun d -> d.IsToolDefect)

        let escapes = rootEscapes report
        Assert.Single escapes |> ignore
        Assert.Contains(key, escapes.Head.Message)
        Assert.Contains(value, escapes.Head.Message)

        // No summary is invented for a workspace the command refused to look at.
        Assert.True(Option.isNone report.Surface, "an escaping root must not produce a surface summary")

        // The load-bearing half: nothing is planned at all — no read, no enumerate, no write.
        Assert.Empty(allPlannedEffects escaping)

    /// The report-level guard is not the containment proof — the filesystem is. `--update` with an
    /// escaping baseline root must leave the parent of the workspace untouched.
    ///
    /// The workspace is nested one level inside its own private parent: the shared per-run temp root
    /// is written to concurrently by every other test, so snapshotting it would be a race, not an
    /// assertion. `../OUTSIDE` from here lands in `parent`, which only this test owns.
    [<Fact>]
    let ``update — an escaping baselineRoot writes nothing outside the workspace root`` () =
        let parent = tempDirectory ()
        let root = Path.Combine(parent, "ws")
        Directory.CreateDirectory root |> ignore
        writeRelative root "src/Foo/Bar.fsi" (signature "int")
        writeRelative root "docs/api-surface/Foo/Bar.fsi" (signature "string") // force drift

        let snapshotOf dir =
            Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            |> Array.map (fun f -> f, File.ReadAllText f)
            |> Array.sortBy fst

        let before = snapshotOf parent
        surfaceWith true [ "baselineRoot", "../OUTSIDE" ] root |> runRequest |> ignore
        Assert.Equal<(string * string) array>(before, snapshotOf parent)
        Assert.False(Directory.Exists(Path.Combine(parent, "OUTSIDE")))

    /// Both roots escaping ⇒ one diagnostic per offending param — and the report is still a clean
    /// block (exit 1, no summary), not just a pair of messages.
    [<Fact>]
    let ``both roots escaping name both params and block`` () =
        let root = coherentFixture ()

        let escaping =
            surfaceWith false [ "sourceRoot", "/etc"; "baselineRoot", "../OUTSIDE" ] root

        let report = runRequest escaping

        let escapes = rootEscapes report
        Assert.Equal(2, List.length escapes)
        Assert.Contains(escapes, fun d -> d.Message.Contains "sourceRoot")
        Assert.Contains(escapes, fun d -> d.Message.Contains "baselineRoot")

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Equal(1, exitCodeForReport report)
        Assert.True(Option.isNone report.Surface)
        Assert.Empty(allPlannedEffects escaping)

    /// The guard is not overbroad: contained overrides still work exactly as before.
    [<Fact>]
    let ``a contained root override is unaffected by the guard`` () =
        let root = tempDirectory ()
        writeRelative root "lib/Foo/Bar.fsi" (signature "int")
        writeRelative root "surface/Foo/Bar.fsi" (signature "int")

        let report =
            surfaceWith false [ "sourceRoot", "lib"; "baselineRoot", "surface" ] root
            |> runRequest

        Assert.Empty(rootEscapes report)
        let summary = summaryOf report
        Assert.Equal(1, summary.CheckedCount)
        Assert.True summary.IsCoherent
        Assert.Equal(0, exitCodeForReport report)

    // ===================================================================================
    // FS.GG.SDD#745 (decision FS.GG.SDD#754) — `surface --check` is a REQUIRED gate on both
    // version axes, and it reported a PASS on a file it never read.
    //
    // `sourceText`/`baselineText` came from `snapshot`, whose `None` means ABSENT. `missing`
    // required `Option.isSome sourceText`; `drifted` required `Some, Some`. An unreadable source
    // `.fsi` was in NEITHER list, and `IsCoherent = isEmpty missing && isEmpty drifted` — so a
    // real API-surface drift shipped green behind one mode bit, with `checkedCount` still
    // counting the file as checked.
    //
    // The table below is #745 §1's, pinned column for column. It is the whole point of the row,
    // so it is asserted as a table rather than one assertion per fact: leg 3 differing from
    // leg 2 in `outcome`/`exit` alone would not have caught the original defect, whose signature
    // was `isCoherent: true` WITH `driftedSourcePaths: []` AND `checkedCount` unchanged.
    // ===================================================================================

    /// A source/baseline pair whose SOURCE is left at mode 000 for the duration of `body`.
    /// Restored in `finally`, so a failing assertion still leaves the temp tree deletable.
    let private withUnreadable (root: string) (relativePath: string) (body: unit -> unit) =
        let absolute =
            Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))

        File.SetUnixFileMode(absolute, enum<UnixFileMode> 0)

        try
            body ()
        finally
            File.SetUnixFileMode(absolute, enum<UnixFileMode> 0o644)

    /// Six matched source/baseline pairs — the shape the #745 reproduction was measured on
    /// (`src/FS.GG.Contracts/*.fsi`, `checkedCount: 6`).
    let private sixPairFixture () =
        let root = tempDirectory ()

        for index in 1..6 do
            writeRelative root $"src/Foo/Sig{index}.fsi" (signature "int")
            writeRelative root $"docs/api-surface/Foo/Sig{index}.fsi" (signature "int")

        root

    let private diagnosticIds (report: CommandReport) =
        report.Diagnostics |> List.map (fun d -> d.Id)

    [<Fact>]
    let ``FS.GG.SDD#745: an unreadable drifted source cannot report coherent — the exact table`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = sixPairFixture ()

            // Leg 1 — clean. Unchanged by #745, and asserted so the fix cannot be a blanket
            // "always incoherent".
            let clean = surfaceReport false root
            let cleanSummary = summaryOf clean
            Assert.Equal(CommandOutcome.NoChange, clean.Outcome)
            Assert.True cleanSummary.IsCoherent
            Assert.Equal(6, cleanSummary.CheckedCount)
            Assert.Empty cleanSummary.DriftedSourcePaths
            Assert.Equal(0, exitCodeForReport clean)

            // Leg 2 — real drift, readable. Also unchanged: the gate still works when it can look.
            writeRelative root "src/Foo/Sig1.fsi" (signature "string")
            let drifted = surfaceReport false root
            let driftedSummary = summaryOf drifted
            Assert.Equal(CommandOutcome.Blocked, drifted.Outcome)
            Assert.False driftedSummary.IsCoherent
            Assert.Equal(6, driftedSummary.CheckedCount)
            Assert.Equal<string list>([ "src/Foo/Sig1.fsi" ], driftedSummary.DriftedSourcePaths)
            Assert.Equal(1, exitCodeForReport drifted)

            // Leg 3 — the SAME drift, now unreadable. Measured before the fix:
            //   outcome succeededWithWarnings (on the #744 branch) / blocked+toolDefect (on main),
            //   isCoherent TRUE, driftedSourcePaths [], checkedCount 6.
            withUnreadable root "src/Foo/Sig1.fsi" (fun () ->
                let hidden = surfaceReport false root
                let hiddenSummary = summaryOf hidden

                // The verdict. This is the assertion the row exists for.
                Assert.False hiddenSummary.IsCoherent

                // CHECKED means READ. It reported 6 for five files it read and one it did not.
                Assert.Equal(5, hiddenSummary.CheckedCount)

                // Still not "drift" — the tool did not observe a difference, it failed to look.
                Assert.Empty hiddenSummary.DriftedSourcePaths

                // And not "missing" either (#745 AC4): the file is right there.
                Assert.Empty hiddenSummary.MissingBaselinePaths

                Assert.Equal(CommandOutcome.Blocked, hidden.Outcome)
                Assert.Equal(1, exitCodeForReport hidden)

                // Exit 1, never 2: a mode bit in the workspace is not a broken tool (AC5).
                Assert.DoesNotContain(hidden.Diagnostics, fun d -> d.IsToolDefect)

                // The finding names the file, and is distinguishable from drift.
                Assert.Contains("unreadableSubject", diagnosticIds hidden)
                Assert.Contains("unreadableFile", diagnosticIds hidden)
                Assert.DoesNotContain("surface.drift", diagnosticIds hidden)

                Assert.Contains(
                    hidden.Diagnostics,
                    fun d -> d.Id = "unreadableSubject" && List.contains "src/Foo/Sig1.fsi" d.RelatedIds
                ))

    /// The mirror-image half. An unreadable BASELINE blinds the comparison exactly as an
    /// unreadable source does — one side always compares equal — and it must not be reported as a
    /// baseline that does not exist (#745 AC4), which would point `--update` at creating a file
    /// that is already there.
    [<Fact>]
    let ``FS.GG.SDD#745: an unreadable baseline blocks and is never reported as missing`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = coherentFixture ()

            withUnreadable root "docs/api-surface/Foo/Bar.fsi" (fun () ->
                let report = surfaceReport false root
                let summary = summaryOf report

                Assert.False summary.IsCoherent
                Assert.Empty summary.MissingBaselinePaths
                Assert.Empty summary.DriftedSourcePaths
                Assert.Equal(1, exitCodeForReport report)
                Assert.Contains("unreadableSubject", diagnosticIds report))

    /// `--update` is the mode that RECONCILES drift, and it cannot reconcile bytes it could not
    /// read. An `--update` that quietly skipped the pair and exited 0 would report the baselines
    /// as refreshed when one of them was not — so the block is emitted in BOTH modes, and the
    /// unreadable pair is never named in `updatedBaselinePaths`.
    [<Fact>]
    let ``FS.GG.SDD#745: update blocks on an unreadable source and never claims to have written it`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = coherentFixture ()
            writeRelative root "src/Foo/Extra.fsi" (signature "unit")

            withUnreadable root "src/Foo/Extra.fsi" (fun () ->
                let report = surfaceReport true root
                let summary = summaryOf report

                Assert.False summary.IsCoherent
                Assert.DoesNotContain("docs/api-surface/Foo/Extra.fsi", summary.UpdatedBaselinePaths)
                Assert.False(existsRelative root "docs/api-surface/Foo/Extra.fsi")
                Assert.Equal(1, exitCodeForReport report)
                Assert.Contains("unreadableSubject", diagnosticIds report))

    /// The vacuous-pass edge, and the one an "absent ⇒ not checked" fold fails hardest: `sources`
    /// is derived from the source ROOT's listing, so a root that could not be opened yields zero
    /// signatures, zero drift and — before this — `isCoherent: true` over an entire tree.
    [<Fact>]
    let ``FS.GG.SDD#745: an unreadable source ROOT is not a coherent empty tree`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = coherentFixture ()
            let sourceRoot = Path.Combine(root, "src")
            File.SetUnixFileMode(sourceRoot, enum<UnixFileMode> 0)

            try
                let report = surfaceReport false root
                let summary = summaryOf report
                Assert.Equal(0, summary.CheckedCount)
                Assert.False summary.IsCoherent
                Assert.Equal(1, exitCodeForReport report)
                Assert.Contains("unreadableSubject", diagnosticIds report)
            finally
                File.SetUnixFileMode(
                    sourceRoot,
                    UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
                )

    /// #745 AC4: the finding must be visible in `--text` (from which `--rich` derives). Before
    /// this the plain projection carried only `diagnostics: <count>`, so the entire mechanism was
    /// invisible in the projection an operator actually reads.
    [<Fact>]
    let ``FS.GG.SDD#745: the unreadable finding is visible in the plain-text projection`` () =
        if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            ()
        else
            let root = coherentFixture ()

            withUnreadable root "src/Foo/Bar.fsi" (fun () ->
                let text = surfaceReport false root |> CommandRendering.renderText
                Assert.Contains("unreadableSubject:", text)
                Assert.Contains("unreadableFile:", text)
                Assert.Contains("src/Foo/Bar.fsi", text))
