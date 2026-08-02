namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module WorkModelTests =
    let private withProfileAndIntent profile intent =
        TestSupport.snapshots "valid-work-item"
        |> List.map (fun snapshot ->
            if snapshot.Path = ".fsgg/project.yml" then
                { snapshot with
                    Text =
                        snapshot.Text.Replace(
                            "  defaultWorkRoot: work",
                            $"  defaultWorkRoot: work\n  profile: {profile}"
                        ) }
            elif snapshot.Path.EndsWith("/spec.md") && Option.isSome intent then
                { snapshot with
                    Text = snapshot.Text.Replace("status: draft\n---", $"status: draft\n{Option.get intent}---")
                    RawBytes = None }
            else
                snapshot)
        |> fun snapshots -> Serialization.normalizeSnapshotsToWorkModel snapshots "001-sdd-artifact-model"

    let private activeIntent =
        """performanceIntent:
  id: PI-001
  disposition: active
  targetFps: 60
  workloadIds: [normal-play]
  workloadDefinitionDigests: [normal-play=sha256:normal-v1]
  maximumExpectedScale: 10000 sprites
  maxP95Ms: 16.67
  maxP99Ms: 25
  maxCatchUpFrames: 0
  structuralCostBudgets: [draw-calls<=500]
  requiredCapability: live-compositor
  liveCompositorRequired: true
  evidenceRefs: []
"""

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
    let ``interactive profile without intent fails closed before implementation`` () =
        let model = withProfileAndIntent "interactive" None
        TestSupport.assertDiagnostic "performance.intentNotReady" model

    [<Fact>]
    let ``active interactive intent is typed and implementation ready`` () =
        let model = withProfileAndIntent "interactive" (Some activeIntent)
        Assert.Equal(Some "PI-001", model.PerformanceIntent |> Option.map _.Id)
        Assert.DoesNotContain(model.Diagnostics, fun diagnostic -> diagnostic.Id = "performance.intentNotReady")

    [<Fact>]
    let ``placeholder workload definition fails closed`` () =
        let model =
            withProfileAndIntent "interactive" (Some(activeIntent.Replace("sha256:normal-v1", "TODO-placeholder")))

        TestSupport.assertDiagnostic "performance.intentNotReady" model

    [<Theory>]
    [<InlineData("other=sha256:normal-v1")>]
    [<InlineData("normal-play=sha256:a, normal-play=sha256:b")>]
    [<InlineData("normal-play=")>]
    [<InlineData("normal-play=md5:not-authoritative")>]
    let ``wrong duplicate or malformed workload binding fails closed`` binding =
        let model =
            withProfileAndIntent
                "interactive"
                (Some(
                    activeIntent.Replace(
                        "workloadDefinitionDigests: [normal-play=sha256:normal-v1]",
                        $"workloadDefinitionDigests: [{binding}]"
                    )
                ))

        TestSupport.assertDiagnostic "performance.intentNotReady" model

    [<Fact>]
    let ``non-interactive profile remains compatible without intent`` () =
        let model = withProfileAndIntent "library" None
        Assert.DoesNotContain(model.Diagnostics, fun diagnostic -> diagnostic.Id = "performance.intentNotReady")

    [<Fact>]
    let ``supported non-applicability is accepted`` () =
        let intent =
            """performanceIntent:
  id: PI-002
  disposition: not-applicable
  targetFps: 0
  workloadIds: []
  workloadDefinitionDigests: []
  maximumExpectedScale: none
  maxP95Ms: 0
  maxP99Ms: 0
  maxCatchUpFrames: 0
  structuralCostBudgets: []
  requiredCapability: none
  liveCompositorRequired: false
  evidenceRefs: [DEC-001]
  rationale: This change has no runtime interactive path.
"""

        let model = withProfileAndIntent "interactive" (Some intent)
        Assert.DoesNotContain(model.Diagnostics, fun diagnostic -> diagnostic.Id = "performance.intentNotReady")

    [<Fact>]
    let ``deferral remains blocking typed debt`` () =
        let intent =
            """performanceIntent:
  id: PI-003
  disposition: deferred
  targetFps: 60
  workloadIds: [normal-play]
  workloadDefinitionDigests: [normal-play=sha256:normal-v1]
  maximumExpectedScale: 10000 sprites
  maxP95Ms: 16.67
  maxP99Ms: 25
  maxCatchUpFrames: 0
  structuralCostBudgets: [draw-calls<=500]
  requiredCapability: live-compositor
  liveCompositorRequired: true
  deferralIssue: FS-GG/Product#123
  evidenceRefs: [DEC-001]
"""

        let model = withProfileAndIntent "interactive" (Some intent)
        TestSupport.assertDiagnostic "performance.intentNotReady" model

    [<Fact>]
    let ``deferral rejects an untyped debt reference`` () =
        let intent =
            activeIntent
                .Replace("disposition: active", "disposition: deferred")
                .Replace("evidenceRefs: []", "deferralIssue: someday\n  evidenceRefs: [DEC-001]")

        let model = withProfileAndIntent "interactive" (Some intent)

        Assert.Contains(
            model.Diagnostics,
            fun diagnostic ->
                diagnostic.Id = "performance.intentNotReady"
                && diagnostic.Message.Contains("owner/repo#N")
        )

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
          Text = $"## Decisions\n{line}\n"
          RawBytes = None }

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
