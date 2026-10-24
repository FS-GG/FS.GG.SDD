namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Diagnostics
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
    let ``source diagnostic accumulation is bounded while retaining the first cause`` () =
        let initial, _ = ProviderSourceWorkflow.init request
        let errors: Fsgg.ProviderCatalog.Diagnostic list =
            [ for index in 0..999 -> { Code = "source.original"; Path = "$"; Message = string index + System.String('x', 4096) } ]
        let refused, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.FailureObserved errors) initial
        let retained = ProviderSourceWorkflow.diagnostics refused
        Assert.True(retained.Length <= 64)
        Assert.StartsWith("0", retained.Head.Message)
        Assert.True((retained |> List.sumBy (fun item -> System.Text.Encoding.UTF8.GetByteCount(item.Code + item.Path + item.Message))) <= 65536)
        Assert.Contains(retained, fun item -> item.Code = "source.diagnosticsTruncated")

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

        // The public API must refuse before copying/parsing or constructing a runner,
        // even when an earlier raw digest would otherwise fail first.
        for oversized in
            [ { actual with SourceSetBytes = Array.zeroCreate (ProviderSource.limits.MaximumSetBytes + 1) }
              { actual with PolicyBytes = Array.zeroCreate (ProviderSource.limits.MaximumPolicyBytes + 1) } ] do
            match ProviderSourceEffects.prepare oversized System.Threading.CancellationToken.None with
            | Error errors -> Assert.Equal<string list>([ "source.requestBound" ], errors |> List.map _.Code)
            | Ok _ -> failwith "Oversized request constructed source custody."

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

    let private modelWithProvenance text =
        let initial, _ = CommandWorkflow.init (TestSupport.request CommandTypes.SddCommand.Doctor "/synthetic/source-reader")
        let snapshot: FileSnapshot =
            { Path = ScaffoldProvenance.provenancePath; Text = text; RawBytes = Some(System.Text.Encoding.UTF8.GetBytes text) }
        let observed: CommandTypes.CommandEffectResult =
            { Effect = CommandTypes.ReadFile snapshot.Path; Succeeded = true; Read = CommandTypes.Bytes snapshot
              Snapshot = Some snapshot; Process = None; Confirmed = None; Diagnostic = None }
        { initial with InterpretedEffects = [ observed ] }

    [<Fact>]
    let ``doctor retains malformed source provenance and upgrade emits no repair effects`` () =
        let model = modelWithProvenance "{\"schemaVersion\":3}"
        let drift = Internal.HandlersDoctor.computeDrift model
        Assert.True drift.HasProvenance
        Assert.False drift.IsCoherent
        Assert.NotEmpty(Internal.Foundation.scaffoldProvenanceDiagnostics DiagnosticSeverity.DiagnosticWarning model)
        let step: CommandTypes.ReconciliationStep =
            { StepId = CommandTypes.ReconciliationStepId.ArtifactReSeed; Kind = CommandTypes.ReconciliationStepId.ArtifactReSeed
              DiffPreview = "synthetic"; Outcome = CommandTypes.ReconciliationOutcome.WouldApply; TargetPaths = [ "AGENTS.md" ] }
        Assert.True(Internal.HandlersUpgrade.applyEffectsFor model model.Request step |> Result.isError)
        // The read gate precedes any decision: its first emission is read-only skill-root enumeration.
        let _, gateEffects = Internal.HandlersUpgrade.computeUpgradeNext model
        Assert.NotEmpty gateEffects
        Assert.All(gateEffects, fun effect -> Assert.True(match effect with CommandTypes.EnumerateDirectory _ -> true | _ -> false))
        let withListings (source: CommandTypes.CommandModel) =
            let listing effect =
                match effect with
                | CommandTypes.EnumerateDirectory dir ->
                    let snapshot: FileSnapshot = { Path = dir; Text = ""; RawBytes = None }
                    let result: CommandTypes.CommandEffectResult =
                        { Effect = effect; Succeeded = true; Read = CommandTypes.Bytes snapshot
                          Snapshot = Some snapshot; Process = None; Confirmed = None; Diagnostic = None }
                    Some result
                | _ -> None
            { source with InterpretedEffects = source.InterpretedEffects @ List.choose listing Internal.HandlersDoctor.skillRootEnumerations }
        let model = withListings model
        let upgraded, effects = Internal.HandlersUpgrade.computeUpgradeNext model
        Assert.Empty effects
        let summary = upgraded.Upgrade |> Option.get
        Assert.True summary.HasProvenance
        Assert.False summary.AlreadyCoherent
        Assert.True summary.ResidualDrift
        Assert.NotEmpty upgraded.Diagnostics
        let absent = withListings { model with InterpretedEffects = [] }
        let absentUpgrade, absentEffects = Internal.HandlersUpgrade.computeUpgradeNext absent
        Assert.Empty absentEffects
        Assert.False((absentUpgrade.Upgrade |> Option.get).HasProvenance)

    [<Fact>]
    let ``all provenance generations share the same owner reader without flattening the document`` () =
        let value = sourceProvenance ()
        let unwrap = function Ok result -> result | Error errors -> failwithf "%A" errors
        for text in
            [ ScaffoldProvenance.serialize value.Catalog.Ownership
              CatalogScaffoldProvenance.serialize value.Catalog |> unwrap
              SourceScaffoldProvenance.serialize value |> unwrap ] do
            let model = modelWithProvenance text
            Assert.Equal(Some value.Catalog.Ownership, Internal.HandlersDoctor.resolveProvenance model)
            Assert.Empty(Internal.Foundation.scaffoldProvenanceDiagnostics DiagnosticSeverity.DiagnosticError model)

    // Actual local source I/O, with synthetic opaque archives and a declared nonexistent tool.
    // This fixture must only run in the separately admitted source-effects test stage.
    let private withInspectionSources duplicateProvider action =
        let unwrap = function Ok value -> value | Error errors -> failwithf "%A" errors
        let encode (text: string) = System.Text.Encoding.UTF8.GetBytes text
        let root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sdd935-inspection-" + System.Guid.NewGuid().ToString("N"))
        System.IO.Directory.CreateDirectory root |> ignore
        let mutable cleanupUnknown = false
        let retainUnknown () = cleanupUnknown <- true
        let releaseOriginal operation =
            match ProviderSourceEffects.release operation with
            | Ok() -> ()
            | Error errors ->
                retainUnknown ()
                failwithf "Original source custody unresolved; fixture retained at %s: %A" root errors
        try
            let cache = System.IO.Path.Combine(root, "cache")
            System.IO.Directory.CreateDirectory cache |> ignore
            let catalog, _, _, _, _, _ = CatalogWorkflowFixture.value ()
            let create id origin providerIds : ProviderSource.Reference =
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
                { Id = id; Origin = origin; Locator = ProviderSource.LocalDirectory sourceRoot
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
                action sources effectRequest result releaseOriginal
            finally
                releaseOriginal operation
        finally
            // A failed fixture must not turn unresolved release into path deletion.
            if not cleanupUnknown then System.IO.Directory.Delete(root, true)

    [<CatalogNamespaceFact; Trait("kind", "provider-source-native")>]
    let ``no-provider inspection lists every captured provider without resolution or reread`` () =
        withInspectionSources false (fun sources request result _ ->
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

    [<CatalogNamespaceFact; Trait("kind", "provider-source-native")>]
    let ``whole-set provider collision refuses before an inspectable handle exists`` () =
        withInspectionSources true (fun _ request result _ ->
            Assert.Empty(System.IO.Directory.GetFileSystemEntries request.CacheRoot)
            match result with
            | Error errors -> Assert.Contains(errors, fun error -> error.Code = "source.providerCollision")
            | Ok _ -> failwith "A colliding set must not become inspectable or selectable.")

    [<CatalogNamespaceFact; Trait("kind", "provider-source-native")>]
    let ``offline exact cache survives removal of original local sources without acquisition`` () =
        withInspectionSources false (fun sources request result releaseOriginal ->
            let unwrap = function Ok value -> value | Error errors -> failwithf "%A" errors
            let original = result |> unwrap |> ProviderSourceEffects.inspect
            for source in sources do
                match source.Locator with
                | ProviderSource.LocalDirectory path -> System.IO.Directory.Delete(path, true)
                | _ -> failwith "The fixture must remain local."
            let operation = ProviderSourceEffects.prepare { request with Offline = true } System.Threading.CancellationToken.None |> unwrap
            try
                let actual = ProviderSourceEffects.run operation |> Async.RunSynchronously |> unwrap |> ProviderSourceEffects.inspect
                Assert.Equal<string list>(original |> List.map (fun view -> view.Source.Id), actual |> List.map (fun view -> view.Source.Id))
                for view in actual do
                    Assert.Equal(ProviderSource.CacheRead, view.Acquisition)
                    Assert.Empty view.NetworkHops
                    Assert.Empty view.Redirects
                    Assert.Equal(None, view.ResolvedUri)
            finally releaseOriginal operation)

    [<CatalogNamespaceFact; Trait("kind", "provider-source-native")>]
    let ``selected source joins reject changed cache bytes and inspection only scaffold targets`` () =
        withInspectionSources false (fun _ _ result _ ->
            let unwrap = function Ok value -> value | Error errors -> failwithf "%A" errors
            let prepared = result |> unwrap
            let selected = ProviderSourceEffects.select prepared "a-provider" [] |> unwrap
            let original = ProviderSourceEffects.catalogBytes selected
            let exposed = ProviderSourceEffects.catalogSelection selected
            System.Array.Fill(exposed.CatalogBytes, 0uy)
            Assert.Equal<byte array>(original, ProviderSourceEffects.catalogBytes selected)
            let captured = ProviderSourceEffects.inputFiles selected |> List.map (fun file -> file.AbsolutePath, System.IO.File.ReadAllBytes file.AbsolutePath)
            Assert.True(ProviderSourceEffects.verifyCaptured selected captured |> Result.isOk)
            let path, bytes = captured.Head
            Assert.True(ProviderSourceEffects.verifyCaptured selected ((path, Array.append bytes [| 0uy |]) :: captured.Tail) |> Result.isError)
            Assert.True(ProviderSourceEffects.verifyCaptured selected captured.Tail |> Result.isError)
            let actual: CatalogScaffoldWorkflow.CatalogScaffoldRequest =
                { Selection = ProviderSourceEffects.catalogSelection selected; TargetRoot = "/not-created/product"
                  TemplateArchive = ProviderSourceEffects.archivePath selected; ExpectedArchiveDigest = ProviderSourceEffects.archiveSha256 selected
                  PolicyBytes = [||]; ExpectedPolicyDigest = digest; SelectedPlatform = "linux-x64"
                  PreflightTimeoutSeconds = 30; ScaffoldTimeoutSeconds = 60; DryRun = true }
            let host: CatalogScaffoldEffects.HostSelection = { Mode = CatalogScaffoldEffects.LocalLinux; TransportExecutable = "/not-executed/dotnet" }
            match CatalogScaffoldEffects.prepareFromSource host selected actual System.Threading.CancellationToken.None with
            | Error errors -> Assert.Contains(errors, fun error -> error.Code = "source.targetBinding")
            | Ok _ -> failwith "Inspection-only source cannot authorize a later target.")

    let private envelope names =
        use stream = new System.IO.MemoryStream()
        do
            use zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true)
            for name in names do
                use output = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression).Open()
                output.Write([| 1uy; 2uy; 3uy |])
        stream.ToArray()

    [<Fact>]
    let ``actual envelope decoder accepts bounded data and refuses aliases traversal and excess expansion`` () =
        let valid = envelope [ "source.json"; "payload/data.bin" ]
        match ProviderSourceEffects.inspectEnvelope 6L valid with
        | Ok files -> Assert.Equal<string list>([ "source.json"; "payload/data.bin" ], List.map fst files)
        | Error errors -> failwithf "%A" errors
        for bytes, bound, message in
            [ envelope [ "a"; "A" ], 100L, "Directory or duplicate/case-alias ZIP member."
              envelope [ "a"; "A/b.nupkg" ], 100L, "ZIP file/ancestor collision."
              envelope [ "a/b.nupkg"; "A" ], 100L, "ZIP file/ancestor collision."
              envelope [ "source.json"; "SOURCE.JSON/child" ], 100L, "ZIP file/ancestor collision."
              envelope [ "../outside" ], 100L, "Unsafe or excessive ZIP path."
              envelope [ "/absolute" ], 100L, "Unsafe or excessive ZIP path."
              valid, 5L, "ZIP advertised aggregate expansion exceeds the remaining source budget."
              Array.append valid [| 0uy |], 100L, "Missing complete ZIP end record."
              Array.append [| 0uy |] valid, 100L, "Central directory has unaccounted bytes or ZIP64 records." ] do
            // Each negative control must fail for its intended invariant, not any incidental error.
            match ProviderSourceEffects.inspectEnvelope bound bytes with
            | Error errors -> Assert.Equal<string list>([ message ], errors |> List.map (fun error -> error.Message))
            | Ok _ -> failwithf "Expected refusal: %s" message

    [<Fact>]
    let ``envelope decoder accepts DEFLATE members and returns the original bytes`` () =
        use stream = new System.IO.MemoryStream()
        let payload = Array.init 4096 (fun index -> byte (index % 7))
        do
            use zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true)
            for name in [ "source.json"; "payload/data.bin" ] do
                use output = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal).Open()
                output.Write(payload, 0, payload.Length)
        match ProviderSourceEffects.inspectEnvelope 10000L (stream.ToArray()) with
        | Ok files ->
            Assert.Equal<string list>([ "source.json"; "payload/data.bin" ], List.map fst files)
            for _, bytes in files do Assert.Equal<byte[]>(payload, bytes)
        | Error errors -> failwithf "%A" errors

    [<Fact>]
    let ``envelope population allows the fixed manifest plus exactly the maximum payload`` () =
        let names = "source.json" :: [ for index in 1 .. ProviderSource.limits.MaximumMembers -> sprintf "p/%04d" index ]
        let bytes = envelope names
        match ProviderSourceEffects.inspectEnvelope (int64 names.Length * 3L) bytes with
        | Ok files -> Assert.Equal(ProviderSource.limits.MaximumMembers + 1, files.Length)
        | Error errors -> failwithf "%A" errors
        Assert.True(ProviderSourceEffects.inspectEnvelope 20000L (envelope ("extra" :: names)) |> Result.isError)

    [<Fact>]
    let ``actual envelope inventory refuses link mode and local central identity disagreement`` () =
        let bytes = envelope [ "source.json" ]
        let central =
            [ 0 .. bytes.Length - 4 ]
            |> List.find (fun index -> System.BitConverter.ToUInt32(bytes, index) = 0x02014b50u)
        let link = Array.copy bytes
        System.BitConverter.GetBytes(0xa1ff0000u).CopyTo(link, central + 38)
        Assert.True(ProviderSourceEffects.inspectEnvelope 100L link |> Result.isError)
        let changed = Array.copy bytes
        changed[30] <- byte 'x'
        Assert.True(ProviderSourceEffects.inspectEnvelope 100L changed |> Result.isError)
