namespace FS.GG.SDD.Knowledge

open System
open System.IO
open System.Text
open System.Text.Json
open System.Security.Cryptography
open System.Text.RegularExpressions
open System.Diagnostics

[<CLIMutable>]
type Evidence = { Locator: string; Repository: string; Revision: string; Path: string; Digest: string; Run: string }
[<CLIMutable>]
type Relation = { Kind: string; Target: string }
/// A concise finding; raw source, logs, attachments and snapshots have no storage field.
[<CLIMutable>]
type Record = {
    SchemaVersion: int; Id: string; Kind: string; Title: string; Summary: string
    Rationale: string; Limits: string; State: string; Basis: string
    Author: string; Created: string; Updated: string; AsOf: string; Scope: string; Applicability: string
    Evidence: Evidence array; Relations: Relation array
}
[<CLIMutable>]
type Version = { Record: Record; Revision: string; GitCommit: string }
[<CLIMutable>]
type Query = { Text: string; Kind: string; Scope: string; State: string; Id: string }
[<CLIMutable>]
type SizeReport = { Bytes: int64; Limit: int64; Growth: int64 }
[<CLIMutable>]
type ExportFile = { Path: string; Digest: string; Text: string }
[<CLIMutable>]
type Export = { Schema: string; HistoryIncluded: bool; Files: ExportFile array; InventoryDigest: string }

