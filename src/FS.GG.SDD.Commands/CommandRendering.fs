namespace FS.GG.SDD.Commands

open System.Text
open FS.GG.SDD.Commands.CommandTypes

module CommandRendering =
    let renderText report =
        let builder = StringBuilder()
        builder.AppendLine($"command: {commandName report.Command}") |> ignore
        builder.AppendLine($"outcome: {outcomeValue report.Outcome}") |> ignore
        builder.AppendLine($"changedArtifacts: {List.length report.ChangedArtifacts}") |> ignore

        match report.Specification with
        | Some specification ->
            builder.AppendLine($"specificationRequirements: {List.length specification.RequirementIds}") |> ignore
            builder.AppendLine($"specificationStories: {List.length specification.StoryIds}") |> ignore
            builder.AppendLine($"specificationAcceptanceScenarios: {List.length specification.AcceptanceScenarioIds}") |> ignore
            builder.AppendLine($"unresolvedAmbiguities: {specification.UnresolvedAmbiguityCount}") |> ignore
        | None -> ()

        match report.Clarification with
        | Some clarification ->
            builder.AppendLine($"clarificationQuestions: {List.length clarification.QuestionIds}") |> ignore
            builder.AppendLine($"clarificationDecisions: {List.length clarification.DecisionIds}") |> ignore
            builder.AppendLine($"acceptedDeferrals: {List.length clarification.AcceptedDeferralIds}") |> ignore
            builder.AppendLine($"remainingAmbiguities: {clarification.RemainingAmbiguityCount}") |> ignore
            builder.AppendLine($"blockingAmbiguities: {clarification.BlockingAmbiguityCount}") |> ignore
        | None -> ()

        match report.Checklist with
        | Some checklist ->
            builder.AppendLine($"checklistItems: {List.length checklist.ItemIds}") |> ignore
            builder.AppendLine($"checklistResults: {List.length checklist.ResultIds}") |> ignore
            builder.AppendLine($"checklistPassed: {checklist.PassedCount}") |> ignore
            builder.AppendLine($"checklistFailedBlocking: {checklist.FailedBlockingCount}") |> ignore
            builder.AppendLine($"checklistAcceptedDeferrals: {checklist.AcceptedDeferralCount}") |> ignore
            builder.AppendLine($"checklistStaleResults: {checklist.StaleResultCount}") |> ignore
            builder.AppendLine($"checklistAdvisory: {checklist.AdvisoryCount}") |> ignore
        | None -> ()

        match report.Plan with
        | Some plan ->
            builder.AppendLine($"planDecisions: {List.length plan.DecisionIds}") |> ignore
            builder.AppendLine($"planContractReferences: {List.length plan.ContractReferenceIds}") |> ignore
            builder.AppendLine($"planVerificationObligations: {List.length plan.VerificationObligationIds}") |> ignore
            builder.AppendLine($"planAcceptedDeferrals: {plan.AcceptedDeferralCount}") |> ignore
            builder.AppendLine($"planStaleDecisions: {plan.StaleDecisionCount}") |> ignore
            builder.AppendLine($"planBlockingFindings: {plan.BlockingFindingCount}") |> ignore
            builder.AppendLine($"planAdvisory: {plan.AdvisoryCount}") |> ignore
        | None -> ()

        builder.AppendLine($"generatedViews: {List.length report.GeneratedViews}") |> ignore
        builder.AppendLine($"diagnostics: {List.length report.Diagnostics}") |> ignore

        match report.NextAction with
        | Some action -> builder.AppendLine($"nextAction: {action.ActionId}") |> ignore
        | None -> builder.AppendLine("nextAction: none") |> ignore

        builder.ToString()
