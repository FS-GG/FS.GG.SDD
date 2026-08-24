namespace FS.GG.SDD.Artifacts.Tests

open System
open System.Diagnostics
open System.IO
open System.IO.Compression
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
