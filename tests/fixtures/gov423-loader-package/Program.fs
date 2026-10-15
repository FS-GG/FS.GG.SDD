// Synthetic codec/admission controls against retained package APIs; no provider execution.
open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open Fsgg
open Fsgg.ProviderCatalog
open FS.GG.SDD.Commands
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Artifacts.CatalogScaffoldProvenance
open FS.GG.Governance.Config

module Fixture =
    let digest (bytes: byte array) = ProviderCatalogIntegrity.digest bytes
    let parameter key : ProviderCatalog.Parameter =
        {
            Key = key
            Kind = ProviderCatalog.String
            Required = true
            Prompt = key
            Help = key
            Default = Some key
            Values = []
            Validation =
                {
                    NonEmpty = true
                    MinLength = 1
                    MaxLength = 100
                    AllowedValues = []
                }
        }


    let command: Provider.DeclaredCommand =
        {
            Executable = "opaque-tool"
            Arguments = [ ""; "with spaces"; "$(literal);&" ]
        }

    let descriptor: ProviderCatalog.Descriptor =
        {
            Id = "generic-fixture"
            DisplayName = "Fixture"
            Help = "Fixture"
            Language = "opaque"
            ProductShape = "fixture"
            DescriptorId = "fixture-descriptor"
            DescriptorRevision = "revision"
            DescriptorDigest = digest [||]
            ContractVersion = "3.0.0"
            TemplateSource = "opaque-source"
            TemplateId = "opaque-template"
            Platforms = [ "fixture-platform" ]
            Parameters = [ parameter "code"; parameter "package"; parameter "raw" ]
            Identities =
                {
                    RawName = "raw"
                    PackageIdentity = "package"
                    CodeIdentifier = "code"
                }
            Tools =
                [
                    {
                        Id = "opaque-tool"
                        Version = "1.2.3"
                        Platforms = [ "fixture-platform" ]
                    }
                ]
            Capabilities =
                [
                    {
                        Id = "build:build"
                        Required = true
                        Platforms = [ "fixture-platform" ]
                        ToolIds = [ "opaque-tool" ]
                        EvidenceIds = [ "build-output" ]
                        Binding =
                            ProviderCatalog.Command(
                                command,
                                {
                                    WorkingDirectory = "."
                                    TimeoutSeconds = 2
                                    CostClass = "cheap"
                                    EnvironmentIds = [ "fixture-local" ]
                                }
                            )
                    }
                ]
            Evidence =
                [
                    {
                        Id = "build-output"
                        Format = "opaque@literal"
                        Path = "out/build.json"
                        Required = true
                    }
                ]
            Skills = []
        }

    let sealedCatalog () : ProviderCatalog.Catalog =
        let d = { descriptor with DescriptorDigest = digest (ProviderCatalogIntegrity.descriptorBytes descriptor) }
        let c = { SchemaVersion = 2; Id = "synthetic-catalog"; Revision = "qualification"; Digest = digest [||]; Providers = [d] }
        { c with Digest = digest (ProviderCatalogIntegrity.catalogBytes c) }

    let synthetic () =
        let catalog = sealedCatalog ()
        let d = catalog.Providers.Head

        let prepared =
            match resolve catalog d.Id [ "raw", "Awkward name!?" ] with
            | Ok p -> p
            | Error e -> failwithf "%A" e

        let hash = "sha256:" + System.String('a', 64)

        let generator: FS.GG.SDD.Artifacts.SchemaVersion.GeneratorVersion =
            {
                Id = "synthetic-generator"
                Version = "1.0.0"
            }

        let path owner name =
            {
                Path = name
                Owner = owner
                Sha256 = Some hash
            }

        let ownership: ScaffoldProvenanceRecord =
            {
                SchemaVersion = 1
                Generator = generator
                RequiredMinimumCliVersion = None
                ProviderName = d.Id
                ProviderContractVersion = d.ContractVersion
                TemplateRef = d.TemplateSource
                Outcome = "providerSucceeded"
                ProducedPaths = [ path GeneratedProduct "src/product.txt" ]
                MirroredPaths = [ path Mirrored ".codex/skills/product/SKILL.md" ]
                SddOwnedPaths = [ path Sdd ".fsgg/project.yml" ]
                DriverPaths = [ path Driver ".agents/skills/driver/SKILL.md" ]
                GameSkillPaths = [ path GameSkill ".agents/skills/game/SKILL.md" ]
                RenderingSkillPaths = [ path RenderingSkill ".agents/skills/render/SKILL.md" ]
                EffectiveParameters = prepared.EffectiveParameters
            }

        let observation: Observation =
            {
                Platform = "fixture-platform"
                Tools =
                    [
                        {
                            Id = "opaque-tool"
                            Version = "1.2.3"
                            Executable = "opaque-tool"
                        }
                    ]
                ConsumedArchiveDigest = hash
                Transport =
                    {
                        Executable = "dotnet"
                        Version = "10.0.401"
                    }
                Invocations =
                    [
                        {
                            Executable = "dotnet"
                            Arguments = [ "new"; "literal space"; ""; "$(literal)" ]
                            WorkingRoot = "staging"
                            WorkingDirectory = "."
                            EnvironmentRoots = []
                            TimeoutSeconds = 30
                            ExitCode = 0
                        }
                    ]
                Result = "succeeded"
                ProducedPaths =
                    ownership.ProducedPaths
                    @ ownership.MirroredPaths
                    @ ownership.SddOwnedPaths
                    @ ownership.DriverPaths
                    @ ownership.GameSkillPaths
                    @ ownership.RenderingSkillPaths
                    |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a.Path, b.Path))
            }

        {
            SchemaVersion = 2
            Generator = generator
            Ownership = ownership
            Observation = observation
            Declaration =
                {
                    RawCatalogDigest = hash
                    CatalogId = catalog.Id
                    CatalogRevision = catalog.Revision
                    CatalogDigest = catalog.Digest
                    Descriptor = d
                    EffectiveParameters = prepared.EffectiveParameters
                    RawProductName = prepared.RawProductName
                    PackageIdentity = prepared.PackageIdentity
                    CodeIdentifier = prepared.CodeIdentifier
                    Archive =
                        {
                            Id = "synthetic-package"
                            Version = "1.0.0"
                            Digest = hash
                        }
                    Policy =
                        {
                            Id = "synthetic-policy"
                            Version = "1.0.0"
                            Digest = hash
                        }
                    EvidenceMap =
                        {
                            Id = "synthetic-map"
                            Version = "1.0.0"
                            Digest = hash
                        }
                    Budgets =
                        {
                            RequestedPreflightSeconds = 10
                            RequestedScaffoldSeconds = 30
                            AdmittedPreflightSeconds = 10
                            AdmittedScaffoldSeconds = 30
                        }
                    Admission =
                        {
                            ContractVersion = "1.0.0"
                            Platform = "fixture-platform"
                            RequiredCapabilityIds = [ "build:build" ]
                            SemanticOnlyCapabilityIds = []
                            SupportedPlatforms = [ "fixture-platform" ]
                            SupportedEnvironmentIds = [ "fixture-local" ]
                            SupportedFormats = [ { Id = "opaque-format"; Version = "1.0.0" } ]
                            EvidenceMappings =
                                [
                                    {
                                        Format = "opaque@literal"
                                        Id = "opaque-format"
                                        Version = "1.0.0"
                                    }
                                ]
                            Descriptor = d
                        }
                }
        }

