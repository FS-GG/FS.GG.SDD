namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts.GenerationManifest
open FS.GG.SDD.Artifacts.ReleaseContract
open FS.GG.SDD.Commands.CommandTypes
open Xunit

/// Boundary & scope exclusion for the release-readiness feature (FR-013/FR-014,
/// SC-008): the contract is entirely SDD-owned and adds no lifecycle stage.
module ReleaseBoundaryTests =
    let workId = "018-release-readiness"
    let title = "Release Readiness"

    // ===== FR-014 / SC-008 — Governance boundary =====

    [<Fact>]
    let ``T024 the release contract carries no Governance gate-logic vocabulary`` () =
        let json = (serialize (currentRelease ())).ToLowerInvariant()

        // Assert against gate-LOGIC vocabulary, not the word "Governance": the
        // optional contractVersion range and the governance* field names are
        // legitimate declared-compat facts (017) and must NOT false-positive.
        for forbidden in
            [ "gate"
              "route"
              "profile"
              "freshness"
              "publish"
              "provenance"
              "verdict"
              "enforce" ] do
            Assert.DoesNotContain(forbidden, json)

    [<Fact>]
    let ``T024 a clean produced governance handoff selects no route, profile, gate, or verdict`` () =
        let root = TestSupport.tempDirectory ()
        TestSupport.initializeVerifiedProject root workId title
        TestSupport.runShip root workId title |> ignore

        let handoff =
            TestSupport.readRelative root $"readiness/{workId}/governance-handoff.json"

        for forbidden in
            [ "\"route\""
              "\"profile\""
              "\"gate\""
              "verdict"
              "enforcement"
              "provenance"
              "publishPlan" ] do
            Assert.DoesNotContain(forbidden, handoff)

    // ===== FR-013 — no scope creep (no new stage, no new view kind) =====

    [<Fact>]
    let ``T024 the catalog adds no GeneratedViewKind beyond the pre-018 enumerable set`` () =
        let known =
            Set.ofList
                [ WorkModel
                  Analysis
                  GeneratedViewKind.Verify
                  GeneratedViewKind.Ship
                  Summary
                  AgentCommands
                  GovernanceHandoff ]

        for entry in (currentRelease ()).Catalog do
            match entry.Kind with
            | GeneratedViewContract(kind, _) -> Assert.Contains(kind, known)
            | CommandOutputContract -> ()

    [<Fact>]
    let ``T024 the feature introduces no new lifecycle command`` () =
        // no `release`/`release-readiness` lifecycle command exists
        Assert.True(
            (match parseCommand "release" with
             | Error _ -> true
             | Ok _ -> false)
        )

        Assert.True(
            (match parseCommand "release-readiness" with
             | Error _ -> true
             | Ok _ -> false)
        )

        // the cross-cutting generators remain non-stages (unchanged)
        Assert.Equal(None, nextLifecycleCommand Agents)
        Assert.Equal(None, nextLifecycleCommand Refresh)
