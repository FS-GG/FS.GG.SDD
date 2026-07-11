namespace FS.GG.SDD.Artifacts

open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion

[<AutoOpen>]
module Clarification =
    type ClarificationDecisionKind =
        | ConcreteDecision
        | AcceptedDeferral

    type ClarificationAnswerKind =
        | DecisionAnswer
        | AcceptedDeferralAnswer
        | StillOpenAnswer
        | NoteAnswer

    type ClarificationFrontMatter =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Title: string
          Stage: LifecycleStage
          ChangeTier: string
          Status: string
          SourceSpec: string
          PublicOrToolFacingImpact: bool option }

    type ClarificationQuestion =
        { QuestionId: ClarificationQuestionId
          Prompt: string
          SourceAmbiguityIds: AmbiguityId list
          Blocking: bool
          State: string
          SourceLocation: SourceLocation option }

    type ClarificationAnswer =
        { QuestionId: ClarificationQuestionId option
          AmbiguityIds: AmbiguityId list
          Text: string
          Kind: ClarificationAnswerKind
          SourceLocation: SourceLocation option }

    type ClarificationDecisionFact =
        { DecisionId: DecisionId
          Title: string
          Kind: ClarificationDecisionKind
          Text: string
          Rationale: string option
          SourceQuestionIds: ClarificationQuestionId list
          SourceAmbiguityIds: AmbiguityId list
          SourceLocation: SourceLocation option }

    type RemainingAmbiguity =
        {
            /// The ambiguity the line is *about* — its first `AMB-###`, the line's ANCHOR. Deliberately
            /// not every id the line names: a line may mention others in its prose ("AMB-001 blocked on
            /// the AMB-002 decision"), and `retireResolvedRemaining` deletes a line by its anchor. Widening
            /// this to a list would report a merely-mentioned, already-decided ambiguity as an unresolved
            /// blocker, and would falsify `remainingLineAnchor`. One subject per line.
            AmbiguityId: AmbiguityId option
            QuestionId: ClarificationQuestionId option
            State: string
            Explanation: string
            RequiredCorrection: string
            SourceLocation: SourceLocation option
        }

    type ClarificationFacts =
        { FrontMatter: ClarificationFrontMatter
          StandardSections: string list
          MissingStandardSections: string list
          Questions: ClarificationQuestion list
          Answers: ClarificationAnswer list
          Decisions: ClarificationDecisionFact list
          AcceptedDeferrals: ClarificationDecisionFact list
          RemainingAmbiguity: RemainingAmbiguity list
          BlockingAmbiguityCount: int
          Diagnostics: Diagnostic list }

    val clarificationStandardSections: unit -> string list
    val parseClarificationFacts: snapshot: FileSnapshot -> Result<ClarificationFacts, Diagnostic list>
