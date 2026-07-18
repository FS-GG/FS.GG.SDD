namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts.DependencySurface
open Xunit

/// Feature 105, Phase 2 (ADR-0004 D2). The `dependency-surface` capture artifact model:
/// canonical construction, content-addressing, deterministic serialization, and round-trip
/// parse. The restore + surface read that *produces* the symbol set is an edge concern
/// (`ReadPackageSurface`); this suite pins the pure, committed-artifact contract.
module DependencySurfaceTests =

    [<Fact>]
    let ``create sorts, deduplicates, and stamps the content digest`` () =
        let capture =
            create "FS.GG.UI.SkiaViewer" "0.12.0" "nuget-cache" [ "M.b"; "M.a"; "M.b"; "M.a" ]

        Assert.Equal<string list>([ "M.a"; "M.b" ], capture.Symbols)
        Assert.Equal(schemaVersion, capture.SchemaVersion)
        Assert.Equal(symbolDigest [ "M.a"; "M.b" ], capture.Sha256)

    [<Fact>]
    let ``symbolDigest is order- and duplicate-independent`` () =
        Assert.Equal(symbolDigest [ "a"; "b"; "c" ], symbolDigest [ "c"; "b"; "a"; "b" ])

    [<Fact>]
    let ``symbolDigest changes when a symbol changes`` () =
        Assert.NotEqual<string>(symbolDigest [ "a"; "b" ], symbolDigest [ "a"; "c" ])

    [<Fact>]
    let ``capturePath is structural and forward-slashed`` () =
        Assert.Equal(
            "docs/dependency-surface/FS.GG.UI.SkiaViewer/0.12.0.json",
            capturePath defaultBaselineRoot "FS.GG.UI.SkiaViewer" "0.12.0"
        )

    [<Fact>]
    let ``serialize is deterministic, sorted, and LF-terminated`` () =
        let capture = create "Pkg" "1.0.0" "nuget-cache" [ "Z.z"; "A.a" ]
        let json = serialize capture

        Assert.EndsWith("\n", json)
        Assert.DoesNotContain("\r\n", json)
        // Sorted symbol order in the emitted bytes.
        Assert.True(json.IndexOf "A.a" < json.IndexOf "Z.z")
        // Byte-identical across repeated serialization of the same capture.
        Assert.Equal(json, serialize capture)

    [<Fact>]
    let ``serialize then tryParse round-trips`` () =
        let capture =
            create
                "FS.GG.UI.SkiaViewer"
                "0.12.0"
                "https://feed"
                [ "SkiaViewer.runApp"; "SkiaViewer.runAppWithPersistence" ]

        match tryParse (serialize capture) with
        | Ok parsed -> Assert.Equal(capture, parsed)
        | Error message -> Assert.Fail $"expected round-trip, got: {message}"

    [<Fact>]
    let ``tryParse rejects malformed JSON`` () =
        match tryParse "{ not json" with
        | Error message -> Assert.Contains("malformed JSON", message)
        | Ok _ -> Assert.Fail "expected a parse error"

    [<Fact>]
    let ``tryParse rejects a missing schemaVersion`` () =
        match tryParse """{ "packageId": "P", "version": "1.0.0", "sha256": "abc", "symbols": [] }""" with
        | Error message -> Assert.Contains("schemaVersion", message)
        | Ok _ -> Assert.Fail "expected a parse error"

    [<Fact>]
    let ``tryParse rejects a missing required field`` () =
        match tryParse """{ "schemaVersion": 1, "version": "1.0.0", "sha256": "abc", "symbols": [] }""" with
        | Error message -> Assert.Contains("packageId", message)
        | Ok _ -> Assert.Fail "expected a parse error"

    [<Fact>]
    let ``symbolSet exposes the captured symbols for an existence oracle`` () =
        let capture = create "Pkg" "1.0.0" "nuget-cache" [ "M.a"; "M.b" ]
        let symbols = symbolSet capture

        Assert.True(Set.contains "M.a" symbols)
        Assert.False(Set.contains "M.missing" symbols)

    [<Fact>]
    let ``symbolsFromAssembly reads a loaded assembly's public module surface`` () =
        // Reflect over this very artifacts assembly: `DependencySurface` is a public module
        // exporting `create`, so `DependencySurface.create` must appear as a module-qualified
        // member symbol — the same `<Module>.<member>` form a `framework:` reference cites.
        let assembly = typeof<DependencySurfaceCapture>.Assembly
        let symbols = symbolsFromAssembly assembly |> Set.ofList

        Assert.Contains("DependencySurface.create", symbols)
        Assert.Contains("DependencySurface.serialize", symbols)
        // Deterministic: sorted + deduplicated.
        let list = symbolsFromAssembly assembly
        Assert.Equal<string list>(list |> List.distinct |> List.sort, list)
