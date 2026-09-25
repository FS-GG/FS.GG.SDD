namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationHeldDirectoryLimitTests =
    let private source = "work/source.txt"

    let private openHandlesUnder root =
        Directory.EnumerateFileSystemEntries("/proc/self/fd")
        |> Seq.filter (fun descriptor ->
            try
                FileInfo(descriptor).LinkTarget
                |> Option.ofObj
                |> Option.exists (fun target -> target.StartsWith(root + "/", StringComparison.Ordinal))
            with :? IOException -> false)
        |> Seq.length

    let private fixture childCount action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-held-limit-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "work")) |> ignore
            File.WriteAllText(Path.Combine(root, source), "bytes")
            for number in 0 .. childCount - 1 do
                Directory.CreateDirectory(Path.Combine(root, "work", "d" + number.ToString("D3"))) |> ignore
            try action root
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``over-limit held child inventory refuses before opening next descriptor`` () =
        fixture 257 (fun root ->
            Assert.Equal(Error(HeldDirectoryLimit "work/d256"),
                         capturePinnedWithHooks ignore ignore root "work" [ source ] ExactBytes))

    [<Fact>]
    let ``at-limit held child inventory remains readable`` () =
        fixture 256 (fun root ->
            match capturePinnedWithHooks ignore ignore root "work" [ source ] ExactBytes with
            | Error reason -> failwithf "at-limit inventory refused: %A" reason
            | Ok files -> Assert.Equal<string list>([ source ], files |> List.map _.Path))

    [<Fact>]
    let ``over-limit refusal releases held child descriptors`` () =
        fixture 257 (fun root ->
            for _ in 1 .. 3 do
                Assert.Equal(Error(HeldDirectoryLimit "work/d256"),
                             capturePinnedWithHooks ignore ignore root "work" [ source ] ExactBytes)
                Assert.Equal(0, openHandlesUnder root))
