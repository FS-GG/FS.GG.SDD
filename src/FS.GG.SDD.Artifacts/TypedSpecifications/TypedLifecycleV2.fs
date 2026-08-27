namespace FS.GG.SDD.Artifacts.TypedSpecifications

open System
open System.IO
open System.Globalization
open System.Text
open System.Text.Json

type QuintAuthorityArtifact =
    { Id: string
      Path: string
      Sha256: string }

type QuintAuthorityManifest =
    { SchemaVersion: int
      Lifecycle: string
      Backend: string
      ProfileIdentity: string
      ToolchainIdentity: string
      PackageIdentity: string
      Artifacts: QuintAuthorityArtifact list
      AuthoringAgent: string
      AuthoringSession: string
      RollbackManifestPath: string option
      RollbackManifestSha256: string option }

type QuintAuthorityArtifactState =
    | Missing
    | Unreadable of detail: string
    | Present of bytes: byte array

type QuintAuthorityArtifactObservation =
    { Path: string
      State: QuintAuthorityArtifactState }

type TypedAuthority =
    | FsharpSpecificationV1 of TypedAuthorityManifest
    | QuintSpecificationV1 of QuintAuthorityManifest

type QuintVerificationRung =
    | ProseOnly
    | StructuralTypecheck
    | TestAndSimulation
    | ModelCheck
    | FullCorpus

type QuintVerificationSelection =
    { ChangedPaths: string list
      Impacts: QuintImpact list }

[<RequireQualifiedAccess>]
module QuintSandbox =
    let contractBytes =
        Encoding.UTF8.GetBytes
            "{\"schema\":\"fsgg.quint.os-sandbox/v1\",\"platform\":\"linux-amd64\",\"primitive\":\"user-network-namespace\",\"launcher\":\"/usr/bin/unshare\",\"arguments\":[\"--user\",\"--map-root-user\",\"--net\",\"--\"]}\n"

[<RequireQualifiedAccess>]
module QuintV1Migration =
    let private diagnostic id message correction : TypedLifecycleDiagnostic =
        { Id = id
          Message = message
          Correction = correction }

    let lower (normalizedModel: byte array) (payloadSource: QuintSourceRange) (baseContract: QuintCompiledContract) =
        try
            let text = UTF8Encoding(false, true).GetString normalizedModel

            match SpecificationCodec.deserialize RequirementsExtension.contract text with
            | Error findings ->
                Error
                    [ diagnostic
                          "typedSdd.v2.migrationModelInvalid"
                          findings.Head.Message
                          "Restore the canonical valid manifest-v1 normalized model." ]
            | Ok model ->
                match SpecificationCodec.serialize RequirementsExtension.contract model with
                | Error findings ->
                    Error
                        [ diagnostic
                              "typedSdd.v2.migrationModelInvalid"
                              findings.Head.Message
                              "Restore the canonical valid manifest-v1 normalized model." ]
                | Ok canonical when Encoding.UTF8.GetBytes(canonical + "\n") <> normalizedModel ->
                    Error
                        [ diagnostic
                              "typedSdd.v2.migrationModelNonCanonical"
                              "The retained manifest-v1 semantic model is not canonical."
                              "Migrate only from a validated manifest-v1 authority." ]
                | Ok _ ->
                    let id value = SpecificationId.value value

                    let entry kind identifier =
                        { Id = id identifier
                          Kind = kind
                          Source = payloadSource }

                    let extension = model.Extension

                    let supplement =
                        [ yield entry ExternalSubject model.Identity
                          yield! extension.Scope |> List.map (fun row -> entry ExternalSubject row.Id)
                          yield! extension.NonGoals |> List.map (fun row -> entry ExternalSubject row.Id)
                          yield! extension.Stories |> List.map (fun row -> entry ExternalSubject row.Id)
                          yield! extension.Requirements |> List.map (fun row -> entry Requirement row.Id)
                          yield! extension.Acceptance |> List.map (fun row -> entry Invariant row.Id)
                          yield!
                              extension.Acceptance
                              |> List.map (fun row ->
                                  { Id = "Evaluate-" + id row.Id
                                    Kind = QuintCatalogueKind.Action
                                    Source = payloadSource })
                          yield! extension.Ambiguities |> List.map (fun row -> entry ExternalSubject row.Id)
                          yield! model.EvidenceObligations |> List.map (fun row -> entry Evidence row.Id) ]

                    let allEntries = baseContract.Catalogue @ supplement

                    let duplicates =
                        allEntries
                        |> List.countBy _.Id
                        |> List.filter (fun (_, count) -> count > 1)
                        |> List.map fst

                    if not (List.isEmpty duplicates) then
                        let names = String.concat ", " duplicates

                        Error
                            [ diagnostic
                                  "typedSdd.v2.migrationIdentityCollision"
                                  ($"Manifest-v1 identities collide with the qualified Quint catalogue: {names}.")
                                  "Rename colliding legacy identities before migration." ]
                    else
                        let relationship fromId kind toId =
                            { FromId = id fromId
                              Kind = kind
                              ToId = id toId }

                        let relationships =
                            [ for requirement in extension.Requirements do
                                  for acceptanceId in requirement.AcceptanceIds do
                                      yield relationship requirement.Id VerifiedBy acceptanceId

                                  for evidenceId in requirement.EvidenceObligationIds do
                                      yield relationship requirement.Id VerifiedBy evidenceId
                              for acceptance in extension.Acceptance do
                                  yield
                                      { FromId = "Evaluate-" + id acceptance.Id
                                        Kind = Requires
                                        ToId = id acceptance.Id }

                                  for storyId in acceptance.StoryIds do
                                      yield relationship acceptance.Id Requires storyId

                                  for requirementId in acceptance.RequirementIds do
                                      yield relationship acceptance.Id Requires requirementId ]
                            |> List.distinct
                            |> List.sortBy (fun row -> row.FromId, row.Kind, row.ToId)

                        let migrationEffects =
                            extension.Acceptance
                            |> List.map (fun acceptance ->
                                let subjects =
                                    (acceptance.StoryIds @ acceptance.RequirementIds)
                                    |> List.map id
                                    |> List.distinct
                                    |> List.sort

                                { ActionId = "Evaluate-" + id acceptance.Id
                                  Reads = subjects
                                  Writes = [ id acceptance.Id ]
                                  Subjects = subjects })

                        let compatibility surface requirement detail =
                            { Surface = surface
                              Requirement = requirement
                              Detail = detail }

                        let state =
                            function
                            | Open -> "open"
                            | Resolved -> "resolved"
                            | Deferred -> "deferred"

                        let compatibilityRows =
                            [ yield compatibility "v1/model" (id model.Identity) model.Intent
                              yield compatibility "v1/user-value" (id model.Identity) extension.UserValue
                              for row in extension.Scope do
                                  yield compatibility "v1/scope" (id row.Id) row.Statement
                              for row in extension.NonGoals do
                                  yield compatibility "v1/non-goal" (id row.Id) row.Statement
                              for row in extension.Stories do
                                  yield compatibility ("v1/story/" + row.Priority) (id row.Id) row.Statement
                              for row in extension.Requirements do
                                  yield compatibility "v1/requirement" (id row.Id) row.Statement
                              for row in extension.Acceptance do
                                  yield compatibility "v1/acceptance" (id row.Id) row.Statement
                              for row in extension.Ambiguities do
                                  yield
                                      compatibility
                                          ("v1/ambiguity/" + state row.State)
                                          (id row.Id)
                                          (row.Question + "\n" + (row.Decision |> Option.defaultValue ""))
                              for row in model.EvidenceObligations do
                                  yield compatibility ("v1/evidence/" + row.Kind) (id row.Id) row.Description
                              for index, value in extension.PublicImpact |> List.indexed do
                                  yield compatibility "v1/public-impact" ($"index-{index:D4}") value
                              for index, value in extension.LifecycleNotes |> List.indexed do
                                  yield compatibility "v1/lifecycle-note" ($"index-{index:D4}") value ]
                            |> List.sortBy (fun row -> row.Surface, row.Requirement, row.Detail)

                        let payloadSha = TypedAuthorityManifest.sha256 normalizedModel

                        Ok
                            { baseContract with
                                Catalogue = allEntries |> List.sortBy (fun row -> row.Id, row.Kind)
                                ActionEffects =
                                    (baseContract.ActionEffects @ migrationEffects) |> List.sortBy _.ActionId
                                Relationships =
                                    (baseContract.Relationships @ relationships)
                                    |> List.distinct
                                    |> List.sortBy (fun row -> row.FromId, row.Kind, row.ToId)
                                Impacts =
                                    ({ SubjectId = id model.Identity
                                       Category = "manifest-v1-semantic-payload"
                                       Detail = Convert.ToBase64String normalizedModel }
                                     :: baseContract.Impacts)
                                    |> List.sortBy (fun row -> row.SubjectId, row.Category, row.Detail)
                                Compatibility =
                                    (baseContract.Compatibility @ compatibilityRows)
                                    |> List.sortBy (fun row -> row.Surface, row.Requirement, row.Detail)
                                Digests =
                                    ({ Name = "requirements-extension-v1"
                                       Sha256 = payloadSha }
                                     :: baseContract.Digests)
                                    |> List.sortBy _.Name }
        with ex ->
            Error
                [ diagnostic
                      "typedSdd.v2.migrationModelInvalid"
                      ex.Message
                      "Restore the canonical valid manifest-v1 normalized model." ]

