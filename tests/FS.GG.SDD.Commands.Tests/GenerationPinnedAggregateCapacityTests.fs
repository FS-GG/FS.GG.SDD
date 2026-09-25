namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedAggregateCapacityTests =
    let private mib value = int64 value * 1024L * 1024L

    let private fixture sizes action =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-total-cap-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "inputs")) |> ignore
            let declared =
                sizes
                |> List.map (fun (name, size) ->
                    let relative = "inputs/" + name
                    do
                        use stream = File.Create(Path.Combine(root, relative))
                        stream.SetLength(mib size)
                    relative)
            try action root declared
            finally Directory.Delete(root, true)

    [<Fact>]
    let ``individually bounded files exceeding aggregate budget refuse`` () =
        fixture [ "a.bin", 24; "b.bin", 24; "c.bin", 24 ] (fun root declared ->
            Assert.Equal(Error(CaptureLimitExceeded "inputs/c.bin"),
                         capturePinnedWithHooks ignore ignore root "inputs" declared ExactBytes))

    [<Fact>]
    let ``aggregate budget exact boundary remains readable`` () =
        fixture [ "a.bin", 24; "b.bin", 24; "c.bin", 16 ] (fun root declared ->
            match capturePinnedWithHooks ignore ignore root "inputs" declared ExactBytes with
            | Error reason -> failwithf "at-budget capture refused: %A" reason
            | Ok files -> Assert.Equal<string list>(declared, files |> List.map _.Path))
