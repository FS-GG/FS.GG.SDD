namespace FS.GG.SDD.Commands.Tests

open System.Diagnostics
open System.IO
open System.Xml.Linq
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.Commands.Internal
open Xunit

/// FS.GG.SDD#845 / `docs/decisions/0005-generated-product-governance-resolution-route.md` — the
/// generated-product route to the org reference gate set, answering FS-GG/FS.GG.Governance#385 AC3
/// under the #386 decision.
///
/// The subject is the ROUTE, not the profile: `init` seeds one pinned, opt-in MSBuild project and
/// emits no Governance content whatever. These tests pin the three properties that make that an
/// honest answer — the pin is explicit, the seed declares nothing, and declining is durable — and
/// then drive the documented verb end to end against the real published package.
///
/// The collection is required by `ProcessGlobalEnvGuardTests`: the last test spawns a
/// PATH-resolved `dotnet`, and this module lives in its own file so the three in-process tests
/// beside it are the only ones serialized along with it.
[<Collection("ProcessGlobalEnv")>]
module GovernanceResolutionRouteTests =
    let private emittedProject root =
        Path.Combine(root, Foundation.governanceResolutionPath) |> File.ReadAllText

    // AC1/AC2. The emitted artifact IS the pin: a product reads the version in its own tree and
    // bumps it deliberately. A floating version would satisfy "a route exists" while making
    // "which profile did this product adopt?" unanswerable, so the absence of one is asserted
    // directly rather than inferred from the presence of a literal.
    [<Fact>]
    let ``init seeds a resolution project pinning the reference gate set to an exact version`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let text = emittedProject root
        let document = XDocument.Parse text // well-formed, or MSBuild could never evaluate it

        let attribute name (element: XElement) =
            element.Attribute(XName.Get name)
            |> Option.ofObj
            |> Option.map (fun a -> a.Value)
            |> Option.defaultValue ""

        let reference =
            document.Descendants(XName.Get "PackageReference")
            |> Seq.filter (fun e -> attribute "Include" e = Foundation.referenceGateSetPackageId)
            |> List.ofSeq

        Assert.Single reference |> ignore
        let version = attribute "Version" reference.Head

        Assert.Equal(Foundation.referenceGateSetVersion, version)
        Assert.DoesNotContain("*", version)
        Assert.DoesNotContain("latest", version.ToLowerInvariant())

        // The two properties without which the verb resolves to the wrong place or fails to
        // restore at all. `.fsgg/.fsgg` is the target's default from inside `.fsgg/`; NU1008 is
        // what a `Version=` attribute raises under the org-shared CPM baseline.
        Assert.Contains("FsggReferenceGateSetDestination", text)
        Assert.Contains("<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>", text)

    // AC4. The seed is a route, not a declaration: it carries no Governance content, so the three
    // optional files stay absent and the facts SDD reports about them stay literally true. This is
    // the assertion that fails if anyone ever "helpfully" makes `init` write a starter profile.
    [<Fact>]
    let ``init emits the route but no Governance declaration and the optional facts stay true`` () =
        let root = TestSupport.tempDirectory ()
        let report = TestSupport.request Init root |> TestSupport.runRequest

        Assert.True(File.Exists(Path.Combine(root, Foundation.governanceResolutionPath)))

        for name in [ "policy.yml"; "capabilities.yml"; "tooling.yml"; "governance.yml" ] do
            Assert.False(
                File.Exists(Path.Combine(root, ".fsgg", name)),
                $"init must not author .fsgg/{name}; Governance owns that content."
            )

        for fact in report.GovernanceCompatibility do
            Assert.Equal("notEvaluated", fact.State)
            Assert.False(fact.RequiredBySdd)

        // SDD emits no Governance semantics of its own anywhere in the seeded project.
        let text = emittedProject root
        Assert.DoesNotContain("checks:", text)
        Assert.DoesNotContain("maturity", text.ToLowerInvariant())

    // The decline case, which the route must not quietly remove. `doctor`/`upgrade` reconcile
    // exactly `Drift.expectedArtifactPaths`, so keeping the route out of that set is what makes
    // "I deleted it because I do not want Governance" a durable answer instead of drift that gets
    // re-seeded on the next remediation pass.
    [<Fact>]
    let ``declining the route is durable because remediation does not reconcile it`` () =
        Assert.DoesNotContain(Foundation.governanceResolutionPath, Drift.expectedArtifactPaths)

        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        File.Delete(Path.Combine(root, Foundation.governanceResolutionPath))

        let report = TestSupport.request Doctor root |> TestSupport.runRequest

        Assert.DoesNotContain(Foundation.governanceResolutionPath, serializeReport report)

    /// AC3 / the issue's own Verification, executed rather than reasoned about: from a CLEAN
    /// `init`, run the two documented commands verbatim against the real published package and
    /// assert the profile lands. Nothing is copied by hand, and no byte of the result is authored
    /// by FS.GG.SDD — `FsggResolveReferenceGateSet` is defined by the package, so this also pins
    /// that SDD keeps using the Governance-owned verb rather than growing a second one.
    [<Fact; Trait("tier", "slow")>]
    let ``the documented verb resolves the org profile from a clean init`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        let fsgg = Path.Combine(root, ".fsgg")
        let projectYml = Path.Combine(fsgg, "project.yml")
        let projectYmlBefore = File.ReadAllText projectYml

        let run (args: string) =
            let info = ProcessStartInfo("dotnet", args, WorkingDirectory = fsgg)
            info.RedirectStandardOutput <- true
            info.RedirectStandardError <- true

            match Process.Start info with
            | null -> failwith $"could not start `dotnet {args}`"
            | started ->
                use proc = started
                let out = proc.StandardOutput.ReadToEnd()
                let err = proc.StandardError.ReadToEnd()
                proc.WaitForExit()
                proc.ExitCode, out + err

        let restoreCode, restoreLog = run "restore governance-resolution.proj"
        Assert.True((restoreCode = 0), $"restore failed:\n{restoreLog}")

        let resolveCode, resolveLog =
            run "msbuild governance-resolution.proj -t:FsggResolveReferenceGateSet"

        Assert.True((resolveCode = 0), $"resolve failed:\n{resolveLog}")

        for name in [ "governance.yml"; "capabilities.yml"; "policy.yml"; "tooling.yml" ] do
            Assert.True(File.Exists(Path.Combine(fsgg, name)), $"expected the resolved .fsgg/{name}")

        // The package payload and the `init` skeleton share `.fsgg/`; the route must not overwrite
        // what SDD owns there.
        Assert.Equal(projectYmlBefore, File.ReadAllText projectYml)
