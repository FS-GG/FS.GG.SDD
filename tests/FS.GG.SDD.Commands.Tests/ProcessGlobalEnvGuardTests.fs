namespace FS.GG.SDD.Commands.Tests

open System.IO
open Xunit

/// Feature 067 / FR-001 durable defense: any test module in this assembly that spawns a
/// PATH-resolved process or mutates process-global environment MUST belong to the
/// `ProcessGlobalEnv` collection, so its mutation is never observed by a concurrently-running
/// sibling. Without this guard the env-mutation race silently returns the first time someone
/// adds a process-spawning module and forgets the attribute.
///
/// This is a source-scan **heuristic**, not a proof: it recognizes the spawn indirections the
/// suite actually uses today — the CLI smoke (`runCliRaw`), a raw `Process.Start`, direct env
/// mutation, and driving the real `scaffold` interpreter (`runScaffold`/`scaffoldRequest`, which
/// launches `dotnet new`). A genuinely novel spawn path (e.g. a future test driving `doctor`/
/// `upgrade`'s real `dotnet tool update` edge under a differently-named helper) would not be
/// caught and must be placed in the collection by hand; add its indicator to `spawnOrMutateMarkers`
/// when that happens.
module ProcessGlobalEnvGuardTests =

    /// Substrings that indicate a module spawns a PATH-resolved process or mutates process env.
    /// `runScaffold`/`scaffoldRequest` cover the interpreter spawn path (`interpretAll` of a
    /// non-dry-run `Scaffold` request → real `dotnet new`), which carries none of the lower-level
    /// markers — the reason `ScaffoldCliCoherenceTests` needs the collection.
    let private spawnOrMutateMarkers =
        [ "runCliRaw"
          "Process.Start"
          "Environment.SetEnvironmentVariable"
          "runScaffold"
          "scaffoldRequest" ]

    let private collectionAttribute = "[<Collection(\"ProcessGlobalEnv\")>]"

    /// Source files that legitimately reference the markers without being a serialized test
    /// class: the shared helper that *defines* the spawn primitive, and this guard itself.
    let private exemptFiles = set [ "TestSupport.fs"; "ProcessGlobalEnvGuardTests.fs" ]

    let private sourceDir =
        Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Commands.Tests")

    /// Non-null file name (Path.GetFileName is nullable under `Nullable enable`).
    let private fileName (path: string) =
        Path.GetFileName path |> Option.ofObj |> Option.defaultValue ""

    /// A non-comment source line that mentions any marker.
    let private usesMarker (text: string) =
        text.Split('\n')
        |> Array.exists (fun line ->
            let trimmed = line.TrimStart()

            not (trimmed.StartsWith "//")
            && spawnOrMutateMarkers |> List.exists trimmed.Contains)

    let private isTestModule (text: string) =
        text.Contains "[<Fact>]" || text.Contains "[<Theory>]"

    // FR-001 / research Decision 1: every process-spawning or env-mutating test module is in
    // the ProcessGlobalEnv collection. Reads this assembly's own committed sources.
    [<Fact>]
    let ``every process-spawning or env-mutating module is in the ProcessGlobalEnv collection`` () =
        let offenders =
            Directory.EnumerateFiles(sourceDir, "*.fs")
            |> Seq.filter (fun path -> not (Set.contains (fileName path) exemptFiles))
            |> Seq.choose (fun path ->
                let text = File.ReadAllText path

                if isTestModule text && usesMarker text && not (text.Contains collectionAttribute) then
                    Some(fileName path)
                else
                    None)
            |> Seq.toList

        Assert.True(
            List.isEmpty offenders,
            $"""These test modules spawn a PATH-resolved process or mutate process-global env but are
not in the ProcessGlobalEnv collection (add {collectionAttribute}); the env-mutation race can
return through them: {String.concat ", " offenders}"""
        )

    // Sanity: the guard is actually scanning real sources (not a silently-empty directory),
    // so a future refactor that moves/renames files can't make the guard vacuously pass. Covers
    // both spawn shapes: the low-level markers (ScaffoldCommandTests: Process.Start + env mutation)
    // and the interpreter path (ScaffoldCliCoherenceTests: spawns `dotnet new` via `runScaffold`,
    // with NONE of the low-level markers — the case the guard previously could not enforce).
    [<Fact>]
    let ``guard scans the known process-spawning modules`` () =
        let scaffold = File.ReadAllText(Path.Combine(sourceDir, "ScaffoldCommandTests.fs"))
        Assert.True(usesMarker scaffold, "Expected ScaffoldCommandTests to still trip the marker scan.")
        Assert.Contains(collectionAttribute, scaffold)

        let coherence =
            File.ReadAllText(Path.Combine(sourceDir, "ScaffoldCliCoherenceTests.fs"))

        Assert.True(
            usesMarker coherence,
            "Expected ScaffoldCliCoherenceTests (interpreter spawn path, no low-level marker) to trip the scan."
        )

        Assert.Contains(collectionAttribute, coherence)
