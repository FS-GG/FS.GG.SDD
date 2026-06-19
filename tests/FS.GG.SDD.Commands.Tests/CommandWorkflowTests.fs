namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

module CommandWorkflowTests =
    [<Fact>]
    let ``init plans skeleton effects without touching filesystem`` () =
        let root = TestSupport.tempDirectory()
        let request = TestSupport.request Init root

        let model, effects = init request

        Assert.Empty(model.Diagnostics)
        Assert.Contains(effects, fun effect -> effect = CreateDirectory ".fsgg")
        Assert.Contains(effects, fun effect -> effect = CreateDirectory "work")
        Assert.Contains(effects, fun effect -> effect = CreateDirectory "readiness")
        Assert.Contains(effects, fun effect ->
            match effect with
            | WriteFile(".fsgg/project.yml", _, StructuredSource) -> true
            | _ -> false)

    [<Fact>]
    let ``unsupported lifecycle command builds blocked report without write effects`` () =
        let root = TestSupport.tempDirectory()
        let request = TestSupport.request Specify root

        let model, effects = init request
        let finalModel = update BuildReport model |> fst
        let report = finalModel.Report |> Option.defaultWith (fun () -> buildReport finalModel)

        Assert.Empty(effects)
        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingWorkId")

    [<Fact>]
    let ``interpreted effects feed report through EffectInterpreted messages`` () =
        let root = TestSupport.tempDirectory()
        let request = TestSupport.request Init root
        let model, effects = init request
        let first = { Effect = List.head effects; Succeeded = true; Snapshot = None; Diagnostic = None }

        let updated = update (EffectInterpreted first) model |> fst
        let final = update BuildReport updated |> fst

        Assert.Single(updated.InterpretedEffects) |> ignore
        Assert.True(final.Report.IsSome)

    [<Fact>]
    let ``charter plans project and work reads before write effects`` () =
        let root = TestSupport.tempDirectory()
        let request = { TestSupport.request Charter root with WorkId = Some "004-charter-command" }

        let model, effects = init request

        Assert.Empty(model.Diagnostics)
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/project.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/sdd.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/agents.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/004-charter-command/charter.md")
        Assert.DoesNotContain(effects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)

    [<Fact>]
    let ``charter requests duplicate candidate reads from interpreted directory snapshot`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.writeRelative root "work/duplicate/charter.md" """---
schemaVersion: 1
workId: 004-charter-command
title: Duplicate
stage: charter
changeTier: tier1
status: chartered
---

# Duplicate Charter
"""

        let request = { TestSupport.charterRequest root "004-charter-command" "Charter Command" with DryRun = true }
        let model, effects = init request

        let produced =
            interpretAll root true effects
            |> List.fold
                (fun (state, emitted) result ->
                    let next, nextEffects = update (EffectInterpreted result) state
                    next, emitted @ nextEffects)
                (model, [])
            |> snd

        Assert.Contains(produced, fun effect -> effect = ReadFile "work/duplicate/charter.md")

    [<Fact>]
    let ``charter blocking diagnostics prevent write effects`` () =
        let root = TestSupport.tempDirectory()
        let request = { TestSupport.request Charter root with WorkId = Some "004-charter-command" }
        let model, effects = init request

        let afterReads =
            interpretAll root false effects
            |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model

        let final = update BuildReport afterReads |> fst
        let report = final.Report |> Option.defaultWith (fun () -> buildReport final)

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "outsideProject")
        Assert.DoesNotContain(afterReads.PendingEffects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)

    [<Fact>]
    let ``specify plans project and work reads before write effects`` () =
        let root = TestSupport.tempDirectory()
        let request = { TestSupport.specifyRequest root "005-specify-command" "Specify Command" with DryRun = true }

        let model, effects = init request

        Assert.Empty(model.Diagnostics)
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/project.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/sdd.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/agents.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/005-specify-command/charter.md")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/005-specify-command/spec.md")
        Assert.Contains(effects, fun effect -> effect = ReadFile "readiness/005-specify-command/work-model.json")
        Assert.DoesNotContain(effects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)

    [<Fact>]
    let ``specify blocking diagnostics prevent write effects`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        let request = { TestSupport.specifyRequest root "005-specify-command" "Specify Command" with DryRun = true }
        let model, effects = init request

        let afterReads =
            interpretAll root true effects
            |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model

        let final = update BuildReport afterReads |> fst
        let report = final.Report |> Option.defaultWith (fun () -> buildReport final)

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingCharterPrerequisite")
        Assert.DoesNotContain(afterReads.PendingEffects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)

    [<Fact>]
    let ``clarify plans project specification clarification task evidence and generated-view reads before writes`` () =
        let root = TestSupport.tempDirectory()
        let request = { TestSupport.clarifyRequest root "006-clarify-command" "Clarify Command" with DryRun = true }

        let model, effects = init request

        Assert.Empty(model.Diagnostics)
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/project.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/006-clarify-command/spec.md")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/006-clarify-command/clarifications.md")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/006-clarify-command/tasks.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/006-clarify-command/evidence.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "readiness/006-clarify-command/work-model.json")
        Assert.DoesNotContain(effects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)

    [<Fact>]
    let ``clarify blocking diagnostics prevent write effects`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        let request = { TestSupport.clarifyRequest root "006-clarify-command" "Clarify Command" with DryRun = true }
        let model, effects = init request

        let afterReads =
            interpretAll root true effects
            |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model

        let final = update BuildReport afterReads |> fst
        let report = final.Report |> Option.defaultWith (fun () -> buildReport final)

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationPrerequisite")
        Assert.DoesNotContain(afterReads.PendingEffects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)

    [<Fact>]
    let ``checklist plans project specification clarification checklist task evidence and generated-view reads before writes`` () =
        let root = TestSupport.tempDirectory()
        let request = { TestSupport.checklistRequest root "007-checklist-command" "Checklist Command" with DryRun = true }

        let model, effects = init request

        Assert.Empty(model.Diagnostics)
        Assert.Contains(effects, fun effect -> effect = ReadFile ".fsgg/project.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/007-checklist-command/spec.md")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/007-checklist-command/clarifications.md")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/007-checklist-command/checklist.md")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/007-checklist-command/tasks.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "work/007-checklist-command/evidence.yml")
        Assert.Contains(effects, fun effect -> effect = ReadFile "readiness/007-checklist-command/work-model.json")
        Assert.DoesNotContain(effects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)

    [<Fact>]
    let ``checklist blocking diagnostics prevent write effects`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        let request = { TestSupport.checklistRequest root "007-checklist-command" "Checklist Command" with DryRun = true }
        let model, effects = init request

        let afterReads =
            interpretAll root true effects
            |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model

        let final = update BuildReport afterReads |> fst
        let report = final.Report |> Option.defaultWith (fun () -> buildReport final)

        Assert.Equal(CommandOutcome.Blocked, report.Outcome)
        Assert.Contains(report.Diagnostics, fun diagnostic -> diagnostic.Id = "missingSpecificationPrerequisite")
        Assert.DoesNotContain(afterReads.PendingEffects, fun effect ->
            match effect with
            | WriteFile _ -> true
            | _ -> false)
