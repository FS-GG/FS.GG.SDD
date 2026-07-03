namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Validation.ValidationContracts
open FS.GG.SDD.Validation.ValidationHarness
open FS.GG.SDD.Validation.ValidationRunner
open Xunit

/// US3 (P3) — the harness runs with no Governance runtime, computes no Governance
/// verdict, and is reachable only via the CLI: no lifecycle command path depends on
/// it (FR-008 / FR-010 / INV-8 / SC-006 / SC-007).
module IsolationTests =
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
    let ``run completes and emits a report with no Governance runtime present`` () =
        // No Governance package is referenced by FS.GG.SDD.Validation; a clean run is
        // proof the sweep needs none (SC-006).
        let report = run tinyOptions
        Assert.Equal(1, report.SchemaVersion)
        Assert.NotEmpty report.Matrices

    [<Fact>]
    let ``the report encodes no Governance route/profile/freshness/gate/verdict`` () =
        let json = run tinyOptions |> serialize

        for forbidden in
            [ "\"route\""
              "\"profile\""
              "\"freshness\""
              "\"gate\""
              "\"verdict\""
              "\"effective\"" ] do
            Assert.False(json.Contains forbidden, $"report leaked a Governance fact: {forbidden}")

    [<Fact>]
    let ``the Commands assembly does not depend on the Validation assembly`` () =
        // One-directional dependency (matrix-runner §isolation): the lifecycle surface
        // cannot drift to satisfy the harness, and no command path can reach it.
        let commandsAssembly = typeof<SddCommand>.Assembly

        let references =
            commandsAssembly.GetReferencedAssemblies()
            |> Array.map (fun reference -> reference.Name |> Option.ofObj |> Option.defaultValue "")

        Assert.DoesNotContain("FS.GG.SDD.Validation", references)
