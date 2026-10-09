namespace FS.GG.SDD.Commands.Tests

open System.Text
open System.Security.Cryptography
open Fsgg
open FS.GG.SDD.Commands
open FS.GG.Governance.Config
open Xunit

module ProviderCapabilityAdmissionTests =
    // Synthetic declarations exercise the real package resolver; no provider is invoked.
    let digest bytes = "sha256:" + (SHA256.HashData(bytes: byte array) |> System.Convert.ToHexString).ToLowerInvariant()
    let identity: CatalogScaffoldPolicy.Identity = { Id = "selected-policy"; Version = "1.0.0"; Digest = digest [||] }
    let policy: CatalogScaffoldPolicy.Policy =
        { Identity = identity; EvidenceMapIdentity = identity
          RequiredCapabilityIds = ["build:build"]; SemanticOnlyCapabilityIds = []
          KnownPlatforms = ["fixture-platform"]
          EnvironmentBindings = [{ Id = "fixture-local"; Environment = Model.Local }]
          SupportedEvidenceFormats = [{ Id = "opaque-format"; Version = "1.0.0" }]
          EvidenceMappings = [{ Format = "opaque@literal"; Id = "opaque-format"; Version = "1.0.0" }]
          MaximumPreflightSeconds = 5; MaximumScaffoldSeconds = 10
          ToolProbes = []; Archives = [] }
    let parameter key : ProviderCatalog.Parameter =
        { Key = key; Kind = ProviderCatalog.String; Required = true
          Prompt = key; Help = key; Default = Some key; Values = []
          Validation = { NonEmpty = true; MinLength = 1; MaxLength = 100; AllowedValues = [] } }
    let command: Provider.DeclaredCommand = { Executable = "opaque-tool"; Arguments = [""; "with spaces"; "$(literal);&"] }
    let descriptor: ProviderCatalog.Descriptor =
        { Id = "generic-fixture"; DisplayName = "Fixture"; Help = "Fixture"
          Language = "opaque"; ProductShape = "fixture"; DescriptorId = "fixture-descriptor"
          DescriptorRevision = "revision"; DescriptorDigest = digest [||]; ContractVersion = "3.0.0"
          TemplateSource = "opaque-source"; TemplateId = "opaque-template"; Platforms = ["fixture-platform"]
          Parameters = [parameter "raw"; parameter "package"; parameter "code"]
          Identities = { RawName = "raw"; PackageIdentity = "package"; CodeIdentifier = "code" }
          Tools = [{ Id = "opaque-tool"; Version = "1.2.3"; Platforms = ["fixture-platform"] }]
          Capabilities =
            [{ Id = "build:build"; Required = true; Platforms = ["fixture-platform"]; ToolIds = ["opaque-tool"]
               EvidenceIds = ["build-output"]
               Binding = ProviderCatalog.Command(command,
                   { WorkingDirectory = "."; TimeoutSeconds = 2; CostClass = "cheap"; EnvironmentIds = ["fixture-local"] }) }]
          Evidence = [{ Id = "build-output"; Format = "opaque@literal"; Path = "out/build.json"; Required = true }]
          Skills = [] }
    let errors (result: Result<'a, ProviderCatalog.Diagnostic list>) = match result with Error ds -> ds |> List.map _.Code | Ok _ -> []

    [<Fact>]
    let ``actual resolver receives whole request with literal argv and distinct protocol mapping`` () =
        match ProviderCapabilityAdmission.request policy "fixture-platform" descriptor with
        | Error ds -> failwithf "%A" ds
        | Ok request ->
            Assert.Equal("1.0.0", request.ContractVersion)
            match request.Bindings.Head.Binding with
            | CapabilityBindings.Command supplied -> Assert.Equal<string list>(command.Arguments, supplied.Arguments)
            | _ -> failwith "Command became semantic-only"
            Assert.Equal("opaque-format", request.Evidence.Head.Format.Id)
            Assert.Equal("1.0.0", request.Evidence.Head.Format.Version)
        Assert.True(Result.isOk (ProviderCapabilityAdmission.resolve policy "fixture-platform" descriptor))

    [<Fact>]
    let ``independent required floor refuses an absent binding without partial success`` () =
        let p = { policy with RequiredCapabilityIds = ["build:build"; "test:test"] }
        Assert.True(Result.isError (ProviderCapabilityAdmission.resolve p "fixture-platform" descriptor))

    [<Fact>]
    let ``optional unknown capability is retained explicitly unsupported`` () =
        let original = descriptor.Capabilities.Head
        let optional = { original with Id = "opaque:optional"; Required = false }
        match ProviderCapabilityAdmission.resolve policy "fixture-platform" { descriptor with Capabilities = descriptor.Capabilities @ [optional] } with
        | Error ds -> failwithf "%A" ds
        | Ok resolved -> Assert.Contains("opaque:optional", resolved.UnsupportedCapabilityIds)

    [<Fact>]
    let ``provider required flag joins independent policy requiredness`` () =
        match ProviderCapabilityAdmission.request { policy with RequiredCapabilityIds = [] } "fixture-platform" descriptor with
        | Error ds -> failwithf "%A" ds
        | Ok request -> Assert.Contains("build:build", request.RequiredCapabilityIds)

    [<Fact>]
    let ``format keys are opaque and missing or conflicting maps refuse`` () =
        for mappings in [ []; policy.EvidenceMappings @ [{ Format = "opaque@literal"; Id = "different"; Version = "9.0.0" }] ] do
            Assert.True(Result.isError (ProviderCapabilityAdmission.request { policy with EvidenceMappings = mappings } "fixture-platform" descriptor))

    [<Fact>]
    let ``provider cannot widen independent platform or environment support`` () =
        Assert.True(Result.isError (ProviderCapabilityAdmission.resolve { policy with KnownPlatforms = [] } "fixture-platform" descriptor))
        Assert.True(Result.isError (ProviderCapabilityAdmission.request { policy with EnvironmentBindings = [] } "fixture-platform" descriptor))

    [<Theory>]
    [<InlineData("../escape")>]
    [<InlineData("/absolute")>]
    [<InlineData("C:/absolute")>]
    let ``governed evidence paths are not normalized into apparent safety`` path =
        let d = { descriptor with Evidence = [{ descriptor.Evidence.Head with Path = path }] }
        Assert.True(Result.isError (ProviderCapabilityAdmission.request policy "fixture-platform" d))

    [<Theory>]
    [<InlineData("unrecognized")>]
    [<InlineData("")>]
    let ``unknown cost class has no fallback`` cost =
        let d = { descriptor with Capabilities = [{ descriptor.Capabilities.Head with Binding = ProviderCatalog.Command(command, { WorkingDirectory = "."; TimeoutSeconds = 2; CostClass = cost; EnvironmentIds = ["fixture-local"] }) }] }
        Assert.True(Result.isError (ProviderCapabilityAdmission.request policy "fixture-platform" d))

    [<Fact>]
    let ``unsupported descriptor protocol never selects capability contract by accident`` () =
        Assert.True(Result.isError (ProviderCapabilityAdmission.request policy "fixture-platform" { descriptor with ContractVersion = "1.0.0" }))

    [<Fact>]
    let ``policy raw digest drift malformed input and unknown fields refuse`` () =
        for text in ["{}"; "{not json"; "{\"schemaVersion\":1,\"unexpected\":true}"; "{\"schemaVersion\":1,\"schemaVersion\":1}"] do
            let bytes = Encoding.UTF8.GetBytes text
            Assert.True(Result.isError (CatalogScaffoldPolicy.parse (digest bytes) bytes))
            Assert.True(Result.isError (CatalogScaffoldPolicy.parse (digest [||]) bytes))
