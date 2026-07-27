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
