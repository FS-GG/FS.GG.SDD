namespace FS.GG.SDD.Artifacts

open System
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

module Knowledge =
    type Diagnostic = { Code: string; Path: string; Message: string }
    type Snapshot = { Path: string; Bytes: byte array }
    type Evidence = {
        Repository: string; Revision: string; Path: string; Digest: string
        Run: string; Url: string
    }
    type Record = {
        Id: string; Kind: string; Title: string; Conclusion: string; Limits: string
        Created: string; Updated: string; Scope: string list; Status: string
        Attribution: string list; Evidence: Evidence list
        Related: string list; Supersedes: string list
    }
    type Store = {
        Records: Record list; TotalBytes: int64; RemainingBytes: int64
        Contributors: (string * int64) list
    }
    type Query = { Text: string; Kind: string option; Status: string option; Scope: string option }

    let maxBytes = 10485760L
    let schemaJson = """{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Project knowledge record v1",
  "type": "object",
  "additionalProperties": false,
  "required": [
    "schemaVersion",
    "id",
    "kind",
    "title",
    "conclusion",
    "limits",
    "created",
    "updated",
    "scope",
    "status",
    "attribution",
    "evidence",
    "related",
    "supersedes"
  ],
  "properties": {
    "schemaVersion": {
      "const": 1
    },
    "id": {
      "type": "string",
      "pattern": "^[a-z0-9][a-z0-9-]{0,127}$"
    },
    "kind": {
      "enum": [
        "architecture",
        "decision",
        "cause-fix",
        "experiment-positive",
        "experiment-negative",
        "qualification",
        "operating-lesson"
      ]
    },
    "title": {
      "type": "string",
      "minLength": 1
    },
    "conclusion": {
      "type": "string",
      "minLength": 1
    },
    "limits": {
      "type": "string",
      "minLength": 1
    },
    "created": {
      "type": "string",
      "format": "date"
    },
    "updated": {
      "type": "string",
      "format": "date"
    },
    "scope": {
      "type": "array",
      "uniqueItems": true,
      "items": {
        "type": "string",
        "minLength": 1
      }
    },
    "status": {
      "enum": [
        "observed",
        "accepted",
        "proposed",
        "open",
        "superseded"
      ]
    },
    "attribution": {
      "type": "array",
      "uniqueItems": true,
      "items": {
        "type": "string",
        "minLength": 1
      }
    },
    "evidence": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": [
          "repository",
          "revision",
          "path",
          "digest",
          "run",
          "url"
        ],
        "properties": {
          "repository": {
            "type": "string"
          },
          "revision": {
            "type": "string"
          },
          "path": {
            "type": "string"
          },
          "digest": {
            "type": "string"
          },
          "run": {
            "type": "string"
          },
          "url": {
            "type": "string"
          }
        }
      }
    },
    "related": {
      "type": "array",
      "uniqueItems": true,
      "items": {
        "type": "string",
        "minLength": 1
      }
    },
    "supersedes": {
      "type": "array",
      "uniqueItems": true,
      "items": {
        "type": "string",
        "minLength": 1
      }
    }
  }
}
"""
    let utf8 = UTF8Encoding(false, true)
    let diagnostic code path message = { Code = code; Path = path; Message = message }
    let safePath (path: string) =
        not (String.IsNullOrWhiteSpace path)
        && not (path.Contains('\\') || path.Contains(':') || path.StartsWith('/'))
        && (path.Split('/') |> Array.forall (fun p -> p <> "" && p <> "." && p <> ".." && not (p |> Seq.exists Char.IsControl)))
    let identity (id: string) = Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{0,127}$")
    let kinds = [ "architecture"; "decision"; "cause-fix"; "experiment-positive"; "experiment-negative"; "qualification"; "operating-lesson" ]
    let statuses = [ "observed"; "accepted"; "proposed"; "open"; "superseded" ]

    let objectKeys allowed (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then failwith "Expected a JSON object."
        let keys = element.EnumerateObject() |> Seq.map _.Name |> Seq.toList
        if keys.Length <> (keys |> List.distinct).Length then failwith "Duplicate JSON properties are not allowed."
        let unknown = keys |> List.filter (fun key -> not (List.contains key allowed))
        if not unknown.IsEmpty then failwithf "Unsupported fields: %s." (String.concat ", " unknown)
        let missing = allowed |> List.filter (fun key -> not (List.contains key keys))
        if not missing.IsEmpty then failwithf "Missing fields: %s." (String.concat ", " missing)

    let text (key: string) (element: JsonElement) =
        let value = element.GetProperty key
        if value.ValueKind <> JsonValueKind.String then failwithf "%s must be a string." key
        value.GetString() |> Option.ofObj |> Option.defaultWith (fun () -> failwithf "%s cannot be null." key)

    let nonempty key element =
        let value = text key element
        if String.IsNullOrWhiteSpace value then failwithf "%s must be nonempty." key
        value

    let texts (key: string) (element: JsonElement) =
        let array = element.GetProperty key
        if array.ValueKind <> JsonValueKind.Array then failwithf "%s must be an array." key
        let values = array.EnumerateArray() |> Seq.map (fun item ->
            if item.ValueKind <> JsonValueKind.String || String.IsNullOrWhiteSpace(item.GetString()) then failwithf "%s must contain nonempty strings." key
            item.GetString() |> Option.ofObj |> Option.defaultWith (fun () -> failwithf "%s cannot contain null." key)) |> Seq.toList
        if values.Length <> (List.distinct values).Length then failwithf "%s has duplicate values." key
        values

    let version (element: JsonElement) =
        let value = element.GetProperty "schemaVersion"
        if value.ValueKind <> JsonValueKind.Number || value.GetRawText() <> "1" then failwith "schemaVersion must be integer 1."

    let decode (path: string) (bytes: byte array) (parser: JsonElement -> 'a) =
        try
            let value = utf8.GetString(bytes).TrimStart('\uFEFF')
            if value.Contains('\000') then failwith "NUL is not canonical text."
            use document = JsonDocument.Parse value
            let rec unique (element: JsonElement) =
                match element.ValueKind with
                | JsonValueKind.Object ->
                    let properties = element.EnumerateObject() |> Seq.toList
                    if properties.Length <> (properties |> List.map _.Name |> List.distinct).Length then failwith "Duplicate JSON properties are not allowed."
                    properties |> List.iter (fun property -> unique property.Value)
                | JsonValueKind.Array -> element.EnumerateArray() |> Seq.iter unique
                | _ -> ()
            unique document.RootElement
            Ok (parser document.RootElement)
        with
        | :? DecoderFallbackException -> Error [ diagnostic "knowledge.utf8" path "Canonical files must be valid UTF-8." ]
        | ex -> Error [ diagnostic "knowledge.schema" path ex.Message ]

    let parse path bytes =
        let result = decode path bytes (fun root ->
            objectKeys [ "schemaVersion"; "id"; "kind"; "title"; "conclusion"; "limits"; "created"; "updated"; "scope"; "status"; "attribution"; "evidence"; "related"; "supersedes" ] root
            version root
            let id = nonempty "id" root
            if not (identity id) then failwith "id must be a stable lowercase ASCII slug."
            let kind = nonempty "kind" root
            if not (List.contains kind kinds) then failwith "Unsupported knowledge kind."
            let status = nonempty "status" root
            if not (List.contains status statuses) then failwith "Unsupported knowledge status."
            let date key =
                let value = nonempty key root
                match DateOnly.TryParseExact(value, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None) with
                | true, _ -> value
                | _ -> failwithf "%s must be an ISO calendar date." key
            let created, updated = date "created", date "updated"
            if updated < created then failwith "updated precedes created."
            let evidence = root.GetProperty "evidence"
            if evidence.ValueKind <> JsonValueKind.Array then failwith "evidence must be an array."
            let evidence = evidence.EnumerateArray() |> Seq.map (fun item ->
                objectKeys [ "repository"; "revision"; "path"; "digest"; "run"; "url" ] item
                let revision, digest, run = text "revision" item, text "digest" item, text "run" item
                if [ revision; digest; run ] |> List.forall String.IsNullOrWhiteSpace then failwith "Evidence requires revision, digest or run provenance."
                { Repository = nonempty "repository" item; Revision = revision; Path = text "path" item; Digest = digest; Run = run; Url = text "url" item }) |> Seq.toList
            let scope, attribution = texts "scope" root, texts "attribution" root
            if scope.IsEmpty || attribution.IsEmpty || evidence.IsEmpty then failwith "scope, attribution and evidence must be nonempty."
            { Id = id; Kind = kind; Status = status; Title = nonempty "title" root
              Conclusion = nonempty "conclusion" root; Limits = nonempty "limits" root
              Created = created; Updated = updated; Scope = scope; Attribution = attribution
              Evidence = evidence; Related = texts "related" root; Supersedes = texts "supersedes" root })
        result |> Result.bind (fun record ->
            let errors = [
                if not (safePath path) || path <> $"records/{record.Id}.json" then
                    yield diagnostic "knowledge.path" path "Record filename must be records/<id>.json."
                for evidence in record.Evidence do
                    if evidence.Path <> "" && not (safePath evidence.Path) then yield diagnostic "knowledge.path" path "Evidence path must be repository-relative without escapes."
                    if evidence.Url <> "" then
                        let validUrl =
                            match Uri.TryCreate(evidence.Url, UriKind.Absolute) with
                            | true, uri -> Option.ofObj uri |> Option.exists (fun value -> (value.Scheme = "https" || value.Scheme = "http") && value.UserInfo = "")
                            | _ -> false
                        if not validUrl then yield diagnostic "knowledge.link" path "Evidence URL must be HTTP(S), absolute and credential-free."
                for id in record.Related @ record.Supersedes do
                    if not (identity id) || id = record.Id then yield diagnostic "knowledge.link" path "Record links require another stable identity."
            ]
            if errors.IsEmpty then Ok record else Error errors)

    let load (snapshots: Snapshot list) =
        let contributors = snapshots |> List.map (fun s -> s.Path, int64 s.Bytes.LongLength) |> List.sortBy (fun (p, n) -> -n, p)
        let total = contributors |> List.fold (fun total (_, bytes) -> Checked.(+) total bytes) 0L
        let diagnostics = ResizeArray<Diagnostic>()
        let records = ResizeArray<Record>()
        let add result = match result with Ok record -> records.Add record | Error errors -> diagnostics.AddRange errors
        for path, files in snapshots |> List.groupBy _.Path do
            if files.Length > 1 then diagnostics.Add(diagnostic "knowledge.duplicatePath" path "Canonical path occurs more than once.")
        for required in [ "manifest.json"; "schema.json"; "README.md" ] do
            if not (snapshots |> List.exists (fun s -> s.Path = required)) then diagnostics.Add(diagnostic "knowledge.missing" required "Required canonical file is absent.")
        for snapshot in snapshots do
            let path = snapshot.Path
            if not (safePath path) then diagnostics.Add(diagnostic "knowledge.path" path "Canonical paths must be relative without escapes.")
            elif path.StartsWith("records/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal) then add (parse path snapshot.Bytes)
            elif path = "manifest.json" then
                match decode path snapshot.Bytes (fun root -> objectKeys [ "schemaVersion" ] root; version root) with
                | Ok () -> ()
                | Error errors -> diagnostics.AddRange errors
            elif path = "schema.json" then
                match decode path snapshot.Bytes (fun root ->
                    let actual = System.Text.Json.Nodes.JsonNode.Parse(root.GetRawText())
                    let expected = System.Text.Json.Nodes.JsonNode.Parse(schemaJson)
                    if not (System.Text.Json.Nodes.JsonNode.DeepEquals(actual, expected)) then failwith "schema.json does not match the supported knowledge record v1 schema.") with
                | Ok () -> ()
                | Error errors -> diagnostics.AddRange errors
            elif path = "README.md" then
                try
                    let value = utf8.GetString snapshot.Bytes
                    if value.Contains('\000') then diagnostics.Add(diagnostic "knowledge.file" path "NUL/binary content is unsupported.")
                with :? DecoderFallbackException -> diagnostics.Add(diagnostic "knowledge.utf8" path "Canonical files must be valid UTF-8.")
            else diagnostics.Add(diagnostic "knowledge.file" path "Unsupported canonical file; caches, raw source, logs and backups belong outside the store.")
        let sorted = records |> Seq.sortBy _.Id |> Seq.toList
        for id, group in sorted |> List.groupBy _.Id do
            if group.Length > 1 then diagnostics.Add(diagnostic "knowledge.duplicateId" id "Stable identity occurs more than once.")
        for record in sorted do
            for id in record.Related @ record.Supersedes do
                match sorted |> List.tryFind (fun r -> r.Id = id) with
                | None -> diagnostics.Add(diagnostic "knowledge.link" record.Id $"Linked identity '{id}' is absent.")
                | Some prior when List.contains id record.Supersedes && prior.Status <> "superseded" -> diagnostics.Add(diagnostic "knowledge.supersession" record.Id $"Superseded identity '{id}' must have superseded status.")
                | Some _ -> ()
        if total > maxBytes then
            let largest = contributors |> List.truncate 5 |> List.map (fun (p,n) -> $"{p}={n}") |> String.concat ", "
            diagnostics.Add(diagnostic "knowledge.budget" "" $"Total {total}; limit {maxBytes}; remaining {maxBytes-total}; largest: {largest}.")
        if diagnostics.Count > 0 then Error (diagnostics |> Seq.sortBy (fun d -> d.Path, d.Code, d.Message) |> Seq.toList)
        else Ok { Records = sorted; TotalBytes = total; RemainingBytes = maxBytes-total; Contributors = contributors }

    let search (query: Query) (store: Store) =
        let includes (text: string) = text.Contains(query.Text, StringComparison.OrdinalIgnoreCase)
        store.Records |> List.filter (fun r ->
            (query.Kind |> Option.forall ((=) r.Kind)) && (query.Status |> Option.forall ((=) r.Status))
            && (query.Scope |> Option.forall (fun scope -> List.contains scope r.Scope))
            && (includes (String.concat "\n" ([r.Id; r.Kind; r.Title; r.Conclusion; r.Limits; r.Status; r.Created; r.Updated] @ r.Scope @ r.Attribution @ r.Related @ r.Supersedes @ (r.Evidence |> List.collect (fun e -> [e.Repository; e.Revision; e.Path; e.Digest; e.Run; e.Url]))))))
        |> List.sortBy _.Id
    let get id (store: Store) = store.Records |> List.tryFind (fun r -> r.Id = id)
    let related id (store: Store) =
        match get id store with
        | None -> Error [ diagnostic "knowledge.notFound" id "Knowledge identity is absent." ]
        | Some record ->
            Ok (store.Records |> List.filter (fun r -> r.Id <> id && (List.contains r.Id (record.Related @ record.Supersedes) || List.contains id (r.Related @ r.Supersedes))) |> List.sortBy _.Id)
