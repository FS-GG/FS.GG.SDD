namespace FS.GG.SDD.Cli

/// `fsgg-sdd registry skill-manifest` — emit / check SDD's process skill-manifest
/// (ADR-0017 P2, issue FS.GG.SDD#109). Peer of `registry validate`, dispatched before
/// the lifecycle `parseCommand`/`CommandReport` contracts, so those stay untouched.
///
/// Modes (deterministic; exit code carries the verdict):
///   bare      → print the canonical manifest JSON to stdout (the automation contract).
///   --write   → (re)write the committed `.agents/skills/skill-manifest.json`.
///   --check   → regenerate in memory and compare to the committed file; exit 0 iff
///               byte-identical, else non-zero with a hint (the CI/drift-guard mode).
/// `--root <dir>` selects the product root (default `.`).
module RegistrySkillManifest =

    /// The canonical committed path of SDD's process producer manifest.
    val manifestPath: string

    val run: args: string list -> int
