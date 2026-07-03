namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness
open FS.GG.SDD.Validation.ValidationRunner
open Xunit

/// US2 (P2) — no public surface escapes coverage. An uncovered real surface is a
/// visible `coverageGap`; a declared entry naming a vanished surface is a detectable
/// failure. The real produced surface is authoritative (FR-009 / FR-012 / INV-7).
module CoverageGapTests =
    let private cellsOf matrixName (report: ValidationReport) =
        (report.Matrices |> List.find (fun matrix -> matrix.Name = matrixName)).Cells

    [<Fact>]
    let ``an uncovered real command is a coverageGap and the run does not pass`` () =
        // Declare only `init`; every other real SddCommand is uncovered.
        let plan =
            { defaultPlan with
                LifecycleCommands = [ Init ]
                Projections = [ Json ]
                States = [ "fresh" ] }

        let report =
            run
                { defaultOptions with
                    OnlyMatrix = Some lifecycleMatrixName
                    Plan = Some plan }

        let gaps =
            cellsOf lifecycleMatrixName report
            |> List.choose (fun cell ->
                match cell.Status with
                | CoverageGap surface -> Some surface
                | _ -> None)

        Assert.Contains(gaps, fun surface -> surface.Contains "refresh")
        Assert.False(report.Summary.OverallPassed)

    [<Fact>]
    let ``a declared contract absent from the real catalog is a detectable failure`` () =
        let plan =
            { defaultPlan with
                LifecycleCommands = [ Init ]
                Projections = [ Json ]
                States = [ "fresh" ]
                BaselineContracts = defaultPlan.BaselineContracts @ [ "ghost-contract.json" ] }

        let report =
            run
                { defaultOptions with
                    OnlyMatrix = Some lifecycleMatrixName
                    Plan = Some plan }

        let staleFailures =
            cellsOf baselineMatrixName report
            |> List.choose (fun cell ->
                match cell.Status with
                | Fail diagnostic -> Some diagnostic.Message
                | _ -> None)

        Assert.Contains(staleFailures, fun message -> message.Contains "ghost-contract.json")
        Assert.False(report.Summary.OverallPassed)

    [<Fact>]
    let ``a clean full-surface plan reports no coverage gaps`` () =
        // The compatibility matrix is cheapest; the default plan's commands and
        // contracts exactly match the real surface, so reconciliation finds nothing.
        let report =
            run
                { defaultOptions with
                    OnlyMatrix = Some compatibilityMatrixName }

        let allGaps =
            report.Matrices
            |> List.collect (fun matrix -> matrix.Cells)
            |> List.filter (fun cell ->
                match cell.Status with
                | CoverageGap _ -> true
                | _ -> false)

        Assert.Empty allGaps
