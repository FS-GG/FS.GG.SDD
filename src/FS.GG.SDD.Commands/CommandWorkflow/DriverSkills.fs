namespace FS.GG.SDD.Commands.Internal

open System.Reflection
open System.Security.Cryptography
open System.Text
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Artifacts

/// The scaffold-time materializer for the `.github`-authored **driver** skills (e.g.
/// `workRoadmap`) delivered as bytes in the pinned `FS.GG.Drivers` package (ADR-0054
/// §Byte-transport, ADR-0062/0063; ADR-0014 verify). The package's `driver-skill-manifest.json`
/// and complete `skills/<id>/**` directories are linked into this assembly as embedded resources at
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

    let private tryLoadResourceBytes (name: string) : byte array option =
        let assembly = Assembly.GetExecutingAssembly()

        match assembly.GetManifestResourceStream(name) with
        | null -> None
        | stream ->
            use stream = stream
            use buffer = new System.IO.MemoryStream()
            stream.CopyTo buffer
            Some(buffer.ToArray())

    let private strictUtf8 = UTF8Encoding(false, true)

    let private tryDecode (bytes: byte array) =
        try
            Some(strictUtf8.GetString bytes)
        with :? DecoderFallbackException ->
            None

    let private rawSha256 (bytes: byte array) =
        SHA256.HashData bytes
        |> System.Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    /// The embedded delivered driver manifest text; `None` when no driver package is embedded
    /// (e.g. a build without the pin) — the materializer then no-ops rather than failing.
    let manifestText () =
        tryLoadResourceBytes manifestResourceName |> Option.bind tryDecode

    // Map of (driver-skill id, relative path) → raw bytes, keyed off the embedded resource names.
    // Robust to the `/` vs `\` a build's `%(RecursiveDir)` may have baked into the logical name.
    let embeddedFiles () : Map<string * string, byte array> =
        let assembly = Assembly.GetExecutingAssembly()

        assembly.GetManifestResourceNames()
        |> Array.choose (fun name ->
            let normalized = name.Replace('\\', '/')

            if normalized.StartsWith(skillResourcePrefix, System.StringComparison.Ordinal) then
                let rest = normalized.Substring(skillResourcePrefix.Length)
                let separator = rest.IndexOf('/')

                if separator <= 0 || separator = rest.Length - 1 then
                    None
                else
                    let id = rest.Substring(0, separator)
                    let relativePath = rest.Substring(separator + 1)
                    tryLoadResourceBytes name |> Option.map (fun bytes -> (id, relativePath), bytes)
            else
                None)
        |> Map.ofArray

    // Compatibility seam retained for tests/acceptance that specifically inspect the canonical body.
    let embeddedBodies () : Map<string, string> =
        embeddedFiles ()
        |> Map.toList
        |> List.choose (fun ((id, path), bytes) ->
            if path = "SKILL.md" then
                tryDecode bytes |> Option.map (fun body -> id, body)
            else
                None)
        |> Map.ofList

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
          Materializable: (DriverManifest.DriverManifestEntry * (DriverManifest.DriverManifestFile * string) list) list }

    let private classifyEntry
        (presentIds: Set<string>)
        (files: Map<string * string, byte array>)
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
                let declaredPaths = entry.Files |> List.map (fun file -> file.Path) |> Set.ofList

                let actualPaths =
                    files
                    |> Map.toList
                    |> List.choose (fun ((id, path), _) -> if id = entry.Id then Some path else None)
                    |> Set.ofList

                let verifiedFiles =
                    entry.Files
                    |> List.map (fun file ->
                        Map.tryFind (entry.Id, file.Path) files
                        |> Option.bind (fun bytes ->
                            let digestMatches =
                                if entry.TreeSha256.IsSome then
                                    rawSha256 bytes = file.Sha256
                                else
                                    tryDecode bytes
                                    |> Option.exists (fun body -> Fsgg.SkillMirror.sha256 body = file.Sha256)

                            if digestMatches then
                                tryDecode bytes |> Option.map (fun body -> file, body)
                            else
                                None))

                match verifiedFiles |> List.choose id with
                | verified when
                    verified.Length = entry.Files.Length
                    && actualPaths = declaredPaths
                    && (verified
                        |> List.tryFind (fun (file, _) -> file.Path = "SKILL.md")
                        |> Option.exists (fun (_, body) -> Fsgg.SkillMirror.sha256 body = entry.Sha256))
                    ->
                    { acc with
                        Materializable = acc.Materializable @ [ entry, verified ] }
                | _ ->
                    // Any missing/extra/unreadable file or digest mismatch invalidates the closed
                    // directory transport. Never materialize a partial row.
                    { acc with
                        VerifyFailed = acc.VerifyFailed @ [ entry.Id ] }

    /// Plan driver materialization from an explicit manifest text + id→body map, gated by the
    /// present skill-id set. The pure core of `plan`, factored out so the fail-closed classes
    /// (tamper, id collision, unevaluable predicate) are testable without the compiled-in bytes.
    let planFilesFrom
        (manifestText: string option)
        (files: Map<string * string, byte array>)
        (presentIds: Set<string>)
        : DriverOutcome =
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
                    ||> List.fold (classifyEntry presentIds files)

                // The declared scope of each manifest row, so a materialized id can be declared in
                // the product manifest with the scope its own producer assigned it (ADR-0063 tail).
                let scopeById =
                    manifest.Skills |> List.map (fun skill -> skill.Id, skill.Scope) |> Map.ofList

                // Fan the verified bodies into every declared root through the shared mirror,
                // exactly as the seeded skeleton does — deterministic (id-sorted, roots in order),
                // byte-identical across roots by construction, all no-clobber.
                let materializedFiles =
                    classified.Materializable
                    |> List.collect (fun (entry, files) ->
                        [ for root in Fsgg.Schemas.agentSkillRoots do
                              for file, body in files do
                                  let path = $"{root}/skills/{entry.Id}/{file.Path}"
                                  yield path, body, file ])

                { Writes =
                    materializedFiles
                    |> List.collect (fun (path, body, file) ->
                        [ yield WriteFile(path, body, AgentGuidanceTarget)

                          if file.Executable then
                              yield SetExecutable path ])
                  ProvenancePaths = materializedFiles |> List.map (fun (path, _, file) -> path, file.Sha256)
                  MaterializedIds = classified.Materializable |> List.map (fun (entry, _) -> entry.Id)
                  MaterializedScopes =
                    classified.Materializable
                    |> List.choose (fun (entry, _) ->
                        Map.tryFind entry.Id scopeById |> Option.map (fun scope -> entry.Id, scope))
                    |> Map.ofList
                  VerifyFailedIds = classified.VerifyFailed
                  PredicateUnevaluatedIds = classified.PredicateUnevaluated
                  NamespaceCollisionIds = classified.Collisions
                  ManifestError = None }

    /// Legacy test seam: map each supplied body to `SKILL.md`. Schema-v2 callers should use
    /// `planFilesFrom` so auxiliary bytes are part of the closed transport.
    let planFrom (manifestText: string option) (bodies: Map<string, string>) (presentIds: Set<string>) : DriverOutcome =
        let files =
            bodies
            |> Map.toList
            |> List.map (fun (id, body) -> (id, "SKILL.md"), strictUtf8.GetBytes body)
            |> Map.ofList

        planFilesFrom manifestText files presentIds

    /// Plan driver materialization from the CLI's embedded package bytes, gated by the set of
    /// skill ids already present in the workspace (seeded ∪ provider). Pure — reads only
    /// compiled-in resources (FR-002 — no NuGet cache / network at scaffold time).
    let plan (presentIds: Set<string>) : DriverOutcome =
        planFilesFrom (manifestText ()) (embeddedFiles ()) presentIds
