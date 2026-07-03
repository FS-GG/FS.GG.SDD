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
        paths
        |> List.collect (fun path -> offenders tokens path (File.ReadAllText path))

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
            Directory.EnumerateFiles(acceptanceRoot, "*.fs", SearchOption.AllDirectories)
            |> Seq.toList
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
        |> List.map (fun relative ->
            Path.Combine(TestSupport.repoRoot, relative.Replace('/', Path.DirectorySeparatorChar)))
        |> List.filter File.Exists

    // ---------- C1 (T020): provider-identifier deny-list ----------

    [<Fact>]
    let ``generic SDD source contains no provider-specific identifiers`` () =
        let found = scanFiles forbiddenTokens (sourceFiles ())

        Assert.True(
            List.isEmpty found,
            "Provider-specific tokens leaked into generic SDD source: "
            + String.Join("; ", found)
        )

    [<Fact>]
    let ``generic scaffold contract tests contain no provider-specific identifiers`` () =
        let found = scanFiles forbiddenTokens (genericContractTestFiles ())

        Assert.True(
            List.isEmpty found,
            "Provider-specific tokens leaked into generic-contract tests: "
            + String.Join("; ", found)
        )

    [<Fact>]
    let ``composition-acceptance project contains no provider-specific identifiers`` () =
        let found = scanFiles forbiddenTokens (acceptanceProjectFiles ())

        Assert.True(
            List.isEmpty found,
            "Provider-specific tokens leaked into the composition-acceptance project: "
            + String.Join("; ", found)
        )

    // ---------- C2 (T021): scoped lifecycle-value scan ----------

    [<Fact>]
    let ``scaffold source path special-cases no lifecycle value`` () =
        let found = scanFiles lifecycleValueTokens (scaffoldSourceFiles ())

        Assert.True(
            List.isEmpty found,
            "Lifecycle-value special-casing leaked into scaffold source: "
            + String.Join("; ", found)
        )

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
        let planted =
            "if author.lifecycle = \""
            + "spec"
            + "-kit\" then useSpecKitTemplate () else useDefault ()"

        let found = offenders lifecycleValueTokens "planted-source.fs" planted
        Assert.NotEmpty(found)
        Assert.Contains("planted-source.fs: spec-kit", found)

    // ===================================================================
    // 050 (T020 / FR-004 / SC-003): no provider-specific STARTER VALUE leaks into generic SDD
    // source, the generic-contract tests, or the SDD-owned scaffold-provider fixtures. The
    // canonical rendering registry (with its real default starter) is owned by FS.GG.Templates
    // and consumed only through the versioned provider contract — never embedded here. This
    // guard file names the deny-list tokens, so (as above) it is excluded from every scan.
    // ===================================================================

    // The bare starter value `game` never appears legitimately in this codebase. (`app` is NOT
    // a bare-word token here — it legitimately suffixes `fsgg-fixture-app` and names `App.fsproj`
    // — so `app`-as-starter is caught by the registry starter-value pattern below, not a word scan.)
    let private starterValueTokens = [ "ga" + "me" ]

    /// The SDD-owned scaffold-provider fixtures (registries + templates). These committed
    /// FIXTURES are SDD-owned and must carry no provider-specific starter value (FR-004).
    let private scaffoldFixtureFiles () =
        let fixturesRoot =
            Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "scaffold-provider")

        if Directory.Exists fixturesRoot then
            Directory.EnumerateFiles(fixturesRoot, "*.*", SearchOption.AllDirectories)
            |> Seq.toList
        else
            []

    /// A registry `default:`/`variant:`/`profile:`/`starter:` line whose VALUE is a known
    /// provider-specific starter (`app`/`game`) — the precise "app-as-starter" / "game-as-starter"
    /// shape, never matching `templateId: fsgg-fixture-app` or `App.fsproj`.
    let private starterValuePattern =
        System.Text.RegularExpressions.Regex(
            "(?im)^\\s*(default|variant|profile|starter)\\s*:\\s*("
            + "app"
            + "|"
            + "ga"
            + "me)\\s*$"
        )

    let private starterValueOffenders (location: string) (text: string) =
        if starterValuePattern.IsMatch text then
            [ $"{location}: starter-value" ]
        else
            []

    // ---------- C4 (050 T020): bare starter-value deny-list ----------

    [<Fact>]
    let ``generic SDD source contains no provider-specific starter value`` () =
        let found = scanFiles starterValueTokens (sourceFiles ())

        Assert.True(
            List.isEmpty found,
            "Provider-specific starter value leaked into generic SDD source: "
            + String.Join("; ", found)
        )

    [<Fact>]
    let ``generic scaffold contract tests contain no provider-specific starter value`` () =
        let found = scanFiles starterValueTokens (genericContractTestFiles ())

        Assert.True(
            List.isEmpty found,
            "Provider-specific starter value leaked into generic-contract tests: "
            + String.Join("; ", found)
        )

    [<Fact>]
    let ``SDD-owned scaffold fixtures carry no provider-specific starter value`` () =
        let bareWord = scanFiles starterValueTokens (scaffoldFixtureFiles ())

        let asValue =
            scaffoldFixtureFiles ()
            |> List.collect (fun path -> starterValueOffenders path (File.ReadAllText path))

        let found = bareWord @ asValue

        Assert.True(
            List.isEmpty found,
            "Provider-specific starter value leaked into SDD-owned scaffold fixtures: "
            + String.Join("; ", found)
        )

    // ---------- C5 (050 T020): planted-violation proof for both shapes ----------

    [<Fact>]
    let ``starter-value scan catches a planted bare token and a planted registry default`` () =
        // A bare-word starter token leaking into generic source.
        let plantedWord = "let defaultStarter = \"" + "ga" + "me\""
        Assert.NotEmpty(offenders starterValueTokens "planted-source.fs" plantedWord)

        // A registry declaring a provider-specific default starter (the literal #44 flip,
        // redirected to FS.GG.Templates — out of scope for SDD-owned fixtures).
        let plantedRegistry =
            "    parameters:\n      - key: variant\n        default: " + "ga" + "me\n"

        Assert.NotEmpty(starterValueOffenders "planted.providers.yml" plantedRegistry)
        // The pattern does NOT false-positive on a legitimate fixture template id.
        Assert.Empty(starterValueOffenders "ok.providers.yml" "    templateId: fsgg-fixture-app\n")
