namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open Xunit

[<Collection("ProcessGlobalEnv")>]
module TypedSddCommandTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    let private configuration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then "Release" else "Debug"

    let private apphost =
        Path.Combine(Commands.repoRoot, "src", "FS.GG.SDD.Cli", "bin", configuration, "net10.0", "FS.GG.SDD.Cli")

    let private run root args =
        let start = ProcessStartInfo(apphost)
        start.WorkingDirectory <- root
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        start.UseShellExecute <- false
        args |> List.iter start.ArgumentList.Add
        use child = Process.Start start |> Option.ofObj |> Option.defaultWith (fun () -> failwith "CLI did not start")
        let stdout = child.StandardOutput.ReadToEnd()
        let stderr = child.StandardError.ReadToEnd()
        Assert.True(child.WaitForExit 30000, "CLI timed out")
        child.ExitCode, stdout, stderr

    let private inTemp body =
        let root = Path.Combine(Path.GetTempPath(), "fsgg-typed-sdd-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try body root finally Directory.Delete(root, true)

    [<Fact>]
    let ``author requires an agent receipt and writes nothing when unavailable`` () =
        inTemp (fun root ->
            let code, stdout, _ = run root [ "typed-sdd"; "author"; "--root"; root; "--work"; "demo" ]
            Assert.Equal(1, code)
            Assert.Contains("typedSdd.authoringAgentUnavailable", stdout)
            Assert.False(Directory.Exists(Path.Combine(root, "work"))))

    [<Fact>]
    let ``author inspect and direct-edit diagnostic form one stable authority flow`` () =
        inTemp (fun root ->
            let code, _, _ = run root [ "typed-sdd"; "author"; "--root"; root; "--work"; "demo"; "--title"; "Demo"; "--agent"; "tern"; "--session"; "s1" ]
            Assert.Equal(0, code)
            let inspectCode, inspect, _ = run root [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo" ]
            Assert.Equal(0, inspectCode)
            Assert.Contains("\"outcome\": \"succeeded\"", inspect)
            let source = Path.Combine(root, "work", "demo", "specification.fsx")
            File.AppendAllText(source, "\n// direct edit\n")
            let editCode, edited, _ = run root [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo" ]
            Assert.Equal(1, editCode)
            Assert.Contains("typedSdd.directCanonicalEdit", edited))

    [<Fact>]
    let ``migration analysis classifies supported input and performs no preaccept write`` () =
        inTemp (fun root ->
            let target = Path.Combine(root, "work", "demo")
            Directory.CreateDirectory target |> ignore
            let source = Path.Combine(target, "spec.md")
            File.Copy(Path.Combine(Commands.repoRoot, "tests", "fixtures", "typed-specifications", "supported-spec.md"), source)
            let before = File.ReadAllBytes source
            let code, stdout, _ = run root [ "typed-sdd"; "migrate"; "--root"; root; "--work"; "demo"; "--source"; "work/demo/spec.md" ]
            Assert.Equal(0, code)
            Assert.Contains("\"classification\": \"Migrated\"", stdout)
            Assert.Equal<byte>(before, File.ReadAllBytes source)
            Assert.Single(Directory.GetFiles(target)) |> ignore)
