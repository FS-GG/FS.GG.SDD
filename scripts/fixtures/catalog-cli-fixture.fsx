// Qualification support calls the existing test request builder and production
// provenance parser. It does not implement catalog/package canonicalization.
open System
open System.IO
open System.Reflection
open System.Text.Json
open System.Collections
open System.Security.Cryptography
open System.IO.Compression
open System.Text
open System.Text.Json.Nodes
open Microsoft.FSharp.Reflection

let args = fsi.CommandLineArgs |> Array.skip 1
let assemblyPath = Path.GetFullPath args[1]
let assemblyDirectory = Path.GetDirectoryName assemblyPath |> Option.ofObj |> Option.get
AppDomain.CurrentDomain.add_AssemblyResolve(ResolveEventHandler(fun _ event ->
    let name = AssemblyName(event.Name).Name |> Option.ofObj |> Option.get
    let path = Path.Combine(assemblyDirectory, name + ".dll")
    if File.Exists path then Assembly.LoadFrom path else null))
// The original test support locates its repository from the host base directory.
// Host the compiled helper at its own build directory, as dotnet test does.
AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", assemblyDirectory)
let assembly = Assembly.LoadFrom assemblyPath
let field name (value: obj) =
    value.GetType().GetProperty(name).GetValue(value)
let text name value = field name value :?> string
let write path (value: obj) = File.WriteAllText(path, JsonSerializer.Serialize(value, JsonSerializerOptions(WriteIndented=true)))
let flags = BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic

let invokeModule (loaded: Assembly) typeName methodName arguments =
    loaded.GetType(typeName, true).GetMethod(methodName, flags).Invoke(null, arguments)
let requireOk value =
    if (field "Tag" value :?> int) <> 0 then failwithf "Production parser refused fixture: %A" value
    field "ResultValue" value
let artifactsAssembly () = Assembly.LoadFrom(Path.Combine(assemblyDirectory, "FS.GG.SDD.Artifacts.dll"))
let digest bytes = invokeModule (artifactsAssembly()) "FS.GG.SDD.Artifacts.ProviderCatalogIntegrity" "digest" [|box bytes|] :?> string
let replaceRecord name replacement (value: obj) =
    let properties = FSharpType.GetRecordFields(value.GetType())
    let values = properties |> Array.map(fun property -> if property.Name = name then replacement else property.GetValue value)
    if not(properties |> Array.exists(fun property -> property.Name = name)) then failwith ("Missing actual record field: " + name)
    FSharpValue.MakeRecord(value.GetType(), values)
let listLike (original: obj) (items: obj list) =
    let cases = FSharpType.GetUnionCases(original.GetType())
    let empty = cases |> Array.find(fun case -> case.GetFields().Length = 0)
    let cons = cases |> Array.find(fun case -> case.GetFields().Length = 2)
    (items, FSharpValue.MakeUnion(empty, [||])) ||> List.foldBack(fun item tail -> FSharpValue.MakeUnion(cons, [|item;tail|]))

match args[0] with
| "seal-policy" ->
    let manifest = JsonNode.Parse(File.ReadAllText args[2])
    let policyBytes = File.ReadAllBytes args[3]
    let policyDigest = digest policyBytes
    let commands = Assembly.LoadFrom(Path.Combine(assemblyDirectory, "FS.GG.SDD.Commands.dll"))
    invokeModule commands "FS.GG.SDD.Commands.CatalogScaffoldPolicy" "parse" [|box policyDigest;box policyBytes|] |> requireOk |> ignore
    manifest["policy"] <- JsonValue.Create(Path.GetFullPath args[3])
    manifest["policyDigest"] <- JsonValue.Create policyDigest
    File.WriteAllText(args[4], manifest.ToJsonString(JsonSerializerOptions(WriteIndented=true)))
