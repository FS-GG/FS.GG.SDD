namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.Globalization
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

type QuintContractMetadata =
    { Specification: string
      Relationships: QuintRelationship list
      VerificationProfiles: QuintVerificationProfile list
      Bounds: QuintFiniteBound list
      Impacts: QuintImpact list
      Compatibility: QuintCompatibility list
      Digests: QuintSemanticDigest list }

type QuintGeneralObservedCompilation =
    { ModuleName: string
      Toolchain: QuintToolchainManifest
      Cache: QuintCacheObservation list
      ProcessRequests: QuintProcessRequest list
      Endpoint: QuintEndpointState
      ProcessObservations: QuintProcessObservation list
      Source: QuintMarkdownSource
      FenceManifest: QuintFenceManifest
      Extraction: QuintExtractionObservation
      SourceMap: QuintSourceMap
      TypedEffect: QuintGeneralTypedEffectObservation
      Metadata: QuintContractMetadata }

// Keep the original profile-1 record last among records sharing these labels. F# resolves an
// otherwise-unannotated record expression to the most recently declared matching shape, so this
// ordering preserves source compatibility for existing consumers while profile 2 remains explicit.
type QuintObservedCompilation =
    { ModuleName: string
      Toolchain: QuintToolchainManifest
      Cache: QuintCacheObservation list
      ProcessRequests: QuintProcessRequest list
      Endpoint: QuintEndpointState
      ProcessObservations: QuintProcessObservation list
      Source: QuintMarkdownSource
      FenceManifest: QuintFenceManifest
      Extraction: QuintExtractionObservation
      SourceMap: QuintSourceMap
      TypedEffect: QuintTypedEffectObservation
      Metadata: QuintContractMetadata }

type QuintCompilationReceipt =
    { Schema: string
      SourceSha256: string
      FenceManifestSha256: string
      GeneratedModulesSha256: string
      ToolchainSha256: string
      TypedEffectSha256: string
      ContractSha256: string
      CompilationFingerprint: string
      ProcessSteps: string list }

type QuintCompilationOutput =
    { Plan: QuintCompilationPlan
      Contract: QuintCompiledContract
      CanonicalContract: string
      CompilationFingerprint: string
      Bindings: QuintGeneratedBindings
      Receipt: QuintCompilationReceipt
      CanonicalReceipt: string }

type QuintGeneralCompilationOutput =
    { Plan: QuintCompilationPlan
      Contract: QuintCompiledContractV2
      CanonicalContract: string
      CompilationFingerprint: string
      BindingManifest: QuintGeneralBindingManifest
      CanonicalBindingManifest: string
      Bindings: QuintGeneratedBindings
      Receipt: QuintCompilationReceipt
      CanonicalReceipt: string }

