namespace FS.GG.SDD.Artifacts

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.Identifiers
open FS.GG.SDD.Artifacts.SchemaVersion
open YamlDotNet.RepresentationModel

[<AutoOpen>]
module Evidence =
    type EvidenceKind =
        | Implementation
        | Verification
        | Review
        | GeneratedViewEvidence
        | Synthetic
        | Deferral
        | Note
        | Missing

    type EvidenceSubject = { SubjectType: string; Id: string }

    type EvidenceSourceSnapshot =
        { Label: string
          Path: string
          Digest: string option
          SchemaVersion: int option
          SourceLocation: SourceLocation option }

    type EvidenceSourceReference =
        { ReferenceId: string option
          Kind: string
          Path: string option
          Uri: string option
          Digest: string option
          RelatedSourceId: string option
          Result: string option
          SourceLocation: SourceLocation option }

    type SyntheticDisclosure = { StandsInFor: string; Reason: string }

    type EvidenceDeclaration =
        { Id: EvidenceId
          Kind: EvidenceKind
          Subject: EvidenceSubject
          TaskRefs: TaskId list
          RequirementRefs: RequirementId list
          AcceptanceScenarioRefs: AcceptanceScenarioId list
          ClarificationDecisionRefs: DecisionId list
          ChecklistResultRefs: ChecklistResultId list
          PlanDecisionRefs: PlanDecisionId list
          ObligationRefs: string list
          ArtifactRefs: ArtifactRef list
          SourceRefs: EvidenceSourceReference list
          Result: string
          Synthetic: bool
          SyntheticDisclosure: SyntheticDisclosure option
          Rationale: string option
          Owner: string option
          Scope: string option
          LaterLifecycleVisibility: string option
          Notes: string list
          Source: ArtifactRef
          SourceLocation: SourceLocation option }

    type EvidenceObligation =
        { ObligationId: string
          Kind: string
          SourceArtifactPath: string
          SourceId: string option
          LinkedTaskIds: TaskId list
          LinkedRequirementIds: RequirementId list
          LinkedDecisionIds: string list
          // Feature 077: the originating task's full source-id lineage bag, carried verbatim so
          // scaffolding can grammar-route it into the declaration's typed ref buckets. Recovers
          // the plan-decision id (and any FR it traces to) that task.Requirements/task.Decisions
          // drop for a plan-decision task.
          LinkedSourceIds: string list
          ExpectedEvidenceKinds: string list
          RequiredSkillOrCapabilityTags: string list
          Blocking: bool
          Correction: string }

    type EvidenceArtifact =
        { SchemaVersion: SchemaVersion
          WorkId: WorkId
          Stage: LifecycleStage
          Status: string
          SourceSpec: string
          SourceClarifications: string
          SourceChecklist: string
          SourcePlan: string
          SourceTasks: string
          SourceAnalysis: string
          SourceSnapshots: EvidenceSourceSnapshot list
          Evidence: EvidenceDeclaration list
          LifecycleNotes: string list
          Diagnostics: Diagnostic list }

    let parseEvidenceKind (value: string) =
        match
            if String.IsNullOrEmpty value then
                ""
            else
                value.Trim().ToLowerInvariant()
        with
        | "implementation" -> Implementation
        | "verification" -> Verification
        | "review" -> Review
        | "generated-view" -> GeneratedViewEvidence
        | "generatedview" -> GeneratedViewEvidence
        | "synthetic" -> Synthetic
        | "deferral" -> Deferral
        | "note" -> Note
        | "missing" -> Missing
        | _ -> Verification

    // The inverse serialization mappings, moved here from HandlersEvidence (Commands) so the shared
    // `EvidenceCodec.declarationFields` can drive both the reader and the renderer over one list
    // (FS.GG.SDD#260). Pure functions; every existing call site resolves unchanged via AutoOpen.
    let evidenceKindSourceValue kind =
        match kind with
        | EvidenceKind.Implementation -> "implementation"
        | EvidenceKind.Verification -> "verification"
        | EvidenceKind.Review -> "review"
        | EvidenceKind.GeneratedViewEvidence -> "generated-view"
        | EvidenceKind.Synthetic -> "synthetic"
        | EvidenceKind.Deferral -> "deferral"
        | EvidenceKind.Note -> "note"
        | EvidenceKind.Missing -> "missing"

    let allowedEvidenceResults =
        [ "pass"; "fail"; "deferred"; "missing"; "stale"; "advisory"; "blocked" ]
        |> Set.ofList

    let normalizedEvidenceResult (result: string) =
        (if String.IsNullOrEmpty result then
             ""
         else
             result.Trim().ToLowerInvariant())

    let parseArtifactRefs values =
        values
        |> List.map (fun path -> artifact path (ArtifactKind.Other "evidenceArtifact") ArtifactOwner.Sdd false)

    let parseEvidenceSourceSnapshots root =
        trySequenceAt [ "sourceSnapshots" ] root
        |> Option.map (fun sequence ->
            sequence.Children
            |> Seq.mapi (fun index node ->
                node
                |> tryMapping
                |> Option.map (fun mapping ->
                    // `digest`/`schemaVersion` are `option` because absence is meaningful:
                    // an absent digest means "not snapshotted", not "the empty digest".
                    // Read null-aware (FS.GG.SDD#182) so a bare-null token is absence rather
                    // than `Some "null"`, and blank-aware so an empty value — plain (`digest:`)
                    // or quoted (`digest: ''`), which `isPlainNullScalar` deliberately does not
                    // treat as null — is absence too. Either read as `Some ""` would make
                    // `evidenceSourceSnapshotStale` compare "" against the real digest as a
                    // permanent, unfixable mismatch, and would re-render as a trailing-whitespace
                    // `digest: ` line. Unlike `rationale`, an empty digest is never a real value.
                    { Label = tryScalarAt [ "label" ] mapping |> Option.defaultValue ""
                      Path = tryScalarAt [ "path" ] mapping |> Option.defaultValue ""
                      Digest =
                        tryScalarNonNullAt [ "digest" ] mapping
                        |> Option.filter (String.IsNullOrWhiteSpace >> not)
                      SchemaVersion =
                        tryScalarNonNullAt [ "schemaVersion" ] mapping
                        |> Option.bind (fun value ->
                            match Int32.TryParse value with
                            | true, parsed -> Some parsed
                            | _ -> None)
                      SourceLocation = sourceLocation (index + 1) }))
            |> Seq.choose id
            |> Seq.toList)
        |> Option.defaultValue []

    // Shared field lists — ADR-0002 invariant 1 / FR-007 (FS.GG.SDD#201, #260). One `FieldCodec`
    // list per authored record drives BOTH the reader here and the renderer in `HandlersEvidence`,
    // so a field can no longer be read without being written or vice versa — the read/write
    // asymmetry behind #180 (bare-null disclosure) and #181 (dropped `id`/`digest`/`relatedSourceId`)
    // becomes unrepresentable. Optional scalars read null-aware (a bare-null token is absence; a
    // quoted "null" survives as the literal string).
    module EvidenceCodec =
        let sourceRefSeed: EvidenceSourceReference =
            { ReferenceId = None
              Kind = "artifact"
              Path = None
              Uri = None
              Digest = None
              RelatedSourceId = None
              Result = None
              SourceLocation = None }

        let sourceRefFields: ArtifactCodec.FieldCodec<EvidenceSourceReference> list =
            [ ArtifactCodec.defaultedScalar "kind" "artifact" (fun r -> r.Kind) (fun v r -> { r with Kind = v })
              ArtifactCodec.optionalScalar "id" (fun r -> r.ReferenceId) (fun v r -> { r with ReferenceId = v })
              ArtifactCodec.optionalScalar "path" (fun r -> r.Path) (fun v r -> { r with Path = v })
              ArtifactCodec.optionalScalar "uri" (fun r -> r.Uri) (fun v r -> { r with Uri = v })
              ArtifactCodec.optionalScalar "digest" (fun r -> r.Digest) (fun v r -> { r with Digest = v })
              ArtifactCodec.optionalScalar "relatedSourceId" (fun r -> r.RelatedSourceId) (fun v r ->
                  { r with RelatedSourceId = v })
              ArtifactCodec.optionalScalar "result" (fun r -> r.Result) (fun v r -> { r with Result = v }) ]

        // The disclosure's inner scalars read null-aware into an option-carrying draft (#180); the
        // caller lifts a fully-populated, non-blank draft to `Some SyntheticDisclosure` and everything
        // else (bare null, absence, blank) to `None`, so the undisclosed-synthetic gate stays honest.
        type DisclosureDraft =
            { StandsInFor: string option
              Reason: string option }

        let disclosureDraftSeed = { StandsInFor = None; Reason = None }

        let disclosureFields: ArtifactCodec.FieldCodec<DisclosureDraft> list =
            [ ArtifactCodec.optionalScalar "standsInFor" (fun d -> d.StandsInFor) (fun v d ->
                  { d with StandsInFor = v })
              ArtifactCodec.optionalScalar "reason" (fun d -> d.Reason) (fun v d -> { d with Reason = v }) ]

        // The disclosure draft <-> field projection (the #180 gate lives in `lift`): a blank/partial
        // draft lifts to None (undisclosed), a fully-populated one to Some.
        let liftDisclosure (draft: DisclosureDraft) : SyntheticDisclosure option =
            match draft.StandsInFor, draft.Reason with
            | Some standsInFor, Some reason when
                not (String.IsNullOrWhiteSpace standsInFor)
                && not (String.IsNullOrWhiteSpace reason)
                ->
                Some
                    { StandsInFor = standsInFor
                      Reason = reason }
            | _ -> None

        let lowerDisclosure (d: SyntheticDisclosure) : DisclosureDraft =
            { StandsInFor = Some d.StandsInFor
              Reason = Some d.Reason }

        let subjectSeed: EvidenceSubject = { SubjectType = "task"; Id = "" }

        let subjectFields: ArtifactCodec.FieldCodec<EvidenceSubject> list =
            [ ArtifactCodec.defaultedScalar "type" "task" (fun s -> s.SubjectType) (fun v s ->
                  { s with SubjectType = v })
              ArtifactCodec.defaultedScalar "id" "" (fun s -> s.Id) (fun v s -> { s with Id = v }) ]

        // A placeholder declaration; the semantic layer in `parseEvidenceArtifact` overwrites `Id`,
        // `Source`, and `SourceLocation` (parse provenance) and applies the subject-type ref merge
        // after `foldInto`, so these seed values never reach the decoded result.
        let declarationSeed: EvidenceDeclaration =
            { Id = { Value = "EV000" }
              Kind = Verification
              Subject = subjectSeed
              TaskRefs = []
              RequirementRefs = []
              AcceptanceScenarioRefs = []
              ClarificationDecisionRefs = []
              ChecklistResultRefs = []
              PlanDecisionRefs = []
              ObligationRefs = []
              ArtifactRefs = []
              SourceRefs = []
              Result = "pending"
              Synthetic = false
              SyntheticDisclosure = None
              Rationale = None
              Owner = None
              Scope = None
              LaterLifecycleVisibility = None
              Notes = []
              Source = sourceArtifact "work/seed/evidence.yml" ArtifactKind.Evidence
              SourceLocation = None }

        // The whole authored declaration, in emission order — `id` first, so the artifact's `evidence`
        // `recordList` frames each item as `  - id: …`. One list drives both the reader and the
        // renderer (FR-007). The semantic layer still validates `id` (malformed → skip + diagnostic)
        // and re-applies it after decode; typed-id ref lists read leniently — the malformed-ref
        // diagnostics stay the semantic layer's job.
        let declarationFields: ArtifactCodec.FieldCodec<EvidenceDeclaration> list =
            [ ArtifactCodec.requiredScalar "id" (fun d -> d.Id.Value) (fun v d -> { d with Id = { Value = v } })
              ArtifactCodec.mappedScalar "kind" evidenceKindSourceValue parseEvidenceKind (fun d -> d.Kind) (fun v d ->
                  { d with Kind = v })
              ArtifactCodec.nested "subject" subjectFields subjectSeed (fun d -> d.Subject) (fun v d ->
                  { d with Subject = v })
              ArtifactCodec.refList
                  "taskRefs"
                  Identifiers.createTaskId
                  (fun (id: TaskId) -> id.Value)
                  (fun d -> d.TaskRefs)
                  (fun v d -> { d with TaskRefs = v })
              ArtifactCodec.refList
                  "requirementRefs"
                  Identifiers.createRequirementId
                  (fun (id: RequirementId) -> id.Value)
                  (fun d -> d.RequirementRefs)
                  (fun v d -> { d with RequirementRefs = v })
              ArtifactCodec.refList
                  "acceptanceScenarioRefs"
                  Identifiers.createAcceptanceScenarioId
                  (fun (id: AcceptanceScenarioId) -> id.Value)
                  (fun d -> d.AcceptanceScenarioRefs)
                  (fun v d -> { d with AcceptanceScenarioRefs = v })
              ArtifactCodec.refList
                  "clarificationDecisionRefs"
                  Identifiers.createDecisionId
                  (fun (id: DecisionId) -> id.Value)
                  (fun d -> d.ClarificationDecisionRefs)
                  (fun v d -> { d with ClarificationDecisionRefs = v })
              ArtifactCodec.refList
                  "checklistResultRefs"
                  Identifiers.createChecklistResultId
                  (fun (id: ChecklistResultId) -> id.Value)
                  (fun d -> d.ChecklistResultRefs)
                  (fun v d -> { d with ChecklistResultRefs = v })
              ArtifactCodec.refList
                  "planDecisionRefs"
                  Identifiers.createPlanDecisionId
                  (fun (id: PlanDecisionId) -> id.Value)
                  (fun d -> d.PlanDecisionRefs)
                  (fun v d -> { d with PlanDecisionRefs = v })
              ArtifactCodec.alwaysInlineList
                  "obligationRefs"
                  (fun d -> d.ObligationRefs)
                  // The reader distinct+sorts obligationRefs to match the pre-codec parser (the
                  // renderer already distinct+sorts every inline list); notes deliberately do not.
                  (fun v d ->
                      { d with
                          ObligationRefs = v |> List.distinct |> List.sort })
              ArtifactCodec.alwaysInlineList
                  "artifacts"
                  (fun d -> d.ArtifactRefs |> List.map (fun (a: ArtifactRef) -> a.Path))
                  (fun v d ->
                      { d with
                          ArtifactRefs = parseArtifactRefs v })
              ArtifactCodec.recordList "sourceRefs" sourceRefFields sourceRefSeed (fun d -> d.SourceRefs) (fun v d ->
                  { d with SourceRefs = v })
              ArtifactCodec.mappedScalar "result" normalizedEvidenceResult id (fun d -> d.Result) (fun v d ->
                  { d with Result = v })
              ArtifactCodec.boolScalar "synthetic" false (fun d -> d.Synthetic) (fun v d -> { d with Synthetic = v })
              ArtifactCodec.optionalNestedVia
                  "syntheticDisclosure"
                  disclosureFields
                  disclosureDraftSeed
                  liftDisclosure
                  lowerDisclosure
                  (fun d -> d.SyntheticDisclosure)
                  (fun v d -> { d with SyntheticDisclosure = v })
              ArtifactCodec.optionalScalar "rationale" (fun d -> d.Rationale) (fun v d -> { d with Rationale = v })
              ArtifactCodec.optionalScalar "owner" (fun d -> d.Owner) (fun v d -> { d with Owner = v })
              ArtifactCodec.optionalScalar "scope" (fun d -> d.Scope) (fun v d -> { d with Scope = v })
              ArtifactCodec.optionalScalar "laterLifecycleVisibility" (fun d -> d.LaterLifecycleVisibility) (fun v d ->
                  { d with LaterLifecycleVisibility = v })
              ArtifactCodec.alwaysInlineList "notes" (fun d -> d.Notes) (fun v d -> { d with Notes = v }) ]

    // `parseEvidenceSourceRefs`/`parseSyntheticDisclosure` were retired when the declaration moved onto
    // `declarationFields` (FS.GG.SDD#260): its `recordList "sourceRefs"` and
    // `optionalNestedVia "syntheticDisclosure"` now own both directions for those records.

    let workIdFromEvidencePath (path: string) =
        let normalized = normalizePath path
        let parts = normalized.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries)

        if parts.Length >= 3 && parts.[0] = "work" then
            parts.[1]
        else
            "unknown-work"

    let parseEvidenceArtifact (snapshot: FileSnapshot) =
        let artifact = sourceArtifact snapshot.Path ArtifactKind.Evidence

        match yamlRoot artifact "Evidence file is empty." 0 snapshot.Text with
        | Error diagnostics -> Error diagnostics
        | Ok root ->
            let version, versionDiagnostics = schemaVersion artifact root

            let workIdValue =
                tryScalarAt [ "workId" ] root
                |> Option.defaultValue (workIdFromEvidencePath snapshot.Path)

            let workId = Identifiers.createWorkId workIdValue

            let stage =
                tryScalarAt [ "stage" ] root
                |> Option.bind (Identifiers.parseStage >> Result.toOption)
                |> Option.defaultValue LifecycleStage.Evidence

            // Each evidence node yields (declaration option, diagnostics). Malformed cross-
            // references and a whole entry skipped for a malformed id are surfaced as blocking
            // diagnostics instead of being silently dropped by the parse*Ids helpers (#70/§2.5).
            let evidenceParse =
                trySequenceAt [ "evidence" ] root
                |> Option.map (fun sequence ->
                    sequence.Children
                    |> Seq.mapi (fun index node ->
                        match node |> tryMapping with
                        | None -> None, []
                        | Some mapping ->
                            match tryScalarAt [ "id" ] mapping with
                            | None -> None, []
                            | Some rawId ->
                                let refDiagnostics =
                                    [ scalarList [ "taskRefs" ] mapping
                                      |> malformedRefs Identifiers.createTaskId
                                      |> List.map (Diagnostics.malformedReference artifact "task")
                                      scalarList [ "requirementRefs" ] mapping
                                      |> malformedRefs Identifiers.createRequirementId
                                      |> List.map (Diagnostics.malformedReference artifact "requirement")
                                      scalarList [ "clarificationDecisionRefs" ] mapping
                                      |> malformedRefs Identifiers.createDecisionId
                                      |> List.map (Diagnostics.malformedReference artifact "decision") ]
                                    |> List.concat

                                match Identifiers.createEvidenceId rawId with
                                | Error _ ->
                                    None, (Diagnostics.malformedReference artifact "evidence" rawId :: refDiagnostics)
                                | Ok id ->
                                    // The shared `declarationFields` codec decodes every authored field
                                    // (FR-007); the semantic layer here owns what is NOT serialization:
                                    // the parse-assigned `Id`/`Source`/`SourceLocation`, and the
                                    // subject-type ref merge — a `task`/`requirement` subject prepends
                                    // its id into the corresponding ref list. Malformed-ref diagnostics
                                    // are computed above (`refDiagnostics`); the codec read is lenient.
                                    let decoded =
                                        match
                                            ArtifactCodec.foldInto
                                                EvidenceCodec.declarationFields
                                                EvidenceCodec.declarationSeed
                                                mapping
                                        with
                                        | Ok value -> value
                                        | Error _ -> EvidenceCodec.declarationSeed

                                    let taskRefs =
                                        match decoded.Subject.SubjectType with
                                        | "task" ->
                                            (Identifiers.createTaskId decoded.Subject.Id
                                             |> Result.toOption
                                             |> Option.toList)
                                            @ decoded.TaskRefs
                                        | _ -> decoded.TaskRefs

                                    let requirementRefs =
                                        match decoded.Subject.SubjectType with
                                        | "requirement" ->
                                            (Identifiers.createRequirementId decoded.Subject.Id
                                             |> Result.toOption
                                             |> Option.toList)
                                            @ decoded.RequirementRefs
                                        | _ -> decoded.RequirementRefs

                                    Some
                                        { decoded with
                                            Id = id
                                            TaskRefs = taskRefs
                                            RequirementRefs = requirementRefs
                                            Source = artifact
                                            SourceLocation = sourceLocation (index + 1) },
                                    refDiagnostics)
                    |> Seq.toList)
                |> Option.defaultValue []

            let evidence = evidenceParse |> List.choose fst
            let referenceDiagnostics = evidenceParse |> List.collect snd

            let duplicateDiagnostics =
                evidence
                |> List.groupBy (fun declaration -> declaration.Id.Value)
                |> List.choose (fun (id, declarations) ->
                    if List.length declarations > 1 then
                        Some(
                            Diagnostics.duplicateIdentifier
                                artifact
                                id
                                (declarations |> List.choose (fun declaration -> declaration.SourceLocation))
                        )
                    else
                        None)

            let artifactDiagnostics =
                [ if stage <> LifecycleStage.Evidence then
                      Diagnostics.workModelInconsistent
                          artifact
                          $"Evidence stage '{Identifiers.stageValue stage}' is not 'evidence'."
                          "Set stage: evidence before rerunning."
                          [ Identifiers.stageValue stage ] ]

            match version, workId, versionDiagnostics with
            | Some schema, Ok workId, [] ->
                Ok
                    { SchemaVersion = schema
                      WorkId = workId
                      Stage = stage
                      Status = tryScalarAt [ "status" ] root |> Option.defaultValue "draft"
                      SourceSpec =
                        tryScalarAt [ "sourceSpec" ] root
                        |> Option.defaultValue $"work/{workId.Value}/spec.md"
                      SourceClarifications =
                        tryScalarAt [ "sourceClarifications" ] root
                        |> Option.defaultValue $"work/{workId.Value}/clarifications.md"
                      SourceChecklist =
                        tryScalarAt [ "sourceChecklist" ] root
                        |> Option.defaultValue $"work/{workId.Value}/checklist.md"
                      SourcePlan =
                        tryScalarAt [ "sourcePlan" ] root
                        |> Option.defaultValue $"work/{workId.Value}/plan.md"
                      SourceTasks =
                        tryScalarAt [ "sourceTasks" ] root
                        |> Option.defaultValue $"work/{workId.Value}/tasks.yml"
                      SourceAnalysis =
                        tryScalarAt [ "sourceAnalysis" ] root
                        |> Option.defaultValue $"readiness/{workId.Value}/analysis.json"
                      SourceSnapshots = parseEvidenceSourceSnapshots root
                      Evidence = evidence |> List.sortBy (fun declaration -> declaration.Id.Value)
                      LifecycleNotes = scalarList [ "lifecycleNotes" ] root
                      Diagnostics =
                        duplicateDiagnostics @ artifactDiagnostics @ referenceDiagnostics
                        |> Diagnostics.sort }
            | _ ->
                let workIdDiagnostics =
                    match workId with
                    | Error message ->
                        [ Diagnostics.workModelInconsistent
                              artifact
                              message
                              "Use a valid work id in evidence.yml."
                              [ workIdValue ] ]
                    | Ok _ -> []

                Error(versionDiagnostics @ duplicateDiagnostics @ workIdDiagnostics)

    let parseEvidence (snapshot: FileSnapshot) =
        parseEvidenceArtifact snapshot |> Result.map (fun artifact -> artifact.Evidence)
