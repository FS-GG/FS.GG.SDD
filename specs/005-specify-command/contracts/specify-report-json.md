# Contract: Specify Command Report JSON

## Scope

Specify reports follow the existing native command report JSON contract and add
specification-specific expectations for changed artifacts, parsed facts,
generated views, diagnostics, and next action. JSON remains the authoritative
result; text output is a projection from the same report object.

## Successful Create Example

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
    "workId": "005-specify-command"
  },
  "invocation": {
    "outputFormat": "json",
    "dryRun": false,
    "overwritePolicy": "refuseUnsafe"
  },
  "outcome": "succeeded",
  "changedArtifacts": [
    {
      "path": "work/005-specify-command/spec.md",
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
  "specification": {
    "workId": "005-specify-command",
    "stage": "specify",
    "status": "specified",
    "storyIds": [
      "US-001"
    ],
    "requirementIds": [
      "FR-001"
    ],
    "acceptanceScenarioIds": [
      "AC-001"
    ],
    "ambiguityIds": [],
    "unresolvedAmbiguityCount": 0
  },
  "generatedViews": [
    {
      "path": "readiness/005-specify-command/work-model.json",
      "kind": "workModel",
      "schemaVersion": 1,
      "generator": {
        "id": "FS.GG.SDD.Commands",
        "version": "0.1.0"
      },
      "sources": [
        {
          "path": "work/005-specify-command/charter.md",
          "digest": {
            "algorithm": "sha256",
            "value": "<lowercase-sha256>"
          },
          "schemaVersion": 1,
          "schemaStatus": "current"
        },
        {
          "path": "work/005-specify-command/spec.md",
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
    "command": "clarify",
    "workId": "005-specify-command",
    "reason": "Command 'specify' completed.",
    "requiredArtifacts": [
      "work/005-specify-command/charter.md",
      "work/005-specify-command/spec.md"
    ],
    "blockingDiagnosticIds": []
  }
}
```

The example uses placeholders only where digest values are implementation
outputs. If the existing command report type cannot include a nested
`specification` object without a broader public API change, the same facts must
be represented through documented report fields, generated-view sources, and
diagnostics before the feature is accepted.

## Required Ordering

The existing ordering contract still applies:

1. Object properties emit in documented order.
2. Artifact changes sort by path, operation, and ownership.
3. Specification ids sort by id family and numeric suffix.
4. Generated views sort by path.
5. Diagnostics sort by severity rank, diagnostic id, artifact path, source
   location, then message.
6. Governance compatibility facts sort by path.
7. Next-action required artifacts sort by path.

## Specify-Specific Changed Artifacts

Required specification artifact entry fields:

- `path`: `work/<id>/spec.md`
- `kind`: `authoredSource` or the existing command write-kind value for
  authored sources
- `ownership`: `authored`
- `operation`: one of `create`, `update`, `preserve`, `refuse`, `noChange`
- `safeWriteDecision`: `safe`, `preserveExisting`, `refuseConflict`,
  `dryRunOnly`, or equivalent existing value with the same meaning

Validation:

- Refused specification writes include diagnostic ids.
- Successful writes include `afterDigest`.
- Existing files include `beforeDigest`.
- Dry-run reports describe proposed changes but do not mutate disk.

## Specify-Specific Diagnostics

Required stable diagnostic coverage:

- `outsideProject` or an equivalent missing-project diagnostic
- `missingWorkId`
- `malformedWorkId`
- `missingCharterPrerequisite`
- `charterIdentityMismatch`
- `missingSpecificationIntent`
- `specificationIdentityMismatch`
- `malformedSpecificationFrontMatter`
- `duplicateSpecificationId`
- `missingSpecificationId`
- `unsafeOverwrite`
- `staleGeneratedView`
- `malformedGeneratedView`
- `blockedGeneratedViewRefresh`
- optional Governance boundary issue diagnostics when applicable

Diagnostic entries must include artifact, severity, message, correction, and
related ids when available.

## Next Action

Successful specify reports set:

```json
{
  "actionId": "nextLifecycleCommand",
  "command": "clarify",
  "workId": "<selected-work-id>",
  "reason": "Command 'specify' completed.",
  "requiredArtifacts": [
    "work/<selected-work-id>/charter.md",
    "work/<selected-work-id>/spec.md"
  ],
  "blockingDiagnosticIds": []
}
```

Blocked reports set `actionId` to `correctBlockingDiagnostics`, omit a next
lifecycle command, and include the blocking diagnostic ids.

If the specification is syntactically valid but contains unresolved ambiguity
records, the result may still point to `clarify`; the report must name the
unresolved ambiguity count so users understand why clarification is next.

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
