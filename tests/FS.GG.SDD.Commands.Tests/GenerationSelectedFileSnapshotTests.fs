namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Runtime.InteropServices
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationSelectedFileSnapshotTests =
    [<DllImport("libc", EntryPoint = "mkfifo")>]
    extern int mkfifo(string path, uint32 mode)

    let private withTree action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-selected-file-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "tests")) |> ignore
            let target = Path.Combine(root, "tests", "performance.txt")
            File.WriteAllBytes(target, [| 1uy; 2uy |])
            try action root target
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``selected file capture accepts unrelated regular and linked siblings`` () =
        withTree (fun root target ->
            File.WriteAllText(Path.Combine(root, "tests", "unrelated.txt"), "other")
            File.CreateSymbolicLink(Path.Combine(root, "tests", "shortcut.txt"), target) |> ignore
            match captureSelectedFile root "tests/performance.txt" with
            | Error reason -> failwithf "selected file refused: %A" reason
            | Ok file ->
                Assert.Equal("tests/performance.txt", file.Path)
                Assert.True(file.Bytes = [| 1uy; 2uy |])
                Assert.Equal(SchemaVersion.sha256Bytes [| 1uy; 2uy |], file.Digest))

    [<Fact>]
    let ``selected leaf link and nonregular file refuse`` () =
        withTree (fun root target ->
            let foreign = Path.Combine(root, "foreign.txt")
            File.WriteAllText(foreign, "foreign")
            File.Delete target
            File.CreateSymbolicLink(target, foreign) |> ignore
            Assert.Equal(Error(Symlink "tests/performance.txt"),
                         captureSelectedFile root "tests/performance.txt")
            File.Delete target
            Assert.Equal(0, mkfifo(target, 0o600u))
            Assert.Equal(Error(NonRegular "tests/performance.txt"),
                         captureSelectedFile root "tests/performance.txt"))

    [<Fact>]
    let ``selected leaf and parent case aliases refuse`` () =
        withTree (fun root target ->
            File.WriteAllText(Path.Combine(root, "tests", "Performance.txt"), "alias")
            Assert.Equal(Error(DuplicatePath "tests/performance.txt"),
                         captureSelectedFile root "tests/performance.txt")
            File.Delete(Path.Combine(root, "tests", "Performance.txt"))
            File.Delete target
            File.WriteAllText(Path.Combine(root, "tests", "Performance.txt"), "alias")
            Assert.Equal(Error(DuplicatePath "tests/performance.txt"),
                         captureSelectedFile root "tests/performance.txt")
            Directory.CreateDirectory(Path.Combine(root, "Tests")) |> ignore
            Assert.Equal(Error(DuplicatePath "tests"),
                         captureSelectedFile root "tests/performance.txt"))

    [<Fact>]
    let ``linked selected ancestor and escaping path refuse`` () =
        withTree (fun root _ ->
            Directory.CreateSymbolicLink(Path.Combine(root, "alias"), Path.Combine(root, "tests")) |> ignore
            Assert.Equal(Error(Symlink "alias"), captureSelectedFile root "alias/performance.txt")
            Assert.Equal(Error(InvalidPath "tests/../outside"),
                         captureSelectedFile root "tests/../outside"))

    [<Fact>]
    let ``link swapped before selected descriptor open refuses`` () =
        withTree (fun root target ->
            let backup = Path.Combine(root, "backup.txt")
            let foreign = Path.Combine(root, "foreign.txt")
            File.WriteAllBytes(foreign, [| 9uy |])
            let beforeOpen path =
                Assert.Equal("tests/performance.txt", path)
                File.Move(target, backup)
                File.CreateSymbolicLink(target, foreign) |> ignore
            try
                Assert.Equal(Error(Unreadable "tests/performance.txt"),
                             captureSelectedFileWithHooks beforeOpen ignore ignore root "tests/performance.txt")
            finally
                File.Delete target
                File.Move(backup, target))

    [<Fact>]
    let ``restored link swap after selected open refuses changed parent`` () =
        withTree (fun root target ->
            let backup = Path.Combine(root, "backup.txt")
            let foreign = Path.Combine(root, "foreign.txt")
            File.WriteAllBytes(foreign, [| 9uy |])
            let afterOpen path =
                Assert.Equal("tests/performance.txt", path)
                File.Move(target, backup)
                try File.CreateSymbolicLink(target, foreign) |> ignore
                finally
                    File.Delete target
                    File.Move(backup, target)
            Assert.Equal(Error(DirectoryUnstable "tests"),
                         captureSelectedFileWithHooks ignore afterOpen ignore root "tests/performance.txt")
            Assert.True(File.ReadAllBytes target = [| 1uy; 2uy |]))

    [<Fact>]
    let ``foreign regular file swapped before open then restored refuses`` () =
        withTree (fun root target ->
            let backup = Path.Combine(root, "backup.txt")
            let foreign = Path.Combine(root, "foreign.txt")
            File.WriteAllBytes(foreign, [| 9uy |])
            let beforeOpen path =
                Assert.Equal("tests/performance.txt", path)
                File.Move(target, backup)
                File.Copy(foreign, target)
            let afterOpen path =
                Assert.Equal("tests/performance.txt", path)
                File.Delete target
                File.Move(backup, target)
            Assert.Equal(Error(DirectoryUnstable "tests"),
                         captureSelectedFileWithHooks beforeOpen afterOpen ignore root "tests/performance.txt")
            Assert.True(File.ReadAllBytes target = [| 1uy; 2uy |]))

    [<Fact>]
    let ``selected file refuses in-place mutation after first byte pass`` () =
        withTree (fun root target ->
            let afterRead path =
                Assert.Equal("tests/performance.txt", path)
                File.WriteAllBytes(target, [| 8uy; 2uy |])
            Assert.Equal(Error(FileUnstable "tests/performance.txt"),
                         captureSelectedFileWithHooks ignore ignore afterRead root "tests/performance.txt"))

    [<Fact>]
    let ``selected file refuses parent roster mutation while read`` () =
        withTree (fun root _ ->
            let afterOpen path =
                Assert.Equal("tests/performance.txt", path)
                File.WriteAllText(Path.Combine(root, "tests", "late.txt"), "late")
            Assert.Equal(Error(DirectoryUnstable "tests"),
                         captureSelectedFileWithHooks ignore afterOpen ignore root "tests/performance.txt"))
