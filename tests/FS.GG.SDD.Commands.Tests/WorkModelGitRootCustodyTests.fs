namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open FS.GG.SDD.Commands.WorkModelGitModePreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitRootCustodyTests =
    let private project = ".fsgg/project.yml"
    let private sdd = ".fsgg/sdd.yml"

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

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-root-" + Guid.NewGuid().ToString("N"))
            let root = Path.Combine(outer, "repo")
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            try
                git root [ "init"; "-q" ] |> ignore
                git root [ "config"; "user.name"; "SDD Test" ] |> ignore
                git root [ "config"; "user.email"; "sdd-test@example.invalid" ] |> ignore
                File.WriteAllText(Path.Combine(root, project), "A0")
                File.WriteAllText(Path.Combine(root, sdd), "B0")
                git root [ "add"; "-A" ] |> ignore
                git root [ "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; "seed" ] |> ignore
                action outer root (git root [ "rev-parse"; "HEAD" ])
            finally Directory.Delete(outer, true)

    [<Fact>]
    let ``nested copied workspace cannot borrow parent Git commit`` () =
        fixture (fun _ root oid ->
            let nested = Path.Combine(root, "nested")
            Directory.CreateDirectory(Path.Combine(nested, ".fsgg")) |> ignore
            File.Copy(Path.Combine(root, project), Path.Combine(nested, project))
            File.Copy(Path.Combine(root, sdd), Path.Combine(nested, sdd))
            Assert.Equal(Error NotRepositoryRoot, captureCoreConfig nested oid)
            Assert.Equal(Error(CommitRefused NotRepositoryRoot), verifyCoreBytesAndGitModeObserved nested oid))

    [<Fact>]
    let ``actual repository root still observes core files`` () =
        fixture (fun _ root oid ->
            Assert.True(captureCoreConfig root oid |> Result.isOk)
            Assert.True(verifyCoreBytesAndGitModeObserved root oid |> Result.isOk))

    [<Fact>]
    let ``linked worktree root with gitfile remains eligible`` () =
        fixture (fun outer root oid ->
            let linked = Path.Combine(outer, "linked")
            git root [ "worktree"; "add"; "--detach"; linked; oid ] |> ignore
            Assert.True(File.Exists(Path.Combine(linked, ".git")))
            Assert.True(captureCoreConfig linked oid |> Result.isOk)
            Assert.True(verifyCoreBytesAndGitModeObserved linked oid |> Result.isOk))

    [<Fact>]
    let ``bare object store cannot pose as a working-tree source root`` () =
        fixture (fun outer root oid ->
            let bare = Path.Combine(outer, "bare.git")
            git root [ "clone"; "--bare"; root; bare ] |> ignore
            Assert.Equal(Error NotRepositoryRoot, captureCoreConfig bare oid))
