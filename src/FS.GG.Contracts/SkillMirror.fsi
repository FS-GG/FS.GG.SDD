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

    /// Canonical on-disk path of the file at `relativePath` INSIDE skill `id` under `root`
    /// (`<root>/skills/<id>/<relativePath>`). Backslashes are normalized to `/`.
    /// `skillFilePath root id "SKILL.md"` is exactly `skillPath root id`.
    ///
    /// A pure path builder that VALIDATES NOTHING — like `skillPath`, it will happily spell a path
    /// that escapes the skill directory if given one. `mirrorFiles` is the validating entry point;
    /// confinement is enforced there, not here.
    val skillFilePath: root: string -> id: string -> relativePath: string -> string

    /// One file of a skill: its body plus the path RELATIVE to the skill's own directory
    /// (`<root>/skills/<id>/`). `SKILL.md` is simply the relative path `"SKILL.md"`.
    type SkillFile = { RelativePath: string; Body: string }

    /// A skill as the ordered set of files it actually is on disk — `SKILL.md` plus whatever
    /// `references/**`, `agents/*.yaml` … it carries. This is what `mirror`'s `(id, body)` model
    /// could not express (FS.GG.SDD#717): the org's own coordination kit skills are 5-7 files each.
    type MultiFileSkill = { Id: string; Files: SkillFile list }

    /// Why a skill was refused. INDEPENDENT, named causes — never collapsed into one verdict, the
    /// same reason `SkillDrift` keeps `MissingRoots`/`Divergent`/`HashMismatchRoots` apart.
    /// `UnsafeSkillId`/`UnsafeRelativePath` are the lexical-confinement guard (FS.GG.SDD#185/#337):
    /// nothing may name a destination outside `<root>/skills/<id>/`. The two `Duplicate*` cases are
    /// the refusal-not-arbitration rule: two entries for one destination are two producers, and
    /// this library never picks a winner between them. `DuplicateRelativePath` is CASE-INSENSITIVE:
    /// `A.md` and `a.md` are one file on macOS/Windows, so a pair that a case-insensitive filesystem
    /// would collapse is refused rather than silently flattened.
    type MirrorRefusalReason =
        | UnsafeSkillId
        | DuplicateSkillId
        | MissingSkillFile
        | UnsafeRelativePath of relativePath: string
        | DuplicateRelativePath of relativePath: string

    /// One skill the fan-out refused, with every reason it was refused for, in a stable order.
    type MirrorRefusal =
        { Id: string
          Reasons: MirrorRefusalReason list }

    /// The plan for a multi-file fan-out: the writes to perform and the skills REFUSED, as two
    /// independent facts on one record. A refusal is reported, never thrown and never a silently
    /// dropped file, and a refused skill contributes NO writes at all — a plan that materialized a
    /// skill's safe files while dropping its unsafe one would place a HALF skill.
    type MirrorPlan =
        { Writes: MirrorWrite list
          Refused: MirrorRefusal list }

    /// Every write placing each multi-file skill into every root — one `MirrorWrite` per
    /// (file × root) at `<root>/skills/<id>/<relativePath>`. Pure and deterministic: skills sorted
    /// by id, then files by relative path, then roots in the given order.
    ///
    /// A strict generalization of `mirror`, which is unchanged and still the single-file spelling:
    /// `mirrorFiles roots [ { Id = id; Files = [ { RelativePath = "SKILL.md"; Body = b } ] } ]`
    /// yields exactly `mirror roots [ id, b ]`.
    val mirrorFiles: roots: string list -> skills: MultiFileSkill list -> MirrorPlan

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
