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

    [<Fact>]
    let ``charter reports optional Governance compatibility without enforcement fields`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        let request = { TestSupport.charterRequest root "004-charter-command" "Charter Command" with DryRun = true }

        let report = TestSupport.runRequest request
        let json = serializeReport report

        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml")
        Assert.DoesNotContain("\"route\"", json)
        Assert.DoesNotContain("\"profile\"", json)
        Assert.DoesNotContain("\"freshness\"", json)
        Assert.DoesNotContain("\"gate\"", json)
        Assert.DoesNotContain("\"audit\"", json)
        Assert.DoesNotContain("\"protectedBranch\"", json)

    [<Fact>]
    let ``specify reports optional Governance compatibility without enforcement fields`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root "005-specify-command" "Specify Command" |> ignore
        let request = { TestSupport.specifyRequest root "005-specify-command" "Specify Command" with DryRun = true }

        let report = TestSupport.runRequest request
        let json = serializeReport report

        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml")
        Assert.DoesNotContain("\"route\"", json)
        Assert.DoesNotContain("\"profile\"", json)
        Assert.DoesNotContain("\"freshness\"", json)
        Assert.DoesNotContain("\"gate\"", json)
        Assert.DoesNotContain("\"audit\"", json)
        Assert.DoesNotContain("\"protectedBranch\"", json)

    [<Fact>]
    let ``clarify reports optional Governance compatibility without enforcement fields`` () =
        let root = TestSupport.tempDirectory()
        TestSupport.initializeProject root
        TestSupport.runCharter root "006-clarify-command" "Clarify Command" |> ignore
        TestSupport.runRequest { TestSupport.specifyRequest root "006-clarify-command" "Clarify Command" with InputText = Some TestSupport.specifyIntentWithAmbiguity } |> ignore
        let request = { TestSupport.clarifyRequest root "006-clarify-command" "Clarify Command" with DryRun = true }

        let report = TestSupport.runRequest request
        let json = serializeReport report

        Assert.Contains(report.GovernanceCompatibility, fun fact -> fact.Path = ".fsgg/policy.yml")
        Assert.DoesNotContain("\"route\"", json)
        Assert.DoesNotContain("\"profile\"", json)
        Assert.DoesNotContain("\"freshness\"", json)
        Assert.DoesNotContain("\"gate\"", json)
        Assert.DoesNotContain("\"audit\"", json)
        Assert.DoesNotContain("\"protectedBranch\"", json)
