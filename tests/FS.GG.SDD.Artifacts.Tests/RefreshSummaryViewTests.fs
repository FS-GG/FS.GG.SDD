namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.GenerationManifest
open Xunit

module RefreshSummaryViewTests =
    let workId = "015-refresh-command"

    let generator = SchemaVersion.currentGeneratorVersion ()

    let sourceIdentity (path: string) (text: string) : SourceIdentity =
        let artifact =
            match ArtifactRef.create path ArtifactKind.GeneratedView ArtifactOwner.Sdd true with
            | Ok value -> value
            | Error message -> failwith message

        let compatibility = SchemaVersion.classifyRaw (Some "1")

        {
            Artifact = artifact
            Digest = SchemaVersion.sha256Text text
            SchemaVersion = compatibility.Version
            SchemaStatus = compatibility.Status
            RawSchemaVersion = Some "1"
        }

    let sources () =
        [
            sourceIdentity $"readiness/{workId}/work-model.json" "work-model-bytes"
            sourceIdentity $"readiness/{workId}/analysis.json" "analysis-bytes"
            sourceIdentity $"readiness/{workId}/verify.json" "verify-bytes"
            sourceIdentity $"readiness/{workId}/ship.json" "ship-bytes"
        ]

    let outputDigest =
        SchemaVersion.createOutputDigest "sha256" (SchemaVersion.sha256Text "summary-body").Value
        |> Result.toOption

    [<Fact>]
    let ``expectedSummaryOutputPath targets the readiness summary file`` () =
        Assert.Equal($"readiness/{workId}/summary.md", expectedSummaryOutputPath workId)

    [<Fact>]
    let ``createSummaryManifest marks the view generated with the summary kind`` () =
        let manifest =
            createSummaryManifest (expectedSummaryOutputPath workId) generator (sources ()) outputDigest

        Assert.Equal(GeneratedViewKind.Summary, manifest.Kind)
        Assert.Equal("summary", viewKindValue manifest.Kind)
        Assert.Equal(SchemaVersion.create 1, manifest.SchemaVersion)
        Assert.Equal(ArtifactKind.GeneratedView, manifest.View.Kind)
        Assert.Equal(ArtifactOwner.Sdd, manifest.View.Owner)

    [<Fact>]
    let ``createSummaryManifest records the structured readiness sources and output digest`` () =
        let manifest =
            createSummaryManifest (expectedSummaryOutputPath workId) generator (sources ()) outputDigest

        Assert.Equal(4, manifest.Sources.Length)
        Assert.Contains(manifest.Sources, (fun source -> source.Artifact.Path = $"readiness/{workId}/work-model.json"))
        Assert.Equal(generator.Id, manifest.Generator.Id)
        Assert.Equal(outputDigest, manifest.OutputDigest)
        // sources are recorded in a deterministic (path-sorted) order.
        let paths = manifest.Sources |> List.map (fun source -> source.Artifact.Path)
        Assert.Equal<string list>(List.sort paths, paths)

    [<Fact>]
    let ``isStale is false when the summary sources are unchanged`` () =
        let current = sources ()

        let manifest =
            createSummaryManifest (expectedSummaryOutputPath workId) generator current outputDigest

        Assert.False(isStale current manifest)
        Assert.False(isStale (List.rev current) manifest)

    [<Fact>]
    let ``isStale is true when a summary source digest changes`` () =
        let manifest =
            createSummaryManifest (expectedSummaryOutputPath workId) generator (sources ()) outputDigest

        let changed =
            [
                sourceIdentity $"readiness/{workId}/work-model.json" "work-model-bytes-CHANGED"
                sourceIdentity $"readiness/{workId}/analysis.json" "analysis-bytes"
                sourceIdentity $"readiness/{workId}/verify.json" "verify-bytes"
                sourceIdentity $"readiness/{workId}/ship.json" "ship-bytes"
            ]

        Assert.True(isStale changed manifest)

    [<Fact>]
    let ``isStale rejects a newly required producer missing from the manifest`` () =
        let current = sources ()
        let recorded = current |> List.take 3
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator recorded outputDigest

        Assert.True(isStale current manifest)

    [<Fact>]
    let ``isStale rejects a producer no longer in the current set`` () =
        let current = sources ()
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator current outputDigest

        Assert.True(isStale (current |> List.take 3) manifest)

    [<Fact>]
    let ``isStale rejects a source from the wrong work root even with the same bytes`` () =
        let current = sources ()
        let wrongRoot =
            sourceIdentity "readiness/other-work/analysis.json" "analysis-bytes"
            :: (current |> List.filter (fun source -> not (source.Artifact.Path.EndsWith("/analysis.json"))))
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator wrongRoot outputDigest

        Assert.True(isStale current manifest)

    [<Fact>]
    let ``isStale rejects duplicate paths in recorded or current sources`` () =
        let current = sources ()
        let duplicate = List.head current
        let recordedDuplicate = createSummaryManifest (expectedSummaryOutputPath workId) generator (duplicate :: current) outputDigest
        let clean = createSummaryManifest (expectedSummaryOutputPath workId) generator current outputDigest

        Assert.True(isStale current recordedDuplicate)
        Assert.True(isStale (duplicate :: current) clean)

    [<Fact>]
    let ``isStale rejects a generated view with no producer`` () =
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator [] outputDigest

        Assert.True(isStale [] manifest)

    [<Theory>]
    [<InlineData("sha256", "")>]
    [<InlineData("sha256", "not-a-digest")>]
    [<InlineData("sha512", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")>]
    [<InlineData("sha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")>]
    let ``isStale rejects identical malformed digests on both sides`` algorithm value =
        let source = List.head (sources ())
        let malformed = { source with Digest = { source.Digest with Algorithm = algorithm; Value = value } }
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator [ malformed ] outputDigest

        Assert.True(isStale [ malformed ] manifest)

    [<Fact>]
    let ``isStale rejects a null digest without throwing`` () =
        let source = List.head (sources ())
        let malformed = { source with Digest = { source.Digest with Value = Unchecked.defaultof<string> } }
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator [ malformed ] outputDigest

        Assert.True(isStale [ malformed ] manifest)

    [<Fact>]
    let ``isStale compares long Unicode paths exactly without order dependence`` () =
        let longSegment = String.replicate 2048 "x"
        let longPath = $"readiness/{workId}/résumé-😃-{longSegment}.json"
        let first = sourceIdentity longPath "unicode-content"
        let second = sourceIdentity $"readiness/{workId}/analysis.json" "analysis-content"
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator [ first; second ] outputDigest

        Assert.False(isStale [ second; first ] manifest)
        let moved = sourceIdentity (longPath.Replace("résumé", "resume")) "unicode-content"
        Assert.True(isStale [ second; moved ] manifest)
