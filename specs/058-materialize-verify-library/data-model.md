# Data model: One materialize-and-verify library (P1)

The library lives in `Fsgg.SkillMirror` (`src/FS.GG.Contracts/SkillMirror.fs` + `.fsi`), BCL-only,
built on the 057 types (`Fsgg.Schemas.SkillScope`, `SkillManifestEntry`, `agentSkillRoots`).

## SkillMirror — helpers

```fsharp
/// The one root a provider owns in the orchestrated lane (ADR-0014 §Decision 6); a provider
/// skill's canonical body lives here and is copied INTO the other roots.
let providerSourceRoot : string = ".agents"

/// Lowercase-hex SHA256 of a skill body's UTF-8 bytes. Pure, BCL-only.
let sha256 (body: string) : string

/// Canonical on-disk path of skill `id` under `root` (`<root>/skills/<id>/SKILL.md`).
let skillPath (root: string) (id: string) : string

/// The `<id>` of a `<root>/skills/<id>/SKILL.md` path, or None if the path is not a skill file.
let skillIdOfPath (path: string) : string option

/// The roots a provider copy is fanned INTO — every root except `providerSourceRoot`.
let mirrorTargetRoots (roots: string list) : string list

/// Rewrite a `providerSourceRoot`-relative skill path into the same tail under `targetRoot`
/// (`.agents/skills/REST` → `targetRoot + "/skills/REST"`), verbatim.
let retargetSkillPath (targetRoot: string) (sourcePath: string) : string
```

## SkillMirror — mirror

```fsharp
/// One concrete (path, body) write the fan-out materializes.
type MirrorWrite = { Path: string; Body: string }

/// Every write placing each (id, body) skill into every root — one per (skill × root).
/// Pure, deterministic (skills sorted by id, roots in given order).
let mirror (roots: string list) (skills: (string * string) list) : MirrorWrite list
```

Seeded fan-out calls `mirror agentSkillRoots pairs`; the caller wraps each `MirrorWrite` in a
`WriteFile(_, _, AgentGuidanceTarget)` effect. The scaffold/refresh provider mirror uses
`retargetSkillPath` over `mirrorTargetRoots agentSkillRoots` (source bodies come from `ReadFile`).

## SkillMirror — verify

```fsharp
/// One expected skill and the canonical digest each present copy must match. An empty `Sha256`
/// means "no reference digest" — hash-match is skipped, presence and cross-root identity still hold.
type ExpectedSkill = { Id: string; Scope: SkillScope; Sha256: string }

/// The body found at (Root, Id), or None when that copy is absent.
type ActualCopy = { Root: string; Id: string; Body: string option }

/// The drift found for one skill. All-empty / false ⇒ coherent (the skill is not returned).
type SkillDrift =
    { Id: string
      Scope: SkillScope
      MissingRoots: string list        // roots lacking the copy (present-in-each-root fails)
      Divergent: bool                  // present copies not all byte-identical (across-roots fails)
      HashMismatchRoots: string list } // present roots whose sha256 ≠ expected (matches-hash fails)

/// For every expected skill: present-in-each-root ∧ byte-identical-across-roots ∧ matches-hash.
/// Returns only skills exhibiting drift, sorted by id. Pure, content-addressed.
let verify (roots: string list) (expected: ExpectedSkill list) (actual: ActualCopy list) : SkillDrift list
```

The three ADR-0014 §Decision 3 checks map one-to-one: `MissingRoots` = "present in each root",
`Divergent` = "byte-identical across roots", `HashMismatchRoots` = "matches the manifest hash".

## DriftReport / DoctorSummary / UpgradeSummary — additive field

```fsharp
// gains, after MissingArtifactPaths:
SkillDriftPaths: string list   // concrete drifted root/skill paths (missing ∪ divergent ∪ mismatch), sorted
```

`IsCoherent` becomes false when `SkillDriftPaths` is non-empty (in addition to the existing CLI-axis
/ missing-artifact drivers). Serialized additively after `missingArtifactPaths` in the doctor/upgrade
report blocks.

## Provenance digest population

`ScaffoldProducedPath.Sha256` (added in 057) is populated for skill copies:

- produced `.agents/skills/<id>/SKILL.md` → `Some (SkillMirror.sha256 body)` (the read canonical body);
- mirrored `.claude`/`.codex` copies → `Some` the same digest (byte-identical copy);
- non-skill produced paths (app source) → `None` (unchanged).

`scaffoldProvenanceVersion` stays `1`; serialization emits `sha256` only when `Some`.
