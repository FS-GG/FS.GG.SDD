namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module RegistryValidatorTests =

    let private coherent: Registry.RegistryModel =
        { Components =
            [ { Id = "FS.GG.Contracts"
                Version = "1.0.0" }
              { Id = "FS.GG.SDD"; Version = "0.2.0" } ]
          Edges =
            [ { Consumer = "FS.GG.SDD"
                Provider = "FS.GG.Contracts"
                CompatibleRange = ">=1.0.0 <2.0.0" } ] }

    let private rulesOf result =
        match result with
        | Registry.Valid -> []
        | Registry.Invalid diagnostics -> diagnostics |> List.map (fun d -> d.Rule)

    // SC-007 / Scenario E: a coherent model validates with no diagnostics.
    [<Fact>]
    let ``coherent model is Valid`` () =
        Assert.Equal(Registry.Valid, Registry.validate coherent)

    [<Fact>]
    let ``incoherent model (range excludes declared version) reports IncompatibleVersion naming the edge`` () =
        let model =
            { coherent with
                Edges =
                    [ { Consumer = "FS.GG.SDD"
                        Provider = "FS.GG.Contracts"
                        CompatibleRange = ">=2.0.0" } ] }

        match Registry.validate model with
        | Registry.Invalid [ d ] ->
            Assert.Equal(Registry.IncompatibleVersion, d.Rule)
            Assert.Equal("FS.GG.SDD -> FS.GG.Contracts", d.Entry)
        | other -> Assert.True(false, $"expected one IncompatibleVersion diagnostic, got {other}")

    [<Fact>]
    let ``incomplete component (missing required field) reports MissingField naming the entry`` () =
        let model =
            { coherent with
                Components =
                    [ { Id = "FS.GG.Contracts"; Version = "" }
                      { Id = "FS.GG.SDD"; Version = "0.2.0" } ] }

        match Registry.validate model with
        | Registry.Invalid diagnostics ->
            let d =
                diagnostics
                |> List.find (fun d ->
                    match d.Rule with
                    | Registry.MissingField _ -> true
                    | _ -> false)

            Assert.Equal(Registry.MissingField "Version", d.Rule)
            Assert.Equal("FS.GG.Contracts", d.Entry)
        | other -> Assert.True(false, $"expected a MissingField diagnostic, got {other}")

    [<Fact>]
    let ``edge to an absent component reports UnknownComponent`` () =
        let model =
            { coherent with
                Edges =
                    [ { Consumer = "FS.GG.SDD"
                        Provider = "FS.GG.Absent"
                        CompatibleRange = ">=1.0.0" } ] }

        Assert.Contains(Registry.UnknownComponent, rulesOf (Registry.validate model))

    [<Fact>]
    let ``non-SemVer version reports MalformedVersion`` () =
        let model =
            { coherent with
                Components =
                    [ { Id = "FS.GG.Contracts"
                        Version = "not-a-version" }
                      { Id = "FS.GG.SDD"; Version = "0.2.0" } ] }

        Assert.Contains(Registry.MalformedVersion, rulesOf (Registry.validate model))

    [<Fact>]
    let ``non-SemVer range reports MalformedVersion`` () =
        let model =
            { coherent with
                Edges =
                    [ { Consumer = "FS.GG.SDD"
                        Provider = "FS.GG.Contracts"
                        CompatibleRange = ">=garbage" } ] }

        Assert.Contains(Registry.MalformedVersion, rulesOf (Registry.validate model))

    [<Fact>]
    let ``blank edge field reports MissingField naming the field`` () =
        let model =
            { coherent with
                Edges =
                    [ { Consumer = ""
                        Provider = "FS.GG.Contracts"
                        CompatibleRange = ">=1.0.0 <2.0.0" } ] }

        match Registry.validate model with
        | Registry.Invalid diagnostics ->
            Assert.Contains(Registry.MissingField "Consumer", diagnostics |> List.map (fun d -> d.Rule))
        | other -> Assert.True(false, $"expected a MissingField diagnostic, got {other}")
