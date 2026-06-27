namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open Xunit

/// SC-005 / FR-002 / FR-007: generic SDD source and the generic-contract tests carry no
/// provider-specific identifier, and the scaffold source path special-cases no lifecycle
/// value. The reference provider's specifics live only in its own repo and in
/// provider-owned fixtures. This guard file itself names the deny-list tokens and the
/// planted-violation literals, so it is intentionally excluded from every scanned surface.
module ScaffoldGuardTests =
    // C1: tokens that would only appear if a specific provider leaked into generic SDD.
    let private forbiddenTokens = [ "fs-gg-ui"; "FS.GG.Rendering" ]

    // C2: the one collision-free lifecycle-*value* token. `spec-kit` never appears in clean
    // SDD source; the values `sdd`/`none` are rejected (they collide with `Ownership = "sdd"`,
    // the `FS.GG.SDD` identifier, and `None`/`(none)` rendering). research Decision 4 & 9.
    let private lifecycleValueTokens = [ "spec-kit" ]

    /// The single offender-detector that guards the tree AND is exercised by the
    /// planted-violation proof (C3): every forbidden token found in `text`, each located
    /// as `"{location}: {token}"` (the SC-005 "names the offending location" shape).
    let private offenders (tokens: string list) (location: string) (text: string) =
        tokens
        |> List.filter (fun token -> text.Contains(token, StringComparison.OrdinalIgnoreCase))
        |> List.map (fun token -> $"{location}: {token}")

    let private scanFiles (tokens: string list) (paths: string list) =
        paths |> List.collect (fun path -> offenders tokens path (File.ReadAllText path))

    let private sourceFiles () =
        let srcRoot = Path.Combine(TestSupport.repoRoot, "src")

        Directory.EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
        |> Seq.filter (fun path ->
            let extension = Path.GetExtension path
            extension = ".fs" || extension = ".fsi")
        |> Seq.toList

    let private genericContractTestFiles () =
        let testsRoot = Path.Combine(TestSupport.repoRoot, "tests")

        // This guard file itself names the forbidden tokens (as the deny-list), so it
        // is intentionally excluded from the scan.
        [ "FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs"
          "FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs"
          "FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs" ]
        |> List.map (fun relative -> Path.Combine(testsRoot, relative.Replace('/', Path.DirectorySeparatorChar)))
        |> List.filter File.Exists

    /// 034 (T021 / FR-009): the opt-in composition-acceptance project must be provably free of
    /// any rendering package id / template id / path / docs URL — the real provider identity
    /// lives only in the external registry, never in the acceptance code.
    let private acceptanceProjectFiles () =
        let acceptanceRoot =
            Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Acceptance.Tests")

        if Directory.Exists acceptanceRoot then
            Directory.EnumerateFiles(acceptanceRoot, "*.fs", SearchOption.AllDirectories) |> Seq.toList
        else
            []

    /// C2 scope: the curated scaffold-source union — `HandlersScaffold.fs` plus the
    /// projection files that render the scaffold report. NOT repo-wide (research Decision 9).
    let private scaffoldSourceFiles () =
        [ "src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs"
          "src/FS.GG.SDD.Commands/CommandSerialization.fs"
          "src/FS.GG.SDD.Commands/CommandRendering.fs"
          "src/FS.GG.SDD.Commands/CommandReports.fs"
          "src/FS.GG.SDD.Cli/Rendering.fs" ]
        |> List.map (fun relative -> Path.Combine(TestSupport.repoRoot, relative.Replace('/', Path.DirectorySeparatorChar)))
        |> List.filter File.Exists

    // ---------- C1 (T020): provider-identifier deny-list ----------

    [<Fact>]
    let ``generic SDD source contains no provider-specific identifiers`` () =
        let found = scanFiles forbiddenTokens (sourceFiles ())
        Assert.True(List.isEmpty found, "Provider-specific tokens leaked into generic SDD source: " + String.Join("; ", found))

    [<Fact>]
    let ``generic scaffold contract tests contain no provider-specific identifiers`` () =
        let found = scanFiles forbiddenTokens (genericContractTestFiles ())
        Assert.True(List.isEmpty found, "Provider-specific tokens leaked into generic-contract tests: " + String.Join("; ", found))

    [<Fact>]
    let ``composition-acceptance project contains no provider-specific identifiers`` () =
        let found = scanFiles forbiddenTokens (acceptanceProjectFiles ())
        Assert.True(List.isEmpty found, "Provider-specific tokens leaked into the composition-acceptance project: " + String.Join("; ", found))

    // ---------- C2 (T021): scoped lifecycle-value scan ----------

    [<Fact>]
    let ``scaffold source path special-cases no lifecycle value`` () =
        let found = scanFiles lifecycleValueTokens (scaffoldSourceFiles ())
        Assert.True(List.isEmpty found, "Lifecycle-value special-casing leaked into scaffold source: " + String.Join("; ", found))

    // ---------- C3 (T022): planted-violation proof ----------

    [<Fact>]
    let ``leak scan catches and locates a planted provider identifier`` () =
        // A synthetic source string that special-cases the reference provider.
        let planted = "let templatePackage = \"" + "FS.GG." + "Rendering.Templates\""
        let found = offenders forbiddenTokens "planted-source.fs" planted
        Assert.NotEmpty(found)
        Assert.Contains("planted-source.fs: FS.GG.Rendering", found)

    [<Fact>]
    let ``leak scan catches and locates a planted lifecycle-value special-case`` () =
        // A synthetic source string that branches on a lifecycle value.
        let planted = "if author.lifecycle = \"" + "spec" + "-kit\" then useSpecKitTemplate () else useDefault ()"
        let found = offenders lifecycleValueTokens "planted-source.fs" planted
        Assert.NotEmpty(found)
        Assert.Contains("planted-source.fs: spec-kit", found)
