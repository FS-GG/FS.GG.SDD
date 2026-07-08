namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FSharp.Reflection
open Xunit

// Foundation tests for the field-list codec (FS.GG.SDD#201, ADR-0002 invariant 1).
// Exercised over a toy model so the abstraction is validated independently of the
// evidence/tasks wiring (which is Blocked by #189). No FsCheck here — the
// artifact round-trip *properties* land with the wiring (spec 097 Phase 6); these
// are the codec-primitive facts (omission, null-as-absence, determinism, coupling).
module ArtifactCodecTests =

    type private Toy =
        { Name: string // required scalar
          Note: string option // optional scalar (null-aware)
          Tags: string list // inline list
          Steps: string list } // scalar block

    let private fields: ArtifactCodec.FieldCodec<Toy> list =
        [ ArtifactCodec.requiredScalar "name" (fun m -> m.Name) (fun v m -> { m with Name = v })
          ArtifactCodec.optionalScalar "note" (fun m -> m.Note) (fun v m -> { m with Note = v })
          ArtifactCodec.inlineList "tags" (fun m -> m.Tags) (fun v m -> { m with Tags = v })
          ArtifactCodec.scalarBlock "steps" (fun m -> m.Steps) (fun v m -> { m with Steps = v }) ]

    let private seed =
        { Name = ""
          Note = None
          Tags = []
          Steps = [] }

    let private roundtrip (m: Toy) =
        match ArtifactCodec.decode fields seed (ArtifactCodec.render fields m) with
        | Ok back -> back
        | Error e -> failwith $"decode failed: {e}"

    [<Fact>]
    let ``round-trips a fully populated model`` () =
        let m =
            { Name = "demo"
              Note = Some "hello world"
              Tags = [ "a"; "b" ]
              Steps = [ "s1"; "s2" ] }

        Assert.Equal<Toy>(m, roundtrip m)

    [<Fact>]
    let ``omits an absent optional and an empty list`` () =
        let text =
            ArtifactCodec.render
                fields
                { Name = "demo"
                  Note = None
                  Tags = []
                  Steps = [] }

        Assert.Equal("name: demo", text) // only the required field, nothing else
        Assert.DoesNotContain("note", text)
        Assert.DoesNotContain("tags", text)
        Assert.DoesNotContain("steps", text)

    [<Theory>]
    [<InlineData("null")>]
    [<InlineData("Null")>]
    [<InlineData("NULL")>]
    [<InlineData("~")>]
    let ``a bare null token reads as absence, not the string "null"`` (token: string) =
        match ArtifactCodec.decode fields seed $"name: demo\nnote: {token}" with
        | Ok m -> Assert.True(Option.isNone m.Note, $"expected None for bare '{token}'")
        | Error e -> failwith e

    [<Fact>]
    let ``an empty value reads as absence`` () =
        match ArtifactCodec.decode fields seed "name: demo\nnote:" with
        | Ok m -> Assert.True(Option.isNone m.Note)
        | Error e -> failwith e

    [<Fact>]
    let ``a quoted "null" keeps the string and survives the round trip`` () =
        // decode: a quoted null is a real string, distinct from the bare token
        match ArtifactCodec.decode fields seed "name: demo\nnote: \"null\"" with
        | Ok m -> Assert.Equal<string option>(Some "null", m.Note)
        | Error e -> failwith e

        // render: Some "null" is quoted so it cannot be misread as absence on the way back
        let m =
            { seed with
                Name = "demo"
                Note = Some "null" }

        Assert.Contains("\"null\"", ArtifactCodec.render fields m)
        Assert.Equal<Toy>(m, roundtrip m)

    [<Fact>]
    let ``render is deterministic and ordered by the field list`` () =
        let m =
            { Name = "demo"
              Note = Some "n"
              Tags = [ "a" ]
              Steps = [ "s" ] }

        let a = ArtifactCodec.render fields m
        Assert.Equal(a, ArtifactCodec.render fields m)

        let idx (k: string) = a.IndexOf(k)

        Assert.True(
            idx "name:" < idx "note:"
            && idx "note:" < idx "tags:"
            && idx "tags:" < idx "steps:"
        )

    [<Fact>]
    let ``a missing required field is an error`` () =
        match ArtifactCodec.decode fields seed "note: x" with
        | Ok _ -> failwith "expected an error for the missing required 'name'"
        | Error msg -> Assert.Contains("name", msg)

    [<Fact>]
    let ``keys are the field list in declaration order`` () =
        Assert.Equal<string list>([ "name"; "note"; "tags"; "steps" ], ArtifactCodec.keys fields)

    [<Fact>]
    let ``the codec key set equals the record field set (FR-007 coupling mechanism)`` () =
        // The mechanism the real evidence/tasks coupling test (spec 097 T031) uses:
        // a field added to the record with no codec entry would fail this.
        let recordFields =
            FSharpType.GetRecordFields(typeof<Toy>, System.Reflection.BindingFlags.NonPublic)
            |> Array.map (fun p -> p.Name.ToLowerInvariant())
            |> Set.ofArray

        let codecKeys =
            ArtifactCodec.keys fields
            |> List.map (fun k -> k.ToLowerInvariant())
            |> Set.ofList

        Assert.Equal<Set<string>>(recordFields, codecKeys)
