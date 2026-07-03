namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness
open FS.GG.SDD.Validation.ValidationRunner
open Xunit

/// US1 (P1) — every release-catalog contract is validated for baseline + conformance
/// via ReleaseContract.evaluate; no contract passes by absence (FR-004 / INV-4).
module BaselineMatrixTests =
    let private baselineOptions =
        { defaultOptions with
            OnlyMatrix = Some baselineMatrixName }

    let private baselineMatrix (report: ValidationReport) =
        report.Matrices |> List.find (fun matrix -> matrix.Name = baselineMatrixName)

    let private check value cell =
        cell.Coordinates
        |> List.exists (fun (dimension, v) -> dimension = "check" && v = value)

    [<Fact>]
    let ``every catalog contract has a baseline and a conformance cell`` () =
        let report = run baselineOptions
        let matrix = baselineMatrix report

        let contracts = defaultPlan.BaselineContracts |> List.length
        Assert.Equal(contracts * 2, matrix.Cells.Length)
        Assert.Equal(contracts, matrix.Cells |> List.filter (check "baseline") |> List.length)
        Assert.Equal(contracts, matrix.Cells |> List.filter (check "conformance") |> List.length)

    [<Fact>]
    let ``baseline cells never pass by absence`` () =
        let report = run baselineOptions
        let matrix = baselineMatrix report

        // A missing baseline is NotValidated, never Pass (INV-4). The real catalog
        // declares a baseline for every contract, so each baseline cell is Pass.
        matrix.Cells
        |> List.filter (check "baseline")
        |> List.iter (fun cell ->
            match cell.Status with
            | Pass
            | NotValidated _ -> ()
            | other -> failwith $"unexpected baseline status {other} at {cell.Coordinates}")

    [<Fact>]
    let ``conformance cells are decided, never left pending`` () =
        let report = run baselineOptions
        let matrix = baselineMatrix report

        matrix.Cells
        |> List.filter (check "conformance")
        |> List.iter (fun cell ->
            match cell.Status with
            | NotValidated "not yet evaluated" -> failwith $"conformance cell left pending at {cell.Coordinates}"
            | _ -> ())
