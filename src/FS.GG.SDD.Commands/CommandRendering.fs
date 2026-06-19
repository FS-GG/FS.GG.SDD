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

        builder.AppendLine($"generatedViews: {List.length report.GeneratedViews}") |> ignore
        builder.AppendLine($"diagnostics: {List.length report.Diagnostics}") |> ignore

        match report.NextAction with
        | Some action -> builder.AppendLine($"nextAction: {action.ActionId}") |> ignore
        | None -> builder.AppendLine("nextAction: none") |> ignore

        builder.ToString()
