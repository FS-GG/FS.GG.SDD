namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitObjectDirectoryTests =
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

    let private initialize outer name =
        let root = Path.Combine(outer, name)
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        git root [ "init"; "-q" ] |> ignore
        git root [ "config"; "user.name"; "SDD Test" ] |> ignore
        git root [ "config"; "user.email"; "sdd-test@example.invalid" ] |> ignore
        File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A0")
        File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B0")
        git root [ "add"; "-A" ] |> ignore
        git root [ "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; name ] |> ignore
        root, git root [ "rev-parse"; "HEAD" ]

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-object-link-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outer |> ignore
            try
                let rootA, oidA = initialize outer "a"
                let rootB, oidB = initialize outer "b"
                Assert.NotEqual<string>(oidA, oidB)
                action outer rootA rootB oidA oidB
            finally Directory.Delete(outer, true)

    let private objects root = Path.Combine(root, ".git/objects")

    let private redirect rootA rootB =
        Directory.Move(objects rootA, Path.Combine(rootA, ".git/objects-held"))
        Directory.CreateSymbolicLink(objects rootA, objects rootB) |> ignore

    let private restore rootA =
        Directory.Delete(objects rootA)
        Directory.Move(Path.Combine(rootA, ".git/objects-held"), objects rootA)

    [<Fact>]
    let ``preexisting object-directory symlink refuses foreign commit`` () =
        fixture (fun _ rootA rootB _ oidB ->
            redirect rootA rootB
            Assert.Equal(Error ObjectDirectoryRedirect, captureCoreConfig rootA oidB))

    [<Fact>]
    let ``object directory redirected after initial checks refuses`` () =
        fixture (fun _ rootA rootB _ oidB ->
            let switch () = redirect rootA rootB
            Assert.Equal(Error ObjectDirectoryRedirect, captureCoreConfigWithHooks switch ignore rootA oidB))

    [<Fact>]
    let ``object directory redirected then restored remains ABA`` () =
        fixture (fun _ rootA rootB _ oidB ->
            let switch () = redirect rootA rootB
            let switchBack () = restore rootA
            match captureCoreConfigWithHooks switch switchBack rootA oidB with
            | Ok observation -> Assert.Equal(oidB, observation.CommitId)
            | Error reason -> failwithf "Object-directory ABA characterization refused: %A" reason)

    [<Fact>]
    let ``ordinary and linked worktree object directories remain eligible`` () =
        fixture (fun outer rootA _ oidA _ ->
            Assert.True(captureCoreConfig rootA oidA |> Result.isOk)
            let linked = Path.Combine(outer, "linked")
            git rootA [ "worktree"; "add"; "--detach"; linked; oidA ] |> ignore
            Assert.True(captureCoreConfig linked oidA |> Result.isOk))
