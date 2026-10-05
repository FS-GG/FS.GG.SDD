namespace FS.GG.Contracts.Tests

open Fsgg.ProviderCatalog
open Xunit

module ProviderCatalogTests =
    // Synthetic declarations stand in for provider-authored inputs; they certify no executable.
    let parameter key required fallback =
        { Key = key; Kind = String; Required = required; Prompt = key; Help = key
          Default = fallback; Values = []; Validation = { NonEmpty = true; MinLength = 1; MaxLength = 128; AllowedValues = [] } }

    let descriptor =
        { Id = "arbitrary"; DisplayName = "Fixture"; Help = "Fixture"; Language = "anything"; ProductShape = "fixture"
          DescriptorId = "fixture-descriptor"; DescriptorRevision = "revision"; DescriptorDigest = "sha256:" + String.replicate 64 "a"
          ContractVersion = "3.0.0"; TemplateSource = "opaque-source"; TemplateId = "opaque-template"; Platforms = ["fixture-os"]
          Parameters = [parameter "raw" true None; parameter "package" true None; parameter "code" true (Some "Code")]
          Identities = { RawName = "raw"; PackageIdentity = "package"; CodeIdentifier = "code" }
          Tools = []; Capabilities = []; Evidence = []; Skills = [] }
    let catalog = { SchemaVersion = 2; Id = "catalog"; Revision = "revision"; Digest = "sha256:" + String.replicate 64 "b"; Providers = [descriptor] }
    let inputs = ["raw", "Awkward name!?"; "package", "example.org/module"]
    let codes result = match result with Error errors -> errors |> List.map (fun e -> e.Code) | Ok _ -> []

    [<Fact>]
    let ``explicit identities preserve awkward raw input and default precedence`` () =
        match resolve catalog "arbitrary" (inputs @ ["code", "Override"]) with
        | Error errors -> failwithf "%A" errors
        | Ok prepared ->
            Assert.Equal("Awkward name!?", prepared.RawProductName)
            Assert.Equal("example.org/module", prepared.PackageIdentity)
            Assert.Equal("Override", prepared.CodeIdentifier)
            Assert.Equal<(string * string) list>(["code", "Override"; "package", "example.org/module"; "raw", "Awkward name!?"], prepared.EffectiveParameters)

    [<Fact>]
    let ``missing explicit module never derives from raw name`` () =
        Assert.Contains("catalog.requiredInput", codes (resolve catalog "arbitrary" ["raw", "Awkward name!?"]))

    [<Fact>]
    let ``invalid hidden default refuses before override`` () =
        let bad = { descriptor with Parameters = descriptor.Parameters |> List.map (fun p -> if p.Key = "code" then {p with Default = Some ""} else p) }
        Assert.Contains("catalog.invalidDefault", codes (resolve {catalog with Providers = [bad]} "arbitrary" (inputs @ ["code", "Good"])))

    [<Theory>]
    [<InlineData("1.22", true)>]
    [<InlineData("2026-10-05", true)>]
    [<InlineData("1.2.3-preview.1", true)>]
    [<InlineData("1.x", false)>]
    [<InlineData("latest", false)>]
    [<InlineData(">=1", false)>]
    [<InlineData("1.", false)>]
    let ``exact literal grammar refuses selectors`` value expected = Assert.Equal(expected, isExactVersion value)

    [<Fact>]
    let ``duplicates unknown overrides and unsupported versions refuse`` () =
        Assert.Contains("catalog.duplicate", codes (resolve catalog "arbitrary" (inputs @ inputs)))
        Assert.Contains("catalog.unknownInput", codes (resolve catalog "arbitrary" (inputs @ ["other", "value"])))
        Assert.Contains("catalog.unsupportedContract", validate {catalog with Providers = [{descriptor with ContractVersion = "2.0.0"}]} |> List.map (fun e -> e.Code))
        Assert.Contains("catalog.unsupportedSchema", validate {catalog with SchemaVersion = 1} |> List.map (fun e -> e.Code))

    [<Fact>]
    let ``command free obligations remain declarations and dangling evidence refuses`` () =
        let capability = { Id = "arbitrary:semantic"; Required = true; Platforms = ["fixture-os"]; ToolIds = []; EvidenceIds = []; Binding = SemanticOnly }
        let c = {catalog with Providers = [{descriptor with Capabilities = [capability]}]}
        Assert.Empty(validate c)
        match resolve c "arbitrary" inputs with
        | Ok result -> Assert.Equal(SemanticOnly,result.Descriptor.Capabilities.Head.Binding)
        | Error errors -> failwithf "%A" errors
        Assert.Contains("catalog.reference", validate {c with Providers = [{descriptor with Capabilities = [{capability with EvidenceIds = ["missing"]}]}]} |> List.map (fun e -> e.Code))

    [<Fact>]
    let ``unordered declarations normalize without changing argv or user values`` () =
        let tool = { Id = "tool"; Version = "1.22"; Platforms = ["z-os";"a-os"] }
        let cmd : Fsgg.Provider.DeclaredCommand = { Executable = "opaque"; Arguments = ["z";"";"a with space"] }
        let cap = { Id = "arbitrary:exec"; Required = true; Platforms = ["z-os";"a-os"]; ToolIds = ["tool"]; EvidenceIds = []; Binding = Command(cmd,{WorkingDirectory=".";TimeoutSeconds=1;CostClass="cheap";EnvironmentIds=["z";"a"]}) }
        let d = {descriptor with Platforms = ["z-os";"a-os"]; Tools=[tool]; Capabilities=[cap]; Skills=["z";"a"]}
        let reversed = {d with Parameters=List.rev d.Parameters; Platforms=List.rev d.Platforms; Skills=List.rev d.Skills}
        Assert.Equal(resolve {catalog with Providers=[d]} d.Id inputs,resolve {catalog with Providers=[reversed]} d.Id (List.rev inputs))
