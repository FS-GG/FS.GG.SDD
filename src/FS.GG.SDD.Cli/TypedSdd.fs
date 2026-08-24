namespace FS.GG.SDD.Cli

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.TypedSpecifications

module TypedSdd =
    type Report =
        { Operation: string
          Outcome: string
          Classification: string option
          ChangedPaths: string list
          SemanticDiff: string list
          RollbackSourceSha256: string option
          Diagnostics: TypedLifecycleDiagnostic list }

    let private optionValue name args =
        args
        |> List.tryFindIndex ((=) name)
        |> Option.bind (fun index -> args |> List.tryItem (index + 1))

    let private has flag args = List.contains flag args
    let private root args = optionValue "--root" args |> Option.defaultValue "." |> Path.GetFullPath
    let private work args = optionValue "--work" args
    let private packageIdentity () =
        let version = SchemaVersion.currentGeneratorVersion().Version
        $"FS.GG.SDD.Artifacts/{version}"

    let private compilerAvailable () =
        try
            let start = ProcessStartInfo("dotnet")
            start.ArgumentList.Add("--version")
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true
            start.UseShellExecute <- false
            use child = Process.Start start |> Option.ofObj |> Option.defaultWith (fun () -> failwith "dotnet did not start")
            child.WaitForExit(10000) && child.ExitCode = 0
        with _ -> false

    let private diagnostic id message correction = { Id = id; Message = message; Correction = correction }

    let private serializeReport report =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteString("operation", report.Operation)
        writer.WriteString("outcome", report.Outcome)
        match report.Classification with Some value -> writer.WriteString("classification", value) | None -> writer.WriteNull("classification")
        writer.WriteStartArray("changedPaths")
        report.ChangedPaths |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteStartArray("semanticDiff")
        report.SemanticDiff |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        match report.RollbackSourceSha256 with Some value -> writer.WriteString("rollbackSourceSha256", value) | None -> writer.WriteNull("rollbackSourceSha256")
        writer.WriteStartArray("diagnostics")
        report.Diagnostics
        |> List.iter (fun item ->
            writer.WriteStartObject()
            writer.WriteString("id", item.Id)
            writer.WriteString("message", item.Message)
            writer.WriteString("correction", item.Correction)
            writer.WriteEndObject())
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let private emit report =
        Console.Out.WriteLine(serializeReport report)
        if List.isEmpty report.Diagnostics then 0 else 1

    let private id value =
        match SpecificationId.create value with
        | Ok result -> result
        | Error message -> invalidArg "value" message

    let private newModel workId title agent session =
        let acceptanceId = id "AC-001"
        let requirementId = id "FR-001"
        let storyId = id "US-001"
        let extension =
            RequirementsDraft.empty
            |> RequirementsDraft.withUserValue title
            |> RequirementsDraft.addScope { Id = id "SB-001"; Statement = "Author the accepted Typed SDD scope." }
            |> RequirementsDraft.addStory { Id = storyId; Priority = "P1"; Statement = "An author can complete the typed lifecycle." }
            |> RequirementsDraft.addRequirement
                { Id = requirementId
                  Statement = "The implementation MUST satisfy the accepted Typed SDD specification."
                  AcceptanceIds = [ acceptanceId ]
                  EvidenceObligationIds = [ id "EV001" ] }
            |> RequirementsDraft.addAcceptance
                { Id = acceptanceId
                  StoryIds = [ storyId ]
                  RequirementIds = [ requirementId ]
                  Statement = "Given the implementation, the declared verification evidence passes." }
            |> RequirementsDraft.addLifecycleNote "Continue with the shared SDD stage sequence."
            |> RequirementsDraft.build
        { Identity = id "SPEC-001"
          SchemaVersion = 1
          Provenance =
            { Agent = agent
              Session = session
              SourcePath = $"work/{workId}/specification.fsx"
              SourceRevision = String.replicate 64 "0"
              AuthoredAtUtc = DateTimeOffset.UtcNow.ToString("O") }
          Intent = title
          EvidenceObligations = [ { Id = id "EV001"; Kind = "test"; Description = "Run the accepted verification suite." } ]
          Extension = extension }

    let private script (packageVersion: string) (normalized: string) =
        let escaped = normalized.Replace("\"\"\"", "\"\"\\\"")
        $"#r \"nuget: FS.GG.SDD.Artifacts, {packageVersion}\"\n\nopen FS.GG.SDD.Artifacts.TypedSpecifications\n\nlet normalizedSpecificationJson = \"\"\"{escaped}\"\"\"\n\nlet model =\n    match SpecificationCodec.deserialize RequirementsExtension.contract normalizedSpecificationJson with\n    | Ok value -> value\n    | Error diagnostics -> failwithf \"Invalid Typed SDD authority: %%A\" diagnostics\n\nlet compiled =\n    match SpecificationCompiler.compile RequirementsExtension.contract model with\n    | Ok value -> value\n    | Error diagnostics -> failwithf \"Typed SDD compilation failed: %%A\" diagnostics\n\nprintfn \"%%s\" compiled.Fingerprint\n"

    let private paths root workId =
        let relativeCanonical = $"work/{workId}/specification.fsx"
        let relativeNormalized = $"readiness/{workId}/specification.normalized.json"
        let relativeMarkdown = $"work/{workId}/spec.md"
        relativeCanonical, relativeNormalized, relativeMarkdown,
        Path.Combine(root, relativeCanonical), Path.Combine(root, relativeNormalized), Path.Combine(root, relativeMarkdown)

    let private writeAuthority root workId model rollback =
        match SpecificationCodec.serialize RequirementsExtension.contract model, SpecificationProjection.generate RequirementsExtension.contract model with
        | Ok normalized, Ok projection ->
            let rc, rn, rm, canonicalPath, normalizedPath, markdownPath = paths root workId
            let canonical = script (SchemaVersion.currentGeneratorVersion().Version) normalized
            let ensureParent (path: string) =
                Path.GetDirectoryName path
                |> Option.ofObj
                |> Option.iter (fun directory -> Directory.CreateDirectory directory |> ignore)
            ensureParent canonicalPath
            ensureParent normalizedPath
            File.WriteAllText(canonicalPath, canonical)
            File.WriteAllText(normalizedPath, normalized + "\n")
            File.WriteAllText(markdownPath, projection.Markdown)
            let bytes path = File.ReadAllBytes path
            let authority =
                { SchemaVersion = 1
                  Lifecycle = "typed-sdd"
                  Backend = "fsharp-specification-v1"
                  CompilerIdentity = "dotnet-fsi/net10.0"
                  PackageIdentity = packageIdentity ()
                  ExtensionIdentity = "fsgg.requirements-extension/v1"
                  CanonicalPath = rc
                  CanonicalSha256 = TypedAuthorityManifest.sha256 (bytes canonicalPath)
                  NormalizedPath = rn
                  NormalizedSha256 = TypedAuthorityManifest.sha256 (bytes normalizedPath)
                  MarkdownPath = rm
                  MarkdownSha256 = TypedAuthorityManifest.sha256 (bytes markdownPath)
                  AuthoringAgent = model.Provenance.Agent
                  AuthoringSession = model.Provenance.Session
                  RollbackSourceSha256 = rollback }
            let authorityPath = Path.Combine(root, TypedAuthorityManifest.path workId)
            File.WriteAllText(authorityPath, TypedAuthorityManifest.serialize authority)
            Ok [ rc; rn; rm; TypedAuthorityManifest.path workId ]
        | Error findings, _
        | _, Error findings ->
            Error(findings |> List.map (fun item -> diagnostic "typedSdd.unsupportedExtension" item.Message "Correct the model using the published requirements extension."))

    let private author args =
        match work args with
        | None -> emit { Operation = "author"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ diagnostic "typedSdd.workRequired" "--work is required." "Pass --work <id>." ] }
        | Some workId ->
            let agent = optionValue "--agent" args |> Option.defaultValue ""
            let session = optionValue "--session" args |> Option.defaultValue ""
            if String.IsNullOrWhiteSpace agent || String.IsNullOrWhiteSpace session then
                emit { Operation = "author"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ diagnostic "typedSdd.authoringAgentUnavailable" "An authoring agent and session receipt are required." "Pass --agent <id> --session <id>." ] }
            else
                let title = optionValue "--title" args |> Option.defaultValue workId
                match writeAuthority (root args) workId (newModel workId title agent session) None with
                | Ok changed -> emit { Operation = "author"; Outcome = "succeeded"; Classification = None; ChangedPaths = changed; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [] }
                | Error findings -> emit { Operation = "author"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = findings }

    let private migrate args =
        match work args, optionValue "--source" args with
        | Some workId, Some source ->
            let rootPath = root args
            let sourcePath = Path.Combine(rootPath, source)
            if not (File.Exists sourcePath) then
                emit { Operation = "migrate"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ diagnostic "typedSdd.migrationSourceMissing" "The Standard SDD source is missing." "Pass an existing --source path." ] }
            else
                let sourceBytes = File.ReadAllBytes sourcePath
                let rollback = TypedAuthorityManifest.sha256 sourceBytes
                match RequirementsMigration.analyzeMarkdown (Encoding.UTF8.GetString sourceBytes) with
                | Ambiguous findings -> emit { Operation = "migrate"; Outcome = "noChange"; Classification = Some "Ambiguous"; ChangedPaths = []; SemanticDiff = findings |> List.map _.Message; RollbackSourceSha256 = Some rollback; Diagnostics = [] }
                | Unsupported findings -> emit { Operation = "migrate"; Outcome = "noChange"; Classification = Some "Unsupported"; ChangedPaths = []; SemanticDiff = findings |> List.map _.Message; RollbackSourceSha256 = Some rollback; Diagnostics = [] }
                | Migrated extension when not (has "--accept" args) -> emit { Operation = "migrate"; Outcome = "noChange"; Classification = Some "Migrated"; ChangedPaths = []; SemanticDiff = [ "Standard Markdown authority -> canonical F# authority" ]; RollbackSourceSha256 = Some rollback; Diagnostics = [] }
                | Migrated extension ->
                    let seed = newModel workId (optionValue "--title" args |> Option.defaultValue workId) (optionValue "--agent" args |> Option.defaultValue "migration") (optionValue "--session" args |> Option.defaultValue "migration")
                    match writeAuthority rootPath workId { seed with Extension = extension } (Some rollback) with
                    | Ok changed ->
                        let rollbackRelative = $"work/{workId}/spec.standard-sdd.rollback.md"
                        File.WriteAllBytes(Path.Combine(rootPath, rollbackRelative), sourceBytes)
                        emit { Operation = "migrate"; Outcome = "succeeded"; Classification = Some "Migrated"; ChangedPaths = rollbackRelative :: changed; SemanticDiff = [ "Standard Markdown authority -> canonical F# authority" ]; RollbackSourceSha256 = Some rollback; Diagnostics = [] }
                    | Error findings -> emit { Operation = "migrate"; Outcome = "blocked"; Classification = Some "Unsupported"; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = Some rollback; Diagnostics = findings }
        | _ -> emit { Operation = "migrate"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ diagnostic "typedSdd.migrationArgumentsRequired" "--work and --source are required." "Pass --work <id> --source work/<id>/spec.md." ] }

    let private inspect args =
        match work args with
        | None -> emit { Operation = "inspect"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ diagnostic "typedSdd.workRequired" "--work is required." "Pass --work <id>." ] }
        | Some workId ->
            let rootPath = root args
            let manifestPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)
            if not (File.Exists manifestPath) then
                emit { Operation = "inspect"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ diagnostic "typedSdd.authorityMissing" "The Typed SDD authority manifest is missing." "Run typed-sdd author or accept a migration." ] }
            else
                match TypedAuthorityManifest.deserialize (File.ReadAllText manifestPath) with
                | Error finding -> emit { Operation = "inspect"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ finding ] }
                | Ok authority ->
                    let read relative = let path = Path.Combine(rootPath, relative) in if File.Exists path then Some(File.ReadAllBytes path) else None
                    let findings = TypedAuthorityManifest.validate (packageIdentity ()) (compilerAvailable ()) (read authority.CanonicalPath) (read authority.NormalizedPath) (read authority.MarkdownPath) authority
                    emit { Operation = "inspect"; Outcome = (if List.isEmpty findings then "succeeded" else "blocked"); Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = authority.RollbackSourceSha256; Diagnostics = findings }

    let run args =
        match args with
        | "author" :: rest -> author rest
        | "inspect" :: rest -> inspect rest
        | "migrate" :: rest -> migrate rest
        | _ -> emit { Operation = "typed-sdd"; Outcome = "blocked"; Classification = None; ChangedPaths = []; SemanticDiff = []; RollbackSourceSha256 = None; Diagnostics = [ diagnostic "typedSdd.unknownOperation" "Unknown Typed SDD operation." "Use author, inspect, or migrate." ] }
