namespace FS.GG.SDD.Artifacts

open Fsgg.Schemas

/// Deterministic JSON emitter for a producer `skill-manifest` (schema v1, ADR-0014 /
/// ADR-0017; issue FS.GG.SDD#109). SDD's manifest is process-only: every entry is
/// `scope: process` and, because the fs-gg-sdd-* skills are unconditionally seeded,
/// `materializes-when` is the ADR-0017 canonical literal `always` for every entry
/// (a bare token in the gate-evaluable grammar). The shape mirrors the org's
/// consumable product producer manifest:
/// `{ schemaVersion, skills:[{ id, scope, sha256, resolvablePath, materializes-when }] }`,
/// entries sorted by id, 2-space indent, trailing LF — a golden/reconcilable artifact
/// `.github` regenerates `registry/skills.yml` from. `body`/`supplied-by` are omitted
/// (single-producer, no cross-producer seam).
module SkillManifestJson =

    /// Serialize a skill-manifest to its canonical deterministic JSON text (LF, sorted
    /// by id, trailing newline).
    val serialize: manifest: SkillManifest -> string
