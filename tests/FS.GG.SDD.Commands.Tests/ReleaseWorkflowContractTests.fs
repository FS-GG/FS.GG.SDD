namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open Xunit

module ReleaseWorkflowContractTests =
    let private workflow =
        Path.Combine(TestSupport.repoRoot, ".github", "workflows", "release.yml")
        |> File.ReadAllText

    let private contract =
        Path.Combine(TestSupport.repoRoot, "specs", "044-publish-cli-tool", "contracts", "release-workflow.md")
        |> File.ReadAllText

    let private count (needle: string) (text: string) =
        let mutable found = 0
        let mutable offset = 0

        while offset < text.Length do
            let index = text.IndexOf(needle, offset, StringComparison.Ordinal)

            if index < 0 then
                offset <- text.Length
            else
                found <- found + 1
                offset <- index + needle.Length

        found

    [<Fact>]
    let ``release publishes the independently consumable artifacts package to both feeds`` () =
        Assert.Equal(1, count "\n  publish-artifacts:\n" workflow)
        Assert.Contains("needs: [resolve-versions, artifacts-tests]", workflow)
        Assert.Contains("target: tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj", workflow)
        Assert.Contains("run: tests/fixtures/typed-specifications/run-clean-consumer.sh", workflow)
        Assert.Contains("artifacts_version: ${{ steps.ver.outputs.artifacts_version }}", workflow)
        Assert.Contains("$artifacts_version\" != \"$cli_version", workflow)

        let start = workflow.IndexOf("\n  publish-artifacts:\n", StringComparison.Ordinal)
        let finish = workflow.IndexOf("\n  publish-cli:\n", start, StringComparison.Ordinal)
        Assert.True(start >= 0 && finish > start, "publish-artifacts must be a distinct job before publish-cli")

        let job = workflow.Substring(start, finish - start)
        Assert.Contains("dotnet pack src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj", job)
        Assert.Contains("FS.GG.SDD.Artifacts.*.nupkg", job)
        Assert.Equal(2, count "dotnet nuget push" job)
        Assert.Equal(2, count "dotnet nuget push \"artifacts/packages/FS.GG.SDD.Artifacts.*.nupkg\"" job)
        Assert.DoesNotContain("dotnet nuget push \"artifacts/packages/FS.GG.SDD.Cli.*.nupkg\"", job)

        let orgFeed =
            job.IndexOf("https://nuget.pkg.github.com/FS-GG/index.json", StringComparison.Ordinal)

        let publicFeed =
            job.IndexOf("https://api.nuget.org/v3/index.json", StringComparison.Ordinal)

        Assert.True(orgFeed >= 0 && publicFeed > orgFeed, "the org feed must be pushed before nuget.org")
        Assert.Contains("three independently consumable packages", contract)
        Assert.Contains("| `publish-artifacts` |", contract)
        Assert.Contains("two exact Artifacts pushes", contract)
