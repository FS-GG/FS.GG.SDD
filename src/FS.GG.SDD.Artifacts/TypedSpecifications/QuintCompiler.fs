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

[<RequireQualifiedAccess>]
module QuintCompiler =
    let receiptSchema = "fsgg.quint.observed-compilation-receipt/v1"
    let generalReceiptSchema = "fsgg.quint.observed-compilation-receipt/v2"
    let encodeReceipt receipt = CompilerInternal.encodeReceipt receipt

    let compileObserved (input: QuintObservedCompilation) : Result<QuintCompilationOutput, SpecificationDiagnostic list> =
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

        let initial =
            CompilerInternal.sorted (sourceFindings @ profileBindingFindings @ planFindings @ profileFindings)

        match initial, planValue, catalogue with
        | [], Some acceptedPlan, Some acceptedCatalogue ->
            let contract: QuintCompiledContractV2 =
                { Schema = QuintContractV2.schema
                  Profile = acceptedCatalogue.Profile
                  Specification = input.Metadata.Specification
                  Exports = acceptedCatalogue.Exports
                  Catalogue = acceptedCatalogue.Catalogue
                  ActionEffects = acceptedCatalogue.ActionEffects
                  Relationships = input.Metadata.Relationships
                  VerificationProfiles = input.Metadata.VerificationProfiles
                  Bounds = input.Metadata.Bounds
                  Impacts = input.Metadata.Impacts
                  Compatibility = input.Metadata.Compatibility
                  Digests = input.Metadata.Digests }

            match QuintContractV2.serializeCanonical contract with
            | Error findings ->
                findings
                |> List.map CompilerInternal.contractDiagnostic
                |> CompilerInternal.sorted
                |> Error
            | Ok canonicalContract ->
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

                match QuintBindingsV2.generate input.ModuleName contract with
                | Error findings ->
                    findings
                    |> List.map CompilerInternal.bindingDiagnostic
                    |> CompilerInternal.sorted
                    |> Error
                | Ok generatedBindings ->
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
                          Bindings = generatedBindings
                          Receipt = receipt
                          CanonicalReceipt = encodeReceipt receipt }
        | findings, _, _ -> Error findings
