namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.WorkModelGitModePreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitModePreviewTests =
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

    let private fixture projectExecutable action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-git-mode-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
            try
                git root [ "init"; "-q" ] |> ignore
                git root [ "config"; "user.name"; "SDD Test" ] |> ignore
                git root [ "config"; "user.email"; "sdd-test@example.invalid" ] |> ignore
                let projectPath = Path.Combine(root, project)
                File.WriteAllText(projectPath, "A0")
                File.WriteAllText(Path.Combine(root, sdd), "B0")
                if projectExecutable then
                    File.SetUnixFileMode(projectPath, File.GetUnixFileMode(projectPath) ||| UnixFileMode.UserExecute)
                git root [ "add"; "-A" ] |> ignore
                git root [ "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; "seed" ] |> ignore
                action root (git root [ "rev-parse"; "HEAD" ])
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``mode added to committed nonexecutable file refuses`` () =
        fixture false (fun root oid ->
            let path = Path.Combine(root, project)
            File.SetUnixFileMode(path, File.GetUnixFileMode(path) ||| UnixFileMode.UserExecute)
            Assert.True(WorkModelGitDirtySourcePreview.verifyCoreBytesObserved root oid |> Result.isOk)
            Assert.Equal(Error(ModeMismatch project), verifyCoreBytesAndGitModeObserved root oid))

    [<Fact>]
    let ``mode removed from committed executable file refuses`` () =
        fixture true (fun root oid ->
            let path = Path.Combine(root, project)
            File.SetUnixFileMode(path, File.GetUnixFileMode(path) &&& ~~~UnixFileMode.UserExecute)
            Assert.True(WorkModelGitDirtySourcePreview.verifyCoreBytesObserved root oid |> Result.isOk)
            Assert.Equal(Error(ModeMismatch project), verifyCoreBytesAndGitModeObserved root oid))

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``unchanged committed executable policy passes`` executable =
        fixture executable (fun root oid ->
            Assert.True(verifyCoreBytesAndGitModeObserved root oid |> Result.isOk))

    [<Fact>]
    let ``byte change still refuses`` () =
        fixture false (fun root oid ->
            File.WriteAllText(Path.Combine(root, sdd), "B1")
            Assert.Equal(Error(DirtySource sdd), verifyCoreBytesAndGitModeObserved root oid))

    [<Fact>]
    let ``symlink still refuses`` () =
        fixture false (fun root oid ->
            File.Delete(Path.Combine(root, sdd))
            File.CreateSymbolicLink(Path.Combine(root, sdd), Path.Combine(root, project)) |> ignore
            Assert.Equal(
                Error(PhysicalRefused(GenerationSourceSnapshot.Symlink sdd)),
                verifyCoreBytesAndGitModeObserved root oid))

    [<Fact>]
    let ``opened descriptor mode change during read refuses`` () =
        fixture false (fun root _ ->
            let path = Path.Combine(root, project)
            let afterRead _ =
                File.SetUnixFileMode(path, File.GetUnixFileMode(path) ||| UnixFileMode.UserExecute)
            Assert.Equal(
                Error(GenerationSourceSnapshot.FileUnstable project),
                GenerationSourceSnapshot.captureSelectedFileWithHooks ignore ignore afterRead root project))
