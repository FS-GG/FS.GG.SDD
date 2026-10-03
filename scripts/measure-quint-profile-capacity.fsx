open System
open System.IO
open System.Diagnostics
open System.Text.Json
open FS.GG.SDD.Artifacts.TypedSpecifications
// Pass the source-bound Contracts and Artifacts DLLs as FSI --reference arguments.
// Inputs are retained compiler output and selector facts, never fabricated provenance.
let args = fsi.CommandLineArgs |> Array.skip 1

if args.Length <> 2 then
    failwith "Expected typed-effect JSON and profile-bindings JSON paths."

if FileInfo(args[0]).Length > 16L * 1024L * 1024L then
    failwith "Measurement input exceeds 16 MiB."

let manifest =
    match QuintGeneralBindingManifest.deserialize (File.ReadAllText args[1]) with
    | Ok value -> value
    | Error errors -> failwithf "%A" errors

let observation =
    {
        Profile = manifest.Profile
        QuintVersion = QuintGeneralProfile.quintVersion
        TypedEffectJson = File.ReadAllText args[0]
        ExportBindings = manifest.Exports
        ActionBindings = manifest.Actions
    }

let doc = JsonDocument.Parse observation.TypedEffectJson

let counts =
    [|
        for name in [ "table"; "types"; "effects" ] ->
            name, (doc.RootElement.GetProperty(name).EnumerateObject() |> Seq.length)
    |]

for iteration in 0..3 do
    GC.Collect()
    GC.WaitForPendingFinalizers()
    GC.Collect()
    let allocated = GC.GetTotalAllocatedBytes(true)
    let cpu = Process.GetCurrentProcess().TotalProcessorTime
    let timer = Stopwatch.StartNew()
    let result = QuintGeneralProfile.adaptTypedEffectJson observation
    timer.Stop()

    let status =
        match result with
        | Ok _ -> "accepted"
        | Error errors -> errors |> List.map _.Code |> String.concat ","

    printfn
        "%s"
        (JsonSerializer.Serialize
            {|
                iteration = iteration
                wallMs = timer.Elapsed.TotalMilliseconds
                cpuMs = (Process.GetCurrentProcess().TotalProcessorTime - cpu).TotalMilliseconds
                allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated
                rows = counts
                bytes = System.Text.Encoding.UTF8.GetByteCount observation.TypedEffectJson
                outcome = status
            |})

doc.Dispose()
