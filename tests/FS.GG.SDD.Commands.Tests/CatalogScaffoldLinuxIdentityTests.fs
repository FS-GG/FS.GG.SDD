namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Reflection
open System.Text
open System.Threading
open FS.GG.SDD.Commands
open Xunit

/// Opt-in schedules this test; the maintained namespace runner supplies teardown.
type CatalogNamespaceFactAttribute() as this =
    inherit FactAttribute()

    do
        if Environment.GetEnvironmentVariable("FSGG_SDD_NAMESPACE_REGRESSION") <> "1" then
            this.Skip <-
                "Run through catalog-runtime-validation.py with the pid-namespace backend and FSGG_SDD_NAMESPACE_REGRESSION=1."

[<Collection("ProcessGlobalEnv")>]
module CatalogScaffoldLinuxIdentityTests =
    module Linux = CatalogScaffoldLinux

    [<Fact>]
    let ``pidfd metadata preserves both identities and refuses incomplete identity pairs`` () =
        // The fdinfo parser is private implementation, not a new product API. Reflection
        // exercises malformed kernel-row inputs without fabricating native descriptor custody.
        let moduleType =
            typeof<CatalogScaffoldEffects.HostSelection>
                .Assembly.GetType("FS.GG.SDD.Commands.CatalogScaffoldLinux", true)
            |> nonNull

        let parser =
            moduleType.GetMethod(
                "parsePidfdIdentities",
                BindingFlags.Static ||| BindingFlags.NonPublic ||| BindingFlags.Public
            )
            |> nonNull

        let parse (rows: string array) =
            parser.Invoke(null, [| box rows |]) |> nonNull :?> (int * int)

        Assert.Equal((41, 41), parse [| "Pid: 41"; "NSpid: 41" |])
        Assert.Equal((41, 3), parse [| "Pid:\t41"; "NSpid:\t41  3" |])

        let invalidRows =
            [
                [| "Pid: 41" |]
                [| "NSpid: 41" |]
                [| "Pid: 41"; "Pid: 41"; "NSpid: 41" |]
                [| "Pid: 41"; "NSpid: 41"; "NSpid: 41" |]
                [| "Pid: 0"; "NSpid: 0" |]
                [| "Pid: 41"; "NSpid: 41 0" |]
                [| "Pid: 41"; "NSpid: 42 3" |]
                [| "Pid: 2147483648"; "NSpid: 2147483648" |]
                [| "Pid: 41"; "NSpid: 41 x" |]
                [| "Pid: 41"; "NSpid:" |]
                [| "Pid: 41 42"; "NSpid: 41" |]
                [| "Pid: +41"; "NSpid: 41" |]
                [| "Pid: 41"; "NSpid: 41 -3" |]
            ]

        for rows in invalidRows do
            let error = Assert.Throws<TargetInvocationException>(fun () -> parse rows |> ignore)

            match error.InnerException with
            | Linux.CustodyUnknown diagnostics ->
                Assert.Contains(diagnostics, fun item -> item.Code = "catalog.pidfdIdentity")
            | _ -> failwithf "Unexpected metadata failure: %A" error.InnerException

    [<CatalogNamespaceFact>]
    let ``held child uses namespace wait identity and outer proc census identity`` () =
        // Reject an accidentally enabled ordinary run before acquiring native custody.
        // The flag alone is neither namespace evidence nor teardown authority.
        Assert.True(OperatingSystem.IsLinux(), "This regression requires the selected Linux namespace backend.")

        let namespacePids =
            File.ReadAllLines("/proc/self/status")
            |> Array.find (fun line -> line.StartsWith("NSpid:", StringComparison.Ordinal))
            |> fun line -> line.Substring(6).Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map int

        Assert.True(
            namespacePids.Length > 1 && namespacePids[0] <> Environment.ProcessId,
            "The regression requires inherited outer procfs and a nested caller PID namespace."
        )

        Assert.Equal(Environment.ProcessId, Array.last namespacePids)
        let workingRoot = TestSupport.tempDirectory ()
        let custody = Linux.createCustody ()
        let budget = Linux.beginPhase 10 CancellationToken.None
        let mutable released = false

        try
            Linux.observeAbi custody budget
            |> Result.defaultWith (fun errors -> failwithf "ABI refused: %A" errors)
            |> ignore

            let root = Linux.openDirectory custody budget workingRoot

            let child =
                Linux.startChild custody budget "/usr/bin/printf" [ Linux.Literal "identity-ok\n" ] [] root

            let _, originalProcPid, _, _ = Linux.childIdentities custody |> List.exactlyOne
            let mutable observation = Linux.observeChild budget child

            while observation.ExitCode.IsNone && observation.Settlement <> Linux.Unknown do
                Linux.checkWork budget
                Thread.Sleep(1)
                observation <- Linux.observeChild budget child

            let final = Linux.settleChild budget child

            let _, finalProcPid, reaped, unknown =
                Linux.childIdentities custody |> List.exactlyOne

            Assert.Equal(Linux.KnownTerminal, final.Settlement)
            Assert.Equal(Some 0, final.ExitCode)
            Assert.True(final.StdoutEof && final.StderrEof && reaped && not unknown)
            Assert.True(originalProcPid.IsSome && originalProcPid = finalProcPid)
            Assert.Equal("identity-ok\n", Encoding.UTF8.GetString final.Stdout)
            Assert.Empty final.Stderr
            Assert.Empty final.Diagnostics
            Assert.True(Linux.settled custody)
            Linux.releaseKnown budget custody
            released <- true
            Directory.Delete workingRoot
        with error ->
            eprintfn "Namespace identity regression failed: %s" (error.ToString())
            // Retain unknown product custody until the runner retires the namespace.
            // Assertion failure cannot manufacture release or detach the original owner.
            if not released then
                use retained = new ManualResetEventSlim(false)
                retained.Wait()
                GC.KeepAlive(custody)

            reraise ()

    [<CatalogNamespaceFact>]
    let ``executable mode preserves owned bytes and refuses uncaptured or substituted files`` () =
        Assert.True(OperatingSystem.IsLinux())

        let namespaceRow =
            File.ReadAllLines("/proc/self/status")
            |> Array.find (fun line -> line.StartsWith("NSpid:", StringComparison.Ordinal))

        let namespacePids =
            namespaceRow.Substring(6).Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)

        Assert.True(namespacePids.Length > 1, "Run the native regression through the maintained namespace backend.")
        let workingRoot = TestSupport.tempDirectory ()
        let custody = Linux.createCustody ()
        let budget = Linux.beginPhase 15 CancellationToken.None
        let mutable released = false

        try
            Linux.observeAbi custody budget
            |> Result.defaultWith (fun errors -> failwithf "ABI refused: %A" errors)
            |> ignore

            let root = Linux.openDirectory custody budget workingRoot
            let original = Encoding.UTF8.GetBytes "#!/bin/sh\nprintf 'owned bytes'\n"
            Linux.writeFile custody budget root "owned.sh" original false
            let path = Path.Combine(workingRoot, "owned.sh")

            let initialMode =
                UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.GroupRead

            File.SetUnixFileMode(path, initialMode)
            let before = Linux.readFile custody budget root "owned.sh" original.Length
            Linux.setExecutable custody budget root "owned.sh"

            Assert.Equal(
                initialMode
                ||| UnixFileMode.UserExecute
                ||| UnixFileMode.GroupExecute
                ||| UnixFileMode.OtherExecute,
                File.GetUnixFileMode path
            )
            // readFile rechecks the retained first file identity, not just matching content.
            let after = Linux.readFile custody budget root "owned.sh" original.Length
            Assert.True(before.Bytes = after.Bytes)
            Assert.Equal(before.Sha256, after.Sha256)

            let uncaptured = Path.Combine(workingRoot, "uncaptured.sh")
            File.WriteAllBytes(uncaptured, original)
            File.SetUnixFileMode(uncaptured, initialMode)

            let uncapturedError =
                Assert.Throws<Linux.Refused>(fun () -> Linux.setExecutable custody budget root "uncaptured.sh")

            match (uncapturedError :> exn) with
            | Linux.Refused diagnostics ->
                Assert.Contains(diagnostics, fun item -> item.Code = "catalog.fileBindingChanged")
            | _ -> failwith "Expected the owned-file identity refusal."

            Assert.Equal(initialMode, File.GetUnixFileMode uncaptured)
            Assert.True((original = File.ReadAllBytes uncaptured))

            File.Move(path, Path.Combine(workingRoot, "original-held.sh"))
            File.WriteAllBytes(path, original)
            File.SetUnixFileMode(path, initialMode)

            let substitutionError =
                Assert.Throws<Linux.Refused>(fun () -> Linux.setExecutable custody budget root "owned.sh")

            match (substitutionError :> exn) with
            | Linux.Refused diagnostics ->
                Assert.Contains(diagnostics, fun item -> item.Code = "catalog.fileBindingChanged")
            | _ -> failwith "Expected the owned-file identity refusal."

            Assert.Equal(initialMode, File.GetUnixFileMode path)
            Assert.True((original = File.ReadAllBytes path))
            Assert.True(Linux.settled custody)
            Linux.releaseKnown budget custody
            released <- true
            Directory.Delete(workingRoot, true)
        with error ->
            eprintfn "Executable-mode regression failed: %s" (error.ToString())

            if not released then
                use retained = new ManualResetEventSlim(false)
                retained.Wait()
                GC.KeepAlive(custody)

            reraise ()