| "generate" | "generate-missing-binding" | "generate-reserved-write" | "generate-test-handoff" ->
    let root = Path.GetFullPath args[2]
    let target = Path.GetFullPath args[3]
    Directory.CreateDirectory root |> ignore
    let moduleType = assembly.GetType("FS.GG.SDD.Commands.Tests.CatalogScaffoldRuntimeTests", true)
    let method = moduleType.GetMethod((if args[0] = "generate-test-handoff" then "testHandoffRequest" else "request"), flags)
    if isNull method then failwith "Compiled original fixture request method is unavailable."
    let originalRequest =
        if args[0] = "generate-test-handoff" then
            let executable = Path.GetFullPath args[5]
            if not(File.Exists executable) then failwith "Select the already built real fixture apphost."
            method.Invoke(null, [|box root;box target;box executable;box(File.ReadAllBytes args[6])|])
        else method.Invoke(null, [|box root;box target|])
    let request =
        if args[0] = "generate-reserved-write" then
            let archive = text "TemplateArchive" originalRequest
            do
                use zip = ZipFile.Open(archive, ZipArchiveMode.Update)
                let entry = zip.CreateEntry("content/.fsgg/scaffold-provenance.json", CompressionLevel.NoCompression)
                use output = entry.Open()
                let bytes = Encoding.UTF8.GetBytes "provider-owned reserved fixture\n"
                output.Write(bytes, 0, bytes.Length)
            replaceRecord "ExpectedArchiveDigest" (box(digest(File.ReadAllBytes archive))) originalRequest
        elif args[0] = "generate-missing-binding" then
            let selection = field "Selection" originalRequest
            let artifacts = artifactsAssembly()
            let producer = "FS.GG.SDD.Artifacts.ProviderCatalogIntegrity"
            let catalog = invokeModule artifacts producer "verify" [|field "ExpectedRawDigest" selection;field "CatalogBytes" selection|] |> requireOk
            let providers = field "Providers" catalog
            let descriptor = (providers :?> IEnumerable) |> Seq.cast<obj> |> Seq.exactlyOne
            let capabilities = field "Capabilities" descriptor
            let remaining = (capabilities :?> IEnumerable) |> Seq.cast<obj> |> Seq.filter(fun capability -> text "Id" capability <> "build:build") |> Seq.toList
            if remaining.Length = ((capabilities :?> IEnumerable) |> Seq.cast<obj> |> Seq.length) then failwith "Required build:build fixture binding was absent before mutation."
            let changed = replaceRecord "Capabilities" (listLike capabilities remaining) descriptor
            let descriptorBytes = invokeModule artifacts producer "descriptorBytes" [|changed|] :?> byte array
            let sealedDescriptor = replaceRecord "DescriptorDigest" (box(digest descriptorBytes)) changed
            let changedCatalog = replaceRecord "Providers" (listLike providers [sealedDescriptor]) catalog
            let canonical = invokeModule artifacts producer "catalogBytes" [|changedCatalog|] :?> byte array
            let complete = JsonNode.Parse(Encoding.UTF8.GetString canonical)
            complete["digest"] <- JsonValue.Create(digest canonical)
            let bytes = Encoding.UTF8.GetBytes(complete.ToJsonString())
            let rawDigest = digest bytes
            invokeModule artifacts producer "verify" [|box rawDigest;box bytes|] |> requireOk |> ignore
            let changedSelection = selection |> replaceRecord "CatalogBytes" (box bytes) |> replaceRecord "ExpectedRawDigest" (box rawDigest)
            replaceRecord "Selection" changedSelection originalRequest
        else originalRequest
    let selection = field "Selection" request
    let catalog = Path.Combine(root,"catalog.json")
    let policy = Path.Combine(root,"policy.json")
    File.WriteAllBytes(catalog, field "CatalogBytes" selection :?> byte array)
    File.WriteAllBytes(policy, field "PolicyBytes" request :?> byte array)
    let provider = field "Provider" selection |> field "Value" :?> string
    let overrides = (field "Overrides" selection :?> IEnumerable) |> Seq.cast<obj> |> Seq.map(fun pair -> [|text "Item1" pair;text "Item2" pair|]) |> Seq.toArray
    let generatorHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes assemblyPath)).ToLowerInvariant()
    write args[4] (box {| generatorAssemblySha256=generatorHash;
                         catalog=catalog; catalogDigest=text "ExpectedRawDigest" selection;
                         policy=policy; policyDigest=text "ExpectedPolicyDigest" request;
                         archive=text "TemplateArchive" request; archiveDigest=text "ExpectedArchiveDigest" request;
                         provider=provider; platform=text "SelectedPlatform" request;
                         preflightSeconds=field "PreflightTimeoutSeconds" request :?> int;
                         scaffoldSeconds=field "ScaffoldTimeoutSeconds" request :?> int;
                         overrides=overrides |})
    if args[0] = "generate-test-handoff" then
        let manifest = JsonNode.Parse(File.ReadAllText args[4])
        let executable = Path.GetFullPath args[5]
        manifest["toolId"] <- JsonValue.Create "governance-test-fixture"
        manifest["toolVersion"] <- JsonValue.Create "1.0.0"
        manifest["templateId"] <- JsonValue.Create "fsgg-catalog-opaque-fixture"
        manifest["fixtureExecutable"] <- JsonValue.Create executable
        manifest["fixtureExecutableSha256"] <- JsonValue.Create(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes executable)).ToLowerInvariant())
        manifest["requiredProducedPath"] <- JsonValue.Create "inputs/tests.json"
        manifest["requiredProducedSha256"] <- JsonValue.Create(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes args[6])).ToLowerInvariant())
        manifest["requiredAbsentOutput"] <- JsonValue.Create "out/governance-handoff.json"
        manifest["requiredCapabilityId"] <- JsonValue.Create "test:test"
        manifest["requiredEvidenceFormat"] <- JsonValue.Create "fsgg.governance-handoff@2.0.0"
        manifest["fixtureArguments"] <- JsonNode.Parse("[\"run\",\"--input\",\"inputs/tests.json\",\"--output\",\"out/governance-handoff.json\",\"\",\"with spaces\",\"$(literal);&\"]")
        File.WriteAllText(args[4], manifest.ToJsonString(JsonSerializerOptions(WriteIndented=true)))
