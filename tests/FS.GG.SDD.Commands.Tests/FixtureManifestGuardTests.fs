namespace FS.GG.SDD.Commands.Tests

open System.IO
open Xunit

/// Feature 067 / FR-004: the lifecycle-command fixture manifests must stay honest. Before this
/// feature, 106 of 107 `manifest.yml` files were consumed by nothing — authoritative-looking
/// documentation that no test exercised. They were deleted; only `deterministic-report` remains
/// (its directory is a real Init dry-run root in CommandReportJsonTests). This guard fails if an
/// unconsumed manifest reaccumulates.
module FixtureManifestGuardTests =

    let private repoRoot = TestSupport.repoRoot

    let private lifecycleCommandsDir =
        Path.Combine(repoRoot, "tests", "fixtures", "lifecycle-commands")

    /// Test source files that could reference a fixture directory, excluding this guard (so the
    /// guard does not count itself as the consumer).
    let private testSources =
        Directory.EnumerateFiles(Path.Combine(repoRoot, "tests"), "*.fs", SearchOption.AllDirectories)
        |> Seq.filter (fun path -> not (path.EndsWith("FixtureManifestGuardTests.fs")))
        |> Seq.map File.ReadAllText
        |> String.concat "\n"

    /// Non-null path segment (Path.GetDirectoryName / GetFileName are nullable under `Nullable enable`).
    let private nonNull (value: string | null) =
        value |> Option.ofObj |> Option.defaultValue ""

    let private manifestDirs () =
        if Directory.Exists lifecycleCommandsDir then
            Directory.EnumerateFiles(lifecycleCommandsDir, "manifest.yml", SearchOption.AllDirectories)
            |> Seq.map (fun manifest -> manifest |> Path.GetDirectoryName |> nonNull |> Path.GetFileName |> nonNull)
            |> Seq.toList
        else
            []

    // FR-004 / SC-002: every remaining lifecycle-command manifest is consumed — its directory name
    // is referenced by an executing (non-guard) test. An orphan makes this fail with its name.
    // The reference must be the directory name as a **quoted string literal** (how fixtures are
    // named, e.g. `Path.Combine(..., "lifecycle-commands", "deterministic-report")`), not a bare
    // substring — so a short/common name (`verify`, `ship`) can't be incidentally "consumed" by
    // matching unrelated identifier text like `runVerify`.
    [<Fact>]
    let ``no orphaned lifecycle-command fixture manifest`` () =
        let isReferenced (name: string) =
            testSources.Contains(sprintf "\"%s\"" name)

        let orphans = manifestDirs () |> List.filter (isReferenced >> not)

        Assert.True(
            List.isEmpty orphans,
            $"""Unconsumed lifecycle-command fixture manifest(s) — no test references these directories
(wire them into a test that verifies their behavior, or delete them): {String.concat ", " orphans}"""
        )

    // The one remaining manifest is coherent and genuinely consumed here: its declared fixture id
    // matches its directory, so it is a real, checked fixture rather than dead documentation.
    [<Fact>]
    let ``deterministic-report manifest is present and self-consistent`` () =
        let manifest =
            Path.Combine(lifecycleCommandsDir, "deterministic-report", "manifest.yml")

        Assert.True(File.Exists manifest, "Expected the deterministic-report fixture manifest to exist.")
        let text = File.ReadAllText manifest
        Assert.Contains("id: deterministic-report", text)
