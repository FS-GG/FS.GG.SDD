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

    // FS.GG.SDD#645: an Accepted Deferrals (or Decisions) line that CITES a prior milestone's
    // decision id in prose ("… the deferral inherited from M2 DEC-002 …") is a REFERENCE, not a
    // second declaration. Only the list-leading `- DEC-###:` token declares — mirroring the #541
    // fix for the specification stable-id scan — so the in-prose citation must not be parsed as a
    // duplicate decision or trigger a spurious "declared more than once".
    [<Fact>]
    let ``Clarification decision cited in prose is not a duplicate declaration`` () =
        let cited =
            clarificationText.Replace(
                "- DEC-002 [CQ-001] [AMB:AMB-001]: Defer checklist command details to the checklist feature.",
                "- DEC-002 [CQ-001] [AMB:AMB-001]: Defer checklist command details to the checklist feature.\n"
                + "- Context: this carries forward the deferral inherited from M2 DEC-002, now superseded here."
            )

        match parseClarificationFacts (snapshot cited) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Equal<string list>(
                [ "DEC-002" ],
                facts.AcceptedDeferrals |> List.map (fun decision -> decision.DecisionId.Value)
            )

            Assert.DoesNotContain("duplicateIdentifier", facts.Diagnostics |> List.map _.Id)

    // FS.GG.SDD#645: a genuine duplicate — two list-leading `- DEC-###:` declarations of the same
    // id in the Accepted Deferrals section — must STILL be reported. Anchoring the scan to the
    // declaration position narrows what counts as a declaration; it does not stop counting real ones.
    [<Fact>]
    let ``Clarification duplicate list-leading decision declarations still block`` () =
        let broken =
            clarificationText.Replace(
                "- DEC-002 [CQ-001] [AMB:AMB-001]: Defer checklist command details to the checklist feature.",
                "- DEC-002 [CQ-001] [AMB:AMB-001]: Defer checklist command details to the checklist feature.\n"
                + "- DEC-002 [CQ-001] [AMB:AMB-001]: A second, conflicting deferral of the same id."
            )

        match parseClarificationFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts -> Assert.Contains("duplicateIdentifier", facts.Diagnostics |> List.map _.Id)

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

    // ---------------------------------------------------------------------------------------
    // Feature 089 (FR-021, contract K9). The blocked-clarify skeleton renders a *generated*
    // explanation under `## Remaining Ambiguity`, and that prose is machine input: the classifier
    // scans it for `accepted deferral`/`defer` and `non-blocking`. The first implementation wrote
    // the obviously-helpful "Unanswered — provide a concrete decision, an accepted deferral, or an
    // explicit still-open note.", which classified as an ACCEPTED DEFERRAL — so the skeleton parsed
    // with zero blocking ambiguities and `checklist` passed with every ambiguity unanswered.
    //
    // Pin both directions: the shipped sentence blocks, and the tempting one does not.
    // ---------------------------------------------------------------------------------------

    [<Fact>]
    let ``Seeded skeleton remaining-ambiguity line counts as blocking`` () =
        let seeded =
            "- AMB-001 [CQ-001] blocking: Unanswered. Resolve source ambiguity AMB-001 before checklist."

        match parseClarificationFacts (snapshot (withRemainingAmbiguity seeded)) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts -> Assert.Equal(1, facts.BlockingAmbiguityCount)

    [<Theory>]
    // Regression guard: any generated explanation that NAMES a resolution stops blocking. These are
    // the shapes 089 must never reintroduce into the seeded skeleton.
    [<InlineData("- AMB-001 [CQ-001] blocking: Unanswered — provide a concrete decision, an accepted deferral, or an explicit still-open note.")>]
    [<InlineData("- AMB-001 [CQ-001] blocking: Unanswered; record a deferred decision if needed.")>]
    [<InlineData("- AMB-001 [CQ-001] blocking: Unanswered, or mark it non-blocking.")>]
    let ``Remaining-ambiguity line naming a resolution stops blocking`` (body: string) =
        match parseClarificationFacts (snapshot (withRemainingAmbiguity body)) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts -> Assert.Equal(0, facts.BlockingAmbiguityCount)

    // ---------------------------------------------------------------------------------------------
    // Feature 093 / FS.GG.SDD#164 (FS.GG.Audio feedback §3.7). A `DEC-###` line naming several refs
    // had all but the first silently dropped. The reported symptom was one bug; it was four, on three
    // paths. These pin two of them at the artifact layer.
    // ---------------------------------------------------------------------------------------------

    /// A Remaining Ambiguity line has ONE subject — its first `AMB-###`, the anchor. Other ids in the
    /// prose are mentions, not subjects. Feature 093 briefly widened this to "every id the line names",
    /// which looked like the same class of drop as the decision refs but is not: `remainingLineAnchor`
    /// retires a line by that first id, so a mention must never be reported as an unresolved blocker
    /// (`AMB-001 blocked on the AMB-002 decision` would name the already-answered AMB-002 as blocking),
    /// and must never retire the line when it is answered. The `tryHead` is the contract, not a bug.
    [<Fact>]
    let ``a remaining-ambiguity line is anchored on its first ambiguity, not every id it mentions`` () =
        let body = "- AMB-002 [CQ-002] blocking: still blocked on the AMB-004 decision."

        match parseClarificationFacts (snapshot (withRemainingAmbiguity body)) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            let remaining = Assert.Single facts.RemainingAmbiguity

            Assert.Equal(Some "AMB-002", remaining.AmbiguityId |> Option.map _.Value)
            Assert.Equal(1, facts.RemainingAmbiguity.Length)
            Assert.Equal(1, facts.BlockingAmbiguityCount)
