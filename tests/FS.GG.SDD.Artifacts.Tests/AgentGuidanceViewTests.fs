namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.WorkModel
open Xunit

module AgentGuidanceViewTests =
    let workModelJson =
        """{
  "schemaVersion": 1,
  "modelVersion": "1.0.0",
  "workId": "014-agent-guidance",
  "project": { "id": "demo", "defaultWorkRoot": "work" },
  "sources": [],
  "workItem": { "id": "014-agent-guidance", "title": "Agent Guidance", "stage": "tasks", "changeTier": "tier1", "status": "draft" },
  "requirements": [ { "id": "FR-001", "title": "First", "text": "x", "acceptanceCriteria": [], "priority": null, "source": "work/014-agent-guidance/spec.md", "linkedTaskIds": ["T002", "T001"], "linkedEvidenceIds": [] } ],
  "decisions": [ { "id": "DEC-001", "title": "D", "decision": "d", "source": "work/014-agent-guidance/plan.md", "linkedTaskIds": ["T001"] } ],
  "tasks": [
    { "id": "T002", "title": "Second task", "status": "pending", "owner": "codex", "dependencies": [], "requirements": ["FR-001"], "decisions": [], "sourceIds": [], "requiredSkills": ["fs-gg-sdd-project"], "requiredEvidence": [], "source": "work/014-agent-guidance/tasks.yml" },
    { "id": "T001", "title": "First task", "status": "pending", "owner": "codex", "dependencies": [], "requirements": ["FR-001"], "decisions": ["DEC-001"], "sourceIds": [], "requiredSkills": ["fs-gg-sdd-project"], "requiredEvidence": [], "source": "work/014-agent-guidance/tasks.yml" }
  ],
  "evidence": [],
  "generatedViews": [],
  "diagnostics": [],
  "governanceBoundaries": []
}"""

    let parsedModel () =
        match
            parseWorkModel
                { Path = "readiness/014-agent-guidance/work-model.json"
                  Text = workModelJson }
        with
        | Ok model -> model
        | Error diagnostics -> failwith $"Expected a parseable work model, got {diagnostics}"

    [<Fact>]
    let ``parseWorkModel reads the guidance-relevant facts`` () =
        let model = parsedModel ()
        Assert.Equal("014-agent-guidance", model.WorkId)
        Assert.Equal("tasks", model.WorkItem.Stage)
        Assert.Equal(2, model.Tasks.Length)

    [<Fact>]
    let ``parseWorkModel rejects malformed JSON`` () =
        match
            parseWorkModel
                { Path = "readiness/x/work-model.json"
                  Text = "{ not json" }
        with
        | Ok _ -> failwith "Expected an error for malformed work-model JSON."
        | Error diagnostics -> Assert.NotEmpty diagnostics

    [<Fact>]
    let ``deriveGuidanceModel sorts commands and skills by stable id`` () =
        let guidance = deriveGuidanceModel (parsedModel ())
        Assert.Equal<string list>([ "T001"; "T002" ], guidance.Commands |> List.map (fun command -> command.Id))
        Assert.Equal<string list>([ "fs-gg-sdd-project" ], guidance.Skills |> List.map (fun skill -> skill.Id))
        Assert.Equal("014-agent-guidance", guidance.WorkId)

    [<Fact>]
    let ``deriveGuidanceModel is identical for the same model (target equivalence by construction)`` () =
        let model = parsedModel ()
        let first = deriveGuidanceModel model
        let second = deriveGuidanceModel model
        Assert.Equal((behaviorModelDigest first).Value, (behaviorModelDigest second).Value)

    [<Fact>]
    let ``behaviorModelDigest changes when the model changes`` () =
        let baseDigest = behaviorModelDigest (deriveGuidanceModel (parsedModel ()))

        let mutatedJson =
            workModelJson.Replace("\"title\": \"First task\"", "\"title\": \"Renamed task\"")

        let mutated =
            match
                parseWorkModel
                    { Path = "readiness/x/work-model.json"
                      Text = mutatedJson }
            with
            | Ok model -> model
            | Error diagnostics -> failwith $"{diagnostics}"

        Assert.True(baseDigest.Value <> (behaviorModelDigest (deriveGuidanceModel mutated)).Value)

    // --- Manifest parsing ---

    let manifestJson =
        """{
  "schemaVersion": 1,
  "viewVersion": "1.0",
  "workId": "014-agent-guidance",
  "targetId": "claude",
  "generator": "fsgg-sdd/0.2.0",
  "generated": true,
  "sources": [ { "path": "readiness/014-agent-guidance/work-model.json", "kind": "workModel", "digest": { "algorithm": "sha256", "value": "1111111111111111111111111111111111111111111111111111111111111111" }, "schemaVersion": 1, "schemaStatus": "current" } ],
  "behaviorModelDigest": { "algorithm": "sha256", "value": "2222222222222222222222222222222222222222222222222222222222222222" },
  "commands": [ { "id": "T001", "title": "First", "stage": "tasks", "purpose": "p", "relatedIds": ["FR-001"] } ],
  "skills": [ { "id": "fs-gg-sdd-project", "title": "fs-gg-sdd-project", "capability": "c", "relatedIds": ["T001"] } ],
  "renderedFiles": [ { "path": "readiness/014-agent-guidance/agent-commands/claude/commands.md", "kind": "commands" } ],
  "diagnostics": []
}"""

    [<Fact>]
    let ``parseGeneratedAgentGuidance reads a well-formed manifest`` () =
        match
            parseGeneratedAgentGuidance
                { Path = "readiness/014-agent-guidance/agent-commands/claude/guidance.json"
                  Text = manifestJson }
        with
        | Ok manifest ->
            Assert.Equal("014-agent-guidance", manifest.WorkId.Value)
            Assert.Equal("claude", manifest.TargetId)
            Assert.True manifest.Generated

            Assert.Equal(
                "2222222222222222222222222222222222222222222222222222222222222222",
                manifest.BehaviorModelDigest.Value
            )

            Assert.Single manifest.Commands |> ignore
            Assert.Single manifest.Skills |> ignore
        | Error diagnostics -> failwith $"Expected a well-formed manifest, got {diagnostics}"

    [<Fact>]
    let ``parseGeneratedAgentGuidance rejects malformed schema version`` () =
        let bad = manifestJson.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": \"x\",")

        match parseGeneratedAgentGuidance { Path = "p"; Text = bad } with
        | Ok _ -> failwith "Expected a malformed-schema error."
        | Error diagnostics -> Assert.NotEmpty diagnostics

    [<Fact>]
    let ``parseGeneratedAgentGuidance rejects malformed body`` () =
        match parseGeneratedAgentGuidance { Path = "p"; Text = "{ not json" } with
        | Ok _ -> failwith "Expected a malformed-body error."
        | Error diagnostics -> Assert.NotEmpty diagnostics
