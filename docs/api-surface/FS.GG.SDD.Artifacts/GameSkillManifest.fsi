namespace FS.GG.SDD.Artifacts

/// The delivered `skill-manifest.json` shipped in the pinned owner-skills package (ADR-0063
/// owner-repo byte source / ADR-0062 substrate / ADR-0014 verify; FS.GG.SDD#623). It is the
/// owner repo's own producer manifest — the content-addressed record of its `scope: product`
/// skills. SDD reads it (from compiled-in bytes) to learn *which* owner skills exist and, per
/// row, the `sha256` to verify a body against, whether the row's bytes are delivered here
/// (`mirrored`), and the `materializes-when` predicate that gates whether it is laid into a
/// scaffold. This models the *shape* of an owner-skill manifest — never the contents of any
/// particular one (no owner-repo literal as behaviour).
///
/// Only a `scope: product` row with `mirrored: false` carries bytes in the package; a
/// `mirrored: true` row is listed (so the manifest is the owner's single authored output) but
/// its bytes reach a scaffold through the frozen provider mirror (ADR-0022 §6), not here.
module GameSkillManifest =
    type GameSkillManifestEntry =
        {
            /// The skill id (the `<id>` of `skills/<id>/SKILL.md`).
            Id: string
            /// The declared skill class (`product`, …) — an opaque token (ADR-0061).
            Scope: string
            /// The canonical-body digest (CRLF→LF-normalized, lowercase hex) the delivered body
            /// must hash to (ADR-0014). Compared with `Fsgg.SkillMirror.sha256`.
            Sha256: string
            /// The owner's frozen-mirror obligation (ADR-0022 §6). `Some false` ⇒ no provider
            /// mirror, so this package delivers the bytes; `Some true` ⇒ delivered via the
            /// provider mirror, listed here but with no bytes; `None` ⇒ the field was absent
            /// (unclassified).
            Mirrored: bool option
            /// The row's origin path in the authoring repo (informational, e.g.
            /// `template/product-skills/<id>/`).
            SuppliedBy: string option
            /// The ADR-0017 predicate gating materialization (`always`, `<key> in [..]`, …).
            MaterializesWhen: string
        }

    type GameSkillManifest =
        { SchemaVersion: int
          Skills: GameSkillManifestEntry list }

    /// Parse an owner-skill `skill-manifest.json` document. `Error` on malformed JSON or a
    /// missing integer `schemaVersion`; a row lacking `id`/`sha256`/`materializes-when` is dropped
    /// (it cannot be safely materialized), never silently materialized.
    val tryParse: text: string -> Result<GameSkillManifest, string>

/// Evaluate an ADR-0017 `materializes-when` predicate against the effective scaffold parameter
/// set (`profile`, `lifecycle`, `feedback`, …). `Some true`/`Some false` when the predicate is a
/// form this evaluator understands (`always`, `false`, `<key> == v`, `<key> != v`,
/// `<key> in [a, b]`, joined by a single `and` **or** a single `or`); `None` when it is not — the
/// caller then fails closed (skips + advisory), never defaulting to materialize (FR-004;
/// publish-before-flip — do not guess at a shape no delivered manifest carries). A parameter the
/// map does not contain compares as the empty string, so an unset key fails an `in [..]` test.
module ProductPredicate =
    val evaluate: predicate: string -> parameters: Map<string, string> -> bool option
