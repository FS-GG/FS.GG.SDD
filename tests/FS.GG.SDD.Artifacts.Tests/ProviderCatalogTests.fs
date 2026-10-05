namespace FS.GG.SDD.Artifacts.Tests

open System.IO
open Fsgg.ProviderCatalog
open Xunit

module ProviderCatalogTests =
    // Synthetic authored declaration; no template/tool is installed or executed by this fixture.
    let private fixture () =
        File.ReadAllText(
            Path.Combine(
                TestSupport.repoRoot,
                "tests/FS.GG.SDD.Artifacts.Tests/Fixtures/ProviderCatalog/five-providers.yml"
            )
        )

    let private parse text =
        FS.GG.SDD.Artifacts.ProviderCatalog.parse text

    let private expectCatalog () =
        match parse (fixture ()) with
        | Ok c -> c
        | Error e -> failwithf "%A" e

    let private refused text =
        match parse text with
        | Error errors -> Assert.NotEmpty errors
        | Ok _ -> failwith "Expected refusal."

    [<Fact>]
    let ``five golden providers resolve via data and retain command argv`` () =
        let catalog = expectCatalog ()
        Assert.Equal(5, catalog.Providers.Length)

        for d in catalog.Providers do
            match resolve catalog d.Id [ "raw", "Awkward name!?"; "package", "example.org/explicit/module" ] with
            | Error errors -> failwithf "%A" errors
            | Ok result ->
                Assert.Equal("Awkward name!?", result.RawProductName)
                Assert.Equal("example.org/explicit/module", result.PackageIdentity)

                match result.Descriptor.Capabilities.Head.Binding with
                | SemanticOnly -> failwith "Expected command declaration."
                | Command(command, limits) ->
                    Assert.Equal<string list>([ "test"; "--literal"; "value with spaces" ], command.Arguments)
                    Assert.Equal(30, limits.TimeoutSeconds)

    [<Fact>]
    let ``schema protocol type field scalar and multiple document failures refuse`` () =
        let text = fixture ()

        [
            text.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1")
            text.Replace("3.0.0", "3.1.0")
            text.Replace("\"kind\": \"string\"", "\"kind\": \"future-kind\"")
            text.Replace("\"displayName\"", "\"unknownRequiredField\"")
            text.Replace("\"required\": true", "\"required\": \"maybe\"")
            text + "---\nschemaVersion: 2\n"
        ]
        |> List.iter refused

    [<Fact>]
    let ``duplicate and dangling declaration mutations refuse`` () =
        let catalog = expectCatalog ()
        let d = catalog.Providers.Head

        let variants =
            [
                { catalog with
                    Providers = d :: catalog.Providers
                }
                { catalog with
                    Providers =
                        [
                            { d with
                                Parameters = d.Parameters @ d.Parameters
                            }
                        ]
                }
                { catalog with
                    Providers = [ { d with Tools = d.Tools @ d.Tools } ]
                }
                { catalog with
                    Providers =
                        [
                            { d with
                                Evidence = d.Evidence @ d.Evidence
                            }
                        ]
                }
                { catalog with
                    Providers =
                        [
                            { d with
                                Capabilities =
                                    [
                                        { d.Capabilities.Head with
                                            ToolIds = [ "missing" ]
                                        }
                                    ]
                            }
                        ]
                }
                { catalog with
                    Providers =
                        [
                            { d with
                                Identities =
                                    { d.Identities with
                                        PackageIdentity = "missing"
                                    }
                            }
                        ]
                }
            ]

        for changed in variants do
            Assert.NotEmpty(validate changed)

    [<Fact>]
    let ``enum exact version and hidden defaults refuse and ordering is deterministic`` () =
        let catalog = expectCatalog ()

        for inputs in [ [ "raw", "Name"; "mode", "unknown" ]; [ "raw", "Name"; "version", "latest" ] ] do
            Assert.True(Result.isError (resolve catalog "typescript-cli" inputs))

        let d = catalog.Providers.Head

        let invalid =
            { d with
                Parameters =
                    d.Parameters
                    |> List.map (fun p ->
                        if p.Key = "version" then
                            { p with Default = Some "latest" }
                        else
                            p)
            }

        Assert.True(
            Result.isError (resolve { catalog with Providers = [ invalid ] } d.Id [ "raw", "Name"; "version", "1.22" ])
        )

        Assert.Equal(
            resolve catalog d.Id [ "raw", "Name" ],
            resolve
                { catalog with
                    Providers = List.rev catalog.Providers
                }
                d.Id
                [ "raw", "Name" ]
        )
