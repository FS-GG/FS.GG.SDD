namespace FS.GG.SDD.Commands.Tests

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
