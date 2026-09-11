namespace FS.GG.SDD.Cli

open System
open System.Globalization
open System.IO
open System.Runtime.InteropServices
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Threading
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.TypedSpecifications

/// Installed-tool boundary for placing already acquired, exact Quint tool objects into a cache.
module internal QuintProvision =
    type private Candidate =
        { Id: string
          Version: string
          Origin: string
          SourcePath: string
          ExpectedSha256: string
          ExpectedBytes: int64 option
          Bytes: byte array
          CachePath: string }

    let private optionValue name args =
        args
        |> List.tryFindIndex ((=) name)
        |> Option.bind (fun index -> args |> List.tryItem (index + 1))

    let private sha256 (bytes: byte array) =
        SHA256.HashData bytes
        |> Array.map (fun value -> value.ToString("x2", CultureInfo.InvariantCulture))
        |> String.concat ""

    let private diagnostic id message correction : TypedLifecycleDiagnostic =
        { Id = id
          Message = message
          Correction = correction }

    let private packageIdentity () =
        $"FS.GG.SDD.Cli/{SchemaVersion.currentGeneratorVersion().Version}"

    let private platformIdentity () =
        let os =
            if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                "linux"
            elif RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                "windows"
            elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
                "osx"
            else
                "unknown"

        let architecture =
            match RuntimeInformation.OSArchitecture with
            | Architecture.X64 -> "amd64"
            | Architecture.Arm64 -> "arm64"
            | value -> value.ToString().ToLowerInvariant()

        $"{os}/{architecture}"

    let private requirement id =
        QuintToolchain.general.Components
        |> List.collect (fun tool ->
            tool.Objects
            |> List.map (fun requirement -> tool.Version, tool.Source, requirement))
        |> List.find (fun (_, _, requirement) -> requirement.Id = id)

    let private serialize
        (outcome: string)
        (profile: string)
        (cacheRoot: string)
        (candidates: Candidate list)
        (diagnostics: TypedLifecycleDiagnostic list)
        =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.typed-sdd.provision-report/v1")
        writer.WriteString("operation", "provision")
        writer.WriteString("outcome", outcome)
        writer.WriteString("package", packageIdentity ())
        writer.WriteString("platform", platformIdentity ())
        writer.WriteString("profile", profile)
        writer.WriteString("cacheRoot", cacheRoot)
        writer.WriteStartArray("objects")

        for candidate in candidates |> List.sortBy _.Id do
            writer.WriteStartObject()
            writer.WriteString("id", candidate.Id)
            writer.WriteString("version", candidate.Version)
            writer.WriteString("origin", candidate.Origin)
            writer.WriteString("sourcePath", candidate.SourcePath)
            writer.WriteString("sha256", candidate.ExpectedSha256)
            writer.WriteNumber("bytes", candidate.Bytes.LongLength)
            writer.WriteString("cachePath", candidate.CachePath)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteStartArray("diagnostics")

        for item in diagnostics do
            writer.WriteStartObject()
            writer.WriteString("id", item.Id)
            writer.WriteString("message", item.Message)
            writer.WriteString("correction", item.Correction)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    let private emit
        (outcome: string)
        (profile: string)
        (cacheRoot: string)
        (candidates: Candidate list)
        (diagnostics: TypedLifecycleDiagnostic list)
        =
        Console.Out.WriteLine(serialize outcome profile cacheRoot candidates diagnostics)
        if List.isEmpty diagnostics then 0 else 1

    let private readCandidate cacheRoot id sourcePath =
        let version, origin, expected = requirement id

        try
            let bytes = File.ReadAllBytes sourcePath
            let actualSha = sha256 bytes

            if
                actualSha <> expected.Sha256
                || expected.Bytes |> Option.exists ((<>) bytes.LongLength)
            then
                Error(
                    diagnostic
                        "typedSdd.provision.objectMismatch"
                        $"The supplied {id} object does not match the accepted SHA256/size."
                        $"Acquire {origin} at version {version} and retry with its exact bytes."
                )
            else
                Ok
                    { Id = id
                      Version = version
                      Origin = origin
                      SourcePath = Path.GetFullPath sourcePath
                      ExpectedSha256 = expected.Sha256
                      ExpectedBytes = expected.Bytes
                      Bytes = bytes
                      CachePath = Path.Combine(cacheRoot, "objects", expected.Sha256) }
        with
        | :? FileNotFoundException
        | :? DirectoryNotFoundException ->
            Error(
                diagnostic
                    "typedSdd.provision.sourceMissing"
                    $"The supplied {id} path is missing."
                    $"Acquire {origin} at version {version} and pass its local path."
            )
        | ex ->
            Error(
                diagnostic
                    "typedSdd.provision.sourceUnreadable"
                    $"The supplied {id} path could not be read: {ex.Message}"
                    "Correct file access and retry."
            )

    let private markExecutable (path: string) =
        if
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        then
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead
                ||| UnixFileMode.UserWrite
                ||| UnixFileMode.UserExecute
                ||| UnixFileMode.GroupRead
                ||| UnixFileMode.GroupExecute
                ||| UnixFileMode.OtherRead
                ||| UnixFileMode.OtherExecute
            )

    let private acquireLock path =
        let rec attempt remaining =
            try
                Ok(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            with :? IOException as ex ->
                if remaining > 0 then
                    Thread.Sleep 50
                    attempt (remaining - 1)
                else
                    Error ex

        attempt 100

    let private installAtomically cacheRoot candidates =
        let objectsRoot = Path.Combine(cacheRoot, "objects")

        let existingConflict =
            candidates
            |> List.tryPick (fun candidate ->
                if File.Exists candidate.CachePath then
                    try
                        let existing = File.ReadAllBytes candidate.CachePath

                        if sha256 existing = candidate.ExpectedSha256 then
                            None
                        else
                            Some candidate.Id
                    with _ ->
                        Some candidate.Id
                else
                    None)

        match existingConflict with
        | Some id ->
            Error(
                diagnostic
                    "typedSdd.provision.cacheConflict"
                    $"The content-addressed cache path for {id} contains different or unreadable bytes."
                    "Remove the invalid cache object explicitly, then provision again."
            )
        | None ->
            Directory.CreateDirectory objectsRoot |> ignore
            let lockPath = Path.Combine(cacheRoot, ".typed-sdd-provision.lock")

            match acquireLock lockPath with
            | Error ex ->
                Error(
                    diagnostic
                        "typedSdd.provision.lockUnavailable"
                        $"The cache provisioning lock could not be acquired: {ex.Message}"
                        "Wait for the active provisioner to finish, then retry."
                )
            | Ok lock ->
                use _lock = lock
                let staging = Path.Combine(objectsRoot, $".provision-{Guid.NewGuid():N}")
                let created = ResizeArray<string>()

                try
                    // Recheck under the cross-process lock. A previous crashed provision can leave one
                    // valid content-addressed object; author/inspect still refuse the incomplete set,
                    // and this retry safely completes it.
                    for candidate in candidates do
                        if File.Exists candidate.CachePath then
                            let existing = File.ReadAllBytes candidate.CachePath

                            if sha256 existing <> candidate.ExpectedSha256 then
                                invalidOp $"cache conflict for {candidate.Id}"

                            markExecutable candidate.CachePath

                    Directory.CreateDirectory staging |> ignore

                    for candidate in candidates do
                        let staged = Path.Combine(staging, candidate.ExpectedSha256)
                        File.WriteAllBytes(staged, candidate.Bytes)
                        markExecutable staged

                    for candidate in candidates do
                        if not (File.Exists candidate.CachePath) then
                            File.Move(Path.Combine(staging, candidate.ExpectedSha256), candidate.CachePath)
                            created.Add candidate.CachePath

                    Directory.Delete(staging, true)
                    Ok()
                with ex ->
                    for path in created do
                        try
                            File.Delete path
                        with _ ->
                            ()

                    try
                        if Directory.Exists staging then
                            Directory.Delete(staging, true)
                    with _ ->
                        ()

                    Error(
                        diagnostic
                            "typedSdd.provision.writeFailed"
                            $"The exact tool objects could not be committed as a complete set: {ex.Message}"
                            "Correct cache access and retry; no object newly accepted by this failed invocation is retained."
                    )

    let run args =
        let profile =
            optionValue "--profile" args
            |> Option.defaultValue QuintToolchain.general.Profile

        let valued = set [ "--cache"; "--profile"; "--quint"; "--lmt" ]

        let rec unknown seen remaining =
            match remaining with
            | [] -> None
            | option :: value :: tail when
                Set.contains option valued
                && not (Set.contains option seen)
                && not (value.StartsWith("-", StringComparison.Ordinal))
                ->
                unknown (Set.add option seen) tail
            | token :: _ -> Some token

        let cacheResult =
            match optionValue "--cache" args with
            | None -> Ok ""
            | Some value ->
                try
                    Ok(Path.GetFullPath value)
                with ex ->
                    Error(
                        diagnostic
                            "typedSdd.provision.cachePathInvalid"
                            $"The cache path is invalid: {ex.Message}"
                            "Pass a valid local cache root."
                    )

        let cacheRoot = cacheResult |> Result.defaultValue ""

        let cacheError =
            match cacheResult with
            | Error error -> Some error
            | Ok _ -> None

        match unknown Set.empty args with
        | Some token ->
            emit
                "blocked"
                profile
                cacheRoot
                []
                [ diagnostic
                      "typedSdd.provision.unknownArgument"
                      $"Unknown or incomplete argument '{token}'."
                      "Use --cache, --profile, --quint and --lmt, supplying every value once." ]
        | None when Option.isSome cacheError -> emit "blocked" profile cacheRoot [] [ Option.get cacheError ]
        | None when platformIdentity () <> QuintToolchain.general.Platform ->
            emit
                "blocked"
                profile
                cacheRoot
                []
                [ diagnostic
                      "typedSdd.provision.platformUnsupported"
                      $"The installed platform '{platformIdentity ()}' does not match '{QuintToolchain.general.Platform}'."
                      "Provision this qualified toolchain only on its declared platform." ]
        | None when profile <> QuintToolchain.general.Profile ->
            emit
                "blocked"
                profile
                cacheRoot
                []
                [ diagnostic
                      "typedSdd.provision.profileUnsupported"
                      $"Profile '{profile}' does not own this tool object set."
                      $"Select {QuintToolchain.general.Profile}; existing profile-1 workspaces require no cache mutation." ]
        | None ->
            match optionValue "--cache" args, optionValue "--quint" args, optionValue "--lmt" args with
            | Some _, Some quintPath, Some lmtPath ->
                let reads =
                    [ readCandidate cacheRoot "quint-binary" quintPath
                      readCandidate cacheRoot "lmt-binary" lmtPath ]

                let diagnostics =
                    reads
                    |> List.choose (function
                        | Error value -> Some value
                        | Ok _ -> None)

                let candidates =
                    reads
                    |> List.choose (function
                        | Ok value -> Some value
                        | Error _ -> None)

                if not (List.isEmpty diagnostics) then
                    emit "blocked" profile cacheRoot [] diagnostics
                else
                    try
                        match installAtomically cacheRoot candidates with
                        | Ok() -> emit "succeeded" profile cacheRoot candidates []
                        | Error error -> emit "blocked" profile cacheRoot [] [ error ]
                    with ex ->
                        emit
                            "blocked"
                            profile
                            cacheRoot
                            []
                            [ diagnostic
                                  "typedSdd.provision.writeFailed"
                                  $"The exact tool objects could not be staged: {ex.Message}"
                                  "Correct cache access and retry; no accepted partial object is retained." ]
            | _ ->
                emit
                    "blocked"
                    profile
                    cacheRoot
                    []
                    [ diagnostic
                          "typedSdd.provision.argumentMissing"
                          "Exact --cache, --quint and --lmt paths are required."
                          "Acquire the qualified objects, then pass all three paths." ]
