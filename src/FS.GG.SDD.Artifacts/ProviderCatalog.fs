namespace FS.GG.SDD.Artifacts

open System
open YamlDotNet.RepresentationModel
open Fsgg.ProviderCatalog

module ProviderCatalog =
    exception Malformed of string * string

    let malformed path message = raise (Malformed(path,message))
    let mapping path allowed (node: YamlNode) =
        match node with
        | :? YamlMappingNode as map ->
            let fields = map.Children |> Seq.map (fun pair ->
                match pair.Key with
                | :? YamlScalarNode as key ->
                    match tryScalarNonNullAt [] key with
                    | Some value -> value,pair.Value
                    | None -> malformed path "Mapping keys must be non-null scalars."
                | _ -> malformed path "Mapping keys must be scalars.") |> Seq.toList
            match fields |> List.countBy fst |> List.tryFind (fun (_,count) -> count > 1) with
            | Some(key,_) -> malformed (path + "." + key) "Duplicate mapping key."
            | None -> ()
            match fields |> List.tryFind (fun (key,_) -> not (List.contains key allowed)) with
            | Some(key,_) -> malformed (path + "." + key) "Unknown field."
            | None -> ()
            fields
        | _ -> malformed path "Expected mapping."
    let field path key fields =
        match List.tryFind (fun (k,_) -> k = key) fields with
        | Some(_,value) -> value
        | None -> malformed (path + "." + key) "Required field is missing."
    let scalar path (node: YamlNode) =
        match node with
        | :? YamlScalarNode as value ->
            match tryScalarNonNullAt [] value with
            | Some text -> text
            | None -> malformed path "Expected non-null scalar."
        | _ -> malformed path "Expected scalar."
    let scalarField path key fields = scalar (path + "." + key) (field path key fields)
    let integer path key fields =
        let value = scalarField path key fields
        match Int32.TryParse value with
        | true,number when string number = value -> number
        | _ -> malformed (path + "." + key) "Expected canonical integer."
    let boolean path key fields =
        match scalarField path key fields with
        | "true" -> true | "false" -> false
        | _ -> malformed (path + "." + key) "Expected true or false."
    let sequence path (node: YamlNode) =
        match node with :? YamlSequenceNode as values -> values.Children |> Seq.toList | _ -> malformed path "Expected sequence."
    let items path key fields = sequence (path + "." + key) (field path key fields)
    let strings path key fields = items path key fields |> List.mapi (fun i node -> scalar (sprintf "%s.%s[%d]" path key i) node)
    let parseParameter path node : Parameter =
        let f = mapping path ["key";"kind";"required";"prompt";"help";"default";"values";"validation"] node
        let vpath = path + ".validation"
        let v = mapping vpath ["nonEmpty";"minLength";"maxLength";"allowedValues"] (field path "validation" f)
        { Key = scalarField path "key" f
          Kind = match scalarField path "kind" f with "string" -> String | "enum" -> Enum | "exact-version" -> ExactVersion | _ -> malformed (path + ".kind") "Unsupported parameter kind."
          Required = boolean path "required" f; Prompt = scalarField path "prompt" f; Help = scalarField path "help" f
          Default = List.tryFind (fun (key,_) -> key = "default") f |> Option.map (fun (_,value) -> scalar (path + ".default") value)
          Values = strings path "values" f
          Validation = { NonEmpty = boolean vpath "nonEmpty" v; MinLength = integer vpath "minLength" v; MaxLength = integer vpath "maxLength" v; AllowedValues = strings vpath "allowedValues" v } }
    let parseTool path node : ToolRequirement =
        let f = mapping path ["id";"version";"platforms"] node
        { Id = scalarField path "id" f; Version = scalarField path "version" f; Platforms = strings path "platforms" f }
    let parseEvidence path node : EvidenceDeclaration =
        let f = mapping path ["id";"format";"path";"required"] node
        { Id = scalarField path "id" f; Format = scalarField path "format" f; Path = scalarField path "path" f; Required = boolean path "required" f }
    let parseBinding path node =
        let f = mapping path ["kind";"executable";"arguments";"workingDirectory";"timeoutSeconds";"costClass";"environmentIds"] node
        match scalarField path "kind" f with
        | "semantic-only" ->
            mapping path ["kind"] node |> ignore
            SemanticOnly
        | "command" ->
            Command({ Executable = scalarField path "executable" f; Arguments = strings path "arguments" f },
                    { WorkingDirectory = scalarField path "workingDirectory" f; TimeoutSeconds = integer path "timeoutSeconds" f; CostClass = scalarField path "costClass" f; EnvironmentIds = strings path "environmentIds" f })
        | _ -> malformed (path + ".kind") "Unsupported binding kind."
    let parseCapability path node : CapabilityDeclaration =
        let f = mapping path ["id";"required";"platforms";"toolIds";"evidenceIds";"binding"] node
        { Id = scalarField path "id" f; Required = boolean path "required" f; Platforms = strings path "platforms" f
          ToolIds = strings path "toolIds" f; EvidenceIds = strings path "evidenceIds" f; Binding = parseBinding (path + ".binding") (field path "binding" f) }
    let declarations parser path key fields = items path key fields |> List.mapi (fun i node -> parser (sprintf "%s.%s[%d]" path key i) node)
    let parseDescriptor path node : Descriptor =
        let f = mapping path ["id";"displayName";"help";"language";"productShape";"descriptorId";"descriptorRevision";"descriptorDigest";"contractVersion";"templateSource";"templateId";"platforms";"parameters";"identities";"tools";"capabilities";"evidence";"skills"] node
        let ip = path + ".identities"
        let ids = mapping ip ["rawName";"packageIdentity";"codeIdentifier"] (field path "identities" f)
        { Id = scalarField path "id" f; DisplayName = scalarField path "displayName" f; Help = scalarField path "help" f; Language = scalarField path "language" f; ProductShape = scalarField path "productShape" f
          DescriptorId = scalarField path "descriptorId" f; DescriptorRevision = scalarField path "descriptorRevision" f; DescriptorDigest = scalarField path "descriptorDigest" f; ContractVersion = scalarField path "contractVersion" f
          TemplateSource = scalarField path "templateSource" f; TemplateId = scalarField path "templateId" f; Platforms = strings path "platforms" f
          Parameters = declarations parseParameter path "parameters" f
          Identities = {RawName = scalarField ip "rawName" ids; PackageIdentity = scalarField ip "packageIdentity" ids; CodeIdentifier = scalarField ip "codeIdentifier" ids}
          Tools = declarations parseTool path "tools" f; Capabilities = declarations parseCapability path "capabilities" f; Evidence = declarations parseEvidence path "evidence" f; Skills = strings path "skills" f }
    let parse (text: string) =
        let diagnostic path message : Diagnostic = { Code = "catalog.malformed"; Path = path; Message = message }
        try
            match parseYamlDocument text with
            | YamlEmpty -> Error [diagnostic "$" "Expected one catalog document."]
            | YamlMalformed(message,_,_) -> Error [diagnostic "$" message]
            | YamlRoot root ->
                let stream = YamlStream()
                use reader = new System.IO.StringReader(text)
                stream.Load reader
                if stream.Documents.Count <> 1 then malformed "$" "Expected exactly one catalog document."
                let f = mapping "$" ["schemaVersion";"id";"revision";"digest";"providers"] root
                let catalog = { SchemaVersion = integer "$" "schemaVersion" f; Id = scalarField "$" "id" f; Revision = scalarField "$" "revision" f; Digest = scalarField "$" "digest" f; Providers = declarations parseDescriptor "$" "providers" f }
                match Fsgg.ProviderCatalog.validate catalog with [] -> Ok catalog | errors -> Error errors
        with
        | Malformed(path,message) -> Error [diagnostic path message]
