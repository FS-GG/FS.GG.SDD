namespace ProviderAuthoring

open System
open System.IO
open System.IO.Compression
open System.Text.Json
open System.Text
open System.Text.Json.Nodes
open Fsgg
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands

module Authoring =
    let require = function Ok value -> value | Error diagnostics -> failwithf "Production contract refused: %A" diagnostics
    let catalog (executable: string) : ProviderCatalog.Catalog =
        if not(System.IO.Path.IsPathFullyQualified executable) then invalidArg "executable" "Select an absolute fixture executable."
        let parameter key required nonempty: ProviderCatalog.Parameter =
            { Key=key; Kind=ProviderCatalog.String; Required=required; Prompt=key; Help=key
              Default=Some key; Values=[]
              Validation={NonEmpty=nonempty;MinLength=(if nonempty then 1 else 0);MaxLength=100;AllowedValues=[]} }
        let descriptor: ProviderCatalog.Descriptor =
            { Id="governance-test-fixture";DisplayName="Generic handoff fixture";Help="Explicit test declaration"
              Language="opaque";ProductShape="fixture";DescriptorId="governance-test-fixture-descriptor"
              DescriptorRevision="revision";DescriptorDigest=ProviderCatalogIntegrity.digest [||];ContractVersion="3.0.0"
              TemplateSource="opaque-source";TemplateId="fsgg-catalog-opaque-fixture";Platforms=["linux-x64"]
              Parameters=[parameter "raw" true true;parameter "packageIdentity" true true;parameter "code" true true;parameter "empty" false false;parameter "literal" false true]
              Identities={RawName="raw";PackageIdentity="packageIdentity";CodeIdentifier="code"}
              Tools=[{Id="governance-test-fixture";Version="1.0.0";Platforms=["linux-x64"]}]
              Capabilities=[{Id="test:test";Required=true;Platforms=["linux-x64"];ToolIds=["governance-test-fixture"];EvidenceIds=["fixture-handoff"]
                             Binding=ProviderCatalog.Command({Executable=executable;Arguments=["run";"--input";"inputs/tests.json";"--output";"out/governance-handoff.json";"";"with spaces";"$(literal);&"]},
                                 {WorkingDirectory=".";TimeoutSeconds=30;CostClass="cheap";EnvironmentIds=["fixture-local"]})}]
              Evidence=[{Id="fixture-handoff";Format="fsgg.governance-handoff@2.0.0";Path="out/governance-handoff.json";Required=true}]
              Skills=["opaque-fixture"] }
        let descriptor = {descriptor with DescriptorDigest=ProviderCatalogIntegrity.digest(ProviderCatalogIntegrity.descriptorBytes descriptor)}
        {SchemaVersion=2;Id="design-catalog";Revision="1";Digest=ProviderCatalogIntegrity.digest [||];Providers=[descriptor]}
    let seal (catalog: ProviderCatalog.Catalog) =
        let canonical = ProviderCatalogIntegrity.catalogBytes catalog
        let node = match JsonNode.Parse(Encoding.UTF8.GetString canonical) with null -> failwith "Canonical catalog is absent." | value -> value
        node["digest"] <- JsonValue.Create(ProviderCatalogIntegrity.digest canonical)
        let bytes = Encoding.UTF8.GetBytes(node.ToJsonString())
        ProviderCatalogIntegrity.verify (ProviderCatalogIntegrity.digest bytes) bytes |> require |> ignore
        bytes
    let validate (policyBytes: byte array) (catalogBytes: byte array) =
        let policy = CatalogScaffoldPolicy.parse (ProviderCatalogIntegrity.digest policyBytes) policyBytes |> require
        let catalog = ProviderCatalogIntegrity.verify (ProviderCatalogIntegrity.digest catalogBytes) catalogBytes |> require
        let selected = ProviderCatalog.resolve catalog "governance-test-fixture" ["raw","Awkward name!?";"packageIdentity","example.org/explicit/module";"code","IndependentCode";"empty","";"literal","$(literal);& with spaces"] |> require
        ProviderCapabilityAdmission.resolve policy "linux-x64" selected.Descriptor |> require |> ignore

    type Inputs =
        { TemplateRoot: string; FixtureExecutable: string; TestInput: string; Policy: string; Output: string }
    let private overrides =
        ["raw","Awkward name!?";"packageIdentity","example.org/explicit/module";"code","IndependentCode";"empty","";"literal","$(literal);& with spaces"]
    let private noLink (path: string) =
        if File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint) then invalidArg "path" ("Links are not accepted: " + path)
    let private inputFile path =
        noLink path
        if not(File.Exists path) then invalidArg "path" ("Required regular file is absent: " + path)
        File.ReadAllBytes path
    let private templateEntries (root: string) =
        noLink root
        let content = Path.Combine(root,"content")
        let rec files directory =
            noLink directory
            Directory.GetFileSystemEntries directory
            |> Array.toList
            |> List.collect(fun path ->
                noLink path
                if Directory.Exists path then files path
                elif File.Exists path then
                    let relative = Path.GetRelativePath(content,path).Replace('\\','/')
                    if Path.IsPathFullyQualified relative || (relative.Split('/') |> Array.exists(fun part -> part=".." || part="." || part="")) then
                        invalidArg "templateRoot" "An archive path escapes the selected content root."
                    ["content/"+relative,inputFile path]
                else invalidArg "templateRoot" "Unsupported input filesystem entry.")
        ("FS.GG.SDD.Catalog.OpaqueFixture.nuspec",inputFile(Path.Combine(root,"fixture.nuspec"))) :: files content
    let emit (inputs: Inputs) =
        let root = Path.GetFullPath inputs.TemplateRoot
        let executable = Path.GetFullPath inputs.FixtureExecutable
        if not(Path.IsPathFullyQualified inputs.FixtureExecutable) then invalidArg "fixtureExecutable" "Select a fully qualified executable."
        inputFile executable |> ignore
        let policyBytes = inputFile inputs.Policy
        let testBytes = inputFile inputs.TestInput
        let catalogBytes = catalog executable |> seal
        validate policyBytes catalogBytes
        let selection: CatalogScaffoldWorkflow.CatalogSelection =
            { CatalogBytes=catalogBytes;ExpectedRawDigest=ProviderCatalogIntegrity.digest catalogBytes;Provider=Some "governance-test-fixture";Overrides=overrides }
        CatalogScaffoldWorkflow.prepare selection |> require |> ignore
        let entries = templateEntries root @ ["content/inputs/tests.json",testBytes;"content/out/.gitkeep",[||]]
        let names = System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
        for name,_ in entries do
            if not(names.Add name) then invalidArg "templateRoot" ("Duplicate archive path: " + name)
        let entries = entries |> List.sortWith(fun (a,_) (b,_) -> StringComparer.Ordinal.Compare(a,b))
        let output = Path.GetFullPath inputs.Output
        if File.Exists output || Directory.Exists output then invalidArg "out" "Select an absent output directory."
        let parent = Path.GetDirectoryName output
        if isNull parent || not(Directory.Exists parent) then invalidArg "out" "An existing output parent is required."
        // This sample supports cooperative inputs, not adversarial replacement of its output root.
        Directory.CreateDirectory output |> ignore
        let write name bytes =
            use stream = new FileStream(Path.Combine(output,name),FileMode.CreateNew,FileAccess.Write,FileShare.None)
            stream.Write(bytes: byte array)
        let archivePath = Path.Combine(output,"opaque-fixture.nupkg")
        do
            use stream = new FileStream(archivePath,FileMode.CreateNew,FileAccess.Write,FileShare.None)
            use archive = new ZipArchive(stream,ZipArchiveMode.Create)
            for name,bytes in entries do
                let entry = archive.CreateEntry(name,CompressionLevel.NoCompression)
                entry.LastWriteTime <- DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero)
                entry.ExternalAttributes <- 0
                use destination = entry.Open()
                destination.Write(bytes: byte array)
        write "catalog.json" catalogBytes
        write "policy.json" policyBytes
        let hashFile path = ProviderCatalogIntegrity.digest(inputFile path) |> fun digest -> digest.Substring(7)
        let manifest =
            {| generatorAssemblySha256=hashFile typeof<Inputs>.Assembly.Location
               catalog=Path.Combine(output,"catalog.json");catalogDigest=ProviderCatalogIntegrity.digest catalogBytes
               policy=Path.Combine(output,"policy.json");policyDigest=ProviderCatalogIntegrity.digest policyBytes
               archive=archivePath;archiveDigest=ProviderCatalogIntegrity.digest(inputFile archivePath)
               provider="governance-test-fixture";platform="linux-x64";preflightSeconds=30;scaffoldSeconds=60
               overrides=overrides |> List.map(fun (a,b) -> [|a;b|]) |> List.toArray
               toolId="governance-test-fixture";toolVersion="1.0.0";templateId="fsgg-catalog-opaque-fixture"
               fixtureExecutable=executable;fixtureExecutableSha256=hashFile executable
               requiredProducedPath="inputs/tests.json";requiredProducedSha256=ProviderCatalogIntegrity.digest testBytes |> fun hash -> hash.Substring(7)
               requiredAbsentOutput="out/governance-handoff.json";requiredCapabilityId="test:test"
               requiredEvidenceFormat="fsgg.governance-handoff@2.0.0"
               fixtureArguments=[|"run";"--input";"inputs/tests.json";"--output";"out/governance-handoff.json";"";"with spaces";"$(literal);&"|] |}
        write "request.json" (JsonSerializer.SerializeToUtf8Bytes(manifest,JsonSerializerOptions(WriteIndented=true)))
    let check (provenancePath: string) (summaryPath: string) =
        let record = CatalogScaffoldProvenance.parse(File.ReadAllText provenancePath) |> require
        let descriptor = record.Declaration.Descriptor
        let commands = descriptor.Capabilities |> List.choose(fun capability ->
            match capability.Binding with
            | ProviderCatalog.SemanticOnly -> None
            | ProviderCatalog.Command(command,limits) -> Some {| capabilityId=capability.Id;executable=command.Executable;arguments=List.toArray command.Arguments;workingDirectory=limits.WorkingDirectory |}) |> List.toArray
        let evidence = descriptor.Evidence |> List.map(fun entry -> {| id=entry.Id;format=entry.Format;path=entry.Path |}) |> List.toArray
        let paths entries = entries |> List.map(fun (entry: ScaffoldProvenance.ScaffoldProducedPath) -> entry.Path) |> List.toArray
        let summary =
            {| declaredCommands=commands;declaredEvidence=evidence
               effectiveParameters=record.Declaration.EffectiveParameters |> List.map(fun (a,b) -> [|a;b|]) |> List.toArray
               invocations=record.Observation.Invocations |> List.map(fun invocation -> {| executable=invocation.Executable;arguments=List.toArray invocation.Arguments;exitCode=invocation.ExitCode |}) |> List.toArray
               rawCatalogDigest=record.Declaration.RawCatalogDigest;policyDigest=record.Declaration.Policy.Digest;archiveDigest=record.Declaration.Archive.Digest
               schemaVersion=record.SchemaVersion;platform=record.Observation.Platform;result=record.Observation.Result
               producedPaths=paths record.Ownership.ProducedPaths;mirroredPaths=paths record.Ownership.MirroredPaths;sddOwnedPaths=paths record.Ownership.SddOwnedPaths
               tools=record.Observation.Tools |> List.map(fun tool -> {| id=tool.Id;version=tool.Version;executable=tool.Executable |}) |> List.toArray
               invocationCount=record.Observation.Invocations.Length |}
        use output = new FileStream(summaryPath,FileMode.CreateNew,FileAccess.Write,FileShare.None)
        let bytes = JsonSerializer.SerializeToUtf8Bytes(summary,JsonSerializerOptions(WriteIndented=true))
        output.Write(bytes,0,bytes.Length)
