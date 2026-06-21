namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.ArtifactRef
open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.ReleaseContract
open Xunit

module ReleaseReadinessCheckTests =
    let release = currentRelease ()

    /// A produced snapshot whose observed inventory matches each catalog entry's
    /// documented inventory exactly (a fully-conformant, fully-baselined release).
    let conformantProduced (r: ReleaseReadiness) =
        r.Catalog
        |> List.map (fun entry ->
            { Contract = entry.Contract
              Source = entry.SourceArtifact
              Inventory = entry.Inventory |> List.map (fun item -> item.Name) })

    let diagIds (diagnostics: Diagnostics.Diagnostic list) =
        diagnostics |> List.map (fun diagnostic -> diagnostic.Id)

    // ===== coverage: every public output is catalogued (SC-002) =====

    [<Fact>]
    let ``T019 the catalog covers every enumerable GeneratedViewKind plus the command-output report`` () =
        let viewKinds =
            release.Catalog
            |> List.choose (fun entry ->
                match entry.Kind with
                | GeneratedViewContract(kind, _) -> Some kind
                | CommandOutputContract -> None)
            |> List.distinct
            |> Set.ofList

        for kind in [ WorkModel; Analysis; Verify; Ship; Summary; AgentCommands; GovernanceHandoff ] do
            Assert.Contains(kind, viewKinds)

        Assert.Contains(release.Catalog, fun entry -> entry.Kind = CommandOutputContract)

    [<Fact>]
    let ``T019 a fully conformant, fully baselined release evaluates ready (no diagnostics)`` () =
        Assert.Empty(evaluate release (conformantProduced release))

    // ===== readiness fails by absence, never passes by it (FR-012) =====

    [<Fact>]
    let ``T019 a produced output with no catalog entry is reported not-ready`` () =
        let trimmed = { release with Catalog = release.Catalog |> List.filter (fun e -> e.Contract <> "ship.json") }
        // ship.json is still produced, but no longer catalogued
        let diagnostics = evaluate trimmed (conformantProduced release)
        Assert.Contains("releaseOutputUndocumented", diagIds diagnostics)

    [<Fact>]
    let ``T019 a catalog entry with no locking baseline is reported not-ready`` () =
        let withoutBaseline =
            { release with
                Catalog =
                    release.Catalog
                    |> List.map (fun e -> if e.Contract = "verify.json" then { e with BaselinePresent = false } else e) }

        let diagnostics = evaluate withoutBaseline (conformantProduced withoutBaseline)
        Assert.Contains("releaseBaselineMissing", diagIds diagnostics)

    [<Fact>]
    let ``T019 an entry with an empty source artifact is reported not-ready`` () =
        // an entry whose SourceArtifact.Path is blank cannot be constructed via
        // ArtifactRef.create, so model the gap by checking evaluate's source guard
        // through a catalog whose entry has been blanked via record copy.
        let emptyRef = { (release.Catalog.Head.SourceArtifact) with Path = "" }
        let blanked =
            { release with
                Catalog = release.Catalog |> List.mapi (fun i e -> if i = 0 then { e with SourceArtifact = emptyRef } else e) }

        let diagnostics = evaluate blanked (conformantProduced blanked)
        Assert.Contains("releaseSourceMissing", diagIds diagnostics)

    // ===== field-level drift, produced artifact authoritative (FR-015) =====

    [<Fact>]
    let ``T019 an undocumented produced field is reported as drift`` () =
        let produced =
            conformantProduced release
            |> List.map (fun p ->
                if p.Contract = "work-model.json" then { p with Inventory = "surpriseField" :: p.Inventory } else p)

        Assert.Contains("releaseFieldUndocumented", diagIds (evaluate release produced))

    [<Fact>]
    let ``T019 a documented field absent from the produced artifact is reported as drift`` () =
        let produced =
            conformantProduced release
            |> List.map (fun p ->
                if p.Contract = "work-model.json" then { p with Inventory = p.Inventory |> List.filter ((<>) "tasks") } else p)

        Assert.Contains("releaseFieldAbsent", diagIds (evaluate release produced))
