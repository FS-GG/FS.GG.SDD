namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open System.Security.Cryptography
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ScaffoldProvenance
open FS.GG.SDD.Commands.Internal
open Xunit

module CatalogSkillDigestTests =
    open RemediationSupport

    let private rawDigest (bytes: byte array) =
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData bytes)

    let private path root relative = Path.Combine(root, relative)

    let private summary root =
        (doctorReport root).Doctor |> Option.get

    let private variants = [ "lf"; "crlf"; "bom" ]

    let private encoded variant (text: string) =
        let text =
            if variant = "crlf" then
                text.Replace("\n", "\r\n")
            else
                text

        let bytes = Encoding.UTF8.GetBytes text

        if variant = "bom" then
            Array.append [| 0xefuy; 0xbbuy; 0xbfuy |] bytes
        else
            bytes

    let private body variant = encoded variant productSkillBody

    // Real schema-2 validation and the actual filesystem read gate, with source-3 using
    // the same unchanged catalogue ownership declaration. No acquisition or tool execution.
    let private fixture variant sourceVersion =
        let root = productCoherentFixture ()

        for relative in productSkillCopies do
            File.WriteAllBytes(path root relative, body variant)

        for relative, text in ownerSourcedCopies [] do
            File.WriteAllBytes(path root relative, encoded variant text)

        let own =
            ScaffoldProvenance.tryParse (File.ReadAllText(path root provenancePath))
            |> Option.get

        let _, _, _, _, _, original = CatalogWorkflowFixture.value ()
        let descriptor = original.Declaration.Descriptor

        let tag (rows: ScaffoldProducedPath list) =
            rows
            |> List.filter (fun row -> File.Exists(path root row.Path))
            |> List.map (fun row ->
                { row with
                    Sha256 = Some(rawDigest (File.ReadAllBytes(path root row.Path)))
                })

        let ownership =
            { own with
                Generator = original.Generator
                ProviderName = descriptor.Id
                ProviderContractVersion = descriptor.ContractVersion
                TemplateRef = descriptor.TemplateSource
                RequiredMinimumCliVersion = None
                EffectiveParameters = original.Declaration.EffectiveParameters
                ProducedPaths = tag own.ProducedPaths
                MirroredPaths = tag own.MirroredPaths
                DriverPaths = tag own.DriverPaths
                GameSkillPaths = tag own.GameSkillPaths
                RenderingSkillPaths = tag own.RenderingSkillPaths
                SddOwnedPaths = []
            }

        let all =
            ownership.ProducedPaths
            @ ownership.MirroredPaths
            @ ownership.DriverPaths
            @ ownership.GameSkillPaths
            @ ownership.RenderingSkillPaths

        let catalog =
            { original with
                Ownership = ownership
                Observation =
                    { original.Observation with
                        ProducedPaths = all
                    }
            }

        let unwrap =
            function
            | Ok value -> value
            | Error errors -> failwithf "%A" errors

        let text =
            if sourceVersion = 2 then
                CatalogScaffoldProvenance.serialize catalog |> unwrap
            else
                let sourceRequest = ProviderSourceWorkflowTests.request
                let digest = ProviderSourceWorkflowTests.digest

                let record: SourceScaffoldProvenance.Record =
                    {
                        SchemaVersion = 3
                        Catalog = catalog
                        Source =
                            {
                                Schema = "fsgg.provider-source-observation/1"
                                SourceSetSha256 = digest
                                AcquisitionPolicySha256 = digest
                                Source =
                                    { sourceRequest.Sources.Sources.Head with
                                        ManifestSha256 = digest
                                    }
                                Revision = "synthetic-source-revision"
                                Acquisition = ProviderSource.CacheRead
                                ResolvedUri = None
                                Redirects = []
                                NetworkHops = []
                                CatalogSha256 = catalog.Declaration.RawCatalogDigest
                                TemplateSource = descriptor.TemplateSource
                                ArchiveSha256 = catalog.Declaration.Archive.Digest
                            }
                    }

                SourceScaffoldProvenance.serialize record |> unwrap

        File.WriteAllText(path root provenancePath, text)
        root

    [<Fact>]
    let ``catalog raw LF CRLF and BOM skills are coherent through doctor and upgrade`` () =
        for version in [ 2; 3 ] do
            for variant in variants do
                let root = fixture variant version

                try
                    let provenance = File.ReadAllBytes(path root provenancePath)

                    let before =
                        productSkillCopies
                        |> List.map (fun relative -> File.ReadAllBytes(path root relative))

                    Assert.Empty((summary root).SkillDriftPaths)
                    let upgrade = (upgradeYes root).Upgrade |> Option.get
                    Assert.False upgrade.ResidualDrift
                    Assert.True((provenance = File.ReadAllBytes(path root provenancePath)))

                    List.zip productSkillCopies before
                    |> List.iter (fun (relative, bytes) -> Assert.True((bytes = File.ReadAllBytes(path root relative))))
                finally
                    Directory.Delete(root, true)

    [<Fact>]
    let ``raw only BOM and line ending tampering flags the changed root`` () =
        for changed in [ "crlf"; "bom" ] do
            let root = fixture "lf" 2

            try
                let relative = productSkillCopies.Head
                File.WriteAllBytes(path root relative, body changed)
                Assert.Equal<string list>([ relative ], (summary root).SkillDriftPaths)
                Assert.True(((upgradeYes root).Upgrade |> Option.get).ResidualDrift)
            finally
                Directory.Delete(root, true)

    [<Fact>]
    let ``owner auxiliary raw only tampering flags the exact changed file`` () =
        for variant in [ "crlf"; "bom" ] do
            let root = fixture "lf" 2

            try
                let relative, text =
                    ownerSourcedCopies []
                    |> List.find (fun (relative, _) -> relative.Contains("/references/"))

                File.WriteAllBytes(path root relative, encoded variant text)
                Assert.Equal<string list>([ relative ], (summary root).SkillDriftPaths)
            finally
                Directory.Delete(root, true)

    [<Fact>]
    let ``canonical text tampering flags only the root contradicting its raw declaration`` () =
        let root = fixture "lf" 3

        try
            let relative = productSkillCopies.Head
            File.AppendAllText(path root relative, "changed\n")
            Assert.Equal<string list>([ relative ], (summary root).SkillDriftPaths)
        finally
            Directory.Delete(root, true)

    [<Fact>]
    let ``legacy bare digests still accept normalized CRLF and decoded BOM`` () =
        for variant in variants do
            let root = productCoherentFixture ()

            try
                for relative in productSkillCopies do
                    File.WriteAllBytes(path root relative, body variant)

                Assert.Empty((summary root).SkillDriftPaths)
            finally
                Directory.Delete(root, true)

    [<Fact>]
    let ``tagged declaration never clears from decoded snapshots alone`` () =
        let root = fixture "bom" 2

        try
            let document =
                ScaffoldProvenanceDocument.parse (File.ReadAllText(path root provenancePath))

            let own =
                match document with
                | Ok value -> ScaffoldProvenanceDocument.ownershipProjection value
                | Error errors -> failwithf "%A" errors

            let bodies =
                Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                |> Array.choose (fun absolute ->
                    let relative = Path.GetRelativePath(root, absolute).Replace('\\', '/')

                    if Drift.skillCopyOfPath relative |> Option.isSome then
                        Some(relative, File.ReadAllText absolute)
                    else
                        None)
                |> Map.ofArray

            let report =
                Drift.computeObserved (Some own) None None installedVersion (Set.ofList coherentPresent) bodies []

            Assert.Contains(productSkillCopies.Head, report.SkillDriftPaths)
            Assert.False report.IsCoherent
        finally
            Directory.Delete(root, true)

    [<Fact>]
    let ``missing catalog skill retains missing copy drift`` () =
        let root = fixture "lf" 2

        try
            let relative = productSkillCopies.Head
            File.Delete(path root relative)
            Assert.Equal<string list>([ relative ], (summary root).SkillDriftPaths)
        finally
            Directory.Delete(root, true)
