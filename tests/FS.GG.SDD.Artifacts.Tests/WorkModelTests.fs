namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module WorkModelTests =
    [<Fact>]
    let ``Valid fixture normalizes to work model with zero blocking diagnostics`` () =
        let model = TestSupport.model "valid-work-item"

        TestSupport.assertNoBlockingDiagnostics model
        Assert.Equal("001-sdd-artifact-model", model.WorkId)
        Assert.Equal("fs-gg-sdd", model.Project.Id)
        Assert.Contains(model.Requirements, fun requirement -> requirement.Id = "FR-001")
        Assert.Contains(model.Tasks, fun task -> task.Id = "T001")
        Assert.Contains(model.Evidence, fun evidence -> evidence.Id = "EV001")

    [<Fact>]
    let ``Work model JSON emits documented top-level fields`` () =
        let json = TestSupport.model "valid-work-item" |> Serialization.serializeWorkModel

        Assert.Contains("\"schemaVersion\"", json)
        Assert.Contains("\"modelVersion\"", json)
        Assert.Contains("\"workId\"", json)
        Assert.Contains("\"governanceBoundaries\"", json)

    [<Fact>]
    let ``Duplicate identifier fixture emits duplicateIdentifier`` () =
        let model = TestSupport.model "duplicate-identifiers"
        TestSupport.assertDiagnostic "duplicateIdentifier" model

    [<Fact>]
    let ``Unknown reference fixture emits unknownReference`` () =
        let model = TestSupport.model "unknown-reference"
        TestSupport.assertDiagnostic "unknownReference" model

    [<Fact>]
    let ``Prose structured mismatch keeps structured model and emits warning`` () =
        let model = TestSupport.model "prose-structured-mismatch"
        TestSupport.assertDiagnostic "proseStructuredMismatch" model
        Assert.Equal("draft", model.WorkItem.Status)
        Assert.Empty(WorkModel.blockingDiagnostics model)

    [<Fact>]
    let ``Stale generated view fixture emits staleGeneratedView`` () =
        let model = TestSupport.model "stale-generated-view"
        TestSupport.assertDiagnostic "staleGeneratedView" model
