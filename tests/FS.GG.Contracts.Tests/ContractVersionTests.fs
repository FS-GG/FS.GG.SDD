namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module ContractVersionTests =

    // FR-012 / quickstart Scenario F: self-describing contract version. Additive
    // minor bump 1.0.1 → 1.1.0 (feature 042: new RegistryDocument model + validateDocument);
    // patch bump 1.1.0 → 1.1.1 (feature 045: widen semVerRegex to accept the 4-segment
    // version form — source behavior changes, no public surface change); additive minor
    // bump 1.1.1 → 1.2.0 (feature 052: new `Fsgg.Version` module + additive
    // `ProviderDescriptor.MinimumCliVersion` public surface).
    [<Fact>]
    let ``contract version self-report matches 1_2_0`` () =
        Assert.Equal("1.2.0", ContractVersion.value)
        Assert.Equal(1, ContractVersion.major)
        Assert.Equal(2, ContractVersion.minor)
        Assert.Equal(0, ContractVersion.patch)
