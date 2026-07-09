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

    // --- typed combinators: the staged foundation for the declaration wiring (FS.GG.SDD#260) ---
    // Flat, indentation-free combinators the evidence declaration needs (DU `kind`, `synthetic`
    // bool, the typed-id ref lists, the always-present string lists). Validated here over a toy so
    // the byte behaviour is pinned before the declaration is refactored onto them.

    type private Typed =
        { Kind: string // mappedScalar (total render/read pair)
          Flag: bool // boolScalar
          Refs: string list // refList (lenient, always-present, distinct+sorted)
          Tags: string list } // alwaysInlineList

    // A total mapping: unknown tokens fold to "other" (as parseEvidenceKind folds to Verification).
    let private kindOfStr s =
        if s = "a" || s = "b" then s else "other"

    let private typedFields: ArtifactCodec.FieldCodec<Typed> list =
        [ ArtifactCodec.mappedScalar "kind" id kindOfStr (fun m -> m.Kind) (fun v m -> { m with Kind = v })
          ArtifactCodec.boolScalar "flag" false (fun m -> m.Flag) (fun v m -> { m with Flag = v })
          ArtifactCodec.refList
              "refs"
              (fun s -> if s.StartsWith "R" then Ok s else Error "bad")
              id
              (fun m -> m.Refs)
              (fun v m -> { m with Refs = v })
          ArtifactCodec.alwaysInlineList "tags" (fun m -> m.Tags) (fun v m -> { m with Tags = v }) ]

    let private typedSeed =
        { Kind = "other"
          Flag = false
          Refs = []
          Tags = [] }

    [<Fact>]
    let ``mappedScalar round-trips a known token and folds an unknown one via ofStr`` () =
        match ArtifactCodec.decode typedFields typedSeed "kind: a\nflag: true\nrefs: []\ntags: []" with
        | Ok m -> Assert.Equal("a", m.Kind)
        | Error e -> failwith e

        match ArtifactCodec.decode typedFields typedSeed "kind: zzz\nflag: false\nrefs: []\ntags: []" with
        | Ok m -> Assert.Equal("other", m.Kind) // unrecognised token -> total default
        | Error e -> failwith e

    [<Fact>]
    let ``boolScalar honours only explicit true/false, else the fallback`` () =
        let flagOf text =
            match ArtifactCodec.decode typedFields typedSeed text with
            | Ok m -> m.Flag
            | Error e -> failwith e

        Assert.True(flagOf "kind: a\nflag: TRUE\nrefs: []\ntags: []")
        Assert.False(flagOf "kind: a\nflag: nonsense\nrefs: []\ntags: []") // junk -> fallback
        Assert.False(flagOf "kind: a\nrefs: []\ntags: []") // absent -> fallback

    [<Fact>]
    let ``refList drops malformed tokens, stays always-present, and renders distinct+sorted`` () =
        match ArtifactCodec.decode typedFields typedSeed "kind: a\nflag: false\nrefs: [R2, bad, R1]\ntags: []" with
        | Ok m -> Assert.Equal<string list>([ "R2"; "R1" ], m.Refs) // 'bad' dropped, read order preserved
        | Error e -> failwith e

        Assert.Contains(
            "refs: [R1, R2]",
            ArtifactCodec.render
                typedFields
                { typedSeed with
                    Refs = [ "R2"; "R1"; "R2" ] }
        )

        Assert.Contains("refs: []", ArtifactCodec.render typedFields typedSeed) // empty -> [], never omitted

    [<Fact>]
    let ``alwaysInlineList renders [] when empty instead of omitting the line`` () =
        Assert.Contains("tags: []", ArtifactCodec.render typedFields typedSeed)

        Assert.Contains(
            "tags: [x, y]",
            ArtifactCodec.render
                typedFields
                { typedSeed with
                    Tags = [ "y"; "x"; "y" ] }
        )

    // --- indented combinators: nested mapping + block sequence of sub-records (FS.GG.SDD#260) ---
    // The declaration needs `subject` (nested) and `sourceRefs` (recordList). These render column-0
    // text with relative indentation that the artifact-level framing then shifts, so the byte layout
    // is pinned here.

    type private Leaf = { A: string; B: string option }

    let private leafFields: ArtifactCodec.FieldCodec<Leaf> list =
        [ ArtifactCodec.requiredScalar "a" (fun m -> m.A) (fun v m -> { m with A = v })
          ArtifactCodec.optionalScalar "b" (fun m -> m.B) (fun v m -> { m with B = v }) ]

    let private leafSeed = { A = ""; B = None }

    type private Parent =
        { Name: string
          Sub: Leaf
          Items: Leaf list }

    let private parentFields: ArtifactCodec.FieldCodec<Parent> list =
        [ ArtifactCodec.requiredScalar "name" (fun m -> m.Name) (fun v m -> { m with Name = v })
          ArtifactCodec.nested "sub" leafFields leafSeed (fun m -> m.Sub) (fun v m -> { m with Sub = v })
          ArtifactCodec.recordList "items" leafFields leafSeed (fun m -> m.Items) (fun v m -> { m with Items = v }) ]

    let private parentSeed =
        { Name = ""
          Sub = leafSeed
          Items = [] }

    [<Fact>]
    let ``nested renders key then two-space-indented sub-fields`` () =
        let text =
            ArtifactCodec.render
                parentFields
                { parentSeed with
                    Name = "p"
                    Sub = { A = "x"; B = Some "y" } }

        Assert.Contains("sub:\n  a: x\n  b: y", text) // sub-fields indented exactly two spaces
        Assert.Contains("items: []", text) // empty recordList stays present

    [<Fact>]
    let ``recordList renders a block sequence with dash items and aligned continuations`` () =
        let text =
            ArtifactCodec.render
                parentFields
                { parentSeed with
                    Name = "p"
                    Items = [ { A = "x"; B = Some "y" }; { A = "z"; B = None } ] }

        // First field on the `- ` marker, the rest two spaces deeper; an absent optional omits its line.
        Assert.Contains("items:\n  - a: x\n    b: y\n  - a: z", text)

    [<Fact>]
    let ``nested + recordList round-trip through decode`` () =
        let model =
            { Name = "p"
              Sub = { A = "x"; B = Some "y" }
              Items = [ { A = "m"; B = None }; { A = "n"; B = Some "o" } ] }

        match ArtifactCodec.decode parentFields parentSeed (ArtifactCodec.render parentFields model) with
        | Ok back -> Assert.Equal<Parent>(model, back)
        | Error e -> failwith e

    // optionalNestedVia: the draft reads null-aware, `lift` rejects a blank/partial draft to None
    // (the synthetic-disclosure gate shape, FS.GG.SDD#180), `lower` projects back for rendering.
    type private Draft = { X: string option; Y: string option }
    type private Field = { X: string; Y: string }

    type private Holder = { D: Field option }

    let private draftFields: ArtifactCodec.FieldCodec<Draft> list =
        [ ArtifactCodec.optionalScalar "x" (fun m -> m.X) (fun v m -> { m with X = v })
          ArtifactCodec.optionalScalar "y" (fun m -> m.Y) (fun v m -> { m with Y = v }) ]

    let private liftField (d: Draft) =
        match d.X, d.Y with
        | Some x, Some y when x <> "" && y <> "" -> Some { X = x; Y = y }
        | _ -> None

    let private holderFields: ArtifactCodec.FieldCodec<Holder> list =
        [ ArtifactCodec.optionalNestedVia
              "d"
              draftFields
              { X = None; Y = None }
              liftField
              (fun (f: Field) -> { X = Some f.X; Y = Some f.Y })
              (fun m -> m.D)
              (fun v m -> { m with D = v }) ]

    [<Fact>]
    let ``optionalNestedVia rejects a bare-null draft and round-trips a populated one`` () =
        // bare-null inner scalars -> draft (None, None) -> lift -> None (key omitted on write)
        match ArtifactCodec.decode holderFields { D = None } "d:\n  x: null\n  y: null" with
        | Ok m -> Assert.Equal<Field option>(None, m.D)
        | Error e -> failwith e

        // populated -> Some, and it round-trips
        let model = { D = Some { X = "a"; Y = "b" } }

        match ArtifactCodec.decode holderFields { D = None } (ArtifactCodec.render holderFields model) with
        | Ok back -> Assert.Equal<Holder>(model, back)
        | Error e -> failwith e

        Assert.Equal("", ArtifactCodec.render holderFields { D = None }) // None -> key omitted

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
