namespace FS.GG.SDD.Commands.Internal

open System.Reflection
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Artifacts

/// The scaffold-time materializer for the rendering owner's own **product** skills (e.g.
/// `fs-gg-feedback-report`) delivered as bytes in the pinned rendering-skills package
/// (ADR-0063 owner-repo byte source, ADR-0062 substrate; ADR-0014 verify). This is the FOURTH
/// enrollment channel and the THIRD instance of the class ADR-0063 named — *"declared ∧ gated-in ∧
/// supplied-from-nowhere"* — after `fs-gg-playtest` (.github#1299) and `workRoadmap` (.github#1300).
/// FS.GG.SDD#864; it is what makes `.github#2639`'s `skills.delivery-channels.yml` flip from
/// `provider-scoped` to `delivered` an honest one, because `delivered` is defined there as *"the
/// bytes reach a tree scaffolded through ANY provider"* and before this channel they reached zero.
///
/// The package's `skill-manifest.json` and `skills/<id>/**` files are linked into this assembly as
/// embedded resources at build time (`RenderingSkill.manifest` / `RenderingSkill.skill/<id>/<file>`),
/// so the materialize reads **compiled-in bytes** — never the NuGet cache, an owner-repo clone,
/// or the network — which is what makes scaffold time offline (FR-002). This mirrors the
/// `GameSkills` seam, one owner over: the same schema-v1 owner manifest shape (parsed by the
/// shape-modelling `GameSkillManifest`, which names no particular owner), the same
/// `ProductPredicate` evaluated against the scaffold PARAMETER set, the same content-addressed
/// verify before any write, the same no-clobber `AgentGuidanceTarget` writes, and the same four
/// fail-closed classes.
///
/// TWO THINGS THIS CHANNEL MUST DO THAT THE ESTABLISHED OWNER-SKILL CHANNEL NEVER HAD TO, both forced by measured
/// facts about the packages rather than by taste:
///
/// 1. **Undeliverable sidecars.** the pinned rendering-skills package ships six files that are not
///    `SKILL.md`, across three skills. Its manifest is schemaVersion 1 and records ONE `sha256` per
///    skill — the canonical digest of `SKILL.md`, per its own `resolvablePath`. A sidecar therefore
///    carries no declared digest, and ADR-0014 fail-closed means it cannot be written: a channel
///    that materializes an unverified body has stopped being a verified channel. So this channel
///    writes exactly the manifest-verified `SKILL.md` of each row — the shape FS.GG.SDD#864 asked
///    for — and reports the withheld files through `undeliverableSidecars` rather than dropping them
///    silently. That report matters concretely: `fs-gg-feedback-report`'s own body instructs the
///    reader to run `.agents/skills/fs-gg-feedback-report/scripts/feedback-tool.fsx` in six separate
///    places, and under this manifest schema that path cannot arrive. The gap's root cause is one
///    layer down and in another repository — the producer's manifest schema, not this consumer — so
///    it is reported here and filed there, never papered over by writing unverified bytes.
///
///    The remedy is already modelled in this codebase: `FS.GG.Drivers`' manifest is schemaVersion 2
///    and declares a per-file `files` array, which is exactly why `DriverSkills` CAN carry a closed
///    multi-file directory transport. When the rendering-skills package publishes a v2 manifest, the
///    sidecars become declarable and this channel can begin writing them; the embedded bytes are
///    already here waiting.
///
/// 2. **Cross-channel id collision.** Four ids — `fs-gg-collision`, `fs-gg-grids`,
///    `fs-gg-line-drawing`, `fs-gg-visibility` — are shipped by BOTH pinned owner-skill packages, with DIFFERENT bodies and the identical profile-gated predicate. Unresolved, a scaffold on the shared profile would emit two writes for one
///    path (the no-clobber write silently keeping the first) while provenance recorded that path
///    under two owners with two different digests — precisely the unattributed/over-claimed path
///    that made .github#2380 an investigation instead of a lookup. This channel therefore YIELDS:
///    `HandlersScaffold.plannedRenderingSkillOutcome` subtracts every path the established channel kept and
///    surfaces the yielded ids as a non-blocking advisory. Yielding rather than winning is the
///    scope-respecting choice — FS.GG.SDD#864 asked for a fourth channel, not for a change to what
///    four landed skills deliver — and the ownership question it exposes belongs to the producers.
module internal RenderingSkills =
    type private StreamReader = System.IO.StreamReader

    let manifestResourceName = "RenderingSkill.manifest"

    // The embedded files carry logical names `RenderingSkill.skill/<id>/<relative-path>`. The lookup
    // enumerates and parses the id out (separator-normalized) rather than reconstructing the name
    // from an id, so a build whose MSBuild `%(RecursiveDir)` used `\` still resolves.
    let private skillResourcePrefix = "RenderingSkill.skill/"

    /// The one canonical body path a schema-v1 owner manifest's per-skill `sha256` covers.
    let private canonicalBodyPath = "SKILL.md"

    let private tryLoadResource (name: string) : string option =
        let assembly = Assembly.GetExecutingAssembly()

        match assembly.GetManifestResourceStream(name) with
        | null -> None
        | stream ->
            use stream = stream
            use reader = new StreamReader(stream)
            Some(reader.ReadToEnd())

    /// The embedded delivered owner-skill manifest text; `None` when no rendering-skills package is embedded (e.g. a build without the pin) — the materializer then no-ops rather than
    /// failing, exactly as the driver and owner-skill seams do.
    let manifestText () = tryLoadResource manifestResourceName

    // Every embedded delivered file as (id, relative path), keyed off the embedded resource names.
    // Robust to the `/` vs `\` a build's `%(RecursiveDir)` may have baked into the logical name.
    let private embeddedFileNames () : (string * string * string) list =
        let assembly = Assembly.GetExecutingAssembly()

        assembly.GetManifestResourceNames()
        |> Array.toList
        |> List.choose (fun name ->
            let normalized = name.Replace('\\', '/')

            if normalized.StartsWith(skillResourcePrefix, System.StringComparison.Ordinal) then
                let rest = normalized.Substring(skillResourcePrefix.Length)
                let separator = rest.IndexOf('/')

                if separator <= 0 || separator = rest.Length - 1 then
                    None
                else
                    Some(rest.Substring(0, separator), rest.Substring(separator + 1), name)
            else
                None)

    /// Map of owner-skill id → embedded canonical body. Only `SKILL.md` is a body: it is the one
    /// file a schema-v1 manifest declares a digest for, and therefore the only one this channel may
    /// materialize.
    let embeddedBodies () : Map<string, string> =
        embeddedFileNames ()
        |> List.choose (fun (id, relative, resourceName) ->
            if relative = canonicalBodyPath then
                tryLoadResource resourceName |> Option.map (fun body -> id, body)
            else
                None)
        |> Map.ofList

    /// Every embedded delivered file that is NOT the manifest-declared canonical body, as
    /// `<id>/<relative-path>`, id-sorted then path-sorted. These are shipped by the producer and
    /// carry NO declared digest at manifest schemaVersion 1, so they are deliberately not written —
    /// see the module note. Empty when the package ships one file per skill (the established owner-skills package
    /// shape) or when no package is embedded.
    let undeliverableSidecars () : string list =
        embeddedFileNames ()
        |> List.choose (fun (id, relative, _) ->
            if relative = canonicalBodyPath then
                None
            else
                Some $"{id}/{relative}")
        |> List.sort

    /// The outcome of planning owner-skill materialization: the no-clobber writes to emit, the
    /// per-path provenance digests (owner `RenderingSkill`), the ids actually materialized, and the
    /// fail-closed classes surfaced as scaffold diagnostics. All lists are id-sorted / path-ordered
    /// and deterministic.
    type RenderingSkillOutcome =
        { Writes: CommandEffect list
          ProvenancePaths: (string * string) list
          MaterializedIds: string list
          // The declared `scope` of each materialized owner-skill id (from its manifest row), so a
          // consumer can declare it in the product `skill-manifest.json` faithfully (ADR-0063 tail).
          MaterializedScopes: Map<string, string>
          VerifyFailedIds: string list
          PredicateUnevaluatedIds: string list
          NamespaceCollisionIds: string list
          // Ids this channel would have materialized but yielded to an earlier channel that already
          // owns the same path (see the module note, point 2). Non-blocking advisory; populated by
          // `HandlersScaffold`, which is the only layer that can see the other channels' plans.
          YieldedIds: string list
          // `<id>/<relative-path>` of every delivered file the manifest declares no digest for, so
          // it could not be verified and was not written (see the module note, point 1).
          UndeliverableSidecars: string list
          ManifestError: string option }

    let empty =
        { Writes = []
          ProvenancePaths = []
          MaterializedIds = []
          MaterializedScopes = Map.empty
          VerifyFailedIds = []
          PredicateUnevaluatedIds = []
          NamespaceCollisionIds = []
          YieldedIds = []
          UndeliverableSidecars = []
          ManifestError = None }

    // The whole `fs-gg-sdd-*` namespace is SDD-owned skeleton (CLAUDE.md; `isSddTree` reserves
    // `.agents/skills/fs-gg-sdd-`), so a delivered row anywhere in it is rejected — a prefix guard,
    // so no `fs-gg-sdd-*` id can ever shadow the skeleton. Product ids are `fs-gg-*` (never
    // `fs-gg-sdd-*`), so this never fires today; it is the defensive parity backstop.
    let private reservedNamespacePrefix = "fs-gg-sdd-"

    // Every product-scoped row is a delivered owner skill; the retired legacy `mirrored`
    // classification is not a materialization input, exactly as in the sibling owner-skill seam.
    let private isDelivered (entry: GameSkillManifest.GameSkillManifestEntry) = entry.Scope = "product"

    // The intermediate per-row classification, folded into the output classes.
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
            acc // Any non-product row belongs to another delivery seam.
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
                // ONE provenance class, ONE digest domain (FS.GG.SDD#752 AC3). The body arrives
                // through a BOM-stripping `StreamReader` and is verified, recorded and later
                // re-verified with `Fsgg.SkillMirror.sha256`: BOM stripped, `\r\n` folded.
                match Map.tryFind entry.Id bodies with
                | Some body when Fsgg.SkillMirror.sha256 body = entry.Sha256 ->
                    { acc with
                        Materializable = acc.Materializable @ [ entry.Id, body, entry.Sha256 ] }
                | _ ->
                    // Body absent or digest mismatch: cannot produce a verified body ⇒ fail closed,
                    // write nothing for this row (FR-003).
                    { acc with
                        VerifyFailed = acc.VerifyFailed @ [ entry.Id ] }

    /// Plan owner-skill materialization from an explicit manifest text + id→body map, gated by the
    /// effective scaffold parameter set. The pure core of `plan`, factored out so the fail-closed
    /// classes (tamper, id collision, unevaluable predicate) are testable without the compiled-in
    /// bytes. `sidecars` is the already-computed undeliverable set; it is an input rather than a
    /// resource read so a test can exercise the report without a package that ships one.
    let planFrom
        (manifestText: string option)
        (bodies: Map<string, string>)
        (sidecars: string list)
        (parameters: Map<string, string>)
        : RenderingSkillOutcome =
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

                let materializedIds =
                    classified.Materializable |> List.map (fun (id, _, _) -> id) |> Set.ofList

                let shaById =
                    classified.Materializable
                    |> List.map (fun (id, _, sha256) -> id, sha256)
                    |> Map.ofList

                // The declared scope of each manifest row, so a materialized id can be declared in
                // the product manifest with the scope its own producer assigned it (ADR-0063 tail).
                let scopeById =
                    manifest.Skills |> List.map (fun skill -> skill.Id, skill.Scope) |> Map.ofList

                // Fan the verified bodies into every declared root through the shared mirror,
                // exactly as the seeded skeleton and the driver/owner-skill seams do — deterministic
                // (id-sorted, roots in order), byte-identical across roots by construction, all
                // no-clobber.
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
                  MaterializedScopes =
                    classified.Materializable
                    |> List.choose (fun (id, _, _) -> Map.tryFind id scopeById |> Option.map (fun scope -> id, scope))
                    |> Map.ofList
                  VerifyFailedIds = classified.VerifyFailed
                  PredicateUnevaluatedIds = classified.PredicateUnevaluated
                  NamespaceCollisionIds = classified.Collisions
                  YieldedIds = []
                  // Only a row that actually materialized can be missing sidecars a reader would
                  // reach for: an off-profile row was not delivered at all, and reporting its
                  // sidecars would be noise about a skill that is legitimately absent.
                  UndeliverableSidecars =
                    sidecars
                    |> List.filter (fun entry ->
                        match entry.Split('/') |> Array.tryHead with
                        | Some id -> materializedIds.Contains id
                        | None -> false)
                  ManifestError = None }

    /// Plan owner-skill materialization from the CLI's embedded package bytes, gated by the
    /// effective scaffold parameter set (`profile`, …) for `materializes-when` evaluation. Pure —
    /// reads only compiled-in resources (FR-002 — no NuGet cache / network at scaffold time).
    let plan (parameters: Map<string, string>) : RenderingSkillOutcome =
        planFrom (manifestText ()) (embeddedBodies ()) (undeliverableSidecars ()) parameters
