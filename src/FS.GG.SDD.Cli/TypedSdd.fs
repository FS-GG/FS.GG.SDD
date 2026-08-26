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

    let private root args =
        optionValue "--root" args |> Option.defaultValue "." |> Path.GetFullPath

    let private work args = optionValue "--work" args

    let private validWorkId (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && value <> "."
        && value <> ".."
        && value.IndexOfAny([| '/'; '\\' |]) < 0

    let private containedPath (root: string) (relative: string) =
        if Path.IsPathRooted relative then
            None
        else
            let full = Path.GetFullPath(Path.Combine(root, relative))

            let prefix =
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + string Path.DirectorySeparatorChar

            if full.StartsWith(prefix, StringComparison.Ordinal) then
                Some full
            else
                None

    let private packageIdentity () =
        let version = SchemaVersion.currentGeneratorVersion().Version
        $"FS.GG.SDD.Artifacts/{version}"

    let private runFsi (sourcePath: string) =
        try
            let start = ProcessStartInfo("dotnet")
            start.ArgumentList.Add("fsi")
            start.ArgumentList.Add("--exec")
            start.ArgumentList.Add(sourcePath)
            start.RedirectStandardOutput <- true
            start.RedirectStandardError <- true
            start.UseShellExecute <- false

            use child =
                Process.Start start
                |> Option.ofObj
                |> Option.defaultWith (fun () -> failwith "dotnet did not start")

            if not (child.WaitForExit 30000) then
                try
                    child.Kill(true)
                with _ ->
                    ()

                Error "The F# compiler timed out."
            elif child.ExitCode = 0 then
                Ok(child.StandardOutput.ReadToEnd().Trim())
            else
                Error(child.StandardError.ReadToEnd().Trim())
        with _ ->
            Error "The F# compiler could not be started."

    let private diagnostic id message correction =
        { Id = id
          Message = message
          Correction = correction }

    let private serializeReport report =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteString("operation", report.Operation)
        writer.WriteString("outcome", report.Outcome)

        match report.Classification with
        | Some value -> writer.WriteString("classification", value)
        | None -> writer.WriteNull("classification")

        writer.WriteStartArray("changedPaths")
        report.ChangedPaths |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteStartArray("semanticDiff")
        report.SemanticDiff |> List.iter writer.WriteStringValue
        writer.WriteEndArray()

        match report.RollbackSourceSha256 with
        | Some value -> writer.WriteString("rollbackSourceSha256", value)
        | None -> writer.WriteNull("rollbackSourceSha256")

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
            |> RequirementsDraft.addScope
                { Id = id "SB-001"
                  Statement = "Author the accepted Typed SDD scope." }
            |> RequirementsDraft.addStory
                { Id = storyId
                  Priority = "P1"
                  Statement = "An author can complete the typed lifecycle." }
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
          EvidenceObligations =
            [ { Id = id "EV001"
                Kind = "test"
                Description = "Run the accepted verification suite." } ]
          Extension = extension }

    let private script (packageVersion: string) (normalized: string) =
        let escaped = normalized.Replace("\"\"\"", "\"\"\\\"")
        $"#r \"nuget: FS.GG.SDD.Artifacts, {packageVersion}\"\n\nopen FS.GG.SDD.Artifacts.TypedSpecifications\n\nlet normalizedSpecificationJson = \"\"\"{escaped}\"\"\"\n\nlet model =\n    match SpecificationCodec.deserialize RequirementsExtension.contract normalizedSpecificationJson with\n    | Ok value -> value\n    | Error diagnostics -> failwithf \"Invalid Typed SDD authority: %%A\" diagnostics\n\nlet compiled =\n    match SpecificationCompiler.compile RequirementsExtension.contract model with\n    | Ok value -> value\n    | Error diagnostics -> failwithf \"Typed SDD compilation failed: %%A\" diagnostics\n\nprintfn \"%%s\" compiled.Fingerprint\n"

    let private normalizedMarker = "let normalizedSpecificationJson = \"\"\""

    let private extractNormalized (source: string) =
        let start = source.IndexOf(normalizedMarker, StringComparison.Ordinal)

        if start < 0 then
            Error "Canonical source does not declare normalizedSpecificationJson."
        else
            let valueStart = start + normalizedMarker.Length
            let finish = source.IndexOf("\"\"\"", valueStart, StringComparison.Ordinal)

            if finish < 0 then
                Error "Canonical normalizedSpecificationJson is unterminated."
            else
                Ok(source.Substring(valueStart, finish - valueStart).Replace("\"\"\\\"", "\"\"\""))

    let private compileCanonical (canonical: string) =
        let assemblyPath =
            typeof<SpecificationId>.Assembly.Location.Replace("\\", "\\\\").Replace("\"", "\\\"")

        let firstLineEnd = canonical.IndexOf('\n')

        let local =
            if firstLineEnd < 0 then
                canonical
            else
                $"#r \"{assemblyPath}\"\n{canonical.Substring(firstLineEnd + 1)}"

        let temporary =
            Path.Combine(Path.GetTempPath(), "fsgg-typed-sdd-" + Guid.NewGuid().ToString("N") + ".fsx")

        try
            File.WriteAllText(temporary, local)
            runFsi temporary
        finally
            if File.Exists temporary then
                File.Delete temporary

    let private atomicWrite (writes: (string * byte array) list) =
        let prior =
            writes
            |> List.map (fun (path, _) ->
                path,
                (if File.Exists path then
                     Some(File.ReadAllBytes path)
                 else
                     None))

        let staged =
            writes
            |> List.map (fun (path, bytes) ->
                Path.GetDirectoryName path
                |> Option.ofObj
                |> Option.iter (fun directory -> Directory.CreateDirectory directory |> ignore)

                let temporary = path + ".typed-sdd-" + Guid.NewGuid().ToString("N") + ".tmp"
                File.WriteAllBytes(temporary, bytes)
                path, temporary)

        try
            staged |> List.iter (fun (path, temporary) -> File.Move(temporary, path, true))
        with _ ->
            for path, bytes in prior do
                match bytes with
                | Some value -> File.WriteAllBytes(path, value)
                | None when File.Exists path -> File.Delete path
                | None -> ()

            staged
            |> List.iter (fun (_, temporary) ->
                if File.Exists temporary then
                    File.Delete temporary)

            reraise ()

    let private paths root workId =
        let relativeCanonical = $"work/{workId}/specification.fsx"
        let relativeNormalized = $"readiness/{workId}/specification.normalized.json"
        let relativeMarkdown = $"work/{workId}/spec.md"

        relativeCanonical,
        relativeNormalized,
        relativeMarkdown,
        Path.Combine(root, relativeCanonical),
        Path.Combine(root, relativeNormalized),
        Path.Combine(root, relativeMarkdown)

    let private writeAuthority root workId model rollback extraWrites =
        match
            SpecificationCodec.serialize RequirementsExtension.contract model,
            TypedAuthorityManifest.markdownProjection workId model
        with
        | Ok normalized, Ok markdown ->
            let rc, rn, rm, canonicalPath, normalizedPath, markdownPath = paths root workId
            let canonical = script (SchemaVersion.currentGeneratorVersion().Version) normalized

            match compileCanonical canonical with
            | Error message ->
                Error
                    [ diagnostic
                          "typedSdd.compilationFailed"
                          message
                          "Correct the canonical F# model and ensure the pinned .NET SDK is installed." ]
            | Ok _ ->
                let canonicalBytes = Encoding.UTF8.GetBytes canonical
                let normalizedBytes = Encoding.UTF8.GetBytes(normalized + "\n")
                let markdownBytes = Encoding.UTF8.GetBytes markdown

                let authority =
                    { SchemaVersion = 1
                      Lifecycle = "typed-sdd"
                      Backend = "fsharp-specification-v1"
                      CompilerIdentity = "dotnet-fsi/net10.0"
                      PackageIdentity = packageIdentity ()
                      ExtensionIdentity = "fsgg.requirements-extension/v1"
                      CanonicalPath = rc
                      CanonicalSha256 = TypedAuthorityManifest.sha256 canonicalBytes
                      NormalizedPath = rn
                      NormalizedSha256 = TypedAuthorityManifest.sha256 normalizedBytes
                      MarkdownPath = rm
                      MarkdownSha256 = TypedAuthorityManifest.sha256 markdownBytes
                      AuthoringAgent = model.Provenance.Agent
                      AuthoringSession = model.Provenance.Session
                      RollbackSourceSha256 = rollback }

                let authorityPath = Path.Combine(root, TypedAuthorityManifest.path workId)

                atomicWrite (
                    extraWrites
                    @ [ canonicalPath, canonicalBytes
                        normalizedPath, normalizedBytes
                        markdownPath, markdownBytes
                        authorityPath, Encoding.UTF8.GetBytes(TypedAuthorityManifest.serialize authority) ]
                )

                Ok [ rc; rn; rm; TypedAuthorityManifest.path workId ]
        | Error findings, _
        | _, Error findings ->
            Error(
                findings
                |> List.map (fun item ->
                    diagnostic
                        "typedSdd.unsupportedExtension"
                        item.Message
                        "Correct the model using the published requirements extension.")
            )

    let private author args =
        match work args with
        | None ->
            emit
                { Operation = "author"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics = [ diagnostic "typedSdd.workRequired" "--work is required." "Pass --work <id>." ] }
        | Some workId when not (validWorkId workId) ->
            emit
                { Operation = "author"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics =
                    [ diagnostic
                          "typedSdd.workInvalid"
                          "--work must be one path-segment identifier."
                          "Pass a work id without separators or traversal segments." ] }
        | Some workId ->
            let agent = optionValue "--agent" args |> Option.defaultValue ""
            let session = optionValue "--session" args |> Option.defaultValue ""

            if String.IsNullOrWhiteSpace agent || String.IsNullOrWhiteSpace session then
                emit
                    { Operation = "author"
                      Outcome = "blocked"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = []
                      RollbackSourceSha256 = None
                      Diagnostics =
                        [ diagnostic
                              "typedSdd.authoringAgentUnavailable"
                              "An authoring agent and session receipt are required."
                              "Pass --agent <id> --session <id>." ] }
            else
                let title = optionValue "--title" args |> Option.defaultValue workId

                let _, _, _, canonicalPath, _, _ = paths (root args) workId

                let modelResult =
                    if File.Exists canonicalPath then
                        if not (has "--accept" args) then
                            Error
                                [ diagnostic
                                      "typedSdd.acceptRequired"
                                      "Canonical F# authority already exists."
                                      "Review the edit, then pass --accept with a fresh authoring receipt." ]
                        else
                            let source = File.ReadAllText canonicalPath

                            match compileCanonical source, extractNormalized source with
                            | Error message, _ ->
                                Error
                                    [ diagnostic
                                          "typedSdd.compilationFailed"
                                          message
                                          "Correct the canonical F# model before accepting it." ]
                            | _, Error message ->
                                Error
                                    [ diagnostic
                                          "typedSdd.canonicalMalformed"
                                          message
                                          "Restore the generated authority shape." ]
                            | Ok _, Ok normalized ->
                                match SpecificationCodec.deserialize RequirementsExtension.contract normalized with
                                | Error findings ->
                                    Error(
                                        findings
                                        |> List.map (fun finding ->
                                            diagnostic
                                                "typedSdd.canonicalMalformed"
                                                finding.Message
                                                "Correct the canonical typed model before accepting it.")
                                    )
                                | Ok current ->
                                    Ok
                                        { current with
                                            Provenance =
                                                { current.Provenance with
                                                    Agent = agent
                                                    Session = session
                                                    AuthoredAtUtc = DateTimeOffset.UtcNow.ToString("O") } }
                    else
                        Ok(newModel workId title agent session)

                match
                    modelResult
                    |> Result.bind (fun model -> writeAuthority (root args) workId model None [])
                with
                | Ok changed ->
                    emit
                        { Operation = "author"
                          Outcome = "succeeded"
                          Classification = None
                          ChangedPaths = changed
                          SemanticDiff = []
                          RollbackSourceSha256 = None
                          Diagnostics = [] }
                | Error findings ->
                    emit
                        { Operation = "author"
                          Outcome = "blocked"
                          Classification = None
                          ChangedPaths = []
                          SemanticDiff = []
                          RollbackSourceSha256 = None
                          Diagnostics = findings }

    let private migrate args =
        match work args, optionValue "--source" args with
        | Some workId, Some _ when not (validWorkId workId) ->
            emit
                { Operation = "migrate"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics =
                    [ diagnostic
                          "typedSdd.workInvalid"
                          "--work must be one path-segment identifier."
                          "Pass a work id without separators or traversal segments." ] }
        | Some workId, Some source ->
            let rootPath = root args
            let sourcePath = containedPath rootPath source

            if Option.isNone sourcePath then
                emit
                    { Operation = "migrate"
                      Outcome = "blocked"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = []
                      RollbackSourceSha256 = None
                      Diagnostics =
                        [ diagnostic
                              "typedSdd.sourceEscapesRoot"
                              "--source resolves outside --root."
                              "Pass a project-relative source path contained by --root." ] }
            elif not (File.Exists sourcePath.Value) then
                emit
                    { Operation = "migrate"
                      Outcome = "blocked"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = []
                      RollbackSourceSha256 = None
                      Diagnostics =
                        [ diagnostic
                              "typedSdd.migrationSourceMissing"
                              "The Standard SDD source is missing."
                              "Pass an existing --source path." ] }
            else
                let sourceBytes = File.ReadAllBytes sourcePath.Value
                let rollback = TypedAuthorityManifest.sha256 sourceBytes

                match RequirementsMigration.analyzeMarkdown (Encoding.UTF8.GetString sourceBytes) with
                | Ambiguous findings ->
                    emit
                        { Operation = "migrate"
                          Outcome = "noChange"
                          Classification = Some "Ambiguous"
                          ChangedPaths = []
                          SemanticDiff = findings |> List.map _.Message
                          RollbackSourceSha256 = Some rollback
                          Diagnostics = [] }
                | Unsupported findings ->
                    emit
                        { Operation = "migrate"
                          Outcome = "noChange"
                          Classification = Some "Unsupported"
                          ChangedPaths = []
                          SemanticDiff = findings |> List.map _.Message
                          RollbackSourceSha256 = Some rollback
                          Diagnostics = [] }
                | Migrated extension when not (has "--accept" args) ->
                    let summary =
                        [ $"scope boundaries: {extension.Scope.Length}"
                          $"user stories: {extension.Stories.Length}"
                          $"requirements: {extension.Requirements.Length}"
                          $"acceptance criteria: {extension.Acceptance.Length}"
                          $"lifecycle notes: {extension.LifecycleNotes.Length}" ]

                    emit
                        { Operation = "migrate"
                          Outcome = "noChange"
                          Classification = Some "Migrated"
                          ChangedPaths = []
                          SemanticDiff = summary
                          RollbackSourceSha256 = Some rollback
                          Diagnostics = [] }
                | Migrated extension ->
                    let summary =
                        [ $"scope boundaries: {extension.Scope.Length}"
                          $"user stories: {extension.Stories.Length}"
                          $"requirements: {extension.Requirements.Length}"
                          $"acceptance criteria: {extension.Acceptance.Length}"
                          $"lifecycle notes: {extension.LifecycleNotes.Length}" ]

                    let seed =
                        newModel
                            workId
                            (optionValue "--title" args |> Option.defaultValue workId)
                            (optionValue "--agent" args |> Option.defaultValue "migration")
                            (optionValue "--session" args |> Option.defaultValue "migration")

                    let rollbackRelative = $"work/{workId}/spec.standard-sdd.rollback.md"
                    let rollbackPath = Path.Combine(rootPath, rollbackRelative)

                    match
                        writeAuthority
                            rootPath
                            workId
                            { seed with Extension = extension }
                            (Some rollback)
                            [ rollbackPath, sourceBytes ]
                    with
                    | Ok changed ->
                        emit
                            { Operation = "migrate"
                              Outcome = "succeeded"
                              Classification = Some "Migrated"
                              ChangedPaths = rollbackRelative :: changed
                              SemanticDiff = summary
                              RollbackSourceSha256 = Some rollback
                              Diagnostics = [] }
                    | Error findings ->
                        emit
                            { Operation = "migrate"
                              Outcome = "blocked"
                              Classification = Some "Unsupported"
                              ChangedPaths = []
                              SemanticDiff = []
                              RollbackSourceSha256 = Some rollback
                              Diagnostics = findings }
        | _ ->
            emit
                { Operation = "migrate"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics =
                    [ diagnostic
                          "typedSdd.migrationArgumentsRequired"
                          "--work and --source are required."
                          "Pass --work <id> --source work/<id>/spec.md." ] }

    let private inspect args =
        match work args with
        | None ->
            emit
                { Operation = "inspect"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics = [ diagnostic "typedSdd.workRequired" "--work is required." "Pass --work <id>." ] }
        | Some workId when not (validWorkId workId) ->
            emit
                { Operation = "inspect"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics =
                    [ diagnostic
                          "typedSdd.workInvalid"
                          "--work must be one path-segment identifier."
                          "Pass a work id without separators or traversal segments." ] }
        | Some workId ->
            let rootPath = root args
            let manifestPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)

            if not (File.Exists manifestPath) then
                emit
                    { Operation = "inspect"
                      Outcome = "blocked"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = []
                      RollbackSourceSha256 = None
                      Diagnostics =
                        [ diagnostic
                              "typedSdd.authorityMissing"
                              "The Typed SDD authority manifest is missing."
                              "Run typed-sdd author or accept a migration." ] }
            else
                match TypedAuthority.deserialize (File.ReadAllText manifestPath) with
                | Error finding ->
                    emit
                        { Operation = "inspect"
                          Outcome = "blocked"
                          Classification = None
                          ChangedPaths = []
                          SemanticDiff = []
                          RollbackSourceSha256 = None
                          Diagnostics = [ finding ] }
                | Ok(FsharpSpecificationV1 authority) ->
                    let read relative =
                        containedPath rootPath relative
                        |> Option.bind (fun path ->
                            if File.Exists path then
                                Some(File.ReadAllBytes path)
                            else
                                None)

                    let rc, rn, rm, _, _, _ = paths rootPath workId
                    let canonicalBytes = read authority.CanonicalPath
                    let normalizedBytes = read authority.NormalizedPath
                    let markdownBytes = read authority.MarkdownPath

                    let pathFindings =
                        [ if
                              authority.CanonicalPath <> rc
                              || authority.NormalizedPath <> rn
                              || authority.MarkdownPath <> rm
                          then
                              yield
                                  diagnostic
                                      "typedSdd.authorityPathMismatch"
                                      "Authority paths do not match the selected work id."
                                      "Regenerate the authority manifest for this work id." ]

                    let compilerResult =
                        canonicalBytes
                        |> Option.map (Encoding.UTF8.GetString >> compileCanonical)
                        |> Option.defaultValue (Error "Canonical F# source is missing.")

                    let derivationFindings =
                        match canonicalBytes with
                        | None -> []
                        | Some bytes ->
                            match compilerResult, normalizedBytes, markdownBytes with
                            | Error message, _, _ ->
                                [ diagnostic
                                      "typedSdd.compilationFailed"
                                      message
                                      "Correct the canonical F# model and ensure the pinned SDK is installed." ]
                            | Ok _, Some normalized, Some markdown ->
                                TypedAuthorityManifest.validateDerivation bytes normalized markdown
                            | Ok _, _, _ -> []

                    let findings =
                        TypedAuthorityManifest.validate
                            (packageIdentity ())
                            (Result.isOk compilerResult)
                            canonicalBytes
                            normalizedBytes
                            markdownBytes
                            authority
                        @ pathFindings
                        @ derivationFindings

                    emit
                        { Operation = "inspect"
                          Outcome = (if List.isEmpty findings then "succeeded" else "blocked")
                          Classification = None
                          ChangedPaths = []
                          SemanticDiff = []
                          RollbackSourceSha256 = authority.RollbackSourceSha256
                          Diagnostics = findings }
                | Ok(QuintSpecificationV1 authority) ->
                    let read relative =
                        containedPath rootPath relative
                        |> Option.bind (fun path ->
                            if File.Exists path then
                                Some(File.ReadAllBytes path)
                            else
                                None)

                    let observations =
                        [ for artifact in authority.Artifacts do
                              yield artifact.Path, read artifact.Path
                          match authority.RollbackManifestPath with
                          | Some path -> yield path, read path
                          | None -> () ]

                    let findings =
                        TypedAuthority.validateQuintV2 (packageIdentity ()) observations authority

                    emit
                        { Operation = "inspect"
                          Outcome = (if List.isEmpty findings then "succeeded" else "blocked")
                          Classification = Some "quint-specification-v1"
                          ChangedPaths = []
                          SemanticDiff = []
                          RollbackSourceSha256 = authority.RollbackManifestSha256
                          Diagnostics = findings }

    let private rollback args =
        match work args with
        | Some workId when validWorkId workId ->
            let rootPath = root args
            let rc, rn, rm, canonicalPath, normalizedPath, markdownPath = paths rootPath workId
            let rollbackRelative = $"work/{workId}/spec.standard-sdd.rollback.md"
            let rollbackPath = Path.Combine(rootPath, rollbackRelative)
            let manifestPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)

            if not (has "--accept" args) then
                emit
                    { Operation = "rollback"
                      Outcome = "noChange"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = [ "restore Standard SDD Markdown authority" ]
                      RollbackSourceSha256 = None
                      Diagnostics = [] }
            elif not (File.Exists rollbackPath) then
                emit
                    { Operation = "rollback"
                      Outcome = "blocked"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = []
                      RollbackSourceSha256 = None
                      Diagnostics =
                        [ diagnostic
                              "typedSdd.rollbackMissing"
                              "No preserved Standard SDD authority exists."
                              "Rollback is available only after an accepted migration." ] }
            else
                let rollbackBytes = File.ReadAllBytes rollbackPath
                let typedPaths = [ canonicalPath; normalizedPath; manifestPath ]
                let transactionPaths = markdownPath :: typedPaths

                let prior =
                    transactionPaths
                    |> List.map (fun path ->
                        path,
                        (if File.Exists path then
                             Some(File.ReadAllBytes path)
                         else
                             None))

                try
                    atomicWrite [ markdownPath, rollbackBytes ]

                    typedPaths
                    |> List.iter (fun path ->
                        if File.Exists path then
                            File.Delete path)

                    emit
                        { Operation = "rollback"
                          Outcome = "succeeded"
                          Classification = None
                          ChangedPaths = [ rm; rc; rn; TypedAuthorityManifest.path workId ]
                          SemanticDiff = [ "restored Standard SDD Markdown authority" ]
                          RollbackSourceSha256 = Some(TypedAuthorityManifest.sha256 rollbackBytes)
                          Diagnostics = [] }
                with ex ->
                    prior
                    |> List.iter (fun (path, bytes) ->
                        match bytes with
                        | Some value ->
                            Path.GetDirectoryName path
                            |> Option.ofObj
                            |> Option.iter (fun directory -> Directory.CreateDirectory directory |> ignore)

                            File.WriteAllBytes(path, value)
                        | None when File.Exists path -> File.Delete path
                        | None -> ())

                    emit
                        { Operation = "rollback"
                          Outcome = "blocked"
                          Classification = None
                          ChangedPaths = []
                          SemanticDiff = []
                          RollbackSourceSha256 = None
                          Diagnostics =
                            [ diagnostic
                                  "typedSdd.rollbackFailed"
                                  ex.Message
                                  "Retry after correcting filesystem access." ] }
        | _ ->
            emit
                { Operation = "rollback"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics =
                    [ diagnostic "typedSdd.workInvalid" "A valid --work is required." "Pass one work id segment." ] }

    let private unknownArgument operation args =
        let valued, flags =
            match operation with
            | "author" -> set [ "--root"; "--work"; "--title"; "--agent"; "--session" ], set [ "--accept" ]
            | "inspect" -> set [ "--root"; "--work" ], Set.empty
            | "migrate" -> set [ "--root"; "--work"; "--source"; "--title"; "--agent"; "--session" ], set [ "--accept" ]
            | "rollback" -> set [ "--root"; "--work" ], set [ "--accept" ]
            | _ -> Set.empty, Set.empty

        let rec loop seen remaining =
            match remaining with
            | [] -> None
            | option :: value :: tail when
                Set.contains option valued
                && not (Set.contains option seen)
                && not (value.StartsWith("-", StringComparison.Ordinal))
                ->
                loop (Set.add option seen) tail
            | flag :: tail when Set.contains flag flags && not (Set.contains flag seen) -> loop (Set.add flag seen) tail
            | token :: _ -> Some token

        loop Set.empty args

    let run args =
        match args with
        | operation :: rest when Set.contains operation (set [ "author"; "inspect"; "migrate"; "rollback" ]) ->
            match unknownArgument operation rest with
            | Some token ->
                emit
                    { Operation = operation
                      Outcome = "blocked"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = []
                      RollbackSourceSha256 = None
                      Diagnostics =
                        [ diagnostic
                              "typedSdd.unknownArgument"
                              $"Unknown or incomplete argument '{token}'."
                              "Use only the documented options and supply every option value." ] }
            | None ->
                match operation with
                | "author" -> author rest
                | "inspect" -> inspect rest
                | "migrate" -> migrate rest
                | "rollback" -> rollback rest
                | _ -> failwith "guarded"
        | _ ->
            emit
                { Operation = "typed-sdd"
                  Outcome = "blocked"
                  Classification = None
                  ChangedPaths = []
                  SemanticDiff = []
                  RollbackSourceSha256 = None
                  Diagnostics =
                    [ diagnostic
                          "typedSdd.unknownOperation"
                          "Unknown Typed SDD operation."
                          "Use author, inspect, migrate, or rollback." ] }
