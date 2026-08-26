namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open Xunit

module ReleaseWorkflowContractTests =
    let private workflow =
        Path.Combine(TestSupport.repoRoot, ".github", "workflows", "release.yml")
        |> File.ReadAllText

    let private gateWorkflow =
        Path.Combine(TestSupport.repoRoot, ".github", "workflows", "gate.yml")
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
        Assert.Contains("needs: [resolve-versions, artifacts-tests, cli-tests]", workflow)
        Assert.Contains("target: tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj", workflow)
        Assert.Contains("run: tests/fixtures/typed-specifications/run-clean-consumer.sh", workflow)
        Assert.Contains("artifacts_version: ${{ steps.ver.outputs.artifacts_version }}", workflow)
        Assert.Contains("$artifacts_version\" != \"$cli_version", workflow)

        let start = workflow.IndexOf("\n  publish-artifacts:\n", StringComparison.Ordinal)
        let finish = workflow.IndexOf("\n  publish-cli:\n", start, StringComparison.Ordinal)
        Assert.True(start >= 0 && finish > start, "publish-artifacts must be a distinct job before publish-cli")

        let job = workflow.Substring(start, finish - start)
        Assert.Contains("dotnet pack src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj", job)
        Assert.Contains("dotnet pack src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj", job)
        Assert.DoesNotContain("-p:Version=", job)
        Assert.DoesNotContain("-p:PackageVersion=", job)
        Assert.Contains("-p:RepositoryCommit=\"$GITHUB_SHA\"", job)
        Assert.Contains("FS.GG.SDD.Artifacts.*.nupkg", job)
        Assert.Contains("coherent-sdd-packages-${{ github.sha }}", job)
        Assert.DoesNotContain("dotnet nuget push", job)

        let publish = workflow.Substring(finish)
        Assert.Equal(4, count "dotnet nuget push" publish)
        Assert.Equal(2, count "dotnet nuget push \"artifacts/packages/FS.GG.SDD.Artifacts.*.nupkg\"" publish)
        Assert.Equal(2, count "dotnet nuget push \"artifacts/packages/FS.GG.SDD.Cli.*.nupkg\"" publish)
        Assert.Contains("Read back both feeds and compare every non-signature entry", publish)
        Assert.Contains("grep -v '^\\.signature\\.p7s$'", publish)
        Assert.Contains("diff -u \"$local_package.payloads\"", publish)
        Assert.Contains("Verify clean public installs", publish)
        Assert.Contains("Q2_PACKAGE_SOURCE: https://api.nuget.org/v3/index.json", publish)
        Assert.Contains("Q3_PACKAGE_SOURCE: https://api.nuget.org/v3/index.json", publish)
        Assert.Contains("kernel.apparmor_restrict_unprivileged_userns=0", publish)
        Assert.Contains("/usr/bin/unshare --user --map-root-user --net -- /usr/bin/true", publish)
        Assert.Contains("bash tests/quint-q3-typed-sdd-acceptance.sh", publish)
        Assert.Contains("quint-q3-public.junit.xml", publish)
        Assert.Contains("kernel.apparmor_restrict_unprivileged_userns=0", gateWorkflow)
        Assert.Contains("/usr/bin/unshare --user --map-root-user --net -- /usr/bin/true", gateWorkflow)

        let orgFeed =
            publish.IndexOf("https://nuget.pkg.github.com/FS-GG/index.json", StringComparison.Ordinal)

        let publicFeed =
            publish.IndexOf("https://api.nuget.org/v3/index.json", StringComparison.Ordinal)

        Assert.True(orgFeed >= 0 && publicFeed > orgFeed, "the org feed must be pushed before nuget.org")
        Assert.Contains("three independently consumable packages", contract)
        Assert.Contains("| `publish-artifacts` |", contract)
