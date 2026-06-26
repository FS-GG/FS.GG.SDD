namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open Xunit

/// SC-005 / FR-002: generic SDD source and the generic-contract tests carry no
/// provider-specific package id, template id, or docs URL. The reference provider's
/// specifics live only in its own repo and in provider-owned fixtures.
module ScaffoldGuardTests =
    // Tokens that would only appear if a specific provider leaked into generic SDD.
    let private forbiddenTokens = [ "fs-gg-ui"; "FS.GG.Rendering" ]

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

    [<Fact>]
    let ``generic SDD source contains no provider-specific identifiers`` () =
        let offenders =
            sourceFiles ()
            |> List.collect (fun path ->
                let text = File.ReadAllText path
                forbiddenTokens
                |> List.filter (fun token -> text.Contains(token, StringComparison.OrdinalIgnoreCase))
                |> List.map (fun token -> $"{path}: {token}"))

        Assert.True(List.isEmpty offenders, "Provider-specific tokens leaked into generic SDD source: " + String.Join("; ", offenders))

    [<Fact>]
    let ``generic scaffold contract tests contain no provider-specific identifiers`` () =
        let offenders =
            genericContractTestFiles ()
            |> List.collect (fun path ->
                let text = File.ReadAllText path
                forbiddenTokens
                |> List.filter (fun token -> text.Contains(token, StringComparison.OrdinalIgnoreCase))
                |> List.map (fun token -> $"{path}: {token}"))

        Assert.True(List.isEmpty offenders, "Provider-specific tokens leaked into generic-contract tests: " + String.Join("; ", offenders))
