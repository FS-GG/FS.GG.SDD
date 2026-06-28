namespace FS.GG.Contracts.Tests

open System
open System.IO
open System.Text.Json
open Xunit

module DependencyClosureTests =

    // SC-004 / FR-002 / quickstart Scenario A: the package dependency closure
    // contains only FSharp.Core. Read the generated `.deps.json` and inspect the
    // resolved dependencies declared for the FS.GG.Contracts library.
    [<Fact>]
    let ``package closure contains only FSharp_Core`` () =
        let depsPath =
            Directory.EnumerateFiles(AppContext.BaseDirectory, "*.deps.json")
            |> Seq.head

        use doc = JsonDocument.Parse(File.ReadAllText depsPath)
        let targets = doc.RootElement.GetProperty("targets")

        // The single .NET target framework moniker (e.g. ".NETCoreApp,Version=v10.0").
        let framework = targets.EnumerateObject() |> Seq.head

        let contractsEntry =
            framework.Value.EnumerateObject()
            |> Seq.find (fun p -> p.Name.StartsWith("FS.GG.Contracts/", StringComparison.Ordinal))

        let dependencies =
            match contractsEntry.Value.TryGetProperty("dependencies") with
            | true, deps -> deps.EnumerateObject() |> Seq.map (fun p -> p.Name) |> Set.ofSeq
            | false, _ -> Set.empty

        Assert.Equal<Set<string>>(set [ "FSharp.Core" ], dependencies)

        // Explicitly assert the forbidden third-party packages never appear.
        for forbidden in [ "YamlDotNet"; "System.Text.Json"; "Spectre.Console" ] do
            Assert.DoesNotContain(forbidden, dependencies)
