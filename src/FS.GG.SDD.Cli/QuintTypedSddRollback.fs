namespace FS.GG.SDD.Cli

open System
open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.TypedSpecifications

module internal QuintTypedSddRollback =
    type private Entry =
        { OriginalPath: string
          BackupPath: string
          Sha256: string
          Bytes: int64 }

    let private diagnostic id message correction : TypedLifecycleDiagnostic =
        { Id = id; Message = message; Correction = correction }

    let private containedPath (root: string) (relative: string) =
        if String.IsNullOrWhiteSpace relative || Path.IsPathRooted relative then None
        else
            let full = Path.GetFullPath(Path.Combine(root, relative))
            let prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + string Path.DirectorySeparatorChar
            if full.StartsWith(prefix, StringComparison.Ordinal) then Some full else None

    let private encode entries =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", "fsgg.typed-sdd.rollback-inventory/v1")
        writer.WriteStartArray("entries")
        for entry in entries |> List.sortBy _.OriginalPath do
            writer.WriteStartObject()
            writer.WriteString("originalPath", entry.OriginalPath)
            writer.WriteString("backupPath", entry.BackupPath)
            writer.WriteString("sha256", entry.Sha256)
            writer.WriteNumber("bytes", entry.Bytes)
            writer.WriteEndObject()
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Array.append (stream.ToArray()) [| byte '\n' |]

    let private decode (bytes: byte array) =
        try
            use document = JsonDocument.Parse bytes
            let root = document.RootElement
            let names = root.EnumerateObject() |> Seq.map _.Name |> Seq.toList
            if Set.ofList names <> set [ "schema"; "entries" ] || names.Length <> 2
               || root.GetProperty("schema").GetString() <> "fsgg.typed-sdd.rollback-inventory/v1"
               || root.GetProperty("entries").ValueKind <> JsonValueKind.Array then
                Error "inventory does not match the closed v1 schema"
            else
                root.GetProperty("entries").EnumerateArray()
                |> Seq.map (fun item ->
                    let itemNames = item.EnumerateObject() |> Seq.map _.Name |> Seq.toList
                    if Set.ofList itemNames <> set [ "originalPath"; "backupPath"; "sha256"; "bytes" ] || itemNames.Length <> 4 then
                        invalidOp "entry does not match the closed schema"
                    { OriginalPath = item.GetProperty("originalPath").GetString() |> Option.ofObj |> Option.defaultValue ""
                      BackupPath = item.GetProperty("backupPath").GetString() |> Option.ofObj |> Option.defaultValue ""
                      Sha256 = item.GetProperty("sha256").GetString() |> Option.ofObj |> Option.defaultValue ""
                      Bytes = item.GetProperty("bytes").GetInt64() })
                |> Seq.toList
                |> Ok
        with ex -> Error ex.Message

    let snapshot rootPath workId sourceRelative =
        try
            let authorityRelative = TypedAuthorityManifest.path workId
            let authorityPath = containedPath rootPath authorityRelative |> Option.defaultWith (fun () -> invalidOp "unsafe authority path")

            let originals =
                if File.Exists authorityPath then
                    match File.ReadAllText authorityPath |> TypedAuthority.deserialize with
                    | Ok(FsharpSpecificationV1 authority) ->
                        let read relative =
                            let path = containedPath rootPath relative |> Option.defaultWith (fun () -> invalidOp "unsafe v1 authority path")
                            if not (File.Exists path) then invalidOp $"v1 authority path '{relative}' is missing"
                            File.ReadAllBytes path
                        let canonical = read authority.CanonicalPath
                        let normalized = read authority.NormalizedPath
                        let markdown = read authority.MarkdownPath
                        let findings =
                            TypedAuthorityManifest.validate authority.PackageIdentity true (Some canonical) (Some normalized) (Some markdown) authority
                            @ TypedAuthorityManifest.validateDerivation canonical normalized markdown
                        if not (List.isEmpty findings) then
                            invalidOp $"v1 authority is not valid: {findings.Head.Message}"
                        [ authorityRelative; authority.CanonicalPath; authority.NormalizedPath; authority.MarkdownPath ]
                    | Ok(QuintSpecificationV1 _) -> invalidOp "authority is already manifest-v2"
                    | Error finding -> invalidOp finding.Message
                else
                    [ sourceRelative ]
                |> List.distinct
                |> List.sort

            let basePath = $".fsgg/typed-sdd-rollback/v1/{workId}"
            let entries, backupWrites =
                originals
                |> List.mapi (fun index original ->
                    let full = containedPath rootPath original |> Option.defaultWith (fun () -> invalidOp $"unsafe rollback source '{original}'")
                    if not (File.Exists full) then invalidOp $"rollback source '{original}' is missing"
                    let bytes = File.ReadAllBytes full
                    let backup = $"{basePath}/{index:D4}.bin"
                    { OriginalPath = original
                      BackupPath = backup
                      Sha256 = TypedAuthorityManifest.sha256 bytes
                      Bytes = int64 bytes.Length }, (backup, bytes))
                |> List.unzip

            let inventoryPath = $"{basePath}/inventory.json"
            let inventoryBytes = encode entries
            let rollback: QuintTypedSddHost.Rollback =
                { ManifestPath = inventoryPath
                  ManifestBytes = inventoryBytes
                  Writes = backupWrites @ [ inventoryPath, inventoryBytes ] }
            Ok rollback
        with ex ->
            Error [ diagnostic "typedSdd.v2.rollbackSnapshotFailed" ex.Message "Restore the complete readable v1 authority before migration." ]

    let restore rootPath workId (authority: QuintAuthorityManifest) apply =
        match authority.RollbackManifestPath, authority.RollbackManifestSha256 with
        | Some inventoryRelative, Some inventorySha ->
            try
                let inventoryPath = containedPath rootPath inventoryRelative |> Option.defaultWith (fun () -> invalidOp "unsafe rollback inventory path")
                let inventoryBytes = File.ReadAllBytes inventoryPath
                if TypedAuthorityManifest.sha256 inventoryBytes <> inventorySha then invalidOp "rollback inventory digest mismatch"
                let entries = decode inventoryBytes |> Result.defaultWith invalidOp
                if List.isEmpty entries || (entries |> List.map _.OriginalPath |> List.distinct |> List.length) <> entries.Length then
                    invalidOp "rollback inventory is empty or aliases original paths"

                let restores =
                    entries
                    |> List.map (fun entry ->
                        let original = containedPath rootPath entry.OriginalPath |> Option.defaultWith (fun () -> invalidOp "unsafe original path")
                        let backup = containedPath rootPath entry.BackupPath |> Option.defaultWith (fun () -> invalidOp "unsafe backup path")
                        let bytes = File.ReadAllBytes backup
                        if int64 bytes.Length <> entry.Bytes || TypedAuthorityManifest.sha256 bytes <> entry.Sha256 then
                            invalidOp $"rollback backup mismatch for '{entry.OriginalPath}'"
                        original, bytes)

                let v2Relatives = TypedAuthorityManifest.path workId :: (authority.Artifacts |> List.map _.Path)
                let backupRelatives = inventoryRelative :: (entries |> List.map _.BackupPath)
                let affectedRelatives = (v2Relatives @ backupRelatives @ (entries |> List.map _.OriginalPath)) |> List.distinct
                let originalSet = entries |> List.map _.OriginalPath |> Set.ofList
                let deletes =
                    (v2Relatives @ backupRelatives)
                    |> List.distinct
                    |> List.filter (originalSet.Contains >> not)
                    |> List.map (fun relative -> containedPath rootPath relative |> Option.defaultWith (fun () -> invalidOp "unsafe delete path"))

                try
                    apply restores deletes
                    for entry in entries do
                        let path = containedPath rootPath entry.OriginalPath |> Option.get
                        if not (File.Exists path) || TypedAuthorityManifest.sha256 (File.ReadAllBytes path) <> entry.Sha256 then
                            invalidOp $"rollback post-state mismatch for '{entry.OriginalPath}'"

                    Ok affectedRelatives
                with ex -> raise ex
            with ex ->
                Error [ diagnostic "typedSdd.v2.rollbackFailed" ex.Message "Restore the authenticated rollback inventory and retry; the live tree was preserved." ]
        | _ ->
            Error [ diagnostic "typedSdd.v2.rollbackMissing" "Manifest-v2 has no authenticated rollback inventory." "Rollback is available only after accepted v1 migration." ]
