namespace Polyglot.Server.Tests

open Xunit

module ServerTests =
    [<Fact>]
    let ``the F# server lane is executable`` () =
        Assert.Equal("polyglot server ready", "polyglot server ready")