module private CompilerInternal =
    let diagnostic code path message location : SpecificationDiagnostic =
        { Code = code
          Path = path
          Message = message
          Location = location }

    let sorted (findings: SpecificationDiagnostic list) =
        findings
        |> List.distinct
        |> List.sortBy (fun finding -> finding.Path, finding.Code, finding.Message)

    let profileDiagnostic (finding: QuintProfileDiagnostic) =
        diagnostic
            finding.Code
            finding.Path
            ($"%s{finding.Message} %s{finding.Correction}")
            (finding.Source
             |> Option.map (fun source ->
                 { Line = source.Start.Line
                   Column = source.Start.Column }))

    let contractDiagnostic (finding: QuintContractDiagnostic) =
        diagnostic finding.Code finding.Path ($"%s{finding.Message} %s{finding.Correction}") None

    let bindingDiagnostic (finding: QuintBindingDiagnostic) =
        diagnostic finding.Code finding.Path finding.Message None

    let sha256Bytes (bytes: byte array) =
        SHA256.HashData bytes
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let sha256Text (text: string) =
        text |> Encoding.UTF8.GetBytes |> sha256Bytes

    let generatedDigest (modules: QuintGeneratedModule list) =
        let frame (value: string) =
            let bytes = Encoding.UTF8.GetBytes value

            Array.concat
                [ Encoding.ASCII.GetBytes(bytes.Length.ToString(CultureInfo.InvariantCulture) + ":")
                  bytes ]

        modules
        |> List.sortBy _.Target
        |> List.collect (fun item -> [ item.Target; item.Sha256; item.Bytes.ToString(CultureInfo.InvariantCulture) ])
        |> List.collect (frame >> Array.toList)
        |> List.toArray
        |> sha256Bytes

    let encodeReceipt (receipt: QuintCompilationReceipt) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("schema", receipt.Schema)
        writer.WriteString("sourceSha256", receipt.SourceSha256)
        writer.WriteString("fenceManifestSha256", receipt.FenceManifestSha256)
        writer.WriteString("generatedModulesSha256", receipt.GeneratedModulesSha256)
        writer.WriteString("toolchainSha256", receipt.ToolchainSha256)
        writer.WriteString("typedEffectSha256", receipt.TypedEffectSha256)
        writer.WriteString("contractSha256", receipt.ContractSha256)
        writer.WriteString("compilationFingerprint", receipt.CompilationFingerprint)
        writer.WriteStartArray("processSteps")
        receipt.ProcessSteps |> List.sort |> List.iter writer.WriteStringValue
        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let fingerprintV2 sourceSha256 fenceManifestSha256 generatedModulesSha256 toolchainSha256 contract =
        let frame (value: string) =
            let bytes = Encoding.UTF8.GetBytes value

            Encoding.ASCII.GetBytes(bytes.Length.ToString(CultureInfo.InvariantCulture) + ":")
            |> fun prefix -> Array.append prefix bytes

        [ "fsgg.quint.compilation-fingerprint/v2"
          sourceSha256
          fenceManifestSha256
          generatedModulesSha256
          toolchainSha256
          contract ]
        |> List.map frame
        |> Array.concat
        |> sha256Bytes

    let generalBindingSourceFindings
        (source: QuintMarkdownSource)
        (sourceMap: QuintSourceMap)
        (exports: QuintGeneralExportBinding list)
        (actions: QuintCatalogueSourceBinding list)
        =
        let contentRanges = sourceMap.Entries |> List.map (fun entry -> entry.Source.Range)
        let lines = source.Text.Split('\n')

        let positionAtOrAfter (left: QuintSourcePosition) (right: QuintSourcePosition) =
            left.Line > right.Line
            || (left.Line = right.Line && left.Column >= right.Column)

        let positionAtOrBefore (left: QuintSourcePosition) (right: QuintSourcePosition) =
            left.Line < right.Line
            || (left.Line = right.Line && left.Column <= right.Column)

        let isContained (candidate: QuintSourceRange) =
            contentRanges
            |> List.exists (fun content ->
                candidate.Path = source.Path
                && content.Path = source.Path
                && positionAtOrAfter candidate.Start content.Start
                && positionAtOrBefore candidate.End content.End)

        let declarationPrefixes =
            [ "type "
              "const "
              "var "
              "val "
              "pure val "
              "def "
              "pure def "
              "nondet "
              "action "
              "assume "
              "run "
              "import "
              "export "
              "instance " ]

        let exactDeclarationRange moduleName prefixes declarationName =
            let moduleLine = $"module %s{moduleName} {{"

            let modules =
                lines
                |> Array.indexed
                |> Array.choose (fun (index, line) ->
                    if line.Trim() = moduleLine then
                        Some(index, line.Length - line.TrimStart().Length)
                    else
                        None)

            match modules with
            | [| moduleStart, moduleIndentation |] ->
                let moduleEnd =
                    lines
                    |> Array.indexed
                    |> Array.tryFind (fun (index, line) ->
                        let suffix = line.TrimStart()

                        index > moduleStart
                        && suffix = "}"
                        && line.Length - suffix.Length = moduleIndentation)

                match moduleEnd with
                | Some(moduleEndIndex, _) ->
                    let starts =
                        lines
                        |> Array.indexed
                        |> Array.choose (fun (index, line) ->
                            let suffix = line.TrimStart()

                            if
                                index > moduleStart
                                && index < moduleEndIndex
                                && (prefixes
                                    |> List.exists (fun prefix ->
                                        suffix.StartsWith($"%s{prefix}%s{declarationName}", StringComparison.Ordinal)))
                            then
                                Some(index, line.Length - suffix.Length)
                            else
                                None)

                    match starts with
                    | [| startIndex, indentation |] ->
                        let nextDeclaration =
                            lines
                            |> Array.indexed
                            |> Array.tryFind (fun (index, line) ->
                                if index <= startIndex || index > moduleEndIndex then
                                    false
                                else
                                    let suffix = line.TrimStart()
                                    let nextIndentation = line.Length - suffix.Length

                                    not (String.IsNullOrWhiteSpace suffix)
                                    && ((nextIndentation = indentation
                                         && (declarationPrefixes
                                             |> List.exists (fun candidate ->
                                                 suffix.StartsWith(candidate, StringComparison.Ordinal))))
                                        || index = moduleEndIndex))

                        nextDeclaration
                        |> Option.map (fun (nextIndex, _) ->
                            { Path = source.Path
                              Start = { Line = startIndex + 1; Column = 1 }
                              End =
                                { Line = nextIndex
                                  Column = indentation + 2 } })
                    | _ -> None
                | None -> None
            | _ -> None

        let finding path id moduleName declarationName prefixes (candidate: QuintSourceRange) =
            [ if not (isContained candidate) then
                  yield
                      diagnostic
                          "QUINT-COMPILER-SOURCE-BINDING"
                          path
                          $"Binding '%s{id}' does not lie inside the selected canonical Quint fence."
                          (Some
                              { Line = candidate.Start.Line
                                Column = candidate.Start.Column })
              elif exactDeclarationRange moduleName prefixes declarationName <> Some candidate then
                  yield
                      diagnostic
                          "QUINT-COMPILER-SOURCE-BINDING"
                          path
                          $"Binding '%s{id}' is not the exact canonical source range of Quint declaration '%s{declarationName}'."
                          (Some
                              { Line = candidate.Start.Line
                                Column = candidate.Start.Column }) ]

        [ yield!
              exports
              |> List.indexed
              |> List.collect (fun (index, binding) ->
                  finding
                      $"/typedEffect/exportBindings/%d{index}/source"
                      binding.Id
                      binding.ModuleName
                      binding.DeclarationName
                      [ "pure val "; "val " ]
                      binding.Source)
          yield!
              actions
              |> List.indexed
              |> List.collect (fun (index, binding) ->
                  finding
                      $"/typedEffect/actionBindings/%d{index}/source"
                      binding.Id
                      binding.ModuleName
                      binding.CatalogueName
                      [ "action " ]
                      binding.Source) ]
        |> sorted

    let private fields =
        function
        | QuintRecord values -> values |> Map.ofList |> Some
        | _ -> None

    let private stringField name values =
        match Map.tryFind name values with
        | Some(QuintString value) -> Some value
        | _ -> None

    let private intField name values =
        match Map.tryFind name values with
        | Some(QuintInt value) -> Some value
        | _ -> None

    let private stringsField name values =
        match Map.tryFind name values with
        | Some(QuintList entries)
        | Some(QuintSet entries) ->
            entries
            |> List.map (function
                | QuintString value -> Some value
                | _ -> None)
            |> function
                | entries when List.forall Option.isSome entries -> entries |> List.choose id |> List.sort |> Some
                | _ -> None
        | _ -> None

    type GeneralContractFacts =
        { Relationships: QuintRelationship list
          VerificationProfiles: QuintVerificationProfile list
          Bounds: QuintFiniteBound list
          Impacts: QuintImpact list
          Compatibility: QuintCompatibility list }

    let deriveGeneralContractFacts (catalogue: QuintModelCatalogueEntry list) =
        let mutable diagnostics = []

        let malformed (row: QuintModelCatalogueEntry) expected =
            diagnostics <-
                diagnostic
                    "QUINT-COMPILER-CONTRACT-FACT"
                    ($"/catalogue/%s{row.Id}")
                    ($"Quint catalogue row '%s{row.Id}' with kind '%s{row.Kind}' is not a valid %s{expected} declaration.")
                    (Some
                        { Line = row.Source.Start.Line
                          Column = row.Source.Start.Column })
                :: diagnostics

        let relationships =
            catalogue
            |> List.choose (fun row ->
                let relationKind =
                    match row.Kind with
                    | "requires" -> Some Requires
                    | "verifiedBy" -> Some VerifiedBy
                    | "implementedBy" -> Some ImplementedBy
                    | "reads" -> Some Reads
                    | "writes" -> Some Writes
                    | _ -> None

                match relationKind, fields row.Value with
                | Some kind, Some values ->
                    match stringField "fromId" values, stringField "toId" values with
                    | Some fromId, Some toId ->
                        Some
                            { FromId = fromId
                              Kind = kind
                              ToId = toId }
                    | _ ->
                        malformed row "relationship"
                        None
                | Some _, None ->
                    malformed row "relationship"
                    None
                | None, _ -> None)

        let verificationProfiles =
            catalogue
            |> List.choose (fun row ->
                if row.Kind <> "verification" then
                    None
                else
                    match fields row.Value with
                    | Some values ->
                        match
                            stringField "verificationKind" values,
                            stringsField "subjectIds" values,
                            stringsField "boundIds" values
                        with
                        | Some kind, Some subjectIds, Some boundIds ->
                            Some
                                { Id = row.Id
                                  Kind = kind
                                  SubjectIds = subjectIds
                                  BoundIds = boundIds }
                        | _ ->
                            malformed row "verification"
                            None
                    | None ->
                        malformed row "verification"
                        None)

        let bounds =
            catalogue
            |> List.choose (fun row ->
                if row.Kind <> "bound" then
                    None
                else
                    match fields row.Value with
                    | Some values ->
                        match intField "minimum" values, intField "maximum" values with
                        | Some minimum, Some maximum ->
                            Some
                                { Id = row.Id
                                  Minimum = minimum
                                  Maximum = maximum }
                        | _ ->
                            malformed row "finite-bound"
                            None
                    | None ->
                        malformed row "finite-bound"
                        None)

        let impacts =
            catalogue
            |> List.choose (fun row ->
                if row.Kind <> "impact" then
                    None
                else
                    match fields row.Value with
                    | Some values ->
                        match
                            stringField "subjectId" values, stringField "category" values, stringField "detail" values
                        with
                        | Some subjectId, Some category, Some detail ->
                            Some
                                { SubjectId = subjectId
                                  Category = category
                                  Detail = detail }
                        | _ ->
                            malformed row "impact"
                            None
                    | None ->
                        malformed row "impact"
                        None)

        let compatibility =
            catalogue
            |> List.choose (fun row ->
                if row.Kind <> "compatibility" then
                    None
                else
                    match fields row.Value with
                    | Some values ->
                        match
                            stringField "surface" values, stringField "requirement" values, stringField "detail" values
                        with
                        | Some surface, Some requirement, Some detail ->
                            Some
                                { Surface = surface
                                  Requirement = requirement
                                  Detail = detail }
                        | _ ->
                            malformed row "compatibility"
                            None
                    | None ->
                        malformed row "compatibility"
                        None)

        { Relationships = relationships
          VerificationProfiles = verificationProfiles
          Bounds = bounds
          Impacts = impacts
          Compatibility = compatibility },
        diagnostics |> sorted

