namespace FS.GG.SDD.Knowledge.Tests

open System
open System.IO
open System.Text
open System.Text.Json
open System.Diagnostics
open Xunit
open FS.GG.SDD.Knowledge

[<Collection("ProcessGlobalEnv")>]
module StoreTests =
    let private temporary test =
        let root = Path.Combine(Path.GetTempPath(), "knowledge-test-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try test root finally Directory.Delete(root, true)
    let private finding id = { Workspace.initialRecord with Id = id; Kind = "experiment"; Title = "Failed experiment"; Summary = "The experiment failed because the assumption was invalid."; State = "observed"; Relations = [|{ Kind = "bug-fix"; Target = "fix" }|] }
    let private query text = { Text = text; Kind = ""; Scope = ""; State = ""; Id = "" }
    let private git root args =
        let start = ProcessStartInfo("git", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        start.ArgumentList.Add "-C"
        start.ArgumentList.Add root
        for arg: string in args do start.ArgumentList.Add arg
        use child = match Process.Start start with null -> failwith "git unavailable" | value -> value
        let output = child.StandardOutput.ReadToEndAsync()
        let error = child.StandardError.ReadToEndAsync()
        child.WaitForExit()
        if child.ExitCode <> 0 then failwith error.Result
        output.Result.Trim()
    let private commit root message =
        git root ["add";"."] |> ignore
        git root ["-c";"user.name=Knowledge Test";"-c";"user.email=knowledge@example.invalid";"commit";"-m";message] |> ignore
        git root ["rev-parse";"HEAD"]

    [<Fact>]
    let ``concise findings search relations and current export roundtrip`` () = temporary (fun root ->
        let store = Path.Combine(root, "store")
        let first, _ = Store.capture store None (finding "failed")
        let correction = { first.Record with Summary = "The correction passed; the failed outcome remains linked in Git history."; State = "accepted" }
        let current, _ = Store.capture store (Some first.Revision) correction
        Assert.Equal(current, Store.get store "failed")
        Assert.Single(Store.search store (query "correction passed")) |> ignore
        Assert.Single(Store.related store "fix") |> ignore
        let bytes = Store.export store [|"failed"|]
        let restored = Path.Combine(root, "restored")
        Store.restore restored bytes |> ignore
        Assert.Equal<byte>(bytes, Store.export restored [|"failed"|])
        Assert.Equal(current, Store.get restored "failed")
        Assert.False((JsonSerializer.Deserialize<Export>(bytes) |> nonNull).HistoryIncluded))

    [<Fact>]
    let ``optimistic conflicting writers refuse and idempotent restore preserves edits`` () = temporary (fun root ->
        let first, _ = Store.capture root None (finding "record")
        let archive = Store.export root [|"record"|]
        Store.restore root archive |> ignore
        Assert.Equal(first, Store.capture root None (finding "record") |> fst)
        Assert.Throws<InvalidDataException>(fun () -> Store.capture root None { finding "record" with Summary = "Divergent finding" } |> ignore) |> ignore
        let revised = { first.Record with Summary = "User authored correction." }
        Store.capture root (Some first.Revision) revised |> ignore
        Assert.Throws<InvalidDataException>(fun () -> Store.capture root (Some first.Revision) (finding "record") |> ignore) |> ignore
        Assert.Throws<InvalidDataException>(fun () -> Store.restore root archive |> ignore) |> ignore
        Assert.Equal(revised, (Store.get root "record").Record))

    [<Fact>]
    let ``source logs indexes unsafe members and unknown schema fields refuse`` () = temporary (fun root ->
        Store.capture root None (finding "record") |> ignore
        File.WriteAllText(Path.Combine(root, "raw.log"), "raw log")
        Assert.Throws<InvalidDataException>(fun () -> Store.check root |> ignore) |> ignore
        File.Delete(Path.Combine(root, "raw.log"))
        Assert.Throws<InvalidDataException>(fun () -> Store.capture root None { finding "source" with Kind = "source" } |> ignore) |> ignore
        Assert.Throws<InvalidDataException>(fun () -> Store.capture root None { finding "dump" with Summary = "```code```" } |> ignore) |> ignore
        Assert.Throws<InvalidDataException>(fun () -> Store.capture root None { finding "claim" with Basis = "inference" } |> ignore) |> ignore
        let json = JsonSerializer.Serialize(finding "record").TrimEnd('}') + ",\"RawBody\":\"source\"}"
        File.WriteAllText(Path.Combine(root, "records", "record.json"), json)
        Assert.Throws<JsonException>(fun () -> Store.check root |> ignore) |> ignore)

    [<Fact>]
    let ``exact aggregate budget is accepted and growth above it refuses`` () = temporary (fun root ->
        Store.capture root None (finding "record") |> ignore
        let schema = Path.Combine(root, "schema.json")
        let size = (Store.check root).Bytes
        use stream = new FileStream(schema, FileMode.Append, FileAccess.Write)
        let padding = Array.create (int (Store.byteLimit - size)) (byte ' ')
        stream.Write(padding, 0, padding.Length)
        stream.Dispose()
        Assert.Equal(Store.byteLimit, (Store.check root).Bytes)
        Assert.Throws<InvalidDataException>(fun () -> Store.capture root None (finding "another") |> ignore) |> ignore
        File.AppendAllText(schema, " ")
        Assert.Throws<InvalidDataException>(fun () -> Store.check root |> ignore) |> ignore)

    [<Fact>]
    let ``actual Git versions clone retrieval and ignored caches are independent`` () = temporary (fun root ->
        git root ["init"] |> ignore
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".fsgg/\n")
        Workspace.initialize root |> ignore
        // Authored later rules must not defeat an earlier generated exception block.
        File.AppendAllText(Path.Combine(root, ".gitignore"), "\n.fsgg/**\n")
        let excludes = Path.Combine(root, "global-excludes")
        File.WriteAllText(excludes, ".fsgg/**\n")
        git root ["config";"core.excludesFile";excludes] |> ignore
        Workspace.initialize root |> ignore
        let effectiveIgnore = File.ReadAllText(Path.Combine(root, ".gitignore"))
        Workspace.initialize root |> ignore
        Assert.Equal(effectiveIgnore, File.ReadAllText(Path.Combine(root, ".gitignore")))
        Assert.True(effectiveIgnore.EndsWith(Workspace.ignoreBlock, StringComparison.Ordinal))
        let store = Path.Combine(root, ".fsgg", "knowledge")
        let first, _ = Store.capture store None (finding "failed")
        let initial = commit root "Initial knowledge"
        Assert.Contains(".fsgg/knowledge/records/failed.json", git root ["ls-files"])
        Assert.Contains(".fsgg/knowledge-guide.md", git root ["ls-files"])
        Store.capture store (Some first.Revision) { first.Record with Summary = "The corrected experiment passed." } |> ignore
        commit root "Correct experiment" |> ignore
        let history = Store.history store "failed"
        Assert.Equal(2, history.Length)
        Assert.Equal(first.Record, (Store.getVersion store "failed" initial).Record)
        Directory.CreateDirectory(Path.Combine(root, ".fsgg", "cache")) |> ignore
        File.WriteAllText(Path.Combine(root, ".fsgg", "cache", "index"), "disposable")
        Assert.DoesNotContain(".fsgg/cache/index", git root ["ls-files";"--others";"--exclude-standard"])
        let clone = Path.Combine(root, "clone")
        let bundle = Path.Combine(root, "history.bundle")
        git root ["bundle";"create";bundle;"--all"] |> ignore
        git root ["clone";bundle;clone] |> ignore
        Assert.Equal(2, (Store.history (Path.Combine(clone, ".fsgg", "knowledge")) "failed").Length)
        Assert.Single(Store.search (Path.Combine(clone, ".fsgg", "knowledge")) (query "corrected experiment")) |> ignore
        Assert.False(Directory.Exists(Path.Combine(clone, ".fsgg", "cache"))))

    [<Fact>]
    let ``initialization no clobber and private custody prevent shared export leaks`` () = temporary (fun root ->
        Workspace.initialize root |> ignore
        let store = Path.Combine(root, ".fsgg", "knowledge")
        let guide = Path.Combine(root, ".fsgg", "knowledge-guide.md")
        File.AppendAllText(guide, "\nUser guidance.\n")
        let prior = File.ReadAllBytes guide
        Workspace.initialize root |> ignore
        Assert.Equal<byte>(prior, File.ReadAllBytes guide)
        Assert.Single(Store.all store) |> ignore
        Store.capture (Path.Combine(root, "private")) None { finding "secret" with Title = "Synthetic private title"; Summary = "Synthetic private finding" } |> ignore
        Assert.Empty(Store.search store (query "Synthetic private"))
        Assert.DoesNotContain("Synthetic private", Encoding.UTF8.GetString(Store.export store [|"project-knowledge"|])))

    [<Fact>]
    let ``symlink canonical paths and malformed restore refuse without writes`` () = temporary (fun root ->
        let outside = Path.Combine(root, "outside")
        Directory.CreateDirectory outside |> ignore
        let store = Path.Combine(root, "store")
        Directory.CreateSymbolicLink(store, outside) |> ignore
        Assert.Throws<InvalidDataException>(fun () -> Store.capture store None (finding "unsafe") |> ignore) |> ignore
        let restored = Path.Combine(root, "restored")
        let attack = { Schema = "fsgg.knowledge-export/1"; HistoryIncluded = false; Files = [|{ Path = "../escape"; Digest = "bad"; Text = "bad" }|]; InventoryDigest = "bad" }
        Assert.Throws<InvalidDataException>(fun () -> Store.restore restored (JsonSerializer.SerializeToUtf8Bytes attack) |> ignore) |> ignore
        Assert.False(Directory.Exists restored))

    [<Fact>]
    let ``divergent Git edits remain explicit conflicts rather than timestamp winners`` () = temporary (fun root ->
        git root ["init"] |> ignore
        let store = Path.Combine(root, ".fsgg", "knowledge")
        let first, _ = Store.capture store None (finding "record")
        let original = commit root "Initial finding"
        git root ["checkout";"-b";"left"] |> ignore
        Store.capture store (Some first.Revision) { first.Record with Summary = "Left proposed correction" } |> ignore
        commit root "Left correction" |> ignore
        git root ["checkout";"-b";"right";original] |> ignore
        Store.capture store (Some first.Revision) { first.Record with Summary = "Right proposed correction" } |> ignore
        commit root "Right correction" |> ignore
        Assert.ThrowsAny<Exception>(fun () -> git root ["-c";"user.name=Test";"-c";"user.email=test@example.invalid";"merge";"left"] |> ignore) |> ignore
        Assert.Contains("UU", git root ["status";"--porcelain"])
        Assert.Throws<JsonException>(fun () -> Store.get store "record" |> ignore) |> ignore)

    [<Fact>]
    let ``failed atomic publication preserves prior record and retry recovers`` () = temporary (fun root ->
        if not (OperatingSystem.IsWindows()) then
            let store = Path.Combine(root, "store")
            let first, _ = Store.capture store None (finding "record")
            let records = Path.Combine(store, "records")
            let mode = File.GetUnixFileMode records
            try
                File.SetUnixFileMode(records, UnixFileMode.UserRead ||| UnixFileMode.UserExecute)
                let failure = Assert.ThrowsAny<Exception>(fun () -> Store.capture store (Some first.Revision) { first.Record with Summary = "Candidate update" } |> ignore)
                Assert.True(failure :? IOException || failure :? UnauthorizedAccessException)
                Assert.Equal(first, Store.get store "record")
            finally File.SetUnixFileMode(records, mode)
            let recovered, _ = Store.capture store (Some first.Revision) { first.Record with Summary = "Candidate update" }
            Assert.Equal("Candidate update", recovered.Record.Summary)
            Assert.Empty(Directory.EnumerateFiles(root, ".knowledge-write-*")))
