namespace ExampleApp.Tests

open ExampleApp
open Xunit

/// Proves FR-002 / AC-002: a point updates the rally score and play continues, with no match-end
/// condition (SB-002 / AMB-002). Cited by evidence EV002, EV006, and EV007.
module RallyScoreTests =

    /// AC-002: scoring a point updates that player's score.
    [<Fact>]
    let ``scoring a point updates the rally score`` () =
        let score = RallyScore.zero |> RallyScore.award PlayerOne

        Assert.Equal(1, RallyScore.pointsFor PlayerOne score)
        Assert.Equal(0, RallyScore.pointsFor PlayerTwo score)

    /// AC-002: play continues — scoring never yields a terminal state, because this example
    /// deliberately has no win condition (Non-Goal SB-002).
    [<Fact>]
    let ``play continues after a point is scored`` () =
        let score = RallyScore.zero |> RallyScore.award PlayerTwo

        Assert.False(RallyScore.isComplete score)

    /// The scoreboard accumulates rather than replaces.
    [<Fact>]
    let ``rally scores accumulate across points`` () =
        let score =
            RallyScore.zero
            |> RallyScore.award PlayerOne
            |> RallyScore.award PlayerTwo
            |> RallyScore.award PlayerOne

        Assert.Equal(2, RallyScore.pointsFor PlayerOne score)
        Assert.Equal(1, RallyScore.pointsFor PlayerTwo score)
