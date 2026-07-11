namespace ExampleApp.Tests

open ExampleApp
open Xunit

/// Proves the generated view stays current with its authored source. Cited by evidence EV011.
module WorkModelViewTests =

    /// A generated view is a projection: regenerating it from an unchanged source must reproduce it
    /// byte for byte, or "stale view" is undetectable.
    [<Fact>]
    let ``regenerating the view from an unchanged source is a no-op`` () =
        let source = MatchSource.sample
        let first = WorkModelView.generate source

        Assert.Equal(first, WorkModelView.generate source)

    /// A changed source must move the view — the other half of currency.
    [<Fact>]
    let ``a changed source changes the view`` () =
        let view = WorkModelView.generate MatchSource.sample
        let changed = WorkModelView.generate { MatchSource.sample with Title = "A different match" }

        Assert.NotEqual(view, changed)
