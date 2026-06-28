namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module ContractVersionTests =

    // FR-012 / quickstart Scenario F: self-describing contract version. Additive
    // minor bump 1.0.1 → 1.1.0 (feature 042: new RegistryDocument model + validateDocument).
    [<Fact>]
    let ``contract version self-report matches 1_1_0`` () =
        Assert.Equal("1.1.0", ContractVersion.value)
        Assert.Equal(1, ContractVersion.major)
        Assert.Equal(1, ContractVersion.minor)
        Assert.Equal(0, ContractVersion.patch)
