namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedPathCapacityTests =
    let private relativePath length =
        let directories = List.replicate 4 (String.replicate 200 "a")
        let fixedLength = "inputs/".Length + 4 * 201
        let fileLength = length - fixedLength
        let name = "f" + String.replicate (fileLength - 5) "b" + ".bin"
        let path = "inputs/" + String.concat "/" (directories @ [ name ])
        Assert.Equal(length, path.Length)
        path

    let private fixture length action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-path-cap-" + Guid.NewGuid().ToString("N"))
            let relative = relativePath length
            let absolute = Path.Combine(root, relative)
            Path.GetDirectoryName absolute |> Option.ofObj |> Option.get |> Directory.CreateDirectory |> ignore
            File.WriteAllBytes(absolute, [| 1uy |])
            try action root relative
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``overlong declared relative path refuses before capture`` () =
        fixture 1025 (fun root relative ->
            Assert.Equal(Error(PathLimitExceeded relative),
                         capturePinnedWithHooks ignore ignore root "inputs" [ relative ] ExactBytes))

    [<Fact>]
    let ``relative path exact boundary remains readable`` () =
        fixture 1024 (fun root relative ->
            match capturePinnedWithHooks ignore ignore root "inputs" [ relative ] ExactBytes with
            | Error reason -> failwithf "at-limit path refused: %A" reason
            | Ok files -> Assert.Equal<string list>([ relative ], files |> List.map _.Path))

    [<Fact>]
    let ``overlong physical extra path refuses before entry storage`` () =
        fixture 1025 (fun root relative ->
            let selected = "inputs/selected.bin"
            File.WriteAllBytes(Path.Combine(root, selected), [| 2uy |])
            Assert.Equal(Error(PathLimitExceeded relative),
                         capturePinnedWithHooks ignore ignore root "inputs" [ selected ] ExactBytes))

    [<Fact>]
    let ``selected-file pinned read refuses overlong relative path`` () =
        fixture 1025 (fun root relative ->
            Assert.Equal(Error(PathLimitExceeded relative), captureSelectedFile root relative))
