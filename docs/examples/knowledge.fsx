// Source-window example: build the Knowledge project before invoking dotnet fsi.
// A published NuGet reference is qualified separately at milestone .5.
#r "../../src/FS.GG.SDD.Knowledge/bin/Debug/net10.0/FS.GG.SDD.Knowledge.dll"
open FS.GG.SDD.Knowledge

let root =
    fsi.CommandLineArgs |> Array.tryItem 1 |> Option.defaultValue ".fsgg/knowledge"

let text = fsi.CommandLineArgs |> Array.tryItem 2 |> Option.defaultValue "failed"

let query =
    {
        Text = text
        Kind = ""
        Scope = ""
        State = ""
        Id = ""
    }

Store.search root query
|> List.iter (fun version ->
    printfn
        "%s %s [%s] %s (revision %s)"
        version.Record.Id
        version.Record.Title
        version.Record.State
        version.Record.Summary
        version.Revision)
