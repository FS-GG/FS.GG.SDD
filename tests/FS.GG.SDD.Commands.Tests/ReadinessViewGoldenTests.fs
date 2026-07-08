namespace FS.GG.SDD.Commands.Tests

open System.IO
open FS.GG.SDD.TestShared
open Xunit

// Feature 068 / US1 / FR-002-003: byte-golden pins for the three readiness views
// (analysis/verify/ship). Before 068 these views were only asserted *self-deterministic*
// (run 3x, first==second==third) plus substring presence — a refactor that consistently
// reordered or renamed envelope fields stayed green. These goldens make structural drift
// fail. The views are portable (no absolute paths / timestamps; digests are content
// sha256); the one release-coupled field is the generator version, re-pinned with the
// shared FSGG_UPDATE_BASELINE=1 switch exactly like every other baseline.
//
// Joins ProcessGlobalEnv for the same reason the sibling command tests do (feature 067).
[<Collection("ProcessGlobalEnv")>]
module ReadinessViewGoldenTests =

    let private workId = "068-readiness-golden"
    let private title = "Readiness Golden"

    let private goldenPath name =
        Path.Combine(TestSupport.repoRoot, "tests", "FS.GG.SDD.Commands.Tests", "goldens", "readiness", name)

    // The seeded `.fsgg/project.yml` (and every downstream digest / work-model over it) embeds a
    // project id that `Foundation.projectIdFromRoot` derives from the ROOT'S LEAF NAME. A random
    // temp-dir leaf therefore makes the views vary per run — invisible to the in-process
    // determinism tests, caught by these cross-run goldens. Use a stable leaf under a unique parent
    // so the id is fixed while isolation/cleanup (feature 067) is preserved. (This is exactly the
    // ambient-derivation sharp edge US3b tightens.)
    let private stableRoot () =
        let root = Path.Combine(TestSupport.tempDirectory (), workId)
        Directory.CreateDirectory root |> ignore
        root

    [<Fact>]
    let ``analysis.json matches committed golden`` () =
        TestShared.Golden.verify (goldenPath "analysis.json") (fun () ->
            let root = stableRoot ()
            TestSupport.initializeTasksReadyProject root workId title
            TestSupport.runAnalyze root workId title |> ignore
            TestSupport.readRelative root $"readiness/{workId}/analysis.json")

    [<Fact>]
    let ``verify.json matches committed golden`` () =
        TestShared.Golden.verify (goldenPath "verify.json") (fun () ->
            let root = stableRoot ()
            TestSupport.initializeEvidencedProject root workId title
            TestSupport.runVerify root workId title |> ignore
            TestSupport.readRelative root $"readiness/{workId}/verify.json")

    [<Fact>]
    let ``ship.json matches committed golden`` () =
        TestShared.Golden.verify (goldenPath "ship.json") (fun () ->
            let root = stableRoot ()
            TestSupport.initializeVerifiedProject root workId title
            TestSupport.runShip root workId title |> ignore
            TestSupport.readRelative root $"readiness/{workId}/ship.json")

    // Feature 092 / ADR-0026: the durable-generated merge-boundary verdict. This is the one
    // readiness view that gets *committed*, so a byte-golden is not merely a refactor net — it pins
    // the exact bytes that land in every consumer's git history.
    [<Fact>]
    let ``ship-verdict.json matches committed golden`` () =
        TestShared.Golden.verify (goldenPath "ship-verdict.json") (fun () ->
            let root = stableRoot ()
            TestSupport.initializeVerifiedProject root workId title
            TestSupport.runShip root workId title |> ignore
            TestSupport.readRelative root $"readiness/{workId}/ship-verdict.json")

    // FR-004: the compaction is the point. A ship-ready verdict is exactly 20 lines against
    // ship.json's 279 — assert it here, where the golden makes a regression obvious.
    [<Fact>]
    let ``ship-verdict.json is at most 20 lines for a ship-ready item`` () =
        let root = stableRoot ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore

        let verdict =
            (TestSupport.readRelative root $"readiness/{workId}/ship-verdict.json")
                .Replace("\r\n", "\n")
                .TrimEnd('\n')

        let lineCount = verdict.Split('\n').Length
        Assert.True(lineCount <= 20, $"ship-verdict.json grew to {lineCount} lines:\n{verdict}")

    // Feature 068 / US2 sub-cluster 2b safety net: refresh's summary.md renders the perViewState
    // table and refreshDisposition — exactly the currency-string output the ViewCurrencyClass /
    // RefreshDisposition.EarlyStage DU refactor could perturb. Pinning it byte-exact guards the
    // codebase's worst complexity hotspot (computeRefreshPlan) against a silent output change.
    [<Fact>]
    let ``refresh summary.md matches committed golden`` () =
        TestShared.Golden.verify (goldenPath "summary.md") (fun () ->
            let root = stableRoot ()
            TestSupport.initializeVerifiedProject root workId title
            TestSupport.runShip root workId title |> ignore
            TestSupport.runRefresh root workId |> ignore
            TestSupport.readRelative root $"readiness/{workId}/summary.md")
