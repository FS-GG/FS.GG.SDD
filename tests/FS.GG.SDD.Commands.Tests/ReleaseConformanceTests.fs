namespace FS.GG.SDD.Commands.Tests

open System.IO
open System.Text.Json
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.ReleaseContract
open FS.GG.SDD.Commands.CommandSerialization
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

    let topKeys (json: string) =
        (JsonDocument.Parse json).RootElement.EnumerateObject()
        |> Seq.map (fun p -> p.Name)
        |> Seq.toList

    let refOf (relative: string) =
        match ArtifactRef.create relative ArtifactRef.GeneratedView ArtifactRef.Sdd false with
        | Ok artifact -> artifact
        | Error message -> failwith message

    let documentedSections contract =
        release.Catalog
        |> List.find (fun e -> e.Contract = contract)
        |> fun e -> e.Inventory |> List.map (fun i -> i.Name)

    /// Build the produced-artifact snapshot the pure `evaluate` check consumes.
    let producedArtifacts (root: string) (shipReport) =
        let rd = Path.Combine(root, "readiness", workId)
        let read rel = File.ReadAllText(Path.Combine(rd, rel))

        let jsonProduced contract file =
            { Contract = contract
              Source = refOf ("readiness/<id>/" + contract)
              Inventory = topKeys (read file) }

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
