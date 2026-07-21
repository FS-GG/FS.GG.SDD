namespace FS.GG.SDD.Artifacts

/// The **product** `skill-manifest.json` a scaffolded product carries under its agent-skill
/// roots (`.agents/skills/skill-manifest.json`). Unlike SDD's own process-only producer manifest
/// (`SkillManifestJson`), this models a manifest that is a UNION across producers: the provider
/// template ships it (its product skills), and SDD — the sole materialize authority — must fold
/// in the skills IT lays down that the template cannot know about: the `.github`-authored driver
/// skills (`FS.GG.Drivers`) and the owner-sourced product skills (the pinned owner-skills package).
///
/// This closes ADR-0063's tail: the owner-sourced skills were re-homed OUT of the provider's static
/// manifest (they are now owner-sourced, no longer frozen in the provider) and the drivers were
/// never in it, yet `scaffold` materializes both — so the on-disk manifest under-declared them and the
/// consumer-side skill-union gate (`.github` `skill-union-assert.sh`) flagged them `[dangling]`.
/// `amend` unions them in with their content-addressed `sha256`, consistent with the
/// `scaffold-provenance.json` treatment (`driverPaths`/`gameSkillPaths`).
module ProductSkillManifest =
    /// One row of a product `skill-manifest.json`. Mirrors the shipped provider shape
    /// (`{ id, scope, sha256, resolvablePath?, materializes-when, supplied-by? }`) so a
    /// parse→amend→serialize round-trip preserves every provider row faithfully.
    type ProductManifestEntry =
        { Id: string
          Scope: string
          Sha256: string
          ResolvablePath: string option
          MaterializesWhen: string
          SuppliedBy: string option }

    /// Parse a product `skill-manifest.json`. `Error` on malformed JSON or a missing integer
    /// `schemaVersion`; a row lacking an `id` is dropped (it cannot be reconciled). `scope`
    /// defaults to `""`, `materializes-when` to the ADR-0017 canonical `always`.
    val tryParse: text: string -> Result<int * ProductManifestEntry list, string>

    /// The deterministic canonical JSON for a product manifest: entries sorted by id, 2-space
    /// indented, one trailing LF — the shape `skill-union-assert.sh` reads and the provider ships.
    val serialize: schemaVersion: int -> entries: ProductManifestEntry list -> string

    /// Union `additions` into an existing product-manifest text and re-serialize. An addition
    /// whose id is already declared is dropped (the existing declaration wins — the provider's
    /// digest and predicate are authoritative for its own skills). `None` when `existingText`
    /// does not parse: a broken provider manifest is never overwritten with a guess (fail closed).
    val amend: existingText: string -> additions: ProductManifestEntry list -> string option
