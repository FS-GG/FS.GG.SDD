namespace FS.GG.SDD.Artifacts.Tests

open System
open System.Text.Json.Nodes
open FS.GG.SDD.Artifacts
open Xunit

/// Characterizes the current work-model currency path before a producer-owned source-set policy is selected.
module WorkModelSourcePolicyTests =
    let private workId = "002-normalized-work-model"
    let private outputPath = $"readiness/{workId}/work-model.json"
    let private baseline () = TestSupport.normalizedSnapshots "valid-work-item"
    let private required (node: JsonNode | null) =
        match node with
        | null -> failwith "Fixture JSON node is missing."
        | value -> value

    let private withGeneratedView change =
        baseline ()
        |> List.map (fun snapshot ->
            if snapshot.Path <> outputPath then snapshot
            else
                let root = JsonNode.Parse snapshot.Text |> required
                let view = (required root.["generatedViews"]).AsArray().[0] |> required
                change ((required view.["sources"]).AsArray())
                // Current currency checking treats an absent output digest as optional. Removing
                // it isolates the source-row comparison from output-byte integrity.
                view.["outputDigest"] <- null
                { snapshot with Text = root.ToJsonString() })

    let private diagnostics snapshots =
        Serialization.checkGeneratedWorkModelCurrency
            snapshots workId (SchemaVersion.currentGeneratorVersion ())

    let private stale snapshots =
        diagnostics snapshots |> List.exists (fun diagnostic -> diagnostic.Id = "staleGeneratedView")

    [<Fact>]
    let ``current currency accepts a generated view with an omitted source row`` () =
        let inputs = withGeneratedView (fun rows -> rows.RemoveAt 0)
        Assert.False(stale inputs)

    [<Fact>]
    let ``current currency rejects an extra recorded source row`` () =
        let inputs =
            withGeneratedView (fun rows ->
                let extra = (required rows.[0]).DeepClone()
                extra.["path"] <- JsonValue.Create "work/002-normalized-work-model/extra.md"
                rows.Add extra)
        Assert.True(stale inputs)

    [<Fact>]
    let ``current source selector ignores a case-alias input beside the selected path`` () =
        let inputs = baseline ()
        let source = inputs |> List.find (fun snapshot -> snapshot.Path = $"work/{workId}/spec.md")
        let alias = { source with Path = $"work/{workId}/Spec.md"; Text = source.Text + "\nchanged\n" }
        Assert.False(stale (alias :: inputs))

    [<Fact>]
    let ``current currency detects selected source digest drift`` () =
        let inputs =
            baseline ()
            |> List.map (fun source ->
                if source.Path = $"work/{workId}/spec.md" then
                    { source with Text = source.Text + "\nchanged\n" }
                else source)
        Assert.True(stale inputs)
