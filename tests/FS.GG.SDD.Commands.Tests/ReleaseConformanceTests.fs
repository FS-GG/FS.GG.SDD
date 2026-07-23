namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.Json
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ReleaseContract
open FS.GG.SDD.Commands.CommandReports
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal.ViewGeneration
open Xunit

/// Conformance: a produced artifact from a real lifecycle run must match its
/// documented schema-reference entry — no undocumented public field, no
/// documented field absent. The produced artifact is authoritative (FR-015,
/// SC-003). This is the link that keeps the release catalog honest against reality.
module ReleaseConformanceTests =
    let workId = "018-release-readiness"
    let title = "Release Readiness"
    let release = currentRelease ()

    /// Run the full lifecycle through ship + agents + refresh and return the
    /// readiness directory plus a shipped `--json` report.
    let producedProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        let shipReport = TestSupport.runShip root workId title
        TestSupport.runAgents root workId |> ignore
        TestSupport.runRefresh root workId |> ignore
        root, shipReport

    // Full-depth observed key set (ADR-0002 Gap B finding 6 / #261) via the shared walker, so a
    // nested add/remove in a produced artifact surfaces as structured drift, not only a golden diff.
    let topKeys (json: string) = ReleaseContract.fullDepthKeys json

    let refOf (relative: string) =
        match ArtifactRef.create relative ArtifactRef.GeneratedView ArtifactRef.Sdd false with
        | Ok artifact -> artifact
        | Error message -> failwith message

    let documentedSections contract =
        release.Catalog
        |> List.find (fun e -> e.Contract = contract)
        |> fun e -> e.Inventory |> List.map (fun i -> i.Name)

    // #660: the shipped conformance project correctly has no analysis diagnostics/findings.
    // Generate a second, in-memory analysis specimen through the real writer with one diagnostic,
    // then union its observed keys with the clean file. This exercises both nested array shapes
    // without hand-copying their fields or weakening the empty-array guard below.
    let analysisDiagnosticShape () =
        let readiness: AnalysisSummary =
            { WorkId = workId
              Stage = "analyze"
              Status = "blocked"
              AnalysisPath = $"readiness/{workId}/analysis.json"
              SourceCount = 0
              SourceRelationshipCount = 0
              ReadyFindingCount = 0
              AdvisoryCount = 0
              WarningCount = 0
              BlockingCount = 1
              StaleSourceCount = 0
              MissingDispositionCount = 0
              MalformedSourceCount = 0
              GeneratedViewFindingCount = 0
              AcceptedDeferralCount = 0
              Readiness = "needsCorrection" }

        analysisJson
            workId
            (SchemaVersion.currentGeneratorVersion ())
            []
            []
            readiness
            [ unsafeOverwrite $"work/{workId}/spec.md" ]
            []
        |> topKeys

    /// Build the produced-artifact snapshot the pure `evaluate` check consumes.
    let producedArtifacts (root: string) (shipReport) =
        let rd = Path.Combine(root, "readiness", workId)
        let read rel = File.ReadAllText(Path.Combine(rd, rel))

        let jsonProduced contract file =
            let observed = topKeys (read file)

            { Contract = contract
              Source = refOf ("readiness/<id>/" + contract)
              Inventory =
                if contract = "analysis.json" then
                    Set.union (Set.ofList observed) (Set.ofList (analysisDiagnosticShape ()))
                    |> Set.toList
                else
                    observed }

        // Markdown projections: observed sections are the documented sections that
        // actually appear in the produced file (a missing one surfaces as drift).
        let mdProduced contract file =
            let text = read file

            { Contract = contract
              Source = refOf ("readiness/<id>/" + contract)
              Inventory = documentedSections contract |> List.filter text.Contains }

        [ jsonProduced "work-model.json" "work-model.json"
          jsonProduced "analysis.json" "analysis.json"
          jsonProduced "verify.json" "verify.json"
          jsonProduced "ship.json" "ship.json"
          jsonProduced "ship-verdict.json" "ship-verdict.json"
          jsonProduced "governance-handoff.json" "governance-handoff.json"
          mdProduced "summary.md" "summary.md"
          jsonProduced "agent-commands/<target>/guidance.json" "agent-commands/claude/guidance.json"
          mdProduced "agent-commands/<target>/commands.md" "agent-commands/claude/commands.md"
          mdProduced "agent-commands/<target>/skills.md" "agent-commands/claude/skills.md"
          { Contract = "command-report (--json)"
            Source = refOf "readiness/<id>/command-report"
            Inventory = topKeys (serializeReport shipReport) } ]

    [<Fact>]
    let ``T015 every produced artifact conforms to its documented schema-reference entry (SC-003)`` () =
        let root, shipReport = producedProject ()
        let diagnostics = evaluate release (producedArtifacts root shipReport)

        let detail =
            diagnostics |> List.map (fun d -> $"{d.Id}: {d.Message}") |> String.concat "\n"

        Assert.True(List.isEmpty diagnostics, $"Expected produced artifacts to conform, but saw drift:\n{detail}")

    // ADR-0002 Gap B / #291. `fullDepthKeys` is blind to fields under an *empty* array: an empty
    // `foo[]` contributes no `foo[].bar` path, so any nested field reachable only through it escapes
    // `evaluate`. The catalog copes by documenting nested fields only for arrays the clean ready
    // fixture actually populates — but nothing pins that the fixture keeps populating them. This guard
    // makes the invariant explicit and self-describing: every catalogued `array[]` prefix must be
    // observed (>=1 element) in the produced conformance snapshot, so a future producer change that
    // silently empties a catalogued array fails HERE with a precise message ("catalogued array X is
    // empty in the conformance fixture") rather than as an opaque `releaseFieldAbsent` drift.
    // Every array level named by a dotted key, each including its "[]" marker: "sources[].path"
    // -> ["sources[]"]; "generatedViews[].sources[].digest" -> ["generatedViews[]";
    // "generatedViews[].sources[]"]. Both levels matter — an outer array can be populated while a
    // nested one is empty, which is the same blind spot one level down.
    let private arrayPrefixes (name: string) =
        let rec loop (from: int) acc =
            match name.IndexOf("[]", from) with
            | -1 -> List.rev acc
            | idx -> loop (idx + 2) (name.Substring(0, idx + 2) :: acc)

        loop 0 []

    let private cataloguedArrayPrefixes contract =
        documentedSections contract |> List.collect arrayPrefixes |> List.distinct

    [<Fact>]
    let ``T016 every catalogued array is populated (>=1 element) in the conformance fixture (#291)`` () =
        let root, shipReport = producedProject ()
        let produced = producedArtifacts root shipReport

        let empties =
            produced
            |> List.collect (fun p ->
                let observed = Set.ofList p.Inventory

                cataloguedArrayPrefixes p.Contract
                |> List.filter (fun prefix -> not (observed |> Set.exists (fun k -> k.StartsWith prefix)))
                |> List.map (fun prefix ->
                    $"{p.Contract}: catalogued array '{prefix}' is empty in the conformance fixture"))

        let detail = empties |> String.concat "\n"

        Assert.True(
            List.isEmpty empties,
            $"Every catalogued array must be exercised with >=1 element so its nested fields stay drift-checkable (#291):\n{detail}"
        )

    [<Fact>]
    let ``T015 the catalog covers exactly the produced public outputs (no gap, no extra)`` () =
        let root, shipReport = producedProject ()

        let producedContracts =
            producedArtifacts root shipReport
            |> List.map (fun p -> p.Contract)
            |> Set.ofList

        let cataloguedContracts =
            release.Catalog |> List.map (fun e -> e.Contract) |> Set.ofList

        Assert.Equal<Set<string>>(cataloguedContracts, producedContracts)
