namespace FS.GG.SDD.Cli

open System
open System.IO
open System.Text.Json
module Knowledge =
    module K = FS.GG.SDD.Artifacts.Knowledge
    type Request = { Root: string; Verb: string; Id: string; Query: K.Query; TextOutput: bool }
    type Model = { Request: Request; Result: Result<K.Store, K.Diagnostic list> option }
    type Msg = Loaded of Result<K.Snapshot list, K.Diagnostic list>
    type Effect = ReadStore of string
    let init (request: Request) = { Request = request; Result = None }, [ ReadStore request.Root ]
    let update (Loaded result) (model: Model) : Model * Effect list = { model with Result = Some (result |> Result.bind K.load) }, []

    let readStore (root: string) =
        let diagnostics = ResizeArray<K.Diagnostic>()
        let files = ResizeArray<K.Snapshot>()
        let paths = ResizeArray<string * string * int64>()
        let error code path message = diagnostics.Add { Code = code; Path = path; Message = message }
        let isLink (path: string) = File.GetAttributes(path).HasFlag FileAttributes.ReparsePoint
        try
            let absolute = Path.GetFullPath root
            let fsgg = Path.Combine(absolute, ".fsgg")
            let store = Path.Combine(fsgg, "knowledge")
            // Reject every ancestor before enumeration; never follow a linked store or parent.
            let rec ancestors path =
                if isLink path then false
                else
                    match Directory.GetParent path |> Option.ofObj with
                    | None -> true
                    | Some parent -> ancestors parent.FullName
            if not (Directory.Exists store) then error "knowledge.missing" store "Knowledge store is absent; reads do not initialize it."
            elif not (ancestors store) then error "knowledge.path" store "Symbolic links/reparse points in store ancestry are unsupported."
            else
                let rec visit directory =
                    for path in Directory.GetFileSystemEntries(directory) |> Array.sort do
                        let relative = Path.GetRelativePath(store, path).Replace('\\', '/')
                        if isLink path then error "knowledge.path" relative "Symbolic links/reparse points are unsupported."
                        elif Directory.Exists path then
                            if relative = "records" then visit path
                            else error "knowledge.file" relative "Only the records directory is canonical; caches and attachments belong outside the store."
                        else
                            let canonical = List.contains relative ["manifest.json"; "schema.json"; "README.md"] || (relative.StartsWith("records/", StringComparison.Ordinal) && relative.EndsWith(".json", StringComparison.Ordinal))
                            if not canonical then error "knowledge.file" relative "Unsupported canonical file; caches, raw source, logs and backups belong outside the store."
                            let size = FileInfo(path).Length
                            paths.Add((relative, path, size))
                visit store
                let total = paths |> Seq.fold (fun total (_, _, bytes) -> Checked.(+) total bytes) 0L
                if total > K.maxBytes then
                    let largest = paths |> Seq.sortBy (fun (p, _, n) -> -n, p) |> Seq.truncate 5 |> Seq.map (fun (p, _, n) -> $"{p}={n}") |> String.concat ", "
                    error "knowledge.budget" "" $"Total {total}; limit {K.maxBytes}; remaining {K.maxBytes-total}; largest: {largest}."
                elif diagnostics.Count = 0 then
                    for relative, path, _ in paths do
                        if isLink path then error "knowledge.path" relative "Path became a symbolic link before read."
                        else files.Add { Path = relative; Bytes = File.ReadAllBytes path }
            if diagnostics.Count = 0 then Ok (files |> Seq.toList) else Error (diagnostics |> Seq.toList)
        with ex -> Error [ { Code = "knowledge.read"; Path = root; Message = ex.Message } ]

    let serialize (request: Request) (result: Result<K.Store, K.Diagnostic list>) =
        let records, errors, total, remaining, contributors =
            match result with
            | Error errors -> [], errors, Nullable<int64>(), Nullable<int64>(), []
            | Ok store ->
                let selected: Result<K.Record list, K.Diagnostic list> =
                    match request.Verb with
                    | "check" -> Ok store.Records
                    | "search" -> Ok (K.search request.Query store)
                    | "get" ->
                        match K.get request.Id store with
                        | Some record -> Ok [record]
                        | None -> Error [{ Code = "knowledge.notFound"; Path = request.Id; Message = "Knowledge identity is absent." }]
                    | "related" -> K.related request.Id store
                    | _ -> Error [{ Code = "knowledge.argv"; Path = ""; Message = "Expected check, search, get or related." }]
                match selected with
                | Ok records -> records, [], Nullable store.TotalBytes, Nullable store.RemainingBytes, store.Contributors
                | Error errors -> [], errors, Nullable store.TotalBytes, Nullable store.RemainingBytes, store.Contributors
        // Explicit field projection keeps the automation contract independent of CLR naming.
        let output =
            {| schemaVersion = 1; command = "knowledge " + request.Verb
               valid = errors.IsEmpty; totalBytes = total; limitBytes = K.maxBytes; remainingBytes = remaining
               contributors = contributors |> List.map (fun (path,bytes) -> {| path = path; bytes = bytes |})
               diagnostics = errors |> List.map (fun d -> {| code = d.Code; path = d.Path; message = d.Message |})
               records = records |> List.map (fun r ->
                   {| id = r.Id; kind = r.Kind; title = r.Title; conclusion = r.Conclusion; limits = r.Limits
                      created = r.Created; updated = r.Updated; scope = r.Scope; status = r.Status
                      attribution = r.Attribution; related = r.Related; supersedes = r.Supersedes
                      evidence = r.Evidence |> List.map (fun e -> {| repository = e.Repository; revision = e.Revision; path = e.Path; digest = e.Digest; run = e.Run; url = e.Url |}) |}) |}
        if request.TextOutput then
            Console.Out.WriteLine($"knowledge {request.Verb}: valid={errors.IsEmpty}; total={total}; limit={K.maxBytes}; remaining={remaining}")
            for r in records do
                let scopes = String.concat ", " r.Scope
                let authors = String.concat ", " r.Attribution
                Console.Out.WriteLine($"{r.Id} [{r.Status}] {r.Title}\n{r.Conclusion}\nLimits: {r.Limits}\nScope: {scopes}\nAttribution: {authors}\nCreated: {r.Created}; updated: {r.Updated}")
                for e in r.Evidence do Console.Out.WriteLine($"Evidence: repository={e.Repository}; revision={e.Revision}; path={e.Path}; digest={e.Digest}; run={e.Run}; url={e.Url}")
                Console.Out.WriteLine("Related: " + String.concat ", " r.Related + "; supersedes: " + String.concat ", " r.Supersedes)
            for path, bytes in contributors do Console.Out.WriteLine($"Bytes: {path}={bytes}")
            for d in errors do Console.Out.WriteLine($"{d.Code} {d.Path}: {d.Message}")
        else Console.Out.WriteLine(JsonSerializer.Serialize output)
        if errors.IsEmpty then 0 else 1

    let run args =
        let help = "fsgg-sdd knowledge check|search|get|related [--root <project>] [--query <text>] [--id <id>] [--kind <kind>] [--status <status>] [--scope <scope>] [--json|--text|--rich]"
        if args |> List.exists (fun a -> a = "--help" || a = "-h") then
            if args |> List.exists (fun a -> a = "--text" || a = "--rich") then Console.Out.WriteLine help
            else Console.Out.WriteLine(JsonSerializer.Serialize {| command = "knowledge"; usage = help |})
            0
        else
            let mutable request = { Root = "."; Verb = ""; Id = ""; Query = { Text = ""; Kind = None; Status = None; Scope = None }; TextOutput = false }
            let mutable error = None
            let seen = Collections.Generic.HashSet<string>()
            let rec parse remaining =
                match remaining with
                | [] -> ()
                | ("--text" | "--rich") :: tail -> request <- { request with TextOutput = true }; parse tail
                | "--json" :: tail -> parse tail
                | flag :: value :: tail when List.contains flag ["--root"; "--query"; "--id"; "--kind"; "--status"; "--scope"] && not (value.StartsWith("--", StringComparison.Ordinal)) ->
                    if not (seen.Add flag) then error <- Some $"Repeated option {flag}."
                    else
                        request <-
                            match flag with
                            | "--root" -> { request with Root = value }
                            | "--id" -> { request with Id = value }
                            | "--query" -> { request with Query = { request.Query with Text = value } }
                            | "--kind" -> { request with Query = { request.Query with Kind = Some value } }
                            | "--status" -> { request with Query = { request.Query with Status = Some value } }
                            | _ -> { request with Query = { request.Query with Scope = Some value } }
                    parse tail
                | value :: tail when request.Verb = "" && List.contains value ["check"; "search"; "get"; "related"] -> request <- { request with Verb = value }; parse tail
                | value :: _ -> error <- Some $"Unknown option, missing value or unsupported verb: {value}."
            parse args
            if request.Verb = "" then error <- Some "Expected check, search, get or related."
            if (request.Verb = "get" || request.Verb = "related") && request.Id = "" then error <- Some "get and related require --id <stable-id>."
            match error with
            | Some message -> serialize request (Error [{ Code = "knowledge.argv"; Path = ""; Message = message }])
            | None ->
                let model, effects = init request
                let result = effects |> List.fold (fun model effect ->
                    match effect with ReadStore root -> update (Loaded (readStore root)) model |> fst) model
                serialize request result.Result.Value
