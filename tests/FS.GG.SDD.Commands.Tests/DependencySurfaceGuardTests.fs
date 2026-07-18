namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open Xunit

/// Feature 105, Phase 2 (FR-009 / SC-005): generic SDD's `dependency-surface` source embeds no
/// package id, feed url, or framework symbol literal — the package/version to capture is authored
/// input (a `--param`, or a committed capture's path), never a value baked into the tooling. This
/// guard file itself names the deny-list tokens, so it is excluded from the scanned surface.
module DependencySurfaceGuardTests =
    // Tokens that would only appear if a specific framework package leaked into generic SDD: the
    // RM2 incident's package/symbols, and the family it belongs to.
    let private forbiddenTokens =
        [ "SkiaViewer"
          "FS.GG.UI"
          "fs-gg-ui"
          "runAppWithPersistence"
          "runAppWithAudioAndPersistence" ]

    // The curated dependency-surface source union: the capture model + surface-read, the edge that
    // reflects a restored package, the handler, and the two projection files.
    let private dependencySurfaceSourceFiles () =
        [ "src/FS.GG.SDD.Artifacts/DependencySurface.fs"
          "src/FS.GG.SDD.Artifacts/DependencySurface.fsi"
          "src/FS.GG.SDD.Commands/CommandEffects.fs"
          "src/FS.GG.SDD.Commands/CommandWorkflow/HandlersDependencySurface.fs"
          "src/FS.GG.SDD.Commands/CommandSerialization.fs"
          "src/FS.GG.SDD.Commands/CommandRendering.fs" ]
        |> List.map (fun relative ->
            Path.Combine(TestSupport.repoRoot, relative.Replace('/', Path.DirectorySeparatorChar)))
        |> List.filter File.Exists

    let private offenders (location: string) (text: string) =
        forbiddenTokens
        |> List.filter (fun token -> text.Contains(token, StringComparison.OrdinalIgnoreCase))
        |> List.map (fun token -> $"{location}: {token}")

    [<Fact>]
    let ``dependency-surface source embeds no package, feed, or symbol literal`` () =
        let found =
            dependencySurfaceSourceFiles ()
            |> List.collect (fun path -> offenders path (File.ReadAllText path))

        Assert.True(
            List.isEmpty found,
            "Package/feed/symbol literals leaked into generic dependency-surface source: "
            + String.Join("; ", found)
        )

    // The deny-list itself must be non-trivial, or the guard would pass vacuously (mirrors the
    // scaffold guard's planted-violation discipline).
    [<Fact>]
    let ``the guard detects a planted violation`` () =
        let planted = offenders "planted" "let x = \"FS.GG.UI.SkiaViewer\""
        Assert.NotEmpty planted
