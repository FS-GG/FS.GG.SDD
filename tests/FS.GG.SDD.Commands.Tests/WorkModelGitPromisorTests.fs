namespace FS.GG.SDD.Commands.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Commands.WorkModelGitCommitCustodyPreview
open Xunit

[<Collection("ProcessGlobalEnv")>]
module WorkModelGitPromisorTests =
    let private runGit root noLazy args =
        let start = ProcessStartInfo("git")
        start.WorkingDirectory <- root
        start.UseShellExecute <- false
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        if noLazy then start.Environment.["GIT_NO_LAZY_FETCH"] <- "1"
        args |> List.iter start.ArgumentList.Add
        use proc =
            Process.Start start
            |> Option.ofObj
            |> Option.defaultWith (fun () -> failwith "Could not start disposable git fixture")
        let output = proc.StandardOutput.ReadToEnd()
        let error = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        proc.ExitCode, output.Trim(), error

    let private git root args =
        let code, output, error = runGit root false args
        if code <> 0 then failwithf "Disposable git fixture failed: %s" error
        output

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let outer = Path.Combine(Path.GetTempPath(), "sdd-git-promisor-" + Guid.NewGuid().ToString("N"))
            let source = Path.Combine(outer, "source")
            Directory.CreateDirectory(Path.Combine(source, ".fsgg")) |> ignore
            try
                git source [ "init"; "-q" ] |> ignore
                git source [ "config"; "user.name"; "SDD Test" ] |> ignore
                git source [ "config"; "user.email"; "sdd-test@example.invalid" ] |> ignore
                git source [ "config"; "uploadpack.allowFilter"; "true" ] |> ignore
                File.WriteAllText(Path.Combine(source, ".fsgg/project.yml"), "A0")
                File.WriteAllText(Path.Combine(source, ".fsgg/sdd.yml"), "B0")
                git source [ "add"; "-A" ] |> ignore
                git source [ "-c"; "core.hooksPath=/dev/null"; "commit"; "-qm"; "seed" ] |> ignore
                let oid = git source [ "rev-parse"; "HEAD" ]
                let blob = git source [ "rev-parse"; "HEAD:.fsgg/project.yml" ]
                action outer source oid blob
            finally Directory.Delete(outer, true)

    let private isLocal root oid =
        let code, _, _ = runGit root true [ "cat-file"; "-e"; oid ]
        code = 0

    [<Fact>]
    let ``partial clone preview refuses missing blob without hydrating object store`` () =
        fixture (fun outer source oid blob ->
            let clone = Path.Combine(outer, "partial")
            git outer [ "clone"; "-q"; "--no-checkout"; "--filter=blob:none"; Uri(source).AbsoluteUri; clone ] |> ignore
            Assert.False(File.Exists(Path.Combine(clone, ".fsgg/project.yml")))
            Assert.False(isLocal clone blob)
            Assert.Equal(Error GitFailure, captureCoreConfig clone oid)
            Assert.False(isLocal clone blob))

    [<Fact>]
    let ``fully materialized clone still reads committed core blobs`` () =
        fixture (fun outer source oid blob ->
            let clone = Path.Combine(outer, "full")
            git outer [ "clone"; "-q"; Uri(source).AbsoluteUri; clone ] |> ignore
            Assert.True(isLocal clone blob)
            Assert.True(captureCoreConfig clone oid |> Result.isOk))
