namespace FS.GG.SDD.Artifacts.Tests

open System.Diagnostics
open FS.GG.SDD.Artifacts
open Xunit

module NormalizedWorkModelTests =
    [<Fact>]
    let ``NormalizedWorkModel generateWorkModel returns deterministic output contract`` () =
        let result = TestSupport.generationResult "valid-work-item"

        Assert.Equal("002-normalized-work-model", result.WorkId)
        Assert.Equal("readiness/002-normalized-work-model/work-model.json", result.OutputPath)
        Assert.Equal("002-normalized-work-model", result.Model.WorkId)
        Assert.Contains("\"modelVersion\": \"1.0.0\"", result.Json)
        Assert.Equal("sha256", result.OutputDigest.Algorithm)
        Assert.Empty(WorkModel.blockingDiagnostics result.Model)

    [<Fact>]
    let ``NormalizedWorkModel valid fixture includes source traceability and linked ids`` () =
        let model = TestSupport.normalizedModel "valid-work-item"

        TestSupport.assertNoBlockingDiagnostics model
        Assert.Equal("fs-gg-sdd", model.Project.Id)

        Assert.Contains(
            model.Sources,
            fun source ->
                source.Path = "work/002-normalized-work-model/spec.md"
                && source.SchemaStatus = "current"
        )

        Assert.Contains(
            model.Requirements,
            fun requirement -> requirement.Id = "FR-001" && requirement.LinkedTaskIds = [ "T001" ]
        )

        Assert.Contains(
            model.Decisions,
            fun decision -> decision.Id = "DEC-001" && decision.LinkedTaskIds = [ "T001"; "T002" ]
        )

        Assert.Contains(model.Tasks, fun task -> task.Id = "T002" && task.Dependencies = [ "T001" ])

        Assert.Contains(
            model.Tasks,
            fun task ->
                task.Id = "T001"
                && task.RequiredEvidence = [ "EV001" ]
                && task.RequiredSkills = [ "fs-gg-sdd-project" ]
        )

        Assert.Contains(model.Evidence, fun evidence -> evidence.Id = "EV001" && evidence.TaskRefs = [ "T001" ])
        Assert.Contains(model.GovernanceBoundaries, fun boundary -> boundary.Path = ".fsgg/capabilities.yml")

    // #241 (ADR-0002 Gap D, finding 1): the builder path populates GovernanceBoundaries and
    // serializeWorkModel persists them, but parseWorkModel used to hardcode `[]` on the
    // round-trip. Because ship/refresh build the governance handoff *through* parseWorkModel,
    // that zeroing shipped an empty `governedReferences` to Governance. Guard the round-trip.
    [<Fact>]
    let ``NormalizedWorkModel round-trips governance boundaries through parseWorkModel`` () =
        let result = TestSupport.generationResult "valid-work-item"

        // The persisted work-model.json carries the boundary (builder path).
        Assert.Contains(result.Model.GovernanceBoundaries, fun boundary -> boundary.Path = ".fsgg/capabilities.yml")

        let parsed =
            match
                WorkModel.parseWorkModel
                    { Path = result.OutputPath
                      Text = result.Json }
            with
            | Ok model -> model
            | Error diagnostics -> failwith $"Expected a parseable work model, got {diagnostics}"

        // Parsing it back must preserve the boundaries verbatim, not zero them.
        Assert.Equal<string list>(
            result.Model.GovernanceBoundaries |> List.map (fun boundary -> boundary.Path),
            parsed.GovernanceBoundaries |> List.map (fun boundary -> boundary.Path)
        )

        Assert.Contains(parsed.GovernanceBoundaries, fun boundary -> boundary.Path = ".fsgg/capabilities.yml")

    // FS-GG/FS.GG.SDD#266 (ADR-0002 Gap D, finding 2): the sibling of the #241 zeroing. The builder
    // populates Sources (source traceability) and GeneratedViews (the work-model manifest) and
    // serializeWorkModel persists both, but parseWorkModel used to hardcode `[]` for each. Because the
    // `agents`/`refresh` generators build the model *through* parseWorkModel, deriveGuidanceModel's
    // sourceIdentities collapsed to the lone work-model output path. Guard the round-trip end to end.
    [<Fact>]
    let ``NormalizedWorkModel round-trips sources and generated views through parseWorkModel`` () =
        let result = TestSupport.generationResult "valid-work-item"

        // The builder populates both fields, and serializeWorkModel persists them.
        Assert.NotEmpty result.Model.Sources
        Assert.NotEmpty result.Model.GeneratedViews

        let parsed =
            match
                WorkModel.parseWorkModel
                    { Path = result.OutputPath
                      Text = result.Json }
            with
            | Ok model -> model
            | Error diagnostics -> failwith $"Expected a parseable work model, got {diagnostics}"

        // Sources round-trip verbatim — every field is serialized — instead of zeroing to [].
        Assert.Equal<WorkModel.SourceEntry list>(result.Model.Sources, parsed.Sources)

        // Generated views round-trip: the persisted manifests survive the parser instead of collapsing
        // to []. Structural equality is asserted on the serialized fields (view path, kind, and each
        // manifest source's path), not the whole ArtifactRef — writeManifestSource does not persist a
        // manifest source's Kind/Owner, so those default on parse (see parseManifestSource).
        Assert.Equal<string list>(
            result.Model.GeneratedViews |> List.map (fun view -> view.View.Path),
            parsed.GeneratedViews |> List.map (fun view -> view.View.Path)
        )

        Assert.Equal<string list>(
            result.Model.GeneratedViews
            |> List.map (fun view -> GenerationManifest.viewKindValue view.Kind),
            parsed.GeneratedViews
            |> List.map (fun view -> GenerationManifest.viewKindValue view.Kind)
        )

        Assert.Equal<string list>(
            result.Model.GeneratedViews
            |> List.collect (fun view -> view.Sources |> List.map (fun source -> source.Artifact.Path)),
            parsed.GeneratedViews
            |> List.collect (fun view -> view.Sources |> List.map (fun source -> source.Artifact.Path))
        )

        // The reported harm (finding 2): deriveGuidanceModel derives sourceIdentities from
        // model.Sources, so restoring the round-trip stops the set collapsing to the lone work-model
        // output path in the agents/refresh flow.
        let identities = (WorkModel.deriveGuidanceModel parsed).SourceIdentities
        Assert.Contains("work/002-normalized-work-model/spec.md", identities)
        Assert.True(identities.Length > 1, "sourceIdentities collapsed to the work-model output path alone")

    // FS-GG/FS.GG.SDD#342 (item 2 of #338): the last field-collapse hole in the work-model round-trip.
    // serializeWorkModel emits `sourceLocation` for every requirement/decision/task/evidence entry, but
    // parseWorkModel used to hardcode `SourceLocation = None`, so a serialize→parse→serialize cycle
    // silently dropped every populated location to null in the agents/refresh/ship generators that
    // build the model *through* the parser. Guard both the field and the whole-document byte-stability
    // (the sibling of the #241/#266 source/view/boundary guards above; the #266/#242/#215 class).
    [<Fact>]
    let ``NormalizedWorkModel round-trips sourceLocation and is byte-stable through parseWorkModel`` () =
        let result = TestSupport.generationResult "valid-work-item"

        // Precondition: the builder path populates sourceLocation on authored entries, so the
        // round-trip has something to lose.
        Assert.Contains(result.Model.Requirements, fun requirement -> requirement.SourceLocation.IsSome)
        Assert.Contains(result.Model.Tasks, fun task -> task.SourceLocation.IsSome)

        let parsed =
            match
                WorkModel.parseWorkModel
                    { Path = result.OutputPath
                      Text = result.Json }
            with
            | Ok model -> model
            | Error diagnostics -> failwith $"Expected a parseable work model, got {diagnostics}"

        // sourceLocation survives the parser verbatim instead of collapsing to None.
        Assert.Equal<Diagnostics.SourceLocation option list>(
            result.Model.Requirements
            |> List.map (fun requirement -> requirement.SourceLocation),
            parsed.Requirements |> List.map (fun requirement -> requirement.SourceLocation)
        )

        Assert.Equal<Diagnostics.SourceLocation option list>(
            result.Model.Tasks |> List.map (fun task -> task.SourceLocation),
            parsed.Tasks |> List.map (fun task -> task.SourceLocation)
        )

        Assert.Equal<Diagnostics.SourceLocation option list>(
            result.Model.Decisions |> List.map (fun decision -> decision.SourceLocation),
            parsed.Decisions |> List.map (fun decision -> decision.SourceLocation)
        )

        Assert.Equal<Diagnostics.SourceLocation option list>(
            result.Model.Evidence |> List.map (fun evidence -> evidence.SourceLocation),
            parsed.Evidence |> List.map (fun evidence -> evidence.SourceLocation)
        )

        // The whole serialize→parse→serialize cycle is byte-stable (regression lock for the
        // #266/#242/#215 field-collapse class).
        Assert.Equal(result.Json, Serialization.serializeWorkModel parsed)

    [<Fact>]
    let ``NormalizedWorkModel invalid fixtures emit actionable diagnostics`` () =
        [ "requirement-not-typed", "requirementNotTyped"
          "work-model-inconsistent", "workModelInconsistent"
          "prose-structured-mismatch", "proseStructuredMismatch"
          "duplicate-logical-id", "duplicateIdentifier"
          "selected-work-item-mismatch", "missingArtifact" ]
        |> List.iter (fun (fixture, diagnosticId) ->
            let model = TestSupport.normalizedModel fixture
            TestSupport.assertDiagnostic diagnosticId model)

    [<Fact>]
    let ``NormalizedWorkModel selected work item mismatch is diagnosed when requested id differs from spec`` () =
        let snapshots = TestSupport.normalizedSnapshots "selected-work-item-mismatch"

        let model =
            Serialization.normalizeSnapshotsToWorkModel snapshots "003-other-work-model"

        TestSupport.assertDiagnostic "workModelInconsistent" model

    [<Fact>]
    let ``NormalizedWorkModel representative performance stays under one second`` () =
        let stopwatch = Stopwatch.StartNew()
        let result = TestSupport.generationResult "valid-work-item"
        stopwatch.Stop()

        Assert.True(
            stopwatch.ElapsedMilliseconds < 1000L,
            $"Expected generation under 1s but took {stopwatch.ElapsedMilliseconds}ms."
        )

        Assert.DoesNotContain("timestamp", result.Json)
