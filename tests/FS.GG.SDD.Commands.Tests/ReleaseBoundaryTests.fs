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

    /// Feature 092 (ADR-0026) named an SDD-owned artifact `ship-verdict.json`, whose view kind
    /// serializes as `shipVerdict`. Its "verdict" is the *readiness disposition* SDD reports at
    /// the merge boundary — not a Governance gate verdict; SDD still computes none. Elide those
    /// two SDD-owned names before the scan so the guard keeps asserting what it means (gate-LOGIC
    /// vocabulary) rather than false-positiving on a legitimate name, exactly as the comment
    /// below has always required of `governance*`. Any *other* occurrence of "verdict" still
    /// fails, so the guard loses no power.
    let private sddOwnedNames = [ "ship-verdict.json"; "shipverdict" ]

    let private elideSddOwnedNames (json: string) =
        sddOwnedNames
        |> List.fold (fun (acc: string) name -> acc.Replace(name, "")) json

    [<Fact>]
    let ``T024 the release contract carries no Governance gate-logic vocabulary`` () =
        let json = (serialize (currentRelease ())).ToLowerInvariant() |> elideSddOwnedNames

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
    let ``T024 the gate-logic vocabulary guard still catches a real verdict token`` () =
        // Guard the guard: eliding the SDD-owned names must not blind the scan. A Governance
        // verdict token anywhere else in the contract still trips it.
        let poisoned =
            (serialize (currentRelease ())).ToLowerInvariant()
            + "\"governanceverdict\": \"pass\""
            |> elideSddOwnedNames

        Assert.Contains("verdict", poisoned)

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

    /// Originally feature 018's FR-013 "no scope creep" guard: *018* added no view kind. Feature
    /// 092 (ADR-0026) deliberately adds exactly one — `ShipVerdict`, the durable-generated
    /// merge-boundary projection — so the known set names it and its introducing feature.
    /// `ReleaseReadinessCheckTests`' T019 ("the catalog covers every enumerable kind") is the
    /// other half: adding a kind without cataloguing it, or cataloguing it without admitting the
    /// kind here, both stay build failures.
    [<Fact>]
    let ``T024 the catalog adds no GeneratedViewKind beyond the pre-018 set plus 092's ShipVerdict`` () =
        let known =
            Set.ofList
                [ WorkModel
                  Analysis
                  GeneratedViewKind.Verify
                  GeneratedViewKind.Ship
                  GeneratedViewKind.ShipVerdict // feature 092 / ADR-0026
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
