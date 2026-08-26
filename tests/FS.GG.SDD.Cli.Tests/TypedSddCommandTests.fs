namespace FS.GG.SDD.Cli.Tests

open System
open System.Diagnostics
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

[<Collection("ProcessGlobalEnv")>]
module TypedSddCommandTests =
    module Commands = FS.GG.SDD.Commands.Tests.TestSupport

    let private configuration =
        if AppContext.BaseDirectory.Replace('\\', '/').Contains("/Release/") then
            "Release"
        else
            "Debug"

    let private apphost =
        Path.Combine(Commands.repoRoot, "src", "FS.GG.SDD.Cli", "bin", configuration, "net10.0", "FS.GG.SDD.Cli")

    let private run root args =
        let start = ProcessStartInfo(apphost)
        start.WorkingDirectory <- root
        start.RedirectStandardOutput <- true
        start.RedirectStandardError <- true
        start.UseShellExecute <- false
        args |> List.iter start.ArgumentList.Add

        use child =
            Process.Start start
            |> Option.ofObj
            |> Option.defaultWith (fun () -> failwith "CLI did not start")

        let stdout = child.StandardOutput.ReadToEnd()
        let stderr = child.StandardError.ReadToEnd()
        Assert.True(child.WaitForExit 30000, "CLI timed out")
        child.ExitCode, stdout, stderr

    let private inTemp body =
        let root =
            Path.Combine(Path.GetTempPath(), "fsgg-typed-sdd-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory root |> ignore

        try
            body root
        finally
            Directory.Delete(root, true)

    [<Fact>]
    let ``author requires an agent receipt and writes nothing when unavailable`` () =
        inTemp (fun root ->
            let code, stdout, _ =
                run root [ "typed-sdd"; "author"; "--root"; root; "--work"; "demo" ]

            Assert.Equal(1, code)
            Assert.Contains("typedSdd.authoringAgentUnavailable", stdout)
            Assert.False(Directory.Exists(Path.Combine(root, "work"))))

    [<Fact>]
    let ``author inspect and direct-edit diagnostic form one stable authority flow`` () =
        inTemp (fun root ->
            let code, _, _ =
                run
                    root
                    [ "typed-sdd"
                      "author"
                      "--root"
                      root
                      "--work"
                      "demo"
                      "--title"
                      "Demo"
                      "--agent"
                      "tern"
                      "--session"
                      "s1" ]

            Assert.Equal(0, code)

            let inspectCode, inspect, _ =
                run root [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo" ]

            Assert.Equal(0, inspectCode)
            Assert.Contains("\"outcome\": \"succeeded\"", inspect)
            let source = Path.Combine(root, "work", "demo", "specification.fsx")
            File.AppendAllText(source, "\n// direct edit\n")

            let editCode, edited, _ =
                run root [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo" ]

            Assert.Equal(1, editCode)
            Assert.Contains("typedSdd.directCanonicalEdit", edited))

    [<Fact>]
    let ``inspect dispatches explicit manifest v2 and rejects edited Quint artifacts`` () =
        inTemp (fun root ->
            let expectOk result =
                match result with
                | Ok value -> value
                | Error findings -> failwithf "expected success, got %A" findings
            let markdown = Text.Encoding.UTF8.GetBytes "# specification\n"
            let source = QuintSource.createMarkdown "work/demo/specification.md" markdown |> expectOk
            let sourceRange =
                { Path = source.Path
                  Start = { Line = 1; Column = 1 }
                  End = { Line = 1; Column = 2 } }
            let contract =
                { Schema = QuintContract.schema
                  Profile = QuintProfile.identity
                  Specification = "DemoSpec"
                  Catalogue =
                    [ { Id = "STATE"; Kind = QuintCatalogueKind.StateVariable; Source = sourceRange }
                      { Id = "ADVANCE"; Kind = QuintCatalogueKind.Action; Source = sourceRange } ]
                  ActionEffects =
                    [ { ActionId = "ADVANCE"; Reads = [ "STATE" ]; Writes = [ "STATE" ]; Subjects = [ "STATE" ] } ]
                  Relationships = []; VerificationProfiles = []; Bounds = []; Impacts = []; Compatibility = []; Digests = [] }
            let contractBytes = QuintContract.serializeCanonical contract |> expectOk |> Text.Encoding.UTF8.GetBytes
            let fenceBytes =
                QuintSource.encodeFenceManifest
                    { Schema = QuintSource.fenceManifestSchema
                      SourcePath = source.Path
                      SourceSha256 = source.Sha256
                      Fences = [] }
            let sourceMapBytes =
                QuintSource.encodeSourceMap
                    { Schema = QuintSource.sourceMapSchema
                      SourceSha256 = source.Sha256
                      Entries = [] }
            let modulesDigest = String.replicate 64 "b"
            let toolchain = QuintToolchain.fingerprint QuintToolchain.q1
            let fingerprint =
                QuintContract.fingerprint
                    { SourceSha256 = source.Sha256
                      FenceManifestSha256 = TypedAuthorityManifest.sha256 fenceBytes
                      GeneratedModulesSha256 = modulesDigest
                      ToolchainSha256 = toolchain
                      Contract = contract }
                |> expectOk
            let receiptBytes =
                QuintCompiler.encodeReceipt
                    { Schema = QuintCompiler.receiptSchema
                      SourceSha256 = source.Sha256
                      FenceManifestSha256 = TypedAuthorityManifest.sha256 fenceBytes
                      GeneratedModulesSha256 = modulesDigest
                      ToolchainSha256 = toolchain
                      TypedEffectSha256 = String.replicate 64 "c"
                      ContractSha256 = TypedAuthorityManifest.sha256 contractBytes
                      CompilationFingerprint = fingerprint
                      ProcessSteps = [ "extract"; "typecheck" ] }
                |> Text.Encoding.UTF8.GetBytes
            let bindingsBytes =
                (QuintBindings.generate "DemoContract" contract |> expectOk).FSharpSource
                |> Text.Encoding.UTF8.GetBytes
            let content: (string * string * byte array) list =
                [ "markdown", "work/demo/specification.md", markdown
                  "fence-manifest", "readiness/demo/quint/fences.json", fenceBytes
                  "generated-modules", "readiness/demo/quint/modules.digest", Text.Encoding.UTF8.GetBytes(modulesDigest + "\n")
                  "source-map", "readiness/demo/quint/source-map.json", sourceMapBytes
                  "compiled-contract", "readiness/demo/quint/contract.json", contractBytes
                  "bindings", "readiness/demo/quint/bindings.fs", bindingsBytes
                  "compilation-receipt", "readiness/demo/quint/receipt.json", receiptBytes ]

            let artifacts =
                content
                |> List.map (fun (id, path, value) ->
                    let full = Path.Combine(root, path)
                    Path.GetDirectoryName full
                    |> Option.ofObj
                    |> Option.iter (fun directory -> Directory.CreateDirectory directory |> ignore)
                    File.WriteAllBytes(full, value)

                    { Id = id
                      Path = path
                      Sha256 = TypedAuthorityManifest.sha256 value })

            let authority =
                { SchemaVersion = 2
                  Lifecycle = "typed-sdd"
                  Backend = "quint-specification-v1"
                  ProfileIdentity = QuintProfile.identity
                  ToolchainIdentity = toolchain
                  PackageIdentity =
                    $"FS.GG.SDD.Artifacts/{SchemaVersion.currentGeneratorVersion().Version}"
                  Artifacts = artifacts
                  AuthoringAgent = "tern"
                  AuthoringSession = "v2"
                  RollbackManifestPath = None
                  RollbackManifestSha256 = None }

            let manifestPath = Path.Combine(root, TypedAuthorityManifest.path "demo")
            Path.GetDirectoryName manifestPath
            |> Option.ofObj
            |> Option.iter (fun directory -> Directory.CreateDirectory directory |> ignore)
            File.WriteAllText(manifestPath, TypedAuthority.serializeQuintV2 authority)

            let code, report, _ =
                run root [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo" ]

            Assert.Equal(0, code)
            Assert.Contains("quint-specification-v1", report)
            Assert.Contains("\"outcome\": \"succeeded\"", report)

            File.WriteAllText(Path.Combine(root, "readiness/demo/quint/contract.json"), "edited")
            let editCode, edited, _ =
                run root [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo" ]

            Assert.Equal(1, editCode)
            Assert.Contains("typedSdd.v2.artifactMismatch", edited))

    [<Fact>]
    let ``migration analysis classifies supported input and performs no preaccept write`` () =
        inTemp (fun root ->
            let target = Path.Combine(root, "work", "demo")
            Directory.CreateDirectory target |> ignore
            let source = Path.Combine(target, "spec.md")

            File.Copy(
                Path.Combine(Commands.repoRoot, "tests", "fixtures", "typed-specifications", "supported-spec.md"),
                source
            )

            let before = File.ReadAllBytes source

            let code, stdout, _ =
                run
                    root
                    [ "typed-sdd"
                      "migrate"
                      "--root"
                      root
                      "--work"
                      "demo"
                      "--source"
                      "work/demo/spec.md" ]

            Assert.Equal(0, code)
            Assert.Contains("\"classification\": \"Migrated\"", stdout)
            Assert.Equal<byte>(before, File.ReadAllBytes source)
            Assert.Single(Directory.GetFiles(target)) |> ignore)

    [<Fact>]
    let ``work and source traversal are rejected without writes outside root`` () =
        inTemp (fun root ->
            let parent =
                Path.GetDirectoryName root
                |> Option.ofObj
                |> Option.defaultWith (fun () -> failwith "temporary root has no parent")

            let escapeName = "escape-" + Guid.NewGuid().ToString("N")

            let code, stdout, _ =
                run
                    root
                    [ "typed-sdd"
                      "author"
                      "--root"
                      root
                      "--work"
                      "../" + escapeName
                      "--agent"
                      "a"
                      "--session"
                      "s" ]

            Assert.Equal(1, code)
            Assert.Contains("typedSdd.workInvalid", stdout)
            Assert.False(Directory.Exists(Path.Combine(parent, escapeName)))

            let migrateCode, migrate, _ =
                run
                    root
                    [ "typed-sdd"
                      "migrate"
                      "--root"
                      root
                      "--work"
                      "demo"
                      "--source"
                      "../outside.md" ]

            Assert.Equal(1, migrateCode)
            Assert.Contains("typedSdd.sourceEscapesRoot", migrate))

    [<Fact>]
    let ``accepted migration preserves semantic inventory and explicit rollback restores source`` () =
        inTemp (fun root ->
            let target = Path.Combine(root, "work", "demo")
            Directory.CreateDirectory target |> ignore
            let source = Path.Combine(target, "spec.md")

            File.Copy(
                Path.Combine(Commands.repoRoot, "tests", "fixtures", "typed-specifications", "supported-spec.md"),
                source
            )

            let before = File.ReadAllBytes source

            let code, migrated, _ =
                run
                    root
                    [ "typed-sdd"
                      "migrate"
                      "--root"
                      root
                      "--work"
                      "demo"
                      "--source"
                      "work/demo/spec.md"
                      "--accept" ]

            Assert.Equal(0, code)
            Assert.Contains("requirements:", migrated)
            Assert.True(File.Exists(Path.Combine(target, "spec.standard-sdd.rollback.md")))

            let rollbackCode, _, _ =
                run root [ "typed-sdd"; "rollback"; "--root"; root; "--work"; "demo"; "--accept" ]

            Assert.Equal(0, rollbackCode)
            Assert.Equal<byte>(before, File.ReadAllBytes source)
            Assert.False(File.Exists(Path.Combine(target, "specification.fsx")))
            Assert.False(File.Exists(Path.Combine(root, "readiness", "demo", "typed-authority.json"))))

    [<Fact>]
    let ``failed rollback restores the prior typed markdown authority`` () =
        if OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() then
            inTemp (fun root ->
                let target = Path.Combine(root, "work", "demo")
                Directory.CreateDirectory target |> ignore
                let source = Path.Combine(target, "spec.md")

                File.Copy(
                    Path.Combine(Commands.repoRoot, "tests", "fixtures", "typed-specifications", "supported-spec.md"),
                    source
                )

                let code, _, _ =
                    run
                        root
                        [ "typed-sdd"
                          "migrate"
                          "--root"
                          root
                          "--work"
                          "demo"
                          "--source"
                          "work/demo/spec.md"
                          "--accept" ]

                Assert.Equal(0, code)
                let typedMarkdown = File.ReadAllBytes source
                let readiness = Path.Combine(root, "readiness", "demo")
                let originalMode = File.GetUnixFileMode readiness

                try
                    File.SetUnixFileMode(readiness, UnixFileMode.UserRead ||| UnixFileMode.UserExecute)

                    let rollbackCode, report, _ =
                        run root [ "typed-sdd"; "rollback"; "--root"; root; "--work"; "demo"; "--accept" ]

                    Assert.Equal(1, rollbackCode)
                    Assert.Contains("typedSdd.rollbackFailed", report)
                finally
                    File.SetUnixFileMode(readiness, originalMode)

                Assert.Equal<byte>(typedMarkdown, File.ReadAllBytes source)
                Assert.True(File.Exists(Path.Combine(target, "specification.fsx")))
                Assert.True(File.Exists(Path.Combine(readiness, "typed-authority.json"))))

    [<Fact>]
    let ``unknown typed option fails closed`` () =
        inTemp (fun root ->
            let code, stdout, _ =
                run root [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo"; "--typo" ]

            Assert.Equal(1, code)
            Assert.Contains("typedSdd.unknownArgument", stdout)

            for malformed in
                [ [ "typed-sdd"; "inspect"; "--root"; root; "--work"; "demo"; "--work"; "again" ]
                  [ "typed-sdd"; "inspect"; "--root"; "--work"; "demo" ]
                  [ "typed-sdd"
                    "author"
                    "--root"
                    root
                    "--work"
                    "-h"
                    "--agent"
                    "a"
                    "--session"
                    "s" ] ] do
                let malformedCode, malformedReport, _ = run root malformed
                Assert.Equal(1, malformedCode)
                Assert.Contains("typedSdd.unknownArgument", malformedReport))

    [<Fact>]
    let ``shared lifecycle command blocks when typed authority projection is stale`` () =
        inTemp (fun root ->
            let initCode, _, _ = run root [ "init"; "--root"; root ]
            Assert.Equal(0, initCode)

            let provenancePath = Path.Combine(root, ScaffoldProvenance.provenancePath)

            let provenance =
                File.ReadAllText provenancePath
                |> ScaffoldProvenance.tryParse
                |> Option.defaultWith (fun () -> failwith "expected init provenance")

            File.WriteAllText(
                provenancePath,
                ScaffoldProvenance.serialize
                    { provenance with
                        EffectiveParameters = [ "lifecycle", "typed-sdd" ] }
            )

            let authorCode, _, _ =
                run
                    root
                    [ "typed-sdd"
                      "author"
                      "--root"
                      root
                      "--work"
                      "demo"
                      "--agent"
                      "a"
                      "--session"
                      "s" ]

            Assert.Equal(0, authorCode)
            let specificationPath = Path.Combine(root, "work", "demo", "spec.md")
            let specificationBefore = File.ReadAllBytes specificationPath
            File.AppendAllText(Path.Combine(root, "readiness", "demo", "specification.normalized.json"), " ")

            let commandCode, report, _ =
                run root [ "specify"; "--root"; root; "--work"; "demo" ]

            Assert.NotEqual(0, commandCode)
            Assert.Contains("typedSdd.staleProjection", report)
            Assert.Equal<byte>(specificationBefore, File.ReadAllBytes specificationPath))

    [<Fact>]
    let ``doctor and upgrade execute canonical F sharp and block a runtime failure`` () =
        inTemp (fun root ->
            let initCode, _, _ = run root [ "init"; "--root"; root ]
            Assert.Equal(0, initCode)

            let provenancePath = Path.Combine(root, ScaffoldProvenance.provenancePath)

            let provenance =
                File.ReadAllText provenancePath
                |> ScaffoldProvenance.tryParse
                |> Option.defaultWith (fun () -> failwith "expected init provenance")

            File.WriteAllText(
                provenancePath,
                ScaffoldProvenance.serialize
                    { provenance with
                        EffectiveParameters = [ "lifecycle", "typed-sdd" ] }
            )

            let authorCode, _, _ =
                run
                    root
                    [ "typed-sdd"
                      "author"
                      "--root"
                      root
                      "--work"
                      "demo"
                      "--agent"
                      "a"
                      "--session"
                      "s" ]

            Assert.Equal(0, authorCode)
            let canonicalPath = Path.Combine(root, "work", "demo", "specification.fsx")
            File.AppendAllText(canonicalPath, "\nfailwith \"runtime mutation\"\n")
            let canonicalBytes = File.ReadAllBytes canonicalPath
            let manifestPath = Path.Combine(root, TypedAuthorityManifest.path "demo")

            let manifest =
                File.ReadAllText manifestPath
                |> TypedAuthorityManifest.deserialize
                |> Result.defaultWith (fun finding -> failwith finding.Message)

            File.WriteAllText(
                manifestPath,
                TypedAuthorityManifest.serialize
                    { manifest with
                        CanonicalSha256 = TypedAuthorityManifest.sha256 canonicalBytes }
            )

            for command in [ "doctor"; "upgrade" ] do
                let args =
                    [ command; "--root"; root; "--work"; "demo" ]
                    @ if command = "upgrade" then [ "--yes" ] else []

                let code, report, _ = run root args
                Assert.NotEqual(0, code)
                Assert.Contains("typedSdd.compilationFailed", report))
