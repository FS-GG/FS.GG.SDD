namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module ContractVersionTests =

    // FR-012 / quickstart Scenario F: self-describing contract version.
    [<Fact>]
    let ``contract version self-report matches 1_0_1`` () =
        Assert.Equal("1.0.1", ContractVersion.value)
        Assert.Equal(1, ContractVersion.major)
        Assert.Equal(0, ContractVersion.minor)
        Assert.Equal(1, ContractVersion.patch)
