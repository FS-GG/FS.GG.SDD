namespace Fsgg

open System
open System.Text.RegularExpressions

module ProviderCatalog =
    type ParameterKind = String | Enum | ExactVersion
    type Validation =
        {
            NonEmpty: bool
            MinLength: int
            MaxLength: int
            AllowedValues: string list
        }
    type Parameter =
        {
            Key: string
            Kind: ParameterKind
            Required: bool
            Prompt: string
            Help: string
            Default: string option
            Values: string list
            Validation: Validation
        }
    type IdentityBindings =
        {
            RawName: string
            PackageIdentity: string
            CodeIdentifier: string
        }
    type ToolRequirement =
        {
            Id: string
            Version: string
            Platforms: string list
        }
    type EvidenceDeclaration =
        {
            Id: string
            Format: string
            Path: string
            Required: bool
        }
    type CommandLimits =
        {
            WorkingDirectory: string
            TimeoutSeconds: int
            CostClass: string
            EnvironmentIds: string list
        }
    type CapabilityBinding = SemanticOnly | Command of command: Provider.DeclaredCommand * limits: CommandLimits
    type CapabilityDeclaration =
        {
            Id: string
            Required: bool
            Platforms: string list
            ToolIds: string list
            EvidenceIds: string list
            Binding: CapabilityBinding
        }
    type Descriptor =
        {
            Id: string
            DisplayName: string
            Help: string
            Language: string
            ProductShape: string
            DescriptorId: string
            DescriptorRevision: string
            DescriptorDigest: string
            ContractVersion: string
            TemplateSource: string
            TemplateId: string
            Platforms: string list
            Parameters: Parameter list
            Identities: IdentityBindings
            Tools: ToolRequirement list
            Capabilities: CapabilityDeclaration list
            Evidence: EvidenceDeclaration list
            Skills: string list
        }
    type Catalog =
        {
            SchemaVersion: int
            Id: string
            Revision: string
            Digest: string
            Providers: Descriptor list
        }
    type Diagnostic =
        {
            Code: string
            Path: string
            Message: string
        }
    type PreparedConfiguration =
        {
            CatalogId: string
            CatalogRevision: string
            CatalogDigest: string
            Descriptor: Descriptor
            EffectiveParameters: (string * string) list
            RawProductName: string
            PackageIdentity: string
            CodeIdentifier: string
        }

    let isExactVersion (value: string) =
        not (String.IsNullOrEmpty value)
        && value.Length <= 128
        && Regex.IsMatch(value, @"\A[0-9][A-Za-z0-9._+-]*\z")
        && (value.Split('.') |> Array.forall (fun part -> part <> "" && part <> "x" && part <> "X"))

    let error code path message = { Code = "catalog." + code; Path = path; Message = message }
    let sorted errors = errors |> List.sortBy (fun e -> e.Path, e.Code, e.Message)
    let blank path value = if String.IsNullOrWhiteSpace value then [error "blank" path "A nonblank value is required."] else []
    let duplicates path values =
        values |> List.countBy id |> List.choose (fun (value, count) -> if count > 1 then Some(error "duplicate" path ("Duplicate identifier: " + value)) else None)
    let setErrors path values = (values |> List.collect (blank path)) @ duplicates path values
    let digest (path: string) (value: string) =
        if Regex.IsMatch(value, @"\Asha256:[0-9a-f]{64}\z") then [] else [error "digest" path "Expected sha256 and 64 lowercase hex digits."]
    let refs path declared values =
        setErrors path values @ (values |> List.choose (fun value -> if List.contains value declared then None else Some(error "reference" path ("Undeclared reference: " + value))))
    let valueValid (p: Parameter) (value: string) =
        not (obj.ReferenceEquals(value, null))
        && (not p.Validation.NonEmpty || value.Length > 0)
        && value.Length >= p.Validation.MinLength && value.Length <= p.Validation.MaxLength
        && (List.isEmpty p.Validation.AllowedValues || List.contains value p.Validation.AllowedValues)
        && (match p.Kind with String -> true | Enum -> List.contains value p.Values | ExactVersion -> isExactVersion value)

    let validate (catalog: Catalog) =
        let providerErrors (d: Descriptor) =
            let path = "providers." + d.Id
            let requiredStrings = ["id",d.Id; "displayName",d.DisplayName; "help",d.Help; "language",d.Language; "productShape",d.ProductShape; "descriptorId",d.DescriptorId; "descriptorRevision",d.DescriptorRevision; "templateSource",d.TemplateSource; "templateId",d.TemplateId]
            let parameterErrors (p: Parameter) =
                let pp = path + ".parameters." + p.Key
                blank (pp + ".key") p.Key @ blank (pp + ".prompt") p.Prompt @ blank (pp + ".help") p.Help
                @ setErrors (pp + ".values") p.Values @ setErrors (pp + ".validation.allowedValues") p.Validation.AllowedValues
                @ (if p.Validation.MinLength < 0 || p.Validation.MaxLength < p.Validation.MinLength then [error "validation" pp "Invalid length bounds."] else [])
                @ (if (p.Kind = Enum && List.isEmpty p.Values) || (p.Kind <> Enum && not (List.isEmpty p.Values)) then [error "validation" pp "Enum requires values; other kinds prohibit them."] else [])
                @ (match p.Default with Some value when not (valueValid p value) -> [error "invalidDefault" pp "Default fails declared validation."] | _ -> [])
            let identityKeys = [d.Identities.RawName; d.Identities.PackageIdentity; d.Identities.CodeIdentifier]
            let capabilityErrors (c: CapabilityDeclaration) =
                let cp = path + ".capabilities." + c.Id
                let bindingErrors =
                    match c.Binding with
                    | SemanticOnly -> []
                    | Command(command,limits) ->
                        blank (cp + ".executable") command.Executable
                        @ blank (cp + ".costClass") limits.CostClass
                        @ blank (cp + ".workingDirectory") limits.WorkingDirectory
                        @ setErrors (cp + ".environmentIds") limits.EnvironmentIds
                        @ (if limits.TimeoutSeconds <= 0 then [error "limits" cp "Timeout must be positive."] else [])
                blank (cp + ".id") c.Id
                @ refs (cp + ".platforms") d.Platforms c.Platforms
                @ (if List.isEmpty c.Platforms then [error "required" (cp + ".platforms") "Capability platforms are required."] else [])
                @ refs (cp + ".toolIds") (d.Tools |> List.map (fun t -> t.Id)) c.ToolIds
                @ refs (cp + ".evidenceIds") (d.Evidence |> List.map (fun e -> e.Id)) c.EvidenceIds
                @ bindingErrors
            let initial =
                requiredStrings |> List.collect (fun (key,value) -> blank (path + "." + key) value)
            initial
                @ digest (path + ".descriptorDigest") d.DescriptorDigest
                @ (if d.ContractVersion <> "3.0.0" then [error "unsupportedContract" (path + ".contractVersion") "Only descriptor protocol 3.0.0 is supported."] else [])
                @ setErrors (path + ".platforms") d.Platforms
                @ (if List.isEmpty d.Platforms then [error "required" (path + ".platforms") "At least one platform is required."] else [])
                @ duplicates (path + ".parameters") (d.Parameters |> List.map (fun p -> p.Key))
                @ (d.Parameters |> List.collect parameterErrors)
                @ refs (path + ".identities") (d.Parameters |> List.map (fun p -> p.Key)) identityKeys
                @ (identityKeys |> List.collect (fun key -> match d.Parameters |> List.tryFind (fun p -> p.Key = key) with Some p when not p.Required -> [error "identity" (path + ".identities") "Identity bindings must refer to required parameters."] | _ -> []))
                @ duplicates (path + ".tools") (d.Tools |> List.map (fun t -> t.Id))
                @ (d.Tools |> List.collect (fun t -> blank (path + ".tools.id") t.Id @ refs (path + ".tools.platforms") d.Platforms t.Platforms @ (if List.isEmpty t.Platforms then [error "required" (path + ".tools.platforms") "Tool platforms are required."] else []) @ (if isExactVersion t.Version then [] else [error "exactVersion" (path + ".tools." + t.Id) "Tool version must be an exact literal."])))
                @ duplicates (path + ".evidence") (d.Evidence |> List.map (fun e -> e.Id))
                @ (d.Evidence |> List.collect (fun e -> blank (path + ".evidence.id") e.Id @ blank (path + ".evidence.format") e.Format @ blank (path + ".evidence.path") e.Path))
                @ duplicates (path + ".capabilities") (d.Capabilities |> List.map (fun c -> c.Id))
                @ (d.Capabilities |> List.collect capabilityErrors)
                @ setErrors (path + ".skills") d.Skills
        (if catalog.SchemaVersion <> 2 then [error "unsupportedSchema" "schemaVersion" "Only catalog schema 2 is supported."] else [])
        @ blank "id" catalog.Id @ blank "revision" catalog.Revision @ digest "digest" catalog.Digest
        @ (if List.isEmpty catalog.Providers then [error "required" "providers" "At least one provider is required."] else [])
        @ duplicates "providers" (catalog.Providers |> List.map (fun d -> d.Id))
        @ duplicates "descriptorIds" (catalog.Providers |> List.map (fun d -> d.DescriptorId))
        @ (catalog.Providers |> List.collect providerErrors) |> sorted

    let normalize (d: Descriptor) =
        let order = List.sortWith (fun a b -> StringComparer.Ordinal.Compare(a,b))
        { d with Platforms = order d.Platforms; Parameters = d.Parameters |> List.sortBy (fun p -> p.Key) |> List.map (fun p -> {p with Values = order p.Values; Validation = {p.Validation with AllowedValues = order p.Validation.AllowedValues}})
                 Tools = d.Tools |> List.sortBy (fun t -> t.Id) |> List.map (fun t -> {t with Platforms = order t.Platforms})
                 Evidence = d.Evidence |> List.sortBy (fun e -> e.Id); Skills = order d.Skills
                 Capabilities = d.Capabilities |> List.sortBy (fun c -> c.Id) |> List.map (fun c -> {c with Platforms = order c.Platforms; ToolIds = order c.ToolIds; EvidenceIds = order c.EvidenceIds; Binding = match c.Binding with SemanticOnly -> SemanticOnly | Command(cmd,limits) -> Command(cmd,{limits with EnvironmentIds = order limits.EnvironmentIds})}) }

    let resolve (catalog: Catalog) providerId overrides =
        let errors = validate catalog
        match catalog.Providers |> List.tryFind (fun d -> d.Id = providerId) with
        | None -> Error(sorted (errors @ [error "unknownProvider" "providerId" "Provider is not declared."]))
        | Some descriptor ->
            let keys = descriptor.Parameters |> List.map (fun p -> p.Key)
            let inputErrors = duplicates "overrides" (overrides |> List.map fst) @ (overrides |> List.choose (fun (key,_) -> if List.contains key keys then None else Some(error "unknownInput" ("overrides." + key) "Input is not declared.")))
            let effective =
                descriptor.Parameters |> List.choose (fun p ->
                    let selected = overrides |> List.tryFind (fun (key,_) -> key = p.Key) |> Option.map snd |> Option.orElse p.Default
                    selected |> Option.map (fun value -> p.Key,value)) |> List.sortBy fst
            let valueErrors = descriptor.Parameters |> List.collect (fun p ->
                match effective |> List.tryFind (fun (key,_) -> key = p.Key) with
                | None when p.Required -> [error "requiredInput" ("inputs." + p.Key) "Required input is missing."]
                | Some(_,value) when not (valueValid p value) -> [error "invalidInput" ("inputs." + p.Key) "Input fails declared validation."]
                | _ -> [])
            let all = sorted (errors @ inputErrors @ valueErrors)
            if not (List.isEmpty all) then Error all
            else
                let get key = effective |> List.find (fun (k,_) -> k = key) |> snd
                Ok { CatalogId = catalog.Id; CatalogRevision = catalog.Revision; CatalogDigest = catalog.Digest; Descriptor = normalize descriptor
                     EffectiveParameters = effective; RawProductName = get descriptor.Identities.RawName; PackageIdentity = get descriptor.Identities.PackageIdentity; CodeIdentifier = get descriptor.Identities.CodeIdentifier }
