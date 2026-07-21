namespace FS.GG.SDD.Artifacts

/// The delivered `driver-skill-manifest.json` (ADR-0014 / ADR-0054): the content-addressed
/// record of the `.github`-authored driver skills shipped in the pinned `FS.GG.Drivers`
/// package. SDD reads it (from compiled-in bytes) to learn *which* driver skills exist and,
/// per row, the `sha256` to verify a body against and the `materializes-when` predicate that
/// gates whether it is laid into a scaffold. This models the *shape* of a driver manifest —
/// never the contents of any particular one (no `.github` literal as behavior).
module DriverManifest =
    type DriverManifestEntry =
        {
            /// The skill id (the `<id>` of `skills/<id>/SKILL.md`).
            Id: string
            /// The declared skill class (`driver`, `operator`, …) — an opaque token (ADR-0061).
            Scope: string
            /// The canonical-body digest (CRLF→LF-normalized, lowercase hex) the delivered body
            /// must hash to (ADR-0014). Compared with `Fsgg.SkillMirror.sha256`.
            Sha256: string
            /// The row's origin path in the authoring repo (informational, e.g. `.claude/skills/<id>`).
            SuppliedBy: string option
            /// The ADR-0017 predicate gating materialization (`always`, `false`, `has X and has Y`, …).
            MaterializesWhen: string
        }

    type DriverManifest =
        { SchemaVersion: int
          Skills: DriverManifestEntry list }

    /// Parse a `driver-skill-manifest.json` document. `Error` on malformed JSON or a missing
    /// integer `schemaVersion`; a row lacking `id`/`sha256`/`materializes-when` is dropped
    /// (it cannot be safely materialized), never silently materialized.
    val tryParse: text: string -> Result<DriverManifest, string>

/// Evaluate an ADR-0017 `materializes-when` predicate against the set of skill ids present in
/// the workspace. `Some true`/`Some false` when the predicate is a form this evaluator
/// understands (`always`, `false`, and `has <glob>` atoms joined by a single `and` **or** a
/// single `or`); `None` when it is not — the caller then fails closed (skips + advisory),
/// never defaulting to materialize (FR-004; publish-before-flip — do not guess at a shape no
/// delivered manifest carries).
module DriverPredicate =
    val evaluate: predicate: string -> presentIds: Set<string> -> bool option
