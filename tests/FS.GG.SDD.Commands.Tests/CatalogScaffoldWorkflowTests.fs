namespace FS.GG.SDD.Commands.Tests

open System.Text
open System.Security.Cryptography
open Fsgg
open FS.GG.SDD.Commands
open FS.GG.Governance.Config

module internal CatalogWorkflowFixture =
    // Synthetic declarations exercise the real package resolver; no provider is invoked.
    let digest bytes =
        "sha256:"
        + (SHA256.HashData(bytes: byte array) |> System.Convert.ToHexString).ToLowerInvariant()

    let identity: CatalogScaffoldPolicy.Identity =
        {
            Id = "selected-policy"
            Version = "1.0.0"
            Digest = digest [||]
        }

    let policy: CatalogScaffoldPolicy.Policy =
        {
            Identity = identity
            EvidenceMapIdentity = identity
            RequiredCapabilityIds = [ "build:build" ]
            SemanticOnlyCapabilityIds = []
            KnownPlatforms = [ "fixture-platform" ]
            EnvironmentBindings =
                [
                    {
                        Id = "fixture-local"
                        Environment = Model.Local
                    }
                ]
            SupportedEvidenceFormats =
                [
                    {
                        Id = "opaque-format"
                        Version = "1.0.0"
                    }
                ]
            EvidenceMappings =
                [
                    {
                        Format = "opaque@literal"
                        Id = "opaque-format"
                        Version = "1.0.0"
                    }
                ]
            MaximumPreflightSeconds = 5
            MaximumScaffoldSeconds = 10
            ToolProbes = []
            Archives = []
        }

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
            Parameters = [ parameter "raw"; parameter "package"; parameter "code" ]
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


    // Complete synthetic declaration/observation data, never actual process or cleanup evidence.
    let value () =
        let hash = digest [||]

        let descriptor =
            { descriptor with
                DescriptorDigest =
                    FS.GG.SDD.Artifacts.ProviderCatalogIntegrity.digest (
                        FS.GG.SDD.Artifacts.ProviderCatalogIntegrity.descriptorBytes descriptor
                    )
            }

        let unsealed: ProviderCatalog.Catalog =
            {
                SchemaVersion = 2
                Id = "design-catalog"
                Revision = "1"
                Digest = hash
                Providers = [ descriptor ]
            }

        let catalog =
            { unsealed with
                Digest =
                    FS.GG.SDD.Artifacts.ProviderCatalogIntegrity.digest (
                        FS.GG.SDD.Artifacts.ProviderCatalogIntegrity.catalogBytes unsealed
                    )
            }

        let prepared: ProviderCatalog.PreparedConfiguration =
            match Fsgg.ProviderCatalog.resolve catalog descriptor.Id [] with
            | Ok prepared -> prepared
            | Error diagnostics -> failwithf "Invalid synthetic declaration: %A" diagnostics

        let generator: FS.GG.SDD.Artifacts.SchemaVersion.GeneratorVersion =
            {
                Id = "synthetic-generator"
                Version = "1.0.0"
            }

        let product: FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath =
            {
                Path = "src/product.txt"
                Owner = FS.GG.SDD.Artifacts.ArtifactRef.GeneratedProduct
                Sha256 = Some hash
            }

        let sdd: FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath =
            {
                Path = ".fsgg/project.yml"
                Owner = FS.GG.SDD.Artifacts.ArtifactRef.Sdd
                Sha256 = Some hash
            }

        let ownership: FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProvenanceRecord =
            {
                SchemaVersion = 1
                Generator = generator
                RequiredMinimumCliVersion = None
                ProviderName = descriptor.Id
                ProviderContractVersion = descriptor.ContractVersion
                TemplateRef = descriptor.TemplateSource
                Outcome = "providerSucceeded"
                ProducedPaths = [ product ]
                MirroredPaths = []
                SddOwnedPaths = [ sdd ]
                DriverPaths = []
                GameSkillPaths = []
                RenderingSkillPaths = []
                EffectiveParameters = prepared.EffectiveParameters
            }

        let archive: FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.ArchiveIdentity =
            {
                Id = "synthetic-package"
                Version = "1.0.0"
                Digest = hash
            }

        let observation: FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Observation =
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
                            TimeoutSeconds = 5
                            ExitCode = 0
                        }
                    ]
                Result = "succeeded"
                ProducedPaths = [ product; sdd ]
            }

        let templateInvocation: FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.Invocation =
            {
                Executable = "dotnet"
                Arguments = [ "new"; descriptor.TemplateId ]
                WorkingRoot = "staging"
                WorkingDirectory = "."
                EnvironmentRoots = []
                TimeoutSeconds = 5
                ExitCode = 0
            }

        let templateObservation =
            { observation with
                Invocations = observation.Invocations @ [ templateInvocation ]
            }

        let provenance: FS.GG.SDD.Artifacts.CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord =
            {
                SchemaVersion = 2
                Generator = generator
                Ownership = ownership
                Observation = templateObservation
                Declaration =
                    {
                        RawCatalogDigest = hash
                        CatalogId = catalog.Id
                        CatalogRevision = catalog.Revision
                        CatalogDigest = catalog.Digest
                        Descriptor = prepared.Descriptor
                        EffectiveParameters = prepared.EffectiveParameters
                        RawProductName = prepared.RawProductName
                        PackageIdentity = prepared.PackageIdentity
                        CodeIdentifier = prepared.CodeIdentifier
                        Archive = archive
                        Policy =
                            {
                                Id = policy.Identity.Id
                                Version = policy.Identity.Version
                                Digest = policy.Identity.Digest
                            }
                        EvidenceMap =
                            {
                                Id = policy.EvidenceMapIdentity.Id
                                Version = policy.EvidenceMapIdentity.Version
                                Digest = policy.EvidenceMapIdentity.Digest
                            }
                        Budgets =
                            {
                                RequestedPreflightSeconds = 5
                                RequestedScaffoldSeconds = 10
                                AdmittedPreflightSeconds = 5
                                AdmittedScaffoldSeconds = 10
                            }
                        Admission =
                            {
                                ContractVersion = "1.0.0"
                                Platform = "fixture-platform"
                                RequiredCapabilityIds = policy.RequiredCapabilityIds
                                SemanticOnlyCapabilityIds = policy.SemanticOnlyCapabilityIds
                                SupportedPlatforms = policy.KnownPlatforms
                                SupportedEnvironmentIds = [ "fixture-local" ]
                                SupportedFormats =
                                    [
                                        {
                                            Id = "opaque-format"
                                            Version = "1.0.0"
                                        }
                                    ]
                                EvidenceMappings =
                                    [
                                        {
                                            Format = "opaque@literal"
                                            Id = "opaque-format"
                                            Version = "1.0.0"
                                        }
                                    ]
                                Descriptor = prepared.Descriptor
                            }
                    }
            }

        catalog, prepared, policy, archive, observation, provenance


