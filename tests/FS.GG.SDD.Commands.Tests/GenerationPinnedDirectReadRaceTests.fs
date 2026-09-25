namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedDirectReadRaceTests =
    let private source = "inputs/source.bin"

    let private fixture length action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-direct-race-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "inputs")) |> ignore
            do
                use stream = File.Create(Path.Combine(root, source))
                stream.SetLength length
            try action root
            finally Directory.Delete(root, true)

    let private appendByte path =
        use writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
        writer.WriteByte 7uy

    [<Fact>]
    let ``shortened opened file refuses during direct first pass`` () =
        fixture 2L (fun root ->
            let afterLength path =
                Assert.Equal(source, path)
                File.WriteAllBytes(Path.Combine(root, source), [| 1uy |])
            Assert.Equal(Error(FileUnstable source),
                         capturePinnedWithLengthHook afterLength root "inputs" [ source ] ExactBytes))

    [<Fact>]
    let ``grown opened file within budgets refuses during direct first pass`` () =
        fixture 1L (fun root ->
            let afterLength path =
                Assert.Equal(source, path)
                appendByte (Path.Combine(root, source))
            Assert.Equal(Error(FileUnstable source),
                         capturePinnedWithLengthHook afterLength root "inputs" [ source ] ExactBytes))

    [<Fact>]
    let ``growth beyond per-file budget refuses before accepting extra byte`` () =
        fixture (32L * 1024L * 1024L) (fun root ->
            let afterLength path =
                Assert.Equal(source, path)
                appendByte (Path.Combine(root, source))
            Assert.Equal(Error(FileLimitExceeded source),
                         capturePinnedWithLengthHook afterLength root "inputs" [ source ] ExactBytes))
