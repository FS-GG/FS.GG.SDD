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
        {
            Contract: string
            Kind: ContractKind
            SchemaVersion: int
            ContractVersion: string option
            Stability: StabilityClass
            Determinism: string
            Inventory: InventoryItem list
            SourceArtifact: ArtifactRef
            BaselinePresent: bool
            /// Feature 092 / ADR-0026: `true` for a committed *durable generated* artifact.
            DurableGenerated: bool
        }

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

    /// Every distinct field path in a JSON document, to full depth, as dotted paths
    /// (`parent.child`, arrays as `parent[].child`). Array elements are deduplicated, so a
    /// populated `sources[]` contributes one `sources[].path` regardless of element count, and a
    /// non-object / unparseable root yields `[]` (matching the former top-level-only walk on those
    /// inputs). This is the observed key set the release drift check compares against a contract's
    /// documented inventory (ADR-0002 Gap B finding 6 / #261): walking to full depth makes a nested
    /// add/remove visible to `evaluate`, not only to the full-shape byte-goldens (#249).
    ///
    /// Blind spot (#291): an **empty** array contributes no `array[].field` paths, so a nested field
    /// reachable only through an array that is empty in the produced conformance snapshot is invisible
    /// to `evaluate` — it can neither be catalogued (the conformance test's `releaseFieldAbsent` check
    /// would flag it as documented-but-unobserved) nor flagged as undocumented drift. Full nested
    /// coverage of a catalogued array therefore depends on the conformance fixture populating that
    /// array with at least one element; `ReleaseConformanceTests` pins that invariant for every
    /// catalogued `array[]`, and the full-shape byte-goldens (#249) backstop the residual (a field
    /// under an array the ready fixture legitimately leaves empty — e.g. `diagnostics`/`findings` in a
    /// clean run — is deliberately left un-catalogued for exactly this reason).
    let fullDepthKeys (text: string) : string list =
        let acc = System.Collections.Generic.HashSet<string>()

        let rec walk (prefix: string) (element: JsonElement) =
            match element.ValueKind with
            | JsonValueKind.Object ->
                for property in element.EnumerateObject() do
                    let path =
                        if prefix = "" then
                            property.Name
                        else
                            prefix + "." + property.Name

                    acc.Add path |> ignore
                    walk path property.Value
            | JsonValueKind.Array ->
                for item in element.EnumerateArray() do
                    walk (prefix + "[]") item
            | _ -> ()

        try
            use document = JsonDocument.Parse text
            walk "" document.RootElement
            acc |> Seq.sort |> Seq.toList
        with _ ->
            []

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

    // Feature 094 / FR-015. Not to be unified with `HandlersSurface.SurfaceClassify.bumpFor`, which
    // maps the *surface-mutation* verdicts (breaking→major, additive→minor, cosmetic/none→**none**).
    // Here `Clarifying` implies a **patch**; there a cosmetic `.fsi` reformat implies **no release**.
    // The two mappings agree on breaking/additive and diverge on the third case by design, so
    // collapsing them is a behavior change, not a refactor (spec 094 AMB-005, research R5).
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
          BaselinePresent = true
          DurableGenerated = false }

    let markdownViewEntry contract viewKind sections =
        { Contract = contract
          Kind = GeneratedViewContract(viewKind, Markdown)
          SchemaVersion = 1
          ContractVersion = None
          Stability = AdditiveOptional
          Determinism = determinism
          Inventory = markdownInventory sections
          SourceArtifact = generatedViewSource ("readiness/<id>/" + contract)
          BaselinePresent = true
          DurableGenerated = false }

    let currentRelease () : ReleaseReadiness =
        let identity =
            { Version = "0.11.0"
              Channel = channelOfVersion "0.11.0"
              PackageIds = [ "FS.GG.SDD.Artifacts"; "FS.GG.SDD.Commands"; "FS.GG.SDD.Cli" ]
              CliCommandName = "fsgg-sdd" }

        let compatibility =
            [ { SddVersionLine = "0.11.x"
                SpecKitRange = ">=0.8.5"
                GovernanceContractVersionRange = Some "1.x" } ]

        let workModel =
            jsonViewEntry
                "work-model.json"
                WorkModel
                AdditiveOptional
                [ "schemaVersion" ]
                [ "decisions"
                  "diagnostics"
                  "evidence"
                  "evidence[].artifactRefs"
                  "evidence[].id"
                  "evidence[].kind"
                  "evidence[].rationale"
                  "evidence[].requirementRefs"
                  "evidence[].result"
                  "evidence[].source"
                  "evidence[].sourceLocation"
                  "evidence[].sourceLocation.column"
                  "evidence[].sourceLocation.line"
                  "evidence[].subjectId"
                  "evidence[].subjectType"
                  "evidence[].synthetic"
                  "evidence[].taskRefs"
                  "generatedViews"
                  "generatedViews[].currency"
                  "generatedViews[].generator"
                  "generatedViews[].generator.id"
                  "generatedViews[].generator.version"
                  "generatedViews[].kind"
                  "generatedViews[].outputDigest"
                  "generatedViews[].outputDigest.algorithm"
                  "generatedViews[].outputDigest.value"
                  "generatedViews[].path"
                  "generatedViews[].schemaVersion"
                  "generatedViews[].sources"
                  "generatedViews[].sources[].digest"
                  "generatedViews[].sources[].digest.algorithm"
                  "generatedViews[].sources[].digest.value"
                  "generatedViews[].sources[].path"
                  "generatedViews[].sources[].schemaVersion"
                  "governanceBoundaries"
                  "modelVersion"
                  "project"
                  "project.defaultWorkRoot"
                  "project.id"
                  "requirements"
                  "requirements[].acceptanceCriteria"
                  "requirements[].id"
                  "requirements[].linkedEvidenceIds"
                  "requirements[].linkedTaskIds"
                  "requirements[].priority"
                  "requirements[].source"
                  "requirements[].sourceLocation"
                  "requirements[].sourceLocation.column"
                  "requirements[].sourceLocation.line"
                  "requirements[].text"
                  "requirements[].title"
                  "schemaVersion"
                  "sources"
                  "sources[].kind"
                  "sources[].owner"
                  "sources[].path"
                  "sources[].rawSchemaVersion"
                  "sources[].schemaStatus"
                  "sources[].schemaVersion"
                  "sources[].sourceDigest"
                  "sources[].sourceDigest.algorithm"
                  "sources[].sourceDigest.value"
                  "tasks"
                  "tasks[].decisions"
                  "tasks[].dependencies"
                  "tasks[].id"
                  "tasks[].owner"
                  "tasks[].requiredEvidence"
                  "tasks[].requiredSkills"
                  "tasks[].requirements"
                  "tasks[].source"
                  "tasks[].sourceIds"
                  "tasks[].sourceLocation"
                  "tasks[].sourceLocation.column"
                  "tasks[].sourceLocation.line"
                  "tasks[].status"
                  "tasks[].title"
                  "workId"
                  "workItem"
                  "workItem.changeTier"
                  "workItem.id"
                  "workItem.stage"
                  "workItem.status"
                  "workItem.title" ]

        let analysis =
            jsonViewEntry
                "analysis.json"
                Analysis
                AdditiveOptional
                [ "schemaVersion" ]
                [ "diagnostics"
                  "findings"
                  "generatedViews"
                  "generatedViews[].currency"
                  "generatedViews[].diagnosticIds"
                  "generatedViews[].kind"
                  "generatedViews[].path"
                  "generator"
                  "nextAction"
                  "nextAction.actionId"
                  "nextAction.command"
                  "nextAction.reason"
                  "optionalBoundaryFacts"
                  "optionalBoundaryFacts[].diagnosticIds"
                  "optionalBoundaryFacts[].path"
                  "optionalBoundaryFacts[].relationship"
                  "optionalBoundaryFacts[].requiredBySdd"
                  "optionalBoundaryFacts[].state"
                  "readiness"
                  "readiness.acceptedDeferralCount"
                  "readiness.advisoryCount"
                  "readiness.blockingCount"
                  "readiness.generatedViewFindingCount"
                  "readiness.malformedSourceCount"
                  "readiness.missingDispositionCount"
                  "readiness.readyCount"
                  "readiness.staleSourceCount"
                  "readiness.status"
                  "readiness.warningCount"
                  "schemaVersion"
                  "sourceRelationships"
                  "sourceRelationships[].diagnosticIds"
                  "sourceRelationships[].id"
                  "sourceRelationships[].relationship"
                  "sourceRelationships[].sourceId"
                  "sourceRelationships[].sourcePath"
                  "sourceRelationships[].state"
                  "sourceRelationships[].targetId"
                  "sourceRelationships[].targetPath"
                  "sources"
                  "sources[].digest"
                  "sources[].digest.algorithm"
                  "sources[].digest.value"
                  "sources[].kind"
                  "sources[].path"
                  "sources[].schemaStatus"
                  "sources[].schemaVersion"
                  "stage"
                  "status"
                  "viewVersion"
                  "workId" ]

        let verify =
            jsonViewEntry
                "verify.json"
                Verify
                AdditiveOptional
                [ "schemaVersion" ]
                [ "diagnostics"
                  "evidenceDispositions"
                  "evidenceDispositions[].affectedSourceIds"
                  "evidenceDispositions[].affectedTaskIds"
                  "evidenceDispositions[].correction"
                  "evidenceDispositions[].diagnosticIds"
                  "evidenceDispositions[].evidenceIds"
                  "evidenceDispositions[].id"
                  "evidenceDispositions[].obligationId"
                  "evidenceDispositions[].observed"
                  "evidenceDispositions[].severity"
                  "evidenceDispositions[].state"
                  "findings"
                  "generatedViews"
                  "generatedViews[].currency"
                  "generatedViews[].diagnosticIds"
                  "generatedViews[].kind"
                  "generatedViews[].path"
                  "generator"
                  "governanceCompatibility"
                  "governanceCompatibility[].diagnosticIds"
                  "governanceCompatibility[].path"
                  "governanceCompatibility[].relationship"
                  "governanceCompatibility[].requiredBySdd"
                  "governanceCompatibility[].state"
                  "lifecycleReadiness"
                  "lifecycleReadiness.stages"
                  "lifecycleReadiness.stages[].stage"
                  "lifecycleReadiness.stages[].status"
                  "lifecycleReadiness.status"
                  "nextAction"
                  "nextAction.actionId"
                  "nextAction.command"
                  "nextAction.reason"
                  "readiness"
                  "schemaVersion"
                  "skillVisibility"
                  "skillVisibility[].correction"
                  "skillVisibility[].diagnosticIds"
                  "skillVisibility[].requiringTaskIds"
                  "skillVisibility[].severity"
                  "skillVisibility[].skill"
                  "skillVisibility[].sourceArtifactPath"
                  "skillVisibility[].visibility"
                  "sources"
                  "sources[].digest"
                  "sources[].digest.algorithm"
                  "sources[].digest.value"
                  "sources[].kind"
                  "sources[].path"
                  "sources[].schemaStatus"
                  "sources[].schemaVersion"
                  "stage"
                  "status"
                  "taskGraph"
                  "taskGraph.dependenciesValid"
                  "taskGraph.dependencyCount"
                  "taskGraph.findingIds"
                  "taskGraph.statusesValid"
                  "taskGraph.taskCount"
                  "testDispositions"
                  "testDispositions[].affectedRequirementIds"
                  "testDispositions[].affectedTaskIds"
                  "testDispositions[].correction"
                  "testDispositions[].diagnosticIds"
                  "testDispositions[].evidenceIds"
                  "testDispositions[].id"
                  "testDispositions[].obligationId"
                  "testDispositions[].observed"
                  "testDispositions[].severity"
                  "testDispositions[].state"
                  "viewVersion"
                  "workId" ]

        let ship =
            jsonViewEntry
                "ship.json"
                Ship
                AdditiveOptional
                [ "schemaVersion" ]
                [ "diagnostics"
                  "disposition"
                  "disposition.advisoryFindingIds"
                  "disposition.blockingFindingIds"
                  "disposition.contributingStages"
                  "disposition.correction"
                  "disposition.state"
                  "disposition.warningFindingIds"
                  "evidenceDispositions"
                  "evidenceDispositions[].diagnosticIds"
                  "evidenceDispositions[].id"
                  "evidenceDispositions[].obligationId"
                  "evidenceDispositions[].observed"
                  "evidenceDispositions[].severity"
                  "evidenceDispositions[].state"
                  "findings"
                  "generatedViews"
                  "generatedViews[].currency"
                  "generatedViews[].diagnosticIds"
                  "generatedViews[].kind"
                  "generatedViews[].path"
                  "generator"
                  "governanceCompatibility"
                  "governanceCompatibility[].diagnosticIds"
                  "governanceCompatibility[].path"
                  "governanceCompatibility[].relationship"
                  "governanceCompatibility[].requiredBySdd"
                  "governanceCompatibility[].state"
                  "lifecycleReadiness"
                  "lifecycleReadiness.stages"
                  "lifecycleReadiness.stages[].stage"
                  "lifecycleReadiness.stages[].status"
                  "lifecycleReadiness.status"
                  "nextAction"
                  "nextAction.actionId"
                  "nextAction.command"
                  "nextAction.reason"
                  "readiness"
                  "schemaVersion"
                  "sources"
                  "sources[].digest"
                  "sources[].digest.algorithm"
                  "sources[].digest.value"
                  "sources[].kind"
                  "sources[].path"
                  "sources[].schemaStatus"
                  "sources[].schemaVersion"
                  "stage"
                  "status"
                  "verificationReadiness"
                  "verificationReadiness.blockingFindingIds"
                  "verificationReadiness.evidenceDeferredCount"
                  "verificationReadiness.evidenceInvalidCount"
                  "verificationReadiness.evidenceMissingCount"
                  "verificationReadiness.evidenceObservedCount"
                  "verificationReadiness.evidenceSelfAttestedCount"
                  "verificationReadiness.evidenceStaleCount"
                  "verificationReadiness.evidenceSupportedCount"
                  "verificationReadiness.evidenceSyntheticCount"
                  "verificationReadiness.status"
                  "viewVersion"
                  "workId" ]

        // The one *durable generated* lifecycle view (feature 092 / ADR-0026): a compact
        // projection of ship.json, committed because its verdict is commit-bound and
        // regeneration reports today's disposition rather than the merge's. `durableGenerated`
        // is what moves it out of the taxonomy doc's regenerable table into the durable one;
        // it is not a cross-repo contract, so it carries no contractVersion.
        let shipVerdict =
            { jsonViewEntry
                  "ship-verdict.json"
                  ShipVerdict
                  AdditiveOptional
                  [ "schemaVersion" ]
                  [ "disposition"
                    "disposition.blockingFindingIds"
                    "disposition.state"
                    "generator"
                    "readiness"
                    "schemaVersion"
                    "sourcesDigest"
                    "sourcesDigest.algorithm"
                    "sourcesDigest.value"
                    "stage"
                    "status"
                    "verificationReadiness"
                    "verificationReadiness.evidenceObservedCount"
                    "verificationReadiness.evidenceSelfAttestedCount"
                    "verificationReadiness.evidenceSupportedCount"
                    "verificationReadiness.status"
                    "viewVersion"
                    "workId" ] with
                DurableGenerated = true }

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
                    [ "contractVersion"
                      "diagnostics"
                      "evidence"
                      "evidence.dependencies"
                      "evidence.dependencies[].dependency"
                      "evidence.dependencies[].dependent"
                      "evidence.nodes"
                      "evidence.nodes[].id"
                      "evidence.nodes[].rationale"
                      "evidence.nodes[].state"
                      "generatorVersion"
                      "governanceConfig"
                      "governanceConfig.capabilitiesPresent"
                      "governanceConfig.policyPresent"
                      "governanceConfig.toolingPresent"
                      "governedReferences"
                      "readiness"
                      "readiness.blockingDiagnosticIds"
                      "readiness.counts"
                      "readiness.counts.advisory"
                      "readiness.counts.blocking"
                      "readiness.counts.warning"
                      "readiness.perViewState"
                      "readiness.perViewState[].state"
                      "readiness.perViewState[].view"
                      "readiness.shipDisposition"
                      "readiness.verificationReadiness"
                      "schemaVersion"
                      "sources"
                      "sources[].digest"
                      "sources[].path"
                      "sources[].schemaVersion"
                      "workId" ]
              SourceArtifact = generatedViewSource "readiness/<id>/governance-handoff.json"
              BaselinePresent = true
              DurableGenerated = false }

        let summary =
            markdownViewEntry "summary.md" Summary [ "Generated-view currency"; "Diagnostics"; "Next action" ]

        let guidance =
            { jsonViewEntry
                  "agent-commands/<target>/guidance.json"
                  AgentCommands
                  AdditiveOptional
                  [ "schemaVersion" ]
                  [ "behaviorModelDigest"
                    "behaviorModelDigest.algorithm"
                    "behaviorModelDigest.value"
                    "commands"
                    "commands[].id"
                    "commands[].purpose"
                    "commands[].relatedIds"
                    "commands[].stage"
                    "commands[].title"
                    "diagnostics"
                    "generated"
                    "generator"
                    "renderedFiles"
                    "renderedFiles[].kind"
                    "renderedFiles[].path"
                    "schemaVersion"
                    "skills"
                    "skills[].capability"
                    "skills[].id"
                    "skills[].relatedIds"
                    "skills[].title"
                    "sources"
                    "sources[].digest"
                    "sources[].digest.algorithm"
                    "sources[].digest.value"
                    "sources[].kind"
                    "sources[].path"
                    "sources[].schemaStatus"
                    "sources[].schemaVersion"
                    "targetId"
                    "viewVersion"
                    "workId" ] with
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
                    [ "agentGuidance"
                      "analysis"
                      "analysis.acceptedDeferralCount"
                      "analysis.advisoryCount"
                      "analysis.analysisPath"
                      "analysis.blockingCount"
                      "analysis.generatedViewFindingCount"
                      "analysis.malformedSourceCount"
                      "analysis.missingDispositionCount"
                      "analysis.readiness"
                      "analysis.readyFindingCount"
                      "analysis.sourceCount"
                      "analysis.sourceRelationshipCount"
                      "analysis.stage"
                      "analysis.staleSourceCount"
                      "analysis.status"
                      "analysis.warningCount"
                      "analysis.workId"
                      "changedArtifacts"
                      "changedArtifacts[].afterDigest"
                      "changedArtifacts[].afterDigest.algorithm"
                      "changedArtifacts[].afterDigest.value"
                      "changedArtifacts[].beforeDigest"
                      "changedArtifacts[].beforeDigest.algorithm"
                      "changedArtifacts[].beforeDigest.value"
                      "changedArtifacts[].diagnosticIds"
                      "changedArtifacts[].kind"
                      "changedArtifacts[].operation"
                      "changedArtifacts[].ownership"
                      "changedArtifacts[].path"
                      "changedArtifacts[].safeWriteDecision"
                      "checklist"
                      "checklist.acceptedDeferralCount"
                      "checklist.advisoryCount"
                      "checklist.failedBlockingCount"
                      "checklist.itemIds"
                      "checklist.passedCount"
                      "checklist.resultIds"
                      "checklist.sourceClarifications"
                      "checklist.sourceSpec"
                      "checklist.stage"
                      "checklist.staleResultCount"
                      "checklist.status"
                      "checklist.workId"
                      "clarification"
                      "clarification.acceptedDeferralIds"
                      "clarification.answeredQuestionIds"
                      "clarification.blockingAmbiguityCount"
                      "clarification.decisionIds"
                      "clarification.questionIds"
                      "clarification.remainingAmbiguityCount"
                      "clarification.sourceSpec"
                      "clarification.stage"
                      "clarification.status"
                      "clarification.workId"
                      "coherent"
                      "command"
                      "command.name"
                      "command.stage"
                      "context"
                      "context.projectRoot"
                      "context.workId"
                      "diagnostics"
                      "doctor"
                      "evidence"
                      "generatedViews"
                      "generatedViews[].currency"
                      "generatedViews[].diagnosticIds"
                      "generatedViews[].generator"
                      "generatedViews[].generator.id"
                      "generatedViews[].generator.version"
                      "generatedViews[].kind"
                      "generatedViews[].path"
                      "generatedViews[].schemaVersion"
                      "generatedViews[].sources"
                      "generatedViews[].sources[].digest"
                      "generatedViews[].sources[].digest.algorithm"
                      "generatedViews[].sources[].digest.value"
                      "generatedViews[].sources[].path"
                      "generatedViews[].sources[].schemaStatus"
                      "generatedViews[].sources[].schemaVersion"
                      "governanceCompatibility"
                      "governanceCompatibility[].diagnosticIds"
                      "governanceCompatibility[].path"
                      "governanceCompatibility[].relationship"
                      "governanceCompatibility[].requiredBySdd"
                      "governanceCompatibility[].state"
                      "help"
                      "invocation"
                      "invocation.dryRun"
                      "invocation.outputFormat"
                      "lifecycleStatus"
                      "lifecycleStatus.currentOrdinal"
                      "lifecycleStatus.isLifecycleStage"
                      "lifecycleStatus.nextCommand"
                      "lifecycleStatus.outcome"
                      "lifecycleStatus.stages"
                      "lifecycleStatus.stages[].command"
                      "lifecycleStatus.stages[].ordinal"
                      "lifecycleStatus.stages[].state"
                      "lifecycleStatus.totalStages"
                      "lifecycleStatus.workId"
                      "lint"
                      "nextAction"
                      "nextAction.actionId"
                      "nextAction.blockingDiagnosticIds"
                      "nextAction.command"
                      "nextAction.reason"
                      "nextAction.requiredArtifacts"
                      "nextAction.workId"
                      "outcome"
                      "plan"
                      "plan.acceptedDeferralCount"
                      "plan.advisoryCount"
                      "plan.blockingFindingCount"
                      "plan.contractReferenceIds"
                      "plan.decisionIds"
                      "plan.generatedViewImpactIds"
                      "plan.migrationNoteIds"
                      "plan.sourceChecklist"
                      "plan.sourceClarifications"
                      "plan.sourceSpec"
                      "plan.stage"
                      "plan.staleDecisionCount"
                      "plan.status"
                      "plan.verificationObligationIds"
                      "plan.workId"
                      "refresh"
                      "reportVersion"
                      "scaffold"
                      "schemaVersion"
                      "ship"
                      "ship.advisoryCount"
                      "ship.blockingCount"
                      "ship.disposition"
                      "ship.evidenceDeferredCount"
                      "ship.evidenceInvalidCount"
                      "ship.evidenceMissingCount"
                      "ship.evidenceObservedCount"
                      "ship.evidenceSelfAttestedCount"
                      "ship.evidenceStaleCount"
                      "ship.evidenceSupportedCount"
                      "ship.evidenceSyntheticCount"
                      "ship.findingIds"
                      "ship.generatedViewState"
                      "ship.lifecycleStageReadiness"
                      "ship.lifecycleStageReadiness.analyze"
                      "ship.lifecycleStageReadiness.checklist"
                      "ship.lifecycleStageReadiness.clarify"
                      "ship.lifecycleStageReadiness.evidence"
                      "ship.lifecycleStageReadiness.plan"
                      "ship.lifecycleStageReadiness.specify"
                      "ship.lifecycleStageReadiness.tasks"
                      "ship.lifecycleStageReadiness.verify"
                      "ship.readiness"
                      "ship.readyFindingCount"
                      "ship.shipPath"
                      "ship.sourceSnapshotCount"
                      "ship.stage"
                      "ship.status"
                      "ship.verificationReadiness"
                      "ship.warningCount"
                      "ship.workId"
                      "specification"
                      "specification.acceptanceScenarioIds"
                      "specification.ambiguityIds"
                      "specification.requirementIds"
                      "specification.stage"
                      "specification.status"
                      "specification.storyIds"
                      "specification.workId"
                      "surface"
                      "tasks"
                      "tasks.acceptedDeferralCount"
                      "tasks.advisoryCount"
                      "tasks.blockingFindingCount"
                      "tasks.dependencyCount"
                      "tasks.doneCount"
                      "tasks.inProgressCount"
                      "tasks.pendingCount"
                      "tasks.requiredEvidenceCount"
                      "tasks.requiredSkillCount"
                      "tasks.skippedCount"
                      "tasks.sourceChecklist"
                      "tasks.sourceClarifications"
                      "tasks.sourcePlan"
                      "tasks.sourceSpec"
                      "tasks.stage"
                      "tasks.staleCount"
                      "tasks.status"
                      "tasks.taskIds"
                      "tasks.workId"
                      "toolVersion"
                      "upgrade"
                      "verification" ]
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
              BaselinePresent = true
              DurableGenerated = false }

        { SchemaVersion = 1
          GeneratorVersion = currentGeneratorVersion ()
          Identity = identity
          Compatibility = compatibility
          Catalog =
            [ workModel
              analysis
              verify
              ship
              shipVerdict
              governanceHandoff
              summary
              guidance
              commandsMd
              skillsMd
              commandReport ]
          // 0.11.0 is ADDITIVE, so it carries NO migration note — `migrationNoteRequired
          // Additive = false` (FR-009 / FR-010), and an EMPTY list is the honest encoding of
          // that. This is not an omission, and the classification was not asserted: it was
          // MEASURED against the released 0.10.0 tree, on the two surfaces the policy names.
          //
          //  1. F# public surface — `PublicSurface.baseline` diffs v0.10.0..HEAD are
          //     INSERTIONS ONLY (43 added, 0 removed), across Contracts/Artifacts/Commands.
          //     ApiCompat agrees, baselined against the published feed artifacts.
          //  2. The public `--json` output shapes — the committed goldens gained exactly four
          //     keys (`observed`, `evidenceObservedCount`, `evidenceSelfAttestedCount`,
          //     `evidenceSupportedCount`; features 415/422) and LOST NONE. "Add an optional
          //     report field" is the policy's Additive row.
          //
          // No command, flag, or exit-code contract was removed or retyped. `--require-observed`
          // (feature 422) is a NEW flag whose default is OFF, so no existing invocation changes
          // its exit code; flipping that default WOULD be breaking, and #422 deliberately left it
          // to a human on a schema major. Nothing here flips it.
          //
          // The 0.10.0 note is NOT carried forward: `Migrations` is the note for THE RELEASE
          // BEING CUT (T023 pins `Path` to `Identity.Version`), not a running history. The
          // history lives in `docs/release/migrations/`, which keeps 0.10.0.md on disk.
          //
          // WHEN A NOTE COMES BACK, its `BreakingChanges` text has two constraints. They are
          // kept here, with the empty list, because here is where the next author will be
          // standing — and the first of them is enforced by NOTHING ELSE:
          //
          //  1. BACKTICK-FREE. The default JavaScriptEncoder escapes U+0060 (as it already
          //     escapes '>' in specKitRange), so a backticked note lands in a committed machine
          //     artifact as ``` noise. NO TEST CATCHES THIS — this comment is the whole
          //     guard, which is exactly why deleting it along with the 0.10.0 note was wrong.
          //  2. No Governance gate-logic vocabulary — "gate"/"route"/"profile"/"freshness"/
          //     "publish"/"provenance"/"verdict"/"enforce". ReleaseBoundaryTests T024 scans the
          //     SERIALIZED contract, so it does cover note text, and it will fail you. SDD
          //     reports blocking readiness; it never gates. Say "blocks"/"blocking".
          //
          // And enumerate EVERY breaking change: a note that under-reports is the exact failure
          // the note exists to prevent.
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
        writer.WriteBoolean("durableGenerated", entry.DurableGenerated)
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
                      BaselinePresent = (prop "baselinePresent" entry).GetBoolean()
                      // Absent ⇒ regenerable, which is what every pre-092 entry meant.
                      DurableGenerated =
                        match entry.TryGetProperty "durableGenerated" with
                        | true, value -> value.GetBoolean()
                        | _ -> false })
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
