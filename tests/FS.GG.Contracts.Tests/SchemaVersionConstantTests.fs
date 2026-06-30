namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module SchemaVersionConstantTests =

    // SC-001 / quickstart Scenario B: every `.fsgg` schema is represented once.
    [<Fact>]
    let ``entries enumerate exactly the 10 named schemas`` () =
        Assert.Equal(10, List.length Schemas.entries)

        let expected =
            set
                [ "providers"
                  "project"
                  "sdd"
                  "agents"
                  "scaffold-provenance"
                  "governance-handoff"
                  "governance"
                  "policy"
                  "capabilities"
                  "tooling" ]

        let actual = Schemas.entries |> List.map (fun e -> e.Name) |> Set.ofList
        Assert.Equal<Set<string>>(expected, actual)

    [<Fact>]
    let ``each entry name appears exactly once`` () =
        let names = Schemas.entries |> List.map (fun e -> e.Name)
        Assert.Equal(names.Length, (names |> List.distinct |> List.length))

    // FR-005 / SC-002: SDD-owned constants equal today's emitted values. Each is
    // grounded against its in-repo source of truth, cited per assertion.
    [<Fact>]
    let ``SDD-owned schema versions equal today's emitted values`` () =
        // SOURCE: src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs — parseProviderRegistry (schemaVersion: 1)
        Assert.Equal(1, Schemas.providersVersion)
        // SOURCE: src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs — parseProjectConfig (schemaVersion: 1)
        Assert.Equal(1, Schemas.projectVersion)
        // SOURCE: src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs — parseSddLifecyclePolicy (schemaVersion: 1)
        Assert.Equal(1, Schemas.sddVersion)
        // SOURCE: src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs — parseAgentGuidanceConfig (schemaVersion: 1)
        Assert.Equal(1, Schemas.agentsVersion)
        // SOURCE: src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs — ScaffoldProvenanceRecord.SchemaVersion = 1
        Assert.Equal(1, Schemas.scaffoldProvenanceVersion)
        // SOURCE: src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs — GovernanceHandoff.SchemaVersion = 1
        Assert.Equal(1, Schemas.governanceHandoffVersion)
        // SOURCE: src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs — GovernanceHandoff.ContractVersion = "1.0.0"
        Assert.Equal("1.0.0", Schemas.governanceHandoffContractVersion)

    // spec Assumptions / data-model "Governance-owned" provenance: these are the
    // values the package DECLARES to the Governance published reference — NOT
    // SDD-emitted. They are asserted as declared-reference values, never against
    // any SDD output.
    [<Fact>]
    let ``Governance-owned schema versions equal the declared reference values`` () =
        Assert.Equal(1, Schemas.governanceVersion)
        Assert.Equal(1, Schemas.policyVersion)
        Assert.Equal(2, Schemas.capabilitiesVersion)
        Assert.Equal(1, Schemas.toolingVersion)

    [<Fact>]
    let ``entry owner matches schema provenance`` () =
        let ownerOf name =
            (Schemas.entries |> List.find (fun e -> e.Name = name)).Owner

        for name in [ "providers"; "project"; "sdd"; "agents"; "scaffold-provenance"; "governance-handoff" ] do
            Assert.Equal(Schemas.Sdd, ownerOf name)

        for name in [ "governance"; "policy"; "capabilities"; "tooling" ] do
            Assert.Equal(Schemas.Governance, ownerOf name)

    [<Fact>]
    let ``governance-handoff entry carries the string contract version`` () =
        let entry = Schemas.entries |> List.find (fun e -> e.Name = "governance-handoff")
        Assert.Equal(Some "1.0.0", entry.ContractVersion)

    // T007b (050 D3): the additive `effectiveParameters` field on scaffold-provenance.json is a
    // purely additive optional field (`tryParse` defaults it to []). It MUST NOT bump the
    // schema major — the scaffold-provenance schema version stays 1. Guards against an
    // unintended major bump for an additive change.
    [<Fact>]
    let ``scaffold-provenance schema stays v1 under the additive effectiveParameters field`` () =
        Assert.Equal(1, Schemas.scaffoldProvenanceVersion)
        let entry = Schemas.entries |> List.find (fun e -> e.Name = "scaffold-provenance")
        Assert.Equal(Schemas.Sdd, entry.Owner)
