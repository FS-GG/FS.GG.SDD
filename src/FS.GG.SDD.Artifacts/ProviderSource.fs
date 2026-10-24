namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Globalization
open System.Collections.Generic

/// Source declarations identify untrusted data. No parser or digest grants execution authority.
module ProviderSource =
    type Origin =
        | BuiltIn
        | Workspace
        | Explicit

    type Locator =
        | LocalDirectory of absolutePath: string
        | HttpsEnvelope of uri: string * revision: string * envelopeSha256: string

    type Reference =
        {
            Id: string
            Origin: Origin
            Locator: Locator
            ManifestSha256: string
        }

    type SourceSet = { Schema: string; Sources: Reference list }

    type Member = { Path: string; Bytes: int64; Sha256: string }
    type Archive = { TemplateSource: string; Path: string }

    type Manifest =
        {
            Schema: string
            Id: string
            Revision: string
            CatalogPath: string
            Members: Member list
            Archives: Archive list
        }

    /// Caller-selected acquisition limits can narrow, never widen, the hard ceilings.
    type Policy =
        {
            Schema: string
            AllowedOrigins: string list
            MaximumRedirects: int
            AcquisitionSeconds: int
            MaximumTransferBytes: int64
            MaximumExpandedBytes: int64
        }

    type Acquisition =
        | LocalCopy
        | HttpsFetch
        | CacheRead

    type NetworkHop = { Uri: string; Address: string; StatusCode: int }

    /// Observed source identity remains distinct from the existing catalog execution verdict.
    type Observation =
        {
            Schema: string
            SourceSetSha256: string
            AcquisitionPolicySha256: string
            Source: Reference
            Revision: string
            Acquisition: Acquisition
            ResolvedUri: string option
            Redirects: string list
            NetworkHops: NetworkHop list
            CatalogSha256: string
            TemplateSource: string
            ArchiveSha256: string
        }

    type Limits =
        {
            MaximumSources: int
            MaximumSetBytes: int
            MaximumManifestBytes: int
            MaximumPolicyBytes: int
            MaximumReceiptBytes: int
            MaximumMembers: int
            MaximumMemberBytes: int64
            MaximumEnvelopeBytes: int64
            MaximumExpandedBytes: int64
            MaximumCatalogBytes: int
            MaximumPathBytes: int
            MaximumPathDepth: int
            MaximumAcquisitionSeconds: int
            CleanupSeconds: int
            MaximumPrivateBytes: int64
        }

    let limits =
        { MaximumSources = 16; MaximumSetBytes = 262144; MaximumManifestBytes = 262144
          MaximumPolicyBytes = 65536; MaximumReceiptBytes = 262144; MaximumMembers = 4096
          MaximumMemberBytes = 268435456L; MaximumEnvelopeBytes = 268435456L
          MaximumExpandedBytes = 536870912L; MaximumCatalogBytes = 1048576
          MaximumPathBytes = 1024; MaximumPathDepth = 32; MaximumAcquisitionSeconds = 180
          CleanupSeconds = 15; MaximumPrivateBytes = 1073741824L }

    let private utf8 = UTF8Encoding(false, true)
    let sha256 (bytes: byte array) = "sha256:" + Convert.ToHexStringLower(SHA256.HashData bytes)
    let private diagnostic code path message : Fsgg.ProviderCatalog.Diagnostic =
        { Code = code; Path = path; Message = message }
    exception private InvalidSource of Fsgg.ProviderCatalog.Diagnostic list
    let private refuse code path message = raise (InvalidSource [ diagnostic code path message ])
    let private check condition code path message = if not condition then refuse code path message
    let private protect f =
        try Ok(f ()) with
        | InvalidSource errors -> Error errors
        | :? JsonException -> Error [ diagnostic "source.malformed" "$" "Malformed source JSON." ]
        | :? DecoderFallbackException -> Error [ diagnostic "source.encoding" "$" "Expected strict UTF-8 without BOM." ]
        | :? EncoderFallbackException -> Error [ diagnostic "source.encoding" "$" "Invalid Unicode text." ]
        | :? InvalidOperationException -> Error [ diagnostic "source.shape" "$" "Unexpected source JSON value kind." ]
        | :? OverflowException -> Error [ diagnostic "source.bound" "$" "Source counter overflow." ]
    let private errors action = match protect action with Ok _ -> [] | Error diagnostics -> diagnostics
    let private requireValid diagnostics = if not (List.isEmpty diagnostics) then raise (InvalidSource diagnostics)
    let private text maximum path (value: string) =
        check (not (String.IsNullOrWhiteSpace value) && value.Length <= maximum && not (value |> Seq.exists Char.IsControl))
            "source.text" path "Expected bounded nonempty text without control characters."
        utf8.GetByteCount value |> ignore
    let private hash path (value: string) =
        check (not (obj.ReferenceEquals(value, null)) && value.Length = 71 && value.StartsWith("sha256:", StringComparison.Ordinal)
               && (value[7..] |> Seq.forall (fun c -> (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))))
            "source.digest" path "Expected sha256: followed by 64 lowercase hexadecimal digits."
    let private portable path (value: string) =
        text limits.MaximumPathBytes path value
        let parts = value.Split('/')
        check (utf8.GetByteCount value <= limits.MaximumPathBytes && parts.Length <= limits.MaximumPathDepth)
            "source.path" path "Member path exceeds the closed portable limits."
        let devices = set ([ "CON"; "PRN"; "AUX"; "NUL" ] @ [ for i in 1..9 do yield "COM" + string i; yield "LPT" + string i ])
        check (parts |> Array.forall (fun part ->
            part <> "" && part <> "." && part <> ".." && not (part.EndsWith(".", StringComparison.Ordinal))
            && not (part.EndsWith(" ", StringComparison.Ordinal))
            && not (part |> Seq.exists (fun c -> c = '\\' || c = ':' || c = '<' || c = '>' || c = '"' || c = '|' || c = '?' || c = '*'))
            && not (Set.contains (((part.Split('.')).[0]).ToUpperInvariant()) devices)))
            "source.path" path "Expected a contained portable member path with no reserved basename."
    let private unique (comparer: IEqualityComparer<string>) code path (values: string list) =
        let seen = HashSet<string>(comparer)
        check (values |> List.forall seen.Add) code path "Duplicate or ambiguous identity."
    let private absolute path value =
        text 4096 path value
        check (Path.IsPathFullyQualified value) "source.path" path "Expected an explicit absolute directory."
    let private uriShape (value: string) =
        text 4096 "$.uri" value
        let mutable uri = Unchecked.defaultof<Uri>
        check (Uri.TryCreate(value, UriKind.Absolute, &uri)) "source.uri" "$.uri" "Expected an absolute HTTPS URI."
        check (uri.Scheme = "https" && uri.Port = 443 && uri.UserInfo = "" && uri.Fragment = ""
               && uri.HostNameType = UriHostNameType.Dns && not (uri.Host.EndsWith(".", StringComparison.Ordinal)))
            "source.uri" "$.uri" "Expected a DNS HTTPS origin on port443 without credentials or fragment."
        uri
    let private origin (uri: Uri) = uri.GetLeftPart(UriPartial.Authority)
    let private rank = function BuiltIn -> 0 | Workspace -> 1 | Explicit -> 2
    let private compareReferences (a: Reference) (b: Reference) =
        let compared = compare (rank a.Origin) (rank b.Origin)
        if compared <> 0 then compared else StringComparer.Ordinal.Compare(a.Id, b.Id)
    let private validateReference (source: Reference) =
        text 128 "$.source.id" source.Id
        hash "$.source.manifestSha256" source.ManifestSha256
        match source.Locator with
        | LocalDirectory path -> absolute "$.source.locator.path" path
        | HttpsEnvelope(uri, revision, digest) ->
            uriShape uri |> ignore
            text 128 "$.source.locator.revision" revision
            hash "$.source.locator.envelopeSha256" digest

    let validateSet (value: SourceSet) = errors (fun () ->
        check (value.Schema = "fsgg.provider-source-set/1") "source.schema" "$.schema" "Unsupported source-set schema."
        check (value.Sources.Length > 0 && value.Sources.Length <= limits.MaximumSources) "source.bound" "$.sources" "Expected one to16 sources."
        value.Sources |> List.iter validateReference
        unique StringComparer.Ordinal "source.duplicateId" "$.sources" (value.Sources |> List.map _.Id))

    let validateManifest (value: Manifest) = errors (fun () ->
        check (value.Schema = "fsgg.provider-source/1") "source.schema" "$.schema" "Unsupported source manifest schema."
        text 128 "$.id" value.Id; text 128 "$.revision" value.Revision
        portable "$.catalog" value.CatalogPath
        check (value.Members.Length > 1 && value.Members.Length <= limits.MaximumMembers) "source.bound" "$.members" "Expected a bounded catalog and archive population."
        let paths = value.Members |> List.map _.Path
        unique StringComparer.OrdinalIgnoreCase "source.memberCollision" "$.members" paths
        let mutable bytes = 0L
        for memberValue in value.Members do
            portable "$.members.path" memberValue.Path
            check (not (String.Equals(memberValue.Path, "source.json", StringComparison.OrdinalIgnoreCase))) "source.manifestSelfMember" "$.members" "The externally pinned manifest is not its own payload."
            hash "$.members.sha256" memberValue.Sha256
            check (memberValue.Bytes >= 0L && memberValue.Bytes <= limits.MaximumMemberBytes) "source.bound" "$.members.bytes" "Member exceeds the byte ceiling."
            bytes <- Checked.(+) bytes memberValue.Bytes
        check (bytes <= limits.MaximumExpandedBytes) "source.bound" "$.members" "Expanded source population exceeds its ceiling."
        unique StringComparer.Ordinal "source.archiveCollision" "$.archives" (value.Archives |> List.map _.TemplateSource)
        unique StringComparer.OrdinalIgnoreCase "source.archiveCollision" "$.archives" (value.Archives |> List.map _.Path)
        for archive in value.Archives do
            text 4096 "$.archives.templateSource" archive.TemplateSource
            portable "$.archives.path" archive.Path
            check (archive.Path <> value.CatalogPath) "source.roleCollision" "$.archives.path" "A member cannot be both catalog and archive."
        check (Set.ofList paths = Set.ofList (value.CatalogPath :: (value.Archives |> List.map _.Path)))
            "source.population" "$.members" "Member population must exactly match the catalog and uniquely associated archives."
        let catalog = value.Members |> List.find (fun memberValue -> memberValue.Path = value.CatalogPath)
        check (catalog.Bytes <= int64 limits.MaximumCatalogBytes) "source.bound" "$.catalog" "Catalog exceeds its byte ceiling.")

    let validatePolicy (value: Policy) = errors (fun () ->
        check (value.Schema = "fsgg.provider-source-policy/1") "source.schema" "$.schema" "Unsupported acquisition policy schema."
        check (value.AllowedOrigins.Length <= 16) "source.bound" "$.allowedOrigins" "At most16 origins are supported."
        for authority in value.AllowedOrigins do
            let uri = uriShape authority
            check (authority = origin uri && uri.Query = "" && uri.AbsolutePath = "/") "source.origin" "$.allowedOrigins" "Expected a canonical HTTPS authority only."
        unique StringComparer.Ordinal "source.originCollision" "$.allowedOrigins" value.AllowedOrigins
        check (value.MaximumRedirects >= 0 && value.MaximumRedirects <= 3) "source.bound" "$.maximumRedirects" "Redirect ceiling is0..3."
        check (value.AcquisitionSeconds > limits.CleanupSeconds && value.AcquisitionSeconds <= limits.MaximumAcquisitionSeconds) "source.bound" "$.acquisitionSeconds" "The original16..180-second window includes15seconds retirement."
        check (value.MaximumTransferBytes > 0L && value.MaximumTransferBytes <= limits.MaximumEnvelopeBytes) "source.bound" "$.maximumTransferBytes" "Transfer bound exceeds the source profile."
        check (value.MaximumExpandedBytes > 0L && value.MaximumExpandedBytes <= limits.MaximumExpandedBytes) "source.bound" "$.maximumExpandedBytes" "Expanded bound exceeds the source profile.")

    let validateUri policy uri = protect (fun () ->
        requireValid (validatePolicy policy)
        let parsed = uriShape uri
        check (List.contains (origin parsed) policy.AllowedOrigins) "source.originDenied" "$.uri" "The selected policy does not admit this HTTPS origin."
        parsed)

    let isPublicAddress (address: IPAddress) =
        let bytes = address.GetAddressBytes()
        if address.IsIPv4MappedToIPv6 then false
        elif address.AddressFamily = AddressFamily.InterNetwork then
            let a, b, c = int bytes[0], int bytes[1], int bytes[2]
            not (a = 0 || a = 10 || a = 127 || a >= 224 || (a = 100 && b >= 64 && b <= 127)
                 || (a = 169 && b = 254) || (a = 172 && b >= 16 && b <= 31)
                 || (a = 192 && (b = 0 || b = 168 || (b = 88 && c = 99)))
                 || (a = 198 && (b = 18 || b = 19 || (b = 51 && c = 100)))
                 || (a = 203 && b = 0 && c = 113))
        elif address.AddressFamily = AddressFamily.InterNetworkV6 then
            bytes[0] &&& 0xe0uy = 0x20uy
            && not (bytes[0] = 0x20uy && bytes[1] = 0x01uy && (bytes[2] <= 1uy || (bytes[2] = 0x0duy && bytes[3] = 0xb8uy)))
            && not (bytes[0] = 0x20uy && bytes[1] = 0x02uy)
            && not (bytes[0] = 0x3fuy && bytes[1] = 0xffuy && bytes[2] < 0x10uy)
        else false

    let validateRedirect policy original followed target = protect (fun () ->
        requireValid (validatePolicy policy)
        check (followed >= 0 && followed < policy.MaximumRedirects) "source.redirectLimit" "$.redirects" "Original redirect budget exhausted."
        let uri = uriShape target
        check (origin uri = origin original && List.contains (origin uri) policy.AllowedOrigins) "source.redirectOrigin" "$.redirects" "Redirect must retain the exact admitted HTTPS origin."
        uri)

    let validateObservation (value: Observation) = errors (fun () ->
        check (value.Schema = "fsgg.provider-source-observation/1") "source.schema" "$.schema" "Unsupported source observation schema."
        validateReference value.Source
        for name, digest in [ "sourceSetSha256", value.SourceSetSha256; "acquisitionPolicySha256", value.AcquisitionPolicySha256; "catalogSha256", value.CatalogSha256; "archiveSha256", value.ArchiveSha256 ] do hash ("$." + name) digest
        text 128 "$.revision" value.Revision
        text 4096 "$.templateSource" value.TemplateSource
        match value.Source.Locator with
        | HttpsEnvelope(_, revision, _) -> check (revision = value.Revision) "source.revision" "$.revision" "Observed and selected revision differ."
        | _ -> ()
        match value.Acquisition with
        | LocalCopy | CacheRead ->
            check (value.ResolvedUri.IsNone && value.Redirects.IsEmpty && value.NetworkHops.IsEmpty) "source.networkObservation" "$.networkHops" "Local/cache reads cannot claim fresh network observations."
            if value.Acquisition = LocalCopy then
                match value.Source.Locator with LocalDirectory _ -> () | _ -> refuse "source.acquisition" "$.acquisition" "A local copy requires a local source."
        | HttpsFetch ->
            let initial = match value.Source.Locator with HttpsEnvelope(uri, _, _) -> uri | _ -> refuse "source.acquisition" "$.acquisition" "HTTPS observations require an HTTPS source."
            check (value.NetworkHops.Length > 0 && value.NetworkHops.Length <= 4 && value.Redirects.Length = value.NetworkHops.Length - 1) "source.networkObservation" "$.networkHops" "Expected the complete bounded request/hop sequence."
            let uris = initial :: value.Redirects
            for index, hop in List.indexed value.NetworkHops do
                let uri = uriShape hop.Uri
                check (hop.Uri = uris[index] && origin uri = origin (uriShape initial)) "source.networkObservation" "$.networkHops.uri" "Observed hop differs from the selected sequence or origin."
                let mutable address = Unchecked.defaultof<IPAddress>
                check (IPAddress.TryParse(hop.Address, &address) && isPublicAddress address) "source.address" "$.networkHops.address" "Observed endpoint is outside the public address profile."
                let status = if index = value.NetworkHops.Length - 1 then hop.StatusCode = 200 else List.contains hop.StatusCode [ 301; 302; 303; 307; 308 ]
                check status "source.networkObservation" "$.networkHops.statusCode" "Only admitted redirects followed by a final200 are supported."
            check (value.ResolvedUri = Some(List.last uris)) "source.networkObservation" "$.resolvedUri" "Resolved URI must equal the observed final response.")

    let verifyMembers manifest (members: (string * byte array) list) = protect (fun () ->
        requireValid (validateManifest manifest)
        unique StringComparer.OrdinalIgnoreCase "source.memberCollision" "$.members" (members |> List.map fst)
        check (Set.ofList (members |> List.map fst) = Set.ofList (manifest.Members |> List.map _.Path)) "source.population" "$.members" "Actual and declared member populations differ."
        let actual = Map.ofList members
        for memberValue in manifest.Members do
            let bytes = actual[memberValue.Path]
            check (int64 bytes.LongLength = memberValue.Bytes && sha256 bytes = memberValue.Sha256) "source.memberDigestMismatch" "$.members" "Actual member bytes or length differ from their declaration.")

    let orderCatalogs (sources: (Reference * Fsgg.ProviderCatalog.Catalog) list) = protect (fun () ->
        requireValid (validateSet { Schema = "fsgg.provider-source-set/1"; Sources = List.map fst sources })
        unique StringComparer.Ordinal "source.providerCollision" "$.providers" (sources |> List.collect (snd >> _.Providers >> List.map _.Id))
        sources |> List.sortWith (fun (a, _) (b, _) -> compareReferences a b))

    let cacheKey source = protect (fun () ->
        validateReference source
        let envelope = match source.Locator with LocalDirectory _ -> "local" | HttpsEnvelope(_, _, digest) -> digest
        sha256 (utf8.GetBytes("fsgg.provider-source/1\n" + source.ManifestSha256 + "\n" + envelope)) |> fun value -> value[7..])

    let private closed names (value: JsonElement) =
        check (value.ValueKind = JsonValueKind.Object) "source.shape" "$" "Expected an object."
        let actual = value.EnumerateObject() |> Seq.map _.Name |> Seq.toList
        unique StringComparer.Ordinal "source.duplicateProperty" "$" actual
        check (Set.ofList actual = Set.ofList names) "source.properties" "$" "Object requires exactly the documented properties."
    let private get (name: string) (value: JsonElement) =
        let mutable property = Unchecked.defaultof<JsonElement>
        check (value.TryGetProperty(name, &property)) "source.properties" ("$." + name) "Missing required property."
        property
    let private readString (value: JsonElement) =
        check (value.ValueKind = JsonValueKind.String) "source.shape" "$" "Expected a string."
        value.GetString() |> nonNull
    let private readInt (value: JsonElement) =
        let mutable number = 0L
        check (value.ValueKind = JsonValueKind.Number && value.TryGetInt64(&number)
               && number >= 0L && value.GetRawText() = number.ToString(CultureInfo.InvariantCulture))
            "source.integer" "$" "Expected a canonical nonnegative integer."
        number
    let private readSmall value =
        let number = readInt value
        check (number <= int64 Int32.MaxValue) "source.integer" "$" "Integer exceeds the supported bound."
        int number
    let private readList maximum read (value: JsonElement) =
        check (value.ValueKind = JsonValueKind.Array && value.GetArrayLength() <= maximum) "source.bound" "$" "Expected a bounded array."
        value.EnumerateArray() |> Seq.map read |> Seq.toList
    let private reference (value: JsonElement) : Reference =
        closed [ "id"; "origin"; "locator"; "manifestSha256" ] value
        let locator = get "locator" value
        let location =
            match get "kind" locator |> readString with
            | "local-directory" ->
                closed [ "kind"; "path" ] locator
                LocalDirectory(get "path" locator |> readString)
            | "https-envelope" ->
                closed [ "kind"; "uri"; "revision"; "envelopeSha256" ] locator
                HttpsEnvelope(get "uri" locator |> readString, get "revision" locator |> readString, get "envelopeSha256" locator |> readString)
            | _ -> refuse "source.locator" "$.locator.kind" "Unsupported source locator."
        { Id = get "id" value |> readString
          Origin = match get "origin" value |> readString with
                   | "built-in" -> BuiltIn | "workspace" -> Workspace | "explicit" -> Explicit
                   | _ -> refuse "source.origin" "$.origin" "Unsupported source origin."
          Locator = location
          ManifestSha256 = get "manifestSha256" value |> readString }
    let private parseBytes maximum expected read validate (bytes: byte array) = protect (fun () ->
        check (not (obj.ReferenceEquals(bytes, null)) && bytes.Length <= maximum) "source.bound" "$" "JSON is absent or exceeds the byte ceiling."
        check (sha256 bytes = expected) "source.rawDigestMismatch" "$" "Selected raw digest does not match the actual bytes."
        check (not (bytes.Length >= 3 && bytes[0..2] = [| 0xefuy; 0xbbuy; 0xbfuy |])) "source.encoding" "$" "UTF-8 BOM is not supported."
        let json = utf8.GetString bytes
        use document = JsonDocument.Parse(json, JsonDocumentOptions(MaxDepth = 16, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow))
        let value = read document.RootElement
        requireValid (validate value)
        value)

    let parseSet expectedSha256 bytes =
        parseBytes limits.MaximumSetBytes expectedSha256 (fun value ->
            closed [ "schema"; "sources" ] value
            { Schema = get "schema" value |> readString; Sources = get "sources" value |> readList limits.MaximumSources reference }) validateSet bytes
    let parseManifest expectedSha256 bytes =
        parseBytes limits.MaximumManifestBytes expectedSha256 (fun value ->
            closed [ "schema"; "id"; "revision"; "catalog"; "members"; "archives" ] value
            { Schema = get "schema" value |> readString; Id = get "id" value |> readString
              Revision = get "revision" value |> readString; CatalogPath = get "catalog" value |> readString
              Members = get "members" value |> readList limits.MaximumMembers (fun item ->
                  closed [ "path"; "bytes"; "sha256" ] item
                  { Path = get "path" item |> readString; Bytes = get "bytes" item |> readInt; Sha256 = get "sha256" item |> readString })
              Archives = get "archives" value |> readList limits.MaximumMembers (fun item ->
                  closed [ "templateSource"; "path" ] item
                  { TemplateSource = get "templateSource" item |> readString; Path = get "path" item |> readString }) }) validateManifest bytes
    let parsePolicy expectedSha256 bytes =
        parseBytes limits.MaximumPolicyBytes expectedSha256 (fun value ->
            closed [ "schema"; "allowedOrigins"; "maximumRedirects"; "acquisitionSeconds"; "maximumTransferBytes"; "maximumExpandedBytes" ] value
            { Schema = get "schema" value |> readString; AllowedOrigins = get "allowedOrigins" value |> readList 16 readString
              MaximumRedirects = get "maximumRedirects" value |> readSmall; AcquisitionSeconds = get "acquisitionSeconds" value |> readSmall
              MaximumTransferBytes = get "maximumTransferBytes" value |> readInt; MaximumExpandedBytes = get "maximumExpandedBytes" value |> readInt }) validatePolicy bytes
    let parseObservation (text: string) =
        match protect (fun () ->
            check (not (obj.ReferenceEquals(text, null)) && text.Length <= limits.MaximumReceiptBytes) "source.bound" "$" "Observation text is absent or exceeds its ceiling."
            utf8.GetBytes(text: string)) with
        | Error diagnostics -> Error diagnostics
        | Ok bytes ->
            parseBytes limits.MaximumReceiptBytes (sha256 bytes) (fun value ->
                closed [ "schema"; "sourceSetSha256"; "acquisitionPolicySha256"; "source"; "revision"; "acquisition"; "resolvedUri"; "redirects"; "networkHops"; "catalogSha256"; "templateSource"; "archiveSha256" ] value
                let resolved = get "resolvedUri" value
                { Schema = get "schema" value |> readString; SourceSetSha256 = get "sourceSetSha256" value |> readString
                  AcquisitionPolicySha256 = get "acquisitionPolicySha256" value |> readString
                  Source = get "source" value |> reference; Revision = get "revision" value |> readString
                  Acquisition = match get "acquisition" value |> readString with
                                | "local-copy" -> LocalCopy | "https-fetch" -> HttpsFetch | "cache-read" -> CacheRead
                                | _ -> refuse "source.acquisition" "$.acquisition" "Unsupported acquisition observation."
                  ResolvedUri = if resolved.ValueKind = JsonValueKind.Null then None else Some(readString resolved)
                  Redirects = get "redirects" value |> readList 3 readString
                  NetworkHops = get "networkHops" value |> readList 4 (fun item ->
                      closed [ "uri"; "address"; "statusCode" ] item
                      { Uri = get "uri" item |> readString; Address = get "address" item |> readString; StatusCode = get "statusCode" item |> readSmall })
                  CatalogSha256 = get "catalogSha256" value |> readString; TemplateSource = get "templateSource" value |> readString
                  ArchiveSha256 = get "archiveSha256" value |> readString }) validateObservation bytes

    let private writeString (writer: Utf8JsonWriter) (name: string) (value: string) = writer.WriteString(name, value)
    let private writeStrings (writer: Utf8JsonWriter) name (values: string list) =
        writer.WriteStartArray(name: string)
        values |> List.iter (fun value -> writer.WriteStringValue value)
        writer.WriteEndArray()
    let private writeReference (writer: Utf8JsonWriter) (source: Reference) =
        writer.WriteStartObject()
        writeString writer "id" source.Id
        writeString writer "origin" (match source.Origin with BuiltIn -> "built-in" | Workspace -> "workspace" | Explicit -> "explicit")
        writer.WriteStartObject "locator"
        match source.Locator with
        | LocalDirectory path -> writeString writer "kind" "local-directory"; writeString writer "path" path
        | HttpsEnvelope(uri, revision, digest) ->
            writeString writer "kind" "https-envelope"; writeString writer "uri" uri
            writeString writer "revision" revision; writeString writer "envelopeSha256" digest
        writer.WriteEndObject()
        writeString writer "manifestSha256" source.ManifestSha256
        writer.WriteEndObject()
    let private encode maximum validate value (write: Utf8JsonWriter -> unit) = protect (fun () ->
        requireValid (validate value)
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        write writer
        writer.Flush()
        check (stream.Length <= int64 maximum) "source.bound" "$" "Serialized JSON exceeds its byte ceiling."
        utf8.GetString(stream.ToArray()))
    let serializeSet value = encode limits.MaximumSetBytes validateSet value (fun writer ->
        writer.WriteStartObject(); writeString writer "schema" value.Schema; writer.WriteStartArray "sources"
        value.Sources |> List.sortWith compareReferences |> List.iter (writeReference writer)
        writer.WriteEndArray(); writer.WriteEndObject())
    let serializeManifest value = encode limits.MaximumManifestBytes validateManifest value (fun writer ->
        writer.WriteStartObject(); writeString writer "schema" value.Schema; writeString writer "id" value.Id
        writeString writer "revision" value.Revision; writeString writer "catalog" value.CatalogPath
        writer.WriteStartArray "members"
        for memberValue in value.Members |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a.Path, b.Path)) do
            writer.WriteStartObject(); writeString writer "path" memberValue.Path; writer.WriteNumber("bytes", memberValue.Bytes)
            writeString writer "sha256" memberValue.Sha256; writer.WriteEndObject()
        writer.WriteEndArray(); writer.WriteStartArray "archives"
        for archive in value.Archives |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a.TemplateSource, b.TemplateSource)) do
            writer.WriteStartObject(); writeString writer "templateSource" archive.TemplateSource
            writeString writer "path" archive.Path; writer.WriteEndObject()
        writer.WriteEndArray(); writer.WriteEndObject())
    let serializePolicy value = encode limits.MaximumPolicyBytes validatePolicy value (fun writer ->
        writer.WriteStartObject(); writeString writer "schema" value.Schema
        writeStrings writer "allowedOrigins" (value.AllowedOrigins |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a, b)))
        writer.WriteNumber("maximumRedirects", value.MaximumRedirects); writer.WriteNumber("acquisitionSeconds", value.AcquisitionSeconds)
        writer.WriteNumber("maximumTransferBytes", value.MaximumTransferBytes); writer.WriteNumber("maximumExpandedBytes", value.MaximumExpandedBytes)
        writer.WriteEndObject())
    let serializeObservation value = encode limits.MaximumReceiptBytes validateObservation value (fun writer ->
        writer.WriteStartObject(); writeString writer "schema" value.Schema
        writeString writer "sourceSetSha256" value.SourceSetSha256; writeString writer "acquisitionPolicySha256" value.AcquisitionPolicySha256
        writer.WritePropertyName "source"; writeReference writer value.Source
        writeString writer "revision" value.Revision
        writeString writer "acquisition" (match value.Acquisition with LocalCopy -> "local-copy" | HttpsFetch -> "https-fetch" | CacheRead -> "cache-read")
        match value.ResolvedUri with None -> writer.WriteNull "resolvedUri" | Some uri -> writeString writer "resolvedUri" uri
        writeStrings writer "redirects" value.Redirects; writer.WriteStartArray "networkHops"
        for hop in value.NetworkHops do
            writer.WriteStartObject(); writeString writer "uri" hop.Uri; writeString writer "address" hop.Address
            writer.WriteNumber("statusCode", hop.StatusCode); writer.WriteEndObject()
        writer.WriteEndArray(); writeString writer "catalogSha256" value.CatalogSha256
        writeString writer "templateSource" value.TemplateSource; writeString writer "archiveSha256" value.ArchiveSha256
        writer.WriteEndObject())

