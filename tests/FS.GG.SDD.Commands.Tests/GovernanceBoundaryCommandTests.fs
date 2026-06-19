namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open Xunit

module GovernanceBoundaryCommandTests =
    [<Fact>]
    let ``init reports optional Governance compatibility without enforcement fields`` () =
        let root = TestSupport.tempDirectory()
        let request = { TestSupport.request Init root with DryRun = true }
        let model, effects = init request

        let report =
            interpretAll root true effects
            |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model
            |> fun state -> update BuildReport state |> fst
            |> fun state -> state.Report.Value

        let json = serializeReport report

        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml")
        Assert.DoesNotContain("\"route\"", json)
        Assert.DoesNotContain("\"profile\"", json)
        Assert.DoesNotContain("\"freshness\"", json)
        Assert.DoesNotContain("\"gate\"", json)
        Assert.DoesNotContain("\"protectedBranch\"", json)
