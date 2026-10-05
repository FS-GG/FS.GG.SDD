namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text
open System.Text.Json
open System.Security.Cryptography
open Fsgg.ProviderCatalog

module ProviderCatalogIntegrity =
    let digest (bytes: byte array) =
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData bytes)

    let ordinal (key: 'a -> string) (values: 'a list) =
        values
        |> List.sortWith (fun a b -> StringComparer.Ordinal.Compare(key a, key b))

    let writeStrings (w: Utf8JsonWriter) name values =
        w.WriteStartArray(name: string)
        values |> List.iter (fun (value: string) -> w.WriteStringValue value)
        w.WriteEndArray()

    let set w name values = writeStrings w name (ordinal id values)

    let objects (w: Utf8JsonWriter) name key write values =
        w.WriteStartArray(name: string)
        ordinal key values |> List.iter write
        w.WriteEndArray()

    let parameter (w: Utf8JsonWriter) (p: Parameter) =
        w.WriteStartObject()
        w.WriteString("key", p.Key)

        w.WriteString(
            "kind",
            match p.Kind with
            | String -> "string"
            | Enum -> "enum"
            | ExactVersion -> "exact-version"
        )

        w.WriteBoolean("required", p.Required)
        w.WriteString("prompt", p.Prompt)
        w.WriteString("help", p.Help)

        match p.Default with
        | Some value -> w.WriteString("default", value)
        | None -> w.WriteNull("default")

        set w "values" p.Values
        w.WriteStartObject("validation")
        w.WriteBoolean("nonEmpty", p.Validation.NonEmpty)
        w.WriteNumber("minLength", p.Validation.MinLength)
        w.WriteNumber("maxLength", p.Validation.MaxLength)
        set w "allowedValues" p.Validation.AllowedValues
        w.WriteEndObject()
        w.WriteEndObject()

    let tool (w: Utf8JsonWriter) (t: ToolRequirement) =
        w.WriteStartObject()
        w.WriteString("id", t.Id)
        w.WriteString("version", t.Version)
        set w "platforms" t.Platforms
        w.WriteEndObject()

    let evidence (w: Utf8JsonWriter) (e: EvidenceDeclaration) =
        w.WriteStartObject()
        w.WriteString("id", e.Id)
        w.WriteString("format", e.Format)
        w.WriteString("path", e.Path)
        w.WriteBoolean("required", e.Required)
        w.WriteEndObject()

    let capability (w: Utf8JsonWriter) (c: CapabilityDeclaration) =
        w.WriteStartObject()
        w.WriteString("id", c.Id)
        w.WriteBoolean("required", c.Required)
        set w "platforms" c.Platforms
        set w "toolIds" c.ToolIds
        set w "evidenceIds" c.EvidenceIds
        w.WriteStartObject("binding")

        match c.Binding with
        | SemanticOnly -> w.WriteString("kind", "semantic-only")
        | Command(command, limits) ->
            w.WriteString("kind", "command")
            w.WriteString("executable", command.Executable)
            writeStrings w "arguments" command.Arguments
            w.WriteString("workingDirectory", limits.WorkingDirectory)
            w.WriteNumber("timeoutSeconds", limits.TimeoutSeconds)
            w.WriteString("costClass", limits.CostClass)
            set w "environmentIds" limits.EnvironmentIds

        w.WriteEndObject()
        w.WriteEndObject()

    let descriptor (w: Utf8JsonWriter) includeDigest (d: Descriptor) =
        w.WriteStartObject()
        w.WriteString("id", d.Id)
        w.WriteString("displayName", d.DisplayName)
        w.WriteString("help", d.Help)
        w.WriteString("language", d.Language)
        w.WriteString("productShape", d.ProductShape)
        w.WriteString("descriptorId", d.DescriptorId)
        w.WriteString("descriptorRevision", d.DescriptorRevision)

        if includeDigest then
            w.WriteString("descriptorDigest", d.DescriptorDigest)

        w.WriteString("contractVersion", d.ContractVersion)
        w.WriteString("templateSource", d.TemplateSource)
        w.WriteString("templateId", d.TemplateId)
        set w "platforms" d.Platforms
        objects w "parameters" (fun (p: Parameter) -> p.Key) (parameter w) d.Parameters
        w.WriteStartObject("identities")
        w.WriteString("rawName", d.Identities.RawName)
        w.WriteString("packageIdentity", d.Identities.PackageIdentity)
        w.WriteString("codeIdentifier", d.Identities.CodeIdentifier)
        w.WriteEndObject()
        objects w "tools" (fun (t: ToolRequirement) -> t.Id) (tool w) d.Tools
        objects w "capabilities" (fun (c: CapabilityDeclaration) -> c.Id) (capability w) d.Capabilities
        objects w "evidence" (fun (e: EvidenceDeclaration) -> e.Id) (evidence w) d.Evidence
        set w "skills" d.Skills
        w.WriteEndObject()

    let encode write =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        write writer
        writer.Flush()
        stream.ToArray()

    let descriptorBytes d = encode (fun w -> descriptor w false d)

    let descriptorJson d =
        encode (fun w -> descriptor w true d) |> Encoding.UTF8.GetString

    let catalogBytes (c: Catalog) =
        encode (fun w ->
            w.WriteStartObject()
            w.WriteNumber("schemaVersion", c.SchemaVersion)
            w.WriteString("id", c.Id)
            w.WriteString("revision", c.Revision)
            objects w "providers" (fun (d: Descriptor) -> d.Id) (descriptor w true) c.Providers
            w.WriteEndObject())

    let verify expectedRawDigest (bytes: byte array) =
        let error code path message : Diagnostic =
            {
                Code = code
                Path = path
                Message = message
            }

        if expectedRawDigest <> digest bytes then
            Error
                [
                    error
                        "catalog.rawDigestMismatch"
                        "$"
                        "Catalog bytes do not match the explicitly supplied raw SHA-256."
                ]
        else
            try
                let text = UTF8Encoding(false, true).GetString bytes

                match ProviderCatalog.parse text with
                | Error diagnostics -> Error diagnostics
                | Ok catalog ->
                    let mismatches =
                        [
                            for d in ordinal (fun (d: Descriptor) -> d.Id) catalog.Providers do
                                if d.DescriptorDigest <> digest (descriptorBytes d) then
                                    yield
                                        error
                                            "catalog.descriptorDigestMismatch"
                                            ($"$.providers[{d.Id}].descriptorDigest")
                                            "Descriptor semantic SHA-256 does not match its canonical bytes."
                            if catalog.Digest <> digest (catalogBytes catalog) then
                                yield
                                    error
                                        "catalog.semanticDigestMismatch"
                                        "$.digest"
                                        "Catalog semantic SHA-256 does not match its canonical bytes."
                        ]

                    if mismatches.IsEmpty then Ok catalog else Error mismatches
            with :? DecoderFallbackException ->
                Error [ error "catalog.malformed" "$" "Catalog is not valid UTF-8." ]
