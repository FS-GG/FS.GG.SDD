namespace FS.GG.SDD.Commands

open System.Text
open FS.GG.SDD.Commands.CommandTypes

module CommandRendering =
    let renderText (report: CommandReport) =
        let builder = StringBuilder()
        builder.AppendLine($"command: {commandName report.Command}") |> ignore
        // FS-GG/FS.GG.SDD#305: unconditional, so a captured text report carries the producing version
        // too. The rich view scrapes this "key: value" line into its details table for free.
        builder.AppendLine($"toolVersion: {report.ToolVersion}") |> ignore
        builder.AppendLine($"outcome: {outcomeValue report.Outcome}") |> ignore

        // FS-GG/FS.GG.SDD#183: surface the positive "clean, advance" signal only when earned, so a bare
        // no-op stays a quiet `outcome: noChange` while a coherent re-run reads unambiguously. The rich
        // view scrapes this line into its details table, so this one seam covers text and rich alike.
        if report.Coherent then
            builder.AppendLine("coherent: true") |> ignore

        builder.AppendLine($"changedArtifacts: {List.length report.ChangedArtifacts}")
        |> ignore

        match report.Specification with
        | Some specification ->
            builder.AppendLine($"specificationRequirements: {List.length specification.RequirementIds}")
            |> ignore

            builder.AppendLine($"specificationStories: {List.length specification.StoryIds}")
            |> ignore

            builder.AppendLine($"specificationAcceptanceScenarios: {List.length specification.AcceptanceScenarioIds}")
            |> ignore
        | None -> ()

        match report.Clarification with
        | Some clarification ->
            builder.AppendLine($"clarificationQuestions: {List.length clarification.QuestionIds}")
            |> ignore

            builder.AppendLine($"clarificationDecisions: {List.length clarification.DecisionIds}")
            |> ignore

            builder.AppendLine($"acceptedDeferrals: {List.length clarification.AcceptedDeferralIds}")
            |> ignore

            builder.AppendLine($"remainingAmbiguities: {clarification.RemainingAmbiguityCount}")
            |> ignore

            builder.AppendLine($"blockingAmbiguities: {clarification.BlockingAmbiguityCount}")
            |> ignore
        | None -> ()

        match report.Checklist with
        | Some checklist ->
            builder.AppendLine($"checklistItems: {List.length checklist.ItemIds}") |> ignore

            builder.AppendLine($"checklistResults: {List.length checklist.ResultIds}")
            |> ignore

            builder.AppendLine($"checklistPassed: {checklist.PassedCount}") |> ignore

            builder.AppendLine($"checklistFailedBlocking: {checklist.FailedBlockingCount}")
            |> ignore

            builder.AppendLine($"checklistAcceptedDeferrals: {checklist.AcceptedDeferralCount}")
            |> ignore

            builder.AppendLine($"checklistStaleResults: {checklist.StaleResultCount}")
            |> ignore

            builder.AppendLine($"checklistAdvisory: {checklist.AdvisoryCount}") |> ignore
        | None -> ()

        match report.Plan with
        | Some plan ->
            builder.AppendLine($"planDecisions: {List.length plan.DecisionIds}") |> ignore

            builder.AppendLine($"planContractReferences: {List.length plan.ContractReferenceIds}")
            |> ignore

            builder.AppendLine($"planVerificationObligations: {List.length plan.VerificationObligationIds}")
            |> ignore

            builder.AppendLine($"planAcceptedDeferrals: {plan.AcceptedDeferralCount}")
            |> ignore

            builder.AppendLine($"planStaleDecisions: {plan.StaleDecisionCount}") |> ignore

            builder.AppendLine($"planBlockingFindings: {plan.BlockingFindingCount}")
            |> ignore

            builder.AppendLine($"planAdvisory: {plan.AdvisoryCount}") |> ignore
        | None -> ()

        match report.Tasks with
        | Some tasks ->
            builder.AppendLine($"tasks: {List.length tasks.TaskIds}") |> ignore
            builder.AppendLine($"taskDependencies: {tasks.DependencyCount}") |> ignore
            builder.AppendLine($"taskRequiredSkills: {tasks.RequiredSkillCount}") |> ignore

            builder.AppendLine($"taskRequiredEvidence: {tasks.RequiredEvidenceCount}")
            |> ignore

            builder.AppendLine($"taskPending: {tasks.PendingCount}") |> ignore
            builder.AppendLine($"taskInProgress: {tasks.InProgressCount}") |> ignore
            builder.AppendLine($"taskDone: {tasks.DoneCount}") |> ignore
            builder.AppendLine($"taskSkipped: {tasks.SkippedCount}") |> ignore
            builder.AppendLine($"taskStale: {tasks.StaleCount}") |> ignore

            builder.AppendLine($"taskAcceptedDeferrals: {tasks.AcceptedDeferralCount}")
            |> ignore

            builder.AppendLine($"taskBlockingFindings: {tasks.BlockingFindingCount}")
            |> ignore

            builder.AppendLine($"taskAdvisory: {tasks.AdvisoryCount}") |> ignore
        | None -> ()

        match report.Analysis with
        | Some analysis ->
            builder.AppendLine($"workId: {analysis.WorkId}") |> ignore
            builder.AppendLine($"analysisPath: {analysis.AnalysisPath}") |> ignore
            builder.AppendLine($"analysisReadiness: {analysis.Readiness}") |> ignore
            builder.AppendLine($"analysisSources: {analysis.SourceCount}") |> ignore

            builder.AppendLine($"analysisRelationships: {analysis.SourceRelationshipCount}")
            |> ignore

            builder.AppendLine($"analysisReadyFindings: {analysis.ReadyFindingCount}")
            |> ignore

            builder.AppendLine($"analysisAdvisory: {analysis.AdvisoryCount}") |> ignore
            builder.AppendLine($"analysisWarnings: {analysis.WarningCount}") |> ignore
            builder.AppendLine($"analysisBlocking: {analysis.BlockingCount}") |> ignore

            builder.AppendLine($"analysisStaleSources: {analysis.StaleSourceCount}")
            |> ignore

            builder.AppendLine($"analysisMissingDispositions: {analysis.MissingDispositionCount}")
            |> ignore

            builder.AppendLine($"analysisGeneratedViewFindings: {analysis.GeneratedViewFindingCount}")
            |> ignore
        | None -> ()

        match report.Evidence with
        | Some evidence ->
            builder.AppendLine($"workId: {evidence.WorkId}") |> ignore
            builder.AppendLine($"evidencePath: {evidence.EvidencePath}") |> ignore
            builder.AppendLine($"evidenceReadiness: {evidence.Readiness}") |> ignore

            builder.AppendLine($"evidenceDeclarations: {evidence.DeclarationCount}")
            |> ignore

            builder.AppendLine($"evidenceObligations: {evidence.ObligationCount}") |> ignore
            builder.AppendLine($"evidenceSupported: {evidence.SupportedCount}") |> ignore
            builder.AppendLine($"evidenceDeferred: {evidence.DeferredCount}") |> ignore
            builder.AppendLine($"evidenceMissing: {evidence.MissingCount}") |> ignore
            builder.AppendLine($"evidenceStale: {evidence.StaleCount}") |> ignore
            builder.AppendLine($"evidenceSynthetic: {evidence.SyntheticCount}") |> ignore
            builder.AppendLine($"evidenceInvalid: {evidence.InvalidCount}") |> ignore
            builder.AppendLine($"evidenceBlocking: {evidence.BlockingCount}") |> ignore

            builder.AppendLine($"evidenceClassifiedObligationsUnmet: {evidence.ClassifiedObligationsUnmetCount}")
            |> ignore
        | None -> ()

        match report.Verification with
        | Some verification ->
            builder.AppendLine($"workId: {verification.WorkId}") |> ignore
            builder.AppendLine($"verifyPath: {verification.VerifyPath}") |> ignore
            builder.AppendLine($"verificationReadiness: {verification.Readiness}") |> ignore

            builder.AppendLine($"verifyReadyFindings: {verification.ReadyFindingCount}")
            |> ignore

            builder.AppendLine($"verifyAdvisory: {verification.AdvisoryCount}") |> ignore
            builder.AppendLine($"verifyWarnings: {verification.WarningCount}") |> ignore
            builder.AppendLine($"verifyBlocking: {verification.BlockingCount}") |> ignore

            builder.AppendLine($"verifyObligations: {verification.ObligationCount}")
            |> ignore

            builder.AppendLine($"verifyEvidenceSupported: {verification.EvidenceSupportedCount}")
            |> ignore

            // #398: the green, and what it rests on. `observed: 0` is not a placeholder — SDD runs
            // nothing — and it stays beside `supported` so the two are never read apart.
            builder.AppendLine($"verifyEvidenceSelfAttested: {verification.EvidenceSelfAttestedCount}")
            |> ignore

            builder.AppendLine($"verifyEvidenceObserved: {verification.EvidenceObservedCount}")
            |> ignore

            builder.AppendLine($"verifyEvidenceDeferred: {verification.EvidenceDeferredCount}")
            |> ignore

            builder.AppendLine($"verifyEvidenceMissing: {verification.EvidenceMissingCount}")
            |> ignore

            builder.AppendLine($"verifyEvidenceStale: {verification.EvidenceStaleCount}")
            |> ignore

            builder.AppendLine($"verifyEvidenceSynthetic: {verification.EvidenceSyntheticCount}")
            |> ignore

            builder.AppendLine($"verifyEvidenceInvalid: {verification.EvidenceInvalidCount}")
            |> ignore

            builder.AppendLine($"verifyClassifiedObligationsUnmet: {verification.ClassifiedObligationsUnmetCount}")
            |> ignore

            builder.AppendLine($"verifyTestSatisfied: {verification.TestSatisfiedCount}")
            |> ignore

            // #398: `verifyTestSatisfied` is the single most misleading number the lifecycle prints —
            // it names a test, and SDD has never run one. It does not get to stand alone.
            builder.AppendLine($"verifyTestSelfAttested: {verification.TestSelfAttestedCount}")
            |> ignore

            builder.AppendLine($"verifyTestObserved: {verification.TestObservedCount}")
            |> ignore

            builder.AppendLine($"verifyTestDeferred: {verification.TestDeferredCount}")
            |> ignore

            builder.AppendLine($"verifyTestMissing: {verification.TestMissingCount}")
            |> ignore

            builder.AppendLine($"verifyTestStale: {verification.TestStaleCount}") |> ignore

            builder.AppendLine($"verifyTestInvalid: {verification.TestInvalidCount}")
            |> ignore

            builder.AppendLine($"verifySkillVisible: {verification.SkillVisibleCount}")
            |> ignore

            builder.AppendLine($"verifySkillMissing: {verification.SkillMissingCount}")
            |> ignore
        | None -> ()

        match report.Ship with
        | Some ship ->
            builder.AppendLine($"workId: {ship.WorkId}") |> ignore
            builder.AppendLine($"shipPath: {ship.ShipPath}") |> ignore
            builder.AppendLine($"shipReadiness: {ship.Readiness}") |> ignore
            builder.AppendLine($"shipDisposition: {ship.Disposition}") |> ignore
            builder.AppendLine($"shipReadyFindings: {ship.ReadyFindingCount}") |> ignore
            builder.AppendLine($"shipAdvisory: {ship.AdvisoryCount}") |> ignore
            builder.AppendLine($"shipWarnings: {ship.WarningCount}") |> ignore
            builder.AppendLine($"shipBlocking: {ship.BlockingCount}") |> ignore

            builder.AppendLine($"shipVerificationReadiness: {ship.VerificationReadiness}")
            |> ignore

            ship.LifecycleStageReadiness
            |> List.sortBy fst
            |> List.iter (fun (stage, status) -> builder.AppendLine($"shipStage.{stage}: {status}") |> ignore)

            builder.AppendLine($"shipEvidenceSupported: {ship.EvidenceSupportedCount}")
            |> ignore

            builder.AppendLine($"shipEvidenceSelfAttested: {ship.EvidenceSelfAttestedCount}")
            |> ignore

            builder.AppendLine($"shipEvidenceObserved: {ship.EvidenceObservedCount}")
            |> ignore

            builder.AppendLine($"shipEvidenceDeferred: {ship.EvidenceDeferredCount}")
            |> ignore

            builder.AppendLine($"shipEvidenceMissing: {ship.EvidenceMissingCount}")
            |> ignore

            builder.AppendLine($"shipEvidenceStale: {ship.EvidenceStaleCount}") |> ignore

            builder.AppendLine($"shipEvidenceSynthetic: {ship.EvidenceSyntheticCount}")
            |> ignore

            builder.AppendLine($"shipEvidenceInvalid: {ship.EvidenceInvalidCount}")
            |> ignore

            builder.AppendLine($"shipClassifiedObligationsUnmet: {ship.ClassifiedObligationsUnmetCount}")
            |> ignore

            builder.AppendLine($"shipGeneratedViewState: {ship.GeneratedViewState}")
            |> ignore
        | None -> ()

        match report.AgentGuidance with
        | Some guidance ->
            builder.AppendLine($"workId: {guidance.WorkId}") |> ignore
            builder.AppendLine($"agentsReadiness: {guidance.Readiness}") |> ignore
            builder.AppendLine($"agentsDisposition: {guidance.Disposition}") |> ignore

            guidance.GeneratedTargetIds
            |> List.sort
            |> List.iter (fun target -> builder.AppendLine($"agentsTarget: {target}") |> ignore)

            guidance.GeneratedRoots
            |> List.sort
            |> List.iter (fun root -> builder.AppendLine($"agentsGeneratedRoot: {root}") |> ignore)

            builder.AppendLine($"agentsEquivalenceRequired: {guidance.EquivalenceRequired}")
            |> ignore

            guidance.DivergentTargetIds
            |> List.sort
            |> List.iter (fun target -> builder.AppendLine($"agentsDivergentTarget: {target}") |> ignore)

            builder.AppendLine($"agentsReadyFindings: {guidance.ReadyFindingCount}")
            |> ignore

            builder.AppendLine($"agentsAdvisory: {guidance.AdvisoryCount}") |> ignore
            builder.AppendLine($"agentsWarnings: {guidance.WarningCount}") |> ignore
            builder.AppendLine($"agentsBlocking: {guidance.BlockingCount}") |> ignore

            builder.AppendLine($"agentsGeneratedViewState: {guidance.GeneratedViewState}")
            |> ignore
        | None -> ()

        match report.Refresh with
        | Some refresh ->
            builder.AppendLine($"workId: {refresh.WorkId}") |> ignore
            builder.AppendLine($"refreshReadiness: {refresh.Readiness}") |> ignore
            builder.AppendLine($"refreshDisposition: {refresh.Disposition}") |> ignore
            builder.AppendLine($"refreshSummaryPath: {refresh.SummaryPath}") |> ignore

            refresh.PerViewState
            |> List.iter (fun (view, state) -> builder.AppendLine($"refreshView.{view}: {state}") |> ignore)

            refresh.RefreshedViewIds
            |> List.sort
            |> List.iter (fun view -> builder.AppendLine($"refreshedView: {view}") |> ignore)

            refresh.AlreadyCurrentViewIds
            |> List.sort
            |> List.iter (fun view -> builder.AppendLine($"refreshAlreadyCurrentView: {view}") |> ignore)

            refresh.BlockedViewIds
            |> List.sort
            |> List.iter (fun view -> builder.AppendLine($"refreshBlockedView: {view}") |> ignore)

            refresh.NotApplicableViewIds
            |> List.sort
            |> List.iter (fun view -> builder.AppendLine($"refreshNotApplicableView: {view}") |> ignore)

            builder.AppendLine($"refreshAdvisory: {refresh.AdvisoryCount}") |> ignore
            builder.AppendLine($"refreshWarnings: {refresh.WarningCount}") |> ignore
            builder.AppendLine($"refreshBlocking: {refresh.BlockingCount}") |> ignore

            builder.AppendLine($"refreshSourceSnapshots: {refresh.SourceSnapshotCount}")
            |> ignore
        | None -> ()

        match report.Scaffold with
        | Some scaffold ->
            let providerName = defaultArg scaffold.ProviderName "(none)"
            let providerVersion = defaultArg scaffold.ProviderContractVersion "(none)"
            let requiredMinimum = defaultArg scaffold.RequiredMinimumCliVersion "(none)"
            builder.AppendLine($"scaffoldProvider: {providerName}") |> ignore

            builder.AppendLine($"scaffoldProviderContractVersion: {providerVersion}")
            |> ignore

            builder.AppendLine($"scaffoldRequiredMinimumCliVersion: {requiredMinimum}")
            |> ignore

            builder.AppendLine($"scaffoldOutcome: {scaffold.Outcome}") |> ignore

            builder.AppendLine($"scaffoldSkeletonCreated: {scaffold.SkeletonCreated}")
            |> ignore

            builder.AppendLine($"scaffoldProviderInvoked: {scaffold.ProviderInvoked}")
            |> ignore

            builder.AppendLine($"scaffoldProducedPaths: {scaffold.ProducedPathCount}")
            |> ignore

            scaffold.ProducedPaths
            |> List.sort
            |> List.iter (fun path -> builder.AppendLine($"scaffoldProducedPath: {path}") |> ignore)
            // 056: the fan-out mirror copies, one line each (parity with the json array).
            builder.AppendLine($"scaffoldMirroredPaths: {List.length scaffold.MirroredPaths}")
            |> ignore

            scaffold.MirroredPaths
            |> List.sort
            |> List.iter (fun path -> builder.AppendLine($"scaffoldMirroredPath: {path}") |> ignore)

            // 108 / ADR-0054: the driver skill copies materialized from the pinned package, one
            // line each (parity with the json array).
            builder.AppendLine($"scaffoldMaterializedDriverPaths: {List.length scaffold.MaterializedDriverPaths}")
            |> ignore

            scaffold.MaterializedDriverPaths
            |> List.sort
            |> List.iter (fun path -> builder.AppendLine($"scaffoldMaterializedDriverPath: {path}") |> ignore)

            // ADR-0063 / FS.GG.SDD#623: the owner-authored product skill copies materialized
            // from the pinned the owner-skills package, one line each (parity with the json array).
            builder.AppendLine($"scaffoldMaterializedGameSkillPaths: {List.length scaffold.MaterializedGameSkillPaths}")
            |> ignore

            scaffold.MaterializedGameSkillPaths
            |> List.sort
            |> List.iter (fun path -> builder.AppendLine($"scaffoldMaterializedGameSkillPath: {path}") |> ignore)

            scaffold.EffectiveParameters
            |> List.sortBy fst
            |> List.iter (fun (key, value) -> builder.AppendLine($"scaffoldEffectiveParam: {key}={value}") |> ignore)

            builder.AppendLine($"scaffoldRepoInit: {scaffold.RepoInitOutcome}") |> ignore

            builder.AppendLine($"scaffoldToolManifest: {scaffold.ToolManifestOutcome}")
            |> ignore

            builder.AppendLine($"scaffoldExecutableScripts: {scaffold.ExecutableScriptCount}")
            |> ignore

            builder.AppendLine($"scaffoldExecutableScriptsSkipped: {scaffold.ExecutableScriptsSkipped}")
            |> ignore

            builder.AppendLine($"scaffoldNextAction: {scaffold.NextActionHint}") |> ignore

            // Feature 054: the provider-defect diagnostic facts, single-line-encoded so the
            // rich renderer's `key: value` derivation stays intact (R6) — embedded newlines
            // become the literal `\n`. Emitted only when a provider defect surfaced them.
            match scaffold.ProviderInvocation with
            | Some invocation ->
                let singleLine (value: string) =
                    value.Replace("\r\n", "\n").Replace("\n", "\\n").Replace("\r", "\\n")

                let exitCode =
                    match invocation.ExitCode with
                    | Some code -> string code
                    | None -> "(not launched)"

                builder.AppendLine($"scaffoldProviderCommandLine: {singleLine invocation.CommandLine}")
                |> ignore

                builder.AppendLine($"scaffoldProviderExitCode: {exitCode}") |> ignore

                builder.AppendLine($"scaffoldProviderStdout: {singleLine invocation.StandardOutput}")
                |> ignore

                builder.AppendLine($"scaffoldProviderStdoutTruncated: {invocation.StandardOutputTruncated}")
                |> ignore

                builder.AppendLine($"scaffoldProviderStderr: {singleLine invocation.StandardError}")
                |> ignore

                builder.AppendLine($"scaffoldProviderStderrTruncated: {invocation.StandardErrorTruncated}")
                |> ignore
            | None -> ()
        | None -> ()

        match report.Doctor with
        | Some doctor ->
            let doctorProvider = defaultArg doctor.ProviderName "(none)"
            let doctorRequiredMinimum = defaultArg doctor.RequiredMinimumCliVersion "(none)"

            let doctorRequiredMinimumSource =
                defaultArg doctor.RequiredMinimumCliVersionSource "(none)"

            let doctorBehindBy = defaultArg doctor.CliBehindBy "(none)"
            builder.AppendLine($"doctorHasProvenance: {doctor.HasProvenance}") |> ignore
            builder.AppendLine($"doctorProvider: {doctorProvider}") |> ignore

            builder.AppendLine($"doctorInstalledCli: {doctor.InstalledCliVersion}")
            |> ignore

            builder.AppendLine($"doctorRequiredMinimumCli: {doctorRequiredMinimum}")
            |> ignore

            builder.AppendLine($"doctorRequiredMinimumCliSource: {doctorRequiredMinimumSource}")
            |> ignore

            builder.AppendLine($"doctorCliAxis: {doctor.CliAxis}") |> ignore
            builder.AppendLine($"doctorCliBehindBy: {doctorBehindBy}") |> ignore

            builder.AppendLine($"doctorExpectedArtifacts: {doctor.ExpectedArtifactCount}")
            |> ignore

            builder.AppendLine($"doctorMissingArtifacts: {List.length doctor.MissingArtifactPaths}")
            |> ignore

            doctor.MissingArtifactPaths
            |> List.sort
            |> List.iter (fun path -> builder.AppendLine($"doctorMissingArtifact: {path}") |> ignore)

            builder.AppendLine($"doctorSkillDrifts: {List.length doctor.SkillDriftPaths}")
            |> ignore

            doctor.SkillDriftPaths
            |> List.sort
            |> List.iter (fun path -> builder.AppendLine($"doctorSkillDrift: {path}") |> ignore)

            doctor.PreviewSteps
            |> List.iter (fun step ->
                builder.AppendLine(
                    $"doctorPreviewStep: {reconciliationStepIdValue step.StepId}={reconciliationOutcomeValue step.Outcome}"
                )
                |> ignore)

            builder.AppendLine($"doctorCoherent: {doctor.IsCoherent}") |> ignore
        | None -> ()

        // Feature 086: `surface` — one `key: value` line per fact; `--rich` derives its table from
        // these lines, so no bespoke rich block is needed.
        match report.Surface with
        | Some surface ->
            builder.AppendLine($"surfaceMode: {surface.Mode}") |> ignore
            builder.AppendLine($"surfaceSourceRoot: {surface.SourceRoot}") |> ignore
            builder.AppendLine($"surfaceBaselineRoot: {surface.BaselineRoot}") |> ignore
            builder.AppendLine($"surfaceChecked: {surface.CheckedCount}") |> ignore

            builder.AppendLine($"surfaceMissingBaselines: {List.length surface.MissingBaselinePaths}")
            |> ignore

            surface.MissingBaselinePaths
            |> List.iter (fun path -> builder.AppendLine($"surfaceMissingBaseline: {path}") |> ignore)

            builder.AppendLine($"surfaceDrifted: {List.length surface.DriftedSourcePaths}")
            |> ignore

            surface.DriftedSourcePaths
            |> List.iter (fun path -> builder.AppendLine($"surfaceDrifted: {path}") |> ignore)

            builder.AppendLine($"surfaceOrphans: {List.length surface.OrphanBaselinePaths}")
            |> ignore

            surface.OrphanBaselinePaths
            |> List.iter (fun path -> builder.AppendLine($"surfaceOrphan: {path}") |> ignore)

            builder.AppendLine($"surfaceUpdated: {List.length surface.UpdatedBaselinePaths}")
            |> ignore

            surface.UpdatedBaselinePaths
            |> List.iter (fun path -> builder.AppendLine($"surfaceUpdated: {path}") |> ignore)

            builder.AppendLine($"surfaceCoherent: {surface.IsCoherent}") |> ignore

            // Feature 087: the additive-vs-breaking classification (rich auto-derives its rows from
            // these `key: value` lines, so no bespoke rich block is needed).
            let classification = surface.Classification

            builder.AppendLine($"surfaceClassificationVerdict: {classification.Verdict}")
            |> ignore

            builder.AppendLine($"surfaceClassificationBump: {classification.RecommendedBump}")
            |> ignore

            builder.AppendLine($"surfaceClassified: {List.length classification.Entries}")
            |> ignore

            classification.Entries
            |> List.sortBy (fun entry -> entry.Path)
            |> List.iter (fun entry ->
                builder.AppendLine($"surfaceClassified: {entry.Path}={entry.Classification} ({entry.RecommendedBump})")
                |> ignore)

            // Feature 094: the coherent-set version obligation. Flat scalars, always emitted (never
            // conditionally, so the projection is a fixed shape), with `(none)` standing in for an
            // unresolved optional — which is why rich needs no bespoke block: it auto-derives its
            // rows from these `key: value` lines.
            let versionBump = surface.VersionBump
            let unresolved = "(none)"
            let currentVersion = defaultArg versionBump.CurrentVersion unresolved
            let suggestedVersion = defaultArg versionBump.SuggestedVersion unresolved

            builder.AppendLine($"surfaceVersionAxis: {versionBump.AxisFile}:{versionBump.AxisProperty}")
            |> ignore

            builder.AppendLine($"surfaceVersionAxisState: {versionBump.AxisState}")
            |> ignore

            builder.AppendLine($"surfaceVersionCurrent: {currentVersion}") |> ignore

            builder.AppendLine($"surfaceVersionRequiredBump: {versionBump.RequiredBump}")
            |> ignore

            builder.AppendLine($"surfaceVersionSuggested: {suggestedVersion}") |> ignore
        | None -> ()

        // Feature 105, Phase 2: `dependency-surface` — one `key: value` line per fact; `--rich`
        // derives its table from these lines, so no bespoke rich block is needed.
        match report.DependencySurface with
        | Some depSurface ->
            builder.AppendLine($"dependencySurfaceMode: {depSurface.Mode}") |> ignore

            builder.AppendLine($"dependencySurfaceBaselineRoot: {depSurface.BaselineRoot}")
            |> ignore

            builder.AppendLine($"dependencySurfaceChecked: {depSurface.CheckedCount}")
            |> ignore

            depSurface.Entries
            |> List.sortBy (fun entry -> entry.PackageId, entry.Version)
            |> List.iter (fun entry ->
                builder.AppendLine(
                    $"dependencySurfaceEntry: {entry.PackageId}@{entry.Version}={entry.Status} ({entry.ObservedSymbolCount} symbols)"
                )
                |> ignore)

            builder.AppendLine($"dependencySurfaceDrifted: {List.length depSurface.DriftedPackages}")
            |> ignore

            depSurface.DriftedPackages
            |> List.iter (fun id -> builder.AppendLine($"dependencySurfaceDrifted: {id}") |> ignore)

            builder.AppendLine($"dependencySurfaceUnavailable: {List.length depSurface.UnavailablePackages}")
            |> ignore

            depSurface.UnavailablePackages
            |> List.iter (fun id -> builder.AppendLine($"dependencySurfaceUnavailable: {id}") |> ignore)

            builder.AppendLine($"dependencySurfaceUpdated: {List.length depSurface.UpdatedPackages}")
            |> ignore

            depSurface.UpdatedPackages
            |> List.iter (fun id -> builder.AppendLine($"dependencySurfaceUpdated: {id}") |> ignore)

            builder.AppendLine($"dependencySurfaceCoherent: {depSurface.IsCoherent}")
            |> ignore
        | None -> ()

        match report.Upgrade with
        | Some upgrade ->
            builder.AppendLine($"upgradeHasProvenance: {upgrade.HasProvenance}") |> ignore
            builder.AppendLine($"upgradeMode: {upgrade.Mode}") |> ignore

            builder.AppendLine($"upgradeAlreadyCoherent: {upgrade.AlreadyCoherent}")
            |> ignore

            upgrade.Steps
            |> List.iter (fun step ->
                builder.AppendLine(
                    $"upgradeStep: {reconciliationStepIdValue step.StepId}={reconciliationOutcomeValue step.Outcome}"
                )
                |> ignore)

            upgrade.AppliedStepIds
            |> List.map reconciliationStepIdValue
            |> List.sort
            |> List.iter (fun id -> builder.AppendLine($"upgradeApplied: {id}") |> ignore)

            upgrade.SkippedStepIds
            |> List.map reconciliationStepIdValue
            |> List.sort
            |> List.iter (fun id -> builder.AppendLine($"upgradeSkipped: {id}") |> ignore)

            upgrade.FailedStepIds
            |> List.map reconciliationStepIdValue
            |> List.sort
            |> List.iter (fun id -> builder.AppendLine($"upgradeFailed: {id}") |> ignore)

            builder.AppendLine($"upgradeSkillDrifts: {List.length upgrade.SkillDriftPaths}")
            |> ignore

            upgrade.SkillDriftPaths
            |> List.sort
            |> List.iter (fun path -> builder.AppendLine($"upgradeSkillDrift: {path}") |> ignore)

            builder.AppendLine($"upgradeResidualDrift: {upgrade.ResidualDrift}") |> ignore
            builder.AppendLine($"upgradeNextAction: {upgrade.NextActionHint}") |> ignore
        | None -> ()

        // Feature 076: the lint block — the facts that live only in the structured summary
        // (artifact kind, outcome, and each defect's class + grammar pointer) so `--text`/`--rich`
        // carry the same facts as `--json` (FR-010).
        match report.Lint with
        | Some lint ->
            builder.AppendLine($"lintArtifact: {lint.ArtifactPath}") |> ignore
            builder.AppendLine($"lintKind: {lintArtifactKindValue lint.Kind}") |> ignore
            builder.AppendLine($"lintOutcome: {lintOutcomeValue lint.Outcome}") |> ignore
            builder.AppendLine($"lintDefects: {List.length lint.Defects}") |> ignore

            lint.Defects
            |> List.iter (fun defect ->
                let pointer =
                    match defect.GrammarPointer with
                    | Some p ->
                        match p.Section with
                        | Some section -> $" -> {p.Skill}#{section}"
                        | None -> $" -> {p.Skill}"
                    | None -> ""

                builder.AppendLine(
                    $"lintDefect: {lintDefectClassValue defect.Class} [{defect.Diagnostic.Id}] {defect.Diagnostic.Message}{pointer}"
                )
                |> ignore

                if not (System.String.IsNullOrWhiteSpace defect.Diagnostic.Correction) then
                    builder.AppendLine($"lintFix: {defect.Diagnostic.Correction}") |> ignore)
        | None -> ()

        builder.AppendLine($"generatedViews: {List.length report.GeneratedViews}")
        |> ignore

        builder.AppendLine($"diagnostics: {List.length report.Diagnostics}") |> ignore

        match report.NextAction with
        | Some action -> builder.AppendLine($"nextAction: {action.ActionId}") |> ignore
        | None -> builder.AppendLine("nextAction: none") |> ignore

        // §3.5: project help (usage, commands, flags) from the same report. The --rich
        // projection auto-derives from this plain-text projection.
        match report.Help with
        | Some help ->
            builder.AppendLine($"usage: {help.Usage}") |> ignore

            match help.Scope with
            | TopLevel -> builder.AppendLine("helpScope: topLevel") |> ignore
            | Command name -> builder.AppendLine($"helpScope: command {name}") |> ignore

            let renderFlag label (flag: HelpFlag) =
                let argument =
                    flag.Argument |> Option.map (fun value -> $" {value}") |> Option.defaultValue ""

                builder.AppendLine($"{label} {flag.Name}{argument}: {flag.Description}")
                |> ignore

            help.Commands
            |> List.iter (fun entry -> builder.AppendLine($"command {entry.Name}: {entry.Description}") |> ignore)

            help.GlobalFlags |> List.iter (renderFlag "globalFlag")
            help.CommandFlags |> List.iter (renderFlag "commandFlag")
        | None -> ()

        // Feature 084: the lifecycle-status footer is the final element of the output. Skipped for
        // the help discoverability surface (not a lifecycle step; it already drops nextAction).
        if Option.isNone report.Help then
            for line in LifecycleFooter.plainLines report do
                builder.AppendLine(line) |> ignore

        builder.ToString()
