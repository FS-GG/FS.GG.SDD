namespace FS.GG.SDD.Commands

open FS.GG.SDD.Commands.Internal

// Facade: keeps `module CommandReports`'s public surface byte-identical (per
// CommandReports.fsi) while the implementation lives in three cohesive internal
// units under CommandReports/ - diagnostic construction, next-action routing, and
// report + exit-code assembly (feature 062). Re-exports only; no logic here.
module CommandReports =
    let commandDiagnostic id severity path message correction relatedIds =
        DiagnosticConstructors.commandDiagnostic id severity path message correction relatedIds

    let unknownCommand value =
        DiagnosticConstructors.unknownCommand value

    let malformedWorkId value =
        DiagnosticConstructors.malformedWorkId value

    let missingWorkId command =
        DiagnosticConstructors.missingWorkId command

    let lintMissingArtifact () =
        DiagnosticConstructors.lintMissingArtifact ()

    let explainUnsupported command =
        DiagnosticConstructors.explainUnsupported command

    let unsupportedCommand command =
        DiagnosticConstructors.unsupportedCommand command

    let outsideProject () =
        DiagnosticConstructors.outsideProject ()

    let missingProjectConfig path =
        DiagnosticConstructors.missingProjectConfig path

    let malformedProjectConfig path =
        DiagnosticConstructors.malformedProjectConfig path

    let missingSddConfig path =
        DiagnosticConstructors.missingSddConfig path

    let malformedSddConfig path =
        DiagnosticConstructors.malformedSddConfig path

    let missingAgentsConfig path =
        DiagnosticConstructors.missingAgentsConfig path

    let malformedAgentsConfig path =
        DiagnosticConstructors.malformedAgentsConfig path

    let duplicateWorkId workId paths =
        DiagnosticConstructors.duplicateWorkId workId paths

    let missingCharterPrerequisite path message =
        DiagnosticConstructors.missingCharterPrerequisite path message

    let charterIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.charterIdentityMismatch path expectedWorkId actualWorkId

    let malformedCharterFrontMatter path message =
        DiagnosticConstructors.malformedCharterFrontMatter path message

    let missingSpecificationIntent path missingFacts =
        DiagnosticConstructors.missingSpecificationIntent path missingFacts

    let missingSpecificationPrerequisite path message =
        DiagnosticConstructors.missingSpecificationPrerequisite path message

    let specificationIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.specificationIdentityMismatch path expectedWorkId actualWorkId

    let malformedSpecificationFrontMatter path message =
        DiagnosticConstructors.malformedSpecificationFrontMatter path message

    let malformedSpecificationFacts path message =
        DiagnosticConstructors.malformedSpecificationFacts path message

    let duplicateSpecificationId path id =
        DiagnosticConstructors.duplicateSpecificationId path id

    let missingSpecificationId path idFamily =
        DiagnosticConstructors.missingSpecificationId path idFamily

    let unknownSpecificationReference path id =
        DiagnosticConstructors.unknownSpecificationReference path id

    let missingClarificationAnswer path missingIds =
        DiagnosticConstructors.missingClarificationAnswer path missingIds

    let missingClarificationPrerequisite path message =
        DiagnosticConstructors.missingClarificationPrerequisite path message

    let clarificationIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.clarificationIdentityMismatch path expectedWorkId actualWorkId

    let malformedClarificationFrontMatter path message =
        DiagnosticConstructors.malformedClarificationFrontMatter path message

    let duplicateClarificationId path id =
        DiagnosticConstructors.duplicateClarificationId path id

    let unknownClarificationReference path id =
        DiagnosticConstructors.unknownClarificationReference path id

    let unsafeDecisionChange path id =
        DiagnosticConstructors.unsafeDecisionChange path id

    let unresolvedBlockingAmbiguity path ids =
        DiagnosticConstructors.unresolvedBlockingAmbiguity path ids

    let failedRequirementsQuality path message correction relatedIds =
        DiagnosticConstructors.failedRequirementsQuality path message correction relatedIds

    let checklistIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.checklistIdentityMismatch path expectedWorkId actualWorkId

    let malformedChecklistFrontMatter path message =
        DiagnosticConstructors.malformedChecklistFrontMatter path message

    let missingChecklistBackReference path id =
        DiagnosticConstructors.missingChecklistBackReference path id

    let duplicateChecklistId path id =
        DiagnosticConstructors.duplicateChecklistId path id

    let unknownChecklistSourceReference path id =
        DiagnosticConstructors.unknownChecklistSourceReference path id

    let staleChecklistResult path resultIds =
        DiagnosticConstructors.staleChecklistResult path resultIds

    let missingChecklistPrerequisite path message =
        DiagnosticConstructors.missingChecklistPrerequisite path message

    let failedChecklistPrerequisite path message relatedIds =
        DiagnosticConstructors.failedChecklistPrerequisite path message relatedIds

    let planIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.planIdentityMismatch path expectedWorkId actualWorkId

    let malformedPlanFrontMatter path message =
        DiagnosticConstructors.malformedPlanFrontMatter path message

    let duplicatePlanId path id =
        DiagnosticConstructors.duplicatePlanId path id

    let unknownPlanSourceReference path id =
        DiagnosticConstructors.unknownPlanSourceReference path id

    let stalePlanDecision path decisionIds =
        DiagnosticConstructors.stalePlanDecision path decisionIds

    let missingPlanPrerequisite path message =
        DiagnosticConstructors.missingPlanPrerequisite path message

    let failedPlanPrerequisite path message relatedIds =
        DiagnosticConstructors.failedPlanPrerequisite path message relatedIds

    let tasksIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.tasksIdentityMismatch path expectedWorkId actualWorkId

    let malformedTasksArtifact path message =
        DiagnosticConstructors.malformedTasksArtifact path message

    let duplicateTaskId path id =
        DiagnosticConstructors.duplicateTaskId path id

    let unknownTaskSourceReference path id =
        DiagnosticConstructors.unknownTaskSourceReference path id

    let unknownTaskDependency path id =
        DiagnosticConstructors.unknownTaskDependency path id

    let taskDependencyCycle path ids =
        DiagnosticConstructors.taskDependencyCycle path ids

    let staleTask path taskIds =
        DiagnosticConstructors.staleTask path taskIds

    let doneTaskMissingEvidence path ids =
        DiagnosticConstructors.doneTaskMissingEvidence path ids

    let skippedTaskMissingRationale path ids =
        DiagnosticConstructors.skippedTaskMissingRationale path ids

    let missingTasksPrerequisite path message =
        DiagnosticConstructors.missingTasksPrerequisite path message

    let failedTasksPrerequisite path message relatedIds =
        DiagnosticConstructors.failedTasksPrerequisite path message relatedIds

    let analysisIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.analysisIdentityMismatch path expectedWorkId actualWorkId

    let malformedAnalysisView path message =
        DiagnosticConstructors.malformedAnalysisView path message

    let missingAnalysisPrerequisite path message =
        DiagnosticConstructors.missingAnalysisPrerequisite path message

    let analysisNotReady path readiness =
        DiagnosticConstructors.analysisNotReady path readiness

    let evidenceIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.evidenceIdentityMismatch path expectedWorkId actualWorkId

    let malformedEvidenceArtifact path message =
        DiagnosticConstructors.malformedEvidenceArtifact path message

    let duplicateEvidenceId path id =
        DiagnosticConstructors.duplicateEvidenceId path id

    let unknownEvidenceReference path id =
        DiagnosticConstructors.unknownEvidenceReference path id

    let missingRequiredEvidence path ids =
        DiagnosticConstructors.missingRequiredEvidence path ids

    let staleEvidence path ids =
        DiagnosticConstructors.staleEvidence path ids

    let staleEvidenceSource path ids =
        DiagnosticConstructors.staleEvidenceSource path ids

    let undisclosedSyntheticEvidence path ids =
        DiagnosticConstructors.undisclosedSyntheticEvidence path ids

    let missingDeferralRationale path ids =
        DiagnosticConstructors.missingDeferralRationale path ids

    let missingRequiredSkill path ids =
        DiagnosticConstructors.missingRequiredSkill path ids

    let unsupportedEvidenceResultState path states =
        DiagnosticConstructors.unsupportedEvidenceResultState path states

    let unsafeEvidenceUpdate path ids =
        DiagnosticConstructors.unsafeEvidenceUpdate path ids

    let missingDisposition path ids =
        DiagnosticConstructors.missingDisposition path ids

    let unsafeOverwrite path =
        DiagnosticConstructors.unsafeOverwrite path

    let malformedGeneratedView path =
        DiagnosticConstructors.malformedGeneratedView path

    let blockedGeneratedViewRefresh path relatedIds =
        DiagnosticConstructors.blockedGeneratedViewRefresh path relatedIds

    let missingEvidencePrerequisite path message =
        DiagnosticConstructors.missingEvidencePrerequisite path message

    let verifyIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.verifyIdentityMismatch path expectedWorkId actualWorkId

    let malformedVerificationView path message =
        DiagnosticConstructors.malformedVerificationView path message

    let missingRequiredTest path ids =
        DiagnosticConstructors.missingRequiredTest path ids

    let staleRequiredTest path ids =
        DiagnosticConstructors.staleRequiredTest path ids

    let toolDefect path message =
        DiagnosticConstructors.toolDefect path message

    let missingVerificationPrerequisite path message =
        DiagnosticConstructors.missingVerificationPrerequisite path message

    let verificationNotReady path status =
        DiagnosticConstructors.verificationNotReady path status

    let failedVerification path ids =
        DiagnosticConstructors.failedVerification path ids

    let staleVerificationView path ids =
        DiagnosticConstructors.staleVerificationView path ids

    let shipIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.shipIdentityMismatch path expectedWorkId actualWorkId

    let malformedShipView path message =
        DiagnosticConstructors.malformedShipView path message

    let agentsNoTargets path =
        DiagnosticConstructors.agentsNoTargets path

    let agentsInvalidGeneratedRoot path targetId =
        DiagnosticConstructors.agentsInvalidGeneratedRoot path targetId

    let agentsWorkModelIdentityMismatch path expectedWorkId actualWorkId =
        DiagnosticConstructors.agentsWorkModelIdentityMismatch path expectedWorkId actualWorkId

    let agentsMissingWorkModel path =
        DiagnosticConstructors.agentsMissingWorkModel path

    let agentsEarlyStageGuidance presentStages =
        DiagnosticConstructors.agentsEarlyStageGuidance presentStages

    let agentsMalformedWorkModel path message =
        DiagnosticConstructors.agentsMalformedWorkModel path message

    let agentsStaleWorkModel path =
        DiagnosticConstructors.agentsStaleWorkModel path

    let agentsBlockedWorkModel path relatedIds =
        DiagnosticConstructors.agentsBlockedWorkModel path relatedIds

    let agentsUnknownSourceReference path id =
        DiagnosticConstructors.agentsUnknownSourceReference path id

    let agentsMalformedGeneratedGuidance path message =
        DiagnosticConstructors.agentsMalformedGeneratedGuidance path message

    let agentsStaleGeneratedGuidance path targetId =
        DiagnosticConstructors.agentsStaleGeneratedGuidance path targetId

    let agentsBehaviorDivergence path targetIds =
        DiagnosticConstructors.agentsBehaviorDivergence path targetIds

    let agentsUnsafeGeneratedViewRefresh path relatedIds =
        DiagnosticConstructors.agentsUnsafeGeneratedViewRefresh path relatedIds

    let refreshMissingSource viewPath sourcePath =
        DiagnosticConstructors.refreshMissingSource viewPath sourcePath

    let refreshMalformedSource viewPath sourcePath message =
        DiagnosticConstructors.refreshMalformedSource viewPath sourcePath message

    let refreshStaleView viewPath sourcePaths =
        DiagnosticConstructors.refreshStaleView viewPath sourcePaths

    let refreshMalformedGeneratedView viewPath message =
        DiagnosticConstructors.refreshMalformedGeneratedView viewPath message

    let refreshBlockedUpstreamView viewPath upstreamViewPath =
        DiagnosticConstructors.refreshBlockedUpstreamView viewPath upstreamViewPath

    let refreshEarlyStageGuidance presentStages =
        DiagnosticConstructors.refreshEarlyStageGuidance presentStages

    let refreshUnrenderableSummary summaryPath relatedIds =
        DiagnosticConstructors.refreshUnrenderableSummary summaryPath relatedIds

    let buildReport model = ReportAssembly.buildReport model

    let helpReport request summary =
        ReportAssembly.helpReport request summary

    let exitCodeForReport report = ReportAssembly.exitCodeForReport report
