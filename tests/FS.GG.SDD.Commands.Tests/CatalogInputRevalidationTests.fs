namespace FS.GG.SDD.Commands.Tests

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Threading
open System.Threading.Tasks
open FS.GG.SDD.Commands
open Xunit

type CatalogReadFactAttribute() as this =
    inherit FactAttribute()

    do
        if not (OperatingSystem.IsLinux()) then
            this.Skip <- "The descriptor-relative read seam requires Linux; no child/runtime carrier is launched."

module CatalogInputRevalidationTests =
    module Linux = CatalogScaffoldLinux

    // Observe the actual allocation/task owner rather than process RSS, which
    // cannot distinguish retained buffers from allocator/GC high-water marks.
    let private owned<'a> name (custody: Linux.Custody) =
        let property =
            custody
                .GetType()
                .GetProperty(name, BindingFlags.Instance ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            |> Option.ofObj
            |> Option.defaultWith (fun () -> failwith ("Missing custody observation: " + name))

        property.GetValue(custody) :?> 'a

    let private refused (action: unit -> unit) =
        match Assert.Throws<Linux.Refused>(fun () -> action ()) :> exn with
        | Linux.Refused errors -> errors
        | other -> failwithf "Unexpected refusal: %A" other

    let private nativeBytes custody =
        (owned<Dictionary<nativeint, int>> "Pointers" custody).Values |> Seq.sumBy int64

    let private captureResults custody =
        owned<ResizeArray<Task>> "Tasks" custody
        |> Seq.choose (fun task ->
            match task with
            | :? Task<Linux.CapturedFile> as capture -> Some capture.Result.Bytes.LongLength
            | _ -> None)
        |> Seq.sum

    let private withFile action =
        let path = Directory.CreateTempSubdirectory("catalog-revalidation-").FullName
        let custody = Linux.createCustody ()
        let budget = Linux.beginPhase 30 CancellationToken.None
        let bytes = Array.init (256 * 1024) (fun index -> byte (index % 251))
        File.WriteAllBytes(Path.Combine(path, "input.bin"), bytes)

        try
            let root = Linux.openDirectory custody budget path
            let original = Linux.readFile custody budget root "input.bin" bytes.Length
            action path custody budget root original
        finally
            Assert.True(Linux.settled custody)
            Linux.releaseKnown budget custody
            Assert.Equal(0L, nativeBytes custody)
            Assert.Empty(owned<ResizeArray<int>> "Descriptors" custody)
            Directory.Delete(path, true)

    [<CatalogReadFact>]
    let ``repeated exact revalidation retains neither native payload buffers nor managed capture results`` () =
        withFile (fun _ custody budget root original ->
            // Initial capture retains its authored bytes, but no native payload copy.
            Assert.Equal(int64 original.Bytes.Length, captureResults custody)
            let before = nativeBytes custody

            for _ in 1..16 do
                Assert.True(Linux.fileMatches custody budget root original)

            // Metadata remains owned until final cleanup; sixteen whole payload
            // copies (4 MiB in each domain) must not be retained alongside it.
            Assert.InRange(nativeBytes custody - before, 0L, 128L * 1024L)
            Assert.True(before < int64 original.Bytes.Length)
            Assert.Equal(int64 original.Bytes.Length, captureResults custody)
            Assert.True(Linux.settled custody))

    [<CatalogReadFact>]
    let ``same length mutation and incorrect expected hash both refuse exact matching`` () =
        withFile (fun path custody budget root original ->
            Assert.False(
                Linux.fileMatches
                    custody
                    budget
                    root
                    { original with
                        Sha256 = "sha256:" + String('0', 64)
                    }
            )

            let changed = Array.copy original.Bytes
            changed[changed.Length / 2] <- changed[changed.Length / 2] ^^^ 1uy
            File.WriteAllBytes(Path.Combine(path, "input.bin"), changed)
            Assert.False(Linux.fileMatches custody budget root original)
            Assert.True(nativeBytes custody < int64 original.Bytes.Length)
            Assert.Equal(int64 original.Bytes.Length, captureResults custody))

    [<CatalogReadFact>]
    let ``identical replacement bytes cannot reset the held physical binding`` () =
        withFile (fun path custody budget root original ->
            let input = Path.Combine(path, "input.bin")
            File.Move(input, input + ".original")
            File.WriteAllBytes(input, original.Bytes)

            let errors =
                refused (fun () -> Linux.fileMatches custody budget root original |> ignore)

            Assert.Contains(errors, fun error -> error.Code = "catalog.fileBindingChanged")
            Assert.True(nativeBytes custody < int64 original.Bytes.Length))

    [<CatalogReadFact>]
    let ``oversize failed reads remain settled and cleanable without payload retention`` () =
        withFile (fun _ custody budget root original ->
            let errors =
                refused (fun () -> Linux.readFile custody budget root original.Path 1 |> ignore)

            Assert.Contains(errors, fun error -> error.Code = "catalog.inputSize")
            Assert.True(Linux.settled custody)
            Assert.True(nativeBytes custody < int64 original.Bytes.Length)
            Assert.True(Linux.fileMatches custody budget root original))

    [<CatalogReadFact>]
    let ``stopped work registers no reread and original cleanup still owns all resources`` () =
        withFile (fun _ custody budget root original ->
            let before = nativeBytes custody
            let tasks = (owned<ResizeArray<Task>> "Tasks" custody).Count
            Linux.stopUsefulWork budget

            let errors =
                refused (fun () -> Linux.fileMatches custody budget root original |> ignore)

            Assert.Contains(errors, fun error -> error.Code = "catalog.workStopped")
            Assert.Equal(before, nativeBytes custody)
            Assert.Equal(tasks, (owned<ResizeArray<Task>> "Tasks" custody).Count)
            Assert.True(Linux.settled custody))
