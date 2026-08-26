namespace FS.GG.SDD.Artifacts.Tests

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Security.Cryptography
open System.Xml.Linq
open Xunit

module PackedDependencyContractTests =
    let private artifactsJob () =
        let workflow =
            Path.Combine(TestSupport.repoRoot, ".github", "workflows", "release.yml")
            |> File.ReadAllText

        let start = workflow.IndexOf("\n  publish-artifacts:\n", StringComparison.Ordinal)
        let finish = workflow.IndexOf("\n  publish-cli:\n", start, StringComparison.Ordinal)
        Assert.True(start >= 0 && finish > start, "publish-artifacts must remain a distinct release job")
        workflow.Substring(start, finish - start)

    let private packProject outputDirectory =
        let project =
            Path.Combine(TestSupport.repoRoot, "src", "FS.GG.SDD.Artifacts", "FS.GG.SDD.Artifacts.fsproj")

        let startInfo = ProcessStartInfo("dotnet")
        startInfo.WorkingDirectory <- TestSupport.repoRoot
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.UseShellExecute <- false

        [ "pack"; project; "-c"; "Release"; "--no-restore"; "-o"; outputDirectory ]
        |> List.iter startInfo.ArgumentList.Add

        use child =
            Process.Start startInfo
            |> Option.ofObj
            |> Option.defaultWith (fun () -> failwith "dotnet pack did not start")

        let output = child.StandardOutput.ReadToEnd()
        let error = child.StandardError.ReadToEnd()
        child.WaitForExit()

        Assert.True(
            child.ExitCode = 0,
            $"The release-equivalent Artifacts pack failed with exit code {child.ExitCode}.\n{output}\n{error}"
        )

    [<Fact>]
    let ``release package identity does not replace the Contracts dependency identity`` () =
        let outputDirectory =
            Path.Combine(Path.GetTempPath(), "fsgg-sdd-artifacts-pack-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory outputDirectory |> ignore

        try
            packProject outputDirectory

            let package =
                Directory.GetFiles(outputDirectory, "FS.GG.SDD.Artifacts.1.4.0-preview.1.nupkg")
                |> Array.exactlyOne

            use archive = ZipFile.OpenRead package

            let nuspecEntry =
                archive.Entries
                |> Seq.filter (fun entry -> entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal))
                |> Seq.exactlyOne

            use stream = nuspecEntry.Open()
            let document = XDocument.Load stream

            let root =
                document.Root
                |> Option.ofObj
                |> Option.defaultWith (fun () -> failwith "the packed nuspec has no root element")

            let ns = root.Name.Namespace

            let dependency =
                document.Descendants(ns + "dependency")
                |> Seq.filter (fun element ->
                    element.Attribute(XName.Get "id")
                    |> Option.ofObj
                    |> Option.exists (fun attribute -> attribute.Value = "FS.GG.Contracts"))
                |> Seq.exactlyOne

            let packageVersion = document.Descendants(ns + "version") |> Seq.head |> _.Value

            let dependencyVersion =
                dependency.Attribute(XName.Get "version")
                |> Option.ofObj
                |> Option.map _.Value
                |> Option.defaultWith (fun () -> failwith "the Contracts dependency has no version")

            Assert.Equal("1.4.0-preview.1", packageVersion)
            Assert.Equal("7.5.2", dependencyVersion)
            Assert.DoesNotContain(packageVersion, dependency.ToString())
        finally
            Directory.Delete(outputDirectory, true)

    [<Fact>]
    let ``artifacts package carries a producer-owned portable Fable kernel`` () =
        let outputDirectory =
            Path.Combine(Path.GetTempPath(), "fsgg-sdd-artifacts-fable-pack-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory outputDirectory |> ignore

        try
            packProject outputDirectory

            let package =
                Directory.GetFiles(outputDirectory, "FS.GG.SDD.Artifacts.1.4.0-preview.1.nupkg")
                |> Array.exactlyOne

            use archive = ZipFile.OpenRead package
            let names = archive.Entries |> Seq.map _.FullName |> Set.ofSeq
            Assert.Contains("fable/FS.GG.SDD.Artifacts.fsproj", names)
            Assert.Contains("fable/SpecificationKernel.fs", names)

            let sourceEntry =
                archive.GetEntry("fable/SpecificationKernel.fs")
                |> Option.ofObj
                |> Option.defaultWith (fun () -> failwith "packed Fable kernel source is missing")

            use reader = new StreamReader(sourceEntry.Open())
            let source = reader.ReadToEnd()
            Assert.Contains("fsgg-typed-specification/v1", source)
            Assert.Contains("module SpecificationCompiler", source)
            Assert.DoesNotContain("System.Text.Json", source)
            Assert.DoesNotContain("SIR.Domain", source)
            Assert.DoesNotContain("RuleDefinition", source)
        finally
            Directory.Delete(outputDirectory, true)

    [<Fact>]
    let ``Artifacts release pack rejects global identity overrides`` () =
        let job = artifactsJob ()
        Assert.Contains("dotnet pack src/FS.GG.SDD.Artifacts/FS.GG.SDD.Artifacts.fsproj", job)
        Assert.DoesNotContain("-p:Version=${{ needs.resolve-versions.outputs.artifacts_version }}", job)
        Assert.DoesNotContain("-p:PackageVersion=${{ needs.resolve-versions.outputs.artifacts_version }}", job)

    [<Fact>]
    let ``artifacts package carries exact reviewed Quint source and identity receipts`` () =
        let outputDirectory =
            Path.Combine(Path.GetTempPath(), "fsgg-sdd-artifacts-quint-pack-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory outputDirectory |> ignore

        let digest (entry: ZipArchiveEntry) =
            use stream = entry.Open()
            SHA256.HashData stream |> Convert.ToHexString |> _.ToLowerInvariant()

        try
            packProject outputDirectory

            let package =
                Directory.GetFiles(outputDirectory, "FS.GG.SDD.Artifacts.1.4.0-preview.1.nupkg")
                |> Array.exactlyOne

            use archive = ZipFile.OpenRead package

            let entry path =
                archive.GetEntry(path)
                |> Option.ofObj
                |> Option.defaultWith (fun () -> failwithf "packed Quint asset is missing: %s" path)

            Assert.Equal(
                "88bc47acae2c26919ab96a5cafa80b12fac762092c57840a2baad1afcc7feda3",
                entry "quint/lmt/main.go" |> digest
            )

            Assert.Equal(
                "2c564ae07eee1abe75f4fdb6aa0208b897bf220d9140bbfcd12a87ce303fbd35",
                entry "quint/lmt/LICENSE" |> digest
            )

            use reader = new StreamReader((entry "quint/q1-identity-manifest.json").Open())
            let receipt = reader.ReadToEnd()
            Assert.Contains("driusan/lmt@62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c", receipt)
            Assert.Contains("quint-co/quint-llm-kit@cc75369f741af7d490936f82002c2d28e3b3d78d", receipt)
            Assert.Contains("\"role\": \"optional-non-authoritative\"", receipt)
        finally
            Directory.Delete(outputDirectory, true)
