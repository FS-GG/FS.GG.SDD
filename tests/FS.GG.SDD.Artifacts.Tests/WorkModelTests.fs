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

    // ---------------------------------------------------------------------------------------------
    // Feature 093 / FS.GG.SDD#164 (FS.GG.Audio feedback §3.7). `RequirementModel.parseDecisions` built
    // a `Decision` with no ref fields at all, so a `DEC-###` that settled several requirements reached
    // `work-model.json` carrying none of them. This is the parser that feeds `WorkItem.decisions`.
    // ---------------------------------------------------------------------------------------------

    let private decisionSnapshot (line: string) : FileSnapshot =
        { Path = "work/demo/clarifications.md"
          Text = $"## Decisions\n{line}\n" }

    /// FR-011. Every FR/US/AC the line names reaches the model, sorted and deduplicated.
    [<Fact>]
    let ``a decision's every reference reaches the work model, sorted`` () =
        let decision =
            decisionSnapshot "- DEC-003: Resolves FR-007, FR-001 and AC-005, touching US-002."
            |> RequirementModel.parseDecisions
            |> Assert.Single

        Assert.Equal<string list>([ "FR-001"; "FR-007" ], decision.RequirementRefs |> List.map _.Value)
        Assert.Equal<string list>([ "US-002" ], decision.StoryRefs |> List.map _.Value)
        Assert.Equal<string list>([ "AC-005" ], decision.AcceptanceRefs |> List.map _.Value)

    /// The same id named twice is one ref, not two.
    [<Fact>]
    let ``a decision's repeated reference is deduplicated`` () =
        let decision =
            decisionSnapshot "- DEC-004: FR-001 supersedes the earlier reading of FR-001."
            |> RequirementModel.parseDecisions
            |> Assert.Single

        Assert.Equal<string list>([ "FR-001" ], decision.RequirementRefs |> List.map _.Value)

    /// FR-011, negative case: refs are optional. A decision naming none is not a diagnostic.
    [<Fact>]
    let ``a decision naming no references has empty ref lists`` () =
        let decision =
            decisionSnapshot "- DEC-005: Record decisions in clarifications.md."
            |> RequirementModel.parseDecisions
            |> Assert.Single

        Assert.Empty decision.RequirementRefs
        Assert.Empty decision.StoryRefs
        Assert.Empty decision.AcceptanceRefs

    /// The refs must survive serialization to `work-model.json` — the artifact an agent actually reads.
    [<Fact>]
    let ``work model JSON carries the decision reference arrays`` () =
        let json = TestSupport.model "valid-work-item" |> Serialization.serializeWorkModel

        Assert.Contains("\"requirementRefs\"", json)
        Assert.Contains("\"storyRefs\"", json)
        Assert.Contains("\"acceptanceRefs\"", json)
