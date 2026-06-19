# Contract: Charter Command Report JSON

## Scope

Charter reports follow the existing native command report JSON contract and add
charter-specific expectations for changed artifacts, generated views,
diagnostics, and next action. JSON remains the authoritative result; text
output is a projection from the same report object.

## Successful Create Example

```json
{
  "schemaVersion": 1,
  "reportVersion": "1.0.0",
  "command": {
    "name": "charter",
    "stage": "charter"
  },
  "context": {
    "projectRoot": ".",
    "workId": "004-charter-command"
  },
  "invocation": {
    "outputFormat": "json",
    "dryRun": false,
    "overwritePolicy": "refuseUnsafe"
  },
  "outcome": "succeeded",
  "changedArtifacts": [
    {
      "path": "work/004-charter-command/charter.md",
      "kind": "authoredSource",
      "ownership": "authored",
      "operation": "create",
      "beforeDigest": null,
      "afterDigest": {
        "algorithm": "sha256",
        "value": "<lowercase-sha256>"
      },
      "safeWriteDecision": "safe",
      "diagnosticIds": []
    }
  ],
  "generatedViews": [
    {
      "path": "readiness/004-charter-command/work-model.json",
      "kind": "workModel",
      "schemaVersion": null,
      "generator": {
        "id": "FS.GG.SDD.Commands",
        "version": "0.1.0"
      },
      "sources": [
        {
          "path": "work/004-charter-command/charter.md",
          "digest": {
            "algorithm": "sha256",
            "value": "<lowercase-sha256>"
          },
          "schemaVersion": 1,
          "schemaStatus": "current"
        }
      ],
      "outputDigest": null,
      "currency": "missing",
      "diagnosticIds": []
    }
  ],
  "diagnostics": [],
  "governanceCompatibility": [],
  "nextAction": {
    "actionId": "nextLifecycleCommand",
    "command": "specify",
    "workId": "004-charter-command",
    "reason": "Command 'charter' completed.",
    "requiredArtifacts": [
      "work/004-charter-command/charter.md"
    ],
    "blockingDiagnosticIds": []
  }
}
```

The example uses placeholders only where digest values are implementation
outputs.

## Required Ordering

The existing ordering contract still applies:

1. Object properties emit in documented order.
2. Artifact changes sort by path, operation, and ownership.
3. Generated views sort by path.
4. Diagnostics sort by severity rank, diagnostic id, artifact path, source
   location, then message.
5. Governance compatibility facts sort by path.
6. Next-action required artifacts sort by path.

## Charter-Specific Changed Artifacts

Required charter artifact entry fields:

- `path`: `work/<id>/charter.md`
- `kind`: `authoredSource` or the existing command write-kind value for
  authored sources
- `ownership`: `authored`
- `operation`: one of `create`, `update`, `preserve`, `refuse`, `noChange`
- `safeWriteDecision`: `safe`, `preserveExisting`, `refuseConflict`,
  `dryRunOnly`, or equivalent existing value with the same meaning

Validation:

- Refused charter writes include diagnostic ids.
- Successful writes include `afterDigest`.
- Existing files include `beforeDigest`.
- Dry-run reports describe proposed changes but do not mutate disk.

## Charter-Specific Diagnostics

Required stable diagnostic coverage:

- `outsideProject` or an equivalent missing-project diagnostic
- `missingWorkId`
- `malformedWorkId`
- `duplicateWorkId`
- `charterIdentityMismatch`
- `malformedCharterFrontMatter`
- `unsafeOverwrite`
- `staleGeneratedView`
- `malformedGeneratedView`
- `blockedGeneratedViewRefresh`
- optional Governance boundary issue diagnostics when applicable

Diagnostic entries must include artifact, severity, message, correction, and
related ids when available.

## Next Action

Successful charter reports set:

```json
{
  "actionId": "nextLifecycleCommand",
  "command": "specify",
  "workId": "<selected-work-id>",
  "reason": "Command 'charter' completed.",
  "requiredArtifacts": [
    "work/<selected-work-id>/charter.md"
  ],
  "blockingDiagnosticIds": []
}
```

Blocked reports set `actionId` to `correctBlockingDiagnostics`, omit a next
lifecycle command, and include the blocking diagnostic ids.

## Determinism Rules

- JSON uses UTF-8 without a byte order mark.
- Paths use `/`.
- Object property order follows the report contract.
- Lists use documented sort keys.
- Digests are lowercase SHA-256 hex over normalized bytes.
- Reports do not contain timestamps, durations, terminal width, ANSI styling,
  process ids, random ids, absolute host paths, or directory enumeration order.
- Text projection tests must prove text mode derives from this report without
  adding separate facts.
