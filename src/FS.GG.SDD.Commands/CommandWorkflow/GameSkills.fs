namespace FS.GG.SDD.Commands.Internal

open System.Reflection
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Artifacts

/// The scaffold-time materializer for an owner repo's owner-authored **`mirrored: false`
/// product** skills (e.g. `fs-gg-playtest`) delivered as bytes in the pinned owner-skills package
/// (ADR-0063 owner-repo byte source, ADR-0062 substrate; ADR-0014 verify). The package's
/// `skill-manifest.json` and `skills/<id>/SKILL.md` bodies are linked into this assembly as
/// embedded resources at build time (`GameSkill.manifest` / `GameSkill.skill/<id>/SKILL.md`), so
/// the materialize reads **compiled-in bytes** — never the NuGet cache, an owner-repo clone, or
/// the network — which is what makes scaffold time offline (FR-002). This mirrors the
/// `DriverSkills` seam exactly, one owner over.
///
/// Two things differ from the driver seam, and both come from the registry: (1) only a
/// `scope: product` row with `mirrored: false` is delivered here — a `mirrored: true` row is
/// listed in the manifest but reaches a scaffold through the frozen provider mirror (ADR-0022 §6),
/// so it is skipped rather than verify-failed for a body that was never shipped; (2) the
/// `materializes-when` predicate is evaluated against the scaffold PARAMETER set (`profile in
/// [..]`), not the present-skill-id set the driver's `has …` grammar reads.
///
/// Enforcement of the delivered shape lives here, the consumer that materializes the token
/// (ADR-0061): each body is content-addressed against its manifest `sha256` before any write
/// (FR-003), and a row is materialized iff its `materializes-when` predicate holds (FR-004).
/// Writes use the no-clobber `AgentGuidanceTarget` kind, and a row whose id collides with the
/// reserved seeded `fs-gg-sdd-*` namespace is rejected.
module internal GameSkills =
    type private StreamReader = System.IO.StreamReader

    let manifestResourceName = "GameSkill.manifest"

    // The embedded skill bodies carry logical names `GameSkill.skill/<id>/SKILL.md`. The lookup
    // enumerates and parses the id out (separator-normalized) rather than reconstructing the name
    // from an id, so a build whose MSBuild `%(RecursiveDir)` used `\` still resolves.
    let private skillResourcePrefix = "GameSkill.skill/"

    let private tryLoadResource (name: string) : string option =
        let assembly = Assembly.GetExecutingAssembly()

        match assembly.GetManifestResourceStream(name) with
        | null -> None
        | stream ->
            use stream = stream
            use reader = new StreamReader(stream)
            Some(reader.ReadToEnd())

    /// The embedded delivered owner-skill manifest text; `None` when no owner-skill package is
    /// embedded (e.g. a build without the pin) — the materializer then no-ops rather than failing.
    let manifestText () = tryLoadResource manifestResourceName

    // Map of owner-skill id → embedded body, keyed off the embedded resource names. Robust to the
    // `/` vs `\` a build's `%(RecursiveDir)` may have baked into the logical name.
    let embeddedBodies () : Map<string, string> =
        let assembly = Assembly.GetExecutingAssembly()

        assembly.GetManifestResourceNames()
        |> Array.choose (fun name ->
            let normalized = name.Replace('\\', '/')

            if normalized.StartsWith(skillResourcePrefix, System.StringComparison.Ordinal) then
                match normalized.Split('/') |> Array.toList with
                | [ _; id; "SKILL.md" ] when id <> "" -> tryLoadResource name |> Option.map (fun body -> id, body)
                | _ -> None
            else
                None)
        |> Map.ofArray

    /// The outcome of planning owner-skill materialization: the no-clobber writes to emit, the
    /// per-path provenance digests (owner `GameSkill`), the ids actually materialized, and the
    /// three fail-closed classes surfaced as scaffold diagnostics. All lists are id-sorted /
    /// path-ordered and deterministic.
    type GameSkillOutcome =
        { Writes: CommandEffect list
          ProvenancePaths: (string * string) list
          MaterializedIds: string list
          VerifyFailedIds: string list
          PredicateUnevaluatedIds: string list
          NamespaceCollisionIds: string list
          ManifestError: string option }

    let empty =
        { Writes = []
          ProvenancePaths = []
          MaterializedIds = []
          VerifyFailedIds = []
          PredicateUnevaluatedIds = []
          NamespaceCollisionIds = []
          ManifestError = None }

    // The whole `fs-gg-sdd-*` namespace is SDD-owned skeleton (CLAUDE.md; `isSddTree` reserves
    // `.agents/skills/fs-gg-sdd-`), so a delivered row anywhere in it is rejected — a prefix guard,
    // so no `fs-gg-sdd-*` id can ever shadow the skeleton. Product ids are `fs-gg-*` (never
    // `fs-gg-sdd-*`), so this never fires today; it is the defensive parity backstop.
    let private reservedNamespacePrefix = "fs-gg-sdd-"

    // The delivered subclass: a `scope: product` row with NO frozen provider mirror (ADR-0022 §6).
    // A `mirrored: true` row is carried in the manifest but its bytes are delivered NOWHERE here —
    // it reaches a scaffold through the provider mirror instead — so it must be SKIPPED, not treated
    // as a body-absent verify failure. Matches the package stager's `is_delivered`.
    let private isDelivered (entry: GameSkillManifest.GameSkillManifestEntry) =
        entry.Scope = "product" && entry.Mirrored = Some false

    // The intermediate per-row classification, folded into the four output classes.
    type private Classified =
        { Collisions: string list
          PredicateUnevaluated: string list
          VerifyFailed: string list
          Materializable: (string * string * string) list } // (id, body, sha256)

    let private classifyEntry
        (parameters: Map<string, string>)
        (bodies: Map<string, string>)
        (acc: Classified)
        (entry: GameSkillManifest.GameSkillManifestEntry)
        =
        if not (isDelivered entry) then
            acc // mirrored:true (delivered via the provider mirror) or any non-product row.
        elif entry.Id.StartsWith(reservedNamespacePrefix, System.StringComparison.Ordinal) then
            { acc with
                Collisions = acc.Collisions @ [ entry.Id ] }
        else
            match ProductPredicate.evaluate entry.MaterializesWhen parameters with
            | None ->
                { acc with
                    PredicateUnevaluated = acc.PredicateUnevaluated @ [ entry.Id ] }
            | Some false -> acc // deliberately not materialized off-profile (predicate held false)
            | Some true ->
                match Map.tryFind entry.Id bodies with
                | Some body when Fsgg.SkillMirror.sha256 body = entry.Sha256 ->
                    { acc with
                        Materializable = acc.Materializable @ [ entry.Id, body, entry.Sha256 ] }
                | _ ->
                    // Body absent or digest mismatch: cannot produce a verified body ⇒ fail
                    // closed, write nothing for this row (FR-003).
                    { acc with
                        VerifyFailed = acc.VerifyFailed @ [ entry.Id ] }

    /// Plan owner-skill materialization from an explicit manifest text + id→body map, gated by the
    /// effective scaffold parameter set. The pure core of `plan`, factored out so the fail-closed
    /// classes (tamper, id collision, unevaluable predicate) are testable without the compiled-in
    /// bytes.
    let planFrom
        (manifestText: string option)
        (bodies: Map<string, string>)
        (parameters: Map<string, string>)
        : GameSkillOutcome =
        match manifestText with
        | None -> empty
        | Some text ->
            match GameSkillManifest.tryParse text with
            | Error message ->
                { empty with
                    ManifestError = Some message }
            | Ok manifest ->
                let classified =
                    ({ Collisions = []
                       PredicateUnevaluated = []
                       VerifyFailed = []
                       Materializable = [] },
                     manifest.Skills |> List.sortBy (fun skill -> skill.Id))
                    ||> List.fold (classifyEntry parameters bodies)

                let shaById =
                    classified.Materializable
                    |> List.map (fun (id, _, sha256) -> id, sha256)
                    |> Map.ofList

                // Fan the verified bodies into every declared root through the shared mirror,
                // exactly as the seeded skeleton and the driver seam do — deterministic (id-sorted,
                // roots in order), byte-identical across roots by construction, all no-clobber.
                let mirrorWrites =
                    classified.Materializable
                    |> List.map (fun (id, body, _) -> id, body)
                    |> Fsgg.SkillMirror.mirror Fsgg.Schemas.agentSkillRoots

                { Writes =
                    mirrorWrites
                    |> List.map (fun write -> WriteFile(write.Path, write.Body, AgentGuidanceTarget))
                  ProvenancePaths =
                    mirrorWrites
                    |> List.map (fun write ->
                        let sha256 =
                            Fsgg.SkillMirror.skillIdOfPath write.Path
                            |> Option.bind (fun id -> Map.tryFind id shaById)
                            |> Option.defaultValue ""

                        write.Path, sha256)
                  MaterializedIds = classified.Materializable |> List.map (fun (id, _, _) -> id)
                  VerifyFailedIds = classified.VerifyFailed
                  PredicateUnevaluatedIds = classified.PredicateUnevaluated
                  NamespaceCollisionIds = classified.Collisions
                  ManifestError = None }

    /// Plan owner-skill materialization from the CLI's embedded package bytes, gated by the
    /// effective scaffold parameter set (`profile`, …) for `materializes-when` evaluation. Pure —
    /// reads only compiled-in resources (FR-002 — no NuGet cache / network at scaffold time).
    let plan (parameters: Map<string, string>) : GameSkillOutcome =
        planFrom (manifestText ()) (embeddedBodies ()) parameters
