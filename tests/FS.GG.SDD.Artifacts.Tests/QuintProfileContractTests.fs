namespace FS.GG.SDD.Artifacts.Tests

open System.IO
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

    let private generalTypedEffect =
        """{"stage":"typechecking","modules":[{"id":100,"name":"Consumer","declarations":[{"id":1,"kind":"var","name":"state","typeAnnotation":{"id":2,"kind":"int"},"depth":0},{"id":20,"kind":"def","name":"rules","qualifier":"pureval","expr":{"id":19,"kind":"app","opcode":"Set","args":[{"id":18,"kind":"app","opcode":"Rec","args":[{"id":11,"kind":"str","value":"id"},{"id":12,"kind":"str","value":"RULE-B"},{"id":13,"kind":"str","value":"kind"},{"id":14,"kind":"str","value":"formula"},{"id":15,"kind":"str","value":"dependencies"},{"id":16,"kind":"app","opcode":"Set","args":[{"id":17,"kind":"str","value":"RULE-A"}]}]},{"id":10,"kind":"app","opcode":"Rec","args":[{"id":3,"kind":"str","value":"id"},{"id":4,"kind":"str","value":"RULE-A"},{"id":5,"kind":"str","value":"kind"},{"id":6,"kind":"str","value":"fact"},{"id":7,"kind":"str","value":"dependencies"},{"id":8,"kind":"app","opcode":"Set","args":[]}]}]},"depth":0},{"id":30,"kind":"def","name":"step","qualifier":"action","expr":{"id":29,"kind":"app","opcode":"assign","args":[{"id":27,"kind":"name","name":"state"},{"id":28,"kind":"int","value":1}]},"depth":0}]}],"table":{"30":{"id":30,"kind":"def","name":"step","qualifier":"action","expr":{"id":29,"kind":"app","opcode":"assign","args":[{"id":27,"kind":"name","name":"state"},{"id":28,"kind":"int","value":1}]}}},"types":{"30":{"kind":"bool"}},"effects":{"30":{"effect":{"kind":"concrete","components":[{"kind":"read","entity":{"kind":"concrete","stateVariables":[{"name":"state","reference":1}]}},{"kind":"update","entity":{"kind":"concrete","stateVariables":[{"name":"state","reference":1}]}}]},"effectVariables":[],"entityVariables":[]}},"errors":[],"warnings":[]}"""

    let private generalObservation profile =
        { Profile = profile
          QuintVersion = QuintGeneralProfile.quintVersion
          TypedEffectJson = generalTypedEffect
          ExportBindings =
            [ { Id = "EXPORT-Rules"
                ModuleName = "Consumer"
                DeclarationName = "rules"
                PromoteCatalogueRows = true
                Source = source 30 } ]
          ActionBindings =
            [ { ModuleName = "Consumer"
                CatalogueName = "step"
                Id = "ACT-Step"
                Kind = Action
                Source = source 40 } ] }

    [<Fact>]
    let ``general profile accepts consumer exports without a program digest`` () =
        let adapted =
            generalObservation QuintGeneralProfile.identity
            |> QuintGeneralProfile.adaptTypedEffectJson
            |> expectOk

        Assert.Equal([ "EXPORT-Rules" ], adapted.Exports |> List.map _.Id)
        Assert.Equal([ "RULE-A"; "RULE-B" ], adapted.Catalogue |> List.map _.Id)

        let effect = Assert.Single adapted.ActionEffects
        Assert.Equal("ACT-Step", effect.ActionId)
        Assert.Equal<string list>([ "state" ], effect.Reads)
        Assert.Equal<string list>([ "state" ], effect.Writes)

    [<Fact>]
    let ``general profile refuses substitution and nonconstant exports distinctly`` () =
        let substitution =
            generalObservation QuintProfile.identity
            |> QuintGeneralProfile.adaptTypedEffectJson
            |> findings

        Assert.Contains(substitution, fun item -> item.Code = "QUINT-PROFILE-IDENTITY")

        let nonconstant =
            { generalObservation QuintGeneralProfile.identity with
                ExportBindings =
                    [ { Id = "EXPORT-Step"
                        ModuleName = "Consumer"
                        DeclarationName = "step"
                        PromoteCatalogueRows = false
                        Source = source 40 } ] }
            |> QuintGeneralProfile.adaptTypedEffectJson
            |> findings

        Assert.Contains(nonconstant, fun item -> item.Code = "QUINT-GENERAL-EXPORT-EXPRESSION")

    [<Fact>]
    let ``general profile reports structural warning source and action mutations together`` () =
        let baseline = generalObservation QuintGeneralProfile.identity

        let invalid =
            { baseline with
                TypedEffectJson =
                    baseline.TypedEffectJson
                        .Replace("\"stage\":", "\"future\":{},\"stage\":")
                        .Replace("\"warnings\":[]", "\"warnings\":[{\"message\":\"mutant\"}]")
                ExportBindings =
                    baseline.ExportBindings
                    |> List.map (fun binding ->
                        { binding with
                            Source =
                                { binding.Source with
                                    Path = "../escape.md"
                                    End = { Line = 1; Column = 1 } } })
                ActionBindings =
                    baseline.ActionBindings
                    |> List.map (fun binding -> { binding with Kind = Requirement }) }

        let codes =
            QuintGeneralProfile.adaptTypedEffectJson invalid |> findings |> List.map _.Code

        Assert.Contains("QUINT-IR-UNSUPPORTED-FIELD", codes)
        Assert.Contains("QUINT-GENERAL-COMPILER-WARNINGS", codes)
        Assert.Contains("QUINT-GENERAL-SOURCE-PATH", codes)
        Assert.Contains("QUINT-GENERAL-SOURCE-RANGE", codes)
        Assert.Contains("QUINT-GENERAL-ACTION-BINDING", codes)

    [<Fact>]
    let ``general binding manifest is canonical strict and carries no semantic values`` () =
        let observation = generalObservation QuintGeneralProfile.identity

        let manifest =
            { Schema = QuintGeneralBindingManifest.schema
              Profile = observation.Profile
              ModuleName = "ConsumerRules"
              Exports = observation.ExportBindings
              Actions = observation.ActionBindings }

        let canonical = QuintGeneralBindingManifest.serializeCanonical manifest |> expectOk
        let roundTrip = QuintGeneralBindingManifest.deserialize canonical |> expectOk
        Assert.Equal(canonical, QuintGeneralBindingManifest.serializeCanonical roundTrip |> expectOk)
        Assert.DoesNotContain("RULE-A", canonical)
        Assert.DoesNotContain("formula", canonical)

        let injected = canonical.Replace("\"profile\":", "\"value\":{},\"profile\":")

        Assert.Contains(
            findings (QuintGeneralBindingManifest.deserialize injected),
            fun item -> item.Code = "QUINT-GENERAL-BINDINGS-MALFORMED"
        )

    [<Fact>]
    let ``complete SIR fixture selectors remain canonical and source bound`` () =
        let path =
            Path.Combine(System.AppContext.BaseDirectory, "Fixtures", "QuintGeneralSir", "profile-bindings.json")

        let canonical = File.ReadAllText path
        let manifest = QuintGeneralBindingManifest.deserialize canonical |> expectOk

        Assert.Equal(8, manifest.Exports.Length)
        Assert.Equal(5, manifest.Actions.Length)

        let sourcePaths: string list =
            (manifest.Exports |> List.map _.Source.Path)
            @ (manifest.Actions |> List.map _.Source.Path)

        Assert.All(
            sourcePaths,
            fun sourcePath -> Assert.Equal("tests/fixtures/quint-general-sir/sir-combat.md", sourcePath)
        )

        Assert.Equal(canonical, QuintGeneralBindingManifest.serializeCanonical manifest |> expectOk)

    let private contractV2 () =
        let adapted =
            generalObservation QuintGeneralProfile.identity
            |> QuintGeneralProfile.adaptTypedEffectJson
            |> expectOk

        { Schema = QuintContractV2.schema
          Profile = QuintGeneralProfile.identity
          Specification = "Consumer"
          Exports = adapted.Exports
          Catalogue = adapted.Catalogue
          ActionEffects = adapted.ActionEffects
          Relationships = []
          VerificationProfiles = []
          Bounds = []
          Impacts = []
          Compatibility = []
          Digests = [ { Name = "source"; Sha256 = digest 'c' } ] }

    [<Fact>]
    let ``contract v2 and generic bindings preserve rich Quint values canonically`` () =
        let expected = contractV2 ()
        let first = QuintContractV2.serializeCanonical expected |> expectOk
        let second = QuintContractV2.serializeCanonical expected |> expectOk
        let roundTrip = QuintContractV2.deserialize first |> expectOk

        Assert.Equal(first, second)
        Assert.Equal(first, QuintContractV2.serializeCanonical roundTrip |> expectOk)

        let bindings = QuintBindingsV2.generate "ConsumerRules" expected |> expectOk
        Assert.Equal(bindings.FSharpSource, bindings.FableSource)
        Assert.Contains("type QuintValue =", bindings.FSharpSource)
        Assert.Contains("RULE-A", bindings.FSharpSource)
        Assert.Contains("dependencies", bindings.FSharpSource)
