namespace FS.GG.SDD.Cli.Tests

open System
open System.IO
open FS.GG.SDD.Cli
open FS.GG.SDD.TestShared
open Xunit

/// FS-GG/FS.GG.SDD#789: `registry validate` over a registry whose bytes are not valid UTF-8.
///
/// FS.GG.SDD#748 routed `CommandEffects`' read through `SkillMirror.decodeBody`; the registry
/// documents did not read through it. `File.ReadAllText` SUBSTITUTES `U+FFFD` for a sequence it
/// cannot decode and returns a string, so `validate` parsed text nobody authored and answered
/// `valid: true` — `.github#266`'s shape, *"I could not read this"* rendered as *"I read it and
/// it passed"*.
///
/// The verdict-level half of the fix lives here because ACs 1 and 3 are stated about `registry
/// validate`, not about the load edge: the refusal must surface as the command's ordinary
/// invalid verdict at its EXISTING failure exit code (1), never as a `toolDefect` at exit 2. A
/// mis-encoded file in the workspace is an authoring accident, not a broken tool
/// (FS.GG.SDD#745/#754/#748). The parse-edge legs are in
/// `FS.GG.SDD.Artifacts.Tests/RegistryDocumentParseTests`.
///
/// In-process against `RegistryValidate.validate`, which is where the verdict is decided —
/// path confinement (#263) is applied by the command entry above it and is a separate concern.
module RegistryValidateEncodingTests =

    let private canonicalFixture =
        Path.Combine(TestShared.repoRoot, "tests", "fixtures", "registry", "dependencies.yml")

    /// First index of `needle` in `haystack`, or -1.
    let private indexOfBytes (haystack: byte array) (needle: byte array) =
        let limit = haystack.Length - needle.Length

        let rec search i =
            if i > limit then -1
            elif Array.sub haystack i needle.Length = needle then i
            else search (i + 1)

        search 0

    /// The canonical fixture with ONE invalid UTF-8 byte (`0x80`, a lone continuation) spliced
    /// into a scalar value, leaving a document that is still perfectly well-formed YAML — and
    /// validates clean — once `U+FFFD` is substituted for it. That is what made the old
    /// `valid: true` so convincing.
    let private mangledFixture () =
        let bytes = File.ReadAllBytes canonicalFixture
        let marker = Text.Encoding.UTF8.GetBytes "name: FS.GG.SDD"
        let at = indexOfBytes bytes marker
        Assert.True(at >= 0, "marker must occur in the canonical fixture")
        let offset = at + marker.Length

        let temp = Path.Combine(Path.GetTempPath(), $"fsgg-sdd-789-{Guid.NewGuid():N}.yml")

        File.WriteAllBytes(temp, Array.concat [ bytes[.. offset - 1]; [| 0x80uy |]; bytes[offset..] ])
        temp, offset

    [<Fact>]
    let ``a mis-encoded registry is NOT reported valid`` () =
        let path, _ = mangledFixture ()

        try
            Assert.False((RegistryValidate.validate path).Valid)
        finally
            File.Delete path

    /// AC3: the validator's existing failure code. Not 0, and emphatically not the exit 2 a
    /// `toolDefect` would take had the refusal escaped as an exception.
    [<Fact>]
    let ``its exit code is the validator's existing failure code, 1`` () =
        let path, _ = mangledFixture ()

        try
            Assert.Equal(1, RegistryValidate.exitCode (RegistryValidate.validate path))
        finally
            File.Delete path

    /// AC2, at the verdict surface: one `MalformedDocument` diagnostic — the same class every
    /// other load failure takes, never a cascade — naming the file and the byte offset, and not
    /// claiming a YAML syntax error the document does not have.
    ///
    /// The offset is asserted WITH its label. A bare `Assert.Contains(string offset, …)` would
    /// be near-tautological here: the temp path this fixture writes already contains `789` and
    /// a 32-char hex GUID, so a bare decimal could match a line, a column, or pure coincidence.
    [<Fact>]
    let ``the verdict carries one MalformedDocument diagnostic naming the file and the offset`` () =
        let path, offset = mangledFixture ()

        try
            let report = RegistryValidate.validate path
            let diagnostic = Assert.Single report.Diagnostics
            Assert.Equal("MalformedDocument", diagnostic.Rule)
            Assert.Contains(path, diagnostic.Message)
            Assert.Contains($"byte offset {offset}", diagnostic.Message)
            Assert.DoesNotContain("YAML syntax error", diagnostic.Message)
        finally
            File.Delete path

    /// The no-op direction at the verdict surface: the untouched canonical fixture still
    /// validates clean at exit 0, so the refusal above cost no valid registry its verdict.
    [<Fact>]
    let ``the untouched canonical fixture still validates clean at exit 0`` () =
        let report = RegistryValidate.validate canonicalFixture
        Assert.True(report.Valid)
        Assert.Equal(0, RegistryValidate.exitCode report)
