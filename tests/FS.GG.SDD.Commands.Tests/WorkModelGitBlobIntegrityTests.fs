namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open System.Text
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitBlobIntegrityTests =
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

    let private fixture sha256 action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-git-blob-id-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        try
            let initArgs = if sha256 then [ "init"; "-q"; "--object-format=sha256" ] else [ "init"; "-q" ]
            git root initArgs |> ignore
            File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A0")
            File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B0")
            git root [ "add"; "-A" ] |> ignore
            git root [ "-c"; "user.name=SDD Test"; "-c"; "user.email=sdd-test@example.invalid"
                       "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; "seed" ] |> ignore
            let commitId = git root [ "rev-parse"; "HEAD" ]
            action root commitId
        finally Directory.Delete(root, true)

    let private corruptNamedBlob root path (replacement: string) =
        let original = git root [ "rev-parse"; "HEAD:" + path ]
        let replacementFile = Path.Combine(root, "replacement.bin")
        File.WriteAllText(replacementFile, replacement)
        let other = git root [ "hash-object"; "-w"; replacementFile ]
        File.Delete replacementFile
        let objectPath (oid: string) = Path.Combine(root, ".git/objects", oid.Substring(0, 2), oid.Substring(2))
        let destination = objectPath original
        File.SetUnixFileMode(destination, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
        File.Copy(objectPath other, destination, true)
        // Git's ordinary read trusts the pathname after decompression. This
        // assertion proves the equal-length false green before preview refusal.
        Assert.Equal(replacement, git root [ "cat-file"; "blob"; original ])

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``clean selected Git blobs match their object IDs`` sha256 =
        if OperatingSystem.IsLinux() then
            fixture sha256 (fun root commitId ->
                let observation =
                    match captureCoreConfig root commitId with
                    | Ok result -> result
                    | Error reason -> failwithf "Clean commit refused: %A" reason
                Assert.Equal((if sha256 then 64 else 40), observation.CommitId.Length)
                Assert.Equal("A0", Encoding.UTF8.GetString(observation.Files.[0].Bytes))
                Assert.Equal("B0", Encoding.UTF8.GetString(observation.Files.[1].Bytes)))

    [<Theory>]
    [<InlineData(false, ".fsgg/project.yml", "A1")>]
    [<InlineData(false, ".fsgg/sdd.yml", "B1")>]
    [<InlineData(true, ".fsgg/project.yml", "A1")>]
    [<InlineData(true, ".fsgg/sdd.yml", "B1")>]
    let ``equal-length foreign payload under selected blob ID refuses`` sha256 path replacement =
        if OperatingSystem.IsLinux() then
            fixture sha256 (fun root commitId ->
                corruptNamedBlob root path replacement
                Assert.Equal(Error(BlobIdMismatch path), captureCoreConfig root commitId))
