namespace FS.GG.SDD.Artifacts.Tests

open System.Text
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
          PackageIdentity = "FS.GG.SDD.Artifacts/1.4.0-preview.1"
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
            ScaffoldProvenance.devRepoRecord
                { Id = "FS.GG.SDD"
                  Version = "1.4.0-preview.1" }
                []

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
                "FS.GG.SDD.Artifacts/1.4.0-preview.1"
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
                "FS.GG.SDD.Artifacts/1.4.0-preview.1"
                true
                (Some source)
                (Some normalized)
                (Some markdown)
                authority
            |> List.map _.Id

        Assert.Contains("typedSdd.compilerIdentityMismatch", ids)
        Assert.Contains("typedSdd.extensionIdentityMismatch", ids)
        Assert.Contains("typedSdd.authoringReceiptMissing", ids)

    let private quintArtifact id path content =
        { Id = id
          Path = path
          Sha256 = content |> bytes |> TypedAuthorityManifest.sha256 }

    let private quintManifest () =
        let artifacts =
            [ quintArtifact "markdown" "work/demo/specification.md" "markdown"
              quintArtifact "fence-manifest" "readiness/demo/quint/fences.json" "fences"
              quintArtifact "generated-modules" "readiness/demo/quint/modules.digest" "modules"
              quintArtifact "source-map" "readiness/demo/quint/source-map.json" "source-map"
              quintArtifact "compiled-contract" "readiness/demo/quint/contract.json" "contract"
              quintArtifact "bindings" "readiness/demo/quint/bindings.fs" "bindings"
              quintArtifact "compilation-receipt" "readiness/demo/quint/receipt.json" "receipt" ]

        { SchemaVersion = 2
          Lifecycle = "typed-sdd"
          Backend = "quint-specification-v1"
          ProfileIdentity = QuintProfile.identity
          ToolchainIdentity = QuintToolchain.fingerprint QuintToolchain.q1
          PackageIdentity = "FS.GG.SDD.Artifacts/1.4.0-preview.1"
          Artifacts = artifacts
          AuthoringAgent = "tern-002"
          AuthoringSession = "session-2"
          RollbackManifestPath = None
          RollbackManifestSha256 = None }

    [<Fact>]
    let ``additive authority decoder preserves v1 and strictly round trips v2`` () =
        let source, normalized, markdown = bytes "source", bytes "{}", bytes "# projection"
        let v1 = manifest source normalized markdown

        Assert.Equal(
            Ok(FsharpSpecificationV1 v1),
            TypedAuthority.deserialize (TypedAuthorityManifest.serialize v1)
        )

        let v2 = quintManifest ()
        let encoded = TypedAuthority.serializeQuintV2 v2
        Assert.Equal(encoded, TypedAuthority.serializeQuintV2 v2)
        let canonical = { v2 with Artifacts = v2.Artifacts |> List.sortBy _.Id }
        Assert.Equal(Ok(QuintSpecificationV1 canonical), TypedAuthority.deserialize encoded)

        let unknown = encoded.Replace("\"authoringAgent\"", "\"unknown\": true,\n  \"authoringAgent\"")

        match TypedAuthority.deserialize unknown with
        | Error finding -> Assert.Equal("typedSdd.v2.manifestUnknownField", finding.Id)
        | Ok _ -> failwith "manifest-v2 accepted an unknown field"

    [<Fact>]
    let ``manifest v2 validates the closed artifact inventory and exact bytes`` () =
        let authority = quintManifest ()

        let observed =
            authority.Artifacts
            |> List.map (fun artifact ->
                let content =
                    match artifact.Id with
                    | "markdown" -> "markdown"
                    | "fence-manifest" -> "fences"
                    | "generated-modules" -> "modules"
                    | "source-map" -> "source-map"
                    | "compiled-contract" -> "contract"
                    | "bindings" -> "bindings"
                    | "compilation-receipt" -> "receipt"
                    | _ -> failwith "closed inventory"

                artifact.Path, Some(bytes content))

        Assert.Empty(TypedAuthority.validateQuintV2 authority.PackageIdentity observed authority)

        let mutant =
            observed
            |> List.map (fun (path, content) ->
                if path.EndsWith("contract.json") then path, Some(bytes "edited") else path, content)

        let ids =
            TypedAuthority.validateQuintV2 authority.PackageIdentity mutant authority
            |> List.map _.Id

        Assert.Contains("typedSdd.v2.artifactMismatch", ids)

        let incomplete =
            { authority with
                Artifacts = authority.Artifacts.Tail }

        let inventoryIds =
            TypedAuthority.validateQuintV2 authority.PackageIdentity observed incomplete
            |> List.map _.Id

        Assert.Contains("typedSdd.v2.artifactInventory", inventoryIds)

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

        Assert.Equal(
            FullCorpus,
            QuintVerificationSelector.select
                { ChangedPaths = [ "src/TypedLifecycleV2.fs" ]
                  Impacts = [ { SubjectId = "doc"; Category = "prose"; Detail = "fixture" } ] }
        )
