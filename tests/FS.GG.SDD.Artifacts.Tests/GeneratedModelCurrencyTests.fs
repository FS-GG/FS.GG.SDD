namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module GeneratedModelCurrencyTests =
    [<Fact>]
    let ``GeneratedModelCurrency reports missing generated work model`` () =
        let diagnostics = TestSupport.currencyDiagnostics "missing-generated-model"

        Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "missingGeneratedWorkModel")

    [<Fact>]
    let ``GeneratedModelCurrency reports stale source digest`` () =
        let diagnostics = TestSupport.currencyDiagnostics "stale-source-digest"

        Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView")

    [<Fact>]
    let ``GeneratedModelCurrency reports stale generator version`` () =
        let diagnostics = TestSupport.currencyDiagnostics "stale-generator-version"

        Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView")

    [<Fact>]
    let ``GeneratedModelCurrency reports malformed generated JSON`` () =
        let diagnostics = TestSupport.currencyDiagnostics "malformed-generated-json"

        Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView" && diagnostic.Message.Contains("could not be parsed"))

    [<Fact>]
    let ``GeneratedModelCurrency valid generated model is current`` () =
        let diagnostics = TestSupport.currencyDiagnostics "valid-work-item"

        Assert.Empty(diagnostics)
