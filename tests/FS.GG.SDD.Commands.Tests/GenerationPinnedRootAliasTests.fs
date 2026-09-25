namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedRootAliasTests =
    let private composed = "caf\u00e9"
    let private decomposed = "cafe\u0301"

    let private fixture action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-root-alias-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(root) |> ignore
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``complete root refuses a canonical alias beside selected root`` () =
        fixture (fun root ->
            let selected = "work/" + composed
            let alias = "work/" + decomposed
            Directory.CreateDirectory(Path.Combine(root, selected)) |> ignore
            Directory.CreateDirectory(Path.Combine(root, alias)) |> ignore
            let file = selected + "/source.bin"
            File.WriteAllBytes(Path.Combine(root, file), [| 1uy |])
            Assert.Equal(Error(DuplicatePath selected),
                         capture root selected [ file ] ExactBytes))

    [<Fact>]
    let ``complete root refuses an alias at an intermediate segment`` () =
        fixture (fun root ->
            let selected = "work/model"
            Directory.CreateDirectory(Path.Combine(root, selected)) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "WORK")) |> ignore
            let file = selected + "/source.bin"
            File.WriteAllBytes(Path.Combine(root, file), [| 1uy |])
            Assert.Equal(Error(DuplicatePath "work"),
                         capture root selected [ file ] ExactBytes))

    [<Fact>]
    let ``late alias beside selected root refuses observed capture`` () =
        fixture (fun root ->
            let selected = "work/" + composed
            let alias = "work/" + decomposed
            Directory.CreateDirectory(Path.Combine(root, selected)) |> ignore
            let file = selected + "/source.bin"
            File.WriteAllBytes(Path.Combine(root, file), [| 1uy |])
            let afterRead path =
                Assert.Equal(file, path)
                Directory.CreateDirectory(Path.Combine(root, alias)) |> ignore
            Assert.Equal(Error(DuplicatePath selected),
                         capturePinnedWithReadHook afterRead root selected [ file ] ExactBytes))

    [<Fact>]
    let ``unrelated sibling root preserves exact capture`` () =
        fixture (fun root ->
            let selected = "work/" + composed
            Directory.CreateDirectory(Path.Combine(root, selected)) |> ignore
            Directory.CreateDirectory(Path.Combine(root, "work/other")) |> ignore
            let file = selected + "/source.bin"
            let bytes = [| 1uy; 2uy |]
            File.WriteAllBytes(Path.Combine(root, file), bytes)
            match capture root selected [ file ] ExactBytes with
            | Ok [ captured ] ->
                Assert.Equal(file, captured.Path)
                Assert.Equal<byte>(bytes, captured.Bytes)
            | result -> failwithf "Expected exact captured file, got %A" result)
