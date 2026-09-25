namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedUnicodeAliasTests =
    let private composed = "caf\u00e9"
    let private decomposed = "cafe\u0301"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-unicode-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "inputs")) |> ignore
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``canonically equivalent declared file names refuse before capture`` () =
        fixture (fun root ->
            let first = "inputs/" + composed + ".bin"
            let second = "inputs/" + decomposed + ".bin"
            File.WriteAllBytes(Path.Combine(root, first), [| 1uy |])
            File.WriteAllBytes(Path.Combine(root, second), [| 2uy |])
            Assert.Equal(Error(DuplicatePath second),
                         capture root "inputs" [ first; second ] ExactBytes))

    [<Fact>]
    let ``physical canonical alias refuses with only one declared file`` () =
        fixture (fun root ->
            let first = "inputs/" + composed + ".bin"
            let second = "inputs/" + decomposed + ".bin"
            File.WriteAllBytes(Path.Combine(root, first), [| 1uy |])
            File.WriteAllBytes(Path.Combine(root, second), [| 2uy |])
            match capture root "inputs" [ first ] ExactBytes with
            | Error(DuplicatePath path) -> Assert.Contains(path, [ first; second ])
            | result -> failwithf "Expected canonical alias refusal, got %A" result)

    [<Fact>]
    let ``selected file refuses a canonical alias in its parent`` () =
        fixture (fun root ->
            let first = "inputs/" + composed + ".bin"
            let second = "inputs/" + decomposed + ".bin"
            File.WriteAllBytes(Path.Combine(root, first), [| 1uy |])
            File.WriteAllBytes(Path.Combine(root, second), [| 2uy |])
            Assert.Equal(Error(DuplicatePath first), captureSelectedFile root first))

    [<Fact>]
    let ``selected directory refuses a canonical alias before descent`` () =
        fixture (fun root ->
            let firstDirectory = "inputs/" + composed
            let secondDirectory = "inputs/" + decomposed
            Directory.CreateDirectory(Path.Combine(root, firstDirectory)) |> ignore
            Directory.CreateDirectory(Path.Combine(root, secondDirectory)) |> ignore
            let path = firstDirectory + "/source.bin"
            File.WriteAllBytes(Path.Combine(root, path), [| 1uy |])
            Assert.Equal(Error(DuplicatePath firstDirectory), captureSelectedFile root path))

    [<Fact>]
    let ``canonical plus case alias refuses in closed root`` () =
        fixture (fun root ->
            let first = "inputs/CAF\u00c9.bin"
            let second = "inputs/" + decomposed + ".bin"
            File.WriteAllBytes(Path.Combine(root, first), [| 1uy |])
            File.WriteAllBytes(Path.Combine(root, second), [| 2uy |])
            Assert.Equal(Error(DuplicatePath second),
                         capture root "inputs" [ first; second ] ExactBytes))

    [<Fact>]
    let ``single unicode name retains exact name and bytes`` () =
        fixture (fun root ->
            let path = "inputs/" + composed + ".bin"
            let bytes = [| 1uy; 2uy; 3uy |]
            File.WriteAllBytes(Path.Combine(root, path), bytes)
            match capture root "inputs" [ path ] ExactBytes with
            | Ok [ file ] ->
                Assert.Equal(path, file.Path)
                Assert.Equal<byte>(bytes, file.Bytes)
            | result -> failwithf "Expected one exact captured file, got %A" result)
