namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts
open Xunit

/// CLI distribution slice (FR-011, buildable portion of SC-007): the CLI is a
/// .NET tool named `fsgg-sdd`, and `fsgg-sdd --version` reports the single
/// reconciled version source. The end-to-end `dotnet tool install` from a public
/// registry is validated manually (registry/signing are Governance/release-ops).
module ReleaseInstallTests =
    // The single <Version> source of truth lives in the repo-owned
    // Directory.Build.local.props (feature 037: the canonical Directory.Build.props
    // is synced byte-identically from FS-GG/.github and carries no repo-specific
    // <Version>; it imports this local override last).
    let directoryBuildPropsVersion () =
        let text =
            File.ReadAllText(Path.Combine(TestSupport.repoRoot, "Directory.Build.local.props"))

        (Regex.Match(text, @"<Version>([^<]+)</Version>")).Groups[1].Value.Trim()

    [<Fact>]
    let ``T021 the CLI project is packaged as the fsgg-sdd .NET tool`` () =
        let fsproj =
            Path.Combine(TestSupport.repoRoot, "src", "FS.GG.SDD.Cli", "FS.GG.SDD.Cli.fsproj")
            |> File.ReadAllText

        Assert.Contains("<PackAsTool>true</PackAsTool>", fsproj)
        Assert.Contains("<ToolCommandName>fsgg-sdd</ToolCommandName>", fsproj)

    [<Fact>]
    let ``T021 the version reported by --version is the single reconciled version source`` () =
        // `fsgg-sdd --version` prints currentGeneratorVersion().Version (see Program.fs);
        // it must equal the single <Version> in Directory.Build.local.props (FR-003/FR-011).
        Assert.Equal(directoryBuildPropsVersion (), SchemaVersion.currentGeneratorVersion().Version)

    [<Fact>]
    let ``T021 no per-project Version overrides the single source`` () =
        for project in [ "FS.GG.SDD.Artifacts"; "FS.GG.SDD.Commands"; "FS.GG.SDD.Cli" ] do
            let fsproj =
                Path.Combine(TestSupport.repoRoot, "src", project, project + ".fsproj")
                |> File.ReadAllText

            Assert.DoesNotContain("<Version>", fsproj)