/// Canonical current records only. Past versions belong to ordinary Git history.
module Store =
    let byteLimit = 10485760L
    let schemaText = "{\"schema\":\"fsgg.knowledge/1\",\"byteLimit\":10485760,\"history\":\"git\"}\n"
    let private encoding = UTF8Encoding(false, true)
    let private options = JsonSerializerOptions(WriteIndented = true, UnmappedMemberHandling = Serialization.JsonUnmappedMemberHandling.Disallow)
    let private hash (bytes: byte array) = Convert.ToHexStringLower(SHA256.HashData bytes)
    let private serialize value = JsonSerializer.SerializeToUtf8Bytes(value, options)
    let private fail message = raise (InvalidDataException message)
    let private token (value: string) =
        if String.IsNullOrWhiteSpace value || not (Regex.IsMatch(value, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$")) then fail "Unsafe knowledge identifier."
        value
    let private safe (root: string) (relative: string) =
        if String.IsNullOrWhiteSpace relative || Path.IsPathRooted relative || relative.Contains('\\') then fail "Unsafe store path."
        let segments = relative.Split('/')
        if segments |> Array.exists (fun p -> p = ".." || p = "." || p = "") then fail "Unsafe store path."
        let full = Path.GetFullPath(Path.Combine(root, relative))
        let mutable current = full
        while not (String.IsNullOrEmpty current) do
            if (File.Exists current || Directory.Exists current) && (File.GetAttributes current).HasFlag FileAttributes.ReparsePoint then fail "Symlink knowledge path refused."
            let parent = Path.GetDirectoryName current
            current <- match parent with null -> "" | value -> value
        full
    let private validate (record: Record) =
        if isNull (box record) || record.SchemaVersion <> 1 then fail "Unsupported knowledge record schema."
        token record.Id |> ignore
        let required = [record.Author;record.Title;record.Summary;record.Limits;record.Basis;record.Created;record.Updated;record.AsOf;record.Scope;record.Applicability]
        if required |> List.exists String.IsNullOrWhiteSpace then fail "Missing finding, limits, dates, scope or evidence basis."
        if isNull (box record.Rationale) || isNull (box record.Evidence) || isNull (box record.Relations) then fail "Missing finding metadata."
        if not (Set.contains record.Kind (set ["architecture";"decision";"diagnostic";"experiment";"bug-fix";"qualification";"operation";"handoff";"guidance"])) then fail "Unsupported finding kind; copied source/log records are excluded."
        if not (Set.contains record.State (set ["proposal";"open-question";"reported";"observed";"accepted";"superseded";"retracted"])) then fail "Unknown semantic state."
        if not (Set.contains record.Basis (set ["evidence";"reported";"inference";"proposal"])) then fail "Unknown evidence basis."
        if (record.State = "observed" || record.State = "accepted") && record.Basis <> "evidence" then fail "Verified/accepted findings require explicit evidence basis."
        if record.Evidence.Length = 0 then fail "At least one provenance reference is required."
        for field in record.Title :: record.Summary :: record.Rationale :: record.Limits :: record.Applicability :: [] do
            if encoding.GetByteCount field > 16384 || field.Contains("```", StringComparison.Ordinal) then fail "Finding text must be concise prose, not a source/log/document dump."
        for date in [record.Created;record.Updated;record.AsOf] do
            match DateOnly.TryParseExact(date, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None) with
            | true, _ -> ()
            | _ -> fail "Dates must use yyyy-MM-dd."
        for evidence in record.Evidence do
            if isNull (box evidence) || String.IsNullOrWhiteSpace evidence.Locator || [evidence.Repository;evidence.Revision;evidence.Path;evidence.Digest;evidence.Run] |> List.exists (box >> isNull) then fail "Malformed evidence reference."
            if String.IsNullOrWhiteSpace evidence.Revision && String.IsNullOrWhiteSpace evidence.Run then fail "Evidence requires a revision or run identity."
            let mutable uri = Unchecked.defaultof<Uri>
            if Uri.TryCreate(evidence.Locator, UriKind.Absolute, &uri) && not (String.IsNullOrEmpty uri.UserInfo) then fail "Credential-bearing references refused."
        for relation in record.Relations do
            if isNull (box relation) || not (Set.contains relation.Kind (set ["related";"supersedes";"duplicate-of";"bug-fix";"experiment-outcome"])) then fail "Unknown relation."
            token relation.Target |> ignore
    let private recordPath id = "records/" + token id + ".json"
    let private decode (bytes: byte array) =
        let record = match JsonSerializer.Deserialize<Record>(bytes, options) with null -> fail "Null knowledge record." | value -> value
        validate record
        record
    let private files root =
        if not (Directory.Exists root) then []
        else
            safe root "schema.json" |> ignore
            let rec walk relative =
                let path = if relative = "" then root else safe root relative
                Directory.EnumerateFileSystemEntries path |> Seq.collect (fun entry ->
                    let rel = Path.GetRelativePath(root, entry).Replace('\\','/')
                    let checkedPath = safe root rel
                    if Directory.Exists checkedPath then
                        if rel <> "records" then fail "Unexpected directory in canonical knowledge store."
                        walk rel |> Seq.ofList
                    else
                        if rel <> "schema.json" && not (Regex.IsMatch(rel, "^records/[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}\\.json$")) then fail "Unexpected canonical file; source/log/cache/backup files are excluded."
                        Seq.singleton rel) |> Seq.toList
            walk "" |> List.sort
    let check root =
        let paths = files root
        if not (List.isEmpty paths) && not (List.contains "schema.json" paths) then fail "Missing knowledge schema."
        let mutable total = 0L
        for path in paths do
            let bytes = File.ReadAllBytes(safe root path)
            total <- total + int64 bytes.Length
            if path = "schema.json" then
                use schema = JsonDocument.Parse bytes
                if schema.RootElement.GetProperty("schema").GetString() <> "fsgg.knowledge/1" || schema.RootElement.GetProperty("byteLimit").GetInt64() <> byteLimit || schema.RootElement.GetProperty("history").GetString() <> "git" || (schema.RootElement.EnumerateObject() |> Seq.length) <> 3 then fail "Unsupported knowledge schema."
            else
                let record = decode bytes
                if recordPath record.Id <> path then fail "Record identity mismatch."
        if total > byteLimit then fail ($"Knowledge byte budget exceeded: {total} > {byteLimit}; consolidate findings preserving Git history.")
        { Bytes = total; Limit = byteLimit; Growth = 0L }
    let get root id =
        check root |> ignore
        let bytes = File.ReadAllBytes(safe root (recordPath id))
        { Record = decode bytes; Revision = hash bytes; GitCommit = "" }
    let all root =
        check root |> ignore
        files root |> List.filter (fun p -> p <> "schema.json") |> List.map (fun p ->
            let bytes = File.ReadAllBytes(safe root p)
            { Record = decode bytes; Revision = hash bytes; GitCommit = "" })
    let private atomicWrite root relative bytes =
        let path = safe root relative
        Directory.CreateDirectory(Path.GetDirectoryName path |> string) |> ignore
        // Temporary files are siblings outside the canonical store and are never Git authority.
        let temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath root) |> string, ".knowledge-write-" + Guid.NewGuid().ToString("N"))
        try
            use stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)
            stream.Write(bytes, 0, bytes.Length)
            stream.Flush(true)
            stream.Dispose()
            File.Move(temp, path, true)
        finally
            if File.Exists temp then File.Delete temp
    let private locked root action =
        safe root "schema.json" |> ignore
        let parent = Path.GetDirectoryName(Path.GetFullPath root) |> string
        Directory.CreateDirectory parent |> ignore
        let path = Path.Combine(parent, ".knowledge-writer.lock")
        use guard = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose)
        action ()
    let capture root (expectedRevision: string option) (record: Record) = locked root (fun () ->
        validate record
        let before = check root
        let path = safe root (recordPath record.Id)
        let bytes = serialize record
        let currentBytes = if File.Exists path then Some(File.ReadAllBytes path) else None
        let current = currentBytes |> Option.map hash
        if current <> expectedRevision && currentBytes <> Some bytes then fail "Knowledge revision conflict: retrieve and reconcile the current finding explicitly."
        let schemaPath = safe root "schema.json"
        let schemaGrowth = if File.Exists schemaPath then 0L else int64 (encoding.GetByteCount schemaText)
        let oldLength = if File.Exists path then FileInfo(path).Length else 0L
        let growth = int64 bytes.Length - oldLength + schemaGrowth
        if before.Bytes + growth > byteLimit then fail ($"Knowledge byte budget exceeded: {before.Bytes + growth} > {byteLimit}; growth={growth}.")
        if not (File.Exists schemaPath) then atomicWrite root "schema.json" (encoding.GetBytes schemaText)
        atomicWrite root (recordPath record.Id) bytes
        { Record = record; Revision = hash bytes; GitCommit = "" }, { Bytes = before.Bytes + growth; Limit = byteLimit; Growth = growth })
    let search root (query: Query) =
        let matches filter value = String.IsNullOrEmpty filter || filter = value
        all root |> List.filter (fun v ->
            let r = v.Record
            matches query.Kind r.Kind && matches query.Scope r.Scope && matches query.State r.State && matches query.Id r.Id
            && (String.IsNullOrEmpty query.Text || (encoding.GetString(serialize r)).Contains(query.Text, StringComparison.OrdinalIgnoreCase)))
    let related root id = all root |> List.filter (fun v -> v.Record.Id = id || v.Record.Relations |> Array.exists (fun r -> r.Target = id))
    let private git root arguments =
        let start = ProcessStartInfo("git", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        start.ArgumentList.Add "-C"
        start.ArgumentList.Add root
        for argument: string in arguments do start.ArgumentList.Add argument
        use child = match Process.Start start with null -> fail "Git is required for historical versions." | value -> value
        let output = child.StandardOutput.ReadToEndAsync()
        let error = child.StandardError.ReadToEndAsync()
        child.WaitForExit()
        if child.ExitCode <> 0 then fail ("Git history unavailable: " + error.Result)
        output.Result
    let private gitLocation root =
        safe root "schema.json" |> ignore
        let top = (git root ["rev-parse";"--show-toplevel"]).Trim()
        top, Path.GetRelativePath(top, Path.GetFullPath root).Replace('\\','/')
    let getVersion root id (commit: string) =
        if not (Regex.IsMatch(commit, "^[a-f0-9]{40}$|^[a-f0-9]{64}$")) then fail "Historical version requires an exact Git commit identity."
        let top, relative = gitLocation root
        let text = git top ["show";commit + ":" + relative + "/" + recordPath id]
        let bytes = encoding.GetBytes text
        let record = decode bytes
        if record.Id <> id then fail "Historical identity mismatch."
        { Record = record; Revision = hash bytes; GitCommit = commit }
    let history root id =
        let top, relative = gitLocation root
        let commits = git top ["log";"--diff-filter=AM";"--format=%H";"--";relative + "/" + recordPath id]
        commits.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.map (getVersion root id) |> Array.toList
    let private inventoryDigest (files: ExportFile array) = files |> Array.map (fun f -> f.Path + " " + f.Digest + "\n") |> String.concat "" |> encoding.GetBytes |> hash
    let export root (ids: string array) =
        check root |> ignore
        let paths = "schema.json" :: (ids |> Array.distinct |> Array.map recordPath |> Array.toList)
        let entries = paths |> List.sort |> List.map (fun path ->
            let bytes = File.ReadAllBytes(safe root path)
            { Path = path; Digest = hash bytes; Text = encoding.GetString bytes }) |> List.toArray
        serialize { Schema = "fsgg.knowledge-export/1"; HistoryIncluded = false; Files = entries; InventoryDigest = inventoryDigest entries }
    let restore root (bytes: byte array) = locked root (fun () ->
        let archive = match JsonSerializer.Deserialize<Export>(bytes, options) with null -> fail "Null knowledge export." | value -> value
        if isNull (box archive) || archive.Schema <> "fsgg.knowledge-export/1" || archive.HistoryIncluded || isNull (box archive.Files) then fail "Unsupported knowledge export."
        if archive.Files <> Array.sortBy (fun f -> f.Path) archive.Files || (Array.distinctBy (fun f -> f.Path) archive.Files).Length <> archive.Files.Length || inventoryDigest archive.Files <> archive.InventoryDigest then fail "Corrupt export inventory."
        let staging = Path.Combine(Path.GetTempPath(), "knowledge-restore-" + Guid.NewGuid().ToString("N"))
        try
            for file in archive.Files do
                if file.Path <> "schema.json" && not (Regex.IsMatch(file.Path, "^records/[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}\\.json$")) then fail "Unsafe export member."
                let value = encoding.GetBytes file.Text
                if hash value <> file.Digest then fail "Corrupt export member."
                atomicWrite staging file.Path value
            check staging |> ignore
            let before = check root
            let mutable growth = 0L
            for file in archive.Files do
                let path = safe root file.Path
                let value = encoding.GetBytes file.Text
                if File.Exists path then
                    if File.ReadAllBytes path <> value then fail "Restore would overwrite authored knowledge; reconcile explicitly."
                else growth <- growth + int64 value.Length
            if before.Bytes + growth > byteLimit then fail "Restore exceeds knowledge byte budget."
            for file in archive.Files do atomicWrite root file.Path (encoding.GetBytes file.Text)
            { Bytes = before.Bytes + growth; Limit = byteLimit; Growth = growth }
        finally
            if Directory.Exists staging then Directory.Delete(staging, true))
    let browse root =
        all root |> List.map (fun version ->
            let r = version.Record
            "## " + r.Title + "\n\n" + r.Summary + "\n\nState: " + r.State + " · basis: " + r.Basis + " · as of: " + r.AsOf + "\n\nLimits: " + r.Limits + "\n\n" + (r.Evidence |> Array.map (fun e -> "- " + e.Locator + " (revision " + e.Revision + "; run " + e.Run + ")") |> String.concat "\n")) |> String.concat "\n\n"
