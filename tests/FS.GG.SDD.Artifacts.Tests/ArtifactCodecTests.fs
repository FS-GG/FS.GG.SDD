namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FSharp.Reflection
open Xunit

// Tests for the field-list codec (FS.GG.SDD#201, ADR-0002 invariant 1). The codec-primitive facts
// (omission, null-as-absence, determinism, coupling) are exercised over a toy model so the
// abstraction is validated independently; the real evidence record↔codec coupling (spec 097 T031,
// FS.GG.SDD#260) is asserted at the bottom, now that `EvidenceCodec.{sourceRefFields,disclosureFields}`
// drive both the Evidence.fs reader and the HandlersEvidence renderer. The generative artifact
// round-trip properties live in the Commands tests (EvidenceRoundTripPropertyTests, spec 097 Phase 6).
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

    // --- new combinators wired for the evidence records (FS.GG.SDD#260) ---

    type private Kinded =
        { Kind: string // defaulted scalar
          Extra: string option }

    let private kindedFields: ArtifactCodec.FieldCodec<Kinded> list =
        [ ArtifactCodec.defaultedScalar "kind" "artifact" (fun m -> m.Kind) (fun v m -> { m with Kind = v })
          ArtifactCodec.optionalScalar "extra" (fun m -> m.Extra) (fun v m -> { m with Extra = v }) ]

    let private kindedSeed = { Kind = "artifact"; Extra = None }

    [<Fact>]
    let ``defaultedScalar reads the fallback when absent and the authored value when present`` () =
        match ArtifactCodec.decode kindedFields kindedSeed "extra: x" with
        | Ok m -> Assert.Equal("artifact", m.Kind)
        | Error e -> failwith e

        match ArtifactCodec.decode kindedFields kindedSeed "kind: test-output" with
        | Ok m -> Assert.Equal("test-output", m.Kind)
        | Error e -> failwith e

    [<Fact>]
    let ``defaultedScalar always writes its line`` () =
        Assert.Contains("kind: artifact", ArtifactCodec.render kindedFields kindedSeed)

    // `foldInto` (decode from an already-parsed mapping) is exercised end-to-end by the evidence
    // wiring — `parseEvidenceSourceRefs`/`parseSyntheticDisclosure` drive it per record — and pinned
    // by EvidenceArtifactTests + the round-trip property; the YAML helpers it needs are assembly-
    // internal, so there is no toy-level unit for it here.

    // === T031 — the real evidence record↔codec coupling (FR-007, FS.GG.SDD#260) ===
    // The wiring has landed: `EvidenceCodec.{sourceRefFields,disclosureFields}` drive both the
    // Evidence.fs reader and the HandlersEvidence renderer. These assert the codec Key set equals
    // each authored record's label set, so adding an authored field with no codec entry — or a codec
    // field with no record field — fails here (SC-004).

    let private authoredKeys (excluded: string list) (ty: System.Type) =
        FSharpType.GetRecordFields ty
        |> Array.map (fun p -> p.Name)
        |> Array.filter (fun name -> not (Set.contains name (set excluded)))
        |> Set.ofArray

    [<Fact>]
    let ``sourceRefFields couple to every authored EvidenceSourceReference field (T031)`` () =
        // Record label -> YAML key: `ReferenceId`/`RelatedSourceId` serialize as `id`/`relatedSourceId`;
        // `SourceLocation` is parse-assigned provenance, excluded from serialization.
        let labelToKey =
            Map
                [ "ReferenceId", "id"
                  "Kind", "kind"
                  "Path", "path"
                  "Uri", "uri"
                  "Digest", "digest"
                  "RelatedSourceId", "relatedSourceId"
                  "Result", "result" ]

        // The map covers exactly the authored record fields — a new field with no mapping fails here...
        Assert.Equal<Set<string>>(
            authoredKeys [ "SourceLocation" ] typeof<EvidenceSourceReference>,
            labelToKey |> Map.toSeq |> Seq.map fst |> Set.ofSeq
        )

        // ...and the codec's Key set equals the mapped YAML keys — codec and record stay coupled.
        Assert.Equal<Set<string>>(
            labelToKey |> Map.toSeq |> Seq.map snd |> Set.ofSeq,
            ArtifactCodec.keys EvidenceCodec.sourceRefFields |> Set.ofList
        )

    [<Fact>]
    let ``disclosureFields couple to every authored SyntheticDisclosure field (T031)`` () =
        let labelToKey = Map [ "StandsInFor", "standsInFor"; "Reason", "reason" ]

        Assert.Equal<Set<string>>(
            authoredKeys [] typeof<SyntheticDisclosure>,
            labelToKey |> Map.toSeq |> Seq.map fst |> Set.ofSeq
        )

        Assert.Equal<Set<string>>(
            labelToKey |> Map.toSeq |> Seq.map snd |> Set.ofSeq,
            ArtifactCodec.keys EvidenceCodec.disclosureFields |> Set.ofList
        )
