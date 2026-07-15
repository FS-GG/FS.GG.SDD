namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandRendering
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.TestShared
open Xunit

// §4 (2026-07-15 architecture review): the `--json` projection has full-shape byte-goldens
// (`FullShapeGoldenTests` / `ReadinessViewGoldenTests`), but the `--text` projection
// (`CommandRendering.renderText`) was guarded only by substring asserts (`TextProjectionTests`)
// — so a reordered/added/dropped `key: value` line, a reworded footer, or a changed stage-table
// shape shipped invisibly. These byte-goldens pin the whole text projection for two representative
// reports, widening the determinism net across the `command × --json/--text` matrix.
//
// Both sources are deterministic and portable — the text projection emits no absolute paths (every
// path field is workspace-relative, e.g. `readiness/<id>/ship.json`, and the report never contains
// the temp root) and no timestamps; the two version-coupled lines (`toolVersion`) re-pin with the
// shared `FSGG_UPDATE_BASELINE=1` switch, exactly like every sibling golden.
//
// Joins `ProcessGlobalEnv` for the same reason the sibling command/golden tests do (feature 067).
[<Collection("ProcessGlobalEnv")>]
module TextProjectionGoldenTests =

    let private goldenPath name =
        Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Commands.Tests", "goldens", "text-projection", name)

    // init (`--text`): the deterministic Init dry-run report — the SAME repeatable, path-free source
    // as `FullShapeGoldenTests.commandReportJson`, rendered as text instead of JSON. A dry run writes
    // nothing, so the fixed-leaf fixture dir keeps the derived work id stable across runs.
    let private initReportText () =
        let root =
            Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "lifecycle-commands", "deterministic-report")

        let request =
            { TestSupport.request Init root with
                DryRun = true }

        let model, effects = init request

        interpretAll root true effects
        |> List.fold (fun state result -> update (EffectInterpreted result) state |> fst) model
        |> fun state -> update BuildReport state |> fst
        |> fun state -> state.Report.Value
        |> renderText

    [<Fact>]
    let ``init text projection matches golden`` () =
        TestShared.Golden.verify (goldenPath "init.txt") initReportText

    // ship (`--text`): a fully produced, ship-ready report — the richest text block (the lifecycle
    // stage table, evidence/test count fan-out, disposition, and the "done" footer). Same stable-leaf
    // discipline as `FullShapeGoldenTests`: a fixed work id under a unique parent, so the id-derived
    // relative paths stay byte-stable while isolation/cleanup are preserved (feature 067).
    let private workId = "247-text-projection-golden"
    let private title = "Text Projection Golden"

    let private shipReportText () =
        let root = Path.Combine(TestSupport.tempDirectory (), workId)
        Directory.CreateDirectory root |> ignore
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> renderText

    [<Fact>]
    let ``ship text projection matches golden`` () =
        TestShared.Golden.verify (goldenPath "ship.txt") shipReportText
