namespace FS.GG.Contracts.Tests

open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Xml.Linq
open FS.GG.SDD.TestShared
open Xunit

/// FS.GG.SDD#742 / AC3 — the guard for #782's producer half.
///
/// `FS.GG.Contracts.fsproj` packs its compiled signature files under `api-surface/` in the nupkg,
/// derived from `@(Compile)` filtered to `.fsi`. This module proves the BUILT PACKAGE actually
/// carries them. A pack step nothing verifies is a pack step that silently stops working, and this
/// package has already demonstrated exactly that failure mode at scale: #782 landed in
/// FS-GG/FS.GG.Rendering on 2026-07-14 and FS.GG.Contracts never did its half, so all fourteen
/// published versions (1.2.0 … 7.3.0) shipped with only `lib/net10.0/` in them and nothing here
/// noticed for two weeks. The consumer noticed instead — FS.GG.Rendering#1094 had to carry a
/// hand-written 7.0.0-era `.fsi` forward to 7.2.0 because no published Contracts package could be
/// read — which is the wrong place for the discovery and the wrong repo to pay for it.
///
/// WHY IT PACKS AND ASSERTS RATHER THAN READING THE .fsproj TWICE. The obvious cheap test parses
/// the fsproj, finds the `<None … PackagePath="api-surface/…">` item, and calls it proven. That
/// test passes on a project that cannot pack at all: it re-reads the same declaration the change
/// under test just wrote and agrees with itself. Every failure mode worth catching here lives
/// BETWEEN the declaration and the archive — a `PackagePath` folder-vs-full-path slip that doubles
/// the directory (`api-surface/Widgets/Widgets/Buttons.fsi`, the exact bug #782 documents), an
/// `@(Compile)` transform that silently yields nothing when the ItemGroup is evaluated before the
/// compile list, a future SDK changing when `Pack="true"` is honoured. So this runs the real
/// `dotnet pack` and opens the real `.nupkg`.
module PackedApiSurfaceTests =

    /// Bound generously: this is a full Release build plus pack, not a unit test. `runBounded` is
    /// the repo's only deadlock-free way to run a child (FS.GG.SDD#212) — it drains both pipes
    /// concurrently, so this bound is actually reachable rather than dead code behind a blocked write.
    let private packTimeoutMs = 300_000

    /// `Path.GetFileName` is nullable under this repo's `Nullable enable` + warnings-as-errors, and
    /// only for inputs (`null`, a bare root) that cannot occur here. Collapsing it to `""` keeps the
    /// pipelines below on non-nullable `string` without pretending the null case is unreachable —
    /// an empty name simply fails the set equality, loudly, instead of compiling to a warning waiver.
    let private nonNull (value: string | null) =
        value |> Option.ofObj |> Option.defaultValue ""

    let private projectPath =
        Path.Combine(TestShared.repoRoot, "src", "FS.GG.Contracts", "FS.GG.Contracts.fsproj")

    let private projectDirectory =
        Path.Combine(TestShared.repoRoot, "src", "FS.GG.Contracts")

    /// The EXPECTED set, read from the compiler's own input list.
    ///
    /// This is the half that makes AC2 checkable rather than merely claimed. The expectation is
    /// derived from `<Compile Include="*.fsi" />` — the same items the pack rule transforms — so a
    /// signature file added to the project is expected here automatically and a hand-maintained
    /// roster can never drift from it. Deriving it from a literal list in this file, or from a
    /// directory glob, would reintroduce precisely the second source of truth #742 exists to remove:
    /// a glob would also demand an orphaned `.fsi` that no longer compiles, and a literal would need
    /// a human to remember.
    let private compiledSignatureFiles () =
        let doc = XDocument.Load projectPath

        doc.Descendants()
        |> Seq.filter (fun e -> e.Name.LocalName = "Compile")
        |> Seq.choose (fun e ->
            match e.Attribute(XName.Get "Include") with
            | null -> None
            | attr -> Some attr.Value)
        |> Seq.map (fun include' -> include'.Replace('\\', '/'))
        |> Seq.filter (fun include' -> include'.EndsWith(".fsi", System.StringComparison.Ordinal))
        |> Seq.map (Path.GetFileName >> nonNull)
        |> Seq.toArray
        |> Array.sort

    /// Pack the real project and return the produced `.nupkg`. A pack that fails, or produces no
    /// package, is a test failure here rather than an empty expectation that passes vacuously —
    /// "fails closed on an absent subject", which is the property #782's own guard script names.
    let private packToTemp () =
        let output = TestShared.tempDirectory ()

        let startInfo = ProcessStartInfo("dotnet", WorkingDirectory = TestShared.repoRoot)

        for arg in
            [ "pack"
              projectPath
              "-c"
              "Release"
              "-o"
              output
              "--nologo"
              "-v"
              "quiet" ] do
            startInfo.ArgumentList.Add arg

        let completion = TestShared.ChildProcess.runBounded packTimeoutMs startInfo

        Assert.True(
            completion.ExitCode = 0,
            $"`dotnet pack` failed with exit code {completion.ExitCode}.\n"
            + $"stdout:\n{completion.StandardOutput}\nstderr:\n{completion.StandardError}"
        )

        match Directory.GetFiles(output, "*.nupkg") with
        | [| package |] -> package
        | produced ->
            failwith (
                $"Expected exactly one .nupkg from `dotnet pack`, got {produced.Length}: "
                + (produced |> Array.map (Path.GetFileName >> nonNull) |> String.concat ", ")
            )

    /// Every entry the archive carries under `api-surface/`, as its path relative to that folder.
    let private packedApiSurfaceEntries (package: string) =
        use archive = ZipFile.OpenRead package

        archive.Entries
        |> Seq.map (fun entry -> entry.FullName.Replace('\\', '/'))
        |> Seq.filter (fun name -> name.StartsWith("api-surface/", System.StringComparison.Ordinal))
        |> Seq.map (fun name -> name.Substring("api-surface/".Length))
        |> Seq.filter (fun name -> name <> "")
        |> Seq.toArray
        |> Array.sort

    // AC3, and the load-bearing assertion of this module: SET EQUALITY, in both directions, against
    // the compiled sources.
    //
    // Both directions matter and they catch different regressions. A packed file the compile list
    // does not have is a stale artifact shipping as though it were the surface. A compiled `.fsi`
    // the package does NOT carry is the #742 defect itself, and it is the direction a count check
    // or a `not empty` check would miss: pack five of six and both still pass.
    [<Fact>]
    let ``the built package carries exactly the compiled signature files under api-surface`` () =
        let expected = compiledSignatureFiles ()

        // Fail closed. If the project ever compiles no `.fsi` at all, the equality below is
        // `[] = []` and this test would certify an EMPTY `api-surface/` as correct — a green that
        // means the opposite of what it reads. The subject must exist for the verdict to mean anything.
        Assert.True(
            expected.Length > 0,
            $"No `<Compile Include=\"*.fsi\" />` items found in {projectPath}; this guard has no subject "
            + "and would otherwise pass vacuously."
        )

        let actual = packedApiSurfaceEntries (packToTemp ())

        Assert.Equal<string array>(expected, actual)

    // AC2's other half. Set equality proves the right NAMES are present; it says nothing about the
    // CONTENT, and "a second hand-copy that can drift" is a defect about content, not names. A pack
    // rule that copied from `docs/api-surface/FS.GG.Contracts/` — a plausible and wrong reading of
    // AC1 — would satisfy the test above with six correctly-named files whose bytes came from a
    // mirror that is only kept honest by a separate gate. Comparing bytes makes the packed surface
    // demonstrably THE compiled surface rather than something that currently resembles it.
    [<Fact>]
    let ``each packed signature file is byte-identical to its compiled source`` () =
        let package = packToTemp ()
        use archive = ZipFile.OpenRead package

        let expected = compiledSignatureFiles ()
        Assert.True(expected.Length > 0, "No compiled `.fsi` to compare; the guard has no subject.")

        for name in expected do
            // Matched rather than null-checked: `GetEntry` returns `ZipArchiveEntry | null`, and a
            // `match` is what actually narrows it for the compiler. An `isNull` guard followed by a
            // dereference would still be a nullness error here, and suppressing that would be the
            // one shape this file must not have — a guard talking itself past its own subject.
            match archive.GetEntry("api-surface/" + name) with
            | null ->
                failwith (
                    $"The package carries no `api-surface/{name}`, but `{name}` is in the project's "
                    + "compile list."
                )
            | entry ->
                use stream = entry.Open()
                use buffer = new MemoryStream()
                stream.CopyTo buffer

                let packedBytes = buffer.ToArray()
                let sourceBytes = File.ReadAllBytes(Path.Combine(projectDirectory, name))

                Assert.True(
                    (packedBytes = sourceBytes),
                    $"`api-surface/{name}` in the package is not byte-identical to `src/FS.GG.Contracts/{name}` "
                    + $"({packedBytes.Length} packed bytes vs {sourceBytes.Length} source bytes). The packed surface "
                    + "must BE the compiled surface, not a copy of it."
                )
