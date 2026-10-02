namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open System.Diagnostics
open System.Text.Json
open FS.GG.SDD.Artifacts
open Xunit

module KnowledgeCommandTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport
    let private fixture = Path.Combine(Commands.repoRoot, "tests/fixtures/project-knowledge")
    let private run root args =
        let configuration = if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then "Release" else "Debug"
        let host = Path.Combine(Commands.repoRoot, "src/FS.GG.SDD.Cli/bin", configuration, "net10.0/FS.GG.SDD.Cli")
        let start = ProcessStartInfo(host)
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        start.UseShellExecute <- false
        ([ "knowledge" ] @ args @ [ "--root"; root ]) |> List.iter start.ArgumentList.Add
        let completion = FS.GG.SDD.TestShared.TestShared.ChildProcess.runBounded 30000 start
        Assert.Equal("", completion.StandardError)
        completion.ExitCode, completion.StandardOutput

    let private inTemp body =
        let root = Path.Combine(Path.GetTempPath(), "knowledge-fixture-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try body root finally Directory.Delete(root, true)

    let private copyFixture root =
        for path in Directory.GetFiles(fixture, "*", SearchOption.AllDirectories) do
            let target = Path.Combine(root, Path.GetRelativePath(fixture, path))
            let parent = Path.GetDirectoryName target |> Option.ofObj |> Option.defaultWith (fun () -> failwith "Fixture target has no parent directory.")
            Directory.CreateDirectory parent |> ignore
            File.Copy(path, target)

    [<Fact>]
    let ``CLI and API return the identical disclosed four finding identities status provenance`` () =
        let storePath = Path.Combine(fixture, ".fsgg/knowledge")
        let snapshots: Knowledge.Snapshot list =
            Directory.GetFiles(storePath, "*", SearchOption.AllDirectories)
            |> Array.map (fun p -> ({ Path = Path.GetRelativePath(storePath, p).Replace('\\', '/'); Bytes = File.ReadAllBytes p }: Knowledge.Snapshot)) |> Array.toList
        let store = Knowledge.load snapshots |> Result.defaultWith (fun e -> failwithf "%A" e)
        let code, output = run fixture ["check"]
        Assert.Equal(0, code)
        use document = JsonDocument.Parse output
        let records = document.RootElement.GetProperty("records").EnumerateArray() |> Seq.toList
        Assert.Equal(4, records.Length)
        for actual, expected in List.zip records store.Records do
            Assert.Equal(expected.Id, actual.GetProperty("id").GetString())
            Assert.Equal(expected.Status, actual.GetProperty("status").GetString())
            let evidence = actual.GetProperty("evidence").EnumerateArray() |> Seq.toList
            for found, known in List.zip evidence expected.Evidence do
                for key, value in ["repository",known.Repository; "revision",known.Revision; "path",known.Path; "digest",known.Digest; "run",known.Run; "url",known.Url] do
                    Assert.Equal(value, found.GetProperty(key).GetString())
        let searchCode, search = run fixture ["search"; "--query"; "CAFÉ"; "--kind"; "operating-lesson"; "--scope"; "日本語"; "--status"; "observed"]
        Assert.Equal(0, searchCode)
        use searchDocument = JsonDocument.Parse search
        let found = searchDocument.RootElement.GetProperty("records").EnumerateArray() |> Seq.exactlyOne
        Assert.Equal("lesson", found.GetProperty("id").GetString())
        let relatedCode, related = run fixture ["related"; "--id"; "architecture"]
        Assert.Equal(0, relatedCode)
        Assert.Contains("\"id\":\"bug\"", related)
        let getCode, get = run fixture ["get"; "--id"; "experiment"]
        Assert.Equal(0, getCode)
        Assert.Contains("superseded", get)

    [<Fact>]
    let ``Read-only commands ignore malformed sibling caches and preserve every input byte`` () =
        inTemp (fun root ->
            copyFixture root
            let cache = Path.Combine(root, ".fsgg/cache")
            Directory.CreateDirectory cache |> ignore
            File.WriteAllBytes(Path.Combine(cache, "knowledge.db"), [|255uy;0uy|])
            let before = Directory.GetFiles(root, "*", SearchOption.AllDirectories) |> Array.map (fun p -> p, Convert.ToBase64String(File.ReadAllBytes p)) |> Array.sort
            for args in [["check"]; ["search"; "--query"; "日本語"]; ["get"; "--id"; "bug"]; ["related"; "--id"; "architecture"]] do
                let code, _ = run root args
                Assert.Equal(0, code)
            let after = Directory.GetFiles(root, "*", SearchOption.AllDirectories) |> Array.map (fun p -> p, Convert.ToBase64String(File.ReadAllBytes p)) |> Array.sort
            Assert.Equal<(string * string) array>(before, after))

    [<Fact>]
    let ``Missing store invalid argv and linked record refuse without initialization`` () =
        inTemp (fun root ->
            let code, output = run root ["check"]
            Assert.Equal(1, code)
            Assert.Contains("knowledge.missing", output)
            Assert.Empty(Directory.GetFileSystemEntries root)
            let badCode, badOutput = run root ["check"; "--query"]
            Assert.Equal(1, badCode)
            Assert.Contains("knowledge.argv", badOutput)
            copyFixture root
            let escaped = Path.Combine(root, "outside.json")
            File.WriteAllText(escaped, "{}")
            File.CreateSymbolicLink(Path.Combine(root, ".fsgg/knowledge/records/escape.json"), escaped) |> ignore
            let linkCode, linkOutput = run root ["check"]
            Assert.Equal(1, linkCode)
            Assert.Contains("knowledge.path", linkOutput))

    [<Fact>]
    let ``CLI aggregate budget accepts the exact ceiling and refuses one additional byte`` () =
        inTemp (fun root ->
            copyFixture root
            let store = Path.Combine(root, ".fsgg/knowledge")
            let bytes = Directory.GetFiles(store, "*", SearchOption.AllDirectories) |> Array.sumBy (fun p -> FileInfo(p).Length)
            let guidance = Path.Combine(store, "README.md")
            File.AppendAllText(guidance, String(' ', int (Knowledge.maxBytes - bytes)))
            let code, output = run root ["check"]
            Assert.Equal(0, code)
            use document = JsonDocument.Parse output
            Assert.Equal(Knowledge.maxBytes, document.RootElement.GetProperty("totalBytes").GetInt64())
            File.AppendAllText(guidance, " ")
            let refused, diagnostics = run root ["check"]
            Assert.Equal(1, refused)
            Assert.Contains("knowledge.budget", diagnostics)
            Assert.Contains("10485761", diagnostics)
            Assert.Contains("remaining -1", diagnostics))

    [<Fact>]
    let ``Knowledge help advertises read verbs without creating a missing store`` () =
        inTemp (fun root ->
            let code, output = run root ["--help"]
            Assert.Equal(0, code)
            Assert.Contains("check|search|get|related", output)
            Assert.Empty(Directory.GetFileSystemEntries root))
