namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitAlternateStoreTests =
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
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-alternate-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outer |> ignore
            try
                let rootA, oidA = initialize outer "a"
                let rootB, oidB = initialize outer "b"
                Assert.NotEqual<string>(oidA, oidB)
                action outer rootA rootB oidA oidB
            finally Directory.Delete(outer, true)

    let private alternatePath root = Path.Combine(root, ".git/objects/info/alternates")

    let private addAlternate rootA rootB =
        let path = alternatePath rootA
        Directory.CreateDirectory(Path.Combine(rootA, ".git/objects/info")) |> ignore
        File.WriteAllText(path, Path.Combine(rootB, ".git/objects") + "\n")

    [<Fact>]
    let ``preexisting alternate object store refuses foreign commit`` () =
        fixture (fun _ rootA rootB _ oidB ->
            addAlternate rootA rootB
            Assert.Equal(Error AlternateObjectStore, captureCoreConfig rootA oidB))

    [<Fact>]
    let ``alternate added after initial checks refuses after blob reads`` () =
        fixture (fun _ rootA rootB _ oidB ->
            let add () = addAlternate rootA rootB
            Assert.Equal(Error AlternateObjectStore, captureCoreConfigWithHooks add ignore rootA oidB))

    [<Fact>]
    let ``alternate added and removed during reads remains ABA`` () =
        fixture (fun _ rootA rootB _ oidB ->
            let add () = addAlternate rootA rootB
            let remove () = File.Delete(alternatePath rootA)
            match captureCoreConfigWithHooks add remove rootA oidB with
            | Ok observation -> Assert.Equal(oidB, observation.CommitId)
            | Error reason -> failwithf "Alternate ABA characterization refused: %A" reason)

    [<Fact>]
    let ``self-contained repository and linked worktree remain eligible`` () =
        fixture (fun outer rootA _ oidA _ ->
            Assert.True(captureCoreConfig rootA oidA |> Result.isOk)
            let linked = Path.Combine(outer, "linked")
            git rootA [ "worktree"; "add"; "--detach"; linked; oidA ] |> ignore
            Assert.True(captureCoreConfig linked oidA |> Result.isOk))

    [<Fact>]
    let ``empty alternate declaration still refuses provisional profile`` () =
        fixture (fun _ rootA _ oidA _ ->
            File.WriteAllText(alternatePath rootA, "")
            Assert.Equal(Error AlternateObjectStore, captureCoreConfig rootA oidA))

    [<Fact>]
    let ``linked worktree sees common object-store alternate declaration`` () =
        fixture (fun outer rootA rootB _ oidB ->
            let linked = Path.Combine(outer, "linked")
            let oidA = git rootA [ "rev-parse"; "HEAD" ]
            git rootA [ "worktree"; "add"; "--detach"; linked; oidA ] |> ignore
            addAlternate rootA rootB
            Assert.Equal(Error AlternateObjectStore, captureCoreConfig linked oidB))
