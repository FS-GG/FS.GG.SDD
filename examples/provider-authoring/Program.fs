open System
open ProviderAuthoring

// Sample I/O is interpreted at this small boundary; declarations remain pure data.
type Effect =
    | Emit of Authoring.Inputs
    | Check of string * string

type Msg =
    | Completed
    | Refused of string

type Model = { ExitCode: int; Error: string option }

let update message =
    match message with
    | Completed -> { ExitCode = 0; Error = None }
    | Refused error -> { ExitCode = 1; Error = Some error }

let init (arguments: string array) =
    match Array.toList arguments with
    | [ "check"; provenance; summary ] -> Check(provenance, summary)
    | "emit" :: pairs ->
        let rec parse result =
            function
            | [] -> result
            | key :: value :: rest when
                List.contains
                    key
                    [
                        "--template-root"
                        "--fixture-executable"
                        "--test-input"
                        "--policy"
                        "--out"
                    ]
                && not (Map.containsKey key result)
                ->
                parse (Map.add key value result) rest
            | _ -> invalidArg "arguments" "Unknown, duplicate or incomplete emit option."

        let options = parse Map.empty pairs

        let selected key =
            match Map.tryFind key options with
            | Some value when value <> "" -> value
            | _ -> invalidArg "arguments" ("Missing " + key)

        Emit
            {
                TemplateRoot = selected "--template-root"
                FixtureExecutable = selected "--fixture-executable"
                TestInput = selected "--test-input"
                Policy = selected "--policy"
                Output = selected "--out"
            }
    | _ -> invalidArg "arguments" "Use emit with five explicit input/output options, or check <provenance> <summary>."

let interpret =
    function
    | Emit inputs -> Authoring.emit inputs
    | Check(provenance, summary) -> Authoring.check provenance summary

[<EntryPoint>]
let main arguments =
    let result =
        try
            init arguments |> interpret
            update Completed
        with error ->
            update (Refused error.Message)

    result.Error |> Option.iter (fun message -> Console.Error.WriteLine message)
    result.ExitCode
