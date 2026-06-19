namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module IdentifierTests =
    [<Fact>]
    let ``WorkId validates lowercase feature ids`` () =
        match Identifiers.createWorkId "001-sdd-artifact-model" with
        | Ok workId -> Assert.Equal("001-sdd-artifact-model", Identifiers.workIdValue workId)
        | Error message -> failwith message

        Assert.True(Identifiers.createWorkId "Bad Id" |> Result.isError)

    [<Fact>]
    let ``LifecycleStage accepts standard SDD stages only`` () =
        let stages = Identifiers.allStages () |> List.map Identifiers.stageValue
        Assert.Contains("specify", stages)
        Assert.Contains("verify", stages)
        Assert.True(Identifiers.parseStage "unknown" |> Result.isError)

    [<Fact>]
    let ``Scoped lifecycle ids are stable and case-insensitive`` () =
        let requirement = Identifiers.createRequirementId "fr-001" |> Result.defaultWith failwith
        let decision = Identifiers.createDecisionId "dec-001" |> Result.defaultWith failwith
        let task = Identifiers.createTaskId "t001" |> Result.defaultWith failwith
        let evidence = Identifiers.createEvidenceId "ev001" |> Result.defaultWith failwith

        Assert.Equal("FR-001", Identifiers.requirementIdValue requirement)
        Assert.Equal("DEC-001", Identifiers.decisionIdValue decision)
        Assert.Equal("T001", Identifiers.taskIdValue task)
        Assert.Equal("EV001", Identifiers.evidenceIdValue evidence)
