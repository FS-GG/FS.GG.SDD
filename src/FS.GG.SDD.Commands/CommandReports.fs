namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.Internal

// Facade: keeps `module CommandReports`'s public surface byte-identical (per
// CommandReports.fsi) while the implementation lives in three cohesive internal
// units under CommandReports/ - diagnostic construction, next-action routing, and
// report + exit-code assembly (feature 062). Re-exports only; no logic here.
module CommandReports =
    let commandDiagnostic a0 a1 a2 a3 a4 a5 =
        DiagnosticConstructors.commandDiagnostic a0 a1 a2 a3 a4 a5

    let unknownCommand a0 =
        DiagnosticConstructors.unknownCommand a0

    let malformedWorkId a0 =
        DiagnosticConstructors.malformedWorkId a0

    let missingWorkId a0 = DiagnosticConstructors.missingWorkId a0

    let unsupportedCommand a0 =
        DiagnosticConstructors.unsupportedCommand a0

    let outsideProject () =
        DiagnosticConstructors.outsideProject ()

    let missingProjectConfig a0 =
        DiagnosticConstructors.missingProjectConfig a0

    let malformedProjectConfig a0 =
        DiagnosticConstructors.malformedProjectConfig a0

    let missingSddConfig a0 =
        DiagnosticConstructors.missingSddConfig a0

    let malformedSddConfig a0 =
        DiagnosticConstructors.malformedSddConfig a0

    let missingAgentsConfig a0 =
        DiagnosticConstructors.missingAgentsConfig a0

    let malformedAgentsConfig a0 =
        DiagnosticConstructors.malformedAgentsConfig a0

    let duplicateWorkId a0 a1 =
        DiagnosticConstructors.duplicateWorkId a0 a1

    let missingCharterPrerequisite a0 a1 =
        DiagnosticConstructors.missingCharterPrerequisite a0 a1

    let charterIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.charterIdentityMismatch a0 a1 a2

    let malformedCharterFrontMatter a0 a1 =
        DiagnosticConstructors.malformedCharterFrontMatter a0 a1

    let missingSpecificationIntent a0 a1 =
        DiagnosticConstructors.missingSpecificationIntent a0 a1

    let missingSpecificationPrerequisite a0 a1 =
        DiagnosticConstructors.missingSpecificationPrerequisite a0 a1

    let specificationIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.specificationIdentityMismatch a0 a1 a2

    let malformedSpecificationFrontMatter a0 a1 =
        DiagnosticConstructors.malformedSpecificationFrontMatter a0 a1

    let malformedSpecificationFacts a0 a1 =
        DiagnosticConstructors.malformedSpecificationFacts a0 a1

    let duplicateSpecificationId a0 a1 =
        DiagnosticConstructors.duplicateSpecificationId a0 a1

    let missingSpecificationId a0 a1 =
        DiagnosticConstructors.missingSpecificationId a0 a1

    let unknownSpecificationReference a0 a1 =
        DiagnosticConstructors.unknownSpecificationReference a0 a1

    let missingClarificationAnswer a0 a1 =
        DiagnosticConstructors.missingClarificationAnswer a0 a1

    let missingClarificationPrerequisite a0 a1 =
        DiagnosticConstructors.missingClarificationPrerequisite a0 a1

    let clarificationIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.clarificationIdentityMismatch a0 a1 a2

    let malformedClarificationFrontMatter a0 a1 =
        DiagnosticConstructors.malformedClarificationFrontMatter a0 a1

    let duplicateClarificationId a0 a1 =
        DiagnosticConstructors.duplicateClarificationId a0 a1

    let unknownClarificationReference a0 a1 =
        DiagnosticConstructors.unknownClarificationReference a0 a1

    let unsafeDecisionChange a0 a1 =
        DiagnosticConstructors.unsafeDecisionChange a0 a1

    let unresolvedBlockingAmbiguity a0 a1 =
        DiagnosticConstructors.unresolvedBlockingAmbiguity a0 a1

    let failedRequirementsQuality a0 a1 a2 a3 =
        DiagnosticConstructors.failedRequirementsQuality a0 a1 a2 a3

    let checklistIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.checklistIdentityMismatch a0 a1 a2

    let malformedChecklistFrontMatter a0 a1 =
        DiagnosticConstructors.malformedChecklistFrontMatter a0 a1

    let duplicateChecklistId a0 a1 =
        DiagnosticConstructors.duplicateChecklistId a0 a1

    let unknownChecklistSourceReference a0 a1 =
        DiagnosticConstructors.unknownChecklistSourceReference a0 a1

    let staleChecklistResult a0 a1 =
        DiagnosticConstructors.staleChecklistResult a0 a1

    let missingChecklistPrerequisite a0 a1 =
        DiagnosticConstructors.missingChecklistPrerequisite a0 a1

    let failedChecklistPrerequisite a0 a1 a2 =
        DiagnosticConstructors.failedChecklistPrerequisite a0 a1 a2

    let planIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.planIdentityMismatch a0 a1 a2

    let malformedPlanFrontMatter a0 a1 =
        DiagnosticConstructors.malformedPlanFrontMatter a0 a1

    let duplicatePlanId a0 a1 =
        DiagnosticConstructors.duplicatePlanId a0 a1

    let unknownPlanSourceReference a0 a1 =
        DiagnosticConstructors.unknownPlanSourceReference a0 a1

    let stalePlanDecision a0 a1 =
        DiagnosticConstructors.stalePlanDecision a0 a1

    let missingPlanPrerequisite a0 a1 =
        DiagnosticConstructors.missingPlanPrerequisite a0 a1

    let failedPlanPrerequisite a0 a1 a2 =
        DiagnosticConstructors.failedPlanPrerequisite a0 a1 a2

    let tasksIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.tasksIdentityMismatch a0 a1 a2

    let malformedTasksArtifact a0 a1 =
        DiagnosticConstructors.malformedTasksArtifact a0 a1

    let duplicateTaskId a0 a1 =
        DiagnosticConstructors.duplicateTaskId a0 a1

    let unknownTaskSourceReference a0 a1 =
        DiagnosticConstructors.unknownTaskSourceReference a0 a1

    let unknownTaskDependency a0 a1 =
        DiagnosticConstructors.unknownTaskDependency a0 a1

    let taskDependencyCycle a0 a1 =
        DiagnosticConstructors.taskDependencyCycle a0 a1

    let staleTask a0 a1 = DiagnosticConstructors.staleTask a0 a1

    let doneTaskMissingEvidence a0 a1 =
        DiagnosticConstructors.doneTaskMissingEvidence a0 a1

    let skippedTaskMissingRationale a0 a1 =
        DiagnosticConstructors.skippedTaskMissingRationale a0 a1

    let missingTasksPrerequisite a0 a1 =
        DiagnosticConstructors.missingTasksPrerequisite a0 a1

    let failedTasksPrerequisite a0 a1 a2 =
        DiagnosticConstructors.failedTasksPrerequisite a0 a1 a2

    let analysisIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.analysisIdentityMismatch a0 a1 a2

    let malformedAnalysisView a0 a1 =
        DiagnosticConstructors.malformedAnalysisView a0 a1

    let missingAnalysisPrerequisite a0 a1 =
        DiagnosticConstructors.missingAnalysisPrerequisite a0 a1

    let analysisNotReady a0 a1 =
        DiagnosticConstructors.analysisNotReady a0 a1

    let evidenceIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.evidenceIdentityMismatch a0 a1 a2

    let malformedEvidenceArtifact a0 a1 =
        DiagnosticConstructors.malformedEvidenceArtifact a0 a1

    let duplicateEvidenceId a0 a1 =
        DiagnosticConstructors.duplicateEvidenceId a0 a1

    let unknownEvidenceReference a0 a1 =
        DiagnosticConstructors.unknownEvidenceReference a0 a1

    let missingRequiredEvidence a0 a1 =
        DiagnosticConstructors.missingRequiredEvidence a0 a1

    let staleEvidence a0 a1 =
        DiagnosticConstructors.staleEvidence a0 a1

    let staleEvidenceSource a0 a1 =
        DiagnosticConstructors.staleEvidenceSource a0 a1

    let undisclosedSyntheticEvidence a0 a1 =
        DiagnosticConstructors.undisclosedSyntheticEvidence a0 a1

    let missingDeferralRationale a0 a1 =
        DiagnosticConstructors.missingDeferralRationale a0 a1

    let missingRequiredSkill a0 a1 =
        DiagnosticConstructors.missingRequiredSkill a0 a1

    let unsupportedEvidenceResultState a0 a1 =
        DiagnosticConstructors.unsupportedEvidenceResultState a0 a1

    let unsafeEvidenceUpdate a0 a1 =
        DiagnosticConstructors.unsafeEvidenceUpdate a0 a1

    let missingDisposition a0 a1 =
        DiagnosticConstructors.missingDisposition a0 a1

    let unsafeOverwrite a0 =
        DiagnosticConstructors.unsafeOverwrite a0

    let malformedGeneratedView a0 =
        DiagnosticConstructors.malformedGeneratedView a0

    let blockedGeneratedViewRefresh a0 a1 =
        DiagnosticConstructors.blockedGeneratedViewRefresh a0 a1

    let missingEvidencePrerequisite a0 a1 =
        DiagnosticConstructors.missingEvidencePrerequisite a0 a1

    let verifyIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.verifyIdentityMismatch a0 a1 a2

    let malformedVerificationView a0 a1 =
        DiagnosticConstructors.malformedVerificationView a0 a1

    let missingRequiredTest a0 a1 =
        DiagnosticConstructors.missingRequiredTest a0 a1

    let staleRequiredTest a0 a1 =
        DiagnosticConstructors.staleRequiredTest a0 a1

    let toolDefect a0 a1 = DiagnosticConstructors.toolDefect a0 a1

    let missingVerificationPrerequisite a0 a1 =
        DiagnosticConstructors.missingVerificationPrerequisite a0 a1

    let verificationNotReady a0 a1 =
        DiagnosticConstructors.verificationNotReady a0 a1

    let failedVerification a0 a1 =
        DiagnosticConstructors.failedVerification a0 a1

    let staleVerificationView a0 a1 =
        DiagnosticConstructors.staleVerificationView a0 a1

    let shipIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.shipIdentityMismatch a0 a1 a2

    let malformedShipView a0 a1 =
        DiagnosticConstructors.malformedShipView a0 a1

    let agentsNoTargets a0 =
        DiagnosticConstructors.agentsNoTargets a0

    let agentsInvalidGeneratedRoot a0 a1 =
        DiagnosticConstructors.agentsInvalidGeneratedRoot a0 a1

    let agentsWorkModelIdentityMismatch a0 a1 a2 =
        DiagnosticConstructors.agentsWorkModelIdentityMismatch a0 a1 a2

    let agentsMissingWorkModel a0 =
        DiagnosticConstructors.agentsMissingWorkModel a0

    let agentsEarlyStageGuidance a0 =
        DiagnosticConstructors.agentsEarlyStageGuidance a0

    let agentsMalformedWorkModel a0 a1 =
        DiagnosticConstructors.agentsMalformedWorkModel a0 a1

    let agentsStaleWorkModel a0 =
        DiagnosticConstructors.agentsStaleWorkModel a0

    let agentsBlockedWorkModel a0 a1 =
        DiagnosticConstructors.agentsBlockedWorkModel a0 a1

    let agentsUnknownSourceReference a0 a1 =
        DiagnosticConstructors.agentsUnknownSourceReference a0 a1

    let agentsMalformedGeneratedGuidance a0 a1 =
        DiagnosticConstructors.agentsMalformedGeneratedGuidance a0 a1

    let agentsStaleGeneratedGuidance a0 a1 =
        DiagnosticConstructors.agentsStaleGeneratedGuidance a0 a1

    let agentsBehaviorDivergence a0 a1 =
        DiagnosticConstructors.agentsBehaviorDivergence a0 a1

    let agentsUnsafeGeneratedViewRefresh a0 a1 =
        DiagnosticConstructors.agentsUnsafeGeneratedViewRefresh a0 a1

    let refreshMissingSource a0 a1 =
        DiagnosticConstructors.refreshMissingSource a0 a1

    let refreshMalformedSource a0 a1 a2 =
        DiagnosticConstructors.refreshMalformedSource a0 a1 a2

    let refreshStaleView a0 a1 =
        DiagnosticConstructors.refreshStaleView a0 a1

    let refreshMalformedGeneratedView a0 a1 =
        DiagnosticConstructors.refreshMalformedGeneratedView a0 a1

    let refreshBlockedUpstreamView a0 a1 =
        DiagnosticConstructors.refreshBlockedUpstreamView a0 a1

    let refreshEarlyStageGuidance a0 =
        DiagnosticConstructors.refreshEarlyStageGuidance a0

    let refreshUnrenderableSummary a0 a1 =
        DiagnosticConstructors.refreshUnrenderableSummary a0 a1

    let buildReport a0 = ReportAssembly.buildReport a0
    let helpReport a0 a1 = ReportAssembly.helpReport a0 a1
    let exitCodeForReport a0 = ReportAssembly.exitCodeForReport a0
