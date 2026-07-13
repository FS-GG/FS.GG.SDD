namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.ArtifactRef
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

    // === T031 — the real record↔codec coupling (FR-007, FS.GG.SDD#260, hardened FS.GG.SDD#290) ===
    // One shared `fields` list drives both the reader (Evidence.fs / Task.fs) and the renderer
    // (HandlersEvidence / task authoring) for each authored record. Each test below pins that
    // coupling THREE ways from a single per-field `spec`, so drift in any direction fails here:
    //   (1) the record's authored LABELS equal the spec's labels — a new authored field with no
    //       spec row (hence no codec entry) fails (SC-004);
    //   (2) the codec's KEY set equals the union of the spec's keys — a codec field with no record
    //       field (or vice-versa) fails;
    //   (3) rendering a fully-populated record (every field a DISTINCT value) puts each field's own
    //       value under its OWN key — a *transposition* (two fields whose keys are cross-wired but
    //       whose key SET is unchanged) fails here. The earlier key-set-only assertions were blind
    //       to that transposition (FS.GG.SDD#290); the round-trip properties covered it only
    //       generatively.
    // A spec row is `label, [ key, valuePinnedFragment ]` — one key for a scalar/list field, two for
    // the task `Status` DU (`status` tag + `skipRationale` payload). Structurally-unique block fields
    // (the nested `subject`/`syntheticDisclosure`, the `sourceRefs` record list) pin their block
    // header plus a first inner value; a scalar can never transpose into a block by shape.

    let private authoredKeys (excluded: string list) (ty: System.Type) =
        FSharpType.GetRecordFields ty
        |> Array.map (fun p -> p.Name)
        |> Array.filter (fun name -> not (Set.contains name (set excluded)))
        |> Set.ofArray

    let private orFail label =
        function
        | Ok value -> value
        | Error(error: string) -> failwithf "%s: %s" label error

    // Assert the three-way coupling of a record type, its codec field list, and a fully-populated
    // model whose every field carries a distinct value, against a per-field `spec`.
    let private assertCoupled
        (excluded: string list)
        (recordType: System.Type)
        (codecFields: ArtifactCodec.FieldCodec<'M> list)
        (model: 'M)
        (spec: (string * (string * string) list) list)
        =
        // (1) labels cover exactly the authored record fields (minus parse-assigned provenance).
        Assert.Equal<Set<string>>(authoredKeys excluded recordType, spec |> List.map fst |> Set.ofList)

        // (2) the codec's Key set equals the union of the spec's keys.
        Assert.Equal<Set<string>>(
            spec |> List.collect (snd >> List.map fst) |> Set.ofList,
            ArtifactCodec.keys codecFields |> Set.ofList
        )

        // (3) each field's own value renders under its own key — catches a label->key transposition.
        let rendered = ArtifactCodec.render codecFields model

        for label, keyed in spec do
            for key, fragment in keyed do
                Assert.True(
                    rendered.Contains(fragment: string),
                    $"field '{label}' (key '{key}') not value-pinned: expected\n  {fragment}\nin rendered:\n{rendered}"
                )

    [<Fact>]
    let ``sourceRefFields couple to every authored EvidenceSourceReference field (T031/#290)`` () =
        // `ReferenceId`/`RelatedSourceId` serialize as `id`/`relatedSourceId`; `SourceLocation` is
        // parse-assigned provenance, excluded from serialization.
        let model =
            { EvidenceCodec.sourceRefSeed with
                ReferenceId = Some "idval"
                Kind = "kindval"
                Path = Some "pathval"
                Uri = Some "urival"
                Digest = Some "digestval"
                RelatedSourceId = Some "relatedval"
                Result = Some "resultval" }

        assertCoupled
            [ "SourceLocation" ]
            typeof<EvidenceSourceReference>
            EvidenceCodec.sourceRefFields
            model
            [ "ReferenceId", [ "id", "id: idval" ]
              "Kind", [ "kind", "kind: kindval" ]
              "Path", [ "path", "path: pathval" ]
              "Uri", [ "uri", "uri: urival" ]
              "Digest", [ "digest", "digest: digestval" ]
              "RelatedSourceId", [ "relatedSourceId", "relatedSourceId: relatedval" ]
              "Result", [ "result", "result: resultval" ] ]

    [<Fact>]
    let ``disclosureFields couple to every authored SyntheticDisclosure field (T031/#290)`` () =
        // The codec operates over the null-aware `DisclosureDraft`, whose fields mirror the authored
        // `SyntheticDisclosure` one-for-one (the coupling is asserted against the authored record).
        let model: EvidenceCodec.DisclosureDraft =
            { StandsInFor = Some "standsval"
              Reason = Some "reasonval" }

        assertCoupled
            []
            typeof<SyntheticDisclosure>
            EvidenceCodec.disclosureFields
            model
            [ "StandsInFor", [ "standsInFor", "standsInFor: standsval" ]
              "Reason", [ "reason", "reason: reasonval" ] ]

    [<Fact>]
    let ``declarationFields couple to every authored EvidenceDeclaration field (T031/#290)`` () =
        // `Id` is a codec field (the `evidence` recordList frames it); `Source`/`SourceLocation` are
        // parse-assigned provenance, excluded. `ArtifactRefs` serializes as `artifacts`.
        let artifactRef =
            ArtifactRef.create "docs/artifactval.md" (ArtifactKind.Other "e") ArtifactOwner.Sdd false
            |> orFail "artifactRef"

        let model =
            { EvidenceCodec.declarationSeed with
                Id = createEvidenceId "EV009" |> orFail "evId"
                Kind = EvidenceKind.Verification
                Subject =
                    { SubjectType = "subjtypeval"
                      Id = "subjidval" }
                TaskRefs = [ createTaskId "T007" |> orFail "taskId" ]
                RequirementRefs = [ createRequirementId "FR-007" |> orFail "reqId" ]
                AcceptanceScenarioRefs = [ createAcceptanceScenarioId "AC-007" |> orFail "acId" ]
                ClarificationDecisionRefs = [ createDecisionId "DEC-007" |> orFail "decId" ]
                ChecklistResultRefs = [ createChecklistResultId "CR-007" |> orFail "crId" ]
                PlanDecisionRefs = [ createPlanDecisionId "PD-007" |> orFail "pdId" ]
                ObligationRefs = [ "obligval" ]
                ArtifactRefs = [ artifactRef ]
                SourceRefs =
                    [ { EvidenceCodec.sourceRefSeed with
                          Kind = "srckindval" } ]
                Result = "advisory"
                Synthetic = true
                SyntheticDisclosure =
                    Some
                        { StandsInFor = "standsval"
                          Reason = "reasonval" }
                ObservedRun =
                    Some
                        { Source = "observedsourceval"
                          Digest = "sha256:" + String.replicate 64 "b"
                          Outcome = "passed"
                          Passed = 7
                          Failed = 0
                          Skipped = 3 }
                Rationale = Some "rationaleval"
                Owner = Some "ownerval"
                Scope = Some "scopeval"
                LaterLifecycleVisibility = Some "visibilityval"
                Notes = [ "noteval" ] }

        assertCoupled
            [ "Source"; "SourceLocation" ]
            typeof<EvidenceDeclaration>
            EvidenceCodec.declarationFields
            model
            [ "Id", [ "id", "id: EV009" ]
              "Kind", [ "kind", "kind: verification" ]
              "Subject", [ "subject", "subject:\n  type: subjtypeval" ]
              "TaskRefs", [ "taskRefs", "taskRefs: [T007]" ]
              "RequirementRefs", [ "requirementRefs", "requirementRefs: [FR-007]" ]
              "AcceptanceScenarioRefs", [ "acceptanceScenarioRefs", "acceptanceScenarioRefs: [AC-007]" ]
              "ClarificationDecisionRefs", [ "clarificationDecisionRefs", "clarificationDecisionRefs: [DEC-007]" ]
              "ChecklistResultRefs", [ "checklistResultRefs", "checklistResultRefs: [CR-007]" ]
              "PlanDecisionRefs", [ "planDecisionRefs", "planDecisionRefs: [PD-007]" ]
              "ObligationRefs", [ "obligationRefs", "obligationRefs: [obligval]" ]
              "ArtifactRefs", [ "artifacts", $"artifacts: [{artifactRef.Path}]" ]
              "SourceRefs", [ "sourceRefs", "sourceRefs:\n  - kind: srckindval" ]
              "Result", [ "result", "result: advisory" ]
              "Synthetic", [ "synthetic", "synthetic: true" ]
              "SyntheticDisclosure", [ "syntheticDisclosure", "syntheticDisclosure:\n  standsInFor: standsval" ]
              "ObservedRun", [ "observedRun", "observedRun:\n  source: observedsourceval" ]
              "Rationale", [ "rationale", "rationale: rationaleval" ]
              "Owner", [ "owner", "owner: ownerval" ]
              "Scope", [ "scope", "scope: scopeval" ]
              "LaterLifecycleVisibility", [ "laterLifecycleVisibility", "laterLifecycleVisibility: visibilityval" ]
              "Notes", [ "notes", "notes: [noteval]" ] ]

    [<Fact>]
    let ``taskFields couple to every authored WorkTask field (T031/#290)`` () =
        // `Id`/`Source`/`SourceLocation` are provenance, excluded. `Status` (a DU) spans two keys:
        // `status` (the tag) and `skipRationale` (the `Skipped` payload).
        let model =
            { TaskCodec.taskSeed with
                Title = "titleval"
                Status = Skipped "skiprationaleval"
                Owner = "ownerval"
                Dependencies = [ createTaskId "T007" |> orFail "taskId" ]
                Requirements = [ createRequirementId "FR-007" |> orFail "reqId" ]
                Decisions = [ createDecisionId "DEC-007" |> orFail "decId" ]
                SourceIds = [ "SRCIDVAL" ]
                RequiredSkills = [ "skillval" ]
                RequiredEvidence = [ createEvidenceId "EV007" |> orFail "evId" ] }

        assertCoupled
            [ "Id"; "Source"; "SourceLocation" ]
            typeof<WorkTask>
            TaskCodec.taskFields
            model
            [ "Title", [ "title", "title: titleval" ]
              "Status",
              [ "status", "status: skipped"
                "skipRationale", "skipRationale: skiprationaleval" ]
              "Owner", [ "owner", "owner: ownerval" ]
              "Dependencies", [ "dependencies", "dependencies: [T007]" ]
              "Requirements", [ "requirements", "requirements: [FR-007]" ]
              "Decisions", [ "decisions", "decisions: [DEC-007]" ]
              "SourceIds", [ "sourceIds", "sourceIds: [SRCIDVAL]" ]
              "RequiredSkills", [ "requiredSkills", "requiredSkills: [skillval]" ]
              "RequiredEvidence", [ "requiredEvidence", "requiredEvidence: [EV007]" ] ]

    // === The ArtifactCodec migration boundary is complete (ADR-0002 §Note; re-scopes #338) ===
    // Every authored/lifecycle artifact is round-tripped by exactly ONE mechanism. This manifest is
    // the executable statement of that boundary: the codec migration (Gap A, invariant 1) is COMPLETE,
    // and the artifacts NOT on the codec are out of scope BY DESIGN, not unfinished work. Pinning it as
    // data makes reclassifying or adding an artifact a conscious, review-visible change — and stops the
    // "~N artifacts still to migrate" over-count (FS.GG.SDD#338) from being re-derived from the raw
    // reader-in-Artifacts / writer-in-Commands heuristic.
    type private RoundTrip =
        | Codec // one FieldCodec list drives read AND write (the Gap A codec)
        | TextSpaceIdentity // author-owned prose; the re-emit is `ensure*Sections x = x` (#288)
        | NotRoundTripped // readiness JSON / read-only / generated view — not an authored round-trip artifact

    let private migrationBoundary: (string * RoundTrip) list =
        [ "tasks.yml", Codec
          "evidence.yml", Codec
          "spec.md", TextSpaceIdentity
          "clarifications.md", TextSpaceIdentity
          "checklist.md", TextSpaceIdentity
          "plan.md", TextSpaceIdentity
          "charter.md", TextSpaceIdentity
          "analysis.json", NotRoundTripped
          "verify.json", NotRoundTripped
          "ship.json", NotRoundTripped
          "registryDocument", NotRoundTripped
          "config", NotRoundTripped
          "requirementModel", NotRoundTripped
          "guidance", NotRoundTripped ]

    [<Fact>]
    let ``the ArtifactCodec migration boundary is complete`` () =
        // (1) every artifact is classified exactly once.
        let names = migrationBoundary |> List.map fst
        Assert.Equal<string list>(List.distinct names, names)

        // (2) the codec-migrated set is EXACTLY tasks.yml and evidence.yml — the whole of Gap A's
        //     codec surface. A new artifact migrated onto the codec, or one of these dropped, fails here.
        let codecArtifacts =
            migrationBoundary
            |> List.choose (fun (name, kind) -> if kind = Codec then Some name else None)
            |> List.sort

        Assert.Equal<string list>([ "evidence.yml"; "tasks.yml" ], codecArtifacts)

        // (3) tie the manifest's Codec claim to the REAL field lists that drive those two artifacts'
        //     reader and renderer (coupled field-by-field in the T031 tests above), so this guards the
        //     code, not just the table.
        Assert.NotEmpty(ArtifactCodec.keys TaskCodec.taskFields)
        Assert.NotEmpty(ArtifactCodec.keys EvidenceCodec.declarationFields)

        // (4) the full classification shape is present: the five Markdown docs are text-space by
        //     design, and the seven readiness-JSON / read-only / generated artifacts are not round-tripped.
        let count kind =
            migrationBoundary |> List.filter (fun (_, k) -> k = kind) |> List.length

        Assert.Equal(2, count Codec)
        Assert.Equal(5, count TextSpaceIdentity)
        Assert.Equal(7, count NotRoundTripped)
