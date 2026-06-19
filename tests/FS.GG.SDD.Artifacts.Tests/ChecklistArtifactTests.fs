namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module ChecklistArtifactTests =
    let checklistText =
        """---
schemaVersion: 1
workId: 007-checklist-command
title: Checklist Command
stage: checklist
changeTier: tier1
status: checklistReady
sourceSpec: work/007-checklist-command/spec.md
sourceClarifications: work/007-checklist-command/clarifications.md
publicOrToolFacingImpact: true
---

# Checklist Command Checklist

Prose status: checklistReady

## Source Specification
- work/007-checklist-command/spec.md

## Source Clarifications
- work/007-checklist-command/clarifications.md

## Source Snapshot
- spec: work/007-checklist-command/spec.md sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa schemaVersion:1
- clarifications: work/007-checklist-command/clarifications.md sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb schemaVersion:1

## Checklist Items
- CHK-001 [FR-001] [AC-001] blocking: Requirement must be testable and linked to acceptance coverage.
- CHK-002 [DEC-001] advisory: Accepted deferral remains visible to planning.

## Review Results
- CR-001 [CHK:CHK-001] [FR-001] [AC-001] pass: Requirement is testable and has acceptance coverage.
- CR-002 [CHK:CHK-002] [DEC-001] stale: Source specification changed since this result was recorded.

## Accepted Deferrals
- CR-003 [CHK:CHK-002] [DEC-001] acceptedDeferral: Deferral is explicit and remains visible to plan.

## Blocking Findings
No blocking findings recorded.

## Advisory Notes
- Accepted deferral remains visible.

## Lifecycle Notes
- Next lifecycle action: plan.
"""

    let snapshot text =
        ({ Path = "work/007-checklist-command/checklist.md"
           Text = text }
        : LifecycleArtifacts.FileSnapshot)

    [<Fact>]
    let ``Checklist parser extracts front matter snapshots items results deferrals and stale counts`` () =
        match LifecycleArtifacts.parseChecklistFacts (snapshot checklistText) with
        | Error diagnostics -> failwith $"Unexpected diagnostics: {diagnostics}"
        | Ok facts ->
            Assert.Equal("007-checklist-command", facts.FrontMatter.WorkId.Value)
            Assert.Equal(Identifiers.LifecycleStage.Checklist, facts.FrontMatter.Stage)
            Assert.Empty(facts.MissingStandardSections)
            Assert.Equal<string list>([ "CHK-001"; "CHK-002" ], facts.Items |> List.map (fun item -> item.ItemId.Value))
            Assert.Equal<string list>([ "CR-001"; "CR-002"; "CR-003" ], facts.Results |> List.map (fun result -> result.ResultId.Value))
            Assert.Equal<string list>([ "CR-003" ], facts.AcceptedDeferrals |> List.map (fun result -> result.ResultId.Value))
            Assert.Equal(2, facts.SourceSnapshots.Length)
            Assert.Equal(1, facts.StaleResultCount)

    [<Fact>]
    let ``Checklist parser reports duplicate item and result ids`` () =
        let broken =
            checklistText
                .Replace("- CHK-001 [FR-001]", "- CHK-001 [FR-001]\n- CHK-001 [FR-001]")
                .Replace("- CR-001 [CHK:CHK-001]", "- CR-001 [CHK:CHK-001]\n- CR-001 [CHK:CHK-001]")

        match LifecycleArtifacts.parseChecklistFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            let ids = facts.Diagnostics |> List.map _.Id
            Assert.Contains("duplicateIdentifier", ids)

    [<Fact>]
    let ``Checklist parser diagnoses unsupported schema versions`` () =
        let broken = checklistText.Replace("schemaVersion: 1", "schemaVersion: 2")

        match LifecycleArtifacts.parseChecklistFacts (snapshot broken) with
        | Ok _ -> failwith "Unsupported schema version should block parsing."
        | Error diagnostics ->
            Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "unsupportedSchemaVersion")

    [<Fact>]
    let ``Checklist parser diagnoses results that reference unknown checklist items`` () =
        let broken = checklistText.Replace("[CHK:CHK-001]", "[CHK:CHK-999]")

        match LifecycleArtifacts.parseChecklistFacts (snapshot broken) with
        | Error diagnostics -> failwith $"Front matter should parse: {diagnostics}"
        | Ok facts ->
            Assert.Contains(facts.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownReference")
