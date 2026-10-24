namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands
open Xunit

module ProviderSourceWorkflowTests =
    // Synthetic events exercise pure control flow. They are not native acquisition receipts.
    let digest = "sha256:" + System.String('a', 64)
    let request: ProviderSourceWorkflow.Request =
        {
            Sources =
                {
                    Schema = "fsgg.provider-source-set/1"
                    Sources =
                        [
                            {
                                Id = "outside-author"
                                Origin = ProviderSource.Explicit
                                Locator = ProviderSource.LocalDirectory "/selected/source"
                                ManifestSha256 = digest
                            }
                        ]
                }
            SourceSetSha256 = digest
            Policy =
                {
                    Schema = "fsgg.provider-source-policy/1"
                    AllowedOrigins = []
                    MaximumRedirects = 0
                    AcquisitionSeconds = 60
                    MaximumTransferBytes = 1048576L
                    MaximumExpandedBytes = 2097152L
                }
            CacheRoot = "/selected/cache"
            TargetRoot = None
            Offline = false
        }

    [<Fact>]
    let ``offline cache miss never emits acquisition`` () =
        let initial, first = ProviderSourceWorkflow.init { request with Offline = true }
        Assert.Equal<ProviderSourceWorkflow.Effect list>([ ProviderSourceWorkflow.ReadExactCache ], first)
        let refused, effects = ProviderSourceWorkflow.update (ProviderSourceWorkflow.CacheObserved false) initial
        Assert.DoesNotContain(ProviderSourceWorkflow.AcquireSelectedData, effects)
        Assert.NotEqual(ProviderSourceWorkflow.Prepared, ProviderSourceWorkflow.outcome refused)

    [<Fact>]
    let ``verified data is not prepared until original operation retirement succeeds`` () =
        let initial, _ = ProviderSourceWorkflow.init request
        let cached, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.CacheObserved true) initial
        let verified, _ = ProviderSourceWorkflow.update ProviderSourceWorkflow.ValidationCompleted cached
        Assert.NotEqual(ProviderSourceWorkflow.Prepared, ProviderSourceWorkflow.outcome verified)
        let unknown, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.RetirementObserved false) verified
        Assert.Equal(ProviderSourceWorkflow.CleanupUnknown, ProviderSourceWorkflow.outcome unknown)

    [<Fact>]
    let ``later collection and success events cannot replace first failure`` () =
        let initial, _ = ProviderSourceWorkflow.init request
        let failure: Fsgg.ProviderCatalog.Diagnostic =
            { Code = "source.digestMismatch"; Path = "$.members[0]"; Message = "Original mismatch" }
        let failed, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.FailureObserved [ failure ]) initial
        let late, _ = ProviderSourceWorkflow.update ProviderSourceWorkflow.CachePublished failed
        let terminal, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.RetirementObserved true) late
        Assert.NotEqual(ProviderSourceWorkflow.Prepared, ProviderSourceWorkflow.outcome terminal)
        Assert.Equal(failure, ProviderSourceWorkflow.diagnostics terminal |> List.head)

    [<Fact>]
    let ``invalid source declarations have no acquisition or cache effects`` () =
        let invalid = { request with CacheRoot = "relative/cache" }
        let refused, effects = ProviderSourceWorkflow.init invalid
        Assert.Empty effects
        Assert.Equal(ProviderSourceWorkflow.Refused, ProviderSourceWorkflow.outcome refused)

    [<Fact>]
    let ``effect preparation binds raw bytes and performs no path acquisition`` () =
        let unwrap = function
            | Ok value -> value
            | Error errors -> failwithf "%A" errors

        let encode value = System.Text.Encoding.UTF8.GetBytes(value: string)
        let sourceBytes = ProviderSource.serializeSet request.Sources |> unwrap |> encode
        let policyBytes = ProviderSource.serializePolicy request.Policy |> unwrap |> encode
        let actual: ProviderSourceEffects.Request =
            {
                SourceSetBytes = sourceBytes
                ExpectedSourceSetSha256 = ProviderSource.sha256 sourceBytes
                PolicyBytes = policyBytes
                ExpectedPolicySha256 = ProviderSource.sha256 policyBytes
                CacheRoot = "/this-path-does-not-exist/source-cache"
                TargetRoot = None
                Offline = true
            }

        let operation = ProviderSourceEffects.prepare actual System.Threading.CancellationToken.None |> unwrap
        let observation = ProviderSourceEffects.observe operation
        Assert.Equal(ProviderSourceWorkflow.Preparing, observation.Phase)
        Assert.Equal(ProviderSourceWorkflow.Pending, observation.Outcome)
        Assert.Equal<Result<unit, Fsgg.ProviderCatalog.Diagnostic list>>(Ok(), ProviderSourceEffects.release operation)

        match ProviderSourceEffects.prepare { actual with PolicyBytes = sourceBytes } System.Threading.CancellationToken.None with
        | Error errors -> Assert.Contains(errors, fun error -> error.Code = "source.rawDigestMismatch")
        | Ok _ -> failwith "Caller metadata did not bind the actual policy bytes."

    [<Fact>]
    let ``cache staging cannot create or mutate a selected target before validation`` () =
        let overlapping = { request with TargetRoot = Some "/selected" }
        let model, effects = ProviderSourceWorkflow.init overlapping
        Assert.Equal(ProviderSourceWorkflow.Refused, ProviderSourceWorkflow.outcome model)
        Assert.Empty effects

    [<Fact>]
    let ``same archive does not authorize another provider target or literal parameter selection`` () =
        let selected: CatalogScaffoldWorkflow.CatalogSelection =
            {
                CatalogBytes = [| 1uy; 2uy |]
                ExpectedRawDigest = digest
                Provider = Some "first-provider"
                Overrides = [ "name", "literal value"; "argument", "$(literal);&" ]
            }

        let actual: CatalogScaffoldWorkflow.CatalogScaffoldRequest =
            {
                Selection = selected
                TargetRoot = "/selected/product"
                TemplateArchive = "/selected/cache/package.nupkg"
                ExpectedArchiveDigest = digest
                PolicyBytes = [||]
                ExpectedPolicyDigest = digest
                SelectedPlatform = "linux-x64"
                PreflightTimeoutSeconds = 60
                ScaffoldTimeoutSeconds = 60
                DryRun = false
            }

        let check target request =
            ProviderSourceWorkflow.validateScaffoldJoin target selected actual.TemplateArchive digest request

        Assert.Empty(check (Some actual.TargetRoot) actual)
        Assert.NotEmpty(check None actual)
        Assert.NotEmpty(check (Some "/other/product") actual)

        for changed in
            [
                { selected with Provider = Some "second-provider" }
                { selected with Overrides = List.rev selected.Overrides }
                { selected with Overrides = [ "name", "different" ] }
                { selected with CatalogBytes = [| 3uy |] }
            ] do
            Assert.NotEmpty(check (Some actual.TargetRoot) { actual with Selection = changed })

    let private sourceProvenance () : SourceScaffoldProvenance.Record =
        let _, _, _, _, _, record = CatalogWorkflowFixture.value ()
        {
            SchemaVersion = 3
            Catalog = record
            Source =
                {
                    Schema = "fsgg.provider-source-observation/1"
                    SourceSetSha256 = digest
                    AcquisitionPolicySha256 = digest
                    Source = { request.Sources.Sources.Head with ManifestSha256 = digest }
                    Revision = "synthetic-source-revision"
                    Acquisition = ProviderSource.CacheRead
                    ResolvedUri = None
                    Redirects = []
                    NetworkHops = []
                    CatalogSha256 = record.Declaration.RawCatalogDigest
                    TemplateSource = record.Declaration.Descriptor.TemplateSource
                    ArchiveSha256 = record.Declaration.Archive.Digest
                }
        }

    [<Fact>]
    let ``work model classifies actual source provenance as schema3 without changing legacy records`` () =
        let record = sourceProvenance ()
        let unwrap = function
            | Ok value -> value
            | Error errors -> failwithf "%A" errors
        let cases =
            [
                1, ScaffoldProvenance.serialize record.Catalog.Ownership
                2, CatalogScaffoldProvenance.serialize record.Catalog |> unwrap
                3, SourceScaffoldProvenance.serialize record |> unwrap
            ]
        for version, text in cases do
            let snapshot: FileSnapshot =
                { Path = ScaffoldProvenance.provenancePath; Text = text
                  RawBytes = Some(System.Text.Encoding.UTF8.GetBytes text) }
            let parsed = WorkItem.loadWorkItemFromSnapshots [ snapshot ] "synthetic-source"
            let source = Assert.Single parsed.Sources
            Assert.Equal(Some(SchemaVersion.create version), source.SchemaVersion)
            Assert.Equal(SchemaVersion.Current, source.SchemaStatus)
            Assert.Equal(SchemaVersion.sha256Text text, source.Digest)
            Assert.DoesNotContain(parsed.Diagnostics, fun diagnostic ->
                diagnostic.Artifact |> Option.exists (fun artifact -> artifact.Path = ScaffoldProvenance.provenancePath))

    [<Fact>]
    let ``malformed schema3 remains an observed invalid source and is never absent`` () =
        let text = "{\"schemaVersion\":3}"
        let snapshot: FileSnapshot =
            { Path = ScaffoldProvenance.provenancePath; Text = text
              RawBytes = Some(System.Text.Encoding.UTF8.GetBytes text) }
        let parsed = WorkItem.loadWorkItemFromSnapshots [ snapshot ] "synthetic-source"
        let source = Assert.Single parsed.Sources
        Assert.Equal(SchemaVersion.Malformed, source.SchemaStatus)
        Assert.Contains(parsed.Diagnostics, fun diagnostic ->
            diagnostic.Artifact |> Option.exists (fun artifact -> artifact.Path = ScaffoldProvenance.provenancePath))

    // Actual local source I/O, with synthetic opaque archives and a declared nonexistent tool.
    // This fixture must only run in the separately admitted source-effects test stage.
    let private withInspectionSources duplicateProvider action =
        let unwrap = function Ok value -> value | Error errors -> failwithf "%A" errors
        let encode (text: string) = System.Text.Encoding.UTF8.GetBytes text
        let root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sdd935-inspection-" + System.Guid.NewGuid().ToString("N"))
        System.IO.Directory.CreateDirectory root |> ignore
        try
            let cache = System.IO.Path.Combine(root, "cache")
            System.IO.Directory.CreateDirectory cache |> ignore
            let catalog, _, _, _, _, _ = CatalogWorkflowFixture.value ()
            let create id origin providerIds =
                let sourceRoot = System.IO.Path.Combine(root, id)
                System.IO.Directory.CreateDirectory sourceRoot |> ignore
                let descriptors =
                    providerIds |> List.map (fun providerId ->
                        let value = { catalog.Providers.Head with Id = providerId; DescriptorId = providerId }
                        { value with DescriptorDigest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes value) })
                let canonical = ProviderCatalogIntegrity.catalogBytes { catalog with Id = id; Providers = descriptors }
                let node = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Encoding.UTF8.GetString canonical) |> nonNull
                node["digest"] <- System.Text.Json.Nodes.JsonValue.Create(ProviderCatalogIntegrity.digest canonical)
                let catalogBytes = encode (node.ToJsonString())
                let archiveBytes = encode "synthetic opaque archive; never installed or executed"
                let memberOf path (bytes: byte array) : ProviderSource.Member =
                    { Path = path; Bytes = int64 bytes.Length; Sha256 = ProviderSource.sha256 bytes }
                let manifest: ProviderSource.Manifest =
                    { Schema = "fsgg.provider-source/1"; Id = id; Revision = "1"; CatalogPath = "catalog.json"
                      Members = [ memberOf "catalog.json" catalogBytes; memberOf "fixture.nupkg" archiveBytes ]
                      Archives = [ { TemplateSource = catalog.Providers.Head.TemplateSource; Path = "fixture.nupkg" } ] }
                let manifestBytes = ProviderSource.serializeManifest manifest |> unwrap |> encode
                for name, bytes in [ "source.json", manifestBytes; "catalog.json", catalogBytes; "fixture.nupkg", archiveBytes ] do
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(sourceRoot, name), bytes)
                { ProviderSource.Reference.Id = id; Origin = origin; Locator = ProviderSource.LocalDirectory sourceRoot
                  ManifestSha256 = ProviderSource.sha256 manifestBytes }
            let sources =
                [ create "z-explicit" ProviderSource.Explicit [ "z-provider"; "a-provider" ]
                  create "z-workspace" ProviderSource.Workspace [ "last-workspace-provider" ]
                  create "a-workspace" ProviderSource.Workspace [ if duplicateProvider then "a-provider" else "workspace-provider" ]
                  create "z-builtin" ProviderSource.BuiltIn [ "builtin-provider" ] ]
            let setBytes = ProviderSource.serializeSet { Schema = "fsgg.provider-source-set/1"; Sources = sources } |> unwrap |> encode
            let policyBytes = ProviderSource.serializePolicy request.Policy |> unwrap |> encode
            let effectRequest: ProviderSourceEffects.Request =
                { SourceSetBytes = setBytes; ExpectedSourceSetSha256 = ProviderSource.sha256 setBytes
                  PolicyBytes = policyBytes; ExpectedPolicySha256 = ProviderSource.sha256 policyBytes
                  CacheRoot = cache; TargetRoot = None; Offline = false }
            let operation = ProviderSourceEffects.prepare effectRequest System.Threading.CancellationToken.None |> unwrap
            try
                let result = ProviderSourceEffects.run operation |> Async.RunSynchronously
                action sources effectRequest result
            finally
                ProviderSourceEffects.release operation |> unwrap
        finally
            System.IO.Directory.Delete(root, true)

    [<Fact>]
    let ``no-provider inspection lists every captured provider without resolution or reread`` () =
        withInspectionSources false (fun sources request result ->
            let prepared = match result with Ok value -> value | Error errors -> failwithf "%A" errors
            let views = ProviderSourceEffects.inspect prepared
            Assert.Equal<string list>([ "z-builtin"; "a-workspace"; "z-workspace"; "z-explicit" ], views |> List.map (fun view -> view.Source.Id))
            Assert.Equal<string list>([ "a-provider"; "z-provider" ], views[3].Catalog.Catalog.Providers |> List.map (fun provider -> provider.Id))
            for view in views do
                Assert.Equal(None, view.Catalog.Selected)
                Assert.Equal(request.ExpectedSourceSetSha256, view.SourceSetSha256)
                Assert.Equal(request.ExpectedPolicySha256, view.AcquisitionPolicySha256)
                Assert.Equal(ProviderSource.LocalCopy, view.Acquisition)
                Assert.Empty view.NetworkHops
                Assert.Empty view.Redirects
            // Captured inspection remains a historical data view after the original sources disappear.
            // It is not enduring authority for the separately captured scaffold inputs.
            for source in sources do
                match source.Locator with
                | ProviderSource.LocalDirectory path -> System.IO.Directory.Delete(path, true)
                | _ -> failwith "Fixture must remain local-only."
            System.Array.Fill(request.SourceSetBytes, 0uy)
            System.Array.Fill(request.PolicyBytes, 0uy)
            Assert.Equal<ProviderSourceEffects.SourcePreview list>(views, ProviderSourceEffects.inspect prepared))

    [<Fact>]
    let ``whole-set provider collision refuses before an inspectable handle exists`` () =
        withInspectionSources true (fun _ _ result ->
            match result with
            | Error errors -> Assert.Contains(errors, fun error -> error.Code = "source.providerCollision")
            | Ok _ -> failwith "A colliding set must not become inspectable or selectable.")
