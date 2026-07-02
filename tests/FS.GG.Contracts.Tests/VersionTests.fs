namespace FS.GG.Contracts.Tests

open Fsgg
open Xunit

module VersionTests =

    // --- tryParse ---

    [<Fact>]
    let ``tryParse accepts a valid triple`` () =
        let expected: Version.Version = { Major = 1; Minor = 2; Patch = 3 }
        Assert.Equal(Some expected, Version.tryParse "1.2.3")

    [<Fact>]
    let ``tryParse accepts zeros`` () =
        let expected: Version.Version = { Major = 0; Minor = 0; Patch = 0 }
        Assert.Equal(Some expected, Version.tryParse "0.0.0")

    [<Theory>]
    [<InlineData("1.2")>] // too few components
    [<InlineData("1.2.3.4")>] // too many components
    [<InlineData("1.2.x")>] // non-numeric
    [<InlineData("v1.2.3")>] // prefix
    [<InlineData("-1.2.3")>] // negative
    [<InlineData("")>]
    [<InlineData("1")>]
    let ``tryParse rejects non-triples`` (text: string) =
        Assert.Equal(None, Version.tryParse text)

    // NumberStyles.None + invariant culture: the default Int32.TryParse
    // (NumberStyles.Integer, current culture) accepted embedded whitespace and
    // leading signs, so "1. 2.+3" parsed as 1.2.3. This grammar gates provider
    // minimumCliVersion coherence, so the looseness must be rejected.
    [<Theory>]
    [<InlineData("1. 2.+3")>] // embedded whitespace and a leading sign
    [<InlineData("1. 2.3")>] // leading whitespace in a component
    [<InlineData("+1.2.3")>] // leading sign
    [<InlineData("1.+2.3")>] // leading sign in a component
    [<InlineData(" 1.2.3")>] // leading whitespace
    [<InlineData("1.2.3 ")>] // trailing whitespace
    let ``tryParse rejects whitespace and signs`` (text: string) =
        Assert.Equal(None, Version.tryParse text)

    // --- compare ---

    [<Fact>]
    let ``compare less-than yields Some -1`` () =
        Assert.Equal(Some -1, Version.compare "0.2.1" "0.3.0")

    [<Fact>]
    let ``compare equal yields Some 0`` () =
        Assert.Equal(Some 0, Version.compare "1.4.2" "1.4.2")

    [<Fact>]
    let ``compare greater-than yields Some 1`` () =
        Assert.Equal(Some 1, Version.compare "2.0.0" "1.9.9")

    [<Fact>]
    let ``compare orders by minor then patch`` () =
        Assert.Equal(Some -1, Version.compare "1.2.9" "1.3.0")
        Assert.Equal(Some 1, Version.compare "1.2.9" "1.2.8")

    [<Theory>]
    [<InlineData("bad", "1.2.3")>]
    [<InlineData("1.2.3", "bad")>]
    [<InlineData("bad", "worse")>]
    let ``compare yields None when either side is unparseable`` (l: string) (r: string) =
        Assert.Equal(None, Version.compare l r)

module RegistryDelegationTests =

    // T005: proves Registry range checks behave identically after delegating their
    // SemVer grammar to Fsgg.Version. The comparator engine still lives in Registry;
    // only the parse/triple grammar was extracted.
    let private modelWithRange (range: string) : Registry.RegistryModel =
        { Components =
            [ { Id = "FS.GG.Contracts"; Version = "1.0.0" }
              { Id = "FS.GG.SDD"; Version = "0.2.0" } ]
          Edges =
            [ { Consumer = "FS.GG.SDD"
                Provider = "FS.GG.Contracts"
                CompatibleRange = range } ] }

    [<Theory>]
    [<InlineData(">=1.0.0 <2.0.0")>] // 1.0.0 in range
    [<InlineData(">=1.0.0")>]
    [<InlineData("=1.0.0")>]
    [<InlineData("1.0.0")>] // bare == exact
    let ``in-range provider version stays Valid after delegation`` (range: string) =
        Assert.Equal(Registry.Valid, Registry.validate (modelWithRange range))

    [<Theory>]
    [<InlineData(">=2.0.0")>] // 1.0.0 below bound
    [<InlineData("<1.0.0")>] // boundary excluded
    [<InlineData(">1.0.0")>]
    let ``out-of-range provider version reports IncompatibleVersion after delegation`` (range: string) =
        match Registry.validate (modelWithRange range) with
        | Registry.Invalid [ d ] -> Assert.Equal(Registry.IncompatibleVersion, d.Rule)
        | other -> Assert.True(false, $"expected one IncompatibleVersion diagnostic, got {other}")
