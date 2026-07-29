namespace FS.GG.SDD.Artifacts

/// The delivered `driver-skill-manifest.json` (ADR-0014 / ADR-0054): the content-addressed
/// record of the `.github`-authored driver skills shipped in the pinned `FS.GG.Drivers`
/// package. SDD reads it (from compiled-in bytes) to learn *which* driver skills exist and,
/// per row, the `sha256` to verify a body against and the `materializes-when` predicate that
/// gates whether it is laid into a scaffold. This models the *shape* of a driver manifest —
/// never the contents of any particular one (no `.github` literal as behavior).
module DriverManifest =
    /// Which function reproduces a `DriverManifestFile.Sha256`. The two schema versions record
    /// genuinely different things in that field, so the domain travels WITH the value instead of
    /// being inferred from `TreeSha256.IsSome` — that inference was a coincidence, and the
    /// consumers that relied on it disagreed (FS-GG/FS.GG.SDD#752).
    type DriverDigestDomain =
        /// `sha256(file bytes)`, un-normalized — BOM and CR included. A schema-v2 `files[]` row.
        /// The org producer writes `hashlib.sha256(raw)` and documents it as a byte-integrity
        /// record for a materialized tree, deliberately NOT a canonical-body digest.
        | RawBytes
        /// `Fsgg.SkillMirror.sha256 body` — BOM stripped, `\r\n` folded. A schema-v1 row, whose
        /// only digest is the canonical-body `sha256` the projected `SKILL.md` row is built from.
        | CanonicalText

    /// One file in a schema-v2 driver skill directory. Paths are normalized forward-slash
    /// relative paths; `DigestDomain` says which function reproduces `Sha256`; and `Executable`
    /// is the intended execute-bit state in every materialized skill root.
    type DriverManifestFile =
        { Path: string
          Sha256: string
          DigestDomain: DriverDigestDomain
          Executable: bool }

    type DriverManifestEntry =
        {
            /// The skill id (the `<id>` of `skills/<id>/SKILL.md`).
            Id: string
            /// The declared skill class (`driver`, `operator`, …) — an opaque token (ADR-0061).
            Scope: string
            /// The canonical-body digest (CRLF→LF-normalized, lowercase hex) the delivered body
            /// must hash to (ADR-0014). Compared with `Fsgg.SkillMirror.sha256`.
            Sha256: string
            /// Schema-v2 digest of the canonical compact JSON `files` array. `None` only for a
            /// legacy schema-v1 manifest.
            TreeSha256: string option
            /// The complete closed directory transport. A schema-v2 row always has at least
            /// `SKILL.md`; schema-v1 rows are projected to that single legacy file, carrying the
            /// row's canonical-body digest and therefore `DigestDomain = CanonicalText`.
            Files: DriverManifestFile list
            /// The row's origin path in the authoring repo (informational, e.g. `.claude/skills/<id>`).
            SuppliedBy: string option
            /// The ADR-0017 predicate gating materialization (`always`, `false`, `has X and has Y`, …).
            MaterializesWhen: string
        }

    type DriverManifest =
        { SchemaVersion: int
          Skills: DriverManifestEntry list }

    /// Parse and validate a `driver-skill-manifest.json` document. Schema v2 is fail-closed:
    /// every file row, raw digest, executable flag, unique safe relative path, and the
    /// `tree-sha256` binding are required. Schema v1 remains readable as a single-SKILL.md
    /// legacy transport. Malformed rows fail the whole manifest; they are never dropped.
    val tryParse: text: string -> Result<DriverManifest, string>

/// Evaluate an ADR-0017 `materializes-when` predicate against the set of skill ids present in
/// the workspace. `Some true`/`Some false` when the predicate is a form this evaluator
/// understands (`always`, `false`, and `has <glob>` atoms joined by a single `and` **or** a
/// single `or`); `None` when it is not — the caller then fails closed (skips + advisory),
/// never defaulting to materialize (FR-004; publish-before-flip — do not guess at a shape no
/// delivered manifest carries).
module DriverPredicate =
    val evaluate: predicate: string -> presentIds: Set<string> -> bool option
