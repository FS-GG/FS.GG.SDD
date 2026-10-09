namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.Tests
open Xunit

[<Collection("ProcessGlobalEnv")>]
module CatalogInspectionTests =
    let catalogBytes () =
        let authored =
            File.ReadAllText(
                Path.Combine(
                    TestSupport.repoRoot,
                    "tests/FS.GG.SDD.Artifacts.Tests/Fixtures/ProviderCatalog/five-providers.yml"
                )
            )

        let catalog =
            match ProviderCatalog.parse authored with
            | Ok c -> c
            | Error errors -> failwithf "%A" errors

        let descriptors =
            catalog.Providers
            |> List.map (fun d ->
                { d with
                    DescriptorDigest = ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes d)
                })

        let node = JsonNode.Parse authored |> nonNull

        node["digest"] <-
            JsonValue.Create(
                ProviderCatalogIntegrity.digest (
                    ProviderCatalogIntegrity.catalogBytes { catalog with Providers = descriptors }
                )
            )

        for providerNode in (node["providers"] |> nonNull).AsArray() do
            let provider = nonNull providerNode
            let id = (provider["id"] |> nonNull).GetValue<string>()

            provider["descriptorDigest"] <-
                JsonValue.Create((descriptors |> List.find (fun d -> d.Id = id)).DescriptorDigest)

        Encoding.UTF8.GetBytes(node.ToJsonString())

    let capture args =
        let previous = Console.Out
        use writer = new StringWriter()

        try
            Console.SetOut writer
            let code = FS.GG.SDD.Cli.Catalog.run args
            code, writer.ToString()
        finally
            Console.SetOut previous

    [<Fact>]
    let ``real file inspection discovers all provider metadata and writes nothing`` () =
        let root = TestSupport.tempDirectory ()
        let path = Path.Combine(root, "catalog.json")
        let bytes = catalogBytes ()
        File.WriteAllBytes(path, bytes)

        let args =
            [
                "inspect"
                "--catalog"
                path
                "--catalog-sha256"
                ProviderCatalogIntegrity.digest bytes
            ]

        let code, json = capture args
        Assert.Equal(0, code)
        use doc = JsonDocument.Parse json
        Assert.Equal("prepared", doc.RootElement.GetProperty("status").GetString())
        Assert.Equal(5, doc.RootElement.GetProperty("providers").GetArrayLength())
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("observations").ValueKind)
        Assert.Single(Directory.GetFiles(root)) |> ignore
        Assert.True((bytes = File.ReadAllBytes path))
        let textCode, text = capture (args @ [ "--text" ])
        let richCode, rich = capture (args @ [ "--rich" ])
        Assert.Equal(textCode, richCode)
        Assert.Equal(text, rich)
        Assert.False(rich.Contains("\u001b", StringComparison.Ordinal))

    [<Fact>]
    let ``raw digest unknown option and malformed parameter failures remain located`` () =
        for args, expected in
            [
                [ "inspect" ], "catalog.selectionMissing"
                [ "inspect"; "--catalog" ], "catalog.missingOptionValue"
                [ "inspect"; "--future" ], "catalog.unknownOption"
                [ "inspect"; "--catalog"; "a"; "--catalog"; "b" ], "catalog.duplicateOption"
            ] do
            let code, json = capture args
            Assert.Equal(1, code)
            Assert.Contains(expected, json)

    [<Fact>]
    let ``catalog scaffold incomplete selections refuse before filesystem or process effects`` () =
        let root = TestSupport.tempDirectory ()
        let before = Directory.GetFileSystemEntries(root)
        let previous = Console.Out
        use writer = new StringWriter()

        try
            Console.SetOut writer
            let code = Program.run [ "scaffold"; "--catalog"; "nonexistent"; "--root"; root ]
            Assert.Equal(1, code)
            Assert.Contains("catalog.selectionMissing", writer.ToString())
        finally
            Console.SetOut previous

        Assert.True((before = Directory.GetFileSystemEntries root))

    let scaffoldArgs =
        [
            "--catalog"
            "/selected/catalog.json"
            "--catalog-sha256"
            "sha256:catalog"
            "--provider"
            "fifth"
            "--root"
            "/selected/new-root"
            "--template-archive"
            "/selected/template.nupkg"
            "--template-sha256"
            "sha256:archive"
            "--admission-policy"
            "/selected/policy.json"
            "--admission-policy-sha256"
            "sha256:policy"
            "--platform"
            "linux-x64"
            "--transport-executable"
            "/selected/dotnet"
            "--preflight-timeout-seconds"
            "10"
            "--scaffold-timeout-seconds"
            "60"
        ]

    [<Fact>]
    let ``synthetic scaffold selections preserve literal values without reads`` () =
        match
            FS.GG.SDD.Cli.Catalog.parseScaffoldOptions (
                scaffoldArgs
                @ [ "--param"; "empty="; "--param"; "literal=a b;$x=tail"; "--dry-run" ]
            )
        with
        | Error errors -> failwithf "%A" errors
        | Ok options ->
            Assert.Equal("/selected/new-root", options.TargetRoot)
            Assert.Equal("/selected/dotnet", options.TransportExecutable)
            Assert.Equal(10, options.PreflightSeconds)
            Assert.Equal(60, options.ScaffoldSeconds)
            Assert.True(options.DryRun)
            Assert.Equal<(string * string) list>([ ("empty", ""); ("literal", "a b;$x=tail") ], options.Overrides)

    [<Fact>]
    let ``closed scaffold parser rejects duplicates unknown missing and invalid budgets`` () =
        for args, expected in
            [
                scaffoldArgs @ [ "--future" ], "catalog.unknownOption"
                scaffoldArgs @ [ "--root"; "/other" ], "catalog.duplicateOption"
                scaffoldArgs @ [ "--dry-run"; "--dry-run" ], "catalog.duplicateOption"
                scaffoldArgs @ [ "--param" ], "catalog.missingOptionValue"
                scaffoldArgs @ [ "--param"; "=bad" ], "catalog.parameterMalformed"
                scaffoldArgs @ [ "--param"; "x=a"; "--param"; "x=b" ], "catalog.duplicateParameter"
                [ "--root"; "--catalog"; "missing" ], "catalog.missingOptionValue"
                [ "--catalog"; "missing" ], "catalog.selectionMissing"
            ] do
            match FS.GG.SDD.Cli.Catalog.parseScaffoldOptions args with
            | Ok _ -> failwithf "Expected %s" expected
            | Error errors -> Assert.Contains(errors, fun error -> error.Code = expected)

        for value in [ "0"; "-1"; "1.5"; "2147483648" ] do
            let args =
                scaffoldArgs |> List.map (fun item -> if item = "10" then value else item)

            match FS.GG.SDD.Cli.Catalog.parseScaffoldOptions args with
            | Ok _ -> failwith "Expected positive integral budget refusal"
            | Error errors -> Assert.Contains(errors, fun error -> error.Code = "catalog.invalidBudget")

    [<Fact>]
    let ``every required scaffold selection is checked before selected file reads`` () =
        let pairs = scaffoldArgs |> List.chunkBySize 2

        for omitted in 0 .. pairs.Length - 1 do
            let args =
                pairs
                |> List.mapi (fun index pair -> index, pair)
                |> List.filter (fun (index, _) -> index <> omitted)
                |> List.collect snd

            match FS.GG.SDD.Cli.Catalog.parseScaffoldOptions args with
            | Ok _ -> failwith "Expected missing explicit selection"
            | Error errors -> Assert.Contains(errors, fun error -> error.Code = "catalog.selectionMissing")

    [<Fact>]
    let ``catalog only flags without catalog cannot fall through legacy scaffold`` () =
        let previous = Console.Out
        use writer = new StringWriter()

        try
            Console.SetOut writer
            let code = Program.run [ "scaffold"; "--admission-policy"; "nonexistent-policy" ]
            Assert.Equal(1, code)
            Assert.Contains("catalog.selectionMissing", writer.ToString())
        finally
            Console.SetOut previous

    [<Fact>]
    let ``selected metadata capture refuses unreadable and oversized files before runtime`` () =
        let root = TestSupport.tempDirectory ()
        let oversized = Path.Combine(root, "oversized-catalog.json")
        File.WriteAllBytes(oversized, Array.zeroCreate<byte>(1024 * 1024 + 1))

        for path in [ Path.Combine(root, "missing-catalog.json"); oversized ] do
            let previous = Console.Out
            use writer = new StringWriter()

            try
                Console.SetOut writer

                let args =
                    scaffoldArgs
                    |> List.map (fun value -> if value = "/selected/catalog.json" then path else value)

                let code = FS.GG.SDD.Cli.Catalog.scaffold args
                Assert.Equal(1, code)
                Assert.Contains("catalog.unreadable", writer.ToString())
                Assert.Contains("not-started", writer.ToString())
                Assert.False(Directory.Exists(Path.Combine(root, "new-root")))
            finally
                Console.SetOut previous

    [<Fact>]
    let ``selected runtime paths are lexical refusals before any read`` () =
        for original in [ "/selected/new-root"; "/selected/template.nupkg"; "/selected/dotnet" ] do
            let args =
                scaffoldArgs
                |> List.map (fun item -> if item = original then "relative-path" else item)

            match FS.GG.SDD.Cli.Catalog.parseScaffoldOptions args with
            | Ok _ -> failwith "Expected explicit absolute path refusal"
            | Error errors -> Assert.Contains(errors, fun error -> error.Code = "catalog.invalidPath")
