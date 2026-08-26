namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module QuintProfileContractTests =
    let private digest character = System.String(character, 64)

    let private source line =
        { Path = "docs/specifications/example.md"
          Start = { Line = line; Column = 1 }
          End = { Line = line; Column = 20 } }

    let private catalogue =
        [ { Id = "ACT-Apply"
            Kind = Action
            Source = source 12 }
          { Id = "STATE-Value"
            Kind = StateVariable
            Source = source 8 }
          { Id = "REQ-Safety"
            Kind = Requirement
            Source = source 4 }
          { Id = "INV-Safe"
            Kind = Invariant
            Source = source 20 }
          { Id = "EV-Check"
            Kind = Evidence
            Source = source 24 } ]

    let private contract () =
        { Schema = QuintContract.schema
          Profile = QuintProfile.identity
          Specification = "ExampleSpecification"
          Catalogue = catalogue
          ActionEffects =
            [ { ActionId = "ACT-Apply"
                Reads = [ "STATE-Value" ]
                Writes = [ "STATE-Value" ]
                Subjects = [ "REQ-Safety" ] } ]
          Relationships =
            [ { FromId = "REQ-Safety"
                Kind = VerifiedBy
                ToId = "EV-Check" }
              { FromId = "INV-Safe"
                Kind = Requires
                ToId = "REQ-Safety" } ]
          VerificationProfiles =
            [ { Id = "VERIFY-Bounded"
                Kind = "apalache"
                SubjectIds = [ "INV-Safe" ]
                BoundIds = [ "BOUND-Steps" ] } ]
          Bounds =
            [ { Id = "BOUND-Steps"
                Minimum = 0L
                Maximum = 8L } ]
          Impacts =
            [ { SubjectId = "REQ-Safety"
                Category = "contract"
                Detail = "The safety obligation is externally visible." } ]
          Compatibility =
            [ { Surface = "generated-bindings"
                Requirement = "additive"
                Detail = "Profile 1 identifiers remain stable." } ]
          Digests =
            [ { Name = "canonicalSource"
                Sha256 = digest 'a' }
              { Name = "generatedModules"
                Sha256 = digest 'b' } ] }

    let private expectOk =
        function
        | Ok value -> value
        | Error findings -> failwithf "expected success, got %A" findings

    let private findings =
        function
        | Ok _ -> failwith "expected refusal"
        | Error values -> values

    [<Fact>]
    let ``adapter refuses absent exact-output facts and wrong out-of-band version distinctly`` () =
        let observation version =
            { Profile = QuintProfile.identity
              QuintVersion = version
              TypedEffectJson = "{}"
              SourceBindings = [] }

        Assert.Contains(
            findings (QuintProfile.adaptTypedEffectJson (observation QuintProfile.quintVersion)),
            fun (finding: QuintProfileDiagnostic) -> finding.Code = "QUINT-IR-REQUIRED"
        )

        Assert.Contains(
            findings (QuintProfile.adaptTypedEffectJson (observation "0.33.0")),
            fun (finding: QuintProfileDiagnostic) -> finding.Code = "QUINT-PROFILE-VERSION"
        )

    [<Fact>]
    let ``profile diagnostics retain safe literate paths and ordered source ranges`` () =
        let invalid =
            { Profile = QuintProfile.identity
              QuintVersion = QuintProfile.quintVersion
              Entries =
                [ { Id = "bad"
                    Kind = Requirement
                    Source =
                      { Path = "../escape.md"
                        Start = { Line = 2; Column = 3 }
                        End = { Line = 1; Column = 1 } } } ]
              ActionEffects = [] }

        let codes = QuintProfile.validate invalid |> List.map _.Code
        Assert.Contains("QUINT-PROFILE-ID", codes)
        Assert.Contains("QUINT-PROFILE-SOURCE-PATH", codes)
        Assert.Contains("QUINT-PROFILE-SOURCE-RANGE", codes)

    [<Fact>]
    let ``compiled contract codec is canonical strict and byte stable`` () =
        let expected = contract ()
        let first = QuintContract.serializeCanonical expected |> expectOk
        let second = QuintContract.serializeCanonical expected |> expectOk
        let roundTrip = QuintContract.deserialize first |> expectOk

        Assert.Equal(first, second)
        Assert.Equal(first, QuintContract.serializeCanonical roundTrip |> expectOk)
        Assert.EndsWith("\n", first)

        let injectedExpression =
            first.Replace("\"specification\":", "\"expression\":{},\"specification\":")

        Assert.Contains(
            findings (QuintContract.deserialize injectedExpression),
            fun finding -> finding.Code = "QUINT-CONTRACT-MALFORMED"
        )

    [<Fact>]
    let ``contract refuses unresolved facts reversed bounds and malformed digests together`` () =
        let invalid =
            { contract () with
                Relationships =
                    [ { FromId = "REQ-Missing"
                        Kind = VerifiedBy
                        ToId = "EV-Check" } ]
                Bounds =
                    [ { Id = "BOUND-Steps"
                        Minimum = 9L
                        Maximum = 2L } ]
                Digests = [ { Name = "source"; Sha256 = "latest" } ] }

        let codes = QuintContract.validate invalid |> List.map _.Code
        Assert.Contains("QUINT-CONTRACT-REFERENCE", codes)
        Assert.Contains("QUINT-CONTRACT-BOUND", codes)
        Assert.Contains("QUINT-CONTRACT-DIGEST", codes)

    [<Fact>]
    let ``compilation fingerprint binds every semantic input and diff names changed component`` () =
        let inputs =
            { SourceSha256 = digest '1'
              FenceManifestSha256 = digest '2'
              GeneratedModulesSha256 = digest '3'
              ToolchainSha256 = digest '4'
              Contract = contract () }

        let first = QuintContract.fingerprint inputs |> expectOk
        let second = QuintContract.fingerprint inputs |> expectOk
        Assert.Equal(64, first.Length)
        Assert.Equal(first, second)

        let changed =
            { contract () with
                Impacts =
                    [ { SubjectId = "REQ-Safety"
                        Category = "contract"
                        Detail = "Changed integration meaning." } ] }

        match QuintContract.semanticDiff (contract ()) changed |> expectOk with
        | QuintContractDiff.Changed changes -> Assert.Contains(changes, fun change -> change.Path = "/impacts")
        | QuintContractDiff.Equivalent -> Assert.Fail("expected an integration-meaning change")

        Assert.Contains(
            findings (
                QuintContract.fingerprint
                    { inputs with
                        ToolchainSha256 = "moving-latest" }
            ),
            fun finding -> finding.Code = "QUINT-FINGERPRINT-DIGEST"
        )

    [<Fact>]
    let ``semantic diff ignores order already normalized by canonical contract bytes`` () =
        let original = contract ()

        let reordered =
            { original with
                Catalogue = List.rev original.Catalogue
                ActionEffects =
                    original.ActionEffects
                    |> List.map (fun effect ->
                        { effect with
                            Reads = List.rev effect.Reads
                            Writes = List.rev effect.Writes
                            Subjects = List.rev effect.Subjects })
                Relationships = List.rev original.Relationships
                VerificationProfiles =
                    original.VerificationProfiles
                    |> List.map (fun profile ->
                        { profile with
                            SubjectIds = List.rev profile.SubjectIds
                            BoundIds = List.rev profile.BoundIds })
                Bounds = List.rev original.Bounds
                Impacts = List.rev original.Impacts
                Compatibility = List.rev original.Compatibility
                Digests = List.rev original.Digests }

        Assert.Equal(
            QuintContract.serializeCanonical original |> expectOk,
            QuintContract.serializeCanonical reordered |> expectOk
        )

        Assert.Equal(QuintContractDiff.Equivalent, QuintContract.semanticDiff original reordered |> expectOk)
