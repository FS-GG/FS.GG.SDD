namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module AnalysisViewTests =
    let validAnalysisJson =
        """{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "010-analyze-command",
  "stage": "analyze",
  "status": "implementationReady",
  "generator": "fsgg-sdd/1.0.0",
  "sources": [
    {
      "path": "work/010-analyze-command/tasks.yml",
      "kind": "tasks",
      "digest": { "algorithm": "sha256", "value": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
      "schemaVersion": 1,
      "schemaStatus": "current"
    }
  ],
  "sourceRelationships": [
    {
      "id": "AR001",
      "sourcePath": "work/010-analyze-command/plan.md",
      "targetPath": "work/010-analyze-command/tasks.yml",
      "sourceId": "VO-001",
      "targetId": "T004",
      "relationship": "verificationDisposition",
      "state": "current",
      "diagnosticIds": []
    }
  ],
  "readiness": {
    "status": "implementationReady",
    "readyCount": 1,
    "advisoryCount": 0,
    "warningCount": 0,
    "blockingCount": 0,
    "staleSourceCount": 0,
    "missingDispositionCount": 0,
    "malformedSourceCount": 0,
    "generatedViewFindingCount": 0,
    "acceptedDeferralCount": 0
  },
  "findings": [],
  "generatedViews": [
    {
      "path": "readiness/010-analyze-command/work-model.json",
      "kind": "workModel",
      "currency": "current",
      "diagnosticIds": []
    }
  ],
  "optionalBoundaryFacts": [],
  "diagnostics": [],
  "nextAction": {
    "actionId": "analysis.next.implement",
    "command": null,
    "reason": "Lifecycle sources are current and ready for implementation."
  }
}"""

    [<Fact>]
    let ``parseAnalysisView reads schema version 1 shape`` () =
        let snapshot =
            { Path = "readiness/010-analyze-command/analysis.json"
              Text = validAnalysisJson }

        match parseAnalysisView snapshot with
        | Ok view ->
            Assert.Equal("010-analyze-command", view.WorkId.Value)
            Assert.Equal("implementationReady", view.Readiness.Status)
            Assert.Single(view.Sources) |> ignore
            Assert.Single(view.SourceRelationships) |> ignore
            Assert.Equal("analysis.next.implement", view.NextAction.Value.ActionId)
        | Error diagnostics -> failwith $"Expected analysis view to parse, got {diagnostics}."

    [<Fact>]
    let ``parseAnalysisView reports malformed generated JSON`` () =
        let snapshot =
            { Path = "readiness/010-analyze-command/analysis.json"
              Text = "{ not-json" }

        match parseAnalysisView snapshot with
        | Ok _ -> failwith "Expected malformed analysis view to fail."
        | Error diagnostics -> Assert.Contains(diagnostics, fun diagnostic -> diagnostic.Id = "workModelInconsistent")

    // SC-005 / FR-003 / FR-004 totality assertion. The shared parseJsonView skeleton
    // now folds the previously-unreachable (Version = None, Status = Current/Deprecated)
    // match arm into the malformed-schema arm. That exact (None, Current/Deprecated)
    // pairing is unreachable through SchemaVersion.classifyRaw (Current/Deprecated always
    // carry Some version), so the equivalent observable path is a missing schemaVersion,
    // which classifies as None/Malformed and routes through the same folded arm. This pins
    // the byte-exact malformed-schema diagnostic and confirms the total match returns a
    // defined Error rather than raising MatchFailureException.
    [<Fact>]
    let ``parseAnalysisView missing schemaVersion returns malformed-schema Error and never raises`` () =
        let snapshot =
            { Path = "readiness/010-analyze-command/analysis.json"
              Text = """{ "workId": "010-analyze-command", "stage": "analyze" }""" }

        match parseAnalysisView snapshot with
        | Ok _ -> failwith "Expected missing schemaVersion to fail."
        | Error diagnostics ->
            let diagnostic = Assert.Single(diagnostics)
            Assert.Equal("malformedSchemaVersion", diagnostic.Id)
            Assert.Equal("Analysis view is missing or has malformed schemaVersion.", diagnostic.Message)
