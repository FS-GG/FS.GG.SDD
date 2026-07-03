namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness
open FS.GG.SDD.Validation.ValidationRunner
open Xunit

/// US1 (P1) — every catalogued output reproduces byte-identically over identical
/// inputs and under a perturbed host environment, and carries no ANSI (FR-003 /
/// INV-3 / INV-3a).
module DeterminismMatrixTests =
    let private focusedPlan =
        { defaultPlan with
            DeterminismOutputs = [ "verify.json"; "ship.json"; "command-report (--json)" ] }

    let private determinismOptions =
        { defaultOptions with
            OnlyMatrix = Some determinismMatrixName
            Plan = Some focusedPlan }

    let private determinismMatrix (report: ValidationReport) =
        report.Matrices |> List.find (fun matrix -> matrix.Name = determinismMatrixName)

    // Declared cells carry the `environment` dimension; reconciliation may append
    // output-only CoverageGap cells for produced views a focused plan omits.
    let private declaredCells (matrix: Matrix) =
        matrix.Cells
        |> List.filter (fun cell ->
            cell.Coordinates
            |> List.exists (fun (dimension, _) -> dimension = "environment"))

    [<Fact>]
    let ``every output x environment is a cell and none fails on a clean run`` () =
        let report = run determinismOptions
        let declared = declaredCells (determinismMatrix report)

        Assert.Equal(3 * 5, declared.Length)

        Assert.All(
            declared,
            fun cell ->
                match cell.Status with
                | Fail diagnostic ->
                    failwith $"unexpected determinism failure at {cell.Coordinates}: {diagnostic.Message}"
                | _ -> ()
        )

    [<Fact>]
    let ``produced outputs reproduce byte-identically under a perturbed host`` () =
        let report = run determinismOptions
        let matrix = determinismMatrix report

        let perturbedCells =
            matrix.Cells
            |> List.filter (fun cell ->
                cell.Coordinates
                |> List.exists (fun (dimension, value) ->
                    dimension = "environment"
                    && value = environmentClassValue PerturbedHostEnvironment))

        Assert.NotEmpty perturbedCells

        // Each produced output is byte-identical under the perturbed host: Pass.
        // (An output the fixture cannot produce is SkippedWithReason, never Fail.)
        Assert.All(
            perturbedCells,
            fun cell ->
                match cell.Status with
                | Pass
                | SkippedWithReason _ -> ()
                | other -> failwith $"unexpected {other} at {cell.Coordinates}"
        )
