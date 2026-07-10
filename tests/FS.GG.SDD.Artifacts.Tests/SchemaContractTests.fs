namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module SchemaContractTests =
    [<Fact>]
    let ``Valid project SDD and agent contracts parse from real fixtures`` () =
        let project = TestSupport.snapshot "valid-work-item" ".fsgg/project.yml"
        let sdd = TestSupport.snapshot "valid-work-item" ".fsgg/sdd.yml"
        let agents = TestSupport.snapshot "valid-work-item" ".fsgg/agents.yml"

        match parseProjectConfig project with
        | Ok config ->
            Assert.Equal("fs-gg-sdd", config.ProjectId)
            Assert.Equal(Some ".fsgg/policy.yml", config.GovernancePolicyPath)
        | Error diagnostics -> failwith $"Project config failed: {diagnostics}"

        match parseSddLifecyclePolicy sdd with
        | Ok config ->
            Assert.True(config.RequireSourceDigests)
            Assert.Equal("diagnostic", config.StaleBehavior)
        | Error diagnostics -> failwith $"SDD config failed: {diagnostics}"

        match parseAgentGuidanceConfig agents with
        | Ok config ->
            Assert.Contains(config.Targets, fun target -> target.Id = "claude")
            Assert.Contains(config.Targets, fun target -> target.Id = "codex")
            Assert.False(config.GeneratedGuidanceIsAuthority)
        | Error diagnostics -> failwith $"Agent config failed: {diagnostics}"

    [<Fact>]
    let ``Artifact inventory covers each SDD lifecycle source and generated view`` () =
        let contracts = standardArtifactContracts ()
        let paths = contracts |> List.map (fun contract -> contract.Artifact.Path)

        Assert.Contains(".fsgg/project.yml", paths)
        Assert.Contains("work/<id>/spec.md", paths)
        Assert.Contains("work/<id>/tasks.yml", paths)
        Assert.Contains("work/<id>/evidence.yml", paths)
        Assert.Contains("readiness/<id>/work-model.json", paths)

        Assert.All(
            contracts,
            fun contract ->
                Assert.False(System.String.IsNullOrWhiteSpace contract.Purpose)
                Assert.False(System.String.IsNullOrWhiteSpace contract.SourceOfTruth)
                Assert.False(System.String.IsNullOrWhiteSpace contract.GeneratedViewRelationship)
                Assert.NotEmpty contract.DiagnosticFamily
        )

    [<Fact>]
    let ``Malformed schema fixture emits malformed and unsupported schema diagnostics`` () =
        let project =
            parseProjectConfig (TestSupport.snapshot "malformed-schema-version" ".fsgg/project.yml")

        let sdd =
            parseSddLifecyclePolicy (TestSupport.snapshot "malformed-schema-version" ".fsgg/sdd.yml")

        let projectIds =
            match project with
            | Ok _ -> []
            | Error diagnostics -> diagnostics |> List.map (fun diagnostic -> diagnostic.Id)

        let sddIds =
            match sdd with
            | Ok _ -> []
            | Error diagnostics -> diagnostics |> List.map (fun diagnostic -> diagnostic.Id)

        let ids = projectIds @ sddIds

        Assert.Contains("malformedSchemaVersion", ids)
        Assert.Contains("unsupportedSchemaVersion", ids)

    [<Fact>]
    let ``Missing-artifact fixture names absent required lifecycle paths`` () =
        let model = TestSupport.model "missing-artifact"
        TestSupport.assertDiagnostic "missingArtifact" model
        Assert.True(WorkModel.blockingDiagnostics model |> List.length >= 6)

    let private projectSnapshot text : FileSnapshot =
        { Path = ".fsgg/project.yml"
          Text = text }

    let private parsedTestFramework text =
        match parseProjectConfig (projectSnapshot text) with
        | Ok config -> config.TestFramework
        | Error diagnostics -> failwith $"Project config failed: {diagnostics}"

    [<Fact>]
    let ``project.testFramework parses present declared scalar`` () =
        let text =
            "schemaVersion: 1\nproject:\n  id: fs-gg-sdd\n  defaultWorkRoot: work\n  testFramework: expecto\nsdd:\n  config: .fsgg/sdd.yml\n  agents: .fsgg/agents.yml\n"

        Assert.Equal(Some "expecto", parsedTestFramework text)

    [<Fact>]
    let ``project.testFramework is None when absent`` () =
        let config =
            match parseProjectConfig (TestSupport.snapshot "valid-work-item" ".fsgg/project.yml") with
            | Ok config -> config
            | Error diagnostics -> failwith $"Project config failed: {diagnostics}"

        Assert.Equal(None, config.TestFramework)

    [<Fact>]
    let ``project.testFramework is None when blank or whitespace`` () =
        let text =
            "schemaVersion: 1\nproject:\n  id: fs-gg-sdd\n  defaultWorkRoot: work\n  testFramework: \"   \"\nsdd:\n  config: .fsgg/sdd.yml\n  agents: .fsgg/agents.yml\n"

        Assert.Equal(None, parsedTestFramework text)

    // FS.GG.SDD#310: `project.implementSkill` is optional, read and filtered exactly like
    // `testFramework`. Normalization and the neutral default live with the task generator.
    let private parsedImplementSkill text =
        match parseProjectConfig (projectSnapshot text) with
        | Ok config -> config.ImplementSkill
        | Error diagnostics -> failwith $"Project config failed: {diagnostics}"

    [<Fact>]
    let ``project.implementSkill parses present declared scalar`` () =
        let text =
            "schemaVersion: 1\nproject:\n  id: fs-gg-sdd\n  defaultWorkRoot: work\n  implementSkill: speckit-implement\nsdd:\n  config: .fsgg/sdd.yml\n  agents: .fsgg/agents.yml\n"

        Assert.Equal(Some "speckit-implement", parsedImplementSkill text)

    [<Fact>]
    let ``project.implementSkill is None when absent`` () =
        let config =
            match parseProjectConfig (TestSupport.snapshot "valid-work-item" ".fsgg/project.yml") with
            | Ok config -> config
            | Error diagnostics -> failwith $"Project config failed: {diagnostics}"

        Assert.Equal(None, config.ImplementSkill)

    [<Fact>]
    let ``project.implementSkill is None when blank or whitespace`` () =
        let text =
            "schemaVersion: 1\nproject:\n  id: fs-gg-sdd\n  defaultWorkRoot: work\n  implementSkill: \"   \"\nsdd:\n  config: .fsgg/sdd.yml\n  agents: .fsgg/agents.yml\n"

        Assert.Equal(None, parsedImplementSkill text)

    // FS.GG.SDD#306 (FR-001): `project.visualSurface` is an optional boolean. It is a convenience
    // flag, not a contract — anything that is not a YAML `true` reads as `false`, so a typo declares
    // "no visual surface" rather than blocking every command in the workspace.
    let private parsedVisualSurface text =
        match parseProjectConfig (projectSnapshot text) with
        | Ok config -> config.VisualSurface
        | Error diagnostics -> failwith $"Project config failed: {diagnostics}"

    let private projectConfigDeclaring (declaration: string) =
        $"schemaVersion: 1\nproject:\n  id: fs-gg-sdd\n  defaultWorkRoot: work\n{declaration}sdd:\n  config: .fsgg/sdd.yml\n  agents: .fsgg/agents.yml\n"

    [<Theory>]
    [<InlineData("  visualSurface: true\n", true)>]
    [<InlineData("  visualSurface: True\n", true)>]
    [<InlineData("  visualSurface: false\n", false)>]
    [<InlineData("", false)>]
    let ``project.visualSurface parses a declared boolean`` (declaration: string) (expected: bool) =
        Assert.Equal(expected, parsedVisualSurface (projectConfigDeclaring declaration))

    [<Theory>]
    [<InlineData("  visualSurface: \"   \"\n")>]
    [<InlineData("  visualSurface: yes-please\n")>]
    [<InlineData("  visualSurface: 1\n")>]
    let ``project.visualSurface degrades to false on a non-boolean scalar`` (declaration: string) =
        Assert.False(parsedVisualSurface (projectConfigDeclaring declaration))

    [<Fact>]
    let ``project.visualSurface is false when absent from a real project config`` () =
        let config =
            match parseProjectConfig (TestSupport.snapshot "valid-work-item" ".fsgg/project.yml") with
            | Ok config -> config
            | Error diagnostics -> failwith $"Project config failed: {diagnostics}"

        Assert.False(config.VisualSurface)
