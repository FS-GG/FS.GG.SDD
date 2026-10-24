namespace FS.GG.SDD.Commands

open System
open System.IO
open System.IO.Compression
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open System.Diagnostics
open System.Collections.Generic
open FS.GG.SDD.Artifacts

/// Data-only source acquisition. Scaffold execution remains owned by CatalogScaffoldEffects.
module ProviderSourceEffects =
    type Request =
        {
            SourceSetBytes: byte array
            ExpectedSourceSetSha256: string
            PolicyBytes: byte array
            ExpectedPolicySha256: string
            CacheRoot: string
            /// Inspection has no target; scaffold supplies its target before source-cache effects.
            TargetRoot: string option
            Offline: bool
        }


    type InputFile = { AbsolutePath: string; Bytes: int64; Sha256: string }

    /// Whole-catalog inspection has no selected provider/archive and grants no execution authority.
    type SourcePreview =
        {
            Source: FS.GG.SDD.Artifacts.ProviderSource.Reference
            Revision: string
            SourceSetSha256: string
            AcquisitionPolicySha256: string
            Acquisition: FS.GG.SDD.Artifacts.ProviderSource.Acquisition
            ResolvedUri: string option
            Redirects: string list
            NetworkHops: FS.GG.SDD.Artifacts.ProviderSource.NetworkHop list
            Catalog: CatalogScaffoldWorkflow.CatalogPreview
        }

    type Observation =
        {
            Phase: ProviderSourceWorkflow.Phase
            Outcome: ProviderSourceWorkflow.Outcome
            Diagnostics: Fsgg.ProviderCatalog.Diagnostic list
            Settled: bool
        }

    module private Linux = CatalogScaffoldLinux
    module private Workflow = ProviderSourceWorkflow
    module private Source = ProviderSource

    type private Entry =
        { Preview: SourcePreview
          Manifest: Source.Manifest
          CatalogBytes: byte array
          PayloadRoot: string
          Files: InputFile list }

    type PreparedSet = private { Entries: Entry list; TargetRoot: string option; CacheRoot: string }
    type SelectedSource =
        private
            { Entry: Entry
              Selection: CatalogScaffoldWorkflow.CatalogSelection
              Archive: InputFile
              Observation: Source.Observation
              TargetRoot: string option
              CacheRoot: string }

    type private TaskSlot = { mutable Started: bool; mutable Task: Task option }
    type private Stage =
        { Name: string
          mutable Root: Linux.DirectoryCustody option
          mutable Published: bool
          mutable PublicationUnknown: bool }

    type Operation =
        private
            { Gate: obj
              Request: Request
              Sources: Source.SourceSet
              Policy: Source.Policy
              Cancellation: CancellationToken
              Native: Linux.Custody
              Tasks: ResizeArray<TaskSlot>
              Disposables: ResizeArray<IDisposable>
              Stages: ResizeArray<Stage>
              mutable Model: Workflow.Model
              mutable Runner: Task<Result<PreparedSet, Fsgg.ProviderCatalog.Diagnostic list>> option
              mutable PhaseBudget: Linux.PhaseBudget option
              mutable CancellationSource: CancellationTokenSource option
              mutable Cache: Linux.DirectoryCustody option
              mutable LocalRoots: (Source.Reference * Linux.DirectoryCustody) list
              mutable CapturedRoots: Linux.DirectoryCustody list
              mutable TargetParent: (Linux.DirectoryCustody * string) option
              mutable End: int64
              mutable WorkEnd: int64
              mutable ExpandedBytes: int64
              mutable TransferBytes: int64
              mutable PrivateBytes: int64
              mutable Settled: bool
              mutable Released: bool
              mutable CleanupUnknown: bool }

    exception private Refusal of Fsgg.ProviderCatalog.Diagnostic list
    let private diagnostic code path message : Fsgg.ProviderCatalog.Diagnostic = { Code = code; Path = path; Message = message }
    let private refuse code message = raise (Refusal [ diagnostic code "$" message ])
    let private unwrap = function Ok value -> value | Error errors -> raise (Refusal errors)
    let private ticks () = Stopwatch.GetTimestamp()
    let private seconds value = int64 (float value * float Stopwatch.Frequency)
    let private budget operation = operation.PhaseBudget |> Option.defaultWith (fun () -> refuse "source.notStarted" "No original acquisition phase exists.")
    let private checkWork operation =
        if operation.Cancellation.IsCancellationRequested || ticks () >= operation.WorkEnd then
            Linux.stopUsefulWork (budget operation)
            refuse "source.deadline" "Cancellation or the original source useful-work deadline stopped acquisition."
        Linux.checkWork (budget operation)
    let private remainingMilliseconds deadline =
        let remaining = float (deadline - ticks ()) * 1000.0 / float Stopwatch.Frequency
        max 0 (int (min remaining (float Int32.MaxValue)))
    let private track<'task when 'task :> Task> (operation: Operation) (start: unit -> 'task) : 'task =
        let slot = { Started = false; Task = None }
        lock operation.Gate (fun () -> operation.Tasks.Add slot; slot.Started <- true)
        let task = start ()
        lock operation.Gate (fun () -> slot.Task <- Some(task :> Task))
        task
    let private awaitWork operation maximumSeconds (task: Task<'a>) =
        checkWork operation
        let deadline = min operation.WorkEnd (ticks () + seconds maximumSeconds)
        let winner = Task.WhenAny(task :> Task, Task.Delay(remainingMilliseconds deadline)).GetAwaiter().GetResult()
        if not (obj.ReferenceEquals(winner, task)) then
            operation.CancellationSource |> Option.iter _.Cancel
            refuse "source.deadline" "An original bounded source task did not complete; its custody is retained."
        let result = task.GetAwaiter().GetResult()
        checkWork operation
        result
    let private own operation (value: #IDisposable) =
        lock operation.Gate (fun () -> operation.Disposables.Add(value :> IDisposable))
        value
    let private send operation message =
        lock operation.Gate (fun () ->
            let model, effects = Workflow.update message operation.Model
            operation.Model <- model
            effects)
    let private sameObject (a: Linux.ObjectIdentity) (b: Linux.ObjectIdentity) =
        // A bind mount may give the same underlying object a different mount ID.
        a.DeviceMajor = b.DeviceMajor && a.DeviceMinor = b.DeviceMinor && a.Inode = b.Inode && a.Kind = b.Kind
    let private overlaps a b =
        let left, right = Linux.identity a, Linux.identity b
        (Linux.directoryAncestry a |> List.exists (sameObject right))
        || (Linux.directoryAncestry b |> List.exists (sameObject left))
    let private guardRoots operation =
        checkWork operation
        let phase = budget operation
        let cache = operation.Cache |> Option.get
        Linux.revalidateDirectory phase cache
        for _, local in operation.LocalRoots do
            Linux.revalidateDirectory phase local
            if overlaps cache local then refuse "source.cacheOverlap" "Local source and cache/staging cannot contain each other, including physical aliases."
        match operation.TargetParent with
        | None -> ()
        | Some(parent, leaf) ->
            Linux.revalidateDirectory phase parent
            let target = Linux.tryOpenChildDirectory operation.Native phase parent leaf
            let roots = cache :: (operation.LocalRoots |> List.map snd) @ operation.CapturedRoots
            for root in roots do
                Linux.revalidateDirectory phase root
                if Linux.directoryAncestry parent |> List.exists (sameObject (Linux.identity root)) then
                    refuse "source.targetOverlap" "The selected target lies inside a source/cache physical object."
                match target with
                | Some target when overlaps root target -> refuse "source.targetOverlap" "Target and source/cache are physical ancestors or aliases."
                | _ -> ()
    let private guardCapturedTarget operation rootPath (files: Linux.CapturedFile list) =
        if operation.TargetParent.IsSome then
            let parents = files |> List.map (fun file -> Path.GetDirectoryName(Path.Combine(rootPath, file.Path)) |> nonNull) |> List.distinct
            for path in parents do
                let root = Linux.openDirectory operation.Native (budget operation) path
                operation.CapturedRoots <- root :: operation.CapturedRoots
            guardRoots operation
    let private chargeExpanded operation bytes =
        operation.ExpandedBytes <- Checked.(+) operation.ExpandedBytes bytes
        if operation.ExpandedBytes > operation.Policy.MaximumExpandedBytes then
            refuse "source.expandedBound" "The complete selected source population exceeds its aggregate expanded-byte bound."
    let private captureFiles operation root maximumBytes maximumFiles maximumDepth =
        checkWork operation
        if maximumBytes < 0L || maximumBytes > int64 Int32.MaxValue then refuse "source.expandedBound" "Invalid remaining source byte budget."
        let files = Linux.filesWithinLimits operation.Native (budget operation) root (int maximumBytes) maximumFiles maximumDepth
        checkWork operation
        files
    let private member name (files: Linux.CapturedFile list) =
        match files |> List.filter (fun file -> file.Path = name) with
        | [ value ] -> value
        | _ -> refuse "source.population" ("Expected exactly one member: " + name)
    let private validatePayload reference (files: Linux.CapturedFile list) =
        let manifestFile = member "source.json" files
        let manifest = Source.parseManifest reference.ManifestSha256 manifestFile.Bytes |> unwrap
        if manifest.Id <> reference.Id then refuse "source.manifestIdentity" "Manifest ID differs from the selected source."
        match reference.Locator with
        | Source.HttpsEnvelope(_, revision, _) when revision <> manifest.Revision -> refuse "source.revision" "Pinned HTTPS revision differs from the manifest."
        | _ -> ()
        Source.verifyMembers manifest (files |> List.filter (fun file -> file.Path <> "source.json") |> List.map (fun file -> file.Path, file.Bytes)) |> unwrap
        let catalogFile = member manifest.CatalogPath files
        let catalog = CatalogScaffoldWorkflow.prepare { CatalogBytes = catalogFile.Bytes; ExpectedRawDigest = catalogFile.Sha256; Provider = None; Overrides = [] } |> unwrap
        let declared = catalog.Catalog.Providers |> List.map _.TemplateSource |> Set.ofList
        if declared <> (manifest.Archives |> List.map _.TemplateSource |> Set.ofList) then
            refuse "source.archiveAssociation" "Archive associations must exactly cover the existing catalog template sources."
        manifest, catalog, catalogFile.Bytes

    let private observation (preview: SourcePreview) catalogSha256 templateSource archiveSha256 : Source.Observation =
        { Schema = "fsgg.provider-source-observation/1"; SourceSetSha256 = preview.SourceSetSha256
          AcquisitionPolicySha256 = preview.AcquisitionPolicySha256; Source = preview.Source; Revision = preview.Revision
          Acquisition = preview.Acquisition; ResolvedUri = preview.ResolvedUri; Redirects = preview.Redirects; NetworkHops = preview.NetworkHops
          CatalogSha256 = catalogSha256
          TemplateSource = templateSource; ArchiveSha256 = archiveSha256 }

    let private archiveDigest (manifest: Source.Manifest) (archive: Source.Archive) =
        manifest.Members |> List.find (fun memberValue -> memberValue.Path = archive.Path) |> _.Sha256
    let private metadataBytes preview manifest catalogSha256 =
        // The selected-archive fields are used only to reuse strict observation validation.
        // The stored whole-source receipt deliberately has no provider/archive selection.
        for archive in manifest.Archives do
            Source.validateObservation (observation preview catalogSha256 archive.TemplateSource (archiveDigest manifest archive))
            |> fun errors -> if not errors.IsEmpty then raise (Refusal errors)
        let archive = manifest.Archives.Head
        let text = Source.serializeObservation (observation preview catalogSha256 archive.TemplateSource (archiveDigest manifest archive)) |> unwrap
        let node = JsonNode.Parse(text) |> nonNull |> _.AsObject()
        node.Remove "templateSource" |> ignore
        node.Remove "archiveSha256" |> ignore
        node["schema"] <- JsonValue.Create "fsgg.provider-source-cache/1"
        let bytes = Encoding.UTF8.GetBytes(node.ToJsonString())
        if bytes.Length > Source.limits.MaximumReceiptBytes then refuse "source.cacheMetadataBound" "Cache metadata exceeds its byte ceiling."
        bytes

    let private readMetadata (reference: Source.Reference) (manifest: Source.Manifest) catalog rawCatalogSha256 (bytes: byte array) =
        if bytes.Length > Source.limits.MaximumReceiptBytes || (bytes.Length >= 3 && bytes[0..2] = [| 0xefuy; 0xbbuy; 0xbfuy |]) then
            refuse "source.cacheMetadataBound" "Cache metadata must be bounded strict UTF-8 without BOM."
        let text = UTF8Encoding(false, true).GetString bytes
        use document = JsonDocument.Parse(text, JsonDocumentOptions(MaxDepth = 16))
        let root = document.RootElement
        let rec noDuplicates (value: JsonElement) =
            match value.ValueKind with
            | JsonValueKind.Object ->
                let seen = HashSet<string>(StringComparer.Ordinal)
                for property in value.EnumerateObject() do
                    if not (seen.Add property.Name) then refuse "source.cacheMetadata" "Duplicate cache metadata property."
                    noDuplicates property.Value
            | JsonValueKind.Array -> for child in value.EnumerateArray() do noDuplicates child
            | _ -> ()
        noDuplicates root
        if root.ValueKind <> JsonValueKind.Object then refuse "source.cacheMetadata" "Expected a closed cache metadata object."
        let names = root.EnumerateObject() |> Seq.map _.Name |> Seq.toList
        let expected = set [ "schema"; "sourceSetSha256"; "acquisitionPolicySha256"; "source"; "revision"; "acquisition"; "resolvedUri"; "redirects"; "networkHops"; "catalogSha256" ]
        if names.Length <> expected.Count || Set.ofList names <> expected
           || root.GetProperty("schema").GetString() <> "fsgg.provider-source-cache/1" then
            refuse "source.cacheMetadata" "Unexpected, missing or duplicate cache metadata fields."
        let node = JsonNode.Parse(text) |> nonNull |> _.AsObject()
        node["schema"] <- JsonValue.Create "fsgg.provider-source-observation/1"
        let archive = manifest.Archives.Head
        node["templateSource"] <- JsonValue.Create archive.TemplateSource
        node["archiveSha256"] <- JsonValue.Create(archiveDigest manifest archive)
        let historical = Source.parseObservation (node.ToJsonString()) |> unwrap
        if historical.Source.Id <> reference.Id || historical.Source.ManifestSha256 <> reference.ManifestSha256
           || historical.Revision <> manifest.Revision || historical.CatalogSha256 <> rawCatalogSha256
           || historical.Acquisition = Source.CacheRead then
            refuse "source.cacheIdentity" "Historical cache identity does not join the currently pinned complete source."
        match historical.Source.Locator, reference.Locator with
        | Source.LocalDirectory _, Source.LocalDirectory _ -> ()
        | Source.HttpsEnvelope(_, aRevision, aDigest), Source.HttpsEnvelope(_, bRevision, bDigest)
                when aRevision = bRevision && aDigest = bDigest -> ()
        | _ -> refuse "source.cacheIdentity" "Cache envelope identity differs from the selected source."
        { Source = historical.Source; Revision = historical.Revision
          SourceSetSha256 = historical.SourceSetSha256; AcquisitionPolicySha256 = historical.AcquisitionPolicySha256
          Acquisition = historical.Acquisition; ResolvedUri = historical.ResolvedUri
          Redirects = historical.Redirects; NetworkHops = historical.NetworkHops; Catalog = catalog }

    let private readCache operation reference root key charge =
        let maximum = if charge then operation.Policy.MaximumExpandedBytes - operation.ExpandedBytes else operation.Policy.MaximumExpandedBytes + int64 Source.limits.MaximumReceiptBytes
        let files = captureFiles operation root maximum (Source.limits.MaximumMembers + 2) (Source.limits.MaximumPathDepth + 1)
        guardCapturedTarget operation (Path.Combine(operation.Request.CacheRoot, key)) files
        let payload =
            files |> List.choose (fun file ->
                if file.Path.StartsWith("payload/", StringComparison.Ordinal) then Some { file with Path = file.Path[8..] }
                elif file.Path = "acquisition.json" then None
                else refuse "source.cachePopulation" "Unexpected member outside the closed cache payload and receipt.")
        let manifest, catalog, catalogBytes = validatePayload reference payload
        readMetadata reference manifest catalog (Source.sha256 catalogBytes) (member "acquisition.json" files).Bytes |> ignore
        if charge then chargeExpanded operation (files |> List.sumBy (fun file -> int64 file.Bytes.LongLength))
        let rootPath = Path.Combine(operation.Request.CacheRoot, key)
        { Preview =
            { Source = reference; Revision = manifest.Revision
              SourceSetSha256 = operation.Request.ExpectedSourceSetSha256; AcquisitionPolicySha256 = operation.Request.ExpectedPolicySha256
              Acquisition = Source.CacheRead; ResolvedUri = None; Redirects = []; NetworkHops = []; Catalog = catalog }
          Manifest = manifest; CatalogBytes = Array.copy catalogBytes; PayloadRoot = Path.Combine(rootPath, "payload")
          Files = files |> List.map (fun file -> { AbsolutePath = Path.Combine(rootPath, file.Path); Bytes = int64 file.Bytes.LongLength; Sha256 = file.Sha256 }) }

    let private fetchEnvelope operation (reference: Source.Reference) =
        let initial, expectedHash =
            match reference.Locator with
            | Source.HttpsEnvelope(uri, _, digest) -> uri, digest
            | _ -> refuse "source.locator" "A network fetch requires a selected HTTPS envelope."
        let token = operation.CancellationSource |> Option.get |> _.Token
        let hops = ResizeArray<Source.NetworkHop>()
        let redirects = ResizeArray<string>()
        let mutable selected = initial
        let mutable completed: byte array option = None
        while completed.IsNone do
            checkWork operation
            let uri = Source.validateUri operation.Policy selected |> unwrap
            let addresses = track operation (fun () -> Dns.GetHostAddressesAsync(uri.IdnHost, token)) |> awaitWork operation 10
            if addresses.Length = 0 || addresses.Length > 64 || (addresses |> Array.exists (Source.isPublicAddress >> not)) then
                refuse "source.address" "The complete DNS answer must be nonempty and within the public unicast profile."
            let address = addresses[0]
            let mutable connected: string option = None
            let handler = own operation (new SocketsHttpHandler())
            handler.AllowAutoRedirect <- false
            handler.AutomaticDecompression <- DecompressionMethods.None
            handler.UseCookies <- false
            handler.UseProxy <- false
            handler.Credentials <- null
            handler.PreAuthenticate <- false
            handler.ConnectTimeout <- TimeSpan.FromSeconds 10.0
            handler.MaxResponseHeadersLength <- 64
            handler.MaxResponseDrainSize <- 0
            handler.ResponseDrainTimeout <- TimeSpan.Zero
            handler.MaxConnectionsPerServer <- 1
            handler.PooledConnectionLifetime <- TimeSpan.Zero
            handler.ConnectCallback <- Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>(fun context cancellation ->
                ValueTask<Stream>(task {
                    if context.DnsEndPoint.Host <> uri.IdnHost || context.DnsEndPoint.Port <> 443 then
                        refuse "source.endpoint" "HTTP requested a different endpoint from the resolved selection."
                    let socket = own operation (new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                    let! _ = track operation (fun () -> socket.ConnectAsync(IPEndPoint(address, 443), cancellation).AsTask())
                    match socket.RemoteEndPoint with
                    | :? IPEndPoint as actual when actual.Address.Equals address && actual.Port = 443 -> connected <- Some(actual.Address.ToString())
                    | _ -> refuse "source.endpoint" "Actual connected endpoint differs from the validated address."
                    return new NetworkStream(socket, false) :> Stream
                }))
            let client = own operation (new HttpClient(handler, false))
            client.Timeout <- Timeout.InfiniteTimeSpan
            let request = own operation (new HttpRequestMessage(HttpMethod.Get, uri))
            request.Version <- Version(1, 1)
            request.VersionPolicy <- HttpVersionPolicy.RequestVersionExact
            let response = track operation (fun () -> client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)) |> awaitWork operation 15 |> own operation
            let endpoint = connected |> Option.defaultWith (fun () -> refuse "source.endpoint" "No original connected endpoint was observed.")
            let status = int response.StatusCode
            hops.Add { Uri = selected; Address = endpoint; StatusCode = status }
            if List.contains status [ 301; 302; 303; 307; 308 ] then
                let target =
                    match response.Headers.Location with
                    | null -> refuse "source.redirect" "Redirect response has no location."
                    | location -> if location.IsAbsoluteUri then location.AbsoluteUri else Uri(uri, location).AbsoluteUri
                let next = Source.validateRedirect operation.Policy (Uri initial) redirects.Count target |> unwrap
                selected <- next.AbsoluteUri
                redirects.Add selected
            elif status <> 200 then refuse "source.httpStatus" ("Source acquisition returned HTTP " + string status + ".")
            else
                if response.Content.Headers.ContentEncoding.Count <> 0 then refuse "source.contentEncoding" "Encoded HTTP bodies are outside the closed ZIP transfer profile."
                let advertised = response.Content.Headers.ContentLength
                if advertised.HasValue && (advertised.Value < 0L || advertised.Value > operation.Policy.MaximumTransferBytes - operation.TransferBytes) then
                    refuse "source.transferBound" "Advertised envelope exceeds the remaining aggregate transfer bound."
                let stream = track operation (fun () -> response.Content.ReadAsStreamAsync token) |> awaitWork operation 15 |> own operation
                use bytes = new MemoryStream()
                let buffer = Array.zeroCreate<byte> 65536
                let mutable eof = false
                while not eof do
                    let length = track operation (fun () -> stream.ReadAsync(buffer.AsMemory(), token).AsTask()) |> awaitWork operation 15
                    if length = 0 then eof <- true
                    else
                        operation.TransferBytes <- Checked.(+) operation.TransferBytes (int64 length)
                        if operation.TransferBytes > operation.Policy.MaximumTransferBytes then refuse "source.transferBound" "Streamed source bytes exceed the aggregate transfer bound."
                        bytes.Write(buffer, 0, length)
                if advertised.HasValue && advertised.Value <> bytes.Length then refuse "source.incompleteTransfer" "Observed EOF length differs from Content-Length."
                let result = bytes.ToArray()
                if Source.sha256 result <> expectedHash then refuse "source.envelopeDigestMismatch" "Actual complete envelope differs from its immutable raw pin."
                completed <- Some result
        completed.Value, selected, List.ofSeq redirects, List.ofSeq hops

    let private decodeEnvelope maximumExpandedBytes check charge (bytes: byte array) =
        let require condition message = if not condition then refuse "source.zipEnvelope" message
        let u16 offset =
            require (offset >= 0 && offset <= bytes.Length - 2) "Truncated ZIP field."
            int (BitConverter.ToUInt16(bytes, offset))
        let u32 offset =
            require (offset >= 0 && offset <= bytes.Length - 4) "Truncated ZIP field."
            let value = BitConverter.ToUInt32(bytes, offset)
            require (value <= uint32 Int32.MaxValue) "ZIP32 field exceeds the supported finite range."
            int value
        let signature offset value = offset >= 0 && offset <= bytes.Length - 4 && BitConverter.ToUInt32(bytes, offset) = value
        require (maximumExpandedBytes >= 0L && maximumExpandedBytes <= Source.limits.MaximumExpandedBytes) "Invalid aggregate expansion selection."
        require (bytes.Length >= 22 && int64 bytes.Length <= Source.limits.MaximumEnvelopeBytes) "Envelope exceeds ZIP32 size bounds."
        let mutable endOffset = -1
        for offset in max 0 (bytes.Length - 65557) .. bytes.Length - 22 do
            if signature offset 0x06054b50u && offset + 22 + u16 (offset + 20) = bytes.Length then
                require (endOffset = -1) "Ambiguous ZIP end records."
                endOffset <- offset
        require (endOffset >= 0) "Missing complete ZIP end record."
        let count = u16 (endOffset + 10)
        require (u16 (endOffset + 4) = 0 && u16 (endOffset + 6) = 0 && u16 (endOffset + 8) = count
                 && count > 0 && count <= Source.limits.MaximumMembers) "Only bounded single-disk ZIP32 is supported."
        let centralStart, centralBytes = u32 (endOffset + 16), u32 (endOffset + 12)
        require (centralStart + centralBytes = endOffset) "Central directory has unaccounted bytes or ZIP64 records."
        let names = HashSet<string>(StringComparer.OrdinalIgnoreCase)
        let inventory = ResizeArray<string * int * int>()
        let spans = ResizeArray<int * int>()
        let checkExtra start length =
            let mutable offset = start
            while offset < start + length do
                require (offset + 4 <= start + length) "Truncated ZIP extra field."
                let kind, size = u16 offset, u16 (offset + 2)
                require (offset + 4 + size <= start + length && (kind = 0x5455 || kind = 0x7875)) "Unsupported ZIP extra field, link metadata or ZIP64."
                offset <- offset + 4 + size
        let mutable cursor = centralStart
        let mutable declaredExpanded = 0L
        for _ in 1..count do
            check ()
            require (signature cursor 0x02014b50u && cursor + 46 <= endOffset) "Invalid central directory member."
            let flags, method = u16 (cursor + 8), u16 (cursor + 10)
            require (flags &&& ~~~0x080e = 0 && (method = 0 || method = 8)) "Encrypted or unsupported ZIP member flags/method."
            let compressed, expanded = u32 (cursor + 20), u32 (cursor + 24)
            require (int64 expanded <= Source.limits.MaximumMemberBytes) "ZIP member exceeds its expanded ceiling."
            declaredExpanded <- Checked.(+) declaredExpanded (int64 expanded)
            require (declaredExpanded <= maximumExpandedBytes) "ZIP advertised aggregate expansion exceeds the remaining source budget."
            let nameLength, extraLength, commentLength = u16 (cursor + 28), u16 (cursor + 30), u16 (cursor + 32)
            let next = cursor + 46 + nameLength + extraLength + commentLength
            require (next <= endOffset && nameLength > 0 && nameLength <= Source.limits.MaximumPathBytes && u16 (cursor + 34) = 0) "Invalid central member bounds or disk."
            let nameBytes = bytes[cursor + 46 .. cursor + 45 + nameLength]
            require (flags &&& 0x0800 <> 0 || (nameBytes |> Array.forall (fun value -> value < 128uy))) "Unflagged ZIP names must be unambiguous ASCII."
            let name = UTF8Encoding(false, true).GetString nameBytes
            require (names.Add name && not (name.EndsWith("/", StringComparison.Ordinal))) "Directory or duplicate/case-alias ZIP member."
            let parts = name.Split('/')
            require (parts.Length <= Source.limits.MaximumPathDepth && not (name.Contains('\\'))
                     && (parts |> Array.forall (fun part -> part <> "" && part <> "." && part <> ".." && not (part.Contains(':'))))) "Unsafe or excessive ZIP path."
            let attributes = BitConverter.ToUInt32(bytes, cursor + 38)
            let kind = (attributes >>> 16) &&& 0xf000u
            require ((kind = 0u || kind = 0x8000u) && attributes &&& 0x10u = 0u) "ZIP symlink, directory or special-file member refused."
            checkExtra (cursor + 46 + nameLength) extraLength
            let local = u32 (cursor + 42)
            require (signature local 0x04034b50u && local + 30 <= centralStart) "Missing local member header."
            let localNameLength, localExtraLength = u16 (local + 26), u16 (local + 28)
            let payload = local + 30 + localNameLength + localExtraLength
            require (payload <= centralStart && compressed <= centralStart - payload
                     && u16 (local + 6) = flags && u16 (local + 8) = method && localNameLength = nameLength
                     && bytes[local + 30 .. local + 29 + localNameLength] = nameBytes) "Local/central member identity differs or overlaps the central directory."
            checkExtra (local + 30 + localNameLength) localExtraLength
            let mutable finish = payload + compressed
            if flags &&& 8 = 0 then
                require (bytes[local + 14 .. local + 17] = bytes[cursor + 16 .. cursor + 19]
                         && u32 (local + 18) = compressed && u32 (local + 22) = expanded) "Local sizes/CRC differ from the central member."
            else
                if signature finish 0x08074b50u then finish <- finish + 4
                require (finish + 12 <= centralStart && bytes[finish .. finish + 3] = bytes[cursor + 16 .. cursor + 19]
                         && u32 (finish + 4) = compressed && u32 (finish + 8) = expanded) "Incomplete or conflicting member data descriptor."
                finish <- finish + 12
            spans.Add(local, finish)
            inventory.Add(name, expanded, compressed)
            cursor <- next
        require (cursor = endOffset) "Unaccounted central directory records."
        let mutable previous = 0
        for first, finish in spans |> Seq.sortBy fst do
            require (first = previous) "Prepended, overlapping or hidden inter-member ZIP bytes are refused."
            previous <- finish
        require (previous = centralStart) "Unaccounted bytes before the central directory."
        use memory = new MemoryStream(bytes, false)
        use archive = new ZipArchive(memory, ZipArchiveMode.Read, false)
        require (archive.Entries.Count = inventory.Count) "ZIP reader population differs from the closed inventory."
        [ for index in 0 .. inventory.Count - 1 do
            check ()
            let name, expanded, compressed = inventory[index]
            let entry = archive.Entries[index]
            require (entry.FullName = name && entry.Length = int64 expanded && entry.CompressedLength = int64 compressed) "ZIP reader metadata differs from the original inventory."
            use stream = entry.Open()
            use output = new MemoryStream()
            let buffer = Array.zeroCreate<byte> 65536
            let mutable eof = false
            while not eof do
                check ()
                let length = stream.Read(buffer, 0, buffer.Length)
                if length = 0 then eof <- true
                else
                    require (output.Length + int64 length <= int64 expanded) "Observed ZIP expansion exceeds its declared bounded size."
                    charge (int64 length)
                    output.Write(buffer, 0, length)
            require (output.Length = int64 expanded) "ZIP member ended before its declared length."
            let payload = output.ToArray()
            yield { Linux.CapturedFile.Path = name; Bytes = payload; Sha256 = Source.sha256 payload } ]

    let private envelopeFiles operation bytes =
        decodeEnvelope (operation.Policy.MaximumExpandedBytes - operation.ExpandedBytes)
            (fun () -> checkWork operation) (chargeExpanded operation) bytes

    let internal inspectEnvelope maximumExpandedBytes bytes =
        try
            let mutable expanded = 0L
            let charge count =
                expanded <- Checked.(+) expanded count
                if expanded > maximumExpandedBytes then refuse "source.expandedBound" "Actual expansion exceeds the selected bound."
            decodeEnvelope maximumExpandedBytes ignore charge bytes
            |> List.map (fun file -> file.Path, file.Bytes)
            |> Ok
        with
        | Refusal errors -> Error errors
        | :? IOException | :? ArgumentException | :? OverflowException ->
            Error [ diagnostic "source.zipEnvelope" "$" "Malformed or unsupported bounded ZIP envelope." ]

    type private Acquired =
        { Reference: Source.Reference
          Manifest: Source.Manifest
          Preview: SourcePreview
          Files: Linux.CapturedFile list
          CatalogBytes: byte array }
    type private SourceState = Cached of Entry | Acquired of Acquired

    let private acquire operation reference =
        guardRoots operation
        let files, acquisition, resolved, redirects, hops =
            match reference.Locator with
            | Source.LocalDirectory path ->
                let root = Linux.openDirectory operation.Native (budget operation) path
                operation.LocalRoots <- (reference, root) :: operation.LocalRoots
                guardRoots operation
                let files = captureFiles operation root (operation.Policy.MaximumExpandedBytes - operation.ExpandedBytes) (Source.limits.MaximumMembers + 1) Source.limits.MaximumPathDepth
                guardCapturedTarget operation path files
                chargeExpanded operation (files |> List.sumBy (fun file -> int64 file.Bytes.LongLength))
                files, Source.LocalCopy, None, [], []
            | Source.HttpsEnvelope _ ->
                let envelope, uri, redirects, hops = fetchEnvelope operation reference
                envelopeFiles operation envelope, Source.HttpsFetch, Some uri, redirects, hops
        let manifest, catalog, catalogBytes = validatePayload reference files
        { Reference = reference; Manifest = manifest; Files = files; CatalogBytes = catalogBytes
          Preview =
            { Source = reference; Revision = manifest.Revision; SourceSetSha256 = operation.Request.ExpectedSourceSetSha256
              AcquisitionPolicySha256 = operation.Request.ExpectedPolicySha256; Acquisition = acquisition
              ResolvedUri = resolved; Redirects = redirects; NetworkHops = hops; Catalog = catalog } }

    let private publishCache operation (acquired: Acquired) =
        guardRoots operation
        let cache = operation.Cache |> Option.get
        let phase = budget operation
        let key = Source.cacheKey acquired.Reference |> unwrap
        let receipt = metadataBytes acquired.Preview acquired.Manifest (Source.sha256 acquired.CatalogBytes)
        let stage = { Name = ".source-" + Guid.NewGuid().ToString("N"); Root = None; Published = false; PublicationUnknown = false }
        operation.Stages.Add stage
        let root = Linux.createDirectory operation.Native phase cache stage.Name
        stage.Root <- Some root
        let payload = Linux.createDirectory operation.Native phase root "payload"
        let write directory path (bytes: byte array) =
            guardRoots operation
            operation.PrivateBytes <- Checked.(+) operation.PrivateBytes (int64 bytes.LongLength)
            if operation.PrivateBytes > Source.limits.MaximumPrivateBytes then refuse "source.privateBound" "Original private cache/staging write population exceeded its ceiling."
            Linux.writeFile operation.Native phase directory path bytes false
        for file in acquired.Files do write payload file.Path file.Bytes
        write root "acquisition.json" receipt
        let expected =
            (acquired.Files |> List.map (fun file -> "payload/" + file.Path, (file.Bytes.LongLength, file.Sha256)))
            @ [ "acquisition.json", (receipt.LongLength, Source.sha256 receipt) ] |> Map.ofList
        let captured = captureFiles operation root (operation.Policy.MaximumExpandedBytes + int64 Source.limits.MaximumReceiptBytes) (Source.limits.MaximumMembers + 2) (Source.limits.MaximumPathDepth + 1)
        let actual = captured |> List.map (fun file -> file.Path, (file.Bytes.LongLength, file.Sha256)) |> Map.ofList
        if actual <> expected then refuse "source.stageReadback" "Original staged source differs from its complete verified preimage."
        guardRoots operation
        stage.PublicationUnknown <- true
        match Linux.commitNoReplace phase cache root stage.Name cache key with
        | CatalogScaffoldWorkflow.Committed -> stage.Published <- true; stage.PublicationUnknown <- false
        | CatalogScaffoldWorkflow.Refused ->
            stage.PublicationUnknown <- false
            refuse "source.cacheConflict" "A cache entry appeared or publication refused; no overwrite or automatic retry is permitted."
        | _ -> refuse "source.cachePublicationUnknown" "Original no-replace cache publication is uncertain; its stage and native custody remain owned."
        let published = Linux.tryOpenChildDirectory operation.Native phase cache key |> Option.defaultWith (fun () -> refuse "source.cacheReadback" "Published entry is absent on independent readback.")
        let readback = readCache operation acquired.Reference published key false
        { readback with Preview = acquired.Preview }

    let private retire operation =
        let phase = budget operation
        Linux.stopUsefulWork phase
        let mutable unknown = false
        try operation.CancellationSource |> Option.iter _.Cancel with _ -> unknown <- true
        let pending () = lock operation.Gate (fun () -> operation.Tasks |> Seq.exists (fun slot -> slot.Started && (slot.Task |> Option.forall (fun task -> not task.IsCompleted))))
        // No callback may append a new socket after the disposal inventory is frozen.
        while pending () && ticks () < operation.End do Thread.Sleep(min 5 (remainingMilliseconds operation.End))
        if pending () then unknown <- true
        let disposables = lock operation.Gate (fun () -> operation.Disposables |> Seq.rev |> Seq.toList)
        let disposalTasks =
            disposables |> List.choose (fun disposable ->
                if unknown || ticks () >= operation.End then
                    unknown <- true
                    None
                else
                    let slot = { Started = true; Task = None }
                    lock operation.Gate (fun () -> operation.Tasks.Add slot)
                    let task = new Task(Action(fun () -> disposable.Dispose()), CancellationToken.None, TaskCreationOptions.None)
                    slot.Task <- Some task
                    task.Start(TaskScheduler.Default)
                    Some task)
        while pending () && ticks () < operation.End do Thread.Sleep(min 5 (remainingMilliseconds operation.End))
        if pending () || (disposalTasks |> List.exists (fun task -> task.IsFaulted || task.IsCanceled)) then unknown <- true
        for task in disposalTasks do if task.IsFaulted then task.Exception |> ignore
        if not (Linux.settled operation.Native) then unknown <- true
        if not unknown then
            try
                for stage in operation.Stages do
                    if stage.PublicationUnknown then unknown <- true
                    elif not stage.Published then
                        match stage.Root with
                        | None -> unknown <- true
                        | Some root ->
                            Linux.captureRetirementInventory operation.Native phase root
                            Linux.retireDirectory operation.Native phase (operation.Cache |> Option.get) stage.Name root
                if not unknown then Linux.releaseKnown phase operation.Native
            with _ -> unknown <- true
        operation.CleanupUnknown <- operation.CleanupUnknown || unknown
        operation.Settled <- not operation.CleanupUnknown && Linux.settled operation.Native
        operation.Settled

    let private execute operation =
        let phase = Linux.beginPhaseWithCleanup operation.Policy.AcquisitionSeconds Source.limits.CleanupSeconds operation.Cancellation
        operation.PhaseBudget <- Some phase
        let workEnd, finish = Linux.phaseDeadlines phase
        operation.WorkEnd <- workEnd
        operation.End <- finish
        let cancellation = CancellationTokenSource.CreateLinkedTokenSource operation.Cancellation
        cancellation.CancelAfter(remainingMilliseconds workEnd)
        operation.CancellationSource <- Some cancellation
        let mutable result: PreparedSet option = None
        try
            if not (OperatingSystem.IsLinux()) || Runtime.InteropServices.RuntimeInformation.ProcessArchitecture <> Runtime.InteropServices.Architecture.X64
               || Environment.Version <> Version(10, 0, 12) then
                refuse "source.hostProfile" "Source filesystem custody requires the existing Linux-x64 .NET10.0.12 catalog host profile."
            Linux.observeAbi operation.Native phase |> unwrap |> ignore
            operation.Cache <- Some(Linux.openDirectory operation.Native phase operation.Request.CacheRoot)
            operation.TargetParent <- operation.Request.TargetRoot |> Option.map (fun target ->
                let parent = Path.GetDirectoryName target |> nonNull
                let leaf = Path.GetFileName target
                if String.IsNullOrEmpty leaf then refuse "source.targetPath" "A target leaf beneath an existing parent is required."
                Linux.openDirectory operation.Native phase parent, leaf)
            guardRoots operation
            let cached =
                operation.Sources.Sources |> List.map (fun source ->
                    let key = Source.cacheKey source |> unwrap
                    match Linux.tryOpenChildDirectory operation.Native phase (operation.Cache |> Option.get) key with
                    | None -> source, None
                    | Some root -> source, Some(readCache operation source root key true))
            let complete = cached |> List.forall (snd >> Option.isSome)
            send operation (Workflow.CacheObserved complete) |> ignore
            if not complete && operation.Request.Offline then raise (Refusal(Workflow.diagnostics operation.Model))
            let all =
                cached |> List.map (fun (source, cached) ->
                    match cached with Some entry -> Cached entry | None -> Acquired(acquire operation source))
            if not complete then send operation Workflow.AcquisitionCompleted |> ignore
            let catalogOf = function Cached entry -> entry.Preview.Source, entry.Preview.Catalog.Catalog | Acquired value -> value.Reference, value.Preview.Catalog.Catalog
            let ordered = Source.orderCatalogs (all |> List.map catalogOf) |> unwrap
            send operation Workflow.ValidationCompleted |> ignore
            let entries =
                ordered |> List.map (fun (source, _) ->
                    match all |> List.find (fun value -> (catalogOf value |> fst).Id = source.Id) with
                    | Cached entry -> entry
                    | Acquired value -> publishCache operation value)
            if not complete then send operation Workflow.CachePublished |> ignore
            guardRoots operation
            result <- Some { Entries = entries; TargetRoot = operation.Request.TargetRoot; CacheRoot = operation.Request.CacheRoot }
        with error ->
            let findings =
                match error with
                | Refusal errors | Linux.Refused errors | Linux.CustodyUnknown errors -> errors
                | _ -> [ diagnostic "source.acquisitionFailed" "$" ("Original source acquisition failed: " + error.GetType().Name + ": " + (if error.Message.Length > 4096 then error.Message[..4095] + " [truncated]" else error.Message)) ]
            send operation (Workflow.FailureObserved findings) |> ignore
        let settled =
            try retire operation
            with error ->
                operation.CleanupUnknown <- true
                operation.Settled <- false
                send operation (Workflow.FailureObserved [ diagnostic "source.cleanupUnknown" "$" ("Original retirement failed: " + error.GetType().Name) ]) |> ignore
                false
        send operation (Workflow.RetirementObserved settled) |> ignore
        if settled then cancellation.Dispose()
        match result with
        | Some prepared when settled && Workflow.outcome operation.Model = Workflow.Prepared -> Ok prepared
        | _ ->
            let findings = Workflow.diagnostics operation.Model
            Error(if findings.IsEmpty then [ diagnostic "source.cleanupUnknown" "$" "Original source resources did not settle; no prepared handle or retry is permitted." ] else findings)

    let prepare (request: Request) cancellationToken =
        try
            if isNull request.SourceSetBytes || isNull request.PolicyBytes then refuse "source.requestBytes" "Explicit source-set and acquisition-policy bytes are required."
            let retained = { request with SourceSetBytes = Array.copy request.SourceSetBytes; PolicyBytes = Array.copy request.PolicyBytes }
            let sources = Source.parseSet retained.ExpectedSourceSetSha256 retained.SourceSetBytes |> unwrap
            let policy = Source.parsePolicy retained.ExpectedPolicySha256 retained.PolicyBytes |> unwrap
            for source in sources.Sources do
                match source.Locator with Source.HttpsEnvelope(uri, _, _) -> Source.validateUri policy uri |> unwrap |> ignore | _ -> ()
            let model, _ = Workflow.init
                                { Sources = sources; SourceSetSha256 = retained.ExpectedSourceSetSha256; Policy = policy
                                  CacheRoot = retained.CacheRoot; TargetRoot = retained.TargetRoot; Offline = retained.Offline }
            if Workflow.outcome model = Workflow.Refused then raise (Refusal(Workflow.diagnostics model))
            Ok { Gate = obj (); Request = retained; Sources = sources; Policy = policy; Cancellation = cancellationToken
                 Native = Linux.createCustody (); Tasks = ResizeArray(); Disposables = ResizeArray(); Stages = ResizeArray()
                 Model = model; Runner = None; PhaseBudget = None; CancellationSource = None; Cache = None
                 LocalRoots = []; CapturedRoots = []; TargetParent = None; End = 0L; WorkEnd = 0L; ExpandedBytes = 0L; TransferBytes = 0L
                 PrivateBytes = 0L; Settled = false; Released = false; CleanupUnknown = false }
        with
        | Refusal errors -> Error errors
        | :? ArgumentException as error -> Error [ diagnostic "source.request" "$" error.Message ]

    let run (operation: Operation) = async {
        let task =
            lock operation.Gate (fun () ->
                if operation.Released then
                    Task.FromResult(Error [ diagnostic "source.released" "$" "The original operation has already been released." ])
                else
                    match operation.Runner with
                    | Some existing -> existing
                    | None ->
                        let task = new Task<Result<PreparedSet, Fsgg.ProviderCatalog.Diagnostic list>>(Func<_>(fun () -> execute operation), CancellationToken.None, TaskCreationOptions.None)
                        operation.Runner <- Some task
                        try
                            task.Start(TaskScheduler.Default)
                            task
                        with _ when task.Status = TaskStatus.Created ->
                            // A cold task that never left Created dispatched no effect.
                            let errors = [ diagnostic "source.runnerStart" "$" "The original preheld source runner could not start." ]
                            send operation (Workflow.FailureObserved errors) |> ignore
                            send operation (Workflow.RetirementObserved true) |> ignore
                            operation.Settled <- true
                            let refused = Task.FromResult(Error errors)
                            operation.Runner <- Some refused
                            refused)
        return! Async.AwaitTask task
    }
    let observe (operation: Operation) =
        lock operation.Gate (fun () ->
            { Phase = if operation.Runner.IsNone then Workflow.Preparing else Workflow.phase operation.Model
              Outcome = Workflow.outcome operation.Model; Diagnostics = Workflow.diagnostics operation.Model
              Settled = operation.Settled })
    let waitForSettled (operation: Operation) = async {
        let runner = lock operation.Gate (fun () -> operation.Runner)
        match runner with
        | Some task ->
            try
                let! _ = Async.AwaitTask task
                ()
            with _ -> ()
        | None -> ()
        return observe operation
    }
    let release (operation: Operation) =
        lock operation.Gate (fun () ->
            if operation.Runner.IsNone || (operation.Settled && operation.Runner.Value.IsCompleted) then
                operation.Released <- true
                Ok()
            else Error [ diagnostic "source.releaseUnsettled" "$" "Original native/network custody remains owned; release cannot erase Unknown." ])

    let inspect (prepared: PreparedSet) =
        prepared.Entries |> List.map (fun entry ->
            { entry.Preview with Catalog =
                { entry.Preview.Catalog with Selected = None
                                             Catalog = { entry.Preview.Catalog.Catalog with Providers = entry.Preview.Catalog.Catalog.Providers |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a.Id, b.Id)) } } })

    let select (prepared: PreparedSet) provider overrides =
        try
            let candidates = prepared.Entries |> List.filter (fun entry -> entry.Preview.Catalog.Catalog.Providers |> List.exists (fun descriptor -> descriptor.Id = provider))
            let entry = match candidates with [ entry ] -> entry | _ -> refuse "source.providerSelection" "Provider selection must identify exactly one previously verified source."
            let selection: CatalogScaffoldWorkflow.CatalogSelection =
                { CatalogBytes = Array.copy entry.CatalogBytes; ExpectedRawDigest = Source.sha256 entry.CatalogBytes; Provider = Some provider; Overrides = overrides }
            let resolved = CatalogScaffoldWorkflow.prepare selection |> unwrap
            let selected = resolved.Selected |> Option.get
            let archive = entry.Manifest.Archives |> List.find (fun archive -> archive.TemplateSource = selected.Descriptor.TemplateSource)
            let archiveFile = entry.Files |> List.find (fun file -> file.AbsolutePath = Path.Combine(entry.PayloadRoot, archive.Path))
            let observed = observation entry.Preview selection.ExpectedRawDigest archive.TemplateSource archiveFile.Sha256
            Source.validateObservation observed |> fun errors -> if not errors.IsEmpty then raise (Refusal errors)
            Ok { Entry = entry; Selection = selection; Archive = archiveFile; Observation = observed; TargetRoot = prepared.TargetRoot; CacheRoot = prepared.CacheRoot }
        with Refusal errors -> Error errors
    let catalogBytes (source: SelectedSource) = Array.copy source.Entry.CatalogBytes
    let catalogSha256 (source: SelectedSource) = source.Selection.ExpectedRawDigest
    let archivePath (source: SelectedSource) = source.Archive.AbsolutePath
    let archiveSha256 (source: SelectedSource) = source.Archive.Sha256
    let sourceObservation (source: SelectedSource) = source.Observation
    let catalogSelection (source: SelectedSource) = { source.Selection with CatalogBytes = Array.copy source.Selection.CatalogBytes }
    let targetRoot (source: SelectedSource) = source.TargetRoot
    let inputFiles (source: SelectedSource) = source.Entry.Files
    let internal cacheRoot (source: SelectedSource) = source.CacheRoot
    let verifyCaptured (source: SelectedSource) (captured: (string * byte array) list) =
        try
            let paths = captured |> List.map fst
            if (paths |> Set.ofList |> Set.count) <> paths.Length || Set.ofList paths <> Set.ofList (source.Entry.Files |> List.map _.AbsolutePath) then
                refuse "source.capturedPopulation" "The complete source input population must be captured exactly once."
            let actual = Map.ofList captured
            for file in source.Entry.Files do
                let bytes = actual[file.AbsolutePath]
                if isNull bytes || bytes.LongLength <> file.Bytes || Source.sha256 bytes <> file.Sha256 then
                    refuse "source.capturedChanged" "A source input differs from its original prepared byte identity."
            let payload =
                captured |> List.choose (fun (path, bytes) ->
                    if path.StartsWith(source.Entry.PayloadRoot + string Path.DirectorySeparatorChar, StringComparison.Ordinal) then
                        Some { Linux.CapturedFile.Path = path[(source.Entry.PayloadRoot.Length + 1)..]; Bytes = bytes; Sha256 = Source.sha256 bytes }
                    else None)
            let manifest, catalog, bytes = validatePayload source.Entry.Preview.Source payload
            if manifest <> source.Entry.Manifest || bytes <> source.Entry.CatalogBytes then refuse "source.capturedChanged" "Captured source manifest/catalog differs from the selected closure."
            let metadata = Path.Combine(Path.GetDirectoryName(source.Entry.PayloadRoot) |> nonNull, "acquisition.json")
            readMetadata source.Entry.Preview.Source manifest catalog (Source.sha256 bytes) actual[metadata] |> ignore
            Ok()
        with Refusal errors -> Error errors

