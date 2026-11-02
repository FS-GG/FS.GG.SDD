namespace FS.GG.SDD.Commands.Internal

open System
open FS.GG.SDD.Commands
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ScaffoldProvenance

/// FS.GG.SDD#1155: replacing an owner-sourced skill that changed upstream, as ONE UNIT, in a
/// workspace that already has it.
///
/// Before this, `upgrade` only ever filled in MISSING owner-skill files (no-clobber), so a skill
/// whose upstream text changed stayed stale in every existing workspace forever, including the
/// seed archive inside the `atlas` driver skill. This module is the pure decision of when that
/// replacement is safe. It performs no I/O; it consumes the bodies already read for `doctor`.
///
/// A skill is replaced only when ALL of the following hold, judged over every file of the skill in
/// every declared runtime root together (the `.claude` and `.agents` copies are one unit):
///
///   1. it changed upstream: some desired file's digest differs from the recorded one, a desired
///      file is not recorded, or a recorded file is no longer desired;
///   2. it was not edited locally: every recorded file carries a digest (a MISSING recorded digest
///      counts as edited), every present recorded file still matches that digest, and no desired
///      path is occupied by a file the record does not attest;
///   3. every file was readable;
///   4. it would not DOWNGRADE: the embedded owner package is strictly newer than the version the
///      skill was last materialized from (or, for a skill with no recorded version, the pack is not
///      below the highest version recorded for its channel and the running CLI is newer than the CLI
///      that last wrote the workspace, or equal to it with a complete upgrade by it recorded).
///
/// Anything else is HELD, never overwritten, and keeps today's behavior and warnings.
module internal OwnerSkillRefresh =
    /// Split `<root>/skills/<id>/<relative>` for one of the declared roots. The one parser shared
    /// by drift, the body collector and this module.
    let skillCopyOf (path: string) : (string * string * string) option =
        let normalized = path.Replace('\\', '/')

        let isConfinedSegment segment =
            segment <> "" && segment <> "." && segment <> ".."

        Fsgg.Schemas.agentSkillRoots
        |> List.tryPick (fun root ->
            let prefix = root + "/skills/"

            if normalized.StartsWith(prefix, StringComparison.Ordinal) then
                let rest = normalized.Substring(prefix.Length)

                match rest.IndexOf '/' with
                | -1 -> None
                | cut ->
                    let id = rest.Substring(0, cut)
                    let relative = rest.Substring(cut + 1)

                    if isConfinedSegment id && relative.Split('/') |> Array.forall isConfinedSegment then
                        Some(root, id, relative)
                    else
                        None
            else
                None)

    /// One file the embedded, content-verified owner plan wants a skill to have.
    type DesiredFile =
        {
            Path: string
            /// `Fsgg.SkillMirror.sha256` of `Text`, the digest domain provenance records.
            Sha256: string
            Text: string
            Executable: bool
            Owner: ArtifactOwner
        }

    type HeldReason =
        /// At least one file differs from, lacks, or is not attested by its recorded digest.
        | LocallyEdited
        /// The embedded owner package is OLDER than the one the skill was last materialized from.
        | WouldDowngrade of recorded: string * embedded: string
        /// Newer-ness cannot be proved: no embedded version, a changed package, an equal version
        /// with different bytes, or a legacy skill whose CLI is not newer than its last writer.
        | VersionUnprovable of detail: string
        /// A file of the skill could not be read, so no verdict over the whole unit is possible.
        | Unreadable
        /// A skill of the same name already exists in the workspace and is not the one SDD delivers
        /// (these are the paths whose bytes differ from the pack's). Nothing is installed around it.
        | NameCollision of paths: string list

    type HeldSkill = { SkillId: string; Reason: HeldReason }

    type SkillRefresh =
        {
            SkillId: string
            Owner: ArtifactOwner
            /// Every desired file of the skill, in every root: the unit is replaced together.
            Writes: DesiredFile list
            /// For each write, the digest the destination must still have for the write to be
            /// allowed (`""` means it must be absent). Re-checked at the write edge.
            ExpectedBefore: Map<string, string>
            /// Recorded files that disappeared upstream and are still on disk, unedited: removed
            /// together with the rest, as `(path, recordedSha256)`.
            Removals: (string * string) list
            /// Recorded files that disappeared upstream and are already absent (only the record row
            /// has to go).
            Dropped: string list
            /// Desired files whose record row is missing or differs from the pack's digest. They are
            /// targets even when the disk already holds the pack's bytes, so an interrupted run's
            /// record is completed on the next one.
            Stale: string list
            /// The `(channel, version)` the skill will be recorded as materialized from.
            Version: (string * string) option
        }

    type Plan =
        {
            Refreshed: SkillRefresh list
            Held: HeldSkill list
        }

    let empty = { Refreshed = []; Held = [] }

    let private ownerRows (record: ScaffoldProvenanceRecord) =
        record.DriverPaths @ record.GameSkillPaths @ record.RenderingSkillPaths

    /// Skill ids that some other authority owns (provider-produced or mirrored copies): those are
    /// never refreshed from a pack even if an owner row also names the id.
    let private foreignIds (record: ScaffoldProvenanceRecord) =
        record.ProducedPaths @ record.MirroredPaths @ record.SddOwnedPaths
        |> List.choose (fun row -> skillCopyOf row.Path |> Option.map (fun (_, id, _) -> id))
        |> Set.ofList

    let private versionVerdict
        (record: ScaffoldProvenanceRecord)
        (cliVersion: string)
        (embeddedFor: ArtifactOwner -> (string * string) option)
        (id: string)
        (owner: ArtifactOwner)
        : Result<(string * string) option, HeldReason> =
        match embeddedFor owner with
        | None -> Error(VersionUnprovable "this CLI build records no version for the owner package")
        // A record that claims version tracking but carries an entry nobody can read is not a
        // legacy record: it is unprovable, and holds, rather than quietly taking the weaker bound.
        | Some _ when record.OwnerSkillVersions |> List.exists (fun entry -> entry.SkillId = "") ->
            Error(VersionUnprovable "the record's ownerSkillVersions has a malformed entry")
        | Some(package, version) ->
            match record.OwnerSkillVersions |> List.tryFind (fun entry -> entry.SkillId = id) with
            | Some recorded when recorded.Channel = "" || recorded.Version = "" ->
                Error(VersionUnprovable "the record's version entry for this skill is malformed")
            | Some recorded when recorded.Channel <> package ->
                Error(
                    VersionUnprovable
                        $"the skill was materialized from channel {recorded.Channel}, not the embedded {package}"
                )
            | Some recorded ->
                match CommandVersion.compare version recorded.Version with
                | Some c when c > 0 -> Ok(Some(package, version))
                | Some c when c < 0 -> Error(WouldDowngrade(recorded.Version, version))
                | Some _ ->
                    Error(
                        VersionUnprovable $"the embedded package is the recorded version {version} but its bytes differ"
                    )
                | None -> Error(VersionUnprovable $"version {recorded.Version} or {version} is not comparable")
            | None ->
                // A record with no version for THIS skill (it predates version tracking, or the skill was
                // never written by a versioning `upgrade`). Two bounds, both required.
                //
                // 1. The channel floor, applied generally: no pack older than the highest version this
                //    record already holds for the channel. This is a fact about the workspace, not about
                //    which CLI is running, so it also stops a DEV build whose version string equals the
                //    last writer's but whose embedded pack is older.
                // 2. The writer bound. `Generator` is the CLI that PRODUCED the workspace and never moves;
                //    `UpgradedBy` is the CLI of the last upgrade in which every file landed. A CLI strictly
                //    NEWER than that writer carries a pack at least as new as the one it wrote. An EQUAL
                //    version is allowed only when `UpgradedBy` names this very CLI: then a complete run of
                //    this release already vouched for the workspace, and this run is the one that finishes
                //    a refresh an edit, a link or a read-only directory had blocked. An equal version with
                //    no such run is not proof of anything (two builds can share a version string), so it
                //    holds.
                let belowFloor =
                    record.OwnerSkillVersions
                    |> List.tryPick (fun entry ->
                        if entry.Channel = package && entry.Version <> "" then
                            match CommandVersion.compare version entry.Version with
                            | Some c when c < 0 -> Some(Error(WouldDowngrade(entry.Version, version)))
                            | None ->
                                Some(
                                    Error(VersionUnprovable $"version {entry.Version} or {version} is not comparable")
                                )
                            | _ -> None
                        else
                            None)

                match belowFloor with
                | Some verdict -> verdict
                | None ->
                    let lastWriter = defaultArg record.UpgradedBy record.Generator.Version

                    match CommandVersion.compare cliVersion lastWriter with
                    | Some c when c > 0 -> Ok(Some(package, version))
                    | Some 0 when record.UpgradedBy = Some cliVersion -> Ok(Some(package, version))
                    | _ ->
                        Error(
                            VersionUnprovable
                                $"the record carries no package version and CLI {cliVersion} is not newer than the {lastWriter} that last wrote it, and no complete upgrade by this CLI is recorded"
                        )

    /// Decide, per recorded owner-sourced skill, whether it is replaced as a unit or held.
    ///
    /// `bodies` maps each on-disk skill-copy path to its decoded text (the same map `doctor` reads);
    /// a path absent from it is absent on disk. `unobserved` are paths that could not be read.
    let plan
        (record: ScaffoldProvenanceRecord)
        (cliVersion: string)
        (desired: DesiredFile list)
        (embeddedFor: ArtifactOwner -> (string * string) option)
        (bodies: Map<string, string>)
        (unobserved: Set<string>)
        : Plan =
        let foreign = foreignIds record
        let desiredByPath = desired |> List.map (fun file -> file.Path, file) |> Map.ofList

        let recordedById =
            ownerRows record
            |> List.choose (fun row -> skillCopyOf row.Path |> Option.map (fun (_, id, _) -> id, row))
            |> List.groupBy fst
            |> List.map (fun (id, rows) -> id, rows |> List.map snd)

        let decide (id: string) (rows: ScaffoldProducedPath list) : Choice<SkillRefresh, HeldSkill> option =
            let owner = (List.head rows).Owner

            let desiredFiles =
                desired
                |> List.filter (fun file -> skillCopyOf file.Path |> Option.exists (fun (_, i, _) -> i = id))

            let recorded = rows |> List.map (fun row -> row.Path, row.Sha256) |> Map.ofList

            // A skill the embedded pack no longer ships AT ALL (or could not verify, or whose
            // predicate no longer holds) has no desired file: that is not "files removed from a
            // still-shipped manifest", and nothing here may delete a whole skill on such a signal.
            let changed =
                not (List.isEmpty desiredFiles)
                && ((desiredFiles
                     |> List.exists (fun file ->
                         match Map.tryFind file.Path recorded with
                         | Some(Some sha) -> sha <> file.Sha256
                         | _ -> true))
                    || (recorded |> Map.exists (fun path _ -> not (Map.containsKey path desiredByPath))))

            // Catalogue/source provenance (schema 2/3) records RAW tagged digests (`sha256:...`) over exact
            // bytes: a different domain from the canonical one the pack digests are in, so the two cannot
            // be compared, and that wrapper cannot be rewritten by `upgrade` anyway (see
            // `ownerBackfillEffects`). Such a skill is not a refresh candidate; the existing behavior
            // for that provenance applies unchanged.
            let rawDomain =
                recorded
                |> Map.exists (fun _ sha ->
                    sha
                    |> Option.exists (fun value -> value.StartsWith("sha256:", StringComparison.Ordinal)))

            if not changed || rawDomain then
                None
            else
                let touched =
                    Set.union
                        (recorded |> Map.toList |> List.map fst |> Set.ofList)
                        (desiredFiles |> List.map (fun file -> file.Path) |> Set.ofList)

                if touched |> Set.exists unobserved.Contains then
                    Some(Choice2Of2 { SkillId = id; Reason = Unreadable })
                else
                    // A file already at the PACK's digest is the pack's own bytes whatever the record
                    // says: it is what an earlier run that was interrupted (a crash, a read-only
                    // directory) left behind, and treating it as an edit would hold that skill forever.
                    let atPack path text =
                        Map.tryFind path desiredByPath
                        |> Option.exists (fun file -> file.Sha256 = Fsgg.SkillMirror.sha256 text)

                    let edited =
                        touched
                        |> Set.exists (fun path ->
                            match Map.tryFind path recorded, Map.tryFind path bodies with
                            // Recorded without a digest: nothing to judge by, so it is edited.
                            | Some None, _ -> true
                            | Some(Some sha), Some text ->
                                Fsgg.SkillMirror.sha256 text <> sha && not (atPack path text)
                            | Some(Some _), None -> false
                            // Wanted by the new pack, present on disk, not attested by the record:
                            // ours only if it is exactly the pack's bytes.
                            | None, Some text -> not (atPack path text)
                            | None, None -> false)

                    if edited then
                        Some(Choice2Of2 { SkillId = id; Reason = LocallyEdited })
                    else
                        match versionVerdict record cliVersion embeddedFor id owner with
                        | Error reason -> Some(Choice2Of2 { SkillId = id; Reason = reason })
                        | Ok version ->
                            let removedPaths =
                                recorded
                                |> Map.toList
                                |> List.filter (fun (path, _) -> not (Map.containsKey path desiredByPath))

                            let removals =
                                removedPaths
                                |> List.choose (fun (path, sha) ->
                                    match sha, Map.tryFind path bodies with
                                    | Some digest, Some _ -> Some(path, digest)
                                    | _ -> None)

                            let dropped =
                                removedPaths
                                |> List.map fst
                                |> List.filter (fun path -> not (Map.containsKey path bodies))

                            let expectedBefore =
                                desiredFiles
                                |> List.map (fun file ->
                                    file.Path,
                                    (match Map.tryFind file.Path recorded, Map.tryFind file.Path bodies with
                                     | Some(Some sha), Some text when Fsgg.SkillMirror.sha256 text = sha -> sha
                                     // At the pack's digest (an interrupted run), so the write is a no-op.
                                     | _, Some text -> Fsgg.SkillMirror.sha256 text
                                     | _ -> ""))
                                |> Map.ofList

                            Some(
                                Choice1Of2
                                    {
                                        SkillId = id
                                        Owner = owner
                                        Writes = desiredFiles |> List.sortBy (fun file -> file.Path)
                                        ExpectedBefore = expectedBefore
                                        Removals = removals |> List.sortBy fst
                                        Dropped = dropped |> List.sort
                                        Stale =
                                            desiredFiles
                                            |> List.filter (fun file ->
                                                Map.tryFind file.Path recorded <> Some(Some file.Sha256))
                                            |> List.map (fun file -> file.Path)
                                            |> List.sort
                                        Version = version
                                    }
                            )

        let results =
            recordedById
            |> List.filter (fun (id, _) ->
                not (Set.contains id foreign)
                && not (id.StartsWith("fs-gg-sdd-", StringComparison.Ordinal)))
            |> List.choose (fun (id, rows) -> decide id rows)

        // A skill the pack delivers that the record does NOT attest, whose name is already taken in
        // the workspace by files that are not the pack's: the user's (or a provider's) own skill.
        // Reported by name with the fix, and left out of the backfill so it cannot fail the re-seed
        // of every other skill. Files that are exactly the pack's bytes are not a collision: they are
        // what an interrupted install left behind, and the backfill completes them.
        let recordedIds = recordedById |> List.map fst |> Set.ofList

        let collisions =
            desired
            |> List.choose (fun file -> skillCopyOf file.Path |> Option.map (fun (_, id, _) -> id))
            |> List.distinct
            |> List.filter (fun id ->
                not (Set.contains id recordedIds)
                && not (id.StartsWith("fs-gg-sdd-", StringComparison.Ordinal)))
            |> List.choose (fun id ->
                let differing =
                    desired
                    |> List.filter (fun file ->
                        skillCopyOf file.Path |> Option.exists (fun (_, i, _) -> i = id)
                        && (match Map.tryFind file.Path bodies with
                            | Some text -> Fsgg.SkillMirror.sha256 text <> file.Sha256
                            | None -> false))
                    |> List.map (fun file -> file.Path)

                if List.isEmpty differing then
                    None
                else
                    Some(
                        Choice2Of2
                            {
                                SkillId = id
                                Reason = NameCollision(List.sort differing)
                            }
                    ))

        let results = results @ collisions

        {
            Refreshed =
                results
                |> List.choose (function
                    | Choice1Of2 refresh -> Some refresh
                    | Choice2Of2 _ -> None)
                |> List.sortBy (fun refresh -> refresh.SkillId)
            Held =
                results
                |> List.choose (function
                    | Choice2Of2 held -> Some held
                    | Choice1Of2 _ -> None)
                |> List.sortBy (fun held -> held.SkillId)
        }

    /// The destination paths a refresh must act on: desired files that are absent, differ from the
    /// pack's digest or whose record row is stale; the removals; and the dropped rows. This is the
    /// `artifactReSeed` target contribution. A file already at the pack's bytes with a current record
    /// is a no-op and not a target.
    let changedPaths (bodies: Map<string, string>) (refresh: SkillRefresh) : string list =
        (refresh.Writes
         |> List.filter (fun file ->
             match Map.tryFind file.Path bodies with
             | Some text -> Fsgg.SkillMirror.sha256 text <> file.Sha256
             | None -> true)
         |> List.map (fun file -> file.Path))
        @ refresh.Stale
        @ (refresh.Removals |> List.map fst)
        @ refresh.Dropped
        |> List.distinct
        |> List.sort

    /// A one-line, operator-facing reason for a held skill.
    let heldText (held: HeldSkill) =
        match held.Reason with
        | LocallyEdited ->
            $"{held.SkillId}: changed upstream but edited locally (or its recorded digest is missing); not replaced"
        | WouldDowngrade(recorded, embedded) ->
            $"{held.SkillId}: this CLI embeds {embedded}, older than the {recorded} already materialized; not replaced"
        | VersionUnprovable detail -> $"{held.SkillId}: not replaced, newer-ness unprovable ({detail})"
        | Unreadable -> $"{held.SkillId}: a file could not be read; not replaced"
        | NameCollision paths ->
            let shown = paths |> List.truncate 3 |> String.concat ", "

            $"{held.SkillId}: a skill with this name already exists here and is not the one SDD delivers ({shown}); it was left alone and the delivered skill was not installed. Rename or remove your skill, then re-run `fsgg-sdd upgrade`"

    /// The desired owner-skill files, from the SAME embedded, content-verified plans scaffold and the
    /// missing-file backfill use, with their precedence (a path has one owner: drivers, then the
    /// established product channel, then rendering, then audio). Pure: compiled-in bytes plus `record`.
    let embeddedDesired (record: ScaffoldProvenanceRecord) : DesiredFile list =
        let providerIds =
            record.ProducedPaths
            |> List.choose (fun row ->
                if row.Path.StartsWith(Fsgg.SkillMirror.providerSourceRoot + "/skills/", StringComparison.Ordinal) then
                    Fsgg.SkillMirror.skillIdOfPath row.Path
                else
                    None)
            |> List.filter (fun id -> not (id.StartsWith("fs-gg-sdd-", StringComparison.Ordinal)))

        let presentIds = Set.ofList (SeededSkills.skillNames @ providerIds)

        let ownerParameters =
            record.EffectiveParameters
            |> Map.ofList
            |> AudioSkills.ownerPredicateParameters record.TemplateRef

        let driver = DriverSkills.plan presentIds
        let product = GameSkills.plan ownerParameters
        let rendering = RenderingSkills.plan ownerParameters
        let audio = AudioSkills.plan ownerParameters

        let classFiles owner (writes: CommandTypes.CommandEffect list) (provenance: (string * string) list) =
            let digests = Map.ofList provenance

            let executables =
                writes
                |> List.choose (function
                    | CommandTypes.SetExecutable path -> Some path
                    | _ -> None)
                |> Set.ofList

            writes
            |> List.choose (function
                | CommandTypes.WriteFile(path, text, _) when (skillCopyOf path).IsSome ->
                    Some
                        {
                            Path = path
                            Sha256 = Map.tryFind path digests |> Option.defaultValue (Fsgg.SkillMirror.sha256 text)
                            Text = text
                            Executable = Set.contains path executables
                            Owner = owner
                        }
                | _ -> None)

        classFiles ArtifactOwner.Driver driver.Writes driver.ProvenancePaths
        @ classFiles ArtifactOwner.GameSkill product.Writes product.ProvenancePaths
        @ classFiles ArtifactOwner.RenderingSkill rendering.Writes rendering.ProvenancePaths
        @ classFiles ArtifactOwner.AudioSkill audio.Writes audio.ProvenancePaths
        |> List.distinctBy (fun file -> file.Path)
