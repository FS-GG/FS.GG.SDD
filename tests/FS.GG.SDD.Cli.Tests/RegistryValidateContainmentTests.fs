namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open FS.GG.SDD.Cli
open FS.GG.SDD.TestShared
open Xunit

/// FS-GG/FS.GG.SDD#263 (Gap C finding 4 / #203): `registry validate <path>` flows the positional
/// straight into `RegistryDocument.load path` → `File.ReadAllText path`, so an absolute or `..`
/// path would read an arbitrary file. It must be confined the same way `registry skill-manifest
/// --root` (#239) and `surface` (#185) are: an escaping path is refused *before* the read with a
/// blocking verdict (exit 1). Unlike skill-manifest's stderr channel, `validate` always emits a
/// verdict JSON (gate-callable), so the escape surfaces there as `valid:false` — parity with its
/// unrecognized-option / missing-path handling (#258). In-process so the tests stay on the fast
/// tier and observe the "no read performed" invariant directly.
///
/// Serialized via the `Console` collection: the capture swaps the process-global `Console.Out`,
/// so it must not run concurrently with any other stdout-capturing class.
[<Collection("Console")>]
module RegistryValidateContainmentTests =

    let private captureStdout (thunk: unit -> int) =
        let original = Console.Out
        use writer = new StringWriter()
        Console.SetOut writer

        try
            let code = thunk ()
            writer.Flush()
            code, writer.ToString()
        finally
            Console.SetOut original

    // The canonical registry fixture validates clean through `Registry.validateDocument`
    // (see FS.GG.SDD.Artifacts.Tests/RegistryDocumentParseTests). Copied to an absolute path
    // outside the workspace, it is the "no read" witness: were the guard absent, `validate`
    // would load it and return `valid:true` — so a `valid:false` escape verdict proves the read
    // never happened.
    let private canonicalFixture =
        Path.Combine(TestShared.repoRoot, "tests", "fixtures", "registry", "dependencies.yml")

    [<Fact>]
    let ``an absolute path is refused before the read, even when it points at a valid registry`` () =
        let outside =
            Path.Combine(Path.GetTempPath(), $"fsgg-sdd-263-{Guid.NewGuid():N}.yml")

        File.Copy(canonicalFixture, outside)

        try
            let code, stdout =
                captureStdout (fun () -> RegistryValidate.run [ "validate"; outside ])

            Assert.Equal(1, code)
            Assert.Contains("escapes the workspace root", stdout)
            // The read never ran: the verdict is the escape, not the valid-file `valid:true`.
            Assert.Contains("\"valid\": false", stdout)
            Assert.DoesNotContain("\"valid\": true", stdout)
        finally
            File.Delete outside

    [<Theory>]
    [<InlineData("..")>]
    [<InlineData("../escape.yml")>]
    [<InlineData("sub/../../etc/passwd")>]
    let ``a path that escapes via .. is refused with exit 1 through the stdout verdict`` (path: string) =
        let code, stdout =
            captureStdout (fun () -> RegistryValidate.run [ "validate"; path ])

        Assert.Equal(1, code)
        Assert.Contains("escapes the workspace root", stdout)
        Assert.Contains("\"valid\": false", stdout)

    [<Fact>]
    let ``a relative in-workspace path is not treated as an escape`` () =
        // A relative subpath stays inside the workspace: an absent file fails as a
        // MalformedDocument-class load diagnostic, never the containment one — proving the guard
        // does not over-reach onto ordinary relative paths.
        let missing = $"some/relative/fsgg-sdd-263-{Guid.NewGuid():N}.yml"

        let code, stdout =
            captureStdout (fun () -> RegistryValidate.run [ "validate"; missing ])

        Assert.Equal(1, code)
        Assert.DoesNotContain("escapes the workspace root", stdout)
        Assert.Contains(missing, stdout)
