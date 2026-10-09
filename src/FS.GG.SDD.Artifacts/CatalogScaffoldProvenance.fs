namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open Fsgg.ProviderCatalog
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.ScaffoldProvenance

module CatalogScaffoldProvenance =
    type Identity =
        {
            Id: string
            Version: string
            Digest: string
        }

    type ArchiveIdentity =
        {
            Id: string
            Version: string
            Digest: string
        }

    type EvidenceFormat = { Id: string; Version: string }

    type EvidenceMapping =
        {
            Format: string
            Id: string
            Version: string
        }

    type AdmissionRequest =
        {
            ContractVersion: string
            Platform: string
            RequiredCapabilityIds: string list
            SemanticOnlyCapabilityIds: string list
            SupportedPlatforms: string list
            SupportedEnvironmentIds: string list
            SupportedFormats: EvidenceFormat list
            EvidenceMappings: EvidenceMapping list
            Descriptor: Fsgg.ProviderCatalog.Descriptor
        }

    type Budgets =
        {
            RequestedPreflightSeconds: int
            RequestedScaffoldSeconds: int
            AdmittedPreflightSeconds: int
            AdmittedScaffoldSeconds: int
        }

    type Declaration =
        {
            RawCatalogDigest: string
            CatalogId: string
            CatalogRevision: string
            CatalogDigest: string
            Descriptor: Fsgg.ProviderCatalog.Descriptor
            EffectiveParameters: (string * string) list
            RawProductName: string
            PackageIdentity: string
            CodeIdentifier: string
            Archive: ArchiveIdentity
            Admission: AdmissionRequest
            Policy: Identity
            EvidenceMap: Identity
            Budgets: Budgets
        }

    type ObservedTool =
        {
            Id: string
            Version: string
            Executable: string
        }

    type TransportIdentity = { Executable: string; Version: string }
    type RootPath = { Role: string; Path: string }

    type Invocation =
        {
            Executable: string
            Arguments: string list
            WorkingRoot: string
            WorkingDirectory: string
            EnvironmentRoots: (string * RootPath) list
            TimeoutSeconds: int
            ExitCode: int
        }

    type Observation =
        {
            Platform: string
            Tools: ObservedTool list
            ConsumedArchiveDigest: string
            Transport: TransportIdentity
            Invocations: Invocation list
            Result: string
            ProducedPaths: FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProducedPath list
        }

    type CatalogScaffoldProvenanceRecord =
        {
            SchemaVersion: int
            Generator: FS.GG.SDD.Artifacts.SchemaVersion.GeneratorVersion
            Declaration: Declaration
            Observation: Observation
            Ownership: FS.GG.SDD.Artifacts.ScaffoldProvenance.ScaffoldProvenanceRecord
        }

    exception Malformed of string * string
    let bad path message = raise (Malformed(path, message))

    let obj path allowed (e: JsonElement) =
        if e.ValueKind <> JsonValueKind.Object then
            bad path "Expected object."

        let names = e.EnumerateObject() |> Seq.map (fun p -> p.Name) |> Seq.toList

        if names.Length <> (List.distinct names).Length then
            bad path "Duplicate property."

        for name in names do
            if not (List.contains name allowed) then
                bad (path + "." + name) "Unknown property."

        for name in allowed do
            if not (List.contains name names) then
                bad (path + "." + name) "Missing required property."

    let get path (name: string) (e: JsonElement) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if not (e.TryGetProperty(name, &value)) then
            bad (path + "." + name) "Missing required property."

        value

    let text path (e: JsonElement) =
        if e.ValueKind <> JsonValueKind.String then
            bad path "Expected string."

        e.GetString()
        |> Option.ofObj
        |> Option.defaultWith (fun () -> bad path "Expected string.")

    let str path name e =
        text (path + "." + name) (get path name e)

    let number path name e =
        let v = get path name e
        let mutable n = 0

        if
            v.ValueKind <> JsonValueKind.Number
            || not (v.TryGetInt32(&n))
            || v.GetRawText() <> string n
        then
            bad (path + "." + name) "Expected canonical integer."

        n

    let array path name e =
        let value = get path name e

        if value.ValueKind <> JsonValueKind.Array then
            bad (path + "." + name) "Expected array."

        value.EnumerateArray() |> Seq.toList

    let strings path name e =
        array path name e |> List.mapi (fun i v -> text ($"{path}.{name}[{i}]") v)

    let nonblank path value =
        if String.IsNullOrWhiteSpace value then
            bad path "Expected nonblank value."

    let hash path (value: string) =
        if
            value.Length <> 71
            || not (value.StartsWith("sha256:", StringComparison.Ordinal))
            || (value.Substring(7)
                |> Seq.exists (fun c -> not ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))))
        then
            bad path "Expected sha256: followed by 64 lowercase hex digits."

    let unique path (values: 'a list) =
        if values.Length <> (List.distinct values).Length then
            bad path "Duplicate identity."

    let relative path allowRoot (value: string) =
        if not (allowRoot && (value = "." || value = "")) then
            if
                String.IsNullOrWhiteSpace value
                || value.Contains '\\'
                || value.Contains ':'
                || value.StartsWith("/", StringComparison.Ordinal)
                || (value.Split('/') |> Array.exists (fun s -> s = "" || s = "." || s = ".."))
                || (value |> Seq.exists Char.IsControl)
            then
                bad path "Expected contained relative path."

    let parseIdentity path e : Identity =
        obj path [ "id"; "version"; "digest" ] e

        {
            Id = str path "id" e
            Version = str path "version" e
            Digest = str path "digest" e
        }

    let parseArchive path e : ArchiveIdentity =
        let value = parseIdentity path e

        {
            Id = value.Id
            Version = value.Version
            Digest = value.Digest
        }

    let parseDescriptor path (e: JsonElement) =
        // JSON is a YAML subset. Null defaults are a canonical projection detail, whereas
        // the legacy authored YAML parser treats an absent default as the optional case.
        if e.ValueKind <> JsonValueKind.Object then
            bad path "Expected descriptor object."

        let node = JsonNode.Parse(e.GetRawText()) |> nonNull
        let parameterElements = array path "parameters" e

        for i, parameter in List.indexed parameterElements do
            if parameter.ValueKind <> JsonValueKind.Object then
                bad ($"{path}.parameters[{i}]") "Expected parameter object."

        let parameters = (node["parameters"] |> nonNull).AsArray()

        for parameter in parameters do
            let p = nonNull parameter

            if p.AsObject().ContainsKey("default") && isNull p["default"] then
                p.AsObject().Remove("default") |> ignore

        let root = JsonObject()
        root["schemaVersion"] <- JsonValue.Create 2
        root["id"] <- JsonValue.Create "provenance-catalog"
        root["revision"] <- JsonValue.Create "1"
        root["digest"] <- JsonValue.Create("sha256:" + String.replicate 64 "0")
        let providers = JsonArray()
        providers.Add node
        root["providers"] <- providers

        match ProviderCatalog.parse (root.ToJsonString()) with
        | Ok c -> c.Providers.Head
        | Error errors -> bad path (errors |> List.map (fun d -> d.Path + ": " + d.Message) |> String.concat "; ")

    let parseFormat path e : EvidenceFormat =
        obj path [ "id"; "version" ] e

        {
            Id = str path "id" e
            Version = str path "version" e
        }

    let parseMapping path e : EvidenceMapping =
        obj path [ "format"; "id"; "version" ] e

        {
            Format = str path "format" e
            Id = str path "id" e
            Version = str path "version" e
        }

    let parseAdmission path e : AdmissionRequest =
        obj
            path
            [
                "contractVersion"
                "platform"
                "requiredCapabilityIds"
                "semanticOnlyCapabilityIds"
                "supportedPlatforms"
                "supportedEnvironmentIds"
                "supportedFormats"
                "evidenceMappings"
                "descriptor"
            ]
            e

        {
            ContractVersion = str path "contractVersion" e
            Platform = str path "platform" e
            RequiredCapabilityIds = strings path "requiredCapabilityIds" e
            SemanticOnlyCapabilityIds = strings path "semanticOnlyCapabilityIds" e
            SupportedPlatforms = strings path "supportedPlatforms" e
            SupportedEnvironmentIds = strings path "supportedEnvironmentIds" e
            SupportedFormats =
                array path "supportedFormats" e
                |> List.mapi (fun i v -> parseFormat ($"{path}.supportedFormats[{i}]") v)
            EvidenceMappings =
                array path "evidenceMappings" e
                |> List.mapi (fun i v -> parseMapping ($"{path}.evidenceMappings[{i}]") v)
            Descriptor = parseDescriptor (path + ".descriptor") (get path "descriptor" e)
        }

    let parseParameters path e =
        array path "effectiveParameters" e
        |> List.mapi (fun i value ->
            let pp = $"{path}.effectiveParameters[{i}]"
            obj pp [ "key"; "value" ] value
            str pp "key" value, str pp "value" value)

    let parseDeclaration path e : Declaration =
        obj
            path
            [
                "rawCatalogDigest"
                "catalogId"
                "catalogRevision"
                "catalogDigest"
                "descriptor"
                "effectiveParameters"
                "rawProductName"
                "packageIdentity"
                "codeIdentifier"
                "archive"
                "admission"
                "policy"
                "evidenceMap"
                "budgets"
            ]
            e

        let bp = path + ".budgets"
        let b = get path "budgets" e

        obj
            bp
            [
                "requestedPreflightSeconds"
                "requestedScaffoldSeconds"
                "admittedPreflightSeconds"
                "admittedScaffoldSeconds"
            ]
            b

        {
            RawCatalogDigest = str path "rawCatalogDigest" e
            CatalogId = str path "catalogId" e
            CatalogRevision = str path "catalogRevision" e
            CatalogDigest = str path "catalogDigest" e
            Descriptor = parseDescriptor (path + ".descriptor") (get path "descriptor" e)
            EffectiveParameters = parseParameters path e
            RawProductName = str path "rawProductName" e
            PackageIdentity = str path "packageIdentity" e
            CodeIdentifier = str path "codeIdentifier" e
            Archive = parseArchive (path + ".archive") (get path "archive" e)
            Admission = parseAdmission (path + ".admission") (get path "admission" e)
            Policy = parseIdentity (path + ".policy") (get path "policy" e)
            EvidenceMap = parseIdentity (path + ".evidenceMap") (get path "evidenceMap" e)
            Budgets =
                {
                    RequestedPreflightSeconds = number bp "requestedPreflightSeconds" b
                    RequestedScaffoldSeconds = number bp "requestedScaffoldSeconds" b
                    AdmittedPreflightSeconds = number bp "admittedPreflightSeconds" b
                    AdmittedScaffoldSeconds = number bp "admittedScaffoldSeconds" b
                }
        }

    let parsePaths path name e =
        array path name e
        |> List.mapi (fun i v ->
            let pp = $"{path}.{name}[{i}]"
            obj pp [ "path"; "owner"; "sha256" ] v
            let owner = str pp "owner" v

            if
                not (
                    List.contains
                        owner
                        [
                            "sdd"
                            "governance"
                            "rendering"
                            "generatedProduct"
                            "mirrored"
                            "driver"
                            "gameSkill"
                            "renderingSkill"
                            "audioSkill"
                        ]
                )
            then
                bad (pp + ".owner") "Unknown ownership category."

            let actualOwner =
                match owner with
                | "sdd" -> Sdd
                | "governance" -> Governance
                | "rendering" -> Rendering
                | "mirrored" -> Mirrored
                | "driver" -> Driver
                | "gameSkill" -> GameSkill
                | "renderingSkill" -> RenderingSkill
                | "audioSkill" -> AudioSkill
                | _ -> GeneratedProduct

            {
                Path = str pp "path" v
                Owner = actualOwner
                Sha256 = Some(str pp "sha256" v)
            })

    let parseOwnership path e =
        obj
            path
            [
                "schemaVersion"
                "generator"
                "requiredMinimumCliVersion"
                "providerName"
                "providerContractVersion"
                "templateRef"
                "outcome"
                "producedPaths"
                "mirroredPaths"
                "sddOwnedPaths"
                "driverPaths"
                "gameSkillPaths"
                "renderingSkillPaths"
                "effectiveParameters"
            ]
            e

        let minimum = get path "requiredMinimumCliVersion" e

        if
            minimum.ValueKind <> JsonValueKind.Null
            && minimum.ValueKind <> JsonValueKind.String
        then
            bad (path + ".requiredMinimumCliVersion") "Expected string or null."

        let generator = get path "generator" e
        obj (path + ".generator") [ "id"; "version" ] generator
        // The schema-2 ownership projection requires all path hashes. Legacy parsing remains
        // unchanged; the closed envelope validates categories before using that same projection.
        for name in
            [
                "producedPaths"
                "mirroredPaths"
                "sddOwnedPaths"
                "driverPaths"
                "gameSkillPaths"
                "renderingSkillPaths"
            ] do
            parsePaths path name e |> ignore

        parseParameters path e |> ignore

        match ScaffoldProvenance.tryParse (e.GetRawText()) with
        | Some ownership -> ownership
        | None -> bad path "Invalid legacy ownership projection."

    let parseTool path e : ObservedTool =
        obj path [ "id"; "version"; "executable" ] e

        {
            Id = str path "id" e
            Version = str path "version" e
            Executable = str path "executable" e
        }

    let parseInvocation path e : Invocation =
        obj
            path
            [
                "executable"
                "arguments"
                "workingRoot"
                "workingDirectory"
                "environmentRoots"
                "timeoutSeconds"
                "exitCode"
            ]
            e

        let roots =
            array path "environmentRoots" e
            |> List.mapi (fun i v ->
                let pp = $"{path}.environmentRoots[{i}]"
                obj pp [ "key"; "role"; "path" ] v

                str pp "key" v,
                {
                    Role = str pp "role" v
                    Path = str pp "path" v
                })

        {
            Executable = str path "executable" e
            Arguments = strings path "arguments" e
            WorkingRoot = str path "workingRoot" e
            WorkingDirectory = str path "workingDirectory" e
            EnvironmentRoots = roots
            TimeoutSeconds = number path "timeoutSeconds" e
            ExitCode = number path "exitCode" e
        }

    let parseObservation path e : Observation =
        obj
            path
            [
                "platform"
                "tools"
                "consumedArchiveDigest"
                "transport"
                "invocations"
                "result"
                "producedPaths"
            ]
            e

        let t = get path "transport" e
        obj (path + ".transport") [ "executable"; "version" ] t

        {
            Platform = str path "platform" e
            Tools =
                array path "tools" e
                |> List.mapi (fun i v -> parseTool ($"{path}.tools[{i}]") v)
            ConsumedArchiveDigest = str path "consumedArchiveDigest" e
            Transport =
                {
                    Executable = str (path + ".transport") "executable" t
                    Version = str (path + ".transport") "version" t
                }
            Invocations =
                array path "invocations" e
                |> List.mapi (fun i v -> parseInvocation ($"{path}.invocations[{i}]") v)
            Result = str path "result" e
            ProducedPaths = parsePaths path "producedPaths" e
        }

    let validate (r: CatalogScaffoldProvenanceRecord) =
        let d, o, own = r.Declaration, r.Observation, r.Ownership

        if r.SchemaVersion <> 2 then
            bad "$.schemaVersion" "Unsupported provenance schema."

        nonblank "$.generator.id" r.Generator.Id
        nonblank "$.generator.version" r.Generator.Version

        for path, value in
            [
                "rawCatalogDigest", d.RawCatalogDigest
                "catalogDigest", d.CatalogDigest
                "descriptor.descriptorDigest", d.Descriptor.DescriptorDigest
                "archive.digest", d.Archive.Digest
                "policy.digest", d.Policy.Digest
                "evidenceMap.digest", d.EvidenceMap.Digest
            ] do
            hash ("$.declaration." + path) value

        nonblank "$.declaration.catalogId" d.CatalogId
        nonblank "$.declaration.catalogRevision" d.CatalogRevision

        for path, value in
            [
                "archive.id", d.Archive.Id
                "archive.version", d.Archive.Version
                "policy.id", d.Policy.Id
                "policy.version", d.Policy.Version
                "evidenceMap.id", d.EvidenceMap.Id
                "evidenceMap.version", d.EvidenceMap.Version
            ] do
            nonblank ("$.declaration." + path) value

        if
            ProviderCatalogIntegrity.digest (ProviderCatalogIntegrity.descriptorBytes d.Descriptor)
            <> d.Descriptor.DescriptorDigest
        then
            bad "$.declaration.descriptor.descriptorDigest" "Semantic descriptor digest mismatch."

        let catalog: Catalog =
            {
                SchemaVersion = 2
                Id = d.CatalogId
                Revision = d.CatalogRevision
                Digest = d.CatalogDigest
                Providers = [ d.Descriptor ]
            }

        match Fsgg.ProviderCatalog.resolve catalog d.Descriptor.Id d.EffectiveParameters with
        | Error _ ->
            bad "$.declaration.effectiveParameters" "Effective inputs do not resolve through the declared descriptor."
        | Ok prepared ->
            if
                Map.ofList prepared.EffectiveParameters <> Map.ofList d.EffectiveParameters
                || prepared.RawProductName <> d.RawProductName
                || prepared.PackageIdentity <> d.PackageIdentity
                || prepared.CodeIdentifier <> d.CodeIdentifier
            then
                bad "$.declaration" "Resolved parameters and explicit identities do not correspond."

        unique "$.declaration.effectiveParameters" (List.map fst d.EffectiveParameters)
        let b = d.Budgets

        if
            min b.RequestedPreflightSeconds b.RequestedScaffoldSeconds <= 0
            || min b.AdmittedPreflightSeconds b.AdmittedScaffoldSeconds <= 0
            || b.RequestedPreflightSeconds > b.AdmittedPreflightSeconds
            || b.RequestedScaffoldSeconds > b.AdmittedScaffoldSeconds
        then
            bad "$.declaration.budgets" "Positive requested budgets must fit admitted ceilings."

        let a = d.Admission

        if a.ContractVersion <> "1.0.0" || a.Descriptor <> d.Descriptor then
            bad
                "$.declaration.admission"
                "Normalized request must bind the complete selected descriptor and neutral contract."

        if
            a.Platform <> o.Platform
            || not (List.contains a.Platform a.SupportedPlatforms)
            || not (List.contains a.Platform d.Descriptor.Platforms)
        then
            bad "$.observation.platform" "Observed platform does not correspond to the selected supported platform."

        for name, values in
            [
                "requiredCapabilityIds", a.RequiredCapabilityIds
                "semanticOnlyCapabilityIds", a.SemanticOnlyCapabilityIds
                "supportedPlatforms", a.SupportedPlatforms
                "supportedEnvironmentIds", a.SupportedEnvironmentIds
            ] do
            unique ("$.declaration.admission." + name) values
            values |> List.iter (nonblank ("$.declaration.admission." + name))

        unique "$.declaration.admission.supportedFormats" (a.SupportedFormats |> List.map (fun f -> f.Id, f.Version))
        unique "$.declaration.admission.evidenceMappings" (a.EvidenceMappings |> List.map (fun m -> m.Format))

        for m in a.EvidenceMappings do
            nonblank "$.declaration.admission.evidenceMappings.format" m.Format
            nonblank "$.declaration.admission.evidenceMappings.id" m.Id
            nonblank "$.declaration.admission.evidenceMappings.version" m.Version

        for f in a.SupportedFormats do
            nonblank "$.declaration.admission.supportedFormats.id" f.Id
            nonblank "$.declaration.admission.supportedFormats.version" f.Version

        for e in d.Descriptor.Evidence do
            match a.EvidenceMappings |> List.filter (fun m -> m.Format = e.Format) with
            | [ m ] when
                a.SupportedFormats
                |> List.exists (fun f -> f.Id = m.Id && f.Version = m.Version)
                ->
                ()
            | _ ->
                bad
                    "$.declaration.admission.evidenceMappings"
                    "Each declared format requires one exact independently supported mapping."

        if
            o.Result <> "succeeded"
            || o.Invocations.IsEmpty
            || List.exists (fun i -> i.ExitCode <> 0) o.Invocations
        then
            bad "$.observation" "Partial or unsuccessful observations cannot be success provenance."

        if o.ConsumedArchiveDigest <> d.Archive.Digest then
            bad "$.observation.consumedArchiveDigest" "Consumed archive digest mismatch."

        nonblank "$.observation.transport.executable" o.Transport.Executable
        nonblank "$.observation.transport.version" o.Transport.Version
        unique "$.observation.tools" (o.Tools |> List.map (fun t -> t.Id))

        let expected =
            d.Descriptor.Tools
            |> List.filter (fun t -> List.contains a.Platform t.Platforms)
            |> List.map (fun t -> t.Id, t.Version)
            |> Map.ofList

        if expected <> (o.Tools |> List.map (fun t -> t.Id, t.Version) |> Map.ofList) then
            bad "$.observation.tools" "All selected tool versions must be observed exactly."

        for t in o.Tools do
            nonblank "$.observation.tools.executable" t.Executable

        for i in o.Invocations do
            nonblank "$.observation.invocations.executable" i.Executable

            if i.Executable <> o.Transport.Executable then
                bad "$.observation.invocations.executable" "Invocation does not match the observed transport."

            // A cumulative observation has no invocation-phase tag. Structural bounds
            // cover either requested phase; the edge owns the original per-phase ends.
            if
                i.TimeoutSeconds <= 0
                || i.TimeoutSeconds > max b.RequestedPreflightSeconds b.RequestedScaffoldSeconds
            then
                bad
                    "$.observation.invocations.timeoutSeconds"
                    "Invocation timeout exceeds both requested phase budgets."

            if not (List.contains i.WorkingRoot [ "operation"; "staging" ]) then
                bad "$.observation.invocations.workingRoot" "Unknown root role."

            relative "$.observation.invocations.workingDirectory" true i.WorkingDirectory
            unique "$.observation.invocations.environmentRoots" (List.map fst i.EnvironmentRoots)

            for key, v in i.EnvironmentRoots do
                nonblank "$.observation.invocations.environmentRoots.key" key

                if not (List.contains v.Role [ "operation"; "staging" ]) then
                    bad "$.observation.invocations.environmentRoots.role" "Unknown root role."

                relative "$.observation.invocations.environmentRoots.path" true v.Path

        if
            own.SchemaVersion <> 1
            || own.Generator <> r.Generator
            || own.ProviderName <> d.Descriptor.Id
            || own.ProviderContractVersion <> d.Descriptor.ContractVersion
            || own.TemplateRef <> d.Descriptor.TemplateSource
            || own.Outcome <> "providerSucceeded"
            || Map.ofList own.EffectiveParameters <> Map.ofList d.EffectiveParameters
        then
            bad "$.ownership" "Ownership and declaration do not correspond."

        unique "$.ownership.effectiveParameters" (List.map fst own.EffectiveParameters)

        let categories =
            [
                own.ProducedPaths, GeneratedProduct
                own.MirroredPaths, Mirrored
                own.SddOwnedPaths, Sdd
                own.DriverPaths, Driver
                own.GameSkillPaths, GameSkill
                own.RenderingSkillPaths, RenderingSkill
            ]

        let all = categories |> List.collect fst
        unique "$.ownership" (all |> List.map (fun p -> p.Path))

        for paths, owner in categories do
            for p in paths do
                if p.Owner <> owner then
                    bad "$.ownership" "Path is in the wrong ownership category."

                relative "$.ownership.path" false p.Path

                match p.Sha256 with
                | Some value -> hash "$.ownership.sha256" value
                | None -> bad "$.ownership.sha256" "Success provenance requires every produced path hash."

        for p in all do
            if
                all
                |> List.exists (fun q -> q.Path.StartsWith(p.Path + "/", StringComparison.Ordinal))
            then
                bad "$.ownership" "Colliding file/directory paths."

        unique "$.observation.producedPaths" (o.ProducedPaths |> List.map (fun p -> p.Path))

        let pathMap (ps: ScaffoldProducedPath list) =
            ps |> List.map (fun p -> p.Path, (p.Owner, p.Sha256)) |> Map.ofList

        if pathMap o.ProducedPaths <> pathMap all then
            bad "$.observation.producedPaths" "Observed hashes must cover every ownership path exactly."

    let rec rejectDuplicates path (e: JsonElement) =
        match e.ValueKind with
        | JsonValueKind.Object ->
            let properties = e.EnumerateObject() |> Seq.toList
            unique path (properties |> List.map (fun p -> p.Name))

            properties
            |> List.iter (fun p -> rejectDuplicates (path + "." + p.Name) p.Value)
        | JsonValueKind.Array -> e.EnumerateArray() |> Seq.iteri (fun i v -> rejectDuplicates ($"{path}[{i}]") v)
        | _ -> ()

    let parse text =
        try
            use doc = JsonDocument.Parse(text: string)
            let e = doc.RootElement
            rejectDuplicates "$" e
            obj "$" [ "schemaVersion"; "generator"; "declaration"; "observation"; "ownership" ] e
            let g = get "$" "generator" e
            obj "$.generator" [ "id"; "version" ] g

            let r =
                {
                    SchemaVersion = number "$" "schemaVersion" e
                    Generator =
                        {
                            Id = str "$.generator" "id" g
                            Version = str "$.generator" "version" g
                        }
                    Declaration = parseDeclaration "$.declaration" (get "$" "declaration" e)
                    Observation = parseObservation "$.observation" (get "$" "observation" e)
                    Ownership = parseOwnership "$.ownership" (get "$" "ownership" e)
                }

            validate r
            Ok r
        with
        | Malformed(path, message) ->
            Error
                [
                    {
                        Code = "provenance.malformed"
                        Path = path
                        Message = message
                    }
                ]
        | :? JsonException ->
            Error
                [
                    {
                        Code = "provenance.malformed"
                        Path = "$"
                        Message = "Malformed provenance JSON."
                    }
                ]
        | :? InvalidOperationException ->
            Error
                [
                    {
                        Code = "provenance.malformed"
                        Path = "$"
                        Message = "Malformed provenance value."
                    }
                ]
        | :? ArgumentException ->
            Error
                [
                    {
                        Code = "provenance.malformed"
                        Path = "$"
                        Message = "Malformed provenance value."
                    }
                ]

    let writeText (w: Utf8JsonWriter) (name: string) (value: string) = w.WriteString(name, value)
    let writeNumber (w: Utf8JsonWriter) (name: string) (value: int) = w.WriteNumber(name, value)

    let sorted (key: 'a -> string) (values: 'a list) =
        values
        |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(key a, key b))

    let writeStrings (w: Utf8JsonWriter) name sortedSet values =
        w.WriteStartArray(name: string)

        (if sortedSet then sorted id values else values)
        |> List.iter (fun (v: string) -> w.WriteStringValue v)

        w.WriteEndArray()

    let writeObject (w: Utf8JsonWriter) name write =
        w.WriteStartObject(name: string)
        write ()
        w.WriteEndObject()

    let writeArray (w: Utf8JsonWriter) name values write =
        w.WriteStartArray(name: string)
        values |> List.iter write
        w.WriteEndArray()

    let writeIdentity w name id version digest =
        writeObject w name (fun () ->
            writeText w "id" id
            writeText w "version" version
            writeText w "digest" digest)

    let writeDescriptor (w: Utf8JsonWriter) name d =
        w.WritePropertyName(name: string)
        use doc = JsonDocument.Parse(ProviderCatalogIntegrity.descriptorJson d)
        doc.RootElement.WriteTo w

    let writePaths (w: Utf8JsonWriter) name paths =
        writeArray w name (sorted (fun (p: ScaffoldProducedPath) -> p.Path) paths) (fun p ->
            w.WriteStartObject()
            writeText w "path" p.Path
            writeText w "owner" (ArtifactRef.ownerValue p.Owner)
            writeText w "sha256" (p.Sha256 |> Option.defaultValue "")
            w.WriteEndObject())

    let serialize (record: CatalogScaffoldProvenanceRecord) =
        let r = record

        try
            validate r
            use stream = new MemoryStream()
            use w = new Utf8JsonWriter(stream)
            w.WriteStartObject()
            writeNumber w "schemaVersion" r.SchemaVersion

            writeObject w "generator" (fun () ->
                writeText w "id" r.Generator.Id
                writeText w "version" r.Generator.Version)

            let d, o = r.Declaration, r.Observation

            writeObject w "declaration" (fun () ->
                writeText w "rawCatalogDigest" d.RawCatalogDigest
                writeText w "catalogId" d.CatalogId
                writeText w "catalogRevision" d.CatalogRevision
                writeText w "catalogDigest" d.CatalogDigest
                writeDescriptor w "descriptor" d.Descriptor

                writeArray w "effectiveParameters" (sorted fst d.EffectiveParameters) (fun (key, value) ->
                    w.WriteStartObject()
                    writeText w "key" key
                    writeText w "value" value
                    w.WriteEndObject())

                writeText w "rawProductName" d.RawProductName
                writeText w "packageIdentity" d.PackageIdentity
                writeText w "codeIdentifier" d.CodeIdentifier
                writeIdentity w "archive" d.Archive.Id d.Archive.Version d.Archive.Digest
                let a = d.Admission

                writeObject w "admission" (fun () ->
                    writeText w "contractVersion" a.ContractVersion
                    writeText w "platform" a.Platform
                    writeStrings w "requiredCapabilityIds" true a.RequiredCapabilityIds
                    writeStrings w "semanticOnlyCapabilityIds" true a.SemanticOnlyCapabilityIds
                    writeStrings w "supportedPlatforms" true a.SupportedPlatforms
                    writeStrings w "supportedEnvironmentIds" true a.SupportedEnvironmentIds

                    writeArray
                        w
                        "supportedFormats"
                        (a.SupportedFormats
                         |> List.sortWith (fun x y ->
                             let c = StringComparer.Ordinal.Compare(x.Id, y.Id) in

                             if c = 0 then
                                 StringComparer.Ordinal.Compare(x.Version, y.Version)
                             else
                                 c))
                        (fun f ->
                            w.WriteStartObject()
                            writeText w "id" f.Id
                            writeText w "version" f.Version
                            w.WriteEndObject())

                    writeArray
                        w
                        "evidenceMappings"
                        (sorted (fun (m: EvidenceMapping) -> m.Format) a.EvidenceMappings)
                        (fun m ->
                            w.WriteStartObject()
                            writeText w "format" m.Format
                            writeText w "id" m.Id
                            writeText w "version" m.Version
                            w.WriteEndObject())

                    writeDescriptor w "descriptor" a.Descriptor)

                writeIdentity w "policy" d.Policy.Id d.Policy.Version d.Policy.Digest
                writeIdentity w "evidenceMap" d.EvidenceMap.Id d.EvidenceMap.Version d.EvidenceMap.Digest

                writeObject w "budgets" (fun () ->
                    writeNumber w "requestedPreflightSeconds" d.Budgets.RequestedPreflightSeconds
                    writeNumber w "requestedScaffoldSeconds" d.Budgets.RequestedScaffoldSeconds
                    writeNumber w "admittedPreflightSeconds" d.Budgets.AdmittedPreflightSeconds
                    writeNumber w "admittedScaffoldSeconds" d.Budgets.AdmittedScaffoldSeconds))

            writeObject w "observation" (fun () ->
                writeText w "platform" o.Platform

                writeArray w "tools" (sorted (fun (t: ObservedTool) -> t.Id) o.Tools) (fun t ->
                    w.WriteStartObject()
                    writeText w "id" t.Id
                    writeText w "version" t.Version
                    writeText w "executable" t.Executable
                    w.WriteEndObject())

                writeText w "consumedArchiveDigest" o.ConsumedArchiveDigest

                writeObject w "transport" (fun () ->
                    writeText w "executable" o.Transport.Executable
                    writeText w "version" o.Transport.Version)

                writeArray w "invocations" o.Invocations (fun i ->
                    w.WriteStartObject()
                    writeText w "executable" i.Executable
                    writeStrings w "arguments" false i.Arguments
                    writeText w "workingRoot" i.WorkingRoot
                    writeText w "workingDirectory" i.WorkingDirectory

                    writeArray w "environmentRoots" (sorted fst i.EnvironmentRoots) (fun (key, root) ->
                        w.WriteStartObject()
                        writeText w "key" key
                        writeText w "role" root.Role
                        writeText w "path" root.Path
                        w.WriteEndObject())

                    writeNumber w "timeoutSeconds" i.TimeoutSeconds
                    writeNumber w "exitCode" i.ExitCode
                    w.WriteEndObject())

                writeText w "result" o.Result
                writePaths w "producedPaths" o.ProducedPaths)

            w.WritePropertyName "ownership"
            use own = JsonDocument.Parse(ScaffoldProvenance.serialize r.Ownership)
            own.RootElement.WriteTo w
            w.WriteEndObject()
            w.Flush()
            Ok(Encoding.UTF8.GetString(stream.ToArray()))
        with Malformed(path, message) ->
            Error
                [
                    {
                        Code = "provenance.malformed"
                        Path = path
                        Message = message
                    }
                ]
