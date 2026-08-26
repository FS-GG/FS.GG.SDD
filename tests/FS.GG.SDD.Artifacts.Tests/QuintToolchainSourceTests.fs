namespace FS.GG.SDD.Artifacts.Tests

open System
open System.Text
open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module QuintToolchainSourceTests =
    let private codes (findings: SpecificationDiagnostic list) = findings |> List.map _.Code

    let private expectCode expected findings =
        Assert.Contains(expected, codes findings)

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error findings -> failwithf "expected success, got %A" findings

    let private expectError result =
        match result with
        | Ok value -> failwithf "expected diagnostics, got %A" value
        | Error findings -> findings

    let private exactCache () =
        QuintToolchain.q1.Components
        |> List.collect _.Objects
        |> List.map (fun requirement ->
            { Id = requirement.Id
              Kind = requirement.Kind
              State = Present(requirement.Sha256, requirement.Bytes, true) })

    let private replaceCache id state (observations: QuintCacheObservation list) =
        observations
        |> List.map (fun observation ->
            if observation.Id = id then
                { observation with State = state }
            else
                observation)

    let private processRequest arguments environment =
        { StepId = "extract"
          ExecutableObjectId = "lmt-binary"
          Arguments = arguments
          Environment = environment
          WorkingDirectory = "run-1" }

    let private sourceFixture () =
        let bytes = Encoding.UTF8.GetBytes("```quint Main.qnt +=\nmodule Main {\n}\n```\n")
        QuintSource.createMarkdown "specs/main.md" bytes |> expectOk

    let private fenceManifest (source: QuintMarkdownSource) =
        { Schema = QuintSource.fenceManifestSchema
          SourcePath = source.Path
          SourceSha256 = source.Sha256
          Fences =
            [ { Ordinal = 0
                Target = "Main.qnt"
                ModuleName = "Main"
                SourceRange =
                  { Path = source.Path
                    Start = { Line = 1; Column = 1 }
                    End = { Line = 4; Column = 3 } }
                ContentSha256 = String.replicate 64 "0" } ] }

    let private sourceMap (source: QuintMarkdownSource) =
        { Schema = QuintSource.sourceMapSchema
          SourceSha256 = source.Sha256
          Entries =
            [ { Target = "Main.qnt"
                GeneratedRange =
                  { Path = "Main.qnt"
                    Start = { Line = 1; Column = 1 }
                    End = { Line = 1; Column = 10 } }
                Source =
                  { FenceOrdinal = 0
                    Range =
                      { Path = source.Path
                        Start = { Line = 2; Column = 1 }
                        End = { Line = 3; Column = 1 } } } } ] }

    [<Fact>]
    let ``exact Q1 manifest and complete offline cache validate deterministically`` () =
        let cache = exactCache ()

        Assert.Empty(QuintToolchain.validateManifest QuintToolchain.q1)
        Assert.Empty(QuintToolchain.validateCache QuintToolchain.q1 cache)

        Assert.Equal<byte array>(
            QuintToolchain.encodeCanonical QuintToolchain.q1,
            QuintToolchain.encodeCanonical QuintToolchain.q1
        )

        Assert.Equal(64, QuintToolchain.fingerprint QuintToolchain.q1 |> String.length)

    [<Fact>]
    let ``cache absence unreadability mismatch and incompleteness stay distinct`` () =
        let cache = exactCache ()

        let absent = cache |> List.filter (fun item -> item.Id <> "quint-binary")

        let unreadable =
            replaceCache "quint-binary" (QuintCacheObjectState.Unreadable "permission denied") cache

        let mismatch =
            replaceCache
                "quint-binary"
                (QuintCacheObjectState.Present(String.replicate 64 "f", Some 125661253L, true))
                cache

        let incomplete =
            replaceCache
                "apalache-tree"
                (QuintCacheObjectState.Present(String.replicate 64 "0", Some 136014794L, false))
                cache

        QuintToolchain.validateCache QuintToolchain.q1 absent
        |> expectCode "QUINT-CACHE-OBJECT-ABSENT"

        QuintToolchain.validateCache QuintToolchain.q1 unreadable
        |> expectCode "QUINT-CACHE-OBJECT-UNREADABLE"

        QuintToolchain.validateCache QuintToolchain.q1 mismatch
        |> expectCode "QUINT-CACHE-OBJECT-DIGEST-MISMATCH"

        QuintToolchain.validateCache QuintToolchain.q1 incomplete
        |> expectCode "QUINT-CACHE-OBJECT-INCOMPLETE"

    [<Fact>]
    let ``moving tool and guidance identities are refused`` () =
        let movingTools =
            QuintToolchain.q1.Components
            |> List.map (fun toolComponent ->
                if toolComponent.Id = "lmt" then
                    { toolComponent with
                        Version = "latest"
                        Source = "github:driusan/lmt@main" }
                else
                    toolComponent)

        let movingGuidance =
            QuintToolchain.q1.Guidance
            |> Option.map (fun guidance ->
                { guidance with
                    Source = "quint-co/quint-llm-kit@main" })

        let findings =
            QuintToolchain.validateManifest
                { QuintToolchain.q1 with
                    Components = movingTools
                    Guidance = movingGuidance }

        expectCode "QUINT-TOOLCHAIN-COMPONENT-MISMATCH" findings
        expectCode "QUINT-GUIDANCE-IDENTITY-MISMATCH" findings

    [<Fact>]
    let ``compilation plan cannot express acquisition proxy or network inputs`` () =
        let request =
            processRequest [ "https://example.invalid/quint"; "quint@latest" ] [ "HTTP_PROXY", "https://proxy.invalid" ]

        let findings =
            QuintToolchain.plan QuintToolchain.q1 (exactCache ()) [ request ] |> expectError

        expectCode "QUINT-PLAN-ACQUISITION-REFUSED" findings
        expectCode "QUINT-PLAN-NETWORK-ENVIRONMENT-REFUSED" findings

    [<Fact>]
    let ``execution reports occupied endpoint and failed process independently`` () =
        let plan =
            QuintToolchain.plan QuintToolchain.q1 (exactCache ()) [ processRequest [ "specs/main.md" ] [] ]
            |> expectOk

        let findings =
            QuintToolchain.validateExecution
                plan
                (Occupied "127.0.0.1:8822")
                [ { StepId = "extract"
                    Outcome = Failed(23, "warnings treated as errors") } ]

        expectCode "QUINT-EXECUTION-ENDPOINT-OCCUPIED" findings
        expectCode "QUINT-EXECUTION-PROCESS-FAILED" findings

    [<Fact>]
    let ``canonical Markdown fences extraction and source map validate and round trip`` () =
        let source = sourceFixture ()
        let manifest = fenceManifest source
        let sourceMap = sourceMap source

        let generated =
            [ { Target = "Main.qnt"
                Sha256 = String.replicate 64 "a"
                Bytes = 42L } ]

        Assert.Empty(QuintSource.validateManifest source manifest)

        Assert.Empty(
            QuintSource.validateExtraction
                source
                manifest
                { First = generated
                  Second = generated
                  Warnings = [] }
        )

        Assert.Empty(QuintSource.validateSourceMap source manifest sourceMap)

        let encoded = QuintSource.encodeSourceMap sourceMap
        Assert.Equal(sourceMap, QuintSource.decodeSourceMap encoded |> expectOk)
        Assert.Equal<byte array>(encoded, QuintSource.encodeSourceMap sourceMap)

        Assert.Equal(
            Some sourceMap.Entries.Head.Source,
            QuintSource.tryResolve "Main.qnt" { Line = 1; Column = 5 } sourceMap
        )

    [<Fact>]
    let ``Markdown and fence mutations fail with stable source diagnostics`` () =
        QuintSource.createMarkdown "../main.md" (Encoding.UTF8.GetBytes("module Main {}\n"))
        |> expectError
        |> expectCode "QUINT-SOURCE-PATH-UNSAFE"

        QuintSource.createMarkdown "specs/main.md" (Encoding.UTF8.GetBytes("line one\r\n"))
        |> expectError
        |> expectCode "QUINT-SOURCE-LINE-ENDINGS-NONCANONICAL"

        let bom =
            Array.concat [ [| 0xEFuy; 0xBBuy; 0xBFuy |]; Encoding.UTF8.GetBytes("module Main {}\n") ]

        QuintSource.createMarkdown "specs/main.md" bom
        |> expectError
        |> expectCode "QUINT-SOURCE-BOM-REFUSED"

        let source = sourceFixture ()
        let manifest = fenceManifest source

        let unsafeFence =
            { manifest.Fences.Head with
                Target = "../Main.qnt"
                ContentSha256 = "moving" }

        let findings =
            QuintSource.validateManifest
                source
                { manifest with
                    Fences = [ unsafeFence ] }

        expectCode "QUINT-FENCE-TARGET-UNSAFE" findings
        expectCode "QUINT-FENCE-CONTENT-DIGEST-INVALID" findings

    [<Fact>]
    let ``extraction and source map mutations cannot masquerade as canonical output`` () =
        let source = sourceFixture ()
        let manifest = fenceManifest source
        let sourceMap = sourceMap source

        let first =
            [ { Target = "Main.qnt"
                Sha256 = String.replicate 64 "a"
                Bytes = 42L } ]

        let second =
            [ { Target = "Main.qnt"
                Sha256 = String.replicate 64 "b"
                Bytes = 42L } ]

        let extractionFindings =
            QuintSource.validateExtraction
                source
                manifest
                { First = first
                  Second = second
                  Warnings = [ "unexpected fence attribute" ] }

        expectCode "QUINT-EXTRACTION-WARNING" extractionFindings
        expectCode "QUINT-EXTRACTION-NONDETERMINISTIC" extractionFindings

        let staleMap =
            { sourceMap with
                SourceSha256 = String.replicate 64 "f"
                Entries =
                    [ { sourceMap.Entries.Head with
                          Target = "Other.qnt"
                          GeneratedRange =
                              { sourceMap.Entries.Head.GeneratedRange with
                                  Path = "Main.qnt" }
                          Source =
                              { sourceMap.Entries.Head.Source with
                                  FenceOrdinal = 99 } } ] }

        let mapFindings = QuintSource.validateSourceMap source manifest staleMap
        expectCode "QUINT-SOURCE-MAP-DIGEST-MISMATCH" mapFindings
        expectCode "QUINT-SOURCE-MAP-GENERATED-PATH-MISMATCH" mapFindings
        expectCode "QUINT-SOURCE-MAP-FENCE-UNKNOWN" mapFindings

        let nonCanonical =
            Array.append [| byte ' ' |] (QuintSource.encodeSourceMap sourceMap)

        QuintSource.decodeSourceMap nonCanonical
        |> expectError
        |> expectCode "QUINT-SOURCE-MAP-NONCANONICAL"
