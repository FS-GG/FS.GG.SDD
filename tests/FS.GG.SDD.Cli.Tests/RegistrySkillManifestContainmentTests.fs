namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open FS.GG.SDD.Cli
open Xunit

/// FS-GG/FS.GG.SDD#237 (Gap C finding 4 / #203): `registry skill-manifest --root` must resolve
/// inside the workspace. An absolute or `..`-bearing root is refused with exit 1 and plans no
/// read and no write — parity with the `surface` #185 mitigation. In-process (no subprocess) so
/// it stays on the fast tier and observes the "no filesystem effect" invariant directly.
///
/// Serialized via the `Console` collection: the capture swaps the process-global `Console.Error`,
/// so it must not run concurrently with any other stderr-capturing class (e.g.
/// `ExceptionBackstopTests`).
[<Collection("Console")>]
module RegistrySkillManifestContainmentTests =

    // `run` reports the escape on stderr; capture it without disturbing the shared console.
    let private captureStderr (thunk: unit -> int) =
        let original = Console.Error
        use writer = new StringWriter()
        Console.SetError writer

        try
            let code = thunk ()
            writer.Flush()
            code, writer.ToString()
        finally
            Console.SetError original

    [<Fact>]
    let ``--root absolute is refused with exit 1 and writes nothing`` () =
        // An out-of-tree absolute root: `Path.Combine` would take it verbatim and `--write`
        // would land the manifest under it. The guard must fire before any directory is created.
        let outside =
            Path.Combine(Path.GetTempPath(), "fsgg-sdd-237-" + Guid.NewGuid().ToString("N"))

        let escapingTarget =
            Path.Combine(outside, ".agents", "skills", "skill-manifest.json")

        let code, stderr =
            captureStderr (fun () -> RegistrySkillManifest.run [ "--write"; "--root"; outside ])

        Assert.Equal(1, code)
        Assert.Contains("escapes the workspace root", stderr)
        Assert.False(File.Exists escapingTarget)
        Assert.False(Directory.Exists outside)

    [<Theory>]
    [<InlineData("..")>]
    [<InlineData("../escape")>]
    [<InlineData("sub/../../etc")>]
    let ``--root that escapes via .. is refused with exit 1`` (root: string) =
        let code, stderr =
            captureStderr (fun () -> RegistrySkillManifest.run [ "--check"; "--root"; root ])

        Assert.Equal(1, code)
        Assert.Contains("escapes the workspace root", stderr)

    [<Fact>]
    let ``a relative in-workspace root is not treated as an escape`` () =
        // A relative subdir stays inside the workspace: `--check` still fails (the manifest is
        // absent under it), but with the MISSING drift hint, never the containment diagnostic.
        let code, stderr =
            captureStderr (fun () -> RegistrySkillManifest.run [ "--check"; "--root"; "some/relative/dir" ])

        Assert.Equal(1, code)
        Assert.DoesNotContain("escapes the workspace root", stderr)
