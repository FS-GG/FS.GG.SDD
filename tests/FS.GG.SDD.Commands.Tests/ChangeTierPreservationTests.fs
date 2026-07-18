namespace FS.GG.SDD.Commands.Tests

open Xunit

/// FS.GG.SDD#573. A stage that re-scaffolds a front matter whose `changeTier` it does not own
/// (`specify`/`plan`) must CARRY the tier established at charter, not reset it to the `tier1`
/// template default. `changeTier` is a free authored string, so a `tier2` (docs/test-only) item
/// stays `tier2` through the generated spec and plan.
module ChangeTierPreservationTests =
    let private workId = "573-tier-preservation"
    let private title = "Tier Preservation"
    let private charterPath = $"work/{workId}/charter.md"
    let private specPath = $"work/{workId}/spec.md"
    let private planPath = $"work/{workId}/plan.md"

    // Charter the item, then author its tier as `tier2` (the charter template defaults `tier1`;
    // `changeTier` is a free authored string an author edits — exactly what RM4 did).
    let private tier2CharteredProject () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore

        let tier2Charter =
            (TestSupport.readRelative root charterPath).Replace("changeTier: tier1", "changeTier: tier2")

        Assert.Contains("changeTier: tier2", tier2Charter)
        TestSupport.writeRelative root charterPath tier2Charter
        root

    [<Fact>]
    let ``specify carries the chartered tier2 into the generated spec front matter`` () =
        let root = tier2CharteredProject ()
        TestSupport.runSpecify root workId title |> ignore

        Assert.Contains("changeTier: tier2", TestSupport.readRelative root specPath)
        Assert.DoesNotContain("changeTier: tier1", TestSupport.readRelative root specPath)

    [<Fact>]
    let ``plan carries the chartered tier2 into the generated plan front matter`` () =
        let root = tier2CharteredProject ()
        TestSupport.runSpecify root workId title |> ignore
        TestSupport.runClarify root workId title |> ignore
        TestSupport.runChecklist root workId title |> ignore
        TestSupport.runPlan root workId title |> ignore

        Assert.Contains("changeTier: tier2", TestSupport.readRelative root planPath)
        Assert.DoesNotContain("changeTier: tier1", TestSupport.readRelative root planPath)

    // The no-charter / default edge stays `tier1` (the template default is preserved when the
    // charter authored nothing different).
    [<Fact>]
    let ``specify defaults to tier1 when the charter left the template default`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeProject root
        TestSupport.runCharter root workId title |> ignore
        TestSupport.runSpecify root workId title |> ignore

        Assert.Contains("changeTier: tier1", TestSupport.readRelative root specPath)
