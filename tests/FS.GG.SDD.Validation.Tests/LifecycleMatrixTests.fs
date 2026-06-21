namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness
open FS.GG.SDD.Validation.ValidationRunner
open Xunit

/// US1 (P1) — the lifecycle-output matrix exhaustively exercises every public
/// command × projection × representative state, and a seeded single-cell divergence
/// fails exactly that cell with an actionable diagnostic (FR-002 / FR-006).
module LifecycleMatrixTests =
    let private focusedPlan =
        { defaultPlan with
            LifecycleCommands = [ Init; Plan; Ship ]
            Projections = [ Json; Text; Rich ]
            States = [ "fresh"; "planReady"; "shipped" ] }

    let private lifecycleOptions =
        { defaultOptions with
            OnlyMatrix = Some lifecycleMatrixName
            Plan = Some focusedPlan }

    let private lifecycleMatrix (report: ValidationReport) =
        report.Matrices |> List.find (fun matrix -> matrix.Name = lifecycleMatrixName)

    // The declared cross-product cells carry all three dimensions; reconciliation may
    // append command-only CoverageGap cells (an uncovered command in a focused plan).
    let private declaredCells (matrix: Matrix) =
        matrix.Cells
        |> List.filter (fun cell -> cell.Coordinates |> List.exists (fun (dimension, _) -> dimension = "state"))

    [<Fact>]
    let ``every command x projection x state appears as a cell with a status`` () =
        let report = run lifecycleOptions
        let declared = declaredCells (lifecycleMatrix report)

        Assert.Equal(3 * 3 * 3, declared.Length)

        // INV-1: no declared cell is left at the pending "not yet evaluated" status.
        Assert.DoesNotContain(
            declared,
            fun cell ->
                match cell.Status with
                | NotValidated "not yet evaluated" -> true
                | _ -> false)

    [<Fact>]
    let ``a clean run has no failing lifecycle cell`` () =
        let report = run lifecycleOptions

        Assert.All(
            declaredCells (lifecycleMatrix report),
            fun cell ->
                match cell.Status with
                | Fail diagnostic -> failwith $"unexpected failure at {cell.Coordinates}: {diagnostic.Message}"
                | _ -> ())

    [<Fact>]
    let ``a seeded single-cell divergence fails exactly that cell with a diagnostic`` () =
        let coordinates = [ "command", "plan"; "projection", "json"; "state", "planReady" ]

        let report =
            run
                { lifecycleOptions with
                    InjectedDivergences = [ lifecycleMatrixName, coordinates ] }

        let matrix = lifecycleMatrix report
        let seeded = matrix.Cells |> List.find (fun cell -> cell.Coordinates = coordinates)

        match seeded.Status with
        | Fail diagnostic ->
            Assert.Contains(lifecycleMatrixName, diagnostic.Message)
            Assert.Contains("command=plan", diagnostic.Message)
            Assert.Contains("state=planReady", diagnostic.Message)
        | other -> failwith $"expected Fail, got {other}"

        // Every other declared cell is Pass or SkippedWithReason — never Fail or
        // NotValidated. (Reconciliation CoverageGap cells, which carry no `state`
        // coordinate in a focused plan, are out of scope for this isolation check.)
        declaredCells matrix
        |> List.filter (fun cell -> cell.Coordinates <> coordinates)
        |> List.iter (fun cell ->
            match cell.Status with
            | Pass
            | SkippedWithReason _ -> ()
            | other -> failwith $"unexpected {other} at {cell.Coordinates}")
