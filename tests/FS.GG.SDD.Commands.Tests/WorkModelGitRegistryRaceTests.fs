namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open System.Text
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitRegistryRaceTests =
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

    let private initialize outer name message =
        let root = Path.Combine(outer, name)
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        git root [ "init"; "-q" ] |> ignore
        git root [ "config"; "user.name"; "SDD Test" ] |> ignore
        git root [ "config"; "user.email"; "sdd-test@example.invalid" ] |> ignore
        File.WriteAllText(Path.Combine(root, project), "A0")
        File.WriteAllText(Path.Combine(root, sdd), "B0")
        git root [ "add"; "-A" ] |> ignore
        git root [ "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; message ] |> ignore
        root, git root [ "rev-parse"; "HEAD" ]

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-registry-race-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outer |> ignore
            try
                let rootA, oidA = initialize outer "a" "commit-a"
                let rootB, oidB = initialize outer "b" "commit-b"
                Assert.NotEqual<string>(oidA, oidB)
                action rootA rootB oidA oidB
            finally Directory.Delete(outer, true)

    let private swapToForeign rootA rootB =
        Directory.Move(Path.Combine(rootA, ".git"), Path.Combine(rootA, ".git-held"))
        Directory.CreateSymbolicLink(Path.Combine(rootA, ".git"), Path.Combine(rootB, ".git")) |> ignore

    let private restoreOwned rootA =
        Directory.Delete(Path.Combine(rootA, ".git"))
        Directory.Move(Path.Combine(rootA, ".git-held"), Path.Combine(rootA, ".git"))

    [<Fact>]
    let ``persistent git-directory swap after registration refuses foreign commit`` () =
        fixture (fun rootA rootB _ oidB ->
            let switch () = swapToForeign rootA rootB
            Assert.Equal(
                Error RepositoryChanged,
                captureCoreConfigWithHooks switch ignore rootA oidB))

    [<Fact>]
    let ``restored git-directory ABA remains an observed false green`` () =
        fixture (fun rootA rootB _ oidB ->
            let switch () = swapToForeign rootA rootB
            let restore () = restoreOwned rootA
            match captureCoreConfigWithHooks switch restore rootA oidB with
            | Ok observation ->
                Assert.Equal(oidB, observation.CommitId)
                let projectBytes = observation.Files |> List.find (fun file -> file.Path = project) |> _.Bytes
                Assert.Equal("A0", Encoding.UTF8.GetString projectBytes)
            | Error reason -> failwithf "ABA characterization refused unexpectedly: %A" reason)

    [<Fact>]
    let ``stable registered repository still reads its selected commit`` () =
        fixture (fun rootA _ oidA _ ->
            Assert.True(captureCoreConfig rootA oidA |> Result.isOk))
