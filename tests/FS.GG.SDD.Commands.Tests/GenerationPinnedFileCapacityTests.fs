namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedFileCapacityTests =
    let private maxBytes = 32L * 1024L * 1024L
    let private source = "inputs/source.bin"

    let private fixture size action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-file-cap-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "inputs")) |> ignore
            do
                use stream = File.Create(Path.Combine(root, source))
                stream.SetLength size
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``oversized pinned regular file refuses before unbounded copy`` () =
        fixture (maxBytes + 1L) (fun root ->
            Assert.Equal(Error(FileLimitExceeded source),
                         capturePinnedWithHooks ignore ignore root "inputs" [ source ] ExactBytes))

    [<Fact>]
    let ``at-cap pinned regular file remains readable`` () =
        fixture maxBytes (fun root ->
            match capturePinnedWithHooks ignore ignore root "inputs" [ source ] ExactBytes with
            | Error reason -> failwithf "at-cap file refused: %A" reason
            | Ok files -> Assert.Equal<string list>([ source ], files |> List.map _.Path))

    [<Fact>]
    let ``selected-file pinned read uses the same file cap`` () =
        fixture (maxBytes + 1L) (fun root ->
            Assert.Equal(Error(FileLimitExceeded source), captureSelectedFile root source))
