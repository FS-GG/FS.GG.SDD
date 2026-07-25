namespace FS.GG.SDD.Acceptance.Tests

open System
open System.IO
open AcceptanceSupport
open Xunit

/// Offline contract coverage for the scheduled real-provider lane. The workflow itself is copied
/// beside the test assembly, so these checks exercise the exact committed definition without
/// requiring a GitHub runner or network access.
module ScheduledAcceptanceWorkflowTests =

    let private workflowPath =
        Path.Combine(AppContext.BaseDirectory, "composition-acceptance.yml")

    let private workflowText () = File.ReadAllText workflowPath

    [<Fact>]
    let ``scheduled lane always reaches the fail-closed registry resolver and provider facts`` () =
        let workflow = workflowText ()

        Assert.Contains("schedule:", workflow)
        Assert.Contains("REGISTRY_SECRET_CONTENT: ${{ secrets.FSGG_SDD_ACCEPTANCE_REGISTRY }}", workflow)
        Assert.DoesNotContain("run_acceptance=false", workflow)
        Assert.DoesNotContain("steps.preflight.outputs.run_acceptance", workflow)
        Assert.DoesNotContain("composition-acceptance skipped", workflow)
        Assert.Contains("run: bash scripts/workflows/resolve-acceptance-registry.sh", workflow)
        Assert.Contains("dotnet test FS.GG.SDD.sln --filter \"kind=composition-acceptance\"", workflow)

    [<Fact>]
    let ``offline provider facts retain an explicit capability skip`` () =
        let original = Environment.GetEnvironmentVariable registryEnvVar

        try
            Environment.SetEnvironmentVariable(registryEnvVar, null)
            let attribute = RequiresRegistryFactAttribute()
            Assert.Contains("unset", attribute.Skip)
            Assert.Contains("opt-in and network-gated", attribute.Skip)
        finally
            Environment.SetEnvironmentVariable(registryEnvVar, original)

    [<Fact>]
    let ``default real-provider product root is a stable valid identifier shape`` () =
        let name = newProductRoot () |> DirectoryInfo |> _.Name

        Assert.Matches("^[A-Za-z][A-Za-z0-9]*$", name)
