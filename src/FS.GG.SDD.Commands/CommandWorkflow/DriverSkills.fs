namespace FS.GG.SDD.Commands.Internal

open System.Reflection
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Artifacts

/// The scaffold-time materializer for the `.github`-authored **driver** skills (e.g.
/// `workRoadmap`) delivered as bytes in the pinned `FS.GG.Drivers` package (ADR-0054
/// §Byte-transport, ADR-0062/0063; ADR-0014 verify). The package's `driver-skill-manifest.json`
/// and `skills/<id>/SKILL.md` bodies are linked into this assembly as embedded resources at
/// build time (`Driver.manifest` / `Driver.skill/<id>/SKILL.md`), so the materialize reads
/// **compiled-in bytes** — never the NuGet cache, a `.github` clone, or the network — which is
/// what makes scaffold time offline (FR-002). This mirrors the `SeededSkills` seam exactly.
///
/// Enforcement of the driver shape lives here, the consumer that materializes the token
/// (ADR-0061): each body is content-addressed against its manifest `sha256` before any write
/// (FR-003), and a row is materialized iff its `materializes-when` predicate holds (FR-004).
/// Writes use the no-clobber `AgentGuidanceTarget` kind (FR-005), and a row whose id collides
/// with the reserved seeded `fs-gg-sdd-*` namespace is rejected (FR-007).
module internal DriverSkills =
    type private StreamReader = System.IO.StreamReader

    let manifestResourceName = "Driver.manifest"

    // The embedded skill bodies carry logical names `Driver.skill/<id>/SKILL.md`. The lookup
    // enumerates and parses the id out (separator-normalized) rather than reconstructing the
    // name from an id, so a build whose MSBuild `%(RecursiveDir)` used `\` still resolves.
    let private skillResourcePrefix = "Driver.skill/"

    let private tryLoadResource (name: string) : string option =
        let assembly = Assembly.GetExecutingAssembly()

        match assembly.GetManifestResourceStream(name) with
        | null -> None
        | stream ->
            use stream = stream
            use reader = new StreamReader(stream)
            Some(reader.ReadToEnd())

    /// The embedded delivered driver manifest text; `None` when no driver package is embedded
    /// (e.g. a build without the pin) — the materializer then no-ops rather than failing.
    let manifestText () = tryLoadResource manifestResourceName

    // Map of driver-skill id → embedded body, keyed off the embedded resource names. Robust to
    // the `/` vs `\` a build's `%(RecursiveDir)` may have baked into the logical name.
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

    /// The outcome of planning driver materialization: the no-clobber writes to emit, the
    /// per-path provenance digests (owner `Driver`), the ids actually materialized, and the
    /// three fail-closed classes surfaced as scaffold diagnostics. All lists are id-sorted /
    /// path-ordered and deterministic.
    type DriverOutcome =
        { Writes: CommandEffect list
          ProvenancePaths: (string * string) list
          MaterializedIds: string list
          // The declared `scope` of each materialized driver id (from its manifest row), so a
          // consumer can declare it in the product `skill-manifest.json` faithfully (ADR-0063 tail).
          MaterializedScopes: Map<string, string>
          VerifyFailedIds: string list
          PredicateUnevaluatedIds: string list
          NamespaceCollisionIds: string list
          ManifestError: string option }

    let empty =
        { Writes = []
          ProvenancePaths = []
          MaterializedIds = []
          MaterializedScopes = Map.empty
          VerifyFailedIds = []
          PredicateUnevaluatedIds = []
          NamespaceCollisionIds = []
          ManifestError = None }

    // The whole `fs-gg-sdd-*` namespace is SDD-owned skeleton (CLAUDE.md; `isSddTree` reserves
    // `.agents/skills/fs-gg-sdd-`), so a driver row anywhere in it is rejected — a prefix guard,
    // not just the 16 concrete seeded ids, so no `fs-gg-sdd-*` id can ever shadow the skeleton.
    let private reservedNamespacePrefix = "fs-gg-sdd-"

    // The intermediate per-row classification, folded into the four output classes.
    type private Classified =
        { Collisions: string list
          PredicateUnevaluated: string list
          VerifyFailed: string list
          Materializable: (string * string * string) list } // (id, body, sha256)

    let private classifyEntry
        (presentIds: Set<string>)
        (bodies: Map<string, string>)
        (acc: Classified)
        (entry: DriverManifest.DriverManifestEntry)
        =
        if entry.Id.StartsWith(reservedNamespacePrefix, System.StringComparison.Ordinal) then
            { acc with
                Collisions = acc.Collisions @ [ entry.Id ] }
        else
            match DriverPredicate.evaluate entry.MaterializesWhen presentIds with
            | None ->
                { acc with
                    PredicateUnevaluated = acc.PredicateUnevaluated @ [ entry.Id ] }
            | Some false -> acc // deliberately not materialized (e.g. `materializes-when: false`)
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

    /// Plan driver materialization from an explicit manifest text + id→body map, gated by the
    /// present skill-id set. The pure core of `plan`, factored out so the fail-closed classes
    /// (tamper, id collision, unevaluable predicate) are testable without the compiled-in bytes.
    let planFrom (manifestText: string option) (bodies: Map<string, string>) (presentIds: Set<string>) : DriverOutcome =
        match manifestText with
        | None -> empty
        | Some text ->
            match DriverManifest.tryParse text with
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
                    ||> List.fold (classifyEntry presentIds bodies)

                let shaById =
                    classified.Materializable
                    |> List.map (fun (id, _, sha256) -> id, sha256)
                    |> Map.ofList

                // The declared scope of each manifest row, so a materialized id can be declared in
                // the product manifest with the scope its own producer assigned it (ADR-0063 tail).
                let scopeById =
                    manifest.Skills |> List.map (fun skill -> skill.Id, skill.Scope) |> Map.ofList

                // Fan the verified bodies into every declared root through the shared mirror,
                // exactly as the seeded skeleton does — deterministic (id-sorted, roots in order),
                // byte-identical across roots by construction, all no-clobber.
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
                  ManifestError = None }

    /// Plan driver materialization from the CLI's embedded package bytes, gated by the set of
    /// skill ids already present in the workspace (seeded ∪ provider). Pure — reads only
    /// compiled-in resources (FR-002 — no NuGet cache / network at scaffold time).
    let plan (presentIds: Set<string>) : DriverOutcome =
        planFrom (manifestText ()) (embeddedBodies ()) presentIds