[<RequireQualifiedAccess>]
module QuintCompiler =
    let receiptSchema = "fsgg.quint.observed-compilation-receipt/v1"
    let generalReceiptSchema = "fsgg.quint.observed-compilation-receipt/v2"
    let encodeReceipt receipt = CompilerInternal.encodeReceipt receipt

    let compileObserved
        (input: QuintObservedCompilation)
        : Result<QuintCompilationOutput, SpecificationDiagnostic list> =
        let sourceFindings =
            QuintSource.validateManifest input.Source input.FenceManifest
            @ QuintSource.validateExtraction input.Source input.FenceManifest input.Extraction
            @ QuintSource.validateSourceMap input.Source input.FenceManifest input.SourceMap

        let profileBindingFindings =
            [ if input.Toolchain.Profile <> input.TypedEffect.Profile then
                  CompilerInternal.diagnostic
                      "QUINT-COMPILER-PROFILE-BINDING"
                      "/toolchain/profile"
                      "Toolchain and typed/effect observations select different profiles."
                      None ]

        let plan = QuintToolchain.plan input.Toolchain input.Cache input.ProcessRequests

        let planFindings, planValue =
            match plan with
            | Ok value -> QuintToolchain.validateExecution value input.Endpoint input.ProcessObservations, Some value
            | Error findings -> findings, None

        let profile = QuintProfile.adaptTypedEffectJson input.TypedEffect

        let profileFindings, catalogue =
            match profile with
            | Ok value -> [], Some value
            | Error findings -> findings |> List.map CompilerInternal.profileDiagnostic, None

        let initial =
            CompilerInternal.sorted (sourceFindings @ profileBindingFindings @ planFindings @ profileFindings)

        match initial, planValue, catalogue with
        | [], Some acceptedPlan, Some acceptedCatalogue ->
            let contract =
                { Schema = QuintContract.schema
                  Profile = acceptedCatalogue.Profile
                  Specification = input.Metadata.Specification
                  Catalogue = acceptedCatalogue.Entries
                  ActionEffects = acceptedCatalogue.ActionEffects
                  Relationships = input.Metadata.Relationships
                  VerificationProfiles = input.Metadata.VerificationProfiles
                  Bounds = input.Metadata.Bounds
                  Impacts = input.Metadata.Impacts
                  Compatibility = input.Metadata.Compatibility
                  Digests = input.Metadata.Digests }

            match QuintContract.serializeCanonical contract with
            | Error findings ->
                findings
                |> List.map CompilerInternal.contractDiagnostic
                |> CompilerInternal.sorted
                |> Error
            | Ok canonicalContract ->
                let generatedModulesSha256 = CompilerInternal.generatedDigest input.Extraction.First
                let fenceManifestSha256 = QuintSource.fenceManifestFingerprint input.FenceManifest
                let toolchainSha256 = QuintToolchain.fingerprint input.Toolchain

                let fingerprint =
                    QuintContract.fingerprint
                        { SourceSha256 = input.Source.Sha256
                          FenceManifestSha256 = fenceManifestSha256
                          GeneratedModulesSha256 = generatedModulesSha256
                          ToolchainSha256 = toolchainSha256
                          Contract = contract }

                let bindings = QuintBindings.generate input.ModuleName contract

                match fingerprint, bindings with
                | Ok compilationFingerprint, Ok generatedBindings ->
                    let receipt =
                        { Schema = receiptSchema
                          SourceSha256 = input.Source.Sha256
                          FenceManifestSha256 = fenceManifestSha256
                          GeneratedModulesSha256 = generatedModulesSha256
                          ToolchainSha256 = toolchainSha256
                          TypedEffectSha256 = CompilerInternal.sha256Text input.TypedEffect.TypedEffectJson
                          ContractSha256 = CompilerInternal.sha256Text canonicalContract
                          CompilationFingerprint = compilationFingerprint
                          ProcessSteps = acceptedPlan.Requests |> List.map _.StepId |> List.sort }

                    Ok
                        { Plan = acceptedPlan
                          Contract = contract
                          CanonicalContract = canonicalContract
                          CompilationFingerprint = compilationFingerprint
                          Bindings = generatedBindings
                          Receipt = receipt
                          CanonicalReceipt = encodeReceipt receipt }
                | Error findings, _ ->
                    findings
                    |> List.map CompilerInternal.contractDiagnostic
                    |> CompilerInternal.sorted
                    |> Error
                | _, Error findings ->
                    findings
                    |> List.map CompilerInternal.bindingDiagnostic
                    |> CompilerInternal.sorted
                    |> Error
        | findings, _, _ -> Error findings

    let compileGeneralObserved (input: QuintGeneralObservedCompilation) =
        let sourceFindings =
            QuintSource.validateManifest input.Source input.FenceManifest
            @ QuintSource.validateExtraction input.Source input.FenceManifest input.Extraction
            @ QuintSource.validateSourceMap input.Source input.FenceManifest input.SourceMap
            @ CompilerInternal.generalBindingSourceFindings
                input.Source
                input.SourceMap
                input.TypedEffect.ExportBindings
                input.TypedEffect.ActionBindings

        let profileBindingFindings =
            [ if input.Toolchain.Profile <> input.TypedEffect.Profile then
                  CompilerInternal.diagnostic
                      "QUINT-COMPILER-PROFILE-BINDING"
                      "/toolchain/profile"
                      "Toolchain and typed/effect observations select different profiles."
                      None ]

        let plan = QuintToolchain.plan input.Toolchain input.Cache input.ProcessRequests

        let planFindings, planValue =
            match plan with
            | Ok value -> QuintToolchain.validateExecution value input.Endpoint input.ProcessObservations, Some value
            | Error findings -> findings, None

        let profileFindings, catalogue =
            match QuintGeneralProfile.adaptTypedEffectJson input.TypedEffect with
            | Ok value -> [], Some value
            | Error findings -> findings |> List.map CompilerInternal.profileDiagnostic, None

        let metadataFindings =
            [ if
                  not input.Metadata.Relationships.IsEmpty
                  || not input.Metadata.VerificationProfiles.IsEmpty
                  || not input.Metadata.Bounds.IsEmpty
                  || not input.Metadata.Impacts.IsEmpty
                  || not input.Metadata.Compatibility.IsEmpty
              then
                  CompilerInternal.diagnostic
                      "QUINT-COMPILER-SEMANTIC-SIDECAR"
                      "/metadata"
                      "Profile-2 semantic contract facts must originate in promoted Quint catalogue rows."
                      None ]

        let initial =
            CompilerInternal.sorted (
                sourceFindings
                @ profileBindingFindings
                @ planFindings
                @ profileFindings
                @ metadataFindings
            )

        match initial, planValue, catalogue with
        | [], Some acceptedPlan, Some acceptedCatalogue ->
            let derivedFacts, factFindings =
                CompilerInternal.deriveGeneralContractFacts acceptedCatalogue.Catalogue

            let contract: QuintCompiledContractV2 =
                { Schema = QuintContractV2.schema
                  Profile = acceptedCatalogue.Profile
                  Specification = input.Metadata.Specification
                  Exports = acceptedCatalogue.Exports
                  Catalogue = acceptedCatalogue.Catalogue
                  ActionEffects = acceptedCatalogue.ActionEffects
                  Relationships = derivedFacts.Relationships
                  VerificationProfiles = derivedFacts.VerificationProfiles
                  Bounds = derivedFacts.Bounds
                  Impacts = derivedFacts.Impacts
                  Compatibility = derivedFacts.Compatibility
                  Digests = input.Metadata.Digests }

            match factFindings, QuintContractV2.serializeCanonical contract with
            | findings, _ when not findings.IsEmpty -> Error findings
            | _, Error findings ->
                findings
                |> List.map CompilerInternal.contractDiagnostic
                |> CompilerInternal.sorted
                |> Error
            | _, Ok canonicalContract ->
                let generatedModulesSha256 = CompilerInternal.generatedDigest input.Extraction.First
                let fenceManifestSha256 = QuintSource.fenceManifestFingerprint input.FenceManifest
                let toolchainSha256 = QuintToolchain.fingerprint input.Toolchain

                let compilationFingerprint =
                    CompilerInternal.fingerprintV2
                        input.Source.Sha256
                        fenceManifestSha256
                        generatedModulesSha256
                        toolchainSha256
                        canonicalContract

                let bindingManifest =
                    { Schema = QuintGeneralBindingManifest.schema
                      Profile = input.TypedEffect.Profile
                      ModuleName = input.ModuleName
                      Exports = input.TypedEffect.ExportBindings
                      Actions = input.TypedEffect.ActionBindings }

                match
                    QuintGeneralBindingManifest.serializeCanonical bindingManifest,
                    QuintBindingsV2.generate input.ModuleName contract
                with
                | Error findings, _ ->
                    findings
                    |> List.map CompilerInternal.profileDiagnostic
                    |> CompilerInternal.sorted
                    |> Error
                | _, Error findings ->
                    findings
                    |> List.map CompilerInternal.bindingDiagnostic
                    |> CompilerInternal.sorted
                    |> Error
                | Ok canonicalBindingManifest, Ok generatedBindings ->
                    let receipt: QuintCompilationReceipt =
                        { Schema = generalReceiptSchema
                          SourceSha256 = input.Source.Sha256
                          FenceManifestSha256 = fenceManifestSha256
                          GeneratedModulesSha256 = generatedModulesSha256
                          ToolchainSha256 = toolchainSha256
                          TypedEffectSha256 = CompilerInternal.sha256Text input.TypedEffect.TypedEffectJson
                          ContractSha256 = CompilerInternal.sha256Text canonicalContract
                          CompilationFingerprint = compilationFingerprint
                          ProcessSteps = acceptedPlan.Requests |> List.map _.StepId |> List.sort }

                    Ok
                        { Plan = acceptedPlan
                          Contract = contract
                          CanonicalContract = canonicalContract
                          CompilationFingerprint = compilationFingerprint
                          BindingManifest = bindingManifest
                          CanonicalBindingManifest = canonicalBindingManifest
                          Bindings = generatedBindings
                          Receipt = receipt
                          CanonicalReceipt = encodeReceipt receipt }
        | findings, _, _ -> Error findings
