namespace ExampleApp.Tests

open ExampleApp
open Xunit

/// Proves the app actually runs end to end — the smoke leg. Cited by evidence EV009.
///
/// A contract test can pass over a model that no real invocation ever produces; this drives the
/// entry point instead, so "it builds" and "it runs" are separately evidenced.
module CommandSmokeTests =

    [<Fact>]
    let ``a match can be played from the entry point`` () =
        let result = Program.run [| "--serve"; "player-one" |]

        Assert.Equal(0, result.ExitCode)
        Assert.False(System.String.IsNullOrWhiteSpace result.Output)

    [<Fact>]
    let ``an unknown argument fails loudly rather than silently`` () =
        let result = Program.run [| "--not-a-flag" |]

        Assert.NotEqual(0, result.ExitCode)
