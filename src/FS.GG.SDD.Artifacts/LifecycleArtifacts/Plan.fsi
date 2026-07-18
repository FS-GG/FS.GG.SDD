namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

[<AutoOpen>]
module Plan =
    type PlanFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          PublicOrToolFacingImpact: bool option }

    type PlanSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type PlanDecision =
        { DecisionId: PlanDecisionId
          Title: string
          Status: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanContractReference =
        { ContractId: PlanContractReferenceId
          Kind: string
          Target: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type VerificationObligation =
        { ObligationId: VerificationObligationId
          Title: string
          EvidenceKind: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanMigrationNote =
        { MigrationId: PlanMigrationNoteId
          Posture: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type GeneratedViewImpact =
        { ImpactId: GeneratedViewImpactId
          Target: string
          CurrencyBehavior: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type AcceptedPlanDeferral =
        { Id: string
          Text: string
          SourceIds: string list
          SourceLocation: SourceLocation option }

    /// FS.GG.SDD#569 (feature 105): whether a framework-API reference is a USE (Contract Impact) or an
    /// ABSENCE claim (`blocked-on-framework:` on a deferral). See ADR-0004 D3.
    type FrameworkReferenceKind =
        | FrameworkUse
        | FrameworkBlockedOn

    /// FS.GG.SDD#569 (feature 105): a structured framework-API reference
    /// `<PackageId>[@<version>]#<symbol>`. `Version = None` means the pinned package version applies.
    type FrameworkApiReference =
        { PackageId: string
          Version: string option
          Symbol: string
          Kind: FrameworkReferenceKind
          SourceIds: string list
          SourceLocation: SourceLocation option }

    type PlanFacts =
        { FrontMatter: PlanFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          SourceSnapshots: PlanSourceSnapshot list
          Decisions: PlanDecision list
          ContractReferences: PlanContractReference list
          VerificationObligations: VerificationObligation list
          MigrationNotes: PlanMigrationNote list
          GeneratedViewImpacts: GeneratedViewImpact list
          AcceptedDeferrals: AcceptedPlanDeferral list
          FrameworkApiReferences: FrameworkApiReference list
          BlockingFindings: string list
          AdvisoryNotes: string list
          LifecycleNotes: string list
          StaleDecisionCount: int
          Diagnostics: Diagnostic list }

    val planStandardSections: unit -> string list
    val parsePlanFacts: snapshot: FileSnapshot -> Result<PlanFacts, Diagnostic list>
