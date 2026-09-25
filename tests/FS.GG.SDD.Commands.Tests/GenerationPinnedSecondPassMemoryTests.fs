namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationPinnedSecondPassMemoryTests =
    let private mib = 1024L * 1024L
    let private source = "inputs/source.bin"

    [<Fact>]
    let ``second pinned byte pass avoids a second file-sized allocation`` () =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "sdd-pinned-second-pass-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(Path.Combine(root, "inputs")) |> ignore
            do
                use stream = File.Create(Path.Combine(root, source))
                stream.SetLength(8L * mib)
            try
                let capture () =
                    match capturePinnedWithHooks ignore ignore root "inputs" [ source ] ExactBytes with
                    | Ok files -> Assert.Single files |> ignore
                    | Error reason -> failwithf "stable sparse file refused: %A" reason
                capture () // warm up JIT and runtime paths outside the measured interval
                let before = GC.GetAllocatedBytesForCurrentThread()
                capture ()
                let allocated = GC.GetAllocatedBytesForCurrentThread() - before
                Assert.True(allocated < 48L * mib,
                            $"An 8 MiB file allocated %d{allocated} bytes during pinned capture")
            finally Directory.Delete(root, true)
