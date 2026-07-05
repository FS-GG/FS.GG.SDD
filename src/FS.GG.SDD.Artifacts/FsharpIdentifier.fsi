namespace FS.GG.SDD.Artifacts

/// Generic, language-level derivation of a valid F# namespace from an arbitrary
/// product name (feature 080). Pure, deterministic, culture-invariant; no provider
/// knowledge, no I/O. Used by scaffold to forward a safe identifier alongside the
/// verbatim raw name so a hyphenated/misspelled product name still compiles.
module FsharpIdentifier =

    /// Why a name cannot be derived into any F# identifier.
    type DerivationError =
        /// The name contains no character valid in an F# identifier, so no
        /// identifier can be formed (drives `scaffold.nameUnrepresentable`).
        | Unrepresentable of name: string

    /// Derive a valid F# *namespace* from an arbitrary product name.
    ///
    /// Dots delimit namespace segments and are preserved; each segment is derived
    /// independently by dropping characters invalid in an F# identifier (keeping
    /// Unicode letters, digits, and `_`), prefixing `_` when a segment would begin
    /// with a digit, and suffixing `_` when a segment collides with an F# reserved
    /// keyword. Empty-derived segments are collapsed. Deterministic and ordinal; a
    /// no-op on inputs already valid as F# namespaces. `Error (Unrepresentable name)`
    /// iff the whole name reduces to no identifier at all.
    val deriveNamespace: name: string -> Result<string, DerivationError>
