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

        Assert.Contains(
            diagnostics,
            fun diagnostic ->
                diagnostic.Id = "staleGeneratedView"
                && diagnostic.Message.Contains("could not be parsed")
        )

    [<Fact>]
    let ``GeneratedModelCurrency valid generated model is current`` () =
        let diagnostics = TestSupport.currencyDiagnostics "valid-work-item"

        Assert.Empty(diagnostics)

    // §3.4 parity (FR-005/007): the currency-check input set MUST mirror the generation
    // source set. A source recorded by the generator but absent from the check set
    // spuriously flags staleGeneratedView — the exact self-inflicted staleness the Commands
    // snapshot-set fix prevents by including every authored generation source (plan/charter).
    [<Fact>]
    let ``GeneratedModelCurrency requires the check set to mirror the generation source set`` () =
        let generatorVersion = SchemaVersion.currentGeneratorVersion ()
        let full = TestSupport.normalizedSnapshots "valid-work-item"

        Assert.Empty(Serialization.checkGeneratedWorkModelCurrency full "002-normalized-work-model" generatorVersion)

        let withoutAuthoredSource =
            full |> List.filter (fun snapshot -> not (snapshot.Path.EndsWith "spec.md"))

        let diagnostics =
            Serialization.checkGeneratedWorkModelCurrency
                withoutAuthoredSource
                "002-normalized-work-model"
                generatorVersion

        Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "staleGeneratedView")
