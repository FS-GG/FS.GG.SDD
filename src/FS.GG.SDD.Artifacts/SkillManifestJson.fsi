namespace FS.GG.SDD.Artifacts

open Fsgg.Schemas

/// Deterministic JSON emitter for a producer `skill-manifest` (schema v2, ADR-0014 /
/// ADR-0017; issues FS.GG.SDD#109, FS.GG.SDD#727). SDD's manifest is process-only: every entry is
/// `scope: process` and, because the fs-gg-sdd-* skills are unconditionally seeded,
/// `materializes-when` is the ADR-0017 canonical literal `always` for every entry
/// (a bare token in the gate-evaluable grammar). The shape mirrors the org's
/// consumable product producer manifest:
/// `{ schemaVersion, skills:[{ id, scope, sha256, files, resolvablePath, materializes-when }] }`,
/// entries sorted by id, 2-space indent, trailing LF — a golden/reconcilable artifact
/// `.github` regenerates `registry/skills.yml` from. `body`/`supplied-by` are omitted
/// (single-producer, no cross-producer seam).
///
/// v2 IS A SUPERSET OF v1 ON THE WIRE, DELIBERATELY (FS.GG.SDD#727). The `files` array — one
/// `{ path, sha256 }` per file of the skill, sorted by path — is ADDED; every v1 property,
/// `sha256` included, is retained with its v1 meaning (`sha256` is still the `SKILL.md` digest,
/// and is the same value `files[]`'s `SKILL.md` row carries). So a v1 consumer that tolerates
/// unknown properties reads a v2 document unchanged and correctly. What the version bump buys is
/// that a consumer which CHECKS `schemaVersion` can tell the two claims apart: at v1 the
/// auxiliaries had no declared authority, at v2 the declared set is complete.
module SkillManifestJson =

    /// Serialize a schema-v2 skill-manifest to its canonical deterministic JSON text (LF, skills
    /// sorted by id, each skill's `files` sorted by path, trailing newline).
    val serialize: manifest: SkillManifestV2 -> string
