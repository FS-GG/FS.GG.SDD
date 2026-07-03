namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module SpecificationArtifactTests =
    let specificationText =
        """---
schemaVersion: 1
workId: 005-specify-command
title: Specify Command
stage: specify
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# Specify Command Specification

Prose status: specified

## User Value
Create a native specify command.

## Scope
- SB-001: One chartered work item.

## Non-Goals
- SB-002: No Governance enforcement.

## User Stories
- US-001 (P1): Create a work specification.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a chartered work item, when specify runs, then spec.md exists.

## Functional Requirements
- FR-001: The specify command creates spec.md. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Command report JSON includes specification facts.

## Lifecycle Notes
- Next lifecycle action: clarify.
"""

    let snapshot text =
        ({ Path = "work/005-specify-command/spec.md"
           Text = text }
        : FileSnapshot)

    [<Fact>]
    let ``Specification scoped ids validate expected shapes`` () =
        Assert.Equal(
            "US-001",
            Identifiers.createUserStoryId "us-001"
            |> Result.map Identifiers.userStoryIdValue
            |> Result.defaultWith failwith
        )

        Assert.Equal(
            "AC-001",
            Identifiers.createAcceptanceScenarioId "ac-001"
            |> Result.map Identifiers.acceptanceScenarioIdValue
            |> Result.defaultWith failwith
        )

        Assert.Equal(
            "SB-001",
            Identifiers.createScopeBoundaryId "sb-001"
            |> Result.map Identifiers.scopeBoundaryIdValue
            |> Result.defaultWith failwith
        )

        Assert.Equal(
            "AMB-001",
            Identifiers.createAmbiguityId "amb-001"
            |> Result.map Identifiers.ambiguityIdValue
            |> Result.defaultWith failwith
        )

        Assert.True(Identifiers.createUserStoryId "story-1" |> Result.isError)

    [<Fact>]
    let ``Specification parser extracts front matter sections ids and references`` () =
        match parseSpecificationFacts (snapshot specificationText) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            Assert.Equal("005-specify-command", facts.FrontMatter.WorkId.Value)
            Assert.Equal(Identifiers.LifecycleStage.Specify, facts.FrontMatter.Stage)
            Assert.Empty(facts.MissingStandardSections)
            Assert.Equal<string list>([ "US-001" ], facts.UserStoryIds |> List.map Identifiers.userStoryIdValue)
            Assert.Equal<string list>([ "FR-001" ], facts.RequirementIds |> List.map Identifiers.requirementIdValue)

            Assert.Equal<string list>(
                [ "AC-001" ],
                facts.AcceptanceScenarioIds |> List.map Identifiers.acceptanceScenarioIdValue
            )

            Assert.Equal<string list>(
                [ "SB-001"; "SB-002" ],
                facts.ScopeBoundaryIds |> List.map Identifiers.scopeBoundaryIdValue
            )

            Assert.Empty(facts.AmbiguityIds)
            Assert.Empty(facts.Diagnostics)

            Assert.Contains(
                facts.RequirementReferences,
                fun reference -> reference.RequirementId.Value = "FR-001" && reference.StoryIds.Length = 1
            )

    // §3.3 (FR-003/004): a "none outstanding" note under ## Ambiguities — prose or bullet —
    // is a non-blocking sentinel (no AmbiguityId, no missing-id diagnostic); genuine AMB-###
    // bullets still parse, and a sentinel alongside a real one does not suppress the real one.
    let private ambiguityMissingId (facts: SpecificationFacts) =
        facts.Diagnostics
        |> List.exists (fun diagnostic -> diagnostic.RelatedIds |> List.contains "AMB-###")

    [<Fact>]
    let ``Specification bullet none-outstanding disclaimer is a non-blocking sentinel`` () =
        let bulletDisclaimer =
            specificationText.Replace("No material ambiguities recorded.", "- None outstanding")

        match parseSpecificationFacts (snapshot bulletDisclaimer) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(facts.AmbiguityIds)
            Assert.False(ambiguityMissingId facts)

    [<Fact>]
    let ``Specification prose none-outstanding disclaimer remains non-blocking`` () =
        match parseSpecificationFacts (snapshot specificationText) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(facts.AmbiguityIds)
            Assert.False(ambiguityMissingId facts)

    [<Fact>]
    let ``Specification genuine ambiguity bullet still yields an AmbiguityId`` () =
        let withAmbiguity =
            specificationText.Replace(
                "No material ambiguities recorded.",
                "- AMB-001 open: Where should durable decisions be recorded?"
            )

        match parseSpecificationFacts (snapshot withAmbiguity) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Equal<string list>([ "AMB-001" ], facts.AmbiguityIds |> List.map Identifiers.ambiguityIdValue)

    [<Fact>]
    let ``Specification mixed disclaimer and genuine ambiguity keeps only the real id`` () =
        let mixed =
            specificationText.Replace(
                "No material ambiguities recorded.",
                "- None outstanding\n- AMB-001 open: Where should durable decisions be recorded?"
            )

        match parseSpecificationFacts (snapshot mixed) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Equal<string list>([ "AMB-001" ], facts.AmbiguityIds |> List.map Identifiers.ambiguityIdValue)
            Assert.False(ambiguityMissingId facts)

    [<Fact>]
    let ``Specification parser reports duplicate ids and unknown references`` () =
        let broken =
            specificationText
                .Replace(
                    "- US-001 (P1): Create a work specification.",
                    "- US-001 (P1): Create a work specification.\n- US-001 (P1): Duplicate story."
                )
                .Replace("Stories: US-001; Acceptance: AC-001", "Stories: US-999; Acceptance: AC-999")

        match parseSpecificationFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            let ids = facts.Diagnostics |> List.map _.Id
            Assert.Contains("duplicateIdentifier", ids)
            Assert.Contains("unknownReference", ids)
