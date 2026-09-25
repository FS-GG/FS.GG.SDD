namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitCommitIntegrityTests =
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

    let private commit root message =
        git root [ "add"; "-A" ] |> ignore
        git root [ "-c"; "user.name=SDD Test"; "-c"; "user.email=sdd-test@example.invalid"
                   "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; message ] |> ignore
        git root [ "rev-parse"; "HEAD" ]

    let private fixture sha256 action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-git-commit-id-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        try
            let initArgs = if sha256 then [ "init"; "-q"; "--object-format=sha256" ] else [ "init"; "-q" ]
            git root initArgs |> ignore
            File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A0")
            File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B0")
            let first = commit root "first"
            action root first
        finally Directory.Delete(root, true)

    let private objectPath root (oid: string) =
        Path.Combine(root, ".git/objects", oid.Substring(0, 2), oid.Substring(2))

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``clean selected commit objects pass`` sha256 =
        if OperatingSystem.IsLinux() then
            fixture sha256 (fun root commitId ->
                Assert.True(captureCoreConfig root commitId |> Result.isOk))

    [<Theory>]
    [<InlineData(false)>]
    [<InlineData(true)>]
    let ``foreign commit payload under selected ID refuses before tree selection`` sha256 =
        if OperatingSystem.IsLinux() then
            fixture sha256 (fun root oldId ->
                File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A1")
                File.WriteAllText(Path.Combine(root, ".fsgg/sdd.yml"), "B1")
                let newId = commit root "second"
                let newPayload = git root [ "cat-file"; "commit"; newId ]
                let destination = objectPath root oldId
                File.SetUnixFileMode(destination, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
                File.Copy(objectPath root newId, destination, true)
                // cat-file accepts the valid foreign commit body under the old
                // ID; ls-tree currently catches this later with GitFailure.
                Assert.Equal(newPayload, git root [ "cat-file"; "commit"; oldId ])
                Assert.Equal(Error CommitIdMismatch, captureCoreConfig root oldId))

    [<Fact>]
    let ``selected commit body above provisional cap refuses`` () =
        if OperatingSystem.IsLinux() then
            fixture false (fun root _ ->
                File.WriteAllText(Path.Combine(root, ".fsgg/project.yml"), "A1")
                let messageFile = Path.Combine(root, "large-message.txt")
                File.WriteAllText(messageFile, String.replicate (1024 * 1024 + 1) "x")
                git root [ "add"; ".fsgg/project.yml" ] |> ignore
                git root [ "-c"; "user.name=SDD Test"; "-c"; "user.email=sdd-test@example.invalid"
                           "-c"; "core.hooksPath=/dev/null"; "commit"; "-q"; "-F"; messageFile ] |> ignore
                let largeId = git root [ "rev-parse"; "HEAD" ]
                Assert.Equal(Error CommitTooLarge, captureCoreConfig root largeId))
