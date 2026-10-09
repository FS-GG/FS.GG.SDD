namespace FS.GG.SDD.Commands

open System
open System.IO
open System.IO.Compression
open System.Text.Json
open System.Xml.Linq
open System.Text
open System.Threading
open System.Threading.Tasks
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.Internal
open FS.GG.SDD.Commands.CommandTypes

/// The actual edge owns resources; the pure workflow only accepts their observed projection.
module CatalogScaffoldEffects =
    module Linux = CatalogScaffoldLinux
    module Workflow = CatalogScaffoldWorkflow
    type HostMode = LocalLinux
    type HostSelection = { Mode: HostMode; TransportExecutable: string }
    type OwnershipStatus = NotStarted | Active | Settled | Unresolved
    type OperationObservation =
        { Ownership: OwnershipStatus
          Model: Workflow.Model option
          Diagnostics: Fsgg.ProviderCatalog.Diagnostic list }

    // Constructed before all effect dispatch. The operation is the caller-held lifetime
    // owner of the cold runner, low-level IO/child inventories, original phase budgets,
    // physical root bindings, immutable original template bytes and failure outcome.
    type private PhaseSnapshot =
        { Phase: string
          Native: Linux.Custody
          RootIdentity: Linux.ObjectIdentity
          mutable Files: Linux.CapturedFile list
          mutable Released: bool }

    type private OwnedChild =
        { Budget: Linux.PhaseBudget
          Child: Linux.ChildCustody
          mutable Settled: bool }
    type private InputRoot =
        { Root: Linux.DirectoryCustody
          Files: Linux.CapturedFile list }

    type Operation =
        private
            { Gate: obj
              Host: HostSelection
              Request: Workflow.CatalogScaffoldRequest
              Cancellation: CancellationToken
              Native: Linux.Custody
              mutable Runner: Task<Workflow.Model> option
              mutable Changed: TaskCompletionSource<unit>
              mutable Model: Workflow.Model
              mutable PhaseBudget: Linux.PhaseBudget option
              mutable Parent: Linux.DirectoryCustody option
              mutable PrivateRoot: Linux.DirectoryCustody option
              mutable Workspace: Linux.DirectoryCustody option
              mutable PrivateLeaf: string option
              mutable OriginalTemplate: Linux.CapturedFile list
              mutable ExpectedManifest: (string * byte array) list
              mutable ExpectedToolManifest: byte array option
              mutable Children: OwnedChild list
              mutable InputRoots: InputRoot list
              Snapshots: ResizeArray<PhaseSnapshot>
              mutable SnapshotBytes: int
              mutable Released: bool
              mutable NativeReleased: bool
              mutable RetirementAttempted: bool
              mutable FirstFailure: exn option
              mutable Ownership: OwnershipStatus }

    type private CompositionPlan =
        { Writes: (string * byte array * ArtifactWriteKind * ArtifactRef.ArtifactOwner) list
          Directories: string list
          Executables: (string * ArtifactRef.ArtifactOwner) list
          ProviderMirrors: (string * string) list
          ExpectedManifest: (string * byte array) list
          ExpectedToolManifest: byte array
          DriverPaths: (string * string) list
          GamePaths: (string * string) list
          RenderingPaths: (string * string) list }

    let private diagnostic code message: Fsgg.ProviderCatalog.Diagnostic =
        { Code = code; Path = "$.scaffold"; Message = message }
    let private refuse code message = raise (Linux.Refused [diagnostic code message])
    let private utf8 = UTF8Encoding(false,true)
    let private requiredText (value: string | null) =
        match value with
        | null -> refuse "catalog.nullMetadata" "A required selected path or metadata string is null."
        | text -> text
    let private jsonString (element: JsonElement) = element.GetString() |> requiredText
    let private manifestSource = Fsgg.SkillMirror.providerSourceRoot + "/skills/skill-manifest.json"

    let private requireResult result =
        match result with
        | Ok value -> value
        | Error diagnostics -> raise (Linux.Refused diagnostics)

    // Only captured declaration bytes enter these pure parsers. The actual local archive
    // still requires a held physical read and exact hash before InputsVerified is emitted.
    let private declaredInputs (request: Workflow.CatalogScaffoldRequest) =
        let preview = Workflow.prepare request.Selection |> requireResult
        let policy = CatalogScaffoldPolicy.parse request.ExpectedPolicyDigest request.PolicyBytes |> requireResult
        let selected =
            preview.Selected
            |> Option.defaultWith (fun () -> refuse "catalog.providerRequired" "An explicit provider is required.")
        if request.PreflightTimeoutSeconds > policy.MaximumPreflightSeconds
           || request.ScaffoldTimeoutSeconds > policy.MaximumScaffoldSeconds then
            refuse "catalog.budgetRefused" "Requested phase budgets exceed independently selected policy."
        // The full request is resolved again at the actual dispatch boundary. This early
        // pure refusal does not convert a prepared descriptor into execution authority.
        ProviderCapabilityAdmission.resolve policy request.SelectedPlatform selected.Descriptor
        |> requireResult
        |> ignore
        let archive =
            policy.Archives
            |> List.filter (fun association -> association.TemplateSource = selected.Descriptor.TemplateSource)
            |> function
                | [association] -> association
                | _ -> refuse "catalog.archiveAssociationRefused" "Exactly one independent policy association for the selected template source is required."
        preview, policy, selected, archive

    type private ArchiveTemplate =
        { Identity: string
          ShortName: string
          Archive: CatalogScaffoldProvenance.ArchiveIdentity
          Parameters: Map<string,string option>
          ConfigPlace: string
          ExpectedPayload: (string * byte array) list }

    // Pure inspection of bytes already captured through held native file custody.
    // The first transport profile accepts data-only archives, never host callbacks.
    let private archiveTemplate (selected: Fsgg.ProviderCatalog.PreparedConfiguration)
                                (association: CatalogScaffoldPolicy.ArchiveAssociation)
                                (expectedDigest: string) (bytes: byte array) =
        if ProviderCatalogIntegrity.digest bytes <> expectedDigest then
            refuse "catalog.archiveDigestMismatch" "The held local archive differs from the explicit selected digest."
        use stream = new MemoryStream(bytes,false)
        use archive = new ZipArchive(stream,ZipArchiveMode.Read,false)
        let names = Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let mutable expanded = 0L
        for entry in archive.Entries do
            let name = entry.FullName
            let components = name.TrimEnd('/').Split('/')
            if String.IsNullOrWhiteSpace name || name.Contains('\\') || name.StartsWith("/",StringComparison.Ordinal)
               || (components |> Array.exists (fun segment -> segment = "" || segment = "." || segment = ".." || segment.Contains(':'))) then
                refuse "catalog.archivePathRefused" "Archive entries must have unambiguous relative data paths."
            if not(names.Add name) then refuse "catalog.archivePathRefused" "Duplicate or case-colliding archive entries are refused."
            let kind = (entry.ExternalAttributes >>> 16) &&& 0xf000
            if kind <> 0 && kind <> 0x8000 && kind <> 0x4000 then
                refuse "catalog.archiveEntryRefused" "Archive links and special entries are not supported by the data-only profile."
            expanded <- expanded + entry.Length
            if expanded > 64L * 1024L * 1024L then refuse "catalog.archiveLimit" "Expanded archive data exceeds the first-profile bound."
            if name.EndsWith("/dotnetcli.host.json",StringComparison.OrdinalIgnoreCase)
               || (["tools/";"runtimes/";"lib/"] |> List.exists (fun prefix -> name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))) then
                refuse "catalog.archiveHostRefused" "Executable host extensions and runtime dependencies are outside the data-only profile."
        let readEntry (entry: ZipArchiveEntry) =
            use input = entry.Open()
            use output = new MemoryStream()
            input.CopyTo output
            if output.Length <> entry.Length then refuse "catalog.archiveEntryRefused" "Archive entry length does not match its declaration."
            output.ToArray()
        let unique (suffix: string) =
            archive.Entries |> Seq.filter (fun entry -> entry.FullName.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)) |> Seq.toList
            |> function
                | [entry] -> entry
                | _ -> refuse "catalog.archiveAssociationRefused" "The selected archive must contain exactly one nuspec and template declaration."
        let nuspec = unique ".nuspec" |> readEntry |> utf8.GetString |> XDocument.Parse
        let values (name: string) = nuspec.Descendants() |> Seq.filter(fun node -> node.Name.LocalName = name) |> Seq.toList
        let one (name: string) =
            match values name with
            | [value] -> value.Value
            | _ -> refuse "catalog.archiveAssociationRefused" "The nuspec package identity is missing or ambiguous."
        if one "id" <> association.PackageId || one "version" <> association.PackageVersion then
            refuse "catalog.archiveAssociationRefused" "Actual nuspec identity differs from the independently selected archive association."
        if not (List.isEmpty (values "dependency")) then
            refuse "catalog.archiveDependencyRefused" "Template dependency acquisition is outside the first data-only profile."
        let templateEntry = unique "/.template.config/template.json"
        let contentRoot = templateEntry.FullName.Substring(0,templateEntry.FullName.Length - ".template.config/template.json".Length)
        use template = templateEntry |> readEntry |> fun raw -> JsonDocument.Parse(ReadOnlyMemory<byte>(raw))
        let rec rejectDuplicateProperties (value: JsonElement) =
            match value.ValueKind with
            | JsonValueKind.Object ->
                let seen = Collections.Generic.HashSet<string>(StringComparer.Ordinal)
                for property in value.EnumerateObject() do
                    if not(seen.Add property.Name) then refuse "catalog.templateDeclarationRefused" "Duplicate template declaration properties are refused."
                    rejectDuplicateProperties property.Value
            | JsonValueKind.Array -> value.EnumerateArray() |> Seq.iter rejectDuplicateProperties
            | _ -> ()
        let root = template.RootElement
        rejectDuplicateProperties root
        let templateKeys = root.EnumerateObject() |> Seq.map(fun property -> property.Name) |> Set.ofSeq
        if not(Set.isSubset templateKeys (Set.ofList ["$schema";"author";"identity";"name";"shortName";"preferNameDirectory";"symbols"])) then
            refuse "catalog.templateBehaviorRefused" "This first qualified transport profile supports literal data templates; other transformations need their own actual qualification."
        let stringValue (key: string) =
            let mutable value = Unchecked.defaultof<JsonElement>
            if not(root.TryGetProperty(key,&value)) || value.ValueKind <> JsonValueKind.String then
                refuse "catalog.templateDeclarationRefused" "The template identity and short name must be explicit strings."
            jsonString(value)
        let mutable forbidden = Unchecked.defaultof<JsonElement>
        if root.TryGetProperty("postActions",&forbidden) && forbidden.ValueKind <> JsonValueKind.Null
           && (forbidden.ValueKind <> JsonValueKind.Array || forbidden.GetArrayLength() <> 0) then
            refuse "catalog.templatePostActionsRefused" "Template post-actions are outside the data-only profile."
        let shortName = stringValue "shortName"
        if shortName <> selected.Descriptor.TemplateId then
            refuse "catalog.templateAssociationRefused" "Actual template short name differs from the selected descriptor."
        for name in ["sourceName";"sources";"primaryOutputs";"constraints"] do
            let mutable unsupported = Unchecked.defaultof<JsonElement>
            if root.TryGetProperty(name,&unsupported) && unsupported.ValueKind <> JsonValueKind.Null then
                refuse "catalog.templateBehaviorRefused" "Custom path transformation and conditional source selection require another qualified transport profile."
        let parameters = Collections.Generic.Dictionary<string,string option>(StringComparer.Ordinal)
        let replacements = ResizeArray<string * string>()
        let effective = Map.ofList selected.EffectiveParameters
        let mutable symbols = Unchecked.defaultof<JsonElement>
        if root.TryGetProperty("symbols",&symbols) then
            if symbols.ValueKind <> JsonValueKind.Object then refuse "catalog.templateDeclarationRefused" "Template symbols must be an object."
            for symbol in symbols.EnumerateObject() do
                let keys = symbol.Value.EnumerateObject() |> Seq.map(fun property -> property.Name) |> Set.ofSeq
                if not(Set.isSubset keys (Set.ofList ["type";"datatype";"replaces";"defaultValue"])) then
                    refuse "catalog.templateBehaviorRefused" "Custom symbol behavior or host metadata requires another qualified profile."
                let mutable dataType = Unchecked.defaultof<JsonElement>
                if symbol.Value.TryGetProperty("datatype",&dataType) && jsonString(dataType) <> "string" then
                    refuse "catalog.templateSymbolRefused" "The first profile supports literal string parameters only."
                let mutable kind = Unchecked.defaultof<JsonElement>
                if not(symbol.Value.TryGetProperty("type",&kind)) || jsonString(kind) <> "parameter" then
                    refuse "catalog.templateSymbolRefused" "The first profile supports literal parameter symbols only."
                let mutable defaultValue = Unchecked.defaultof<JsonElement>
                let defaultText =
                    if symbol.Value.TryGetProperty("defaultValue",&defaultValue) then
                        if defaultValue.ValueKind <> JsonValueKind.String then refuse "catalog.templateSymbolRefused" "Parameter defaults must be literal strings."
                        Some(jsonString(defaultValue))
                    else None
                let mutable rename = Unchecked.defaultof<JsonElement>
                if symbol.Value.TryGetProperty("fileRename",&rename) then
                    refuse "catalog.templateBehaviorRefused" "Parameter-driven filename changes require another qualified transport profile."
                let mutable token = Unchecked.defaultof<JsonElement>
                if symbol.Value.TryGetProperty("replaces",&token) then
                    if token.ValueKind <> JsonValueKind.String || String.IsNullOrEmpty(jsonString(token)) then
                        refuse "catalog.templateSymbolRefused" "Literal replacement tokens must be nonempty strings."
                    let value = Map.tryFind symbol.Name effective |> Option.orElse defaultText |> Option.defaultValue ""
                    replacements.Add(jsonString(token),value)
                if not(parameters.TryAdd(symbol.Name,defaultText)) then
                    refuse "catalog.templateSymbolRefused" "Duplicate template parameter symbols are refused."
        for parameter in selected.Descriptor.Parameters do
            if not(parameters.ContainsKey parameter.Key) then
                refuse "catalog.templateParameterRefused" "A declared provider parameter is absent from the actual archive."
        { Identity = stringValue "identity"
          ShortName = shortName
          Archive = { Id = association.PackageId; Version = association.PackageVersion; Digest = expectedDigest }
          Parameters = parameters |> Seq.map(fun pair -> pair.Key,pair.Value) |> Map.ofSeq
          ConfigPlace = "/" + templateEntry.FullName
          ExpectedPayload =
            archive.Entries
            |> Seq.filter(fun entry -> entry.FullName.StartsWith(contentRoot,StringComparison.Ordinal) && not(entry.FullName.EndsWith("/",StringComparison.Ordinal))
                                      && not(entry.FullName.StartsWith(contentRoot + ".template.config/",StringComparison.Ordinal)))
            |> Seq.map(fun entry ->
                let relative = entry.FullName.Substring(contentRoot.Length)
                let raw = readEntry entry
                let expected =
                    if replacements.Count = 0 then raw
                    else
                        let mutable body = utf8.GetString raw
                        for token,value in replacements do body <- body.Replace(token,value,StringComparison.Ordinal)
                        utf8.GetBytes body
                relative,expected)
            |> Seq.sortBy fst
            |> Seq.toList }

    let private initRequest (targetRoot: string) (generator: SchemaVersion.GeneratorVersion) (selected: Fsgg.ProviderCatalog.PreparedConfiguration) : CommandRequest =
        { Command = Scaffold; ProjectRoot = targetRoot; WorkId = None; Title = None
          InputText = None; OutputFormat = Json; DryRun = false; GeneratorVersion = generator
          Provider = Some selected.Descriptor.Id; Parameters = selected.EffectiveParameters
          Force = false; TemplateUpdate = false; AssumeYes = false; IsInteractive = false
          Artifact = None; Explain = false; FromTests = None; FromTestReport = None
          SyncObservedRun = None; SurfaceUpdate = false; AcceptUpstream = false; RequireObserved = false }

    let private compositionPlan (targetRoot: string) (generator: SchemaVersion.GeneratorVersion) (selected: Fsgg.ProviderCatalog.PreparedConfiguration) (original: Linux.CapturedFile list) =
        let paths = original |> List.map (fun value -> value.Path)
        let captured = original |> List.map (fun value -> value.Path,value.Bytes) |> Map.ofList
        let directories = ResizeArray<string>()
        let executables = ResizeArray<string * ArtifactRef.ArtifactOwner>()
        let writes = ResizeArray<string * byte array * ArtifactWriteKind * ArtifactRef.ArtifactOwner>()
        // This interprets only shared pure materialization plans. Legacy process effects
        // cannot gain authority through the catalog route or this composition function.
        let materialize owner effects =
            for effect in effects do
                match effect with
                | CreateDirectory path -> directories.Add path
                | WriteFile(path,text,kind) -> writes.Add(path,utf8.GetBytes text,kind,owner)
                | SetExecutable path -> executables.Add(path,owner)
                | _ -> refuse "catalog.compositionEffectRefused" "The shared composition plan contains a non-materialization effect."
        Foundation.scaffoldInitEffects (initRequest targetRoot generator selected)
        |> materialize ArtifactRef.Sdd
        let effective = Map.ofList selected.EffectiveParameters
        let ignoreBytes = HandlersScaffold.composeRootGitignoreBytes (Map.tryFind "lifecycle" effective = Some "typed-sdd") (Map.tryFind ".gitignore" captured)
        writes.Add(".gitignore",ignoreBytes,HybridArtifact(SectionMerge([], [], [])),ArtifactRef.Sdd)

        let toolManifestPath = ScaffoldMutation.toolManifestPath
        let expectedToolManifest =
            match Map.tryFind toolManifestPath captured with
            | None ->
                let seed = ScaffoldMutation.toolManifestText generator.Version |> utf8.GetBytes
                writes.Add(toolManifestPath,seed,StructuredSource,ArtifactRef.Sdd)
                seed
            | Some originalBytes ->
                match ScaffoldMutation.mergeCatalogToolManifestBytes generator.Version originalBytes with
                | Error _ -> refuse "catalog.toolManifestRefused" "The captured tool manifest is ambiguous, malformed or conflicts with the exact shared tool selections."
                | Ok None -> Array.copy originalBytes // No rewrite of an already complete manifest.
                | Ok(Some mergedBytes) ->
                    // A hybrid merge owns entries only. The whole-file path remains
                    // provider-produced, matching the existing legacy ownership contract.
                    writes.Add(toolManifestPath,mergedBytes,HybridArtifact StructuredMerge,ArtifactRef.GeneratedProduct)
                    mergedBytes

        let driver = HandlersScaffold.plannedDriverOutcome paths
        let predicates = AudioSkills.ownerPredicateParameters selected.Descriptor.TemplateId effective
        let gameSkills = HandlersScaffold.plannedGameSkillOutcome paths predicates
        let rendering = HandlersScaffold.plannedRenderingSkillOutcome paths predicates (gameSkills.ProvenancePaths |> List.map fst)
        let audio = HandlersScaffold.plannedAudioSkillOutcome paths predicates ((gameSkills.ProvenancePaths @ rendering.ProvenancePaths) |> List.map fst)
        let problems =
            HandlersScaffold.driverDiagnostics driver
            @ HandlersScaffold.gameSkillDiagnostics gameSkills
            @ HandlersScaffold.renderingSkillDiagnostics rendering
            @ HandlersScaffold.audioSkillDiagnostics audio
        if not problems.IsEmpty then
            refuse "catalog.skillMaterializationRefused" "The shared skill plan reported a manifest, collision, verification or predicate diagnostic."
        materialize ArtifactRef.Driver driver.Writes
        materialize ArtifactRef.GameSkill gameSkills.Writes
        materialize ArtifactRef.RenderingSkill rendering.Writes
        materialize ArtifactRef.RenderingSkill audio.Writes
        let mirrors =
            HandlersScaffold.providerSkillFiles paths
            |> List.collect (fun source -> HandlersScaffold.mirrorTargetsFor source |> List.map (fun target -> source,target))
        let additions = HandlersScaffold.productManifestAdditions driver gameSkills rendering audio
        let expectedManifest =
            match Map.tryFind manifestSource captured with
            | None -> [] // Match legacy behavior: never synthesize a missing provider manifest.
            | Some originalBytes ->
                let expected =
                    if additions.IsEmpty then Array.copy originalBytes
                    else
                        match Fsgg.SkillMirror.decodeBody originalBytes with
                        | Error _ -> refuse "catalog.productManifestRefused" "The captured provider manifest bytes cannot be decoded."
                        | Ok originalText ->
                            match ProductSkillManifest.amend originalText additions with
                            | Ok amended -> utf8.GetBytes amended
                            | Error _ -> refuse "catalog.productManifestRefused" "The actual shared manifest union refused the captured provider declaration."
                (manifestSource :: HandlersScaffold.mirrorTargetsFor manifestSource)
                |> List.map (fun path -> path,Array.copy expected)
        { Writes = List.ofSeq writes
          Directories = List.ofSeq directories
          Executables = executables |> Seq.distinct |> Seq.toList
          ProviderMirrors = mirrors
          ExpectedManifest = expectedManifest
          ExpectedToolManifest = expectedToolManifest
          DriverPaths = driver.ProvenancePaths
          GamePaths = gameSkills.ProvenancePaths
          RenderingPaths = rendering.ProvenancePaths @ audio.ProvenancePaths }

    let private verifyManifestUnion (expected: (string * byte array) list) (observed: Linux.CapturedFile list) =
        let actual = observed |> List.map (fun value -> value.Path,value.Bytes) |> Map.ofList
        for path,bytes in expected do
            if Map.tryFind path actual <> Some bytes then
                refuse "catalog.productManifestMismatch" "The final source manifest or a shared mirror differs from the exact shared amend of the captured original and sole declared addition set."

    let private verifyToolManifest (expected: byte array) (observed: Linux.CapturedFile list) =
        match observed |> List.tryFind (fun value -> value.Path = ScaffoldMutation.toolManifestPath) with
        | Some actual when actual.Bytes = expected && actual.Sha256 = ProviderCatalogIntegrity.digest expected -> ()
        | _ -> refuse "catalog.toolManifestMismatch" "The final tool manifest does not equal the exact shared seed or merge of the immutable original capture."

    // Cache JSON may be atomically replaced by a later SDK invocation. Each closed
    // phase therefore gets its own first-identity inventory. The main operation
    // retains that inventory before dispatch and retains the same original root
    // owner until every snapshot and SDK child is actually settled and released.
    let private captureSdkSnapshot (operation: Operation) (budget: Linux.PhaseBudget) (phase: string) =
        Linux.checkWork budget
        if not (Linux.settled operation.Native) then
            refuse "catalog.snapshotWhileActive" "SDK children and their readers must settle before raw cache capture."
        let root =
            match operation.PrivateRoot with
            | Some root -> root
            | None -> refuse "catalog.snapshotRootMissing" "The original held operation root is unavailable."
        let snapshot =
            { Phase = phase
              Native = Linux.createCustody ()
              RootIdentity = Linux.identity root
              Files = []
              Released = false }
        lock operation.Gate (fun () -> operation.Snapshots.Add snapshot)
        // The root and its parent descriptor chain stay owned by operation.Native.
        // files owns only its new opens, allocations and cold IO task in snapshot.Native.
        // Releasing the snapshot cannot close the passed original root descriptor.
        Linux.revalidateDirectory budget root
        let remaining = lock operation.Gate (fun () -> 8 * 1024 * 1024 - operation.SnapshotBytes)
        let captured = Linux.files snapshot.Native budget root remaining
        Linux.revalidateDirectory budget root
        if Linux.identity root <> snapshot.RootIdentity then
            refuse "catalog.snapshotRootChanged" "The cache snapshot no longer belongs to the same held operation directory."
        let retained = captured |> List.map (fun value -> { value with Bytes = Array.copy value.Bytes })
        let size = retained |> List.sumBy (fun value -> value.Bytes.Length)
        lock operation.Gate (fun () ->
            snapshot.Files <- retained
            operation.SnapshotBytes <- operation.SnapshotBytes + size)
        // No finally release: a failed or unsettled snapshot remains in the actual
        // operation inventory, so the original runner/owner must retain it.
        if not (Linux.settled snapshot.Native) then
            refuse "catalog.snapshotUnsettled" "Raw cache capture still owns an unfinished native IO task."
        Linux.checkCleanup budget
        Linux.releaseKnown budget snapshot.Native
        lock operation.Gate (fun () -> snapshot.Released <- true)
        retained

    let private snapshotsReleased (operation: Operation) =
        lock operation.Gate (fun () ->
            operation.Snapshots |> Seq.forall (fun snapshot -> snapshot.Released && Linux.settled snapshot.Native))

    let private releaseSnapshotCustodies (operation: Operation) (budget: Linux.PhaseBudget) =
        // Called by the original operation's cleanup path; never grants new business.
        let snapshots = lock operation.Gate (fun () -> operation.Snapshots |> Seq.toList)
        for snapshot in snapshots do
            if not snapshot.Released && Linux.settled snapshot.Native then
                Linux.checkCleanup budget
                Linux.releaseKnown budget snapshot.Native
                lock operation.Gate (fun () -> snapshot.Released <- true)
        snapshotsReleased operation

    let private requiredCapture (path: string) (files: Linux.CapturedFile list) =
        files |> List.tryFind(fun file -> file.Path = path)
        |> Option.defaultWith(fun () -> refuse "catalog.transportStateMissing" "The supported SDK did not produce required state under the held operation root.")

    let private parseSdkCacheJson (raw: byte array) =
        // SDK-owned JSON is UTF-8 and may carry its standard encoding marker.
        // Parse a view only: original captured bytes and their digest stay unchanged.
        let offset = if raw.Length >= 3 && raw[0] = 0xEFuy && raw[1] = 0xBBuy && raw[2] = 0xBFuy then 3 else 0
        JsonDocument.Parse(ReadOnlyMemory<byte>(raw,offset,raw.Length-offset))

    let private verifySdkMountInventory (builtinMounts: Set<string>) installed selectedMount (mounted: Set<string>) =
        let valid =
            if installed then mounted = Set.add selectedMount builtinMounts
            else Set.isSubset mounted builtinMounts
        if not valid then
            refuse "catalog.transportMountMismatch" "SDK mounts must be captured builtin archives before install, then exactly the observed baseline plus the selected local archive."
        mounted

    let private verifyTransportCache (template: ArchiveTemplate) (builtinMounts: Set<string>) installed (files: Linux.CapturedFile list) =
        if files |> List.exists(fun file -> file.Path.StartsWith("home/.templateengine/",StringComparison.Ordinal) || file.Path.StartsWith("cli-home/.templateengine/",StringComparison.Ordinal)) then
            refuse "catalog.transportStateEscape" "SDK engine state appeared outside the explicitly held private hive."
        let cacheBytes = (requiredCapture "engine/dotnetcli/10.0.401/templatecache.json" files).Bytes
        use cache = parseSdkCacheJson cacheBytes
        let mounted = cache.RootElement.GetProperty("MountPointsInfo").EnumerateObject() |> Seq.map(fun property -> property.Name) |> Set.ofSeq
        let packageName = template.Archive.Id + "." + template.Archive.Version + ".nupkg"
        let selectedMount = "/proc/self/fd/3/engine/packages/" + packageName
        verifySdkMountInventory builtinMounts installed selectedMount mounted |> ignore
        if installed then
            let rows =
                cache.RootElement.GetProperty("TemplateInfo").EnumerateArray()
                |> Seq.filter(fun row -> jsonString(row.GetProperty("Identity")) = template.Identity) |> Seq.toList
            let row =
                match rows with
                | [row] -> row
                | _ -> refuse "catalog.templateAssociationRefused" "The installed template identity is absent or ambiguous."
            if jsonString(row.GetProperty("MountPointUri")) <> selectedMount
               || jsonString(row.GetProperty("ConfigPlace")) <> template.ConfigPlace
               || (row.GetProperty("ShortNameList").EnumerateArray() |> Seq.map(fun item -> jsonString(item)) |> Seq.toList) <> [template.ShortName] then
                refuse "catalog.templateAssociationRefused" "Installed template aliases/config/package association differ from the captured archive."
            for key in ["HostData";"HostConfigPlace";"LocaleConfigPlace"] do
                if row.GetProperty(key).ValueKind <> JsonValueKind.Null then
                    refuse "catalog.templateHostRefused" "Custom host and locale mappings require another qualified profile."
            for key in ["PostActions";"Constraints"] do
                if row.GetProperty(key).GetArrayLength() <> 0 then refuse "catalog.templateBehaviorRefused" "Unsupported SDK template behavior was observed."
            let parameters = row.GetProperty("Parameters").EnumerateArray() |> Seq.toList
            let actualNames = parameters |> List.map(fun parameter -> jsonString(parameter.GetProperty("Name")))
            let expectedNames = Set.add "name" (template.Parameters |> Map.keys |> Set.ofSeq)
            if Set.ofList actualNames <> expectedNames || actualNames.Length <> expectedNames.Count then
                refuse "catalog.templateParameterRefused" "SDK parameter inventory differs from the complete actual template declaration."
            for KeyValue(key,defaultText) in template.Parameters do
                let actual = parameters |> List.filter(fun parameter -> jsonString(parameter.GetProperty("Name")) = key)
                match actual with
                | [parameter] ->
                    let mutable value = Unchecked.defaultof<JsonElement>
                    let observedDefault =
                        if parameter.TryGetProperty("DefaultValue",&value) && value.ValueKind = JsonValueKind.String then Some(jsonString(value))
                        else None
                    if observedDefault <> defaultText then refuse "catalog.templateParameterRefused" "SDK parameter default differs from the literal archive declaration."
                    let keys = parameter.EnumerateObject() |> Seq.map(fun property -> property.Name) |> Set.ofSeq
                    let expectedKeys = Set.ofList ["Documentation";"Name";"Priority";"Precedence";"Type";"IsName";"DefaultValue";"DataType";"DefaultIfOptionWithoutValue";"Choices";"Description";"DisplayName";"AllowMultipleValues"]
                    if keys <> expectedKeys
                       || parameter.GetProperty("Priority").GetInt32() <> 2
                       || jsonString(parameter.GetProperty("Type")) <> "parameter"
                       || jsonString(parameter.GetProperty("DataType")) <> "string"
                       || parameter.GetProperty("IsName").GetBoolean()
                       || parameter.GetProperty("AllowMultipleValues").GetBoolean() then
                        refuse "catalog.templateParameterRefused" "SDK parameter type/shape differs from the qualified literal profile."
                    for field in ["Documentation";"Description";"DisplayName"] do
                        if parameter.GetProperty(field).GetString() <> "" then refuse "catalog.templateParameterRefused" "Unqualified parameter documentation mapping was observed."
                    for field in ["DefaultIfOptionWithoutValue";"Choices"] do
                        if parameter.GetProperty(field).ValueKind <> JsonValueKind.Null then refuse "catalog.templateParameterRefused" "Implicit value or choice transformations are outside the literal profile."
                    let precedence = parameter.GetProperty("Precedence")
                    if (precedence.EnumerateObject() |> Seq.map(fun property -> property.Name) |> Set.ofSeq) <> Set.ofList ["PrecedenceDefinition";"IsRequiredCondition";"IsEnabledCondition";"IsRequired";"CanBeRequired"]
                       || precedence.GetProperty("PrecedenceDefinition").GetInt32() <> 2
                       || precedence.GetProperty("IsRequired").GetBoolean()
                       || precedence.GetProperty("CanBeRequired").GetBoolean()
                       || precedence.GetProperty("IsRequiredCondition").ValueKind <> JsonValueKind.Null
                       || precedence.GetProperty("IsEnabledCondition").ValueKind <> JsonValueKind.Null then
                        refuse "catalog.templateParameterRefused" "Conditional parameter behavior is outside the literal profile."
                | _ -> refuse "catalog.templateParameterRefused" "SDK parameter metadata is ambiguous."
            let installedArchive = requiredCapture ("engine/packages/" + packageName) files
            if installedArchive.Sha256 <> template.Archive.Digest then refuse "catalog.installedArchiveChanged" "Installed package bytes differ from the exact captured archive."
            use packages = parseSdkCacheJson (requiredCapture "engine/packages.json" files).Bytes
            let package =
                packages.RootElement.GetProperty("Packages").EnumerateArray() |> Seq.toList
                |> function
                    | [package] -> package
                    | _ -> refuse "catalog.packageInventoryRefused" "Private SDK state must contain exactly the selected local package."
            let details = package.GetProperty("Details")
            if jsonString(details.GetProperty("PackageId")) <> template.Archive.Id
               || jsonString(details.GetProperty("Version")) <> template.Archive.Version
               || jsonString(details.GetProperty("LocalPackage")) <> "True"
               || jsonString(package.GetProperty("MountPointUri")) <> selectedMount then
                refuse "catalog.packageAssociationRefused" "Package identity/version/local/mount association differs from the independently selected archive."
        mounted

    let private verifyAliases (selected: Fsgg.ProviderCatalog.PreparedConfiguration) (stdout: byte array) =
        let help = utf8.GetString stdout
        let start = help.IndexOf("Template options:",StringComparison.Ordinal)
        if start < 0 then refuse "catalog.templateAliasesMissing" "The SDK did not expose an actual template option section."
        let options = help.Substring start
        for parameter in selected.Descriptor.Parameters do
            if not(options.Contains("--" + parameter.Key + " ",StringComparison.Ordinal))
               || options.Contains("--param:" + parameter.Key,StringComparison.Ordinal) then
                refuse "catalog.templateAliasRefused" "SDK renaming or unavailable literal long aliases refuse the original provider key."

    let private verifyTemplatePayload (template: ArchiveTemplate) (files: Linux.CapturedFile list) =
        let actual = files |> List.map(fun file -> file.Path,file.Bytes) |> Map.ofList
        let expected = Map.ofList template.ExpectedPayload
        if actual <> expected then
            refuse "catalog.templatePayloadMismatch" "Actual output differs from the captured literal template and complete effective parameters, including empty overrides."

    let private publish (operation: Operation) (model: Workflow.Model) ownership =
        let prior =
            lock operation.Gate (fun () ->
                operation.Model <- model
                operation.Ownership <- ownership
                let prior = operation.Changed
                operation.Changed <- TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                prior)
        prior.TrySetResult () |> ignore

    let private send (operation: Operation) message =
        let model = lock operation.Gate (fun () -> operation.Model)
        let updated,effects = Workflow.update message model
        publish operation updated Active
        effects

    let private originalRoot (operation: Operation) =
        operation.PrivateRoot |> Option.defaultWith(fun () -> refuse "catalog.operationRootMissing" "The original held operation root is missing.")

    let private workspaceRoot (operation: Operation) =
        operation.Workspace |> Option.defaultWith(fun () -> refuse "catalog.workspaceMissing" "The original held workspace root is missing.")

    let private fixedEnvironment =
        [ "PATH","/usr/bin:/bin"
          "DOTNET_PROCESSOR_COUNT","2"
          "DOTNET_CLI_TELEMETRY_OPTOUT","1"
          "DOTNET_SKIP_FIRST_TIME_EXPERIENCE","1"
          "DOTNET_NOLOGO","1"
          "DOTNET_GENERATE_ASPNET_CERTIFICATE","false"
          "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE","true"
          "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH","false"
          "DOTNET_CLI_USE_MSBUILD_SERVER","0"
          "DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER","1"
          "MSBUILDDISABLENODEREUSE","1"
          "DOTNET_ROLL_FORWARD","Disable"
          "LANG","C.UTF-8"
          "LC_ALL","C.UTF-8" ]
    let private environmentPaths =
        [ "HOME","home";"DOTNET_CLI_HOME","cli-home";"TMPDIR","tmp";"TMP","tmp";"TEMP","tmp"
          "NUGET_PACKAGES","cache";"NUGET_HTTP_CACHE_PATH","http-cache";"NUGET_PLUGINS_CACHE_PATH","plugin-cache"
          "NUGET_SCRATCH","scratch";"DOTNET_BUNDLE_EXTRACT_BASE_DIR","bundle"
          "XDG_CONFIG_HOME","xdg-config";"XDG_CACHE_HOME","xdg-cache";"XDG_DATA_HOME","xdg-data" ]
    let private environment (operation: Operation) =
        let root = originalRoot operation
        (fixedEnvironment |> List.map(fun (key,value) -> key,Linux.Literal value))
        @ (environmentPaths |> List.map(fun (key,path) -> key,Linux.DirectoryPath(root,path)))

    let private environmentObservation: (string * CatalogScaffoldProvenance.RootPath) list =
        environmentPaths |> List.map(fun (key,path) -> key,{ Role = "operation"; Path = path })
    let private startupWarning = utf8.GetBytes "An issue was encountered verifying workloads. For more information, run \"dotnet workload update\".\n\n"
    let private startupWarningSingleLine = startupWarning |> Array.take (startupWarning.Length - 1)

    let private invoke (operation: Operation) (budget: Linux.PhaseBudget) (executable: string) arguments
                       (commandRole: string) (canonicalArguments: string list) =
        Linux.checkWork budget
        let child = Linux.startChild operation.Native budget executable arguments (environment operation) (originalRoot operation)
        let held = { Budget = budget; Child = child; Settled = false }
        lock operation.Gate (fun () -> operation.Children <- operation.Children @ [held])
        let mutable current = Linux.observeChild budget child
        while current.ExitCode.IsNone && current.Settlement <> Linux.Unknown do
            Linux.checkWork budget
            Thread.Sleep 1
            current <- Linux.observeChild budget child
        let completed = Linux.settleChild budget child
        if completed.Settlement <> Linux.KnownTerminal || not completed.StdoutEof || not completed.StderrEof || completed.ExitCode.IsNone then
            raise (Linux.CustodyUnknown [diagnostic "catalog.childUnsettled" "The original launch, readers, pidfd or reap remains unknown."])
        held.Settled <- true
        if not completed.Diagnostics.IsEmpty then raise (Linux.Refused completed.Diagnostics)
        let commandFailure code reason =
            // Original bounded stream bytes remain on the caller-held child. Only a
            // bounded escaped excerpt enters the existing diagnostic quota in execute.
            let context = JsonSerializer.Serialize(canonicalArguments)
            let context = if context.Length > 2048 then context.Substring(0,2048) + " [truncated]" else context
            let length = min 2048 completed.Stderr.Length
            let excerpt = JsonSerializer.Serialize(Encoding.UTF8.GetString(completed.Stderr,0,length))
            let suffix = if length < completed.Stderr.Length then " [truncated]" else ""
            refuse code
                ($"{reason} Role={commandRole}; executable={Path.GetFileName executable}; argv={context}; exit={completed.ExitCode.Value}; stderrBytes={completed.Stderr.Length}; stderr={excerpt}{suffix}")
        if completed.ExitCode <> Some 0 then
            commandFailure "catalog.commandFailed" "The selected command returned a known nonzero exit."
        if completed.Stderr.Length <> 0 && completed.Stderr <> startupWarning && completed.Stderr <> startupWarningSingleLine then
            commandFailure "catalog.commandDiagnostics" "Unknown command stderr is refused without repair."
        Linux.checkWork budget
        completed

    let private transport (operation: Operation) (budget: Linux.PhaseBudget) arguments canonicalArguments =
        let timeout = Linux.remainingSeconds budget
        let observed = invoke operation budget operation.Host.TransportExecutable arguments "sdk-transport" canonicalArguments
        let record: CatalogScaffoldProvenance.Invocation =
            { Executable = "dotnet"
              Arguments = canonicalArguments
              WorkingRoot = "operation"
              WorkingDirectory = "."
              EnvironmentRoots = environmentObservation
              TimeoutSeconds = timeout
              ExitCode = observed.ExitCode.Value }
        observed,record

    let private captureInputRoot (operation: Operation) (budget: Linux.PhaseBudget) (absolute: string) maximumBytes =
        let root = Linux.openDirectory operation.Native budget absolute
        let files = Linux.files operation.Native budget root maximumBytes
        let held = { Root = root; Files = files }
        lock operation.Gate (fun () -> operation.InputRoots <- operation.InputRoots @ [held])
        held

    let private revalidateInputs (operation: Operation) (budget: Linux.PhaseBudget) =
        let roots = lock operation.Gate (fun () -> operation.InputRoots)
        for held in roots do
            Linux.revalidateDirectory budget held.Root
            for original in held.Files do
                Linux.checkWork budget
                let current = Linux.readFile operation.Native budget held.Root original.Path original.Bytes.Length
                if current.Sha256 <> original.Sha256 || current.Bytes <> original.Bytes then
                    refuse "catalog.consumedInputChanged" "The captured executable/SDK/runtime/template input changed before consumption or publication."
            Linux.revalidateDirectory budget held.Root

    let private settleChildren (operation: Operation) =
        let children = lock operation.Gate (fun () -> operation.Children)
        for held in children do
            // Stop all original producer authorities, including already terminal children.
            // The one consuming runner is the only caller that can dispatch on this custody.
            Linux.stopUsefulWork held.Budget
            if not held.Settled then
                let observed = Linux.settleChild held.Budget held.Child
                held.Settled <- observed.Settlement = Linux.KnownTerminal && observed.StdoutEof && observed.StderrEof && observed.ExitCode.IsSome
        (children |> List.forall(fun held -> held.Settled)) && Linux.settled operation.Native

    let private completeRetirement (operation: Operation) (budget: Linux.PhaseBudget) =
        if operation.RetirementAttempted then
            raise (Linux.CustodyUnknown [diagnostic "catalog.retirementConsumed" "The original retirement attempt failed or remains unresolved; it is not replayed."])
        operation.RetirementAttempted <- true
        Linux.stopUsefulWork budget
        let childrenSettled = settleChildren operation
        let snapshotsSettled = releaseSnapshotCustodies operation budget
        if not childrenSettled || not snapshotsSettled || not(Linux.settled operation.Native) then
            raise (Linux.CustodyUnknown [diagnostic "catalog.cleanupUnknown" "Original children, snapshots or native task custody remains unresolved."])
        if Workflow.commitOutcome operation.Model = Workflow.Unknown then
            raise (Linux.CustodyUnknown [diagnostic "catalog.commitUnknown" "Unknown publication retains staging and all original custody."])
        match operation.Parent,operation.PrivateRoot,operation.PrivateLeaf with
        | Some parent,Some root,Some leaf ->
            // The sole runner has stopped every producer budget above and completed all
            // children/tasks/snapshot owners. No other API exposes mutable native custody.
            Linux.captureRetirementInventory operation.Native budget root
            Linux.retireDirectory operation.Native budget parent leaf root
        | None,None,None -> ()
        | Some _,None,None -> ()
        | _ -> raise (Linux.CustodyUnknown [diagnostic "catalog.cleanupBindingUnknown" "Private staging ownership is incomplete."])
        Linux.checkCleanup budget
        Linux.releaseKnown budget operation.Native
        operation.NativeReleased <- true
        send operation (Workflow.CleanupObserved(true,true)) |> ignore
        publish operation operation.Model Settled

    let private captureInputFile (operation: Operation) (budget: Linux.PhaseBudget) (absolute: string) maximumBytes =
        Linux.checkWork budget
        let root = Linux.openDirectory operation.Native budget ((Path.GetDirectoryName absolute |> requiredText))
        let captured = Linux.readFile operation.Native budget root ((Path.GetFileName absolute |> requiredText)) maximumBytes
        lock operation.Gate (fun () -> operation.InputRoots <- operation.InputRoots @ [{ Root = root; Files = [captured] }])
        captured

    let private supportedHost (operation: Operation) (budget: Linux.PhaseBudget) (policy: CatalogScaffoldPolicy.Policy)
                              (selected: Fsgg.ProviderCatalog.PreparedConfiguration) =
        Linux.checkWork budget
        if not(OperatingSystem.IsLinux())
           || Runtime.InteropServices.RuntimeInformation.ProcessArchitecture <> Runtime.InteropServices.Architecture.X64
           || Environment.Version <> Version(10,0,12)
           || operation.Request.SelectedPlatform <> "linux-x64" then
            refuse "catalog.hostProfileRefused" "The first transport profile requires the actual Linux-x64 .NET 10.0.12 host and matching explicit platform."
        for capability in selected.Descriptor.Capabilities do
            match capability.Binding with
            | Fsgg.ProviderCatalog.SemanticOnly -> ()
            | Fsgg.ProviderCatalog.Command(_,limits) ->
                let local =
                    limits.EnvironmentIds
                    |> List.exists (fun id -> policy.EnvironmentBindings |> List.exists(fun binding -> binding.Id = id && binding.Environment = FS.GG.Governance.Config.Model.Local))
                if not local then
                    refuse "catalog.environmentRefused" "The selected command has no independently mapped Local environment; this host cannot supply CI or Release guarantees."
        match Linux.observeAbi operation.Native budget with
        | Ok abi when abi.LibcVersion = "2.44" -> ()
        | Ok _ -> refuse "catalog.unsupportedAbi" "The selected first-host libc profile differs from the qualified ABI."
        | Error diagnostics -> raise (Linux.Refused diagnostics)
        Linux.checkWork budget

    let private createPrivateRoot (operation: Operation) (budget: Linux.PhaseBudget) =
        let target = operation.Request.TargetRoot.TrimEnd(Path.DirectorySeparatorChar)
        let parentPath = Path.GetDirectoryName target |> requiredText
        let targetLeaf = Path.GetFileName target |> requiredText
        if String.IsNullOrWhiteSpace targetLeaf || String.IsNullOrWhiteSpace parentPath then
            refuse "catalog.targetRootRefused" "Publication requires a named target beneath an explicit parent directory."
        Linux.checkWork budget
        // This is an early refusal only. Absence here grants no commit authority:
        // the actual publication syscall still uses RENAME_NOREPLACE exactly once.
        let exists = Directory.Exists target || File.Exists target
        Linux.checkWork budget
        if exists then refuse "catalog.targetExists" "The selected target already exists."
        let parent = Linux.openDirectory operation.Native budget parentPath
        operation.Parent <- Some parent
        let leaf = ".fsgg-catalog-" + Guid.NewGuid().ToString("N")
        operation.PrivateLeaf <- Some leaf
        let root = Linux.createDirectory operation.Native budget parent leaf
        operation.PrivateRoot <- Some root
        for _,directory in environmentPaths |> List.distinctBy snd do
            Linux.createDirectory operation.Native budget root directory |> ignore
        Linux.writeFile operation.Native budget root "global.json"
            (utf8.GetBytes "{\"sdk\":{\"version\":\"10.0.401\",\"rollForward\":\"disable\",\"allowPrerelease\":false}}") false
        Linux.writeFile operation.Native budget root "NuGet.Config"
            (utf8.GetBytes "<configuration><packageSources><clear /></packageSources></configuration>") false
        root,targetLeaf

    let private captureTransportInputs (operation: Operation) (budget: Linux.PhaseBudget) =
        let executable = operation.Host.TransportExecutable
        captureInputFile operation budget executable (64 * 1024 * 1024) |> ignore
        // Paths are derived from the explicit selected muxer, never from PATH or an
        // inherited SDK selector. Exact captured bytes remain held and are revalidated.
        let installation = Path.GetDirectoryName executable |> requiredText
        captureInputRoot operation budget (Path.Combine(installation,"sdk","10.0.401")) (512 * 1024 * 1024) |> ignore
        captureInputRoot operation budget (Path.Combine(installation,"shared","Microsoft.NETCore.App","10.0.12")) (128 * 1024 * 1024) |> ignore
        captureInputRoot operation budget (Path.Combine(installation,"host","fxr","10.0.12")) (32 * 1024 * 1024) |> ignore
        let templateRoot = Path.Combine(installation,"templates")
        let templates = captureInputRoot operation budget templateRoot (64 * 1024 * 1024)
        templates.Files
        |> List.filter(fun file -> file.Path.EndsWith(".nupkg",StringComparison.Ordinal))
        |> List.map(fun file -> Path.Combine(templateRoot,file.Path))
        |> Set.ofList

    let private probeTools (operation: Operation) (budget: Linux.PhaseBudget) (policy: CatalogScaffoldPolicy.Policy)
                           (selected: Fsgg.ProviderCatalog.PreparedConfiguration) =
        selected.Descriptor.Tools
        |> List.filter(fun tool -> List.contains operation.Request.SelectedPlatform tool.Platforms)
        |> List.map(fun tool ->
            let probe =
                policy.ToolProbes |> List.filter(fun probe -> probe.Id = tool.Id)
                |> function
                    | [probe] -> probe
                    | _ -> refuse "catalog.toolProbeRefused" "Exactly one independently selected tool probe is required for each selected tool."
            if not(Path.IsPathFullyQualified probe.Executable) then
                refuse "catalog.toolSelectionRefused" "The tool probe must select an absolute executable; PATH discovery is not admitted."
            captureInputFile operation budget probe.Executable (64 * 1024 * 1024) |> ignore
            revalidateInputs operation budget
            let observed = invoke operation budget probe.Executable (probe.Arguments |> List.map Linux.Literal) ("tool-probe:" + tool.Id) probe.Arguments
            let text = utf8.GetString(observed.Stdout)
            if text.Length > probe.MaximumOutputCharacters
               || text <> probe.VersionPrefix + tool.Version + probe.VersionSuffix then
                refuse "catalog.toolVersionRefused" "The actual selected tool probe did not report the exact declared version."
            let observedTool: CatalogScaffoldProvenance.ObservedTool =
                { Id = tool.Id
                  Version = tool.Version
                  Executable = tool.Id }
            observedTool)

    let private providerPaths (files: Linux.CapturedFile list) : ScaffoldProvenance.ScaffoldProducedPath list =
        files |> List.map(fun file -> { Path = file.Path; Owner = ArtifactRef.GeneratedProduct; Sha256 = Some file.Sha256 })

    let private validatePayloadCorrespondence (original: Linux.CapturedFile list) (final: Linux.CapturedFile list) =
        let allowed = Set.ofList [".gitignore";manifestSource;ScaffoldMutation.toolManifestPath]
        let actual = final |> List.map(fun file -> file.Path,file) |> Map.ofList
        for file in original do
            match Map.tryFind file.Path actual with
            | Some current when current.Sha256 = file.Sha256 || Set.contains file.Path allowed -> ()
            | _ -> refuse "catalog.payloadChanged" "An immutable original provider-produced path changed or disappeared."

    let private applyComposition (operation: Operation) (budget: Linux.PhaseBudget) (plan: CompositionPlan) =
        let root = workspaceRoot operation
        let original = operation.OriginalTemplate |> List.map(fun file -> file.Path,file) |> Map.ofList
        let writes = Collections.Generic.Dictionary<string,byte array * ArtifactRef.ArtifactOwner>(StringComparer.Ordinal)
        let add path bytes owner =
            match writes.TryGetValue path with
            | true,(previous,priorOwner) when previous <> bytes || priorOwner <> owner ->
                refuse "catalog.compositionCollision" "Two materialization plans claim different bytes or ownership for one path."
            | true,_ -> ()
            | _ -> writes.Add(path,(bytes,owner))
        for path,bytes,_,owner in plan.Writes do add path bytes owner
        for source,target in plan.ProviderMirrors do
            let file = Map.tryFind source original |> Option.defaultWith(fun () -> refuse "catalog.mirrorSource" "The selected provider mirror source was not captured.")
            add target file.Bytes ArtifactRef.Mirrored
        for path,bytes in plan.ExpectedManifest do
            // The exact shared union deliberately supersedes the original mirror input.
            writes[path] <- bytes,(if path = manifestSource then ArtifactRef.GeneratedProduct else ArtifactRef.Mirrored)
        // Metadata materialization is limited to files this same shared plan owns
        // and writes. It grants no permission change to original provider payload.
        for path,owner in plan.Executables do
            match writes.TryGetValue path with
            | true,(_,writeOwner) when writeOwner = owner && not(Map.containsKey path original) -> ()
            | _ -> refuse "catalog.executableMaterializationRefused" "A shared executable declaration must name a newly materialized file of the same owner."
        // Directory declarations are created before file materialization. Every mkdir
        // is relative to an already held parent; original provider directories with
        // captured descendants already satisfy that declaration and are not adopted.
        let heldDirectories = Collections.Generic.Dictionary<string,Linux.DirectoryCustody>(StringComparer.Ordinal)
        heldDirectories.Add("",root)
        for directory in plan.Directories |> List.sort do
            let relative = directory.Replace('\\','/').Trim('/')
            let alreadyPresent = original |> Map.exists(fun path _ -> path.StartsWith(relative + "/",StringComparison.Ordinal))
            if not alreadyPresent then
                let mutable parent = root
                let mutable prefix = ""
                for part in relative.Split('/') do
                    if String.IsNullOrWhiteSpace part then refuse "catalog.compositionPath" "Empty materialization path segments are refused."
                    prefix <- if prefix = "" then part else prefix + "/" + part
                    match heldDirectories.TryGetValue prefix with
                    | true,value -> parent <- value
                    | _ ->
                        parent <- Linux.createDirectory operation.Native budget parent part
                        heldDirectories.Add(prefix,parent)
        for KeyValue(path,(bytes,_)) in writes do
            Linux.checkWork budget
            let existed = Map.containsKey path original
            if existed && not(Set.contains path (Set.ofList [".gitignore";manifestSource;ScaffoldMutation.toolManifestPath])) then
                refuse "catalog.compositionCollision" "A shared SDD/skill write conflicts with an original provider payload path."
            Linux.writeFile operation.Native budget root path bytes existed
            let readback = Linux.readFile operation.Native budget root path bytes.Length
            if readback.Bytes <> bytes then refuse "catalog.compositionReadback" "The exact shared materialization bytes differ after write."
        for path,_ in plan.Executables do
            Linux.checkWork budget
            Linux.setExecutable operation.Native budget root path
        operation.ExpectedManifest <- plan.ExpectedManifest
        operation.ExpectedToolManifest <- Some plan.ExpectedToolManifest
        let final = Linux.files operation.Native budget root (8 * 1024 * 1024)
        validatePayloadCorrespondence operation.OriginalTemplate final
        verifyManifestUnion plan.ExpectedManifest final
        verifyToolManifest plan.ExpectedToolManifest final
        final,(writes |> Seq.map(fun (KeyValue(path,value)) -> path,value) |> Map.ofSeq)

    let private provenanceRecord (operation: Operation) (preview: Workflow.CatalogPreview)
                                 (policy: CatalogScaffoldPolicy.Policy) (selected: Fsgg.ProviderCatalog.PreparedConfiguration)
                                 (archive: CatalogScaffoldProvenance.ArchiveIdentity)
                                 (observation: CatalogScaffoldProvenance.Observation)
                                 (writes: Map<string,byte array * ArtifactRef.ArtifactOwner>) =
        let admission = ProviderCapabilityAdmission.request policy operation.Request.SelectedPlatform selected.Descriptor |> requireResult
        let originalPaths = operation.OriginalTemplate |> List.map _.Path |> Set.ofList
        let classified =
            observation.ProducedPaths |> List.map(fun path ->
                let owner =
                    if Set.contains path.Path originalPaths then ArtifactRef.GeneratedProduct
                    else match Map.tryFind path.Path writes with
                         | Some(_,owner) -> owner
                         | None -> refuse "catalog.unownedProducedPath" "The final workspace contains a path not produced by the template or actual shared materialization."
                { path with Owner = owner })
        let owned owner = classified |> List.filter(fun path -> path.Owner = owner)
        let generator = SchemaVersion.currentGeneratorVersion()
        let ownership: ScaffoldProvenance.ScaffoldProvenanceRecord =
            { SchemaVersion = 1; Generator = generator; RequiredMinimumCliVersion = None
              ProviderName = selected.Descriptor.Id; ProviderContractVersion = selected.Descriptor.ContractVersion
              TemplateRef = selected.Descriptor.TemplateSource; Outcome = "providerSucceeded"
              ProducedPaths = owned ArtifactRef.GeneratedProduct; MirroredPaths = owned ArtifactRef.Mirrored
              SddOwnedPaths = owned ArtifactRef.Sdd; DriverPaths = owned ArtifactRef.Driver
              GameSkillPaths = owned ArtifactRef.GameSkill; RenderingSkillPaths = owned ArtifactRef.RenderingSkill
              EffectiveParameters = selected.EffectiveParameters }
        let record: CatalogScaffoldProvenance.CatalogScaffoldProvenanceRecord =
            { SchemaVersion = 2; Generator = generator; Ownership = ownership
              Observation = { observation with ProducedPaths = classified }
              Declaration =
                { RawCatalogDigest = operation.Request.Selection.ExpectedRawDigest
                  CatalogId = preview.Catalog.Id; CatalogRevision = preview.Catalog.Revision; CatalogDigest = preview.Catalog.Digest
                  Descriptor = selected.Descriptor; EffectiveParameters = selected.EffectiveParameters
                  RawProductName = selected.RawProductName; PackageIdentity = selected.PackageIdentity; CodeIdentifier = selected.CodeIdentifier
                  Archive = archive
                  Policy = { Id = policy.Identity.Id; Version = policy.Identity.Version; Digest = policy.Identity.Digest }
                  EvidenceMap = { Id = policy.EvidenceMapIdentity.Id; Version = policy.EvidenceMapIdentity.Version; Digest = policy.EvidenceMapIdentity.Digest }
                  Budgets =
                    { RequestedPreflightSeconds = operation.Request.PreflightTimeoutSeconds
                      RequestedScaffoldSeconds = operation.Request.ScaffoldTimeoutSeconds
                      AdmittedPreflightSeconds = operation.Request.PreflightTimeoutSeconds
                      AdmittedScaffoldSeconds = operation.Request.ScaffoldTimeoutSeconds }
                  Admission =
                    { ContractVersion = admission.ContractVersion; Platform = operation.Request.SelectedPlatform
                      RequiredCapabilityIds = admission.RequiredCapabilityIds; SemanticOnlyCapabilityIds = admission.SemanticOnlyObligationIds
                      SupportedPlatforms = policy.KnownPlatforms; SupportedEnvironmentIds = policy.EnvironmentBindings |> List.map _.Id
                      SupportedFormats = policy.SupportedEvidenceFormats |> List.map(fun format -> { Id = format.Id; Version = format.Version })
                      EvidenceMappings = policy.EvidenceMappings |> List.map(fun mapping -> { Format = mapping.Format; Id = mapping.Id; Version = mapping.Version })
                      Descriptor = selected.Descriptor } } }
        record

    let private execute (operation: Operation) : Workflow.Model =
        // All business is serialized in this single cold runner. Native workers own
        // their individual tasks; no public API exposes same-custody producers.
        let initial = Linux.beginPhase operation.Request.PreflightTimeoutSeconds operation.Cancellation
        operation.PhaseBudget <- Some initial
        try
            let preview,policy,selected,association = declaredInputs operation.Request
            supportedHost operation initial policy selected
            let archiveFile = captureInputFile operation initial operation.Request.TemplateArchive (64 * 1024 * 1024)
            let template = archiveTemplate selected association operation.Request.ExpectedArchiveDigest archiveFile.Bytes
            let archive: CatalogScaffoldProvenance.ArchiveIdentity =
                { Id = association.PackageId; Version = association.PackageVersion; Digest = archiveFile.Sha256 }
            Linux.checkWork initial
            if operation.Request.DryRun then
                // The exact archive/ABI were actually read, but no child, staging root
                // or produced-path observation exists on this route.
                Linux.stopUsefulWork initial
                if not(Linux.settled operation.Native) then
                    raise (Linux.CustodyUnknown [diagnostic "catalog.dryRunUnsettled" "Input/ABI capture still owns unfinished custody."])
                operation.RetirementAttempted <- true
                Linux.releaseKnown initial operation.Native
                operation.NativeReleased <- true
                // Publish Prepared only after original-budget capture retirement succeeds.
                // A retirement refusal must still reach the workflow's failure transition.
                send operation (Workflow.InputsVerified(preview,policy,archive)) |> ignore
                if Workflow.outcome operation.Model = Workflow.Failed then
                    raise (Linux.Refused(Workflow.diagnostics operation.Model))
                publish operation operation.Model Settled
            else
                send operation (Workflow.InputsVerified(preview,policy,archive)) |> ignore
                if Workflow.outcome operation.Model = Workflow.Failed then
                    raise (Linux.Refused(Workflow.diagnostics operation.Model))
                let root,targetLeaf = createPrivateRoot operation initial
                Linux.writeFile operation.Native initial root "input.nupkg" archiveFile.Bytes false
                let capturedBuiltinMounts = captureTransportInputs operation initial
                revalidateInputs operation initial
                ProviderCapabilityAdmission.resolve policy operation.Request.SelectedPlatform selected.Descriptor |> requireResult |> ignore
                let version,versionInvocation = transport operation initial [Linux.Literal "--version"] ["--version"]
                if utf8.GetString(version.Stdout).TrimEnd('\r','\n') <> "10.0.401" then
                    refuse "catalog.sdkVersionRefused" "The actual explicit transport did not select SDK 10.0.401."
                let tools = probeTools operation initial policy selected
                let hive = [Linux.Literal "--debug:custom-hive";Linux.DirectoryPath(root,"engine")]
                revalidateInputs operation initial
                let _,configInvocation =
                    transport operation initial ([Linux.Literal "new"] @ hive @ [Linux.Literal "--debug:show-config"])
                        ["new";"--debug:custom-hive";"engine";"--debug:show-config"]
                let configSnapshot = captureSdkSnapshot operation initial "preflight-config"
                let builtinMounts = verifyTransportCache template capturedBuiltinMounts false configSnapshot
                let preflight: CatalogScaffoldProvenance.Observation =
                    { Platform = operation.Request.SelectedPlatform; Tools = tools; ConsumedArchiveDigest = archive.Digest
                      Transport = { Executable = "dotnet"; Version = "10.0.401" }
                      Invocations = [versionInvocation;configInvocation]; Result = "succeeded"; ProducedPaths = [] }
                Linux.checkWork initial
                send operation (Workflow.PreflightObserved preflight) |> ignore
                if Workflow.phase operation.Model <> Workflow.Staging then
                    raise (Linux.Refused(Workflow.diagnostics operation.Model))
                // Stop all preflight producer authority before creating the one original
                // scaffold phase. No retry or renewed end exists within either phase.
                Linux.stopUsefulWork initial
                for held in operation.Children do Linux.stopUsefulWork held.Budget
                if not(Linux.settled operation.Native) || not(snapshotsReleased operation) then
                    raise (Linux.CustodyUnknown [diagnostic "catalog.preflightCustody" "The original preflight owners are not settled."])
                let scaffold = Linux.beginPhase operation.Request.ScaffoldTimeoutSeconds operation.Cancellation
                operation.PhaseBudget <- Some scaffold
                revalidateInputs operation scaffold
                ProviderCapabilityAdmission.resolve policy operation.Request.SelectedPlatform selected.Descriptor |> requireResult |> ignore
                let workspace = Linux.createDirectory operation.Native scaffold root "workspace"
                operation.Workspace <- Some workspace
                let _,installInvocation =
                    transport operation scaffold ([Linux.Literal "new";Linux.Literal "install";Linux.DirectoryPath(root,"input.nupkg")] @ hive)
                        ["new";"install";"input.nupkg";"--debug:custom-hive";"engine"]
                let installed = captureSdkSnapshot operation scaffold "scaffold-install"
                verifyTransportCache template builtinMounts true installed |> ignore
                revalidateInputs operation scaffold
                let help,helpInvocation =
                    transport operation scaffold ([Linux.Literal "new";Linux.Literal template.ShortName] @ hive @ [Linux.Literal "--help"])
                        ["new";template.ShortName;"--debug:custom-hive";"engine";"--help"]
                verifyAliases selected help.Stdout
                let createPrefix = ["new";template.ShortName;"--output"]
                let overrideArguments = selected.EffectiveParameters |> List.collect(fun (key,value) -> ["--" + key;value])
                let arguments =
                    (createPrefix |> List.map Linux.Literal)
                    @ [Linux.DirectoryPath(root,"workspace");Linux.Literal "--no-update-check"] @ hive
                    @ (overrideArguments |> List.map Linux.Literal)
                revalidateInputs operation scaffold
                let _,createInvocation =
                    transport operation scaffold arguments
                        (createPrefix @ ["workspace";"--no-update-check";"--debug:custom-hive";"engine"] @ overrideArguments)
                let createdSnapshot = captureSdkSnapshot operation scaffold "scaffold-create"
                verifyTransportCache template builtinMounts true createdSnapshot |> ignore
                let original = Linux.files operation.Native scaffold workspace (8 * 1024 * 1024)
                verifyTemplatePayload template original
                if original |> List.exists(fun file -> file.Path = ScaffoldProvenance.provenancePath) then
                    refuse "catalog.provenanceCollision" "The provider payload may not pre-author the SDD provenance artifact."
                operation.OriginalTemplate <- original |> List.map(fun file -> {file with Bytes = Array.copy file.Bytes})
                let templateObservation =
                    { preflight with Invocations = preflight.Invocations @ [installInvocation;helpInvocation;createInvocation]
                                     ProducedPaths = providerPaths original }
                Linux.checkWork scaffold
                send operation (Workflow.TemplateObserved templateObservation) |> ignore
                if Workflow.phase operation.Model <> Workflow.Composing then
                    raise (Linux.Refused(Workflow.diagnostics operation.Model))
                let generator = SchemaVersion.currentGeneratorVersion()
                let plan = compositionPlan operation.Request.TargetRoot generator selected original
                let final,writes = applyComposition operation scaffold plan
                let record =
                    provenanceRecord operation preview policy selected archive
                        {templateObservation with ProducedPaths = providerPaths final} writes
                let serialized = CatalogScaffoldProvenance.serialize record |> requireResult |> utf8.GetBytes
                Linux.writeFile operation.Native scaffold workspace ScaffoldProvenance.provenancePath serialized false
                let readback = Linux.readFile operation.Native scaffold workspace ScaffoldProvenance.provenancePath serialized.Length
                if readback.Bytes <> serialized then refuse "catalog.provenanceReadback" "The exact strict provenance bytes differ after materialization."
                // Recheck the physical payload and the three narrow shared byte joins.
                let beforeCommit = Linux.files operation.Native scaffold workspace (8 * 1024 * 1024)
                let withoutProvenance = beforeCommit |> List.filter(fun file -> file.Path <> ScaffoldProvenance.provenancePath)
                if withoutProvenance <> final then refuse "catalog.workspaceChanged" "Workspace bytes or paths changed before publication."
                verifyManifestUnion plan.ExpectedManifest withoutProvenance
                verifyToolManifest plan.ExpectedToolManifest withoutProvenance
                revalidateInputs operation scaffold
                Linux.checkWork scaffold
                send operation (Workflow.WorkspaceComposed record) |> ignore
                if Workflow.phase operation.Model <> Workflow.Committing then
                    raise (Linux.Refused(Workflow.diagnostics operation.Model))
                let parent = operation.Parent |> Option.defaultWith(fun () -> refuse "catalog.parentMissing" "The held target parent is unavailable.")
                let commit = Linux.commitNoReplace scaffold root workspace "workspace" parent targetLeaf
                send operation (Workflow.CommitObserved commit) |> ignore
                if commit = Workflow.Refused then
                    let diagnostics = Linux.chargeDiagnostics scaffold [diagnostic "catalog.commitRefused" "The original no-replace publication syscall refused the target; it is not retried."]
                    send operation (Workflow.RefusalObserved diagnostics) |> ignore
                completeRetirement operation scaffold
        with error ->
            // Retain the actual original exception under the same operation. Only its
            // bounded diagnostic projection enters the model; cleanup cannot replace it.
            if operation.FirstFailure.IsNone then operation.FirstFailure <- Some error
            // Primary projection is charged before any secondary cleanup activity.
            // Native IO may already have charged its diagnostics; a second reservation
            // can truthfully refuse detail but never promotes cleanup as the first cause.
            let original =
                match error with
                | Linux.Refused diagnostics | Linux.CustodyUnknown diagnostics -> diagnostics
                | _ ->
                    let message = error.Message
                    let excerpt = if message.Length > 2048 then message.Substring(0,2048) + " [truncated]" else message
                    let stack = error.StackTrace |> Option.ofObj |> Option.defaultValue ""
                    let frames =
                        stack.Split([|'\r';'\n'|],StringSplitOptions.RemoveEmptyEntries)
                        |> Array.truncate 3
                        |> String.concat " | "
                    let frames = if frames.Length > 2048 then frames.Substring(0,2048) + " [truncated]" else frames
                    [diagnostic "catalog.edgeFailure"
                        ($"The original catalog edge failed. Phase={Workflow.phase operation.Model}; exception={error.GetType().Name}; message={excerpt}; callsite={frames}")]
            let budget = operation.PhaseBudget |> Option.defaultValue initial
            let primary = Linux.chargeDiagnostics budget original
            send operation (Workflow.RefusalObserved primary) |> ignore
            try
                completeRetirement operation budget
            with _ ->
                // Cleanup is not business and has no replacement end. The first refusal
                // remains the model's cause; unresolved physical ownership stays retained.
                send operation (Workflow.CleanupObserved(false,false)) |> ignore
                publish operation operation.Model Unresolved
        operation.Model

    let prepare (host: HostSelection) (request: Workflow.CatalogScaffoldRequest) (cancellationToken: CancellationToken) =
        // Lexical input checks and private copies only: never sense the target, tool or archive.
        let invalidPath (value: string) = String.IsNullOrWhiteSpace value || value.Contains(char 0) || not(Path.IsPathFullyQualified value)
        if invalidPath host.TransportExecutable then
            Error [diagnostic "catalog.transportSelection" "An independently selected fully qualified transport executable is required."]
        elif invalidPath request.TargetRoot || invalidPath request.TemplateArchive then
            Error [diagnostic "catalog.requestPath" "The selected target and local archive must have explicit fully qualified paths."]
        elif obj.ReferenceEquals(request.Selection.CatalogBytes,null) || obj.ReferenceEquals(request.PolicyBytes,null) then
            Error [diagnostic "catalog.requestBytes" "Explicit catalog and policy bytes are required."]
        else
            let retained =
                { request with
                    Selection = {request.Selection with CatalogBytes = Array.copy request.Selection.CatalogBytes}
                    PolicyBytes = Array.copy request.PolicyBytes }
            let model, _ = Workflow.init retained
            if Workflow.outcome model = Workflow.Failed then Error(Workflow.diagnostics model)
            else
                Ok
                    { Gate = obj(); Host = host; Request = retained; Cancellation = cancellationToken
                      Native = Linux.createCustody(); Runner = None; Changed = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously); Model = model; PhaseBudget = None
                      Parent = None; PrivateRoot = None; Workspace = None; PrivateLeaf = None
                      OriginalTemplate = []; ExpectedManifest = []; ExpectedToolManifest = None; Children = []; InputRoots = []; Snapshots = ResizeArray()
                      SnapshotBytes = 0; Released = false; NativeReleased = false; RetirementAttempted = false; FirstFailure = None; Ownership = NotStarted }

    let run (operation: Operation) = async {
        let task =
            lock operation.Gate (fun () ->
                if operation.Released || operation.Runner.IsSome || operation.Ownership <> NotStarted then
                    invalidOp "The original catalog operation is one-use and cannot resume business."
                let task = new Task<Workflow.Model>(fun () -> execute operation)
                // The actual runner task and operation are held before Start.
                operation.Runner <- Some task
                operation.Ownership <- Active
                task)
        let started =
            try
                task.Start(TaskScheduler.Default)
                true
            with _ -> false
        if not started then
            // Start refused before execute dispatched any effect; no native custody
            // can be inferred from scheduling alone.
            lock operation.Gate (fun () -> operation.Runner <- None)
            send operation (Workflow.RefusalObserved [diagnostic "catalog.runnerStart" "The preheld cold runner could not start."]) |> ignore
            send operation (Workflow.CleanupObserved(true,true)) |> ignore
            operation.NativeReleased <- true
            publish operation operation.Model Settled
            return operation.Model
        else
            return! task |> Async.AwaitTask
    }

    let observe (operation: Operation) : OperationObservation =
        lock operation.Gate (fun () ->
            { Ownership = operation.Ownership
              Model = Some operation.Model
              Diagnostics = Workflow.diagnostics operation.Model })

    // Passive observation never settles a child, retires a descriptor or renews a
    // phase. The actual caller continues to own Operation while this wait is active.
    let rec waitForSettled (operation: Operation) = async {
        let current, notification =
            lock operation.Gate (fun () ->
                { Ownership = operation.Ownership
                  Model = Some operation.Model
                  Diagnostics = Workflow.diagnostics operation.Model }, operation.Changed.Task)
        if current.Ownership = NotStarted || current.Ownership = Settled then
            return current
        else
            do! notification |> Async.AwaitTask
            return! waitForSettled operation
    }

    let release (operation: Operation) =
        lock operation.Gate (fun () ->
            if operation.Released then Ok ()
            elif operation.Ownership = NotStarted then
                // Pure preparation has acquired no physical/native resource. Discarding
                // this unused owner grants neither a run nor a cleanup budget.
                operation.Released <- true
                operation.Ownership <- Settled
                Ok ()
            elif operation.Ownership = Settled
                 && operation.NativeReleased
                 && Linux.settled operation.Native
                 && snapshotsReleased operation
                 && (operation.Runner |> Option.forall (fun task -> task.IsCompleted)) then
                // The original runner must already have retired physical custody inside
                // its original end. Public release performs no late close/free/disposal.
                operation.Released <- true
                Ok ()
            else
                Error [diagnostic "catalog.releaseUnsettled" "The original operation still owns active, unknown or unretired resources."])
