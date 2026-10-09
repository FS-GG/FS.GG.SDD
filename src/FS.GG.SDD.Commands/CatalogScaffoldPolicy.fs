namespace FS.GG.SDD.Commands

open System
open System.Text
open System.Text.Json
open System.Security.Cryptography
open FS.GG.Governance.Config
open FS.GG.Governance.Config.CapabilityBindings
open Fsgg.ProviderCatalog

module CatalogScaffoldPolicy =
    type Identity =
        {
            Id: string
            Version: string
            Digest: string
        }

    type EnvironmentBinding =
        {
            Id: string
            Environment: Model.EnvironmentClass
        }

    type EvidenceMapping =
        {
            Format: string
            Id: string
            Version: string
        }

    type ToolProbe =
        {
            Id: string
            Executable: string
            Arguments: string list
            VersionPrefix: string
            VersionSuffix: string
            MaximumOutputCharacters: int
        }

    type ArchiveAssociation =
        {
            TemplateSource: string
            PackageId: string
            PackageVersion: string
        }

    type Policy =
        {
            Identity: Identity
            EvidenceMapIdentity: Identity
            RequiredCapabilityIds: string list
            SemanticOnlyCapabilityIds: string list
            KnownPlatforms: string list
            EnvironmentBindings: EnvironmentBinding list
            SupportedEvidenceFormats: CapabilityBindings.EvidenceFormat list
            EvidenceMappings: EvidenceMapping list
            MaximumPreflightSeconds: int
            MaximumScaffoldSeconds: int
            ToolProbes: ToolProbe list
            Archives: ArchiveAssociation list
        }

    let private digest (bytes: byte array) =
        "sha256:" + (SHA256.HashData bytes |> Convert.ToHexString).ToLowerInvariant()

    let private require condition message =
        if not condition then
            invalidArg "policy" message

    let private fields names (element: JsonElement) =
        require (element.ValueKind = JsonValueKind.Object) "Expected a policy object."
        let actual = element.EnumerateObject() |> Seq.map _.Name |> Seq.toList
        require (actual.Length = (actual |> List.distinct).Length) "Duplicate policy properties."
        require (Set.ofList actual = Set.ofList names) "Missing or unknown policy properties."

    let private text (element: JsonElement) =
        require (element.ValueKind = JsonValueKind.String) "Expected a string."
        element.GetString() |> Option.ofObj |> Option.defaultValue ""

    let private nonblank element =
        let value = text element

        require
            (not (String.IsNullOrWhiteSpace value)
             && not (value |> Seq.exists Char.IsControl))
            "Expected a nonblank literal."

        value

    let private version element =
        let value = nonblank element
        require (Fsgg.ProviderCatalog.isExactVersion value) "Expected an exact version."
        value

    let private strings (element: JsonElement) =
        require (element.ValueKind = JsonValueKind.Array) "Expected an array."
        element.EnumerateArray() |> Seq.map nonblank |> Seq.toList

    let private unique label (values: 'a list) =
        require (values.Length = (values |> List.distinct).Length) ("Duplicate " + label + ".")
        values

    let private positive (element: JsonElement) =
        let value = element.GetInt32()
        require (value > 0) "Expected a positive integer budget."
        value

    let private array reader (element: JsonElement) =
        require (element.ValueKind = JsonValueKind.Array) "Expected an array."
        element.EnumerateArray() |> Seq.map reader |> Seq.toList

    let private mappingBytes (mappings: EvidenceMapping list) =
        let quote (value: string) = JsonSerializer.Serialize value

        mappings
        |> List.sortBy _.Format
        |> List.map (fun m ->
            "{\"format\":"
            + quote m.Format
            + ",\"id\":"
            + quote m.Id
            + ",\"version\":"
            + quote m.Version
            + "}")
        |> String.concat ","
        |> fun body -> Encoding.UTF8.GetBytes("[" + body + "]")

    let parse expectedRawDigest (bytes: byte array) =
        try
            require (not (obj.ReferenceEquals(bytes, null))) "Policy bytes are required."
            require (digest bytes = expectedRawDigest) "Policy raw digest does not match selected bytes."
            let mutable options = JsonDocumentOptions()
            options.MaxDepth <- 32
            use document = JsonDocument.Parse(ReadOnlyMemory<byte>(bytes), options)
            let root = document.RootElement

            fields
                [
                    "schemaVersion"
                    "id"
                    "version"
                    "evidenceMap"
                    "requiredCapabilityIds"
                    "semanticOnlyCapabilityIds"
                    "knownPlatforms"
                    "environments"
                    "supportedEvidenceFormats"
                    "maximumPreflightSeconds"
                    "maximumScaffoldSeconds"
                    "toolProbes"
                    "archives"
                ]
                root

            require (root.GetProperty("schemaVersion").GetInt32() = 1) "Unsupported policy schema version."
            let map = root.GetProperty("evidenceMap")
            fields [ "id"; "version"; "digest"; "mappings" ] map

            let mappings: EvidenceMapping list =
                map.GetProperty("mappings")
                |> array (fun row ->
                    fields [ "format"; "id"; "version" ] row

                    {
                        Format = nonblank (row.GetProperty("format"))
                        Id = nonblank (row.GetProperty("id"))
                        Version = version (row.GetProperty("version"))
                    })

            mappings |> List.map _.Format |> unique "evidence mapping key" |> ignore
            let mapDigest = nonblank (map.GetProperty("digest"))

            require
                (digest (mappingBytes mappings) = mapDigest)
                "Evidence map semantic digest does not match its mappings."

            let environments: EnvironmentBinding list =
                root.GetProperty("environments")
                |> array (fun row ->
                    fields [ "id"; "environment" ] row

                    let environment =
                        match nonblank (row.GetProperty("environment")) with
                        | "local" -> Model.Local
                        | "ci" -> Model.Ci
                        | "local-or-ci" -> Model.LocalOrCi
                        | "release" -> Model.Release
                        | _ -> invalidArg "policy" "Unknown environment class."

                    {
                        Id = nonblank (row.GetProperty("id"))
                        Environment = environment
                    })

            environments |> List.map _.Id |> unique "environment identity" |> ignore

            let formats: CapabilityBindings.EvidenceFormat list =
                root.GetProperty("supportedEvidenceFormats")
                |> array (fun row ->
                    fields [ "id"; "version" ] row

                    {
                        CapabilityBindings.EvidenceFormat.Id = nonblank (row.GetProperty("id"))
                        Version = version (row.GetProperty("version"))
                    })
                |> unique "supported format"

            let probes: ToolProbe list =
                root.GetProperty("toolProbes")
                |> array (fun row ->
                    fields
                        [
                            "id"
                            "executable"
                            "arguments"
                            "versionPrefix"
                            "versionSuffix"
                            "maximumOutputCharacters"
                        ]
                        row

                    let limit = positive (row.GetProperty("maximumOutputCharacters"))
                    require (limit <= 1048576) "Tool output bound exceeds the host limit."

                    {
                        Id = nonblank (row.GetProperty("id"))
                        Executable = nonblank (row.GetProperty("executable"))
                        Arguments = row.GetProperty("arguments") |> array text
                        VersionPrefix = text (row.GetProperty("versionPrefix"))
                        VersionSuffix = text (row.GetProperty("versionSuffix"))
                        MaximumOutputCharacters = limit
                    })

            probes |> List.map _.Id |> unique "tool probe identity" |> ignore

            let archives: ArchiveAssociation list =
                root.GetProperty("archives")
                |> array (fun row ->
                    fields [ "templateSource"; "packageId"; "packageVersion" ] row

                    {
                        TemplateSource = nonblank (row.GetProperty("templateSource"))
                        PackageId = nonblank (row.GetProperty("packageId"))
                        PackageVersion = version (row.GetProperty("packageVersion"))
                    })

            archives |> List.map _.TemplateSource |> unique "template association" |> ignore

            Ok
                {
                    Identity =
                        {
                            Id = nonblank (root.GetProperty("id"))
                            Version = version (root.GetProperty("version"))
                            Digest = expectedRawDigest
                        }
                    EvidenceMapIdentity =
                        {
                            Id = nonblank (map.GetProperty("id"))
                            Version = version (map.GetProperty("version"))
                            Digest = mapDigest
                        }
                    RequiredCapabilityIds =
                        strings (root.GetProperty("requiredCapabilityIds"))
                        |> unique "required capability"
                    SemanticOnlyCapabilityIds =
                        strings (root.GetProperty("semanticOnlyCapabilityIds"))
                        |> unique "semantic-only capability"
                    KnownPlatforms = strings (root.GetProperty("knownPlatforms")) |> unique "platform"
                    EnvironmentBindings = environments
                    SupportedEvidenceFormats = formats
                    EvidenceMappings = mappings
                    MaximumPreflightSeconds = positive (root.GetProperty("maximumPreflightSeconds"))
                    MaximumScaffoldSeconds = positive (root.GetProperty("maximumScaffoldSeconds"))
                    ToolProbes = probes
                    Archives = archives
                }
        with
        | :? ArgumentException as e ->
            Error
                [
                    {
                        Fsgg.ProviderCatalog.Diagnostic.Code = "catalog.policyInvalid"
                        Path = "$.policy"
                        Message = e.Message
                    }
                ]
        | :? JsonException as e ->
            Error
                [
                    {
                        Fsgg.ProviderCatalog.Diagnostic.Code = "catalog.policyInvalid"
                        Path = "$.policy"
                        Message = e.Message
                    }
                ]
        | :? InvalidOperationException as e ->
            Error
                [
                    {
                        Fsgg.ProviderCatalog.Diagnostic.Code = "catalog.policyInvalid"
                        Path = "$.policy"
                        Message = e.Message
                    }
                ]
        | :? System.Collections.Generic.KeyNotFoundException as e ->
            Error
                [
                    {
                        Fsgg.ProviderCatalog.Diagnostic.Code = "catalog.policyInvalid"
                        Path = "$.policy"
                        Message = e.Message
                    }
                ]
        | :? FormatException as e ->
            Error
                [
                    {
                        Fsgg.ProviderCatalog.Diagnostic.Code = "catalog.policyInvalid"
                        Path = "$.policy"
                        Message = e.Message
                    }
                ]
        | :? OverflowException as e ->
            Error
                [
                    {
                        Fsgg.ProviderCatalog.Diagnostic.Code = "catalog.policyInvalid"
                        Path = "$.policy"
                        Message = e.Message
                    }
                ]
