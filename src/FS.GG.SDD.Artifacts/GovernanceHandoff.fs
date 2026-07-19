namespace FS.GG.SDD.Artifacts

open System.IO
open System.Text
open System.Text.Json
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Artifacts.WorkModel

module GovernanceHandoff =
    type DeclaredEvidenceState =
        | Pending
        | Real
        | Synthetic
        | Failed
        | Skipped

    type EvidenceNode =
        { Id: string
          State: DeclaredEvidenceState
          Rationale: string option }

    type EvidenceEdge =
        { Dependent: string
          Dependency: string }

    type EvidenceProjection =
        { Nodes: EvidenceNode list
          Dependencies: EvidenceEdge list }

    type GovernedReference =
        { Path: string
          Owner: string
          Relationship: string
          Kind: string option
          Operation: string option }

    type GovernanceConfigPresence =
        { PolicyPresent: bool
          PolicyPointer: string option
          CapabilitiesPresent: bool
          CapabilitiesPointer: string option
          ToolingPresent: bool
          ToolingPointer: string option }

    type ReadinessFacts =
        {
            ShipDisposition: string
            VerificationReadiness: string
            AdvisoryCount: int
            WarningCount: int
            BlockingCount: int
            /// WI-4 (ADR-0048): classified `{gameplay}` FR obligations left unmet at the merge boundary —
            /// the aggregate a Governance gate binds to block-on-ship. `0` when no FR is classified.
            ClassifiedObligationsUnmet: int
            BlockingDiagnosticIds: string list
            PerViewState: (string * string) list
        }

    type GovernanceHandoff =
        { SchemaVersion: int
          ContractVersion: string
          GeneratorVersion: GeneratorVersion
          WorkId: string
          Sources: SourceIdentity list
          Evidence: EvidenceProjection
          GovernedReferences: GovernedReference list
          GovernanceConfig: GovernanceConfigPresence
          Readiness: ReadinessFacts
          Diagnostics: Diagnostic list }

    let declaredEvidenceStateValue state =
        match state with
        | Pending -> "pending"
        | Real -> "real"
        | Synthetic -> "synthetic"
        | Failed -> "failed"
        | Skipped -> "skipped"

    let normalize (value: string) = value.Trim().ToLowerInvariant()

    let isStaleEvidenceResult (result: string) = normalize result = "stale"

    let mapEvidenceState (result: string) (synthetic: bool) =
        // Synthetic declaration dominates the result (root-cause taint, declared at source).
        if synthetic then
            Synthetic
        else
            match normalize result with
            | "supported"
            | "passed"
            | "pass"
            | "real"
            | "verified" -> Real
            | "deferred"
            | "accepted-deferral" -> Skipped
            | "failed"
            | "invalid" -> Failed
            // Staleness is Governance-owned freshness: SDD maps to the underlying declared
            // (supported) state and surfaces a `staleEvidence` diagnostic separately, never a
            // distinct state token.
            | "stale" -> Real
            // missing/none/not-started/pending and any unrecognized authored token: not started.
            | _ -> Pending

    let emptyGovernanceConfig =
        { PolicyPresent = false
          PolicyPointer = None
          CapabilitiesPresent = false
          CapabilitiesPointer = None
          ToolingPresent = false
          ToolingPointer = None }

    let sourceIdentity (path: string) (text: string) : SourceIdentity =
        let artifact =
            match
                ArtifactRef.create
                    path
                    (ArtifactRef.ArtifactKind.Other "generatedSource")
                    ArtifactRef.ArtifactOwner.Sdd
                    true
            with
            | Ok value -> value
            | Error message -> invalidArg (nameof path) message

        { Artifact = artifact
          Digest = SchemaVersion.sha256Text text
          SchemaVersion = Some(SchemaVersion.create 1)
          SchemaStatus = SchemaCompatibilityStatus.Current
          RawSchemaVersion = None }

    let taskStateOf (status: string) =
        match normalize status with
        | "done" -> Real
        | "blocked"
        | "failed" -> Failed
        | _ -> Pending

    let evidencePrefix = "evidence:"
    let taskPrefix = "task:"

    let fromWorkModel
        (model: WorkModel)
        (sources: SourceIdentity list)
        (config: GovernanceConfigPresence)
        (readiness: ReadinessFacts)
        (generator: GeneratorVersion)
        : GovernanceHandoff =
        let tasksById = model.Tasks |> List.map (fun task -> task.Id, task) |> Map.ofList

        // Edge derivation (Kernel.Evidence.build shape: dependent rests on dependency).
        let edges =
            [ for evidence in model.Evidence do
                  for taskRef in evidence.TaskRefs ->
                      { Dependent = evidencePrefix + evidence.Id
                        Dependency = taskPrefix + taskRef }
              for task in model.Tasks do
                  for dependency in task.Dependencies ->
                      { Dependent = taskPrefix + task.Id
                        Dependency = taskPrefix + dependency }

                  for required in task.RequiredEvidence ->
                      { Dependent = taskPrefix + task.Id
                        Dependency = evidencePrefix + required } ]
            |> List.distinct
            |> List.sortBy (fun edge -> edge.Dependent, edge.Dependency)

        let endpointIds =
            edges
            |> List.collect (fun edge -> [ edge.Dependent; edge.Dependency ])
            |> Set.ofList

        // Every declared evidence entry is a node (consumer taint flows over all of them).
        // Rationale is carried only for synthetic/skipped states (data-model), filtering the
        // normalization's empty/"null" sentinel so a real node never reports a spurious rationale.
        let carriedRationale state (rationale: string option) =
            match state with
            | Synthetic
            | Skipped ->
                rationale
                |> Option.filter (fun text ->
                    let trimmed = text.Trim()
                    trimmed <> "" && trimmed <> "null")
            | _ -> None

        let evidenceNodes =
            model.Evidence
            |> List.map (fun evidence ->
                let state = mapEvidenceState evidence.Result evidence.Synthetic

                { Id = evidencePrefix + evidence.Id
                  State = state
                  Rationale = carriedRationale state evidence.Rationale })

        let evidenceNodeIds = evidenceNodes |> List.map (fun node -> node.Id) |> Set.ofList

        // Tasks become nodes when they participate in an edge; state from declared status.
        let taskNodes =
            endpointIds
            |> Set.toList
            |> List.filter (fun id -> id.StartsWith taskPrefix)
            |> List.map (fun id ->
                let taskId = id.Substring taskPrefix.Length

                let state =
                    tasksById
                    |> Map.tryFind taskId
                    |> Option.map (fun task -> taskStateOf task.Status)
                    |> Option.defaultValue Pending

                { Id = id
                  State = state
                  Rationale = None })

        // Evidence referenced by an edge but lacking a declared entry: present-but-pending,
        // so the consumer's build never returns UnknownNode for an SDD-produced handoff.
        let danglingEvidenceNodes =
            endpointIds
            |> Set.toList
            |> List.filter (fun id -> id.StartsWith evidencePrefix && not (evidenceNodeIds.Contains id))
            |> List.map (fun id ->
                { Id = id
                  State = Pending
                  Rationale = None })

        let nodes =
            evidenceNodes @ taskNodes @ danglingEvidenceNodes
            |> List.sortBy (fun node -> node.Id)

        // Governed references: normalized governed/changed paths from the work model.
        let governedReferences =
            model.GovernanceBoundaries
            |> List.map (fun boundary ->
                { Path = boundary.Path.Replace('\\', '/')
                  Owner = boundary.Owner
                  Relationship = boundary.Relationship
                  Kind = None
                  Operation = None })
            |> List.sortBy (fun reference -> reference.Path)

        // Carry existing work-model diagnostics verbatim; append a staleEvidence diagnostic
        // for declared-stale evidence (Governance owns effective freshness).
        let staleEvidenceIds =
            model.Evidence
            |> List.filter (fun evidence -> isStaleEvidenceResult evidence.Result)
            |> List.map (fun evidence -> evidencePrefix + evidence.Id)
            |> List.sort

        let staleDiagnostics =
            if List.isEmpty staleEvidenceIds then
                []
            else
                [ Diagnostics.create
                      "staleEvidence"
                      DiagnosticWarning
                      None
                      None
                      "Declared evidence is stale; effective freshness is computed and enforced by Governance."
                      "Re-run verification to refresh the stale evidence before relying on it."
                      staleEvidenceIds ]

        let diagnostics = model.Diagnostics @ staleDiagnostics |> Diagnostics.sort

        { SchemaVersion = Fsgg.Schemas.governanceHandoffVersion
          ContractVersion = Fsgg.Schemas.governanceHandoffContractVersion
          GeneratorVersion = generator
          WorkId = model.WorkId
          Sources = sources |> List.sortBy (fun source -> source.Artifact.Path)
          Evidence = { Nodes = nodes; Dependencies = edges }
          GovernedReferences = governedReferences
          GovernanceConfig = config
          Readiness = readiness
          Diagnostics = diagnostics }

    let writeNullableString (writer: Utf8JsonWriter) (name: string) (value: string option) =
        match value with
        | Some text -> writer.WriteString(name, text)
        | None -> writer.WriteNull name

    let toJson (handoff: GovernanceHandoff) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", handoff.SchemaVersion)
        writer.WriteString("contractVersion", handoff.ContractVersion)
        writer.WriteString("generatorVersion", $"{handoff.GeneratorVersion.Id}/{handoff.GeneratorVersion.Version}")
        writer.WriteString("workId", handoff.WorkId)

        writer.WriteStartArray("sources")

        handoff.Sources
        |> List.iter (fun source ->
            writer.WriteStartObject()
            writer.WriteString("path", source.Artifact.Path)
            writer.WriteString("digest", $"{source.Digest.Algorithm}:{source.Digest.Value}")

            match source.SchemaVersion with
            | Some version -> writer.WriteNumber("schemaVersion", version.Major)
            | None -> writer.WriteNull "schemaVersion"

            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteStartObject("evidence")
        writer.WriteStartArray("nodes")

        handoff.Evidence.Nodes
        |> List.iter (fun node ->
            writer.WriteStartObject()
            writer.WriteString("id", node.Id)
            writer.WriteString("state", declaredEvidenceStateValue node.State)
            writeNullableString writer "rationale" node.Rationale
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteStartArray("dependencies")

        handoff.Evidence.Dependencies
        |> List.iter (fun edge ->
            writer.WriteStartObject()
            writer.WriteString("dependent", edge.Dependent)
            writer.WriteString("dependency", edge.Dependency)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()

        writer.WriteStartArray("governedReferences")

        handoff.GovernedReferences
        |> List.iter (fun reference ->
            writer.WriteStartObject()
            writer.WriteString("path", reference.Path)
            writer.WriteString("owner", reference.Owner)
            writer.WriteString("relationship", reference.Relationship)
            writeNullableString writer "kind" reference.Kind
            writeNullableString writer "operation" reference.Operation
            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteStartObject("governanceConfig")
        writer.WriteBoolean("policyPresent", handoff.GovernanceConfig.PolicyPresent)

        match handoff.GovernanceConfig.PolicyPointer with
        | Some pointer -> writer.WriteString("policyPointer", pointer)
        | None -> ()

        writer.WriteBoolean("capabilitiesPresent", handoff.GovernanceConfig.CapabilitiesPresent)

        match handoff.GovernanceConfig.CapabilitiesPointer with
        | Some pointer -> writer.WriteString("capabilitiesPointer", pointer)
        | None -> ()

        writer.WriteBoolean("toolingPresent", handoff.GovernanceConfig.ToolingPresent)

        match handoff.GovernanceConfig.ToolingPointer with
        | Some pointer -> writer.WriteString("toolingPointer", pointer)
        | None -> ()

        writer.WriteEndObject()

        writer.WriteStartObject("readiness")
        writer.WriteString("shipDisposition", handoff.Readiness.ShipDisposition)
        writer.WriteString("verificationReadiness", handoff.Readiness.VerificationReadiness)
        writer.WriteStartObject("counts")
        writer.WriteNumber("advisory", handoff.Readiness.AdvisoryCount)
        writer.WriteNumber("warning", handoff.Readiness.WarningCount)
        writer.WriteNumber("blocking", handoff.Readiness.BlockingCount)
        writer.WriteNumber("classifiedObligationsUnmet", handoff.Readiness.ClassifiedObligationsUnmet)
        writer.WriteEndObject()
        writer.WriteStartArray("blockingDiagnosticIds")

        handoff.Readiness.BlockingDiagnosticIds
        |> List.iter (fun id -> writer.WriteStringValue(id: string))

        writer.WriteEndArray()
        writer.WriteStartArray("perViewState")

        handoff.Readiness.PerViewState
        |> List.iter (fun (view, state) ->
            writer.WriteStartObject()
            writer.WriteString("view", view)
            writer.WriteString("state", state)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()

        writer.WriteStartArray("diagnostics")

        handoff.Diagnostics
        |> List.iter (fun diagnostic ->
            writer.WriteStartObject()
            writer.WriteString("id", diagnostic.Id)
            writer.WriteString("severity", Diagnostics.severityValue diagnostic.Severity)
            writer.WriteString("message", diagnostic.Message)
            writer.WriteString("correction", diagnostic.Correction)
            writer.WriteStartArray("relatedIds")

            diagnostic.RelatedIds
            |> List.iter (fun id -> writer.WriteStringValue(id: string))

            writer.WriteEndArray()
            writer.WriteEndObject())

        writer.WriteEndArray()

        writer.WriteEndObject()
        writer.Flush()
        Encoding.UTF8.GetString(stream.ToArray())
