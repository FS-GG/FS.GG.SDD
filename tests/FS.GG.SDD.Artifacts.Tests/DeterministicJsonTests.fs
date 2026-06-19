namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module DeterministicJsonTests =
    [<Fact>]
    let ``Deterministic ordering fixture emits byte-identical JSON across runs`` () =
        let outputs =
            [ 1..3 ]
            |> List.map (fun _ -> TestSupport.model "deterministic-ordering" |> Serialization.serializeWorkModel)

        Assert.True(outputs.[0] = outputs.[1])
        Assert.True(outputs.[1] = outputs.[2])

    [<Fact>]
    let ``Tasks requirements decisions and evidence are sorted by stable ids`` () =
        let model = TestSupport.model "deterministic-ordering"

        Assert.True([ "FR-001"; "FR-002" ] = (model.Requirements |> List.map (fun requirement -> requirement.Id)))
        Assert.True([ "DEC-001"; "DEC-002" ] = (model.Decisions |> List.map (fun decision -> decision.Id)))
        Assert.True([ "T001"; "T002" ] = (model.Tasks |> List.map (fun task -> task.Id)))
        Assert.True([ "EV001"; "EV002" ] = (model.Evidence |> List.map (fun evidence -> evidence.Id)))

    [<Fact>]
    let ``NormalizedWorkModel generation emits byte-identical JSON across runs`` () =
        let outputs =
            [ 1..3 ]
            |> List.map (fun _ -> TestSupport.generationResult "deterministic-ordering" |> fun result -> result.Json)

        Assert.Equal(outputs.[0], outputs.[1])
        Assert.Equal(outputs.[1], outputs.[2])
        Assert.DoesNotContain("timestamp", outputs.[0])

    [<Fact>]
    let ``NormalizedWorkModel JSON keeps documented top-level property order`` () =
        let json = TestSupport.generationResult "valid-work-item" |> fun result -> result.Json
        let expectedOrder =
            [ "\"schemaVersion\":"
              "\"modelVersion\":"
              "\"workId\":"
              "\"project\":"
              "\"sources\":"
              "\"workItem\":"
              "\"requirements\":"
              "\"decisions\":"
              "\"tasks\":"
              "\"evidence\":"
              "\"generatedViews\":"
              "\"diagnostics\":"
              "\"governanceBoundaries\":" ]

        let indexes = expectedOrder |> List.map json.IndexOf
        Assert.All(indexes, fun index -> Assert.True(index >= 0))
        let sortedIndexes = indexes |> List.sort
        Assert.True((indexes = sortedIndexes))
