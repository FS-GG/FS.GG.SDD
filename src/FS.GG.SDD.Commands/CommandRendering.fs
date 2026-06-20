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

        match report.Tasks with
        | Some tasks ->
            builder.AppendLine($"tasks: {List.length tasks.TaskIds}") |> ignore
            builder.AppendLine($"taskDependencies: {tasks.DependencyCount}") |> ignore
            builder.AppendLine($"taskRequiredSkills: {tasks.RequiredSkillCount}") |> ignore
            builder.AppendLine($"taskRequiredEvidence: {tasks.RequiredEvidenceCount}") |> ignore
            builder.AppendLine($"taskPending: {tasks.PendingCount}") |> ignore
            builder.AppendLine($"taskInProgress: {tasks.InProgressCount}") |> ignore
            builder.AppendLine($"taskDone: {tasks.DoneCount}") |> ignore
            builder.AppendLine($"taskSkipped: {tasks.SkippedCount}") |> ignore
            builder.AppendLine($"taskStale: {tasks.StaleCount}") |> ignore
            builder.AppendLine($"taskAcceptedDeferrals: {tasks.AcceptedDeferralCount}") |> ignore
            builder.AppendLine($"taskBlockingFindings: {tasks.BlockingFindingCount}") |> ignore
            builder.AppendLine($"taskAdvisory: {tasks.AdvisoryCount}") |> ignore
        | None -> ()

        match report.Analysis with
        | Some analysis ->
            builder.AppendLine($"workId: {analysis.WorkId}") |> ignore
            builder.AppendLine($"analysisPath: {analysis.AnalysisPath}") |> ignore
            builder.AppendLine($"analysisReadiness: {analysis.Readiness}") |> ignore
            builder.AppendLine($"analysisSources: {analysis.SourceCount}") |> ignore
            builder.AppendLine($"analysisRelationships: {analysis.SourceRelationshipCount}") |> ignore
            builder.AppendLine($"analysisReadyFindings: {analysis.ReadyFindingCount}") |> ignore
            builder.AppendLine($"analysisAdvisory: {analysis.AdvisoryCount}") |> ignore
            builder.AppendLine($"analysisWarnings: {analysis.WarningCount}") |> ignore
            builder.AppendLine($"analysisBlocking: {analysis.BlockingCount}") |> ignore
            builder.AppendLine($"analysisStaleSources: {analysis.StaleSourceCount}") |> ignore
            builder.AppendLine($"analysisMissingDispositions: {analysis.MissingDispositionCount}") |> ignore
            builder.AppendLine($"analysisGeneratedViewFindings: {analysis.GeneratedViewFindingCount}") |> ignore
        | None -> ()

        match report.Evidence with
        | Some evidence ->
            builder.AppendLine($"workId: {evidence.WorkId}") |> ignore
            builder.AppendLine($"evidencePath: {evidence.EvidencePath}") |> ignore
            builder.AppendLine($"evidenceReadiness: {evidence.Readiness}") |> ignore
            builder.AppendLine($"evidenceDeclarations: {evidence.DeclarationCount}") |> ignore
            builder.AppendLine($"evidenceObligations: {evidence.ObligationCount}") |> ignore
            builder.AppendLine($"evidenceSupported: {evidence.SupportedCount}") |> ignore
            builder.AppendLine($"evidenceDeferred: {evidence.DeferredCount}") |> ignore
            builder.AppendLine($"evidenceMissing: {evidence.MissingCount}") |> ignore
            builder.AppendLine($"evidenceStale: {evidence.StaleCount}") |> ignore
            builder.AppendLine($"evidenceSynthetic: {evidence.SyntheticCount}") |> ignore
            builder.AppendLine($"evidenceInvalid: {evidence.InvalidCount}") |> ignore
            builder.AppendLine($"evidenceBlocking: {evidence.BlockingCount}") |> ignore
        | None -> ()

        match report.Verification with
        | Some verification ->
            builder.AppendLine($"workId: {verification.WorkId}") |> ignore
            builder.AppendLine($"verifyPath: {verification.VerifyPath}") |> ignore
            builder.AppendLine($"verificationReadiness: {verification.Readiness}") |> ignore
            builder.AppendLine($"verifyReadyFindings: {verification.ReadyFindingCount}") |> ignore
            builder.AppendLine($"verifyAdvisory: {verification.AdvisoryCount}") |> ignore
            builder.AppendLine($"verifyWarnings: {verification.WarningCount}") |> ignore
            builder.AppendLine($"verifyBlocking: {verification.BlockingCount}") |> ignore
            builder.AppendLine($"verifyObligations: {verification.ObligationCount}") |> ignore
            builder.AppendLine($"verifyEvidenceSupported: {verification.EvidenceSupportedCount}") |> ignore
            builder.AppendLine($"verifyEvidenceDeferred: {verification.EvidenceDeferredCount}") |> ignore
            builder.AppendLine($"verifyEvidenceMissing: {verification.EvidenceMissingCount}") |> ignore
            builder.AppendLine($"verifyEvidenceStale: {verification.EvidenceStaleCount}") |> ignore
            builder.AppendLine($"verifyEvidenceSynthetic: {verification.EvidenceSyntheticCount}") |> ignore
            builder.AppendLine($"verifyEvidenceInvalid: {verification.EvidenceInvalidCount}") |> ignore
            builder.AppendLine($"verifyTestSatisfied: {verification.TestSatisfiedCount}") |> ignore
            builder.AppendLine($"verifyTestDeferred: {verification.TestDeferredCount}") |> ignore
            builder.AppendLine($"verifyTestMissing: {verification.TestMissingCount}") |> ignore
            builder.AppendLine($"verifyTestStale: {verification.TestStaleCount}") |> ignore
            builder.AppendLine($"verifyTestInvalid: {verification.TestInvalidCount}") |> ignore
            builder.AppendLine($"verifySkillVisible: {verification.SkillVisibleCount}") |> ignore
            builder.AppendLine($"verifySkillMissing: {verification.SkillMissingCount}") |> ignore
        | None -> ()

        builder.AppendLine($"generatedViews: {List.length report.GeneratedViews}") |> ignore
        builder.AppendLine($"diagnostics: {List.length report.Diagnostics}") |> ignore

        match report.NextAction with
        | Some action -> builder.AppendLine($"nextAction: {action.ActionId}") |> ignore
        | None -> builder.AppendLine("nextAction: none") |> ignore

        builder.ToString()
