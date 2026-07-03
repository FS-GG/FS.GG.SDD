namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module CharterCommandTests =
    let workId = "004-charter-command"
    let title = "Charter Command"
    let charterPath = $"work/{workId}/charter.md"

    let existingCharter =
        $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: charter
changeTier: tier1
status: chartered
---

# {title} Charter

## Identity
- Existing identity stays here.

## Principles
- User-authored principle stays here.

## Scope Boundaries
- User-authored boundary stays here.

## Policy Pointers
- User-authored policy note stays here.

## Lifecycle Notes
- User-authored lifecycle note stays here.
"""

    [<Fact>]
    let ``charter creates authored work charter with real filesystem evidence`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let report = TestSupport.runCharter root workId title
        let charter = TestSupport.readRelative root charterPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("schemaVersion: 1", charter)
        Assert.Contains($"workId: {workId}", charter)
        Assert.Contains("## Identity", charter)
        Assert.Contains("## Principles", charter)
        Assert.Contains("## Scope Boundaries", charter)
        Assert.Contains("## Policy Pointers", charter)
        Assert.Contains("## Lifecycle Notes", charter)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = charterPath && change.Operation = ArtifactOperation.Create
        )

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = $"readiness/{workId}/work-model.json"
                && view.Currency = GeneratedViewCurrency.Missing
        )

        Assert.Equal(Some Specify, report.NextAction |> Option.bind _.Command)
        Assert.Contains(charterPath, report.NextAction.Value.RequiredArtifacts)

    [<Fact>]
    let ``charter creation does not require Governance files`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let report = TestSupport.runCharter root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``charter rerun preserves authored content`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeRelative root charterPath existingCharter

        let report = TestSupport.runCharter root workId title
        let after = TestSupport.readRelative root charterPath

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(existingCharter, after)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = charterPath && change.Operation = ArtifactOperation.NoChange
        )

    [<Fact>]
    let ``charter safely appends missing standard sections without rewriting prose`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let partial =
            $"""---
schemaVersion: 1
workId: {workId}
title: {title}
stage: charter
changeTier: tier1
status: chartered
---

# {title} Charter

## Identity
- Custom identity remains untouched.
"""

        TestSupport.writeRelative root charterPath partial

        let report = TestSupport.runCharter root workId title
        let after = TestSupport.readRelative root charterPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("- Custom identity remains untouched.", after)
        Assert.Contains("## Principles", after)
        Assert.Contains("## Scope Boundaries", after)
        Assert.Contains("## Policy Pointers", after)
        Assert.Contains("## Lifecycle Notes", after)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = charterPath && change.Operation = ArtifactOperation.Update
        )

    [<Fact>]
    let ``charter identity mismatch blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let original =
            existingCharter.Replace($"workId: {workId}", "workId: 999-other-work")

        TestSupport.writeRelative root charterPath original

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "charterIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root charterPath)

    [<Fact>]
    let ``charter malformed front matter blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        let original = "# Missing front matter\n"
        TestSupport.writeRelative root charterPath original

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedCharterFrontMatter")
        Assert.Equal(original, TestSupport.readRelative root charterPath)

    [<Fact>]
    let ``charter unsafe overwrite marker blocks before authored write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        let original = existingCharter + "\n<!-- fsgg-sdd: unsafe-overwrite -->\n"
        TestSupport.writeRelative root charterPath original

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unsafeOverwrite")
        Assert.Equal(original, TestSupport.readRelative root charterPath)

    [<Fact>]
    let ``charter outside project reports actionable diagnostic and creates no artifact`` () =
        let root = TestSupport.tempDirectory ()

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "outsideProject")
        Assert.False(TestSupport.existsRelative root charterPath)

    [<Fact>]
    let ``charter malformed work id reports diagnostic without effects`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let request =
            { TestSupport.charterRequest root "INVALID WORK ID" title with
                WorkId = Some "INVALID WORK ID" }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedWorkId")

    [<Fact>]
    let ``charter malformed project config reports diagnostic`` () =
        let root = TestSupport.tempDirectory ()
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        TestSupport.writeRelative root ".fsgg/project.yml" "schemaVersion: 1\nproject:\n  id: broken\n"
        TestSupport.writeRelative root ".fsgg/sdd.yml" "schemaVersion: 1\nlifecycle:\n  stages: [charter]\n"
        TestSupport.writeRelative root ".fsgg/agents.yml" "schemaVersion: 1\nagents: []\n"

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "malformedProjectConfig")

    [<Fact>]
    let ``charter duplicate logical work id blocks before selected charter changes`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.writeRelative root "work/other/charter.md" existingCharter

        let report = TestSupport.runCharter root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateWorkId")
        Assert.False(TestSupport.existsRelative root charterPath)
