namespace FS.GG.SDD.Commands.Internal

open System.Reflection
open System.Text
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

    /// Normalize the *portable* relative form accepted by the manifest transport. The manifest
    /// travels from a package into both Unix and Windows workspaces, so accepting a path that one
    /// platform treats as rooted or a separator and the other does not would make the declared
    /// byte set depend on the receiving host. Keep the transport deliberately narrower than a
    /// filesystem path: slash-separated non-empty names only, with no dot/traversal, drive, or
    /// backslash form. The caller rejects the whole row before it constructs a `WriteFile`.
    let private tryNormalizeSkillRelativePath (path: string) : string option =
        if System.String.IsNullOrWhiteSpace path
           || path.Contains('\\')
           || path.Contains(':')
           || System.IO.Path.IsPathRooted path then
            None
        else
            let segments = path.Split('/', System.StringSplitOptions.None) |> Array.toList

            if List.isEmpty segments
               || segments |> List.exists (fun segment -> System.String.IsNullOrWhiteSpace segment || segment = "." || segment = "..") then
                None
            else
                Some(String.concat "/" segments)

    let private tryLoadResourceBytes (name: string) : byte array option =
        let assembly = Assembly.GetExecutingAssembly()

        match assembly.GetManifestResourceStream(name) with
        | null -> None
        | stream ->
            use stream = stream
            use buffer = new System.IO.MemoryStream()
            stream.CopyTo buffer
            Some(buffer.ToArray())

    let private tryLoadResource name =
        tryLoadResourceBytes name
        |> Option.bind (fun bytes ->
            match Fsgg.SkillMirror.decodeBody bytes with
            | Ok body -> Some body
            | Error _ -> None)

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

    /// Complete embedded transport, keyed by skill id and skill-relative path.  Schema-v2 rows
    /// close this set: every declared file must be present and digest-matching and no extra bytes
    /// may reach a workspace.
    let embeddedFiles () : Map<string * string, byte array> =
        embeddedFileNames ()
        |> List.choose (fun (id, relative, resourceName) ->
            tryLoadResourceBytes resourceName |> Option.map (fun bytes -> (id, relative), bytes))
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
    let private isDelivered (entry: ProductSkillManifest.ProductManifestEntry) = entry.Scope = "product"

    // The intermediate per-row classification, folded into the output classes.
    type private Classified =
        { Collisions: string list
          PredicateUnevaluated: string list
          VerifyFailed: string list
          Materializable: (ProductSkillManifest.ProductManifestEntry * (ProductSkillManifest.ProductManifestFile * string) list) list }

    let private classifyEntry
        (parameters: Map<string, string>)
        (schemaVersion: int)
        (files: Map<string * string, byte array>)
        (acc: Classified)
        (entry: ProductSkillManifest.ProductManifestEntry)
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
                // v1 implicitly declares its canonical body. v2 is a closed per-file transport:
                // each declared sidecar is verified before ANY row write, and an undeclared
                // embedded byte refuses the whole row rather than leaking a partial skill.
                let declared =
                    // Schema v1 had no per-file transport, so its sole row digest still
                    // implicitly names SKILL.md. Schema v2 is a closed declaration: absent or
                    // empty `files` cannot be promoted into an invented write target.
                    if schemaVersion < 2 && List.isEmpty entry.Files then
                        let implicitFile: ProductSkillManifest.ProductManifestFile =
                            { Path = canonicalBodyPath
                              Sha256 = entry.Sha256 }
                        [ implicitFile ]
                    else entry.Files
                // A schema-v2 file set is a closed transport, not a list of suggested writes.
                // Validate every path and its uniqueness BEFORE looking up bytes or emitting any
                // `WriteFile`: duplicate paths would otherwise schedule two writes, while rooted,
                // traversal, or backslash paths could escape the skill directory on a receiver.
                let normalizedDeclared =
                    declared
                    |> List.map (fun file -> tryNormalizeSkillRelativePath file.Path |> Option.map (fun path -> path, file))

                let declaredAreSafe = normalizedDeclared |> List.forall Option.isSome

                let declared =
                    normalizedDeclared
                    |> List.choose id
                    |> List.map (fun (path, file) -> { file with Path = path })

                let declaredPaths = declared |> List.map _.Path |> Set.ofList
                let declaredAreUnique = declaredPaths.Count = declared.Length
                let actualPaths =
                    files |> Map.toList |> List.choose (fun ((id, path), _) -> if id = entry.Id then Some path else None) |> Set.ofList
                let verified =
                    declared
                    |> List.choose (fun file ->
                        Map.tryFind (entry.Id, file.Path) files
                        |> Option.bind (fun bytes ->
                            match Fsgg.SkillMirror.decodeBody bytes with
                            | Ok body when Fsgg.SkillMirror.sha256 body = file.Sha256 -> Some(file, body)
                            | _ -> None))
                let canonicalOk =
                    verified |> List.tryFind (fun (file, _) -> file.Path = canonicalBodyPath)
                    |> Option.exists (fun (_, body) -> Fsgg.SkillMirror.sha256 body = entry.Sha256)
                if declaredAreSafe
                   && declaredAreUnique
                   && verified.Length = declared.Length
                   && (schemaVersion < 2 || actualPaths = declaredPaths)
                   && canonicalOk then
                    { acc with Materializable = acc.Materializable @ [ entry, verified ] }
                else
                    { acc with VerifyFailed = acc.VerifyFailed @ [ entry.Id ] }

    /// Plan owner-skill materialization from an explicit manifest text + id→body map, gated by the
    /// effective scaffold parameter set. The pure core of `plan`, factored out so the fail-closed
    /// classes (tamper, id collision, unevaluable predicate) are testable without the compiled-in
    /// bytes. `sidecars` is the already-computed undeliverable set; it is an input rather than a
    /// resource read so a test can exercise the report without a package that ships one.
    let planFilesFrom
        (manifestText: string option)
        (files: Map<string * string, byte array>)
        (parameters: Map<string, string>)
        : RenderingSkillOutcome =
        match manifestText with
        | None -> empty
        | Some text ->
            match ProductSkillManifest.tryParse text with
            | Error message ->
                { empty with
                    ManifestError = Some message }
            | Ok(schemaVersion, entries) ->
                let classified =
                    ({ Collisions = []
                       PredicateUnevaluated = []
                       VerifyFailed = []
                       Materializable = [] },
                     entries |> List.sortBy (fun skill -> skill.Id))
                    ||> List.fold (fun acc entry -> classifyEntry parameters schemaVersion files acc entry)

                // The declared scope of each manifest row, so a materialized id can be declared in
                // the product manifest with the scope its own producer assigned it (ADR-0063 tail).
                let scopeById =
                    entries |> List.map (fun skill -> skill.Id, skill.Scope) |> Map.ofList

                // Fan the verified bodies into every declared root through the shared mirror,
                // exactly as the seeded skeleton and the driver/owner-skill seams do — deterministic
                // (id-sorted, roots in order), byte-identical across roots by construction, all
                // no-clobber.
                let materializedFiles =
                    classified.Materializable
                    |> List.collect (fun (entry, verified) ->
                        [ for root in Fsgg.Schemas.agentSkillRoots do
                              for file, body in verified do
                                  yield $"{root}/skills/{entry.Id}/{file.Path}", body ])

                { Writes =
                    materializedFiles |> List.map (fun (path, body) -> WriteFile(path, body, AgentGuidanceTarget))
                  ProvenancePaths =
                    materializedFiles |> List.map (fun (path, body) -> path, Fsgg.SkillMirror.sha256 body)
                  MaterializedIds = classified.Materializable |> List.map (fun (entry, _) -> entry.Id)
                  MaterializedScopes =
                    classified.Materializable
                    |> List.choose (fun (entry, _) -> Map.tryFind entry.Id scopeById |> Option.map (fun scope -> entry.Id, scope))
                    |> Map.ofList
                  VerifyFailedIds = classified.VerifyFailed
                  PredicateUnevaluatedIds = classified.PredicateUnevaluated
                  NamespaceCollisionIds = classified.Collisions
                  YieldedIds = []
                  UndeliverableSidecars = []
                  ManifestError = None }

    /// v1-compatible test seam: callers supplying bodies still exercise the canonical file path.
    let planFrom manifestText (bodies: Map<string, string>) (_sidecars: string list) parameters =
        let files =
            bodies |> Map.toList |> List.map (fun (id, body) -> (id, canonicalBodyPath), Encoding.UTF8.GetBytes body) |> Map.ofList
        planFilesFrom manifestText files parameters

    /// Plan owner-skill materialization from the CLI's embedded package bytes, gated by the
    /// effective scaffold parameter set (`profile`, …) for `materializes-when` evaluation. Pure —
    /// reads only compiled-in resources (FR-002 — no NuGet cache / network at scaffold time).
    let plan (parameters: Map<string, string>) : RenderingSkillOutcome =
        let manifest = manifestText ()
        let outcome = planFilesFrom manifest (embeddedFiles ()) parameters
        match manifest |> Option.bind (fun text -> ProductSkillManifest.tryParse text |> Result.toOption) with
        | Some(schemaVersion, _) when schemaVersion < 2 ->
            let materialized = outcome.MaterializedIds |> Set.ofList
            { outcome with
                UndeliverableSidecars =
                    undeliverableSidecars ()
                    |> List.filter (fun entry -> materialized.Contains(entry.Split('/') |> Array.head)) }
        | _ -> outcome
