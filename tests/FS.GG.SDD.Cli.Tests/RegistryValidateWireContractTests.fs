namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open FS.GG.SDD.Cli
open Xunit

/// FS.GG.SDD#589 / ADR-0052: `registry validate` understands the wire-contract dimension
/// END-TO-END through the shipped CLI — not just at the `Registry.validateDocument` /
/// `RegistryDocument.load` layers the other suites cover. The dispatch itself is unchanged
/// (a document with no root `skills:` key routes to the dependency-registry validator, which
/// now checks wire contracts), so this is the wiring proof: a well-formed provenance emits
/// `valid:true`, and a malformed one surfaces the `wire-contract` diagnostic in the CLI's own
/// stdout verdict.
///
/// In-process, CWD-relative path (the containment guard refuses absolute / `..` paths, so the
/// fixture is written beside the test's working directory and passed by its relative name), and
/// serialized via the `Console` collection because the capture swaps process-global `Console.Out`.
[<Collection("Console")>]
module RegistryValidateWireContractTests =

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

    /// A minimal, otherwise-coherent registry document whose single contract carries `wireBlock`
    /// verbatim. Written to a CWD-relative file so it clears the lexical containment guard.
    let private runValidateWith (wireBlock: string) =
        let relative = $"fsgg-sdd-589-wire-{Guid.NewGuid():N}.yml"

        let text =
            "schemaVersion: 1\n"
            + "repos:\n  net:\n    name: FS.GG.Net\n    role: r\n"
            + "contracts:\n"
            + "  - id: sc2-client-protocol\n"
            + "    version: \"1.0.0\"\n"
            + "    owner: net\n"
            + "    surface: src/FS.GG.Net/Protocol.fsi\n"
            + "    consumers: []\n"
            + wireBlock

        File.WriteAllText(relative, text)

        try
            captureStdout (fun () -> RegistryValidate.run [ "validate"; relative ])
        finally
            File.Delete relative

    [<Fact>]
    let ``a well-formed vendored-proto wire contract validates clean through the CLI`` () =
        let wire =
            "    wire-contract:\n"
            + "      provenance: vendored-proto\n"
            + "      upstream: Blizzard/s2client-proto\n"
            + "      upstream-version: \"5.0.12\"\n"

        let code, stdout = runValidateWith wire
        Assert.Equal(0, code)
        Assert.Contains("\"valid\": true", stdout)

    [<Fact>]
    let ``a malformed wire contract surfaces the wire-contract diagnostic through the CLI`` () =
        let wire = "    wire-contract:\n      provenance: grpc\n"
        let code, stdout = runValidateWith wire
        Assert.Equal(1, code)
        Assert.Contains("\"valid\": false", stdout)
        Assert.Contains("wire-contract", stdout)