[<RequireQualifiedAccess>]
module TypedAuthority =
    let private diagnostic id message correction : TypedLifecycleDiagnostic =
        { Id = id
          Message = message
          Correction = correction }

    let private profile1ArtifactIds =
        set
            [ "markdown"
              "fence-manifest"
              "generated-modules"
              "source-map"
              "typed-effect"
              "sandbox-contract"
              "compiled-contract"
              "bindings"
              "compilation-receipt" ]

    let private requiredArtifactIds profile =
        if profile = QuintGeneralProfile.identity then
            Set.add "profile-bindings" profile1ArtifactIds
        else
            profile1ArtifactIds

    let private duplicatePropertyNames (element: JsonElement) =
        element.EnumerateObject()
        |> Seq.map _.Name
        |> Seq.countBy id
        |> Seq.filter (fun (_, count) -> count > 1)
        |> Seq.map fst
        |> Seq.sort
        |> Seq.toList

    let private safeRelativePath (value: string) =
        not (String.IsNullOrWhiteSpace value)
        && not (Path.IsPathRooted value)
        && value.Replace('\\', '/').Split('/')
           |> Array.forall (fun segment -> segment <> "" && segment <> "." && segment <> "..")

    let private isSha256 (value: string) =
        not (obj.ReferenceEquals(value, null))
        && value.Length = 64
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))

    let private utf8Strict (bytes: byte array) =
        try
            Ok(UTF8Encoding(false, true).GetString bytes)
        with :? DecoderFallbackException as ex ->
            Error ex.Message

    let private generatedModuleDigest target (moduleBytes: byte array) =
        let frame (value: string) =
            let valueBytes = Encoding.UTF8.GetBytes value

            Array.concat
                [ Encoding.ASCII.GetBytes(valueBytes.Length.ToString(CultureInfo.InvariantCulture) + ":")
                  valueBytes ]

        [ target
          TypedAuthorityManifest.sha256 moduleBytes
          moduleBytes.LongLength.ToString(CultureInfo.InvariantCulture) ]
        |> List.collect (frame >> Array.toList)
        |> List.toArray
        |> TypedAuthorityManifest.sha256

    let private semanticClosureV1 observations (manifest: QuintAuthorityManifest) =
        let artifacts =
            manifest.Artifacts
            |> List.map (fun artifact -> artifact.Id, artifact)
            |> Map.ofList

        let states =
            observations
            |> List.map (fun observation -> observation.Path, observation.State)
            |> Map.ofList

        let bytes id =
            artifacts
            |> Map.tryFind id
            |> Option.bind (fun artifact ->
                match Map.tryFind artifact.Path states with
                | Some(Present value) when TypedAuthorityManifest.sha256 value = artifact.Sha256 -> Some value
                | _ -> None)

        let text id =
            bytes id |> Option.bind (utf8Strict >> Result.toOption)

        let closure id message correction = diagnostic id message correction

        [ match text "compiled-contract" with
          | Some contractText ->
              match QuintContract.deserialize contractText with
              | Error _ ->
                  yield
                      closure
                          "typedSdd.v2.contractMalformed"
                          "The compiled-contract artifact is not valid closed-profile canonical JSON."
                          "Recompile the authority from the exact observed Quint output."
              | Ok contract ->
                  match QuintContract.serializeCanonical contract with
                  | Ok canonical when canonical = contractText -> ()
                  | _ ->
                      yield
                          closure
                              "typedSdd.v2.contractNonCanonical"
                              "The compiled-contract bytes are not the canonical serialization of their meaning."
                              "Regenerate the contract with the qualified compiler."
          | None -> ()

          match
              text "compilation-receipt",
              text "compiled-contract",
              bytes "fence-manifest",
              text "generated-modules",
              bytes "source-map",
              bytes "markdown",
              text "typed-effect",
              bytes "sandbox-contract",
              text "bindings"
          with
          | Some receiptText,
            Some contractText,
            Some fenceBytes,
            Some modulesText,
            Some sourceMapBytes,
            Some markdownBytes,
            Some typedEffectText,
            Some sandboxBytes,
            Some bindingsText ->
              try
                  use receiptDocument = JsonDocument.Parse receiptText
                  let receipt = receiptDocument.RootElement

                  let required =
                      set
                          [ "schema"
                            "sourceSha256"
                            "fenceManifestSha256"
                            "generatedModulesSha256"
                            "toolchainSha256"
                            "typedEffectSha256"
                            "contractSha256"
                            "compilationFingerprint"
                            "processSteps" ]

                  let names = receipt.EnumerateObject() |> Seq.map _.Name |> Seq.toList
                  let fields = Set.ofList names

                  if
                      receipt.ValueKind <> JsonValueKind.Object
                      || fields <> required
                      || names.Length <> required.Count
                  then
                      yield
                          closure
                              "typedSdd.v2.receiptMalformed"
                              "The compilation receipt does not match its closed schema."
                              "Regenerate the receipt with the qualified compiler."
                  else
                      let read (name: string) =
                          receipt.GetProperty(name).GetString() |> Option.ofObj |> Option.defaultValue ""

                      let schema = read "schema"
                      let sourceSha = read "sourceSha256"
                      let fenceSha = read "fenceManifestSha256"
                      let modulesSha = read "generatedModulesSha256"
                      let toolchainSha = read "toolchainSha256"
                      let typedEffectSha = read "typedEffectSha256"
                      let contractSha = read "contractSha256"
                      let fingerprint = read "compilationFingerprint"
                      let processSteps = receipt.GetProperty("processSteps")

                      let processStepValues =
                          if processSteps.ValueKind = JsonValueKind.Array then
                              processSteps.EnumerateArray()
                              |> Seq.choose (fun item ->
                                  if item.ValueKind = JsonValueKind.String then
                                      item.GetString() |> Option.ofObj
                                  else
                                      None)
                              |> Seq.toList
                          else
                              []

                      let digests =
                          [ sourceSha
                            fenceSha
                            modulesSha
                            toolchainSha
                            typedEffectSha
                            contractSha
                            fingerprint ]

                      if
                          schema <> QuintCompiler.receiptSchema
                          || processSteps.ValueKind <> JsonValueKind.Array
                          || (processSteps.EnumerateArray()
                              |> Seq.exists (fun item -> item.ValueKind <> JsonValueKind.String))
                          || processStepValues <> [ "extract"; "typecheck" ]
                          || digests |> List.exists (isSha256 >> not)
                      then
                          yield
                              closure
                                  "typedSdd.v2.receiptMalformed"
                                  "The compilation receipt contains an unsupported schema or malformed value."
                                  "Regenerate the receipt with the qualified compiler."
                      else
                          let contractHash =
                              TypedAuthorityManifest.sha256 (Encoding.UTF8.GetBytes contractText)

                          let fenceHash = TypedAuthorityManifest.sha256 fenceBytes
                          let sourceHash = TypedAuthorityManifest.sha256 markdownBytes

                          match QuintContract.deserialize contractText with
                          | Error _ -> ()
                          | Ok contract ->
                              let catalogueName kind =
                                  match kind with
                                  | Requirement -> "requirements"
                                  | Evidence -> "evidenceCatalogue"
                                  | Action -> "actionCatalogue"
                                  | Invariant
                                  | TemporalProperty
                                  | ReachabilityProperty -> "propertyCatalogue"
                                  | StateVariable -> "stateCatalogue"
                                  | Implementation -> "implementationCatalogue"
                                  | ExternalSubject -> "externalSubjectCatalogue"

                              let typedObservation =
                                  let migration =
                                      contract.Impacts
                                      |> List.exists (fun impact -> impact.Category = "manifest-v1-semantic-payload")

                                  let q1Ids =
                                      set
                                          [ "REQ-AUDIT-001"
                                            "EV-VERIFY-001"
                                            "ObserveEvidence"
                                            "AcceptRequirement"
                                            "AcceptedOnlyWithEvidence"
                                            "RequirementCanBeAccepted" ]

                                  { Profile = manifest.ProfileIdentity
                                    QuintVersion = QuintProfile.quintVersion
                                    TypedEffectJson = typedEffectText
                                    SourceBindings =
                                      contract.Catalogue
                                      |> List.filter (fun entry -> not migration || q1Ids.Contains entry.Id)
                                      |> List.map (fun entry ->
                                          { ModuleName = "RequirementsSlice"
                                            CatalogueName = catalogueName entry.Kind
                                            Id = entry.Id
                                            Kind = entry.Kind
                                            Source = entry.Source }) }

                              let migrationImpacts =
                                  contract.Impacts
                                  |> List.filter (fun impact -> impact.Category = "manifest-v1-semantic-payload")

                              let mutable adaptedCatalogue = None

                              match QuintProfile.adaptTypedEffectJson typedObservation with
                              | Ok adapted when
                                  adapted.Profile = contract.Profile
                                  && ((adapted.Entries = contract.Catalogue
                                       && adapted.ActionEffects = contract.ActionEffects)
                                      || migrationImpacts.Length = 1)
                                  ->
                                  adaptedCatalogue <- Some adapted
                              | _ ->
                                  yield
                                      closure
                                          "typedSdd.v2.typedEffectClosure"
                                          "The retained Quint typed/effect observation does not adapt to the compiled contract catalogue and action effects."
                                          "Recompile from the exact qualified Quint observation and source bindings."

                              let typedDigest =
                                  { Name = "typed-effect"
                                    Sha256 = typedEffectSha }

                              let sandboxDigest =
                                  { Name = "sandbox-contract"
                                    Sha256 = TypedAuthorityManifest.sha256 sandboxBytes }

                              if sandboxBytes <> QuintSandbox.contractBytes then
                                  yield
                                      closure
                                          "typedSdd.v2.sandboxContract"
                                          "The recorded OS sandbox contract is not the qualified Linux user/network namespace contract."
                                          "Re-author with the qualified sandbox effect edge."

                              match migrationImpacts with
                              | [] when contract.Impacts = [] && contract.Digests = [ sandboxDigest; typedDigest ] -> ()
                              | [ impact ] when contract.Impacts = [ impact ] ->
                                  try
                                      let payload = Convert.FromBase64String impact.Detail
                                      let payloadSha = TypedAuthorityManifest.sha256 payload

                                      let expectedDigests =
                                          [ { Name = "requirements-extension-v1"
                                              Sha256 = payloadSha }
                                            sandboxDigest
                                            typedDigest ]

                                      let marker = "fsgg.requirements-extension/v1+base64 " + impact.Detail + "\n"
                                      let markdownText = UTF8Encoding(false, true).GetString markdownBytes
                                      let markdownLines = markdownText.Split('\n')

                                      let payloadLine =
                                          markdownLines
                                          |> Array.findIndex (fun line -> line = marker.TrimEnd('\n'))
                                          |> (+) 1

                                      let payloadRange =
                                          { Path = artifacts["markdown"].Path
                                            Start = { Line = payloadLine; Column = 1 }
                                            End =
                                              { Line = payloadLine
                                                Column = markdownLines[payloadLine - 1].Length } }

                                      let rollbackBindsPayload =
                                          match manifest.RollbackManifestPath with
                                          | Some rollbackPath ->
                                              match Map.tryFind rollbackPath states with
                                              | Some(Present rollbackBytes) ->
                                                  use inventory = JsonDocument.Parse rollbackBytes

                                                  inventory.RootElement.GetProperty("entries").EnumerateArray()
                                                  |> Seq.exists (fun entry ->
                                                      let original =
                                                          entry.GetProperty("originalPath").GetString()
                                                          |> Option.ofObj
                                                          |> Option.defaultValue ""

                                                      let digest =
                                                          entry.GetProperty("sha256").GetString()
                                                          |> Option.ofObj
                                                          |> Option.defaultValue ""

                                                      original.EndsWith(
                                                          "specification.normalized.json",
                                                          StringComparison.Ordinal
                                                      )
                                                      && digest = payloadSha)
                                              | _ -> false
                                          | None -> false

                                      let loweringMatches =
                                          match adaptedCatalogue with
                                          | Some adapted ->
                                              let baseContract: QuintCompiledContract =
                                                  { contract with
                                                      Catalogue = adapted.Entries
                                                      ActionEffects = adapted.ActionEffects
                                                      Relationships = []
                                                      Impacts = []
                                                      Compatibility = []
                                                      Digests = [ sandboxDigest; typedDigest ] }

                                              match QuintV1Migration.lower payload payloadRange baseContract with
                                              | Ok expected ->
                                                  QuintContract.serializeCanonical expected = Ok contractText
                                              | Error _ -> false
                                          | None -> false

                                      if
                                          contract.Digests <> expectedDigests
                                          || not (markdownText.EndsWith(marker, StringComparison.Ordinal))
                                          || not rollbackBindsPayload
                                          || not loweringMatches
                                      then
                                          yield
                                              closure
                                                  "typedSdd.v2.migrationCorrespondence"
                                                  "The migrated semantic payload is not identically bound by Markdown, compiled-contract digest, and authenticated rollback inventory."
                                                  "Re-run migration from the complete canonical manifest-v1 authority."
                                  with _ ->
                                      yield
                                          closure
                                              "typedSdd.v2.migrationCorrespondence"
                                              "The migrated semantic payload is malformed or not authenticated by the rollback inventory."
                                              "Re-run migration from the complete canonical manifest-v1 authority."
                              | _ ->
                                  yield
                                      closure
                                          "typedSdd.v2.migrationCorrespondence"
                                          "The compiled contract has an unsupported migration impact or semantic digest inventory."
                                          "Retain either no migration payload or exactly one authenticated manifest-v1 semantic payload."

                              let expectedFingerprint =
                                  QuintContract.fingerprint
                                      { SourceSha256 = sourceSha
                                        FenceManifestSha256 = fenceSha
                                        GeneratedModulesSha256 = modulesSha
                                        ToolchainSha256 = toolchainSha
                                        Contract = contract }

                              if
                                  sourceSha <> sourceHash
                                  || toolchainSha <> manifest.ToolchainIdentity
                                  || contractSha <> contractHash
                                  || TypedAuthorityManifest.sha256 (Encoding.UTF8.GetBytes typedEffectText)
                                     <> typedEffectSha
                                  || expectedFingerprint <> Ok fingerprint
                              then
                                  yield
                                      closure
                                          "typedSdd.v2.receiptClosure"
                                          "The receipt does not close over the declared source, fences, modules, toolchain, contract, and fingerprint."
                                          "Re-author all authority artifacts in one atomic compilation."

                              match
                                  QuintSource.createMarkdown artifacts["markdown"].Path markdownBytes,
                                  QuintSource.decodeFenceManifest fenceBytes,
                                  QuintSource.decodeSourceMap sourceMapBytes
                              with
                              | Ok source, Ok fenceManifest, Ok sourceMap ->
                                  let sourceFindings =
                                      QuintSource.validateManifest source fenceManifest
                                      @ QuintSource.validateSourceMap source fenceManifest sourceMap

                                  let targets = fenceManifest.Fences |> List.map _.Target |> List.distinct
                                  let sourceLines = source.Text.Split('\n')

                                  let extracted =
                                      try
                                          fenceManifest.Fences
                                          |> List.sortBy _.Ordinal
                                          |> List.map (fun fence ->
                                              let contentLines =
                                                  sourceLines[fence.SourceRange.Start.Line .. fence.SourceRange.End.Line
                                                                                              - 2]

                                              let content = String.Join("\n", contentLines) + "\n"

                                              fence.Target,
                                              content,
                                              TypedAuthorityManifest.sha256 (Encoding.UTF8.GetBytes content))
                                          |> Ok
                                      with ex ->
                                          Error ex.Message

                                  let contentDigestsMatch =
                                      match extracted with
                                      | Error _ -> false
                                      | Ok values ->
                                          List.zip values (fenceManifest.Fences |> List.sortBy _.Ordinal)
                                          |> List.forall (fun ((_, _, digest), fence) -> digest = fence.ContentSha256)

                                  if
                                      not (List.isEmpty sourceFindings)
                                      || QuintSource.encodeFenceManifest fenceManifest <> fenceBytes
                                      || QuintSource.encodeSourceMap sourceMap <> sourceMapBytes
                                      || targets.Length <> 1
                                      || not contentDigestsMatch
                                  then
                                      yield
                                          closure
                                              "typedSdd.v2.sourceMapClosure"
                                              "The source, fence manifest, and source map do not form one canonical closed mapping."
                                              "Regenerate all source projections from the same Markdown source."
                                  else
                                      let actualModulesSha =
                                          generatedModuleDigest targets.Head (Encoding.UTF8.GetBytes modulesText)

                                      let extractedModule =
                                          extracted
                                          |> Result.map (List.map (fun (_, content, _) -> content) >> String.concat "")

                                      if actualModulesSha <> modulesSha || extractedModule <> Ok modulesText then
                                          yield
                                              closure
                                                  "typedSdd.v2.modulesClosure"
                                                  "The generated module bytes do not bind the compilation receipt."
                                                  "Regenerate modules and receipt in the same compilation."
                              | _ ->
                                  yield
                                      closure
                                          "typedSdd.v2.sourceMapClosure"
                                          "The source, fence manifest, or source map is malformed."
                                          "Regenerate all source projections from the same Markdown source."

                              match QuintBindings.generate "RequirementsBindings" contract with
                              | Ok bindings when bindings.FSharpSource = bindingsText -> ()
                              | _ ->
                                  yield
                                      closure
                                          "typedSdd.v2.bindingsClosure"
                                          "Generated bindings do not identify the declared compiled contract."
                                          "Regenerate bindings from the same compiled contract."
              with _ ->
                  yield
                      closure
                          "typedSdd.v2.receiptMalformed"
                          "The compilation receipt is not valid closed-schema JSON."
                          "Regenerate the receipt with the qualified compiler."
          | _ -> () ]

    let private semanticClosureGeneral observations (manifest: QuintAuthorityManifest) =
        let artifacts =
            manifest.Artifacts |> List.map (fun item -> item.Id, item) |> Map.ofList

        let states =
            observations |> List.map (fun item -> item.Path, item.State) |> Map.ofList

        let bytes id =
            artifacts
            |> Map.tryFind id
            |> Option.bind (fun artifact ->
                match Map.tryFind artifact.Path states with
                | Some(Present value) when TypedAuthorityManifest.sha256 value = artifact.Sha256 -> Some value
                | _ -> None)

        let text id =
            bytes id |> Option.bind (utf8Strict >> Result.toOption)

        let fingerprint sourceSha fenceSha modulesSha toolchainSha contractText =
            let frame (value: string) =
                let valueBytes = Encoding.UTF8.GetBytes value

                Encoding.ASCII.GetBytes(valueBytes.Length.ToString(CultureInfo.InvariantCulture) + ":")
                |> fun prefix -> Array.append prefix valueBytes

            [ "fsgg.quint.compilation-fingerprint/v2"
              sourceSha
              fenceSha
              modulesSha
              toolchainSha
              contractText ]
            |> List.map frame
            |> Array.concat
            |> TypedAuthorityManifest.sha256

        [ match text "compiled-contract", text "profile-bindings", text "typed-effect", text "bindings" with
          | Some contractText, Some selectorText, Some typedEffectText, Some bindingText ->
              match QuintContractV2.deserialize contractText, QuintGeneralBindingManifest.deserialize selectorText with
              | Ok contract, Ok selectors ->
                  if QuintContractV2.serializeCanonical contract <> Ok contractText then
                      yield
                          diagnostic
                              "typedSdd.v2.contractNonCanonical"
                              "The profile-2 compiled contract is not canonical."
                              "Regenerate it with the qualified compiler."

                  let observation =
                      { Profile = manifest.ProfileIdentity
                        QuintVersion = QuintGeneralProfile.quintVersion
                        TypedEffectJson = typedEffectText
                        ExportBindings = selectors.Exports
                        ActionBindings = selectors.Actions }

                  match QuintGeneralProfile.adaptTypedEffectJson observation with
                  | Ok adapted when
                      adapted.Profile = contract.Profile
                      && adapted.Exports = contract.Exports
                      && adapted.Catalogue = contract.Catalogue
                      && adapted.ActionEffects = contract.ActionEffects
                      ->
                      ()
                  | _ ->
                      yield
                          diagnostic
                              "typedSdd.v2.typedEffectClosure"
                              "The retained profile-2 selectors and typed/effect observation do not reproduce the compiled contract."
                              "Recompile the contract and selector artifact from the same Quint source."

                  match QuintBindingsV2.generate selectors.ModuleName contract with
                  | Ok generated when generated.FSharpSource = bindingText -> ()
                  | _ ->
                      yield
                          diagnostic
                              "typedSdd.v2.bindingsClosure"
                              "Generated profile-2 bindings do not reproduce the retained bindings artifact."
                              "Regenerate bindings from the same compiled contract."
              | _ ->
                  yield
                      diagnostic
                          "typedSdd.v2.contractMalformed"
                          "The profile-2 contract or selector manifest is malformed."
                          "Regenerate both artifacts with the qualified compiler."
          | _ -> ()

          match
              text "compilation-receipt",
              text "compiled-contract",
              bytes "markdown",
              bytes "fence-manifest",
              text "generated-modules",
              text "typed-effect"
          with
          | Some receiptText,
            Some contractText,
            Some markdownBytes,
            Some fenceBytes,
            Some modulesText,
            Some typedEffectText ->
              try
                  use document = JsonDocument.Parse receiptText
                  let root = document.RootElement

                  let read (name: string) =
                      root.GetProperty(name).GetString() |> Option.ofObj |> Option.defaultValue ""

                  let sourceSha = read "sourceSha256"
                  let fenceSha = read "fenceManifestSha256"
                  let modulesSha = read "generatedModulesSha256"
                  let toolchainSha = read "toolchainSha256"
                  let typedSha = read "typedEffectSha256"
                  let contractSha = read "contractSha256"
                  let compilationSha = read "compilationFingerprint"

                  if
                      read "schema" <> QuintCompiler.generalReceiptSchema
                      || sourceSha <> TypedAuthorityManifest.sha256 markdownBytes
                      || fenceSha <> TypedAuthorityManifest.sha256 fenceBytes
                      || toolchainSha <> manifest.ToolchainIdentity
                      || typedSha
                         <> TypedAuthorityManifest.sha256 (Encoding.UTF8.GetBytes typedEffectText)
                      || contractSha
                         <> TypedAuthorityManifest.sha256 (Encoding.UTF8.GetBytes contractText)
                      || compilationSha
                         <> fingerprint sourceSha fenceSha modulesSha toolchainSha contractText
                  then
                      yield
                          diagnostic
                              "typedSdd.v2.receiptClosure"
                              "The profile-2 receipt does not close over its source, toolchain, typed effects, and contract."
                              "Re-author all profile-2 artifacts atomically."

                  match QuintSource.decodeFenceManifest fenceBytes with
                  | Ok fenceManifest ->
                      let targets = fenceManifest.Fences |> List.map _.Target |> List.distinct

                      if
                          targets.Length <> 1
                          || generatedModuleDigest targets.Head (Encoding.UTF8.GetBytes modulesText)
                             <> modulesSha
                      then
                          yield
                              diagnostic
                                  "typedSdd.v2.modulesClosure"
                                  "The generated profile-2 module bytes do not bind the compilation receipt."
                                  "Regenerate modules and receipt together."
                  | Error _ ->
                      yield
                          diagnostic
                              "typedSdd.v2.sourceMapClosure"
                              "The profile-2 fence manifest is malformed."
                              "Regenerate the source projections."
              with _ ->
                  yield
                      diagnostic
                          "typedSdd.v2.receiptMalformed"
                          "The profile-2 compilation receipt is malformed."
                          "Regenerate it with the qualified compiler."
          | _ -> ()

          match bytes "sandbox-contract" with
          | Some value when value <> QuintSandbox.contractBytes ->
              yield
                  diagnostic
                      "typedSdd.v2.sandboxContract"
                      "The retained sandbox contract is not the qualified contract."
                      "Re-author with the qualified sandbox edge."
          | _ -> () ]

    let private semanticClosure observations manifest =
        if manifest.ProfileIdentity = QuintGeneralProfile.identity then
            semanticClosureGeneral observations manifest
        else
            semanticClosureV1 observations manifest

    let serializeQuintV2 manifest =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion)
        writer.WriteString("lifecycle", manifest.Lifecycle)
        writer.WriteString("backend", manifest.Backend)
        writer.WriteString("profileIdentity", manifest.ProfileIdentity)
        writer.WriteString("toolchainIdentity", manifest.ToolchainIdentity)
        writer.WriteString("packageIdentity", manifest.PackageIdentity)
        writer.WriteStartArray("artifacts")

        manifest.Artifacts
        |> List.sortBy _.Id
        |> List.iter (fun artifact ->
            writer.WriteStartObject()
            writer.WriteString("id", artifact.Id)
            writer.WriteString("path", artifact.Path)
            writer.WriteString("sha256", artifact.Sha256)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteString("authoringAgent", manifest.AuthoringAgent)
        writer.WriteString("authoringSession", manifest.AuthoringSession)

        match manifest.RollbackManifestPath with
        | Some value -> writer.WriteString("rollbackManifestPath", value)
        | None -> writer.WriteNull("rollbackManifestPath")

        match manifest.RollbackManifestSha256 with
        | Some value -> writer.WriteString("rollbackManifestSha256", value)
        | None -> writer.WriteNull("rollbackManifestSha256")

        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"

    let private readString (root: JsonElement) (name: string) =
        match root.TryGetProperty name with
        | true, value when value.ValueKind = JsonValueKind.String ->
            match value.GetString() |> Option.ofObj with
            | Some text when not (String.IsNullOrWhiteSpace text) -> Ok text
            | _ ->
                Error(
                    diagnostic
                        "typedSdd.v2.manifestMalformed"
                        $"Field '{name}' is empty."
                        "Regenerate the manifest-v2 authority."
                )
        | _ ->
            Error(
                diagnostic
                    "typedSdd.v2.manifestMalformed"
                    $"Missing string field '{name}'."
                    "Regenerate the manifest-v2 authority."
            )

    let private readOptionalString (root: JsonElement) (name: string) =
        match root.TryGetProperty name with
        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() |> Option.ofObj |> Ok
        | true, value when value.ValueKind = JsonValueKind.Null -> Ok None
        | false, _ ->
            Error(
                diagnostic
                    "typedSdd.v2.manifestMalformed"
                    $"Missing field '{name}'."
                    "Regenerate the manifest-v2 authority."
            )
        | _ ->
            Error(
                diagnostic
                    "typedSdd.v2.manifestMalformed"
                    $"Field '{name}' must be a string or null."
                    "Regenerate the manifest-v2 authority."
            )

    let private requiredItemString (item: JsonElement) (name: string) =
        match item.GetProperty(name).GetString() |> Option.ofObj with
        | Some value when not (String.IsNullOrWhiteSpace value) -> value
        | _ -> raise (FormatException($"Artifact field '{name}' is empty."))

    let private decodeV2 (root: JsonElement) =
        let allowed =
            set
                [ "schemaVersion"
                  "lifecycle"
                  "backend"
                  "profileIdentity"
                  "toolchainIdentity"
                  "packageIdentity"
                  "artifacts"
                  "authoringAgent"
                  "authoringSession"
                  "rollbackManifestPath"
                  "rollbackManifestSha256" ]

        let duplicates = duplicatePropertyNames root

        let unknown =
            root.EnumerateObject()
            |> Seq.map _.Name
            |> Seq.filter (allowed.Contains >> not)
            |> Seq.sort
            |> Seq.toList

        if not (List.isEmpty duplicates) then
            let names = String.concat ", " duplicates

            Error(
                diagnostic
                    "typedSdd.v2.manifestDuplicateField"
                    $"Manifest-v2 contains duplicate fields: {names}."
                    "Regenerate the canonical manifest-v2 authority."
            )
        elif not (List.isEmpty unknown) then
            let names = String.concat ", " unknown

            Error(
                diagnostic
                    "typedSdd.v2.manifestUnknownField"
                    $"Manifest-v2 contains unknown fields: {names}."
                    "Remove unknown fields or upgrade the CLI explicitly."
            )
        else
            let artifactsResult =
                match root.TryGetProperty "artifacts" with
                | true, value when value.ValueKind = JsonValueKind.Array ->
                    try
                        value.EnumerateArray()
                        |> Seq.map (fun item ->
                            let itemAllowed = set [ "id"; "path"; "sha256" ]
                            let itemDuplicates = duplicatePropertyNames item

                            let itemUnknown =
                                item.EnumerateObject()
                                |> Seq.map _.Name
                                |> Seq.filter (itemAllowed.Contains >> not)
                                |> Seq.toList

                            if not (List.isEmpty itemDuplicates) then
                                raise (FormatException("Artifact contains duplicate fields."))
                            elif not (List.isEmpty itemUnknown) then
                                raise (FormatException("Artifact contains unknown fields."))

                            { Id = requiredItemString item "id"
                              Path = requiredItemString item "path"
                              Sha256 = requiredItemString item "sha256" })
                        |> Seq.toList
                        |> Ok
                    with ex ->
                        Error(
                            diagnostic
                                "typedSdd.v2.manifestMalformed"
                                ex.Message
                                "Regenerate the manifest-v2 authority."
                        )
                | _ ->
                    Error(
                        diagnostic
                            "typedSdd.v2.manifestMalformed"
                            "Missing array field 'artifacts'."
                            "Regenerate the manifest-v2 authority."
                    )

            match
                readString root "lifecycle",
                readString root "backend",
                readString root "profileIdentity",
                readString root "toolchainIdentity",
                readString root "packageIdentity",
                artifactsResult,
                readString root "authoringAgent",
                readString root "authoringSession",
                readOptionalString root "rollbackManifestPath",
                readOptionalString root "rollbackManifestSha256"
            with
            | Ok lifecycle,
              Ok backend,
              Ok profile,
              Ok toolchain,
              Ok package,
              Ok artifacts,
              Ok agent,
              Ok session,
              Ok rollbackPath,
              Ok rollbackSha ->
                if lifecycle <> "typed-sdd" || backend <> "quint-specification-v1" then
                    Error(
                        diagnostic
                            "typedSdd.v2.wrongAuthority"
                            "Manifest-v2 does not explicitly select typed-sdd/quint-specification-v1."
                            "Regenerate the authority with the declared Quint backend."
                    )
                else
                    Ok(
                        QuintSpecificationV1
                            { SchemaVersion = 2
                              Lifecycle = lifecycle
                              Backend = backend
                              ProfileIdentity = profile
                              ToolchainIdentity = toolchain
                              PackageIdentity = package
                              Artifacts = artifacts
                              AuthoringAgent = agent
                              AuthoringSession = session
                              RollbackManifestPath = rollbackPath
                              RollbackManifestSha256 = rollbackSha }
                    )
            | results ->
                let firstError =
                    [ match results with
                      | Error error, _, _, _, _, _, _, _, _, _ -> yield error
                      | _, Error error, _, _, _, _, _, _, _, _ -> yield error
                      | _, _, Error error, _, _, _, _, _, _, _ -> yield error
                      | _, _, _, Error error, _, _, _, _, _, _ -> yield error
                      | _, _, _, _, Error error, _, _, _, _, _ -> yield error
                      | _, _, _, _, _, Error error, _, _, _, _ -> yield error
                      | _, _, _, _, _, _, Error error, _, _, _ -> yield error
                      | _, _, _, _, _, _, _, Error error, _, _ -> yield error
                      | _, _, _, _, _, _, _, _, Error error, _ -> yield error
                      | _, _, _, _, _, _, _, _, _, Error error -> yield error
                      | _ -> () ]
                    |> List.head

                Error firstError

    let deserialize (text: string) =
        let legacyMalformed () =
            match TypedAuthorityManifest.deserialize text with
            | Error finding -> Error finding
            | Ok manifest -> Ok(FsharpSpecificationV1 manifest)

        try
            use document = JsonDocument.Parse text
            let root = document.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                Error(
                    diagnostic
                        "typedSdd.v2.manifestMalformed"
                        "Authority manifest must be a JSON object."
                        "Regenerate the authority manifest."
                )
            else
                match root.TryGetProperty "schemaVersion" with
                | true, value when value.ValueKind = JsonValueKind.Number && value.GetInt32() = 1 ->
                    let allowed =
                        set
                            [ "schemaVersion"
                              "lifecycle"
                              "backend"
                              "compilerIdentity"
                              "packageIdentity"
                              "extensionIdentity"
                              "canonicalPath"
                              "canonicalSha256"
                              "normalizedPath"
                              "normalizedSha256"
                              "markdownPath"
                              "markdownSha256"
                              "authoringAgent"
                              "authoringSession"
                              "rollbackSourceSha256" ]

                    let duplicates = duplicatePropertyNames root

                    let unknown =
                        root.EnumerateObject()
                        |> Seq.map _.Name
                        |> Seq.filter (allowed.Contains >> not)
                        |> Seq.toList

                    if not (List.isEmpty duplicates) then
                        Error(
                            diagnostic
                                "typedSdd.authorityDuplicateField"
                                "Manifest-v1 contains duplicate fields."
                                "Restore the canonical manifest-v1 authority."
                        )
                    elif not (List.isEmpty unknown) then
                        Error(
                            diagnostic
                                "typedSdd.authorityUnknownField"
                                "Manifest-v1 contains unknown fields."
                                "Remove unknown fields or upgrade explicitly."
                        )
                    else
                        TypedAuthorityManifest.deserialize text
                        |> Result.bind (fun manifest ->
                            if manifest.Lifecycle = "typed-sdd" && manifest.Backend = "fsharp-specification-v1" then
                                Ok(FsharpSpecificationV1 manifest)
                            else
                                Error(
                                    diagnostic
                                        "typedSdd.wrongLifecycle"
                                        "Manifest-v1 does not explicitly select typed-sdd/fsharp-specification-v1."
                                        "Restore the declared v1 authority backend."
                                ))
                | true, value when value.ValueKind = JsonValueKind.Number && value.GetInt32() = 2 -> decodeV2 root
                | true, value when value.ValueKind = JsonValueKind.Number ->
                    Error(
                        diagnostic
                            "typedSdd.authorityCompatibility"
                            $"Authority schema {value.GetInt32()} is unsupported."
                            "Upgrade the CLI or use manifest schemaVersion 1 or 2."
                    )
                | _ -> legacyMalformed ()
        with _ ->
            legacyMalformed ()

    let validateQuintV2 expectedPackageIdentity observations manifest =
        let observationMap =
            observations
            |> List.map (fun observation -> observation.Path, observation.State)
            |> Map.ofList

        let offeredIds = manifest.Artifacts |> List.map _.Id

        let duplicates =
            offeredIds
            |> List.countBy id
            |> List.filter (fun (_, count) -> count > 1)
            |> List.map fst

        let duplicatePaths =
            manifest.Artifacts
            |> List.countBy _.Path
            |> List.filter (fun (_, count) -> count > 1)
            |> List.map fst

        let duplicateObservations =
            observations
            |> List.countBy _.Path
            |> List.filter (fun (_, count) -> count > 1)
            |> List.map fst

        let offered = Set.ofList offeredIds

        [ if
              manifest.SchemaVersion <> 2
              || manifest.Lifecycle <> "typed-sdd"
              || manifest.Backend <> "quint-specification-v1"
          then
              yield
                  diagnostic
                      "typedSdd.v2.wrongAuthority"
                      "Manifest does not select typed-sdd/quint-specification-v1 schema v2."
                      "Use the explicit manifest-v2 Quint backend."
          if
              manifest.ProfileIdentity <> QuintProfile.identity
              && manifest.ProfileIdentity <> QuintGeneralProfile.identity
          then
              yield
                  diagnostic
                      "typedSdd.v2.profileIdentityMismatch"
                      $"Profile identity '{manifest.ProfileIdentity}' is unsupported."
                      $"Use {QuintProfile.identity} or {QuintGeneralProfile.identity}."
          let expectedToolchain =
              if manifest.ProfileIdentity = QuintGeneralProfile.identity then
                  QuintToolchain.general
              else
                  QuintToolchain.q1

          if manifest.ToolchainIdentity <> QuintToolchain.fingerprint expectedToolchain then
              yield
                  diagnostic
                      "typedSdd.v2.toolchainIdentityMismatch"
                      "Toolchain identity differs from the Q1-qualified manifest."
                      "Provision and select the exact Q1/Q2 toolchain cache."

          if manifest.PackageIdentity <> expectedPackageIdentity then
              yield
                  diagnostic
                      "typedSdd.v2.packageIdentityMismatch"
                      "Authority package identity differs from the installed producer."
                      "Install the exact recorded coherent package set."

          if
              String.IsNullOrWhiteSpace manifest.AuthoringAgent
              || String.IsNullOrWhiteSpace manifest.AuthoringSession
          then
              yield
                  diagnostic
                      "typedSdd.v2.authoringReceiptMissing"
                      "Manifest-v2 lacks a complete authoring receipt."
                      "Re-author with --agent and --session."

          if not (List.isEmpty duplicates) then
              let names = String.concat ", " duplicates

              yield
                  diagnostic
                      "typedSdd.v2.artifactDuplicate"
                      $"Duplicate artifact ids: {names}."
                      "Keep exactly one entry for each required artifact."

          if not (List.isEmpty duplicatePaths) then
              yield
                  diagnostic
                      "typedSdd.v2.artifactPathAlias"
                      "Distinct artifact roles share a path."
                      "Give every required artifact one distinct canonical path."

          if not (List.isEmpty duplicateObservations) then
              yield
                  diagnostic
                      "typedSdd.v2.observationDuplicate"
                      "The effect edge supplied duplicate path observations."
                      "Observe every declared path exactly once."

          let requiredArtifactIds = requiredArtifactIds manifest.ProfileIdentity

          if offered <> requiredArtifactIds then
              let missing = Set.difference requiredArtifactIds offered |> String.concat ", "
              let extra = Set.difference offered requiredArtifactIds |> String.concat ", "

              yield
                  diagnostic
                      "typedSdd.v2.artifactInventory"
                      $"Artifact inventory differs (missing: {missing}; extra: {extra})."
                      "Regenerate the complete closed manifest-v2 inventory."

          for artifact in manifest.Artifacts |> List.sortBy _.Id do
              if not (safeRelativePath artifact.Path) then
                  yield
                      diagnostic
                          "typedSdd.v2.artifactPath"
                          $"Artifact '{artifact.Id}' has unsafe path '{artifact.Path}'."
                          "Use one contained project-relative path."
              elif not (isSha256 artifact.Sha256) then
                  yield
                      diagnostic
                          "typedSdd.v2.artifactDigest"
                          $"Artifact '{artifact.Id}' has an invalid SHA-256."
                          "Regenerate the manifest from staged bytes."
              else
                  match Map.tryFind artifact.Path observationMap with
                  | None
                  | Some Missing ->
                      yield
                          diagnostic
                              "typedSdd.v2.artifactMissing"
                              $"Artifact '{artifact.Id}' is missing."
                              "Restore or re-author the complete v2 authority."
                  | Some(Unreadable detail) ->
                      yield
                          diagnostic
                              "typedSdd.v2.artifactUnreadable"
                              $"Artifact '{artifact.Id}' is unreadable: {detail}"
                              "Correct filesystem access and inspect again."
                  | Some(Present bytes) when TypedAuthorityManifest.sha256 bytes <> artifact.Sha256 ->
                      yield
                          diagnostic
                              "typedSdd.v2.artifactMismatch"
                              $"Artifact '{artifact.Id}' differs from its manifest digest."
                              "Re-author instead of editing generated authority artifacts."
                  | _ -> ()

          match manifest.RollbackManifestPath, manifest.RollbackManifestSha256 with
          | None, None -> ()
          | Some path, Some sha when safeRelativePath path && isSha256 sha ->
              match Map.tryFind path observationMap with
              | Some(Present bytes) when TypedAuthorityManifest.sha256 bytes = sha -> ()
              | Some(Present _) ->
                  yield
                      diagnostic
                          "typedSdd.v2.rollbackMismatch"
                          "Rollback manifest differs from its recorded digest."
                          "Restore the authenticated rollback inventory before rollback."
              | Some(Unreadable detail) ->
                  yield
                      diagnostic
                          "typedSdd.v2.rollbackUnreadable"
                          $"Rollback manifest is unreadable: {detail}"
                          "Correct filesystem access before rollback."
              | _ ->
                  yield
                      diagnostic
                          "typedSdd.v2.rollbackMissing"
                          "The recorded rollback manifest is missing."
                          "Restore the authenticated rollback inventory."
          | _ ->
              yield
                  diagnostic
                      "typedSdd.v2.rollbackBinding"
                      "Rollback path and digest must both be present or both be null."
                      "Regenerate the migration receipt."

          yield! semanticClosure observations manifest ]

[<RequireQualifiedAccess>]
module QuintVerificationSelector =
    let value rung =
        match rung with
        | ProseOnly -> "prose-only"
        | StructuralTypecheck -> "structural-typecheck"
        | TestAndSimulation -> "test-and-simulation"
        | ModelCheck -> "model-check"
        | FullCorpus -> "full-corpus"

    let private rank =
        function
        | ProseOnly -> 0
        | StructuralTypecheck -> 1
        | TestAndSimulation -> 2
        | ModelCheck -> 3
        | FullCorpus -> 4

    let private strongest left right =
        if rank left >= rank right then left else right

    let private category (value: string) =
        if obj.ReferenceEquals(value, null) || String.IsNullOrWhiteSpace value then
            FullCorpus
        else
            match value.Trim().ToLowerInvariant() with
            | "prose"
            | "documentation"
            | "docs" -> ProseOnly
            | "catalogue"
            | "metadata"
            | "relationship" -> StructuralTypecheck
            | "action"
            | "simulation"
            | "test" -> TestAndSimulation
            | "invariant"
            | "temporal"
            | "bounds"
            | "reachability"
            | "model-check" -> ModelCheck
            | "adapter"
            | "runtime"
            | "compiler"
            | "profile"
            | "toolchain"
            | "schema"
            | "selector" -> FullCorpus
            | _ -> FullCorpus

    let private pathRung (path: string) =
        if obj.ReferenceEquals(path, null) || String.IsNullOrWhiteSpace path then
            FullCorpus
        else
            let normalized = path.Replace('\\', '/').ToLowerInvariant()

            if
                normalized.Contains("quintverificationselector")
                || normalized.Contains("quintcompiler")
                || normalized.Contains("quintprofile")
                || normalized.Contains("quinttoolchain")
                || normalized.Contains("typedlifecyclev2")
                || normalized.EndsWith(".schema.json", StringComparison.Ordinal)
            then
                FullCorpus
            elif normalized.EndsWith(".md", StringComparison.Ordinal) then
                ProseOnly
            else
                FullCorpus

    let select (selection: QuintVerificationSelection) =
        let impactRung =
            selection.Impacts |> List.map (fun impact -> category impact.Category)

        let pathRungs = selection.ChangedPaths |> List.map pathRung

        match impactRung @ pathRungs with
        | [] -> FullCorpus
        | values -> values |> List.fold strongest ProseOnly
