namespace FS.GG.SDD.Artifacts

/// The YAML load edge for `FS-GG/.github` `registry/skills.yml`, the org's authoritative
/// skill catalog (ADR-0017). Sibling of `RegistryDocument`, which loads the *dependency*
/// registry; the two are separate documents that share a root directory and nothing else.
/// I/O lives here, never in the BCL-only `Fsgg` leaf (Constitution V).
module SkillRegistryDocument =

    /// Which registry document a file is, decided by its SHAPE (root keys) rather than
    /// its filename — the path `.github` passes is `dotgithub/registry/skills.yml`, and a
    /// filename rule would be one rename away from silently validating the wrong schema.
    type RegistryKind =
        /// A root `skills:` key — the org skill catalog.
        | SkillRegistry
        /// Everything else, INCLUDING an unreadable or malformed file. Detection is
        /// deliberately conservative: only a positive `skills:` sighting diverts from
        /// today's behaviour, so every file that validates now keeps validating the
        /// same way, and a malformed one still produces today's diagnostics.
        | DependencyRegistry

    /// Peek a registry YAML's root keys to decide which document it is. Never throws;
    /// an unreadable/malformed file is reported as `DependencyRegistry` so it flows into
    /// the existing path and yields the existing load diagnostic (Constitution VIII —
    /// a parse failure is not a content diagnostic, and detection must not invent one).
    val detectKind: path: string -> RegistryKind

    /// Parse the on-disk skill catalog into the typed `Fsgg.Registry.SkillRegistryDocument`.
    /// Never throws: a missing/unreadable file, non-YAML input, a non-mapping root, or a
    /// missing/non-integer `schemaVersion` returns `Error`. Order-preserving and tolerant
    /// of unknown/extra keys (additive registry evolution must not break it).
    ///
    /// `mirrored` is parsed THREE-STATE (absent / declared / present-but-unparseable) and
    /// is the one field here that must not be read with `Internal.boolAt`, whose
    /// `| _ -> defaultValue` arm maps BOTH an absent key AND an unparseable value onto the
    /// caller's default — the precise `absent → false` coercion the catalog's `mirrored:`
    /// field was added to close.
    val load: path: string -> Result<Fsgg.Registry.SkillRegistryDocument, RegistryDocument.RegistryLoadError>
