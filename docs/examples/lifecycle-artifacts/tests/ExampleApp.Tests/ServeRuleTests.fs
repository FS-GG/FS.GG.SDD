namespace ExampleApp.Tests

open ExampleApp
open Xunit

/// Proves FR-001 / AC-001: the serve after a point goes to the player who LOST the prior rally.
/// Cited by evidence EV001 and EV003 in the example's `evidence.yml`.
///
/// This file is part of the shipped example corpus. It exists because `fsgg-sdd evidence` will not
/// accept a `result: pass` that cites an artifact which is not on disk (FS.GG.SDD#349) — an example
/// whose evidence pointed at tests that were never written was teaching authors to cite fiction.
module ServeRuleTests =

    /// AC-001: given a rally has just ended, the next serve goes toward the loser.
    [<Fact>]
    let ``serve goes to the player who lost the prior rally`` () =
        let rally =
            { Winner = PlayerOne
              Loser = PlayerTwo }

        Assert.Equal(PlayerTwo, ServeRule.nextServer rally)

    /// The rule is symmetric — it is "the loser serves", not "player two serves".
    [<Fact>]
    let ``serve follows the loser, whichever player that is`` () =
        let rally =
            { Winner = PlayerTwo
              Loser = PlayerOne }

        Assert.Equal(PlayerOne, ServeRule.nextServer rally)

    /// AMB-001 was resolved to "the loser serves" (not alternating), so two consecutive rallies won
    /// by the same player must serve to the same opponent twice — the failure leg of that decision.
    [<Fact>]
    let ``consecutive rallies won by one player serve to the same opponent`` () =
        let rally =
            { Winner = PlayerOne
              Loser = PlayerTwo }

        Assert.Equal(ServeRule.nextServer rally, ServeRule.nextServer rally)
