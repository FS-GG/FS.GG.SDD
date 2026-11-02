namespace FS.GG.SDD.Cli

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading.Tasks
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.TypedSpecifications

/// Controlled execution of one accepted profile-2 workspace against the SDD reducer.
module internal QuintSync =
    type private Step = { Name: string; ExitCode: int; Output: string; Sha256: string }
    type private Report =
        { Outcome: string; AcceptedSha256: string; ContractSha256: string; GeneratedSha256: string
          ToolSha256: string; HostToolSha256: string; VerifierToolSha256: string
          ImplementationSha256: string; AdapterSha256: string; CorrespondenceSha256: string
          RunSha256: string; PackageIdentity: string; AcceptedRevision: int64; Seed: int64; MaxSamples: int64; MaxSteps: int64
          ScenarioCount: int; ActionCount: int; Steps: Step list; Diagnostics: string list }

    let private sha (bytes: byte array) =
        SHA256.HashData bytes |> Convert.ToHexString |> _.ToLowerInvariant()

    let private optionValue name args =
        args |> List.tryFindIndex ((=) name) |> Option.bind (fun index -> List.tryItem (index + 1) args)

    let private report outcome diagnostics =
        { Outcome = outcome; AcceptedSha256 = ""; ContractSha256 = ""; GeneratedSha256 = ""
          ToolSha256 = ""; HostToolSha256 = ""; VerifierToolSha256 = ""
          ImplementationSha256 = ""; AdapterSha256 = ""; CorrespondenceSha256 = ""
          RunSha256 = ""; PackageIdentity = ""; AcceptedRevision = 0L; Seed = 0L; MaxSamples = 0L; MaxSteps = 0L
          ScenarioCount = 0; ActionCount = 0; Steps = []; Diagnostics = diagnostics }

    let private render value =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.typed-sdd-sync-report/v1")
        writer.WriteString("operation", "sync")
        writer.WriteString("outcome", value.Outcome)
        writer.WriteString("acceptedSha256", value.AcceptedSha256)
        writer.WriteString("contractSha256", value.ContractSha256)
        writer.WriteString("generatedSha256", value.GeneratedSha256)
        writer.WriteString("toolSha256", value.ToolSha256)
        writer.WriteString("hostToolSha256", value.HostToolSha256)
        writer.WriteString("verifierToolSha256", value.VerifierToolSha256)
        writer.WriteString("implementationSha256", value.ImplementationSha256)
        writer.WriteString("adapterSha256", value.AdapterSha256)
        writer.WriteString("correspondenceSha256", value.CorrespondenceSha256)
        writer.WriteString("runSha256", value.RunSha256)
        writer.WriteString("packageIdentity", value.PackageIdentity)
        writer.WriteNumber("acceptedRevision", value.AcceptedRevision)
        writer.WriteNumber("seed", value.Seed)
        writer.WriteNumber("maxSamples", value.MaxSamples)
        writer.WriteNumber("maxSteps", value.MaxSteps)
        writer.WriteNumber("scenarioCount", value.ScenarioCount)
        writer.WriteNumber("actionCount", value.ActionCount)
        writer.WriteStartArray("steps")
        for step in value.Steps do
            writer.WriteStartObject()
            writer.WriteString("name", step.Name)
            writer.WriteNumber("exitCode", step.ExitCode)
            writer.WriteString("sha256", step.Sha256)
            writer.WriteString("output", step.Output)
            writer.WriteEndObject()
        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")
        value.Diagnostics |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let private emit format value =
        if format = "json" then Console.Out.WriteLine(render value)
        else
            Console.Out.WriteLine($"typed-sdd sync: {value.Outcome}")
            if value.AcceptedSha256 <> "" then
                Console.Out.WriteLine($"  accepted: {value.AcceptedSha256}")
                Console.Out.WriteLine($"  contract: {value.ContractSha256}")
                Console.Out.WriteLine($"  generated: {value.GeneratedSha256}")
                Console.Out.WriteLine($"  implementation: {value.ImplementationSha256}")
                Console.Out.WriteLine($"  adapter: {value.AdapterSha256}")
                Console.Out.WriteLine($"  tool: {value.ToolSha256}")
                Console.Out.WriteLine($"  host tools: {value.HostToolSha256}")
                Console.Out.WriteLine($"  verifier: {value.VerifierToolSha256}")
                Console.Out.WriteLine($"  correspondence: {value.CorrespondenceSha256}")
                Console.Out.WriteLine($"  run: {value.RunSha256}")
                Console.Out.WriteLine($"  package: {value.PackageIdentity}")
                Console.Out.WriteLine($"  bounds: seed {value.Seed}, samples {value.MaxSamples}, steps {value.MaxSteps}")
                Console.Out.WriteLine($"  bounds checked: {value.ScenarioCount} scenarios, {value.ActionCount} adapter actions")
            for step in value.Steps do
                Console.Out.WriteLine($"  {step.Name}: exit {step.ExitCode}, output sha256 {step.Sha256}")
            for reason in value.Diagnostics do Console.Out.WriteLine($"  blocked: {reason}")
        if value.Outcome = "satisfied" then 0 else 1

    let private contained (root: string) (relative: string) =
        if Path.IsPathRooted relative then None
        else
            let full = Path.GetFullPath(Path.Combine(root, relative))
            let prefix = root.TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar
            if full.StartsWith(prefix, StringComparison.Ordinal) then Some full else None

    let private artifact root (manifest: QuintAuthorityManifest) id =
        manifest.Artifacts
        |> List.tryFind (fun item -> item.Id = id)
        |> Option.bind (fun item ->
            contained root item.Path
            |> Option.bind (fun path ->
                if File.Exists path then
                    let bytes = File.ReadAllBytes path
                    if sha bytes = item.Sha256 then Some(item, bytes) else None
                else None))

    let private execute environment tool cwd name args =
        try
            let start = ProcessStartInfo("/usr/bin/unshare")
            start.WorkingDirectory <- cwd
            start.UseShellExecute <- false
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true
            start.Environment.Clear()
            start.Environment["LANG"] <- "C.UTF-8"
            start.Environment["TZ"] <- "UTC"
            for key, value in environment do
                start.Environment[key] <- value
            let launch =
                if environment |> List.exists (fst >> ((=) "QUINT_HOME")) then
                    [ "--user"; "--map-root-user"; "--net"; "--"; "/usr/bin/sh"; "-c"
                      "/usr/sbin/ip link set lo up && exec \"$@\""; "quint-sync"; tool ] @ args
                else
                    [ "--user"; "--map-root-user"; "--net"; "--"; tool ] @ args
            for part in launch do
                start.ArgumentList.Add part
            use child = Process.Start start |> Option.ofObj |> Option.defaultWith (fun () -> failwith "process did not start")
            let readBounded (reader: StreamReader) =
                task {
                    let buffer = Array.zeroCreate<char> 4096
                    let retained = StringBuilder()
                    let mutable total = 0
                    let mutable reading = true
                    while reading do
                        let! count = reader.ReadAsync(buffer.AsMemory())
                        if count = 0 then reading <- false
                        else
                            total <- total + count
                            if retained.Length < 524288 then
                                retained.Append(buffer, 0, min count (524288 - retained.Length)) |> ignore
                    return retained.ToString(), total
                }
            let stdout = readBounded child.StandardOutput
            let stderr = readBounded child.StandardError
            if not (child.WaitForExit 60000) then
                child.Kill(true)
                Error $"{name}: process exceeded 60 seconds"
            else
                Task.WhenAll([| stdout :> Task; stderr :> Task |]).GetAwaiter().GetResult()
                let outText, outCount = stdout.Result
                let errText, errCount = stderr.Result
                let output = outText + errText
                if outCount + errCount > 1048576 || Encoding.UTF8.GetByteCount output > 1048576 then
                    Error $"{name}: output exceeded 1 MiB"
                else
                    let step = { Name = name; ExitCode = child.ExitCode; Output = output.Trim(); Sha256 = sha (Encoding.UTF8.GetBytes output) }
                    if child.ExitCode = 0 then Ok step else Error $"{name}: exit {child.ExitCode}: {step.Output}"
        with ex -> Error $"{name}: {ex.Message}"

    let private treeSha root =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun path -> path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/'), path)
        |> Seq.sortBy fst
        |> Seq.map (fun (relative, path) -> $"{sha (File.ReadAllBytes path)}  ./{relative}\n")
        |> String.concat ""
        |> Encoding.UTF8.GetBytes
        |> sha

    // The launcher is part of the qualified executable closure: it sets up the
    // isolated loopback used by Quint and its Apalache child.
    let private hostToolClosure () =
        [ "/usr/bin/unshare"; "/usr/bin/sh"; "/usr/sbin/ip" ]
        |> List.map (fun path ->
            if not (File.Exists path) then Error $"Required isolated-loopback utility {path} is unavailable."
            else Ok(path, sha (File.ReadAllBytes path)))
        |> List.fold (fun state item ->
            match state, item with
            | Ok values, Ok value -> Ok(value :: values)
            | Error reason, _ -> Error reason
            | _, Error reason -> Error reason) (Ok [])
        |> Result.map List.rev

    type private RevisionLink =
        { ManifestSha256: string; Revision: int64; WorkspaceFingerprint: string; PriorManifestSha256: string }

    let private revisionPath work = Path.Combine("readiness", work, "quint", "sync-authority.json")

    let private encodeRevisionLink value =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.typed-sdd-sync-authority/v1")
        writer.WriteString("manifestSha256", value.ManifestSha256)
        writer.WriteNumber("revision", value.Revision)
        writer.WriteString("workspaceFingerprint", value.WorkspaceFingerprint)
        writer.WriteString("priorManifestSha256", value.PriorManifestSha256)
        writer.WriteEndObject()
        writer.Flush()
        stream.WriteByte(byte '\n')
        stream.ToArray()

    let private readRevisionLink root work expectedManifestSha =
        let path = Path.Combine(root, revisionPath work)
        if not (File.Exists path) then Ok None
        else
            try
                use document = JsonDocument.Parse(File.ReadAllBytes path)
                let value = document.RootElement
                let fields = value.EnumerateObject() |> Seq.map _.Name |> Seq.toList
                if value.ValueKind <> JsonValueKind.Object
                   || fields <> [ "schema"; "manifestSha256"; "revision"; "workspaceFingerprint"; "priorManifestSha256" ] then
                    Error "Accepted sync revision link has an unsupported shape."
                elif value.GetProperty("schema").GetString() <> "fsgg.typed-sdd-sync-authority/v1" then
                    Error "Accepted sync revision link schema mismatches."
                else
                    let readString (name: string) = value.GetProperty(name).GetString() |> Option.ofObj |> Option.defaultValue ""
                    let link =
                        { ManifestSha256 = readString "manifestSha256"
                          Revision = value.GetProperty("revision").GetInt64()
                          WorkspaceFingerprint = readString "workspaceFingerprint"
                          PriorManifestSha256 = readString "priorManifestSha256" }
                    if link.ManifestSha256 <> expectedManifestSha || link.Revision < 0L
                       || not (Regex.IsMatch(link.WorkspaceFingerprint, "^[0-9a-f]{64}$")) then
                        Error "Accepted sync revision link does not bind the current manifest and workspace."
                    else Ok(Some link)
            with ex -> Error $"Accepted sync revision link is unreadable: {ex.Message}"

    let private deriveAccepted root work manifestBytes contract =
        match readRevisionLink root work (sha manifestBytes) with
        | Error reason -> Error reason
        | Ok link ->
            let revision = link |> Option.map _.Revision |> Option.defaultValue 0L
            match WorkspaceSync.deriveModel revision contract with
            | Error findings -> Error(sprintf "Workspace authority join refused: %s" (findings |> List.map _.Code |> String.concat ", "))
            | Ok accepted ->
                match WorkspaceLifecycle.fingerprint accepted with
                | Error findings -> Error(sprintf "Workspace fingerprint refused: %A" findings)
                | Ok fingerprint ->
                    match link with
                    | Some prior when prior.WorkspaceFingerprint <> fingerprint ->
                        Error "Accepted sync revision link fingerprint differs from the compiled workspace."
                    | _ -> Ok accepted

    let private correspondence (accepted: WorkspaceModel) contract sourcePath implementationPath testDigest verifyDigest =
        match WorkspaceLifecycle.fingerprint accepted with
        | Error findings -> Error(sprintf "Accepted workspace fingerprint failed: %A" findings)
        | Ok fingerprint ->
            let observations =
                [
                    for obligation in accepted.Modules do
                        if obligation.Kind = WorkspaceModuleKind.EvidenceRequirement then
                            let subjects =
                                accepted.Modules
                                |> List.filter (fun item ->
                                    item.Id = obligation.Id
                                    || List.contains obligation.Id item.EvidenceObligationIds)
                                |> List.map (fun item -> SpecificationId.value item.Id)
                                |> Set.ofList
                            let testBindings =
                                contract.VerificationProfiles
                                |> List.filter (fun item ->
                                    item.Kind = "scenario"
                                    && (item.SubjectIds |> List.exists (fun subject -> Set.contains subject subjects)))
                                |> List.map (fun item -> "WorkspaceLifecycleTests." + item.Id.Substring(5))
                                |> List.sort
                            for kind in
                                [ CorrespondenceObservationKind.GeneratedContract
                                  CorrespondenceObservationKind.SourceBinding
                                  CorrespondenceObservationKind.Test
                                  CorrespondenceObservationKind.EvidenceReceipt ] do
                                yield
                                    { ObligationId = obligation.Id
                                      Kind = kind
                                      AcceptedFingerprint = fingerprint
                                      SubjectFingerprint = Some obligation.ContentSha256
                                      State = CorrespondenceObservationState.Observed
                                      SourceBindings = [ sourcePath; implementationPath ]
                                      TestBindings = testBindings
                                      EvidenceRefs = [ "quint:test/" + testDigest; "quint:verify/" + verifyDigest ]
                                      Explanation = "Controlled sync executed the selected model and production reducer adapter." }
                ]
            if observations |> List.exists (fun item -> item.TestBindings.IsEmpty) then
                Error "An accepted evidence obligation has no declared scenario binding."
            else
                match WorkspaceCorrespondence.evaluate accepted contract observations CorrespondenceScope.All with
                | Error findings -> Error(sprintf "Correspondence refused: %A" findings)
                | Ok result when result.Entries.IsEmpty || not result.Diagnostics.IsEmpty ->
                    Error "Correspondence has missing, stale, contradicted or unobserved obligations."
                | Ok result ->
                    WorkspaceCorrespondence.serializeReport result
                    |> Encoding.UTF8.GetBytes
                    |> sha
                    |> Ok

    let private runAccepted root work cache javaHome apalacheDist =
        let manifestPath = Path.Combine(root, TypedAuthorityManifest.path work)
        if not (File.Exists manifestPath) then Error "The accepted Typed SDD authority manifest is missing."
        else
            let manifestBytes = File.ReadAllBytes manifestPath
            let linkPath = Path.Combine(root, revisionPath work)
            let linkDigest = if File.Exists linkPath then sha (File.ReadAllBytes linkPath) else ""
            match Encoding.UTF8.GetString manifestBytes |> TypedAuthority.deserialize with
            | Error finding -> Error $"Authority is invalid: {finding.Id}: {finding.Message}"
            | Ok(FsharpSpecificationV1 _) -> Error "F# authority is not a selected Quint sync workspace."
            | Ok(QuintSpecificationV1 manifest) when manifest.ProfileIdentity <> QuintGeneralProfile.identity ->
                Error "Selected Quint authority does not use profile 2."
            | Ok(QuintSpecificationV1 manifest) ->
                let observed =
                    manifest.Artifacts
                    |> List.map (fun item ->
                        { Path = item.Path
                          State =
                            match contained root item.Path with
                            | None -> QuintAuthorityArtifactState.Unreadable "Path escapes root."
                            | Some path when File.Exists path -> QuintAuthorityArtifactState.Present(File.ReadAllBytes path)
                            | Some _ -> QuintAuthorityArtifactState.Missing })
                let identity = $"FS.GG.SDD.Artifacts/{SchemaVersion.currentGeneratorVersion().Version}"
                match TypedAuthority.validateQuintV2 identity observed manifest with
                | findings when not (List.isEmpty findings) ->
                    Error(sprintf "Authority closure is invalid: %s" (findings |> List.map _.Id |> String.concat ", "))
                | _ ->
                    match artifact root manifest "compiled-contract", artifact root manifest "generated-modules", artifact root manifest "markdown" with
                    | Some(contractArtifact, contractBytes), Some(generatedArtifact, generatedBytes), Some(sourceArtifact, sourceBytes) ->
                        match QuintContractV2.deserialize (Encoding.UTF8.GetString contractBytes) with
                        | Error findings -> Error(sprintf "Compiled contract invalid: %s" (findings |> List.map _.Code |> String.concat ", "))
                        | Ok contract ->
                            match deriveAccepted root work manifestBytes contract with
                            | Error reason -> Error reason
                            | Ok accepted ->
                                if contract.Specification <> "WorkspaceLifecycle" then
                                    Error "Only the selected WorkspaceLifecycle production reducer adapter is installed."
                                elif not (accepted.Modules |> List.exists (fun item -> item.Kind = WorkspaceModuleKind.ProductSpecification)) then
                                    Error "The WorkspaceLifecycle production reducer adapter requires an accepted product specification."
                                else
                                    let scenarios = contract.VerificationProfiles |> List.filter (fun item -> item.Kind = "scenario")
                                    let invariants = contract.VerificationProfiles |> List.filter (fun item -> item.Kind = "invariant")
                                    let required = contract.VerificationProfiles |> List.filter (fun item -> item.Kind = "model-check")
                                    let bound name = contract.Bounds |> List.tryFind (fun item -> item.Id = name) |> Option.map _.Maximum
                                    if scenarios.IsEmpty || invariants.IsEmpty || required.IsEmpty then
                                        Error "Scenario, invariant and model-check declarations are all required."
                                    elif [ "SYNC-MAX-SAMPLES"; "SYNC-MAX-STEPS"; "SYNC-SEED" ] |> List.exists (bound >> Option.isNone) then
                                        Error "Finite SYNC-MAX-SAMPLES, SYNC-MAX-STEPS and SYNC-SEED bounds are required."
                                    elif scenarios |> List.exists (fun item -> not (Regex.IsMatch(item.Id, "^TEST-[A-Za-z][A-Za-z0-9]*$"))) then
                                        Error "Scenario ids must bind TEST-<Quint-test-name>."
                                    elif invariants |> List.exists (fun item -> not (Regex.IsMatch(item.Id, "^INVAR-[A-Za-z][A-Za-z0-9]*$"))) then
                                        Error "Invariant ids must bind INVAR-<Quint-value-name>."
                                    elif not (scenarios |> List.exists (fun item -> item.Id = "TEST-coherentDeltaCanReachAcceptance")) then
                                        Error "The production reducer requires a declared coherentDeltaCanReachAcceptance witness."
                                    elif (WorkspaceSync.select None accepted contract []).ObligationIds.IsEmpty then
                                        Error "Selected authority has no evidence obligation."
                                    else
                                        let javaPath = Path.Combine(javaHome, "bin", "java")
                                        let hostTools = hostToolClosure ()
                                        let hostToolError =
                                            match hostTools with Error reason -> Some reason | Ok _ -> None
                                        if hostToolError.IsSome then
                                            Error(hostToolError.Value)
                                        elif not (File.Exists javaPath) || sha (File.ReadAllBytes javaPath) <> "e865867065e48928c58293f30e7ae26a79c842f8607fa51d7e2e9fb90b602786" then
                                            Error "Exact Temurin21.0.9+10 java binary is unavailable or mismatched."
                                        elif not (File.Exists(Path.Combine(apalacheDist, "apalache", "lib", "apalache.jar"))) then
                                            Error "Exact Apalache0.56.1 distribution is unavailable."
                                        elif treeSha apalacheDist <> "3466d07f06d7ac80ee0f171a96383183cee9d91bf1b5995d897d4f15c004569f" then
                                            Error "Apalache0.56.1 distribution tree mismatches."
                                        else
                                            let maxSamples = bound "SYNC-MAX-SAMPLES" |> Option.get
                                            let maxSteps = bound "SYNC-MAX-STEPS" |> Option.get
                                            let seed = bound "SYNC-SEED" |> Option.get
                                            if maxSamples < 1L || maxSamples > 1000L || maxSteps < 1L || maxSteps > 20L || seed < 1L then
                                                Error "Declared sync bounds exceed the qualified 1..1000 samples, 1..20 steps and positive seed envelope."
                                            else
                                                let expectedTool =
                                                    QuintToolchain.general.Components
                                                    |> List.collect _.Objects
                                                    |> List.find (fun item -> item.Id = "quint-binary")
                                                let toolPath = Path.Combine(cache, "objects", expectedTool.Sha256)
                                                if not (File.Exists toolPath) then Error $"Exact Quint object {expectedTool.Sha256} is unavailable."
                                                elif sha (File.ReadAllBytes toolPath) <> expectedTool.Sha256 then Error "Exact Quint object digest mismatches."
                                                else
                                                    let temp = Path.Combine(Path.GetTempPath(), "fsgg-typed-sync-" + Guid.NewGuid().ToString("N"))
                                                    Directory.CreateDirectory temp |> ignore
                                                    let mutable succeeded = false
                                                    let result =
                                                        try
                                                            let modelName =
                                                                Path.GetFileName generatedArtifact.Path
                                                                |> Option.ofObj
                                                                |> Option.defaultWith (fun () -> failwith "Generated model file name is missing.")
                                                            let modelPath = Path.Combine(temp, modelName)
                                                            File.WriteAllBytes(modelPath, generatedBytes)
                                                            let scenarioNames = scenarios |> List.map (fun item -> item.Id.Substring(5))
                                                            let selector = scenarioNames |> List.map Regex.Escape |> String.concat "|"
                                                            let invariantNames = invariants |> List.map (fun item -> item.Id.Substring(6))
                                                            let runArgs =
                                                                [ "run"; modelName; "--main"; "WorkspaceLifecycle"; $"--max-samples={maxSamples}"; $"--max-steps={maxSteps}"
                                                                  $"--seed={seed}"; "--backend"; "typescript"; "--invariants" ] @ invariantNames
                                                            let testArgs =
                                                                [ "test"; modelName; "--main"; "WorkspaceLifecycleTests"; "--match"; selector
                                                                  $"--seed={seed}"; "--backend"; "typescript"; "--out-itf"; "case_{test}_{seq}.itf.json" ]
                                                            let modelSteps =
                                                                match execute [] toolPath temp "typecheck" [ "typecheck"; modelName ] with
                                                                | Error reason -> Error reason
                                                                | Ok typed ->
                                                                    match execute [] toolPath temp "test" testArgs with
                                                                    | Error reason -> Error reason
                                                                    | Ok tested ->
                                                                        execute [] toolPath temp "run" runArgs
                                                                        |> Result.map (fun sampled -> typed, tested, sampled)
                                                            match modelSteps with
                                                            | Ok(typed, tested, sampled) ->
                                                                let testCount =
                                                                    Regex.Match(tested.Output, @"(?m)^\s*(\d+) passing")
                                                                    |> fun matched -> if matched.Success then int matched.Groups[1].Value else 0
                                                                let witnesses = Directory.GetFiles(temp, "case_coherentDeltaCanReachAcceptance_*.itf.json")
                                                                let discovered =
                                                                    scenarioNames
                                                                    |> List.forall (fun name -> tested.Output.Contains($"ok {name} passed 1 test(s)", StringComparison.Ordinal))
                                                                if testCount <> scenarios.Length || not discovered || witnesses.Length <> 1 then
                                                                    Error "Quint discovered fewer required scenarios than declared, or emitted no acceptance witness."
                                                                elif not (sampled.Output.Contains("[ok] No violation found", StringComparison.Ordinal)) then
                                                                    Error "Bounded simulation did not report a passing invariant check."
                                                                else
                                                                    let implementationBytes = File.ReadAllBytes typeof<WorkspaceModel>.Assembly.Location
                                                                    let adapterBytes = implementationBytes
                                                                    let quintHome = Path.Combine(temp, "quint-home")
                                                                    Directory.CreateDirectory quintHome |> ignore
                                                                    Directory.CreateSymbolicLink(Path.Combine(quintHome, "apalache-dist-0.56.1"), apalacheDist) |> ignore
                                                                    let verifierEnv =
                                                                        [ "JAVA_HOME", javaHome
                                                                          "PATH", Path.Combine(javaHome, "bin") + ":/usr/sbin:/usr/bin"
                                                                          "QUINT_HOME", quintHome ]
                                                                    let verifyArgs =
                                                                        [ "verify"; modelName; "--main"; "WorkspaceLifecycle"; "--max-steps"; string maxSteps
                                                                          "--apalache-version"; "0.56.1"; "--server-endpoint"; "127.0.0.1:19222"
                                                                          "--invariants" ] @ invariantNames
                                                                    match execute verifierEnv toolPath temp "verify" verifyArgs with
                                                                    | Error reason -> Error reason
                                                                    | Ok verified when not (verified.Output.Contains("[ok] No violation found", StringComparison.Ordinal)) ->
                                                                        Error "Symbolic verifier did not report a passing bounded check."
                                                                    | Ok verified ->
                                                                        let environment =
                                                                            { Seed = string seed; Bounds = [ "maxSamples", maxSamples; "maxSteps", maxSteps ]
                                                                              ToolFingerprint = expectedTool.Sha256; ProfileFingerprint = QuintToolchain.fingerprint QuintToolchain.general
                                                                              ContractFingerprint = sha contractBytes; AdapterFingerprint = sha adapterBytes
                                                                              ImplementationFingerprint = sha implementationBytes }
                                                                        match WorkspaceReducerAdapter.replayCoherent accepted (File.ReadAllBytes witnesses[0]) environment,
                                                                              WorkspaceReducerAdapter.checkStaleBase accepted with
                                                                        | Ok QuintReplayResult.Equivalent, Ok() ->
                                                                            let unchanged =
                                                                                sha (File.ReadAllBytes manifestPath) = sha manifestBytes
                                                                                && (if File.Exists linkPath then sha (File.ReadAllBytes linkPath) else "") = linkDigest
                                                                                && sha (File.ReadAllBytes(Path.Combine(root, sourceArtifact.Path))) = sha sourceBytes
                                                                                && sha (File.ReadAllBytes(Path.Combine(root, generatedArtifact.Path))) = sha generatedBytes
                                                                                && sha (File.ReadAllBytes(Path.Combine(root, contractArtifact.Path))) = sha contractBytes
                                                                                && sha (File.ReadAllBytes typeof<WorkspaceModel>.Assembly.Location) = sha implementationBytes
                                                                                && sha (File.ReadAllBytes toolPath) = expectedTool.Sha256
                                                                                && sha (File.ReadAllBytes javaPath) = "e865867065e48928c58293f30e7ae26a79c842f8607fa51d7e2e9fb90b602786"
                                                                                && treeSha apalacheDist = "3466d07f06d7ac80ee0f171a96383183cee9d91bf1b5995d897d4f15c004569f"
                                                                                && hostToolClosure () = hostTools
                                                                            if not unchanged then Error "Accepted/source/implementation/tool dependency changed during sync."
                                                                            else
                                                                                match correspondence accepted contract sourceArtifact.Path typeof<WorkspaceModel>.Assembly.Location tested.Sha256 verified.Sha256 with
                                                                                | Error reason -> Error reason
                                                                                | Ok correspondenceSha ->
                                                                                    succeeded <- true
                                                                                    Ok
                                                                                        { (report "satisfied" []) with
                                                                                            AcceptedSha256 = sha manifestBytes
                                                                                            ContractSha256 = sha contractBytes
                                                                                            GeneratedSha256 = sha generatedBytes
                                                                                            ToolSha256 = expectedTool.Sha256
                                                                                            HostToolSha256 =
                                                                                                hostTools
                                                                                                |> Result.defaultValue []
                                                                                                |> List.map (fun (path, digest) -> path + "=" + digest + "\n")
                                                                                                |> String.concat ""
                                                                                                |> Encoding.UTF8.GetBytes
                                                                                                |> sha
                                                                                            VerifierToolSha256 =
                                                                                                (sha (File.ReadAllBytes javaPath) + "\n" + treeSha apalacheDist + "\n")
                                                                                                |> Encoding.UTF8.GetBytes
                                                                                                |> sha
                                                                                            ImplementationSha256 = sha implementationBytes
                                                                                            AdapterSha256 = sha adapterBytes
                                                                                            CorrespondenceSha256 = correspondenceSha
                                                                                            RunSha256 =
                                                                                                [ sha manifestBytes; sha implementationBytes; typed.Sha256; tested.Sha256
                                                                                                  sampled.Sha256; verified.Sha256; correspondenceSha ]
                                                                                                |> String.concat "\n"
                                                                                                |> Encoding.UTF8.GetBytes
                                                                                                |> sha
                                                                                            PackageIdentity = identity
                                                                                            AcceptedRevision = accepted.Revision
                                                                                            Seed = seed
                                                                                            MaxSamples = maxSamples
                                                                                            MaxSteps = maxSteps
                                                                                            ScenarioCount = testCount
                                                                                            ActionCount = 3
                                                                                            Steps = [ typed; tested; sampled; verified ] }
                                                                        | Ok(QuintReplayResult.Diverged divergence), _ -> Error $"Production reducer diverged at {divergence.Action}: {divergence.Reason}"
                                                                        | Error findings, _ -> Error(sprintf "Production reducer replay refused: %s" (findings |> List.map _.Code |> String.concat ", "))
                                                                        | _, Error findings -> Error(sprintf "Stale-base adapter refused: %s" (findings |> List.map _.Code |> String.concat ", "))
                                                            | Error message -> Error message
                                                        finally
                                                            if succeeded then Directory.Delete(temp, true)
                                                    match result with
                                                    | Ok _ -> result
                                                    | Error reason -> Error $"{reason} Retained attempt: {temp}"
                    | _ -> Error "Accepted Quint source, generated module or contract is missing."

    /// Check a proposed host transaction before it can replace accepted bytes.
    /// The returned revision link is committed in the same authority transaction.
    let prepareProposed root work cache javaHome apalacheDist (writes: (string * byte array) list) =
        try
            let root = Path.GetFullPath root
            let manifestPath = TypedAuthorityManifest.path work
            let proposedManifestBytes =
                writes |> List.find (fst >> ((=) manifestPath)) |> snd
            let priorManifestPath = Path.Combine(root, manifestPath)
            let priorRevision, priorManifestSha =
                if not (File.Exists priorManifestPath) then -1L, ""
                else
                    let bytes = File.ReadAllBytes priorManifestPath
                    match Encoding.UTF8.GetString bytes |> TypedAuthority.deserialize with
                    | Ok(QuintSpecificationV1 manifest) when manifest.ProfileIdentity = QuintGeneralProfile.identity ->
                        match artifact root manifest "compiled-contract" with
                        | None -> invalidOp "Prior accepted compiled contract is missing or changed."
                        | Some(_, contractBytes) ->
                            match QuintContractV2.deserialize (Encoding.UTF8.GetString contractBytes) with
                            | Error _ -> invalidOp "Prior accepted compiled contract is invalid."
                            | Ok contract ->
                                match deriveAccepted root work bytes contract with
                                | Ok accepted -> accepted.Revision, sha bytes
                                | Error reason -> invalidOp reason
                    | _ -> invalidOp "Prior accepted authority is not a supported profile-2 workspace."
            let revision = priorRevision + 1L
            let manifest =
                match Encoding.UTF8.GetString proposedManifestBytes |> TypedAuthority.deserialize with
                | Ok(QuintSpecificationV1 value) when value.ProfileIdentity = QuintGeneralProfile.identity -> value
                | _ -> invalidOp "Proposed authority is not profile 2."
            let contractPath =
                manifest.Artifacts |> List.find (fun item -> item.Id = "compiled-contract") |> _.Path
            let contractBytes = writes |> List.find (fst >> ((=) contractPath)) |> snd
            let contract =
                match QuintContractV2.deserialize (Encoding.UTF8.GetString contractBytes) with
                | Ok value -> value
                | Error _ -> invalidOp "Proposed compiled contract is invalid."
            let model =
                match WorkspaceSync.deriveModel revision contract with
                | Ok value -> value
                | Error findings -> invalidOp (sprintf "Proposed workspace join refused: %s" (findings |> List.map _.Code |> String.concat ", "))
            let fingerprint =
                WorkspaceLifecycle.fingerprint model |> Result.defaultWith (sprintf "%A" >> invalidOp)
            let linkBytes =
                encodeRevisionLink
                    { ManifestSha256 = sha proposedManifestBytes; Revision = revision
                      WorkspaceFingerprint = fingerprint; PriorManifestSha256 = priorManifestSha }
            let linkWrite = revisionPath work, linkBytes
            let staging = Path.Combine(Path.GetTempPath(), "fsgg-sync-proposed-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory staging |> ignore
            for relative, bytes in writes @ [ linkWrite ] do
                match contained staging relative with
                | None -> invalidOp $"Proposed authority emitted unsafe path {relative}."
                | Some path ->
                    Path.GetDirectoryName path
                    |> Option.ofObj
                    |> Option.iter (fun parent -> Directory.CreateDirectory parent |> ignore)
                    File.WriteAllBytes(path, bytes)
            match runAccepted staging work (Path.GetFullPath cache) (Path.GetFullPath javaHome) (Path.GetFullPath apalacheDist) with
            | Error reason -> Error $"Proposed model sync refused: {reason}; retained staging: {staging}"
            | Ok _ ->
                Directory.Delete(staging, true)
                Ok linkWrite
        with ex -> Error $"Proposed model sync could not prepare: {ex.Message}"

    let private blockedReport root work cache javaHome apalacheDist reason =
        let fileDigest path =
            try if File.Exists path then sha (File.ReadAllBytes path) else ""
            with _ -> ""
        let manifestPath = Path.Combine(root, TypedAuthorityManifest.path work)
        let artifactDigest id =
            try
                match File.ReadAllText manifestPath |> TypedAuthority.deserialize with
                | Ok(QuintSpecificationV1 manifest) ->
                    manifest.Artifacts
                    |> List.tryFind (fun item -> item.Id = id)
                    |> Option.bind (fun item -> contained root item.Path)
                    |> Option.map fileDigest
                    |> Option.defaultValue ""
                | _ -> ""
            with _ -> ""
        let toolDigest =
            QuintToolchain.general.Components
            |> List.collect _.Objects
            |> List.find (fun item -> item.Id = "quint-binary")
            |> fun item -> fileDigest (Path.Combine(cache, "objects", item.Sha256))
        let hostDigest =
            match hostToolClosure () with
            | Ok tools ->
                tools |> List.map (fun (path, digest) -> path + "=" + digest + "\n")
                |> String.concat "" |> Encoding.UTF8.GetBytes |> sha
            | Error _ -> ""
        let verifierDigest =
            try
                let javaDigest = fileDigest (Path.Combine(javaHome, "bin", "java"))
                if javaDigest = "" || not (Directory.Exists apalacheDist) then ""
                else (javaDigest + "\n" + treeSha apalacheDist + "\n") |> Encoding.UTF8.GetBytes |> sha
            with _ -> ""
        let implementationDigest = fileDigest typeof<WorkspaceModel>.Assembly.Location
        let acceptedDigest = fileDigest manifestPath
        { (report "blocked" [ reason ]) with
            AcceptedSha256 = acceptedDigest
            ContractSha256 = artifactDigest "compiled-contract"
            GeneratedSha256 = artifactDigest "generated-modules"
            ToolSha256 = toolDigest
            HostToolSha256 = hostDigest
            VerifierToolSha256 = verifierDigest
            ImplementationSha256 = implementationDigest
            AdapterSha256 = implementationDigest
            PackageIdentity = $"FS.GG.SDD.Artifacts/{SchemaVersion.currentGeneratorVersion().Version}"
            RunSha256 =
                (acceptedDigest + "\n" + implementationDigest + "\n" + toolDigest + "\n" + reason)
                |> Encoding.UTF8.GetBytes |> sha }

    let private receiptPath root work = Path.Combine(root, "readiness", work, "quint", "sync-report.json")

    let private saveReceipt root work value =
        let path = receiptPath root work
        Directory.CreateDirectory(Path.Combine(root, "readiness", work, "quint")) |> ignore
        File.WriteAllText(path, render value + "\n", UTF8Encoding(false))

    /// Fresh executable gate for normal lifecycle operations. The accepted
    /// manifest is always re-read; a prior JSON report never supplies success.
    let check root work cache javaHome apalacheDist =
        try
            let root = Path.GetFullPath root
            let cache = Path.GetFullPath cache
            let javaHome = Path.GetFullPath javaHome
            let apalacheDist = Path.GetFullPath apalacheDist
            match runAccepted root work cache javaHome apalacheDist with
            | Ok value -> saveReceipt root work value; Ok()
            | Error reason -> saveReceipt root work (blockedReport root work cache javaHome apalacheDist reason); Error reason
        with ex -> Error $"Sync failed: {ex.Message}"

    let checkSelected root work cache javaHome apalacheDist =
        let manifestPath = Path.Combine(Path.GetFullPath root, TypedAuthorityManifest.path work)
        if not (File.Exists manifestPath) then Ok()
        else
            try
                match File.ReadAllText manifestPath |> TypedAuthority.deserialize with
                | Error finding -> Error $"Accepted authority is unreadable: {finding.Id}: {finding.Message}"
                | Ok(FsharpSpecificationV1 _) -> Ok()
                | Ok(QuintSpecificationV1 authority) when authority.ProfileIdentity <> QuintGeneralProfile.identity -> Ok()
                | Ok(QuintSpecificationV1 _) ->
                    match cache, javaHome, apalacheDist with
                    | Some cachePath, Some javaPath, Some apalachePath ->
                        check root work cachePath javaPath apalachePath
                    | _ -> Error "Profile-2 verify/ship requires --sync-cache, --sync-java-home and --sync-apalache-dist."
            with ex -> Error $"Accepted authority could not be read: {ex.Message}"

    let run args =
        let allowed = set [ "--root"; "--work"; "--cache"; "--java-home"; "--apalache-dist" ]
        let formats = set [ "--json"; "--text"; "--plain"; "--rich" ]
        let format = if args |> List.exists (fun item -> item = "--text" || item = "--plain" || item = "--rich") then "text" else "json"
        let pairs =
            let rec collect rest acc =
                match rest with
                | key :: value :: tail when Set.contains key allowed && not (value.StartsWith("-", StringComparison.Ordinal)) -> collect tail ((key, value) :: acc)
                | key :: tail when Set.contains key formats -> collect tail acc
                | [] -> Ok acc
                | _ -> Error "Pass --root, --work, --cache, --java-home and --apalache-dist."
            collect args []
        match pairs with
        | Error reason -> emit format (report "blocked" [ reason ])
        | Ok entries when entries.Length <> 5 || (entries |> List.map fst |> List.distinct |> List.length) <> 5 ->
            emit format (report "blocked" [ "Pass each required path exactly once." ])
        | Ok _ ->
            let root = optionValue "--root" args |> Option.get |> Path.GetFullPath
            let work = optionValue "--work" args |> Option.get
            let cache = optionValue "--cache" args |> Option.get |> Path.GetFullPath
            let javaHome = optionValue "--java-home" args |> Option.get |> Path.GetFullPath
            let apalacheDist = optionValue "--apalache-dist" args |> Option.get |> Path.GetFullPath
            if work = "." || work = ".." || work.IndexOfAny([| '/'; '\\' |]) >= 0 then
                emit format (report "blocked" [ "Work id must be one contained path segment." ])
            else
                try
                    match runAccepted root work cache javaHome apalacheDist with
                    | Error reason ->
                        let blocked = blockedReport root work cache javaHome apalacheDist reason
                        saveReceipt root work blocked
                        emit format blocked
                    | Ok value ->
                        saveReceipt root work value
                        emit format value
                with ex -> emit format (report "blocked" [ $"Sync failed: {ex.Message}" ])
