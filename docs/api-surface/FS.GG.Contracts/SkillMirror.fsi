namespace Fsgg

/// The one materialize-and-verify algorithm for agent-skill roots (ADR-0014 §Decision 2/3/5),
/// consumed by every SDD lane (`scaffold`/`refresh`/`doctor`/`upgrade`) and vendored byte-for-byte
/// by the standalone lane. Pure and BCL-only: `mirror` computes the (path, body) writes that place a
/// skill union into every root; `verify` asserts the three-root invariant (present in each root ∧
/// byte-identical across roots ∧ matches the canonical hash). Every destination derives from the
/// caller-supplied root set (`Fsgg.Schemas.agentSkillRoots`); the module hardcodes no root except
/// `providerSourceRoot`, itself the ADR §Decision 6 provider-confinement invariant.
module SkillMirror =

    open Fsgg.Schemas

    /// The one root a provider owns in the orchestrated lane (ADR-0014 §Decision 6); a provider
    /// skill's canonical body lives here and is copied INTO the other roots.
    val providerSourceRoot: string

    /// Lowercase-hex SHA256 of a skill body's UTF-8 bytes. Pure, BCL-only.
    val sha256: body: string -> string

    /// Canonical on-disk path of skill `id` under `root` (`<root>/skills/<id>/SKILL.md`).
    val skillPath: root: string -> id: string -> string

    /// The `<id>` of a `<root>/skills/<id>/SKILL.md` path (any root), or `None` when the path is
    /// not a `skills/<id>/SKILL.md` skill file.
    val skillIdOfPath: path: string -> string option

    /// The roots a provider copy is fanned INTO — every root except `providerSourceRoot`.
    val mirrorTargetRoots: roots: string list -> string list

    /// Rewrite a `providerSourceRoot`-relative skill path into the same tail under `targetRoot`
    /// (`.agents/skills/REST` → `targetRoot + "/skills/REST"`), verbatim.
    val retargetSkillPath: targetRoot: string -> sourcePath: string -> string

    /// One concrete (path, body) write the fan-out materializes.
    type MirrorWrite = { Path: string; Body: string }

    /// Every write placing each `(id, body)` skill into every root — one per (skill × root) at
    /// `<root>/skills/<id>/SKILL.md`. Pure and deterministic (skills sorted by id, roots in order).
    val mirror: roots: string list -> skills: (string * string) list -> MirrorWrite list

    /// One expected skill and the canonical digest each present copy must match. An empty `Sha256`
    /// means "no reference digest" — hash-match is skipped; presence and cross-root identity hold.
    type ExpectedSkill =
        { Id: string
          Scope: SkillScope
          Sha256: string }

    /// The body found at `(Root, Id)`, or `None` when that copy is absent.
    type ActualCopy =
        { Root: string
          Id: string
          Body: string option }

    /// The drift found for one skill. All-clean (`MissingRoots`/`HashMismatchRoots` empty and
    /// `Divergent` false) ⇒ the skill is coherent and is not returned by `verify`.
    type SkillDrift =
        { Id: string
          Scope: SkillScope
          MissingRoots: string list
          Divergent: bool
          HashMismatchRoots: string list }

    /// For every expected skill: present-in-each-root ∧ byte-identical-across-roots ∧ matches-hash.
    /// Returns only the skills exhibiting drift, sorted by id. Pure, content-addressed.
    val verify: roots: string list -> expected: ExpectedSkill list -> actual: ActualCopy list -> SkillDrift list
