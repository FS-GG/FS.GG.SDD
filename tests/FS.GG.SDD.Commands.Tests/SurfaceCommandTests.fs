namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
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
