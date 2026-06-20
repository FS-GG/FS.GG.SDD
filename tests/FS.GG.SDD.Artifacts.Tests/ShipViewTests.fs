namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts.LifecycleArtifacts
open Xunit

module ShipViewTests =
    let validShipJson =
        """{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "013-ship-command",
  "stage": "ship",
  "status": "shipReady",
  "generator": "fsgg-sdd/1.0.0",
  "sources": [
    {
      "path": "readiness/013-ship-command/verify.json",
      "kind": "verification",
      "digest": { "algorithm": "sha256", "value": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
      "schemaVersion": 1,
      "schemaStatus": "current"
    }
  ],
  "lifecycleReadiness": {
    "status": "shipReady",
    "stages": [
      { "stage": "analyze", "status": "ready" },
      { "stage": "verify", "status": "ready" }
    ]
  },
  "verificationReadiness": {
    "status": "verificationReady",
    "blockingFindingIds": [],
    "evidenceSupportedCount": 0,
    "evidenceDeferredCount": 0,
    "evidenceMissingCount": 0,
    "evidenceStaleCount": 0,
    "evidenceSyntheticCount": 0,
    "evidenceInvalidCount": 0
  },
  "evidenceDispositions": [],
  "generatedViews": [
    { "path": "readiness/013-ship-command/work-model.json", "kind": "workModel", "currency": "current", "diagnosticIds": [] }
  ],
  "disposition": {
    "state": "shipReady",
    "blockingFindingIds": [],
    "warningFindingIds": [],
    "advisoryFindingIds": [],
    "contributingStages": [],
    "correction": ""
  },
  "findings": [],
  "governanceCompatibility": [],
  "diagnostics": [],
  "readiness": "shipReady"
}"""

    [<Fact>]
    let ``parseShipView reads schema version one ship identity and stage`` () =
        match parseShipView { Path = "readiness/013-ship-command/ship.json"; Text = validShipJson } with
        | Ok view ->
            Assert.Equal(1, view.SchemaVersion.Major)
            Assert.Equal("013-ship-command", view.WorkId.Value)
            Assert.Equal("ship", FS.GG.SDD.Artifacts.Identifiers.stageValue view.Stage)
            Assert.Equal("shipReady", view.Readiness)
            Assert.Equal("shipReady", view.Status)
        | Error diagnostics -> failwith $"Expected a valid ship view, got {diagnostics}."

    [<Fact>]
    let ``parseShipView reads disposition lifecycle and verification readiness`` () =
        match parseShipView { Path = "readiness/013-ship-command/ship.json"; Text = validShipJson } with
        | Ok view ->
            Assert.Equal("shipReady", view.Disposition)
            Assert.Equal("verificationReady", view.VerificationReadiness.Status)
            Assert.Contains(view.LifecycleReadiness, fun stage -> stage.Stage = "verify" && stage.Status = "ready")
            Assert.Contains(view.GeneratedViews, fun generated -> generated.Path = "readiness/013-ship-command/work-model.json")
        | Error diagnostics -> failwith $"Expected a valid ship view, got {diagnostics}."

    [<Fact>]
    let ``parseShipView rejects malformed ship json`` () =
        match parseShipView { Path = "readiness/013-ship-command/ship.json"; Text = "{ not valid ship json" } with
        | Ok _ -> failwith "Expected malformed ship view to fail parsing."
        | Error diagnostics -> Assert.NotEmpty diagnostics

    [<Fact>]
    let ``parseShipView rejects unsupported future schema version`` () =
        let futureJson = validShipJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 999")

        match parseShipView { Path = "readiness/013-ship-command/ship.json"; Text = futureJson } with
        | Ok _ -> failwith "Expected future schema version to fail parsing."
        | Error diagnostics -> Assert.NotEmpty diagnostics