open System.IO
open System.Text
open System.Text.Json.Nodes
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CatalogScaffoldWorkflow
open Xunit

module CatalogScaffoldWorkflowTests =
    let bytes () =
        let text =
            File.ReadAllText(
                Path.Combine(
                    TestSupport.repoRoot,
                    "tests/FS.GG.SDD.Artifacts.Tests/Fixtures/ProviderCatalog/five-providers.yml"
                )
            )

        let catalog =
            match ProviderCatalog.parse text with
            | Ok c -> c
            | Error e -> failwithf "%A" e

        let ds =
            catalog.Providers
            |> List.map (fun d ->
                { d with
                    DescriptorDigest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes d)
                })

        let c = { catalog with Providers = ds }
        let node = JsonNode.Parse text |> nonNull
        node["digest"] <- JsonValue.Create(ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.catalogBytes c))

        for provider in (node["providers"] |> nonNull).AsArray() do
            let p = nonNull provider
            let id = (p["id"] |> nonNull).GetValue<string>()
            p["descriptorDigest"] <- JsonValue.Create((ds |> List.find (fun d -> d.Id = id)).DescriptorDigest)

        Encoding.UTF8.GetBytes(node.ToJsonString())

    [<Fact>]
    let ``catalog-only fifth provider appears and selected identities retain raw values`` () =
        let bytes = bytes ()

        let selection =
            {
                CatalogBytes = bytes
                ExpectedRawDigest = ProviderCatalogIntegrity.digest bytes
                Provider = None
                Overrides = []
            }

        match prepare selection with
        | Error e -> failwithf "%A" e
        | Ok preview ->
            Assert.Equal("prepared", preview.Status)
            Assert.Equal(5, preview.Catalog.Providers.Length)
            let fifth = preview.Catalog.Providers |> List.last

            match
                prepare
                    { selection with
                        Provider = Some fifth.Id
                        Overrides = [ "raw", "Awkward name!?"; "package", "example.org/explicit/module" ]
                    }
            with
            | Error e -> failwithf "%A" e
            | Ok selected ->
                Assert.Equal("Awkward name!?", selected.Selected.Value.RawProductName)
                Assert.Equal("example.org/explicit/module", selected.Selected.Value.PackageIdentity)
                Assert.Equal("ExplicitCode", selected.Selected.Value.CodeIdentifier)

        Assert.True(
            Result.isError (
                prepare
                    { selection with
                        Overrides = [ "raw", "ignored" ]
                    }
            )
        )

    [<Fact>]
    let ``malformed or future provenance blocks refresh and preserves original files`` () =
        for document in [ "{\"schemaVersion\":2}"; "{\"schemaVersion\":99}"; "{not json" ] do
            let root = TestSupport.tempDirectory ()

            TestSupport.request FS.GG.SDD.Commands.CommandTypes.Init root
            |> TestSupport.runRequest
            |> ignore

            let path = Path.Combine(root, ScaffoldProvenance.provenancePath)
            File.WriteAllText(path, document)

            let before =
                Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                |> Array.map (fun p -> p, File.ReadAllBytes p)
                |> Map.ofArray

            let report = TestSupport.runRefresh root "missing-work"
            Assert.Contains(report.Diagnostics, fun d -> d.Id = "scaffold.provenanceMalformed")

            let lifecycleReport =
                TestSupport.runCharter root "missing-work" "Blocked catalog document"

            Assert.Contains(lifecycleReport.Diagnostics, fun d -> d.Id = "provenance.malformed")

            for KeyValue(path, bytes) in before do
                Assert.True((bytes = File.ReadAllBytes path))

    let private syntheticRequest () : CatalogScaffoldRequest =
        let catalog, selected, policy, archive, observation, record =
            CatalogWorkflowFixture.value ()

        let request: CatalogScaffoldRequest =
            {
                Selection =
                    {
                        CatalogBytes = [||]
                        ExpectedRawDigest = record.Declaration.RawCatalogDigest
                        Provider = Some selected.Descriptor.Id
                        Overrides = selected.EffectiveParameters
                    }
                TargetRoot = "synthetic-target"
                TemplateArchive = "synthetic-package.nupkg"
                ExpectedArchiveDigest = archive.Digest
                PolicyBytes = [||]
                ExpectedPolicyDigest = policy.Identity.Digest
                SelectedPlatform = observation.Platform
                PreflightTimeoutSeconds = 5
                ScaffoldTimeoutSeconds = 10
                DryRun = false
            }

        request

    [<Fact>]
    let ``workflow retains original input bytes independently of caller and observer mutation`` () =
        let source = syntheticRequest ()

        let original =
            { source with
                Selection =
                    { source.Selection with
                        CatalogBytes = [| 1uy; 2uy |]
                    }
                PolicyBytes = [| 3uy; 4uy |]
            }

        let model, _ = init original
        original.Selection.CatalogBytes[0] <- 9uy
        original.PolicyBytes[0] <- 9uy
        let first = CatalogScaffoldWorkflow.request model
        Assert.True(first.Selection.CatalogBytes = [| 1uy; 2uy |])
        Assert.True(first.PolicyBytes = [| 3uy; 4uy |])
        first.Selection.CatalogBytes[0] <- 8uy
        first.PolicyBytes[0] <- 8uy
        let second = CatalogScaffoldWorkflow.request model
        Assert.True(second.Selection.CatalogBytes = [| 1uy; 2uy |])
        Assert.True(second.PolicyBytes = [| 3uy; 4uy |])
        Assert.Equal(original.Selection.ExpectedRawDigest, second.Selection.ExpectedRawDigest)
        Assert.Equal(original.ExpectedPolicyDigest, second.ExpectedPolicyDigest)
        Assert.Equal<(string * string) list>(original.Selection.Overrides, second.Selection.Overrides)

    let private syntheticCommitted () =
        let catalog, selected, policy, archive, observation, record =
            CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let model, _ = init (syntheticRequest ())
        let preflight, _ = update (InputsVerified(preview, policy, archive)) model
        let staging, _ = update (PreflightObserved observation) preflight
        let composing, _ = update (TemplateObserved record.Observation) staging
        let committing, _ = update (WorkspaceComposed record) composing
        let committed, _ = update (CommitObserved Committed) committing
        Assert.Equal(Cleaning, phase committed)
        Assert.Equal(Committed, commitOutcome committed)
        committed

    [<Fact>]
    let ``synthetic postcommit refusal remains failed after known cleanup and never repeats retirement`` () =
        let committed = syntheticCommitted ()

        let diagnostic: Fsgg.ProviderCatalog.Diagnostic =
            {
                Code = "synthetic.postCommitFailure"
                Path = "$.synthetic"
                Message = "Synthetic postcommit refusal."
            }

        let refused, effects = update (RefusalObserved [ diagnostic ]) committed
        Assert.Empty effects
        let terminal, terminalEffects = update (CleanupObserved(true, true)) refused
        Assert.Equal(Failed, outcome terminal)
        Assert.Equal(Committed, commitOutcome terminal)
        Assert.Contains(diagnostic, diagnostics terminal)
        Assert.Equal(Terminal, phase terminal)
        Assert.Empty terminalEffects

    [<Fact>]
    let ``synthetic unknown cleanup remains unknown while retaining committed outcome and first refusal`` () =
        let committed = syntheticCommitted ()

        let diagnostic: Fsgg.ProviderCatalog.Diagnostic =
            {
                Code = "synthetic.postCommitFailure"
                Path = "$.synthetic"
                Message = "Synthetic postcommit refusal."
            }

        let refused, _ = update (RefusalObserved [ diagnostic ]) committed

        for children, staging in [ false, true; true, false; false, false ] do
            let terminal, effects = update (CleanupObserved(children, staging)) refused
            Assert.Equal(CleanupUnknown, outcome terminal)
            Assert.Equal(Committed, commitOutcome terminal)
            Assert.Contains(diagnostic, diagnostics terminal)
            Assert.Empty effects

    [<Fact>]
    let ``invalid provider or phase budgets cannot request effects`` () =
        let request = syntheticRequest ()

        for invalid in
            [
                { request with
                    PreflightTimeoutSeconds = 0
                }
                { request with
                    ScaffoldTimeoutSeconds = 0
                }
                { request with
                    Selection =
                        { request.Selection with
                            Provider = None
                        }
                }
            ] do
            let model, effects = init invalid
            Assert.Equal(Failed, outcome model)
            Assert.Equal(Terminal, phase model)
            Assert.Empty effects

    [<Fact>]
    let ``synthetic dryrun is prepared without process observations or workspace effects`` () =
        let catalog, selected, policy, archive, _, _ = CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ =
            init
                { syntheticRequest () with
                    DryRun = true
                }

        let prepared, effects = update (InputsVerified(preview, policy, archive)) initial
        Assert.Equal(Prepared, outcome prepared)
        Assert.Empty effects
        Assert.Equal(None, preflightObservation prepared)
        Assert.Equal(None, templateObservation prepared)
        Assert.Equal(None, provenance prepared)

    [<Fact>]
    let ``synthetic independent policy ceiling refusal emits only cleanup`` () =
        let catalog, selected, policy, archive, _, _ = CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())

        let refused, effects =
            update
                (InputsVerified(
                    preview,
                    { policy with
                        MaximumScaffoldSeconds = 1
                    },
                    archive
                ))
                initial

        Assert.Equal(Failed, outcome refused)
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)

    let private parseCapturedProvenance text =
        let snapshot: FileSnapshot =
            {
                Path = ScaffoldProvenance.provenancePath
                Text = text
                RawBytes = Some(Encoding.UTF8.GetBytes text)
            }

        loadWorkItemFromSnapshots [ snapshot ] "synthetic-capture"

    [<Fact>]
    let ``serialized catalog producer is retained as current schema2 source`` () =
        let _, _, _, _, _, record = CatalogWorkflowFixture.value ()

        let text =
            match CatalogScaffoldProvenance.serialize record with
            | Ok text -> text
            | Error errors -> failwithf "Invalid synthetic producer declaration: %A" errors

        let parsed = parseCapturedProvenance text
        let source = Assert.Single parsed.Sources
        Assert.Equal(ScaffoldProvenance.provenancePath, source.Artifact.Path)
        Assert.Equal(Some(SchemaVersion.create 2), source.SchemaVersion)
        Assert.Equal(SchemaVersion.Current, source.SchemaStatus)
        Assert.Equal(SchemaVersion.sha256Text text, source.Digest)

        Assert.DoesNotContain(
            parsed.Diagnostics,
            fun diagnostic ->
                diagnostic.Artifact
                |> Option.exists (fun artifact -> artifact.Path = ScaffoldProvenance.provenancePath)
        )

    [<Fact>]
    let ``legacy provenance producer remains a current schema1 source`` () =
        let _, _, _, _, _, record = CatalogWorkflowFixture.value ()
        let parsed = parseCapturedProvenance (ScaffoldProvenance.serialize record.Ownership)
        let source = Assert.Single parsed.Sources
        Assert.Equal(Some(SchemaVersion.create 1), source.SchemaVersion)
        Assert.Equal(SchemaVersion.Current, source.SchemaStatus)

        Assert.DoesNotContain(
            parsed.Diagnostics,
            fun diagnostic ->
                diagnostic.Artifact
                |> Option.exists (fun artifact -> artifact.Path = ScaffoldProvenance.provenancePath)
        )

    [<Fact>]
    let ``captured malformed and future provenance refuse without legacy fallback`` () =
        for text in
            [
                "{\"schemaVersion\":2}"
                "{\"schemaVersion\":1}"
                "{\"schemaVersion\":99}"
                "{\"schemaVersion\":1,\"schemaVersion\":2}"
                "{\"schemaVersion\":1.0}"
                "{not json"
            ] do
            let parsed = parseCapturedProvenance text
            let source = Assert.Single parsed.Sources
            Assert.NotEqual(SchemaVersion.Current, source.SchemaStatus)

            Assert.Contains(
                parsed.Diagnostics,
                fun diagnostic ->
                    diagnostic.Artifact
                    |> Option.exists (fun artifact -> artifact.Path = ScaffoldProvenance.provenancePath)
            )

    [<Fact>]
    let ``provenance exception does not admit unrelated JSON or change schema defaults`` () =
        let _, _, _, _, _, record = CatalogWorkflowFixture.value ()
        let text = ScaffoldProvenance.serialize record.Ownership

        let snapshot path body : FileSnapshot =
            {
                Path = path
                Text = body
                RawBytes = Some(Encoding.UTF8.GetBytes body)
            }

        let parsed =
            loadWorkItemFromSnapshots
                [
                    snapshot "/.fsgg/scaffold-provenance.json" text
                    snapshot "unrelated/scaffold-provenance.json" text
                    snapshot "unrelated.yml" "schemaVersion: 2\n"
                ]
                "synthetic-capture"

        Assert.DoesNotContain(parsed.Sources, fun source -> source.Artifact.Path = "unrelated/scaffold-provenance.json")
        Assert.Contains(parsed.Sources, fun source -> source.Artifact.Path = ScaffoldProvenance.provenancePath)

        let unrelated =
            parsed.Sources
            |> List.find (fun source -> source.Artifact.Path = "unrelated.yml")

        Assert.Equal(SchemaVersion.Unsupported, unrelated.SchemaStatus)

    [<Fact>]
    let ``synthetic successful label without any preflight invocation cannot stage a provider`` () =
        let catalog, selected, policy, archive, observation, _ =
            CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial

        let refused, effects =
            update (PreflightObserved { observation with Invocations = [] }) preflighting

        Assert.Equal(Failed, outcome refused)
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)

    [<Fact>]
    let ``composed provenance cannot substitute independently selected policy version or archive identity`` () =
        let catalog, selected, policy, archive, observation, record =
            CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
        let staging, _ = update (PreflightObserved observation) preflighting
        let composing, _ = update (TemplateObserved record.Observation) staging
        let declaration = record.Declaration

        for substituted in
            [
                { record with
                    Declaration =
                        { declaration with
                            Policy =
                                { declaration.Policy with
                                    Version = "2.0.0"
                                }
                        }
                }
                { record with
                    Declaration =
                        { declaration with
                            Archive =
                                { declaration.Archive with
                                    Id = "other-package"
                                }
                        }
                }
            ] do
            let refused, effects = update (WorkspaceComposed substituted) composing
            Assert.Equal(Failed, outcome refused)
            Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)

    [<Fact>]
    let ``recognized malformed current provenance and actual future integer have distinct statuses`` () =
        for text, expected in
            [
                ("{\"schemaVersion\":2}", SchemaVersion.Malformed)
                ("{\"schemaVersion\":99}", SchemaVersion.Future)
                ("{\"schemaVersion\":3.0}", SchemaVersion.Malformed)
                ("{\"schemaVersion\":\"99\"}", SchemaVersion.Malformed)
            ] do
            let source = (parseCapturedProvenance text).Sources |> Assert.Single
            Assert.Equal(expected, source.SchemaStatus)

    [<Fact>]
    let ``valid composed provenance cannot substitute actual template invocation observations`` () =
        let catalog, selected, policy, archive, observation, record =
            CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
        let staging, _ = update (PreflightObserved observation) preflighting
        let composing, _ = update (TemplateObserved record.Observation) staging

        let changed =
            { record with
                Observation =
                    { record.Observation with
                        Invocations =
                            record.Observation.Invocations
                            |> List.map (fun invocation ->
                                { invocation with
                                    Arguments = invocation.Arguments @ [ "changed-observation" ]
                                })
                    }
            }

        Assert.True(Result.isOk (CatalogScaffoldProvenance.serialize changed))
        let refused, effects = update (WorkspaceComposed changed) composing
        Assert.Equal(Failed, outcome refused)
        Assert.Contains(diagnostics refused, fun diagnostic -> diagnostic.Code = "catalog.observationMismatch")
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)

    [<Fact>]
    let ``template observation cannot replace measured preflight tools`` () =
        let catalog, selected, policy, archive, observation, record =
            CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
        let staging, _ = update (PreflightObserved observation) preflighting

        let changed =
            { record.Observation with
                Tools =
                    record.Observation.Tools
                    |> List.map (fun tool ->
                        { tool with
                            Executable = "other-observed-tool"
                        })
            }

        let refused, effects = update (TemplateObserved changed) staging
        Assert.Equal(Failed, outcome refused)
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)

    [<Fact>]
    let ``valid composed provenance cannot substitute immutable template payload hashes`` () =
        let catalog, selected, policy, archive, observation, record =
            CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
        let staging, _ = update (PreflightObserved observation) preflighting
        let composing, _ = update (TemplateObserved record.Observation) staging

        let changedPaths =
            record.Ownership.ProducedPaths
            |> List.map (fun path ->
                { path with
                    Sha256 = Some(CatalogWorkflowFixture.digest [| 1uy |])
                })

        let changed =
            { record with
                Ownership =
                    { record.Ownership with
                        ProducedPaths = changedPaths
                    }
                Observation =
                    { record.Observation with
                        ProducedPaths = changedPaths @ record.Ownership.SddOwnedPaths
                    }
            }

        Assert.True(Result.isOk (CatalogScaffoldProvenance.serialize changed))
        let refused, effects = update (WorkspaceComposed changed) composing
        Assert.Equal(Failed, outcome refused)
        Assert.Contains(diagnostics refused, fun diagnostic -> diagnostic.Code = "catalog.observationMismatch")
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)

    [<Fact>]
    let ``reusing preflight-only observation cannot advance a template stage`` () =
        let catalog, selected, policy, archive, observation, _ =
            CatalogWorkflowFixture.value ()

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
        let staging, _ = update (PreflightObserved observation) preflighting
        let refused, effects = update (TemplateObserved observation) staging
        Assert.Equal(Failed, outcome refused)
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)


    [<Fact>]
    let ``sole source manifest may retain original observation while recording its final union hash`` () =
        let catalog, selected, policy, archive, preflightObservation, original =
            CatalogWorkflowFixture.value ()

        let sourcePath = ".agents/skills/skill-manifest.json"

        let originalManifest =
            { original.Ownership.ProducedPaths.Head with
                Path = sourcePath
            }

        let templateObservation =
            { original.Observation with
                ProducedPaths = originalManifest :: original.Ownership.SddOwnedPaths
            }

        let finalManifest =
            { originalManifest with
                Sha256 = Some(CatalogWorkflowFixture.digest [| 1uy |])
            }

        let composed =
            { original with
                Ownership =
                    { original.Ownership with
                        ProducedPaths = [ finalManifest ]
                    }
                Observation =
                    { templateObservation with
                        ProducedPaths = finalManifest :: original.Ownership.SddOwnedPaths
                    }
            }

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
        let staging, _ = update (PreflightObserved preflightObservation) preflighting
        let composing, _ = update (TemplateObserved templateObservation) staging
        Assert.True(Result.isOk (CatalogScaffoldProvenance.serialize composed))
        let committing, effects = update (WorkspaceComposed composed) composing
        Assert.Equal(Committing, phase committing)
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ CommitWorkspace ], effects)
        // This transition preserves the observed source hash. Exact shared-amend byte
        // equality is an edge obligation, not an authority granted by this pure test.
        Assert.NotEqual(originalManifest.Sha256, finalManifest.Sha256)

    [<Fact>]
    let ``manifest union exception refuses other similarly named payload paths`` () =
        for sourcePath in
            [
                "skill-manifest.json"
                "other/skills/skill-manifest.json"
                ".agents/skills/other.json"
                ".config/dotnet-tools.JSON"
                "other/.config/dotnet-tools.json"
                ".config/other-tools.json"
            ] do
            let catalog, selected, policy, archive, preflightObservation, original =
                CatalogWorkflowFixture.value ()

            let produced =
                { original.Ownership.ProducedPaths.Head with
                    Path = sourcePath
                }

            let templateObservation =
                { original.Observation with
                    ProducedPaths = produced :: original.Ownership.SddOwnedPaths
                }

            let changed =
                { produced with
                    Sha256 = Some(CatalogWorkflowFixture.digest [| 1uy |])
                }

            let composed =
                { original with
                    Ownership =
                        { original.Ownership with
                            ProducedPaths = [ changed ]
                        }
                    Observation =
                        { templateObservation with
                            ProducedPaths = changed :: original.Ownership.SddOwnedPaths
                        }
                }

            let preview: CatalogPreview =
                {
                    Status = "prepared"
                    Catalog = catalog
                    Selected = Some selected
                }

            let initial, _ = init (syntheticRequest ())
            let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
            let staging, _ = update (PreflightObserved preflightObservation) preflighting
            let composing, _ = update (TemplateObserved templateObservation) staging
            Assert.True(Result.isOk (CatalogScaffoldProvenance.serialize composed))
            let refused, effects = update (WorkspaceComposed composed) composing
            Assert.Equal(Failed, outcome refused)
            Assert.Contains(diagnostics refused, fun diagnostic -> diagnostic.Code = "catalog.observationMismatch")
            Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ RetireOwnedOperation ], effects)


    [<Fact>]
    let ``catalog tool manifest uses actual shared merge and preserves co tenant declarations`` () =
        let original =
            Encoding.UTF8.GetBytes
                """{"version":1,"isRoot":true,"note":"co-tenant","tools":{"other.tool":{"version":"9.0","commands":["other"]}}}"""

        let captured = Array.copy original

        let merged =
            FS.GG.SDD.Commands.Internal.ScaffoldMutation.mergeCatalogToolManifestBytes "1.2.3" original
            |> Result.defaultWith failwith
            |> Option.defaultWith (fun () -> failwith "Missing selected tool entries must be added")

        let expected =
            FS.GG.SDD.Commands.Internal.ScaffoldMutation.mergeToolManifestText
                "1.2.3"
                (Encoding.UTF8.GetString original)
            |> Result.defaultWith failwith
            |> Option.get
            |> Encoding.UTF8.GetBytes

        Assert.True((merged = expected))
        Assert.True((original = captured))
        let node = JsonNode.Parse(Encoding.UTF8.GetString merged) |> nonNull

        let value (path: string list) =
            path
            |> List.fold (fun (parent: JsonNode) (key: string) -> parent[key] |> nonNull) node
            |> fun property -> property.GetValue<string>()

        Assert.Equal("co-tenant", value [ "note" ])
        Assert.Equal("9.0", value [ "tools"; "other.tool"; "version" ])
        Assert.Equal("1.2.3", value [ "tools"; "fs.gg.sdd.cli"; "version" ])

    [<Fact>]
    let ``catalog completed tool manifest needs no rewrite and conflicting pin refuses`` () =
        let seed = FS.GG.SDD.Commands.Internal.ScaffoldMutation.toolManifestText "1.2.3"
        let original = Encoding.UTF8.GetBytes("  " + seed + "  ")
        let captured = Array.copy original

        Assert.Equal(
            Ok None,
            FS.GG.SDD.Commands.Internal.ScaffoldMutation.mergeCatalogToolManifestBytes "1.2.3" original
        )

        Assert.True((original = captured))

        Assert.True(
            Result.isError (FS.GG.SDD.Commands.Internal.ScaffoldMutation.mergeCatalogToolManifestBytes "2.0.0" original)
        )

        Assert.True((original = captured))

    [<Fact>]
    let ``catalog tool manifest rejects invalid bytes and duplicate object names before merge`` () =
        let duplicates =
            [
                """{"version":1,"version":1,"isRoot":true,"tools":{}}"""
                """{"version":1,"isRoot":true,"tools":{"other":{},"other":{}}}"""
                """{"version":1,"isRoot":true,"tools":{"other":{"version":"1","version":"2"}}}"""
                """{"version":1,"isRoot":true,"tools":{},"metadata":{"x":1,"x":2}}"""
                """{"version":1,"isRoot":true,"tools":{},"metadata":[{"x":1,"x":2}]}"""
                "not JSON"
            ]

        for text in duplicates do
            Assert.True(
                Result.isError (
                    FS.GG.SDD.Commands.Internal.ScaffoldMutation.mergeCatalogToolManifestBytes
                        "1.2.3"
                        (Encoding.UTF8.GetBytes text)
                )
            )

        Assert.True(
            Result.isError (
                FS.GG.SDD.Commands.Internal.ScaffoldMutation.mergeCatalogToolManifestBytes "1.2.3" [| 255uy |]
            )
        )

    [<Fact>]
    let ``exact co tenant tool manifest correspondence preserves original observed hash`` () =
        let catalog, selected, policy, archive, preflightObservation, original =
            CatalogWorkflowFixture.value ()

        let sourcePath = ".config/dotnet-tools.json"

        let produced =
            { original.Ownership.ProducedPaths.Head with
                Path = sourcePath
            }

        let observed =
            { original.Observation with
                ProducedPaths = produced :: original.Ownership.SddOwnedPaths
            }

        let changed =
            { produced with
                Sha256 = Some(CatalogWorkflowFixture.digest [| 1uy |])
            }

        let composed =
            { original with
                Ownership =
                    { original.Ownership with
                        ProducedPaths = [ changed ]
                    }
                Observation =
                    { observed with
                        ProducedPaths = changed :: original.Ownership.SddOwnedPaths
                    }
            }

        let preview: CatalogPreview =
            {
                Status = "prepared"
                Catalog = catalog
                Selected = Some selected
            }

        let initial, _ = init (syntheticRequest ())
        let preflighting, _ = update (InputsVerified(preview, policy, archive)) initial
        let staging, _ = update (PreflightObserved preflightObservation) preflighting
        let composing, _ = update (TemplateObserved observed) staging
        Assert.True(Result.isOk (CatalogScaffoldProvenance.serialize composed))
        let committing, effects = update (WorkspaceComposed composed) composing
        Assert.Equal(Committing, phase committing)
        Assert.Equal<FS.GG.SDD.Commands.CatalogScaffoldWorkflow.Effect list>([ CommitWorkspace ], effects)
        Assert.Equal(Some observed, templateObservation committing)
        // Exact actual shared merge bytes and physical identity remain edge obligations.
