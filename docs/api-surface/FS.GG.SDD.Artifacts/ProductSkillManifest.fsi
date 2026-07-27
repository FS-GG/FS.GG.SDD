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
///
/// **Schema versions (ADR-0017).** v1 is the six scalar row properties. v2 (FS.GG.SDD#727) APPENDS
/// the complete per-file digest set to each row. This codec round-trips both; a document declaring a
/// HIGHER version is refused by `amend` rather than re-emitted from the subset that parsed
/// (FS.GG.SDD#739 — the header must never assert a completeness the rows no longer carry).
module ProductSkillManifest =
    /// One file of an ADR-0017 v2 row's complete file set: the skill-relative path
    /// (`SKILL.md`, `references/deep-detail.md`, …) and its content digest, in the same
    /// digest domain the row's own `sha256` uses. Absent at v1, where the row declares
    /// `SKILL.md`'s digest and nothing else.
    type ProductManifestFile = { Path: string; Sha256: string }

    /// One row of a product `skill-manifest.json`. Mirrors the shipped provider shape
    /// (`{ id, scope, sha256, resolvablePath?, materializes-when, supplied-by?, files? }`) so a
    /// parse→amend→serialize round-trip preserves every provider row faithfully — including, at
    /// v2, every per-file digest the provider declared. `Files` is `[]` for a v1 row.
    type ProductManifestEntry =
        { Id: string
          Scope: string
          Sha256: string
          ResolvablePath: string option
          MaterializesWhen: string
          SuppliedBy: string option
          Files: ProductManifestFile list }

    /// Why `amend` declined to rewrite a manifest. Every case is a fact the caller must SAY —
    /// mapping any of them to "no manifest writes" silently is the FS.GG.SDD#739 defect, which
    /// traded a wrong document for a missing amend and told nobody.
    type AmendRefusal =
        /// `existingText` is not a readable product manifest (bad JSON, no integer
        /// `schemaVersion`, or a declared `files` array that cannot be read).
        | ManifestUnparseable of message: string
        /// The document declares a schema version newer than this codec models, so re-emitting it
        /// could drop row properties it does not know about.
        | SchemaVersionUnroundTrippable of schemaVersion: int
        /// The document declares `files` (v2+) but one or more additions carry no file set, so
        /// folding them in would yield a v2 document with v1 rows — #739 by a second route.
        | AdditionsMissingFileSet of schemaVersion: int * ids: string list

    /// Parse a product `skill-manifest.json`. `Error` on malformed JSON, a missing integer
    /// `schemaVersion`, or an unreadable `files` array; a row lacking an `id` is dropped (it cannot
    /// be reconciled). `scope` defaults to `""`, `materializes-when` to the ADR-0017 canonical
    /// `always`, `Files` to `[]`. Deliberately TOLERANT of an unknown `schemaVersion`: reading a
    /// future document to inspect it loses nothing — only rewriting it does, which is `amend`'s call.
    val tryParse: text: string -> Result<int * ProductManifestEntry list, string>

    /// The deterministic canonical JSON for a product manifest: entries sorted by id, each row's
    /// `files` sorted by path, 2-space indented, one trailing LF — the shape `skill-union-assert.sh`
    /// reads and the provider ships. `files` is emitted iff `schemaVersion` declares it (v2+), so a
    /// v1 document never grows a v2 property and a v2 document never loses one.
    val serialize: schemaVersion: int -> entries: ProductManifestEntry list -> string

    /// Union `additions` into an existing product-manifest text and re-serialize at the version the
    /// document declares. An addition whose id is already declared is dropped (the existing
    /// declaration wins — the provider's digest, predicate and file set are authoritative for its own
    /// skills). `Error` — never a silent no-op — when the manifest cannot be read, when its schema
    /// cannot be round-tripped faithfully, or when a v2 fold-in would be file-set-incomplete: a
    /// broken or un-round-trippable provider manifest is never overwritten with a guess (fail closed),
    /// and the caller is obliged to say so.
    val amend: existingText: string -> additions: ProductManifestEntry list -> Result<string, AmendRefusal>
