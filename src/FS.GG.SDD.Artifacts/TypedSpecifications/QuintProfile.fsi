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
    | ReachabilityProperty
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

/// A deterministic literate-source binding supplied by QuintSource for one explicit catalogue row.
/// Quint 0.32.0 typecheck output contains compiler node ids but no source coordinates, so the adapter
/// requires this separate boundary instead of manufacturing locations from unstable IR identities.
type QuintCatalogueSourceBinding =
    { ModuleName: string
      CatalogueName: string
      Id: string
      Kind: QuintCatalogueKind
      Source: QuintSourceRange }

/// One exact Quint typecheck observation plus the out-of-band bindings absent from its JSON output.
type QuintTypedEffectObservation =
    { Profile: string
      QuintVersion: string
      TypedEffectJson: string
      SourceBindings: QuintCatalogueSourceBinding list }

[<RequireQualifiedAccess>]
module QuintProfile =
    /// The only profile identity accepted by this Q2 implementation.
    val identity: string

    /// The exact Quint compiler version whose typed/effect shape the private adapter accepts.
    val quintVersion: string

    /// Validate stable public catalogue facts and return every finding in deterministic order.
    val validate: catalogue: QuintProfileCatalogue -> QuintProfileDiagnostic list

    /// Adapt exact Quint 0.32.0 `typecheck --out` JSON into stable facts without exposing raw IR.
    /// Profile/version identity and QuintSource row bindings are required out of band because the
    /// compiler payload contains none of those facts. Missing or mismatched bindings fail closed.
    val adaptTypedEffectJson:
        observation: QuintTypedEffectObservation -> Result<QuintProfileCatalogue, QuintProfileDiagnostic list>

/// Closed, recursively canonical value vocabulary exported by fsgg-quint-profile/2.
/// Function bodies and raw compiler expressions are deliberately unrepresentable.
type QuintModelValue =
    | QuintBool of bool
    | QuintInt of int64
    | QuintString of string
    | QuintTuple of QuintModelValue list
    | QuintRecord of (string * QuintModelValue) list
    | QuintVariant of tag: string * value: QuintModelValue option
    | QuintList of QuintModelValue list
    | QuintSet of QuintModelValue list
    | QuintMap of (QuintModelValue * QuintModelValue) list

/// One source-bound declaration selected for export; semantic values remain in Quint.
type QuintGeneralExportBinding =
    { Id: string
      ModuleName: string
      DeclarationName: string
      PromoteCatalogueRows: bool
      Source: QuintSourceRange }

/// One stable catalogue row promoted from an exported record carrying `id` and `kind`.
type QuintModelCatalogueEntry =
    { Id: string
      Kind: string
      ExportId: string
      Value: QuintModelValue
      Source: QuintSourceRange }

/// One accepted exported declaration and its canonical value.
type QuintGeneralExport =
    { Id: string
      ModuleName: string
      DeclarationName: string
      Value: QuintModelValue
      Source: QuintSourceRange }

/// Stable public facts projected from a consumer-defined Quint program.
type QuintGeneralProfileCatalogue =
    { Profile: string
      QuintVersion: string
      Exports: QuintGeneralExport list
      Catalogue: QuintModelCatalogueEntry list
      ActionEffects: QuintActionEffect list }

/// Exact typed/effect observation plus source-owned export and action bindings.
type QuintGeneralTypedEffectObservation =
    { Profile: string
      QuintVersion: string
      TypedEffectJson: string
      ExportBindings: QuintGeneralExportBinding list
      ActionBindings: QuintCatalogueSourceBinding list }

/// Canonical retained host facts that select exports and actions without carrying semantic values.
type QuintGeneralBindingManifest =
    { Schema: string
      Profile: string
      ModuleName: string
      Exports: QuintGeneralExportBinding list
      Actions: QuintCatalogueSourceBinding list }

[<RequireQualifiedAccess>]
module QuintGeneralBindingManifest =
    /// Stable schema for retained profile-2 selector facts.
    val schema: string

    /// Emit strict canonical JSON after validating identities and source ranges.
    val serializeCanonical: manifest: QuintGeneralBindingManifest -> Result<string, QuintProfileDiagnostic list>

    /// Decode strict canonical JSON; unknown fields and malformed selectors fail closed.
    val deserialize: text: string -> Result<QuintGeneralBindingManifest, QuintProfileDiagnostic list>

[<RequireQualifiedAccess>]
module QuintGeneralProfile =
    /// Explicit identity of the general profile; profile 1 remains frozen.
    val identity: string

    /// Exact compiler version admitted by the general profile.
    val quintVersion: string

    /// Adapt bounded exact Quint output into domain-neutral exports and effects.
    val adaptTypedEffectJson:
        observation: QuintGeneralTypedEffectObservation ->
            Result<QuintGeneralProfileCatalogue, QuintProfileDiagnostic list>
