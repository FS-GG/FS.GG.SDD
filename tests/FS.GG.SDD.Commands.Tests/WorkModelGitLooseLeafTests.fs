namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitLooseLeafTests =
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
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-loose-leaf-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outer |> ignore
            try
                let rootA = initialize outer "a" false
                let rootB = initialize outer "b" true
                let oidB = git rootB [ "rev-parse"; "HEAD" ]
                let objectList = git rootB [ "rev-list"; "--objects"; "--all" ]
                let objects =
                    objectList.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun line -> line.Split(' ')[0])
                    |> Array.toList
                action rootA rootB oidB objects
            finally Directory.Delete(outer, true)

    let private objectPath root (oid: string) =
        Path.Combine(root, ".git/objects", oid.Substring(0, 2), oid.Substring(2))

    let private redirect rootA rootB (objects: string list) =
        for oid in objects do
            Directory.CreateDirectory(Path.Combine(rootA, ".git/objects", oid.Substring(0, 2))) |> ignore
            File.CreateSymbolicLink(objectPath rootA oid, objectPath rootB oid) |> ignore

    let private restore rootA (objects: string list) =
        for oid in objects do File.Delete(objectPath rootA oid)

    [<Fact>]
    let ``preexisting loose-object leaf symlinks refuse foreign commit`` () =
        fixture (fun rootA rootB oidB objects ->
            redirect rootA rootB objects
            Assert.Equal(Error LooseObjectLeafRedirect, captureCoreConfig rootA oidB))

    [<Fact>]
    let ``loose-object leaf redirect after initial checks refuses`` () =
        fixture (fun rootA rootB oidB objects ->
            let switch () = redirect rootA rootB objects
            Assert.Equal(Error LooseObjectLeafRedirect, captureCoreConfigWithHooks switch ignore rootA oidB))

    [<Fact>]
    let ``loose-object leaf redirect then restore remains ABA`` () =
        fixture (fun rootA rootB oidB objects ->
            let switch () = redirect rootA rootB objects
            let switchBack () = restore rootA objects
            match captureCoreConfigWithHooks switch switchBack rootA oidB with
            | Ok observation -> Assert.Equal(oidB, observation.CommitId)
            | Error reason -> failwithf "Loose leaf ABA characterization refused: %A" reason)

    [<Fact>]
    let ``ordinary loose objects pass and foreign objects remain absent`` () =
        fixture (fun rootA rootB oidB _ ->
            Assert.True(captureCoreConfig rootB oidB |> Result.isOk)
            Assert.True(captureCoreConfig rootA oidB |> Result.isError))

    [<Fact>]
    let ``loose-object leaf inventory has a provisional count limit`` () =
        fixture (fun rootA _ oidB _ ->
            let fanout = Path.Combine(rootA, ".git/objects/00")
            Directory.CreateDirectory fanout |> ignore
            for index in 0 .. 4096 do
                use file = File.Create(Path.Combine(fanout, index.ToString("x38")))
                ()
            Assert.Equal(Error LooseObjectLeafLimit, captureCoreConfig rootA oidB))
