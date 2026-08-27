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

    let private observeAuthorityArtifact rootPath relative =
        match containedPath rootPath relative with
        | None ->
            { Path = relative
              State = QuintAuthorityArtifactState.Unreadable "path is outside the selected project root" }
        | Some path ->
            try
                if File.Exists path then
                    { Path = relative
                      State = QuintAuthorityArtifactState.Present(File.ReadAllBytes path) }
                else
                    { Path = relative
                      State = QuintAuthorityArtifactState.Missing }
            with ex ->
                { Path = relative
                  State = QuintAuthorityArtifactState.Unreadable ex.Message }

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

    type private TransactionEntry =
        { Target: string
          Backup: string
          Existed: bool }

    let private transactionRoot rootPath =
        Path.Combine(rootPath, ".fsgg", "typed-sdd-transactions")

    let private writeJournal (transaction: string) (state: string) (entries: TransactionEntry list) =
        let path = Path.Combine(transaction, "journal.json")
        let temporary = path + ".new"
        use stream = new MemoryStream()
        use writer = new System.Text.Json.Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.typed-sdd-transaction/v1")
        writer.WriteString("state", state)
        writer.WriteStartArray("entries")

        for entry in entries do
            writer.WriteStartObject()
            writer.WriteString("target", entry.Target)
            writer.WriteString("backup", entry.Backup)
            writer.WriteBoolean("existed", entry.Existed)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        File.WriteAllBytes(temporary, Array.append (stream.ToArray()) [| byte '\n' |])
        File.Move(temporary, path, true)

    let private readJournal transaction =
        use document =
            System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(transaction, "journal.json")))

        let root = document.RootElement

        if root.GetProperty("schema").GetString() <> "fsgg.typed-sdd-transaction/v1" then
            invalidOp "unsupported Typed SDD transaction journal"

        let state =
            root.GetProperty("state").GetString() |> Option.ofObj |> Option.defaultValue ""

        let entries =
            root.GetProperty("entries").EnumerateArray()
            |> Seq.map (fun item ->
                { Target = item.GetProperty("target").GetString() |> Option.ofObj |> Option.defaultValue ""
                  Backup = item.GetProperty("backup").GetString() |> Option.ofObj |> Option.defaultValue ""
                  Existed = item.GetProperty("existed").GetBoolean() })
            |> Seq.toList

        state, entries

    let private recoverTransactions rootPath =
        let coordination = transactionRoot rootPath
        Directory.CreateDirectory coordination |> ignore

        for transaction in Directory.GetDirectories coordination |> Array.sort do
            let journalPath = Path.Combine(transaction, "journal.json")

            if File.Exists journalPath then
                let state, entries = readJournal transaction

                if state = "prepared" then
                    for entry in entries do
                        let target =
                            containedPath rootPath entry.Target
                            |> Option.defaultWith (fun () -> invalidOp "unsafe transaction recovery target")

                        let backup = Path.Combine(transaction, entry.Backup)

                        if entry.Existed then
                            if not (File.Exists backup) then
                                invalidOp "transaction recovery backup is missing"

                            Path.GetDirectoryName target
                            |> Option.ofObj
                            |> Option.iter (Directory.CreateDirectory >> ignore)

                            File.Copy(backup, target, true)
                        elif File.Exists target then
                            File.Delete target
                elif state <> "committed" then
                    invalidOp "unknown Typed SDD transaction state"

            Directory.Delete(transaction, true)

    let private acquireAuthorityLock rootPath =
        let coordination = transactionRoot rootPath
        Directory.CreateDirectory coordination |> ignore
        let lockPath = Path.Combine(coordination, "authority.lock")
        let timer = Stopwatch.StartNew()
        let mutable transactionLock: FileStream option = None

        while transactionLock.IsNone && timer.Elapsed < TimeSpan.FromSeconds 60.0 do
            try
                transactionLock <-
                    Some(new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            with :? IOException ->
                Threading.Thread.Sleep 10

        let transactionLock =
            transactionLock
            |> Option.defaultWith (fun () ->
                raise (TimeoutException("Timed out waiting for the Typed SDD authority transaction lock.")))

        recoverTransactions rootPath
        transactionLock

    let private atomicReplaceUnlocked rootPath (writes: (string * byte array) list) (deletes: string list) =
        let coordination = Path.Combine(rootPath, ".fsgg", "typed-sdd-transactions")
        Directory.CreateDirectory coordination |> ignore
        let transaction = Path.Combine(coordination, Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory transaction |> ignore
        let writePaths = writes |> List.map fst |> Set.ofList

        if deletes |> List.exists writePaths.Contains then
            invalidArg "deletes" "a transaction path cannot be written and deleted"

        let affectedPaths = (writes |> List.map fst) @ deletes |> List.distinct

        let journalEntries, prior =
            affectedPaths
            |> List.mapi (fun index path ->
                let backup = Path.Combine(transaction, $"prior-{index:D4}.bin")

                let previous =
                    if File.Exists path then
                        let bytes = File.ReadAllBytes path
                        File.WriteAllBytes(backup, bytes)
                        Some bytes
                    else
                        None

                { Target = Path.GetRelativePath(rootPath, path)
                  Backup =
                    Path.GetFileName backup
                    |> Option.ofObj
                    |> Option.defaultValue $"prior-{index:D4}.bin"
                  Existed = previous.IsSome },
                (path, previous))
            |> List.unzip

        let staged = ResizeArray<string * string * byte array>()

        try
            writes
            |> List.iteri (fun index (path, bytes) ->
                Path.GetDirectoryName path
                |> Option.ofObj
                |> Option.iter (Directory.CreateDirectory >> ignore)

                let temporary = Path.Combine(transaction, $"stage-{index:D4}.bin")
                File.WriteAllBytes(temporary, bytes)
                staged.Add(path, temporary, bytes))

            writeJournal transaction "prepared" journalEntries

            Environment.GetEnvironmentVariable("FSGG_TYPED_SDD_TEST_PAUSE_AFTER_PREPARE_MS")
            |> Option.ofObj
            |> Option.bind (fun value ->
                match Int32.TryParse value with
                | true, parsed when parsed > 0 -> Some parsed
                | _ -> None)
            |> Option.iter Threading.Thread.Sleep

            let crashAfter =
                Environment.GetEnvironmentVariable("FSGG_TYPED_SDD_TEST_CRASH_AFTER_MOVE")
                |> Option.ofObj
                |> Option.bind (fun value ->
                    match Int32.TryParse value with
                    | true, parsed when parsed > 0 -> Some parsed
                    | _ -> None)

            let mutable moved = 0

            staged
            |> Seq.sortBy (fun (path, _, _) ->
                if path.EndsWith("typed-authority.json", StringComparison.Ordinal) then
                    1
                else
                    0)
            |> Seq.iter (fun (path, temporary, _) ->
                File.Move(temporary, path, true)
                moved <- moved + 1

                if crashAfter = Some moved then
                    Environment.FailFast($"injected Typed SDD crash after move {moved}"))

            for path in deletes do
                if File.Exists path then
                    File.Delete path

                moved <- moved + 1

                if crashAfter = Some moved then
                    Environment.FailFast($"injected Typed SDD crash after move {moved}")

            for path, _, expected in staged do
                if not (File.Exists path) || File.ReadAllBytes path <> expected then
                    invalidOp $"transaction post-state mismatch for '{path}'"

            for path in deletes do
                if File.Exists path then
                    invalidOp $"transaction delete post-state mismatch for '{path}'"

            writeJournal transaction "committed" journalEntries
            Directory.Delete(transaction, true)
        with ex ->
            for path, bytes in prior do
                match bytes with
                | Some value -> File.WriteAllBytes(path, value)
                | None when File.Exists path -> File.Delete path
                | None -> ()

            staged
            |> Seq.iter (fun (_, temporary, _) ->
                if File.Exists temporary then
                    File.Delete temporary)

            if Directory.Exists transaction then
                Directory.Delete(transaction, true)

            raise ex

    let private atomicWrite rootPath writes =
        use transactionLock = acquireAuthorityLock rootPath
        atomicReplaceUnlocked rootPath writes []

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

                atomicWrite
                    root
                    (extraWrites
                     @ [ canonicalPath, canonicalBytes
                         normalizedPath, normalizedBytes
                         markdownPath, markdownBytes
                         authorityPath, Encoding.UTF8.GetBytes(TypedAuthorityManifest.serialize authority) ])

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

    let private authorQuint args workId agent session =
        let rootPath = root args
        let manifestPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)

        let profile =
            optionValue "--profile" args |> Option.defaultValue QuintProfile.identity

        match optionValue "--cache" args with
        | None ->
            Error
                [ diagnostic
                      "typedSdd.v2.cacheRequired"
                      "Explicit Quint authoring requires a caller-selected local cache."
                      "Pass --cache <path> containing objects/<qualified-sha256>; no acquisition is performed." ]
        | Some cache when not (Directory.Exists cache) ->
            Error
                [ diagnostic
                      "typedSdd.v2.cacheMissing"
                      "The selected local Quint cache does not exist."
                      "Preseed the exact Q1/Q2 cache and pass its path." ]
        | Some cache ->
            use transactionLock = acquireAuthorityLock rootPath

            let existing =
                if File.Exists manifestPath then
                    try
                        File.ReadAllText manifestPath |> TypedAuthority.deserialize |> Some
                    with ex ->
                        Some(
                            Error(
                                diagnostic
                                    "typedSdd.authorityUnreadable"
                                    ex.Message
                                    "Correct the existing authority before authoring."
                            )
                        )
                else
                    None

            match existing with
            | Some(Ok(FsharpSpecificationV1 _)) ->
                Error
                    [ diagnostic
                          "typedSdd.v2.migrationRequired"
                          "A manifest-v1 authority cannot be replaced by author --accept."
                          "Use typed-sdd migrate so the exact v1 rollback inventory is retained." ]
            | Some(Error finding) -> Error [ finding ]
            | Some(Ok(QuintSpecificationV1 authority)) when authority.ProfileIdentity <> profile ->
                Error
                    [ diagnostic
                          "typedSdd.v2.profileMigrationRequired"
                          $"Existing authority selects '{authority.ProfileIdentity}', not requested '{profile}'."
                          "Use typed-sdd migrate so profile replacement retains an authenticated rollback." ]
            | Some(Ok(QuintSpecificationV1 _)) when not (has "--accept" args) ->
                Error
                    [ diagnostic
                          "typedSdd.acceptRequired"
                          "Typed SDD authority already exists."
                          "Review the replacement, then pass --accept with a fresh authoring receipt." ]
            | _ ->
                let title = optionValue "--title" args |> Option.defaultValue workId

                if
                    title.Contains('\r')
                    || title.Contains('\n')
                    || title |> Seq.exists Char.IsControl
                then
                    Error
                        [ diagnostic
                              "typedSdd.v2.titleInvalid"
                              "Quint authority titles must be one printable line."
                              "Remove line breaks and control characters from --title." ]
                else
                    let hostResult =
                        if profile = QuintProfile.identity then
                            QuintTypedSddHost.author
                                (packageIdentity ())
                                workId
                                title
                                agent
                                session
                                (Path.GetFullPath cache)
                                None
                                None
                        elif profile = QuintGeneralProfile.identity then
                            match optionValue "--source" args, optionValue "--bindings" args with
                            | Some sourceRelative, Some bindingsRelative ->
                                match
                                    containedPath rootPath sourceRelative, containedPath rootPath bindingsRelative
                                with
                                | Some sourcePath, Some bindingsPath when
                                    File.Exists sourcePath && File.Exists bindingsPath
                                    ->
                                    match File.ReadAllText bindingsPath |> QuintGeneralBindingManifest.deserialize with
                                    | Error findings ->
                                        findings
                                        |> List.map (fun finding ->
                                            diagnostic finding.Code finding.Message finding.Correction)
                                        |> Error
                                    | Ok selectors ->
                                        QuintTypedSddHost.authorGeneral
                                            (packageIdentity ())
                                            workId
                                            title
                                            agent
                                            session
                                            (Path.GetFullPath cache)
                                            sourceRelative
                                            (File.ReadAllBytes sourcePath)
                                            selectors
                                            None
                                | _ ->
                                    Error
                                        [ diagnostic
                                              "typedSdd.v2.generalInputMissing"
                                              "The selected profile-2 source or binding manifest is absent or unsafe."
                                              "Pass contained project-relative --source and --bindings paths." ]
                            | _ ->
                                Error
                                    [ diagnostic
                                          "typedSdd.v2.generalInputRequired"
                                          "Profile 2 requires an authored literate source and selector manifest."
                                          "Pass --source <markdown> --bindings <selector-json>." ]
                        else
                            Error
                                [ diagnostic
                                      "typedSdd.v2.profileIdentityMismatch"
                                      $"Profile identity '{profile}' is unsupported."
                                      $"Use {QuintProfile.identity} or {QuintGeneralProfile.identity}." ]

                    match hostResult with
                    | Error findings -> Error findings
                    | Ok output ->
                        try
                            let writes =
                                output.Writes
                                |> List.map (fun (relative, bytes) ->
                                    match containedPath rootPath relative with
                                    | Some path -> path, bytes
                                    | None -> invalidOp $"Quint host emitted an unsafe path: {relative}")

                            atomicReplaceUnlocked rootPath writes []
                            Ok(output.Writes |> List.map fst)
                        with ex ->
                            Error
                                [ diagnostic
                                      "typedSdd.v2.transactionFailed"
                                      ex.Message
                                      "Correct filesystem access and retry; the prior authority was restored." ]

    let private migrateQuint args workId sourceRelative (migrationPayload: byte array) expectedSourceSha =
        let rootPath = root args
        let agent = optionValue "--agent" args |> Option.defaultValue ""
        let session = optionValue "--session" args |> Option.defaultValue ""

        match optionValue "--cache" args with
        | None ->
            Error
                [ diagnostic
                      "typedSdd.v2.cacheRequired"
                      "Quint migration requires a caller-selected local cache."
                      "Pass --cache <path> containing the exact qualified objects." ]
        | Some _ when String.IsNullOrWhiteSpace agent || String.IsNullOrWhiteSpace session ->
            Error
                [ diagnostic
                      "typedSdd.authoringAgentUnavailable"
                      "Migration requires an explicit authoring agent and session receipt."
                      "Pass --agent <id> --session <id>." ]
        | Some cache when not (Directory.Exists cache) ->
            Error
                [ diagnostic
                      "typedSdd.v2.cacheMissing"
                      "The selected local Quint cache does not exist."
                      "Preseed the exact Q1/Q2 cache and pass its path." ]
        | Some cache ->
            use transactionLock = acquireAuthorityLock rootPath
            let manifestPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)

            let currentMatchesProposal =
                if File.Exists manifestPath then
                    try
                        match File.ReadAllText manifestPath |> TypedAuthority.deserialize with
                        | Ok(FsharpSpecificationV1 authority) ->
                            containedPath rootPath authority.NormalizedPath
                            |> Option.exists (fun path -> File.Exists path && File.ReadAllBytes path = migrationPayload)
                        | _ -> false
                    with _ ->
                        false
                else
                    containedPath rootPath sourceRelative
                    |> Option.exists (fun path ->
                        File.Exists path
                        && TypedAuthorityManifest.sha256 (File.ReadAllBytes path) = expectedSourceSha)

            if not currentMatchesProposal then
                Error
                    [ diagnostic
                          "typedSdd.v2.migrationProposalStale"
                          "The source authority changed after migration preflight."
                          "Run preflight again and accept only its current semantic payload digest." ]
            else
                match QuintTypedSddRollback.snapshot rootPath workId sourceRelative with
                | Error findings -> Error findings
                | Ok rollback ->
                    let title = optionValue "--title" args |> Option.defaultValue workId

                    if
                        title.Contains('\r')
                        || title.Contains('\n')
                        || title |> Seq.exists Char.IsControl
                    then
                        Error
                            [ diagnostic
                                  "typedSdd.v2.titleInvalid"
                                  "Quint authority titles must be one printable line."
                                  "Remove line breaks and control characters from --title." ]
                    else
                        match
                            QuintTypedSddHost.author
                                (packageIdentity ())
                                workId
                                title
                                agent
                                session
                                (Path.GetFullPath cache)
                                (Some rollback)
                                (Some migrationPayload)
                        with
                        | Error findings -> Error findings
                        | Ok output ->
                            try
                                let writes =
                                    output.Writes
                                    |> List.map (fun (relative, bytes) ->
                                        match containedPath rootPath relative with
                                        | Some path -> path, bytes
                                        | None -> invalidOp $"Quint migration emitted an unsafe path: {relative}")

                                atomicReplaceUnlocked rootPath writes []
                                Ok(output.Writes |> List.map fst)
                            with ex ->
                                Error
                                    [ diagnostic
                                          "typedSdd.v2.transactionFailed"
                                          ex.Message
                                          "Correct filesystem access and retry; the exact v1 authority was restored." ]

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
                let backend =
                    optionValue "--backend" args |> Option.defaultValue "fsharp-specification-v1"

                if backend = "quint" || backend = "quint-specification-v1" then
                    match authorQuint args workId agent session with
                    | Ok changed ->
                        emit
                            { Operation = "author"
                              Outcome = "succeeded"
                              Classification = Some "quint-specification-v1"
                              ChangedPaths = changed
                              SemanticDiff = []
                              RollbackSourceSha256 = None
                              Diagnostics = [] }
                    | Error findings ->
                        emit
                            { Operation = "author"
                              Outcome = "blocked"
                              Classification = Some "quint-specification-v1"
                              ChangedPaths = []
                              SemanticDiff = []
                              RollbackSourceSha256 = None
                              Diagnostics = findings }
                elif backend <> "fsharp" && backend <> "fsharp-specification-v1" then
                    emit
                        { Operation = "author"
                          Outcome = "blocked"
                          Classification = None
                          ChangedPaths = []
                          SemanticDiff = []
                          RollbackSourceSha256 = None
                          Diagnostics =
                            [ diagnostic
                                  "typedSdd.backendUnsupported"
                                  $"Unsupported explicit Typed SDD backend '{backend}'."
                                  "Use fsharp-specification-v1 or quint-specification-v1." ] }
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

                let migrationAnalysis =
                    let authorityPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)

                    if File.Exists authorityPath then
                        try
                            match File.ReadAllText authorityPath |> TypedAuthority.deserialize with
                            | Ok(FsharpSpecificationV1 authority) ->
                                match containedPath rootPath authority.NormalizedPath with
                                | Some normalizedPath when File.Exists normalizedPath ->
                                    let normalized = File.ReadAllText normalizedPath

                                    match SpecificationCodec.deserialize RequirementsExtension.contract normalized with
                                    | Ok model -> Migrated model.Extension
                                    | Error _ ->
                                        RequirementsMigration.analyzeMarkdown (Encoding.UTF8.GetString sourceBytes)
                                | _ -> RequirementsMigration.analyzeMarkdown (Encoding.UTF8.GetString sourceBytes)
                            | _ -> RequirementsMigration.analyzeMarkdown (Encoding.UTF8.GetString sourceBytes)
                        with _ ->
                            RequirementsMigration.analyzeMarkdown (Encoding.UTF8.GetString sourceBytes)
                    else
                        RequirementsMigration.analyzeMarkdown (Encoding.UTF8.GetString sourceBytes)

                let migrationPayload extension =
                    try
                        let authorityPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)

                        if File.Exists authorityPath then
                            match File.ReadAllText authorityPath |> TypedAuthority.deserialize with
                            | Ok(FsharpSpecificationV1 authority) ->
                                match containedPath rootPath authority.NormalizedPath with
                                | Some normalizedPath when File.Exists normalizedPath ->
                                    Ok(File.ReadAllBytes normalizedPath)
                                | _ -> Error "The manifest-v1 normalized authority is missing."
                            | _ -> Error "The existing authority is not manifest-v1."
                        else
                            let seed =
                                newModel
                                    workId
                                    (optionValue "--title" args |> Option.defaultValue workId)
                                    (optionValue "--agent" args |> Option.defaultValue "migration")
                                    (optionValue "--session" args |> Option.defaultValue "migration")

                            match
                                SpecificationCodec.serialize
                                    RequirementsExtension.contract
                                    { seed with Extension = extension }
                            with
                            | Ok normalized -> Ok(Encoding.UTF8.GetBytes(normalized + "\n"))
                            | Error findings -> Error findings.Head.Message
                    with ex ->
                        Error ex.Message

                match migrationAnalysis with
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
                    let payloadSummary =
                        migrationPayload extension
                        |> Result.map (fun payload ->
                            [ $"semantic payload sha256: {TypedAuthorityManifest.sha256 payload}" ])
                        |> Result.defaultValue []

                    let summary =
                        [ $"scope boundaries: {extension.Scope.Length}"
                          $"user stories: {extension.Stories.Length}"
                          $"requirements: {extension.Requirements.Length}"
                          $"acceptance criteria: {extension.Acceptance.Length}"
                          $"lifecycle notes: {extension.LifecycleNotes.Length}"
                          yield! payloadSummary ]

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

                    let backend =
                        optionValue "--backend" args |> Option.defaultValue "fsharp-specification-v1"

                    if backend = "quint" || backend = "quint-specification-v1" then
                        match migrationPayload extension with
                        | Error detail ->
                            emit
                                { Operation = "migrate"
                                  Outcome = "blocked"
                                  Classification = Some "Unsupported"
                                  ChangedPaths = []
                                  SemanticDiff = []
                                  RollbackSourceSha256 = Some rollback
                                  Diagnostics =
                                    [ diagnostic
                                          "typedSdd.v2.migrationPayloadInvalid"
                                          detail
                                          "Restore the canonical manifest-v1 normalized authority and retry." ] }
                        | Ok payload ->
                            let acceptedSummary =
                                summary
                                @ [ $"semantic payload sha256: {TypedAuthorityManifest.sha256 payload}" ]

                            match migrateQuint args workId source payload rollback with
                            | Ok changed ->
                                emit
                                    { Operation = "migrate"
                                      Outcome = "succeeded"
                                      Classification = Some "Migrated"
                                      ChangedPaths = changed
                                      SemanticDiff = acceptedSummary
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
                    else
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
            use transactionLock = acquireAuthorityLock rootPath
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
                let decoded =
                    try
                        File.ReadAllText manifestPath |> TypedAuthority.deserialize
                    with ex ->
                        Error(
                            diagnostic
                                "typedSdd.authorityUnreadable"
                                $"The Typed SDD authority manifest is unreadable: {ex.Message}"
                                "Correct filesystem access and inspect again."
                        )

                match decoded with
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
                    let observations =
                        [ for artifact in authority.Artifacts do
                              yield observeAuthorityArtifact rootPath artifact.Path
                          match authority.RollbackManifestPath with
                          | Some path -> yield observeAuthorityArtifact rootPath path
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
            use transactionLock = acquireAuthorityLock rootPath
            let rc, rn, rm, canonicalPath, normalizedPath, markdownPath = paths rootPath workId
            let rollbackRelative = $"work/{workId}/spec.standard-sdd.rollback.md"
            let rollbackPath = Path.Combine(rootPath, rollbackRelative)
            let manifestPath = Path.Combine(rootPath, TypedAuthorityManifest.path workId)

            let quintAuthority =
                if File.Exists manifestPath then
                    try
                        match File.ReadAllText manifestPath |> TypedAuthority.deserialize with
                        | Ok(QuintSpecificationV1 authority) -> Some authority
                        | _ -> None
                    with _ ->
                        None
                else
                    None

            if not (has "--accept" args) then
                emit
                    { Operation = "rollback"
                      Outcome = "noChange"
                      Classification = None
                      ChangedPaths = []
                      SemanticDiff = [ "restore Standard SDD Markdown authority" ]
                      RollbackSourceSha256 = None
                      Diagnostics = [] }
            elif Option.isSome quintAuthority then
                let authority = quintAuthority.Value

                let observations =
                    [ for artifact in authority.Artifacts do
                          yield observeAuthorityArtifact rootPath artifact.Path
                      match authority.RollbackManifestPath with
                      | Some path -> yield observeAuthorityArtifact rootPath path
                      | None -> () ]

                let findings =
                    TypedAuthority.validateQuintV2 (packageIdentity ()) observations authority

                if not (List.isEmpty findings) then
                    emit
                        { Operation = "rollback"
                          Outcome = "blocked"
                          Classification = Some "quint-specification-v1"
                          ChangedPaths = []
                          SemanticDiff = []
                          RollbackSourceSha256 = authority.RollbackManifestSha256
                          Diagnostics = findings }
                else
                    match QuintTypedSddRollback.restore rootPath workId authority (atomicReplaceUnlocked rootPath) with
                    | Ok changed ->
                        emit
                            { Operation = "rollback"
                              Outcome = "succeeded"
                              Classification = Some "fsharp-specification-v1"
                              ChangedPaths = changed
                              SemanticDiff = [ "restored exact pre-migration v1 authority inventory" ]
                              RollbackSourceSha256 = authority.RollbackManifestSha256
                              Diagnostics = [] }
                    | Error findings ->
                        emit
                            { Operation = "rollback"
                              Outcome = "blocked"
                              Classification = Some "quint-specification-v1"
                              ChangedPaths = []
                              SemanticDiff = []
                              RollbackSourceSha256 = authority.RollbackManifestSha256
                              Diagnostics = findings }
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
                    atomicReplaceUnlocked rootPath [ markdownPath, rollbackBytes ] typedPaths

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
            | "author" ->
                set
                    [ "--root"
                      "--work"
                      "--title"
                      "--agent"
                      "--session"
                      "--backend"
                      "--cache"
                      "--profile"
                      "--source"
                      "--bindings" ],
                set [ "--accept" ]
            | "inspect" -> set [ "--root"; "--work" ], Set.empty
            | "migrate" ->
                set
                    [ "--root"
                      "--work"
                      "--source"
                      "--title"
                      "--agent"
                      "--session"
                      "--backend"
                      "--cache" ],
                set [ "--accept" ]
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
