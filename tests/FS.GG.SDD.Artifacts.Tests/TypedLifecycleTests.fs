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
            ScaffoldProvenance.devRepoRecord { Id = "FS.GG.SDD"; Version = "1.4.0-preview.1" } []
        Assert.Equal(Ok StandardSdd, ScaffoldProvenance.lifecycleLane provenance)
        Assert.Equal(Ok TypedSdd, ScaffoldProvenance.lifecycleLane { provenance with EffectiveParameters = [ "lifecycle", "typed-sdd" ] })

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
