namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.Commands.CommandEffects
open FS.GG.SDD.Commands.CommandSerialization
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.CommandWorkflow
open FS.GG.SDD.TestShared
open Xunit

// ADR-0002 Gap B, findings 5–6 (FS-GG/FS.GG.SDD#247). Invariant 2's full-shape golden for the four
// JSON contracts `ReadinessViewGoldenTests` does NOT cover: the command-report (`--json`) and the
// work-model / guidance / governance-handoff views. Those views' analysis/verify/ship siblings are
// byte-goldened; these four were guarded only by field-presence/substring asserts, a depth-1
// `jsonInventory` freeze (`ReleaseContract.fs`), and — for the report — a `reportVersion` literal
// pin. So a *nested* block/field add/remove/rename shipped invisibly (finding 6), and the
// `reportVersion` / `modelVersion` / `viewVersion` literals could stay put while the shape moved
// under them (finding 5).
//
// A byte golden pins the FULL nested shape, so any structural drift reddens the golden; because the
// golden also contains the version literal, the redgreen is exactly the moment to bump the version
// per `docs/release/versioning-policy.md` ("Change class to bump rule": additive ⇒ minor,
// removal/retype ⇒ major). The one release-coupled field (generator version) is re-pinned with the
// shared `FSGG_UPDATE_BASELINE=1` switch, exactly like every sibling golden. The produced views are
// portable (relative paths, content sha256 digests, no timestamps), so the goldens are stable.
//
// Joins `ProcessGlobalEnv` for the same reason the sibling command tests do (feature 067).
[<Collection("ProcessGlobalEnv")>]
module FullShapeGoldenTests =

    let private workId = "247-full-shape-golden"
    let private title = "Full Shape Golden"

    let private goldenPath name =
        Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Commands.Tests", "goldens", "full-shape", name)

    // Same stable-leaf discipline as `ReadinessViewGoldenTests`: the seeded project id derives from
    // the ROOT'S LEAF NAME, so a random temp-dir leaf would vary every view per run. Use a fixed
    // leaf under a unique parent — stable id, preserved isolation/cleanup (feature 067).
    let private stableRoot () =
        let root = Path.Combine(TestSupport.tempDirectory (), workId)
        Directory.CreateDirectory root |> ignore
        root

    // A fully produced project: every generated-view JSON exists on disk after this. Mirrors
    // `ReleaseConformanceTests.producedProject` (verify → ship → agents → refresh).
    let private producedRoot () =
        let root = stableRoot ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore
        TestSupport.runAgents root workId |> ignore
        TestSupport.runRefresh root workId |> ignore
        root

    // command-report (`--json`): the deterministic Init dry-run report, identical to the source used
    // by `CommandReportJsonTests.dryRunReport` (a dry run writes nothing, so it is repeatable).
    let private commandReportJson () =
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
        |> serializeReport

    [<Fact>]
    let ``command-report (--json) matches full-shape golden`` () =
        TestShared.Golden.verify (goldenPath "command-report.json") commandReportJson

    [<Fact>]
    let ``work-model.json matches full-shape golden`` () =
        TestShared.Golden.verify (goldenPath "work-model.json") (fun () ->
            let root = producedRoot ()
            TestSupport.readRelative root $"readiness/{workId}/work-model.json")

    [<Fact>]
    let ``guidance.json matches full-shape golden`` () =
        TestShared.Golden.verify (goldenPath "guidance.json") (fun () ->
            let root = producedRoot ()
            TestSupport.readRelative root $"readiness/{workId}/agent-commands/claude/guidance.json")

    [<Fact>]
    let ``governance-handoff.json matches full-shape golden`` () =
        TestShared.Golden.verify (goldenPath "governance-handoff.json") (fun () ->
            let root = producedRoot ()
            TestSupport.readRelative root $"readiness/{workId}/governance-handoff.json")

    // Finding 5, made an explicit named guard rather than an implicit consequence of the goldens
    // above: the view-version literals are load-bearing. `reportVersion` is already pinned by
    // `CommandReportJsonTests`; these are its `modelVersion` / `viewVersion` siblings, which had no
    // guard. If a view's shape changes, its golden reddens AND this pin is the reminder to move the
    // literal — the two fail together, never silently one without the other.
    [<Fact>]
    let ``work-model modelVersion is pinned to its current contract value`` () =
        let root = producedRoot ()
        let json = TestSupport.readRelative root $"readiness/{workId}/work-model.json"
        Assert.Contains("\"modelVersion\": \"1.1.0\"", json)

    [<Fact>]
    let ``guidance viewVersion is pinned to its current contract value`` () =
        let root = producedRoot ()

        let json =
            TestSupport.readRelative root $"readiness/{workId}/agent-commands/claude/guidance.json"

        Assert.Contains("\"viewVersion\": \"1.0\"", json)
