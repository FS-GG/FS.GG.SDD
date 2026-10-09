namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.IO.Compression
open System.Text
open System.Text.Json.Nodes
open System.Runtime.InteropServices
open System.Threading
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.CatalogScaffoldWorkflow
open Xunit

module CatalogScaffoldRuntimeTests =
    // Real-port controls, unqualified until the separately selected native test run.
    // The archive contains generic data and one provider skill, not a language project.
    let private platform =
        if OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture = Architecture.X64 then
            "linux-x64"
        else
            "unsupported-fixture-platform"

    let private fixture root =
        let source = Path.Combine(TestSupport.repoRoot, "tests/fixtures/provider-catalog-runtime/opaque-template")
        let archive = Path.Combine(root, "opaque-fixture.nupkg")
        use stream = File.Create archive
        use zip = new ZipArchive(stream, ZipArchiveMode.Create)
        let entry name bytes =
            use destination = zip.CreateEntry(name, CompressionLevel.NoCompression).Open()
            destination.Write(bytes: byte array)
        entry "FS.GG.SDD.Catalog.OpaqueFixture.nuspec" (File.ReadAllBytes(Path.Combine(source,"fixture.nuspec")))
        for path in Directory.GetFiles(Path.Combine(source,"content"),"*",SearchOption.AllDirectories) do
            let relative = Path.GetRelativePath(Path.Combine(source,"content"),path).Replace('\\','/')
            entry ("content/"+relative) (File.ReadAllBytes path)
        archive

    let private policyBytes () =
        let mappings = "[{\"format\":\"opaque@literal\",\"id\":\"opaque-format\",\"version\":\"1.0.0\"}]"
        let mappingDigest = ProviderCatalogIntegrity.digest (Encoding.UTF8.GetBytes mappings)
        let text = """{
          "schemaVersion":1,"id":"runtime-fixture-policy","version":"1.0.0",
          "evidenceMap":{"id":"runtime-fixture-map","version":"1.0.0","digest":"MAPPING_DIGEST","mappings":MAPPINGS},
          "requiredCapabilityIds":["build:build"],"semanticOnlyCapabilityIds":[],
          "knownPlatforms":["PLATFORM"],"environments":[{"id":"fixture-local","environment":"local"}],
          "supportedEvidenceFormats":[{"id":"opaque-format","version":"1.0.0"}],
          "maximumPreflightSeconds":30,"maximumScaffoldSeconds":60,
          "toolProbes":[{"id":"opaque-tool","executable":"/usr/share/dotnet/dotnet","arguments":["--version"],"versionPrefix":"","versionSuffix":"\n","maximumOutputCharacters":128}],
          "archives":[{"templateSource":"opaque-source","packageId":"FS.GG.SDD.Catalog.OpaqueFixture","packageVersion":"1.0.1"}]
        }"""
        text.Replace("MAPPING_DIGEST",mappingDigest).Replace("MAPPINGS",mappings).Replace("PLATFORM",platform)
        |> Encoding.UTF8.GetBytes

    let private selection () =
        let baseCatalog, _, _, _, _, _ = CatalogWorkflowFixture.value ()
        let descriptor = baseCatalog.Providers.Head
        let parameter key defaultValue nonempty: Fsgg.ProviderCatalog.Parameter =
            { Key = key; Kind = Fsgg.ProviderCatalog.String; Required = (key = "raw" || key = "packageIdentity" || key = "code")
              Prompt = key; Help = key; Default = Some defaultValue; Values = []
              Validation = { NonEmpty = nonempty; MinLength = (if nonempty then 1 else 0); MaxLength = 100; AllowedValues = [] } }
        let declared =
            { descriptor with
                TemplateId = "fsgg-catalog-opaque-fixture"
                Platforms = [platform]
                Parameters = [parameter "raw" "raw" true; parameter "packageIdentity" "packageIdentity" true
                              parameter "code" "code" true; parameter "empty" "empty" false; parameter "literal" "literal" true]
                Identities = { RawName = "raw"; PackageIdentity = "packageIdentity"; CodeIdentifier = "code" }
                Tools = [{Id="opaque-tool";Version="10.0.401";Platforms=[platform]}]
                Capabilities = descriptor.Capabilities |> List.map (fun capability -> {capability with Platforms=[platform]})
                Skills = ["opaque-fixture"] }
        let sealedDescriptor = {declared with DescriptorDigest=ProviderCatalogIntegrity.digest(ProviderCatalogIntegrity.descriptorBytes declared)}
        let catalog = {baseCatalog with Providers=[sealedDescriptor]}
        let bytes = ProviderCatalogIntegrity.catalogBytes catalog
        let node = JsonNode.Parse(Encoding.UTF8.GetString bytes) |> nonNull
        node["digest"] <- JsonValue.Create(ProviderCatalogIntegrity.digest bytes)
        let complete = Encoding.UTF8.GetBytes(node.ToJsonString())
        { CatalogBytes=complete; ExpectedRawDigest=ProviderCatalogIntegrity.digest complete
          Provider=Some sealedDescriptor.Id
          Overrides=["raw","Awkward name!?";"packageIdentity","example.org/explicit/module";"code","IndependentCode";"empty","";"literal","$(literal);& with spaces"] }

    [<Fact>]
    let ``opaque runtime fixture identities resolve through the real catalog contract`` () =
        match CatalogScaffoldWorkflow.prepare (selection ()) with
        | Ok _ -> ()
        | Error diagnostics -> failwithf "The actual CLI fixture declaration must prepare before native execution: %A" diagnostics

    [<Fact>]
    let ``actual SDK cache parser handles only full BOM and preserves captured bytes`` () =
        let assembly = typeof<CatalogScaffoldEffects.HostSelection>.Assembly
        let moduleType =
            match assembly.GetType("FS.GG.SDD.Commands.CatalogScaffoldEffects") with
            | null -> failwith "Actual compiled Effects module is unavailable."
            | value -> value
        let method =
            match moduleType.GetMethod("parseSdkCacheJson", System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic) with
            | null -> failwith "Actual SDK-owned cache parser is unavailable."
            | value -> value
        let parse (raw: byte array) = (method.Invoke(null, [|box raw|]) |> nonNull) :?> System.Text.Json.JsonDocument
        let raw = Encoding.UTF8.GetBytes "{\"MountPointsInfo\":{}}"
        let marked = Array.append [|0xEFuy;0xBBuy;0xBFuy|] raw
        let original = Array.copy marked
        use plain = parse raw
        use bom = parse marked
        Assert.Equal<string>(plain.RootElement.GetRawText(), bom.RootElement.GetRawText())
        Assert.True((marked = original))
        for invalid in [ [|0xEFuy;0xBBuy|]; Encoding.UTF8.GetBytes "{malformed" ] do
            let error = Assert.Throws<System.Reflection.TargetInvocationException>(Action(fun () -> use document = parse invalid in ()))
            match error.InnerException with
            | :? System.Text.Json.JsonException -> ()
            | _ -> failwith "Malformed SDK JSON must remain refused by the actual parser."

    [<Fact>]
    let ``actual SDK mount verifier binds captured subset then exact active baseline`` () =
        let assembly = typeof<CatalogScaffoldEffects.HostSelection>.Assembly
        let moduleType =
            match assembly.GetType("FS.GG.SDD.Commands.CatalogScaffoldEffects") with
            | null -> failwith "Actual compiled Effects module is unavailable."
            | value -> value
        let method =
            match moduleType.GetMethod("verifySdkMountInventory", System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic) with
            | null -> failwith "Actual SDK mount verifier is unavailable."
            | value -> value
        let verify captured installed selected mounted =
            (method.Invoke(null, [|box captured;box installed;box selected;box mounted|]) |> nonNull) :?> Set<string>
        let captured = [1..20] |> List.map (fun index -> sprintf "/captured/archive-%d.nupkg" index) |> Set.ofList
        let active = [1..13] |> List.map (fun index -> sprintf "/captured/archive-%d.nupkg" index) |> Set.ofList
        let selected = "/held/selected-fixture.nupkg"
        Assert.True((verify captured false selected active = active))
        let installed = Set.add selected active
        Assert.True((verify active true selected installed = installed))
        let refuses captured installed mounted =
            let error = Assert.Throws<System.Reflection.TargetInvocationException>(Action(fun () -> verify captured installed selected mounted |> ignore))
            Assert.NotNull(error.InnerException)
        refuses captured false (Set.add "/foreign/archive.nupkg" active)
        refuses active true (Set.remove "/captured/archive-1.nupkg" installed)
        refuses active true (Set.add "/additional/archive.nupkg" installed)
        refuses active true active
        refuses active true (Set.add "/wrong/provider.nupkg" active)

    [<Fact>]
    let ``actual diagnostic reserve survives repeated charging of oversized cause`` () =
        let budget = CatalogScaffoldLinux.beginPhase 10 CancellationToken.None
        let oversized: Fsgg.ProviderCatalog.Diagnostic =
            { Code = "fixture.oversized"; Path = "$.fixture"; Message = String('x', 70000) }
        let first = CatalogScaffoldLinux.chargeDiagnostics budget [oversized]
        let second = CatalogScaffoldLinux.chargeDiagnostics budget [oversized]
        Assert.Single(first) |> ignore
        Assert.Equal<string>("catalog.diagnosticLimit", first.Head.Code)
        Assert.True((first = second))

    let private request root target =
        let archive = fixture root
        let policy = policyBytes ()
        { Selection=selection (); TargetRoot=target; TemplateArchive=archive
          ExpectedArchiveDigest=ProviderCatalogIntegrity.digest(File.ReadAllBytes archive)
          PolicyBytes=policy; ExpectedPolicyDigest=ProviderCatalogIntegrity.digest policy
          SelectedPlatform=platform; PreflightTimeoutSeconds=30; ScaffoldTimeoutSeconds=60; DryRun=false }

    // Synthetic declarations; the CLI must independently observe this selected real executable.
    let private testHandoffRequest root target (executable: string) (inputBytes: byte array) =
        if not (Path.IsPathFullyQualified executable) then invalidArg "executable" "Select the absolute fixture apphost."
        let original = request root target
        do
            use zip = ZipFile.Open(original.TemplateArchive, ZipArchiveMode.Update)
            let write name (bytes: byte array) =
                use output = zip.CreateEntry(name, CompressionLevel.NoCompression).Open()
                output.Write(bytes, 0, bytes.Length)
            write "content/inputs/tests.json" inputBytes
            write "content/out/.gitkeep" [||]
        let sourceSelection = original.Selection
        let catalog =
            match ProviderCatalogIntegrity.verify sourceSelection.ExpectedRawDigest sourceSelection.CatalogBytes with
            | Ok value -> value
            | Error diagnostics -> failwithf "Original fixture declaration failed: %A" diagnostics
        let command: Fsgg.Provider.DeclaredCommand =
            { Executable = executable
              Arguments = ["run"; "--input"; "inputs/tests.json"; "--output"; "out/governance-handoff.json"; ""; "with spaces"; "$(literal);&"] }
        let declared =
            { catalog.Providers.Head with
                Id = "governance-test-fixture"
                DescriptorId = "governance-test-fixture-descriptor"
                Tools = [{ Id = "governance-test-fixture"; Version = "1.0.0"; Platforms = [platform] }]
                Capabilities =
                    [{ Id = "test:test"; Required = true; Platforms = [platform]
                       ToolIds = ["governance-test-fixture"]; EvidenceIds = ["fixture-handoff"]
                       Binding = Fsgg.ProviderCatalog.Command(command,
                           { WorkingDirectory = "."; TimeoutSeconds = 30; CostClass = "cheap"; EnvironmentIds = ["fixture-local"] }) }]
                Evidence = [{ Id = "fixture-handoff"; Format = "fsgg.governance-handoff@2.0.0"
                              Path = "out/governance-handoff.json"; Required = true }] }
        let descriptor = { declared with DescriptorDigest = ProviderCatalogIntegrity.digest(ProviderCatalogIntegrity.descriptorBytes declared) }
        let canonical = ProviderCatalogIntegrity.catalogBytes { catalog with Providers = [descriptor] }
        let node = JsonNode.Parse(Encoding.UTF8.GetString canonical) |> nonNull
        node["digest"] <- JsonValue.Create(ProviderCatalogIntegrity.digest canonical)
        let catalogBytes = Encoding.UTF8.GetBytes(node.ToJsonString())
        let mappings = "[{\"format\":\"fsgg.governance-handoff@2.0.0\",\"id\":\"fsgg.governance-handoff\",\"version\":\"2.0.0\"}]"
        let policy = JsonNode.Parse(Encoding.UTF8.GetString original.PolicyBytes) |> nonNull
        policy["requiredCapabilityIds"] <- JsonNode.Parse("[\"test:test\"]")
        (policy["evidenceMap"] |> nonNull)["mappings"] <- JsonNode.Parse mappings
        (policy["evidenceMap"] |> nonNull)["digest"] <- JsonValue.Create(ProviderCatalogIntegrity.digest(Encoding.UTF8.GetBytes mappings))
        policy["supportedEvidenceFormats"] <- JsonNode.Parse("[{\"id\":\"fsgg.governance-handoff\",\"version\":\"2.0.0\"}]")
        let probe = (policy["toolProbes"] |> nonNull)[0] |> nonNull
        probe["id"] <- JsonValue.Create "governance-test-fixture"
        probe["executable"] <- JsonValue.Create executable
        probe["versionPrefix"] <- JsonValue.Create "gov423-test-fixture "
        let selectedPolicy = Encoding.UTF8.GetBytes(policy.ToJsonString())
        { original with
            Selection = { sourceSelection with CatalogBytes = catalogBytes; ExpectedRawDigest = ProviderCatalogIntegrity.digest catalogBytes; Provider = Some descriptor.Id }
            ExpectedArchiveDigest = ProviderCatalogIntegrity.digest(File.ReadAllBytes original.TemplateArchive)
            PolicyBytes = selectedPolicy; ExpectedPolicyDigest = ProviderCatalogIntegrity.digest selectedPolicy }

    [<Fact>]
    let ``synthetic handoff fixture declares actual test command and independent handoff format`` () =
        let root = Path.Combine(Path.GetTempPath(), "sdd-handoff-declaration-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            let executable = Path.Combine(root, "fixture-apphost")
            let input = Encoding.UTF8.GetBytes "{\"schemaVersion\":1}"
            let selected = testHandoffRequest root (Path.Combine(root, "target")) executable input
            let prepared =
                match CatalogScaffoldWorkflow.prepare selected.Selection with
                | Ok preview ->
                    match preview.Selected with
                    | Some value -> value
                    | None -> failwith "The fixture must select its exact declared provider."
                | Error diagnostics -> failwithf "Actual declaration prepare refused: %A" diagnostics
            let policy =
                match CatalogScaffoldPolicy.parse selected.ExpectedPolicyDigest selected.PolicyBytes with
                | Ok value -> value
                | Error diagnostics -> failwithf "Actual independent policy parser refused: %A" diagnostics
            match ProviderCapabilityAdmission.resolve policy platform prepared.Descriptor with
            | Error diagnostics -> failwithf "Actual whole admission refused: %A" diagnostics
            | Ok resolved -> Assert.Equal<int>(1, resolved.Bindings.Length)
            Assert.Equal<string>("test:test", prepared.Descriptor.Capabilities.Head.Id)
            Assert.Equal<string>("fsgg.governance-handoff@2.0.0", prepared.Descriptor.Evidence.Head.Format)
            Assert.Equal<string>(executable, policy.ToolProbes.Head.Executable)
            use archive = ZipFile.OpenRead selected.TemplateArchive
            use contents = (archive.GetEntry("content/inputs/tests.json") |> nonNull).Open()
            use copied = new MemoryStream()
            contents.CopyTo copied
            Assert.True((input = copied.ToArray()))
            Assert.Null(archive.GetEntry("content/out/governance-handoff.json"))
        finally
            Directory.Delete(root, true)

    let private host: CatalogScaffoldEffects.HostSelection =
        { Mode = CatalogScaffoldEffects.LocalLinux
          TransportExecutable = "/usr/share/dotnet/dotnet" }

    let private inMemoryRequest () =
        let policy = policyBytes ()
        { Selection = selection (); TargetRoot = "/nonexistent/catalog-preparation-target"
          TemplateArchive = "/nonexistent/catalog-preparation-template.nupkg"
          ExpectedArchiveDigest = ProviderCatalogIntegrity.digest [||]
          PolicyBytes = policy; ExpectedPolicyDigest = ProviderCatalogIntegrity.digest policy
          SelectedPlatform = platform; PreflightTimeoutSeconds = 30; ScaffoldTimeoutSeconds = 60; DryRun = false }

    [<Fact>]
    let ``catalog preparation constructs an owner without sensing nonexistent paths`` () =
        let unavailableHost = {host with TransportExecutable = "/nonexistent/catalog-preparation-dotnet"}
        let operation =
            CatalogScaffoldEffects.prepare unavailableHost (inMemoryRequest ()) CancellationToken.None
            |> Result.defaultWith(fun diagnostics -> failwithf "Pure preparation refused: %A" diagnostics)
        let observed = CatalogScaffoldEffects.observe operation
        Assert.Equal(CatalogScaffoldEffects.NotStarted, observed.Ownership)
        let model = observed.Model |> Option.defaultWith(fun () -> failwith "Prepared model missing")
        Assert.Equal(Pending, outcome model)
        Assert.Equal(None, preflightObservation model)
        Assert.Equal(None, templateObservation model)

    [<Fact>]
    let ``catalog prepared owner retains input bytes independently of caller mutation`` () =
        let selected = inMemoryRequest ()
        let operation =
            CatalogScaffoldEffects.prepare host selected CancellationToken.None
            |> Result.defaultWith(fun diagnostics -> failwithf "Pure preparation refused: %A" diagnostics)
        selected.Selection.CatalogBytes[0] <- selected.Selection.CatalogBytes[0] ^^^ 1uy
        selected.PolicyBytes[0] <- selected.PolicyBytes[0] ^^^ 1uy
        let model = (CatalogScaffoldEffects.observe operation).Model |> Option.get
        let captured = CatalogScaffoldWorkflow.request model
        Assert.Equal(selected.Selection.ExpectedRawDigest, ProviderCatalogIntegrity.digest captured.Selection.CatalogBytes)
        Assert.Equal(selected.ExpectedPolicyDigest, ProviderCatalogIntegrity.digest captured.PolicyBytes)

    [<Fact>]
    let ``catalog preparation refuses a relative transport before native dispatch`` () =
        let selected = {host with TransportExecutable = "dotnet"}
        match CatalogScaffoldEffects.prepare selected (inMemoryRequest ()) CancellationToken.None with
        | Error diagnostics -> Assert.Contains(diagnostics, fun value -> value.Code = "catalog.transportSelection")
        | Ok _ -> failwith "Relative transport must not infer ambient executable authority"

    [<Fact>]
    let ``catalog unstarted owner releases without acquiring paths or transport`` () =
        let unavailableHost = {host with TransportExecutable = "/nonexistent/catalog-preparation-dotnet"}
        let operation =
            CatalogScaffoldEffects.prepare unavailableHost (inMemoryRequest ()) CancellationToken.None
            |> Result.defaultWith (fun diagnostics -> failwithf "Pure preparation refused: %A" diagnostics)
        Assert.True(Result.isOk(CatalogScaffoldEffects.release operation))
        Assert.Equal(CatalogScaffoldEffects.Settled, (CatalogScaffoldEffects.observe operation).Ownership)
        Assert.True(Result.isOk(CatalogScaffoldEffects.release operation))
        Assert.Equal(None, (CatalogScaffoldEffects.observe operation).Model |> Option.bind templateObservation)

    let private execute request cancellation =
        let operation =
            CatalogScaffoldEffects.prepare host request cancellation
            |> Result.defaultWith (fun diagnostics -> failwithf "Preparation refused: %A" diagnostics)
        let model = CatalogScaffoldEffects.run operation |> Async.RunSynchronously
        match CatalogScaffoldEffects.release operation with
        | Ok () -> model
        | Error diagnostics -> failwithf "Fixture must retain unresolved operation rather than drop it: %A" diagnostics

    [<Fact(Skip = "Requires separately admitted native CatalogScaffold qualification with a retained outer owner.")>]
    let ``real catalog dryrun validates original inputs without observations or target mutation`` () =
        let root = TestSupport.tempDirectory ()
        let target = Path.Combine(root,"product")
        let model = execute {request root target with DryRun=true} CancellationToken.None
        Assert.Equal(Prepared,outcome model)
        Assert.False(Directory.Exists target)
        Assert.Equal(None,preflightObservation model)
        Assert.Equal(None,templateObservation model)
        Assert.Equal(None,provenance model)

    [<Fact(Skip = "Requires separately admitted native CatalogScaffold qualification with a retained outer owner.")>]
    let ``real catalog archive drift refuses before any target mutation`` () =
        let root = TestSupport.tempDirectory ()
        let target = Path.Combine(root,"product")
        let selected = request root target
        File.AppendAllText(selected.TemplateArchive,"tamper")
        let model = execute selected CancellationToken.None
        Assert.Equal(Failed,outcome model)
        Assert.False(Directory.Exists target)
        Assert.Equal(None,templateObservation model)

    [<Fact(Skip = "Requires separately admitted native CatalogScaffold qualification with a retained outer owner.")>]
    let ``real catalog refuses existing target and preserves occupant bytes`` () =
        let root = TestSupport.tempDirectory ()
        let target = Path.Combine(root,"occupied")
        Directory.CreateDirectory target |> ignore
        let occupant = Path.Combine(target,"keep.txt")
        File.WriteAllText(occupant,"owned by existing caller")
        let model = execute (request root target) CancellationToken.None
        Assert.Equal(Failed,outcome model)
        Assert.Equal("owned by existing caller",File.ReadAllText occupant)
        Assert.Equal(NotAttempted,commitOutcome model)

    [<Fact(Skip = "Requires separately admitted native CatalogScaffold qualification with a retained outer owner.")>]
    let ``real catalog observes exact literal template outputs and complete skill ownership before commit`` () =
        let root = TestSupport.tempDirectory ()
        let target = Path.Combine(root,"product")
        let model = execute (request root target) CancellationToken.None
        Assert.Equal(Succeeded,outcome model)
        Assert.Equal(Committed,commitOutcome model)
        Assert.Equal("raw=Awkward name!?\npackage=example.org/explicit/module\ncode=IndependentCode\nempty=\nliteral=$(literal);& with spaces\n",File.ReadAllText(Path.Combine(target,"product.txt")))
        let record = provenance model |> Option.defaultWith (fun () -> failwith "Missing observed success provenance.")
        Assert.Equal("linux-x64",record.Observation.Platform)
        Assert.NotEmpty record.Observation.Invocations
        Assert.Contains(record.Observation.Tools,fun tool -> tool.Id="opaque-tool" && tool.Version="10.0.401")
        Assert.Contains(record.Ownership.ProducedPaths,fun path -> path.Path=".agents/skills/opaque-fixture/SKILL.md")
        Assert.NotEmpty record.Ownership.MirroredPaths
        Assert.NotEmpty record.Ownership.SddOwnedPaths
        Assert.Contains(record.Ownership.SddOwnedPaths, fun path -> path.Path = ".config/dotnet-tools.json")
        let expectedTools = FS.GG.SDD.Commands.Internal.ScaffoldMutation.toolManifestText record.Generator.Version
        Assert.Equal(expectedTools, File.ReadAllText(Path.Combine(target,".config/dotnet-tools.json")))
        Assert.True(File.Exists(Path.Combine(target,ScaffoldProvenance.provenancePath)))

    [<Fact(Skip = "Requires separately admitted native CatalogScaffold qualification with a retained outer owner.")>]
    let ``real catalog cancellation before dispatch leaves target absent`` () =
        let root = TestSupport.tempDirectory ()
        let target = Path.Combine(root,"product")
        use cancellation = new CancellationTokenSource()
        cancellation.Cancel()
        let model = execute (request root target) cancellation.Token
        Assert.Equal(Failed,outcome model)
        Assert.False(Directory.Exists target)
        Assert.Equal(None,templateObservation model)

    [<Fact>]
    let ``catalog owner preparation performs no filesystem acquisition or child dispatch`` () =
        let selection = selection ()
        let policy = policyBytes ()
        let input: CatalogScaffoldRequest =
            { Selection = selection
              TargetRoot = "/unobserved-catalog-owner-parent/product"
              TemplateArchive = "/unobserved-catalog-owner-input/template.nupkg"
              ExpectedArchiveDigest = "sha256:" + String.replicate 64 "a"
              PolicyBytes = policy
              ExpectedPolicyDigest = ProviderCatalogIntegrity.digest policy
              SelectedPlatform = platform
              PreflightTimeoutSeconds = 30
              ScaffoldTimeoutSeconds = 60
              DryRun = true }
        let operation =
            CatalogScaffoldEffects.prepare host input CancellationToken.None
            |> Result.defaultWith (fun diagnostics -> failwithf "Pure preparation refused: %A" diagnostics)
        let observed = CatalogScaffoldEffects.observe operation
        Assert.Equal(CatalogScaffoldEffects.NotStarted, observed.Ownership)
        Assert.Empty(observed.Diagnostics)
        Assert.Equal(None, observed.Model |> Option.bind preflightObservation)
        Assert.Equal(None, observed.Model |> Option.bind templateObservation)
        Assert.Equal(Ok (), CatalogScaffoldEffects.release operation)

    [<Fact>]
    let ``catalog pure owner preparation refuses ambient transport executable selection`` () =
        let selection = selection ()
        let policy = policyBytes ()
        let input: CatalogScaffoldRequest =
            { Selection = selection
              TargetRoot = "/unobserved-catalog-owner-parent/product"
              TemplateArchive = "/unobserved-catalog-owner-input/template.nupkg"
              ExpectedArchiveDigest = "sha256:" + String.replicate 64 "a"
              PolicyBytes = policy
              ExpectedPolicyDigest = ProviderCatalogIntegrity.digest policy
              SelectedPlatform = platform
              PreflightTimeoutSeconds = 30
              ScaffoldTimeoutSeconds = 60
              DryRun = false }
        match CatalogScaffoldEffects.prepare { host with TransportExecutable = "dotnet" } input CancellationToken.None with
        | Ok _ -> failwith "Ambient executable must not construct an admitted owner."
        | Error diagnostics -> Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Code = "catalog.transportSelection")


    [<Fact(Skip = "Requires separately admitted native CatalogScaffold qualification with a retained outer owner.")>]
    let ``real catalog co tenant tool merge retains original evidence and exact final shared bytes`` () =
        let root = TestSupport.tempDirectory ()
        let target = Path.Combine(root, "product")
        let selected = request root target
        let originalBytes = Encoding.UTF8.GetBytes """{"version":1,"isRoot":true,"note":"provider","tools":{"other.tool":{"version":"9.0","commands":["other"]}}}"""
        // Explicit new archive fixture candidate; the selected digest covers these exact bytes.
        do
            use archive = ZipFile.Open(selected.TemplateArchive, ZipArchiveMode.Update)
            use destination = archive.CreateEntry("content/.config/dotnet-tools.json", CompressionLevel.NoCompression).Open()
            destination.Write originalBytes
        let updated =
            { selected with ExpectedArchiveDigest = ProviderCatalogIntegrity.digest(File.ReadAllBytes selected.TemplateArchive) }
        let model = execute updated CancellationToken.None
        Assert.Equal(Succeeded, outcome model)
        let record = provenance model |> Option.get
        let expected =
            FS.GG.SDD.Commands.Internal.ScaffoldMutation.mergeCatalogToolManifestBytes record.Generator.Version originalBytes
            |> Result.defaultWith failwith
            |> Option.get
        let actual = File.ReadAllBytes(Path.Combine(target,".config/dotnet-tools.json"))
        Assert.True((actual = expected))
        let originalObservation = templateObservation model |> Option.get
        let originalPath = originalObservation.ProducedPaths |> List.find (fun path -> path.Path = ".config/dotnet-tools.json")
        Assert.Equal(Some(ProviderCatalogIntegrity.digest originalBytes), originalPath.Sha256)
        let finalPath = record.Ownership.ProducedPaths |> List.find (fun path -> path.Path = ".config/dotnet-tools.json")
        Assert.Equal(Some(ProviderCatalogIntegrity.digest expected), finalPath.Sha256)
        Assert.Equal(ArtifactRef.GeneratedProduct, finalPath.Owner)
        Assert.DoesNotContain(record.Ownership.SddOwnedPaths, fun path -> path.Path = ".config/dotnet-tools.json")

    [<Fact>]
    let ``released prepared catalog owner cannot dispatch its cold runner`` () =
        let operation =
            CatalogScaffoldEffects.prepare host (inMemoryRequest ()) CancellationToken.None
            |> Result.defaultWith (fun diagnostics -> failwithf "Pure preparation refused: %A" diagnostics)
        Assert.Equal(Ok (), CatalogScaffoldEffects.release operation)
        Assert.Throws<InvalidOperationException>(fun () ->
            CatalogScaffoldEffects.run operation |> Async.RunSynchronously |> ignore) |> ignore
        let observed = CatalogScaffoldEffects.observe operation
        Assert.Equal(CatalogScaffoldEffects.Settled, observed.Ownership)
        Assert.Equal(None, observed.Model |> Option.bind preflightObservation)
        Assert.Equal(None, observed.Model |> Option.bind templateObservation)
        Assert.Equal(None, observed.Model |> Option.bind provenance)
