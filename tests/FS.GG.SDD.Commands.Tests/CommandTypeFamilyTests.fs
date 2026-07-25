namespace FS.GG.SDD.Commands.Tests

open System
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open Xunit

module CommandTypeFamilyTests =
    let private sameType<'canonical, 'family> () =
        Assert.Equal<Type>(typeof<'canonical>, typeof<'family>)

    [<Fact>]
    let ``every family alias preserves its canonical runtime type`` () =
        sameType<SddCommand, CommandFamilies.Invocation.Command> ()
        sameType<OutputFormat, CommandFamilies.Invocation.Format> ()
        sameType<CommandOutcome, CommandFamilies.Invocation.Outcome> ()
        sameType<CommandRequest, CommandFamilies.Invocation.Request> ()

        sameType<MergePolicy, CommandFamilies.Artifacts.Policy> ()
        sameType<ArtifactWriteKind, CommandFamilies.Artifacts.WriteKind> ()
        sameType<ArtifactOperation, CommandFamilies.Artifacts.Operation> ()
        sameType<GeneratedViewCurrency, CommandFamilies.Artifacts.ViewCurrency> ()
        sameType<GeneratedViewSource, CommandFamilies.Artifacts.ViewSource> ()
        sameType<ArtifactChange, CommandFamilies.Artifacts.Change> ()
        sameType<GeneratedViewState, CommandFamilies.Artifacts.ViewState> ()

        sameType<SpecificationSummary, CommandFamilies.Lifecycle.Specification> ()
        sameType<ClarificationSummary, CommandFamilies.Lifecycle.Clarification> ()
        sameType<ChecklistSummary, CommandFamilies.Lifecycle.Checklist> ()
        sameType<PlanSummary, CommandFamilies.Lifecycle.Plan> ()
        sameType<TasksSummary, CommandFamilies.Lifecycle.Tasks> ()
        sameType<AnalysisSummary, CommandFamilies.Lifecycle.Analysis> ()
        sameType<EvidenceSummary, CommandFamilies.Lifecycle.Evidence> ()
        sameType<VerificationSummary, CommandFamilies.Lifecycle.Verification> ()
        sameType<ShipSummary, CommandFamilies.Lifecycle.Ship> ()

        sameType<GuidanceDisposition, CommandFamilies.Guidance.Disposition> ()
        sameType<AgentGuidanceFinding, CommandFamilies.Guidance.Finding> ()
        sameType<AgentGuidanceSummary, CommandFamilies.Guidance.Summary> ()
        sameType<RefreshDisposition, CommandFamilies.Guidance.RefreshState> ()
        sameType<RefreshSummary, CommandFamilies.Guidance.Refresh> ()

        sameType<ProviderInvocationResult, CommandFamilies.Scaffold.InvocationResult> ()
        sameType<ScaffoldSummary, CommandFamilies.Scaffold.Summary> ()

        sameType<ReconciliationStepId, CommandFamilies.Remediation.StepId> ()
        sameType<ReconciliationOutcome, CommandFamilies.Remediation.Outcome> ()
        sameType<ReconciliationStep, CommandFamilies.Remediation.Step> ()
        sameType<DoctorSummary, CommandFamilies.Remediation.Doctor> ()
        sameType<UpgradeSummary, CommandFamilies.Remediation.Upgrade> ()

        sameType<ClassifiedEntry, CommandFamilies.Surfaces.Entry> ()
        sameType<SurfaceClassification, CommandFamilies.Surfaces.Classification> ()
        sameType<VersionBumpPrompt, CommandFamilies.Surfaces.BumpPrompt> ()
        sameType<SurfaceSummary, CommandFamilies.Surfaces.Summary> ()
        sameType<DependencySurfaceEntry, CommandFamilies.Surfaces.DependencyEntry> ()
        sameType<DependencySurfaceSummary, CommandFamilies.Surfaces.DependencySummary> ()

        sameType<LintArtifactKind, CommandFamilies.Lint.ArtifactKind> ()
        sameType<LintDefectClass, CommandFamilies.Lint.DefectClass> ()
        sameType<LintOutcome, CommandFamilies.Lint.Outcome> ()
        sameType<GrammarPointer, CommandFamilies.Lint.Pointer> ()
        sameType<LintDefect, CommandFamilies.Lint.Defect> ()
        sameType<LintSummary, CommandFamilies.Lint.Summary> ()

        sameType<GovernanceCompatibilityFact, CommandFamilies.Reporting.GovernanceFact> ()
        sameType<NextAction, CommandFamilies.Reporting.Action> ()
        sameType<HelpFlag, CommandFamilies.Reporting.Flag> ()
        sameType<HelpCommandEntry, CommandFamilies.Reporting.HelpCommand> ()
        sameType<HelpScope, CommandFamilies.Reporting.Scope> ()
        sameType<HelpSummary, CommandFamilies.Reporting.Help> ()
        sameType<StageState, CommandFamilies.Reporting.Stage> ()
        sameType<StageEntry, CommandFamilies.Reporting.StageReport> ()
        sameType<LifecycleStatus, CommandFamilies.Reporting.Lifecycle> ()
        sameType<CommandReport, CommandFamilies.Reporting.Report> ()

        sameType<CommandEffect, CommandFamilies.Runtime.Effect> ()
        sameType<ProcessRunResult, CommandFamilies.Runtime.ProcessResult> ()
        sameType<CommandEffectResult, CommandFamilies.Runtime.EffectResult> ()
        sameType<CommandModel, CommandFamilies.Runtime.Model> ()
        sameType<CommandMsg, CommandFamilies.Runtime.Msg> ()

    [<Fact>]
    let ``family report annotation preserves serialized bytes`` () =
        let canonical: CommandReport =
            TestSupport.request Init (TestSupport.tempDirectory ())
            |> TestSupport.runRequest

        let grouped: CommandFamilies.Reporting.Report = canonical

        Assert.Equal(serializeReport canonical, serializeReport grouped)
