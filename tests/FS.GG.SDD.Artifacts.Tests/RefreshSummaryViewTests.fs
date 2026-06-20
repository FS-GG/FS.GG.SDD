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

        { Artifact = artifact
          Digest = SchemaVersion.sha256Text text
          SchemaVersion = compatibility.Version
          SchemaStatus = compatibility.Status
          RawSchemaVersion = Some "1" }

    let sources () =
        [ sourceIdentity $"readiness/{workId}/work-model.json" "work-model-bytes"
          sourceIdentity $"readiness/{workId}/analysis.json" "analysis-bytes"
          sourceIdentity $"readiness/{workId}/verify.json" "verify-bytes"
          sourceIdentity $"readiness/{workId}/ship.json" "ship-bytes" ]

    let outputDigest =
        SchemaVersion.createOutputDigest "sha256" (SchemaVersion.sha256Text "summary-body").Value
        |> Result.toOption

    [<Fact>]
    let ``expectedSummaryOutputPath targets the readiness summary file`` () =
        Assert.Equal($"readiness/{workId}/summary.md", expectedSummaryOutputPath workId)

    [<Fact>]
    let ``createSummaryManifest marks the view generated with the summary kind`` () =
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator (sources ()) outputDigest

        Assert.Equal(GeneratedViewKind.Summary, manifest.Kind)
        Assert.Equal("summary", viewKindValue manifest.Kind)
        Assert.Equal(SchemaVersion.create 1, manifest.SchemaVersion)
        Assert.Equal(ArtifactKind.GeneratedView, manifest.View.Kind)
        Assert.Equal(ArtifactOwner.Sdd, manifest.View.Owner)

    [<Fact>]
    let ``createSummaryManifest records the structured readiness sources and output digest`` () =
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator (sources ()) outputDigest

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
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator current outputDigest

        Assert.False(isStale current manifest)

    [<Fact>]
    let ``isStale is true when a summary source digest changes`` () =
        let manifest = createSummaryManifest (expectedSummaryOutputPath workId) generator (sources ()) outputDigest

        let changed =
            [ sourceIdentity $"readiness/{workId}/work-model.json" "work-model-bytes-CHANGED"
              sourceIdentity $"readiness/{workId}/analysis.json" "analysis-bytes"
              sourceIdentity $"readiness/{workId}/verify.json" "verify-bytes"
              sourceIdentity $"readiness/{workId}/ship.json" "ship-bytes" ]

        Assert.True(isStale changed manifest)
