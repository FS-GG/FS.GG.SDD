namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitTreeIntegrityTests =
    let private git root args =
        let start = ProcessStartInfo("git")
        start.WorkingDirectory <- root
        start.UseShellExecute <- false
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
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

    let private commit root message =
        git root [ "add"; "-A" ] |> ignore
        git root [ "-c"; "user.name=SDD Test"; "-c"; "user.email=sdd-test@example.invalid"
                   "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; message ] |> ignore
        git root [ "rev-parse"; "HEAD" ]

    let private fixture sha256 action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-git-tree-id-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        try
            git root (if sha256 then [ "init"; "-q"; "--object-format=sha256" ] else [ "init"; "-q" ]) |> ignore
            File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A0")
            File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B0")
            let first = commit root "first"
            action root first
        finally Directory.Delete(root, true)

    let private objectPath root (oid: string) =
        Path.Combine(root, ".git/objects", oid.Substring(0, 2), oid.Substring(2))

    let private substituteTree root oldId newId =
        let destination = objectPath root oldId
        File.SetUnixFileMode(destination, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
        File.Copy(objectPath root newId, destination, true)

    let private secondVersion root =
        File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A1")
        File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B1")
        commit root "second"

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``clean selected root and child trees pass`` sha256 =
        if OperatingSystem.IsLinux() then
            fixture sha256 (fun root commitId ->
                Assert.True(captureCoreConfig root commitId |> Result.isOk))

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``foreign child-tree payload under selected ID refuses`` sha256 =
        if OperatingSystem.IsLinux() then
            fixture sha256 (fun root oldCommit ->
                let oldChild = git root [ "rev-parse"; oldCommit + ":.fsgg" ]
                let newCommit = secondVersion root
                let newChild = git root [ "rev-parse"; newCommit + ":.fsgg" ]
                substituteTree root oldChild newChild
                // Git's selected-path traversal accepts the foreign tree and
                // returns a valid foreign blob ID under the old commit.
                let selected = git root [ "ls-tree"; "--full-tree"; oldCommit; "--"; ".fsgg/project.yml" ]
                Assert.Contains(git root [ "rev-parse"; newCommit + ":.fsgg/project.yml" ], selected)
                Assert.Equal(Error(TreeIdMismatch ".fsgg"), captureCoreConfig root oldCommit))

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``foreign root-tree payload under selected ID refuses`` sha256 =
        if OperatingSystem.IsLinux() then
            fixture sha256 (fun root oldCommit ->
                let oldRoot = git root [ "rev-parse"; oldCommit + "^{tree}" ]
                let newCommit = secondVersion root
                let newRoot = git root [ "rev-parse"; newCommit + "^{tree}" ]
                substituteTree root oldRoot newRoot
                Assert.Equal(Error(TreeIdMismatch "<root>"), captureCoreConfig root oldCommit))
