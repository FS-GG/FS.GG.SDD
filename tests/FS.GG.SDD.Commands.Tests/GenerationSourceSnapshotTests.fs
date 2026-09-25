namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Runtime.InteropServices
open FS.GG.SDD.Commands
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Artifacts
open Xunit

module GenerationSourceSnapshotTests =
    [<DllImport("libc", EntryPoint = "mkfifo")>]
    extern int mkfifo(string path, uint32 mode)

    let private withTree action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-source-snapshot-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, "inputs", "nested")) |> ignore
        try action root
        finally Directory.Delete(root, true)

    let private write root relative (bytes: byte[]) =
        let path = Path.Combine(root, relative)
        File.WriteAllBytes(path, bytes)
        path

    let private captured = function
        | Ok files -> files
        | Error refusal -> failwithf "valid capture refused: %A" refusal

    [<Fact>]
    let ``closed source bytes and digest snapshots are immutable`` () =
        withTree (fun root ->
            let original = [| 1uy; 2uy; 3uy |]
            let path = write root "inputs/nested/A.bin" original
            let first = capture root "inputs" [ "inputs/nested/A.bin" ] ExactBytes |> captured
            Assert.Single(first) |> ignore
            Assert.Equal("inputs/nested/A.bin", first.Head.Path)
            Assert.Equal(SchemaVersion.sha256Bytes original, first.Head.Digest)
            let getter = first.Head.Bytes
            getter.[0] <- 9uy
            File.WriteAllBytes(path, [| 8uy; 2uy; 3uy |])
            Assert.True(first.Head.Bytes = original)
            let second = capture root "inputs" [ "inputs/nested/A.bin" ] ExactBytes |> captured
            Assert.NotEqual(first.Head.Digest, second.Head.Digest))

    [<Fact>]
    let ``missing extra duplicate and escaping paths refuse without touching prior output`` () =
        withTree (fun root ->
            let prior = write root "prior-view.json" [| 42uy |]
            write root "inputs/A.txt" [| 65uy |] |> ignore
            Assert.Equal(Error(MissingFile "inputs/B.txt"),
                capture root "inputs" [ "inputs/A.txt"; "inputs/B.txt" ] ExactBytes)
            Assert.Equal(Error(DuplicatePath "inputs/a.txt"),
                capture root "inputs" [ "inputs/A.txt"; "inputs/a.txt" ] ExactBytes)
            Assert.Equal(Error(InvalidPath "inputs/../outside"),
                capture root "inputs" [ "inputs/../outside" ] ExactBytes)
            Assert.Equal(Error InvalidRoot,
                capture root "../outside" [ "inputs/A.txt" ] ExactBytes)
            Assert.True(File.ReadAllBytes prior = [| 42uy |]))

    [<Fact>]
    let ``extra and symlink source entries refuse`` () =
        withTree (fun root ->
            write root "inputs/A.txt" [| 65uy |] |> ignore
            write root "inputs/nested/extra.txt" [| 66uy |] |> ignore
            Assert.Equal(Error(UnexpectedFile "inputs/nested/extra.txt"),
                capture root "inputs" [ "inputs/A.txt" ] ExactBytes)
            File.Delete(Path.Combine(root, "inputs/nested/extra.txt"))
            if not (OperatingSystem.IsWindows()) then
                File.CreateSymbolicLink(Path.Combine(root, "inputs/nested/link.txt"), Path.Combine(root, "inputs/A.txt"))
                |> ignore
                Assert.Equal(Error(Symlink "inputs/nested/link.txt"),
                    capture root "inputs" [ "inputs/A.txt" ] ExactBytes))

    [<Fact>]
    let ``a declared directory is not a regular source file`` () =
        withTree (fun root ->
            Assert.Equal(Error(NonRegular "inputs/nested"),
                capture root "inputs" [ "inputs/nested" ] ExactBytes))

    [<Fact>]
    let ``physical case collisions and directory links refuse`` () =
        withTree (fun root ->
            write root "inputs/A.txt" [| 1uy |] |> ignore
            if not (OperatingSystem.IsWindows()) then
                write root "inputs/a.txt" [| 2uy |] |> ignore
                Assert.Equal(Error(DuplicatePath "inputs/a.txt"),
                    capture root "inputs" [ "inputs/A.txt" ] ExactBytes)
                File.Delete(Path.Combine(root, "inputs/a.txt"))
                Directory.CreateSymbolicLink(Path.Combine(root, "inputs/nested/linkdir"), root) |> ignore
                Assert.Equal(Error(Symlink "inputs/nested/linkdir"),
                    capture root "inputs" [ "inputs/A.txt" ] ExactBytes))

    [<Fact>]
    let ``symlink in a workspace ancestor refuses before source capture`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let physical = Path.Combine(root, "physical", "workspace")
                Directory.CreateDirectory(Path.Combine(physical, "inputs")) |> ignore
                File.WriteAllBytes(Path.Combine(physical, "inputs", "A.bin"), [| 1uy |])
                Directory.CreateSymbolicLink(Path.Combine(root, "alias"), Path.Combine(root, "physical")) |> ignore
                let aliasedWorkspace = Path.Combine(root, "alias", "workspace")
                Assert.Equal(Error(Symlink ".."),
                    capture aliasedWorkspace "inputs" [ "inputs/A.bin" ] ExactBytes))

    [<Fact>]
    let ``Unix FIFO refuses before a blocking byte read`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                write root "inputs/A.txt" [| 1uy |] |> ignore
                let pipe = Path.Combine(root, "inputs", "nested", "pipe")
                Assert.Equal(0, mkfifo(pipe, 0o600u))
                Assert.Equal(Error(NonRegular "inputs/nested/pipe"),
                    capture root "inputs" [ "inputs/A.txt" ] ExactBytes))

    [<Fact>]
    let ``text digest policy folds BOM and CRLF while retaining original bytes`` () =
        withTree (fun root ->
            let raw = [| 0xefuy; 0xbbuy; 0xbfuy; byte 'x'; 13uy; 10uy |]
            write root "inputs/A.txt" raw |> ignore
            let file = capture root "inputs" [ "inputs/A.txt" ] Utf8LfText |> captured |> List.head
            Assert.True(file.Bytes = raw)
            Assert.Equal(SchemaVersion.sha256Text "x\n", file.Digest)
            write root "inputs/A.txt" [| 0xffuy |] |> ignore
            Assert.Equal(Error(Unreadable "inputs/A.txt"),
                capture root "inputs" [ "inputs/A.txt" ] Utf8LfText))
