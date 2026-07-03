namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open FS.GG.SDD.Artifacts
open Xunit

module GovernanceBoundaryTests =
    [<Fact>]
    let ``Initial lifecycle rule contracts expose required SDD checks`` () =
        let ids = LifecycleRuleContracts.contractIds ()

        Assert.Contains("requiredSpecSections", ids)
        Assert.Contains("planObligations", ids)
        Assert.Contains("taskGraphShape", ids)
        Assert.Contains("evidenceDeclarations", ids)
        Assert.Contains("loadedAgentSkills", ids)
        Assert.Contains("testObligations", ids)

    [<Fact>]
    let ``Lifecycle rule contracts exclude Governance runtime semantics`` () =
        let contracts = LifecycleRuleContracts.initialContracts ()

        Assert.All(
            contracts,
            fun contract ->
                Assert.False(contract.GovernanceCompatibility.RouteAware)
                Assert.False(contract.GovernanceCompatibility.ProfileAware)
                Assert.False(contract.GovernanceCompatibility.FreshnessAware)
                Assert.False(contract.GovernanceCompatibility.EnforceableBySdd)
        )

    [<Fact>]
    let ``Governance files are optional compatibility boundaries in work model`` () =
        let boundaries =
            TestSupport.model "valid-work-item" |> WorkModel.governanceBoundaryEntries

        Assert.Contains(
            boundaries,
            fun boundary ->
                boundary.Path = ".fsgg/policy.yml"
                && boundary.Owner = "governance"
                && not boundary.RequiredBySdd
        )

        Assert.Contains(
            boundaries,
            fun boundary ->
                boundary.Path = ".fsgg/capabilities.yml"
                && boundary.Relationship = "optionalCompatibilityBoundary"
        )

        Assert.Contains(
            boundaries,
            fun boundary ->
                boundary.Path = ".fsgg/tooling.yml"
                && boundary.Relationship = "optionalCompatibilityBoundary"
        )

    [<Fact>]
    let ``Artifact model project has no FS GG Governance package or project reference`` () =
        let fsproj =
            Path.Combine(TestSupport.repoRoot, "src", "FS.GG.SDD.Artifacts", "FS.GG.SDD.Artifacts.fsproj")
            |> File.ReadAllText

        Assert.DoesNotContain("FS.GG.Governance", fsproj)
