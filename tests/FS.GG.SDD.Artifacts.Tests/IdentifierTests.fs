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
        let requirement =
            Identifiers.createRequirementId "fr-001" |> Result.defaultWith failwith

        let question =
            Identifiers.createClarificationQuestionId "cq-001"
            |> Result.defaultWith failwith

        let decision = Identifiers.createDecisionId "dec-001" |> Result.defaultWith failwith
        let task = Identifiers.createTaskId "t001" |> Result.defaultWith failwith
        let evidence = Identifiers.createEvidenceId "ev001" |> Result.defaultWith failwith

        Assert.Equal("FR-001", Identifiers.requirementIdValue requirement)
        Assert.Equal("CQ-001", Identifiers.clarificationQuestionIdValue question)
        Assert.Equal("DEC-001", Identifiers.decisionIdValue decision)
        Assert.Equal("T001", Identifiers.taskIdValue task)
        Assert.Equal("EV001", Identifiers.evidenceIdValue evidence)
        Assert.True(Identifiers.createClarificationQuestionId "Q-1" |> Result.isError)

    [<Fact>]
    let ``Checklist ids are stable and case-insensitive`` () =
        let item =
            Identifiers.createChecklistItemId "chk-001" |> Result.defaultWith failwith

        let result =
            Identifiers.createChecklistResultId "cr-001" |> Result.defaultWith failwith

        Assert.Equal("CHK-001", Identifiers.checklistItemIdValue item)
        Assert.Equal("CR-001", Identifiers.checklistResultIdValue result)
        Assert.True(Identifiers.createChecklistItemId "CHECK-1" |> Result.isError)
        Assert.True(Identifiers.createChecklistResultId "R-1" |> Result.isError)

    [<Fact>]
    let ``Plan ids are stable and case-insensitive`` () =
        let decision =
            Identifiers.createPlanDecisionId "pd-001" |> Result.defaultWith failwith

        let contract =
            Identifiers.createPlanContractReferenceId "pc-001"
            |> Result.defaultWith failwith

        let obligation =
            Identifiers.createVerificationObligationId "vo-001"
            |> Result.defaultWith failwith

        let migration =
            Identifiers.createPlanMigrationNoteId "pm-001" |> Result.defaultWith failwith

        let generated =
            Identifiers.createGeneratedViewImpactId "gv-001" |> Result.defaultWith failwith

        Assert.Equal("PD-001", Identifiers.planDecisionIdValue decision)
        Assert.Equal("PC-001", Identifiers.planContractReferenceIdValue contract)
        Assert.Equal("VO-001", Identifiers.verificationObligationIdValue obligation)
        Assert.Equal("PM-001", Identifiers.planMigrationNoteIdValue migration)
        Assert.Equal("GV-001", Identifiers.generatedViewImpactIdValue generated)
        Assert.True(Identifiers.createPlanDecisionId "DEC-001" |> Result.isError)
        Assert.True(Identifiers.createPlanContractReferenceId "CONTRACT-1" |> Result.isError)
