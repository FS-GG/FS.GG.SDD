namespace FS.GG.SDD.Cli

open System
open System.IO
open System.Text.Json
open FS.GG.SDD.Knowledge

module Knowledge =
    let run (args: string list) =
        try
            let rec parse (remaining: string list) (values: Map<string,string>) =
                match remaining with
                | [] -> values
                | name :: value :: tail when name.StartsWith("--", StringComparison.Ordinal) && not (value.StartsWith("--", StringComparison.Ordinal)) && not (Map.containsKey name values) -> parse tail (Map.add name value values)
                | _ -> invalidArg "args" "Expected unique --option value pairs."
            match args with
            | [] -> invalidArg "args" "Use initialize, capture, search, get, get-version, history, related, check, browse, export or restore."
            | operation :: rest ->
                let values = parse rest Map.empty
                let get name fallback = Map.tryFind name values |> Option.defaultValue fallback
                let required name = match Map.tryFind name values with Some value -> value | None -> invalidArg name "Missing required option."
                let root = Path.GetFullPath(get "--root" ".")
                let store = Path.GetFullPath(get "--store" (Path.Combine(root, ".fsgg", "knowledge")))
                let output value = Console.Out.WriteLine(JsonSerializer.Serialize value)
                let allowed =
                    match operation with
                    | "capture" -> ["--record";"--expected"]
                    | "search" -> ["--text";"--kind";"--scope";"--state";"--id"]
                    | "get" | "history" | "related" -> ["--id"]
                    | "get-version" -> ["--id";"--commit"]
                    | "export" -> ["--archive";"--ids"]
                    | "restore" -> ["--archive"]
                    | "check" | "browse" | "initialize" | "--help" | "help" -> []
                    | _ -> invalidArg "operation" "Unknown knowledge operation."
                for KeyValue(key, _) in values do
                    if not (List.contains key (["--root";"--store"] @ allowed)) then invalidArg key "Unknown knowledge option."
                match operation with
                | "--help" | "help" -> output {| command = "knowledge"; operations = [|"initialize";"capture --record JSON [--expected DIGEST]";"search [--text TEXT] [--kind KIND] [--scope SCOPE] [--state STATE] [--id ID]";"get --id ID";"history --id ID";"get-version --id ID --commit SHA";"related --id ID";"check";"browse";"export --ids IDS --archive PATH";"restore --archive PATH"|]; store = "--root ROOT [--store DIRECTORY]" |}
                | "initialize" ->
                    if Map.containsKey "--store" values then invalidArg "--store" "Workspace initialize uses its shared canonical store."
                    output (Workspace.initialize root)
                | "capture" ->
                    let record = JsonSerializer.Deserialize<Record>(File.ReadAllText(required "--record")) |> nonNull
                    let version, size = Store.capture store (Map.tryFind "--expected" values) record
                    output {| version = version; size = size |}
                | "search" -> output (Store.search store { Text = get "--text" ""; Kind = get "--kind" ""; Scope = get "--scope" ""; State = get "--state" ""; Id = get "--id" "" })
                | "get" -> output (Store.get store (required "--id"))
                | "history" -> output (Store.history store (required "--id"))
                | "related" -> output (Store.related store (required "--id"))
                | "get-version" -> output (Store.getVersion store (required "--id") (required "--commit"))
                | "check" -> output (Store.check store)
                | "browse" -> Console.Out.WriteLine(Store.browse store)
                | "export" -> File.WriteAllBytes(required "--archive", Store.export store ((required "--ids").Split(',', StringSplitOptions.RemoveEmptyEntries)))
                | "restore" -> output (Store.restore store (File.ReadAllBytes(required "--archive")))
                | _ -> failwith "guarded"
                0
        with ex ->
            Console.Error.WriteLine(JsonSerializer.Serialize {| outcome = "blocked"; diagnostic = "knowledge.invalid"; message = ex.Message |})
            1
