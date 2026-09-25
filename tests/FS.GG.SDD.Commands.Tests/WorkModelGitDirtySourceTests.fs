namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.WorkModelGitDirtySourcePreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitDirtySourceTests =
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
            let root = Path.Combine(Path.GetTempPath(), "sdd-git-dirty-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            try
                git root [ "init"; "-q" ] |> ignore
                git root [ "config"; "user.name"; "SDD Test" ] |> ignore
                git root [ "config"; "user.email"; "sdd-test@example.invalid" ] |> ignore
                File.WriteAllText(Path.Combine(root, project), "A0")
                File.WriteAllText(Path.Combine(root, sdd), "B0")
                git root [ "add"; "-A" ] |> ignore
                git root [ "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; "seed" ] |> ignore
                action root (git root [ "rev-parse"; "HEAD" ])
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``clean selected working files match the commit bytes`` () =
        fixture (fun root oid ->
            match verifyCoreBytesObserved root oid with
            | Ok(WorktreeBytesMatchedCommitObserved observation) ->
                Assert.Equal(oid, observation.CommitId)
            | Error reason -> failwithf "Clean core config refused: %A" reason)

    [<Fact>]
    let ``dirty project bytes refuse despite unchanged commit blobs`` () =
        fixture (fun root oid ->
            File.WriteAllText(Path.Combine(root, project), "A1")
            Assert.True(WorkModelGitCommitCustodyPreview.captureCoreConfig root oid |> Result.isOk)
            Assert.Equal(Error(DirtySource project), verifyCoreBytesObserved root oid))

    [<Fact>]
    let ``dirty sdd bytes refuse despite unchanged commit blobs`` () =
        fixture (fun root oid ->
            File.WriteAllText(Path.Combine(root, sdd), "B1")
            Assert.True(WorkModelGitCommitCustodyPreview.captureCoreConfig root oid |> Result.isOk)
            Assert.Equal(Error(DirtySource sdd), verifyCoreBytesObserved root oid))

    [<Fact>]
    let ``working-tree symlink refuses even when it resolves to expected bytes`` () =
        fixture (fun root oid ->
            File.Delete(Path.Combine(root, sdd))
            let externalBytes = Path.Combine(root, "external.yml")
            File.WriteAllText(externalBytes, "B0")
            File.CreateSymbolicLink(Path.Combine(root, sdd), externalBytes) |> ignore
            Assert.Equal(
                Error(PhysicalRefused(GenerationSourceSnapshot.Symlink sdd)),
                verifyCoreBytesObserved root oid))

    [<Fact>]
    let ``unselected untracked file is outside this two-path observation`` () =
        fixture (fun root oid ->
            File.WriteAllText(Path.Combine(root, ".fsgg/other.yml"), "extra")
            Assert.True(verifyCoreBytesObserved root oid |> Result.isOk))

    [<Fact>]
    let ``mode-only change remains outside the byte observation`` () =
        fixture (fun root oid ->
            let path = Path.Combine(root, project)
            let original = File.GetUnixFileMode path
            File.SetUnixFileMode(path, original ||| UnixFileMode.UserExecute)
            Assert.True(verifyCoreBytesObserved root oid |> Result.isOk))