module Qualification =
    let success label = function
        | Ok value -> value
        | Error errors -> failwithf "%s: %A" label errors

    let require condition message =
        if not condition then failwith message

    let refused label = function
        | Error _ -> ()
        | Ok _ -> failwith (label + " unexpectedly accepted")

    let mutate (text: string) (change: JsonNode -> unit) =
        let node = JsonNode.Parse text
        change node
        node.ToJsonString()

    let main (args: string array) =
        let destination = args[0]
        Directory.CreateDirectory destination |> ignore
        let results = ResizeArray<string * string * string>()
        let check name action =
            try
                action ()
                results.Add(name, "passed", "")
            with ex ->
                results.Add(name, "failed", ex.ToString())

        let policyBytes = File.ReadAllBytes args[1]
        let rawDigest = Fixture.digest policyBytes
        let policy = CatalogScaffoldPolicy.parse rawDigest policyBytes |> success "independent policy"
        let value = Fixture.synthetic ()
        let declaration = value.Declaration
        let value =
            { value with
                Declaration =
                    { declaration with
                        Policy = { Id = policy.Identity.Id; Version = policy.Identity.Version; Digest = policy.Identity.Digest }
                        EvidenceMap = { Id = policy.EvidenceMapIdentity.Id; Version = policy.EvidenceMapIdentity.Version; Digest = policy.EvidenceMapIdentity.Digest } } }
        let text = CatalogScaffoldProvenance.serialize value |> success "synthetic provenance serialize"
        File.WriteAllText(Path.Combine(destination, "synthetic-provenance.json"), text)
        let descriptor = value.Declaration.Descriptor
        let parsed () = ScaffoldProvenanceDocument.parse text |> success "strict provenance parse"

        check "policy.bytes.digest" (fun () -> require (policy.Identity.Digest = rawDigest) "policy digest lost")
        check "policy.raw.digest.refusal" (fun () -> CatalogScaffoldPolicy.parse (Fixture.digest [||]) policyBytes |> refused "digest")
        for name, change in
            [ "schema", (fun (n: JsonNode) -> n["schemaVersion"] <- JsonValue.Create 2)
              "unknown", (fun n -> n["unexpected"] <- JsonValue.Create true)
              "map.digest", (fun n -> n["evidenceMap"]["digest"] <- JsonValue.Create (Fixture.digest [||])) ] do
            check ("policy." + name + ".refusal") (fun () ->
                let bytes = mutate (Encoding.UTF8.GetString policyBytes) change |> Encoding.UTF8.GetBytes
                CatalogScaffoldPolicy.parse (Fixture.digest bytes) bytes |> refused name)
        check "policy.duplicate.refusal" (fun () ->
            let bytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":1," + (Encoding.UTF8.GetString policyBytes).TrimStart().Substring(1))
            CatalogScaffoldPolicy.parse (Fixture.digest bytes) bytes |> refused "duplicate")

        check "admission.complete.literal.request" (fun () ->
            let request = ProviderCapabilityAdmission.request policy "fixture-platform" descriptor |> success "whole request"
            require (request.ContractVersion = "1.0.0") "protocol mapping changed"
            require (List.contains "build:build" request.RequiredCapabilityIds) "required floor lost"
            match request.Bindings.Head.Binding with
            | CapabilityBindings.Command command -> require (command.Arguments = Fixture.command.Arguments) "literal argv changed"
            | _ -> failwith "executable binding lost"
            require (request.Evidence.Head.Format.Id = "opaque-format" && request.Evidence.Head.Format.Version = "1.0.0") "opaque format mapping changed")
        check "admission.resolve.actual.config" (fun () -> ProviderCapabilityAdmission.resolve policy "fixture-platform" descriptor |> success "actual Config" |> ignore)
        check "admission.required.binding.refusal" (fun () ->
            ProviderCapabilityAdmission.resolve { policy with RequiredCapabilityIds = [ "build:build"; "test:test" ] } "fixture-platform" descriptor |> refused "required binding")
        check "admission.platform.refusal" (fun () -> ProviderCapabilityAdmission.resolve policy "wrong-platform" descriptor |> refused "platform")
        check "admission.format.refusal" (fun () -> ProviderCapabilityAdmission.resolve { policy with EvidenceMappings = [] } "fixture-platform" descriptor |> refused "format")
        check "admission.deleted.binding.refusal" (fun () -> ProviderCapabilityAdmission.resolve policy "fixture-platform" { descriptor with Capabilities = [] } |> refused "deleted binding")
        check "admission.protocol.refusal" (fun () -> ProviderCapabilityAdmission.resolve policy "fixture-platform" { descriptor with ContractVersion = "2.0.0" } |> refused "protocol")

        check "provenance.schema2.roundtrip" (fun () ->
            match parsed () with
            | ScaffoldProvenanceDocument.Catalog received -> require (received = value) "complete schema2 record changed"
            | _ -> failwith "schema2 fell through legacy")
        check "provenance.ownership.projection" (fun () -> require (ScaffoldProvenanceDocument.ownershipProjection (parsed ()) = value.Ownership) "ownership changed")
        check "provenance.recompute.independent.policy" (fun () ->
            match parsed () with
            | ScaffoldProvenanceDocument.Catalog received ->
                ProviderCapabilityAdmission.resolve { policy with RequiredCapabilityIds = [ "test:test" ] } "fixture-platform" received.Declaration.Descriptor |> refused "independent policy"
            | _ -> failwith "unexpected legacy")
        for name, change in
            [ "schema", (fun (n: JsonNode) -> n["schemaVersion"] <- JsonValue.Create 3)
              "unknown", (fun n -> n["unexpected"] <- JsonValue.Create true)
              "descriptor", (fun n -> n["declaration"]["descriptor"]["help"] <- JsonValue.Create "tampered")
              "archive", (fun n -> n["observation"]["consumedArchiveDigest"] <- JsonValue.Create (Fixture.digest [||]))
              "platform", (fun n -> n["observation"]["platform"] <- JsonValue.Create "wrong-platform")
              "tool", (fun n ->
                  let toolsNode = n["observation"]["tools"]
                  let tools = toolsNode.AsArray()
                  let tool = tools[0]
                  tool["version"] <- JsonValue.Create "9.0.0")
              "policy.digest", (fun n -> n["declaration"]["policy"]["digest"] <- JsonValue.Create "invalid")
              "failed.observation", (fun n -> n["observation"]["result"] <- JsonValue.Create "failed") ] do
            check ("provenance." + name + ".refusal") (fun () -> ScaffoldProvenanceDocument.parse (mutate text change) |> refused name)
        check "provenance.malformed.refusal" (fun () -> ScaffoldProvenanceDocument.parse "{" |> refused "malformed")
        check "provenance.duplicate.refusal" (fun () -> ScaffoldProvenanceDocument.parse ("{\"schemaVersion\":2," + text.TrimStart().Substring(1)) |> refused "duplicate")

        let rows = results |> Seq.map (fun (name, status, detail) -> {| name = name; status = status; detail = detail |}) |> Seq.toArray
        let failed = rows |> Array.filter (fun r -> r.status <> "passed")
        let result = {| schema = "fsgg.gov423.loader-package-qualification/v1"; synthetic = true; providerExecution = false; status = (if failed.Length = 0 then "passed" else "failed"); checks = rows |}
        let json = JsonSerializer.Serialize(result, JsonSerializerOptions(WriteIndented = true))
        File.WriteAllText(Path.Combine(destination, "api-result.json"), json)
        printfn "%s" json
        if failed.Length = 0 then 0 else 1

[<EntryPoint>]
let main args =
    try Qualification.main args
    with ex ->
        eprintfn "%O" ex
        1
