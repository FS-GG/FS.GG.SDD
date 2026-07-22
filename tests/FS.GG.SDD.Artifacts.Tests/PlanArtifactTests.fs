namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module PlanArtifactTests =
    let planText =
        """---
schemaVersion: 1
workId: 008-plan-command
title: Plan Command
stage: plan
changeTier: tier1
status: planned
sourceSpec: work/008-plan-command/spec.md
sourceClarifications: work/008-plan-command/clarifications.md
sourceChecklist: work/008-plan-command/checklist.md
publicOrToolFacingImpact: true
---

# Plan Command Plan

Prose status: planned

## Source Snapshot
- spec: work/008-plan-command/spec.md sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa schemaVersion:1
- clarifications: work/008-plan-command/clarifications.md sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb schemaVersion:1
- checklist: work/008-plan-command/checklist.md sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc schemaVersion:1

## Plan Scope
- Work item 008-plan-command is planned.

## Plan Decisions
- PD-001 [FR-001] [AC-001] complete: Plan command creates technical plans.
- PD-002 [CR-002] acceptedDeferral: Accepted checklist deferral remains visible.
- PD-003 [PD-001] stale: Source facts changed after PD-001 was recorded.

## Contract Impact
- PC-001 [PD-001] command report: fsgg-sdd plan JSON is tool-facing.

## Verification Obligations
- VO-001 [PD-001] [PC-001] semanticTest: Run command tests and CLI smoke evidence.

## Migration Posture
- PM-001 [PC-001] diagnoseOnly: Plan schemaVersion 1 is accepted.

## Generated View Impact
- GV-001 [PD-001] workModel: readiness/008-plan-command/work-model.json refreshes from plan sources.

## Accepted Deferrals
- CR-002 acceptedDeferral: Deferral remains visible to tasks and evidence.

## Planning Findings
No blocking planning findings recorded.

## Advisory Notes
- Optional Governance pointers remain compatibility facts only.

## Lifecycle Notes
- Next lifecycle action: tasks.
"""

    let snapshot text =
        ({ Path = "work/008-plan-command/plan.md"
           Text = text }
        : FileSnapshot)

    [<Fact>]
    let ``Plan parser extracts front matter snapshots ids deferrals and stale counts`` () =
        match parsePlanFacts (snapshot planText) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            Assert.Equal("008-plan-command", facts.FrontMatter.WorkId.Value)
            Assert.Equal(Identifiers.LifecycleStage.Plan, facts.FrontMatter.Stage)
            Assert.Empty(facts.MissingStandardSections)

            Assert.Equal<string list>(
                [ "PD-001"; "PD-002"; "PD-003" ],
                facts.Decisions |> List.map (fun decision -> decision.DecisionId.Value)
            )

            Assert.Equal<string list>(
                [ "PC-001" ],
                facts.ContractReferences
                |> List.map (fun reference -> reference.ContractId.Value)
            )

            Assert.Equal<string list>(
                [ "VO-001" ],
                facts.VerificationObligations
                |> List.map (fun obligation -> obligation.ObligationId.Value)
            )

            Assert.Equal<string list>(
                [ "PM-001" ],
                facts.MigrationNotes |> List.map (fun note -> note.MigrationId.Value)
            )

            Assert.Equal<string list>(
                [ "GV-001" ],
                facts.GeneratedViewImpacts |> List.map (fun impact -> impact.ImpactId.Value)
            )

            Assert.Equal(3, facts.SourceSnapshots.Length)
            Assert.Equal(1, facts.AcceptedDeferrals.Length)
            Assert.Equal(1, facts.StaleDecisionCount)

    // FS.GG.SDD#648 (#541 family) — a plan decision's SOURCE references are the ids it carries in
    // `[...]` bracket tags, not every id its prose happens to name. A prior-milestone / inherited id
    // cited in a decision's PROSE ("… extending the SB-008 seam", "… inherited from M2 DEC-006") is a
    // citation, not a dangling plan reference — so it must NOT appear in the decision's SourceIds,
    // which is the set `tasks`/`analyze` resolve against.

    [<Fact>]
    let ``Plan decision source ids come from bracket tags, not prose citations`` () =
        // SB-008 and DEC-006 are named only in the decision's prose — neither is bracket-tagged, and
        // neither belongs to this work item's known set. Before the fix both were lifted into SourceIds
        // and blocked tasks/analyze with "Plan reference '…' does not resolve".
        let text =
            planText.Replace(
                "- PD-001 [FR-001] [AC-001] complete: Plan command creates technical plans.",
                "- PD-001 [FR-001] [AC-001] complete: Plan command creates technical plans, extending the SB-008 seam inherited from M2 DEC-006."
            )

        match parsePlanFacts (snapshot text) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            let decision =
                facts.Decisions
                |> List.find (fun decision -> decision.DecisionId.Value = "PD-001")

            // Bracket-tagged refs are kept; prose-only citations are not.
            Assert.Equal<string list>([ "FR-001"; "AC-001" ], decision.SourceIds)
            Assert.DoesNotContain("SB-008", decision.SourceIds)
            Assert.DoesNotContain("DEC-006", decision.SourceIds)

    // FS.GG.SDD#653 (#645/#648 prose-token family) — a decision's authored status is the `<status>:`
    // marker at the DECLARATION position (before the first colon, after the id and bracket tags), not
    // a word its free prose happens to use. A decision whose PROSE merely discusses staleness ("… so a
    // stale prior frame is never re-fired.") must NOT read as a `stale`-flagged decision — otherwise
    // `stalePlanDecision` fires and blocks `tasks` even with every `## Source Snapshot` digest current.
    // A genuinely marked `- PD-### … stale: …` decision still must (covered by StaleDecisionCount = 1
    // in the parser test above, whose PD-003 carries a real `stale:` marker).

    [<Fact>]
    let ``Decision prose mentioning stale is not read as a stale-flagged decision`` () =
        // PD-001 keeps its `complete:` marker; only its description now uses the word "stale", and even
        // an embedded prose colon precedes it. Before the fix the whole-line word scan flipped its
        // status to "stale" and lifted StaleDecisionCount, blocking tasks with digests current.
        let text =
            planText.Replace(
                "- PD-001 [FR-001] [AC-001] complete: Plan command creates technical plans.",
                "- PD-001 [FR-001] [AC-001] complete: Prior-frame guard: so a stale prior frame is never re-fired."
            )

        match parsePlanFacts (snapshot text) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            let decision =
                facts.Decisions
                |> List.find (fun decision -> decision.DecisionId.Value = "PD-001")

            Assert.Equal("complete", decision.Status)
            // PD-003 still carries a genuine `stale:` marker; the prose "stale" in PD-001 must not add
            // to the count.
            Assert.Equal(1, facts.StaleDecisionCount)

    // FS.GG.SDD#569 (feature 105) — the framework-API reference grammar (Phase 1).

    [<Fact>]
    let ``Plan parser extracts framework-API references from both grammars`` () =
        let text =
            planText
                .Replace(
                    "## Verification Obligations",
                    "- framework: FS.GG.UI.SkiaViewer@0.12.0#runAppWithAudioAndPersistence — the persistence host.\n\n## Verification Obligations"
                )
                .Replace(
                    "- CR-002 acceptedDeferral: Deferral remains visible to tasks and evidence.",
                    "- CR-002 acceptedDeferral: Deferral remains visible to tasks and evidence.\n- CR-003 blocked-on-framework: FS.GG.UI.SkiaViewer#missingSymbol — believed absent."
                )

        match parsePlanFacts (snapshot text) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.DoesNotContain(facts.Diagnostics, (fun diagnostic -> diagnostic.Id = "malformedFrameworkReference"))

            let uses =
                facts.FrameworkApiReferences
                |> List.filter (fun reference -> reference.Kind = FrameworkUse)

            let blocked =
                facts.FrameworkApiReferences
                |> List.filter (fun reference -> reference.Kind = FrameworkBlockedOn)

            Assert.Equal(1, uses.Length)
            Assert.Equal("FS.GG.UI.SkiaViewer", uses.Head.PackageId)
            Assert.Equal<string option>(Some "0.12.0", uses.Head.Version)
            Assert.Equal("runAppWithAudioAndPersistence", uses.Head.Symbol)

            Assert.Equal(1, blocked.Length)
            Assert.Equal("FS.GG.UI.SkiaViewer", blocked.Head.PackageId)
            Assert.Equal<string option>(None, blocked.Head.Version)
            Assert.Equal("missingSymbol", blocked.Head.Symbol)

    [<Fact>]
    let ``Plan parser diagnoses a malformed framework reference and drops it`` () =
        // No '#symbol' — the token is not the grammar, so it must NOT silently parse as "no reference".
        let text =
            planText.Replace(
                "## Verification Obligations",
                "- framework: FS.GG.UI.SkiaViewer.runAppWithAudioAndPersistence\n\n## Verification Obligations"
            )

        match parsePlanFacts (snapshot text) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Contains(facts.Diagnostics, (fun diagnostic -> diagnostic.Id = "malformedFrameworkReference"))
            Assert.Empty(facts.FrameworkApiReferences)

    [<Fact>]
    let ``Plan parser yields no framework references when none are cited`` () =
        match parsePlanFacts (snapshot planText) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Empty(facts.FrameworkApiReferences)
            Assert.DoesNotContain(facts.Diagnostics, (fun diagnostic -> diagnostic.Id = "malformedFrameworkReference"))

    [<Fact>]
    let ``Plan parser reports duplicate plan ids`` () =
        let broken =
            planText.Replace("- PD-001 [FR-001]", "- PD-001 [FR-001]\n- PD-001 [FR-001]")

        match parsePlanFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts -> Assert.Contains(facts.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateIdentifier")

    [<Fact>]
    let ``Plan parser diagnoses unsupported schema versions`` () =
        let broken = planText.Replace("schemaVersion: 1", "schemaVersion: 2")

        match parsePlanFacts (snapshot broken) with
        | Ok _ -> failwith "Unsupported schema version should block parsing."
        | Error diagnostics ->
            Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "unsupportedSchemaVersion")
