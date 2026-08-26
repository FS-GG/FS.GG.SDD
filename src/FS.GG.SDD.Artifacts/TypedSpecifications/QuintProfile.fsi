namespace FS.GG.SDD.Artifacts.TypedSpecifications

/// A stable source coordinate in the canonical literate Markdown document.
type QuintSourcePosition = { Line: int; Column: int }

/// An inclusive stable source range; compiler node ids and host paths are deliberately absent.
type QuintSourceRange =
    { Path: string
      Start: QuintSourcePosition
      End: QuintSourcePosition }

/// The closed set of catalogue rows admitted by fsgg-quint-profile/1.
type QuintCatalogueKind =
    | Requirement
    | StateVariable
    | Action
    | Invariant
    | TemporalProperty
    | Evidence
    | Implementation
    | ExternalSubject

/// A stable, explicitly authored catalogue identity and its source binding.
type QuintCatalogueEntry =
    { Id: string
      Kind: QuintCatalogueKind
      Source: QuintSourceRange }

/// Reads, writes, and subjects projected from a profile-accepted action.
type QuintActionEffect =
    { ActionId: string
      Reads: string list
      Writes: string list
      Subjects: string list }

/// Public stable facts projected from the private Quint 0.32 typed/effect adapter.
type QuintProfileCatalogue =
    { Profile: string
      QuintVersion: string
      Entries: QuintCatalogueEntry list
      ActionEffects: QuintActionEffect list }

/// One deterministic profile refusal with an optional literate source binding.
type QuintProfileDiagnostic =
    { Code: string
      Path: string
      Message: string
      Correction: string
      Source: QuintSourceRange option }

[<RequireQualifiedAccess>]
module QuintProfile =
    /// The only profile identity accepted by this Q2 implementation.
    val identity: string

    /// The exact Quint compiler version whose typed/effect shape the private adapter accepts.
    val quintVersion: string

    /// Validate stable public catalogue facts and return every finding in deterministic order.
    val validate: catalogue: QuintProfileCatalogue -> QuintProfileDiagnostic list

    /// Adapt exact Quint 0.32 typed/effect JSON into stable facts without exposing raw IR.
    val adaptTypedEffectJson:
        canonicalSourcePath: string -> typedEffectJson: string -> Result<QuintProfileCatalogue, QuintProfileDiagnostic list>
