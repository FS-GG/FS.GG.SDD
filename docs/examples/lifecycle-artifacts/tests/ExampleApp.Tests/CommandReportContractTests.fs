namespace ExampleApp.Tests

open ExampleApp
open Xunit

/// Proves the command-report contract the spec declares under "Public Or Tool-Facing Impact":
/// the app's report is a stable, deterministic JSON contract. Cited by evidence EV008.
module CommandReportContractTests =

    /// The JSON shape is the machine contract (Constitution II) — the keys are the promise.
    [<Fact>]
    let ``report carries the contracted keys`` () =
        let json = Report.toJson (Report.forMatch RallyScore.zero)

        Assert.Contains("\"schemaVersion\"", json)
        Assert.Contains("\"score\"", json)
        Assert.Contains("\"nextServer\"", json)

    /// Determinism: the same model must render byte-identically, or goldens are worthless.
    [<Fact>]
    let ``report rendering is deterministic`` () =
        let model = Report.forMatch (RallyScore.zero |> RallyScore.award PlayerOne)

        Assert.Equal(Report.toJson model, Report.toJson model)