| "verify" ->
    let moduleType = assembly.GetType("FS.GG.SDD.Artifacts.CatalogScaffoldProvenance", true)
    let parsed = moduleType.GetMethod("parse", flags).Invoke(null, [|box(File.ReadAllText args[2])|])
    if (field "Tag" parsed :?> int) <> 0 then failwithf "Strict production provenance parser refused: %A" parsed
    let record = field "ResultValue" parsed
    let observation = field "Observation" record
    let ownership = field "Ownership" record
    let paths name = (field name ownership :?> IEnumerable) |> Seq.cast<obj> |> Seq.map(fun p -> text "Path" p) |> Seq.toArray
    let tools = (field "Tools" observation :?> IEnumerable) |> Seq.cast<obj> |> Seq.map(fun tool -> {| id=text "Id" tool;version=text "Version" tool;executable=text "Executable" tool |}) |> Seq.toArray
    let declaration = field "Declaration" record
    let descriptor = field "Descriptor" declaration
    let declaredCommands =
        (field "Capabilities" descriptor :?> IEnumerable) |> Seq.cast<obj> |> Seq.choose(fun capability ->
            let binding = field "Binding" capability
            let case, values = FSharpValue.GetUnionFields(binding, binding.GetType())
            if case.Name <> "Command" then None
            else Some {| capabilityId=text "Id" capability; executable=text "Executable" values[0]
                         arguments=(field "Arguments" values[0] :?> IEnumerable) |> Seq.cast<obj> |> Seq.map string |> Seq.toArray
                         workingDirectory=text "WorkingDirectory" values[1] |}) |> Seq.toArray
    let declaredEvidence =
        (field "Evidence" descriptor :?> IEnumerable) |> Seq.cast<obj> |> Seq.map(fun evidence ->
            {| id=text "Id" evidence;format=text "Format" evidence;path=text "Path" evidence |}) |> Seq.toArray
    let parameters = (field "EffectiveParameters" declaration :?> IEnumerable) |> Seq.cast<obj> |> Seq.map(fun pair -> [|text "Item1" pair;text "Item2" pair|]) |> Seq.toArray
    let invocations = (field "Invocations" observation :?> IEnumerable) |> Seq.cast<obj> |> Seq.map(fun invocation ->
        {| executable=text "Executable" invocation
           arguments=(field "Arguments" invocation :?> IEnumerable) |> Seq.cast<obj> |> Seq.map string |> Seq.toArray
           exitCode=field "ExitCode" invocation :?> int |}) |> Seq.toArray
    write args[3] (box {| declaredCommands=declaredCommands;declaredEvidence=declaredEvidence;
                         effectiveParameters=parameters;invocations=invocations;
                         rawCatalogDigest=text "RawCatalogDigest" declaration;
                         policyDigest=field "Policy" declaration |> text "Digest";
                         archiveDigest=field "Archive" declaration |> text "Digest";
                         schemaVersion=field "SchemaVersion" record :?> int;
                         platform=text "Platform" observation; result=text "Result" observation;
                         producedPaths=paths "ProducedPaths";mirroredPaths=paths "MirroredPaths";sddOwnedPaths=paths "SddOwnedPaths";
                         tools=tools; invocationCount=(field "Invocations" observation :?> IEnumerable |> Seq.cast<obj> |> Seq.length) |})
| _ -> failwith "Expected generate, generate-missing-binding, generate-reserved-write, generate-test-handoff, seal-policy or verify mode."
