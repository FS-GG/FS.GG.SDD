namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module ProviderDescriptorTests =

    let private baseDescriptor: Provider.ProviderDescriptor =
        { Name = "fixture"
          ContractVersion = "1.0.0"
          TemplateId = "fixture-template"
          Source = "/abs/path/ok"
          Parameters = []
          Build = None
          Test = None
          Run = None
          Verify = None
          NameParameter = "name"
          IdentifierParameter = None
          MinimumCliVersion = None }

    // SC-003 / Scenario D: a descriptor with no command fields exposes them absent,
    // so consumers fall back to today's platform defaults (no observable change).
    [<Fact>]
    let ``descriptor with no declared commands exposes them as absent`` () =
        let d = baseDescriptor
        Assert.Equal(None, d.Build)
        Assert.Equal(None, d.Test)
        Assert.Equal(None, d.Run)
        Assert.Equal(None, d.Verify)

    [<Fact>]
    let ``descriptor declaring commands exposes executable and arguments as authored`` () =
        let cmd: Provider.DeclaredCommand =
            { Executable = "dotnet"
              Arguments = [ "build"; "-c"; "Release" ] }

        let d = { baseDescriptor with Build = Some cmd }

        match d.Build with
        | Some declared ->
            Assert.Equal("dotnet", declared.Executable)
            Assert.Equal<string list>([ "build"; "-c"; "Release" ], declared.Arguments)
        | None -> Assert.True(false, "expected a declared Build command")

    // FR-006 Scenario 4: the five preserved fields match SDD's current descriptor shape.
    [<Fact>]
    let ``the five preserved fields carry the current SDD descriptor shape`` () =
        let param: Provider.ProviderParameterSpec =
            { Key = "license"
              Required = false
              Default = Some "MIT" }

        let d =
            { baseDescriptor with
                Parameters = [ param ] }

        Assert.Equal("fixture", d.Name)
        Assert.Equal("1.0.0", d.ContractVersion)
        Assert.Equal("fixture-template", d.TemplateId)
        Assert.Equal("/abs/path/ok", d.Source)
        Assert.Equal(1, d.Parameters.Length)
        Assert.Equal("license", d.Parameters.Head.Key)
        Assert.False(d.Parameters.Head.Required)
        Assert.Equal(Some "MIT", d.Parameters.Head.Default)

    // FR-007 / Scenario 3.
    [<Fact>]
    let ``defaultNameParameter is name`` () =
        Assert.Equal("name", Provider.defaultNameParameter)

    [<Fact>]
    let ``resolveNameParameter falls back to default for blank or whitespace declarations`` () =
        Assert.Equal(
            "name",
            Provider.resolveNameParameter
                { baseDescriptor with
                    NameParameter = "" }
        )

        Assert.Equal(
            "name",
            Provider.resolveNameParameter
                { baseDescriptor with
                    NameParameter = "   " }
        )

    [<Fact>]
    let ``resolveNameParameter returns the authored value when declared`` () =
        Assert.Equal(
            "projectName",
            Provider.resolveNameParameter
                { baseDescriptor with
                    NameParameter = "projectName" }
        )

    // Edge Case / Principle VIII: a declared command with a blank executable is
    // malformed (surfaced), distinct from absent.
    [<Fact>]
    let ``isMalformed is true for a blank or whitespace executable`` () =
        Assert.True(Provider.isMalformed { Executable = ""; Arguments = [] })

        Assert.True(
            Provider.isMalformed
                { Executable = "   "
                  Arguments = [ "x" ] }
        )

    [<Fact>]
    let ``isMalformed is false for a declared executable`` () =
        Assert.False(
            Provider.isMalformed
                { Executable = "dotnet"
                  Arguments = [] }
        )
