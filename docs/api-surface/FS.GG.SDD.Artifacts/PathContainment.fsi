namespace FS.GG.SDD.Artifacts

/// The single authoritative lexical path-containment guard (ADR-0002 invariant 4).
///
/// Every CLI and command-workflow site that must reject a raw `--param`/`--out`/`--root`/registry
/// path escaping the workspace root shares this one predicate. It replaces the copies that used to
/// live in `Cli.Program`, `Cli.RegistryValidate`, `Cli.RegistrySkillManifest`, and
/// `Commands.Internal.Foundation` — copies that were "comment-linked so they cannot drift" with
/// nothing enforcing it (FS-GG/FS.GG.SDD#337). Hoisting to `FS.GG.SDD.Artifacts` — referenced by
/// both the CLI and the command workflow — makes the containment logic un-duplicatable and lets a
/// direct unit test lock it against silent regression.
///
/// This is a LEXICAL guard over the raw string, NOT a filesystem containment proof: a symlink under
/// the root still resolves wherever it points. What is enforced is "no path NAMES a location outside
/// the root". Containing the effect *edge* is the one-containment-primitive work in
/// FS-GG/FS.GG.SDD#203 (ADR-0002); do not read this predicate as having already done it.
module PathContainment =
    /// Returns true when `raw` is empty/whitespace, absolute, or carries a `..` segment — i.e. it
    /// names a path that could escape the workspace root.
    ///
    /// MUST be applied to the RAW value, never a normalized one: normalization ends in
    /// `.TrimStart('/')`, which strips the leading slash *before* `Path.IsPathRooted` could see it,
    /// so a normalize-then-test predicate would let `/etc/passwd` through as `etc/passwd`.
    val escapesRoot: raw: string -> bool
