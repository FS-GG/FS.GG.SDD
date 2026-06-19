# Contract: Command Report JSON

## Scope

Command report JSON is the authoritative result for every native SDD lifecycle
command. It is emitted to stdout by default in `--json` mode or may be written
to a requested report path in a later feature. Plain text output is a projection
from the same report and is not an automation contract.

## Top-Level Shape

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0.0",
  "command": {
    "name": "specify",
    "stage": "specify"
  },
  "context": {
    "projectRoot": ".",
    "workId": "003-native-sdd-lifecycle-commands"
  },
  "invocation": {
    "outputFormat": "json",
    "dryRun": false,
    "overwritePolicy": "refuseUnsafe"
  },
  "outcome": "succeeded",
  "changedArtifacts": [],
  "generatedViews": [],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": {}
}
```

Required ordering:

1. Object properties emit in the order documented by this contract.
2. Artifact changes sort by path, operation, and ownership.
3. Generated views sort by path.
4. Diagnostics sort by severity rank, diagnostic id, artifact path, source
   location, then message.
5. Governance compatibility facts sort by path.
6. Next-action required artifacts sort by path.

The report must not contain implicit timestamps, durations, terminal width,
ANSI styling, process ids, random ids, absolute host paths, or directory
enumeration order.

## Command Entry

```json
{
  "name": "tasks",
  "stage": "tasks"
}
```

Validation:

- `name` is one of `init`, `charter`, `specify`, `clarify`, `checklist`,
  `plan`, `tasks`, `analyze`.
- `stage` is the lifecycle stage affected by the command. For `init`, the
  stage value is `project`.

## Context Entry

```json
{
  "projectRoot": ".",
  "workId": "003-native-sdd-lifecycle-commands"
}
```

Validation:

- `projectRoot` is a deterministic root token. It is `.` for the selected
  project root, not an absolute machine path.
- `workId` is omitted for project-only `init` reports unless `init` also
  creates a selected work item in a later feature.

## Invocation Entry

```json
{
  "outputFormat": "json",
  "dryRun": true,
  "overwritePolicy": "refuseUnsafe",
  "inputDigest": {
    "algorithm": "sha256",
    "value": "..."
  }
}
```

Validation:

- Only normalized options that affect lifecycle behavior are recorded.
- User-supplied input text is represented by digest when the full text would
  duplicate authored artifacts.
- Non-deterministic shell details are excluded.

## Changed Artifact Entry

```json
{
  "path": "work/003-native-sdd-lifecycle-commands/spec.md",
  "kind": "spec",
  "ownership": "authored",
  "operation": "update",
  "beforeDigest": {
    "algorithm": "sha256",
    "value": "..."
  },
  "afterDigest": {
    "algorithm": "sha256",
    "value": "..."
  },
  "safeWriteDecision": "safe",
  "diagnosticIds": []
}
```

Allowed operations:

- `create`
- `update`
- `preserve`
- `refuse`
- `noChange`

Validation:

- Authored artifacts include `beforeDigest` when a file already existed.
- Generated artifacts include `afterDigest` and generated-view metadata.
- Refused writes include diagnostic ids that explain the conflict.

## Generated View Entry

```json
{
  "path": "readiness/003-native-sdd-lifecycle-commands/work-model.json",
  "kind": "workModel",
  "schemaVersion": 1,
  "generator": {
    "id": "FS.GG.SDD.Commands",
    "version": "0.1.0"
  },
  "sources": [
    {
      "path": "work/003-native-sdd-lifecycle-commands/spec.md",
      "digest": {
        "algorithm": "sha256",
        "value": "..."
      },
      "schemaVersion": 1,
      "schemaStatus": "current"
    }
  ],
  "outputDigest": {
    "algorithm": "sha256",
    "value": "..."
  },
  "currency": "current",
  "diagnosticIds": []
}
```

Allowed kinds in this feature:

- `workModel`
- `analysis`

Allowed currency values:

- `current`
- `missing`
- `stale`
- `malformed`
- `blocked`

Validation:

- `workModel` entries reuse the existing generation manifest contract from
  `FS.GG.SDD.Artifacts`.
- `analysis` entries are produced only by `analyze`.
- Blocked refreshes include diagnostics that name the source artifact that must
  be corrected.

## Diagnostic Entry

```json
{
  "id": "unsafeOverwrite",
  "severity": "error",
  "artifact": "work/003-native-sdd-lifecycle-commands/spec.md",
  "location": null,
  "message": "The command would overwrite user-authored content.",
  "correction": "Review the existing file and rerun with an explicit safe update path.",
  "relatedIds": ["003-native-sdd-lifecycle-commands"]
}
```

Validation:

- Shape matches the existing `Diagnostics.Diagnostic` contract.
- `severity` is one of `error`, `warning`, `info`.
- Command-specific diagnostics must use stable ids and deterministic messages.

## Governance Compatibility Entry

```json
{
  "path": ".fsgg/policy.yml",
  "relationship": "optionalGovernancePolicy",
  "requiredBySdd": false,
  "state": "absent",
  "diagnosticIds": []
}
```

Validation:

- Entries describe optional boundaries only.
- SDD does not emit route, profile, freshness, gate, audit, or enforcement
  verdicts.

## Next Action Entry

```json
{
  "actionId": "nextLifecycleCommand",
  "command": "clarify",
  "workId": "003-native-sdd-lifecycle-commands",
  "reason": "Specification authoring completed.",
  "requiredArtifacts": [
    "work/003-native-sdd-lifecycle-commands/spec.md"
  ],
  "blockingDiagnosticIds": []
}
```

Validation:

- Successful reports identify the next expected lifecycle command.
- Blocked reports identify the correction target instead of guessing a next
  lifecycle stage.
- No next action may require Governance runtime behavior in this feature.

## Outcomes

- `succeeded`: requested lifecycle action completed with no diagnostics above
  info severity.
- `succeededWithWarnings`: action completed with non-blocking diagnostics.
- `blocked`: no unsafe writes were performed and the user must correct input.
- `noChange`: inputs were already current and no write was needed.

## Determinism Rules

- JSON uses UTF-8 without a byte order mark.
- Paths use `/`.
- Object property order follows this contract.
- Lists use the documented sort keys.
- Digests are lowercase SHA-256 hex over normalized bytes.
- Text projection tests must prove that text mode is derived from this report
  without adding separate facts.
