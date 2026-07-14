namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module SchemaVersionConstantTests =

    // SC-001 / quickstart Scenario B: every `.fsgg` schema is represented once.
    // Feature 057 / ADR-0014: `skill-manifest` joins the set (10 -> 11).
    [<Fact>]
    let ``entries enumerate exactly the 11 named schemas`` () =
        Assert.Equal(11, List.length Schemas.entries)

        let expected =
            set
                [ "providers"
                  "project"
                  "sdd"
                  "agents"
                  "scaffold-provenance"
                  "governance-handoff"
                  "skill-manifest"
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
        // governance-handoff: every other site CONSUMES these constants (the emitter and the
        // release-contract declaration both read them), so there is no second literal to drift
        // against. This assertion is therefore not a mirror-check but the reviewed ANCHOR: the one
        // place the intended values are pinned. Changing the contract version means changing the
        // constant, and this line, deliberately — and in step with FS-GG/.github's registry.
        Assert.Equal(1, Schemas.governanceHandoffVersion)
        // 1.1.0: ADR-0035 stage 3 / FS.GG.SDD#422 — `ship.unobservedEvidence` reachable in
        // readiness.blockingDiagnosticIds[]. Additive ⇒ minor.
        Assert.Equal("1.1.0", Schemas.governanceHandoffContractVersion)
        // Feature 057 / ADR-0014: the skill-manifest contract starts at schema version 1.
        Assert.Equal(1, Schemas.skillManifestVersion)

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

        for name in
            [ "providers"
              "project"
              "sdd"
              "agents"
              "scaffold-provenance"
              "governance-handoff"
              "skill-manifest" ] do
            Assert.Equal(Schemas.Sdd, ownerOf name)

        for name in [ "governance"; "policy"; "capabilities"; "tooling" ] do
            Assert.Equal(Schemas.Governance, ownerOf name)

    // WIRING, not value: the entry must carry the declared constant rather than a literal of its
    // own. The value itself is pinned once, in the anchor above — asserting it again here would
    // re-create the hand-mirror this contract's 1.0.0/1.1.0 drift came from.
    [<Fact>]
    let ``governance-handoff entry carries the declared contract version`` () =
        let entry = Schemas.entries |> List.find (fun e -> e.Name = "governance-handoff")
        Assert.Equal(Some Schemas.governanceHandoffContractVersion, entry.ContractVersion)

    // T007b (050 D3): the additive `effectiveParameters` field on scaffold-provenance.json is a
    // purely additive optional field (`tryParse` defaults it to []). It MUST NOT bump the
    // schema major — the scaffold-provenance schema version stays 1. Guards against an
    // unintended major bump for an additive change.
    [<Fact>]
    let ``scaffold-provenance schema stays v1 under the additive effectiveParameters field`` () =
        Assert.Equal(1, Schemas.scaffoldProvenanceVersion)
        let entry = Schemas.entries |> List.find (fun e -> e.Name = "scaffold-provenance")
        Assert.Equal(Schemas.Sdd, entry.Owner)

    // Feature 057 / ADR-0014 §Decision 5: AGENT_SKILL_ROOTS is the single declared root set.
    [<Fact>]
    let ``agentSkillRoots is the declared three-root set`` () =
        Assert.Equal<string list>([ ".claude"; ".codex"; ".agents" ], Schemas.agentSkillRoots)

    // Feature 057 / ADR-0014 §Decision 1: the manifest expresses id, scope, digest, and a
    // body source (inline or a resolvable path) for both process and product skills.
    [<Fact>]
    let ``SkillManifest expresses process and product skills with a digest and a body source`` () =
        let manifest: Schemas.SkillManifest =
            { SchemaVersion = Schemas.skillManifestVersion
              Skills =
                [ { Id = "fs-gg-sdd-plan"
                    Scope = Schemas.Process
                    Sha256 = "aa"
                    Body = Some "# plan"
                    ResolvablePath = None }
                  { Id = "fs-gg-elmish"
                    Scope = Schemas.Product
                    Sha256 = "bb"
                    Body = None
                    ResolvablePath = Some "skills/fs-gg-elmish/SKILL.md" } ] }

        Assert.Equal(2, List.length manifest.Skills)
        Assert.Equal(Schemas.Process, manifest.Skills.[0].Scope)
        Assert.Equal(Schemas.Product, manifest.Skills.[1].Scope)
        Assert.Equal(Some "# plan", manifest.Skills.[0].Body)
        Assert.Equal(Some "skills/fs-gg-elmish/SKILL.md", manifest.Skills.[1].ResolvablePath)
