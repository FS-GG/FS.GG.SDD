namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.FsharpIdentifier
open Xunit

/// Feature 080: the generic name → valid-F#-namespace derivation. Golden table from
/// specs/080-scaffold-name-sanitization/contracts/fsharp-identifier-derivation.md plus
/// the module invariants (idempotence, no-op-on-valid, determinism).
module FsharpIdentifierTests =

    [<Theory>]
    // Input, expected Ok output.
    [<InlineData("Roquelike-DungeonCrawler", "RoquelikeDungeonCrawler")>] // the reported defect: hyphen dropped
    [<InlineData("Acme", "Acme")>] // no-op
    [<InlineData("Acme.Foo", "Acme.Foo")>] // dots preserved as segment boundaries
    [<InlineData("My App", "MyApp")>] // space dropped
    [<InlineData("foo.bar-baz", "foo.barbaz")>] // per-segment sanitization
    [<InlineData("3Crawler", "_3Crawler")>] // leading digit guarded
    [<InlineData("type", "type_")>] // reserved keyword guarded
    [<InlineData("Acme..Foo", "Acme.Foo")>] // empty interior segment collapsed
    let ``deriveNamespace produces the golden identifier`` (input: string) (expected: string) =
        Assert.Equal(Ok expected, deriveNamespace input)

    [<Theory>]
    [<InlineData("---")>] // no identifier character
    [<InlineData("")>] // empty
    [<InlineData(".")>] // only a separator
    let ``deriveNamespace reports an unrepresentable name`` (input: string) =
        Assert.Equal(Error(Unrepresentable input), deriveNamespace input)

    // FR-003 / SC-005: already-valid names are forwarded unchanged.
    [<Theory>]
    [<InlineData("Acme")>]
    [<InlineData("Acme.Foo.Bar")>]
    [<InlineData("_3Crawler")>]
    [<InlineData("type_")>]
    let ``deriveNamespace is a no-op on already-valid namespaces`` (valid: string) =
        Assert.Equal(Ok valid, deriveNamespace valid)

    // Invariant: deriving a derived value changes nothing (idempotent).
    [<Theory>]
    [<InlineData("Roquelike-DungeonCrawler")>]
    [<InlineData("My App")>]
    [<InlineData("3Crawler")>]
    [<InlineData("type")>]
    let ``deriveNamespace is idempotent`` (input: string) =
        match deriveNamespace input with
        | Ok once -> Assert.Equal(Ok once, deriveNamespace once)
        | Error _ -> Assert.Fail "expected a derivable input"

    // Determinism: repeated calls agree (pure, ordinal, no culture/clock).
    [<Fact>]
    let ``deriveNamespace is deterministic across repeated calls`` () =
        let input = "Roquelike-DungeonCrawler"
        Assert.Equal(deriveNamespace input, deriveNamespace input)
