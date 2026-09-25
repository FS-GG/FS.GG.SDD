namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open FS.GG.SDD.Commands.WorkModelGitModePreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitEntryCustodyTests =
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
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-entry-" + Guid.NewGuid().ToString("N"))
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

    let private copiedConfig root copy =
        Directory.CreateDirectory(Path.Combine(copy, ".fsgg")) |> ignore
        File.Copy(Path.Combine(root, project), Path.Combine(copy, project))
        File.Copy(Path.Combine(root, sdd), Path.Combine(copy, sdd))

    [<Fact>]
    let ``symlinked git directory cannot borrow sibling commit`` () =
        fixture (fun outer root oid ->
            let copy = Path.Combine(outer, "copy")
            copiedConfig root copy
            Directory.CreateSymbolicLink(Path.Combine(copy, ".git"), Path.Combine(root, ".git")) |> ignore
            Assert.Equal(Error UnregisteredWorktree, captureCoreConfig copy oid)
            Assert.Equal(Error(CommitRefused UnregisteredWorktree), verifyCoreBytesAndGitModeObserved copy oid))

    [<Fact>]
    let ``forged regular gitfile cannot borrow sibling commit`` () =
        fixture (fun outer root oid ->
            let copy = Path.Combine(outer, "copy")
            copiedConfig root copy
            File.WriteAllText(Path.Combine(copy, ".git"), "gitdir: " + Path.Combine(root, ".git") + "\n")
            Assert.Equal(Error UnregisteredWorktree, captureCoreConfig copy oid)
            Assert.Equal(Error(CommitRefused UnregisteredWorktree), verifyCoreBytesAndGitModeObserved copy oid))

    [<Fact>]
    let ``registered repository root remains eligible`` () =
        fixture (fun _ root oid ->
            Assert.True(captureCoreConfig root oid |> Result.isOk)
            Assert.True(verifyCoreBytesAndGitModeObserved root oid |> Result.isOk))

    [<Fact>]
    let ``registered linked worktree root remains eligible`` () =
        fixture (fun outer root oid ->
            let linked = Path.Combine(outer, "linked")
            git root [ "worktree"; "add"; "--detach"; linked; oid ] |> ignore
            Assert.True(captureCoreConfig linked oid |> Result.isOk)
            Assert.True(verifyCoreBytesAndGitModeObserved linked oid |> Result.isOk))
