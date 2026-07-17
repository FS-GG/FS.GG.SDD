namespace FS.GG.SDD.Commands

/// Builds SDD's process `skill-manifest` (ADR-0017 P2, issue FS.GG.SDD#109): one entry
/// per seeded fs-gg-sdd-* skill, `scope: process`, `sha256` the canonical-body digest
/// (`Fsgg.SkillMirror.sha256` — byte-equivalent to `sha256sum SKILL.md`), built from the
/// same embedded bodies `init`/`scaffold` seed. The single source of the id set stays
/// `SeededSkills.skillNames`; this is a pure projection, never a second source of truth.
/// The public entry point the CLI `registry skill-manifest` sub-verb emits from.
module ProcessSkillManifest =

    /// The process skill-manifest for the currently-seeded fs-gg-sdd-* set.
    val build: unit -> Fsgg.Schemas.SkillManifest
