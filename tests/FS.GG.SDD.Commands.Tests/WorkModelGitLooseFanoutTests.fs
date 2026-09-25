namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitLooseFanoutTests =
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

    let private initialize outer name commit =
        let root = Path.Combine(outer, name)
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        git root [ "init"; "-q" ] |> ignore
        File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A0")
        File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B0")
        if commit then
            git root [ "add"; "-A" ] |> ignore
            git root [ "-c"; "user.name=SDD Test"; "-c"; "user.email=sdd-test@example.invalid"
                       "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; "seed" ] |> ignore
        root

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-loose-link-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outer |> ignore
            try
                let rootA = initialize outer "a" false
                let rootB = initialize outer "b" true
                let oidB = git rootB [ "rev-parse"; "HEAD" ]
                let objectList = git rootB [ "rev-list"; "--objects"; "--all" ]
                let prefixes =
                    objectList.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun line -> line.Substring(0, 2))
                    |> Array.distinct
                    |> Array.toList
                action rootA rootB oidB prefixes
            finally Directory.Delete(outer, true)

    let private fanout root prefix = Path.Combine(root, ".git/objects", prefix)

    let private redirect rootA rootB prefixes =
        for prefix in prefixes do
            Directory.CreateSymbolicLink(fanout rootA prefix, fanout rootB prefix) |> ignore

    let private restore rootA prefixes =
        for prefix in prefixes do Directory.Delete(fanout rootA prefix)

    [<Fact>]
    let ``preexisting loose-object fanout symlinks refuse foreign commit`` () =
        fixture (fun rootA rootB oidB prefixes ->
            redirect rootA rootB prefixes
            Assert.Equal(Error LooseObjectDirectoryRedirect, captureCoreConfig rootA oidB))

    [<Fact>]
    let ``loose fanouts redirected after initial checks refuse`` () =
        fixture (fun rootA rootB oidB prefixes ->
            let switch () = redirect rootA rootB prefixes
            Assert.Equal(Error LooseObjectDirectoryRedirect, captureCoreConfigWithHooks switch ignore rootA oidB))

    [<Fact>]
    let ``loose fanouts redirected then restored remain ABA`` () =
        fixture (fun rootA rootB oidB prefixes ->
            let switch () = redirect rootA rootB prefixes
            let switchBack () = restore rootA prefixes
            match captureCoreConfigWithHooks switch switchBack rootA oidB with
            | Ok observation -> Assert.Equal(oidB, observation.CommitId)
            | Error reason -> failwithf "Loose fanout ABA characterization refused: %A" reason)

    [<Fact>]
    let ``ordinary loose objects still read and foreign objects remain absent`` () =
        fixture (fun rootA rootB oidB _ ->
            Assert.True(captureCoreConfig rootB oidB |> Result.isOk)
            Assert.True(captureCoreConfig rootA oidB |> Result.isError))
