namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedFileCountTests =
    let private fixture count action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-file-count-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "inputs")) |> ignore
            let declared =
                [ for number in 0 .. count - 1 do
                    let relative = "inputs/f" + number.ToString("D4") + ".bin"
                    File.WriteAllBytes(Path.Combine(root, relative), [||])
                    yield relative ]
            try action root declared
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``declared zero-byte files above captured-file limit refuse`` () =
        fixture 4097 (fun root declared ->
            Assert.Equal(Error(CapturedFileLimit "inputs/f4096.bin"),
                         capturePinnedWithHooks ignore ignore root "inputs" declared ExactBytes))

    [<Fact>]
    let ``captured-file count exact boundary remains readable`` () =
        fixture 4096 (fun root declared ->
            match capturePinnedWithHooks ignore ignore root "inputs" declared ExactBytes with
            | Error reason -> failwithf "at-count capture refused: %A" reason
            | Ok files -> Assert.Equal(4096, List.length files))

    [<Fact>]
    let ``physical file beyond declared count refuses before opening`` () =
        fixture 4097 (fun root declared ->
            Assert.Equal(Error(CapturedFileLimit "inputs/f4096.bin"),
                         capturePinnedWithHooks ignore ignore root "inputs"
                                                (declared |> List.take 4096) ExactBytes))
