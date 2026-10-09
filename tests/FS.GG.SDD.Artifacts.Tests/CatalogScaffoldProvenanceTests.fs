namespace FS.GG.SDD.Artifacts.Tests

open System.Text.Json.Nodes
open Fsgg.ProviderCatalog
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Artifacts.CatalogScaffoldProvenance
open Xunit

module CatalogScaffoldProvenanceTests =
    // Synthetic complete observations test the codec, never actual provider certification.
    let synthetic () =
        let catalog = CatalogIntegrityFixture.sealedCatalog ()
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
                Platform = "fixture-os"
                Tools =
                    [
                        {
                            Id = "fixture-tool"
                            Version = "1.22"
                            Executable = "fixture-tool"
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
                            Version = "1"
                            Digest = hash
                        }
                    EvidenceMap =
                        {
                            Id = "synthetic-map"
                            Version = "1"
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
                            Platform = "fixture-os"
                            RequiredCapabilityIds = []
                            SemanticOnlyCapabilityIds = []
                            SupportedPlatforms = [ "fixture-os" ]
                            SupportedEnvironmentIds = [ "fixture-environment" ]
                            SupportedFormats = [ { Id = "fixture-format"; Version = "1" } ]
                            EvidenceMappings =
                                [
                                    {
                                        Format = "fixture-format"
                                        Id = "fixture-format"
                                        Version = "1"
                                    }
                                ]
                            Descriptor = d
                        }
                }
        }

    let text () =
        match serialize (synthetic ()) with
        | Ok text -> text
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``synthetic schema2 roundtrip preserves every ownership category and literal argv`` () =
        let value = synthetic ()

        match ScaffoldProvenanceDocument.parse (text ()) with
        | Error e -> failwithf "%A" e
        | Ok doc ->
            Assert.Equal(value.Ownership, ScaffoldProvenanceDocument.ownershipProjection doc)

            match doc with
            | ScaffoldProvenanceDocument.Catalog parsed ->
                Assert.True(value.Observation.Invocations = parsed.Observation.Invocations)
            | _ -> failwith "No legacy fallback."

    [<Fact>]
    let ``schema1 document projection preserves legacy bytes`` () =
        let record = (synthetic ()).Ownership
        let bytes = ScaffoldProvenance.serialize record

        match ScaffoldProvenanceDocument.parse bytes with
        | Ok doc ->
            Assert.Equal(bytes, ScaffoldProvenance.serialize (ScaffoldProvenanceDocument.ownershipProjection doc))
        | Error e -> failwithf "%A" e

    [<Fact>]
    let ``closed envelope refuses duplicate unknown unsupported and partial observations`` () =
        let original = text ()

        for invalid in
            [
                original.Replace("\"schemaVersion\":2", "\"schemaVersion\":3")
                original.Replace("\"schemaVersion\":2", "\"schemaVersion\":2,\"schemaVersion\":2")
                original.Replace("\"platform\":\"fixture-os\"", "\"unknownPlatform\":\"fixture-os\"")
                original.Replace("\"exitCode\":0", "\"exitCode\":1")
                original.Replace("\"result\":\"succeeded\"", "\"result\":\"pending\"")
            ] do
            Assert.True(Result.isError (ScaffoldProvenanceDocument.parse invalid), invalid)

        let node = JsonNode.Parse original |> nonNull
        (node["observation"] |> nonNull).AsObject().Remove("tools") |> ignore
        Assert.True(Result.isError (ScaffoldProvenanceDocument.parse (node.ToJsonString())))

    [<Fact>]
    let ``ownership identity hash path and observation correspondence are mandatory`` () =
        let r = synthetic ()

        let variants =
            [
                { r with
                    Declaration =
                        { r.Declaration with
                            RawProductName = "not the declared identity"
                        }
                }
                { r with
                    Ownership =
                        { r.Ownership with
                            EffectiveParameters = []
                        }
                }
                { r with
                    Observation =
                        { r.Observation with
                            ConsumedArchiveDigest = "sha256:" + System.String('b', 64)
                        }
                }
                { r with
                    Observation =
                        { r.Observation with
                            ProducedPaths = []
                        }
                }
                { r with
                    Ownership =
                        { r.Ownership with
                            ProducedPaths =
                                [
                                    { r.Ownership.ProducedPaths.Head with
                                        Path = "../escape"
                                    }
                                ]
                        }
                }
                { r with
                    Ownership =
                        { r.Ownership with
                            ProducedPaths = r.Ownership.ProducedPaths @ r.Ownership.ProducedPaths
                        }
                }
            ]

        for invalid in variants do
            Assert.True(Result.isError (serialize invalid))


    [<Fact>]
    let ``cumulative invocation bounds permit the independently longer preflight phase`` () =
        let original = synthetic ()
        let budgets: Budgets =
            { RequestedPreflightSeconds = 120
              RequestedScaffoldSeconds = 60
              AdmittedPreflightSeconds = 120
              AdmittedScaffoldSeconds = 60 }
        let invocation: Invocation =
            { original.Observation.Invocations.Head with TimeoutSeconds = 120 }
        let record: CatalogScaffoldProvenanceRecord =
            { original with
                Declaration = { original.Declaration with Budgets = budgets }
                Observation = { original.Observation with Invocations = [ invocation ] } }
        // These synthetic codec controls confer no execution/phase-timing evidence.
        Assert.True(Result.isOk (serialize record))
        let over: CatalogScaffoldProvenanceRecord =
            { record with
                Observation =
                    { record.Observation with
                        Invocations = [ { invocation with TimeoutSeconds = 121 } ] } }
        Assert.True(Result.isError (serialize over))
        let zero: CatalogScaffoldProvenanceRecord =
            { record with
                Observation =
                    { record.Observation with
                        Invocations = [ { invocation with TimeoutSeconds = 0 } ] } }
        Assert.True(Result.isError (serialize zero))
