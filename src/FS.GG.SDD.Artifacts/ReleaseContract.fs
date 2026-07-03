namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.SchemaVersion

module ReleaseContract =
    type ReleaseChannel =
        | PreRelease
        | StableRelease

    type ChangeClass =
        | Breaking
        | Additive
        | Clarifying

    type StabilityClass =
        | Stable
        | AdditiveOptional
        | Experimental

    type ContractFormat =
        | Json
        | Markdown

    type ContractKind =
        | GeneratedViewContract of GeneratedViewKind * ContractFormat
        | CommandOutputContract

    type InventoryKind =
        | JsonField
        | MarkdownSection

    type InventoryItem =
        { Name: string
          Kind: InventoryKind
          Stability: StabilityClass }

    type PackageVersionIdentity =
        { Version: string
          Channel: ReleaseChannel
          PackageIds: string list
          CliCommandName: string }

    type CompatibilityMatrixEntry =
        { SddVersionLine: string
          SpecKitRange: string
          GovernanceContractVersionRange: string option }

    type SchemaReferenceEntry =
        { Contract: string
          Kind: ContractKind
          SchemaVersion: int
          ContractVersion: string option
          Stability: StabilityClass
          Determinism: string
          Inventory: InventoryItem list
          SourceArtifact: ArtifactRef
          BaselinePresent: bool }

    type MigrationNoteRef =
        { Version: string
          Path: string
          BreakingChanges: string list }

    type ReleaseReadiness =
        { SchemaVersion: int
          GeneratorVersion: GeneratorVersion
          Identity: PackageVersionIdentity
          Compatibility: CompatibilityMatrixEntry list
          Catalog: SchemaReferenceEntry list
          Migrations: MigrationNoteRef list }

    type ProducedArtifact =
        { Contract: string
          Source: ArtifactRef
          Inventory: string list }

    // ---- value labels ----

    let releaseChannelValue channel =
        match channel with
        | PreRelease -> "preRelease"
        | StableRelease -> "stable"

    let changeClassValue changeClass =
        match changeClass with
        | Breaking -> "breaking"
        | Additive -> "additive"
        | Clarifying -> "clarifying"

    let stabilityClassValue stability =
        match stability with
        | Stable -> "stable"
        | AdditiveOptional -> "additiveOptional"
        | Experimental -> "experimental"

    let contractFormatValue format =
        match format with
        | Json -> "json"
        | Markdown -> "markdown"

    let inventoryKindValue kind =
        match kind with
        | JsonField -> "jsonField"
        | MarkdownSection -> "markdownSection"

    // ---- policy ----

    let channelOfVersion (version: string) =
        let value = if String.IsNullOrEmpty version then "" else version.Trim()

        let major =
            match value.Split('.') |> Array.tryHead with
            | Some head ->
                match Int32.TryParse head with
                | true, parsed -> parsed
                | _ -> 0
            | None -> 0

        if major = 0 then PreRelease else StableRelease

    let bumpRule changeClass =
        match changeClass with
        | Breaking -> "major"
        | Additive -> "minor"
        | Clarifying -> "patch"

    let migrationNoteRequired changeClass =
        match changeClass with
        | Breaking -> true
        | Additive
        | Clarifying -> false

    // ---- the current release contract ----

    let determinism = "byte-stable; canonical key order; no clock/path/ANSI"

    let generatedViewSource (relative: string) =
        match ArtifactRef.create relative ArtifactRef.GeneratedView Sdd false with
        | Ok artifact -> artifact
        | Error message -> invalidArg (nameof relative) message

    let inventory kind (stableNames: string list) (names: string list) =
        names
        |> List.map (fun name ->
            { Name = name
              Kind = kind
              Stability =
                (if List.contains name stableNames then
                     Stable
                 else
                     AdditiveOptional) })

    let jsonInventory stableNames names = inventory JsonField stableNames names
    let markdownInventory names = inventory MarkdownSection [] names

    let jsonViewEntry contract viewKind stability stableNames names =
        { Contract = contract
          Kind = GeneratedViewContract(viewKind, Json)
          SchemaVersion = 1
          ContractVersion = None
          Stability = stability
          Determinism = determinism
          Inventory = jsonInventory stableNames names
          SourceArtifact = generatedViewSource ("readiness/<id>/" + contract)
          BaselinePresent = true }

    let markdownViewEntry contract viewKind sections =
        { Contract = contract
          Kind = GeneratedViewContract(viewKind, Markdown)
          SchemaVersion = 1
          ContractVersion = None
          Stability = AdditiveOptional
          Determinism = determinism
          Inventory = markdownInventory sections
          SourceArtifact = generatedViewSource ("readiness/<id>/" + contract)
          BaselinePresent = true }

    let currentRelease () : ReleaseReadiness =
        let identity =
            { Version = "0.5.0"
              Channel = channelOfVersion "0.5.0"
              PackageIds = [ "FS.GG.SDD.Artifacts"; "FS.GG.SDD.Commands"; "FS.GG.SDD.Cli" ]
              CliCommandName = "fsgg-sdd" }

        let compatibility =
            [ { SddVersionLine = "0.5.x"
                SpecKitRange = ">=0.8.5"
                GovernanceContractVersionRange = Some "1.x" } ]

        let workModel =
            jsonViewEntry
                "work-model.json"
                WorkModel
                AdditiveOptional
                [ "schemaVersion" ]
                [ "schemaVersion"
                  "modelVersion"
                  "workId"
                  "project"
                  "sources"
                  "workItem"
                  "requirements"
                  "decisions"
                  "tasks"
                  "evidence"
                  "generatedViews"
                  "diagnostics"
                  "governanceBoundaries" ]

        let analysis =
            jsonViewEntry
                "analysis.json"
                Analysis
                AdditiveOptional
                [ "schemaVersion" ]
                [ "schemaVersion"
                  "viewVersion"
                  "workId"
                  "stage"
                  "status"
                  "generator"
                  "sources"
                  "sourceRelationships"
                  "readiness"
                  "findings"
                  "generatedViews"
                  "optionalBoundaryFacts"
                  "diagnostics"
                  "nextAction" ]

        let verify =
            jsonViewEntry
                "verify.json"
                Verify
                AdditiveOptional
                [ "schemaVersion" ]
                [ "schemaVersion"
                  "viewVersion"
                  "workId"
                  "stage"
                  "status"
                  "generator"
                  "sources"
                  "lifecycleReadiness"
                  "taskGraph"
                  "evidenceDispositions"
                  "testDispositions"
                  "skillVisibility"
                  "generatedViews"
                  "findings"
                  "governanceCompatibility"
                  "diagnostics"
                  "readiness"
                  "nextAction" ]

        let ship =
            jsonViewEntry
                "ship.json"
                Ship
                AdditiveOptional
                [ "schemaVersion" ]
                [ "schemaVersion"
                  "viewVersion"
                  "workId"
                  "stage"
                  "status"
                  "generator"
                  "sources"
                  "lifecycleReadiness"
                  "verificationReadiness"
                  "evidenceDispositions"
                  "generatedViews"
                  "disposition"
                  "findings"
                  "governanceCompatibility"
                  "diagnostics"
                  "readiness"
                  "nextAction" ]

        // The governance handoff is the one cross-repo contract: it carries a
        // contractVersion and its envelope shape is Stable (FR-002 declared
        // integration fact only; no Governance gate logic — FR-014).
        let governanceHandoff =
            { Contract = "governance-handoff.json"
              Kind = GeneratedViewContract(GovernanceHandoff, Json)
              SchemaVersion = 1
              ContractVersion = Some "1.0.0"
              Stability = Stable
              Determinism = determinism
              Inventory =
                jsonInventory
                    [ "schemaVersion"; "contractVersion" ]
                    [ "schemaVersion"
                      "contractVersion"
                      "generatorVersion"
                      "workId"
                      "sources"
                      "evidence"
                      "governedReferences"
                      "governanceConfig"
                      "readiness"
                      "diagnostics" ]
              SourceArtifact = generatedViewSource "readiness/<id>/governance-handoff.json"
              BaselinePresent = true }

        let summary =
            markdownViewEntry "summary.md" Summary [ "Generated-view currency"; "Diagnostics"; "Next action" ]

        let guidance =
            { jsonViewEntry
                  "agent-commands/<target>/guidance.json"
                  AgentCommands
                  AdditiveOptional
                  [ "schemaVersion" ]
                  [ "schemaVersion"
                    "viewVersion"
                    "workId"
                    "targetId"
                    "generator"
                    "generated"
                    "sources"
                    "behaviorModelDigest"
                    "commands"
                    "skills"
                    "renderedFiles"
                    "diagnostics" ] with
                SourceArtifact = generatedViewSource "readiness/<id>/agent-commands/<target>/guidance.json" }

        let commandsMd =
            markdownViewEntry "agent-commands/<target>/commands.md" AgentCommands [ "Agent commands" ]

        let skillsMd =
            markdownViewEntry "agent-commands/<target>/skills.md" AgentCommands [ "Agent skills" ]

        let commandReport =
            { Contract = "command-report (--json)"
              Kind = CommandOutputContract
              SchemaVersion = 1
              ContractVersion = None
              Stability = AdditiveOptional
              Determinism = determinism
              Inventory =
                jsonInventory
                    [ "schemaVersion" ]
                    [ "schemaVersion"
                      "reportVersion"
                      "command"
                      "context"
                      "invocation"
                      "outcome"
                      "changedArtifacts"
                      "specification"
                      "clarification"
                      "checklist"
                      "plan"
                      "tasks"
                      "analysis"
                      "evidence"
                      "verification"
                      "ship"
                      "agentGuidance"
                      "refresh"
                      "scaffold"
                      // Feature 053: additive remediation report blocks.
                      "doctor"
                      "upgrade"
                      "generatedViews"
                      "diagnostics"
                      "governanceCompatibility"
                      "nextAction"
                      "help" ]
              SourceArtifact =
                (match
                    ArtifactRef.create
                        "src/FS.GG.SDD.Commands/CommandSerialization.fs"
                        (ArtifactRef.Other "commandOutput")
                        Sdd
                        false
                 with
                 | Ok artifact -> artifact
                 | Error message ->
                     failwithf
                         "release contract source artifact path %s rejected: %s"
                         "src/FS.GG.SDD.Commands/CommandSerialization.fs"
                         message)
              BaselinePresent = true }

        { SchemaVersion = 1
          GeneratorVersion = currentGeneratorVersion ()
          Identity = identity
          Compatibility = compatibility
          Catalog =
            [ workModel
              analysis
              verify
              ship
              governanceHandoff
              summary
              guidance
              commandsMd
              skillsMd
              commandReport ]
          // Additive-only release (adds public surface, breaks no existing
          // contract): no migration note (FR-009; classified in T002).
          Migrations = [] }

    // ---- canonical serialization ----

    let writeNullableString (writer: Utf8JsonWriter) (name: string) (value: string option) =
        match value with
        | Some text -> writer.WriteString(name, text)
        | None -> writer.WriteNull name

    let writeInventoryItem (writer: Utf8JsonWriter) (item: InventoryItem) =
        writer.WriteStartObject()
        writer.WriteString("name", item.Name)
        writer.WriteString("kind", inventoryKindValue item.Kind)
        writer.WriteString("stability", stabilityClassValue item.Stability)
        writer.WriteEndObject()

    let writeKind (writer: Utf8JsonWriter) (kind: ContractKind) =
        writer.WriteStartObject("kind")

        match kind with
        | GeneratedViewContract(viewKind, format) ->
            writer.WriteString("generatedView", viewKindValue viewKind)
            writer.WriteString("format", contractFormatValue format)
        | CommandOutputContract ->
            writer.WriteBoolean("commandOutput", true)
            writer.WriteString("format", "json")

        writer.WriteEndObject()

    let writeEntry (writer: Utf8JsonWriter) (entry: SchemaReferenceEntry) =
        writer.WriteStartObject()
        writer.WriteString("contract", entry.Contract)
        writeKind writer entry.Kind
        writer.WriteNumber("schemaVersion", entry.SchemaVersion)
        writeNullableString writer "contractVersion" entry.ContractVersion
        writer.WriteString("stability", stabilityClassValue entry.Stability)
        writer.WriteString("determinism", entry.Determinism)
        writer.WriteStartArray("inventory")

        entry.Inventory
        |> List.sortBy (fun item -> item.Name)
        |> List.iter (writeInventoryItem writer)

        writer.WriteEndArray()
        writer.WriteStartObject("sourceArtifact")
        writer.WriteString("path", entry.SourceArtifact.Path)
        writer.WriteString("kind", kindValue entry.SourceArtifact.Kind)
        writer.WriteString("owner", ownerValue entry.SourceArtifact.Owner)
        writer.WriteBoolean("requiredBySdd", entry.SourceArtifact.RequiredBySdd)
        writer.WriteEndObject()
        writer.WriteBoolean("baselinePresent", entry.BaselinePresent)
        writer.WriteEndObject()

    let serialize (release: ReleaseReadiness) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", release.SchemaVersion)

        writer.WriteStartObject("generatorVersion")
        writer.WriteString("id", release.GeneratorVersion.Id)
        writer.WriteString("version", release.GeneratorVersion.Version)
        writer.WriteEndObject()

        writer.WriteStartObject("identity")
        writer.WriteString("version", release.Identity.Version)
        writer.WriteString("channel", releaseChannelValue release.Identity.Channel)
        writer.WriteStartArray("packageIds")

        release.Identity.PackageIds
        |> List.iter (fun id -> writer.WriteStringValue(id: string))

        writer.WriteEndArray()
        writer.WriteString("cliCommandName", release.Identity.CliCommandName)
        writer.WriteEndObject()

        writer.WriteStartArray("compatibility")

        release.Compatibility
        |> List.sortBy (fun entry -> entry.SddVersionLine)
        |> List.iter (fun entry ->
            writer.WriteStartObject()
            writer.WriteString("sddVersionLine", entry.SddVersionLine)
            writer.WriteString("specKitRange", entry.SpecKitRange)
            writeNullableString writer "governanceContractVersionRange" entry.GovernanceContractVersionRange
            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteStartArray("catalog")

        release.Catalog
        |> List.sortBy (fun entry -> entry.Contract)
        |> List.iter (writeEntry writer)

        writer.WriteEndArray()

        writer.WriteStartArray("migrations")

        release.Migrations
        |> List.sortBy (fun note -> note.Version)
        |> List.iter (fun note ->
            writer.WriteStartObject()
            writer.WriteString("version", note.Version)
            writer.WriteString("path", note.Path)
            writer.WriteStartArray("breakingChanges")

            note.BreakingChanges
            |> List.iter (fun change -> writer.WriteStringValue(change: string))

            writer.WriteEndArray()
            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())

    // ---- parse (round-trip) ----

    let parseViewKind (value: string) =
        match value with
        | "workModel" -> WorkModel
        | "analysis" -> Analysis
        | "verify" -> Verify
        | "ship" -> Ship
        | "summary" -> Summary
        | "agentCommands" -> AgentCommands
        | "governance-handoff" -> GovernanceHandoff
        | other -> Other other

    let parseFormat (value: string) =
        match value with
        | "markdown" -> Markdown
        | _ -> Json

    let parseStability (value: string) =
        match value with
        | "stable" -> Stable
        | "experimental" -> Experimental
        | _ -> AdditiveOptional

    let parseInventoryKind (value: string) =
        match value with
        | "markdownSection" -> MarkdownSection
        | _ -> JsonField

    let parseChannel (value: string) =
        match value with
        | "stable" -> StableRelease
        | _ -> PreRelease

    let parseArtifactKind (value: string) =
        match value with
        | "generatedView" -> ArtifactRef.GeneratedView
        | other -> ArtifactRef.Other other

    let parseOwner (value: string) =
        match value with
        | "governance" -> Governance
        | "rendering" -> Rendering
        | "generatedProduct" -> GeneratedProduct
        | "mirrored" -> Mirrored
        | _ -> Sdd

    let optString (element: JsonElement) (name: string) =
        match element.TryGetProperty name with
        | true, value when value.ValueKind = JsonValueKind.String -> Option.ofObj (value.GetString())
        | _ -> None

    let parse (json: string) : Result<ReleaseReadiness, string> =
        try
            use document = JsonDocument.Parse json
            let root = document.RootElement
            let prop (name: string) (element: JsonElement) = element.GetProperty name

            let str name element =
                (prop name element).GetString() |> Option.ofObj |> Option.defaultValue ""

            let intp name element = (prop name element).GetInt32()

            let artifactOf (element: JsonElement) =
                let path = str "path" element
                let kind = parseArtifactKind (str "kind" element)
                let owner = parseOwner (str "owner" element)
                let required = (prop "requiredBySdd" element).GetBoolean()

                match ArtifactRef.create path kind owner required with
                | Ok artifact -> artifact
                | Error message -> failwithf "release contract: parsed-back artifact path %s rejected: %s" path message

            let generatorElement = prop "generatorVersion" root

            let generator: GeneratorVersion =
                { Id = str "id" generatorElement
                  Version = str "version" generatorElement }

            let identityElement = prop "identity" root

            let identity =
                { Version = str "version" identityElement
                  Channel = parseChannel (str "channel" identityElement)
                  PackageIds =
                    (prop "packageIds" identityElement).EnumerateArray()
                    |> Seq.map (fun item -> item.GetString() |> Option.ofObj |> Option.defaultValue "")
                    |> Seq.toList
                  CliCommandName = str "cliCommandName" identityElement }

            let compatibility =
                (prop "compatibility" root).EnumerateArray()
                |> Seq.map (fun entry ->
                    { SddVersionLine = str "sddVersionLine" entry
                      SpecKitRange = str "specKitRange" entry
                      GovernanceContractVersionRange = optString entry "governanceContractVersionRange" })
                |> Seq.toList

            let catalog =
                (prop "catalog" root).EnumerateArray()
                |> Seq.map (fun entry ->
                    let kindElement = prop "kind" entry

                    let kind =
                        match kindElement.TryGetProperty "generatedView" with
                        | true, view ->
                            GeneratedViewContract(
                                parseViewKind (view.GetString() |> Option.ofObj |> Option.defaultValue ""),
                                parseFormat (str "format" kindElement)
                            )
                        | _ -> CommandOutputContract

                    let inventory =
                        (prop "inventory" entry).EnumerateArray()
                        |> Seq.map (fun item ->
                            { Name = str "name" item
                              Kind = parseInventoryKind (str "kind" item)
                              Stability = parseStability (str "stability" item) })
                        |> Seq.toList

                    { Contract = str "contract" entry
                      Kind = kind
                      SchemaVersion = intp "schemaVersion" entry
                      ContractVersion = optString entry "contractVersion"
                      Stability = parseStability (str "stability" entry)
                      Determinism = str "determinism" entry
                      Inventory = inventory
                      SourceArtifact = artifactOf (prop "sourceArtifact" entry)
                      BaselinePresent = (prop "baselinePresent" entry).GetBoolean() })
                |> Seq.toList

            let migrations =
                (prop "migrations" root).EnumerateArray()
                |> Seq.map (fun note ->
                    { Version = str "version" note
                      Path = str "path" note
                      BreakingChanges =
                        (prop "breakingChanges" note).EnumerateArray()
                        |> Seq.map (fun change -> change.GetString() |> Option.ofObj |> Option.defaultValue "")
                        |> Seq.toList })
                |> Seq.toList

            Ok
                { SchemaVersion = intp "schemaVersion" root
                  GeneratorVersion = generator
                  Identity = identity
                  Compatibility = compatibility
                  Catalog = catalog
                  Migrations = migrations }
        with ex ->
            Error ex.Message

    // ---- pure readiness check ----

    let gap id artifact message correction =
        Diagnostics.create id DiagnosticError artifact None message correction []

    let evaluate (release: ReleaseReadiness) (produced: ProducedArtifact list) : Diagnostic list =
        let entriesByContract =
            release.Catalog |> List.map (fun entry -> entry.Contract, entry) |> Map.ofList

        let undocumented =
            produced
            |> List.filter (fun item -> not (Map.containsKey item.Contract entriesByContract))
            |> List.map (fun item ->
                gap
                    "releaseOutputUndocumented"
                    (Some item.Source)
                    $"Produced output '{item.Contract}' has no release-readiness catalog entry."
                    "Add a SchemaReferenceEntry for this output to release-readiness.json.")

        let entryGaps =
            release.Catalog
            |> List.collect (fun entry ->
                [ if not entry.BaselinePresent then
                      gap
                          "releaseBaselineMissing"
                          (Some entry.SourceArtifact)
                          $"Public contract '{entry.Contract}' has no locking baseline."
                          "Capture a golden baseline for this contract under tests/**/baselines/."
                  if String.IsNullOrWhiteSpace entry.SourceArtifact.Path then
                      gap
                          "releaseSourceMissing"
                          None
                          $"Public contract '{entry.Contract}' has no source artifact back-reference."
                          "Set the SchemaReferenceEntry.SourceArtifact for this contract." ])

        let drift =
            produced
            |> List.collect (fun item ->
                match Map.tryFind item.Contract entriesByContract with
                | None -> []
                | Some entry ->
                    let documented = entry.Inventory |> List.map (fun field -> field.Name) |> Set.ofList
                    let observed = Set.ofList item.Inventory

                    [ for name in Set.toList (Set.difference observed documented) ->
                          gap
                              "releaseFieldUndocumented"
                              (Some item.Source)
                              $"Produced '{item.Contract}' has undocumented field '{name}'."
                              "Add the field to the catalog inventory (the produced artifact is authoritative)."
                      for name in Set.toList (Set.difference documented observed) ->
                          gap
                              "releaseFieldAbsent"
                              (Some entry.SourceArtifact)
                              $"Documented field '{name}' is absent from produced '{item.Contract}'."
                              "Remove the stale field from the catalog or restore it in the producer." ])

        undocumented @ entryGaps @ drift |> Diagnostics.sort
