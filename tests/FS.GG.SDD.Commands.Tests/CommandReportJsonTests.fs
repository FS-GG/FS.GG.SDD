namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

module CommandReportJsonTests =
    let dryRunReport () =
        let root =
            Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "lifecycle-commands", "deterministic-report")

        let request =
            { TestSupport.request Init root with
                DryRun = true }

        let model, effects = init request

        interpretAll root true effects
        |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model
        |> fun state -> update BuildReport state |> fst
        |> fun state -> state.Report.Value

    // Guards hole #1 of FS-GG/FS.GG.SDD#198: `reportVersion` is a hand-maintained literal, and
    // nothing forced it to move when feature 093 (#164) removed `specification.unresolvedAmbiguityCount`
    // — so a `0.8.0`- and a `0.9.0`-shaped report both claimed `1.3.0`. This pin reddens whenever the
    // literal changes: any change to the report's shape (adding/removing a block or field) must bump
    // `reportVersion` per `docs/release/versioning-policy.md` ("Change class to bump rule") — additive
    // ⇒ minor, removal/retype ⇒ major — and then deliberately update this expected value.
    [<Fact>]
    let ``reportVersion is pinned to its current contract value`` () =
        let expected = "2.1.0"
        Assert.Equal(expected, dryRunReport().ReportVersion)
        Assert.Contains(sprintf "\"reportVersion\": \"%s\"" expected, dryRunReport () |> serializeReport)

    [<Fact>]
    let ``deterministic JSON excludes absolute project root`` () =
        let first = dryRunReport () |> serializeReport
        let second = dryRunReport () |> serializeReport

        Assert.Equal(first, second)
        Assert.Contains("\"projectRoot\": \".\"", first)
        Assert.DoesNotContain(TestSupport.repoRoot, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``charter deterministic JSON is byte stable`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        let request =
            { TestSupport.charterRequest root "004-charter-command" "Charter Command" with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"charter\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``specify deterministic JSON includes specification object`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "005-specify-command" "Specify Command" |> ignore

        let request =
            { TestSupport.specifyRequest root "005-specify-command" "Specify Command" with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"specify\"", first)
        Assert.Contains("\"specification\"", first)
        Assert.Contains("\"requirementIds\"", first)
        // Feature 093 / FS.GG.SDD#164. Safe to remove: `ReleaseContract.fs`'s jsonInventory freezes
        // only the TOP-LEVEL report keys (`specification` among them), never the counters nested
        // inside, and no Governance-boundary artifact carries an ambiguity count.
        Assert.DoesNotContain("unresolvedAmbiguityCount", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``clarify deterministic JSON includes clarification object`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "006-clarify-command" "Clarify Command" |> ignore

        TestSupport.runRequest
            { TestSupport.specifyRequest root "006-clarify-command" "Clarify Command" with
                InputText = Some TestSupport.specifyIntentWithAmbiguity }
        |> ignore

        let request =
            { TestSupport.clarifyRequest root "006-clarify-command" "Clarify Command" with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"clarify\"", first)
        Assert.Contains("\"clarification\"", first)
        Assert.Contains("\"questionIds\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``checklist deterministic JSON includes checklist object`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root

        TestSupport.runCharter root "007-checklist-command" "Checklist Command"
        |> ignore

        TestSupport.runSpecify root "007-checklist-command" "Checklist Command"
        |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root "007-checklist-command" "Checklist Command" with
                InputText = None }
        |> ignore

        let request =
            { TestSupport.checklistRequest root "007-checklist-command" "Checklist Command" with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"checklist\"", first)
        Assert.Contains("\"checklist\"", first)
        Assert.Contains("\"itemIds\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``plan deterministic JSON includes plan object`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root "008-plan-command" "Plan Command" |> ignore
        TestSupport.runSpecify root "008-plan-command" "Plan Command" |> ignore

        TestSupport.runRequest
            { TestSupport.clarifyRequest root "008-plan-command" "Plan Command" with
                InputText = None }
        |> ignore

        TestSupport.runChecklist root "008-plan-command" "Plan Command" |> ignore

        let request =
            { TestSupport.planRequest root "008-plan-command" "Plan Command" with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"plan\"", first)
        Assert.Contains("\"decisionIds\"", first)
        Assert.Contains("\"contractReferenceIds\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)

    [<Fact>]
    let ``tasks deterministic JSON includes task graph object`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializePlanReadyProject root "009-tasks-command" "Tasks Command"

        let request =
            { TestSupport.tasksRequest root "009-tasks-command" "Tasks Command" with
                DryRun = true }

        let first = TestSupport.runRequest request |> serializeReport
        let second = TestSupport.runRequest request |> serializeReport
        let third = TestSupport.runRequest request |> serializeReport

        Assert.Equal(first, second)
        Assert.Equal(second, third)
        Assert.Contains("\"name\": \"tasks\"", first)
        Assert.Contains("\"tasks\"", first)
        Assert.Contains("\"taskIds\"", first)
        Assert.DoesNotContain(root, first)
        Assert.DoesNotContain("timestamp", first)
