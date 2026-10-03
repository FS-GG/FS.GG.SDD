namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open System.Diagnostics
open System.Text.Json
open FS.GG.SDD.Knowledge
open Xunit

[<Collection("ProcessGlobalEnv")>]
module KnowledgeCommandTests =
    let private temporary body =
        let root = Path.Combine(Path.GetTempPath(), "knowledge-cli-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try body root finally Directory.Delete(root, true)
    let private invoke args =
        use output = new StringWriter()
        use error = new StringWriter()
        let priorOut, priorError = Console.Out, Console.Error
        try
            Console.SetOut output
            Console.SetError error
            let result = FS.GG.SDD.Cli.Knowledge.run args
            result, output.ToString(), error.ToString()
        finally
            Console.SetOut priorOut
            Console.SetError priorError
    [<Fact>]
    let ``CLI API agent and thin script queries share records and exact revisions`` () = temporary (fun root ->
        Assert.Equal(0, invoke ["initialize";"--root";root] |> fun (code,_,_) -> code)
        let store = Path.Combine(root, ".fsgg", "knowledge")
        for kind in ["architecture";"decision";"diagnostic";"experiment";"bug-fix"] do
            let record = { Workspace.initialRecord with Id = kind; Kind = kind; Title = kind; Summary = "Reusable " + kind + " finding with failed-outcome limits." }
            let path = Path.Combine(root, kind + ".json")
            File.WriteAllText(path, JsonSerializer.Serialize record)
            Assert.Equal(0, invoke ["capture";"--root";root;"--record";path] |> fun (code,_,_) -> code)
        let code, text, _ = invoke ["search";"--root";root;"--text";"failed-outcome"]
        Assert.Equal(0, code)
        let cli = JsonSerializer.Deserialize<Version array>(text) |> nonNull
        let api = Store.search store { Text = "failed-outcome"; Kind = ""; Scope = ""; State = ""; Id = "" }
        Assert.Equal<string>(api |> List.map (fun v -> v.Revision), cli |> Array.map (fun v -> v.Revision))
        Assert.Equal(5, cli.Length)
        let _, browse, _ = invoke ["browse";"--root";root]
        Assert.Contains("revision", browse)
        let archive = Path.Combine(root, "export.json")
        Assert.Equal(0, invoke ["export";"--root";root;"--ids";"architecture,decision,diagnostic,experiment,bug-fix";"--archive";archive] |> fun (code,_,_) -> code)
        let restored = Path.Combine(root, "restored")
        Assert.Equal(0, invoke ["restore";"--root";root;"--store";restored;"--archive";archive] |> fun (code,_,_) -> code)
        Assert.Equal(5, (Store.all restored).Length))
    [<Fact>]
    let ``CLI rejects arbitrary file import unknown option and false facts`` () = temporary (fun root ->
        Assert.Equal(1, invoke ["capture";"--root";root;"--source";"source.fs"] |> fun (code,_,_) -> code)
        Assert.Equal(1, invoke ["search";"--root";root;"--unknown";"x"] |> fun (code,_,_) -> code)
        Assert.False(Directory.Exists(Path.Combine(root, ".fsgg", "knowledge"))))
