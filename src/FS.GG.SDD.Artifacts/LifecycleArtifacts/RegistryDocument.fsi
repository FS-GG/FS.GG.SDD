namespace FS.GG.SDD.Artifacts

module RegistryDocument =

    /// A load/parse failure: the file could not be read or parsed into the typed
    /// model. Distinct from content diagnostics (Constitution VIII) — surfaced
    /// downstream as a single `MalformedDocument`-class diagnostic, never a crash.
    type RegistryLoadError =
        { Path: string
          Message: string }

    /// Parse the on-disk registry YAML into the typed `Fsgg.Registry.RegistryDocument`.
    /// I/O lives here (not in the BCL-only Contracts leaf) — Constitution V. Never
    /// throws: a missing/unreadable file, non-YAML input, a non-mapping root, or a
    /// missing/non-integer `schemaVersion` returns `Error`. Order-preserving and
    /// tolerant of unknown/extra keys (additive registry evolution must not break it).
    val load: path: string -> Result<Fsgg.Registry.RegistryDocument, RegistryLoadError>
