namespace FS.GG.SDD.Commands.Tests

open System.Text.Json
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

// Joins ProcessGlobalEnv (FS.GG.SDD#538): the `--input` CLI-boundary regression below spawns a
// PATH-resolved process (`runCliRaw`), so it must not run while a sibling mutates process-global
// PATH (feature 067 / FR-001).
[<Collection("ProcessGlobalEnv")>]
module SpecifyCommandTests =
    let workId = "005-specify-command"
    let title = "Specify Command"
    let charterPath = $"work/{workId}/charter.md"
    let specPath = $"work/{workId}/spec.md"
    let workModelPath = $"readiness/{workId}/work-model.json"

    let initializedCharteredProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        root

    let existingSpecification stage workIdValue =
        $"""---
schemaVersion: 1
workId: {workIdValue}
title: {title}
stage: {stage}
changeTier: tier1
status: specified
publicOrToolFacingImpact: true
---

# {title} Specification

Prose status: specified

## User Value
Existing user value remains.

## Scope
- SB-001: Existing scope remains.

## Non-Goals
- SB-002: Existing non-goal remains.

## User Stories
- US-001 (P1): Existing story remains.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Existing scenario remains.

## Functional Requirements
- FR-001: Existing requirement remains. (Stories: US-001; Acceptance: AC-001)

## Ambiguities
No material ambiguities recorded.

## Public Or Tool-Facing Impact
- Existing impact remains.

## Lifecycle Notes
- Existing lifecycle note remains.
"""

    [<Fact>]
    let ``specify creates authored specification with real filesystem evidence`` () =
        let root = initializedCharteredProject ()

        let report = TestSupport.runSpecify root workId title
        let spec = TestSupport.readRelative root specPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("stage: specify", spec)
        Assert.Contains("## User Value", spec)
        Assert.Contains("## Functional Requirements", spec)
        Assert.Contains("- FR-001:", spec)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = specPath && change.Operation = ArtifactOperation.Create
        )

        Assert.Contains(
            report.GeneratedViews,
            fun view -> view.Path = workModelPath && view.Currency = GeneratedViewCurrency.Missing
        )

        Assert.Equal(Some Clarify, report.NextAction |> Option.bind _.Command)
        Assert.Contains(specPath, report.NextAction.Value.RequiredArtifacts)

        Assert.Equal(
            Some "FR-001",
            report.Specification
            |> Option.bind (fun summary -> summary.RequirementIds |> List.tryHead)
        )

    [<Fact>]
    let ``specify creation does not require Governance files`` () =
        let root = initializedCharteredProject ()

        let report = TestSupport.runSpecify root workId title

        Assert.NotEqual(CommandOutcome.Blocked, report.Outcome)
        Assert.False(TestSupport.existsRelative root ".fsgg/policy.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/capabilities.yml")
        Assert.False(TestSupport.existsRelative root ".fsgg/tooling.yml")

        Assert.Contains(
            report.GovernanceCompatibility,
            fun fact -> fact.Path = ".fsgg/policy.yml" && fact.State = "notEvaluated"
        )

    [<Fact>]
    let ``specify rerun preserves authored content`` () =
        let root = initializedCharteredProject ()
        TestSupport.runSpecify root workId title |> ignore

        let authored =
            TestSupport.readRelative root specPath + "\nUser-authored prose stays here.\n"

        TestSupport.writeRelative root specPath authored

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.Equal(authored, TestSupport.readRelative root specPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = specPath && change.Operation = ArtifactOperation.NoChange
        )

    // §3.2 (FR-002, SC-002): an edited-but-section-complete spec re-run is never a bare,
    // ambiguous NoChange — the report carries the deterministic statement that specify
    // promotes only the first draft and that spec.md is read live by downstream stages.
    [<Fact>]
    let ``specify edited rerun reports live-read statement and is never bare NoChange`` () =
        let root = initializedCharteredProject ()
        TestSupport.runSpecify root workId title |> ignore

        let edited =
            (TestSupport.readRelative root specPath)
                .Replace("Prose status: specified", "Prose status: specified\nAuthor added a clarifying sentence.")

        TestSupport.writeRelative root specPath edited

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.NoChange, report.Outcome)
        Assert.True(report.NextAction.IsSome)
        let action = report.NextAction.Value
        Assert.Equal("specify.next.clarify", action.ActionId)
        Assert.Equal(Some Clarify, action.Command)
        Assert.Contains("read live", action.Reason)

    [<Fact>]
    let ``specify safely appends missing standard sections`` () =
        let root = initializedCharteredProject ()

        let partial =
            (existingSpecification "specify" workId)
                .Replace("## Ambiguities\nNo material ambiguities recorded.\n\n", "")

        TestSupport.writeRelative root specPath partial

        let report = TestSupport.runSpecify root workId title
        let after = TestSupport.readRelative root specPath

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.Contains("Existing user value remains.", after)
        Assert.Contains("## Ambiguities", after)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = specPath && change.Operation = ArtifactOperation.Update
        )

    [<Fact>]
    let ``specify identity mismatch blocks before authored write`` () =
        let root = initializedCharteredProject ()
        let original = existingSpecification "specify" "999-other-work"
        TestSupport.writeRelative root specPath original

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "specificationIdentityMismatch")
        Assert.Equal(original, TestSupport.readRelative root specPath)

    [<Fact>]
    let ``specify missing charter blocks before specification write`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingCharterPrerequisite")
        Assert.False(TestSupport.existsRelative root specPath)

    [<Fact>]
    let ``specify missing intent blocks new specification`` () =
        let root = initializedCharteredProject ()

        let request =
            { TestSupport.specifyRequest root workId title with
                InputText = None }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationIntent")
        // FR-009: the correction shows the exact labeled-line --input form the parser accepts.
        Assert.Contains(
            report.Diagnostics,
            fun diagnostic ->
                diagnostic.Id = "missingSpecificationIntent"
                && diagnostic.Correction.Contains("value:")
                && diagnostic.Correction.Contains("scope:")
                && diagnostic.Correction.Contains("requirement:")
        )

        Assert.False(TestSupport.existsRelative root specPath)

    [<Fact>]
    let ``specify malformed and duplicate ids block before authored write`` () =
        let root = initializedCharteredProject ()

        let original =
            (existingSpecification "specify" workId)
                .Replace(
                    "- US-001 (P1): Existing story remains.",
                    "- US-001 (P1): Existing story remains.\n- US-001 (P1): Duplicate story."
                )
                .Replace("Stories: US-001; Acceptance: AC-001", "Stories: US-999; Acceptance: AC-999")

        TestSupport.writeRelative root specPath original

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "duplicateSpecificationId")
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "unknownSpecificationReference")
        Assert.Equal(original, TestSupport.readRelative root specPath)

    [<Fact>]
    let ``specify dry run reports proposed changes without mutation`` () =
        let root = initializedCharteredProject ()

        let request =
            { TestSupport.specifyRequest root workId title with
                DryRun = true }

        let report = TestSupport.runRequest request

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.False(TestSupport.existsRelative root specPath)

        Assert.Contains(
            report.ChangedArtifacts,
            fun change -> change.Path = specPath && change.SafeWriteDecision = "dryRunOnly"
        )

    [<Fact>]
    let ``specify refreshes generated work model when source data is valid`` () =
        let root = initializedCharteredProject ()
        TestSupport.writeValidTasksAndEvidence root

        let report = TestSupport.runSpecify root workId title

        Assert.Equal(CommandOutcome.Succeeded, report.Outcome)
        Assert.True(TestSupport.existsRelative root workModelPath)

        Assert.Contains(
            report.GeneratedViews,
            fun view ->
                view.Path = workModelPath
                && view.Currency = GeneratedViewCurrency.Current
                && view.Sources |> List.exists (fun source -> source.Path = specPath)
        )

    [<Fact>]
    let ``specify deterministic JSON is byte stable`` () =
        let root = initializedCharteredProject ()

        let request =
            { TestSupport.specifyRequest root workId title with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"specify\"", first)
        Assert.Contains("\"specification\"", first)
        Assert.DoesNotContain(root, first)

    // ---------------------------------------------------------------------------------------
    // Feature 089 §WD7 (FS-GG/FS.GG.SDD#174): the seeded story and acceptance scenario.
    //
    // Before 089, an invocation supplying no story seeded two sentences about the SDD process
    // ("As a maintainer, I can specify X after chartering the work item." / "Given a chartered
    // work item, when specify runs with intent, then spec.md is created with stable ids."), which
    // the author deleted wholesale. They now read as the feature, derived from the author's own
    // `value:` fact. The ids and every cross-reference are deliberately unchanged.
    // ---------------------------------------------------------------------------------------

    let private specifyWithIntentAndTitle intent specifyTitle root =
        let request =
            { TestSupport.specifyRequest root workId title with
                Title = Some specifyTitle
                InputText = Some intent }

        TestSupport.runRequest request |> ignore
        TestSupport.readRelative root specPath

    let private specifyWithIntent intent root =
        specifyWithIntentAndTitle intent title root

    /// SC-007. The prohibition is on the *seed's own* boilerplate, not on author-supplied text the
    /// seed interpolates: a work item legitimately titled "Specify Command" puts "specify" in its
    /// own acceptance scenario, and a user value may say anything. So the process-vocabulary check
    /// runs against a neutral title and a neutral user value; the old meta-seed phrases are banned
    /// unconditionally.
    let private seedProcessVocabulary =
        [ "charter"; "specify"; "spec.md"; "stable ids" ]

    let private oldMetaSeedPhrases =
        [ "after chartering the work item"; "when specify runs with intent" ]

    let private storyLine (spec: string) =
        spec.Split('\n') |> Array.find (fun line -> line.StartsWith("- US-001"))

    let private acceptanceLine (spec: string) =
        spec.Split('\n') |> Array.find (fun line -> line.StartsWith("- AC-001"))

    [<Fact>]
    let ``specify seeds a feature-shaped story derived from the user value`` () =
        let root = initializedCharteredProject ()

        let spec =
            specifyWithIntent
                "value: Let a player keep a highlight of their match\nscope: one chartered work item\nrequirement: the export plays back in a standard media player"
                root

        let story = storyLine spec

        // FR-001: the `As a <user>, I can <capability>` shape, capability from the user value.
        Assert.Equal("- US-001 (P1): As a user, I can let a player keep a highlight of their match.", story)

        // FR-002: the old meta seed is gone, unconditionally.
        for phrase in oldMetaSeedPhrases do
            Assert.DoesNotContain(phrase, spec.ToLowerInvariant())

    [<Fact>]
    let ``specify seed carries no SDD process vocabulary of its own`` () =
        let root = initializedCharteredProject ()

        // Neutral title and neutral user value: anything left is the seed's own phrasing (SC-007).
        let spec =
            specifyWithIntentAndTitle
                "value: Let a player keep a highlight of their match\nscope: one work item\nrequirement: the export plays back"
                "Highlight Export"
                root

        let seeded = (storyLine spec + " " + acceptanceLine spec).ToLowerInvariant()

        for term in seedProcessVocabulary do
            Assert.DoesNotContain(term, seeded)

    [<Fact>]
    let ``specify seeds a feature-shaped acceptance scenario carrying its references`` () =
        let root = initializedCharteredProject ()

        let spec =
            specifyWithIntent
                "value: Let a player keep a highlight of their match\nscope: one chartered work item\nrequirement: the export plays back in a standard media player"
                root

        // FR-003/FR-004: Given/When/Then about the feature, still tagged [US-001] [FR-001].
        // The title interpolates verbatim — here the fixture's own "Specify Command".
        Assert.Equal(
            "- AC-001 [US-001] [FR-001]: Given Specify Command is available, when the user exercises it, then they can let a player keep a highlight of their match.",
            acceptanceLine spec
        )

    [<Fact>]
    let ``specify seed preserves the ids and cross-references the lifecycle depends on`` () =
        let root = initializedCharteredProject ()
        let spec = specifyWithIntent TestSupport.specifyIntent root

        // FR-004: `checklist` coverage and the plan/tasks back-references key off exactly these.
        Assert.StartsWith("- US-001 (P1): ", storyLine spec)
        Assert.StartsWith("- AC-001 [US-001] [FR-001]: ", acceptanceLine spec)
        Assert.Contains("(Stories: US-001; Acceptance: AC-001)", spec)

    [<Fact>]
    let ``specify uses the author story and acceptance verbatim when supplied`` () =
        let root = initializedCharteredProject ()

        let spec =
            specifyWithIntent
                (TestSupport.specifyIntent
                 + "\nstory: As an operator, I can roll back a bad deploy\nacceptance: Given a bad deploy, when I roll back, then the prior version serves traffic")
                root

        // FR-005: no seed substituted.
        Assert.Equal("- US-001 (P1): As an operator, I can roll back a bad deploy", storyLine spec)

        Assert.Equal(
            "- AC-001 [US-001] [FR-001]: Given a bad deploy, when I roll back, then the prior version serves traffic",
            acceptanceLine spec
        )

    [<Fact>]
    let ``specify seed neutralizes id-shaped tokens in author text`` () =
        let root = initializedCharteredProject ()

        let spec =
            specifyWithIntent
                "value: Supersede FR-002 with a shareable export\nscope: one chartered work item\nrequirement: the export plays back"
                root

        let story = storyLine spec

        // FR-017: the author's "FR-002" must not become a cross-reference inside the US-001 line.
        Assert.Contains("FR 002", story)
        Assert.DoesNotContain("FR-002", story)

    [<Fact>]
    let ``specify seed leaves an acronym-initial user value uncapitalized`` () =
        let root = initializedCharteredProject ()

        let spec =
            specifyWithIntent
                "value: MP4 export keeps a highlight.\nscope: one chartered work item\nrequirement: the export plays back"
                root

        // S3: only an ordinary Capitalized word is decapitalized. S4: the trailing period is not doubled.
        Assert.Equal("- US-001 (P1): As a user, I can MP4 export keeps a highlight.", storyLine spec)

    [<Fact>]
    let ``specify is deterministic across repeated runs`` () =
        let root = initializedCharteredProject ()
        let first = specifyWithIntent TestSupport.specifyIntent root
        let second = specifyWithIntent TestSupport.specifyIntent root

        // FR-015 / SC-010.
        Assert.Equal(first, second)

    /// Every id scanner in the artifact layer matches case-INSENSITIVELY. A case-sensitive
    /// `neutralizeIds` let a lowercase `amb-001` in the author's user value survive into the seeded
    /// US-001/AC-001 lines, where the specification parser counted it as a real ambiguity reference.
    /// (The counter that exposed this, `unresolvedAmbiguityCount`, was itself removed in feature 093
    /// — it never read clarifications.md — but the neutralization it caught is still load-bearing.)
    [<Fact>]
    let ``specify seed neutralizes lowercase id-shaped tokens too`` () =
        let root = initializedCharteredProject ()

        let spec =
            specifyWithIntent
                "value: Let a reviewer close amb-001 tickets\nscope: one chartered work item\nrequirement: the export plays back"
                root

        let story = storyLine spec
        let acceptance = acceptanceLine spec

        Assert.Contains("amb 001", story)
        Assert.DoesNotContain("amb-001", story)
        Assert.DoesNotContain("amb-001", acceptance)

    /// Neutralization must run BEFORE decapitalization: decapitalizing first turns `Amb-001` into
    /// `amb-001`, which only a case-insensitive rewrite would still catch.
    [<Fact>]
    let ``specify seed neutralizes a capitalized id-shaped token at the start of the user value`` () =
        let root = initializedCharteredProject ()

        let spec =
            specifyWithIntent
                "value: Amb-001 must be resolved before export\nscope: one chartered work item\nrequirement: the export plays back"
                root

        let story = storyLine spec

        Assert.DoesNotContain("Amb-001", story)
        Assert.DoesNotContain("amb-001", story)
        Assert.Contains("amb 001", story)

    /// FS.GG.SDD#538: `--input` is repeatable and newline-joined, so the intuitive
    /// one-flag-per-labeled-fact form (`--input "value: …" --input "scope: …" --input
    /// "requirement: …"`) composes instead of silently keeping one occurrence and dropping the
    /// rest — which blocked on "missing required facts: scope, measurable requirement". Driven
    /// through the REAL host (`runCliRaw`), because the join lives in the CLI argv→request mapping
    /// (`Program.fs`), not the in-process request builder the other specify tests use.
    [<Fact; Trait("tier", "slow")>]
    let ``specify CLI newline-joins repeated --input flags into one intent`` () =
        let root = initializedCharteredProject ()

        let exitCode, stdout, _ =
            TestSupport.runCliRaw
                30000
                [ "specify"
                  "--root"
                  root
                  "--work"
                  workId
                  "--title"
                  title
                  "--input"
                  "value: create a native specify command"
                  "--input"
                  "scope: one chartered work item"
                  "--input"
                  "requirement: create a specification artifact with stable ids" ]

        // All three labeled facts were seen: the run succeeded rather than blocking on missing
        // facts, and the spec was authored. (The exit code, and the report on stdout — the
        // FS.GG.SDD#535 automation contract — agree.)
        Assert.Equal(0, exitCode)
        use document = JsonDocument.Parse stdout
        Assert.Equal("succeeded", document.RootElement.GetProperty("outcome").GetString())
        Assert.True(TestSupport.existsRelative root specPath)
