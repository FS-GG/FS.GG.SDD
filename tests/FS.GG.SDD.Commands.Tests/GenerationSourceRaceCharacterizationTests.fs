namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Net.Sockets
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

/// Controlled local interleavings document the remaining path-based observation gap.
module GenerationSourceRaceCharacterizationTests =
    let private withTree action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-race-characterization-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, "inputs")) |> ignore
        try action root
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``Linux Unix socket is a static nonregular source entry`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                File.WriteAllBytes(Path.Combine(root, "inputs", "A.bin"), [| 1uy |])
                use socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
                socket.Bind(UnixDomainSocketEndPoint(Path.Combine(root, "inputs", "socket")))
                Assert.Equal(Error(NonRegular "inputs/socket"),
                    capture root "inputs" [ "inputs/A.bin" ] ExactBytes))

    [<Fact>]
    let ``path swap around the byte read can evade both path type probes`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let target = Path.Combine(root, "inputs", "A.bin")
                let backup = Path.Combine(root, "backup.bin")
                let foreign = Path.Combine(root, "foreign.bin")
                File.WriteAllBytes(target, [| 1uy |])
                File.WriteAllBytes(foreign, [| 9uy |])
                let interleavedRead absolute =
                    Assert.Equal(target, absolute)
                    File.Move(target, backup)
                    try
                        File.CreateSymbolicLink(target, foreign) |> ignore
                        File.ReadAllBytes absolute
                    finally
                        File.Delete target
                        File.Move(backup, target)
                let result =
                    captureWithReader interleavedRead root "inputs" [ "inputs/A.bin" ] ExactBytes
                match result with
                | Error reason -> failwithf "interleaving was unexpectedly refused: %A" reason
                | Ok captured ->
                    Assert.Single captured |> ignore
                    Assert.True(captured.Head.Bytes = [| 9uy |])
                    Assert.True(File.ReadAllBytes target = [| 1uy |]))

    [<Fact>]
    let ``descriptor-pinned capture refuses a path swap that changes the roster stamp`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let target = Path.Combine(root, "inputs", "A.bin")
                let backup = Path.Combine(root, "backup.bin")
                let foreign = Path.Combine(root, "foreign.bin")
                File.WriteAllBytes(target, [| 1uy |])
                File.WriteAllBytes(foreign, [| 9uy |])
                let mutable opened = 0
                let afterOpen path =
                    Assert.Equal("inputs/A.bin", path)
                    opened <- opened + 1
                    File.Move(target, backup)
                    try
                        File.CreateSymbolicLink(target, foreign) |> ignore
                    finally
                        File.Delete target
                        File.Move(backup, target)
                let result =
                    capturePinnedWithHooks ignore afterOpen root "inputs" [ "inputs/A.bin" ] ExactBytes
                Assert.Equal(1, opened)
                Assert.Equal(Error(DirectoryUnstable "inputs"), result)
                Assert.True(File.ReadAllBytes target = [| 1uy |]))

    [<Fact>]
    let ``a link swapped in before descriptor open is refused`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let target = Path.Combine(root, "inputs", "A.bin")
                let backup = Path.Combine(root, "backup.bin")
                let foreign = Path.Combine(root, "foreign.bin")
                File.WriteAllBytes(target, [| 1uy |])
                File.WriteAllBytes(foreign, [| 9uy |])
                let beforeOpen path =
                    Assert.Equal("inputs/A.bin", path)
                    File.Move(target, backup)
                    File.CreateSymbolicLink(target, foreign) |> ignore
                try
                    Assert.Equal(Error(Unreadable "inputs/A.bin"),
                        capturePinnedWithHooks beforeOpen ignore root "inputs" [ "inputs/A.bin" ] ExactBytes)
                finally
                    File.Delete target
                    File.Move(backup, target))

    [<Fact>]
    let ``late undeclared file after enumeration refuses`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let original = Path.Combine(root, "inputs", "A.bin")
                let extra = Path.Combine(root, "inputs", "late.bin")
                File.WriteAllBytes(original, [| 1uy |])
                let beforeOpen path =
                    Assert.Equal("inputs/A.bin", path)
                    File.WriteAllBytes(extra, [| 2uy |])
                let result =
                    capturePinnedWithHooks beforeOpen ignore root "inputs" [ "inputs/A.bin" ] ExactBytes
                Assert.True(File.Exists extra)
                Assert.Equal(Error(DirectoryUnstable "inputs"), result))

    [<Fact>]
    let ``late undeclared nested file refuses its held parent`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let nested = Path.Combine(root, "inputs", "nested")
                Directory.CreateDirectory(nested) |> ignore
                File.WriteAllBytes(Path.Combine(nested, "A.bin"), [| 1uy |])
                let extra = Path.Combine(nested, "late.bin")
                let beforeOpen path =
                    Assert.Equal("inputs/nested/A.bin", path)
                    File.WriteAllBytes(extra, [| 2uy |])
                let result =
                    capturePinnedWithHooks beforeOpen ignore root "inputs" [ "inputs/nested/A.bin" ] ExactBytes
                Assert.True(File.Exists extra)
                Assert.Equal(Error(DirectoryUnstable "inputs/nested"), result))
