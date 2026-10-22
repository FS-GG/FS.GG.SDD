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

    let private svgWorkspaceQuintAcceptance =
        Path.Combine(TestSupport.repoRoot, "tests", "quint-svg-workspace-installed-acceptance.sh")
        |> File.ReadAllText

    let private contract =
        Path.Combine(TestSupport.repoRoot, "specs", "044-publish-cli-tool", "contracts", "release-workflow.md")
        |> File.ReadAllText

    // The public job's credential boundary is an allowlist: exactly one secret reference, the
    // read-only workflow token, and no publisher or feed-read secret by any spelling.
    let private publicSecretBoundaryHolds (job: string) =
        let secretReferences =
            System.Text.RegularExpressions.Regex.Matches(job, @"\bsecrets\b").Count

        secretReferences = 1
        && job.Contains("secrets.GITHUB_TOKEN", StringComparison.Ordinal)
        && not (job.Contains("FSGG_PACKAGES_READ", StringComparison.Ordinal))
        && not (job.Contains("NUGET_API_KEY", StringComparison.Ordinal))

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

    let private assertPayloadReceiptMaps () =
        let verifier =
            Path.Combine(TestSupport.repoRoot, "scripts", "verify-package-payloads.py")
            |> File.ReadAllText

        Assert.Contains("if name != \".signature.p7s\"", verifier)
        Assert.Contains("if manifest != original:", verifier)
        Assert.Contains("Path(str(path) + \".entries\").write_text(", verifier)
        Assert.Contains("Path(str(path) + \".payloads\").write_text(", verifier)

    [<Fact>]
    let ``release publishes the independently consumable artifacts package to both feeds`` () =
        Assert.Equal(1, count "\n  publish-artifacts:\n" workflow)

        Assert.Contains(
            "needs: [resolve-versions, contracts-tests, artifacts-tests, commands-tests, cli-tests, knowledge-tests]",
            workflow
        )

        Assert.Contains("target: tests/FS.GG.SDD.Artifacts.Tests/FS.GG.SDD.Artifacts.Tests.fsproj", workflow)
        Assert.Contains("run: tests/fixtures/typed-specifications/run-clean-consumer.sh", workflow)
        Assert.Contains("artifacts_version: ${{ steps.ver.outputs.artifacts_version }}", workflow)
        Assert.Contains("all four SDD projects must share one coherent version", workflow)

        let start = workflow.IndexOf("\n  publish-artifacts:\n", StringComparison.Ordinal)

        let locate =
            workflow.IndexOf("\n  locate-artifacts:\n", start, StringComparison.Ordinal)

        let finish =
            workflow.IndexOf("\n  publish-cli:\n", locate, StringComparison.Ordinal)

        Assert.True(
            start >= 0 && locate > start && finish > locate,
            "candidate build, custody lookup, and publisher must remain distinct ordered jobs"
        )

        let job = workflow.Substring(start, locate - start)
        Assert.Contains("dotnet pack \"src/$id/$id.fsproj\"", job)
        Assert.Contains("done < scripts/sdd-release-packages.txt", job)

        Assert.Equal(
            [|
                "FS.GG.SDD.Artifacts"
                "FS.GG.SDD.Commands"
                "FS.GG.SDD.Cli"
                "FS.GG.SDD.Knowledge"
            |],
            File.ReadAllLines(Path.Combine(TestSupport.repoRoot, "scripts", "sdd-release-packages.txt"))
        )

        Assert.DoesNotContain("-p:Version=", job)
        Assert.DoesNotContain("-p:PackageVersion=", job)
        Assert.Contains("-p:RepositoryCommit=\"$GITHUB_SHA\"", job)
        Assert.Contains("packages=FS.GG.SDD.Artifacts,FS.GG.SDD.Commands,FS.GG.SDD.Cli,FS.GG.SDD.Knowledge", job)
        Assert.Contains("coherent-sdd-packages-${{ github.sha }}", job)
        Assert.Contains("needs.resolve-versions.outputs.push == 'false'", job)
        Assert.Contains("scripts/verify-release-candidate.sh", job)
        Assert.Contains("candidate.env", job)
        Assert.Contains("release-candidate/v4", job)
        Assert.Contains("--record-contracts", job)
        Assert.Contains("packages/dependencies/FS.GG.Contracts.", job)
        Assert.DoesNotContain("dotnet pack src/FS.GG.Contracts", workflow)
        Assert.DoesNotContain("dotnet nuget push \"artifacts/packages/FS.GG.Contracts", workflow)
        Assert.Contains("target: tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj", workflow)
        Assert.DoesNotContain("dotnet nuget push", job)

        let custody = workflow.Substring(locate, finish - locate)

        let readbackStart =
            workflow.IndexOf("\n  readback-cli:\n", finish, StringComparison.Ordinal)

        let qualificationStart =
            workflow.IndexOf("\n  qualify-public:\n", finish, StringComparison.Ordinal)

        Assert.True(
            finish < qualificationStart && qualificationStart < readbackStart,
            "publisher, credential-free public qualification, and read-only readback must remain distinct jobs"
        )

        let publish = workflow.Substring(finish, qualificationStart - finish)

        let qualification =
            workflow.Substring(qualificationStart, readbackStart - qualificationStart)

        Assert.Contains("event=workflow_dispatch&status=completed", custody)
        Assert.Contains("expected exactly one retained no-push candidate", custody)
        Assert.Contains("run-id: ${{ needs.locate-artifacts.outputs.candidate_run_id }}", publish)
        Assert.Contains("needs: [resolve-versions, locate-artifacts]", publish)
        Assert.Contains("Verify retained candidate identity and hashes before feed credentials", publish)
        Assert.Contains("scripts/verify-release-candidate.sh", publish)
        Assert.DoesNotContain("dotnet pack src/FS.GG.SDD.Artifacts", publish)
        Assert.DoesNotContain("dotnet pack src/FS.GG.SDD.Cli", publish)
        Assert.Equal(8, count "dotnet nuget push" publish)
        Assert.Equal(2, count "dotnet nuget push \"artifacts/packages/FS.GG.SDD.Commands.*.nupkg\"" publish)
        Assert.Equal(2, count "dotnet nuget push \"artifacts/packages/FS.GG.SDD.Artifacts.*.nupkg\"" publish)
        Assert.Equal(2, count "dotnet nuget push \"artifacts/packages/FS.GG.SDD.Cli.*.nupkg\"" publish)
        Assert.Equal(2, count "dotnet nuget push \"artifacts/packages/FS.GG.SDD.Knowledge.*.nupkg\"" publish)
        Assert.Contains("Read back both feeds and compare every non-signature entry", publish)
        Assert.Equal(1, count "python3 scripts/verify-package-payloads.py" publish)
        Assert.Contains("python3 scripts/verify-package-payloads.py \"$local_package\"", publish)

        Assert.Contains(
            "\"artifacts/feed-readback/$id.github.nupkg\" \\\n              \"artifacts/feed-readback/$id.nuget.nupkg\"",
            publish
        )

        Assert.Contains("artifacts/packages/*.nupkg.payloads", publish)
        Assert.Contains("artifacts/feed-readback/*.payloads", publish)
        Assert.Contains("artifacts/feed-readback/*.entries", publish)
        assertPayloadReceiptMaps ()
        Assert.Contains("needs: [resolve-versions, locate-artifacts, publish-cli]", qualification)
        Assert.Contains("contents: read", qualification)
        Assert.Contains("actions: read", qualification)
        Assert.Contains("packages: read", qualification)
        Assert.Contains("persist-credentials: false", qualification)
        Assert.DoesNotContain(": write", qualification)
        Assert.True(publicSecretBoundaryHolds qualification, "public job may reference only secrets.GITHUB_TOKEN")

        for extra in
            [
                "NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}"
                "FSGG_PACKAGES_READ_TOKEN: ${{ secrets.FSGG_PACKAGES_READ_TOKEN }}"
                "EXTRA: ${{ toJSON(secrets) }}"
            ] do
            let widened =
                qualification.Replace(
                    "GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}",
                    "GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}\n          " + extra
                )

            Assert.NotEqual<string>(qualification, widened)

            Assert.False(
                publicSecretBoundaryHolds widened,
                $"adding '{extra}' to the public job must fail the boundary"
            )

        Assert.Contains("| tee retained-disposition.json", custody)
        Assert.Contains("| tee retained-promotion-disposition.json", publish)
        Assert.DoesNotContain("NuGet/login", qualification)
        Assert.DoesNotContain("dotnet nuget push", qualification)
        Assert.Contains("receipt_artifact_id: ${{ steps.publish-receipt.outputs.artifact-id }}", publish)
        Assert.Contains("receipt_digest: ${{ steps.publish-receipt.outputs.artifact-digest }}", publish)
        Assert.Contains("ARTIFACT_ID: ${{ needs.publish-cli.outputs.receipt_artifact_id }}", qualification)
        Assert.Contains("ARTIFACT_SHA256: ${{ needs.publish-cli.outputs.receipt_digest }}", qualification)
        Assert.Contains("scripts/verify-publication-receipt.py", qualification)
        Assert.Contains("SOURCE: ${{ needs.locate-artifacts.outputs.candidate_source }}", qualification)
        Assert.Contains("scripts/verify-release-candidate.sh artifacts/qualified-receipt/packages", qualification)
        Assert.Equal(1, count "python3 scripts/verify-package-payloads.py" qualification)
        Assert.Contains("for id in Artifacts Commands Cli Knowledge; do", qualification)
        Assert.DoesNotContain("Verify clean public installs", publish)
        Assert.DoesNotContain("scripts/verify-knowledge-packages.sh", publish)
        Assert.DoesNotContain("tests/quint-svg-workspace-installed-acceptance.sh", publish)
        Assert.Contains("Verify clean public installs", qualification)
        Assert.Contains("Q2_PACKAGE_SOURCE: https://api.nuget.org/v3/index.json", qualification)
        Assert.Contains("Q3_PACKAGE_SOURCE: https://api.nuget.org/v3/index.json", qualification)
        Assert.Contains("kernel.apparmor_restrict_unprivileged_userns=0", qualification)
        Assert.Contains("/usr/bin/unshare --user --map-root-user --net -- /usr/bin/true", qualification)
        Assert.Contains("bash tests/quint-svg-workspace-installed-acceptance.sh", qualification)
        Assert.DoesNotContain("bash tests/quint-q3-typed-sdd-acceptance.sh", qualification)
        Assert.Contains("/usr/bin/unshare --user --map-root-user --net", svgWorkspaceQuintAcceptance)
        Assert.Contains("tests=\"16\" failures=\"0\"", svgWorkspaceQuintAcceptance)
        Assert.Contains("tests=\"20\" failures=\"0\"", svgWorkspaceQuintAcceptance)
        Assert.Contains("tests=\"22\" failures=\"0\"", svgWorkspaceQuintAcceptance)
        Assert.Contains("17 fail-closed mutations passed", svgWorkspaceQuintAcceptance)
        Assert.Contains("offline acceptance requires a real user and network namespace", svgWorkspaceQuintAcceptance)
        Assert.Contains("FSGG_TYPED_SDD_TEST_CRASH_AFTER_MOVE", svgWorkspaceQuintAcceptance)
        Assert.Contains("replacement-rollback-decision-lock", svgWorkspaceQuintAcceptance)
        Assert.Contains("quint-q3-public.junit.xml", qualification)
        Assert.Contains("artifacts/feed-readback/*.nupkg", publish)
        Assert.Contains("for attempt in $(seq 1 99)", publish)
        Assert.Contains("sleep $(( attempt < 20 ? 15 : 30 ))", publish)
        Assert.Contains("kernel.apparmor_restrict_unprivileged_userns=0", gateWorkflow)
        Assert.Contains("/usr/bin/unshare --user --map-root-user --net -- /usr/bin/true", gateWorkflow)
        Assert.Contains("bash tests/quint-svg-workspace-installed-acceptance.sh", gateWorkflow)
        Assert.Contains("Q2_EXACT_IR_JUNIT_OUT", gateWorkflow)
        Assert.DoesNotContain("bash tests/quint-q2-compiler-acceptance.sh", gateWorkflow)
        Assert.DoesNotContain("bash tests/quint-q3-typed-sdd-acceptance.sh", gateWorkflow)

        let orgFeed =
            publish.IndexOf("https://nuget.pkg.github.com/FS-GG/index.json", StringComparison.Ordinal)

        let publicFeed =
            publish.IndexOf("https://api.nuget.org/v3/index.json", StringComparison.Ordinal)

        Assert.True(orgFeed >= 0 && publicFeed > orgFeed, "the org feed must be pushed before nuget.org")
        Assert.Contains("four independently consumable packages", contract)
        Assert.Contains("| `publish-artifacts` |", contract)

    [<Fact>]
    let ``release has a source-bound read-only dual-feed resume`` () =
        Assert.Contains("readback_version:", workflow)
        Assert.Contains("inputs.readback_version != ''", workflow)
        Assert.Contains("git rev-parse \"refs/tags/v$VERSION^{commit}\"", workflow)
        Assert.Contains("for attempt in $(seq 1 40)", workflow)
        Assert.Contains("RepositoryCommit", workflow)

        let readbackStart =
            workflow.IndexOf("\n  readback-cli:\n", StringComparison.Ordinal)

        Assert.True(readbackStart >= 0, "read-only readback job must exist")

        let readbackEnd =
            workflow.IndexOf("\n  qualify-published:\n", readbackStart, StringComparison.Ordinal)

        Assert.True(readbackEnd > readbackStart, "published-set qualification must delimit the read-only readback job")
        let readback = workflow.Substring(readbackStart, readbackEnd - readbackStart)
        Assert.Equal(1, count "python3 scripts/verify-package-payloads.py" readback)

        Assert.Contains(
            "python3 scripts/verify-package-payloads.py \\\n              \"artifacts/feed-readback/$id.github.nupkg\" \\\n              \"artifacts/feed-readback/$id.nuget.nupkg\"",
            readback
        )

        Assert.Contains("grep -F \"commit=\\\"$source_head\\\"\"", readback)
        Assert.Contains("Retain read-only feed archives and payload maps", readback)
        Assert.DoesNotContain("dotnet nuget push", readback)
        assertPayloadReceiptMaps ()
        Assert.Contains("artifacts/feed-readback/*", workflow)

    [<Fact>]
    let ``release qualifies an already-published set read-only without effects or publisher credentials`` () =
        for input in
            [
                "qualify_published_version:"
                "qualify_candidate_run:"
                "qualify_candidate_sha256:"
                "qualify_candidate_source:"
            ] do
            Assert.Contains(input, workflow)

        let qualifyStart =
            workflow.IndexOf("\n  qualify-published:\n", StringComparison.Ordinal)

        let occupancyStart =
            workflow.IndexOf("\n  occupancy-preflight:\n", StringComparison.Ordinal)

        Assert.True(
            qualifyStart >= 0 && occupancyStart > qualifyStart,
            "qualification must be its own job before occupancy"
        )

        let qualify = workflow.Substring(qualifyStart, occupancyStart - qualifyStart)

        let publicStart =
            workflow.IndexOf("\n  qualify-public:\n", StringComparison.Ordinal)

        let readbackStart =
            workflow.IndexOf("\n  readback-cli:\n", StringComparison.Ordinal)

        let publicJob = workflow.Substring(publicStart, readbackStart - publicStart)

        Assert.Contains("github.event_name == 'workflow_dispatch'", qualify)
        Assert.Contains("refs/heads/main", qualify)
        Assert.Contains("contents: read", qualify)
        Assert.Contains("actions: read", qualify)
        Assert.Contains("packages: read", qualify)
        Assert.Contains("persist-credentials: false", qualify)
        Assert.DoesNotContain(": write", qualify)
        Assert.DoesNotContain("id-token", qualify)
        Assert.DoesNotContain("NuGet/login", qualify)
        Assert.DoesNotContain("dotnet nuget push", qualify)
        Assert.DoesNotContain("dotnet pack", qualify)
        Assert.True(publicSecretBoundaryHolds qualify, "qualification may reference only secrets.GITHUB_TOKEN")
        Assert.Contains("python3 scripts/qualify-published-set.py", qualify)
        Assert.Contains("scripts/verify-release-candidate.sh artifacts/qualified-receipt/packages", qualify)
        Assert.Equal(1, count "python3 scripts/verify-package-payloads.py" qualify)
        Assert.Contains("published-dual-feed-receipt", qualify)

        Assert.Contains(
            "\"artifacts/qualified-receipt/feed-readback/fs.gg.sdd.$lower.github.nupkg\" \\\n              \"artifacts/qualified-receipt/feed-readback/fs.gg.sdd.$lower.nuget.nupkg\"",
            qualify
        )

        let receiptStep =
            qualify.Substring(qualify.IndexOf("- name: Retain the durable dual-feed receipt", StringComparison.Ordinal))

        let receiptEnd =
            receiptStep.IndexOf("\n      - name:", 10, StringComparison.Ordinal)

        Assert.DoesNotContain("if:", receiptStep.Substring(0, receiptEnd))
        Assert.Contains("github.repository == 'FS-GG/FS.GG.SDD'", qualify)
        Assert.Contains("qualification excludes publish, readback and promotion modes", qualify)

        // The installed-code gate is the publisher-following gate, byte for byte, bound to the selected version.
        let installedGate (job: string) =
            let first =
                job.IndexOf("      - name: Qualify the public Knowledge CLI and SDK\n", StringComparison.Ordinal)

            let last =
                job.IndexOf("      - name: Retain public qualification evidence", StringComparison.Ordinal)

            Assert.True(first >= 0 && last > first, "installed-code gate must be delimited")
            job.Substring(first, last - first)

        Assert.Equal(
            (installedGate publicJob)
                .Replace("${{ needs.resolve-versions.outputs.cli_version }}", "${{ inputs.qualify_published_version }}"),
            installedGate qualify
        )

        // Every other mode is excluded when a qualification is selected.
        for name in
            [
                "resolve-versions"
                "publish-artifacts"
                "locate-artifacts"
                "publish-cli"
                "qualify-public"
                "readback-cli"
            ] do
            let start = workflow.IndexOf("\n  " + name + ":\n", StringComparison.Ordinal)
            Assert.True(start >= 0, name)
            let ifStart = workflow.IndexOf("    if: ", start, StringComparison.Ordinal)
            let ifLine = workflow.Substring(ifStart, workflow.IndexOf('\n', ifStart) - ifStart)
            Assert.Contains("inputs.qualify_published_version == ''", ifLine)
            Assert.Contains("inputs.qualify_candidate_run == ''", ifLine)
            Assert.Contains("inputs.qualify_candidate_sha256 == ''", ifLine)
            Assert.Contains("inputs.qualify_candidate_source == ''", ifLine)

        for needle in [ "qualify-published-set.py"; "qualification of an already-published" ] do
            Assert.Contains(needle, contract)
