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

    // ---------------------------------------------------------------------------------------------
    // FS.GG.SDD#265 / ADR-0003. `parseDecisions` must converge on the *authored* decision grammar the
    // clarify stage and `.fsgg/early-stage-guidance.md` teach and the shipped example uses:
    // `- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]: text`. Before, it accepted only the bare
    // `- DEC-001: text` form, so a canonically-authored decision never entered the work model and any
    // task referencing it raised `unknownReference` — the Gap D fixpoint blocker.
    // ---------------------------------------------------------------------------------------------

    /// The bold id and the bracketed tags between the id and the colon are the authored form. The tags
    /// are not part of the decision text, and a tag that itself carries a colon (`[AMB:AMB-001]`) must
    /// not be read as the terminating colon.
    [<Fact>]
    let ``a decision authored in the bold-id tagged grammar parses, tags excluded from its text`` () =
        let decision =
            decisionSnapshot "- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]: The serve targets the loser."
            |> RequirementModel.parseDecisions
            |> Assert.Single

        Assert.Equal("DEC-001", decision.Id.Value)
        Assert.Equal("The serve targets the loser.", decision.Title)
        Assert.Equal("The serve targets the loser.", decision.Decision)
        Assert.Equal<string list>([ "FR-001" ], decision.RequirementRefs |> List.map _.Value)
        Assert.Equal<string list>([ "AC-001" ], decision.AcceptanceRefs |> List.map _.Value)

    /// The exact line `clarify` writes (`renderDecisionLine`): a non-bold id carrying its question and
    /// ambiguity tags. It must round-trip identically to the bold form.
    [<Fact>]
    let ``a decision in the clarify-written non-bold tagged grammar parses`` () =
        let decision =
            decisionSnapshot "- DEC-002 [CQ-002] [AMB:AMB-002]: A match-end condition is deferred."
            |> RequirementModel.parseDecisions
            |> Assert.Single

        Assert.Equal("DEC-002", decision.Id.Value)
        Assert.Equal("A match-end condition is deferred.", decision.Decision)

    /// The bare `- DEC-001: text` grammar the fixtures use keeps parsing — the fix is additive.
    [<Fact>]
    let ``the bare decision grammar still parses`` () =
        let decision =
            decisionSnapshot "- DEC-006: A plain decision with no tags."
            |> RequirementModel.parseDecisions
            |> Assert.Single

        Assert.Equal("DEC-006", decision.Id.Value)
        Assert.Equal("A plain decision with no tags.", decision.Decision)

    /// The refs must survive serialization to `work-model.json` — the artifact an agent actually reads.
    [<Fact>]
    let ``work model JSON carries the decision reference arrays`` () =
        let json = TestSupport.model "valid-work-item" |> Serialization.serializeWorkModel

        Assert.Contains("\"requirementRefs\"", json)
        Assert.Contains("\"storyRefs\"", json)
        Assert.Contains("\"acceptanceRefs\"", json)
