namespace FS.GG.SDD.Artifacts.Tests

open System
open System.Text
open System.Globalization
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module TypedLifecycleTests =
    let private bytes (value: string) = Encoding.UTF8.GetBytes value

    let private manifest canonical normalized markdown =
        { SchemaVersion = 1
          Lifecycle = "typed-sdd"
          Backend = "fsharp-specification-v1"
          CompilerIdentity = "dotnet-fsi/net10.0"
          PackageIdentity = "FS.GG.SDD.Artifacts/1.4.0"
          ExtensionIdentity = "fsgg.requirements-extension/v1"
          CanonicalPath = "work/demo/specification.fsx"
          CanonicalSha256 = TypedAuthorityManifest.sha256 canonical
          NormalizedPath = "readiness/demo/specification.normalized.json"
          NormalizedSha256 = TypedAuthorityManifest.sha256 normalized
          MarkdownPath = "work/demo/spec.md"
          MarkdownSha256 = TypedAuthorityManifest.sha256 markdown
          AuthoringAgent = "tern-001"
          AuthoringSession = "session-1"
          RollbackSourceSha256 = None }

    [<Fact>]
    let ``omitted lifecycle remains Standard SDD and explicit lanes never alias`` () =
        Assert.Equal(Ok StandardSdd, LifecycleLane.resolve None)
        Assert.Equal(Ok NoLifecycle, LifecycleLane.resolve (Some "none"))
        Assert.Equal(Ok StandardSdd, LifecycleLane.resolve (Some "sdd"))
        Assert.Equal(Ok TypedSdd, LifecycleLane.resolve (Some "typed-sdd"))
        Assert.Equal(Ok LegacySpecKit, LifecycleLane.resolve (Some "spec-kit"))
        Assert.NotEqual(LifecycleLane.backend StandardSdd, LifecycleLane.backend TypedSdd)

    [<Fact>]
    let ``scaffold provenance selects the representation backend without fallback`` () =
        let provenance =
            ScaffoldProvenance.devRepoRecord { Id = "FS.GG.SDD"; Version = "1.4.0" } []

        Assert.Equal(Ok StandardSdd, ScaffoldProvenance.lifecycleLane provenance)

        Assert.Equal(
            Ok TypedSdd,
            ScaffoldProvenance.lifecycleLane
                { provenance with
                    EffectiveParameters = [ "lifecycle", "typed-sdd" ] }
        )

    [<Fact>]
    let ``authority manifest round trips deterministically`` () =
        let source, normalized, markdown = bytes "source", bytes "{}", bytes "# projection"
        let expected = manifest source normalized markdown
        let encoded = TypedAuthorityManifest.serialize expected
        Assert.Equal(encoded, TypedAuthorityManifest.serialize expected)
        Assert.Equal(Ok expected, TypedAuthorityManifest.deserialize encoded)

    [<Fact>]
    let ``negative controls have distinct stable diagnostic identities`` () =
        let source, normalized, markdown = bytes "source", bytes "{}", bytes "# projection"

        let authority =
            { manifest source normalized markdown with
                Lifecycle = "sdd"
                PackageIdentity = "wrong" }

        let findings =
            TypedAuthorityManifest.validate
                "FS.GG.SDD.Artifacts/1.4.0"
                false
                (Some(bytes "direct edit"))
                (Some(bytes "stale"))
                (Some markdown)
                authority

        let ids = findings |> List.map _.Id |> Set.ofList
        Assert.Contains("typedSdd.wrongLifecycle", ids)
        Assert.Contains("typedSdd.compilerUnavailable", ids)
        Assert.Contains("typedSdd.identityMismatch", ids)
        Assert.Contains("typedSdd.directCanonicalEdit", ids)
        Assert.Contains("typedSdd.staleProjection", ids)
        Assert.Equal(findings.Length, ids.Count)

    [<Fact>]
    let ``compiler extension and authoring receipt identities fail closed`` () =
        let source, normalized, markdown = bytes "source", bytes "{}", bytes "# projection"

        let authority =
            { manifest source normalized markdown with
                CompilerIdentity = "unknown"
                ExtensionIdentity = "unknown"
                AuthoringAgent = ""
                AuthoringSession = "" }

        let ids =
            TypedAuthorityManifest.validate
                "FS.GG.SDD.Artifacts/1.4.0"
                true
                (Some source)
                (Some normalized)
                (Some markdown)
                authority
            |> List.map _.Id

        Assert.Contains("typedSdd.compilerIdentityMismatch", ids)
        Assert.Contains("typedSdd.extensionIdentityMismatch", ids)
        Assert.Contains("typedSdd.authoringReceiptMissing", ids)

    let private expectOk result =
        match result with
        | Ok value -> value
        | Error findings -> failwithf "expected success, got %A" findings

    let private generatedDigest target (moduleBytes: byte array) =
        let frame (value: string) =
            let valueBytes = Encoding.UTF8.GetBytes value

            Array.concat
                [ Encoding.ASCII.GetBytes(valueBytes.Length.ToString(CultureInfo.InvariantCulture) + ":")
                  valueBytes ]

        [ target
          TypedAuthorityManifest.sha256 moduleBytes
          moduleBytes.LongLength.ToString(CultureInfo.InvariantCulture) ]
        |> List.collect (frame >> Array.toList)
        |> List.toArray
        |> TypedAuthorityManifest.sha256

    let private quintArtifact id path content =
        { Id = id
          Path = path
          Sha256 = content |> TypedAuthorityManifest.sha256 }

    let private quintFixture () =
        let markdown = bytes "# specification\n```quint demo.qnt +=\nmodule Demo {}\n```\n"

        let source =
            QuintSource.createMarkdown "work/demo/specification.md" markdown |> expectOk

        let typedEffectBytes = bytes "{\"typed\":true}\n"
        let typedEffectDigest = TypedAuthorityManifest.sha256 typedEffectBytes

        let range =
            { Path = source.Path
              Start = { Line = 3; Column = 1 }
              End = { Line = 3; Column = 14 } }

        let fenceRange =
            { Path = source.Path
              Start = { Line = 2; Column = 1 }
              End = { Line = 4; Column = 3 } }

        let contract =
            { Schema = QuintContract.schema
              Profile = QuintProfile.identity
              Specification = "DemoSpec"
              Catalogue =
                [ { Id = "STATE"
                    Kind = QuintCatalogueKind.StateVariable
                    Source = range }
                  { Id = "ADVANCE"
                    Kind = QuintCatalogueKind.Action
                    Source = range } ]
              ActionEffects =
                [ { ActionId = "ADVANCE"
                    Reads = [ "STATE" ]
                    Writes = [ "STATE" ]
                    Subjects = [ "STATE" ] } ]
              Relationships = []
              VerificationProfiles = []
              Bounds = []
              Impacts = []
              Compatibility = []
              Digests =
                [ { Name = "sandbox-contract"
                    Sha256 = TypedAuthorityManifest.sha256 QuintSandbox.contractBytes }
                  { Name = "typed-effect"
                    Sha256 = typedEffectDigest } ] }

        let contractText = QuintContract.serializeCanonical contract |> expectOk
        let contractBytes = bytes contractText
        let moduleBytes = bytes "module Demo {}\n"

        let fenceManifest =
            { Schema = QuintSource.fenceManifestSchema
              SourcePath = source.Path
              SourceSha256 = source.Sha256
              Fences =
                [ { Ordinal = 0
                    Target = "demo.qnt"
                    ModuleName = "Demo"
                    SourceRange = fenceRange
                    ContentSha256 = TypedAuthorityManifest.sha256 moduleBytes } ] }

        let fenceBytes = QuintSource.encodeFenceManifest fenceManifest

        let sourceMap =
            { Schema = QuintSource.sourceMapSchema
              SourceSha256 = source.Sha256
              Entries =
                [ { Target = "demo.qnt"
                    GeneratedRange =
                      { Path = "demo.qnt"
                        Start = { Line = 1; Column = 1 }
                        End = { Line = 1; Column = 14 } }
                    Source = { FenceOrdinal = 0; Range = range } } ] }

        let sourceMapBytes = QuintSource.encodeSourceMap sourceMap
        let generatedModulesDigest = generatedDigest "demo.qnt" moduleBytes
        let toolchain = QuintToolchain.fingerprint QuintToolchain.q1

        let compilationFingerprint =
            QuintContract.fingerprint
                { SourceSha256 = source.Sha256
                  FenceManifestSha256 = TypedAuthorityManifest.sha256 fenceBytes
                  GeneratedModulesSha256 = generatedModulesDigest
                  ToolchainSha256 = toolchain
                  Contract = contract }
            |> expectOk

        let receipt =
            { Schema = QuintCompiler.receiptSchema
              SourceSha256 = source.Sha256
              FenceManifestSha256 = TypedAuthorityManifest.sha256 fenceBytes
              GeneratedModulesSha256 = generatedModulesDigest
              ToolchainSha256 = toolchain
              TypedEffectSha256 = typedEffectDigest
              ContractSha256 = TypedAuthorityManifest.sha256 contractBytes
              CompilationFingerprint = compilationFingerprint
              ProcessSteps = [ "extract"; "typecheck" ] }

        let bindings = QuintBindings.generate "RequirementsBindings" contract |> expectOk

        let contents =
            Map
                [ "markdown", markdown
                  "fence-manifest", fenceBytes
                  "generated-modules", moduleBytes
                  "source-map", sourceMapBytes
                  "typed-effect", typedEffectBytes
                  "sandbox-contract", QuintSandbox.contractBytes
                  "compiled-contract", contractBytes
                  "bindings", bytes bindings.FSharpSource
                  "compilation-receipt", bytes (QuintCompiler.encodeReceipt receipt) ]

        let artifacts =
            [ quintArtifact "markdown" "work/demo/specification.md" contents["markdown"]
              quintArtifact "fence-manifest" "readiness/demo/quint/fences.json" contents["fence-manifest"]
              quintArtifact "generated-modules" "readiness/demo/quint/modules.digest" contents["generated-modules"]
              quintArtifact "source-map" "readiness/demo/quint/source-map.json" contents["source-map"]
              quintArtifact "typed-effect" "readiness/demo/quint/typed-effect.json" contents["typed-effect"]
              quintArtifact "sandbox-contract" "readiness/demo/quint/sandbox-contract.json" contents["sandbox-contract"]
              quintArtifact "compiled-contract" "readiness/demo/quint/contract.json" contents["compiled-contract"]
              quintArtifact "bindings" "readiness/demo/quint/bindings.fs" contents["bindings"]
              quintArtifact "compilation-receipt" "readiness/demo/quint/receipt.json" contents["compilation-receipt"] ]

        { SchemaVersion = 2
          Lifecycle = "typed-sdd"
          Backend = "quint-specification-v1"
          ProfileIdentity = QuintProfile.identity
          ToolchainIdentity = toolchain
          PackageIdentity = "FS.GG.SDD.Artifacts/1.4.0"
          Artifacts = artifacts
          AuthoringAgent = "tern-002"
          AuthoringSession = "session-2"
          RollbackManifestPath = None
          RollbackManifestSha256 = None },
        contents

    let private quintManifest () = quintFixture () |> fst

    [<Fact>]
    let ``v1 migration lowers every semantic identity relationship and text field`` () =
        let baseManifest, contents = quintFixture ()

        let baseContract =
            contents["compiled-contract"]
            |> Encoding.UTF8.GetString
            |> QuintContract.deserialize
            |> expectOk
            |> fun contract ->
                { contract with
                    Relationships =
                        [ { FromId = "ADVANCE"
                            Kind = Reads
                            ToId = "STATE" } ]
                    Impacts =
                        [ { SubjectId = "STATE"
                            Category = "base"
                            Detail = "kept" } ]
                    Compatibility =
                        [ { Surface = "base"
                            Requirement = "STATE"
                            Detail = "kept" } ] }

        let id value =
            SpecificationId.create value |> expectOk

        let payload =
            { Identity = id "SPEC-001"
              SchemaVersion = 1
              Provenance =
                { Agent = "test"
                  Session = "test"
                  SourcePath = "work/demo/specification.fsx"
                  SourceRevision = String.replicate 64 "0"
                  AuthoredAtUtc = "2026-08-26T00:00:00Z" }
              Intent = "intent text"
              EvidenceObligations =
                [ { Id = id "EV001"
                    Kind = "test"
                    Description = "evidence text" } ]
              Extension =
                { UserValue = "user value"
                  Scope =
                    [ { Id = id "SB-001"
                        Statement = "scope text" } ]
                  NonGoals = []
                  Stories =
                    [ { Id = id "US-001"
                        Priority = "P1"
                        Statement = "story text" } ]
                  Requirements =
                    [ { Id = id "FR-001"
                        Statement = "requirement text"
                        AcceptanceIds = [ id "AC-001" ]
                        EvidenceObligationIds = [ id "EV001" ] } ]
                  Acceptance =
                    [ { Id = id "AC-001"
                        StoryIds = [ id "US-001" ]
                        RequirementIds = [ id "FR-001" ]
                        Statement = "acceptance text" } ]
                  Ambiguities = []
                  PublicImpact = []
                  LifecycleNotes = [ "note text" ] } }
            |> SpecificationCodec.serialize RequirementsExtension.contract
            |> expectOk
            |> fun text -> bytes (text + "\n")

        let payloadRange =
            { Path =
                baseManifest.Artifacts
                |> List.find (fun artifact -> artifact.Id = "markdown")
                |> _.Path
              Start = { Line = 1; Column = 1 }
              End = { Line = 1; Column = 2 } }

        let lowered = QuintV1Migration.lower payload payloadRange baseContract |> expectOk
        let ids = lowered.Catalogue |> List.map _.Id |> Set.ofList

        for id in
            [ "SPEC-001"
              "SB-001"
              "US-001"
              "FR-001"
              "AC-001"
              "EV001"
              "Evaluate-AC-001" ] do
            Assert.Contains(id, ids)

        Assert.Contains(lowered.Relationships, fun item -> item.FromId = "ADVANCE" && item.ToId = "STATE")
        Assert.Contains(lowered.Impacts, fun item -> item.Category = "base" && item.Detail = "kept")
        Assert.Contains(lowered.Compatibility, fun item -> item.Surface = "base" && item.Detail = "kept")

        let effect =
            lowered.ActionEffects
            |> List.find (fun item -> item.ActionId = "Evaluate-AC-001")

        Assert.Contains("FR-001", effect.Reads)
        Assert.Contains("AC-001", effect.Writes)
        Assert.Contains("US-001", effect.Subjects)
        Assert.Contains(lowered.Relationships, fun item -> item.FromId = "FR-001" && item.ToId = "EV001")

        for text in
            [ "intent text"
              "user value"
              "scope text"
              "story text"
              "requirement text"
              "acceptance text"
              "evidence text"
              "note text" ] do
            Assert.Contains(lowered.Compatibility, fun item -> item.Detail = text)

    [<Fact>]
    let ``additive authority decoder preserves v1 and strictly round trips v2`` () =
        let source, normalized, markdown = bytes "source", bytes "{}", bytes "# projection"
        let v1 = manifest source normalized markdown

        Assert.Equal(Ok(FsharpSpecificationV1 v1), TypedAuthority.deserialize (TypedAuthorityManifest.serialize v1))

        let v2 = quintManifest ()
        let encoded = TypedAuthority.serializeQuintV2 v2
        Assert.Equal(encoded, TypedAuthority.serializeQuintV2 v2)

        let canonical =
            { v2 with
                Artifacts = v2.Artifacts |> List.sortBy _.Id }

        Assert.Equal(Ok(QuintSpecificationV1 canonical), TypedAuthority.deserialize encoded)

        let unknown =
            encoded.Replace("\"authoringAgent\"", "\"unknown\": true,\n  \"authoringAgent\"")

        match TypedAuthority.deserialize unknown with
        | Error finding -> Assert.Equal("typedSdd.v2.manifestUnknownField", finding.Id)
        | Ok _ -> failwith "manifest-v2 accepted an unknown field"

        let wrongBackend = encoded.Replace("quint-specification-v1", "evil-backend")

        match TypedAuthority.deserialize wrongBackend with
        | Error finding -> Assert.Equal("typedSdd.v2.wrongAuthority", finding.Id)
        | Ok _ -> failwith "manifest-v2 accepted a wrong backend"

        let duplicate =
            encoded.Replace(
                "\"lifecycle\": \"typed-sdd\"",
                "\"lifecycle\": \"typed-sdd\",\n  \"lifecycle\": \"typed-sdd\""
            )

        match TypedAuthority.deserialize duplicate with
        | Error finding -> Assert.Equal("typedSdd.v2.manifestDuplicateField", finding.Id)
        | Ok _ -> failwith "manifest-v2 accepted a duplicate property"

        let v1Unknown =
            (TypedAuthorityManifest.serialize v1).Replace("\"lifecycle\"", "\"unknown\": true,\n  \"lifecycle\"")

        match TypedAuthority.deserialize v1Unknown with
        | Error finding -> Assert.Equal("typedSdd.authorityUnknownField", finding.Id)
        | Ok _ -> failwith "manifest-v1 accepted an unknown field through the strict dispatcher"

    [<Fact>]
    let ``manifest v2 validates the closed artifact inventory and exact bytes`` () =
        let authority, contents = quintFixture ()

        let observed =
            authority.Artifacts
            |> List.map (fun artifact ->
                { Path = artifact.Path
                  State = QuintAuthorityArtifactState.Present(contents[artifact.Id]) })

        // This small synthetic fixture exercises manifest/receipt/source closure but deliberately is
        // not one of the exact Q1-qualified typed/effect programs. The semantic adapter must refuse it.
        Assert.Equal<string list>(
            [ "typedSdd.v2.typedEffectClosure" ],
            TypedAuthority.validateQuintV2 authority.PackageIdentity observed authority
            |> List.map _.Id
        )

        let mutant =
            observed
            |> List.map (fun observation ->
                if observation.Path.EndsWith("contract.json") then
                    { observation with
                        State = QuintAuthorityArtifactState.Present(bytes "edited") }
                else
                    observation)

        let ids =
            TypedAuthority.validateQuintV2 authority.PackageIdentity mutant authority
            |> List.map _.Id

        Assert.Contains("typedSdd.v2.artifactMismatch", ids)

        let receiptArtifact =
            authority.Artifacts
            |> List.find (fun artifact -> artifact.Id = "compilation-receipt")

        let wrongSource = String.replicate 64 "d"
        let receiptBytes = contents["compilation-receipt"]
        let receiptText = Encoding.UTF8.GetString receiptBytes

        let semanticMutantBytes =
            bytes (
                receiptText.Replace(
                    (QuintSource.createMarkdown "work/demo/specification.md" contents["markdown"]
                     |> expectOk)
                        .Sha256,
                    wrongSource
                )
            )

        let semanticAuthority =
            { authority with
                Artifacts =
                    authority.Artifacts
                    |> List.map (fun artifact ->
                        if artifact.Id = "compilation-receipt" then
                            { artifact with
                                Sha256 = TypedAuthorityManifest.sha256 semanticMutantBytes }
                        else
                            artifact) }

        let semanticObserved =
            observed
            |> List.map (fun observation ->
                if observation.Path = receiptArtifact.Path then
                    { observation with
                        State = QuintAuthorityArtifactState.Present semanticMutantBytes }
                else
                    observation)

        let semanticIds =
            TypedAuthority.validateQuintV2 semanticAuthority.PackageIdentity semanticObserved semanticAuthority
            |> List.map _.Id

        Assert.Contains("typedSdd.v2.receiptClosure", semanticIds)

        let validateSelfConsistentMutant id mutantBytes =
            let artifact = authority.Artifacts |> List.find (fun item -> item.Id = id)

            let mutantAuthority =
                { authority with
                    Artifacts =
                        authority.Artifacts
                        |> List.map (fun item ->
                            if item.Id = id then
                                { item with
                                    Sha256 = TypedAuthorityManifest.sha256 mutantBytes }
                            else
                                item) }

            let mutantObserved =
                observed
                |> List.map (fun item ->
                    if item.Path = artifact.Path then
                        { item with
                            State = QuintAuthorityArtifactState.Present mutantBytes }
                    else
                        item)

            TypedAuthority.validateQuintV2 mutantAuthority.PackageIdentity mutantObserved mutantAuthority
            |> List.map _.Id

        let receipt = Encoding.UTF8.GetString contents["compilation-receipt"]
        let typedEffectC = TypedAuthorityManifest.sha256 contents["typed-effect"]
        let typedEffectD = String.replicate 64 "d"

        let wrongTypedEffect =
            receipt.Replace($"\"typedEffectSha256\":\"{typedEffectC}\"", $"\"typedEffectSha256\":\"{typedEffectD}\"")
            |> bytes

        Assert.Contains(
            "typedSdd.v2.receiptClosure",
            validateSelfConsistentMutant "compilation-receipt" wrongTypedEffect
        )

        let unexpectedProcess =
            receipt.Replace("[\"extract\",\"typecheck\"]", "[\"unexpected-tool\"]") |> bytes

        Assert.Contains(
            "typedSdd.v2.receiptMalformed",
            validateSelfConsistentMutant "compilation-receipt" unexpectedProcess
        )

        let contractDigest = TypedAuthorityManifest.sha256 contents["compiled-contract"]
        let forgedBindings = bytes ($"not F# {contractDigest}")
        Assert.Contains("typedSdd.v2.bindingsClosure", validateSelfConsistentMutant "bindings" forgedBindings)

        let ghostMap =
            Encoding.UTF8
                .GetString(contents["source-map"])
                .Replace("demo.qnt", "ghost.qnt")
                .Replace("\"fenceOrdinal\":0", "\"fenceOrdinal\":99")
            |> bytes

        Assert.Contains("typedSdd.v2.sourceMapClosure", validateSelfConsistentMutant "source-map" ghostMap)

        let incomplete =
            { authority with
                Artifacts = authority.Artifacts.Tail }

        let inventoryIds =
            TypedAuthority.validateQuintV2 authority.PackageIdentity observed incomplete
            |> List.map _.Id

        Assert.Contains("typedSdd.v2.artifactInventory", inventoryIds)

        let aliased =
            { authority with
                Artifacts =
                    authority.Artifacts
                    |> List.map (fun artifact -> { artifact with Path = "same.bin" }) }

        let aliasIds =
            TypedAuthority.validateQuintV2
                authority.PackageIdentity
                [ { Path = "same.bin"
                    State = QuintAuthorityArtifactState.Present(bytes "markdown") } ]
                aliased
            |> List.map _.Id

        Assert.Contains("typedSdd.v2.artifactPathAlias", aliasIds)

    [<Fact>]
    let ``dispatcher preserves stable malformed v1 diagnostics`` () =
        for malformed in [ "{"; "{}"; "{\"schemaVersion\":\"1\"}" ] do
            match TypedAuthority.deserialize malformed with
            | Error finding -> Assert.Equal("typedSdd.authorityMalformed", finding.Id)
            | Ok _ -> failwith "malformed authority was accepted"

    [<Fact>]
    let ``verification selector broadens monotonically and unknown input fails safe`` () =
        let select category =
            QuintVerificationSelector.select
                { ChangedPaths = []
                  Impacts =
                    [ { SubjectId = "subject"
                        Category = category
                        Detail = "fixture" } ] }

        Assert.Equal(ProseOnly, select "prose")
        Assert.Equal(StructuralTypecheck, select "catalogue")
        Assert.Equal(TestAndSimulation, select "action")
        Assert.Equal(ModelCheck, select "temporal")
        Assert.Equal(FullCorpus, select "compiler")
        Assert.Equal(FullCorpus, select "not-a-declared-category")
        Assert.Equal(FullCorpus, select (Unchecked.defaultof<string>))

        Assert.Equal(
            FullCorpus,
            QuintVerificationSelector.select
                { ChangedPaths = [ "src/TypedLifecycleV2.fs" ]
                  Impacts =
                    [ { SubjectId = "doc"
                        Category = "prose"
                        Detail = "fixture" } ] }
        )
