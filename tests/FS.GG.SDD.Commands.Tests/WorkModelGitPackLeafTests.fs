namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitPackLeafTests =
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
        if commit then
            File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A0")
            File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B0")
            git root [ "add"; "-A" ] |> ignore
            git root [ "-c"; "user.name=SDD Test"; "-c"; "user.email=sdd-test@example.invalid"
                       "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; "seed" ] |> ignore
            git root [ "repack"; "-ad" ] |> ignore
        root

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-pack-leaf-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outer |> ignore
            try
                let rootA = initialize outer "a" false
                let rootB = initialize outer "b" true
                let oidB = git rootB [ "rev-parse"; "HEAD" ]
                let packB = Path.Combine(rootB, ".git/objects/pack")
                let packedFiles = Directory.GetFiles packB |> Array.toList
                Assert.Contains(packedFiles, fun path -> path.EndsWith(".pack", StringComparison.Ordinal))
                Assert.Contains(packedFiles, fun path -> path.EndsWith(".idx", StringComparison.Ordinal))
                action rootA rootB oidB packedFiles
            finally Directory.Delete(outer, true)

    let private leafName (source: string) =
        source.Substring(source.LastIndexOf(Path.DirectorySeparatorChar) + 1)

    let private redirect rootA (packedFiles: string list) =
        let packA = Path.Combine(rootA, ".git/objects/pack")
        Directory.CreateDirectory packA |> ignore
        for source in packedFiles do
            File.CreateSymbolicLink(Path.Combine(packA, leafName source), source) |> ignore

    let private restore rootA (packedFiles: string list) =
        let packA = Path.Combine(rootA, ".git/objects/pack")
        for source in packedFiles do
            File.Delete(Path.Combine(packA, leafName source))

    [<Fact>]
    let ``preexisting pack-file symlinks refuse foreign commit`` () =
        fixture (fun rootA _ oidB packedFiles ->
            redirect rootA packedFiles
            Assert.Equal(Error PackFileRedirect, captureCoreConfig rootA oidB))

    [<Fact>]
    let ``pack-file redirect after initial checks refuses`` () =
        fixture (fun rootA _ oidB packedFiles ->
            let switch () = redirect rootA packedFiles
            Assert.Equal(Error PackFileRedirect, captureCoreConfigWithHooks switch ignore rootA oidB))

    [<Fact>]
    let ``pack-file redirect then restore remains ABA`` () =
        fixture (fun rootA _ oidB packedFiles ->
            let switch () = redirect rootA packedFiles
            let switchBack () = restore rootA packedFiles
            match captureCoreConfigWithHooks switch switchBack rootA oidB with
            | Ok observation -> Assert.Equal(oidB, observation.CommitId)
            | Error reason -> failwithf "Pack leaf ABA characterization refused: %A" reason)

    [<Fact>]
    let ``ordinary packed objects pass and foreign objects remain absent`` () =
        fixture (fun rootA rootB oidB _ ->
            Assert.True(captureCoreConfig rootB oidB |> Result.isOk)
            Assert.True(captureCoreConfig rootA oidB |> Result.isError))

    [<Fact>]
    let ``pack-file inventory has a provisional count limit`` () =
        fixture (fun rootA _ oidB _ ->
            let packA = Path.Combine(rootA, ".git/objects/pack")
            Directory.CreateDirectory packA |> ignore
            for index in 0 .. 4096 do
                use file = File.Create(Path.Combine(packA, "unused-" + index.ToString("x4")))
                ()
            Assert.Equal(Error PackFileLimit, captureCoreConfig rootA oidB))
