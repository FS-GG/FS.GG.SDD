namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness
open FS.GG.SDD.Validation.ValidationRunner
open Xunit

/// US1 (P1) / SC-004 — two runs over an identical tree serialize byte-identically
/// once `sensed` is excluded, and the report carries no clock / duration / host path
/// / ANSI outside the fenced `sensed` block (FR-007 / INV-2 / INV-5).
module ReportDeterminismTests =
    let private tinyOptions =
        { defaultOptions with
            OnlyMatrix = Some lifecycleMatrixName
            Plan =
                Some
                    { defaultPlan with
                        LifecycleCommands = [ Init ]
                        Projections = [ Json ]
                        States = [ "fresh" ] } }

    [<Fact>]
    let ``two runs serialize byte-identically`` () =
        let first = run tinyOptions |> serialize
        let second = run tinyOptions |> serialize
        Assert.Equal(first, second)

    [<Fact>]
    let ``the serialized report fences sensed metadata to null and carries no ANSI`` () =
        let json = run tinyOptions |> serialize

        Assert.Contains("\"sensed\"", json)
        Assert.Contains("\"startedAtUtc\": null", json)
        Assert.Contains("\"durationMs\": null", json)
        Assert.Contains("\"host\": null", json)

        let control =
            json
            |> Seq.filter (fun c -> int c < 32 && c <> '\n' && c <> '\r' && c <> '\t')
            |> Seq.map int
            |> Seq.distinct
            |> Seq.toList

        Assert.True(List.isEmpty control, $"unexpected control/ANSI codes in report: {control}")

    [<Fact>]
    let ``a parsed report round-trips to the same bytes`` () =
        let json = run tinyOptions |> serialize

        match parse json with
        | Ok report -> Assert.Equal(json, serialize report)
        | Error message -> failwith message
