namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module QuintReplayBindingsTests =
    let private digest = String.replicate 64 "a"

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error findings -> failwithf "expected success, got %A" findings

    let private sourceRange : QuintSourceRange =
        { Path = "docs/experiments/quint-q1/slices/login.md"
          Start = { Line = 10; Column = 1 }
          End = { Line = 10; Column = 12 } }

    let private entry id kind : QuintCatalogueEntry =
        { Id = id
          Kind = kind
          Source = sourceRange }

    let private contract : QuintCompiledContract =
        { Schema = QuintContract.schema
          Profile = QuintProfile.identity
          Specification = "LoginSpec"
          Catalogue =
            [ entry "SESSION-ID" QuintCatalogueKind.StateVariable
              entry "ADVANCE" QuintCatalogueKind.Action ]
          ActionEffects =
            [ { ActionId = "ADVANCE"
                Reads = [ "SESSION-ID" ]
                Writes = [ "SESSION-ID" ]
                Subjects = [ "SESSION-ID" ] } ]
          Relationships = []
          VerificationProfiles = []
          Bounds = []
          Impacts = []
          Compatibility = []
          Digests = [] }

    let private replaySource : QuintReplaySourceBinding =
        { Path = sourceRange.Path
          Line = 10
          Column = 1 }

    let private state bindings =
        let draft : QuintReplayState =
            { Identity = digest
              Bindings = bindings }

        { draft with
            Identity = QuintReplay.stateFingerprint draft |> expectOk }

    let private initialState =
        state
            [ "attempts", QuintReplayValue.Integer "0"
              "session", QuintReplayValue.Text "pending" ]

    let private expectedState =
        state
            [ "attempts", QuintReplayValue.Integer "1"
              "session", QuintReplayValue.Text "accepted" ]

    let private environment contractFingerprint : QuintReplayEnvironment =
        { Seed = "923"
          Bounds = [ "steps", 1L ]
          ToolFingerprint = digest
          ProfileFingerprint = digest
          ContractFingerprint = contractFingerprint
          AdapterFingerprint = digest
          ImplementationFingerprint = digest }

    let private trace contractFingerprint : QuintReplayTrace =
        { SchemaVersion = 1
          TraceIdentity = digest
          Environment = environment contractFingerprint
          Initial = initialState
          Steps =
            [ { Index = 1
                Action = "ADVANCE"
                Source = replaySource
                Expected = expectedState } ] }

    let private observation actual : QuintReplayObservation =
        { Index = 1
          Action = "ADVANCE"
          Source = replaySource
          Actual = actual }

    let private replayDiagnosticCodes (findings: QuintReplayDiagnostic list) =
        findings |> List.map _.Code |> Set.ofList

    let private bindingDiagnosticCodes (findings: QuintBindingDiagnostic list) =
        findings |> List.map _.Code |> Set.ofList

    [<Fact>]
    let ``replay accepts the matching fingerprinted observation`` () =
        let bindings = QuintBindings.generate "LoginContract" contract |> expectOk
        let result = QuintReplay.compare (trace bindings.ContractFingerprint) [ observation expectedState ]
        Assert.Equal(Ok QuintReplayResult.Equivalent, result)

    [<Fact>]
    let ``replay reports the exact first divergent step action source and states`` () =
        let bindings = QuintBindings.generate "LoginContract" contract |> expectOk

        let wrongState =
            state
                [ "attempts", QuintReplayValue.Integer "1"
                  "session", QuintReplayValue.Text "rejected" ]

        match QuintReplay.compare (trace bindings.ContractFingerprint) [ observation wrongState ] with
        | Ok(QuintReplayResult.Diverged divergence) ->
            Assert.Equal(1, divergence.Step)
            Assert.Equal("ADVANCE", divergence.Action)
            Assert.Equal(replaySource, divergence.Source)
            Assert.Equal(Some expectedState, divergence.Expected)
            Assert.Equal(Some wrongState, divergence.Actual)
            Assert.Equal("state", divergence.Reason)
        | result -> failwithf "expected a state divergence, got %A" result

    [<Fact>]
    let ``replay refuses malformed environment order state and source bindings`` () =
        let bindings = QuintBindings.generate "LoginContract" contract |> expectOk
        let valid = trace bindings.ContractFingerprint

        let malformedEnvironment =
            { valid with
                Environment =
                    { valid.Environment with
                        ToolFingerprint = "not-a-sha256" } }

        let malformedOrder =
            { valid with
                Steps = [ { valid.Steps.Head with Index = 2 } ] }

        let malformedState =
            { valid with
                Steps =
                    [ { valid.Steps.Head with
                          Expected =
                            { valid.Steps.Head.Expected with
                                Identity = digest } } ] }

        let malformedSource =
            { valid with
                Steps =
                    [ { valid.Steps.Head with
                          Source = { replaySource with Line = 0 } } ] }

        Assert.Contains("QRP-ENV-FINGERPRINT", malformedEnvironment |> QuintReplay.validateTrace |> replayDiagnosticCodes)
        Assert.Contains("QRP-STEP-ORDER", malformedOrder |> QuintReplay.validateTrace |> replayDiagnosticCodes)
        Assert.Contains("QRP-STATE-FINGERPRINT", malformedState |> QuintReplay.validateTrace |> replayDiagnosticCodes)
        Assert.Contains("QRP-SOURCE-LINE", malformedSource |> QuintReplay.validateTrace |> replayDiagnosticCodes)

    [<Fact>]
    let ``bindings derive deterministically from compiled contract v1 for native and Fable`` () =
        let first = QuintBindings.generate "LoginContract" contract |> expectOk
        let second = QuintBindings.generate "LoginContract" contract |> expectOk

        Assert.Equal(first, second)
        Assert.Equal(first.FSharpSource, first.FableSource)
        Assert.Equal<string list>([ "Advance"; "SessionId" ], first.Identifiers)
        Assert.EndsWith("\n", first.CanonicalJson)
        Assert.Contains("let Advance = \"ADVANCE\"", first.FSharpSource)
        Assert.Contains("let SessionId = \"SESSION-ID\"", first.FSharpSource)
        Assert.Contains(first.ContractFingerprint, first.FSharpSource)

    [<Fact>]
    let ``bindings refuse wrong compiled contract schema and profile`` () =
        let wrongSchema = { contract with Schema = "fsgg.quint.compiled-contract/v2" }
        let wrongProfile = { contract with Profile = "fsgg-quint-profile/2" }

        let schemaCodes =
            match QuintBindings.generate "LoginContract" wrongSchema with
            | Ok _ -> failwith "expected schema refusal"
            | Error findings -> bindingDiagnosticCodes findings

        let profileCodes =
            match QuintBindings.generate "LoginContract" wrongProfile with
            | Ok _ -> failwith "expected profile refusal"
            | Error findings -> bindingDiagnosticCodes findings

        Assert.Contains("QUINT-CONTRACT-SCHEMA", schemaCodes)
        Assert.Contains("QUINT-CONTRACT-PROFILE", profileCodes)

    [<Fact>]
    let ``bindings refuse duplicate and colliding catalogue identifiers`` () =
        let duplicate =
            { contract with
                Catalogue =
                    [ entry "ADVANCE" QuintCatalogueKind.Action
                      entry "ADVANCE" QuintCatalogueKind.Action ] }

        let collision =
            { contract with
                Catalogue =
                    [ entry "USER-ID" QuintCatalogueKind.StateVariable
                      entry "USER.ID" QuintCatalogueKind.Action ]
                ActionEffects = [] }

        let duplicateCodes =
            match QuintBindings.generate "LoginContract" duplicate with
            | Ok _ -> failwith "expected duplicate refusal"
            | Error findings -> bindingDiagnosticCodes findings

        let collisionCodes =
            match QuintBindings.generate "LoginContract" collision with
            | Ok _ -> failwith "expected collision refusal"
            | Error findings -> bindingDiagnosticCodes findings

        Assert.Contains("QUINT-CONTRACT-CATALOGUE-DUPLICATE", duplicateCodes)
        Assert.Contains("QBD-CATALOGUE-ID-DUPLICATE", duplicateCodes)
        Assert.Contains("QBD-IDENTIFIER-COLLISION", collisionCodes)
