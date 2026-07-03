namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module VerificationViewTests =
    let validVerifyJson =
        """{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "012-verify-command",
  "stage": "verify",
  "status": "verificationReady",
  "generator": "fsgg-sdd/1.0.0",
  "sources": [
    {
      "path": "work/012-verify-command/tasks.yml",
      "kind": "tasks",
      "digest": { "algorithm": "sha256", "value": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
      "schemaVersion": 1,
      "schemaStatus": "current"
    }
  ],
  "lifecycleReadiness": {
    "status": "implementationReady",
    "stages": [
      { "stage": "analyze", "status": "implementationReady" },
      { "stage": "evidence", "status": "evidenceReady" }
    ]
  },
  "taskGraph": {
    "taskCount": 6,
    "dependencyCount": 0,
    "dependenciesValid": true,
    "statusesValid": true,
    "findingIds": []
  },
  "evidenceDispositions": [
    {
      "id": "ED-EV001",
      "obligationId": "EV001",
      "state": "supported",
      "evidenceIds": ["EV001"],
      "affectedTaskIds": ["T001"],
      "affectedSourceIds": [],
      "severity": "ready",
      "diagnosticIds": [],
      "correction": ""
    }
  ],
  "testDispositions": [
    {
      "id": "TD-EV001",
      "obligationId": "EV001",
      "state": "satisfied",
      "evidenceIds": ["EV001"],
      "affectedTaskIds": ["T001"],
      "affectedRequirementIds": ["FR-001"],
      "severity": "ready",
      "diagnosticIds": [],
      "correction": ""
    }
  ],
  "skillVisibility": [
    {
      "skill": "automated-tests",
      "requiringTaskIds": ["T004"],
      "visibility": "visible",
      "sourceArtifactPath": "work/012-verify-command/tasks.yml",
      "severity": "ready",
      "diagnosticIds": [],
      "correction": ""
    }
  ],
  "generatedViews": [
    {
      "path": "readiness/012-verify-command/work-model.json",
      "kind": "workModel",
      "currency": "current",
      "diagnosticIds": []
    }
  ],
  "findings": [],
  "governanceCompatibility": [],
  "diagnostics": [],
  "readiness": "verificationReady",
  "nextAction": {
    "actionId": "verify.next.ship",
    "command": null,
    "reason": "Verification readiness is current and ready for ship."
  }
}"""

    [<Fact>]
    let ``parseVerificationView reads schema version 1 shape`` () =
        let snapshot =
            { Path = "readiness/012-verify-command/verify.json"
              Text = validVerifyJson }

        match parseVerificationView snapshot with
        | Ok view ->
            Assert.Equal("012-verify-command", view.WorkId.Value)
            Assert.Equal("verify", FS.GG.SDD.Artifacts.Identifiers.stageValue view.Stage)
            Assert.Equal("verificationReady", view.Readiness)
            Assert.Single(view.Sources) |> ignore
            Assert.Single(view.EvidenceDispositions) |> ignore
            Assert.Single(view.TestDispositions) |> ignore
            Assert.Single(view.SkillVisibility) |> ignore
            Assert.Equal(6, view.TaskGraph.TaskCount)
            Assert.True(view.TaskGraph.DependenciesValid)
        | Error diagnostics -> failwith $"Expected verification view to parse, got {diagnostics}."

    [<Fact>]
    let ``parseVerificationView recovers evidence and test disposition states`` () =
        let snapshot =
            { Path = "readiness/012-verify-command/verify.json"
              Text = validVerifyJson }

        match parseVerificationView snapshot with
        | Ok view ->
            let evidence = List.head view.EvidenceDispositions
            let test = List.head view.TestDispositions
            let skill = List.head view.SkillVisibility
            Assert.Equal(EvidenceSupported, evidence.State)
            Assert.Equal(TestSatisfied, test.State)
            Assert.Equal(SkillVisible, skill.Visibility)
        | Error diagnostics -> failwith $"Expected verification view to parse, got {diagnostics}."

    [<Fact>]
    let ``parseVerificationView reports malformed generated JSON`` () =
        let snapshot =
            { Path = "readiness/012-verify-command/verify.json"
              Text = "{ not-json" }

        match parseVerificationView snapshot with
        | Ok _ -> failwith "Expected malformed verification view to fail."
        | Error diagnostics -> Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "workModelInconsistent")
