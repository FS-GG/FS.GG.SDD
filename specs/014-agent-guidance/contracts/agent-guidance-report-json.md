# Contract: Agent Guidance Command Report JSON

Owner: `FS.GG.SDD` (commands library). Status: new in feature
`014-agent-guidance`. Report `schemaVersion: 1`.

## Surface additions

- New `AgentGuidanceSummary` record (see [data-model.md](../data-model.md)).
- New `CommandReport.AgentGuidance: AgentGuidanceSummary option` field (and the
  matching `CommandModel.AgentGuidance` field).
- Serialization in `CommandSerialization` writes the `agentGuidance` object (or
  `null`) following the established pattern used for `ship`, `verification`, etc.
- Text projection in `CommandRendering` renders the same summary facts.

## JSON shape (illustrative, not exhaustive)

```json
{
  "schemaVersion": 1,
  "reportVersion": "1",
  "command": "agents",
  "projectRoot": ".",
  "outputFormat": "json",
  "dryRun": false,
  "overwritePolicy": "allowGeneratedRefresh",
  "outcome": "succeeded",
  "workId": "014-agent-guidance",
  "changedArtifacts": [
    {
      "path": "readiness/014-agent-guidance/agent-commands/claude/guidance.json",
      "kind": "agentGuidanceTarget",
      "operation": "create",
      "safeWriteDecision": "allowGeneratedRefresh",
      "diagnosticIds": []
    }
  ],
  "agentGuidance": {
    "workId": "014-agent-guidance",
    "stage": "agents",
    "status": "generated-current",
    "generatedRoots": [
      "readiness/014-agent-guidance/agent-commands/claude",
      "readiness/014-agent-guidance/agent-commands/codex"
    ],
    "generatedTargetIds": ["claude", "codex"],
    "refusedTargetIds": [],
    "findingIds": [],
    "readyFindingCount": 2,
    "advisoryCount": 0,
    "warningCount": 0,
    "blockingCount": 0,
    "disposition": "generated-current",
    "equivalenceRequired": true,
    "divergentTargetIds": [],
    "generatedViewState": "current",
    "sourceSnapshotCount": 1,
    "readiness": "generated-current"
  },
  "generatedViews": [
    {
      "path": "readiness/014-agent-guidance/agent-commands/claude/guidance.json",
      "kind": "agent-commands",
      "currency": "current",
      "diagnosticIds": []
    }
  ],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": {
    "actionId": "agentsGenerated",
    "command": null,
    "workId": "014-agent-guidance",
    "reason": "Generated agent guidance is current; regenerate when the work model changes.",
    "requiredArtifacts": [],
    "blockingDiagnosticIds": []
  }
}
```

## Determinism rules

- Field order is fixed by the serializer; list members are sorted by stable id or
  path (target ids, generated roots, finding ids, generated views, changed
  artifacts).
- No wall-clock timestamps, durations, terminal width, ANSI styling, directory
  enumeration order, host path separators, random values, or absolute host paths
  appear in the JSON.
- Three identical executions over the `deterministic-report` fixture produce
  byte-identical report JSON.

## Text projection rules

- The text projection is a presentation of the same summary: it adds no facts
  absent from the JSON report.
- It surfaces work id, disposition, generated target ids, generated-view state,
  divergent target ids (if any), blocking/warning/advisory counts, and the next
  action.

## Blocked example (divergence)

When `requireEquivalentClaudeAndCodexBehavior` is true and an existing target
manifest's behavior model diverges from the shared derived model, `outcome` is
`blocked`, `agentGuidance.disposition` is `blocked`,
`agentGuidance.divergentTargetIds` lists the diverging target(s), at least one
blocking diagnostic names the affected target and correction, and no generated
guidance is treated as current.
