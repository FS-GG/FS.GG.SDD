namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitCommitCustodyTests =
    let private project = ".fsgg/project.yml"
    let private sdd = ".fsgg/sdd.yml"

    let private git root args =
        let start = ProcessStartInfo("git")
        start.WorkingDirectory <- root
        start.UseShellExecute <- false
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        start.Environment.Remove "GIT_NO_REPLACE_OBJECTS" |> ignore
        args |> List.iter start.ArgumentList.Add
        use proc =
            Process.Start start
            |> Option.ofObj
            |> Option.defaultWith (fun () -> failwith "Could not start disposable git fixture")
        let output = proc.StandardOutput.ReadToEnd()
        let error = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        if proc.ExitCode <> 0 then failwithf "Disposable git fixture failed: %s" error
        output.Trim()

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-git-custody-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            try
                git root [ "init"; "-q" ] |> ignore
                git root [ "config"; "user.name"; "SDD Test" ] |> ignore
                git root [ "config"; "user.email"; "sdd-test@example.invalid" ] |> ignore
                action root
            finally Directory.Delete(root, true)

    let private commit root message =
        git root [ "add"; "-A" ] |> ignore
        git root [ "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; message ] |> ignore
        git root [ "rev-parse"; "HEAD" ]

    let private bytes path (observation: Observation) =
        observation.Files |> List.find (fun file -> file.Path = path) |> _.Bytes

    [<Fact>]
    let ``full commit pins both config blobs while working tree and HEAD move`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, project), "A0")
            File.WriteAllText(Path.Combine(root, sdd), "B0")
            let first = commit root "first"
            File.WriteAllText(Path.Combine(root, project), "A1")
            File.WriteAllText(Path.Combine(root, sdd), "B1")
            let second = commit root "second"
            File.WriteAllText(Path.Combine(root, project), "uncommitted")
            let capture oid =
                match captureCoreConfig root oid with
                | Ok result -> result
                | Error reason -> failwithf "Commit observation refused: %A" reason
            let earlier = capture first
            let later = capture second
            Assert.Equal(first, earlier.CommitId)
            Assert.Equal("A0", Encoding.UTF8.GetString(bytes project earlier))
            Assert.Equal("B0", Encoding.UTF8.GetString(bytes sdd earlier))
            Assert.Equal("A1", Encoding.UTF8.GetString(bytes project later))
            Assert.Equal("B1", Encoding.UTF8.GetString(bytes sdd later))
            Assert.NotEqual<string>("A0/B1", Encoding.UTF8.GetString(bytes project earlier) + "/" + Encoding.UTF8.GetString(bytes sdd earlier))
            Assert.NotEqual<string>("A0/B1", Encoding.UTF8.GetString(bytes project later) + "/" + Encoding.UTF8.GetString(bytes sdd later))
            let source = earlier.Files |> List.find (fun file -> file.Path = project)
            Assert.Equal(SchemaVersion.sha256Bytes(Encoding.UTF8.GetBytes "A0"), source.RawSha256)
            let mutableCopy = source.Bytes
            mutableCopy.[0] <- 0uy
            Assert.Equal("A0", Encoding.UTF8.GetString(source.Bytes)))

    [<Fact>]
    let ``moving refs are not commit IDs and tree objects are refused`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, project), "A0")
            File.WriteAllText(Path.Combine(root, sdd), "B0")
            commit root "first" |> ignore
            Assert.Equal(Error InvalidCommitId, captureCoreConfig root "HEAD")
            let treeId = git root [ "rev-parse"; "HEAD^{tree}" ]
            Assert.Equal(Error NotCommit, captureCoreConfig root treeId))

    [<Fact>]
    let ``replace ref cannot redirect a pinned commit tree`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, project), "A0")
            File.WriteAllText(Path.Combine(root, sdd), "B0")
            let first = commit root "first"
            File.WriteAllText(Path.Combine(root, project), "A1")
            File.WriteAllText(Path.Combine(root, sdd), "B1")
            let second = commit root "second"
            git root [ "replace"; first; second ] |> ignore
            Assert.Equal("A1", git root [ "show"; first + ":" + project ])
            match captureCoreConfig root first with
            | Ok observation ->
                Assert.Equal("A0", Encoding.UTF8.GetString(bytes project observation))
                Assert.Equal("B0", Encoding.UTF8.GetString(bytes sdd observation))
            | Error reason -> failwithf "Pinned commit under replace ref refused: %A" reason)

    [<Fact>]
    let ``missing selected path and committed symlink refuse`` () =
        fixture (fun root ->
            File.WriteAllText(Path.Combine(root, project), "A0")
            let missing = commit root "missing-sdd"
            Assert.Equal(Error(MissingPath sdd), captureCoreConfig root missing)
            File.CreateSymbolicLink(Path.Combine(root, sdd), Path.Combine(root, project)) |> ignore
            let linked = commit root "linked-sdd"
            Assert.Equal(Error(NonRegularPath sdd), captureCoreConfig root linked))

    [<Fact>]
    let ``oversized committed config refuses before blob read`` () =
        fixture (fun root ->
            use file = File.Create(Path.Combine(root, project))
            file.SetLength(32L * 1024L * 1024L + 1L)
            file.Dispose()
            File.WriteAllText(Path.Combine(root, sdd), "B0")
            let oid = commit root "large-project"
            Assert.Equal(Error(BlobTooLarge project), captureCoreConfig root oid))
