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
    ///
    /// Takes text a caller has ALREADY DECODED, and cannot see how that decoding went. A caller
    /// decoding with `File.ReadAllText` substitutes `U+FFFD` for every invalid sequence before the
    /// body arrives, so for a body that is not valid UTF-8 this digest addresses something the file
    /// does not contain and two DIFFERENT files collide under one digest (FS.GG.SDD#737). Hash from
    /// `sha256Bytes` when the raw bytes are still in hand; this spelling is unchanged and its digest
    /// is unchanged for every body that decodes.
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

    /// The files found at `(Root, Id)` — the multi-file generalization of `ActualCopy`. `None`
    /// means the root carries NO copy of the skill at all; `Some files` is the copy's whole file
    /// set, relative to `<root>/skills/<id>/`. A relative path repeated within one copy is not a
    /// drift fact (the first wins): two entries for one destination is a producer question, and
    /// `mirrorFiles` already refuses it as `DuplicateRelativePath`.
    type ActualSkillFiles =
        { Root: string
          Id: string
          Files: SkillFile list option }

    /// The drift found for ONE FILE of a skill, as the SAME three INDEPENDENT facts `SkillDrift`
    /// keeps apart — never collapsed into a per-file verdict. `MissingRoots` here ranges only over
    /// roots that DO carry the skill (a root missing the skill entirely is `MultiFileSkillDrift`'s
    /// own `MissingRoots`, a different repair).
    ///
    /// `HashMismatchRoots` means WHAT THE CALLER ASKED FOR, and the two entry points ask for
    /// different things (FS.GG.SDD#727):
    ///
    ///   - under `verifyFiles`, it is populated for `SKILL.md` ONLY, because `ExpectedSkill.Sha256`
    ///     content-addresses that body and nothing else. On every auxiliary it is empty BY
    ///     CONSTRUCTION — "no digest was available", NOT "a digest was checked and matched";
    ///   - under `verifyFileSet`, it is populated for EVERY file the v2 manifest declares, and the
    ///     absence of a declaration is itself reported rather than passed over.
    ///
    /// An empty `HashMismatchRoots` is therefore only as strong as the expectation fed in. That is
    /// exactly why the ADR-0017 manifest was amended: reading the empty list as "hash checked,
    /// clean" is the misreading, and at v1 it was unavoidable for 19 of this repo's 51 skill files.
    type SkillFileDrift =
        { RelativePath: string
          MissingRoots: string list
          Divergent: bool
          HashMismatchRoots: string list }

    /// The drift found for one multi-file skill: the roots carrying no copy at all, plus the
    /// per-FILE drift naming the offending relative path. All-clean (both lists empty) ⇒ the skill
    /// is coherent and is not returned by `verifyFiles`.
    type MultiFileSkillDrift =
        { Id: string
          Scope: SkillScope
          MissingRoots: string list
          Files: SkillFileDrift list }

    /// The verify half of `mirrorFiles`: present-in-each-root ∧ every file byte-identical across
    /// roots ∧ `SKILL.md` matching the canonical digest, over a skill's WHOLE file set. Returns
    /// only the skills exhibiting drift, sorted by id, each naming the offending files sorted by
    /// relative path. Pure, content-addressed.
    ///
    /// Files are compared over the UNION of what the present roots carry, so the comparison is
    /// symmetric — a `references/deep-detail.md` only `.codex` has is drift exactly as a
    /// `references/deep-detail.md` only `.claude` has is.
    ///
    /// A strict generalization of `verify`, which is unchanged and still the SKILL.md-only
    /// spelling: fed copies whose file set is exactly `[ { RelativePath = "SKILL.md"; Body = b } ]`
    /// (and `None` where `verify` would be given `Body = None`), `verifyFiles` reports precisely
    /// what `verify` reports — same missing roots, same divergence, same hash-mismatch roots.
    val verifyFiles:
        roots: string list -> expected: ExpectedSkill list -> actual: ActualSkillFiles list -> MultiFileSkillDrift list

    /// One expected skill and the digest its producer declares for EVERY file it carries — the
    /// ADR-0017 schema-v2 expectation (FS.GG.SDD#727), and the multi-file generalization of
    /// `ExpectedSkill`.
    ///
    /// `Files` is the COMPLETE declared file set, `SKILL.md` included. An EMPTY `Files` means "no
    /// declared file set" and is the exact analogue of `ExpectedSkill.Sha256 = ""`: hash-match is
    /// skipped entirely and only presence + cross-root identity hold. That case is real, not
    /// theoretical — a root may vendor a CO-TENANT skill from a producer whose manifest this
    /// caller does not hold, and inventing an expectation for it would be a fabricated authority.
    type ExpectedSkillFiles =
        { Id: string
          Scope: SkillScope
          Files: SkillManifestFile list }

    /// The drift found for ONE FILE against a DECLARED file set — `SkillFileDrift`'s three
    /// independent facts, unchanged in meaning, plus the fourth one a declaration makes statable.
    ///
    /// `HashMismatchRoots` here means EXACTLY what it means under `verifyFiles`: roots whose copy
    /// of a file that HAS a declared digest does not match it. It is never borrowed to report
    /// anything else.
    ///
    /// `UndeclaredRoots` is the roots carrying a file the declaration does not cover at all. It is
    /// a SEPARATE FIELD rather than a reuse of `HashMismatchRoots` because the two are different
    /// causes with different repairs — regenerate the manifest, versus restore the file — and
    /// folding them together would make one field mean two things. That is the exact defect this
    /// amendment exists to remove (at v1 an empty `HashMismatchRoots` meant "unchecked" and read as
    /// "checked and clean"), and it would be no better pointing the other way.
    ///
    /// Empty when the caller holds no declaration for the skill: with no authority, nothing can be
    /// said to lie outside it.
    type DeclaredFileDrift =
        { RelativePath: string
          MissingRoots: string list
          Divergent: bool
          HashMismatchRoots: string list
          UndeclaredRoots: string list }

    /// The drift found for one skill against a DECLARED file set — `MultiFileSkillDrift`'s shape
    /// over `DeclaredFileDrift`. All-clean (both lists empty) ⇒ the skill is coherent WITH ITS
    /// DECLARATION and is not returned by `verifyFileSet`.
    type DeclaredSkillDrift =
        { Id: string
          Scope: SkillScope
          MissingRoots: string list
          Files: DeclaredFileDrift list }

    /// `verifyFiles` with the third fact widened from `SKILL.md` to the WHOLE declared file set:
    /// present-in-each-root ∧ every file byte-identical across roots ∧ every file matching the
    /// digest its producer declared. Returns only the skills exhibiting drift, sorted by id, each
    /// naming the offending files sorted by relative path. Pure, content-addressed.
    ///
    /// The three facts stay INDEPENDENT and keep their `verifyFiles` meanings — this widens what
    /// fact 3 is computed OVER, and collapses nothing into a verdict.
    ///
    /// THE DECLARATION IS AN AUTHORITY, NOT A FILTER, and that is the whole strength gain. When
    /// `Files` is non-empty the compared set is `declared ∪ observed`, so:
    ///
    ///   - a DECLARED file absent from a root that carries the skill is `MissingRoots` — including
    ///     when it is absent from EVERY root. `verifyFiles` cannot state that: its comparison set
    ///     is the observed union, so a file deleted everywhere leaves nothing to compare and the
    ///     skill reads coherent. A surviving auxiliary masking a lost file is precisely the shape
    ///     that made a deleted skill read clean in FS.GG.SDD#726;
    ///   - an OBSERVED file the declaration does not cover is reported in `UndeclaredRoots`, its
    ///     own fact. It is NOT passed over in silence: at v2 the declared set is complete, so an
    ///     undeclared file is a finding about the tree, and a file that cannot be verified must not
    ///     read as coherent either.
    ///
    /// Fed an expectation whose `Files` is exactly `[ { RelativePath = "SKILL.md"; Sha256 = s } ]`,
    /// this reports what `verifyFiles` reports for `ExpectedSkill.Sha256 = s` on a single-file
    /// skill; fed `Files = []` it reports what `verifyFiles` reports for `Sha256 = ""`. Both
    /// equivalences are pinned in `SkillMirrorTests`.
    val verifyFileSet:
        roots: string list ->
        expected: ExpectedSkillFiles list ->
        actual: ActualSkillFiles list ->
            DeclaredSkillDrift list

    /// Why a body's RAW BYTES are not a skill body. A NAMED cause, kept in its own type rather than
    /// spelled as a bare `string option`, for the reason `MirrorRefusalReason` is: a refusal must be
    /// distinguishable from every other outcome BY CONSTRUCTION, never by a caller's convention.
    ///
    /// `NotDecodable` carries the offset — within the file, preamble included — at which the first
    /// invalid sequence begins. Deterministic: the same bytes always name the same offset. The
    /// library never names the FILE, because it never sees one; naming the file is the caller's half
    /// of the diagnostic.
    type BodyRefusalReason = NotDecodable of byteOffset: int

    /// Decode a skill body's raw bytes the way the read seam does — BOM-detecting exactly as
    /// `File.ReadAllText` does, preamble stripped — but REFUSING a body that does not decode instead
    /// of silently substituting `U+FFFD` for it (FS.GG.SDD#737, ADR-0014 §Decision 3 clause (c)).
    ///
    /// `Ok` is character-for-character what `File.ReadAllText` returns for the same bytes, so a
    /// caller that swaps its read for this one changes NO digest and needs NO manifest migration.
    /// Only the mangling case is refused. A null array is the empty body, matching `sha256`'s own
    /// null coercion.
    ///
    /// EVERY encoding the read seam can select is held to this, not UTF-8 alone: `File.ReadAllText`
    /// substitutes `U+FFFD` in a UTF-16/UTF-32 body too — on an odd byte length, an unpaired
    /// surrogate, or a scalar above `U+10FFFF` — and that is the same collision behind a BOM. What
    /// this does NOT touch is the separate FS-GG/.github#1589 disagreement about which BOMs the
    /// consuming shells strip: a WELL-FORMED UTF-16/UTF-32 body decodes here and is not refused.
    val decodeBody: bytes: byte array -> Result<string, BodyRefusalReason>

    /// `sha256` computed from RAW BYTES: the ADR-0014 §Decision 3 clause (c) digest taken from what
    /// is on disk rather than from what a caller's decoder made of it.
    ///
    /// `Ok` is byte-for-byte the digest `sha256` already produces for that body — the digest is NOT
    /// redefined, because rehashing over raw bytes would change the digest of EVERY file and force a
    /// coordinated manifest migration in every repo. `Error` is the case `sha256` cannot express at
    /// all: the bytes never decoded, so there is no body to address.
    val sha256Bytes: bytes: byte array -> Result<string, BodyRefusalReason>
