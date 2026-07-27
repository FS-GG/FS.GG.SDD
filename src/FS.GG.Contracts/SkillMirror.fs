namespace Fsgg

open System
open System.Security.Cryptography
open System.Text
open Fsgg.Schemas

module SkillMirror =

    let providerSourceRoot = ".agents"

    // Normalize CRLF -> LF before hashing so a byte-for-byte-logically-identical skill body
    // hashes the same regardless of a checkout's line endings. This matches
    // FS.GG.SDD.Artifacts SchemaVersion.sha256Text, so the 057/058 per-skill sha256 manifest
    // does not spuriously flag drift on a CRLF checkout of LF-authored content.
    let sha256 (body: string) : string =
        (if String.IsNullOrEmpty body then
             ""
         else
             body.Replace("\r\n", "\n"))
        |> Encoding.UTF8.GetBytes
        |> SHA256.HashData
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat ""

    let skillPath (root: string) (id: string) : string = root + "/skills/" + id + "/SKILL.md"

    // The `<id>` of a `<root>/skills/<id>/SKILL.md` path. Matches only the canonical skill-file
    // shape (a `skills` segment, then `<id>`, then a trailing `SKILL.md`); anything else is `None`.
    let skillIdOfPath (path: string) : string option =
        match path.Replace('\\', '/').Split('/') |> Array.toList |> List.rev with
        | "SKILL.md" :: id :: "skills" :: _ when id <> "" -> Some id
        | _ -> None

    let mirrorTargetRoots (roots: string list) : string list =
        roots |> List.filter (fun r -> r <> providerSourceRoot)

    let retargetSkillPath (targetRoot: string) (sourcePath: string) : string =
        let normalized = sourcePath.Replace('\\', '/')
        let prefix = providerSourceRoot + "/skills/"

        if normalized.StartsWith(prefix, StringComparison.Ordinal) then
            targetRoot + "/skills/" + normalized.Substring(prefix.Length)
        else
            normalized

    type MirrorWrite = { Path: string; Body: string }

    let mirror (roots: string list) (skills: (string * string) list) : MirrorWrite list =
        [ for (id, body) in skills |> List.sortBy fst do
              for root in roots ->
                  { Path = skillPath root id
                    Body = body } ]

    // -----------------------------------------------------------------------------------------
    // MULTI-FILE skills (FS.GG.SDD#717).
    // -----------------------------------------------------------------------------------------
    // `mirror` models a skill as `(id, body)` — one id, one body — so a skill whose canonical form
    // is `SKILL.md` + `references/**` + `agents/*.yaml` could not be expressed by the "one
    // implementation" at all (ADR-0014 §Decision 2). That is not a corner case: every kit-owned
    // coordination skill in this repository is 5-7 files, and they reach the three roots through a
    // SECOND materializer because this one could not carry them.
    //
    // Everything below is ADDITIVE. `mirror`, `skillPath`, `verify` and the rest are untouched, and
    // `mirrorFiles roots [{ Id = id; Files = [ { RelativePath = "SKILL.md"; Body = b } ] }]` returns
    // exactly what `mirror roots [ id, b ]` returns — same paths, same bodies, same order.

    type SkillFile = { RelativePath: string; Body: string }

    type MultiFileSkill = { Id: string; Files: SkillFile list }

    type MirrorRefusalReason =
        | UnsafeSkillId
        | DuplicateSkillId
        | MissingSkillFile
        | UnsafeRelativePath of relativePath: string
        | DuplicateRelativePath of relativePath: string

    type MirrorRefusal =
        { Id: string
          Reasons: MirrorRefusalReason list }

    type MirrorPlan =
        { Writes: MirrorWrite list
          Refused: MirrorRefusal list }

    let private normalizeRelative (path: string) =
        if isNull (box path) then "" else path.Replace('\\', '/')

    /// Lexical confinement: can this relative path, appended to `<root>/skills/<id>/`, name
    /// anything OUTSIDE it? Purely syntactic on purpose — the same guard class as FS.GG.SDD#185 /
    /// #337 — so it needs no filesystem and cannot be defeated by one that has not been created
    /// yet. Rejects the empty path, a rooted path, a drive/scheme-qualified path, any `.`/`..`/
    /// empty segment, and control characters.
    let private isConfinedRelativePath (raw: string) =
        let path = normalizeRelative raw

        if String.IsNullOrWhiteSpace path then
            false
        elif path.StartsWith("/", StringComparison.Ordinal) then
            false
        elif path.Contains ':' then
            false
        elif path |> Seq.exists (fun c -> c < ' ') then
            false
        else
            path.Split('/')
            |> Array.forall (fun segment -> segment <> "" && segment <> "." && segment <> "..")

    /// An id is interpolated straight into `<root>/skills/<id>/`, so it must be exactly ONE
    /// confined segment — `..` there escapes just as surely as it does in a relative path.
    let private isSafeSkillId (id: string) =
        isConfinedRelativePath id && not ((normalizeRelative id).Contains '/')

    let skillFilePath (root: string) (id: string) (relativePath: string) : string =
        root + "/skills/" + id + "/" + normalizeRelative relativePath

    // A canonical, stable order for the reasons on one refusal, so a refusal renders identically
    // every run. Whole-skill facts first, then per-file ones sorted by path.
    let private reasonRank reason =
        match reason with
        | UnsafeSkillId -> 0, ""
        | DuplicateSkillId -> 1, ""
        | MissingSkillFile -> 2, ""
        | UnsafeRelativePath path -> 3, path
        | DuplicateRelativePath path -> 4, path

    let mirrorFiles (roots: string list) (skills: MultiFileSkill list) : MirrorPlan =
        let duplicatedIds =
            skills
            |> List.countBy (fun skill -> skill.Id)
            |> List.choose (fun (id, count) -> if count > 1 then Some id else None)
            |> Set.ofList

        // Every reason a skill cannot be materialized, INDEPENDENTLY — the same shape as
        // `verify`'s missingRoots / divergent / hashMismatchRoots. A skill can be refused for
        // several distinct causes at once and every one of them is reported, because collapsing
        // them into a single verdict is what makes a report unable to explain itself.
        let reasonsFor (skill: MultiFileSkill) =
            let normalized =
                skill.Files |> List.map (fun file -> normalizeRelative file.RelativePath)

            // Duplicates are detected CASE-INSENSITIVELY, and that is deliberate. `references/A.md`
            // and `references/a.md` are two files on Linux and ONE file on macOS/Windows, so an
            // ordinal-only check would emit two writes that a case-insensitive filesystem collapses
            // into one — last body wins, silently. This library fans a skill out byte-identically
            // across roots and platforms, so a pair that cannot survive that trip is refused rather
            // than flattened. Exact duplicates report the one path they share; a case-only collision
            // reports each spelling, because naming them is what makes it fixable.
            let duplicated =
                normalized
                |> List.groupBy (fun path -> path.ToLowerInvariant())
                |> List.filter (fun (_, group) -> List.length group > 1)
                |> List.collect (fun (_, group) -> group |> List.distinct |> List.sort)
                |> List.map DuplicateRelativePath

            let unsafe =
                normalized
                |> List.filter (isConfinedRelativePath >> not)
                |> List.distinct
                |> List.map UnsafeRelativePath

            [ if not (isSafeSkillId skill.Id) then
                  UnsafeSkillId
              if Set.contains skill.Id duplicatedIds then
                  DuplicateSkillId
              // `<root>/skills/<id>/SKILL.md` is what MAKES a directory a skill — it is what
              // `skillPath` names and what `skillIdOfPath` recognises. Materializing only the
              // auxiliaries would place a directory no discovery pass can see.
              if not (normalized |> List.contains "SKILL.md") then
                  MissingSkillFile
              yield! unsafe
              yield! duplicated ]
            |> List.distinct
            |> List.sortBy reasonRank

        let evaluated = skills |> List.map (fun skill -> skill, reasonsFor skill)

        // A refused skill contributes NO writes AT ALL. Emitting its safe files and dropping the
        // unsafe one would materialize a HALF skill — silent under-materialization, which is the
        // exact failure this library exists to make impossible.
        let writes =
            [ for skill, reasons in evaluated |> List.sortBy (fun (skill, _) -> skill.Id) do
                  if List.isEmpty reasons then
                      for file in
                          skill.Files
                          |> List.map (fun file -> normalizeRelative file.RelativePath, file.Body)
                          |> List.sortBy fst do
                          for root in roots ->
                              { Path = skillFilePath root skill.Id (fst file)
                                Body = snd file } ]

        // One refusal per distinct id (a duplicated id is one subject, not two), reasons merged.
        let refused =
            evaluated
            |> List.filter (fun (_, reasons) -> not (List.isEmpty reasons))
            |> List.groupBy (fun (skill, _) -> skill.Id)
            |> List.map (fun (id, group) ->
                { Id = id
                  Reasons = group |> List.collect snd |> List.distinct |> List.sortBy reasonRank })
            |> List.sortBy (fun refusal -> refusal.Id)

        { Writes = writes; Refused = refused }

    type ExpectedSkill =
        { Id: string
          Scope: SkillScope
          Sha256: string }

    type ActualCopy =
        { Root: string
          Id: string
          Body: string option }

    type SkillDrift =
        { Id: string
          Scope: SkillScope
          MissingRoots: string list
          Divergent: bool
          HashMismatchRoots: string list }

    let verify (roots: string list) (expected: ExpectedSkill list) (actual: ActualCopy list) : SkillDrift list =
        let bodyAt =
            actual
            |> List.choose (fun copy -> copy.Body |> Option.map (fun body -> (copy.Root, copy.Id), body))
            |> Map.ofList

        expected
        |> List.sortBy (fun skill -> skill.Id)
        |> List.choose (fun skill ->
            let perRoot =
                roots |> List.map (fun root -> root, Map.tryFind (root, skill.Id) bodyAt)

            let missingRoots =
                perRoot
                |> List.choose (fun (root, body) -> if Option.isNone body then Some root else None)

            let presentBodies = perRoot |> List.choose snd

            // "byte-identical across roots": every present copy equal to the others.
            let divergent =
                match presentBodies with
                | [] -> false
                | first :: rest -> rest |> List.exists (fun body -> body <> first)

            // "matches the manifest hash": only when a reference digest is known.
            let hashMismatchRoots =
                if String.IsNullOrWhiteSpace skill.Sha256 then
                    []
                else
                    perRoot
                    |> List.choose (fun (root, body) ->
                        match body with
                        | Some content when sha256 content <> skill.Sha256 -> Some root
                        | _ -> None)

            if List.isEmpty missingRoots && not divergent && List.isEmpty hashMismatchRoots then
                None
            else
                Some
                    { Id = skill.Id
                      Scope = skill.Scope
                      MissingRoots = missingRoots
                      Divergent = divergent
                      HashMismatchRoots = hashMismatchRoots })

    // -----------------------------------------------------------------------------------------
    // MULTI-FILE verify (FS.GG.SDD#721).
    // -----------------------------------------------------------------------------------------
    // #717 made the library MATERIALIZE a multi-file skill (`mirrorFiles`) but left `verify` on the
    // `ActualCopy` model — one body per (root, id) — so the library wrote files it had no way to
    // check. `scripts/materialize-skill-roots.fsx` compensated with a hand-rolled cross-root byte
    // comparison sitting next to the library: a SECOND implementation of the verify half, which is
    // exactly what ADR-0014 §Decision 2 exists to end.
    //
    // Everything below is ADDITIVE, for the same reason #717 was: `verify`, `ActualCopy` and
    // `SkillDrift` are untouched, so every existing caller keeps its byte-for-byte call shape.
    // `verifyFiles` is a strict generalization — fed copies whose file set is exactly `SKILL.md`,
    // it reports precisely what `verify` reports (see `SkillMirrorTests`).

    type ActualSkillFiles =
        { Root: string
          Id: string
          Files: SkillFile list option }

    type SkillFileDrift =
        { RelativePath: string
          MissingRoots: string list
          Divergent: bool
          HashMismatchRoots: string list }

    type MultiFileSkillDrift =
        { Id: string
          Scope: SkillScope
          MissingRoots: string list
          Files: SkillFileDrift list }

    let verifyFiles
        (roots: string list)
        (expected: ExpectedSkill list)
        (actual: ActualSkillFiles list)
        : MultiFileSkillDrift list =
        // `(root, id)` -> that copy's files, keyed by NORMALIZED relative path. A relative path
        // repeated within one copy is not a drift fact — two entries for one destination is a
        // producer question, and `mirrorFiles` already refuses it as `DuplicateRelativePath`. The
        // first entry wins so the fold is deterministic rather than order-of-arrival.
        let filesAt =
            actual
            |> List.choose (fun copy ->
                copy.Files
                |> Option.map (fun files ->
                    (copy.Root, copy.Id),
                    files
                    |> List.map (fun file -> normalizeRelative file.RelativePath, file.Body)
                    |> List.distinctBy fst
                    |> Map.ofList))
            |> Map.ofList

        expected
        |> List.sortBy (fun skill -> skill.Id)
        |> List.choose (fun skill ->
            let perRoot =
                roots |> List.map (fun root -> root, Map.tryFind (root, skill.Id) filesAt)

            // Fact 1, at SKILL level: the root carries NO copy of this skill at all. Kept separate
            // from the per-file facts below on purpose — "the whole skill is absent from `.agents`"
            // and "`.agents`'s copy is missing one reference" are different repairs, and reporting
            // the first as N per-file absences would bury it under its own detail.
            let missingRoots =
                perRoot
                |> List.choose (fun (root, files) -> if Option.isNone files then Some root else None)

            // Only roots that HAVE the skill can be judged on its file set.
            let presentRoots =
                perRoot
                |> List.choose (fun (root, files) -> files |> Option.map (fun f -> root, f))

            // Every relative path ANY present root carries. A union, not the canonical root's set,
            // so the comparison is symmetric: a file only `.codex` has is drift just as surely as
            // one only `.claude` has, and neither root gets to define the expectation by itself.
            let fileUnion =
                presentRoots
                |> List.collect (fun (_, files) -> files |> Map.keys |> List.ofSeq)
                |> List.distinct
                |> List.sort

            let fileDrift =
                fileUnion
                |> List.choose (fun relativePath ->
                    let bodyPerRoot =
                        presentRoots
                        |> List.map (fun (root, files) -> root, Map.tryFind relativePath files)

                    // Fact 1, at FILE level: the skill is here, this file is not.
                    let fileMissingRoots =
                        bodyPerRoot
                        |> List.choose (fun (root, body) -> if Option.isNone body then Some root else None)

                    let presentBodies = bodyPerRoot |> List.choose snd

                    // Fact 2: "byte-identical across roots", per file — the invariant the driver
                    // hand-rolled, now stated once, here.
                    let divergent =
                        match presentBodies with
                        | [] -> false
                        | first :: rest -> rest |> List.exists (fun body -> body <> first)

                    // Fact 3: "matches the canonical digest". `ExpectedSkill.Sha256` content-
                    // addresses the SKILL.md body and nothing else — that is what the ADR-0017
                    // producer manifest declares — so hash-match is asserted on `SKILL.md` alone
                    // and is empty for the auxiliaries by CONSTRUCTION, not by oversight. The
                    // auxiliaries are held by presence + cross-root identity, which is the whole
                    // guarantee the manifest offers for them.
                    let hashMismatchRoots =
                        if relativePath <> "SKILL.md" || String.IsNullOrWhiteSpace skill.Sha256 then
                            []
                        else
                            bodyPerRoot
                            |> List.choose (fun (root, body) ->
                                match body with
                                | Some content when sha256 content <> skill.Sha256 -> Some root
                                | _ -> None)

                    if List.isEmpty fileMissingRoots && not divergent && List.isEmpty hashMismatchRoots then
                        None
                    else
                        Some
                            { RelativePath = relativePath
                              MissingRoots = fileMissingRoots
                              Divergent = divergent
                              HashMismatchRoots = hashMismatchRoots })

            if List.isEmpty missingRoots && List.isEmpty fileDrift then
                None
            else
                Some
                    { Id = skill.Id
                      Scope = skill.Scope
                      MissingRoots = missingRoots
                      Files = fileDrift })

    // -----------------------------------------------------------------------------------------
    // WHOLE-FILE-SET verify (FS.GG.SDD#727).
    // -----------------------------------------------------------------------------------------
    // #721 gave the library a multi-file VERIFY, but its third fact stayed pinned to `SKILL.md`,
    // because that is all `ExpectedSkill.Sha256` — and the ADR-0017 v1 manifest behind it — could
    // ever address. So `verifyFiles` reported `HashMismatchRoots = []` on every auxiliary BY
    // CONSTRUCTION, and an empty list there reads as "hash checked, clean" when it means "no hash
    // was available". Measured on this repo: 32 skills, 51 files, of which the producer manifest
    // declared a digest for 16 — every `references/**` and `agents/*.yaml` held by presence and
    // cross-root identity alone. Cross-root identity is a CONSISTENCY guarantee, not an
    // AUTHENTICITY one: three roots all materialized from one tampered producer copy are
    // byte-identical, and no digest anywhere contradicts them.
    //
    // Everything below is ADDITIVE, for the same reason #717 and #721 were: `verify`, `verifyFiles`,
    // `ExpectedSkill`, `SkillFileDrift` and `MultiFileSkillDrift` are untouched, so every existing
    // caller keeps its byte-for-byte call shape and the two spellings coexist as ONE algorithm
    // (ADR-0014 §Decision 2). It is a new expectation type and a new entry point, NOT a new field
    // on a shipped record — which would have deleted that record's positional constructor and cost
    // a coordinated MAJOR nobody authorised (docs/release/contracts-version-bump-checklist.md).

    type ExpectedSkillFiles =
        { Id: string
          Scope: SkillScope
          Files: SkillManifestFile list }

    type DeclaredFileDrift =
        { RelativePath: string
          MissingRoots: string list
          Divergent: bool
          HashMismatchRoots: string list
          UndeclaredRoots: string list }

    type DeclaredSkillDrift =
        { Id: string
          Scope: SkillScope
          MissingRoots: string list
          Files: DeclaredFileDrift list }

    let verifyFileSet
        (roots: string list)
        (expected: ExpectedSkillFiles list)
        (actual: ActualSkillFiles list)
        : DeclaredSkillDrift list =
        // Identical observation fold to `verifyFiles` — same normalization, same first-entry-wins
        // determinism — so the two entry points cannot disagree about what is ON DISK. Only the
        // EXPECTATION differs, which is the entire point of having two.
        let filesAt =
            actual
            |> List.choose (fun copy ->
                copy.Files
                |> Option.map (fun files ->
                    (copy.Root, copy.Id),
                    files
                    |> List.map (fun file -> normalizeRelative file.RelativePath, file.Body)
                    |> List.distinctBy fst
                    |> Map.ofList))
            |> Map.ofList

        expected
        |> List.sortBy (fun skill -> skill.Id)
        |> List.choose (fun skill ->
            // The declared set, normalized the same way the observation is. A relative path
            // declared twice is a producer question and not a drift fact (first wins), exactly as
            // a path repeated within one copy is.
            let declared =
                skill.Files
                |> List.map (fun file -> normalizeRelative file.RelativePath, file.Sha256)
                |> List.distinctBy fst
                |> Map.ofList

            let perRoot =
                roots |> List.map (fun root -> root, Map.tryFind (root, skill.Id) filesAt)

            let missingRoots =
                perRoot
                |> List.choose (fun (root, files) -> if Option.isNone files then Some root else None)

            let presentRoots =
                perRoot
                |> List.choose (fun (root, files) -> files |> Option.map (fun f -> root, f))

            // `declared ∪ observed`, NOT the observed union alone. This is the strength gain over
            // `verifyFiles`: a declared file that no root carries still gets a row, so a file
            // deleted from EVERY root is drift instead of nothing to compare. With an empty
            // declaration this degenerates to exactly `verifyFiles`'s observed union.
            let fileUnion =
                (declared |> Map.keys |> List.ofSeq)
                @ (presentRoots |> List.collect (fun (_, files) -> files |> Map.keys |> List.ofSeq))
                |> List.distinct
                |> List.sort

            let fileDrift =
                fileUnion
                |> List.choose (fun relativePath ->
                    let bodyPerRoot =
                        presentRoots
                        |> List.map (fun (root, files) -> root, Map.tryFind relativePath files)

                    let fileMissingRoots =
                        bodyPerRoot
                        |> List.choose (fun (root, body) -> if Option.isNone body then Some root else None)

                    let presentBodies = bodyPerRoot |> List.choose snd

                    let divergent =
                        match presentBodies with
                        | [] -> false
                        | first :: rest -> rest |> List.exists (fun body -> body <> first)

                    // Fact 3: "matches the declared digest", over the WHOLE declared set rather than
                    // `SKILL.md` alone. It means EXACTLY what it means under `verifyFiles` — roots
                    // whose copy of a file that HAS a declared digest does not match it — and it is
                    // never borrowed to say anything else.
                    let hashMismatchRoots =
                        match Map.tryFind relativePath declared with
                        | Some declaredSha when not (String.IsNullOrWhiteSpace declaredSha) ->
                            bodyPerRoot
                            |> List.choose (fun (root, body) ->
                                match body with
                                | Some content when sha256 content <> declaredSha -> Some root
                                | _ -> None)
                        // Declared with a BLANK digest: the producer named the file and declared
                        // nothing about its content. Presence and cross-root identity still hold;
                        // there is no digest to contradict. Undeclared entirely: that is fact 4's
                        // subject, not this one.
                        | _ -> []

                    // Fact 4, and the reason it is a FIELD rather than a reuse of fact 3: "this
                    // file is not in the declaration at all" and "this file's bytes contradict its
                    // declared digest" are DIFFERENT CAUSES with different repairs — regenerate the
                    // manifest versus restore the file. Reporting the first through
                    // `HashMismatchRoots` would make one field mean two things, which is precisely
                    // the defect this whole item exists to remove: at v1 an empty
                    // `HashMismatchRoots` meant "unchecked" and read as "checked and clean", and
                    // overloading it here would rebuild that ambiguity pointing the other way.
                    //
                    // Empty when the caller holds NO declaration for this skill (`Files = []`) —
                    // with no authority, nothing can be said to be outside it, and a co-tenant
                    // skill must not be flooded with findings for files nobody here declared.
                    let undeclaredRoots =
                        if Map.isEmpty declared || Map.containsKey relativePath declared then
                            []
                        else
                            bodyPerRoot
                            |> List.choose (fun (root, body) -> if Option.isSome body then Some root else None)

                    if
                        List.isEmpty fileMissingRoots
                        && not divergent
                        && List.isEmpty hashMismatchRoots
                        && List.isEmpty undeclaredRoots
                    then
                        None
                    else
                        Some
                            { RelativePath = relativePath
                              MissingRoots = fileMissingRoots
                              Divergent = divergent
                              HashMismatchRoots = hashMismatchRoots
                              UndeclaredRoots = undeclaredRoots })

            if List.isEmpty missingRoots && List.isEmpty fileDrift then
                None
            else
                Some
                    { Id = skill.Id
                      Scope = skill.Scope
                      MissingRoots = missingRoots
                      Files = fileDrift })

    // -----------------------------------------------------------------------------------------
    // THE BYTE SEAM (FS.GG.SDD#737).
    // -----------------------------------------------------------------------------------------
    // `sha256` takes text a caller has ALREADY DECODED, and every caller in the org decodes with
    // `File.ReadAllText`, whose UTF-8 decoder SUBSTITUTES U+FFFD for an invalid sequence before the
    // body ever reaches this module. So for a body that is not valid UTF-8 the digest addresses
    // something the file does not contain, and two DIFFERENT files collide under ONE digest:
    //
    //     bytes 0xFF -> U+FFFD -> 83d544ccc223c057d2bf80d3f2a32982c32c3c0db8e2674820da5064783fb097
    //     bytes 0xFE -> U+FFFD -> 83d544ccc223c057d2bf80d3f2a32982c32c3c0db8e2674820da5064783fb097
    //
    // Under ADR-0014 §Decision 3 clause (c) — "hash matches the manifest" — that is a fail-open on
    // the PRODUCING side: two distinct bodies recorded under one digest, and nothing downstream able
    // to tell them apart. The CONSUMING side fails closed (a raw-byte shell digest reports
    // `[drifted]`), which is exactly why it stayed invisible.
    //
    // THE REFUSAL CANNOT LIVE INSIDE `sha256`: by the time it is called the bytes are gone. So it
    // lives at a BYTE-level entry point beside it, and the DIGEST IS NOT REDEFINED. Rehashing over
    // raw bytes was the other candidate and is rejected: it changes the digest of every file, so
    // every recorded manifest digest in every repo would need regenerating in one coordinated act,
    // and it is a behaviour change on the published `FS.GG.Contracts` surface. Refusing costs no
    // digest change for any valid file, so no manifest migration anywhere.
    //
    // Everything here is ADDITIVE, for the same reason #717/#721/#727 were: `sha256` is untouched
    // and still the string spelling every caller holds today.
    //
    // MEASURED EXPOSURE when this landed: across all 1881 tracked files in this repository —
    // including all 103 `SKILL.md` — ZERO contain invalid UTF-8 and zero carry a UTF-16/32 BOM. The
    // single invalid-UTF-8 file is `assets/icon.png` (a PNG, never read as a skill body) and the
    // single UTF-8 BOM is on `FS.GG.SDD.sln`. The equivalent measurement in `FS-GG/.github` (766
    // files, 39 `SKILL.md`) is also zero. So the refusal turns no currently-green tree red.

    type BodyRefusalReason = NotDecodable of byteOffset: int

    // Decoders that REPORT an invalid sequence rather than papering over it. The framework's static
    // instances — `Encoding.UTF8`, `Encoding.Unicode`, `Encoding.BigEndianUnicode`, and the ones
    // `StreamReader` builds for UTF-32 — all carry a REPLACEMENT fallback, and that substitution IS
    // the defect. These carry the throwing one, and they differ from what `File.ReadAllText` uses in
    // NO other respect, so every body that decodes cleanly decodes identically.
    //
    // ALL FIVE throw, not just UTF-8. `File.ReadAllText` mangles a UTF-16/32 body too — on an odd
    // byte length, an unpaired surrogate, or a UTF-32 scalar above U+10FFFF — and produces the SAME
    // U+FFFD, so `FE FF 41` and `FE FF 42` collide under the same 83d544cc… digest as `0xFF` and
    // `0xFE` do. An earlier draft of this seam excused those branches as "detected and decoded
    // correctly, so there is nothing to refuse"; that was FALSE, and it left the exact collision this
    // module exists to close open behind a BOM. What stays out of scope is the SEPARATE
    // FS-GG/.github#1589 disagreement about which BOMs the consuming shells strip — a WELL-FORMED
    // UTF-16/32 body still decodes here and is still not refused.
    let private strictUtf8 = UTF8Encoding(false, true)
    let private strictUtf16Le = UnicodeEncoding(false, true, true) :> Encoding
    let private strictUtf16Be = UnicodeEncoding(true, true, true) :> Encoding
    let private strictUtf32Le = UTF32Encoding(false, true, true) :> Encoding
    let private strictUtf32Be = UTF32Encoding(true, true, true) :> Encoding

    // `File.ReadAllText path` is `StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks
    // = true)`, so it BOM-DETECTS before it decodes. This reproduces that detection exactly — same
    // preambles, same precedence, same lengths, including the `FF FE` UTF-16-vs-UTF-32
    // disambiguation on a byte length below 4 — because a seam that SELECTED a different encoding
    // would not be the same read: a body whose digest is recorded today would acquire a new one,
    // which is precisely the migration this change exists to avoid. Differential-tested against
    // `File.ReadAllText` on this runtime and pinned in `SkillMirrorTests`.
    //
    // UTF-8 is the default when no preamble matches, exactly as `StreamReader` leaves it.
    let private detectEncoding (bytes: byte array) : Encoding * int =
        let at index =
            if index < bytes.Length then int bytes[index] else -1

        if at 0 = 0xFE && at 1 = 0xFF then
            strictUtf16Be, 2
        elif at 0 = 0xFF && at 1 = 0xFE && at 2 = 0x00 && at 3 = 0x00 then
            strictUtf32Le, 4
        elif at 0 = 0xFF && at 1 = 0xFE then
            strictUtf16Le, 2
        elif at 0 = 0xEF && at 1 = 0xBB && at 2 = 0xBF then
            strictUtf8, 3
        elif at 0 = 0x00 && at 1 = 0x00 && at 2 = 0xFE && at 3 = 0xFF then
            strictUtf32Be, 4
        else
            strictUtf8, 0

    let decodeBody (bytes: byte array) : Result<string, BodyRefusalReason> =
        // A null array is the empty body, matching `sha256`'s own null coercion — the caller handed
        // over no bytes, which is a different thing from bytes that would not decode.
        let bytes = if isNull (box bytes) then Array.empty else bytes
        let encoding, preamble = detectEncoding bytes

        try
            Ok(encoding.GetString(bytes, preamble, bytes.Length - preamble))
        with :? DecoderFallbackException as ex ->
            // `Index` is the offset, within the block handed to the decoder, at which the first
            // invalid sequence BEGINS. Add the preamble back so the offset names a byte of the FILE
            // rather than of a slice the caller never took.
            Error(NotDecodable(preamble + ex.Index))

    // The composition is the point: decode-or-refuse, then the EXISTING digest over the EXISTING
    // string. That is what makes "byte-identical to today's digests for every body that decodes"
    // true BY CONSTRUCTION rather than by a test that could drift.
    let sha256Bytes (bytes: byte array) : Result<string, BodyRefusalReason> = decodeBody bytes |> Result.map sha256
