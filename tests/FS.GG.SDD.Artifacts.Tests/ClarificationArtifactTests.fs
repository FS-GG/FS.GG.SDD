namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module ClarificationArtifactTests =
    let clarificationText =
        """---
schemaVersion: 1
workId: 006-clarify-command
title: Clarify Command
stage: clarify
changeTier: tier1
status: clarified
sourceSpec: work/006-clarify-command/spec.md
publicOrToolFacingImpact: true
---

# Clarify Command Clarifications

## Source Specification
- work/006-clarify-command/spec.md

## Clarification Questions
- CQ-001 [AMB:AMB-001] [FR-001] [US-001] [AC-001] blocking answered: Which artifact records decisions?

## Answers
- CQ-001 [AMB:AMB-001] decision: Clarification decisions live in clarifications.md.

## Decisions
- DEC-001 [CQ-001] [AMB:AMB-001] [FR-001] [US-001] [AC-001]: Clarification decisions live in clarifications.md.

## Accepted Deferrals
- DEC-002 [CQ-001] [AMB:AMB-001]: Defer checklist command details to the checklist feature.

## Remaining Ambiguity
- AMB-002 [CQ-002] blocking: Still needs an answer.

## Lifecycle Notes
- Next lifecycle action: checklist.
"""

    let snapshot text =
        ({ Path = "work/006-clarify-command/clarifications.md"
           Text = text }
        : FileSnapshot)

    [<Fact>]
    let ``Clarification parser extracts front matter questions decisions deferrals and remaining ambiguity`` () =
        match parseClarificationFacts (snapshot clarificationText) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            Assert.Equal("006-clarify-command", facts.FrontMatter.WorkId.Value)
            Assert.Equal(Identifiers.LifecycleStage.Clarify, facts.FrontMatter.Stage)
            Assert.Equal("work/006-clarify-command/spec.md", facts.FrontMatter.SourceSpec)
            Assert.Empty(facts.MissingStandardSections)

            Assert.Equal<string list>(
                [ "CQ-001" ],
                facts.Questions |> List.map (fun question -> question.QuestionId.Value)
            )

            Assert.Equal<string list>(
                [ "DEC-001" ],
                facts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
            )

            Assert.Equal<string list>(
                [ "DEC-002" ],
                facts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value)
            )

            Assert.Equal(1, facts.BlockingAmbiguityCount)

    [<Fact>]
    let ``Clarification parser reports duplicate question and decision ids`` () =
        let broken =
            clarificationText
                .Replace("- CQ-001 [AMB:AMB-001]", "- CQ-001 [AMB:AMB-001]\n- CQ-001 [AMB:AMB-001]")
                .Replace("- DEC-001 [CQ-001]", "- DEC-001 [CQ-001]\n- DEC-001 [CQ-001]")

        match parseClarificationFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            let ids = facts.Diagnostics |> List.map _.Id
            Assert.Contains("duplicateIdentifier", ids)

    [<Fact>]
    let ``Clarification question state does not misread (unanswered) as answered`` () =
        // Regression (#67): substring `Contains("answered")` misclassified an
        // "(unanswered)" question as answered. Word-boundary matching keeps it open.
        let text =
            clarificationText.Replace(
                "- CQ-001 [AMB:AMB-001] [FR-001] [US-001] [AC-001] blocking answered: Which artifact records decisions?",
                "- CQ-001 [AMB:AMB-001] [FR-001] [US-001] [AC-001] blocking (unanswered): Which artifact records decisions?"
            )

        match parseClarificationFacts (snapshot text) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            let question = facts.Questions |> List.exactlyOne
            Assert.Equal("open", question.State)

    [<Fact>]
    let ``Clarification question blocking treats nonblocking (no hyphen) as non-blocking`` () =
        let text = clarificationText.Replace("blocking answered:", "nonblocking answered:")

        match parseClarificationFacts (snapshot text) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            let question = facts.Questions |> List.exactlyOne
            Assert.False(question.Blocking)

    [<Fact>]
    let ``Clarification answer kind does not misread noted as a note`` () =
        // Regression (#67): `Contains("note")` matched "noted".
        let text =
            clarificationText.Replace(
                "- CQ-001 [AMB:AMB-001] decision: Clarification decisions live in clarifications.md.",
                "- CQ-001 [AMB:AMB-001] decision: The owner noted the decision and recorded it."
            )

        match parseClarificationFacts (snapshot text) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            let answer = facts.Answers |> List.exactlyOne
            Assert.Equal(DecisionAnswer, answer.Kind)

    /// Swap the single genuine `## Remaining Ambiguity` bullet for an arbitrary body.
    let private withRemainingAmbiguity (body: string) =
        clarificationText.Replace("- AMB-002 [CQ-002] blocking: Still needs an answer.", body)

    [<Theory>]
    // #105 Trap 1: a "no-outstanding" disclaimer that names resolved AMB/CQ ids is not itself
    // an unresolved item — it must contribute 0 blocking ambiguities, not one-per-id.
    [<InlineData("- None. AMB-001, AMB-002, AMB-003 resolved above.")>]
    [<InlineData("- No remaining ambiguities; AMB-001 resolved.")>]
    [<InlineData("None. AMB-001 resolved.")>]
    let ``Remaining-ambiguity disclaimer naming resolved ids counts as zero blocking`` (body: string) =
        match parseClarificationFacts (snapshot (withRemainingAmbiguity body)) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts -> Assert.Equal(0, facts.BlockingAmbiguityCount)

    [<Theory>]
    // The disciplined `none`/`No <noun>` distinction keeps genuine unresolved lines blocking:
    // an explicit ambiguity, and a `No decision yet …` line that names no disclaimer noun.
    [<InlineData("- AMB-001: The scoring rule is unclear.")>]
    [<InlineData("- No decision yet on AMB-001.")>]
    let ``Remaining-ambiguity genuine unresolved line still counts as blocking`` (body: string) =
        match parseClarificationFacts (snapshot (withRemainingAmbiguity body)) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts -> Assert.Equal(1, facts.BlockingAmbiguityCount)

    [<Fact>]
    let ``Clarification parser diagnoses unsupported schema versions`` () =
        let broken = clarificationText.Replace("schemaVersion: 1", "schemaVersion: 2")

        match parseClarificationFacts (snapshot broken) with
        | Ok _ -> failwith "Unsupported schema version should block parsing."
        | Error diagnostics ->
            Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "unsupportedSchemaVersion")
